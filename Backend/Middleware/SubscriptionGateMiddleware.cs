using Backend.Services;

namespace Backend.Middleware;

/// <summary>
/// Refuses the finished modules once an office's trial or subscription is over.
///
/// This is the whole lock. The notice the user reads on /troskovnik and
/// /klijenti runs on hardware they control and can be skipped by typing a URL or
/// calling the API directly; the module is only actually closed because this
/// middleware refuses its prefixes, and it refuses them before MapControllers
/// runs — so a refused call never reaches a controller and costs nothing.
///
/// Which prefixes: <see cref="ModuleRegistry.PaidApiPrefixes"/>, i.e. every
/// module marked Spreman. Today that is Troškovnik and Stranke. A module that
/// later flips to Spreman is covered by that edit alone.
///
/// Deliberately NOT guarded: /api/settings/*. The subscription screen is where
/// the user finds out the access is closed and how to pay for it, so shutting it
/// along with everything else would leave them staring at an error with no way
/// forward.
///
/// MUST run after SupabaseJwtMiddleware (needs AuthTenantId and AuthEmail) and
/// after AdminGuardMiddleware, so an in-development module keeps answering
/// MODULE_IN_DEVELOPMENT rather than a payment message that would send the user
/// looking for their wallet over a feature that does not exist yet.
///
/// 402, not 403: this codebase already answers 402 for "this needs paying for"
/// (LimitEnforcementFilter) and reserves 403 for "not for you" (admin-only
/// tooling, unfinished modules). The frontend tells them apart by the code
/// field, not the status alone.
/// </summary>
public sealed class SubscriptionGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AdminIdentity _admin;
    private readonly ILogger<SubscriptionGateMiddleware> _logger;

    private const string ExpiredBody =
        "{\"error\":\"Pristup je istekao. Pristup Troskovniku i Strankama produljuje se " +
        "bez naknade — javite se na vackadavid@gmail.com.\"," +
        "\"code\":\"TRIAL_EXPIRED\"}";

    public SubscriptionGateMiddleware(
        RequestDelegate next,
        AdminIdentity admin,
        ILogger<SubscriptionGateMiddleware> logger)
    {
        _next = next;
        _admin = admin;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!ModuleRegistry.IsPaidPath(path))
        {
            await _next(context);
            return;
        }

        // The admin works without a subscription — otherwise the one account
        // that can fix a lockout would be locked out by it.
        if (_admin.IsAdminRequest(context))
        {
            await _next(context);
            return;
        }

        var gate = context.RequestServices.GetRequiredService<ISubscriptionGate>();
        var tenantId = context.Items["AuthTenantId"] as string;

        var access = await gate.EvaluateAsync(tenantId, context.RequestAborted);
        if (access.Allowed)
        {
            await _next(context);
            return;
        }

        _logger.LogInformation(
            "Ured {TenantId} je pozvao {Path} s isteklom pretplatom — odgovaram 402.",
            tenantId, path);

        context.Response.StatusCode = 402; // Payment Required
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(ExpiredBody);
    }
}
