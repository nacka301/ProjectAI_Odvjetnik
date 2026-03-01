using System.Collections.Concurrent;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Resolves user_id → law_firm_id (tenant_id) by querying user_profiles via PostgREST.
/// Results are cached in a thread-safe ConcurrentDictionary for 5 minutes to avoid
/// per-request DB calls in the middleware hot path.
/// </summary>
public interface ITenantResolver
{
    Task<string?> GetTenantIdForUserAsync(string userId);
    void InvalidateCache(string userId);
}

public sealed class TenantResolver : ITenantResolver
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<TenantResolver> _logger;
    private readonly bool _isConfigured;

    // Thread-safe cache: userId → (tenantId, expiry)
    private static readonly ConcurrentDictionary<string, (string? TenantId, DateTime Expiry)> Cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public TenantResolver(HttpClient httpClient, IConfiguration config, ILogger<TenantResolver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl)
                     && !string.IsNullOrWhiteSpace(_serviceKey);
    }

    public async Task<string?> GetTenantIdForUserAsync(string userId)
    {
        if (!_isConfigured || string.IsNullOrWhiteSpace(userId))
            return null;

        // Check cache first
        if (Cache.TryGetValue(userId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.TenantId;

        try
        {
            var safeId = Uri.EscapeDataString(userId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles?user_id=eq.{safeId}&select=law_firm_id&limit=1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var arr = doc.RootElement;

            string? tenantId = null;
            if (arr.GetArrayLength() > 0)
            {
                var row = arr[0];
                if (row.TryGetProperty("law_firm_id", out var lfProp) && lfProp.ValueKind == JsonValueKind.String)
                    tenantId = lfProp.GetString();
            }

            Cache[userId] = (tenantId, DateTime.UtcNow.Add(CacheDuration));
            return tenantId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TenantResolver: failed to resolve tenant for user {UserId}", userId);
            return null;
        }
    }

    public void InvalidateCache(string userId)
    {
        Cache.TryRemove(userId, out _);
    }
}
