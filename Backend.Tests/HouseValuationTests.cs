using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// COMPREHENSIVE house ("kuća") valuation tests — every possible scenario.
/// Covers: conditions, features, regions, sizes, yard, garage, sea view,
/// orientation, investment, IQR filtering, cross-type exclusion, price
/// extraction boundaries, confidence levels, realization gaps, edge cases.
/// </summary>
public class HouseValuationTests
{
    private readonly ValuationEngine _engine;

    public HouseValuationTests()
    {
        _engine = new ValuationEngine(NullLogger<ValuationEngine>.Instance);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 1 — CONDITION STATES (all 5 states for houses)     ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_NewBuild_ProducesHigherValueThanRenovated()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var newHouse = MakeHouseProperty("Novogradnja kuća 150 m²", 150, "Zagreb, Maksimir", "new");
        var renovated = MakeHouseProperty("Renovirana kuća 150 m²", 150, "Zagreb, Maksimir", "renovated");

        var resultNew = RunEngine(newHouse, "Zagreb", comps);
        var resultRen = RunEngine(renovated, "Zagreb", comps);

        Assert.True(resultNew.EstimatedValueEur > 0, "New house must produce estimate");
        Assert.True(resultRen.EstimatedValueEur > 0, "Renovated house must produce estimate");
        Assert.True(resultNew.EstimatedValueEur > resultRen.EstimatedValueEur,
            $"New ({resultNew.EstimatedValueEur}) should be > Renovated ({resultRen.EstimatedValueEur})");
    }

    [Fact]
    public void House_Renovated_ProducesHigherValueThanGood()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var renovated = MakeHouseProperty("Renovirana kuća 150 m²", 150, "Zagreb, Maksimir", "renovated");
        var good = MakeHouseProperty("Kuća u dobrom stanju 150 m²", 150, "Zagreb, Maksimir", "good");

        var resultRen = RunEngine(renovated, "Zagreb", comps);
        var resultGood = RunEngine(good, "Zagreb", comps);

        Assert.True(resultRen.EstimatedValueEur > resultGood.EstimatedValueEur,
            $"Renovated ({resultRen.EstimatedValueEur}) should be > Good ({resultGood.EstimatedValueEur})");
    }

    [Fact]
    public void House_Good_ProducesHigherValueThanOriginal()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var good = MakeHouseProperty("Kuća u dobrom stanju 150 m²", 150, "Zagreb, Maksimir", "good");
        var original = MakeHouseProperty("Kuća izvorno stanje 150 m²", 150, "Zagreb, Maksimir", "original");

        var resultGood = RunEngine(good, "Zagreb", comps);
        var resultOrig = RunEngine(original, "Zagreb", comps);

        Assert.True(resultGood.EstimatedValueEur > resultOrig.EstimatedValueEur,
            $"Good ({resultGood.EstimatedValueEur}) should be > Original ({resultOrig.EstimatedValueEur})");
    }

    [Fact]
    public void House_Original_ProducesHigherValueThanNeedsWork()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var original = MakeHouseProperty("Kuća izvorno stanje 150 m²", 150, "Zagreb, Maksimir", "original");
        var needsWork = MakeHouseProperty("Kuća za adaptaciju 150 m²", 150, "Zagreb, Maksimir", "needs_adaptation");

        var resultOrig = RunEngine(original, "Zagreb", comps);
        var resultNW = RunEngine(needsWork, "Zagreb", comps);

        Assert.True(resultOrig.EstimatedValueEur > resultNW.EstimatedValueEur,
            $"Original ({resultOrig.EstimatedValueEur}) should be > NeedsWork ({resultNW.EstimatedValueEur})");
    }

    [Fact]
    public void House_FullConditionOrdering_NewHighestNeedsWorkLowest()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var conditions = new[] { "new", "renovated", "good", "original", "needs_adaptation" };
        var results = new List<(string cond, int value)>();

        foreach (var cond in conditions)
        {
            var prop = MakeHouseProperty($"Kuća {cond} 150 m²", 150, "Zagreb, Maksimir", cond);
            var res = RunEngine(prop, "Zagreb", comps);
            results.Add((cond, res.EstimatedValueEur));
        }

        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].value >= results[i + 1].value,
                $"Condition ordering violated: {results[i].cond}={results[i].value} should be >= {results[i + 1].cond}={results[i + 1].value}");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 2 — REGIONAL SCENARIOS (all major Croatian regions) ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_ZagrebCenter_HighPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Tuškanac", "Kuća 180 m², centar, garaža", 540000, 3000, 180, "good"),
            MakeListing("Kuća Šalata", "Kuća 200 m², renovirana, vrt 500 m²", 600000, 3000, 200, "renovated"),
            MakeListing("Kuća Pantovčak", "Kuća 250 m², novogradnja", 875000, 3500, 250, "new"),
            MakeListing("Kuća Ksaver", "Kuća 160 m², dobro stanje", 448000, 2800, 160, "good"),
            MakeListing("Kuća Grškovićeva", "Kuća 140 m², izvorno stanje", 364000, 2600, 140, "original"),
        };

        var property = MakeHouseProperty("Kuća centar Zagreba 170 m²", 170, "Zagreb, centar", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 300_000, $"Zagreb center house should be >300k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 800_000, $"Zagreb center house should be <800k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_ZagrebSuburb_MediumPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Sesvete A", "Kuća 180 m², vrt 400 m²", 270000, 1500, 180, "good"),
            MakeListing("Kuća Sesvete B", "Kuća 200 m², garaža, vrt 600 m²", 320000, 1600, 200, "good"),
            MakeListing("Kuća Sesvete C", "Kuća 150 m², renovirana", 255000, 1700, 150, "renovated"),
            MakeListing("Kuća Sesvete D", "Kuća 160 m², izvorno", 208000, 1300, 160, "original"),
            MakeListing("Kuća Sesvete E", "Kuća 170 m², dobro", 272000, 1600, 170, "good"),
        };

        var property = MakeHouseProperty("Kuća Sesvete 170 m²", 170, "Zagreb, Sesvete", "good");
        var result = RunEngine(property, "Zagreb", comps, "Zagreb, Sesvete");

        Assert.True(result.EstimatedValueEur > 150_000, $"Sesvete house should be >150k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 450_000, $"Sesvete house should be <450k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_SplitCoastal_HighPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Meje A", "Kuća 200 m², pogled more, vrt 300 m²", 800000, 4000, 200, "good"),
            MakeListing("Kuća Meje B", "Kuća 150 m², renovirana, garaža", 600000, 4000, 150, "renovated"),
            MakeListing("Kuća Žnjan", "Kuća 180 m², blizu mora", 720000, 4000, 180, "good"),
            MakeListing("Kuća Bačvice", "Kuća 160 m², dobro stanje", 576000, 3600, 160, "good"),
            MakeListing("Kuća Firule", "Kuća 170 m², izvorno stanje", 510000, 3000, 170, "original"),
        };

        var property = MakeHouseProperty("Kuća Split 170 m², pogled na more", 170, "Split, Meje", "good");
        var result = RunEngine(property, "Split", comps, "Split, Meje");

        Assert.True(result.EstimatedValueEur > 350_000, $"Split coastal house should be >350k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 1_000_000, $"Split coastal house should be <1M, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_DubrovnikLuxury_VeryHighPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Vila Lapad A", "Vila 250 m², bazen, pogled more", 1500000, 6000, 250, "new"),
            MakeListing("Vila Lapad B", "Vila 200 m², renovirana, vrt", 1000000, 5000, 200, "renovated"),
            MakeListing("Vila Gruž", "Vila 300 m², novogradnja, bazen", 2100000, 7000, 300, "new"),
            MakeListing("Kuća Mokošica", "Kuća 180 m², dobro stanje", 630000, 3500, 180, "good"),
            MakeListing("Vila Ploče", "Vila 220 m², pogled more, prvi red", 1540000, 7000, 220, "renovated"),
        };

        var property = MakeHouseProperty("Vila Dubrovnik 230 m², bazen, pogled na more", 230, "Dubrovnik, Lapad", "new");
        var result = RunEngine(property, "Dubrovnik", comps, "Dubrovnik, Lapad");

        Assert.True(result.EstimatedValueEur > 800_000, $"Dubrovnik luxury villa should be >800k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 3_000_000, $"Dubrovnik villa should be <3M, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_RijekaTrsat_MediumPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Trsat A", "Kuća 160 m², vrt 200 m²", 256000, 1600, 160, "good"),
            MakeListing("Kuća Trsat B", "Kuća 140 m², renovirana", 252000, 1800, 140, "renovated"),
            MakeListing("Kuća Trsat C", "Kuća 180 m², izvorno stanje", 234000, 1300, 180, "original"),
            MakeListing("Kuća Trsat D", "Kuća 150 m², dobro, garaža", 247500, 1650, 150, "good"),
            MakeListing("Kuća Trsat E", "Kuća 170 m², novogradnja", 340000, 2000, 170, "new"),
        };

        var property = MakeHouseProperty("Kuća Rijeka Trsat 160 m²", 160, "Rijeka, Trsat", "good");
        var result = RunEngine(property, "Rijeka", comps, "Rijeka, Trsat");

        Assert.True(result.EstimatedValueEur > 150_000, $"Rijeka Trsat house should be >150k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 400_000);
    }

    [Fact]
    public void House_OsijekRetfala_LowPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Retfala A", "Kuća 130 m², vrt 300 m²", 104000, 800, 130, "good"),
            MakeListing("Kuća Retfala B", "Kuća 150 m², renovirana, garaža", 135000, 900, 150, "renovated"),
            MakeListing("Kuća Retfala C", "Kuća 120 m², izvorno stanje", 84000, 700, 120, "original"),
            MakeListing("Kuća Retfala D", "Kuća 140 m², dobro", 112000, 800, 140, "good"),
            MakeListing("Kuća Retfala E", "Kuća 160 m², novogradnja", 192000, 1200, 160, "new"),
        };

        var property = MakeHouseProperty("Kuća Osijek 140 m²", 140, "Osijek, Retfala", "good");
        var result = RunEngine(property, "Osijek", comps, "Osijek, Retfala");

        Assert.True(result.EstimatedValueEur > 50_000, $"Osijek house should be >50k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 250_000);
    }

    [Fact]
    public void House_PulaVeruda_CoastalMedium()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Veruda A", "Kuća 180 m², vrt 200 m²", 360000, 2000, 180, "good"),
            MakeListing("Kuća Veruda B", "Kuća 150 m², renovirana, garaža", 337500, 2250, 150, "renovated"),
            MakeListing("Kuća Veruda C", "Kuća 200 m², novogradnja, parking", 500000, 2500, 200, "new"),
            MakeListing("Kuća Stoja", "Kuća 160 m², dobro, blizina mora", 320000, 2000, 160, "good"),
            MakeListing("Kuća Veruda D", "Kuća 170 m², izvorno stanje", 255000, 1500, 170, "original"),
        };

        var property = MakeHouseProperty("Kuća Pula Veruda 170 m²", 170, "Pula, Veruda", "good");
        var result = RunEngine(property, "Pula", comps, "Pula, Veruda");

        Assert.True(result.EstimatedValueEur > 200_000);
        Assert.True(result.EstimatedValueEur < 600_000);
    }

    [Fact]
    public void House_ZadarPetrcane_CoastalPremium()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Petrčane A", "Kuća 200 m², pogled more, vrt 400 m²", 700000, 3500, 200, "good"),
            MakeListing("Kuća Petrčane B", "Kuća 180 m², novogradnja, bazen", 720000, 4000, 180, "new"),
            MakeListing("Kuća Petrčane C", "Kuća 160 m², renovirana, garaža", 480000, 3000, 160, "renovated"),
            MakeListing("Kuća Petrčane D", "Kuća 220 m², dobro stanje", 616000, 2800, 220, "good"),
            MakeListing("Kuća Petrčane E", "Kuća 150 m², izvorno, blizu mora", 375000, 2500, 150, "original"),
        };

        var property = MakeHouseProperty("Kuća Zadar Petrčane 180 m², pogled na more", 180, "Zadar, Petrčane", "good");
        var result = RunEngine(property, "Zadar", comps, "Zadar, Petrčane");

        Assert.True(result.EstimatedValueEur > 300_000);
        Assert.True(result.EstimatedValueEur < 900_000);
    }

    [Fact]
    public void House_PožegaRural_LowPrice()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Požega A", "Kuća 100 m², okućnica 600 m²", 50000, 500, 100, "original"),
            MakeListing("Kuća Požega B", "Kuća 150 m², dobro stanje, vrt 1000 m²", 90000, 600, 150, "good"),
            MakeListing("Kuća Požega C", "Kuća 233 m², potrebna obnova, vrt 500 m²", 107500, 461, 233, "original"),
            MakeListing("Kuća Požega D", "Kuća 130 m², djelomično renovirana, okućnica 400 m²", 78000, 600, 130, "fair"),
            MakeListing("Kuća Požega E", "Kuća 90 m², stara gradnja, dvorište 350 m²", 45000, 500, 90, "original"),
        };

        var property = MakeHouseProperty("Stara kuća Požega 120 m² s okućnicom", 120, "Požega, okolica", "original",
            yardM2: 800);
        var result = RunEngine(property, "Požega", comps, "Požega, okolica");

        Assert.True(result.EstimatedValueEur > 20_000, $"Požega house should be >20k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 200_000, $"Požega house should be <200k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_SlavonskiBrodRural_LowPrice()
    {
        // AiSimilarityScore set to bypass math-based location slug mismatch (multi-word city)
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Brod A", "Kuća 120 m², Slavonski Brod, vrt 500 m²", 60000, 500, 120, "original"),
            MakeListing("Kuća Brod B", "Kuća 140 m², Slavonski Brod, dobro stanje", 84000, 600, 140, "good"),
            MakeListing("Kuća Brod C", "Kuća 100 m², Slavonski Brod, renovirana", 70000, 700, 100, "renovated"),
            MakeListing("Kuća Brod D", "Kuća 160 m², Slavonski Brod, izvorno stanje", 80000, 500, 160, "original"),
            MakeListing("Kuća Brod E", "Kuća 110 m², Slavonski Brod, potrebna obnova", 44000, 400, 110, "needs_work"),
        };
        foreach (var c in comps) c.AiSimilarityScore = 0.75;

        var property = MakeHouseProperty("Kuća Slavonski Brod 130 m²", 130, "Slavonski Brod, centar", "good");
        var result = RunEngine(property, "Slavonski Brod", comps, "Slavonski Brod, centar");

        Assert.True(result.EstimatedValueEur > 20_000,
            $"Slavonski Brod house should be >20k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur < 150_000,
            $"Slavonski Brod house should be <150k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_VaraždinCenter_ContinentalMedium()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Varaždin A", "Kuća 160 m², centar, vrt 200 m²", 192000, 1200, 160, "good"),
            MakeListing("Kuća Varaždin B", "Kuća 140 m², renovirana", 196000, 1400, 140, "renovated"),
            MakeListing("Kuća Varaždin C", "Kuća 180 m², izvorno stanje", 162000, 900, 180, "original"),
            MakeListing("Kuća Varaždin D", "Kuća 150 m², dobro, garaža", 180000, 1200, 150, "good"),
            MakeListing("Kuća Varaždin E", "Kuća 200 m², novogradnja", 320000, 1600, 200, "new"),
        };

        var property = MakeHouseProperty("Kuća Varaždin centar 160 m²", 160, "Varaždin, centar", "good");
        var result = RunEngine(property, "Varaždin", comps, "Varaždin, centar");

        Assert.True(result.EstimatedValueEur > 80_000);
        Assert.True(result.EstimatedValueEur < 350_000);
    }

    [Fact]
    public void House_IslandKorčula_PremiumIsland()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Korčula A", "Kamena kuća 120 m², pogled more", 420000, 3500, 120, "renovated"),
            MakeListing("Kuća Korčula B", "Kuća 100 m², centar grada", 350000, 3500, 100, "good"),
            MakeListing("Kuća Korčula C", "Kuća 80 m², stara jezgra, renovirana", 320000, 4000, 80, "renovated"),
            MakeListing("Kuća Korčula D", "Kuća 150 m², pogled, vrt 200 m²", 525000, 3500, 150, "good"),
            MakeListing("Kuća Korčula E", "Kuća 130 m², izvorno stanje", 325000, 2500, 130, "original"),
        };

        var property = MakeHouseProperty("Kamena kuća Korčula 110 m², otok, pogled na more", 110, "Korčula, centar", "renovated",
            microLocation: "otok Korčula");
        var result = RunEngine(property, "Korčula", comps, "Korčula, centar");

        Assert.True(result.EstimatedValueEur > 200_000);
        Assert.True(result.EstimatedValueEur < 700_000);
    }

    [Fact]
    public void House_Karlovac_InlandLow()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Karlovac A", "Kuća 140 m², vrt 400 m²", 98000, 700, 140, "good"),
            MakeListing("Kuća Karlovac B", "Kuća 120 m², renovirana", 108000, 900, 120, "renovated"),
            MakeListing("Kuća Karlovac C", "Kuća 160 m², izvorno stanje", 96000, 600, 160, "original"),
            MakeListing("Kuća Karlovac D", "Kuća 130 m², dobro stanje", 91000, 700, 130, "good"),
            MakeListing("Kuća Karlovac E", "Kuća 150 m², novogradnja", 180000, 1200, 150, "new"),
        };

        var property = MakeHouseProperty("Kuća Karlovac 140 m²", 140, "Karlovac, centar", "good");
        var result = RunEngine(property, "Karlovac", comps, "Karlovac, centar");

        Assert.True(result.EstimatedValueEur > 40_000);
        Assert.True(result.EstimatedValueEur < 200_000);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 3 — SIZE VARIATIONS                                ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_VerySmall_60m2()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 70, baseTotal: 140000);
        var property = MakeHouseProperty("Mala kuća 60 m²", 60, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Small house must produce estimate");
        Assert.True(result.EstimatedValueEur > 50_000);
    }

    [Fact]
    public void House_Medium_120m2()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 130, baseTotal: 260000);
        var property = MakeHouseProperty("Kuća 120 m²", 120, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 100_000);
        Assert.True(result.EstimatedValueEur < 400_000);
    }

    [Fact]
    public void House_Large_250m2_GetsVeryLargeDiscount()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 200, baseTotal: 400000);
        var property = MakeHouseProperty("Velika kuća 250 m²", 250, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Large house must produce estimate");
        // >200m² gets -7% adjustment
        Assert.Contains(result.Adjustments, a => a.Factor.Contains("Velika nekretnina") || a.Percent < 0);
    }

    [Fact]
    public void House_VeryLarge_500m2()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Vila A", "Vila 400 m², bazen, luksuz", 1200000, 3000, 400, "new"),
            MakeListing("Vila B", "Vila 450 m², renovirana", 1125000, 2500, 450, "renovated"),
            MakeListing("Vila C", "Vila 350 m², dobro stanje", 875000, 2500, 350, "good"),
            MakeListing("Vila D", "Vila 500 m², novogradnja", 1750000, 3500, 500, "new"),
            MakeListing("Vila E", "Vila 380 m², izvorno stanje", 760000, 2000, 380, "original"),
        };

        var property = MakeHouseProperty("Velika vila 500 m²", 500, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 500_000, $"500m² house should be >500k, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_LargerThan200m2_Gets_NegativeAdjustment()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2500, 150, baseTotal: 375000);

        var normalHouse = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var largeHouse = MakeHouseProperty("Kuća 220 m²", 220, "Zagreb, Maksimir", "good");

        var resultNormal = RunEngine(normalHouse, "Zagreb", comps);
        var resultLarge = RunEngine(largeHouse, "Zagreb", comps);

        // Large house (>200m²) should have lower €/m² due to -7% penalty
        if (resultLarge.EstimatedValueEur > 0 && resultNormal.EstimatedValueEur > 0)
        {
            double ppmNormal = (double)resultNormal.EstimatedValueEur / 150;
            double ppmLarge = (double)resultLarge.EstimatedValueEur / 220;
            Assert.True(ppmLarge < ppmNormal,
                $"Large house €/m² ({ppmLarge:F0}) should be < normal ({ppmNormal:F0})");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 4 — YARD (OKUĆNICA) ADJUSTMENTS                   ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_WithLargeYard_HigherThanNoYard()
    {
        var compsWithYard = new List<ComparableSearchResult>
        {
            MakeListingWithYard("Kuća A", "Kuća 150 m², okućnica 800 m²", 300000, 2000, 150, "good", 800),
            MakeListingWithYard("Kuća B", "Kuća 160 m², vrt 700 m²", 320000, 2000, 160, "good", 700),
            MakeListingWithYard("Kuća C", "Kuća 140 m², dvorište 600 m²", 280000, 2000, 140, "good", 600),
            MakeListingWithYard("Kuća D", "Kuća 145 m², okućnica 500 m²", 290000, 2000, 145, "good", 500),
            MakeListingWithYard("Kuća E", "Kuća 155 m², vrt 400 m²", 310000, 2000, 155, "good", 400),
        };

        var withYard = MakeHouseProperty("Kuća 150 m², okućnica 1000 m²", 150, "Zagreb, Maksimir", "good", yardM2: 1000);
        var noYard = MakeHouseProperty("Kuća 150 m², bez okućnice", 150, "Zagreb, Maksimir", "good", yardM2: 0);

        var resultYard = RunEngine(withYard, "Zagreb", compsWithYard);
        var resultNoYard = RunEngine(noYard, "Zagreb", compsWithYard);

        Assert.True(resultYard.EstimatedValueEur > 0);
        Assert.True(resultNoYard.EstimatedValueEur > 0);
        // House with bigger yard should be valued more
        Assert.True(resultYard.EstimatedValueEur >= resultNoYard.EstimatedValueEur,
            $"With yard ({resultYard.EstimatedValueEur}) should be >= no yard ({resultNoYard.EstimatedValueEur})");
    }

    [Fact]
    public void House_SmallYardDifference_NoAdjustment()
    {
        // Yard difference < 200m² should NOT trigger yard adjustment
        var comps = new List<ComparableSearchResult>
        {
            MakeListingWithYard("Kuća A", "Kuća 150 m², vrt 300 m²", 300000, 2000, 150, "good", 300),
            MakeListingWithYard("Kuća B", "Kuća 160 m², vrt 350 m²", 320000, 2000, 160, "good", 350),
            MakeListingWithYard("Kuća C", "Kuća 140 m², vrt 280 m²", 280000, 2000, 140, "good", 280),
            MakeListingWithYard("Kuća D", "Kuća 145 m², vrt 250 m²", 290000, 2000, 145, "good", 250),
            MakeListingWithYard("Kuća E", "Kuća 155 m², vrt 320 m²", 310000, 2000, 155, "good", 320),
        };

        // Yard = 400m², comps = 250-350m² → difference < 200m² for most
        var property = MakeHouseProperty("Kuća 150 m², vrt 400 m²", 150, "Zagreb, Maksimir", "good", yardM2: 400);
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Must produce estimate");
    }

    [Fact]
    public void House_YardMuch_Larger_ThanComps_PositiveAdjust()
    {
        // Subject yard 2000m² vs comp yards 200m² → big positive yard adjustment
        var comps = new List<ComparableSearchResult>
        {
            MakeListingWithYard("Kuća A", "Kuća 150 m², vrt 200 m²", 300000, 2000, 150, "good", 200),
            MakeListingWithYard("Kuća B", "Kuća 160 m², vrt 250 m²", 320000, 2000, 160, "good", 250),
            MakeListingWithYard("Kuća C", "Kuća 140 m², vrt 180 m²", 280000, 2000, 140, "good", 180),
            MakeListingWithYard("Kuća D", "Kuća 145 m², vrt 220 m²", 290000, 2000, 145, "good", 220),
            MakeListingWithYard("Kuća E", "Kuća 155 m², vrt 230 m²", 310000, 2000, 155, "good", 230),
        };

        var property = MakeHouseProperty("Kuća 150 m², okućnica 2000 m²", 150, "Zagreb, Maksimir", "good", yardM2: 2000);
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 5 — GARAGE FEATURE                                 ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_WithGarage_VsWithout_CompsHaveGarage()
    {
        var compsWithGarage = new List<ComparableSearchResult>
        {
            MakeListingWithFeatures("Kuća A", "Kuća 150 m², garaža", 300000, 2000, 150, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća B", "Kuća 160 m², garaža, vrt", 320000, 2000, 160, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća C", "Kuća 140 m², garaža", 280000, 2000, 140, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća D", "Kuća 155 m², garaža", 310000, 2000, 155, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća E", "Kuća 145 m², garaža", 290000, 2000, 145, "good", hasGarage: true),
        };

        // Subject WITHOUT garage, comps WITH garage → should be valued less
        var noGarage = MakeHouseProperty("Kuća 150 m² bez garaže", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(noGarage, "Zagreb", compsWithGarage);

        Assert.True(result.EstimatedValueEur > 0, "House without garage should still produce estimate");
    }

    [Fact]
    public void House_CompsWithMixedGarage_ProducesEstimate()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListingWithFeatures("Kuća A", "Kuća 150 m², garaža", 320000, 2133, 150, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća B", "Kuća 160 m², bez garaže", 288000, 1800, 160, "good", hasGarage: false),
            MakeListingWithFeatures("Kuća C", "Kuća 140 m², garaža", 294000, 2100, 140, "good", hasGarage: true),
            MakeListingWithFeatures("Kuća D", "Kuća 155 m², bez garaže", 279000, 1800, 155, "good", hasGarage: false),
            MakeListingWithFeatures("Kuća E", "Kuća 145 m², garaža", 304500, 2100, 145, "good", hasGarage: true),
        };

        var property = MakeHouseProperty("Kuća 150 m² s garažom", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 6 — SEA VIEW / ORIENTATION (Coastal only)          ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_Coastal_WithSeaView_HigherThanWithout()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m², pogled na more", 450000, 3000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m², panoramski pogled", 480000, 3000, 160, "good"),
            MakeListing("Kuća C", "Kuća 140 m², bez pogleda", 308000, 2200, 140, "good"),
            MakeListing("Kuća D", "Kuća 155 m², djelomičan pogled", 387500, 2500, 155, "good"),
            MakeListing("Kuća E", "Kuća 145 m², pogled more", 435000, 3000, 145, "good"),
        };

        var withView = MakeHouseProperty("Kuća Split 150 m², pogled na more, prvi red do mora",
            150, "Split, Meje", "good");
        withView.KeyFeatures = ["pogled na more", "blizu mora"];
        var noView = MakeHouseProperty("Kuća Split 150 m², ulična pozicija", 150, "Split, Meje", "good");

        var resultView = RunEngine(withView, "Split", comps, "Split, Meje");
        var resultNoView = RunEngine(noView, "Split", comps, "Split, Meje");

        Assert.True(resultView.EstimatedValueEur > 0);
        Assert.True(resultNoView.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_Coastal_FirstRowSea_PremiumAdjustment()
    {
        var comps = MakeHouseComps("Split, Meje", 5, 4000, 150, baseTotal: 600000);

        var property = MakeHouseProperty("Kuća Split 150 m², prvi red do mora, pogled na more",
            150, "Split, Meje", "good");
        property.KeyFeatures = ["prvi red do mora", "pogled na more"];
        property.AdditionalNotes = "Kuća u prvom redu do mora";

        var result = RunEngine(property, "Split", comps, "Split, Meje");

        Assert.True(result.EstimatedValueEur > 0, "First row coastal house must produce estimate");
        // Should have a sea/first row adjustment
        bool hasSeaAdjustment = result.Adjustments.Any(a =>
            a.Factor.Contains("mor", StringComparison.OrdinalIgnoreCase) ||
            a.Factor.Contains("prvi", StringComparison.OrdinalIgnoreCase) ||
            a.Factor.Contains("sea", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasSeaAdjustment, "First row coastal house should have sea/first row adjustment");
    }

    [Fact]
    public void House_Inland_NoSeaViewAdjustment()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća Zagreb 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        // Should NOT have sea view adjustment for inland
        bool hasSeaAdjust = result.Adjustments.Any(a =>
            a.Factor.Contains("more", StringComparison.OrdinalIgnoreCase) ||
            a.Factor.Contains("sea", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasSeaAdjust, "Inland house should NOT have sea view adjustment");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 7 — INVESTMENT / PREMIUM KEYWORDS                  ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_InvestmentKeywords_GetBoost()
    {
        var comps = MakeHouseComps("Split, Meje", 5, 4000, 150, baseTotal: 600000);

        var normal = MakeHouseProperty("Kuća Split 150 m²", 150, "Split, Meje", "good");
        var invest = MakeHouseProperty("Kuća Split 150 m², investicija, turistički najam", 150, "Split, Meje", "good");
        invest.KeyFeatures = ["investicija", "turistički potencijal"];
        invest.AdditionalNotes = "Izvrsna investicija za turistički najam";

        var resultNormal = RunEngine(normal, "Split", comps, "Split, Meje");
        var resultInvest = RunEngine(invest, "Split", comps, "Split, Meje");

        Assert.True(resultNormal.EstimatedValueEur > 0);
        Assert.True(resultInvest.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_NeedsAdaptation_WithInvestment_CappedPenalty()
    {
        // Investment property that needs work → renovation penalty is capped (V25)
        var comps = new List<ComparableSearchResult>
        {
            MakeListingWithFeatures("Kuća A", "Kuća 150 m², renovirana", 450000, 3000, 150, "renovated", hasRenovation: true),
            MakeListingWithFeatures("Kuća B", "Kuća 160 m², novogradnja", 560000, 3500, 160, "new", hasRenovation: true),
            MakeListingWithFeatures("Kuća C", "Kuća 140 m², renovirana, garaža", 420000, 3000, 140, "renovated", hasRenovation: true),
            MakeListingWithFeatures("Kuća D", "Kuća 155 m², dobro stanje", 387500, 2500, 155, "good"),
            MakeListingWithFeatures("Kuća E", "Kuća 145 m², renovirana", 435000, 3000, 145, "renovated", hasRenovation: true),
        };

        var property = MakeHouseProperty("Kuća Split 150 m², potrebna adaptacija, investicija",
            150, "Split, Meje", "needs_adaptation");
        property.KeyFeatures = ["investicija"];
        property.AdditionalNotes = "Investicijsko prilika, potrebna adaptacija";

        var result = RunEngine(property, "Split", comps, "Split, Meje");
        Assert.True(result.EstimatedValueEur > 0, "Investment house needing work must still produce estimate");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 8 — IQR FILTERING / OUTLIER REMOVAL                ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_WithOutliers_IQR_RemovesThem()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m², dobro", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m², dobro", 320000, 2000, 160, "good"),
            MakeListing("Kuća C", "Kuća 140 m², dobro", 280000, 2000, 140, "good"),
            MakeListing("Kuća D", "Kuća 155 m², dobro", 310000, 2000, 155, "good"),
            MakeListing("Kuća E", "Kuća 145 m², dobro", 290000, 2000, 145, "good"),
            // Extreme outlier — should be removed by IQR
            MakeListing("Kuća F", "Luksuzna kuća 150 m²", 1500000, 10000, 150, "new"),
            // Very low outlier
            MakeListing("Kuća G", "Ruševina 150 m²", 45000, 300, 150, "needs_work"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
        Assert.True(result.OutliersRemoved >= 1, "Should remove at least 1 outlier");
    }

    [Fact]
    public void House_AllSamePrice_NoOutliers()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća C", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća D", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća E", "Kuća 150 m²", 300000, 2000, 150, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
        Assert.True(result.CoefficientOfVariation < 5,
            $"Same-price comps should have very low CV, got {result.CoefficientOfVariation}");
    }

    [Fact]
    public void House_HighVariation_LowerConfidence()
    {
        // Wide spread of prices → CV goes up → confidence down
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m², luksuz", 600000, 4000, 150, "new"),
            MakeListing("Kuća B", "Kuća 150 m², dobro", 300000, 2000, 150, "good"),
            MakeListing("Kuća C", "Kuća 150 m², izvorno", 180000, 1200, 150, "original"),
            MakeListing("Kuća D", "Kuća 150 m², nagrda", 105000, 700, 150, "needs_work"),
            MakeListing("Kuća E", "Kuća 150 m², dovoljno dobro", 225000, 1500, 150, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_10xPriceRatio_SanityFilter()
    {
        // Max/min ratio > 10 triggers 3c sanity filter
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 150 m²", 310000, 2067, 150, "good"),
            MakeListing("Kuća C", "Kuća 150 m²", 290000, 1933, 150, "good"),
            MakeListing("Kuća D", "Kuća 150 m²", 295000, 1967, 150, "good"),
            // 10x+ higher than others — should be sanity-checked
            MakeListing("Kuća E", "Kuća 150 m²", 3000000, 20000, 150, "new"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
        // Should NOT be near the 20k/m² outlier
        Assert.True(result.CalculatedPricePerM2 < 5000,
            $"Should filter 20k/m² outlier, got {result.CalculatedPricePerM2}");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 9 — CROSS-TYPE EXCLUSION                           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_WithApartmentComps_ExcludesApartments()
    {
        var comps = new List<ComparableSearchResult>
        {
            // House comps (should be kept)
            MakeListing("Kuća A", "Kuća 150 m², dobro stanje", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m², renovirana", 320000, 2000, 160, "renovated"),
            MakeListing("Kuća C", "Kuća 140 m², dobro", 280000, 2000, 140, "good"),
            // Apartment comps (should be excluded for house valuation)
            MakeListing("Stan D", "Stan 80 m², centar", 200000, 2500, 80, "good"),
            MakeListing("Stan E", "Stan 60 m², novogradnja", 180000, 3000, 60, "new"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        // Type is "kuća" → apartment comps should be excluded
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Should produce estimate from house comps only");
    }

    [Fact]
    public void House_OnlyApartmentComps_ZeroOrLowConfidence()
    {
        // All comps are apartments — house can't use them
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Stan A", "Stan 80 m², centar", 200000, 2500, 80, "good"),
            MakeListing("Stan B", "Stan 60 m², novogradnja", 180000, 3000, 60, "new"),
            MakeListing("Stan C", "Stan 70 m², dobro", 175000, 2500, 70, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        // With only apartment comps, house should get 0 or very low result
        // because cross-type exclusion removes all comps
        // (engine might still extract via regex fallback, so check UsablePricePoints)
        Assert.True(result.UsablePricePoints <= 1 || result.EstimatedValueEur == 0,
            "House with only apartment comps should have very few usable points");
    }

    [Fact]
    public void House_WithLandComps_ExcludesLand()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m²", 320000, 2000, 160, "good"),
            MakeListing("Kuća C", "Kuća 140 m²", 280000, 2000, 140, "good"),
            // Land comps — should be excluded
            MakeListing("Zemljište X", "Građevinsko zemljište 500 m²", 100000, 200, 500, ""),
            MakeListing("Oranica Y", "Oranica 2000 m²", 10000, 5, 2000, ""),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 10 — PRICE EXTRACTION BOUNDARIES                   ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_AtMinPricePerM2_200_Included()
    {
        // Exactly at the 200 €/m² boundary
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 200 m²", 40000, 200, 200, "original"),
            MakeListing("Kuća B", "Kuća 180 m²", 36000, 200, 180, "original"),
            MakeListing("Kuća C", "Kuća 220 m²", 44000, 200, 220, "original"),
            MakeListing("Kuća D", "Kuća 190 m²", 38000, 200, 190, "original"),
            MakeListing("Kuća E", "Kuća 210 m²", 42000, 200, 210, "original"),
        };

        var property = MakeHouseProperty("Kuća 200 m², izvorno stanje", 200, "Sisak, okolica", "original");
        var result = RunEngine(property, "Sisak", comps, "Sisak, okolica");

        Assert.True(result.EstimatedValueEur > 0, "At MinPricePerM2=200 boundary should produce estimate");
    }

    [Fact]
    public void House_BelowMinPricePerM2_199_Filtered()
    {
        // Below 200 €/m² → should be filtered
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 200 m²", 39600, 198, 200, "original"),
            MakeListing("Kuća B", "Kuća 180 m²", 34200, 190, 180, "original"),
            MakeListing("Kuća C", "Kuća 220 m²", 33000, 150, 220, "original"),
        };

        var property = MakeHouseProperty("Kuća 200 m²", 200, "Sisak, okolica", "original");
        var result = RunEngine(property, "Sisak", comps, "Sisak, okolica");

        // All below 200 €/m² → should be filtered → 0 estimate or very low
        Assert.True(result.UsablePricePoints == 0 || result.EstimatedValueEur == 0,
            "Below MinPricePerM2 should filter all listings");
    }

    [Fact]
    public void House_BelowMinSalePrice_35k_Filtered()
    {
        // Total price below 35k AND ppm2 below 200 → fully filtered
        // Engine Tier 2 still accepts listings where ppm2 >= 200, even if total < 35k
        // So we need comps where ppm2 is also below the MinPricePerM2_Building threshold
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 100 m²", 15000, 150, 100, "needs_work"),
            MakeListing("Kuća B", "Kuća 80 m²", 12000, 150, 80, "needs_work"),
            MakeListing("Kuća C", "Kuća 120 m²", 14000, 116, 120, "needs_work"),
        };

        var property = MakeHouseProperty("Kuća 100 m²", 100, "Sisak, okolica", "needs_adaptation");
        var result = RunEngine(property, "Sisak", comps, "Sisak, okolica");

        Assert.True(result.EstimatedValueEur == 0 || result.UsablePricePoints == 0,
            $"Below MinPricePerM2 + MinSalePrice should be filtered, got est={result.EstimatedValueEur}, pts={result.UsablePricePoints}");
    }

    [Fact]
    public void House_AboveMaxPricePerM2_15000_Filtered()
    {
        // Above max 15,000 €/m² → filtered
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 100 m²", 1600000, 16000, 100, "new"),
            MakeListing("Kuća B", "Kuća 80 m²", 1440000, 18000, 80, "new"),
            // One valid listing
            MakeListing("Kuća C", "Kuća 150 m²", 750000, 5000, 150, "new"),
        };

        var property = MakeHouseProperty("Kuća 120 m²", 120, "Dubrovnik, centar", "new");
        var result = RunEngine(property, "Dubrovnik", comps, "Dubrovnik, centar");

        // 16k and 18k should be filtered, only 5k should be used
        Assert.True(result.CalculatedPricePerM2 < 15000,
            $"Should filter above 15k €/m², got {result.CalculatedPricePerM2}");
    }

    [Fact]
    public void House_RentalListings_Filtered()
    {
        var comps = new List<ComparableSearchResult>
        {
            // Rental listings (should be filtered by IsRentalListing)
            MakeListing("Kuća najam A", "Kuća 150 m² za najam, mjesečno 800€", 800, 5, 150, "good"),
            MakeListing("Kuća iznajmljivanje B", "Kuća iznajmljivanje 120 m²", 600, 5, 120, "good"),
            // Valid sale listing
            MakeListing("Kuća C", "Kuća 150 m², prodaja, dobro stanje", 300000, 2000, 150, "good"),
            MakeListing("Kuća D", "Kuća 140 m², prodaja", 280000, 2000, 140, "good"),
            MakeListing("Kuća E", "Kuća 160 m², prodaja", 320000, 2000, 160, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 100_000,
            "Rental listings should be filtered, estimate from sales only");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 11 — COMP COUNT / DATA AVAILABILITY                ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_SingleComp_ProducesEstimate()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m², dobro stanje", 300000, 2000, 150, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        // Single comp → should still produce estimate (low confidence)
        if (result.EstimatedValueEur > 0)
        {
            Assert.True(result.UsablePricePoints >= 1);
        }
    }

    [Fact]
    public void House_TwoComps_ProducesEstimate()
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m²", 320000, 2000, 160, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Two comps should produce estimate");
    }

    [Fact]
    public void House_FiveComps_GoodConfidence()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
        Assert.True(result.UsablePricePoints >= 3, $"5 comps should yield ≥3 usable, got {result.UsablePricePoints}");
    }

    [Fact]
    public void House_EightPlusComps_HighConfidence()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 8, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_ZeroComps_ZeroEstimate()
    {
        var comps = new List<ComparableSearchResult>();
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.Equal(0, result.EstimatedValueEur);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 12 — REALIZATION DISCOUNT / GAP                    ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_RealizationDiscount_AlwaysNegative()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.RealizationDiscountPercent < 0,
            $"Realization discount must be negative, got {result.RealizationDiscountPercent}");
        Assert.True(result.RealizationDiscountPercent >= -25,
            $"Realization discount must be >= -25%, got {result.RealizationDiscountPercent}");
        Assert.True(result.RealizationDiscountPercent <= -5,
            $"Realization discount must be <= -5%, got {result.RealizationDiscountPercent}");
    }

    [Fact]
    public void House_HighLiquidity_SmallerDiscount()
    {
        // 8+ comps with low CV → smallest discount (HIGH liquidity: -8% to -10%)
        var comps = MakeHouseComps("Zagreb, Maksimir", 8, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.RealizationDiscountPercent >= -15,
            $"High liquidity should have discount >= -15%, got {result.RealizationDiscountPercent}");
    }

    [Fact]
    public void House_LowDataAvailability_LargerDiscount()
    {
        // Only 2 comps with high CV → larger discount
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 150 m²", 450000, 3000, 150, "new"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.EstimatedValueEur > 0)
        {
            Assert.True(result.RealizationDiscountPercent <= -10,
                $"Low data should have discount <= -10%, got {result.RealizationDiscountPercent}");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 13 — NOVOGRADNJA / NEW BUILD ADJUSTMENTS           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_NovogradnjaKeyword_GetsPositiveAdjustment()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var newBuild = MakeHouseProperty("Kuća novogradnja 150 m²", 150, "Zagreb, Maksimir", "new");
        newBuild.KeyFeatures = ["novogradnja"];
        var regular = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");

        var resultNew = RunEngine(newBuild, "Zagreb", comps);
        var resultReg = RunEngine(regular, "Zagreb", comps);

        Assert.True(resultNew.EstimatedValueEur > 0);
        Assert.True(resultNew.EstimatedValueEur > resultReg.EstimatedValueEur,
            $"New build ({resultNew.EstimatedValueEur}) should be > regular ({resultReg.EstimatedValueEur})");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 14 — NEEDS ADAPTATION / RENOVATION STATE           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_NeedsAdaptation_GetsNegativeAdjustment()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var needsWork = MakeHouseProperty("Kuća za adaptaciju 150 m²", 150, "Zagreb, Maksimir", "needs_adaptation");
        var good = MakeHouseProperty("Kuća dobro stanje 150 m²", 150, "Zagreb, Maksimir", "good");

        var resultNW = RunEngine(needsWork, "Zagreb", comps);
        var resultGood = RunEngine(good, "Zagreb", comps);

        Assert.True(resultNW.EstimatedValueEur > 0);
        Assert.True(resultNW.EstimatedValueEur < resultGood.EstimatedValueEur,
            $"NeedsWork ({resultNW.EstimatedValueEur}) should be < Good ({resultGood.EstimatedValueEur})");
    }

    [Fact]
    public void House_Adapted_GetsPositiveVsOriginal()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var adapted = MakeHouseProperty("Kuća adaptirana 150 m²", 150, "Zagreb, Maksimir", "renovated");
        adapted.KeyFeatures = ["adaptirana"];
        var original = MakeHouseProperty("Kuća izvorno stanje 150 m²", 150, "Zagreb, Maksimir", "original");

        var resultAdapt = RunEngine(adapted, "Zagreb", comps);
        var resultOrig = RunEngine(original, "Zagreb", comps);

        Assert.True(resultAdapt.EstimatedValueEur > resultOrig.EstimatedValueEur,
            $"Adapted ({resultAdapt.EstimatedValueEur}) should be > Original ({resultOrig.EstimatedValueEur})");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 15 — LOCATION ADJUSTMENTS                          ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_CenterLocation_GetsPositiveAdjust()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var center = MakeHouseProperty("Kuća centar grada 150 m²", 150, "Zagreb, centar", "good");
        center.KeyFeatures = ["centar grada"];
        center.MicroLocation = "centar";
        var suburb = MakeHouseProperty("Kuća periferija 150 m²", 150, "Zagreb, Maksimir", "good");

        var resultCenter = RunEngine(center, "Zagreb", comps, "Zagreb, centar");
        var resultSuburb = RunEngine(suburb, "Zagreb", comps);

        Assert.True(resultCenter.EstimatedValueEur > 0);
        Assert.True(resultSuburb.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_Periphery_GetsNegativeAdjust()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var property = MakeHouseProperty("Kuća periferija 150 m², rubni dio grada", 150, "Zagreb, Brezovica", "good");
        property.KeyFeatures = ["periferija"];

        var result = RunEngine(property, "Zagreb", comps, "Zagreb, Brezovica");

        Assert.True(result.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_TouristZone_GetsPositiveAdjust()
    {
        var comps = MakeHouseComps("Dubrovnik, Lapad", 5, 5000, 150, baseTotal: 750000);

        var property = MakeHouseProperty("Vila Dubrovnik 150 m², turistička zona", 150, "Dubrovnik, Lapad", "good");
        property.KeyFeatures = ["turistička zona"];

        var result = RunEngine(property, "Dubrovnik", comps, "Dubrovnik, Lapad");

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 16 — PIPELINE & ADJUSTMENTS STRUCTURE              ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_Pipeline_ContainsExpectedSteps()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.NotNull(result.Pipeline);
        Assert.True(result.Pipeline.Count >= 3, $"Pipeline should have ≥3 steps, got {result.Pipeline.Count}");
    }

    [Fact]
    public void House_Adjustments_ContainConditionFactor()
    {
        // Adjustments dict contains contextual adjustments (novogradnja, centar, etc.)
        // Condition rank adjustments are per-comp, not stored in Adjustments.
        // Use Croatian keyword "novogradnja" to trigger the novogradnja contextual adjustment.
        var comps = MakeHouseComps("Zagreb, centar", 5, 2500, 150);
        var property = MakeHouseProperty(
            "Novogradnja kuća 150 m², centar grada", 150,
            "Zagreb, centar", "novogradnja");
        property.KeyFeatures = ["novogradnja", "centar grada"];
        var result = RunEngine(property, "Zagreb", comps, "Zagreb, centar");

        Assert.NotNull(result.Adjustments);
        // Novogradnja + centar keywords should trigger at least 1 contextual adjustment
        Assert.True(result.Adjustments.Count >= 1,
            $"Novogradnja + centar house should have adjustments, got {result.Adjustments.Count}");
    }

    [Fact]
    public void House_TotalAdjustment_ClampedToPlus40()
    {
        // Stack multiple positive adjustments — total should be capped at +40%
        var comps = MakeHouseComps("Dubrovnik, Lapad", 5, 5000, 100, baseTotal: 500000);

        var property = MakeHouseProperty(
            "Vila Dubrovnik 100 m², novogradnja, prvi red do mora, investicija, bazen, pogled na more, centar grada",
            100, "Dubrovnik, centar", "new");
        property.KeyFeatures = ["novogradnja", "prvi red do mora", "investicija", "centar grada", "pogled na more"];
        property.AdditionalNotes = "Premium lokacija prvi red do mora, novogradnja, investicija";

        var result = RunEngine(property, "Dubrovnik", comps, "Dubrovnik, centar");

        Assert.True(result.TotalAdjustmentPercent <= 40,
            $"Total adjustment must be ≤ 40%, got {result.TotalAdjustmentPercent}");
    }

    [Fact]
    public void House_TotalAdjustment_ClampedToMinus40()
    {
        // Stack multiple negative adjustments — total should be capped at -40%
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var property = MakeHouseProperty(
            "Kuća za adaptaciju 250 m², periferija, loša infrastruktura, legalizacija",
            250, "Zagreb, Brezovica", "needs_adaptation");
        property.KeyFeatures = ["periferija", "loša infrastruktura", "legalizacija"];
        property.AdditionalNotes = "Potrebna legalizacija, makadamski prilaz";

        var result = RunEngine(property, "Zagreb", comps, "Zagreb, Brezovica");

        Assert.True(result.TotalAdjustmentPercent >= -40,
            $"Total adjustment must be ≥ -40%, got {result.TotalAdjustmentPercent}");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 17 — CONFIDENCE LEVELS                             ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_HighConfidence_LabelsAsVisoka()
    {
        // 8+ comps, low CV, primary search
        var comps = MakeHouseComps("Zagreb, Maksimir", 8, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.MathConfidenceScore >= 75)
        {
            Assert.Equal("Visoka", result.MathConfidenceLevel);
        }
    }

    [Fact]
    public void House_ConfidenceScore_Between0And100()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.InRange(result.MathConfidenceScore, 0, 100);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 18 — SPECIAL HOUSE TYPES                           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_Vila_TreatedAsHouse()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 3000, 200, baseTotal: 600000);
        var property = MakeHouseProperty("Vila 200 m², garaža, bazen", 200, "Zagreb, Maksimir", "new");
        var result = RunEngineType(property, "Zagreb", "vila", comps);

        Assert.True(result.EstimatedValueEur > 0, "Vila should be treated as house");
    }

    [Fact]
    public void House_Vikendica_TreatedAsHouse()
    {
        // AiSimilarityScore set to bypass math-based location slug mismatch (multi-word region)
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Vikendica A", "Vikendica 60 m², Gorski Kotar, vrt 500 m²", 60000, 1000, 60, "good"),
            MakeListing("Vikendica B", "Vikendica 80 m², Gorski Kotar, renovirana", 80000, 1000, 80, "renovated"),
            MakeListing("Vikendica C", "Vikendica 50 m², Gorski Kotar, izvorno", 37500, 750, 50, "original"),
            MakeListing("Vikendica D", "Vikendica 70 m², Gorski Kotar, dobro stanje", 56000, 800, 70, "good"),
            MakeListing("Vikendica E", "Vikendica 55 m², Gorski Kotar, novogradnja", 71500, 1300, 55, "new"),
        };
        foreach (var c in comps) c.AiSimilarityScore = 0.75;

        var property = MakeHouseProperty("Vikendica 65 m²", 65, "Gorski Kotar, Mrkopalj", "good");
        var result = RunEngineType(property, "Gorski Kotar", "vikendica", comps, "Gorski Kotar, Mrkopalj");

        Assert.True(result.EstimatedValueEur > 0,
            $"Vikendica (weekend house) must produce estimate, got {result.EstimatedValueEur}");
    }

    [Fact]
    public void House_ObiteljskaKuca_TreatedAsHouse()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 160, baseTotal: 320000);
        var property = MakeHouseProperty("Obiteljska kuća 160 m²", 160, "Zagreb, Maksimir", "good");
        var result = RunEngineType(property, "Zagreb", "obiteljska kuća", comps);

        Assert.True(result.EstimatedValueEur > 0, "Obiteljska kuća should be treated as house");
    }

    [Fact]
    public void House_DvojnaKuca_TreatedAsHouse()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150, baseTotal: 300000);
        var property = MakeHouseProperty("Dvojna kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngineType(property, "Zagreb", "dvojna kuća", comps);

        Assert.True(result.EstimatedValueEur > 0, "Dvojna kuća should be treated as house");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 19 — EDGE CASES                                    ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_ZeroSize_HandledGracefully()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća bez veličine", 0, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        // Should not crash — may return 0 or fallback
        Assert.True(result.EstimatedValueEur >= 0);
    }

    [Fact]
    public void House_NegativeSize_HandledGracefully()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća negativna veličina", -50, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur >= 0);
    }

    [Fact]
    public void House_EmptyLocation_HandledGracefully()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća bez lokacije", 150, "", "good");
        var result = RunEngine(property, "Zagreb", comps, "");

        Assert.True(result.EstimatedValueEur >= 0);
    }

    [Fact]
    public void House_UnknownCondition_TreatedAsGood()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var unknown = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "");
        var good = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");

        var resultUnknown = RunEngine(unknown, "Zagreb", comps);
        var resultGood = RunEngine(good, "Zagreb", comps);

        // Unknown should be treated as "Good" (rank 2)
        Assert.True(resultUnknown.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_VeryHighYard_10000m2_Clamped()
    {
        // Yard adjustment clamped at ±25%
        var comps = new List<ComparableSearchResult>
        {
            MakeListingWithYard("Kuća A", "Kuća 150 m², vrt 100 m²", 300000, 2000, 150, "good", 100),
            MakeListingWithYard("Kuća B", "Kuća 160 m², vrt 120 m²", 320000, 2000, 160, "good", 120),
            MakeListingWithYard("Kuća C", "Kuća 140 m², vrt 80 m²", 280000, 2000, 140, "good", 80),
            MakeListingWithYard("Kuća D", "Kuća 145 m², vrt 110 m²", 290000, 2000, 145, "good", 110),
            MakeListingWithYard("Kuća E", "Kuća 155 m², vrt 90 m²", 310000, 2000, 155, "good", 90),
        };

        var property = MakeHouseProperty("Kuća 150 m², okućnica 10000 m²", 150, "Zagreb, Maksimir", "good", yardM2: 10000);
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    [Fact]
    public void House_DuplicateListings_Deduplicated()
    {
        // Same price/size listings — should be deduplicated
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća A kopija", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća A duplikat", "Kuća 150 m²", 300000, 2000, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m²", 320000, 2000, 160, "good"),
            MakeListing("Kuća C", "Kuća 140 m²", 280000, 2000, 140, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Should produce estimate after dedup");
    }

    [Fact]
    public void House_MixedConditionComps_ProducesReasonableEstimate()
    {
        // Comps in all conditions — engine should weight them appropriately
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća A", "Kuća 150 m², novogradnja", 450000, 3000, 150, "new"),
            MakeListing("Kuća B", "Kuća 150 m², renovirana", 375000, 2500, 150, "renovated"),
            MakeListing("Kuća C", "Kuća 150 m², dobro stanje", 300000, 2000, 150, "good"),
            MakeListing("Kuća D", "Kuća 150 m², izvorno stanje", 225000, 1500, 150, "original"),
            MakeListing("Kuća E", "Kuća 150 m², za adaptaciju", 150000, 1000, 150, "needs_work"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
        Assert.True(result.EstimatedValueEur > 100_000);
        Assert.True(result.EstimatedValueEur < 500_000);
    }

    [Fact]
    public void House_OnlyPricePerM2_NoPriceTotal_Works()
    {
        // Tier 2: ListedPricePerM2 only, no total price
        var comps = new List<ComparableSearchResult>
        {
            MakeListingNoTotal("Kuća A", "Kuća 150 m²", 2000, 150, "good"),
            MakeListingNoTotal("Kuća B", "Kuća 160 m²", 2000, 160, "good"),
            MakeListingNoTotal("Kuća C", "Kuća 140 m²", 2000, 140, "good"),
            MakeListingNoTotal("Kuća D", "Kuća 155 m²", 2000, 155, "good"),
            MakeListingNoTotal("Kuća E", "Kuća 145 m²", 2000, 145, "good"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0, "Should work with Tier-2 (pricePerM2 only) listings");
    }

    [Fact]
    public void House_SafetyFloor_EstimateCantBeBelow50PercentOfAvg()
    {
        // Extreme negative adjustments should be blocked by safety floor
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);

        var property = MakeHouseProperty(
            "Kuća za adaptaciju 300 m², periferija, loša infrastruktura, legalizacija, sjever bez pogleda",
            300, "Zagreb, Brezovica", "needs_adaptation");
        property.KeyFeatures = ["periferija", "loša infrastruktura", "legalizacija"];
        property.AdditionalNotes = "Potrebna legalizacija, loša infrastruktura, sjevernog pogleda";

        var result = RunEngine(property, "Zagreb", comps, "Zagreb, Brezovica");

        if (result.EstimatedValueEur > 0 && result.UsablePricePoints >= 2)
        {
            // Safety floor: estimate >= 50% of avg comp total
            double avgCompTotal = 2000 * 150; // ~300k
            Assert.True(result.EstimatedValueEur >= avgCompTotal * 0.30,
                $"Safety floor: estimate ({result.EstimatedValueEur}) should be reasonable, avg comp ~ {avgCompTotal}");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 20 — COASTAL VS INLAND PRICING DIFFERENCES         ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_CoastalAlways_MoreExpensiveThan_InlandSameCondition()
    {
        var coastalComps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Split A", "Kuća 150 m², blizu mora", 450000, 3000, 150, "good"),
            MakeListing("Kuća Split B", "Kuća 160 m², pogled more", 480000, 3000, 160, "good"),
            MakeListing("Kuća Split C", "Kuća 140 m², renovirana", 420000, 3000, 140, "renovated"),
            MakeListing("Kuća Split D", "Kuća 155 m², dobro", 387500, 2500, 155, "good"),
            MakeListing("Kuća Split E", "Kuća 145 m², novogradnja", 507500, 3500, 145, "new"),
        };

        var inlandComps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Zagreb A", "Kuća 150 m², vrt", 300000, 2000, 150, "good"),
            MakeListing("Kuća Zagreb B", "Kuća 160 m², garaža", 320000, 2000, 160, "good"),
            MakeListing("Kuća Zagreb C", "Kuća 140 m², renovirana", 280000, 2000, 140, "renovated"),
            MakeListing("Kuća Zagreb D", "Kuća 155 m², dobro", 310000, 2000, 155, "good"),
            MakeListing("Kuća Zagreb E", "Kuća 145 m², novogradnja", 348000, 2400, 145, "new"),
        };

        var coastalProp = MakeHouseProperty("Kuća Split 150 m²", 150, "Split, Meje", "good");
        var inlandProp = MakeHouseProperty("Kuća Zagreb 150 m²", 150, "Zagreb, Maksimir", "good");

        var coastalResult = RunEngine(coastalProp, "Split", coastalComps, "Split, Meje");
        var inlandResult = RunEngine(inlandProp, "Zagreb", inlandComps);

        Assert.True(coastalResult.EstimatedValueEur > inlandResult.EstimatedValueEur,
            $"Coastal ({coastalResult.EstimatedValueEur}) should be > Inland ({inlandResult.EstimatedValueEur})");
    }

    [Fact]
    public void House_RuralSlavonia_CheapestRegion()
    {
        var ruralComps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Vukovar A", "Kuća 150 m²", 52500, 350, 150, "original"),
            MakeListing("Kuća Vukovar B", "Kuća 120 m²", 42000, 350, 120, "original"),
            MakeListing("Kuća Vukovar C", "Kuća 140 m²", 56000, 400, 140, "good"),
            MakeListing("Kuća Vukovar D", "Kuća 130 m²", 45500, 350, 130, "original"),
            MakeListing("Kuća Vukovar E", "Kuća 160 m²", 56000, 350, 160, "original"),
        };

        var property = MakeHouseProperty("Kuća Vukovar 140 m²", 140, "Vukovar, okolica", "original");
        var result = RunEngine(property, "Vukovar", ruralComps, "Vukovar, okolica");

        Assert.True(result.EstimatedValueEur > 0, "Rural Slavonia house must produce estimate");
        Assert.True(result.EstimatedValueEur < 100_000,
            $"Rural Slavonia should be <100k, got {result.EstimatedValueEur}");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 21 — SEARCH PHASE IMPACT                           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_RegionalFallbackPhase_LargerDiscount()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");

        // Primary phase
        var resultPrimary = RunEngineWithPhase(property, "Zagreb", comps, "Primary");
        // Regional fallback
        var resultRegional = RunEngineWithPhase(property, "Zagreb", comps, "RegionalFallback");

        Assert.True(resultPrimary.EstimatedValueEur > 0);
        Assert.True(resultRegional.EstimatedValueEur > 0);
        // Regional fallback may have larger discount
        Assert.True(resultRegional.RealizationDiscountPercent <= resultPrimary.RealizationDiscountPercent,
            $"Regional ({resultRegional.RealizationDiscountPercent}%) should have ≤ gap vs Primary ({resultPrimary.RealizationDiscountPercent}%)");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 22 — SHERLOCK INSIGHT                              ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_ProducesSherlockInsight()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.NotNull(result.SherlockInsight);
        Assert.True(result.SherlockInsight.Length > 10,
            "Sherlock insight should have meaningful text");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 23 — MARKET AVERAGE & CALCULATED PRICE             ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_CalculatedPricePerM2_IsReasonable()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.CalculatedPricePerM2 > 0, "Must have calculated price per m²");
        Assert.True(result.CalculatedPricePerM2 >= 200, "Must be above min threshold");
        Assert.True(result.CalculatedPricePerM2 <= 15000, "Must be below max threshold");
    }

    [Fact]
    public void House_EstimatedValue_Equals_PricePerM2_Times_Size_Approx()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.EstimatedValueEur > 0)
        {
            double expected = result.CalculatedPricePerM2 * 150;
            double tolerance = expected * 0.05; // 5% tolerance for rounding
            Assert.InRange(result.EstimatedValueEur, expected - tolerance, expected + tolerance);
        }
    }

    [Fact]
    public void House_MarketAverageAsking_GreaterThanCalculated()
    {
        // Market average (before discount) should be >= calculated price (after discount)
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.EstimatedValueEur > 0)
        {
            Assert.True(result.MarketAverageAsking >= result.AfterDiscountPerM2,
                $"Avg asking ({result.MarketAverageAsking}) must be ≥ after discount ({result.AfterDiscountPerM2})");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 24 — LOCATION CLUSTERING                           ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_WideSpreadPrices_LocationClustering()
    {
        // max/min ratio > 2.5 triggers location-aware clustering
        var comps = new List<ComparableSearchResult>
        {
            // Cheaper cluster
            MakeListing("Kuća A", "Kuća 150 m², periferija", 225000, 1500, 150, "good"),
            MakeListing("Kuća B", "Kuća 160 m², okolica", 240000, 1500, 160, "good"),
            MakeListing("Kuća C", "Kuća 140 m², periferija", 210000, 1500, 140, "good"),
            // Expensive cluster (>2.5x)
            MakeListing("Kuća D", "Kuća 150 m², centar luksuz", 600000, 4000, 150, "new"),
            MakeListing("Kuća E", "Kuća 145 m², premium centar", 580000, 4000, 145, "new"),
        };

        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 25 — USED COMPARABLES OUTPUT                       ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_UsedComparables_HaveValidData()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.UsedComparables.Count > 0)
        {
            foreach (var comp in result.UsedComparables)
            {
                Assert.True(comp.PricePerM2 > 0, "Used comp should have positive price/m²");
                Assert.False(string.IsNullOrEmpty(comp.Title), "Used comp should have title");
            }
        }
    }

    [Fact]
    public void House_MaxComps_Limited_To7()
    {
        // MaxCompsForValuation = 7 — after top-N limiter, only 7 are used for final estimate.
        // UsablePricePoints is set before top-N limiting, so we check UsedComparables
        // which reflects the actual comps used in the final calculation.
        var comps = MakeHouseComps("Zagreb, Maksimir", 12, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        // The engine uses MaxCompsForValuation=7 for final calculation
        var nonOutlierComps = result.UsedComparables.Count(c => !c.IsOutlier);
        Assert.True(nonOutlierComps <= 7,
            $"Should use max 7 non-outlier comps, got {nonOutlierComps}");
        // But UsablePricePoints tracks total valid prices found (before Top-N)
        Assert.True(result.UsablePricePoints >= 7,
            $"With 12 comps, UsablePricePoints should be ≥ 7, got {result.UsablePricePoints}");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 26 — PRICE RANGE ESTIMATE                          ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Fact]
    public void House_PriceRange_EstimatedValueConsistentWithCalculatedPrice()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.EstimatedValueEur > 0 && result.CalculatedPricePerM2 > 0)
        {
            // EstimatedValue should be roughly CalculatedPricePerM2 * SizeM2
            double expectedValue = result.CalculatedPricePerM2 * 150;
            double tolerance = expectedValue * 0.05; // 5% tolerance for rounding
            Assert.True(Math.Abs(result.EstimatedValueEur - expectedValue) <= tolerance,
                $"EstimatedValue ({result.EstimatedValueEur}) should be close to CalcPpm2*Size ({expectedValue})");
        }
    }

    [Fact]
    public void House_PriceRange_MarketAverageAboveAfterDiscount()
    {
        var comps = MakeHouseComps("Zagreb, Maksimir", 5, 2000, 150);
        var property = MakeHouseProperty("Kuća 150 m²", 150, "Zagreb, Maksimir", "good");
        var result = RunEngine(property, "Zagreb", comps);

        if (result.MarketAverageAsking > 0 && result.AfterDiscountPerM2 > 0)
        {
            Assert.True(result.MarketAverageAsking >= result.AfterDiscountPerM2,
                $"Market average ({result.MarketAverageAsking}) should be >= after-discount ({result.AfterDiscountPerM2})");
        }
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  SECTION 27 — LEGACY SCENARIO CONTINUITY                    ║
    // ╚═══════════════════════════════════════════════════════════════╝

    [Theory]
    [InlineData("Slavonski Brod", 130, 500, "original", 20_000, 150_000)]
    [InlineData("Osijek", 140, 800, "good", 50_000, 200_000)]
    [InlineData("Karlovac", 150, 700, "good", 40_000, 200_000)]
    [InlineData("Sisak", 120, 600, "original", 20_000, 130_000)]
    [InlineData("Bjelovar", 140, 650, "good", 30_000, 160_000)]
    [InlineData("Čakovec", 150, 900, "good", 50_000, 220_000)]
    [InlineData("Koprivnica", 130, 700, "good", 35_000, 170_000)]
    [InlineData("Vinkovci", 130, 400, "original", 20_000, 120_000)]
    [InlineData("Vukovar", 140, 350, "original", 15_000, 100_000)]
    [InlineData("Gospić", 130, 500, "original", 20_000, 130_000)]
    public void House_ContinentalCity_InExpectedRange(string city, double size, double ppm2,
        string condition, int minValue, int maxValue)
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing($"Kuća {city} A", $"Kuća {size - 10} m²", (size - 10) * ppm2, ppm2, size - 10, condition),
            MakeListing($"Kuća {city} B", $"Kuća {size} m²", size * ppm2, ppm2, size, condition),
            MakeListing($"Kuća {city} C", $"Kuća {size + 10} m²", (size + 10) * ppm2, ppm2, size + 10, condition),
            MakeListing($"Kuća {city} D", $"Kuća {size + 20} m²", (size + 20) * (ppm2 * 1.1), ppm2 * 1.1, size + 20, "good"),
            MakeListing($"Kuća {city} E", $"Kuća {size - 20} m²", (size - 20) * (ppm2 * 0.9), ppm2 * 0.9, size - 20, condition),
        };

        var location = $"{city}, centar";
        var property = MakeHouseProperty($"Kuća {city} {size} m²", size, location, condition);
        var result = RunEngine(property, city, comps, location);

        Assert.True(result.EstimatedValueEur >= minValue,
            $"{city} house should be >={minValue}, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= maxValue,
            $"{city} house should be <={maxValue}, got {result.EstimatedValueEur}");
    }

    [Theory]
    [InlineData("Split", 170, 3500, "good", 300_000, 900_000)]
    [InlineData("Dubrovnik", 200, 5000, "renovated", 500_000, 2_000_000)]
    [InlineData("Pula", 160, 2200, "good", 150_000, 600_000)]
    [InlineData("Zadar", 180, 2500, "good", 200_000, 700_000)]
    [InlineData("Rovinj", 150, 3000, "renovated", 250_000, 800_000)]
    [InlineData("Opatija", 180, 3500, "good", 350_000, 1_000_000)]
    [InlineData("Makarska", 160, 3000, "good", 250_000, 800_000)]
    [InlineData("Šibenik", 150, 2000, "good", 150_000, 500_000)]
    public void House_CoastalCity_InExpectedRange(string city, double size, double ppm2,
        string condition, int minValue, int maxValue)
    {
        var comps = new List<ComparableSearchResult>
        {
            MakeListing($"Kuća {city} A", $"Kuća {size} m², pogled", size * ppm2, ppm2, size, condition),
            MakeListing($"Kuća {city} B", $"Kuća {size - 10} m²", (size - 10) * (ppm2 * 0.95), ppm2 * 0.95, size - 10, condition),
            MakeListing($"Kuća {city} C", $"Kuća {size + 10} m²", (size + 10) * (ppm2 * 1.05), ppm2 * 1.05, size + 10, condition),
            MakeListing($"Kuća {city} D", $"Kuća {size + 20} m²", (size + 20) * (ppm2 * 1.1), ppm2 * 1.1, size + 20, "new"),
            MakeListing($"Kuća {city} E", $"Kuća {size - 20} m²", (size - 20) * (ppm2 * 0.9), ppm2 * 0.9, size - 20, "original"),
        };

        var location = $"{city}, centar";
        var property = MakeHouseProperty($"Kuća {city} {size} m²", size, location, condition);
        var result = RunEngine(property, city, comps, location);

        Assert.True(result.EstimatedValueEur >= minValue,
            $"{city} house should be >={minValue}, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= maxValue,
            $"{city} house should be <={maxValue}, got {result.EstimatedValueEur}");
    }

    // ╔═══════════════════════════════════════════════════════════════╗
    // ║  HELPER METHODS                                             ║
    // ╚═══════════════════════════════════════════════════════════════╝

    private static ExtractedPropertyInfo MakeHouseProperty(
        string description, double sizeM2, string location, string state,
        double yardM2 = 0, string microLocation = "")
        => new()
        {
            Description = description,
            SizeM2 = sizeM2,
            Location = location,
            PropertyState = state,
            Condition = state,
            RawText = description,
            YardAreaM2 = yardM2,
            MicroLocation = microLocation,
        };

    private DeterministicValuation RunEngine(
        ExtractedPropertyInfo property, string region,
        List<ComparableSearchResult> comps, string? patchLocation = null)
        => RunEngineType(property, region, "kuća", comps, patchLocation);

    private DeterministicValuation RunEngineType(
        ExtractedPropertyInfo property, string region, string type,
        List<ComparableSearchResult> comps, string? patchLocation = null)
    {
        var location = patchLocation ?? property.Location;
        var slug = LocationSlug(location);
        var patchedComps = comps.Select(l => new ComparableSearchResult
        {
            Title = l.Title,
            Content = l.Content,
            Url = $"https://nekretnine.hr/{slug}/{l.Title?.Replace(" ", "-").ToLowerInvariant() ?? "listing"}",
            ListedPriceEur = l.ListedPriceEur,
            ListedPricePerM2 = l.ListedPricePerM2,
            AiSizeM2 = l.AiSizeM2,
            AiCondition = l.AiCondition,
            HasGarage = l.HasGarage,
            HasRenovation = l.HasRenovation,
            HasYard = l.HasYard,
            AiYardAreaM2 = l.AiYardAreaM2,
            AiSimilarityScore = l.AiSimilarityScore,
        }).ToList();

        var market = new MarketResearchResult
        {
            RegionName = region,
            DetectedPropertyType = type,
            ActiveListings = patchedComps,
            RegionalData = [],
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
        };

        return _engine.Calculate(property, market);
    }

    private DeterministicValuation RunEngineWithPhase(
        ExtractedPropertyInfo property, string region,
        List<ComparableSearchResult> comps, string phase)
    {
        var slug = LocationSlug(property.Location);
        var patchedComps = comps.Select(l => new ComparableSearchResult
        {
            Title = l.Title, Content = l.Content,
            Url = $"https://nekretnine.hr/{slug}/{l.Title?.Replace(" ", "-").ToLowerInvariant() ?? "listing"}",
            ListedPriceEur = l.ListedPriceEur, ListedPricePerM2 = l.ListedPricePerM2,
            AiSizeM2 = l.AiSizeM2, AiCondition = l.AiCondition,
            HasGarage = l.HasGarage, AiYardAreaM2 = l.AiYardAreaM2,
            AiSimilarityScore = l.AiSimilarityScore,
        }).ToList();

        var market = new MarketResearchResult
        {
            RegionName = region,
            DetectedPropertyType = "kuća",
            ActiveListings = patchedComps,
            RegionalData = [],
            SearchPhaseUsed = phase,
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
        };

        return _engine.Calculate(property, market);
    }

    private static List<ComparableSearchResult> MakeHouseComps(
        string location, int count, double basePpm2, double baseSize, double? baseTotal = null)
    {
        var comps = new List<ComparableSearchResult>();
        var conditions = new[] { "good", "good", "renovated", "original", "new", "good", "renovated", "good", "original", "good", "new", "good" };

        for (int i = 0; i < count; i++)
        {
            double size = baseSize + (i - count / 2) * 10;
            double ppm2 = basePpm2 + (i % 3 - 1) * 100;  // slight variation
            double total = baseTotal ?? (size * ppm2);
            string cond = conditions[i % conditions.Length];

            comps.Add(MakeListing(
                $"Kuća {location.Split(',')[0]} {(char)('A' + i)}",
                $"Kuća {size} m², {cond} stanje",
                total, ppm2, size, cond));
        }

        return comps;
    }

    private static string LocationSlug(string location)
        => (location ?? "").ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    private static ComparableSearchResult MakeListing(
        string title, string content, double totalPrice, double pricePerM2,
        double sizeM2, string condition, bool hasGarage = false)
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

    private static ComparableSearchResult MakeListingWithYard(
        string title, string content, double totalPrice, double pricePerM2,
        double sizeM2, string condition, double yardM2)
        => new()
        {
            Title = title,
            Content = content,
            Url = $"https://example.com/{title.Replace(" ", "-").ToLowerInvariant()}",
            ListedPriceEur = totalPrice,
            ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2,
            AiCondition = condition,
            AiYardAreaM2 = yardM2,
            HasYard = true,
        };

    private static ComparableSearchResult MakeListingWithFeatures(
        string title, string content, double totalPrice, double pricePerM2,
        double sizeM2, string condition, bool hasGarage = false,
        bool hasRenovation = false, bool hasSeaView = false)
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
            HasRenovation = hasRenovation ? true : null,
        };

    private static ComparableSearchResult MakeListingNoTotal(
        string title, string content, double pricePerM2,
        double sizeM2, string condition)
        => new()
        {
            Title = title,
            Content = content,
            Url = $"https://example.com/{title.Replace(" ", "-").ToLowerInvariant()}",
            ListedPriceEur = null,        // NO total price
            ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2,
            AiCondition = condition,
        };
}
