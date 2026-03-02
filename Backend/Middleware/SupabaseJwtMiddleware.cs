using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Services;

namespace Backend.Middleware;

/// <summary>
/// Verifies Supabase JWT access tokens on every API request.
/// Extracts and stores in HttpContext.Items:
///   • "AuthUserId"   — sub claim (UUID)
///   • "AuthEmail"    — email claim (string)
///   • "AuthTenantId" — resolved via ITenantResolver (law_firm_id)
///
/// Security model:
///   - Production: SUPABASE_JWT_SECRET must be set. Every request must include
///     a valid "Authorization: Bearer {access_token}" header.
///   - Development: If SUPABASE_JWT_SECRET is not set, falls back to x-user-id
///     header (trusted, for local dev only). Logs a warning on startup.
/// </summary>
public sealed class SupabaseJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[]? _jwtSecret;
    private readonly bool _enforceJwt;

    public SupabaseJwtMiddleware(RequestDelegate next, IConfiguration config, ILogger<SupabaseJwtMiddleware> logger)
    {
        _next = next;

        var secret = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET")
            ?? config["Supabase:JwtSecret"];

        if (!string.IsNullOrWhiteSpace(secret))
        {
            _jwtSecret = Encoding.UTF8.GetBytes(secret);
            _enforceJwt = true;
            logger.LogInformation("Supabase JWT verification ENABLED — all requests must include valid Bearer token");
        }
        else
        {
            _jwtSecret = null;
            _enforceJwt = false;
            logger.LogWarning("SUPABASE_JWT_SECRET not configured — falling back to x-user-id header (DEVELOPMENT ONLY)");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for: health check, CORS preflight, Swagger
        if (path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Method == "OPTIONS"
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Try JWT verification first
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();

            if (_jwtSecret != null)
            {
                var claims = VerifyJwtAndExtractClaims(token);
                if (claims != null)
                {
                    SetContextItems(context, claims.Value);
                    await ResolveTenantIdAsync(context, claims.Value.UserId);
                    await _next(context);
                    return;
                }

                // JWT present but invalid
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Nevazeci ili istekli token.\"}");
                return;
            }
            else
            {
                // Dev mode — extract claims from JWT payload without verification
                var claims = ExtractClaimsWithoutVerification(token);
                if (claims != null)
                {
                    SetContextItems(context, claims.Value);
                    await ResolveTenantIdAsync(context, claims.Value.UserId);
                    await _next(context);
                    return;
                }
            }
        }

        // Fallback: x-user-id header (only in dev mode)
        if (!_enforceJwt)
        {
            if (context.Request.Headers.TryGetValue("x-user-id", out var userIdHeader))
            {
                var raw = userIdHeader.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(raw) && raw.Length <= 40 && Guid.TryParse(raw, out _))
                {
                    context.Items["AuthUserId"] = raw;
                    await ResolveTenantIdAsync(context, raw);
                }
            }
        }

        await _next(context);
    }

    // ── Claims DTO ──

    private readonly record struct JwtClaims(string UserId, string? Email, string? Role);

    private static void SetContextItems(HttpContext context, JwtClaims claims)
    {
        context.Items["AuthUserId"] = claims.UserId;
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
            using var hmac = new HMACSHA256(_jwtSecret!);
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

    private static JwtClaims? ExtractClaimsWithoutVerification(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            return ExtractClaimsFromPayload(parts[1]);
        }
        catch { return null; }
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
