using System.Security.Cryptography;
using System.Text.Json;
using Backend.Utils;

namespace Backend.Services;

/// <summary>
/// Supplies the public signing keys Supabase publishes at
/// <c>{SUPABASE_URL}/auth/v1/.well-known/jwks.json</c>.
///
/// Supabase moved from a single shared HS256 secret to asymmetric signing keys
/// (ECC P-256 / ES256). Access tokens are now signed with a private key that
/// never leaves Supabase, so the backend can no longer verify them with a secret
/// it holds — it has to fetch the matching public key by <c>kid</c>.
/// </summary>
public interface ISupabaseJwksProvider
{
    /// <summary>False when SUPABASE_URL is absent, i.e. ES256 cannot be verified at all.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// The P-256 public key for <paramref name="kid"/>, or null if it is unknown
    /// or the key set could not be fetched. Null always means "reject the token" —
    /// never "accept it unverified".
    /// </summary>
    Task<ECParameters?> GetEcKeyAsync(string kid, CancellationToken ct = default);
}

public sealed class SupabaseJwksProvider : ISupabaseJwksProvider
{
    /// <summary>Named client so the fetch gets its own short timeout.</summary>
    public const string HttpClientName = "supabase-jwks";

    /// <summary>After this the cached set is refetched even on a cache hit, so a revoked key stops working.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Floor between two fetches. Without it an unknown <c>kid</c> — which is
    /// exactly what a forged token carries — would let anyone drive traffic at
    /// Supabase, one request per attempt.
    /// </summary>
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SupabaseJwksProvider> _logger;
    private readonly string? _jwksUrl;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Replaced wholesale on refresh rather than mutated, so readers never observe
    // a half-built set and need no lock.
    private volatile Dictionary<string, ECParameters> _keys = new();
    private DateTimeOffset _lastSuccess = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;

    public SupabaseJwksProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<SupabaseJwksProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];

        _jwksUrl = string.IsNullOrWhiteSpace(supabaseUrl)
            ? null
            : $"{supabaseUrl.TrimEnd('/')}/auth/v1/.well-known/jwks.json";
    }

    public bool IsConfigured => _jwksUrl != null;

    public async Task<ECParameters?> GetEcKeyAsync(string kid, CancellationToken ct = default)
    {
        if (_jwksUrl == null) return null;

        var stale = DateTimeOffset.UtcNow - _lastSuccess > CacheTtl;
        if (!stale && _keys.TryGetValue(kid, out var cached))
            return cached;

        // Either the set aged out or this kid is new — a rotation looks exactly
        // like the latter, so a miss is a legitimate reason to refetch.
        await RefreshAsync(ct);

        return _keys.TryGetValue(kid, out var fetched) ? fetched : null;
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _lastAttempt < MinRefreshInterval) return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Re-check inside the lock: while we queued, another request may have
            // already refreshed, which would make this fetch pure waste.
            if (DateTimeOffset.UtcNow - _lastAttempt < MinRefreshInterval) return;
            _lastAttempt = DateTimeOffset.UtcNow;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(_jwksUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "JWKS fetch failed with {Status} — ES256 tokens will be rejected until this recovers.",
                    (int)response.StatusCode);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = ParseJwks(body);

            if (parsed.Count == 0)
            {
                // Keep the previous set: an empty answer is far more likely to be a
                // malformed response than a genuine "this project has no keys".
                _logger.LogError("JWKS returned no usable P-256 keys — keeping the previously cached set.");
                return;
            }

            _keys = parsed;
            _lastSuccess = DateTimeOffset.UtcNow;
            _logger.LogInformation("JWKS refreshed — {Count} signing key(s) cached.", parsed.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWKS fetch threw — ES256 tokens will be rejected until this recovers.");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Keeps only what can actually verify an ES256 signature: EC keys on P-256
    /// marked for signature use. Anything else is silently skipped rather than
    /// coerced into a key.
    /// </summary>
    private static Dictionary<string, ECParameters> ParseJwks(string json)
    {
        var keys = new Dictionary<string, ECParameters>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("keys", out var keysProp)
            || keysProp.ValueKind != JsonValueKind.Array)
            return keys;

        foreach (var key in keysProp.EnumerateArray())
        {
            var kid = ReadString(key, "kid");
            if (string.IsNullOrWhiteSpace(kid)) continue;

            if (ReadString(key, "kty") != "EC") continue;
            if (ReadString(key, "crv") != "P-256") continue;

            var use = ReadString(key, "use");
            if (use != null && use != "sig") continue;

            var x = ReadString(key, "x");
            var y = ReadString(key, "y");
            if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y)) continue;

            try
            {
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = Base64Url.Decode(x),
                        Y = Base64Url.Decode(y),
                    },
                };
                parameters.Validate();
                keys[kid] = parameters;
            }
            catch
            {
                // A key we cannot construct is a key we cannot verify with.
            }
        }

        return keys;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
