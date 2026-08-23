using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// The access rule itself. SubscriptionGate.Decide takes the clock as an
/// argument precisely so this file can drive every boundary without a database
/// and without waiting seven days.
///
/// What is being protected here is the pair of promises the whole change rests
/// on: that a trial lasts exactly TrialDays from valid_from, and that an office
/// is never refused for a reason we did not positively establish.
/// </summary>
public class SubscriptionGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static SubscriptionAccess Decide(
        string? status,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null)
        => SubscriptionGate.Decide(status, validFrom, validUntil, Now);

    // ═══════════════════════════════════════════════════════════
    //  Trial
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Trial_JustStarted_IsAllowed()
    {
        var access = Decide("trial", validFrom: Now);

        Assert.True(access.Allowed);
        Assert.Equal(SubscriptionState.Trial, access.State);
        Assert.Equal(Now.AddDays(SubscriptionPlans.TrialDays), access.AccessUntil);
    }

    /// <summary>
    /// The number the badge prints. A brand new office must read "još 7 dana",
    /// not 6 — the off-by-one here is the difference between an honest promise
    /// and one the user can catch us shortening.
    /// </summary>
    [Fact]
    public void Trial_JustStarted_HasFullTrialDaysRemaining()
    {
        var access = Decide("trial", validFrom: Now);
        Assert.Equal(SubscriptionPlans.TrialDays, access.DaysRemaining);
    }

    [Theory]
    [InlineData(0, 7)]   // signed up this instant
    [InlineData(1, 6)]
    [InlineData(5, 2)]
    [InlineData(6, 1)]   // one full day left
    public void Trial_DaysRemaining_CountsDown(int daysElapsed, int expectedRemaining)
    {
        var access = Decide("trial", validFrom: Now.AddDays(-daysElapsed));

        Assert.True(access.Allowed);
        Assert.Equal(expectedRemaining, access.DaysRemaining);
    }

    /// <summary>
    /// Zero is reserved for the final day and the badge words it "Zadnji dan
    /// probe". If this collapsed into 1 the user would be told they have a day
    /// left on the morning access disappears.
    /// </summary>
    [Fact]
    public void Trial_OnTheLastDay_ReportsZeroDaysNotOne()
    {
        var access = Decide("trial", validFrom: Now.AddDays(-SubscriptionPlans.TrialDays).AddHours(2));

        Assert.True(access.Allowed);
        Assert.Equal(0, access.DaysRemaining);
    }

    [Fact]
    public void Trial_OneSecondBeforeExpiry_IsStillAllowed()
    {
        var access = Decide("trial", validFrom: Now.AddDays(-SubscriptionPlans.TrialDays).AddSeconds(1));
        Assert.True(access.Allowed);
    }

    [Fact]
    public void Trial_ExactlyAtExpiry_IsRefused()
    {
        var access = Decide("trial", validFrom: Now.AddDays(-SubscriptionPlans.TrialDays));

        Assert.False(access.Allowed);
        Assert.Equal(SubscriptionState.Expired, access.State);
    }

    [Fact]
    public void Trial_LongOver_IsRefused()
    {
        var access = Decide("trial", validFrom: Now.AddDays(-90));

        Assert.False(access.Allowed);
        Assert.Equal(SubscriptionState.Expired, access.State);
    }

    /// <summary>
    /// valid_until in the database is written for the benefit of anyone reading
    /// the row by hand. If the gate consulted it, TrialDays would stop being the
    /// single source of truth the moment the two disagreed.
    /// </summary>
    [Fact]
    public void Trial_IgnoresValidUntilFromDatabase()
    {
        var generous = Decide("trial",
            validFrom: Now.AddDays(-90),
            validUntil: Now.AddYears(5));
        Assert.False(generous.Allowed);

        var stingy = Decide("trial",
            validFrom: Now,
            validUntil: Now.AddDays(-1));
        Assert.True(stingy.Allowed);
    }

    // ═══════════════════════════════════════════════════════════
    //  Paid
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("active")]
    [InlineData("canceled")]
    public void Paid_WithFutureDate_IsAllowed(string status)
    {
        var access = Decide(status, validUntil: Now.AddDays(30));

        Assert.True(access.Allowed);
        Assert.Equal(SubscriptionState.Paid, access.State);
        Assert.Null(access.DaysRemaining);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("canceled")]
    public void Paid_WithPastDate_IsRefused(string status)
    {
        var access = Decide(status, validUntil: Now.AddDays(-1));

        Assert.False(access.Allowed);
        Assert.Equal(SubscriptionState.Expired, access.State);
    }

    /// <summary>
    /// An office the admin set to active and left without an end date is not
    /// expired. Reading a missing date as "over" would shut out exactly the
    /// account someone was in the middle of fixing.
    /// </summary>
    [Fact]
    public void Paid_WithNoEndDate_IsOpenEnded()
    {
        var access = Decide("active", validUntil: null);

        Assert.True(access.Allowed);
        Assert.Equal(SubscriptionState.Paid, access.State);
        Assert.Null(access.AccessUntil);
    }

    // ═══════════════════════════════════════════════════════════
    //  Refusals
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Expired_IsRefusedRegardlessOfDates()
    {
        var access = Decide("expired", validUntil: Now.AddYears(1));
        Assert.False(access.Allowed);
    }

    /// <summary>
    /// A status nobody has taught the gate about is refused, not waved through.
    /// A future migration adding a value would otherwise grant free access to
    /// every office carrying it until someone noticed.
    /// </summary>
    [Theory]
    [InlineData("paused")]
    [InlineData("free")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownStatus_IsRefused(string? status)
    {
        Assert.False(Decide(status, validUntil: Now.AddYears(1)).Allowed);
    }

    // ═══════════════════════════════════════════════════════════
    //  The three states are the ones the frontend switches on
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void States_AreTheCroatianWordsTheFrontendExpects()
    {
        Assert.Equal("proba", SubscriptionState.Trial);
        Assert.Equal("pretplata", SubscriptionState.Paid);
        Assert.Equal("isteklo", SubscriptionState.Expired);
    }
}
