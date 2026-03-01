using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Backend.Services;

namespace Backend.Middleware;

/// <summary>
/// Global middleware that logs every HTTP request to the api_audit_logs table.
///
/// Captures ONLY non-sensitive metadata:
///   • UserId + TenantId (from JWT via SupabaseJwtMiddleware → HttpContext.Items)
///   • Endpoint path, HTTP method, response status code
///   • Request duration (ms)
///   • SHA-256 hash of the client IP (NOT the raw IP)
///
/// EXPLICITLY DOES NOT read or log:
///   • Request body / payload
///   • Response body / payload
///   • Authorization headers or tokens
///   • Query string parameters (may contain sensitive data)
///
/// Fire-and-forget: the audit insert runs after the response is sent,
/// so it adds zero latency to the API response time.
/// </summary>
public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that are too noisy or non-actionable to log
    private static readonly string[] SkipPrefixes =
    [
        "/api/health",
        "/swagger",
        "/favicon"
    ];

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip: CORS preflight, health, swagger — no value in logging these
        if (context.Request.Method == "OPTIONS"
            || SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Start timing
        var sw = Stopwatch.StartNew();

        // Let the request execute normally
        await _next(context);

        sw.Stop();

        // ── Fire-and-forget: log asynchronously AFTER response is committed ──
        // Extract all metadata from context BEFORE the fire-and-forget closure,
        // because HttpContext is not safe to access after the request completes.

        var userId = context.Items["AuthUserId"] as string;
        var tenantId = context.Items["AuthTenantId"] as string;
        var httpMethod = context.Request.Method;
        var statusCode = context.Response.StatusCode;
        var durationMs = (int)sw.ElapsedMilliseconds;
        var actionName = $"{httpMethod} {path}";
        var endpoint = path;

        // Hash the IP — never store raw IP (GDPR)
        string? ipHash = null;
        var rawIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(rawIp))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawIp));
            ipHash = Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // Fire-and-forget — never awaited on the response path
        _ = Task.Run(async () =>
        {
            try
            {
                await auditLogService.LogAsync(
                    userId,
                    tenantId,
                    actionName,
                    endpoint,
                    httpMethod,
                    statusCode,
                    durationMs,
                    ipHash);
            }
            catch
            {
                // Swallow — audit logging must never crash the app
            }
        });
    }
}
