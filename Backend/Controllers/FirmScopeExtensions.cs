using Backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Stamps the caller's firm onto a record before it is persisted.
///
/// The firm id comes from <c>HttpContext.Items["AuthTenantId"]</c>, which
/// SupabaseJwtMiddleware resolves from the verified token via ITenantResolver —
/// never from the request body. A client-supplied law_firm_id would be a way to
/// write into another firm's data, which is exactly what migration 016's
/// <c>WITH CHECK</c> exists to stop.
/// </summary>
public static class FirmScopeExtensions
{
    /// <summary>The caller's law_firm_id, or null if identity could not be resolved.</summary>
    public static string? FirmId(this ControllerBase controller) =>
        controller.HttpContext.Items["AuthTenantId"] as string;

    /// <summary>
    /// Sets <see cref="IFirmScoped.LawFirmId"/> from the verified token,
    /// overwriting anything the client may have sent.
    ///
    /// Returns false when the user has no firm — that means the profile row is
    /// missing or unlinked, and the insert would fail on the RLS check anyway.
    /// Callers turn that into a 403 rather than letting Postgres produce an
    /// opaque error.
    /// </summary>
    public static bool ScopeToFirm<T>(this ControllerBase controller, T record)
        where T : IFirmScoped
    {
        var firmId = controller.FirmId();
        if (string.IsNullOrWhiteSpace(firmId))
            return false;

        record.LawFirmId = firmId;
        return true;
    }

    /// <summary>
    /// Standard response for a caller whose firm could not be resolved.
    /// Practically this means the user_profiles row is missing — the trigger
    /// from migration 015 should make that impossible for new accounts.
    /// </summary>
    public static IActionResult NoFirmResult(this ControllerBase controller) =>
        controller.StatusCode(403, new
        {
            error = "Vaš korisnički račun nije povezan s odvjetničkim uredom. " +
                    "Postavite ured u Postavkama ili se obratite podršci.",
            code = "FIRM_NOT_RESOLVED"
        });
}
