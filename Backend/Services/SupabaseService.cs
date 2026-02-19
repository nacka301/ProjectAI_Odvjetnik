using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Services;

/// <summary>
/// Lightweight Supabase REST client for persisting all module results.
/// Uses the PostgREST API directly — no heavy SDK needed.
/// Tables: evaluations, contract_analyses, chronologies, legal_strategies
/// </summary>
public class SupabaseService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<SupabaseService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SupabaseService(HttpClient httpClient, IConfiguration config, ILogger<SupabaseService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl) &&
                        !string.IsNullOrWhiteSpace(_serviceKey);

        if (!_isConfigured)
        {
            _logger.LogWarning("Supabase is not configured. Data saving is disabled. " +
                "Set SUPABASE_URL and SUPABASE_SERVICE_KEY environment variables to enable.");
        }
    }

    // ── Generic insert into any table ──

    private async Task<string?> InsertAsync(string table, object record)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("Supabase not configured — skipping save to {Table}.", table);
            return null;
        }

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}";
            var payload = JsonSerializer.Serialize(record, JsonOpts);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
            request.Headers.Add("Prefer", "return=representation");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Supabase save to {Table} failed ({Status}): {Body}",
                    table, response.StatusCode, responseBody);
                return null;
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                doc.RootElement.GetArrayLength() > 0)
            {
                var firstRow = doc.RootElement[0];
                if (firstRow.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    _logger.LogInformation("Saved to {Table}: {Id}", table, id);
                    return id;
                }
            }

            _logger.LogInformation("Saved to {Table} (no ID returned).", table);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving to {Table}.", table);
            return null;
        }
    }

    // ── Public save methods ──

    public Task<string?> SaveEvaluationAsync(EvaluationRecord record)
        => InsertAsync("evaluations", record);

    public Task<string?> SaveContractAnalysisAsync(ContractAnalysisRecord record)
        => InsertAsync("contract_analyses", record);

    public Task<string?> SaveChronologyAsync(ChronologyRecord record)
        => InsertAsync("chronologies", record);

    public Task<string?> SaveLegalStrategyAsync(LegalStrategyRecord record)
        => InsertAsync("legal_strategies", record);

    public Task<string?> SaveInvoiceAsync(InvoiceRecord record)
        => InsertAsync("invoices", record);

    public Task<string?> SaveAuditResultAsync(AuditResultRecord record)
        => InsertAsync("valuation_audits", record);

    public Task<string?> SaveZkAnalysisAsync(ZkAnalysisRecord record)
        => InsertAsync("zk_analyses", record);

    public Task<string?> SaveAuditLogAsync(AuditLogRecord record)
        => InsertAsync("audit_logs", record);

    /// <summary>Save multiple audit log entries (one per AI reasoning step).</summary>
    public async Task SaveAuditLogBatchAsync(IEnumerable<AuditLogRecord> records)
    {
        foreach (var record in records)
        {
            await InsertAsync("audit_logs", record);
        }
    }

    // ── Read methods (for audit layer) ──

    /// <summary>
    /// Fetch a single evaluation by ID so the audit service can read it.
    /// </summary>
    // Regex for UUID validation — prevents PostgREST filter injection
    private static readonly Regex UuidRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    public async Task<EvaluationReadRecord?> GetEvaluationByIdAsync(string evaluationId)
    {
        if (!_isConfigured) return null;

        // Validate UUID format to prevent PostgREST filter injection
        if (string.IsNullOrWhiteSpace(evaluationId) || !UuidRegex.IsMatch(evaluationId))
        {
            _logger.LogWarning("Invalid evaluation ID format: {Id}", evaluationId);
            return null;
        }

        try
        {
            var safeId = Uri.EscapeDataString(evaluationId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/evaluations?id=eq.{safeId}&select=*&limit=1";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch evaluation {Id}: {Body}", evaluationId, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<EvaluationReadRecord>>(body, JsonOpts);

            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching evaluation {Id}", evaluationId);
            return null;
        }
    }

    /// <summary>
    /// Fetch recent evaluations that have NOT been audited yet.
    /// </summary>
    public async Task<List<EvaluationReadRecord>> GetUnauditedEvaluationsAsync(int limit = 20)
    {
        if (!_isConfigured) return [];
        limit = Math.Clamp(limit, 1, 100);

        try
        {
            // Fetch recent evaluations
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/evaluations?select=*&order=created_at.desc&limit={limit}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch evaluations: {Body}", body);
                return [];
            }

            var evals = JsonSerializer.Deserialize<List<EvaluationReadRecord>>(body, JsonOpts) ?? [];

            if (evals.Count == 0) return [];

            // Fetch existing audit evaluation IDs (validate UUIDs from deserialized data)
            var validIds = evals.Where(e => UuidRegex.IsMatch(e.Id)).Select(e => $"\"{e.Id}\"");
            var evalIds = string.Join(",", validIds);
            if (string.IsNullOrEmpty(evalIds)) return evals;
            var auditUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/valuation_audits?select=evaluation_id&evaluation_id=in.({evalIds})";
            var auditReq = new HttpRequestMessage(HttpMethod.Get, auditUrl);
            auditReq.Headers.Add("apikey", _serviceKey);
            auditReq.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var auditResp = await _httpClient.SendAsync(auditReq);
            var auditBody = await auditResp.Content.ReadAsStringAsync();

            var auditedIds = new HashSet<string>();
            if (auditResp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(auditBody);
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    if (row.TryGetProperty("evaluation_id", out var eid))
                        auditedIds.Add(eid.GetString() ?? "");
                }
            }

            return evals.Where(e => !auditedIds.Contains(e.Id)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unaudited evaluations");
            return [];
        }
    }
}

// ═══════════════════════════════════════════════════════════
//  Record DTOs — match Supabase table columns
// ═══════════════════════════════════════════════════════════

public class EvaluationRecord
{
    public string PropertyType { get; set; } = "";
    public string Location { get; set; } = "";
    public double? SizeM2 { get; set; }
    public int? EstimatedValue { get; set; }
    public object? RawJsonOutput { get; set; }
}

public class ContractAnalysisRecord
{
    public string DocumentType { get; set; } = "";
    public string Summary { get; set; } = "";
    public string OverallRisk { get; set; } = "";
    public int RiskScore { get; set; }
    public int RedFlagsCount { get; set; }
    public object? RawJsonOutput { get; set; }
}

public class ChronologyRecord
{
    public int TotalDocuments { get; set; }
    public int TotalEvents { get; set; }
    public string DateRange { get; set; } = "";
    public object? RawJsonOutput { get; set; }
}

public class LegalStrategyRecord
{
    public string KeyLegalIssue { get; set; } = "";
    public string ClientPosition { get; set; } = "kupac";
    public int WinProbability { get; set; }
    public int WinProbabilityMin { get; set; }
    public int WinProbabilityMax { get; set; }
    public int PrecedentsCount { get; set; }
    public object? RawJsonOutput { get; set; }
}

public class InvoiceRecord
{
    public string InvoiceNumber { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string IssuerName { get; set; } = "";
    public string IssuerOib { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientOib { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal PdvRate { get; set; }
    public decimal PdvAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Module { get; set; } = "";
    public string? AnalysisId { get; set; }
    public string? CaseReference { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string UblXml { get; set; } = "";
    public object? RawJsonOutput { get; set; }
}

// ═══════════════════════════════════════════════════════════
//  Audit Records
// ═══════════════════════════════════════════════════════════

/// <summary>Record written to valuation_audits table.</summary>
public class AuditResultRecord
{
    public string EvaluationId { get; set; } = "";
    public string Verdict { get; set; } = "";
    public int OverallConfidence { get; set; }
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public int PriceScore { get; set; }
    public int LocationScore { get; set; }
    public int MethodologyScore { get; set; }
    public int DataQualityScore { get; set; }
    public int FindingsCount { get; set; }
    public int CriticalCount { get; set; }
    public string ModelUsed { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public object? RawJsonOutput { get; set; }
}

/// <summary>Record written to zk_analyses table.</summary>
public class ZkAnalysisRecord
{
    public string PropertyId { get; set; } = "";
    public string? CadastralMunicipality { get; set; }
    public string? ParcelNumber { get; set; }
    public string? PropertyType { get; set; }
    public string Location { get; set; } = "";
    public double? LandAreaM2 { get; set; }
    public double? BuildingAreaM2 { get; set; }
    public string RiskScore { get; set; } = "";
    public object? Owners { get; set; }
    public object? Mortgages { get; set; }
    public object? Disputes { get; set; }
    public object? Encumbrances { get; set; }
    public string? LawyerComment { get; set; }
    public string? EvaluationId { get; set; }
    public object? RawJsonOutput { get; set; }
}

/// <summary>Record written to audit_logs table (EU AI Act Article 13 compliance).</summary>
public class AuditLogRecord
{
    public string Module { get; set; } = "";
    public string? AnalysisId { get; set; }
    public int Step { get; set; } = 1;
    public string Action { get; set; } = "";
    public string LegalBasis { get; set; } = "";
    public string Citation { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string Confidence { get; set; } = "SREDNJE";
    public string ModelUsed { get; set; } = "anthropic/claude-sonnet-4.5";
    public double ModelTemperature { get; set; } = 0.10;
}

/// <summary>Record read back from evaluations table (includes id + created_at).</summary>
public class EvaluationReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string Location { get; set; } = "";
    public double? SizeM2 { get; set; }
    public double? EstimatedValue { get; set; }
    public object? RawJsonOutput { get; set; }
}
