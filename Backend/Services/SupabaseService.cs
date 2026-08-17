using System.Text;
using System.Text.Json;
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
}
