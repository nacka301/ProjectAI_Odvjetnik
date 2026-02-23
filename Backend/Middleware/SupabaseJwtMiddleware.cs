using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Backend.Middleware;

/// <summary>
/// Verifies Supabase JWT access tokens on every API request.
/// Extracts the authenticated user_id (sub claim) and stores it in HttpContext.Items.
///
/// Security model:
///   - Production: SUPABASE_JWT_SECRET must be set. Every request must include
///     a valid "Authorization: Bearer {access_token}" header.
///   - Development: If SUPABASE_JWT_SECRET is not set, falls back to x-user-id
///     header (trusted, for local dev only). Logs a warning on startup.
///
/// Controllers read the verified user ID via:
///   HttpContext.Items["AuthUserId"] as string
///
/// This eliminates the user impersonation vulnerability where an attacker
/// could set x-user-id to any UUID and access another lawyer's data.
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
                var userId = VerifyJwtAndExtractSub(token);
                if (userId != null)
                {
                    context.Items["AuthUserId"] = userId;
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
                // Dev mode — extract user_id from JWT payload without verification
                var userId = ExtractSubWithoutVerification(token);
                if (userId != null)
                {
                    context.Items["AuthUserId"] = userId;
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
                }
            }
        }

        // Allow request through — controllers enforce auth via GetUserId().
        // AI endpoints reject null userId via credit check (ConsumeCreditAsync).
        // Non-AI endpoints (settings, norms, health) don't need user_id.
        await _next(context);
    }

    /// <summary>
    /// Verify HS256 JWT signature and extract the "sub" claim (user UUID).
    /// Also checks token expiration.
    /// </summary>
    private string? VerifyJwtAndExtractSub(string token)
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

            // Decode payload
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var payload = doc.RootElement;

            // Check expiration
            if (payload.TryGetProperty("exp", out var expProp))
            {
                var exp = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
                if (exp < DateTimeOffset.UtcNow)
                    return null; // Token expired
            }

            // Extract sub (user UUID) — this is the Supabase user ID
            if (payload.TryGetProperty("sub", out var subProp))
            {
                var sub = subProp.GetString();
                if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out _))
                    return sub;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract "sub" claim without signature verification (development only).
    /// </summary>
    private static string? ExtractSubWithoutVerification(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);

            if (doc.RootElement.TryGetProperty("sub", out var subProp))
            {
                var sub = subProp.GetString();
                if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out _))
                    return sub;
            }
            return null;
        }
        catch { return null; }
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
