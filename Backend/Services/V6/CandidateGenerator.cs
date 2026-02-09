using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// V6 Phase 1 — Candidate Generation (High Recall).
/// 
/// Strategy: Fires broad search queries via Tavily API across multiple
/// Croatian real-estate portals using the existing V4 "Shotgun" approach
/// but adapted for V6 canonical pipeline.
/// 
/// Target: ~50 raw candidates.
/// Goal: Maximize Recall — don't miss potential matches.
/// Search criteria: City + PropertyType + Area ±30%.
/// </summary>
public class CandidateGenerator : ICandidateGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<CandidateGenerator> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";
    private const int TargetCandidates = 50;

    private static readonly string[] Portals =
    [
        "site:njuskalo.hr",
        "site:index.hr/oglasi",
        "site:crozilla.com",
        "site:nekretnine.hr"
    ];

    private static readonly Regex PriceGateRegex = new(
        @"\b\d{1,3}(?:\.\d{3})*\s?€",
        RegexOptions.Compiled);

    private static readonly Regex PriceExtractRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SizeExtractRegex = new(
        @"(\d{1,6}(?:[,\.]\d+)?)\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string[]> TypeSynonyms = new()
    {
        ["stan"]                  = ["stan", "apartman", "garsonijera"],
        ["kuća"]                  = ["kuća", "vila", "vikendica", "obiteljska kuća"],
        ["zemljište"]             = ["građevinsko zemljište", "zemljište", "parcela"],
        ["poslovni prostor"]      = ["poslovni prostor", "ured", "lokal"],
    };

    public CandidateGenerator(HttpClient httpClient, IConfiguration config, ILogger<CandidateGenerator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["Tavily:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Tavily__ApiKey")
            ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
            ?? Environment.GetEnvironmentVariable("TAVILY_APIKEY")
            ?? "";
    }

    public async Task<List<ListingData>> GenerateCandidatesAsync(
        CanonicalPropertyModel subject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("V6 Phase 1: Tavily API key not configured — skipping search.");
            return [];
        }

        var allCandidates = new List<ListingData>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build queries with different granularity levels
        var queries = BuildSearchQueries(subject);

        _logger.LogInformation("V6 Phase 1: Firing {Count} search queries for {City} {Type}",
            queries.Count, subject.Location, subject.PropertyType);

        // Fire queries in parallel batches (max 6 concurrent)
        var semaphore = new SemaphoreSlim(6);
        var tasks = queries.Select(async query =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteTavilySearch(query, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        foreach (var batch in results)
        {
            foreach (var listing in batch)
            {
                if (seenUrls.Contains(listing.Url)) continue;
                seenUrls.Add(listing.Url);

                // Price-first gate
                if (listing.PassedPriceGate)
                {
                    allCandidates.Add(listing);
                }
            }

            if (allCandidates.Count >= TargetCandidates) break;
        }

        _logger.LogInformation("V6 Phase 1: {Count} unique candidates with prices (target: {Target})",
            allCandidates.Count, TargetCandidates);

        return allCandidates;
    }

    // ══════════════════════════════════════════════════════════
    //  QUERY BUILDER  
    // ══════════════════════════════════════════════════════════

    private List<string> BuildSearchQueries(CanonicalPropertyModel subject)
    {
        var queries = new List<string>();
        string city = subject.Location ?? "Hrvatska";
        string type = subject.PropertyType ?? "nekretnina";
        double areaMin = subject.SizeM2 * 0.7;
        double areaMax = subject.SizeM2 * 1.3;

        // Get type synonyms
        string[] synonyms = [type];
        foreach (var kvp in TypeSynonyms)
        {
            if (type.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                synonyms = kvp.Value;
                break;
            }
        }

        // Phase A: Exact queries per portal (4 portals × 1 synonym)
        foreach (var portal in Portals)
        {
            queries.Add($"{portal} {type} {city} {areaMin:F0}-{areaMax:F0} m2 cijena €");
        }

        // Phase B: Synonym expansion (top 2 portals × all synonyms)
        foreach (var synonym in synonyms.Take(3))
        {
            foreach (var portal in Portals.Take(2))
            {
                queries.Add($"{portal} {synonym} {city} prodaja");
            }
        }

        // Phase C: Broader location (parent city)
        string? broader = GetBroaderRegion(city);
        if (broader != null)
        {
            foreach (var portal in Portals.Take(2))
            {
                queries.Add($"{portal} {type} {broader} prodaja cijena");
            }
        }

        // Phase D: Area-range queries
        if (subject.SizeM2 > 0)
        {
            queries.Add($"site:njuskalo.hr {type} {city} {areaMin:F0} {areaMax:F0} m2");
            queries.Add($"site:nekretnine.hr {type} {city} {areaMin:F0} {areaMax:F0} m2 EUR");
        }

        return queries.Distinct().Take(17).ToList(); // cap at 17 queries
    }

    // ══════════════════════════════════════════════════════════
    //  TAVILY API CALL
    // ══════════════════════════════════════════════════════════

    private async Task<List<ListingData>> ExecuteTavilySearch(
        string query, CancellationToken ct)
    {
        var results = new List<ListingData>();

        try
        {
            var requestBody = new
            {
                api_key = _apiKey,
                query = query,
                search_depth = "basic",
                max_results = 10,
                include_raw_content = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(TavilyUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tavily returned {Status} for query: {Query}",
                    response.StatusCode, query[..Math.Min(60, query.Length)]);
                return results;
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (!doc.RootElement.TryGetProperty("results", out var resultsArray))
                return results;

            foreach (var item in resultsArray.EnumerateArray())
            {
                string url = item.GetProperty("url").GetString() ?? "";
                string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                string snippet = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                double score = item.TryGetProperty("score", out var s) ? s.GetDouble() : 0;

                string combinedText = $"{title} {snippet}";

                // Price-first gate
                bool hasPrice = PriceGateRegex.IsMatch(combinedText);

                var listing = new ListingData
                {
                    Url = url,
                    Title = title,
                    Snippet = snippet,
                    SearchScore = score,
                    Portal = DetectPortal(url),
                    PassedPriceGate = hasPrice,
                    ScrapedAt = DateTime.UtcNow,
                    PriceEur = ExtractPrice(combinedText),
                    SizeM2 = ExtractSize(combinedText),
                    Location = query // store query context
                };

                if (listing.PriceEur > 0 && listing.SizeM2 > 0)
                {
                    listing.PricePerM2 = listing.PriceEur.Value / listing.SizeM2.Value;
                }

                results.Add(listing);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tavily search failed for query: {Query}", query[..Math.Min(60, query.Length)]);
        }

        return results;
    }

    // ══════════════════════════════════════════════════════════
    //  PARSING HELPERS
    // ══════════════════════════════════════════════════════════

    private static double? ExtractPrice(string text)
    {
        var match = PriceExtractRegex.Match(text);
        if (!match.Success) return null;

        string raw = match.Groups[1].Value
            .Replace(".", "")
            .Replace(",", ".");

        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double val) ? val : null;
    }

    private static double? ExtractSize(string text)
    {
        var match = SizeExtractRegex.Match(text);
        if (!match.Success) return null;

        string raw = match.Groups[1].Value.Replace(",", ".");
        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double val) ? val : null;
    }

    private static string DetectPortal(string url)
    {
        if (url.Contains("njuskalo", StringComparison.OrdinalIgnoreCase)) return "njuskalo";
        if (url.Contains("index.hr", StringComparison.OrdinalIgnoreCase)) return "index";
        if (url.Contains("crozilla", StringComparison.OrdinalIgnoreCase)) return "crozilla";
        if (url.Contains("nekretnine.hr", StringComparison.OrdinalIgnoreCase)) return "nekretnine.hr";
        return "other";
    }

    private static readonly Dictionary<string, string> BroaderRegion =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Vir", "Zadar" }, { "Murter", "Šibenik" }, { "Čiovo", "Trogir" },
        { "Šolta", "Split" }, { "Brač", "Split" }, { "Hvar", "Split" },
        { "Vis", "Split" }, { "Korčula", "Dubrovnik" }, { "Mljet", "Dubrovnik" },
        { "Krk", "Rijeka" }, { "Rab", "Rijeka" }, { "Cres", "Rijeka" },
        { "Lošinj", "Rijeka" }, { "Pag", "Zadar" },
        { "Kaštela", "Split" }, { "Solin", "Split" }, { "Podstrana", "Split" },
        { "Velika Gorica", "Zagreb" }, { "Samobor", "Zagreb" },
        { "Zaprešić", "Zagreb" }, { "Opatija", "Rijeka" },
        { "Rovinj", "Pula" }, { "Poreč", "Pula" }, { "Umag", "Pula" },
        { "Makarska", "Split" }, { "Trogir", "Split" },
    };

    private static string? GetBroaderRegion(string city)
    {
        if (BroaderRegion.TryGetValue(city.Trim(), out var broader))
            return broader;

        foreach (var kvp in BroaderRegion)
        {
            if (city.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
