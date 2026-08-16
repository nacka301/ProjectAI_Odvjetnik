using System.Security.Cryptography;
using System.Text.Json;
using Backend.Utils;

namespace Backend.Services;

/// <summary>
/// Why a key lookup did not produce a key. The two failures are not the same
/// thing and must not produce the same HTTP status:
///
///   UnknownKid  — we hold a key set and this <c>kid</c> is not in it. The token
///                 names a key this project does not publish, which is a claim
///                 about the token. Reject it.
///   Unavailable — we hold no key set at all. We cannot judge any token, valid or
///                 forged. That is an outage on our side, not a verdict on the
///                 caller.
///
/// Collapsing these into a bare null is what made a backend that could not reach
/// Supabase indistinguishable, from the outside, from a backend looking at a
/// forged token — both answered 401, and the frontend gate showed the user
/// "your sign-in was refused" for what was our own network failure.
/// </summary>
public enum JwksLookupStatus
{
    Found,
    UnknownKid,
    Unavailable,
}

public readonly record struct JwksLookup(JwksLookupStatus Status, ECParameters? Key)
{
    public static JwksLookup Found(ECParameters key) => new(JwksLookupStatus.Found, key);
    public static JwksLookup UnknownKid() => new(JwksLookupStatus.UnknownKid, null);
    public static JwksLookup Unavailable() => new(JwksLookupStatus.Unavailable, null);
}

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
    /// The P-256 public key for <paramref name="kid"/>, or the reason there is
    /// none. A lookup never returns a key it did not verify came from the
    /// configured project.
    /// </summary>
    Task<JwksLookup> LookupAsync(string kid, CancellationToken ct = default);

    /// <summary>
    /// Loads the key set if it is not loaded yet; true when keys are available
    /// afterwards. Called at startup so the first user to sign in does not pay
    /// for the fetch — measured at 3.6 s on a freshly booted Render container,
    /// against a 5 s fetch timeout.
    /// </summary>
    Task<bool> EnsureLoadedAsync(CancellationToken ct = default);
}

public sealed class SupabaseJwksProvider : ISupabaseJwksProvider
{
    /// <summary>Named client so the fetch gets its own short timeout.</summary>
    public const string HttpClientName = "supabase-jwks";

    /// <summary>After this the cached set is refetched even on a cache hit, so a revoked key stops working.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Floor between two fetches once a usable key set exists. Without it an
    /// unknown <c>kid</c> — which is exactly what a forged token carries — would
    /// let anyone drive traffic at Supabase, one request per attempt.
    /// </summary>
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// While no key set has ever loaded, that same floor is indefensible: the
    /// backend can verify nothing, so one lost race would become a full minute in
    /// which every token is refused. Retry quickly instead, escalating so a real
    /// Supabase outage still cannot turn into a fetch loop.
    /// </summary>
    private static readonly TimeSpan[] ColdRetryBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SupabaseJwksProvider> _logger;
    private readonly string? _jwksUrl;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Replaced wholesale on refresh rather than mutated, so readers never observe
    // a half-built set and need no lock.
    private volatile Dictionary<string, ECParameters> _keys = new();
    private DateTimeOffset _lastSuccess = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;
    private int _consecutiveFailures;

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

    public async Task<bool> EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_jwksUrl == null) return false;
        if (_keys.Count > 0) return true;

        await RefreshAsync(ct);
        return _keys.Count > 0;
    }

    public async Task<JwksLookup> LookupAsync(string kid, CancellationToken ct = default)
    {
        // No URL means no way to verify any ES256 token — an outage by
        // configuration, not a bad token.
        if (_jwksUrl == null) return JwksLookup.Unavailable();

        var keys = _keys;
        var stale = DateTimeOffset.UtcNow - _lastSuccess > CacheTtl;
        if (!stale && keys.TryGetValue(kid, out var cached))
            return JwksLookup.Found(cached);

        // Either the set aged out or this kid is new — a rotation looks exactly
        // like the latter, so a miss is a legitimate reason to refetch.
        await RefreshAsync(ct);

        keys = _keys;
        if (keys.TryGetValue(kid, out var fetched))
            return JwksLookup.Found(fetched);

        // A stale-but-populated set still answers this question: we know which
        // keys the project publishes, and this is not one of them.
        return keys.Count > 0 ? JwksLookup.UnknownKid() : JwksLookup.Unavailable();
    }

    /// <summary>
    /// How long to wait before another fetch. Short while we are blind, long once
    /// we can answer from cache — the floor exists to stop forged-kid traffic,
    /// and that concern only applies when refusing costs the caller nothing.
    /// </summary>
    private TimeSpan RefreshFloor()
    {
        if (_keys.Count > 0) return MinRefreshInterval;

        // One failure means "wait ColdRetryBackoff[0] before trying again", so the
        // first retry is the fastest one rather than one rung up the ladder.
        var rung = Math.Clamp(_consecutiveFailures - 1, 0, ColdRetryBackoff.Length - 1);
        return ColdRetryBackoff[rung];
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _lastAttempt < RefreshFloor()) return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Re-check inside the lock: while we queued, another request may have
            // already refreshed, which would make this fetch pure waste.
            if (DateTimeOffset.UtcNow - _lastAttempt < RefreshFloor()) return;
            _lastAttempt = DateTimeOffset.UtcNow;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(_jwksUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _consecutiveFailures++;
                _logger.LogError(
                    "JWKS fetch failed with {Status} — ES256 tokens cannot be verified until this recovers. Keys held: {Held}, consecutive failures: {Failures}.",
                    (int)response.StatusCode, _keys.Count, _consecutiveFailures);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = ParseJwks(body);

            if (parsed.Count == 0)
            {
                // Keep the previous set: an empty answer is far more likely to be a
                // malformed response than a genuine "this project has no keys".
                _consecutiveFailures++;
                _logger.LogError(
                    "JWKS returned no usable P-256 keys — keeping the previously cached set. Keys held: {Held}, consecutive failures: {Failures}.",
                    _keys.Count, _consecutiveFailures);
                return;
            }

            _keys = parsed;
            _lastSuccess = DateTimeOffset.UtcNow;
            _consecutiveFailures = 0;
            _logger.LogInformation("JWKS refreshed — {Count} signing key(s) cached: {Kids}.",
                parsed.Count, string.Join(", ", parsed.Keys));
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _logger.LogError(ex,
                "JWKS fetch threw — ES256 tokens cannot be verified until this recovers. Keys held: {Held}, consecutive failures: {Failures}.",
                _keys.Count, _consecutiveFailures);
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
