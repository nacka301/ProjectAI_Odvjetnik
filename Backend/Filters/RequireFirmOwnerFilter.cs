using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Filters;

/// <summary>
/// Action filter that restricts access to users whose firm-level role is "admin" or "owner".
/// Reads "AuthFirmRole" from HttpContext.Items (set by SupabaseJwtMiddleware via ITenantResolver).
///
/// Returns 403 Forbidden if the role is not one of the allowed values.
///
/// Usage:
///   [RequireFirmOwner]
///   public async Task&lt;IActionResult&gt; RestoreCase(…) { … }
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireFirmOwnerAttribute : ActionFilterAttribute
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "owner"
    };

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var firmRole = context.HttpContext.Items["AuthFirmRole"] as string;

        if (string.IsNullOrWhiteSpace(firmRole) || !AllowedRoles.Contains(firmRole))
        {
            context.Result = new ObjectResult(new
            {
                error = "Samo vlasnik odvjetničkog ureda može pristupiti ovom resursu.",
                code  = "FIRM_OWNER_REQUIRED"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
