namespace Backend.Middleware;

/// <summary>
/// Protects admin-only API endpoints by checking the caller's email
/// (extracted from JWT by SupabaseJwtMiddleware → HttpContext.Items["AuthEmail"])
/// against the ADMIN_EMAIL environment variable.
///
/// MUST run AFTER SupabaseJwtMiddleware in the pipeline.
///
/// Protected paths:
///   - /api/rag/*          — RAG seeding/testing
///   - /api/admin/*        — admin dashboard endpoints
///   - /api/audit/test-*   — test validation endpoints
/// </summary>
public sealed class AdminGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _adminEmail;
    private readonly ILogger<AdminGuardMiddleware> _logger;

    private static readonly string[] AdminPrefixes =
    [
        "/api/rag/",
        "/api/admin/",
        "/api/audit/test-",
    ];

    public AdminGuardMiddleware(RequestDelegate next, IConfiguration config, ILogger<AdminGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        _adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")
            ?? config["Admin:Email"];

        if (string.IsNullOrWhiteSpace(_adminEmail))
            logger.LogWarning("ADMIN_EMAIL not configured — admin endpoints will reject all requests");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        var isAdminRoute = AdminPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!isAdminRoute)
        {
            await _next(context);
            return;
        }

        // Must be authenticated
        var userId = context.Items["AuthUserId"] as string;
        var email = context.Items["AuthEmail"] as string;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Authentication required for admin endpoints.\"}");
            return;
        }

        // Check admin email
        if (string.IsNullOrWhiteSpace(_adminEmail) ||
            !string.Equals(email, _adminEmail, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Non-admin user {Email} attempted to access {Path}", email, path);
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Pristup odbijen. Samo administrator moze pristupiti ovom resursu.\"}");
            return;
        }

        await _next(context);
    }
}
