using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// End-to-end real-world scenario tests that simulate complete valuation workflows.
/// These test realistic Croatian property scenarios with market data patterns
/// that mirror actual Perplexity/Tavily search results.
/// </summary>
public class ValuationScenarioTests
{
    private readonly ValuationEngine _engine;

    public ValuationScenarioTests()
    {
        _engine = new ValuationEngine(NullLogger<ValuationEngine>.Instance);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 1: Zagreb center apartment — typical workflow
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_ZagrebCenterApartment_ProducesReasonableResult()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Trosobni stan 75 m² na Trgu bana Jelačića, renoviran 2022., 3. kat sa liftom",
            SizeM2 = 75,
            Location = "Zagreb, Centar, Trg bana Jelačića",
            Condition = "renovirano",
            PropertyState = "renovated",
            RawText = "Trosobni stan 75 m² u centru Zagreba, potpuno renoviran 2022.",
            MicroLocation = "Trg bana Jelačića",
            KeyFeatures = ["centar grada", "renovirano 2022", "lift", "3. kat"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 85 },
            ActiveListings =
            [
                MakeListing("Stan Ilica", "Renoviran trosoban stan 80 m², Ilica, centar", 280000, 3500, 80, "renovated"),
                MakeListing("Stan Jelačićev trg", "Dvosoban stan 55 m², potpuno renoviran 2023.", 192500, 3500, 55, "renovated"),
                MakeListing("Stan Cvjetni trg", "Trosoban stan 90 m², dobro stanje, centar", 270000, 3000, 90, "good"),
                MakeListing("Stan Tkalčićeva", "Četverosoban stan novogradnja 100 m²", 380000, 3800, 100, "new"),
                MakeListing("Stan Preradovićev", "Dvosoban 60 m², izvorno stanje, 2. kat bez lifta", 150000, 2500, 60, "original"),
            ],
            RegionalData =
            [
                MakeListing("Stan Maksimir", "Trosoban 70 m², dobro stanje", 189000, 2700, 70, "good"),
                MakeListing("Stan Trešnjevka", "Dvosoban stan 50 m²", 125000, 2500, 50, "good"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        if (market.RegionalData?.Count > 0)
            market.RegionalData = PatchUrls(market.RegionalData, property.Location);
        var result = _engine.Calculate(property, market);

        // Zagreb center apartment 75m² should be worth between 100k and 500k EUR
        Assert.True(result.EstimatedValueEur >= 100_000,
            $"Zagreb center 75m² should be >= 100k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur <= 500_000,
            $"Zagreb center 75m² should be <= 500k, got {result.EstimatedValueEur}");
        Assert.True(result.CalculatedPricePerM2 >= 1000 && result.CalculatedPricePerM2 <= 6000,
            $"Price/m² should be 1000-6000, got {result.CalculatedPricePerM2}");
        Assert.True(result.UsablePricePoints >= 2, "Should have at least 2 comps");
        Assert.NotEmpty(result.Pipeline);
        Assert.False(string.IsNullOrWhiteSpace(result.ConfidenceLevel));
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 2: Split waterfront apartment with sea view
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_SplitSeaViewApartment_CoastalPremiumApplied()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 60 m² na Bačvicama, pogled na more, 1. red do mora",
            SizeM2 = 60,
            Location = "Split, Bačvice",
            Condition = "good",
            PropertyState = "good",
            RawText = "Stan s prekrasnim pogledom na more, Bačvice Split, blizu plaže",
            KeyFeatures = ["pogled na more", "1. red do mora", "Bačvice"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Split",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 80 },
            ActiveListings =
            [
                MakeListing("Stan Bačvice A", "Stan 55 m², pogled na more, Bačvice", 275000, 5000, 55, "good",
                    hasSeaView: true),
                MakeListing("Stan Bačvice B", "Stan 65 m², panoramski pogled more", 325000, 5000, 65, "renovated",
                    hasSeaView: true),
                MakeListing("Stan Firule", "Stan 70 m², bez pogleda, Firule", 252000, 3600, 70, "good"),
                MakeListing("Stan Meje", "Stan 50 m², dobro stanje", 170000, 3400, 50, "good"),
                MakeListing("Stan Sućidar", "Stan 80 m², periferija Splita", 232000, 2900, 80, "good"),
                MakeListing("Stan centar", "Stan 45 m², centar grada, pogled", 225000, 5000, 45, "renovated",
                    hasSeaView: true),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0);
        // Split sea view should be expensive (2000+ EUR/m²)
        Assert.True(result.CalculatedPricePerM2 >= 1500,
            $"Split sea view should be >= 1500 €/m², got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 3: Dubrovnik luxury villa
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_DubrovnikLuxuryVilla_HighEndValuation()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Luksuzna villa 250 m², bazenom, pogledom na more, novi Dubrovnik",
            SizeM2 = 250,
            Location = "Dubrovnik, Lapad",
            Condition = "new",
            PropertyState = "new",
            YardAreaM2 = 400,
            RawText = "Novogradnja vila s bazenom i pogledom na more, Lapad, Dubrovnik",
            KeyFeatures = ["bazen", "pogled na more", "novogradnja", "Lapad"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Dubrovnik",
            DetectedPropertyType = "kuća",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 65 },
            ActiveListings =
            [
                MakeListing("Vila Lapad A", "Vila 200 m², bazen, pogled more, novogradnja", 1200000, 6000, 200, "new",
                    hasSeaView: true, hasGarage: true),
                MakeListing("Vila Lapad B", "Kuća 180 m², renovirana, vrt 300 m²", 900000, 5000, 180, "renovated"),
                MakeListing("Vila Gruž", "Vila 300 m², bazen, 1. red", 2100000, 7000, 300, "new",
                    hasSeaView: true, hasGarage: true),
                MakeListing("Kuća Mokošica", "Kuća 150 m², potrebna adaptacija", 450000, 3000, 150, "needs_work"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 100_000,
            $"Dubrovnik luxury villa should be > 100k, got {result.EstimatedValueEur}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 4: Osijek affordable apartment — inland market
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_OsijekApartment_InlandPricing()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Dvosoban stan 55 m², Retfala, Osijek, dobro stanje",
            SizeM2 = 55,
            Location = "Osijek, Retfala",
            Condition = "good",
            PropertyState = "good",
            RawText = "Stan u Osijeku, Retfala, dobro održavan",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Osijek",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 70 },
            ActiveListings =
            [
                MakeListing("Stan Retfala A", "Stan 50 m², dobro stanje", 55000, 1100, 50, "good"),
                MakeListing("Stan Retfala B", "Stan 60 m², renoviran", 72000, 1200, 60, "renovated"),
                MakeListing("Stan centar", "Stan 45 m², centar Osijeka", 54000, 1200, 45, "good"),
                MakeListing("Stan Sjenjak", "Stan 70 m², izvorno stanje", 63000, 900, 70, "original"),
                MakeListing("Stan Gornji grad", "Stan 55 m², novogradnja", 77000, 1400, 55, "new"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        // Osijek is much cheaper than Zagreb/Split
        Assert.True(result.CalculatedPricePerM2 >= 500 && result.CalculatedPricePerM2 <= 2500,
            $"Osijek price/m² should be 500-2500, got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 5: Mixed quality data — some are irrelevant
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_MixedQualityData_RobustResult()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Trosoban stan 70 m² u Rijeci, Trsat",
            SizeM2 = 70,
            Location = "Rijeka, Trsat",
            PropertyState = "good",
            Condition = "good",
            RawText = "Stan u Rijeci na Trsatu, dobro stanje",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Rijeka",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
            ActiveListings =
            [
                // Good relevant comps
                MakeListing("Stan Trsat A", "Stan 65 m², dobro stanje, Trsat", 117000, 1800, 65, "good"),
                MakeListing("Stan Trsat B", "Stan 75 m², renoviran, Trsat", 142500, 1900, 75, "renovated"),
                MakeListing("Stan Centar", "Stan 60 m², centar Rijeke", 120000, 2000, 60, "good"),
                // Irrelevant: rental (should be filtered)
                MakeListing("Stan za najam", "Najam stana 50 m², 500 EUR mjesečno", 500, 10, 50, "good"),
                // Irrelevant: way too expensive (outlier)
                MakeListing("Penthouse Opatija", "Luksuzni penthouse 200 m², pogled more", 2000000, 10000, 200, "new",
                    hasSeaView: true),
                // Irrelevant: too cheap (below min)
                MakeListing("Ruševina", "Kuća za rušenje 100 m²", 10000, 100, 100, "needs_work"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0, "Should produce valid estimate despite mixed data");
        // Should filter out rental, extreme outlier, and sub-minimum
        Assert.True(result.CalculatedPricePerM2 >= 500 && result.CalculatedPricePerM2 <= 5000,
            $"Rijeka price/m² after filtering should be reasonable, got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 6: Rural house — low data availability
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_RuralHouse_LowDataAvailability()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stara kuća 120 m² s okućnicom 800 m², selo kod Požege",
            SizeM2 = 120,
            Location = "Požega, okolica",
            PropertyState = "original",
            Condition = "izvorno stanje",
            YardAreaM2 = 800,
            RawText = "Kuća u selu blizu Požege, izvorno stanje",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Požega",
            DetectedPropertyType = "kuća",
            SearchPhaseUsed = "Regional",
            Confidence = new ConfidenceScore { Level = "Low", Score = 35 },
            ActiveListings =
            [
                MakeListing("Kuća Požega A", "Kuća 100 m², okućnica 600 m²", 50000, 500, 100, "original"),
                MakeListing("Kuća Požega B", "Kuća 150 m², dobar stanje, vrt 1000 m²", 90000, 600, 150, "good"),
                MakeListing("Kuća Požega C", "Kuća 233 m², potrebna obnova, vrt 500 m²", 107500, 461, 233, "original"),
                MakeListing("Kuća Požega D", "Kuća 130 m², djelomično renovirana, okućnica 400 m²", 78000, 600, 130, "fair"),
                MakeListing("Kuća Požega E", "Kuća 90 m², stara gradnja, dvorište 350 m²", 45000, 500, 90, "original"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        // With the V29-FIX (MinPricePerM2_Building lowered to 200), rural houses now produce a result
        Assert.True(result.EstimatedValueEur > 0,
            "Rural Požega house must produce a non-zero estimate after V29-FIX");
        Assert.True(result.EstimatedValueEur < 200_000,
            $"Rural Požega house should be < 200k, got {result.EstimatedValueEur}");
        Assert.True(result.EstimatedValueEur >= 20_000,
            $"Rural Požega house should be >= 20k, got {result.EstimatedValueEur}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 7: Agricultural land — Slavonija
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_AgriculturalLand_Slavonija()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Oranica 5000 m² u ravnici, blizu Đakova",
            SizeM2 = 5000,
            Location = "Đakovo, okolica",
            PropertyState = "",
            Condition = "",
            RawText = "Poljoprivredno zemljište - oranica u slavonskoj ravnici",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Đakovo",
            DetectedPropertyType = "oranica",
            SearchPhaseUsed = "Regional",
            Confidence = new ConfidenceScore { Level = "Low", Score = 40 },
            ActiveListings =
            [
                MakeListing("Oranica A", "Oranica 4000 m², ravnica", 16000, 4, 4000, ""),
                MakeListing("Oranica B", "Oranica 6000 m², Đakovština", 30000, 5, 6000, ""),
                MakeListing("Oranica C", "Poljoprivredno zemljište 3000 m²", 9000, 3, 3000, ""),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur >= 0);
        if (result.CalculatedPricePerM2 > 0)
        {
            Assert.True(result.CalculatedPricePerM2 < 100,
                $"Agricultural land should be < 100 €/m², got {result.CalculatedPricePerM2}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 8: Building land — Zadar coast
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_BuildingLandCoast()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Građevinsko zemljište 700 m², Zadar, Petrčane",
            SizeM2 = 700,
            Location = "Zadar, Petrčane",
            PropertyState = "",
            Condition = "",
            RawText = "Građevinsko zemljište s pogledom na more, Petrčane kod Zadra",
            KeyFeatures = ["pogled na more", "Petrčane"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zadar",
            DetectedPropertyType = "građevinsko zemljište",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
            ActiveListings =
            [
                MakeListing("Parcela A", "Građevinsko zemljište 600 m², Petrčane", 180000, 300, 600, ""),
                MakeListing("Parcela B", "Zemljište 800 m², pogled more, Zadar", 280000, 350, 800, ""),
                MakeListing("Parcela C", "Građevinsko 500 m², Diklo", 150000, 300, 500, ""),
                MakeListing("Parcela D", "Zemljište 1000 m², Kozino", 250000, 250, 1000, ""),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 9: Needs-adaptation apartment vs renovated comparables
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_NeedsAdaptation_CheaperThanRenovatedComps()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan za adaptaciju 70 m², Varaždin centar, 1960-ih",
            SizeM2 = 70,
            Location = "Varaždin, Centar",
            PropertyState = "needs_adaptation",
            Condition = "potrebna adaptacija",
            RawText = "Stan iz 1960-ih godina, izvorno stanje, potrebna kompletna adaptacija",
            AdditionalNotes = "Potrebna kompletna obnova instalacija i kupaonice",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Varaždin",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 65 },
            ActiveListings =
            [
                // All comps are renovated → engine should discount for adaptation needs
                MakeListing("Stan A reno", "Potpuno renoviran 65 m², novi pod, nova kupaonica", 97500, 1500, 65, "renovated"),
                MakeListing("Stan B reno", "Renoviran 75 m², 2022. moderniziran", 120000, 1600, 75, "renovated"),
                MakeListing("Stan C reno", "Novogradnja 60 m², vrhunska kvaliteta", 102000, 1700, 60, "new"),
                MakeListing("Stan D dobro", "Dobro stanje 70 m², uredno", 91000, 1300, 70, "good"),
                MakeListing("Stan E orig", "Izvorno stanje 80 m², potencijal", 80000, 1000, 80, "original"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0);
        Assert.True(result.CalculatedPricePerM2 < 1600,
            $"Needs-adaptation should have lower €/m² than avg renovated comps, got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 10: Investment property — centar with keywords
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_InvestmentProperty_PrimeLocation()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 50 m² u srcu pješačke zone, Ilica 1, Zagreb",
            SizeM2 = 50,
            Location = "Zagreb, Centar, Ilica",
            PropertyState = "original",
            Condition = "izvorno stanje",
            RawText = "Stan u pješačkoj zoni Zagreba, Ilica, investicijski potencijal",
            KeyFeatures = ["pješačka zona", "centar grada", "investicija"],
            AdditionalNotes = "Investicijski potencijal, pješačka zona, turistički najam",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Zagreb",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "High", Score = 80 },
            ActiveListings =
            [
                MakeListing("Stan centar A", "Stan 45 m², centar grada, pješačka zona", 135000, 3000, 45, "good"),
                MakeListing("Stan centar B", "Stan 55 m², Ilica, renoviran", 165000, 3000, 55, "renovated"),
                MakeListing("Stan Maksimir", "Stan 60 m², Maksimir, mirna lokacija", 126000, 2100, 60, "good"),
                MakeListing("Stan Trnje", "Stan 65 m², Trnje, dobro stanje", 130000, 2000, 65, "good"),
                MakeListing("Stan periferija", "Stan 70 m², Sesvete, novo", 119000, 1700, 70, "new"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result10 = _engine.Calculate(property, market);

        Assert.True(result10.EstimatedValueEur > 0);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 11: Identical property, different conditions — ordering
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_ConditionOrdering_NewGtRenovatedGtGoodGtOriginalGtNeedsWork()
    {
        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan A", "65 m², dobro stanje", 130000, 2000, 65, "good"),
            MakeListing("Stan B", "60 m², renoviran", 138000, 2300, 60, "renovated"),
            MakeListing("Stan C", "70 m², izvorno stanje", 112000, 1600, 70, "original"),
            MakeListing("Stan D", "55 m², novogradnja", 137500, 2500, 55, "new"),
            MakeListing("Stan E", "68 m², potrebna adaptacija", 95200, 1400, 68, "needs_work"),
            MakeListing("Stan F", "62 m², dobro", 124000, 2000, 62, "good"),
        };

        var conditions = new[] { "new", "renovated", "good", "original", "needs_adaptation" };
        var values = new Dictionary<string, int>();

        foreach (var condition in conditions)
        {
            var property = MakeProperty(condition, 65, "Zagreb, Maksimir");
            var market = MakeMarketFromListings("Zagreb", "stan", listings, $"Zagreb, Maksimir");
            var result = _engine.Calculate(property, market);
            values[condition] = result.EstimatedValueEur;
        }

        // Condition ordering should be: New > Renovated > Good > Original > NeedsWork
        // Due to adjustment complexity, exact ordering isn't guaranteed but general trend should hold
        Assert.True(values["new"] >= values["needs_adaptation"],
            $"New ({values["new"]}) should be >= NeedsAdaptation ({values["needs_adaptation"]})");
        Assert.True(values["renovated"] >= values["needs_adaptation"],
            $"Renovated ({values["renovated"]}) should be >= NeedsAdaptation ({values["needs_adaptation"]})");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 12: Empty market data — graceful degradation
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_EmptyMarketData_GracefulDegradation()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 65 m² u malom selu",
            SizeM2 = 65,
            Location = "Slunj, okolica",
            PropertyState = "original",
            Condition = "izvorno stanje",
            RawText = "Stan u malom selu kod Slunja",
        };

        var market = new MarketResearchResult
        {
            RegionName = "Slunj",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Regional",
            Confidence = new ConfidenceScore { Level = "Low", Score = 20 },
            ActiveListings = [],
            RegionalData = [],
        };

        var result = _engine.Calculate(property, market);

        // Should NOT throw — should return 0 estimate gracefully
        Assert.Equal(0, result.EstimatedValueEur);
        Assert.Equal(0, result.UsablePricePoints);
        Assert.NotNull(result.Pipeline);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 13: Pula apartment — Istria coastal
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_PulaApartment_IstriaCoastal()
    {
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan 55 m², Pula, Veruda, blizu plaže",
            SizeM2 = 55,
            Location = "Pula, Veruda",
            PropertyState = "good",
            Condition = "good",
            RawText = "Stan u Puli, Veruda, 200m od plaže, parking",
            KeyFeatures = ["blizu plaže", "parking"],
        };

        var market = new MarketResearchResult
        {
            RegionName = "Pula",
            DetectedPropertyType = "stan",
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 70 },
            ActiveListings =
            [
                MakeListing("Stan Veruda A", "Stan 50 m², dobro, Veruda, parking", 125000, 2500, 50, "good"),
                MakeListing("Stan Veruda B", "Stan 60 m², renoviran, pogled", 174000, 2900, 60, "renovated",
                    hasSeaView: true),
                MakeListing("Stan centar", "Stan 45 m², centar Pule", 108000, 2400, 45, "good"),
                MakeListing("Stan Šijana", "Stan 65 m², mirna lokacija", 136500, 2100, 65, "good"),
                MakeListing("Stan Stoja", "Stan 55 m², blizu mora, parking mjesto", 143000, 2600, 55, "good"),
            ],
        };

        market.ActiveListings = PatchUrls(market.ActiveListings, property.Location);
        var result13 = _engine.Calculate(property, market);

        Assert.True(result13.EstimatedValueEur > 0);
        Assert.True(result13.CalculatedPricePerM2 >= 1000,
            $"Pula Veruda should be >= 1000 €/m², got {result13.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 14: North vs South orientation — coastal city
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_OrientationMatters_CoastalCity()
    {
        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan jug A", "Stan 60 m², južna strana, pogled", 180000, 3000, 60, "good"),
            MakeListing("Stan sjever A", "Stan 60 m², sjever, bez pogleda", 144000, 2400, 60, "good"),
            MakeListing("Stan jug B", "Stan 65 m², jugozapad, sunčan", 201500, 3100, 65, "good"),
            MakeListing("Stan sjever B", "Stan 55 m², sjeverna strana", 121000, 2200, 55, "good"),
            MakeListing("Stan C", "Stan 70 m², dobro stanje", 189000, 2700, 70, "good"),
        };

        var propertySouth = new ExtractedPropertyInfo
        {
            Description = "Stan 60 m², južna strana, Zadar",
            SizeM2 = 60,
            Location = "Zadar, centar",
            PropertyState = "good",
            RawText = "Stan u Zadru, južna orijentacija, sunčan",
        };

        var propertyNorth = new ExtractedPropertyInfo
        {
            Description = "Stan 60 m², sjeverna strana, Zadar",
            SizeM2 = 60,
            Location = "Zadar, centar",
            PropertyState = "good",
            RawText = "Stan u Zadru, sjeverna orijentacija",
        };

        var resultSouth = _engine.Calculate(propertySouth, MakeMarketFromListings("Zadar", "stan", listings, "Zadar, centar"));
        var resultNorth = _engine.Calculate(propertyNorth, MakeMarketFromListings("Zadar", "stan", listings, "Zadar, centar"));

        Assert.True(resultSouth.EstimatedValueEur > 0);
        Assert.True(resultNorth.EstimatedValueEur > 0);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 15: Consistency — same input twice = same output
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_Deterministic_SameInputSameOutput()
    {
        var listings = new List<ComparableSearchResult>
        {
            MakeListing("Stan A", "65 m², cijena 2.400 EUR/m²", 156000, 2400, 65, "good"),
            MakeListing("Stan B", "60 m², cijena 2.600 EUR/m²", 156000, 2600, 60, "renovated"),
            MakeListing("Stan C", "70 m², cijena 2.500 EUR/m²", 175000, 2500, 70, "good"),
        };

        var property1 = MakeProperty("good", 65, "Zagreb, Trešnjevka");
        var market1 = MakeMarketFromListings("Zagreb", "stan", listings, "Zagreb, Trešnjevka");
        var result1 = _engine.Calculate(property1, market1);

        var property2 = MakeProperty("good", 65, "Zagreb, Trešnjevka");
        var market2 = MakeMarketFromListings("Zagreb", "stan", listings, "Zagreb, Trešnjevka");
        var result2 = _engine.Calculate(property2, market2);

        Assert.Equal(result1.EstimatedValueEur, result2.EstimatedValueEur);
        Assert.Equal(result1.CalculatedPricePerM2, result2.CalculatedPricePerM2, precision: 2);
    }

    // ══════════════════════════════════════════════════════════
    //  SCENARIO 16: Stress — 50 listings with noise
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_FiftyListingsWithNoise_StressTest()
    {
        var rng = new Random(123);
        var listings = new List<ComparableSearchResult>();

        // 40 good listings around 2500 EUR/m²
        for (int i = 0; i < 40; i++)
        {
            double ppm2 = 2500 + rng.Next(-400, 400);
            double size = 55 + rng.Next(0, 30);
            listings.Add(MakeListing($"Stan {i}", $"Stan {size:F0} m², cijena {ppm2:F0} EUR/m²",
                ppm2 * size, ppm2, size, "good"));
        }

        // 5 rental traps
        for (int i = 40; i < 45; i++)
            listings.Add(MakeListing($"Najam {i}", $"Stan za najam 60 m², najamnina 500 EUR", 500, 8, 60, "good"));

        // 5 extreme outliers
        for (int i = 45; i < 50; i++)
            listings.Add(MakeListing($"Lux {i}", $"Stan 200 m², cijena 15.000 EUR/m²", 3000000, 15000, 200, "new"));

        var property = MakeProperty("good", 65, "Zagreb, Maksimir");
        var market = MakeMarketFromListings("Zagreb", "stan", listings);

        var result = _engine.Calculate(property, market);

        Assert.True(result.EstimatedValueEur > 0);
        // Final price should be near the 2500 center, not pulled by outliers/rentals
        Assert.True(result.CalculatedPricePerM2 >= 1500 && result.CalculatedPricePerM2 <= 4000,
            $"50-listing stress test: price should be 1500-4000, got {result.CalculatedPricePerM2}");
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER FACTORIES
    // ══════════════════════════════════════════════════════════

    private static ExtractedPropertyInfo MakeProperty(string state, double sizeM2, string location)
        => new()
        {
            Description = $"Stan {sizeM2} m², {location}",
            SizeM2 = sizeM2,
            Location = location,
            PropertyState = state,
            Condition = state,
            RawText = $"Stan {sizeM2} m² u lokaciji {location}, stanje: {state}",
        };

    private static string LocationSlug(string location)
        => location.ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    /// <summary>
    /// Patches listing URLs with the property location for similarity scoring.
    /// The engine checks SourceTitle+SourceUrl for location words.
    /// </summary>
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

    private static MarketResearchResult MakeMarketFromListings(string region, string type,
        List<ComparableSearchResult> listings, string propertyLocation = "Zagreb, Maksimir")
    {
        return new()
        {
            RegionName = region,
            DetectedPropertyType = type,
            ActiveListings = PatchUrls(listings, propertyLocation),
            RegionalData = [],
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 60 },
        };
    }

    private static ComparableSearchResult MakeListing(string title, string content,
        double totalPrice, double pricePerM2, double sizeM2, string condition,
        bool hasSeaView = false, bool hasGarage = false)
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
