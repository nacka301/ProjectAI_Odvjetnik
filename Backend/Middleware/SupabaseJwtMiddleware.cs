using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Services;
using Backend.Utils;

namespace Backend.Middleware;

/// <summary>
/// Verifies Supabase JWT access tokens on every API request.
/// Extracts and stores in HttpContext.Items:
///   • "AuthUserId"      — sub claim (UUID)
///   • "AuthEmail"       — email claim (string)
///   • "AuthAccessToken" — the raw Bearer token, so data services can forward it
///                         to PostgREST and let RLS evaluate auth.uid()
///   • "AuthTenantId"    — resolved via ITenantResolver (law_firm_id)
///
/// Supports both signature algorithms Supabase issues:
///   • HS256 — the legacy shared secret (SUPABASE_JWT_SECRET)
///   • ES256 — asymmetric signing keys, verified against the project's published
///             JWKS. Supabase migrated projects to these, and a HS256-only
///             verifier rejects every one of those tokens with 401.
/// Both are kept so a key rotation does not require a synchronised deploy.
///
/// Security model — FAIL CLOSED:
///   At least one verification method must be configured; with neither the host
///   refuses to start. Every request outside <see cref="PublicPaths"/> must carry
///   a valid "Authorization: Bearer {access_token}" header, otherwise it is
///   rejected with 401 before it ever reaches a controller.
///
///   The algorithm is matched strictly against the key type — HS256 only against
///   the shared secret, ES256 only against a JWKS key. Anything else, "none"
///   included, is rejected outright; accepting the token's own claim about how to
///   verify it is the classic alg-confusion bypass.
///
///   There is deliberately NO x-user-id fallback and NO unverified-JWT path.
///   Both existed as dev conveniences and both turned into full authentication
///   bypasses the moment the secret was missing in a deployed environment.
/// </summary>
public sealed class SupabaseJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[]? _jwtSecret;
    private readonly ISupabaseJwksProvider _jwks;

    /// <summary>
    /// The only endpoints reachable without a token. Anything not listed here
    /// requires a verified JWT — new controllers are protected by default.
    /// </summary>
    private static readonly string[] PublicPaths =
    [
        "/api/health",
        "/api/auth/register",
        "/api/auth/verify",
        "/swagger",
    ];

    public SupabaseJwtMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ISupabaseJwksProvider jwks,
        ILogger<SupabaseJwtMiddleware> logger)
    {
        _next = next;
        _jwks = jwks;

        var secret = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET")
            ?? config["Supabase:JwtSecret"];

        _jwtSecret = string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(secret);

        // The secret alone is no longer required — a project on asymmetric signing
        // keys has no use for it. What must hold is that *some* way to verify a
        // signature exists, otherwise every request would be unauthenticated.
        if (_jwtSecret == null && !jwks.IsConfigured)
            throw new InvalidOperationException(
                "Nema nacina za verifikaciju tokena: ni SUPABASE_JWT_SECRET ni SUPABASE_URL nisu " +
                "postavljeni. Bez toga bi backend prihvacao neautentificirane zahtjeve. Postavite " +
                "SUPABASE_URL (za ES256 preko JWKS-a) ili SUPABASE_JWT_SECRET (legacy HS256).");

        logger.LogInformation(
            "Supabase JWT verification ENABLED — HS256: {Hs256}, ES256 via JWKS: {Es256}",
            _jwtSecret != null, jwks.IsConfigured);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // CORS preflight never carries an Authorization header.
        if (context.Request.Method == "OPTIONS" || IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader)
            || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, "Nedostaje pristupni token.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var claims = await VerifyJwtAndExtractClaimsAsync(token, context.RequestAborted);
        if (claims == null)
        {
            await RejectAsync(context, "Nevazeci ili istekli token.");
            return;
        }

        SetContextItems(context, claims.Value, token);
        await ResolveTenantIdAsync(context, claims.Value.UserId);
        await _next(context);
    }

    private static bool IsPublicPath(string path) =>
        PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"error\":\"{message}\"}}");
    }

    // ── Claims DTO ──

    private readonly record struct JwtClaims(string UserId, string? Email, string? Role);

    private static void SetContextItems(HttpContext context, JwtClaims claims, string accessToken)
    {
        context.Items["AuthUserId"] = claims.UserId;
        context.Items["AuthAccessToken"] = accessToken;
        if (claims.Email != null)
            context.Items["AuthEmail"] = claims.Email;
        if (claims.Role != null)
            context.Items["AuthRole"] = claims.Role;
    }

    // ── Tenant resolution via ITenantResolver (cached) ──

    private static async Task ResolveTenantIdAsync(HttpContext context, string userId)
    {
        try
        {
            var resolver = context.RequestServices.GetService<ITenantResolver>();
            if (resolver != null)
            {
                var tenantId = await resolver.GetTenantIdForUserAsync(userId);
                if (tenantId != null)
                    context.Items["AuthTenantId"] = tenantId;

                var firmRole = await resolver.GetFirmRoleForUserAsync(userId);
                if (firmRole != null)
                    context.Items["AuthFirmRole"] = firmRole;
            }
        }
        catch
        {
            // Non-critical — don't fail the request
        }
    }

    // ── JWT verification + claims extraction ──

    private readonly record struct JwtHeader(string Alg, string? Kid);

    private async Task<JwtClaims?> VerifyJwtAndExtractClaimsAsync(string token, CancellationToken ct)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var header = ParseHeader(parts[0]);
            if (header == null) return null;

            var signingInput = $"{parts[0]}.{parts[1]}";

            // The token names its own algorithm, so each branch is pinned to one
            // key type. An unrecognised alg — "none" above all — falls through to
            // false rather than to a default.
            var signatureValid = header.Value.Alg switch
            {
                "HS256" => VerifyHs256(signingInput, parts[2]),
                "ES256" => await VerifyEs256Async(signingInput, parts[2], header.Value.Kid, ct),
                _ => false,
            };

            if (!signatureValid) return null;

            return ExtractClaimsFromPayload(parts[1]);
        }
        catch
        {
            return null;
        }
    }

    private static JwtHeader? ParseHeader(string base64Header)
    {
        var json = Encoding.UTF8.GetString(Base64Url.Decode(base64Header));
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("alg", out var algProp)) return null;

        var alg = algProp.GetString();
        if (string.IsNullOrWhiteSpace(alg)) return null;

        var kid = doc.RootElement.TryGetProperty("kid", out var kidProp)
            ? kidProp.GetString()
            : null;

        return new JwtHeader(alg, kid);
    }

    private bool VerifyHs256(string signingInput, string providedSignature)
    {
        if (_jwtSecret == null) return false;

        using var hmac = new HMACSHA256(_jwtSecret);
        var computedSig = Base64Url.Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSig),
            Encoding.UTF8.GetBytes(providedSignature));
    }

    private async Task<bool> VerifyEs256Async(
        string signingInput, string providedSignature, string? kid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kid)) return false;

        var signature = Base64Url.Decode(providedSignature);

        // JWS carries ES256 as raw r||s (IEEE P1363), 32 bytes each — which is
        // also the format ECDsa.VerifyData expects by default. A DER-wrapped
        // signature of another length is not a P-256 JWS signature.
        if (signature.Length != 64) return false;

        var key = await _jwks.GetEcKeyAsync(kid, ct);
        if (key == null) return false;

        using var ecdsa = ECDsa.Create(key.Value);
        return ecdsa.VerifyData(
            Encoding.UTF8.GetBytes(signingInput),
            signature,
            HashAlgorithmName.SHA256);
    }

    private static JwtClaims? ExtractClaimsFromPayload(string base64Payload)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64Url.Decode(base64Payload));
        using var doc = JsonDocument.Parse(payloadJson);
        var payload = doc.RootElement;

        // Check expiration
        if (payload.TryGetProperty("exp", out var expProp))
        {
            var exp = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
            if (exp < DateTimeOffset.UtcNow)
                return null;
        }

        // Extract sub (user UUID)
        string? userId = null;
        if (payload.TryGetProperty("sub", out var subProp))
        {
            var sub = subProp.GetString();
            if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out _))
                userId = sub;
        }
        if (userId == null) return null;

        // Extract email
        string? email = null;
        if (payload.TryGetProperty("email", out var emailProp))
            email = emailProp.GetString();

        // Extract role (Supabase puts it in "role" claim)
        string? role = null;
        if (payload.TryGetProperty("role", out var roleProp))
            role = roleProp.GetString();

        return new JwtClaims(userId, email, role);
    }
}
