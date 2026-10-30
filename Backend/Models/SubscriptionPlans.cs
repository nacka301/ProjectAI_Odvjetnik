namespace Backend.Models;

/// <summary>
/// What is left of the plan model now that PravoTek is free.
///
/// There is one plan, no trial, no price and no expiry: every registered user
/// has permanent access and nothing in the backend refuses a request because of
/// what is written in tenant_subscriptions. The table and its columns (status,
/// valid_until, price_eur) are still in the database as dead data; nothing reads
/// them to decide access, and they are not dropped because dropping a column is
/// a destructive schema change with nothing to gain.
///
/// What still reads the table is UsageService and CreditService, for the case /
/// invoice ceilings (absent on this plan) and the AI-credit allowance of the
/// unfinished modules.
/// </summary>
public static class SubscriptionPlans
{
    /// <summary>Stored in tenant_subscriptions.plan_id. One value, because there is one plan.</summary>
    public const string PlanId = "pravotek";

    /// <summary>
    /// -1 means "no limit". UsageService counts cases and invoices against a
    /// ceiling, and on this plan that ceiling is absent.
    /// </summary>
    public const int Unlimited = -1;

    public static bool IsUnlimited(int limit) => limit < 0;
}
