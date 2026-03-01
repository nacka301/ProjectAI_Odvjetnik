using System.Text;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Persists HTTP-level audit entries to the Supabase api_audit_logs table
/// via PostgREST. Fire-and-forget — never throws, never blocks the API response.
///
/// GDPR / attorney-client privilege:
///   • Zero payload logging — no request body, no response body, no headers.
///   • Only metadata: userId, tenantId, endpoint, HTTP method, status code, duration.
///   • Raw IP is never stored — only SHA-256 hash.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<AuditLogService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AuditLogService(HttpClient httpClient, IConfiguration config, ILogger<AuditLogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl)
                     && !string.IsNullOrWhiteSpace(_serviceKey);

        if (!_isConfigured)
            logger.LogWarning("AuditLogService: Supabase not configured — audit logging disabled.");
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string? userId,
        string? tenantId,
        string actionName,
        string endpoint,
        string httpMethod,
        int statusCode,
        int? durationMs = null,
        string? ipHash = null)
    {
        if (!_isConfigured) return;

        try
        {
            var record = new AuditLogRecord
            {
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                ActionName = actionName,
                Endpoint = endpoint,
                HttpMethod = httpMethod.ToUpperInvariant(),
                StatusCode = statusCode,
                DurationMs = durationMs,
                IpHash = ipHash
            };

            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/api_audit_logs";
            var payload = JsonSerializer.Serialize(record, JsonOpts);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
            request.Headers.Add("Prefer", "return=minimal"); // no response body needed

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("AuditLog insert failed ({Status}): {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let audit logging crash the API
            _logger.LogError(ex, "AuditLog insert error for {Endpoint}", endpoint);
        }
    }

    // ── Internal DTO — maps 1:1 to api_audit_logs table columns ──
    private sealed class AuditLogRecord
    {
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string ActionName { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string HttpMethod { get; set; } = "GET";
        public int StatusCode { get; set; } = 200;
        public int? DurationMs { get; set; }
        public string? IpHash { get; set; }
    }
}
