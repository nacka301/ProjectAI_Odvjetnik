using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// The plan constants and the usage-check result type. Pure in-memory — no HTTP,
/// no database.
///
/// This file used to assert a three-tier table (free / starter / pro) with its
/// own limits, labels and prices. All of that is gone: there is one free plan.
/// </summary>
public class UsageLimitTests
{
    // ═══════════════════════════════════════════════════════════
    //  Identity
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void PlanId_IsTheSingleCanonicalValue()
    {
        // Matches the CHECK constraint from migration 024.
        Assert.Equal("pravotek", SubscriptionPlans.PlanId);
    }

    // ═══════════════════════════════════════════════════════════
    //  IsUnlimited — still used by UsageService for -1 ceilings
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

    [Fact]
    public void Unlimited_Constant_ReadsAsUnlimited()
    {
        Assert.True(SubscriptionPlans.IsUnlimited(SubscriptionPlans.Unlimited));
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
    //  UsageResource enum covers three resource types
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void UsageResource_HasExpectedValues()
    {
        Assert.Equal(0, (int)UsageResource.Case);
        Assert.Equal(1, (int)UsageResource.Invoice);
        Assert.Equal(2, (int)UsageResource.AiCredit);
    }
}
