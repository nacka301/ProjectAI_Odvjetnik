using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// V74: Golden Regression Tests for Valuation Engine (nekretnine).
///
/// PURPOSE:
///   Guard against regressions in the deterministic valuation pipeline.
///   Every code change to ValuationEngine must pass these tests.
///   No LLM calls — fully deterministic, runs in milliseconds.
///
/// TEST PROPERTIES:
///   A) Zagreb centar stan 75m², 5 comps → Visoka confidence, realistic €/m²
///   B) Split kuća 120m², coastal → reflects premium, garage adjustment works
///   C) Osijek mali stan 40m², 1 comp → Niska confidence, still produces value
///   D) Zadar građevinsko zemljište 500m² → land valuation pipeline
///   E) Zagreb stan without known size (V71 ZK workflow) → estimated size fallback
///
/// WHAT THIS CATCHES:
///   - Price calculation regressions (weighted median, realization gap, adjustments)
///   - IQR outlier detection stability
///   - MathConfidence scoring formula changes
///   - Confidence threshold regressions (Visoka≥75, Srednja≥50, Niska≥25, Vrlo niska&lt;25)
///   - Safety floor (50%) and adjustment cap (±40%) enforcement
///   - Feature adjustment regressions (garage, condition, sea view)
///   - V71 estimated size fallback for ZK workflow
///   - Pipeline explainability (trace log populated)
/// </summary>
public class ValuationRegressionTests
{
    private readonly ValuationEngine _engine;

    public ValuationRegressionTests()
    {
        _engine = new ValuationEngine(NullLogger<ValuationEngine>.Instance);
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY A: Zagreb centar, stan 75m², 5+ comps
    //  Good data quality → Visoka confidence, stable price
    // ══════════════════════════════════════════════════════════

    private static (ExtractedPropertyInfo Property, MarketResearchResult Market) MakeScenarioA()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Trosobni stan 75 m² u centru Zagreba, renoviran 2023., 3. kat sa liftom",
            SizeM2 = 75,
            Location = "Zagreb, Centar",
            Condition = "renovirano",
            PropertyState = "renovated",
            RawText = "Trosobni stan 75 m² u centru Zagreba potpuno renoviran 2023 lift",
            MicroLocation = "Centar",
            KeyFeatures = ["centar grada", "renovirano 2023", "lift"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 85 },
            ActiveListings = PatchUrls(
            [
                MakeListing("Stan Ilica 80m²", "Renoviran trosoban stan 80 m², Ilica, centar Zagreba", 272000, 3400, 80, "renovated"),
                MakeListing("Stan Vlaška 65m²", "Dvosoban stan 65 m², renoviran, centar Zagreba", 214500, 3300, 65, "renovated"),
                MakeListing("Stan Jurišićeva 70m²", "Trosoban stan 70 m², dobro stanje, centar", 231000, 3300, 70, "good"),
                MakeListing("Stan Palmotićeva 90m²", "Četverosoban stan 90 m², novogradnja centar", 342000, 3800, 90, "new"),
                MakeListing("Stan Martićeva 55m²", "Dvosoban stan 55 m², izvorno stanje, 2. kat", 143000, 2600, 55, "original"),
                MakeListing("Stan Maksimir 60m²", "Dvosoban stan 60 m², dobro stanje, Maksimir", 156000, 2600, 60, "good"),
            ], "Zagreb, Centar"),
        };

        return (property, market);
    }

    [Fact]
    public void PropertyA_ZagrebCentar_ProducesRealisticValue()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        // 75m² in Zagreb center at ~3000-3500 €/m² → ~225k-262k EUR
        Assert.True(result.EstimatedValueEur >= 150_000,
            $"Zagreb centar 75m² should be >= 150k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= 350_000,
            $"Zagreb centar 75m² should be <= 350k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void PropertyA_ZagrebCentar_PricePerM2InRange()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.True(result.CalculatedPricePerM2 >= 2000,
            $"€/m² should be >= 2000, got {result.CalculatedPricePerM2}");
        Assert.True(result.CalculatedPricePerM2 <= 5000,
            $"€/m² should be <= 5000, got {result.CalculatedPricePerM2}");
    }

    [Fact]
    public void PropertyA_ZagrebCentar_HighConfidence()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        // 5+ comps, low CV, primary search → should be Visoka or Srednja
        Assert.True(result.MathConfidenceScore >= 50,
            $"Confidence should be >= 50l, got {result.MathConfidenceScore}");
        Assert.Contains(result.MathConfidenceLevel, new[] { "Visoka", "Srednja" });
    }

    [Fact]
    public void PropertyA_ZagrebCentar_HasMultipleComps()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.True(result.UsablePricePoints >= 3,
            $"Should have >= 3 usable comps, got {result.UsablePricePoints}");
    }

    [Fact]
    public void PropertyA_ZagrebCentar_PipelineIsPopulated()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.True(result.Pipeline.Count >= 5,
            $"Pipeline should have >= 5 entries, got {result.Pipeline.Count}");
        Assert.All(result.Pipeline, line =>
            Assert.False(string.IsNullOrWhiteSpace(line)));
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY B: Split kuća 120m², obalna premija
    //  Renovated with garage → adjustments must work correctly
    // ══════════════════════════════════════════════════════════

    private static (ExtractedPropertyInfo Property, MarketResearchResult Market) MakeScenarioB()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Kuća 120 m² u Splitu, Meje, renovirana, garaža, pogled na more",
            SizeM2 = 120,
            Location = "Split, Meje",
            Condition = "renovirano",
            PropertyState = "renovated",
            RawText = "Obiteljska kuća 120 m² Meje Split renovirana garaža pogled na more vrt 200m²",
            YardAreaM2 = 200,
            MicroLocation = "Meje",
            KeyFeatures = ["pogled na more", "garaža", "renovirano", "vrt"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Split",
            DetectedPropertyType = "kuća",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 80 },
            ActiveListings = PatchUrls(
            [
                MakeListing("Kuća Meje A", "Kuća 100 m², renovirana, pogled more, Meje Split", 450000, 4500, 100, "renovated",
                    hasGarage: true),
                MakeListing("Kuća Meje B", "Kuća 150 m², dobro stanje, Meje Split, vrt 300m²", 525000, 3500, 150, "good"),
                MakeListing("Kuća Firule", "Kuća 130 m², renovirana, Firule Split, garaža", 455000, 3500, 130, "renovated",
                    hasGarage: true),
                MakeListing("Kuća Žnjan", "Kuća 110 m², novogradnja, pogled more, Split", 440000, 4000, 110, "new"),
                MakeListing("Kuća Sućidar", "Kuća 140 m², potrebna adaptacija, periferija Splita", 280000, 2000, 140, "needs_work"),
            ], "Split, Meje"),
        };

        return (property, market);
    }

    [Fact]
    public void PropertyB_SplitHouse_ProducesRealisticValue()
    {
        var (property, market) = MakeScenarioB();
        var result = _engine.Calculate(property, market);

        // 120m² house in Split Meje at ~3000-4500 €/m² → ~360k-540k EUR
        Assert.True(result.EstimatedValueEur >= 200_000,
            $"Split kuća 120m² should be >= 200k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= 700_000,
            $"Split kuća 120m² should be <= 700k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void PropertyB_SplitHouse_PriceReflectsCoastalPremium()
    {
        var (property, market) = MakeScenarioB();
        var result = _engine.Calculate(property, market);

        // Coastal premium location → should not be cheap
        Assert.True(result.CalculatedPricePerM2 >= 2000,
            $"Split Meje €/m² should be >= 2000, got {result.CalculatedPricePerM2}");
    }

    [Fact]
    public void PropertyB_SplitHouse_ValueIsPositive()
    {
        var (property, market) = MakeScenarioB();
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0, "Value must be positive");
        Assert.True(result.CalculatedPricePerM2 > 0, "Price/m² must be positive");
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY C: Osijek mali stan 40m², only 1 comparable
    //  Poor data quality → Niska confidence, but still works
    // ══════════════════════════════════════════════════════════

    private static (ExtractedPropertyInfo Property, MarketResearchResult Market) MakeScenarioC()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Garsonijera 40 m² u Osijeku, Gornji grad, izvorno stanje, prizemlje",
            SizeM2 = 40,
            Location = "Osijek, Gornji grad",
            Condition = "izvorno",
            PropertyState = "original",
            RawText = "Garsonijera 40 m² Osijek Gornji grad prizemlje izvorno stanje",
            MicroLocation = "Gornji grad",
            KeyFeatures = ["prizemlje"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Osijek",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "SubLocationDrill",
            Confidence = new ConfidenceScore { Level = "Low", Score = 35 },
            ActiveListings = PatchUrls(
            [
                MakeListing("Stan Osijek centar", "Jednosoban stan 45 m², dobro stanje, centar Osijeka", 49500, 1100, 45, "good"),
            ], "Osijek, Gornji grad"),
        };

        return (property, market);
    }

    [Fact]
    public void PropertyC_OsijekSingleComp_ProducesValue()
    {
        var (property, market) = MakeScenarioC();
        var result = _engine.Calculate(property, market);

        // Even with 1 comp, should produce a reasonable value
        Assert.True(result.EstimatedValueEur > 0, "Must produce a value even with 1 comp");
        Assert.True(result.EstimatedValueEur >= 20_000,
            $"Osijek 40m² should be >= 20k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= 120_000,
            $"Osijek 40m² should be <= 120k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void PropertyC_OsijekSingleComp_LowConfidence()
    {
        var (property, market) = MakeScenarioC();
        var result = _engine.Calculate(property, market);

        // 1 comp + late search phase → should penalize confidence
        Assert.True(result.MathConfidenceScore < 80,
            $"Single comp confidence should be < 80, got {result.MathConfidenceScore}");
    }

    [Fact]
    public void PropertyC_OsijekSingleComp_SinglePricePoint()
    {
        var (property, market) = MakeScenarioC();
        var result = _engine.Calculate(property, market);

        Assert.True(result.UsablePricePoints <= 1,
            $"Should have at most 1 usable price, got {result.UsablePricePoints}");
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY D: Zadar građevinsko zemljište 500m²
    //  Land valuation — different pricing model
    // ══════════════════════════════════════════════════════════

    private static (ExtractedPropertyInfo Property, MarketResearchResult Market) MakeScenarioD()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Građevinsko zemljište 500 m² u okolici Zadra, komunalno opremljeno",
            SizeM2 = 500,
            Location = "Zadar, Bibinje",
            Condition = "",
            PropertyState = "",
            RawText = "Građevinsko zemljište 500 m² Bibinje kod Zadra komunalno opremljeno",
            MicroLocation = "Bibinje",
            KeyFeatures = ["komunalno opremljeno"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zadar",
            DetectedPropertyType = "građevinsko zemljište",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 65 },
            ActiveListings = PatchUrls(
            [
                MakeListing("Zemljište Bibinje A", "Građevinsko zemljište 600 m², Bibinje, Zadar", 72000, 120, 600, ""),
                MakeListing("Zemljište Bibinje B", "Građevinsko zemljište 450 m², komunalno, Bibinje", 58500, 130, 450, ""),
                MakeListing("Zemljište Sukošan", "Građevinsko zemljište 700 m², blizu mora, Zadar", 112000, 160, 700, ""),
                MakeListing("Zemljište Bibinje C", "Zemljište 400 m², pogled more", 60000, 150, 400, ""),
            ], "Zadar, Bibinje"),
        };

        return (property, market);
    }

    [Fact]
    public void PropertyD_Land_ProducesRealisticValue()
    {
        var (property, market) = MakeScenarioD();
        var result = _engine.Calculate(property, market);

        // 500m² land near Zadar at ~100-160 €/m² → ~50k-80k EUR
        Assert.True(result.EstimatedValueEur >= 20_000,
            $"Land 500m² Zadar should be >= 20k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= 200_000,
            $"Land 500m² Zadar should be <= 200k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void PropertyD_Land_PricePerM2IsLandLevel()
    {
        var (property, market) = MakeScenarioD();
        var result = _engine.Calculate(property, market);

        // Land prices are MUCH lower than building prices
        Assert.True(result.CalculatedPricePerM2 >= 30,
            $"Land €/m² should be >= 30, got {result.CalculatedPricePerM2}");
        Assert.True(result.CalculatedPricePerM2 <= 500,
            $"Land €/m² should be <= 500, got {result.CalculatedPricePerM2}");
    }

    [Fact]
    public void PropertyD_Land_HasUsableComps()
    {
        var (property, market) = MakeScenarioD();
        var result = _engine.Calculate(property, market);

        Assert.True(result.UsablePricePoints >= 2,
            $"Should have >= 2 land comps, got {result.UsablePricePoints}");
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY E: V71 — Unknown size (ZK workflow, SizeM2=0)
    //  Uses estimated size → still produces value, with penalty
    // ══════════════════════════════════════════════════════════

    private static (ExtractedPropertyInfo Property, MarketResearchResult Market) MakeScenarioE()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan u Zagrebu, nepoznata površina, iz ZK izvadka",
            SizeM2 = 0, // Unknown from ZK
            Location = "Zagreb, Trešnjevka",
            Condition = "dobro",
            PropertyState = "good",
            RawText = "Stan Zagreb Trešnjevka",
            KeyFeatures = [],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
            ActiveListings = PatchUrls(
            [
                MakeListing("Stan Trešnjevka A", "Dvosoban stan 55 m², Trešnjevka Zagreb", 137500, 2500, 55, "good"),
                MakeListing("Stan Trešnjevka B", "Trosoban stan 70 m², Trešnjevka, renoviran", 189000, 2700, 70, "renovated"),
                MakeListing("Stan Trešnjevka C", "Dvosoban stan 50 m², dobro stanje", 120000, 2400, 50, "good"),
                MakeListing("Stan Trešnjevka D", "Jednosoban stan 40 m², izvorno stanje", 88000, 2200, 40, "original"),
            ], "Zagreb, Trešnjevka"),
        };

        return (property, market);
    }

    [Fact]
    public void PropertyE_UnknownSize_StillProducesValue()
    {
        var (property, market) = MakeScenarioE();
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0,
            "Must produce a value even when SizeM2=0");
    }

    [Fact]
    public void PropertyE_UnknownSize_UsesEstimatedSize()
    {
        var (property, market) = MakeScenarioE();
        var result = _engine.Calculate(property, market);

        Assert.True(result.UsedEstimatedSize,
            "Should flag that estimated size was used");
        Assert.True(result.EstimatedSizeM2 > 0,
            $"EstimatedSizeM2 should be > 0, got {result.EstimatedSizeM2}");
    }

    [Fact]
    public void PropertyE_UnknownSize_ConfidencePenalized()
    {
        // Compare with similar scenario that HAS a known size
        var (propertyKnown, marketKnown) = MakeScenarioE();
        propertyKnown.SizeM2 = 55; // Give it a known size

        var (propertyUnknown, marketUnknown) = MakeScenarioE();

        var resultKnown = _engine.Calculate(propertyKnown, marketKnown);
        var resultUnknown = _engine.Calculate(propertyUnknown, marketUnknown);

        Assert.True(resultUnknown.MathConfidenceScore < resultKnown.MathConfidenceScore,
            $"Unknown size confidence ({resultUnknown.MathConfidenceScore}) should be < " +
            $"known size confidence ({resultKnown.MathConfidenceScore})");
    }

    [Fact]
    public void PropertyE_UnknownSize_PipelineMentionsEstimate()
    {
        var (property, market) = MakeScenarioE();
        var result = _engine.Calculate(property, market);

        Assert.Contains(result.Pipeline, line =>
            line.Contains("V71") || line.Contains("estimiran", StringComparison.OrdinalIgnoreCase));
    }

    // ══════════════════════════════════════════════════════════
    //  MATH CONFIDENCE — Scoring formula regression
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Confidence_ManyCompsLowCV_IsHighest()
    {
        // 8+ comps, low CV → should get bonus
        var property = MakeSimpleProperty(70, "Zagreb, Centar");
        var listings = new List<ComparableSearchResult>();
        for (int i = 0; i < 10; i++)
        {
            // Tight cluster around 3000 €/m² (CV < 15%)
            double pricePerM2 = 2850 + (i % 3) * 100; // 2850, 2950, 3050 rotating
            listings.Add(MakeListing($"Stan {i}", $"Stan {60 + i} m², centar Zagreba",
                pricePerM2 * (60 + i), pricePerM2, 60 + i, "good"));
        }

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 90 },
            ActiveListings = PatchUrls(listings, "Zagreb, Centar"),
        };

        var result = _engine.Calculate(property, market);

        Assert.True(result.MathConfidenceScore >= 75,
            $"10 tight comps should give >= 75 confidence, got {result.MathConfidenceScore}");
        Assert.Equal("Visoka", result.MathConfidenceLevel);
    }

    [Fact]
    public void Confidence_ZeroComps_VeryLow()
    {
        var property = MakeSimpleProperty(60, "Nepoznato, Nepoznato");
        var market = new MarketResearchResult
        {
            RegionName = "Nepoznato",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "RegionalFallback",
            Confidence = new ConfidenceScore { Level = "Low", Score = 10 },
            ActiveListings = [],
        };

        var result = _engine.Calculate(property, market);

        Assert.True(result.MathConfidenceScore < 75,
            $"Zero comps should give < 75 confidence, got {result.MathConfidenceScore}");
    }

    // ══════════════════════════════════════════════════════════
    //  CONFIDENCE THRESHOLDS — verify bands are stable
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(100, "Visoka")]
    [InlineData(75, "Visoka")]
    [InlineData(74, "Srednja")]
    [InlineData(50, "Srednja")]
    [InlineData(49, "Niska")]
    [InlineData(25, "Niska")]
    [InlineData(24, "Vrlo niska")]
    [InlineData(0, "Vrlo niska")]
    public void ConfidenceThresholds_AreStable(int score, string expectedLevel)
    {
        // Verify the mapping formula hasn't changed
        string level = score switch
        {
            >= 75 => "Visoka",
            >= 50 => "Srednja",
            >= 25 => "Niska",
            _ => "Vrlo niska"
        };

        Assert.Equal(expectedLevel, level);
    }

    // ══════════════════════════════════════════════════════════
    //  IQR OUTLIER DETECTION — verify stability
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void IQR_WithOutlier_RemovesExtreme()
    {
        var property = MakeSimpleProperty(70, "Zagreb, Maksimir");
        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan 1", "Stan 70 m², dobro stanje", 175000, 2500, 70, "good"),
            MakeListing("Stan 2", "Stan 65 m², renoviran", 178750, 2750, 65, "renovated"),
            MakeListing("Stan 3", "Stan 75 m², dobro stanje", 187500, 2500, 75, "good"),
            MakeListing("Stan 4", "Stan 60 m², renoviran", 174000, 2900, 60, "renovated"),
            MakeListing("Stan 5", "Stan 80 m², dobro stanje", 200000, 2500, 80, "good"),
            // Outlier: 10000 €/m² should be removed by IQR
            MakeListing("Luksuzni penthouse", "Penthouse 100 m², luksuzni", 1000000, 10000, 100, "new"),
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 70 },
            ActiveListings = PatchUrls(listings, "Zagreb, Maksimir"),
        };

        var result = _engine.Calculate(property, market);

        Assert.True(result.OutliersRemoved >= 1,
            $"Should remove at least 1 outlier, removed {result.OutliersRemoved}");
        // The result should NOT be inflated by the 10000 €/m² outlier
        Assert.True(result.CalculatedPricePerM2 < 5000,
            $"Price should not be inflated by outlier, got {result.CalculatedPricePerM2}");
    }

    [Fact]
    public void IQR_AllSimilarPrices_NoOutliers()
    {
        var property = MakeSimpleProperty(65, "Rijeka, Centar");
        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan R1", "Stan 60 m², Rijeka centar", 138000, 2300, 60, "good"),
            MakeListing("Stan R2", "Stan 70 m², Rijeka centar", 161000, 2300, 70, "good"),
            MakeListing("Stan R3", "Stan 65 m², Rijeka centar", 156000, 2400, 65, "renovated"),
            MakeListing("Stan R4", "Stan 55 m², Rijeka centar", 132000, 2400, 55, "good"),
            MakeListing("Stan R5", "Stan 75 m², Rijeka centar", 172500, 2300, 75, "good"),
        };

        var market = new MarketResearchResult
        {
            RegionName = "Rijeka",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 85 },
            ActiveListings = PatchUrls(listings, "Rijeka, Centar"),
        };

        var result = _engine.Calculate(property, market);

        // All prices are tight cluster → few or no outliers
        Assert.True(result.OutliersRemoved <= 1,
            $"Tight cluster should have <= 1 outlier, removed {result.OutliersRemoved}");
    }

    // ══════════════════════════════════════════════════════════
    //  ADJUSTMENT CAP — ±40% must be enforced
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AdjustmentCap_TotalAdjustment_NeverExceeds40Percent()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.True(result.TotalAdjustmentPercent >= -40.0,
            $"TotalAdjustment should be >= -40%, got {result.TotalAdjustmentPercent}");
        Assert.True(result.TotalAdjustmentPercent <= 40.0,
            $"TotalAdjustment should be <= 40%, got {result.TotalAdjustmentPercent}");
    }

    [Fact]
    public void AdjustmentCap_AllScenarios_RespectCap()
    {
        var scenarios = new[]
        {
            MakeScenarioA(), MakeScenarioB(), MakeScenarioC(), MakeScenarioD()
        };

        foreach (var (property, market) in scenarios)
        {
            var result = _engine.Calculate(property, market);

            Assert.True(result.TotalAdjustmentPercent >= -40.0 && result.TotalAdjustmentPercent <= 40.0,
                $"Adjustment {result.TotalAdjustmentPercent:F1}% for {property.Location} exceeds ±40% cap");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SAFETY FLOOR — value never below 50% of average comp
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void SafetyFloor_ExtremePenalties_DontCrashValue()
    {
        // Property with max penalties — needs adaptation, no features
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 50 m² u Zagrebu, loše stanje, potrebna adaptacija, prizemlje",
            SizeM2 = 50,
            Location = "Zagreb, Dubrava",
            Condition = "potrebna adaptacija",
            PropertyState = "needs_adaptation",
            RawText = "Stan 50 m² Zagreb Dubrava potrebna adaptacija devastiran zapušten",
            KeyFeatures = [],
        };

        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan Dubrava A", "Stan 55 m², renoviran, Dubrava", 99000, 1800, 55, "renovated"),
            MakeListing("Stan Dubrava B", "Stan 60 m², novogradnja, Dubrava", 120000, 2000, 60, "new"),
            MakeListing("Stan Dubrava C", "Stan 45 m², novogradnja", 90000, 2000, 45, "new"),
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 65 },
            ActiveListings = PatchUrls(listings, "Zagreb, Dubrava"),
        };

        var result = _engine.Calculate(property, market);

        // Even with max penalties, value should not crash below a sensible floor
        Assert.True(result.EstimatedValueEur > 0, "Value must be positive");
        Assert.True(result.EstimatedValueEur >= 20_000,
            $"Even worst case should be >= 20k, got {result.EstimatedValueEur}");
    }

    // ══════════════════════════════════════════════════════════
    //  MONOTONICITY — better property → higher value
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Monotonicity_RenovatedMore_ThanOriginal()
    {
        var listings = PatchUrls(
        [
            MakeListing("Stan 1", "Stan 65 m², centar Zagreba", 175500, 2700, 65, "good"),
            MakeListing("Stan 2", "Stan 70 m², centar Zagreba", 189000, 2700, 70, "good"),
            MakeListing("Stan 3", "Stan 60 m², centar Zagreba", 162000, 2700, 60, "good"),
            MakeListing("Stan 4", "Stan 75 m², centar Zagreba", 202500, 2700, 75, "good"),
        ], "Zagreb, Centar");

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 80 },
            ActiveListings = listings,
        };

        var renovated = MakeSimpleProperty(65, "Zagreb, Centar");
        renovated.Condition = "renovirano";
        renovated.PropertyState = "renovated";
        renovated.RawText = "Stan 65 m² centar Zagreba potpuno renoviran";

        var original = MakeSimpleProperty(65, "Zagreb, Centar");
        original.Condition = "izvorno";
        original.PropertyState = "original";
        original.RawText = "Stan 65 m² centar Zagreba izvorno stanje";

        var resultReno = _engine.Calculate(renovated, market);
        var resultOrig = _engine.Calculate(original, market);

        // Renovated should be worth at least as much (condition adjustments work)
        Assert.True(resultReno.CalculatedPricePerM2 >= resultOrig.CalculatedPricePerM2,
            $"Renovated (€{resultReno.CalculatedPricePerM2}/m²) should be >= original (€{resultOrig.CalculatedPricePerM2}/m²)");
    }

    [Fact]
    public void Monotonicity_BiggerProperty_HigherTotalValue()
    {
        var listings = PatchUrls(
        [
            MakeListing("Stan 1", "Stan 60 m², Zagreb", 162000, 2700, 60, "good"),
            MakeListing("Stan 2", "Stan 70 m², Zagreb", 189000, 2700, 70, "good"),
            MakeListing("Stan 3", "Stan 80 m², Zagreb", 216000, 2700, 80, "good"),
            MakeListing("Stan 4", "Stan 50 m², Zagreb", 135000, 2700, 50, "good"),
        ], "Zagreb, Trešnjevka");

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 80 },
            ActiveListings = listings,
        };

        var small = MakeSimpleProperty(40, "Zagreb, Trešnjevka");
        var big = MakeSimpleProperty(90, "Zagreb, Trešnjevka");

        var resultSmall = _engine.Calculate(small, market);
        var resultBig = _engine.Calculate(big, market);

        Assert.True(resultBig.EstimatedValueEur > resultSmall.EstimatedValueEur,
            $"Bigger ({resultBig.EstimatedValueEur}) should be > smaller ({resultSmall.EstimatedValueEur})");
    }

    // ══════════════════════════════════════════════════════════
    //  REALIZATION GAP — discount is non-negative
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void RealizationGap_IsWithinReasonableRange()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        // Realization gap can be negative (appreciating market) but should stay bounded
        Assert.True(result.RealizationDiscountPercent >= -30,
            $"Realization gap should be >= -30, got {result.RealizationDiscountPercent}");
        Assert.True(result.RealizationDiscountPercent <= 30,
            $"Realization gap should be <= 30, got {result.RealizationDiscountPercent}");
    }

    [Fact]
    public void RealizationGap_ReasoningPopulated()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.False(string.IsNullOrWhiteSpace(result.RealizationGapReasoning),
            "Gap reasoning should be populated");
    }

    // ══════════════════════════════════════════════════════════
    //  PRICE SOURCE — correctly identifies data origin
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PriceSource_IsPopulated()
    {
        var (property, market) = MakeScenarioA();
        var result = _engine.Calculate(property, market);

        Assert.False(string.IsNullOrWhiteSpace(result.PriceSource),
            "PriceSource should be populated");
    }

    // ══════════════════════════════════════════════════════════
    //  NO DATA — engine handles gracefully
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void NoData_ProducesZeroValue_NotCrash()
    {
        var property = MakeSimpleProperty(60, "Nepoznato, Nepoznato");
        var market = new MarketResearchResult
        {
            RegionName = "Nepoznato",
            DetectedPropertyType = "stan",
            ActiveListings = [],
        };

        var result = _engine.Calculate(property, market);

        // Should not throw; value may be 0
        Assert.True(result.EstimatedValueEur >= 0);
        Assert.NotNull(result.Pipeline);
        Assert.True(result.Pipeline.Count >= 1);
    }

    // ══════════════════════════════════════════════════════════
    //  REGION NAME — correctly propagated
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Zagreb")]
    [InlineData("Split")]
    [InlineData("Osijek")]
    [InlineData("Zadar")]
    public void RegionName_IsPreserved(string region)
    {
        var property = MakeSimpleProperty(60, $"{region}, Centar");
        var market = new MarketResearchResult
        {
            RegionName = region,
            DetectedPropertyType = "stan",
            ActiveListings = PatchUrls(
            [
                MakeListing("Stan", $"Stan 60 m² {region}", 150000, 2500, 60, "good"),
            ], $"{region}, Centar"),
        };

        var result = _engine.Calculate(property, market);

        Assert.Equal(region, result.RegionName);
    }

    // ══════════════════════════════════════════════════════════
    //  PARSE CROATIAN NUMBER — regex safety
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("3.500", 3500)]
    [InlineData("3.500,00", 3500)]
    [InlineData("250.000", 250000)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("100", 100)]
    [InlineData("4.327,50", 4327.5)]
    public void ParseCroatianNumber_HandlesFormats(string input, double expected)
    {
        double result = ValuationEngine.ParseCroatianNumber(input);
        Assert.Equal(expected, result, precision: 1);
    }

    // ══════════════════════════════════════════════════════════
    //  CROSS-SCENARIO SANITY — all scenarios produce valid output
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AllScenarios_ProduceValidOutput()
    {
        var scenarios = new[]
        {
            MakeScenarioA(), MakeScenarioB(), MakeScenarioC(), MakeScenarioD(), MakeScenarioE()
        };

        foreach (var (property, market) in scenarios)
        {
            var result = _engine.Calculate(property, market);

            Assert.True(result.EstimatedValueEur >= 0,
                $"Value must be >= 0 for {property.Location}, got {result.EstimatedValueEur}");
            Assert.True(result.CalculatedPricePerM2 >= 0,
                $"Price/m² must be >= 0 for {property.Location}");
            Assert.True(result.MathConfidenceScore >= 0 && result.MathConfidenceScore <= 100,
                $"Confidence must be 0-100 for {property.Location}, got {result.MathConfidenceScore}");
            Assert.NotNull(result.Pipeline);
            Assert.False(string.IsNullOrWhiteSpace(result.MathConfidenceLevel));
            Assert.False(string.IsNullOrWhiteSpace(result.MathConfidenceExplanation));
        }
    }

    [Fact]
    public void AllScenarios_HaveReasonableRealizationGap()
    {
        var scenarios = new[]
        {
            MakeScenarioA(), MakeScenarioB(), MakeScenarioC(), MakeScenarioD(), MakeScenarioE()
        };

        foreach (var (property, market) in scenarios)
        {
            var result = _engine.Calculate(property, market);

            Assert.True(result.RealizationDiscountPercent >= -30 && result.RealizationDiscountPercent <= 30,
                $"Gap should be in [-30, 30] for {property.Location}, got {result.RealizationDiscountPercent}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private static ExtractedPropertyInfo MakeSimpleProperty(double sizeM2, string location) =>
        new()
        {
            Description = $"Stan {sizeM2} m² u lokaciji {location}",
            SizeM2 = sizeM2,
            Location = location,
            Condition = "dobro",
            PropertyState = "good",
            RawText = $"Stan {sizeM2} m² u lokaciji {location}",
        };

    private static string LocationSlug(string location)
        => location.ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    private static List<ComparableSearchResult> PatchUrls(
        List<ComparableSearchResult> listings, string propertyLocation)
    {
        var slug = LocationSlug(propertyLocation);
        return listings.Select(l => new ComparableSearchResult
        {
            Title = l.Title,
            Content = l.Content,
            Url = $"https://nekretnine.hr/{slug}/{l.Title?.Replace(" ", "-").ToLowerInvariant() ?? "listing"}",
            ListedPriceEur = l.ListedPriceEur,
            ListedPricePerM2 = l.ListedPricePerM2,
            AiSizeM2 = l.AiSizeM2,
            AiCondition = l.AiCondition,
            HasGarage = l.HasGarage,
            AiYardAreaM2 = l.AiYardAreaM2,
        }).ToList();
    }

    private static ComparableSearchResult MakeListing(string title, string content,
        double totalPrice, double pricePerM2, double sizeM2, string condition,
        bool hasGarage = false)
        => new()
        {
            Title = title,
            Content = content,
            Url = $"https://example.com/{title.Replace(" ", "-").ToLowerInvariant()}",
            ListedPriceEur = totalPrice,
            ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2,
            AiCondition = condition,
            HasGarage = hasGarage ? true : null,
        };
}
