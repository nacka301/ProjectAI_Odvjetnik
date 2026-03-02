using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Filters;

/// <summary>
/// Action filter that blocks the request with 402 Payment Required when
/// the authenticated tenant has exceeded a plan limit.
///
/// Usage:
///   [LimitEnforcement(UsageResource.Case)]
///   public async Task&lt;IActionResult&gt; Create(…) { … }
///
/// The filter reads "AuthTenantId" from HttpContext.Items (set by SupabaseJwtMiddleware)
/// and delegates to <see cref="UsageService"/>. If the check fails, the action body
/// never executes.
///
/// After a successful action that returns 2xx, the filter automatically
/// increments the tenant's usage counter so the next call sees the updated count.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class LimitEnforcementAttribute : ActionFilterAttribute
{
    private readonly UsageResource _resource;

    public LimitEnforcementAttribute(UsageResource resource)
    {
        _resource = resource;
    }

    // ── Before the action executes ──
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usageService = context.HttpContext.RequestServices.GetRequiredService<UsageService>();
        var tenantId = context.HttpContext.Items["AuthTenantId"] as string;

        var check = await usageService.CheckLimitAsync(tenantId, _resource);

        if (!check.Allowed)
        {
            context.Result = new ObjectResult(new
            {
                code    = check.Code,
                message = check.Message,
                limit   = check.Limit,
                used    = check.Used,
            })
            {
                StatusCode = 402 // Payment Required
            };
            return; // short-circuit — action body never runs
        }

        // Run the action
        var executedContext = await next();

        // ── After the action: if it succeeded (2xx), increment the counter ──
        if (executedContext.Exception == null && executedContext.Result is ObjectResult objResult)
        {
            var statusCode = objResult.StatusCode ?? 200;
            if (statusCode >= 200 && statusCode < 300)
            {
                await usageService.IncrementAsync(tenantId, _resource);
            }
        }
        else if (executedContext.Exception == null && executedContext.Result is CreatedResult)
        {
            await usageService.IncrementAsync(tenantId, _resource);
        }
    }
}
