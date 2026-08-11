namespace Backend.Services;

/// <summary>
/// Supplies the credentials a PostgREST call should travel with, and makes the
/// choice between them explicit at every call site.
///
/// Two modes, deliberately named so the difference cannot be overlooked:
///
///   • <see cref="ApplyUserAuth"/>   — anon key + the caller's access token.
///     Postgres sees the real <c>auth.uid()</c>, so Row Level Security decides
///     what the query returns. This is the default for anything that touches a
///     firm's own data.
///
///   • <see cref="ApplyServiceAuth"/> — the service_role key, which BYPASSES RLS
///     entirely. Legitimate for a short list of jobs: resolving identity before
///     an RLS context exists, MFA secrets the user must never read, append-only
///     audit writes, provisioning rows no user policy allows (a new firm, a
///     default subscription), and cross-tenant admin screens. Every use of it
///     should carry a comment saying which of those it is.
///
/// <see cref="ApplyUserAuth"/> throws when there is no token rather than quietly
/// falling back to the service key. A silent fallback would turn one missing
/// header into a full RLS bypass, which is the exact failure this class exists
/// to prevent.
/// </summary>
public interface ISupabaseRequestContext
{
    /// <summary>Supabase project URL, without a trailing slash. Null when unconfigured.</summary>
    string? Url { get; }

    /// <summary>True when both the URL and the keys needed for user-scoped calls are present.</summary>
    bool IsConfigured { get; }

    /// <summary>True when the current request carried a verified Bearer token.</summary>
    bool HasUserToken { get; }

    /// <summary>Adds anon key + the caller's token. Throws if there is no token.</summary>
    void ApplyUserAuth(HttpRequestMessage request);

    /// <summary>Adds the service_role key. Bypasses RLS — use only where documented.</summary>
    void ApplyServiceAuth(HttpRequestMessage request);
}

public sealed class SupabaseRequestContext : ISupabaseRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string? _anonKey;
    private readonly string? _serviceKey;

    public SupabaseRequestContext(IHttpContextAccessor httpContextAccessor, IConfiguration config)
    {
        _httpContextAccessor = httpContextAccessor;

        Url = (Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"])?.TrimEnd('/');

        _anonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
            ?? config["Supabase:AnonKey"];

        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];
    }

    public string? Url { get; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(_anonKey);

    public bool HasUserToken => !string.IsNullOrWhiteSpace(AccessToken);

    private string? AccessToken =>
        _httpContextAccessor.HttpContext?.Items["AuthAccessToken"] as string;

    public void ApplyUserAuth(HttpRequestMessage request)
    {
        var token = AccessToken;

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "Pokušaj upita nad korisnickim podacima bez korisnikovog tokena. " +
                "Ovaj poziv mora dolaziti iz zahtjeva koji je prosao SupabaseJwtMiddleware, " +
                "ili mora eksplicitno koristiti ApplyServiceAuth uz obrazlozenje.");

        if (string.IsNullOrWhiteSpace(_anonKey))
            throw new InvalidOperationException(
                "SUPABASE_ANON_KEY nije postavljen. PostgREST zahtijeva apikey zaglavlje " +
                "uz korisnikov token.");

        request.Headers.Remove("apikey");
        request.Headers.Add("apikey", _anonKey);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void ApplyServiceAuth(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_serviceKey))
            throw new InvalidOperationException("SUPABASE_SERVICE_KEY nije postavljen.");

        request.Headers.Remove("apikey");
        request.Headers.Add("apikey", _serviceKey);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _serviceKey);
    }
}
