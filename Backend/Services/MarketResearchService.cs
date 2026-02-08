using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Backend.Services;

/// <summary>
/// V3 — "Price-First Stealth" Market Research Service.
/// Every result MUST contain a price (€) to enter the pipeline.
/// Ghost/archived/stale listings are killed before they reach the engine.
/// Confidence scoring is built in — teta sees exactly how reliable the data is.
/// </summary>
public class MarketResearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MarketResearchService> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";

    // ══════════════════════════════════════════════════════════
    //  V3 PRICE-FIRST REGEX — the gatekeeper
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Matches: 150.000 €, 85.000€, 3.500 €, 250000 €, 1.200.000 €
    /// Does NOT match: "cijena na upit", "kontaktirajte nas"
    /// </summary>
    private static readonly Regex PriceGateRegex = new(
        @"\b\d{1,3}(?:\.\d{3})*\s?€",
        RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════
    //  2025/2026 REGIONAL BENCHMARKS (€/m²)
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, double> RegionalBenchmarks =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Major cities
        { "Zagreb", 2751 }, { "Split", 3728 }, { "Dubrovnik", 3217 },
        { "Rijeka", 2200 }, { "Osijek", 1200 }, { "Zadar", 2800 },
        { "Šibenik", 2500 }, { "Sibenik", 2500 }, { "Pula", 2600 },
        // Istra
        { "Istra", 2600 }, { "Rovinj", 3500 }, { "Poreč", 2800 },
        { "Porec", 2800 }, { "Umag", 2500 }, { "Novigrad", 2500 },
        { "Opatija", 3000 },
        // Coast
        { "Makarska", 3200 }, { "Trogir", 3000 }, { "Kaštela", 2400 },
        { "Kastela", 2400 }, { "Solin", 2600 }, { "Crikvenica", 2200 },
        { "Vodice", 2500 }, { "Biograd", 2400 }, { "Omiš", 2600 },
        { "Omis", 2600 },
        // Islands
        { "Murter", 2500 }, { "Hvar", 3500 }, { "Brač", 2800 },
        { "Brac", 2800 }, { "Korčula", 2500 }, { "Korcula", 2500 },
        { "Krk", 2800 }, { "Rab", 2200 }, { "Pag", 2000 },
        { "Vis", 2500 }, { "Mljet", 2000 }, { "Šolta", 2200 },
        { "Solta", 2200 }, { "Čiovo", 3000 }, { "Ciovo", 3000 },
        { "Pelješac", 2500 }, { "Peljesac", 2500 }, { "Vir", 2200 },
        // Continental
        { "Varaždin", 1400 }, { "Varazdin", 1400 }, { "Karlovac", 1100 },
        { "Slavonski Brod", 900 }, { "Vukovar", 800 }, { "Vinkovci", 850 },
        { "Požega", 800 }, { "Bjelovar", 900 }, { "Čakovec", 1200 },
        { "Koprivnica", 1100 }, { "Sisak", 800 },
    };

    private const double DefaultBenchmark = 1250; // Kontinentalna HR fallback

    // Ghost-listing keywords → listing no longer active
    private static readonly string[] GhostKeywords =
    [
        "prodano", "neaktivan", "oglas je neaktivan", "arhiva", "završen",
        "zavrseno", "istekao", "nije dostupan", "oglas uklonjen",
        "oglas je istekao", "deaktiviran", "sold", "expired"
    ];

    // "No price" markers → listing hides the price on purpose
    private static readonly string[] NoPriceMarkers =
    [
        "cijena na upit", "na upit", "price on request", "kontaktirajte",
        "dogovor", "po dogovoru"
    ];

    // Years considered "stale" — if a result references ONLY these years, skip it
    private static readonly int[] StaleYears = [2018, 2019, 2020, 2021, 2022];

    public MarketResearchService(HttpClient httpClient, IConfiguration config, ILogger<MarketResearchService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Tavily:ApiKey"]
            ?? throw new InvalidOperationException("Tavily:ApiKey is not configured.");
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// V3 "Price-First" search: every result must pass 3 gates:
    ///   Gate 1: Contains a price (number + €)
    ///   Gate 2: Not ghost/archived/expired
    ///   Gate 3: Not "cijena na upit" / stale
    /// NEVER returns empty — always falls back to regional benchmarks.
    /// </summary>
    public async Task<MarketResearchResult> SearchComparables(
        string location, double sizeM2, string pdfText = "")
    {
        var detectedType = DetectPropertyType(pdfText);
        var regionName = ExtractRegionName(location);
        var benchmarkPerM2 = FindRegionalBenchmark(regionName);
        var now = DateTime.Now;
        var searchPhase = SearchPhase.Primary; // Track which phase provided results

        _logger.LogInformation(
            "V3 Market Research: Type={Type}, Region={Region}, Benchmark={Benchmark} €/m²",
            detectedType, regionName, benchmarkPerM2);

        // ── Phase 1: Njuškalo pinpoint (ulica + naselje + €) ─────
        var primaryQuery = $"site:njuskalo.hr \"prodaja {detectedType}\" \"{regionName}\" cijena € {now.Year}";
        _logger.LogInformation("Phase 1 query: {Query}", primaryQuery);

        var rawPhase1 = await ExecuteTavilySearch(primaryQuery, maxResults: 8);
        var filtered = PriceFirstFilter(rawPhase1, "Phase 1");

        _logger.LogInformation(
            "Phase 1: {Passed}/{Total} passed Price-First filter",
            filtered.Count, rawPhase1.Count);

        // ── Phase 2: Broader search if <2 results with price ─────
        if (filtered.Count < 2)
        {
            searchPhase = SearchPhase.Broader;
            _logger.LogWarning("Phase 1 insufficient ({Count}). Expanding to Phase 2.", filtered.Count);

            var broaderQuery =
                $"prodaja {detectedType} {regionName} cijena € {now.Year} site:njuskalo.hr OR site:index.hr/oglasi";
            var rawPhase2 = await ExecuteTavilySearch(broaderQuery, maxResults: 8);
            var filteredPhase2 = PriceFirstFilter(rawPhase2, "Phase 2");

            // Merge, deduplicate by URL
            foreach (var r in filteredPhase2)
            {
                if (!filtered.Any(existing => existing.Url == r.Url))
                    filtered.Add(r);
            }

            _logger.LogInformation(
                "Phase 2: total price-verified listings = {Count}", filtered.Count);
        }

        // ── Phase 3: Regional fallback search (market data, not ads) ──
        List<ComparableSearchResult> regionalData = [];
        if (filtered.Count < 2)
        {
            searchPhase = filtered.Count == 0 ? SearchPhase.BenchmarkOnly : SearchPhase.RegionalFallback;
            _logger.LogWarning("Still <2 price-verified listings. Running Phase 3 regional fallback.");

            var regionalQuery =
                $"cijena nekretnina {regionName} {detectedType} {now.Year} €/m² prosječna cijena";
            var regionalResults = await ExecuteTavilySearch(regionalQuery, maxResults: 4);

            foreach (var r in regionalResults)
                r.Content = $"[REGIONALNI TRŽIŠNI PODACI] {r.Content}";

            regionalData = regionalResults;
        }

        // ── Confidence Scoring ───────────────────────────────────
        var distinctCount = filtered.DistinctBy(x => x.Url).Count();
        var confidence = CalculateConfidence(distinctCount, searchPhase, regionName);

        // ── Build note ───────────────────────────────────────────
        var note = BuildNote(distinctCount, searchPhase, regionName, benchmarkPerM2, now.Year);

        var result = new MarketResearchResult
        {
            ActiveListings = filtered.DistinctBy(x => x.Url).Take(8).ToList(),
            RegionalData = regionalData,
            RegionalBenchmarkPerM2 = benchmarkPerM2,
            RegionName = regionName,
            DetectedPropertyType = detectedType,
            TargetSizeM2 = sizeM2,
            Note = note,
            Confidence = confidence,
            SearchPhaseUsed = searchPhase.ToString()
        };

        _logger.LogInformation(
            "V3 Result: {Active} price-verified listings, {Regional} regional entries, " +
            "Confidence={Confidence}, Phase={Phase}",
            result.ActiveListings.Count, result.RegionalData.Count,
            confidence.Level, searchPhase);

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  V3 PRICE-FIRST FILTER — THE CORE INNOVATION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Every result must pass 3 mandatory gates:
    ///   Gate 1: Contains a visible price (number + €)
    ///   Gate 2: Not a ghost/archived/expired listing
    ///   Gate 3: Not "cijena na upit" and not stale-only years
    /// Results that fail ANY gate are logged and discarded.
    /// </summary>
    private List<ComparableSearchResult> PriceFirstFilter(
        List<ComparableSearchResult> results, string phaseName)
    {
        var passed = new List<ComparableSearchResult>();

        foreach (var r in results)
        {
            var combined = (r.Title + " " + r.Content);
            var combinedLower = combined.ToLowerInvariant();

            // ── Gate 1: Must contain a price (number + €) ───────
            if (!PriceGateRegex.IsMatch(combined))
            {
                _logger.LogInformation(
                    "[{Phase}] REJECTED (no price €): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── Gate 2: Must not be ghost/archived ──────────────
            if (GhostKeywords.Any(kw => combinedLower.Contains(kw)))
            {
                _logger.LogInformation(
                    "[{Phase}] REJECTED (ghost/archived): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── Gate 3a: Must not be "cijena na upit" ──────────
            if (NoPriceMarkers.Any(m => combinedLower.Contains(m)))
            {
                _logger.LogInformation(
                    "[{Phase}] REJECTED (cijena na upit): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── Gate 3b: Must not reference ONLY stale years ────
            var mentionsCurrent = combinedLower.Contains(DateTime.Now.Year.ToString()) ||
                                  combinedLower.Contains((DateTime.Now.Year - 1).ToString());
            var mentionsOnlyOld = StaleYears.Any(y => combinedLower.Contains(y.ToString())) &&
                                  !mentionsCurrent;

            if (mentionsOnlyOld)
            {
                _logger.LogInformation(
                    "[{Phase}] REJECTED (stale year only): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── ALL GATES PASSED ────────────────────────────────
            _logger.LogInformation(
                "[{Phase}] ✓ ACCEPTED: {Title}",
                phaseName, Truncate(r.Title, 60));
            passed.Add(r);
        }

        return passed;
    }

    // ══════════════════════════════════════════════════════════
    //  CONFIDENCE SCORING 🧠
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate confidence based on:
    ///   - Number of price-verified listings
    ///   - Which search phase provided them
    ///   - Whether we know the region benchmark
    /// </summary>
    private ConfidenceScore CalculateConfidence(
        int priceVerifiedCount, SearchPhase phase, string regionName)
    {
        bool hasKnownBenchmark = RegionalBenchmarks.ContainsKey(regionName) ||
            RegionalBenchmarks.Keys.Any(k =>
                regionName.Contains(k, StringComparison.OrdinalIgnoreCase));

        // ── HIGH: 3+ price-verified listings from Phase 1 ───────
        if (priceVerifiedCount >= 3 && phase == SearchPhase.Primary)
        {
            return new ConfidenceScore
            {
                Level = "Visoka",
                Score = Math.Min(95, 70 + priceVerifiedCount * 5),
                Explanation = $"Pronađeno {priceVerifiedCount} aktivnih oglasa s cijenom na Njuškalo.hr za ciljanu lokaciju.",
                Color = "green"
            };
        }

        // ── MEDIUM-HIGH: 3+ listings but needed Phase 2 ────────
        if (priceVerifiedCount >= 3 && phase == SearchPhase.Broader)
        {
            return new ConfidenceScore
            {
                Level = "Srednja-Visoka",
                Score = 70,
                Explanation = $"Pronađeno {priceVerifiedCount} oglasa s cijenom, ali uz proširenu pretragu na širu regiju.",
                Color = "green"
            };
        }

        // ── MEDIUM: 1-2 price-verified listings ─────────────────
        if (priceVerifiedCount >= 1)
        {
            return new ConfidenceScore
            {
                Level = "Srednja",
                Score = hasKnownBenchmark ? 55 : 45,
                Explanation = priceVerifiedCount == 1
                    ? $"Pronađen 1 oglas s cijenom + regionalni benchmark za {regionName}."
                    : $"Pronađena {priceVerifiedCount} oglasa s cijenom, proširena pretraga na regiju.",
                Color = "yellow"
            };
        }

        // ── LOW: No price-verified listings at all ──────────────
        if (hasKnownBenchmark)
        {
            return new ConfidenceScore
            {
                Level = "Niska",
                Score = 30,
                Explanation = $"Nema aktivnih oglasa s cijenom. Korišten regionalni benchmark za {regionName}.",
                Color = "red"
            };
        }

        // ── VERY LOW: No listings + unknown region ──────────────
        return new ConfidenceScore
        {
            Level = "Vrlo niska",
            Score = 15,
            Explanation = "Nema oglasa s cijenom i regija nije u bazi benchmarkova. Korišten nacionalni prosjek.",
            Color = "red"
        };
    }

    // ══════════════════════════════════════════════════════════
    //  NOTE BUILDER
    // ══════════════════════════════════════════════════════════

    private static string BuildNote(
        int priceVerifiedCount, SearchPhase phase, string regionName,
        double benchmarkPerM2, int currentYear)
    {
        if (priceVerifiedCount >= 3 && phase == SearchPhase.Primary)
            return ""; // No note needed — solid data

        if (priceVerifiedCount >= 3 && phase == SearchPhase.Broader)
            return $"Pretraga proširena izvan uže lokacije na cijelu regiju {regionName}.";

        if (priceVerifiedCount >= 1)
            return $"Pronađen {(priceVerifiedCount == 1 ? "samo 1 oglas" : $"{priceVerifiedCount} oglasa")} " +
                   $"s verificiranom cijenom. Korišten benchmark od {benchmarkPerM2:N0} €/m² kao dopuna.";

        return $"Nisu pronađeni aktivni oglasi s cijenom za {currentYear}. " +
               $"Korišten regionalni benchmark za {regionName}: {benchmarkPerM2:N0} €/m².";
    }

    // ══════════════════════════════════════════════════════════
    //  REGIONAL BENCHMARK LOOKUP
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Match a location string to the nearest regional benchmark (€/m²).
    /// </summary>
    private static double FindRegionalBenchmark(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return DefaultBenchmark;

        // Exact key match
        foreach (var (region, benchmark) in RegionalBenchmarks)
        {
            if (location.Contains(region, StringComparison.OrdinalIgnoreCase))
                return benchmark;
        }

        // Word-level match
        var words = location.Split([' ', ',', '-', '/'],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.Length < 3) continue;
            foreach (var (region, benchmark) in RegionalBenchmarks)
            {
                if (region.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                    word.Contains(region, StringComparison.OrdinalIgnoreCase))
                    return benchmark;
            }
        }

        return DefaultBenchmark;
    }

    // ══════════════════════════════════════════════════════════
    //  TAVILY API
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Execute a Tavily search and return parsed results.
    /// </summary>
    private async Task<List<ComparableSearchResult>> ExecuteTavilySearch(
        string query, int maxResults = 5)
    {
        var requestBody = new TavilyRequest
        {
            ApiKey = _apiKey,
            Query = query,
            SearchDepth = "advanced",
            MaxResults = maxResults,
            IncludeAnswer = false
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(TavilyUrl, jsonContent);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();

        var tavilyResponse = JsonSerializer.Deserialize<TavilyResponse>(
            responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

        if (tavilyResponse?.Results is null || tavilyResponse.Results.Count == 0)
        {
            _logger.LogWarning("Tavily returned no results for query: {Query}", query);
            return [];
        }

        _logger.LogInformation("Tavily returned {Count} results for query: {Query}",
            tavilyResponse.Results.Count, query);

        return tavilyResponse.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Content))
            .Select(r => new ComparableSearchResult
            {
                Url = r.Url ?? "",
                Title = r.Title ?? "",
                Content = r.Content ?? ""
            })
            .ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Extract a broad region name from a detailed location string.
    /// "Murter, Betina, k.o. Betina" → "Murter"
    /// "Split, Bačvice" → "Split"
    /// </summary>
    private static string ExtractRegionName(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "Hrvatska";

        var firstPart = location.Split(',')[0].Trim();

        firstPart = firstPart
            .Replace("k.o.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("K.O.", "")
            .Trim();

        return string.IsNullOrWhiteSpace(firstPart) ? "Hrvatska" : firstPart;
    }

    /// <summary>
    /// Detect property type from PDF text.
    /// </summary>
    private static string DetectPropertyType(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return "stan";

        var text = pdfText.ToLowerInvariant();

        if (text.Contains("poslovni prostor") || text.Contains("lokal"))
            return "poslovni prostor";

        bool looksLikeFlat = text.Contains("stan na") || text.Contains("stan u");
        if (!looksLikeFlat &&
            (text.Contains("kuća") || text.Contains("kuca") ||
             text.Contains("vikendica") ||
             text.Contains("zgrada mješovite uporabe") ||
             text.Contains("zgrada mjesovite uporabe") ||
             text.Contains("stambena zgrada")))
            return "kuća";

        if (text.Contains("građevinsko") || text.Contains("gradjevinsko") ||
            text.Contains("oranica") || text.Contains("pašnjak") ||
            text.Contains("pasnjak") || text.Contains("livada") ||
            text.Contains("šuma") || text.Contains("suma") ||
            text.Contains("neizgrađeno") || text.Contains("neizgradjeno"))
            return "građevinsko zemljište";

        return "stan";
    }

    /// <summary>Truncate a string for logging.</summary>
    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}

// ══════════════════════════════════════════════════════════
//  SEARCH PHASE ENUM
// ══════════════════════════════════════════════════════════

public enum SearchPhase
{
    Primary,         // Phase 1: Njuškalo pinpoint — ulica + naselje
    Broader,         // Phase 2: Expanded to broader region + Index.hr
    RegionalFallback,// Phase 3: Only regional market data articles
    BenchmarkOnly    // Phase 3: Zero listings, pure benchmark
}

// ══════════════════════════════════════════════════════════
//  CONFIDENCE SCORING MODEL
// ══════════════════════════════════════════════════════════

/// <summary>
/// Confidence score for the valuation — shown to teta on screen.
/// </summary>
public class ConfidenceScore
{
    /// <summary>Human-readable level: Visoka / Srednja-Visoka / Srednja / Niska / Vrlo niska</summary>
    public string Level { get; set; } = "";

    /// <summary>Numeric score 0-100 for sorting/comparison.</summary>
    public int Score { get; set; }

    /// <summary>Croatian explanation of WHY this confidence level.</summary>
    public string Explanation { get; set; } = "";

    /// <summary>CSS color hint: green / yellow / red.</summary>
    public string Color { get; set; } = "";
}

// ══════════════════════════════════════════════════════════
//  TAVILY API MODELS
// ══════════════════════════════════════════════════════════

public class TavilyRequest
{
    public string ApiKey { get; set; } = "";
    public string Query { get; set; } = "";
    public string SearchDepth { get; set; } = "advanced";
    public int MaxResults { get; set; } = 5;
    public bool IncludeAnswer { get; set; } = false;
}

public class TavilyResponse
{
    public List<TavilyResult> Results { get; set; } = [];
}

public class TavilyResult
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Content { get; set; }
    public double Score { get; set; }
}

// ══════════════════════════════════════════════════════════
//  OUTPUT MODELS
// ══════════════════════════════════════════════════════════

public class ComparableSearchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>
/// Complete market research result: price-verified listings + benchmark context.
/// NEVER empty — always has at least a regional benchmark.
/// </summary>
public class MarketResearchResult
{
    /// <summary>Price-verified, ghost-filtered active listings from Njuškalo/Index.</summary>
    public List<ComparableSearchResult> ActiveListings { get; set; } = [];

    /// <summary>Regional fallback data (used when active listings are insufficient).</summary>
    public List<ComparableSearchResult> RegionalData { get; set; } = [];

    /// <summary>Regional benchmark €/m² from 2025/2026 official data.</summary>
    public double RegionalBenchmarkPerM2 { get; set; }

    /// <summary>Matched region name (e.g., "Split", "Murter").</summary>
    public string RegionName { get; set; } = "";

    /// <summary>Detected property type from PDF.</summary>
    public string DetectedPropertyType { get; set; } = "";

    /// <summary>Target property size for context.</summary>
    public double TargetSizeM2 { get; set; }

    /// <summary>Informational note about data quality / fallback usage.</summary>
    public string Note { get; set; } = "";

    /// <summary>V3 Confidence scoring — level + explanation + color.</summary>
    public ConfidenceScore Confidence { get; set; } = new();

    /// <summary>Which search phase ultimately provided the data.</summary>
    public string SearchPhaseUsed { get; set; } = "";

    /// <summary>All search results combined (active + regional) for AI consumption.</summary>
    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];

    /// <summary>True if we had to rely on benchmarks because no active ads were found.</summary>
    public bool IsBenchmarkFallback => ActiveListings.Count == 0;
}
