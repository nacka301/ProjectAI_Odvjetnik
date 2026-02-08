using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Backend.Services;

/// <summary>
/// Searches Njuškalo via Tavily for ACTIVE real-estate listings,
/// filters ghost/archived data, and provides regional benchmark context.
/// </summary>
public class MarketResearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MarketResearchService> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";

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
    /// Search Njuškalo for ACTIVE comparable listings, filter junk,
    /// and provide regional benchmark context. NEVER returns empty —
    /// always falls back to regional benchmarks.
    /// </summary>
    public async Task<MarketResearchResult> SearchComparables(
        string location, double sizeM2, string pdfText = "")
    {
        var detectedType = DetectPropertyType(pdfText);
        var regionName = ExtractRegionName(location);
        var benchmarkPerM2 = FindRegionalBenchmark(regionName);
        var now = DateTime.Now;

        _logger.LogInformation(
            "Market research: Type={Type}, Region={Region}, Benchmark={Benchmark} €/m²",
            detectedType, regionName, benchmarkPerM2);

        // ── Phase 1: Primary Njuškalo-specific search ────────────
        var primaryQuery = $"site:njuskalo.hr \"prodaja {detectedType}\" \"{regionName}\" {now.Year}";
        _logger.LogInformation("Primary Njuškalo query: {Query}", primaryQuery);

        var rawResults = await ExecuteTavilySearch(primaryQuery, maxResults: 8);
        var activeResults = FilterGhostListings(rawResults);

        _logger.LogInformation(
            "Phase 1: {Active}/{Total} active after ghost filter",
            activeResults.Count, rawResults.Count);

        // ── Phase 2: Broader search if few results ───────────────
        if (activeResults.Count <= 1)
        {
            _logger.LogWarning("Few active results. Trying broader Njuškalo + Index search.");
            var broaderQuery =
                $"prodaja {detectedType} {regionName} cijena {now.Year} site:njuskalo.hr OR site:index.hr/oglasi";
            var broaderRaw = await ExecuteTavilySearch(broaderQuery, maxResults: 6);
            var broaderActive = FilterGhostListings(broaderRaw);

            foreach (var r in broaderActive)
            {
                if (!activeResults.Any(a => a.Url == r.Url))
                    activeResults.Add(r);
            }

            _logger.LogInformation(
                "Phase 2: total active listings now = {Count}", activeResults.Count);
        }

        // ── Phase 3: Regional fallback search ────────────────────
        List<ComparableSearchResult> regionalData = [];
        if (activeResults.Count <= 1)
        {
            _logger.LogWarning("Still ≤1 active listings. Running regional fallback.");
            var regionalQuery =
                $"cijena nekretnina {regionName} {detectedType} {now.Year} €/m² prosječna cijena";
            var regionalResults = await ExecuteTavilySearch(regionalQuery, maxResults: 3);

            foreach (var r in regionalResults)
                r.Content = $"[REGIONALNI TRŽIŠNI PODACI] {r.Content}";

            regionalData = regionalResults;
        }

        // ── Phase 4: Build note ──────────────────────────────────
        var note = "";
        if (activeResults.Count == 0 && regionalData.Count == 0)
        {
            note = $"Nisu pronađeni aktivni oglasi na Njuškalo.hr, korištena službena metodologija za {now.Year}. " +
                   $"Regionalni prosjek za {regionName}: {benchmarkPerM2:N0} €/m².";
        }
        else if (activeResults.Count == 0)
        {
            note = $"Nisu pronađeni aktivni oglasi. Korišteni regionalni tržišni podaci i " +
                   $"službeni prosjek za {regionName}: {benchmarkPerM2:N0} €/m².";
        }

        return new MarketResearchResult
        {
            ActiveListings = activeResults,
            RegionalData = regionalData,
            RegionalBenchmarkPerM2 = benchmarkPerM2,
            RegionName = regionName,
            DetectedPropertyType = detectedType,
            TargetSizeM2 = sizeM2,
            Note = note
        };
    }

    // ══════════════════════════════════════════════════════════
    //  GHOST / STALE FILTER
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Remove listings that are sold, archived, expired, or reference only old years.
    /// </summary>
    private List<ComparableSearchResult> FilterGhostListings(List<ComparableSearchResult> results)
    {
        return results.Where(r =>
        {
            var combined = (r.Content + " " + r.Title).ToLowerInvariant();

            // Check ghost keywords
            if (GhostKeywords.Any(kw => combined.Contains(kw)))
            {
                _logger.LogInformation("Filtered ghost listing: {Title}", r.Title);
                return false;
            }

            // Check if content references ONLY old years (stale data)
            var mentionsCurrent = combined.Contains(DateTime.Now.Year.ToString()) ||
                                  combined.Contains((DateTime.Now.Year - 1).ToString());
            var mentionsOnlyOld = StaleYears.Any(y => combined.Contains(y.ToString())) &&
                                  !mentionsCurrent;

            if (mentionsOnlyOld)
            {
                _logger.LogInformation("Filtered stale listing (old year only): {Title}", r.Title);
                return false;
            }

            return true;
        }).ToList();
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
/// Complete market research result: active listings + benchmark context.
/// NEVER empty — always has at least a regional benchmark.
/// </summary>
public class MarketResearchResult
{
    /// <summary>Filtered active listings from Njuškalo/Index (ghost/stale removed).</summary>
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

    /// <summary>All search results combined (active + regional) for AI consumption.</summary>
    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];

    /// <summary>True if we had to rely on benchmarks because no active ads were found.</summary>
    public bool IsBenchmarkFallback => ActiveListings.Count == 0;
}
