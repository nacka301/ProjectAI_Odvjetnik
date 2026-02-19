using Backend.Helpers;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Comprehensive tests for SseHelpers utility methods:
/// PatchReasoningValue, BuildComparisonTable, BuildPriceRange, BuildBenchmarkSummary.
/// </summary>
public class SseHelpersTests
{
    // ══════════════════════════════════════════════════════════
    //  PatchReasoningValue — replaces AI's wrong value with engine value
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PatchReasoningValue_ReplacesAiValueWithEngineValue()
    {
        string reasoning = "Procijenjena vrijednost nekretnine iznosi 250.000 € na temelju usporedivih.";
        double aiValue = 250_000;
        int engineValue = 230_000;

        string patched = SseHelpers.PatchReasoningValue(reasoning, aiValue, engineValue);

        Assert.Contains("230.000", patched);
        Assert.DoesNotContain("250.000", patched);
    }

    [Fact]
    public void PatchReasoningValue_HandlesCroatianFormat()
    {
        // Croatian format: 250.000 (dot = thousands)
        string reasoning = "Vrijednost je 350.000 EUR.";
        double aiValue = 350_000;
        int engineValue = 320_000;

        string patched = SseHelpers.PatchReasoningValue(reasoning, aiValue, engineValue);

        Assert.Contains("320.000", patched);
    }

    [Fact]
    public void PatchReasoningValue_EmptyReasoning_ReturnsEmpty()
    {
        string result = SseHelpers.PatchReasoningValue("", 100_000, 90_000);
        Assert.Equal("", result);
    }

    [Fact]
    public void PatchReasoningValue_NullReasoning_ReturnsNull()
    {
        string? result = SseHelpers.PatchReasoningValue(null!, 100_000, 90_000);
        Assert.Null(result);
    }

    [Fact]
    public void PatchReasoningValue_NoMatchingValue_ReturnsUnchanged()
    {
        string reasoning = "Cijena nekretnine je razumna.";
        string patched = SseHelpers.PatchReasoningValue(reasoning, 250_000, 230_000);
        Assert.Equal(reasoning, patched);
    }

    [Fact]
    public void PatchReasoningValue_MultipleOccurrences_ReplacesAll()
    {
        string reasoning = "Vrijednost je 200.000 €. Ponavljam: cijena 200.000 EUR.";
        string patched = SseHelpers.PatchReasoningValue(reasoning, 200_000, 180_000);

        // Both occurrences should be replaced
        Assert.DoesNotContain("200.000", patched);
    }

    [Fact]
    public void PatchReasoningValue_SmallValues()
    {
        string reasoning = "Zemljište vrijedi 50.000 EUR ukupno.";
        string patched = SseHelpers.PatchReasoningValue(reasoning, 50_000, 45_000);
        Assert.Contains("45.000", patched);
    }

    [Fact]
    public void PatchReasoningValue_MillionValues()
    {
        string reasoning = "Luksuzna vila vrijednosti 1.500.000 EUR na obali.";
        string patched = SseHelpers.PatchReasoningValue(reasoning, 1_500_000, 1_350_000);
        Assert.Contains("1.350.000", patched);
    }

    // ══════════════════════════════════════════════════════════
    //  BuildComparisonTable — builds transparent comp table
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildComparisonTable_EmptyComps_ReturnsEmptyList()
    {
        var engine = new DeterministicValuation { UsedComparables = [] };
        var rows = SseHelpers.BuildComparisonTable(engine, 0);
        Assert.Empty(rows);
    }

    [Fact]
    public void BuildComparisonTable_NullComps_ReturnsEmptyList()
    {
        var engine = new DeterministicValuation { UsedComparables = null! };
        var rows = SseHelpers.BuildComparisonTable(engine, 0);
        Assert.Empty(rows);
    }

    [Fact]
    public void BuildComparisonTable_ExcludesOutliers()
    {
        var engine = MakeEngineWithComps(new[]
        {
            (2500.0, 0.85, false, 10.0),  // Good comp
            (2600.0, 0.80, false, 5.0),   // Good comp
            (8000.0, 0.30, true, 0.0),    // Outlier — should be excluded
        });

        var rows = SseHelpers.BuildComparisonTable(engine, 0);

        Assert.Equal(2, rows.Count); // Only non-outliers
        Assert.All(rows, r => Assert.True(r.RawPricePerM2 < 5000));
    }

    [Fact]
    public void BuildComparisonTable_OrdersBySimilarityDescending()
    {
        var engine = MakeEngineWithComps(new[]
        {
            (2500.0, 0.60, false, 0.0),
            (2600.0, 0.95, false, 0.0),
            (2400.0, 0.75, false, 0.0),
        });

        var rows = SseHelpers.BuildComparisonTable(engine, 0);

        Assert.Equal(3, rows.Count);
        // Should be ordered by similarity descending
        Assert.True(rows[0].SimilarityScore >= rows[1].SimilarityScore);
        Assert.True(rows[1].SimilarityScore >= rows[2].SimilarityScore);
    }

    [Fact]
    public void BuildComparisonTable_CalculatesAdjustedPrice()
    {
        var engine = new DeterministicValuation
        {
            UsedComparables =
            [
                new UsedComparable
                {
                    Title = "Stan A",
                    Url = "https://example.com",
                    PricePerM2 = 2500,
                    SimilarityScore = 0.85,
                    IsOutlier = false,
                    CorrectionPercent = -10, // -10% per-comp correction
                    AreaM2 = 65,
                    TotalPrice = 162500,
                }
            ]
        };

        var rows = SseHelpers.BuildComparisonTable(engine, 0);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(2500, row.RawPricePerM2);
        Assert.Equal(-10, row.CorrectionPercent);
        // Adjusted: 2500 * (1 + (-10)/100) = 2500 * 0.9 = 2250
        Assert.Equal(2250, row.AdjustedPricePerM2, precision: 0);
    }

    [Fact]
    public void BuildComparisonTable_IndexesFromOne()
    {
        var engine = MakeEngineWithComps(new[]
        {
            (2500.0, 0.85, false, 0.0),
            (2600.0, 0.80, false, 0.0),
        });

        var rows = SseHelpers.BuildComparisonTable(engine, 0);

        Assert.Equal(1, rows[0].Index);
        Assert.Equal(2, rows[1].Index);
    }

    [Fact]
    public void BuildComparisonTable_SimilarityScoreNormalization()
    {
        // Score <= 1 should be multiplied by 100 for display
        var engine = MakeEngineWithComps(new[]
        {
            (2500.0, 0.85, false, 0.0), // 0.85 → 85
        });

        var rows = SseHelpers.BuildComparisonTable(engine, 0);
        Assert.Equal(85, rows[0].SimilarityScore);
    }

    // ══════════════════════════════════════════════════════════
    //  BuildPriceRange — 3-tier price estimate
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildPriceRange_ValidInput_ReturnsThreeTiers()
    {
        var engine = new DeterministicValuation
        {
            CalculatedPricePerM2 = 2500,
            StdDevPerM2 = 300
        };

        var range = SseHelpers.BuildPriceRange(engine, 65);

        Assert.NotNull(range);
        Assert.True(range!.ConservativePricePerM2 < range.RealisticPricePerM2);
        Assert.True(range.RealisticPricePerM2 < range.OptimisticPricePerM2);
        Assert.True(range.ConservativeTotal < range.RealisticTotal);
        Assert.True(range.RealisticTotal < range.OptimisticTotal);
    }

    [Fact]
    public void BuildPriceRange_ZeroPricePerM2_ReturnsNull()
    {
        var engine = new DeterministicValuation { CalculatedPricePerM2 = 0 };
        var range = SseHelpers.BuildPriceRange(engine, 65);
        Assert.Null(range);
    }

    [Fact]
    public void BuildPriceRange_ZeroSize_ReturnsNull()
    {
        var engine = new DeterministicValuation { CalculatedPricePerM2 = 2500 };
        var range = SseHelpers.BuildPriceRange(engine, 0);
        Assert.Null(range);
    }

    [Fact]
    public void BuildPriceRange_ZeroStdDev_UsesFallback15Percent()
    {
        var engine = new DeterministicValuation
        {
            CalculatedPricePerM2 = 2500,
            StdDevPerM2 = 0  // Should fallback to 15% of ppm2
        };

        var range = SseHelpers.BuildPriceRange(engine, 65);

        Assert.NotNull(range);
        // Fallback stdDev = 2500 * 0.15 = 375
        // Conservative = max(2500 - 375, 2500 * 0.7) = max(2125, 1750) = 2125
        Assert.Equal(2125, range!.ConservativePricePerM2, precision: 0);
    }

    [Fact]
    public void BuildPriceRange_ConservativeFlooredAt70Percent()
    {
        var engine = new DeterministicValuation
        {
            CalculatedPricePerM2 = 2500,
            StdDevPerM2 = 2000  // Very high stdDev → conservative would be 500, but floored at 70%
        };

        var range = SseHelpers.BuildPriceRange(engine, 65);

        Assert.NotNull(range);
        // Conservative = max(2500 - 2000, 2500 * 0.7) = max(500, 1750) = 1750
        Assert.Equal(1750, range!.ConservativePricePerM2, precision: 0);
    }

    [Fact]
    public void BuildPriceRange_OptimisticCappedAt130Percent()
    {
        var engine = new DeterministicValuation
        {
            CalculatedPricePerM2 = 2500,
            StdDevPerM2 = 2000  // Very high stdDev → optimistic would be 4500, capped at 130%
        };

        var range = SseHelpers.BuildPriceRange(engine, 65);

        Assert.NotNull(range);
        // Optimistic = min(2500 + 2000, 2500 * 1.3) = min(4500, 3250) = 3250
        Assert.Equal(3250, range!.OptimisticPricePerM2, precision: 0);
    }

    [Fact]
    public void BuildPriceRange_TotalPricesAreCorrect()
    {
        var engine = new DeterministicValuation
        {
            CalculatedPricePerM2 = 2500,
            StdDevPerM2 = 300
        };

        var range = SseHelpers.BuildPriceRange(engine, 65);

        Assert.NotNull(range);
        // Realistic total = 2500 * 65 = 162500
        Assert.Equal(162500, range!.RealisticTotal);
    }

    // ══════════════════════════════════════════════════════════
    //  BuildBenchmarkSummary — summary string from engine data
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildBenchmarkSummary_FullData_ContainsAllParts()
    {
        var engine = new DeterministicValuation
        {
            RegionName = "Zagreb",
            MarketAverageAsking = 2500,
            CalculatedPricePerM2 = 2300,
            RealizationDiscountPercent = -10.5,
            UsablePricePoints = 8,
            CoefficientOfVariation = 12.3
        };
        var market = new MarketResearchResult
        {
            DetectedPropertyType = "stan"
        };

        string summary = SseHelpers.BuildBenchmarkSummary(engine, market);

        Assert.Contains("Zagreb", summary);
        Assert.Contains("stan", summary);
        Assert.Contains("2.500", summary); // Asking
        Assert.Contains("2.300", summary); // Calculated
        Assert.Contains("-10,5", summary); // Discount
        Assert.Contains("8", summary);     // Price points
        Assert.Contains("12,3", summary);  // CV
    }

    [Fact]
    public void BuildBenchmarkSummary_EmptyData_ReturnsEmptyParts()
    {
        var engine = new DeterministicValuation();
        var market = new MarketResearchResult();

        string summary = SseHelpers.BuildBenchmarkSummary(engine, market);

        // Should not crash, returns empty or minimal
        Assert.NotNull(summary);
    }

    [Fact]
    public void BuildBenchmarkSummary_PartialData_IncludesAvailableFields()
    {
        var engine = new DeterministicValuation
        {
            RegionName = "Split",
            CalculatedPricePerM2 = 3500,
            // Other fields left at default (0)
        };
        var market = new MarketResearchResult();

        string summary = SseHelpers.BuildBenchmarkSummary(engine, market);

        Assert.Contains("Split", summary);
        Assert.Contains("3.500", summary);
    }

    [Fact]
    public void BuildBenchmarkSummary_UsesPipeDelimiter()
    {
        var engine = new DeterministicValuation
        {
            RegionName = "Zadar",
            CalculatedPricePerM2 = 2800,
        };
        var market = new MarketResearchResult { DetectedPropertyType = "stan" };

        string summary = SseHelpers.BuildBenchmarkSummary(engine, market);
        Assert.Contains(" | ", summary);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER FACTORIES
    // ══════════════════════════════════════════════════════════

    private static DeterministicValuation MakeEngineWithComps(
        (double ppm2, double similarity, bool isOutlier, double correction)[] comps)
    {
        return new DeterministicValuation
        {
            UsedComparables = comps.Select((c, i) => new UsedComparable
            {
                Title = $"Comparable {i + 1}",
                Url = $"https://example.com/comp-{i + 1}",
                PricePerM2 = c.ppm2,
                SimilarityScore = c.similarity,
                IsOutlier = c.isOutlier,
                CorrectionPercent = c.correction,
                AreaM2 = 65,
                TotalPrice = c.ppm2 * 65,
            }).ToList()
        };
    }
}
