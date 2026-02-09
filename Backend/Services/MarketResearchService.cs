using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Backend.Services;

/// <summary>
/// V4 — "Shotgun" Market Research Service.
/// Fires MANY parallel searches across multiple portals, query variations,
/// property-type synonyms, and location hierarchy levels.
/// Every result must pass the Price-First gate before entering the pipeline.
/// </summary>
public class MarketResearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MarketResearchService> _logger;

    private const string TavilyUrl = "https://api.tavily.com/search";

    // ══════════════════════════════════════════════════════════
    //  PRICE-FIRST REGEX — the gatekeeper
    // ══════════════════════════════════════════════════════════
    private static readonly Regex PriceGateRegex = new(
        @"\b\d{1,3}(?:\.\d{3})*\s?€",
        RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════
    //  PROPERTY TYPE SYNONYMS — more search surface
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> TypeSynonyms = new()
    {
        ["stan"]                  = ["stan", "apartman", "garsonijera", "dvosoban", "trosoban", "jednosoban"],
        ["kuća"]                  = ["kuća", "vila", "vikendica", "obiteljska kuća", "dvojna kuća"],
        ["građevinsko zemljište"] = ["građevinsko zemljište", "zemljište", "parcela", "oranica", "građevinski teren"],
        ["poslovni prostor"]      = ["poslovni prostor", "ured", "lokal", "trgovina", "skladište"],
    };

    // ══════════════════════════════════════════════════════════
    //  PORTALS — multiple Croatian RE portals
    // ══════════════════════════════════════════════════════════
    private static readonly string[] Portals =
    [
        "site:njuskalo.hr",
        "site:index.hr/oglasi",
        "site:crozilla.com",
        "site:nekretnine.hr",
    ];

    // ══════════════════════════════════════════════════════════
    //  LOCATION HIERARCHY MAPPING (broader region for islands/suburbs)
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> BroaderRegion =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Island → nearest coast city / county
        { "Vir", "Zadar" }, { "Murter", "Šibenik" }, { "Čiovo", "Trogir" },
        { "Ciovo", "Trogir" }, { "Šolta", "Split" }, { "Solta", "Split" },
        { "Brač", "Split" }, { "Brac", "Split" }, { "Hvar", "Split" },
        { "Vis", "Split" }, { "Korčula", "Dubrovnik" }, { "Korcula", "Dubrovnik" },
        { "Mljet", "Dubrovnik" }, { "Pelješac", "Dubrovnik" }, { "Peljesac", "Dubrovnik" },
        { "Krk", "Rijeka" }, { "Rab", "Rijeka" }, { "Cres", "Rijeka" },
        { "Lošinj", "Rijeka" }, { "Losinj", "Rijeka" }, { "Pag", "Zadar" },
        { "Ugljan", "Zadar" }, { "Pašman", "Zadar" }, { "Pasman", "Zadar" },
        { "Dugi Otok", "Zadar" },
        // Zagreb suburbs
        { "Velika Gorica", "Zagreb" }, { "Samobor", "Zagreb" },
        { "Zaprešić", "Zagreb" }, { "Zapresic", "Zagreb" },
        { "Sesvete", "Zagreb" }, { "Dugo Selo", "Zagreb" },
        // Split suburbs
        { "Kaštela", "Split" }, { "Kastela", "Split" },
        { "Solin", "Split" }, { "Podstrana", "Split" },
        { "Stobreč", "Split" }, { "Stobrec", "Split" },
        // Rijeka suburbs
        { "Opatija", "Rijeka" }, { "Crikvenica", "Rijeka" },
        // Zadar suburbs
        { "Nin", "Zadar" }, { "Sukošan", "Zadar" }, { "Sukosan", "Zadar" },
        { "Bibinje", "Zadar" }, { "Privlaka", "Zadar" },
        // Šibenik suburbs
        { "Vodice", "Šibenik" }, { "Tribunj", "Šibenik" },
        { "Primošten", "Šibenik" }, { "Primosten", "Šibenik" },
        // Split coast
        { "Makarska", "Split" }, { "Omiš", "Split" }, { "Omis", "Split" },
        { "Trogir", "Split" }, { "Supetar", "Split" },
        // Istra
        { "Rovinj", "Pula" }, { "Poreč", "Pula" }, { "Porec", "Pula" },
        { "Umag", "Pula" }, { "Novigrad", "Pula" }, { "Medulin", "Pula" },
        { "Labin", "Pula" }, { "Buzet", "Pula" },
    };

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

    private const double DefaultBenchmark = 1250;

    // Ghost-listing keywords
    private static readonly string[] GhostKeywords =
    [
        "prodano", "neaktivan", "oglas je neaktivan", "arhiva", "završen",
        "zavrseno", "istekao", "nije dostupan", "oglas uklonjen",
        "oglas je istekao", "deaktiviran", "sold", "expired"
    ];

    // "No price" markers
    private static readonly string[] NoPriceMarkers =
    [
        "cijena na upit", "na upit", "price on request", "kontaktirajte",
        "dogovor", "po dogovoru"
    ];

    private static readonly int[] StaleYears = [2018, 2019, 2020, 2021, 2022];

    public MarketResearchService(HttpClient httpClient, IConfiguration config, ILogger<MarketResearchService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Tavily:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Tavily__ApiKey")
            ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
            ?? Environment.GetEnvironmentVariable("TAVILY_APIKEY")
            ?? throw new InvalidOperationException(
                "Tavily API key is not configured. Set Tavily__ApiKey or TAVILY_API_KEY environment variable.");
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT — V4 "SHOTGUN"
    // ══════════════════════════════════════════════════════════

    public async Task<MarketResearchResult> SearchComparables(
        string location, double sizeM2, string pdfText = "")
    {
        var detectedType = DetectPropertyType(pdfText);
        var regionName = ExtractRegionName(location);
        var subLocations = ExtractLocationHierarchy(location);
        var benchmarkPerM2 = FindRegionalBenchmark(regionName);
        var synonyms = GetTypeSynonyms(detectedType);
        var now = DateTime.Now;
        var searchPhase = SearchPhase.Primary;
        int totalApiCalls = 0;

        // Master list — all price-verified results across all phases
        var allVerified = new List<ComparableSearchResult>();

        _logger.LogInformation(
            "V4 Shotgun: Type={Type}, Synonyms=[{Syns}], Region={Region}, SubLocs=[{Subs}], Bench={Bench} €/m²",
            detectedType, string.Join(", ", synonyms), regionName,
            string.Join(" → ", subLocations), benchmarkPerM2);

        // ─────────────────────────────────────────────────────
        // PHASE 1: Njuškalo pinpoint — exact location, main type + synonym
        //   Fires 3 parallel queries
        // ─────────────────────────────────────────────────────
        {
            var queries = new List<string>
            {
                // Q1: Main type, exact location, Njuškalo
                $"site:njuskalo.hr prodaja {detectedType} {regionName} cijena € {now.Year}",
                // Q2: Synonym variation
                $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} {regionName} € {now.Year}",
                // Q3: Include m² if known
                sizeM2 > 0
                    ? $"site:njuskalo.hr {detectedType} {regionName} {sizeM2:0}m2 €"
                    : $"site:njuskalo.hr nekretnina {regionName} prodaja € {now.Year}",
            };

            var phase1Results = await FireParallelSearches(queries, 7, "Phase1");
            totalApiCalls += queries.Count;
            var filtered = PriceFirstFilter(phase1Results, "Phase1");
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 1: {New}/{Raw} passed → total={Total}",
                filtered.Count, phase1Results.Count, allVerified.Count);
        }

        // ─────────────────────────────────────────────────────
        // PHASE 2: Multi-portal spread — same location, different portals
        //   Fires 3 parallel queries (only if <3 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3)
        {
            searchPhase = SearchPhase.MultiPortal;

            var queries = new List<string>
            {
                $"site:index.hr/oglasi prodaja {detectedType} {regionName} €",
                $"site:crozilla.com {detectedType} {regionName} prodaja €",
                $"site:nekretnine.hr {detectedType} {regionName} cijena €",
            };

            var phase2Results = await FireParallelSearches(queries, 5, "Phase2-Portals");
            totalApiCalls += queries.Count;
            var filtered = PriceFirstFilter(phase2Results, "Phase2");
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 2 (multi-portal): +{New} → total={Total}",
                filtered.Count, allVerified.Count);
        }

        // ─────────────────────────────────────────────────────
        // PHASE 3: Wider region — go from Murter → Šibenik, Vir → Zadar, etc.
        //   Fires 4 parallel queries (only if <3 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3)
        {
            searchPhase = SearchPhase.WiderRegion;

            // Get the broader region (island → nearest city)
            var widerRegion = GetBroaderRegion(regionName);

            var queries = new List<string>
            {
                $"site:njuskalo.hr prodaja {detectedType} {widerRegion} cijena € {now.Year}",
                $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} {widerRegion} €",
                $"prodaja {detectedType} {widerRegion} cijena € site:njuskalo.hr OR site:index.hr/oglasi",
                $"site:njuskalo.hr {detectedType} {widerRegion} {now.Year} €/m²",
            };

            var phase3Results = await FireParallelSearches(queries, 6, "Phase3-Wider");
            totalApiCalls += queries.Count;
            var filtered = PriceFirstFilter(phase3Results, "Phase3");
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 3 (wider: {Region}→{Wider}): +{New} → total={Total}",
                regionName, widerRegion, filtered.Count, allVerified.Count);
        }

        // ─────────────────────────────────────────────────────
        // PHASE 4: Synonym blitz — try ALL synonyms across all portals
        //   Fires up to 4 parallel queries (only if <3 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3)
        {
            searchPhase = SearchPhase.SynonymBlitz;

            var widerRegion = GetBroaderRegion(regionName);
            var queries = new List<string>();

            // Build queries from remaining synonyms
            foreach (var syn in synonyms.Skip(1).Take(3))
            {
                queries.Add($"site:njuskalo.hr prodaja {syn} {regionName} € {now.Year}");
            }
            // Also try with "nekretnina" generic + all portals
            queries.Add($"prodaja nekretnina {regionName} cijena € {now.Year} site:njuskalo.hr OR site:crozilla.com");

            if (queries.Count > 0)
            {
                var phase4Results = await FireParallelSearches(queries, 5, "Phase4-Synonyms");
                totalApiCalls += queries.Count;
                var filtered = PriceFirstFilter(phase4Results, "Phase4");
                MergeInto(allVerified, filtered);

                _logger.LogInformation("Phase 4 (synonym blitz): +{New} → total={Total}",
                    filtered.Count, allVerified.Count);
            }
        }

        // ─────────────────────────────────────────────────────
        // PHASE 5: Sub-location drill — try every part of the address
        //   "Vir, Torovi, k.o. Vir" → try "Torovi", "Vir Torovi"
        //   Fires 1 query per sub-location (only if <2 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 2 && subLocations.Count > 1)
        {
            searchPhase = SearchPhase.SubLocationDrill;

            var queries = subLocations
                .Where(loc => loc != regionName && loc.Length >= 3)
                .Take(3)
                .Select(loc => $"site:njuskalo.hr prodaja {detectedType} {loc} cijena €")
                .ToList();

            if (queries.Count > 0)
            {
                var phase5Results = await FireParallelSearches(queries, 5, "Phase5-SubLoc");
                totalApiCalls += queries.Count;
                var filtered = PriceFirstFilter(phase5Results, "Phase5");
                MergeInto(allVerified, filtered);

                _logger.LogInformation("Phase 5 (sub-location): +{New} → total={Total}",
                    filtered.Count, allVerified.Count);
            }
        }

        // ─────────────────────────────────────────────────────
        // PHASE 6: Market intelligence — articles about prices (always if <5)
        //   These are NOT listings but price data articles
        // ─────────────────────────────────────────────────────
        List<ComparableSearchResult> regionalData = [];
        if (allVerified.Count < 5)
        {
            if (allVerified.Count == 0)
                searchPhase = SearchPhase.BenchmarkOnly;
            else if (allVerified.Count < 2 && searchPhase >= SearchPhase.WiderRegion)
                searchPhase = SearchPhase.RegionalFallback;

            var widerRegion = GetBroaderRegion(regionName);
            var queries = new List<string>
            {
                $"prosječna cijena {detectedType} {regionName} {now.Year} €/m²",
                $"tržište nekretnina {widerRegion} {now.Year} cijena kvadrat",
            };

            var phase6Results = await FireParallelSearches(queries, 4, "Phase6-Intel");
            totalApiCalls += queries.Count;

            foreach (var r in phase6Results)
                r.Content = $"[REGIONALNI TRŽIŠNI PODACI] {r.Content}";

            regionalData = phase6Results;

            _logger.LogInformation("Phase 6 (market intel): +{Count} articles", regionalData.Count);
        }

        // ─────────────────────────────────────────────────────
        // ASSEMBLE RESULT
        // ─────────────────────────────────────────────────────
        var distinctListings = allVerified
            .DistinctBy(x => x.Url)
            .Take(12)
            .ToList();

        var confidence = CalculateConfidence(distinctListings.Count, searchPhase, regionName);
        var note = BuildNote(distinctListings.Count, searchPhase, regionName, benchmarkPerM2, now.Year);

        var result = new MarketResearchResult
        {
            ActiveListings = distinctListings,
            RegionalData = regionalData,
            RegionalBenchmarkPerM2 = benchmarkPerM2,
            RegionName = regionName,
            DetectedPropertyType = detectedType,
            TargetSizeM2 = sizeM2,
            Note = note,
            Confidence = confidence,
            SearchPhaseUsed = searchPhase.ToString(),
            TotalApiCalls = totalApiCalls,
        };

        _logger.LogInformation(
            "V4 FINAL: {Active} price-verified, {Regional} articles, " +
            "Confidence={Level} ({Score}/100), Phase={Phase}, API calls={Calls}",
            result.ActiveListings.Count, result.RegionalData.Count,
            confidence.Level, confidence.Score, searchPhase, totalApiCalls);

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  PARALLEL SEARCH ENGINE
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Fire multiple Tavily searches in parallel, merge all results.
    /// </summary>
    private async Task<List<ComparableSearchResult>> FireParallelSearches(
        List<string> queries, int maxResultsPerQuery, string phaseName)
    {
        _logger.LogInformation("[{Phase}] Firing {Count} parallel queries", phaseName, queries.Count);

        var tasks = queries.Select(q => ExecuteTavilySearch(q, maxResultsPerQuery));
        var results = await Task.WhenAll(tasks);

        var merged = new List<ComparableSearchResult>();
        for (int i = 0; i < results.Length; i++)
        {
            _logger.LogInformation("[{Phase}] Query {Index}: {Count} raw results — {Query}",
                phaseName, i + 1, results[i].Count, Truncate(queries[i], 80));
            merged.AddRange(results[i]);
        }

        return merged;
    }

    // ══════════════════════════════════════════════════════════
    //  PRICE-FIRST FILTER
    // ══════════════════════════════════════════════════════════

    // Rent-trap constants
    private const double MinSalePriceEur = 15_000;
    private const double MinPricePerM2 = 500;

    private static readonly string[] RentalKeywords =
    [
        "najam", "iznajmljivanje", "iznajmi", "rent", "rental",
        "mjesečno", "mjesecno", "monthly", "polog", "deposit",
        "za najam", "u najam", "for rent", "do iznajmljivanja",
        "kratkoročni", "kratkorocni", "dugoročni", "dugorocni",
        "najamnina", "rezije", "režije"
    ];

    private static readonly Regex PriceAmountRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private List<ComparableSearchResult> PriceFirstFilter(
        List<ComparableSearchResult> results, string phaseName)
    {
        var passed = new List<ComparableSearchResult>();

        foreach (var r in results)
        {
            var combined = r.Title + " " + r.Content;
            var combinedLower = combined.ToLowerInvariant();

            // Gate 1: Must contain a price (number + €)
            if (!PriceGateRegex.IsMatch(combined))
            {
                _logger.LogDebug("[{Phase}] REJECTED (no price €): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // Gate 2: Must not be ghost/archived
            if (GhostKeywords.Any(kw => combinedLower.Contains(kw)))
            {
                _logger.LogDebug("[{Phase}] REJECTED (ghost): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // Gate 3a: Must not be "cijena na upit"
            if (NoPriceMarkers.Any(m => combinedLower.Contains(m)))
            {
                _logger.LogDebug("[{Phase}] REJECTED (na upit): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // Gate 3b: Must not reference ONLY stale years
            var mentionsCurrent = combinedLower.Contains(DateTime.Now.Year.ToString()) ||
                                  combinedLower.Contains((DateTime.Now.Year - 1).ToString());
            var mentionsOnlyOld = StaleYears.Any(y => combinedLower.Contains(y.ToString())) &&
                                  !mentionsCurrent;
            if (mentionsOnlyOld)
            {
                _logger.LogDebug("[{Phase}] REJECTED (stale): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── GATE 4: RENT-TRAP — Rental / Parking / Sub-€15k ──

            // 4a: Rental keyword detection
            if (RentalKeywords.Any(kw => combinedLower.Contains(kw)))
            {
                _logger.LogDebug("[{Phase}] REJECTED (rental keyword): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // 4b: Price floor — extract first price and reject if < €15k
            var priceMatch = PriceAmountRegex.Match(combined);
            if (priceMatch.Success)
            {
                string priceStr = priceMatch.Groups[1].Value
                    .Replace(".", "").Replace(",", ".");
                if (double.TryParse(priceStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double extractedPrice))
                {
                    if (extractedPrice < MinSalePriceEur)
                    {
                        _logger.LogDebug("[{Phase}] REJECTED (price €{Price:N0} < €15k floor): {Title}",
                            phaseName, extractedPrice, Truncate(r.Title, 60));
                        continue;
                    }
                }
            }

            _logger.LogInformation("[{Phase}] ✓ ACCEPTED: {Title}", phaseName, Truncate(r.Title, 60));
            passed.Add(r);
        }

        return passed;
    }

    // ══════════════════════════════════════════════════════════
    //  MERGE HELPER (deduplicate by URL)
    // ══════════════════════════════════════════════════════════

    private static void MergeInto(
        List<ComparableSearchResult> master, List<ComparableSearchResult> incoming)
    {
        foreach (var r in incoming)
        {
            if (!master.Any(existing =>
                existing.Url == r.Url ||
                (existing.Title == r.Title && existing.Title.Length > 10)))
            {
                master.Add(r);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  CONFIDENCE SCORING
    // ══════════════════════════════════════════════════════════

    private ConfidenceScore CalculateConfidence(
        int priceVerifiedCount, SearchPhase phase, string regionName)
    {
        bool hasKnownBenchmark = RegionalBenchmarks.ContainsKey(regionName) ||
            RegionalBenchmarks.Keys.Any(k =>
                regionName.Contains(k, StringComparison.OrdinalIgnoreCase));

        // HIGH: 5+ from early phases
        if (priceVerifiedCount >= 5 && phase <= SearchPhase.MultiPortal)
        {
            return new ConfidenceScore
            {
                Level = "Visoka",
                Score = Math.Min(97, 75 + priceVerifiedCount * 3),
                Explanation = $"Pronađeno {priceVerifiedCount} aktivnih oglasa s cijenom u prvim pretragama.",
                Color = "green"
            };
        }

        // HIGH: 3+ from Phase 1 or 2
        if (priceVerifiedCount >= 3 && phase <= SearchPhase.MultiPortal)
        {
            return new ConfidenceScore
            {
                Level = "Visoka",
                Score = Math.Min(90, 70 + priceVerifiedCount * 4),
                Explanation = $"Pronađeno {priceVerifiedCount} oglasa s cijenom na ciljnoj lokaciji.",
                Color = "green"
            };
        }

        // MEDIUM-HIGH: 3+ but needed wider region
        if (priceVerifiedCount >= 3)
        {
            return new ConfidenceScore
            {
                Level = "Srednja-Visoka",
                Score = 68,
                Explanation = $"Pronađeno {priceVerifiedCount} oglasa s cijenom uz proširenu pretragu.",
                Color = "green"
            };
        }

        // MEDIUM: 1-2
        if (priceVerifiedCount >= 1)
        {
            return new ConfidenceScore
            {
                Level = "Srednja",
                Score = hasKnownBenchmark ? 50 : 40,
                Explanation = priceVerifiedCount == 1
                    ? $"Pronađen samo 1 oglas s cijenom + regionalni benchmark za {regionName}."
                    : $"Pronađena {priceVerifiedCount} oglasa s cijenom uz benchmark dopunu.",
                Color = "yellow"
            };
        }

        // LOW
        if (hasKnownBenchmark)
        {
            return new ConfidenceScore
            {
                Level = "Niska",
                Score = 25,
                Explanation = $"Nema aktivnih oglasa s cijenom unatoč {(int)SearchPhase.BenchmarkOnly + 1} faza pretrage. Korišten benchmark za {regionName}.",
                Color = "red"
            };
        }

        // VERY LOW
        return new ConfidenceScore
        {
            Level = "Vrlo niska",
            Score = 12,
            Explanation = "Nema oglasa s cijenom, nepoznata regija. Korišten nacionalni prosjek.",
            Color = "red"
        };
    }

    // ══════════════════════════════════════════════════════════
    //  NOTE BUILDER
    // ══════════════════════════════════════════════════════════

    private static string BuildNote(
        int count, SearchPhase phase, string region, double bench, int year)
    {
        if (count >= 5 && phase <= SearchPhase.MultiPortal)
            return "";

        if (count >= 3 && phase <= SearchPhase.MultiPortal)
            return $"Pronađeno {count} oglasa s verificiranom cijenom na lokaciji {region}.";

        if (count >= 3)
            return $"Pronađeno {count} oglasa — pretraga proširena izvan uže lokacije.";

        if (count >= 1)
            return $"Pronađen {(count == 1 ? "samo 1 oglas" : $"{count} oglasa")} " +
                   $"s cijenom. Dopunjeno benchmarkom od {bench:N0} €/m².";

        return $"Nijedan aktivan oglas s cijenom nije pronađen za {year} unatoč pretrazi " +
               $"Njuškalo, Index, Crozilla i Nekretnine.hr. " +
               $"Korišten regionalni benchmark za {region}: {bench:N0} €/m².";
    }

    // ══════════════════════════════════════════════════════════
    //  LOCATION HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Build a list of location levels from most specific to broadest.
    /// "Vir, Torovi, k.o. Vir" → ["Vir", "Torovi", "Zadar"]
    /// </summary>
    private List<string> ExtractLocationHierarchy(string location)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(location))
            return ["Hrvatska"];

        // Split by common delimiters
        var parts = location
            .Replace("k.o.", ",", StringComparison.OrdinalIgnoreCase)
            .Replace("K.O.", ",")
            .Replace("č.z.", ",", StringComparison.OrdinalIgnoreCase)
            .Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        result.AddRange(parts);

        // Add broader region if known
        foreach (var part in parts)
        {
            if (BroaderRegion.TryGetValue(part, out var broader) &&
                !result.Contains(broader, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(broader);
            }
        }

        if (result.Count == 0) result.Add("Hrvatska");
        return result;
    }

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

    /// <summary>Get the next broader region for an island/suburb.</summary>
    private static string GetBroaderRegion(string regionName)
    {
        if (BroaderRegion.TryGetValue(regionName, out var broader))
            return broader;

        // Try word-level match
        foreach (var (key, value) in BroaderRegion)
        {
            if (regionName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return regionName; // No broader known — use self
    }

    private static string[] GetTypeSynonyms(string detectedType)
    {
        if (TypeSynonyms.TryGetValue(detectedType, out var syns))
            return syns;
        return [detectedType];
    }

    // ══════════════════════════════════════════════════════════
    //  REGIONAL BENCHMARK LOOKUP
    // ══════════════════════════════════════════════════════════

    private static double FindRegionalBenchmark(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return DefaultBenchmark;

        foreach (var (region, benchmark) in RegionalBenchmarks)
        {
            if (location.Contains(region, StringComparison.OrdinalIgnoreCase))
                return benchmark;
        }

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

    private async Task<List<ComparableSearchResult>> ExecuteTavilySearch(
        string query, int maxResults = 5)
    {
        try
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
                _logger.LogWarning("Tavily 0 results for: {Query}", Truncate(query, 80));
                return [];
            }

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
        catch (Exception ex)
        {
            // Don't let one failed search kill the whole pipeline
            _logger.LogWarning(ex, "Tavily search failed for: {Query}", Truncate(query, 80));
            return [];
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PROPERTY TYPE DETECTION
    // ══════════════════════════════════════════════════════════

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

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}

// ══════════════════════════════════════════════════════════
//  ENUMS & MODELS
// ══════════════════════════════════════════════════════════

public enum SearchPhase
{
    Primary = 0,
    MultiPortal = 1,
    WiderRegion = 2,
    SynonymBlitz = 3,
    SubLocationDrill = 4,
    RegionalFallback = 5,
    BenchmarkOnly = 6,
}

public class ConfidenceScore
{
    public string Level { get; set; } = "";
    public int Score { get; set; }
    public string Explanation { get; set; } = "";
    public string Color { get; set; } = "";
}

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

public class ComparableSearchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

public class MarketResearchResult
{
    public List<ComparableSearchResult> ActiveListings { get; set; } = [];
    public List<ComparableSearchResult> RegionalData { get; set; } = [];
    public double RegionalBenchmarkPerM2 { get; set; }
    public string RegionName { get; set; } = "";
    public string DetectedPropertyType { get; set; } = "";
    public double TargetSizeM2 { get; set; }
    public string Note { get; set; } = "";
    public ConfidenceScore Confidence { get; set; } = new();
    public string SearchPhaseUsed { get; set; } = "";
    public int TotalApiCalls { get; set; }

    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];

    public bool IsBenchmarkFallback => ActiveListings.Count == 0;
}
