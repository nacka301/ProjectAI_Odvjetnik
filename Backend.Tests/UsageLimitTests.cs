using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Unit tests for SubscriptionPlans constants and UsageCheckResult / UsageResource enums.
/// These are pure in-memory tests — no HTTP or DB required.
/// </summary>
public class UsageLimitTests
{
    // ═══════════════════════════════════════════════════════════
    //  SubscriptionPlans.LimitsFor
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("free",    5,  3,   5)]
    [InlineData("starter", -1, -1,  0)]
    [InlineData("pro",     -1, -1, 100)]
    [InlineData(null,      5,  3,   5)]   // unknown → free
    [InlineData("unknown", 5,  3,   5)]   // unknown → free
    public void LimitsFor_ReturnsCorrectValues(string? planId, int cases, int invoices, int ai)
    {
        var (c, i, a) = SubscriptionPlans.LimitsFor(planId);
        Assert.Equal(cases, c);
        Assert.Equal(invoices, i);
        Assert.Equal(ai, a);
    }

    // ═══════════════════════════════════════════════════════════
    //  SubscriptionPlans.LabelFor
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("free",    "Besplatno")]
    [InlineData("starter", "Starter")]
    [InlineData("pro",     "Pro")]
    [InlineData(null,      "Besplatno")]
    [InlineData("garbage", "Besplatno")]
    public void LabelFor_ReturnsCorrectLabel(string? planId, string expected)
    {
        Assert.Equal(expected, SubscriptionPlans.LabelFor(planId));
    }

    // ═══════════════════════════════════════════════════════════
    //  SubscriptionPlans.PriceFor
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("free",    0)]
    [InlineData("starter", 19)]
    [InlineData("pro",     39)]
    [InlineData(null,      0)]
    public void PriceFor_ReturnsCorrectPrice(string? planId, int expectedEur)
    {
        Assert.Equal((decimal)expectedEur, SubscriptionPlans.PriceFor(planId));
    }

    // ═══════════════════════════════════════════════════════════
    //  IsUnlimited
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-999, true)]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(100, false)]
    public void IsUnlimited_CorrectForBoundary(int limit, bool expected)
    {
        Assert.Equal(expected, SubscriptionPlans.IsUnlimited(limit));
    }

    // ═══════════════════════════════════════════════════════════
    //  UsageCheckResult
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void UsageCheckResult_Ok_IsAllowed()
    {
        var result = UsageCheckResult.Ok();
        Assert.True(result.Allowed);
        Assert.Equal("", result.Code);
        Assert.Null(result.Limit);
        Assert.Null(result.Used);
    }

    [Fact]
    public void UsageCheckResult_Blocked_HasCorrectFields()
    {
        var result = UsageCheckResult.Blocked("Limit reached", 5, 5);
        Assert.False(result.Allowed);
        Assert.Equal("UPGRADE_REQUIRED", result.Code);
        Assert.Equal("Limit reached", result.Message);
        Assert.Equal(5, result.Limit);
        Assert.Equal(5, result.Used);
    }

    // ═══════════════════════════════════════════════════════════
    //  Free-tier boundary conditions
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void FreeTier_CasesLimit_Is5()
    {
        Assert.Equal(5, SubscriptionPlans.FreeCasesLimit);
    }

    [Fact]
    public void FreeTier_InvoicesLimit_Is3()
    {
        Assert.Equal(3, SubscriptionPlans.FreeInvoicesLimit);
    }

    [Fact]
    public void FreeTier_AiCreditsPerMonth_Is5()
    {
        Assert.Equal(5, SubscriptionPlans.FreeAiCreditsPerMonth);
    }

    [Fact]
    public void StarterTier_AiCredits_IsZero()
    {
        Assert.Equal(0, SubscriptionPlans.StarterAiCreditsPerMonth);
    }

    [Fact]
    public void ProTier_AiCredits_Is100()
    {
        Assert.Equal(100, SubscriptionPlans.ProAiCreditsPerMonth);
    }

    // ═══════════════════════════════════════════════════════════
    //  Plan ID constants match expected strings
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void PlanIds_AreCanonicalLowerCase()
    {
        Assert.Equal("free",    SubscriptionPlans.FreePlanId);
        Assert.Equal("starter", SubscriptionPlans.StarterPlanId);
        Assert.Equal("pro",     SubscriptionPlans.ProPlanId);
    }

    // ═══════════════════════════════════════════════════════════
    //  UsageResource enum covers three resource types
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void UsageResource_HasExpectedValues()
    {
        Assert.Equal(0, (int)UsageResource.Case);
        Assert.Equal(1, (int)UsageResource.Invoice);
        Assert.Equal(2, (int)UsageResource.AiCredit);
    }

    // ═══════════════════════════════════════════════════════════
    //  Starter/Pro unlimited limits
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("starter")]
    [InlineData("pro")]
    public void PaidPlans_HaveUnlimitedCasesAndInvoices(string planId)
    {
        var (cases, invoices, _) = SubscriptionPlans.LimitsFor(planId);
        Assert.True(SubscriptionPlans.IsUnlimited(cases));
        Assert.True(SubscriptionPlans.IsUnlimited(invoices));
    }
}
