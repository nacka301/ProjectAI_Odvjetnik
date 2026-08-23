using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>Which of the three states an office is in. Serialized to the frontend as-is.</summary>
public static class SubscriptionState
{
    public const string Trial   = "proba";
    public const string Paid    = "pretplata";
    public const string Expired = "isteklo";
}

/// <summary>
/// The verdict on one office: may it work, and what should the user be told.
/// </summary>
public sealed record SubscriptionAccess(
    bool Allowed,
    string State,
    int? DaysRemaining,
    DateTimeOffset? AccessUntil);

public interface ISubscriptionGate
{
    Task<SubscriptionAccess> EvaluateAsync(string? tenantId, CancellationToken ct = default);
    void InvalidateCache(string tenantId);
}

/// <summary>
/// The one place that decides whether an office's trial or subscription is still
/// good. Both the API guard and the badge the user reads come from this answer,
/// so the lock and the label cannot disagree.
///
/// The rule:
///
///   status='trial'                → allowed while now is before valid_from + TrialDays
///   status='active' | 'canceled'  → allowed while now is before valid_until
///   anything else ('expired')     → refused
///   no row at all                 → allowed
///   database unreachable          → allowed, logged at Error
///
/// The last two lines are deliberate, and they are the ones worth arguing about.
/// This is a paywall, not authentication: "we could not check" is not a verdict,
/// and the same split is already written into SupabaseJwtMiddleware (401 for a
/// bad token, 503 for a verifier we cannot reach). Refusing on an outage would
/// lock out the people who have paid, over a fault that is ours. Allowing on one
/// costs, at worst, a few minutes of access to someone whose trial has lapsed.
///
/// For a trial, valid_until in the database is ignored on purpose. The expiry is
/// derived from valid_from + SubscriptionPlans.TrialDays so that changing the
/// trial length is one constant in one file rather than a migration.
/// </summary>
public sealed class SubscriptionGate : ISubscriptionGate
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly ILogger<SubscriptionGate> _logger;

    // tenantId → (verdict, expiry). Static so it survives the scoped lifetime,
    // same shape as UsageService's cache.
    private static readonly ConcurrentDictionary<string, (SubscriptionAccess Access, DateTime Expiry)> Cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// What an office gets when we have nothing to go on. Allowed — see the
    /// class comment; this is the fail-open path, not a real subscription.
    /// </summary>
    private static readonly SubscriptionAccess Unknown =
        new(true, SubscriptionState.Paid, null, null);

    public SubscriptionGate(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        ILogger<SubscriptionGate> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _logger = logger;
    }

    public async Task<SubscriptionAccess> EvaluateAsync(string? tenantId, CancellationToken ct = default)
    {
        if (!_supabase.IsConfigured || string.IsNullOrWhiteSpace(tenantId))
            return Unknown;

        if (Cache.TryGetValue(tenantId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Access;

        var access = await LoadAsync(tenantId, ct);
        Cache[tenantId] = (access, DateTime.UtcNow.Add(CacheTtl));
        return access;
    }

    /// <summary>
    /// Drop one office's cached verdict. Called after the admin changes a
    /// subscription, so the change takes effect on the next request instead of
    /// up to a minute later — a delay that reads as "the button did nothing".
    /// </summary>
    public void InvalidateCache(string tenantId) => Cache.TryRemove(tenantId, out _);

    private async Task<SubscriptionAccess> LoadAsync(string tenantId, CancellationToken ct)
    {
        try
        {
            var safeTenant = Uri.EscapeDataString(tenantId);
            var url = $"{_supabase.Url!.TrimEnd('/')}/rest/v1/tenant_subscriptions" +
                      $"?law_firm_id=eq.{safeTenant}" +
                      "&select=status,valid_from,valid_until&order=created_at.desc&limit=1";

            // service_role, same reasoning as UsageService: the party being
            // enforced against must not be able to influence what comes back.
            // The tenant is fixed from the verified token before the call.
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "SubscriptionGate: tenant_subscriptions nedostupan za ured {TenantId} ({Status}) — propustam zahtjev.",
                    tenantId, response.StatusCode);
                return Unknown;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.GetArrayLength() == 0)
                return Unknown;

            var row = doc.RootElement[0];
            var status = row.TryGetProperty("status", out var s) ? s.GetString() : null;

            return Decide(status, ReadTime(row, "valid_from"), ReadTime(row, "valid_until"), DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubscriptionGate: provjera nije uspjela za ured {TenantId} — propustam zahtjev.", tenantId);
            return Unknown;
        }
    }

    /// <summary>
    /// The rule itself, with no I/O in it, so the tests can drive every branch
    /// by passing a clock instead of standing up a database.
    /// </summary>
    public static SubscriptionAccess Decide(
        string? status,
        DateTimeOffset? validFrom,
        DateTimeOffset? validUntil,
        DateTimeOffset now)
    {
        switch (status)
        {
            case "trial":
            {
                // valid_until from the database is deliberately not consulted:
                // TrialDays is the single source of truth for how long a trial is.
                var start = validFrom ?? now;
                var until = start.AddDays(SubscriptionPlans.TrialDays);
                return until > now
                    ? new SubscriptionAccess(true, SubscriptionState.Trial, DaysLeft(until, now), until)
                    : new SubscriptionAccess(false, SubscriptionState.Expired, 0, until);
            }

            case "active":
            case "canceled":
            {
                // A row with no end date is open-ended, not expired. Reading a
                // missing value as "over" would shut out an office the admin set
                // to active and left without a date.
                if (validUntil == null)
                    return new SubscriptionAccess(true, SubscriptionState.Paid, null, null);

                return validUntil > now
                    ? new SubscriptionAccess(true, SubscriptionState.Paid, null, validUntil)
                    : new SubscriptionAccess(false, SubscriptionState.Expired, 0, validUntil);
            }

            default:
                // 'expired', and anything a future migration might introduce.
                // An unrecognised status is refused rather than waved through.
                return new SubscriptionAccess(false, SubscriptionState.Expired, 0, validUntil);
        }
    }

    /// <summary>
    /// Whole days left, rounded down.
    ///
    /// A brand new office has exactly TrialDays; with 25 hours to go it has 1;
    /// inside the final 24 hours it has 0, which the badge words as "Zadnji dan
    /// probe" rather than "Još 0 dana". Rounding up instead would print "7 dana"
    /// on a trial with six days and one hour left — a promise the user can catch
    /// us shortening.
    /// </summary>
    private static int DaysLeft(DateTimeOffset until, DateTimeOffset now)
    {
        var hours = (until - now).TotalHours;
        return hours <= 0 ? 0 : (int)Math.Floor(hours / 24d);
    }

    private static DateTimeOffset? ReadTime(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }
}
