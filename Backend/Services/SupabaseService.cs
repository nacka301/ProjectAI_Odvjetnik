using System.Text;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Lightweight Supabase REST client for saving evaluations from the backend.
/// Uses the PostgREST API directly — no heavy SDK needed.
/// </summary>
public class SupabaseService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<SupabaseService> _logger;
    private readonly bool _isConfigured;

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
            _logger.LogWarning("Supabase is not configured. Evaluation saving is disabled. " +
                "Set SUPABASE_URL and SUPABASE_SERVICE_KEY environment variables to enable.");
        }
    }

    /// <summary>
    /// Save an evaluation result to the Supabase 'evaluations' table.
    /// Returns the UUID of the created row, or null if Supabase is not configured.
    /// </summary>
    public async Task<string?> SaveEvaluationAsync(EvaluationRecord record)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("Supabase not configured — skipping save.");
            return null;
        }

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/evaluations";

            var payload = JsonSerializer.Serialize(record, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

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
                _logger.LogError("Supabase save failed ({Status}): {Body}",
                    response.StatusCode, responseBody);
                return null;
            }

            // Parse the returned row to get the UUID
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                doc.RootElement.GetArrayLength() > 0)
            {
                var firstRow = doc.RootElement[0];
                if (firstRow.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    _logger.LogInformation("Evaluation saved to Supabase: {Id}", id);
                    return id;
                }
            }

            _logger.LogInformation("Evaluation saved to Supabase (no ID returned).");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving evaluation to Supabase.");
            return null;
        }
    }
}

/// <summary>
/// Record to insert into the Supabase 'evaluations' table.
/// Matches the schema.sql columns.
/// </summary>
public class EvaluationRecord
{
    public string PropertyType { get; set; } = "";
    public string Location { get; set; } = "";
    public double? SizeM2 { get; set; }
    public int? EstimatedValue { get; set; }
    public string? PdfUrl { get; set; }
    public object? RawJsonOutput { get; set; }
}
