namespace Backend.Models;

/// <summary>
/// Authoritative pricing / limit constants for the three PravoTek tiers.
/// All money values are in EUR.  Limits of -1 mean "unlimited".
///
/// Free  (0 €)  – Max 5 predmeta, 3 računa, 5 AI kredita/mj
/// Starter (19 €) – Neograničeno predmeta/računa, 0 AI kredita
/// Pro  (39 €) – Neograničeno predmeta/računa, 100 AI kredita/mj
/// </summary>
public static class SubscriptionPlans
{
    // ─── Plan identifiers (stored in tenant_subscriptions.plan_id) ───
    public const string FreePlanId     = "free";
    public const string StarterPlanId  = "starter";
    public const string ProPlanId      = "pro";

    // ─── Display labels (Croatian) ───
    public const string FreePlanLabel     = "Besplatno";
    public const string StarterPlanLabel  = "Starter";
    public const string ProPlanLabel      = "Pro";

    // ─── Monthly price (EUR, net) ───
    public const decimal FreePriceEur     = 0m;
    public const decimal StarterPriceEur  = 19m;
    public const decimal ProPriceEur      = 39m;

    // ─── Hard limits (-1 = unlimited) ───

    // Cases (predmeti)
    public const int FreeCasesLimit      = 5;
    public const int StarterCasesLimit   = -1;
    public const int ProCasesLimit       = -1;

    // Invoices (računi)
    public const int FreeInvoicesLimit     = 3;
    public const int StarterInvoicesLimit  = -1;
    public const int ProInvoicesLimit      = -1;

    // AI credits per billing period (month)
    public const int FreeAiCreditsPerMonth     = 5;
    public const int StarterAiCreditsPerMonth  = 0;
    public const int ProAiCreditsPerMonth      = 100;

    // ─── Helper: is the limit unlimited? ───
    public static bool IsUnlimited(int limit) => limit < 0;

    /// <summary>
    /// Returns (casesLimit, invoicesLimit, aiCreditsPerMonth) for a given plan_id.
    /// Falls back to Free limits for unknown plan identifiers.
    /// </summary>
    public static (int Cases, int Invoices, int AiCredits) LimitsFor(string? planId) =>
        planId switch
        {
            StarterPlanId => (StarterCasesLimit, StarterInvoicesLimit, StarterAiCreditsPerMonth),
            ProPlanId     => (ProCasesLimit,     ProInvoicesLimit,     ProAiCreditsPerMonth),
            _             => (FreeCasesLimit,    FreeInvoicesLimit,    FreeAiCreditsPerMonth),
        };

    /// <summary>
    /// Returns the Croatian display label for a plan_id.
    /// </summary>
    public static string LabelFor(string? planId) =>
        planId switch
        {
            StarterPlanId => StarterPlanLabel,
            ProPlanId     => ProPlanLabel,
            _             => FreePlanLabel,
        };

    /// <summary>
    /// Returns monthly price in EUR for a plan_id.
    /// </summary>
    public static decimal PriceFor(string? planId) =>
        planId switch
        {
            StarterPlanId => StarterPriceEur,
            ProPlanId     => ProPriceEur,
            _             => FreePriceEur,
        };
}
