using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// V6 Phase 1 — Candidate Generation with IRON GATE + JUNK DETECTION + FUNNEL.
/// 
/// Critical fixes (V6.1):
///   1. IRON GATE: Strict type matching — a House CANNOT match Land, etc.
///      Applied BEFORE any similarity scoring.
///   2. JUNK DETECTION: Discard articles, market reports, news — only real listings.
///   3. FUNNEL: Over-fetch 60+ → Iron Gate → Junk Filter → Top N.
///      If &lt;10 remain, automatically widen search and retry (max 3 rounds).
/// 
/// GUARANTEE: Minimum 10 valid, type-matched, non-junk comparables
/// (or exhaust all 3 search rounds trying).
/// </summary>
public class CandidateGenerator : ICandidateGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<CandidateGenerator> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";
    private const int MinValidCandidates = 10;       // guaranteed minimum after filtering
    private const int MaxSearchRounds = 3;            // retry rounds with wider search

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

    // ══════════════════════════════════════════════════════════
    //  IRON GATE — TYPE CLASSIFICATION KEYWORDS
    // ══════════════════════════════════════════════════════════

    private static readonly Dictionary<PropertyCategory, string[]> TypeKeywords = new()
    {
        [PropertyCategory.Apartment] = [
            "stan", "apartman", "garsonijera", "dvosoban", "trosoban",
            "jednosoban", "četverosoban", "penthouse", "studio"
        ],
        [PropertyCategory.House] = [
            "kuća", "kuca", "vila", "vikendica", "obiteljska kuća",
            "dvojna kuća", "house", "cottage", "katnica", "prizemnica"
        ],
        [PropertyCategory.Land] = [
            "zemljište", "zemljiste", "parcela", "oranica", "teren",
            "građevinsko zemljište", "gradjevinsko", "building land",
            "poljoprivredno", "šuma", "suma"
        ],
        [PropertyCategory.Commercial] = [
            "poslovni prostor", "ured", "lokal", "trgovina",
            "skladište", "skladiste", "office", "ugostiteljski"
        ]
    };

    /// <summary>
    /// EXCLUSION keywords: if subject is type X, these in a listing → INSTANT DISCARD.
    /// </summary>
    private static readonly Dictionary<PropertyCategory, string[]> ExclusionKeywords = new()
    {
        [PropertyCategory.Apartment] = [
            "zemljište", "zemljiste", "parcela", "teren", "oranica",
            "kuća", "kuca", "vila", "vikendica", "poslovni prostor"
        ],
        [PropertyCategory.House] = [
            "zemljište", "zemljiste", "teren", "oranica", "parcela",
            "stan", "apartman", "garsonijera", "poslovni prostor"
        ],
        [PropertyCategory.Land] = [
            "kuća", "kuca", "stan", "apartman", "vila", "vikendica",
            "poslovni prostor", "ured", "lokal"
        ],
        [PropertyCategory.Commercial] = [
            "stan", "apartman", "kuća", "kuca", "vila", "vikendica",
            "zemljište", "zemljiste", "parcela", "oranica"
        ]
    };

    // ══════════════════════════════════════════════════════════
    //  JUNK DETECTION — ANTI-ARTICLE FILTER
    // ══════════════════════════════════════════════════════════

    private static readonly string[] JunkUrlPatterns =
    [
        "/vijesti/", "/news/", "/blog/", "/magazin/", "/article/",
        "/izvjestaj/", "/analiza/", "/trendovi/", "/clanak/",
        "/press/", "/info/", "/savjeti/"
    ];

    private static readonly string[] JunkTitleKeywords =
    [
        "tržište", "trziste", "izvještaj", "izvjestaj", "kretanje cijena",
        "vijesti", "analiza tržišta", "trend", "statistika",
        "prosječna cijena", "prosjecna cijena", "rast cijena", "pad cijena",
        "prognoza", "savjet", "kako kupiti", "kako prodati", "vodič", "vodic",
        "report", "market overview", "news", "porezna reforma",
        "zakon o", "propisi", "regulativa", "indeks cijena"
    ];

    // ══════════════════════════════════════════════════════════
    //  TYPE SYNONYMS FOR SEARCH QUERY BUILDING
    // ══════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════
    //  MAIN ENTRY — FUNNEL STRATEGY WITH AUTO-RETRY
    // ══════════════════════════════════════════════════════════════

    public async Task<List<ListingData>> GenerateCandidatesAsync(
        CanonicalPropertyModel subject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("V6 Phase 1: Tavily API key not configured — skipping search.");
            return [];
        }

        var allValid = new List<ListingData>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalFetched = 0, discardedType = 0, discardedJunk = 0, discardedNoPrice = 0;

        // Ensure subject has a type category
        if (subject.TypeCategory == PropertyCategory.Unknown)
            subject.TypeCategory = ClassifyPropertyType(subject.PropertyType);

        _logger.LogInformation(
            "V6 Phase 1: Subject type={Type} ({Category}), location={City}, size={Size:F0} m²",
            subject.PropertyType, subject.TypeCategory, subject.Location, subject.SizeM2);

        // ── FUNNEL: Up to 3 search rounds with increasing radius ──
        for (int round = 0; round < MaxSearchRounds; round++)
        {
            double areaExpandFactor = 1.0 + (round * 0.15);
            bool useBroaderLocation = round >= 1;
            bool useDeepSearch = round >= 2;

            var queries = BuildSearchQueries(subject, areaExpandFactor, useBroaderLocation, useDeepSearch);

            _logger.LogInformation(
                "V6 Phase 1 Round {Round}: Firing {Count} queries (broader={Broader})",
                round + 1, queries.Count, useBroaderLocation);

            var semaphore = new SemaphoreSlim(6);
            var tasks = queries.Select(async query =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try { return await ExecuteTavilySearch(query, cancellationToken); }
                finally { semaphore.Release(); }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var batch in results)
            {
                foreach (var listing in batch)
                {
                    if (seenUrls.Contains(listing.Url)) continue;
                    seenUrls.Add(listing.Url);
                    totalFetched++;

                    // ── GATE 1: PRICE ──
                    if (!listing.PassedPriceGate || listing.PriceEur is null or <= 0)
                    { discardedNoPrice++; continue; }

                    // ── GATE 2: JUNK DETECTION ──
                    if (!PassesJunkFilter(listing))
                    { listing.PassedJunkFilter = false; discardedJunk++; continue; }

                    // ── GATE 3: IRON GATE (Type Matching) ──
                    if (!PassesIronGate(listing, subject.TypeCategory))
                    { listing.PassedIronGate = false; discardedType++; continue; }

                    listing.PassedIronGate = true;
                    listing.PassedJunkFilter = true;
                    allValid.Add(listing);
                }
            }

            _logger.LogInformation(
                "V6 Phase 1 Round {Round}: {Valid} valid (fetched={Fetched}, junk={Junk}, type={Type}, noPrice={Price})",
                round + 1, allValid.Count, totalFetched, discardedJunk, discardedType, discardedNoPrice);

            if (allValid.Count >= MinValidCandidates) break;

            if (round < MaxSearchRounds - 1)
                _logger.LogInformation("V6 Phase 1: Only {Count} valid — widening search (round {Next})",
                    allValid.Count, round + 2);
        }

        _logger.LogInformation(
            "V6 Phase 1 FINAL: {Valid}/{Fetched} passed all gates (junk={Junk}, type={Type}, noPrice={Price})",
            allValid.Count, totalFetched, discardedJunk, discardedType, discardedNoPrice);

        return allValid;
    }

    // ══════════════════════════════════════════════════════════
    //  IRON GATE — Strict Type Match
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns FALSE (= discard) if listing is the wrong property type.
    /// If subject is House, the listing MUST NOT be Land/Apartment/etc.
    /// </summary>
    public static bool PassesIronGate(ListingData listing, PropertyCategory subjectCategory)
    {
        if (subjectCategory == PropertyCategory.Unknown) return true;

        string titleLower = listing.Title.ToLowerInvariant();
        string combinedLower = $"{listing.Title} {listing.Snippet} {listing.RawDescription}"
            .ToLowerInvariant();

        // Check exclusion keywords
        if (ExclusionKeywords.TryGetValue(subjectCategory, out var exclusions))
        {
            foreach (var keyword in exclusions)
            {
                if (titleLower.Contains(keyword))
                {
                    // If the TITLE explicitly has an exclusion keyword,
                    // check if it ALSO matches the correct type in the title
                    if (TypeKeywords.TryGetValue(subjectCategory, out var positives))
                    {
                        bool titleAlsoCorrect = positives.Any(p => titleLower.Contains(p));
                        if (titleAlsoCorrect) continue; // e.g. "kuća sa zemljištem" for House
                    }
                    return false; // DISCARD
                }
            }
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════
    //  JUNK DETECTION — Anti-Article Filter
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Filters out articles, market reports, news, and non-listing content.
    /// </summary>
    public static bool PassesJunkFilter(ListingData listing)
    {
        string urlLower = listing.Url.ToLowerInvariant();
        string titleLower = listing.Title.ToLowerInvariant();
        string snippetLower = (listing.Snippet ?? "").ToLowerInvariant();

        // Check URL path patterns
        foreach (var pattern in JunkUrlPatterns)
            if (urlLower.Contains(pattern)) return false;

        // Check title for article/report keywords
        foreach (var keyword in JunkTitleKeywords)
            if (titleLower.Contains(keyword)) return false;

        // Heuristic: 3+ market buzzwords in snippet → market report
        int buzzCount = 0;
        string[] buzzwords = ["prosječna", "prosjecna", "prosjek", "rast", "pad",
            "statistik", "indeks", "trend", "prognoz", "tržišt", "trzist"];
        foreach (var bw in buzzwords)
            if (snippetLower.Contains(bw)) buzzCount++;
        if (buzzCount >= 3) return false;

        // If not from a known portal, require both price and size
        string[] validPortals = ["njuskalo", "index.hr", "crozilla", "nekretnine.hr",
            "oglasnik", "plavi-oglasnik"];
        bool isFromPortal = validPortals.Any(p => urlLower.Contains(p));
        if (!isFromPortal && (listing.PriceEur is null or <= 0 || listing.SizeM2 is null or <= 0))
            return false;

        return true;
    }

    // ══════════════════════════════════════════════════════════
    //  TYPE CLASSIFICATION (public static — reusable)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Classify a property type string into a normalized category.
    /// </summary>
    public static PropertyCategory ClassifyPropertyType(string? propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType)) return PropertyCategory.Unknown;
        string lower = propertyType.ToLowerInvariant();

        foreach (var kvp in TypeKeywords)
            foreach (var keyword in kvp.Value)
                if (lower.Contains(keyword)) return kvp.Key;

        return PropertyCategory.Unknown;
    }

    /// <summary>
    /// Detect property type from listing text (title + snippet).
    /// </summary>
    public static PropertyCategory DetectListingType(string title, string snippet)
    {
        string combined = $"{title} {snippet}".ToLowerInvariant();

        foreach (var kvp in TypeKeywords)
            foreach (var keyword in kvp.Value)
                if (combined.Contains(keyword)) return kvp.Key;

        return PropertyCategory.Unknown;
    }

    // ══════════════════════════════════════════════════════════
    //  QUERY BUILDER (with round-based widening)
    // ══════════════════════════════════════════════════════════

    private List<string> BuildSearchQueries(
        CanonicalPropertyModel subject,
        double areaExpandFactor,
        bool useBroaderLocation,
        bool useDeepSearch)
    {
        var queries = new List<string>();
        string city = subject.Location ?? "Hrvatska";
        string type = subject.PropertyType ?? "nekretnina";
        double areaMin = subject.SizeM2 * (1.0 - 0.30 * areaExpandFactor);
        double areaMax = subject.SizeM2 * (1.0 + 0.30 * areaExpandFactor);
        if (areaMin < 1) areaMin = 1;

        string[] synonyms = [type];
        foreach (var kvp in TypeSynonyms)
        {
            if (type.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            { synonyms = kvp.Value; break; }
        }

        // Phase A: Exact per portal
        foreach (var portal in Portals)
            queries.Add($"{portal} {type} {city} prodaja cijena €");

        // Phase B: With area range
        if (subject.SizeM2 > 0)
        {
            foreach (var portal in Portals.Take(2))
                queries.Add($"{portal} {type} {city} {areaMin:F0}-{areaMax:F0} m2 cijena €");
        }

        // Phase C: Synonym expansion
        foreach (var synonym in synonyms.Take(3))
            foreach (var portal in Portals.Take(2))
                queries.Add($"{portal} {synonym} {city} prodaja");

        // Phase D: Broader location (round 2+)
        if (useBroaderLocation)
        {
            string searchCity = GetBroaderRegion(city) ?? city;
            foreach (var portal in Portals)
                queries.Add($"{portal} {type} {searchCity} prodaja cijena");
            foreach (var synonym in synonyms.Take(2))
                queries.Add($"site:njuskalo.hr {synonym} {searchCity} prodaja €");
        }

        // Phase E: Deep search (round 3)
        if (useDeepSearch)
        {
            queries.Add($"{type} {city} oglasi prodaja nekretnine cijena");
            queries.Add($"{type} {city} kupovina EUR m2");
            string? broader = GetBroaderRegion(city);
            if (broader != null)
            {
                queries.Add($"{type} {broader} oglasi prodaja cijena €");
                queries.Add($"{type} {broader} nekretnine EUR m2 prodaja");
            }
        }

        return queries.Distinct().Take(20).ToList();
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
                bool hasPrice = PriceGateRegex.IsMatch(combinedText);
                var detectedCategory = DetectListingType(title, snippet);

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
                    Location = query,
                    DetectedType = detectedCategory.ToString()
                };

                if (listing.PriceEur > 0 && listing.SizeM2 > 0)
                    listing.PricePerM2 = listing.PriceEur.Value / listing.SizeM2.Value;

                results.Add(listing);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tavily search failed for query: {Query}",
                query[..Math.Min(60, query.Length)]);
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
