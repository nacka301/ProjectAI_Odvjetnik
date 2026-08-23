namespace Backend.Models;

/// <summary>
/// The one plan PravoTek sells, and the one place its numbers live.
///
/// There used to be three tiers — free / starter / pro — none of which was ever
/// sold, and whose names were copied by hand into four more files that then
/// drifted apart. What replaced them is a single path: seven days of trial from
/// the day the office is opened, then a paid subscription.
///
/// ─── TRAJANJE PROBE ───
/// <see cref="TrialDays"/> is the only place the length of the trial is written.
/// Change it here and every office currently on trial is recomputed on the next
/// request — no migration, no database edit. The SQL trigger that creates the
/// row also writes a valid_until seven days out, but only so that a row read
/// straight out of Supabase is not misleading; for status='trial' the backend
/// derives the expiry from valid_from + TrialDays and ignores that column.
///
/// Payment is manual: the user writes an e-mail, and the admin sets status and
/// valid_until in /admin. There is no payment provider, by design.
/// </summary>
public static class SubscriptionPlans
{
    /// <summary>Stored in tenant_subscriptions.plan_id. One value, because there is one plan.</summary>
    public const string PlanId = "pravotek";

    /// <summary>
    /// How long a new office may work before paying.
    /// THIS IS THE ONLY PLACE TO CHANGE IT.
    /// </summary>
    public const int TrialDays = 7;

    // ─── Price (EUR, net) ───
    public const decimal MonthlyPriceEur = 19m;
    public const decimal YearlyPriceEur  = 190m;

    /// <summary>
    /// -1 means "no limit". Kept because UsageService still counts cases and
    /// invoices against a ceiling, and on this plan that ceiling is absent.
    /// </summary>
    public const int Unlimited = -1;

    public static bool IsUnlimited(int limit) => limit < 0;
}
