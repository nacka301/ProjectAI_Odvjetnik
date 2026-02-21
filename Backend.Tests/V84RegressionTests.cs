using Backend.Services;
using Backend.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// V84 — Regression tests for 3 critical fixes:
///   BUG 1: Intel/News articles must NOT enter ValuationEngine math pipeline
///   BUG 2: City name match must guarantee ≥60% similarity score
///   BUG 3: Sanity check — apartments below 400 €/m² trigger InvalidDataException
/// </summary>
public class V84RegressionTests
{
    private static readonly ILogger<ValuationEngine> Logger =
        LoggerFactory.Create(b => b.AddDebug()).CreateLogger<ValuationEngine>();

    // ══════════════════════════════════════════════════════════
    //  BUG 1: Intel/News articles blocked from engine
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Simulates the exact Dubrovnik bug: 3 real Njuškalo listings (4933–6000 €/m²)
    /// mixed with 5 Phase6-Intel articles (575–800 €/m² from news). Before V84, the
    /// articles contaminated the math pipeline → 671 €/m². After V84, ONLY the 3 real
    /// listings should be used → result should be ~4500–6000 €/m².
    /// </summary>
    [Fact]
    public void BUG1_IntelArticles_ExcludedFromEngine_DubrovnikScenario()
    {
        var engine = new ValuationEngine(Logger);

        // Real Njuškalo listings (ActiveListings)
        var activeListings = new List<ComparableSearchResult>
        {
            MakeListing("Luksuzni Stan: Dubrovnik, Gruž", "Stan 65 m² Dubrovnik Gruž pogled na more",
                320000, 4923, 65, "renovated", "https://njuskalo.hr/oglas/dubrovnik-1"),
            MakeListing("Apartman Dubrovnik centar", "Stan 50 m² stari grad Dubrovnik",
                300000, 6000, 50, "renovated", "https://njuskalo.hr/oglas/dubrovnik-2"),
            MakeListing("Dvosoban stan Dubrovnik Lapad", "Stan 75 m² Dubrovnik Lapad",
                375000, 5000, 75, "good", "https://njuskalo.hr/oglas/dubrovnik-3"),
        };

        // Phase6-Intel articles (RegionalData) — these MUST NOT enter engine
        var intelArticles = new List<ComparableSearchResult>
        {
            MakeArticle("Prosječne cijene nekretnina u Hrvatskoj 2026",
                "[REGIONALNI TRŽIŠNI PODACI] Prosječna cijena stana u Dubrovniku iznosi 575 €/m²",
                "https://gohome.hr/dubrovnik-prosjecne-cijene"),
            MakeArticle("Tržište nekretnina Dalmacija",
                "[REGIONALNI TRŽIŠNI PODACI] Cijena po kvadratu u Dubrovniku kreće se od 600 €/m²",
                "https://example.com/vijesti/trziste-dalmacija"),
            MakeArticle("Dubrovnik nekretnine analiza",
                "[REGIONALNI TRŽIŠNI PODACI] Prosječna cijena 700 €/m² za stanove",
                "https://example.com/blog/dubrovnik-analiza"),
            MakeArticle("GoHome Dubrovnik Prosjek",
                "[REGIONALNI TRŽIŠNI PODACI] Prosjek cijena 650 €/m²",
                "https://gohome.hr/dubrovnik-analiza"),
            MakeArticle("Index Vijesti Dubrovnik",
                "[REGIONALNI TRŽIŠNI PODACI] Cijena nekretnina 800 €/m²",
                "https://index.hr/vijesti/dubrovnik-nekretnine"),
        };

        var property = MakeProperty("Luksuzni stan 85 m² u Dubrovniku", 85, "Dubrovnik centar", "good");
        var marketData = new MarketResearchResult
        {
            RegionName = "Dubrovnik",
            DetectedPropertyType = "Stan",
            SearchPhaseUsed = "Primary",
            ActiveListings = activeListings,
            RegionalData = intelArticles,
        };

        var result = engine.Calculate(property, marketData);

        // With V84 BUG 1 fix: engine should ONLY use the 3 real listings (4923–6000 €/m²)
        // NOT the 5 articles (575–800 €/m²). Result must be in realistic Dubrovnik range.
        Assert.True(result.CalculatedPricePerM2 >= 3500,
            $"V84 BUG1: Calculated price {result.CalculatedPricePerM2} €/m² is too low — " +
            $"Intel articles leaked into engine! Expected ≥3500 for Dubrovnik");
        Assert.True(result.CalculatedPricePerM2 <= 8000,
            $"Calculated price {result.CalculatedPricePerM2} €/m² unexpectedly high");
        Assert.True(result.EstimatedValueEur >= 200000 && result.EstimatedValueEur <= 700000,
            $"Total value {result.EstimatedValueEur} EUR outside expected 200K-700K for Dubrovnik 85m²");

        // Verify pipeline mentions Intel blocking
        Assert.Contains(result.Pipeline, p => p.Contains("Intel/News"));
    }

    /// <summary>
    /// IsNewsOrArticle correctly identifies Phase6 tagged articles and GoHome URLs.
    /// </summary>
    [Theory]
    [InlineData("[REGIONALNI TRŽIŠNI PODACI] Prosječna cijena stanova", "https://gohome.hr/test", true)]
    [InlineData("[REGIONALNI TRŽIŠNI PODACI] Cijena kvadrata", "https://example.com/clanak/1", true)]
    [InlineData("Stan 65 m² Dubrovnik Gruž", "https://njuskalo.hr/oglas/123456", false)]
    [InlineData("Apartman prodaja Split", "https://njuskalo.hr/oglas/split-abc", false)]
    [InlineData("Market report 2026 prices", "https://example.com/blog/report", true)]
    public void BUG1_IsNewsOrArticle_CorrectlyClassifies(string content, string url, bool expectedIsArticle)
    {
        // Use reflection to call private static IsNewsOrArticle
        var method = typeof(ValuationEngine).GetMethod("IsNewsOrArticle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var listing = new ComparableSearchResult
        {
            Title = content.Length > 60 ? content[..60] : content,
            Content = content,
            Url = url,
        };

        var result = (bool)method!.Invoke(null, [listing])!;
        Assert.Equal(expectedIsArticle, result);
    }

    // ══════════════════════════════════════════════════════════
    //  BUG 2: City name match → guaranteed ≥60% similarity
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// When Location = "Dubrovnik centar" (space-separated, no comma), and comp text
    /// contains "Dubrovnik", math score must be ≥60%. Before V84, the full-string
    /// "dubrovnik centar" didn't match in comp text → score was 0.03.
    /// </summary>
    [Fact]
    public void BUG2_CityMatch_DubrovnikCentar_SpaceSeparated_Gets60Percent()
    {
        // Call CalculateSimilarityScore via reflection
        var engine = new ValuationEngine(Logger);
        var method = typeof(ValuationEngine).GetMethod("CalculateSimilarityScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var comp = new ExtractedPrice
        {
            SourceTitle = "Luksuzni Stan: Dubrovnik, Gruž",
            SourceUrl = "https://njuskalo.hr/oglas/dubrovnik-gruz-luksuzni-stan-12345",
            PricePerM2 = 5000,
        };

        var target = new ExtractedPropertyInfo
        {
            Location = "Dubrovnik centar",  // Space-separated! No comma!
            SizeM2 = 85,
            PropertyState = "good",
        };

        var score = (double)method!.Invoke(engine, [comp, target, "Stan", "Dubrovnik"])!;

        // V84: word-level city match should give 0.60 for location + size/condition bonuses  
        Assert.True(score >= 0.60,
            $"V84 BUG2: Score {score:P0} is too low for city match 'Dubrovnik centar' vs " +
            $"'Dubrovnik, Gruž'. Expected ≥60% (word-level city match).");
    }

    /// <summary>
    /// With comma-separated location "Dubrovnik, centar" — locParts[0]="dubrovnik" should
    /// be found in comp text → 0.60 neighborhood match.
    /// </summary>
    [Fact]
    public void BUG2_CityMatch_DubrovnikCentar_CommaSeparated()
    {
        var engine = new ValuationEngine(Logger);
        var method = typeof(ValuationEngine).GetMethod("CalculateSimilarityScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var comp = new ExtractedPrice
        {
            SourceTitle = "Stan Dubrovnik Lapad 75m²",
            SourceUrl = "https://njuskalo.hr/oglas/dubrovnik-lapad-456",
            PricePerM2 = 4500,
        };

        var target = new ExtractedPropertyInfo
        {
            Location = "Dubrovnik, centar",
            SizeM2 = 85,
            PropertyState = "good",
        };

        var score = (double)method!.Invoke(engine, [comp, target, "Stan", "Dubrovnik"])!;
        Assert.True(score >= 0.60,
            $"Score {score:P0} too low for 'Dubrovnik, centar' vs comp with 'Dubrovnik'");
    }

    /// <summary>
    /// Single-word location "Dubrovnik" (V83 fix) should also give ≥60%.
    /// </summary>
    [Fact]
    public void BUG2_CityMatch_SingleWord_Dubrovnik()
    {
        var engine = new ValuationEngine(Logger);
        var method = typeof(ValuationEngine).GetMethod("CalculateSimilarityScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var comp = new ExtractedPrice
        {
            SourceTitle = "Apartman Dubrovnik prodaja",
            SourceUrl = "https://njuskalo.hr/oglas/dubrovnik-789",
            PricePerM2 = 5500,
        };

        var target = new ExtractedPropertyInfo
        {
            Location = "Dubrovnik",
            SizeM2 = 85,
            PropertyState = "good",
        };

        var score = (double)method!.Invoke(engine, [comp, target, "Stan", "Dubrovnik"])!;
        Assert.True(score >= 0.60,
            $"Score {score:P0} too low for single-word 'Dubrovnik' city match");
    }

    /// <summary>
    /// Croatian inflected city names should still match via CroatianStemMatch.
    /// "Dubrovniku" in comp text should match target location "Dubrovnik".
    /// </summary>
    [Fact]
    public void BUG2_CityMatch_InflectedForm_Dubrovniku()
    {
        var engine = new ValuationEngine(Logger);
        var method = typeof(ValuationEngine).GetMethod("CalculateSimilarityScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var comp = new ExtractedPrice
        {
            SourceTitle = "Prodaja stana u Dubrovniku",
            SourceUrl = "https://njuskalo.hr/oglas/dubrovniku-101",
            PricePerM2 = 4800,
        };

        var target = new ExtractedPropertyInfo
        {
            Location = "Dubrovnik centar",
            SizeM2 = 70,
            PropertyState = "good",
        };

        var score = (double)method!.Invoke(engine, [comp, target, "Stan", "Dubrovnik"])!;
        Assert.True(score >= 0.60,
            $"Score {score:P0} too low — 'Dubrovniku' should stem-match 'Dubrovnik'");
    }

    // ══════════════════════════════════════════════════════════
    //  BUG 3: Sanity check — contaminated apartment prices throw
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// If engine somehow produces ≤400 €/m² for an apartment, it must throw
    /// InvalidDataException. Only article-contaminated data can produce this.
    /// </summary>
    [Fact]
    public void BUG3_SanityCheck_ApartmentBelow400_ThrowsInvalidData()
    {
        var engine = new ValuationEngine(Logger);

        // Intentionally garbage data — all comps at 300 €/m² for an apartment
        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Fake A", "Stan 60 m² Dubrovnik", 18000, 300, 60, "good",
                "https://njuskalo.hr/oglas/fake-a"),
            MakeListing("Fake B", "Stan 65 m² Dubrovnik", 19500, 300, 65, "good",
                "https://njuskalo.hr/oglas/fake-b"),
            MakeListing("Fake C", "Stan 70 m² Dubrovnik", 21000, 300, 70, "good",
                "https://njuskalo.hr/oglas/fake-c"),
        };

        var property = MakeProperty("Stan 85 m² Dubrovnik", 85, "Dubrovnik", "good");
        var marketData = new MarketResearchResult
        {
            RegionName = "Dubrovnik",
            DetectedPropertyType = "Stan",
            SearchPhaseUsed = "Primary",
            ActiveListings = comps,
            RegionalData = [],
        };

        var ex = Assert.Throws<InvalidDataException>(() => engine.Calculate(property, marketData));
        Assert.Contains("SANITY CHECK FAILED", ex.Message);
        Assert.Contains("kontaminiran", ex.Message);
    }

    /// <summary>
    /// Sanity check should NOT fire for houses — rural houses CAN be 200 €/m².
    /// </summary>
    [Fact]
    public void BUG3_SanityCheck_HouseAt200_DoesNotThrow()
    {
        var engine = new ValuationEngine(Logger);

        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Kuća Sisak A", "Kuća 200 m² Sisak", 40000, 200, 200, "original",
                "https://njuskalo.hr/oglas/sisak-a"),
            MakeListing("Kuća Sisak B", "Kuća 180 m² Sisak", 36000, 200, 180, "original",
                "https://njuskalo.hr/oglas/sisak-b"),
            MakeListing("Kuća Sisak C", "Kuća 220 m² Sisak", 44000, 200, 220, "original",
                "https://njuskalo.hr/oglas/sisak-c"),
            MakeListing("Kuća Sisak D", "Kuća 190 m² Sisak", 38000, 200, 190, "original",
                "https://njuskalo.hr/oglas/sisak-d"),
        };

        var property = MakeProperty("Kuća 200 m²", 200, "Sisak", "original");
        var marketData = new MarketResearchResult
        {
            RegionName = "Sisak",
            DetectedPropertyType = "Kuća",
            SearchPhaseUsed = "Primary",
            ActiveListings = comps,
            RegionalData = [],
        };

        // Should NOT throw — houses at 200 €/m² are legitimate in rural Croatia
        var result = engine.Calculate(property, marketData);
        Assert.True(result.EstimatedValueEur > 0);
    }

    /// <summary>
    /// Sanity check should NOT fire for ruševina (ruin) — even apartments.
    /// </summary>
    [Fact]
    public void BUG3_SanityCheck_ApartmentRuin_DoesNotThrow()
    {
        var engine = new ValuationEngine(Logger);

        var comps = new List<ComparableSearchResult>
        {
            MakeListing("Stan ruševina A", "Stan 60 m² Sisak ruševina", 12000, 200, 60, "needs_work",
                "https://njuskalo.hr/oglas/ruin-a"),
            MakeListing("Stan ruševina B", "Stan 55 m² Sisak ruševina", 11000, 200, 55, "needs_work",
                "https://njuskalo.hr/oglas/ruin-b"),
            MakeListing("Stan ruševina C", "Stan 65 m² Sisak ruševina", 13000, 200, 65, "needs_work",
                "https://njuskalo.hr/oglas/ruin-c"),
        };

        // Subject explicitly marked as ruševina
        var property = new ExtractedPropertyInfo
        {
            Description = "Stan u ruševini, potrebna kompletna obnova",
            SizeM2 = 60,
            Location = "Sisak",
            PropertyState = "needs_work",
            Condition = "needs_work",
            RawText = "Ruševina stana u Sisku",
            AdditionalNotes = "ruševina, devastirano",
            MicroLocation = "",
            KeyFeatures = [],
        };

        var marketData = new MarketResearchResult
        {
            RegionName = "Sisak",
            DetectedPropertyType = "Stan",
            SearchPhaseUsed = "Primary",
            ActiveListings = comps,
            RegionalData = [],
        };

        // Ruševina exemption — should NOT throw even at 200 €/m²
        var result = engine.Calculate(property, marketData);
        Assert.True(result.EstimatedValueEur > 0);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private static ExtractedPropertyInfo MakeProperty(
        string desc, double sizeM2, string location, string state)
    {
        return new ExtractedPropertyInfo
        {
            Description = desc,
            SizeM2 = sizeM2,
            Location = location,
            Condition = state,
            PropertyState = state,
            RawText = $"{desc} u {location}",
            AdditionalNotes = "",
            MicroLocation = "",
            KeyFeatures = [],
        };
    }

    private static ComparableSearchResult MakeListing(
        string title, string content, double totalPrice, double pricePerM2,
        double sizeM2, string condition, string url)
    {
        return new ComparableSearchResult
        {
            Title = title,
            Url = url,
            Content = content,
            ListedPriceEur = totalPrice,
            ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2,
            AiCondition = condition,
            AiSimilarityScore = 0.75,
        };
    }

    private static ComparableSearchResult MakeArticle(
        string title, string content, string url)
    {
        return new ComparableSearchResult
        {
            Title = title,
            Url = url,
            Content = content,
            // Articles typically don't have structured price data from portal
            ListedPriceEur = null,
            ListedPricePerM2 = null,
            AiSizeM2 = null,
            AiCondition = null,
            AiSimilarityScore = null,
        };
    }
}
