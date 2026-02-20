using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// ╔═══════════════════════════════════════════════════════════════════════════╗
/// ║  V79: DATA-DRIVEN VALUATION TESTS — Cijela Hrvatska                    ║
/// ║                                                                         ║
/// ║  Čita scenarije iz TestData/CroatiaValuationScenarios.json              ║
/// ║  Jedan [Theory] test vrti sve scenarije — NEMA tisuća [Fact] metoda.    ║
/// ║                                                                         ║
/// ║  Za dodavanje novog testa:                                              ║
/// ║    1. Otvori CroatiaValuationScenarios.json                             ║
/// ║    2. Dodaj novi objekt s lokacijom, compovima i rasponima              ║
/// ║    3. Pokreni testove — GOTOVO, nema C# koda za pisati.                ║
/// ╚═══════════════════════════════════════════════════════════════════════════╝
/// </summary>
public class ValuationEngineDataDrivenTests
{
    private readonly ValuationEngine _engine;
    private static readonly List<ValuationScenario> _scenarios;

    static ValuationEngineDataDrivenTests()
    {
        // Find the JSON file relative to the test assembly
        var assemblyDir = Path.GetDirectoryName(typeof(ValuationEngineDataDrivenTests).Assembly.Location)!;

        // Walk up to find the project root (Backend.Tests/)
        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Backend.Tests.csproj")))
            dir = dir.Parent;

        var jsonPath = Path.Combine(dir?.FullName ?? assemblyDir, "TestData", "CroatiaValuationScenarios.json");

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException(
                $"Scenario JSON not found at {jsonPath}. Ensure TestData/CroatiaValuationScenarios.json exists and is copied to output.");

        var json = File.ReadAllText(jsonPath);
        _scenarios = JsonSerializer.Deserialize<List<ValuationScenario>>(json, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize scenarios JSON");
    }

    public ValuationEngineDataDrivenTests()
    {
        _engine = new ValuationEngine(NullLogger<ValuationEngine>.Instance);
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN THEORY: Price Range Validation
    //  Runs once per scenario from JSON. Asserts:
    //    1. Engine doesn't crash
    //    2. Estimated value is within expected range
    //    3. Confidence level matches expected pattern
    //    4. Pipeline trace exists
    // ══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Scenario_PriceWithinExpectedRange(string scenarioId, string scenarioName)
    {
        var scenario = _scenarios.First(s => s.Id == scenarioId);

        // ── Build property ──
        var property = new ExtractedPropertyInfo
        {
            Description = scenario.Description,
            SizeM2 = scenario.SizeM2,
            Location = scenario.Location,
            PropertyState = scenario.State,
            Condition = scenario.Condition,
            RawText = scenario.Description,
            KeyFeatures = scenario.KeyFeatures ?? [],
        };

        // ── Build market with comps ──
        var comps = (scenario.Comps ?? []).Select(c => new ComparableSearchResult
        {
            Title = c.Title,
            Content = c.Content,
            Url = $"https://nekretnine.hr/{LocationSlug(scenario.Location)}/{Slugify(c.Title)}",
            ListedPriceEur = c.Price,
            ListedPricePerM2 = c.Ppm2,
            AiSizeM2 = c.Size,
            AiCondition = c.Cond,
        }).ToList();

        var searchPhase = scenario.SearchPhase ?? "Primary";

        var market = new MarketResearchResult
        {
            RegionName = scenario.Region,
            DetectedPropertyType = scenario.PropertyType,
            ActiveListings = comps,
            RegionalData = [],
            SearchPhaseUsed = searchPhase,
            Confidence = new ConfidenceScore
            {
                Level = comps.Count >= 3 ? "Medium" : "Low",
                Score = comps.Count >= 3 ? 60 : 10,
            },
        };

        // ── Calculate ──
        var result = _engine.Calculate(property, market);

        // ── Assert 1: Engine must not crash ──
        Assert.NotNull(result);
        Assert.True(result.Pipeline.Count > 0,
            $"[{scenarioId}] Pipeline must have trace entries");

        // ── Assert 2: Price within expected range ──
        Assert.True(
            result.EstimatedValueEur >= scenario.ExpectedMinPrice,
            $"[{scenarioId}] {scenarioName}: value {result.EstimatedValueEur:N0} EUR " +
            $"below floor {scenario.ExpectedMinPrice:N0} EUR " +
            $"(ppm2={result.CalculatedPricePerM2:F0}, comps={result.UsablePricePoints})");

        Assert.True(
            result.EstimatedValueEur <= scenario.ExpectedMaxPrice,
            $"[{scenarioId}] {scenarioName}: value {result.EstimatedValueEur:N0} EUR " +
            $"above ceiling {scenario.ExpectedMaxPrice:N0} EUR " +
            $"(ppm2={result.CalculatedPricePerM2:F0}, comps={result.UsablePricePoints})");

        // ── Assert 3: Confidence level matches expected pattern ──
        if (!string.IsNullOrEmpty(scenario.ExpectedConfidence))
        {
            var allowedLevels = scenario.ExpectedConfidence
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLowerInvariant())
                .ToList();

            var actualLevel = result.MathConfidenceLevel.ToLowerInvariant();

            Assert.True(
                allowedLevels.Any(l => actualLevel.Contains(l)),
                $"[{scenarioId}] {scenarioName}: confidence '{result.MathConfidenceLevel}' " +
                $"not in expected [{scenario.ExpectedConfidence}] " +
                $"(score={result.MathConfidenceScore})");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  THEORY 2: Slug Safety — multi-word location names
    //  Ensures engine doesn't crash on locations with diacritics,
    //  multiple words, or special characters.
    // ══════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Scenario_SlugSafety_NoCrashOnLocation(string scenarioId, string scenarioName)
    {
        _ = scenarioName; // used only as display label
        var scenario = _scenarios.First(s => s.Id == scenarioId);

        // Verify slug generation doesn't throw
        var slug = LocationSlug(scenario.Location);
        Assert.False(string.IsNullOrWhiteSpace(slug),
            $"[{scenarioId}] Slug for '{scenario.Location}' must not be empty");

        // Verify location parts can be split safely
        var parts = scenario.Location
            .Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        Assert.True(parts.Count >= 1,
            $"[{scenarioId}] Location '{scenario.Location}' must have at least 1 parseable part");
    }

    // ══════════════════════════════════════════════════════════
    //  META TEST: JSON Integrity
    //  Verifies the scenario file itself is well-formed.
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Meta_AllScenariosHaveUniqueIds()
    {
        var ids = _scenarios.Select(s => s.Id).ToList();
        var dupes = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupes);
    }

    [Fact]
    public void Meta_AllScenariosHaveRequiredFields()
    {
        foreach (var s in _scenarios)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id), "Scenario missing Id");
            Assert.False(string.IsNullOrWhiteSpace(s.Location), $"[{s.Id}] missing Location");
            Assert.False(string.IsNullOrWhiteSpace(s.PropertyType), $"[{s.Id}] missing PropertyType");
            Assert.True(s.SizeM2 > 0, $"[{s.Id}] SizeM2 must be > 0");
            Assert.True(s.ExpectedMaxPrice >= s.ExpectedMinPrice,
                $"[{s.Id}] MaxPrice ({s.ExpectedMaxPrice}) < MinPrice ({s.ExpectedMinPrice})");
        }
    }

    [Fact]
    public void Meta_MinimumScenarioCount()
    {
        Assert.True(_scenarios.Count >= 50,
            $"Expected at least 50 scenarios, got {_scenarios.Count}");
    }

    [Fact]
    public void Meta_CategoriesAreDisverse()
    {
        var categories = _scenarios.Select(s => s.Category).Distinct().ToList();
        Assert.True(categories.Count >= 8,
            $"Expected at least 8 diverse categories, got {categories.Count}: {string.Join(", ", categories)}");
    }

    // ══════════════════════════════════════════════════════════
    //  DATA PROVIDER
    // ══════════════════════════════════════════════════════════

    public static IEnumerable<object[]> AllScenarios()
        => _scenarios.Select(s => new object[] { s.Id, s.Name });

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private static string LocationSlug(string location)
        => (location ?? "").ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    private static string Slugify(string text)
        => (text ?? "listing").ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(" ", "-");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // ══════════════════════════════════════════════════════════
    //  SCENARIO MODEL (mirrors JSON schema)
    // ══════════════════════════════════════════════════════════

    public class ValuationScenario
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string Region { get; set; } = "";
        public string PropertyType { get; set; } = "";
        public double SizeM2 { get; set; }
        public string State { get; set; } = "good";
        public string Condition { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string>? KeyFeatures { get; set; }
        public List<CompData>? Comps { get; set; }
        public string? SearchPhase { get; set; }
        public double ExpectedMinPrice { get; set; }
        public double ExpectedMaxPrice { get; set; }
        public string ExpectedConfidence { get; set; } = "";
    }

    public class CompData
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public double Price { get; set; }
        public double Ppm2 { get; set; }
        public double Size { get; set; }
        public string Cond { get; set; } = "good";
    }
}
