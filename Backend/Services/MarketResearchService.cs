using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Services;

/// <summary>
/// Searches the web for comparable real-estate listings using Tavily AI.
/// </summary>
public class MarketResearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MarketResearchService> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";

    public MarketResearchService(HttpClient httpClient, IConfiguration config, ILogger<MarketResearchService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Tavily:ApiKey"]
            ?? throw new InvalidOperationException("Tavily:ApiKey is not configured.");
        _logger = logger;
    }

    /// <summary>
    /// Search Croatian real-estate portals for comparable listings.
    /// Returns up to 5 results with content + URL.
    /// If primary search returns ≤1 result, performs a fallback regional market trends search.
    /// </summary>
    public async Task<List<ComparableSearchResult>> SearchComparables(string location, double sizeM2, string pdfText = "")
    {
        var detectedType = DetectPropertyType(pdfText);
        var query = $"prodaja {detectedType} {location} {sizeM2} m2 cijena site:njuskalo.hr OR site:index.hr/oglasi";

        _logger.LogInformation("Detected property type: {Type}. Tavily search query: {Query}", detectedType, query);

        var results = await ExecuteTavilySearch(query);

        // Fallback: if primary search returned ≤1 usable results, search for regional market trends
        if (results.Count <= 1)
        {
            _logger.LogWarning("Primary search returned only {Count} results. Running fallback regional search.", results.Count);
            var fallbackResults = await SearchRegionalTrends(location, detectedType);
            if (fallbackResults.Count > 0)
            {
                // Mark fallback results so the AI knows they are regional data, not direct ads
                foreach (var fr in fallbackResults)
                {
                    fr.Content = $"[REGIONALNI TRŽIŠNI PODACI] {fr.Content}";
                }
                results.AddRange(fallbackResults);
            }
        }

        return results;
    }

    /// <summary>
    /// Fallback search for regional market trends when direct ads are blocked or unavailable.
    /// </summary>
    private async Task<List<ComparableSearchResult>> SearchRegionalTrends(string location, string propertyType)
    {
        // Extract the broad region/city name (first word or two) for wider search
        var regionName = ExtractRegionName(location);
        var query = $"tržišna kretanja nekretnina {regionName} 2025 cijena {propertyType} po m2";

        _logger.LogInformation("Fallback regional search query: {Query}", query);

        return await ExecuteTavilySearch(query, maxResults: 3);
    }

    /// <summary>
    /// Extract a broad region name from a detailed location string.
    /// E.g., "Murter, Betina, k.o. Betina" → "Murter"
    ///        "Split, Bačvice" → "Split"
    /// </summary>
    private static string ExtractRegionName(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "Hrvatska";

        // Take text before the first comma
        var firstPart = location.Split(',')[0].Trim();

        // Remove common cadastral prefixes
        firstPart = firstPart
            .Replace("k.o.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("K.O.", "")
            .Trim();

        return string.IsNullOrWhiteSpace(firstPart) ? "Hrvatska" : firstPart;
    }

    /// <summary>
    /// Execute a Tavily search with the given query and return parsed results.
    /// </summary>
    private async Task<List<ComparableSearchResult>> ExecuteTavilySearch(string query, int maxResults = 5)
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

        var tavilyResponse = JsonSerializer.Deserialize<TavilyResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (tavilyResponse?.Results is null || tavilyResponse.Results.Count == 0)
        {
            _logger.LogWarning("Tavily returned no results for query: {Query}", query);
            return [];
        }

        _logger.LogInformation("Tavily returned {Count} results for query: {Query}", tavilyResponse.Results.Count, query);

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

    /// <summary>
    /// Detect property type from PDF text to build a more accurate search query.
    /// </summary>
    private static string DetectPropertyType(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return "stan";

        var text = pdfText.ToLowerInvariant();

        // 1. Business premises
        if (text.Contains("poslovni prostor") || text.Contains("lokal"))
            return "poslovni prostor";

        // 2. House (but NOT if it explicitly mentions "stan na" or "stan u" — that's a flat inside a building)
        bool looksLikeFlat = text.Contains("stan na") || text.Contains("stan u");
        if (!looksLikeFlat &&
            (text.Contains("kuća") || text.Contains("kuca") ||
             text.Contains("vikendica") ||
             text.Contains("zgrada mješovite uporabe") || text.Contains("zgrada mjesovite uporabe") ||
             text.Contains("stambena zgrada")))
            return "kuća";

        // 3. Land
        if (text.Contains("građevinsko") || text.Contains("gradjevinsko") ||
            text.Contains("oranica") || text.Contains("pašnjak") || text.Contains("pasnjak") ||
            text.Contains("livada") || text.Contains("šuma") || text.Contains("suma") ||
            text.Contains("neizgrađeno") || text.Contains("neizgradjeno"))
            return "građevinsko zemljište";

        // 4. Default: apartment
        return "stan";
    }
}

// --- Tavily API Models ---

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

// --- Output model ---

public class ComparableSearchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
