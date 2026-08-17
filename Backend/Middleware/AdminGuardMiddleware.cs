namespace Backend.Middleware;

/// <summary>
/// Restricts API endpoints to the ADMIN_EMAIL account by checking the caller's
/// email (extracted from the JWT by SupabaseJwtMiddleware →
/// HttpContext.Items["AuthEmail"]) against the ADMIN_EMAIL environment variable.
///
/// MUST run AFTER SupabaseJwtMiddleware in the pipeline.
///
/// Two separate reasons a request can be refused here:
///
///   1. Admin-only tooling — never meant for users at all:
///        /api/rag/*          — RAG seeding/testing
///        /api/admin/*        — admin dashboard endpoints
///        /api/audit/test-*   — test validation endpoints
///
///   2. Modules still in development — meant for users eventually, but not yet.
///      The list lives in ModuleRegistry, mirrored from frontend/lib/modules.ts.
///      These answer with code MODULE_IN_DEVELOPMENT so the frontend can tell
///      "not for you" apart from "not finished".
///
/// This is the whole lock. The blurred cards and the route redirect on the
/// frontend are courtesy; both run on hardware the user controls. A module is
/// only actually closed because this middleware refuses its prefixes, and it
/// refuses them before MapControllers runs — which is also why a refused call
/// never reaches ConsumeCreditAsync and so costs the caller nothing.
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

        // Fail closed: with no admin to recognise, nobody is one — which now
        // also means every in-development module stays shut for everybody.
        if (string.IsNullOrWhiteSpace(_adminEmail))
            logger.LogWarning(
                "ADMIN_EMAIL not configured — admin endpoints and in-development modules will reject all requests");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Admin-only tooling is checked first, so /api/audit/test-* keeps its
        // original "you are not the admin" wording even though /api/audit is
        // also locked as an in-development module.
        var isAdminRoute = AdminPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        var lockedModuleId = isAdminRoute ? null : ModuleRegistry.FindLockedModuleId(path);

        if (!isAdminRoute && lockedModuleId is null)
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
            if (lockedModuleId is not null)
            {
                _logger.LogInformation(
                    "User {Email} called {Path}, which belongs to module {Module} — still in development",
                    email, path, lockedModuleId);
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"error\":\"Ovaj modul je jos u razvoju i dostupan je samo administratoru.\",\"code\":\"MODULE_IN_DEVELOPMENT\"}");
                return;
            }

            _logger.LogWarning("Non-admin user {Email} attempted to access {Path}", email, path);
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Pristup odbijen. Samo administrator moze pristupiti ovom resursu.\"}");
            return;
        }

        await _next(context);
    }
}
