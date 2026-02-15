using System.Text;
using System.Text.Json;

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
