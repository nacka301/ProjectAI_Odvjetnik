using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Lightweight Supabase REST client for persisting all module results.
/// Uses the PostgREST API directly — no heavy SDK needed.
/// Tables: evaluations, contract_analyses, chronologies, legal_strategies
/// </summary>
public class SupabaseService
:ISupabaseService {
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly string? _supabaseUrl;
    private readonly ILogger<SupabaseService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SupabaseService(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        ILogger<SupabaseService> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _logger = logger;

        _supabaseUrl = supabase.Url;
        _isConfigured = supabase.IsConfigured;

        if (!_isConfigured)
        {
            _logger.LogWarning("Supabase is not configured. Data saving is disabled. " +
                "Set SUPABASE_URL, SUPABASE_ANON_KEY and SUPABASE_SERVICE_KEY to enable.");
        }
    }

    // ── Generic insert into any table ──

    /// <param name="asService">
    /// True only for append-only audit writes. Migration 016 gives `audit_logs`
    /// a SELECT policy for the firm owner and no INSERT policy at all — the log
    /// must not be writable (or forgeable) by the account it describes, so those
    /// rows go in with the service key. Everything else goes in as the user, so
    /// RLS rejects a row aimed at another firm.
    /// </param>
    private async Task<string?> InsertAsync(string table, object record, bool asService = false)
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

            if (asService) _supabase.ApplyServiceAuth(request);
            else           _supabase.ApplyUserAuth(request);

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
        => InsertAsync("audit_logs", record, asService: true);

    /// <summary>Save multiple audit log entries in a single batch POST to PostgREST.</summary>
    public async Task SaveAuditLogBatchAsync(IEnumerable<AuditLogRecord> records)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("Supabase not configured — skipping batch save to audit_logs.");
            return;
        }

        var list = records.ToList();
        if (list.Count == 0) return;

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/audit_logs";
            var payload = JsonSerializer.Serialize(list, JsonOpts);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            // Same reasoning as InsertAsync(asService: true) — audit_logs has no
            // INSERT policy, deliberately.
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Batch save to audit_logs failed ({Status}): {Body}",
                    response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Batch-saved {Count} audit log entries.", list.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch-saving to audit_logs.");
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

            // User-scoped on purpose. With the service key this method returned any
            // evaluation by id, and POST /api/audit/single never checked ownership —
            // so one firm could audit, and read back, another firm's valuation.
            // Under RLS an id from outside the caller's firm simply yields no row.
            _supabase.ApplyUserAuth(request);

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
    ///
    /// Scoped to the caller's firm by RLS. It used to run on the service key with
    /// no filter, so `POST /api/audit/batch` with an empty id list would pull the
    /// newest evaluations across every firm on the platform and audit them on the
    /// caller's behalf. That endpoint is not admin-guarded, so any signed-in user
    /// could do it.
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
            _supabase.ApplyUserAuth(request);

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
            _supabase.ApplyUserAuth(auditReq);

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
