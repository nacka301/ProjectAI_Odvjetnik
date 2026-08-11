using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Services;

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
/// Security model — FAIL CLOSED:
///   SUPABASE_JWT_SECRET is mandatory; without it the host refuses to start.
///   Every request outside <see cref="PublicPaths"/> must carry a valid
///   "Authorization: Bearer {access_token}" header, otherwise it is rejected
///   with 401 before it ever reaches a controller.
///
///   There is deliberately NO x-user-id fallback and NO unverified-JWT path.
///   Both existed as dev conveniences and both turned into full authentication
///   bypasses the moment the secret was missing in a deployed environment.
/// </summary>
public sealed class SupabaseJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[] _jwtSecret;

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

    public SupabaseJwtMiddleware(RequestDelegate next, IConfiguration config, ILogger<SupabaseJwtMiddleware> logger)
    {
        _next = next;

        var secret = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET")
            ?? config["Supabase:JwtSecret"];

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(
                "SUPABASE_JWT_SECRET nije postavljen. Bez njega se tokeni ne mogu verificirati " +
                "i backend bi prihvaćao neautentificirane zahtjeve. Postavite varijablu okoline " +
                "SUPABASE_JWT_SECRET (Supabase → Settings → API → JWT Secret).");

        _jwtSecret = Encoding.UTF8.GetBytes(secret);
        logger.LogInformation("Supabase JWT verification ENABLED — all requests must include valid Bearer token");
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
        var claims = VerifyJwtAndExtractClaims(token);
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

    private JwtClaims? VerifyJwtAndExtractClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            // Verify HS256 signature
            var signatureInput = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_jwtSecret);
            var computedSig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureInput)));

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSig),
                Encoding.UTF8.GetBytes(parts[2])))
                return null;

            return ExtractClaimsFromPayload(parts[1]);
        }
        catch
        {
            return null;
        }
    }

    private static JwtClaims? ExtractClaimsFromPayload(string base64Payload)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(base64Payload));
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

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
