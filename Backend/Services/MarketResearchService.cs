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
    private readonly NjuskaloScraperService _scraper;

    private const string TavilySearchUrl = "https://api.tavily.com/search";
    private const string TavilyExtractUrl = "https://api.tavily.com/extract";

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
    //  NJUŠKALO URL SLUGS — for direct page extraction
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> NjuskaloTypeSlug = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stan"]                  = "prodaja-stanova",
        ["kuća"]                  = "prodaja-kuca",
        ["kuca"]                  = "prodaja-kuca",
        ["građevinsko zemljište"] = "prodaja-zemljista",
        ["gradjevinsko zemljiste"] = "prodaja-zemljista",
        ["poslovni prostor"]      = "prodaja-poslovnih-prostora",
    };

    private static readonly Regex NjuskaloListingPattern = new(
        @"###\s*\[(?<title>[^\]]+)\]\((?<url>https://www\.njuskalo\.hr/[^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex NjuskaloPricePattern = new(
        @"(?<price>\d{1,3}(?:\.\d{3})*)\s*€",
        RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════
    //  PRESTIGE PEERS — neighborhood-level fallback groups
    //  If <3 results in the exact neighborhood, search ONLY these
    //  peer neighborhoods. NEVER expand to the whole city.
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> PrestigePeers =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Zagreb — elite residential
        { "Srebrnjak",   ["Pantovčak", "Tuškanac", "Šalata", "Jurjevska"] },
        { "Pantovčak",   ["Srebrnjak", "Tuškanac", "Šalata", "Jurjevska"] },
        { "Tuškanac",    ["Srebrnjak", "Pantovčak", "Šalata", "Jurjevska"] },
        { "Šalata",      ["Srebrnjak", "Pantovčak", "Tuškanac", "Jurjevska"] },
        { "Jurjevska",   ["Srebrnjak", "Pantovčak", "Tuškanac", "Šalata"] },
        // Zagreb — upper middle
        { "Maksimir",    ["Ravnice", "Bukovac", "Šalata", "Srebrnjak"] },
        { "Ravnice",     ["Maksimir", "Bukovac", "Šalata"] },
        { "Bukovac",     ["Maksimir", "Ravnice", "Srebrnjak"] },
        // Zagreb — central
        { "Centar",      ["Donji grad", "Gornji grad", "Tuškanac", "Šalata"] },
        { "Donji grad",  ["Centar", "Gornji grad", "Vlaška", "Tuškanac"] },
        { "Gornji grad", ["Centar", "Donji grad", "Tuškanac", "Pantovčak"] },
        { "Vlaška",      ["Donji grad", "Centar", "Šalata", "Maksimir"] },
        // Zagreb — south/east residential
        { "Trnje",       ["Savica", "Martinovka", "Siget"] },
        { "Savica",      ["Trnje", "Martinovka", "Borovje"] },
        // Split — neighborhoods
        { "Spinut",      ["Firule", "Bačvice", "Meje", "Trstenik"] },
        { "Firule",      ["Spinut", "Bačvice", "Trstenik", "Meje"] },
        { "Bačvice",     ["Firule", "Spinut", "Trstenik"] },
        { "Meje",        ["Spinut", "Firule", "Bačvice", "Manuš"] },
        { "Manuš",       ["Meje", "Spinut", "Varoš"] },
        { "Varoš",       ["Manuš", "Meje", "Spinut"] },
        // Rijeka — neighborhoods
        { "Kantrida",    ["Kostabela", "Pećine", "Martinkovac"] },
        { "Kostabela",   ["Kantrida", "Pećine"] },
        { "Pećine",      ["Kantrida", "Kostabela"] },
    };

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
        // ── Osijek — city neighborhoods ──
        { "Retfala", "Osijek" }, { "Gornji grad", "Osijek" }, { "Donji grad", "Osijek" },
        { "Tvrđa", "Osijek" }, { "Tvrda", "Osijek" },
        { "Sjenjak", "Osijek" }, { "Jug II", "Osijek" }, { "Jug 2", "Osijek" },
        { "Vijenac", "Osijek" }, { "Višnjevac", "Osijek" }, { "Visnjevac", "Osijek" },
        { "Josipovac", "Osijek" }, { "Čepin", "Osijek" }, { "Cepin", "Osijek" },
        { "Tenja", "Osijek" }, { "Sarvaš", "Osijek" }, { "Sarvas", "Osijek" },
        { "Briješće", "Osijek" }, { "Brijesce", "Osijek" },
        { "Industrijska", "Osijek" }, { "Nemetin", "Osijek" },
        // ── Rijeka — city neighborhoods ──
        { "Trsat", "Rijeka" }, { "Sušak", "Rijeka" }, { "Susak", "Rijeka" },
        { "Martinkovac", "Rijeka" }, { "Kozala", "Rijeka" }, { "Turnić", "Rijeka" },
        { "Turnic", "Rijeka" }, { "Zamet", "Rijeka" },
        // ── Dubrovnik — neighborhoods ──
        { "Lapad", "Dubrovnik" }, { "Gruž", "Dubrovnik" }, { "Gruz", "Dubrovnik" },
        { "Mokošica", "Dubrovnik" }, { "Mokosica", "Dubrovnik" },
        // ── Zadar — neighborhoods ──
        { "Borik", "Zadar" }, { "Brodarica", "Zadar" }, { "Bili Brig", "Zadar" },
        { "Arbanasi", "Zadar" }, { "Diklo", "Zadar" }, { "Višnjik", "Zadar" },
        // ── Pula — neighborhoods ──
        { "Veruda", "Pula" }, { "Stoja", "Pula" }, { "Vidikovac", "Pula" },
        { "Šijana", "Pula" }, { "Sijana", "Pula" }, { "Kaštanjer", "Pula" },
        // ── Šibenik — neighborhoods ──
        { "Baldekin", "Šibenik" }, { "Meterize", "Šibenik" }, { "Vidici", "Šibenik" },
        // ── Split — neighborhoods (not already in PrestigePeers) ──
        { "Split 3", "Split" }, { "Sućidar", "Split" }, { "Sucidar", "Split" },
        { "Kman", "Split" }, { "Brda", "Split" }, { "Gripe", "Split" },
        { "Lovret", "Split" }, { "Lučac", "Split" }, { "Lucac", "Split" },
        { "Pujanke", "Split" }, { "Ravne njive", "Split" },
        { "Mejaši", "Split" }, { "Mejasi", "Split" },
    };

    // ══════════════════════════════════════════════════════════
    //  VILLAGE / SMALL MUNICIPALITY → NEAREST LARGER TOWN
    //  Unlike BroaderRegion (neighborhoods of a city), these are
    //  independent small places that Njuškalo does NOT have a
    //  dedicated sub-page for. We search the nearest town instead.
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string> VillageFallback =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Požeško-slavonska županija ──
        { "Kukunjevac", "Pakrac" }, { "Lipik", "Pakrac" },
        { "Badljevina", "Pakrac" }, { "Prekopakra", "Pakrac" },
        { "Pleternica", "Požega" }, { "Kutjevo", "Požega" },
        { "Brestovac", "Požega" }, { "Kaptol", "Požega" },
        { "Velika", "Požega" }, { "Jakšić", "Požega" }, { "Jaksic", "Požega" },
        // ── Brodsko-posavska županija ──
        { "Sibinj", "Slavonski Brod" }, { "Bebrina", "Slavonski Brod" },
        { "Oprisavci", "Slavonski Brod" }, { "Podcrkavlje", "Slavonski Brod" },
        { "Gornja Vrba", "Slavonski Brod" }, { "Bukovlje", "Slavonski Brod" },
        { "Cernik", "Nova Gradiška" }, { "Okučani", "Nova Gradiška" },
        { "Okucani", "Nova Gradiška" }, { "Staro Petrovo Selo", "Nova Gradiška" },
        { "Nova Gradiška", "Slavonski Brod" }, { "Nova Gradiska", "Slavonski Brod" },
        // ── Virovitičko-podravska županija ──
        { "Slatina", "Virovitica" }, { "Orahovica", "Virovitica" },
        { "Pitomača", "Virovitica" }, { "Pitomaca", "Virovitica" },
        { "Čačinci", "Virovitica" }, { "Cacinci", "Virovitica" },
        { "Suhopolje", "Virovitica" }, { "Špišić Bukovica", "Virovitica" },
        // ── Bjelovarsko-bilogorska županija ──
        { "Čazma", "Bjelovar" }, { "Cazma", "Bjelovar" },
        { "Garešnica", "Bjelovar" }, { "Garesnica", "Bjelovar" },
        { "Grubišno Polje", "Bjelovar" }, { "Grubisno Polje", "Bjelovar" },
        { "Daruvar", "Bjelovar" }, { "Hercegovac", "Bjelovar" },
        // ── Koprivničko-križevačka županija ──
        { "Đurđevac", "Koprivnica" }, { "Durdevac", "Koprivnica" },
        { "Križevci", "Koprivnica" }, { "Krizevci", "Koprivnica" },
        { "Virje", "Koprivnica" }, { "Novigrad Podravski", "Koprivnica" },
        // ── Sisačko-moslavačka županija ──
        { "Petrinja", "Sisak" }, { "Kutina", "Sisak" },
        { "Novska", "Sisak" }, { "Glina", "Sisak" },
        { "Popovača", "Sisak" }, { "Popovaca", "Sisak" },
        { "Sunja", "Sisak" }, { "Hrvatska Kostajnica", "Sisak" },
        { "Dvor", "Sisak" }, { "Topusko", "Sisak" },
        // ── Vukovarsko-srijemska županija ──
        { "Županja", "Vinkovci" }, { "Zupanja", "Vinkovci" },
        { "Ilok", "Vukovar" }, { "Tovarnik", "Vukovar" },
        { "Nuštar", "Vukovar" }, { "Nustar", "Vukovar" },
        { "Cerna", "Vinkovci" }, { "Stari Jankovci", "Vinkovci" },
        { "Drenovci", "Vinkovci" }, { "Gunja", "Vinkovci" },
        // ── Osječko-baranjska (smaller municipalities) ──
        { "Đakovo", "Osijek" }, { "Dakovo", "Osijek" },
        { "Belišće", "Osijek" }, { "Belisce", "Osijek" },
        { "Valpovo", "Osijek" }, { "Donji Miholjac", "Osijek" },
        { "Našice", "Osijek" }, { "Nasice", "Osijek" },
        { "Beli Manastir", "Osijek" },
        // ── Karlovačka županija ──
        { "Duga Resa", "Karlovac" }, { "Ozalj", "Karlovac" },
        { "Slunj", "Karlovac" }, { "Ogulin", "Karlovac" },
        { "Vojnić", "Karlovac" }, { "Vojnic", "Karlovac" },
        // ── Krapinsko-zagorska županija ──
        { "Krapina", "Zabok" }, { "Pregrada", "Zabok" },
        { "Klanjec", "Zabok" }, { "Zlatar", "Zabok" },
        { "Zabok", "Zagreb" }, { "Oroslavje", "Zabok" },
        // ── Varaždinska županija (smaller) ──
        { "Ivanec", "Varaždin" }, { "Ludbreg", "Varaždin" },
        { "Novi Marof", "Varaždin" }, { "Lepoglava", "Varaždin" },
        // ── Međimurska županija ──
        { "Mursko Središće", "Čakovec" }, { "Mursko Sredisce", "Čakovec" },
        { "Prelog", "Čakovec" },
        // ── Ličko-senjska županija ──
        { "Otočac", "Gospić" }, { "Otocac", "Gospić" },
        { "Senj", "Rijeka" }, { "Novalja", "Zadar" },
        { "Gospić", "Zadar" }, { "Gospic", "Zadar" },
        // ── Zadarska (inland/small) ──
        { "Benkovac", "Zadar" }, { "Obrovac", "Zadar" },
        { "Biograd na Moru", "Zadar" }, { "Gračac", "Zadar" }, { "Gracac", "Zadar" },
        // ── Šibensko-kninska (inland) ──
        { "Knin", "Šibenik" }, { "Drniš", "Šibenik" }, { "Drnis", "Šibenik" },
        // ── Splitsko-dalmatinska (inland/small) ──
        { "Sinj", "Split" }, { "Imotski", "Split" },
        { "Vrgorac", "Split" }, { "Trilj", "Split" },
        { "Bol", "Split" },
        // ── Dubrovačko-neretvanska (inland/small) ──
        { "Metković", "Dubrovnik" }, { "Metkovic", "Dubrovnik" },
        { "Ploče", "Dubrovnik" }, { "Ploce", "Dubrovnik" },
        { "Opuzen", "Dubrovnik" },
        // ── Istarska (inland/small) ──
        { "Pazin", "Pula" }, { "Buje", "Pula" },
        { "Vodnjan", "Pula" }, { "Višnjan", "Pula" }, { "Visnjan", "Pula" },
    };

    // ══════════════════════════════════════════════════════════
    //  NOTE: Regional benchmarks REMOVED.
    //  Engine works ONLY with real search data from Tavily.
    // ══════════════════════════════════════════════════════════

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

    public MarketResearchService(HttpClient httpClient, IConfiguration config, ILogger<MarketResearchService> logger, NjuskaloScraperService scraper)
    {
        _httpClient = httpClient;
        _scraper = scraper;
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
        _currentDetectedType = detectedType;   // Set for PriceFirstFilter gates
        var regionName = ExtractRegionName(location);
        var subLocations = ExtractLocationHierarchy(location);
        bool isLand = IsLandType(detectedType);
        var synonyms = GetTypeSynonyms(detectedType);
        var now = DateTime.Now;
        var searchPhase = SearchPhase.Primary;
        int totalApiCalls = 0;

        // Master list — all price-verified results across all phases
        var allVerified = new List<ComparableSearchResult>();

        _logger.LogInformation(
            "V4 Shotgun: Type={Type}, Synonyms=[{Syns}], Region={Region}, SubLocs=[{Subs}]",
            detectedType, string.Join(", ", synonyms), regionName,
            string.Join(" → ", subLocations));

        // Log village fallback and size cap decisions
        if (VillageFallback.TryGetValue(regionName, out var logFallback))
            _logger.LogInformation("Village detected: {Village} → fallback town: {Town}", regionName, logFallback);
        if (!isLand && sizeM2 > 300)
            _logger.LogWarning("Size {Size:0}m² too large for living area ({Type}) — skipping livingArea filter (likely plot area)",
                sizeM2, detectedType);

        // ─────────────────────────────────────────────────────
        // PHASE 0: Njuškalo DIRECT — extract the actual search page
        //   Constructs njuskalo.hr/prodaja-zemljista/vir URL
        //   Uses Tavily Extract API (their browser infra bypasses CAPTCHA)
        //   Gets 25+ listings per page vs. 3-5 from search
        // ─────────────────────────────────────────────────────
        {
            var njuskaloResults = await FetchNjuskaloDirectListings(detectedType, regionName, sizeM2);
            totalApiCalls += (njuskaloResults.Count > 0 ? 1 : 0);

            // Phase 0 location gate: allow both the neighborhood AND parent city.
            // When searching city-level URL (e.g. /prodaja-kuca/osijek for Retfala),
            // listings say "Osijek" not "Retfala" — must accept both.
            // For villages, also accept the fallback town (e.g. Kukunjevac → Pakrac).
            var phase0Locs = new List<string> { regionName };
            if (BroaderRegion.TryGetValue(regionName, out var parentCity))
                phase0Locs.Add(parentCity);
            else if (VillageFallback.TryGetValue(regionName, out var fallbackTown0))
                phase0Locs.Add(fallbackTown0);

            var filtered = PriceFirstFilter(njuskaloResults, "Phase0-NjuskaloDirect", phase0Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 0 (Njuškalo Direct): {New}/{Raw} passed → total={Total}",
                filtered.Count, njuskaloResults.Count, allVerified.Count);
        }

        // ─────────────────────────────────────────────────────
        // PHASE 1: Tavily search — exact neighborhood in quotes
        //   Fires 3 parallel queries using "exact quotes" for neighborhood
        //   For villages, also includes fallback town queries.
        //   Location gate: STRICT for cities, relaxed for villages.
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 5)
        {
            // Determine if we have a village fallback — add extra queries for the town
            string searchLoc = regionName;
            bool isVillage = VillageFallback.TryGetValue(regionName, out var villageFallbackTown1);

            var queries = new List<string>
            {
                // Q1: Exact neighborhood/village in quotes
                $"site:njuskalo.hr prodaja {detectedType} \"{searchLoc}\" cijena € {now.Year}",
                // Q2: Synonym variation with exact location
                $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} \"{searchLoc}\" € {now.Year}",
                // Q3: Include m² if known
                sizeM2 > 0
                    ? $"site:njuskalo.hr {detectedType} \"{searchLoc}\" {sizeM2:0}m2 €"
                    : $"site:njuskalo.hr nekretnina \"{searchLoc}\" prodaja € {now.Year}",
            };

            // For villages: also search the fallback town explicitly
            if (isVillage)
            {
                queries.Add($"site:njuskalo.hr prodaja {detectedType} \"{villageFallbackTown1}\" cijena € {now.Year}");
            }

            var phase1Results = await FireParallelSearches(queries, 7, "Phase1");
            totalApiCalls += queries.Count;
            // STRICT for city neighborhoods — but for villages, also accept
            // the fallback town (Tavily results rarely mention tiny villages by name).
            var phase1Locs = new List<string> { regionName };
            if (isVillage)
                phase1Locs.Add(villageFallbackTown1!);
            var filtered = PriceFirstFilter(phase1Results, "Phase1", phase1Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 1: {New}/{Raw} passed → total={Total}",
                filtered.Count, phase1Results.Count, allVerified.Count);
        } // end Phase 1 (if <5)

        // ─────────────────────────────────────────────────────
        // PHASE 2: Multi-portal spread — same location, different portals
        //   Fires 3 parallel queries (only if <5 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 5)
        {
            searchPhase = SearchPhase.MultiPortal;

            var queries = new List<string>
            {
                $"site:index.hr/oglasi prodaja {detectedType} \"{regionName}\" €",
                $"site:crozilla.com {detectedType} \"{regionName}\" prodaja €",
                $"site:nekretnine.hr {detectedType} \"{regionName}\" cijena €",
            };

            var phase2Results = await FireParallelSearches(queries, 5, "Phase2-Portals");
            totalApiCalls += queries.Count;
            // For villages, also accept the fallback town.
            var phase2Locs = new List<string> { regionName };
            if (VillageFallback.TryGetValue(regionName, out var fallbackTown2))
                phase2Locs.Add(fallbackTown2);
            var filtered = PriceFirstFilter(phase2Results, "Phase2", phase2Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 2 (multi-portal): +{New} → total={Total}",
                filtered.Count, allVerified.Count);
        }

        // ─────────────────────────────────────────────────────
        // PHASE 3: PRESTIGE PEERS — search only peer neighborhoods
        //   e.g. Srebrnjak → [Pantovčak, Tuškanac, Šalata, Jurjevska]
        //   If no peers defined, falls back to BroaderRegion as before.
        //   Fires 1 query per peer (max 4) — only if <3 results
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3)
        {
            searchPhase = SearchPhase.WiderRegion;

            var hasPeers = PrestigePeers.TryGetValue(regionName, out var peers);

            if (hasPeers && peers is { Length: > 0 })
            {
                // Search each peer neighborhood individually (exact quotes)
                var queries = peers.Take(4)
                    .Select(peer => $"site:njuskalo.hr prodaja {detectedType} \"{peer}\" cijena € {now.Year}")
                    .ToList();

                var peerResults = await FireParallelSearches(queries, 5, "Phase3-Peers");
                totalApiCalls += queries.Count;

                // Only allow listings from the peer neighborhoods (or regionName itself)
                var allowedLocs = new List<string> { regionName };
                allowedLocs.AddRange(peers);
                var filtered = PriceFirstFilter(peerResults, "Phase3-Peers", allowedLocs);
                MergeInto(allVerified, filtered);

                _logger.LogInformation(
                    "Phase 3 (peers of {Region}: [{Peers}]): +{New} → total={Total}",
                    regionName, string.Join(", ", peers), filtered.Count, allVerified.Count);
            }
            else
            {
                // No peer list → fall back to broader region (islands/suburbs)
                var widerRegion = GetBroaderRegion(regionName);

                var queries = new List<string>
                {
                    $"site:njuskalo.hr prodaja {detectedType} \"{widerRegion}\" cijena € {now.Year}",
                    $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} \"{widerRegion}\" €",
                    $"prodaja {detectedType} \"{widerRegion}\" cijena € site:njuskalo.hr OR site:index.hr/oglasi",
                    $"site:njuskalo.hr {detectedType} \"{widerRegion}\" {now.Year}",
                };

                var phase3Results = await FireParallelSearches(queries, 6, "Phase3-Wider");
                totalApiCalls += queries.Count;
                var filtered = PriceFirstFilter(phase3Results, "Phase3", [regionName, widerRegion]);
                MergeInto(allVerified, filtered);

                _logger.LogInformation("Phase 3 (wider: {Region}->{Wider}): +{New} → total={Total}",
                    regionName, widerRegion, filtered.Count, allVerified.Count);
            }
        }

        // ─────────────────────────────────────────────────────
        // KILL SWITCH — if neighborhood has prestige peers defined
        //   and we STILL have <3 listings after searching exact +
        //   peers, do NOT expand further. Return Insufficient_Data.
        // ─────────────────────────────────────────────────────
        bool hasPrestigePeers = PrestigePeers.ContainsKey(regionName);
        if (hasPrestigePeers && allVerified.Count < 3)
        {
            _logger.LogWarning(
                "KILL SWITCH: {Region} has prestige peers but only {Count} listings found. " +
                "Refusing to expand to generic city areas.",
                regionName, allVerified.Count);

            var killNote = "Nema dovoljno slicnih elitnih lokacija za preciznu usporedbu.";
            var killConfidence = new ConfidenceScore
            {
                Level = "Insufficient_Data",
                Score = Math.Max(5, allVerified.Count * 10),
                Explanation = $"Pretrazene su lokacije {regionName} i peer kvartovi " +
                              $"({string.Join(", ", PrestigePeers[regionName])}), " +
                              $"ali pronadjeno je samo {allVerified.Count} oglas(a). " +
                              "Pretraga NIJE prosirena na sire gradsko podrucje radi ocuvanja preciznosti.",
                Color = "red"
            };

            return new MarketResearchResult
            {
                ActiveListings = allVerified.DistinctBy(x => x.Url).Take(20).ToList(),
                RegionalData = [],
                RegionName = regionName,
                DetectedPropertyType = detectedType,
                TargetSizeM2 = sizeM2,
                Note = killNote,
                Confidence = killConfidence,
                SearchPhaseUsed = "Insufficient_Data",
                TotalApiCalls = totalApiCalls,
            };
        }

        // ─────────────────────────────────────────────────────
        // PHASE 4: Synonym blitz — try ALL synonyms across portals
        //   Fires up to 4 parallel queries (only if <3 results)
        //   Location gate: regionName + peers (if any) — NEVER generic city
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3)
        {
            searchPhase = SearchPhase.SynonymBlitz;

            var queries = new List<string>();

            // Build queries from remaining synonyms — exact location
            foreach (var syn in synonyms.Skip(1).Take(3))
            {
                queries.Add($"site:njuskalo.hr prodaja {syn} \"{regionName}\" € {now.Year}");
            }
            // Also try with "nekretnina" generic + all portals
            queries.Add($"prodaja nekretnina \"{regionName}\" cijena € {now.Year} site:njuskalo.hr OR site:crozilla.com");

            if (queries.Count > 0)
            {
                var phase4Results = await FireParallelSearches(queries, 5, "Phase4-Synonyms");
                totalApiCalls += queries.Count;

                // Location gate: regionName + peers if defined, else regionName + broader
                var allowedLocs = new List<string> { regionName };
                if (PrestigePeers.TryGetValue(regionName, out var p4peers))
                    allowedLocs.AddRange(p4peers);
                else
                    allowedLocs.Add(GetBroaderRegion(regionName));
                var filtered = PriceFirstFilter(phase4Results, "Phase4", allowedLocs);
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
                var filtered = PriceFirstFilter(phase5Results, "Phase5", subLocations);
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
                searchPhase = SearchPhase.NoComparables;
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
            .Take(20)
            .ToList();

        // ─────────────────────────────────────────────────────
        // DEEP DIVE — visit top 12 listing pages for rich data
        //   Only for Njuškalo URLs (we have the DOM selectors).
        //   Enriches Content with full description, attributes,
        //   equipment list, renovation year, yard size, etc.
        //   Discards vanknjižno, reclassifies partial-floor sales.
        //   ~60s total (12 pages × ~5s each, sequential to avoid bans).
        // ─────────────────────────────────────────────────────
        if (distinctListings.Count > 0)
        {
            try
            {
                await DeepDiveTopListingsAsync(distinctListings, regionName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deep Dive failed (non-fatal) — proceeding with search-page data only");
            }
        }

        var confidence = CalculateConfidence(distinctListings.Count, searchPhase);
        var note = BuildNote(distinctListings.Count, searchPhase, regionName, now.Year);

        var result = new MarketResearchResult
        {
            ActiveListings = distinctListings,
            RegionalData = regionalData,
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
    //  PHASE 0: NJUŠKALO DIRECT — Tavily Extract on search page
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Constructs the Njuškalo search URL, tries Playwright scraper first
    /// (full scroll + DOM capture), falls back to Tavily Extract on failure.
    /// </summary>
    private async Task<List<ComparableSearchResult>> FetchNjuskaloDirectListings(
        string detectedType, string regionName, double sizeM2 = 0)
    {
        try
        {
            var njuskaloUrl = BuildNjuskaloSearchUrl(detectedType, regionName, sizeM2);
            if (njuskaloUrl is null)
            {
                _logger.LogWarning("Phase0: Could not build Njuškalo URL for type={Type}, region={Region}",
                    detectedType, regionName);
                return [];
            }

            var results = await TryScrapeSingleUrl(njuskaloUrl);

            // ── CITY-LEVEL FALLBACK: if neighborhood/village URL returned empty,
            //    try the parent city URL directly (e.g. /prodaja-kuca/pakrac). ──
            if (results.Count == 0)
            {
                string? parentCityName = null;
                if (BroaderRegion.TryGetValue(regionName, out var pc))
                    parentCityName = pc;
                else if (VillageFallback.TryGetValue(regionName, out var ft))
                    parentCityName = ft;

                if (parentCityName != null)
                {
                    var cityUrl = BuildNjuskaloSearchUrl(detectedType, parentCityName, sizeM2);
                    if (cityUrl != null && cityUrl != njuskaloUrl)
                    {
                        _logger.LogInformation(
                            "Phase0: Primary URL empty → trying city-level: {Url}", cityUrl);
                        results = await TryScrapeSingleUrl(cityUrl);
                    }
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phase0: Njuškalo direct extraction failed");
            return [];
        }
    }

    /// <summary>
    /// Attempt to scrape a single Njuškalo URL via Playwright, falling back to Tavily Extract.
    /// </summary>
    private async Task<List<ComparableSearchResult>> TryScrapeSingleUrl(string njuskaloUrl)
    {
        // ── PRIMARY: Playwright scraper (scrolls page, captures full DOM) ──
        _logger.LogInformation("Phase0: Playwright scraper → {Url}", njuskaloUrl);
        var scraped = await _scraper.ScrapeListingsAsync(njuskaloUrl);

        if (scraped.Count > 0)
        {
            var converted = scraped.Select(s => new ComparableSearchResult
            {
                Url = s.Url,
                Title = s.Title,
                Content = BuildScrapedContent(s)
            }).ToList();

            _logger.LogInformation(
                "Phase0: Playwright captured {Count} organic listings", converted.Count);
            return converted;
        }

        _logger.LogWarning("Phase0: Playwright returned 0 listings — falling back to Tavily Extract");

        // ── FALLBACK: Tavily Extract API ──
        var extractedText = await ExecuteTavilyExtract(njuskaloUrl);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            _logger.LogWarning("Phase0: Tavily Extract also returned empty for {Url}", njuskaloUrl);
            return [];
        }

        var listings = ParseNjuskaloExtractedText(extractedText);
        _logger.LogInformation("Phase0: Tavily fallback parsed {Count} listings", listings.Count);
        return listings;
    }

    /// <summary>
    /// Build the Content string from a ScrapedListing for the downstream pipeline.
    /// </summary>
    private static string BuildScrapedContent(ScrapedListing s)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Title))
            parts.Add(s.Title);
        if (!string.IsNullOrWhiteSpace(s.Location))
            parts.Add($"Lokacija: {s.Location}");
        if (!string.IsNullOrWhiteSpace(s.PriceText))
            parts.Add($"Cijena: {s.PriceText}");
        if (!string.IsNullOrWhiteSpace(s.Description))
            parts.Add(s.Description);

        // If structured parts are sparse, include the raw fullText
        if (parts.Count < 2 && !string.IsNullOrWhiteSpace(s.FullText))
            parts.Add(s.FullText);

        return string.Join(". ", parts);
    }

    // ══════════════════════════════════════════════════════════
    //  DEEP DIVE — Visit individual listing pages for rich data
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Price regex for rough-filter scoring (Croatian format: 150.000 €).
    /// </summary>
    private static readonly Regex _deepDivePriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Select the top 12 most promising Njuškalo listings, visit each detail page,
    /// and enrich the ComparableSearchResult with the extracted data.
    /// Discards listings with "vanknjižno vlasništvo" and reclassifies
    /// "prodaje se samo gornji kat" as apartment.
    /// </summary>
    private async Task DeepDiveTopListingsAsync(List<ComparableSearchResult> listings, string regionName = "")
    {
        // ── Step 1: Only Njuškalo URLs (we have selectors for those) ──
        var njuskaloListings = listings
            .Where(l => l.Url.Contains("njuskalo.hr", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (njuskaloListings.Count == 0)
        {
            _logger.LogInformation("DeepDive: no Njuškalo URLs to visit");
            return;
        }

        // ── Step 2: Neighborhood filter — prioritize listings matching target region ──
        //    Score: neighborhood match in URL/title/content → high priority
        //    This prevents wasting deep-dive time on Gornji Grad when searching Retfala.
        var regionLower = regionName.ToLowerInvariant();
        var regionNormalized = NormalizeLocationSlug(regionName);

        var scored = njuskaloListings
            .Select(l => new
            {
                Listing = l,
                HasPrice = _deepDivePriceRegex.IsMatch(l.Content),
                ContentLength = l.Content.Length,
                MatchesNeighborhood = !string.IsNullOrEmpty(regionLower) && (
                    l.Url.Contains(regionNormalized, StringComparison.OrdinalIgnoreCase) ||
                    l.Title.Contains(regionLower, StringComparison.OrdinalIgnoreCase) ||
                    l.Content.Contains(regionLower, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.MatchesNeighborhood)  // Neighborhood matches first!
            .ThenByDescending(x => x.HasPrice)
            .ThenBy(x => x.ContentLength)
            .Take(12)
            .Select(x => x.Listing)
            .ToList();

        _logger.LogInformation("DeepDive: selected {Count}/{Total} Njuškalo listings for detail scrape",
            scored.Count, njuskaloListings.Count);

        // ── Step 3: Visit each listing sequentially (avoid rate-limiting) ──
        int enriched = 0;
        var toDiscard = new List<ComparableSearchResult>();

        foreach (var listing in scored)
        {
            try
            {
                var details = await _scraper.ScrapeListingDetailsAsync(listing.Url);
                if (details is null) continue;

                var descLower = (details.Description ?? "").ToLowerInvariant();

                // ── DISCARD: "vanknjižno vlasništvo" — unreliable property rights ──
                if (descLower.Contains("vanknjižno vlasništvo") || descLower.Contains("vanknjižno"))
                {
                    _logger.LogInformation("DeepDive: DISCARDING {Url} — vanknjižno vlasništvo", listing.Url);
                    toDiscard.Add(listing);
                    continue;
                }

                // ── RECLASSIFY: "prodaje se samo gornji kat" → Apartment ──
                if (descLower.Contains("prodaje se samo gornji kat") ||
                    descLower.Contains("prodaje se gornji kat") ||
                    descLower.Contains("samo kat") ||
                    descLower.Contains("prodaje se jedan kat"))
                {
                    listing.AiPropertyType = "stan";
                    _logger.LogInformation("DeepDive: reclassified {Url} as APARTMENT (gornji kat)", listing.Url);
                }

                // ── Enrich the listing's Content with detail page data ──
                var enrichedText = details.ToEnrichedText();
                if (!string.IsNullOrWhiteSpace(enrichedText))
                {
                    listing.Content = $"{listing.Content} [DEEP DIVE] {enrichedText}";
                }

                // ── Intelligent condition detection ──
                if (descLower.Contains("potrebna adaptacija") || descLower.Contains("za rušenje") ||
                    descLower.Contains("zapušteno") || descLower.Contains("dotrajalo") ||
                    descLower.Contains("vlaga") || descLower.Contains("za adaptaciju") ||
                    descLower.Contains("izvorno stanje") || descLower.Contains("roh-bau") ||
                    descLower.Contains("roh bau"))
                {
                    listing.AiCondition ??= "needs_work";
                    listing.NeedsRenovation = true;
                    listing.IsRenovated = false;
                }
                else if (details.YearLastRenovation.HasValue && details.YearLastRenovation >= 2020)
                {
                    listing.AiCondition ??= "renovated";
                    listing.IsRenovated = true;
                    listing.NeedsRenovation = false;
                }
                else if (descLower.Contains("novogradnja") || descLower.Contains("novo izgrađen"))
                {
                    listing.AiCondition ??= "new";
                    listing.IsRenovated = true;
                    listing.NeedsRenovation = false;
                }
                else if (descLower.Contains("renoviran") || descLower.Contains("adaptiran") ||
                         descLower.Contains("obnovljen") || descLower.Contains("kompletno uređen") ||
                         descLower.Contains("novi krov"))
                {
                    listing.AiCondition ??= "renovated";
                    listing.IsRenovated = true;
                    listing.NeedsRenovation = false;
                }
                else if (details.YearLastRenovation.HasValue && details.YearLastRenovation >= 2015)
                {
                    listing.IsRenovated = true;
                    listing.NeedsRenovation = false;
                    listing.AiCondition ??= "renovated";
                }
                else if (descLower.Contains("održavan") || descLower.Contains("useljiv") ||
                         descLower.Contains("dobro stanje"))
                {
                    listing.AiCondition ??= "good";
                    listing.IsRenovated = false;
                    listing.NeedsRenovation = false;
                }
                else
                {
                    // Default: can't determine from description
                    listing.IsRenovated ??= false;
                    listing.NeedsRenovation ??= false;
                }

                // ── Store renovation year ──
                if (details.YearLastRenovation.HasValue)
                    listing.YearLastRenovation = details.YearLastRenovation;

                // ── Yard area from detail page ──
                if (details.YardAreaM2.HasValue && details.YardAreaM2 > 0)
                    listing.AiYardAreaM2 ??= details.YardAreaM2;

                // ── Size from detail page ──
                if (details.SizeM2.HasValue && details.SizeM2 > 0)
                    listing.AiSizeM2 ??= details.SizeM2;

                // ── Equipment-based feature detection ──
                var equipLower = details.Equipment
                    .Select(e => e.ToLowerInvariant()).ToList();

                if (equipLower.Any(e => e.Contains("garaž") || e.Contains("garaz") ||
                                        e.Contains("parking")))
                    listing.HasGarage ??= true;

                if (equipLower.Any(e => e.Contains("bazen") || e.Contains("pool")))
                    listing.Content += " [Oprema: Bazen]";

                if (equipLower.Any(e => e.Contains("plinsko") || e.Contains("centralno grijanje")))
                    listing.Content += $" [Grijanje: {details.HeatingType ?? "plinsko"}]";

                enriched++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeepDive: failed for individual listing {Url}", listing.Url);
                // Continue with next listing — non-fatal
            }
        }

        // ── Remove discarded listings from the main list ──
        if (toDiscard.Count > 0)
        {
            foreach (var bad in toDiscard)
                listings.Remove(bad);
            _logger.LogInformation("DeepDive: discarded {Count} listings (vanknjižno/invalid)", toDiscard.Count);
        }

        _logger.LogInformation("DeepDive: enriched {Enriched}/{Selected} listings with detail page data",
            enriched, scored.Count);
    }

    /// <summary>
    /// Build the Njuškalo search URL from property type + location.
    /// e.g. "građevinsko zemljište" + "Vir" → "https://www.njuskalo.hr/prodaja-zemljista/vir"
    /// </summary>
    private static string? BuildNjuskaloSearchUrl(string detectedType, string regionName, double sizeM2 = 0)
    {
        // Find the correct URL slug for this property type
        string? slug = null;
        foreach (var (key, value) in NjuskaloTypeSlug)
        {
            if (detectedType.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                slug = value;
                break;
            }
        }

        // Default fallback for unknown types
        slug ??= detectedType.Contains("zemlj", StringComparison.OrdinalIgnoreCase)
            ? "prodaja-zemljista"
            : detectedType.Contains("kuć", StringComparison.OrdinalIgnoreCase)
                ? "prodaja-kuca"
                : "prodaja-stanova";

        // NEIGHBORHOOD URL: Njuškalo supports neighborhood-level slugs!
        // Format: "{city-slug}-{neighborhood-slug}" e.g. "osijek-retfala"
        // Try neighborhood-specific URL first for much better results.
        string urlRegion = regionName;
        string? neighborhoodUrl = null;

        if (BroaderRegion.TryGetValue(regionName, out var parentCity))
        {
            // Build neighborhood-specific URL: osijek-retfala, split-spinut, etc.
            var citySlug = NormalizeLocationSlug(parentCity);
            var neighborhoodSlug = NormalizeLocationSlug(regionName);
            neighborhoodUrl = $"https://www.njuskalo.hr/{slug}/{citySlug}-{neighborhoodSlug}";
            urlRegion = parentCity; // fallback to city-level
        }
        else if (VillageFallback.TryGetValue(regionName, out var fallbackTown))
        {
            // Village/small municipality → search the nearest larger town directly.
            // Njuškalo won't have a sub-page for small villages.
            urlRegion = fallbackTown;
        }

        var locSlug = NormalizeLocationSlug(urlRegion);
        if (string.IsNullOrWhiteSpace(locSlug))
            return null;

        var baseUrl = neighborhoodUrl ?? $"https://www.njuskalo.hr/{slug}/{locSlug}";

        // ── QUERY PARAMETERS: Size filter (±30%) ──
        // Njuškalo supports livingArea[min]/[max] for houses & apartments,
        // plotArea[min]/[max] for land. This dramatically improves result quality.
        var queryParams = new List<string>();

        if (sizeM2 > 10)
        {
            bool isLand = slug.Contains("zemljist");

            if (!isLand && sizeM2 > 300)
            {
                // Houses/apartments NEVER have 300+ m² living area.
                // If sizeM2 > 300, the AI likely confused plot area with living area.
                // Skip size filter entirely — downstream similarity scoring will handle it.
            }
            else
            {
                string sizeParam = isLand ? "plotArea" : "livingArea";

                int minSize = (int)(sizeM2 * 0.70);  // -30%
                int maxSize = (int)(sizeM2 * 1.30);  // +30%

                queryParams.Add($"{sizeParam}%5Bmin%5D={minSize}");
                queryParams.Add($"{sizeParam}%5Bmax%5D={maxSize}");
            }
        }

        if (queryParams.Count > 0)
            baseUrl += "?" + string.Join("&", queryParams);

        return baseUrl;
    }

    /// <summary>
    /// Normalize Croatian location name to Njuškalo URL slug.
    /// "Šibenik" → "sibenik", "Velika Gorica" → "velika-gorica"
    /// </summary>
    private static string NormalizeLocationSlug(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "";

        var slug = location.ToLowerInvariant().Trim();

        // Strip Croatian diacritics
        slug = slug
            .Replace("č", "c").Replace("ć", "c")
            .Replace("đ", "d").Replace("ð", "d")
            .Replace("š", "s").Replace("ž", "z");

        // Replace spaces with hyphens, remove non-alphanumeric
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');

        return slug;
    }

    /// <summary>
    /// Call Tavily Extract API to fetch content from a specific URL.
    /// Returns the raw text content of the page.
    /// </summary>
    private async Task<string> ExecuteTavilyExtract(string url)
    {
        var requestBody = new
        {
            urls = new[] { url },
            extract_depth = "advanced",
            format = "markdown",
            timeout = 30
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, TavilyExtractUrl)
        {
            Content = jsonContent
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
        {
            var first = results[0];
            if (first.TryGetProperty("raw_content", out var rawContent))
                return rawContent.GetString() ?? "";
        }

        return "";
    }

    /// <summary>
    /// Parse Njuškalo listings from text extracted by Tavily.
    /// The text format has: ### [Title](URL) ... Price €
    /// </summary>
    private List<ComparableSearchResult> ParseNjuskaloExtractedText(string text)
    {
        var listings = new List<ComparableSearchResult>();

        // Split text into listing blocks: each starts with "### ["
        var blocks = Regex.Split(text, @"(?=###\s*\[)");

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block))
                continue;

            // Extract title + URL: ### [Title](URL)
            var listingMatch = NjuskaloListingPattern.Match(block);
            if (!listingMatch.Success)
                continue;

            var title = listingMatch.Groups["title"].Value.Trim();
            var url = listingMatch.Groups["url"].Value.Trim();

            // Skip non-Njuškalo links that might appear in navigation
            if (!url.Contains("njuskalo.hr/"))
                continue;

            // Extract price: number + €
            var priceMatch = NjuskaloPricePattern.Match(block);
            string priceStr = priceMatch.Success ? priceMatch.Groups["price"].Value : "";

            // Build content string with all available data
            var contentParts = new List<string> { title };

            // Extract location if present: "Lokacija: City, Area"
            var locMatch = Regex.Match(block, @"Lokacija:\s*([^\n]+)");
            if (locMatch.Success)
                contentParts.Add($"Lokacija: {locMatch.Groups[1].Value.Trim()}");

            if (!string.IsNullOrEmpty(priceStr))
                contentParts.Add($"Cijena: {priceStr} €");

            listings.Add(new ComparableSearchResult
            {
                Url = url,
                Title = title,
                Content = string.Join(". ", contentParts)
            });
        }

        _logger.LogInformation("Phase0 parser: {Total} listings extracted from text ({Chars} chars)",
            listings.Count, text.Length);

        return listings;
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

    // ── Sales-only gatekeeper constants ──
    // Dynamic: land has much lower floors (30-150 €/m² is normal)
    private const double MinSalePriceEur_Building = 35_000;
    private const double MinSalePriceEur_Land     =  5_000;
    private const double MinPricePerM2_Building    =    800;
    private const double MinPricePerM2_Land        =     15;  // Land can be 20-150 €/m²

    // ── Article / junk keywords — not actual listings ──
    private static readonly string[] ArticleKeywords =
    [
        "tržište nekretnina", "trziste nekretnina",
        "prosječna cijena", "prosjecna cijena",
        "stručnjaci", "strucnjaci",
        "analiza tržišta", "analitičar", "analiticar",
        "porezi i lokacija diktiraju", "trendovi",
        "prognoza", "izvještaj", "izvjestaj",
        "rast cijena", "pad cijena",
        "hina.hr", "jutarnji.hr", "vecernji.hr", "index.hr/vijesti",
        "poslovni.hr", "forbes.hr", "lider.media",
        "telegram.hr", "dnevnik.hr", "n1info.hr"
    ];

    // ── Wrong-type keywords — used by Type Gate ──
    private static readonly Dictionary<string, string[]> WrongTypeKeywords = new()
    {
        ["građevinsko zemljište"] = ["stan", "apartman", "garsonijera", "soba",
            "kuća", "kuca", "vila", "vikendica", "etaža", "etaza",
            "poslovni prostor", "ured", "lokal"],
        ["stan"]                  = ["građevinsko zemljište", "parcela", "oranica",
            "pašnjak", "livada", "šuma"],
        ["kuća"]                  = ["građevinsko zemljište", "parcela", "oranica"],
        ["poslovni prostor"]      = ["parcela", "oranica", "livada"],
    };

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

    /// <summary>The currently detected property type — set per search call.</summary>
    private string _currentDetectedType = "stan";

    private List<ComparableSearchResult> PriceFirstFilter(
        List<ComparableSearchResult> results, string phaseName,
        IReadOnlyList<string>? allowedLocations = null)
    {
        var passed = new List<ComparableSearchResult>();
        var locationsLower = allowedLocations?
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.ToLowerInvariant())
            .ToList();

        foreach (var r in results)
        {
            var combined = r.Title + " " + r.Content;
            var combinedLower = combined.ToLowerInvariant();

            // Gate 0: LOCATION — must mention at least one allowed location
            if (locationsLower is { Count: > 0 })
            {
                bool locationMatch = locationsLower.Any(loc =>
                    combinedLower.Contains(loc) ||
                    r.Url.Contains(loc, StringComparison.OrdinalIgnoreCase));

                if (!locationMatch)
                {
                    _logger.LogDebug("[{Phase}] REJECTED (wrong location, need [{Locs}]): {Title}",
                        phaseName, string.Join("|", allowedLocations!), Truncate(r.Title, 60));
                    continue;
                }
            }

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

            // ── GATE 4: RENT-TRAP — Rental keywords ──
            if (RentalKeywords.Any(kw => combinedLower.Contains(kw)))
            {
                _logger.LogDebug("[{Phase}] REJECTED (rental keyword): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── GATE 5: ARTICLE FILTER — news/analysis, not a listing ──
            if (ArticleKeywords.Any(kw => combinedLower.Contains(kw)))
            {
                _logger.LogDebug("[{Phase}] REJECTED (article/news): {Title}",
                    phaseName, Truncate(r.Title, 60));
                continue;
            }

            // ── GATE 6: TYPE GATE — reject wrong property type ──
            // If we search for "zemljište", reject results about "stan" or "kuća"
            if (WrongTypeKeywords.TryGetValue(_currentDetectedType, out var wrongKws))
            {
                // Check title (strongest signal) — URL is OK too
                string titleLower = r.Title.ToLowerInvariant();
                if (wrongKws.Any(wk => titleLower.Contains(wk)))
                {
                    _logger.LogDebug("[{Phase}] REJECTED (wrong type '{WrongType}' for '{Wanted}'): {Title}",
                        phaseName, wrongKws.First(wk => titleLower.Contains(wk)),
                        _currentDetectedType, Truncate(r.Title, 60));
                    continue;
                }
            }

            // ── GATE 7: PRICE FLOOR (dynamic by type) ──
            bool isLand = IsLandType(_currentDetectedType);
            double minSale = isLand ? MinSalePriceEur_Land : MinSalePriceEur_Building;

            var priceMatch = PriceAmountRegex.Match(combined);
            if (priceMatch.Success)
            {
                string priceStr = priceMatch.Groups[1].Value
                    .Replace(".", "").Replace(",", ".");
                if (double.TryParse(priceStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double extractedPrice))
                {
                    if (extractedPrice < minSale)
                    {
                        _logger.LogDebug("[{Phase}] REJECTED (price €{Price:N0} < €{Floor:N0} floor): {Title}",
                            phaseName, extractedPrice, minSale, Truncate(r.Title, 60));
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
        int priceVerifiedCount, SearchPhase phase)
    {
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
                Score = 40,
                Explanation = priceVerifiedCount == 1
                    ? "Pronađen samo 1 oglas s cijenom. Procjena ograničene pouzdanosti."
                    : $"Pronađena {priceVerifiedCount} oglasa s cijenom.",
                Color = "yellow"
            };
        }

        // LOW — no listings at all
        return new ConfidenceScore
        {
            Level = "Vrlo niska",
            Score = 10,
            Explanation = "Nema aktivnih oglasa s cijenom. Nedovoljno podataka za pouzdanu procjenu.",
            Color = "red"
        };
    }

    // ══════════════════════════════════════════════════════════
    //  NOTE BUILDER
    // ══════════════════════════════════════════════════════════

    private static string BuildNote(
        int count, SearchPhase phase, string region, int year)
    {
        if (count >= 5 && phase <= SearchPhase.MultiPortal)
            return "";

        if (count >= 3 && phase <= SearchPhase.MultiPortal)
            return $"Pronađeno {count} oglasa s verificiranom cijenom na lokaciji {region}.";

        if (count >= 3)
            return $"Pronađeno {count} oglasa — pretraga proširena izvan uže lokacije.";

        if (count >= 1)
            return $"Pronađen {(count == 1 ? "samo 1 oglas" : $"{count} oglasa")} " +
                   $"s cijenom. Ograničeni podaci za procjenu.";

        return $"Nijedan aktivan oglas s cijenom nije pronađen za {year} unatoč pretrazi " +
               $"Njuškalo, Index, Crozilla i Nekretnine.hr za {region}.";
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

        // Add broader region if known (neighborhoods + villages)
        foreach (var part in parts)
        {
            if (BroaderRegion.TryGetValue(part, out var broader) &&
                !result.Contains(broader, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(broader);
            }
            else if (VillageFallback.TryGetValue(part, out var fallback) &&
                     !result.Contains(fallback, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(fallback);
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

    /// <summary>Get the next broader region for an island/suburb/village.</summary>
    private static string GetBroaderRegion(string regionName)
    {
        if (BroaderRegion.TryGetValue(regionName, out var broader))
            return broader;

        // Village / small municipality fallback
        if (VillageFallback.TryGetValue(regionName, out var fallback))
            return fallback;

        // Try word-level match in BroaderRegion
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

            var response = await _httpClient.PostAsync(TavilySearchUrl, jsonContent);
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
    //  Order matters! Land MUST be checked BEFORE house/flat
    //  because ZK extracts for parcels often contain words like
    //  "stambena zgrada" or "kuća" in their legal descriptions.
    // ══════════════════════════════════════════════════════════

    // ── Land-specific keywords found in Croatian ZK / cadastral documents ──
    private static readonly string[] LandKeywords =
    [
        // Cadastral terms
        "čest.zem.", "čest. zem.", "č.zem.", "čestica zemlje",
        "k.č.", "k.č.br.", "kat.čestica", "katastarska čestica",
        "katastar", "čest.zgr",  // čest.zgr can still be land parcel
        // Land types
        "građevinsko zemljište", "građevinsko", "gradjevinsko",
        "oranica", "pašnjak", "pasnjak", "livada",
        "šuma", "suma", "vinograd", "voćnjak", "vocnjak",
        "neizgrađeno", "neizgradjeno", "neplodno",
        "parcela", "zemljište", "zemljiste",
        // Legal land terms
        "nekretnina upisana kao", "etažno vlasništvo" // negative — skip below
    ];

    // If these appear, it's NOT land even if land keywords match
    private static readonly string[] NotLandOverrides =
    [
        "etažno vlasništvo", "etazno vlasnistvo",
        "stan na adresi", "stan u ",
        "posebni dio zgrade", "suvlasnički dio",
        "suvlasnicki dio"
    ];

    private static string DetectPropertyType(string pdfText)
    {
        if (string.IsNullOrWhiteSpace(pdfText))
            return "stan";

        var text = pdfText.ToLowerInvariant();

        // ── 1. Commercial — check first (rare, unambiguous) ──
        if (text.Contains("poslovni prostor") || text.Contains("lokal"))
            return "poslovni prostor";

        // ── 2. LAND — check BEFORE house! ──
        // ZK extracts for pure land parcels often mention "stambena zgrada"
        // in the broader context, so we must check land indicators first.
        bool hasLandKeyword = LandKeywords.Any(kw => text.Contains(kw));
        bool hasNotLandOverride = NotLandOverrides.Any(kw => text.Contains(kw));

        if (hasLandKeyword && !hasNotLandOverride)
        {
            // Extra confidence: if it says "oranica", "pašnjak" etc. → definitely land
            // If it says "k.č." but also says "stan" prominently → might be flat
            bool hasStrongLand = text.Contains("oranica") || text.Contains("pašnjak")
                || text.Contains("pasnjak") || text.Contains("livada")
                || text.Contains("šuma") || text.Contains("suma")
                || text.Contains("vinograd") || text.Contains("voćnjak")
                || text.Contains("neplodno") || text.Contains("neizgrađeno")
                || text.Contains("neizgradjeno") || text.Contains("građevinsko zemljište")
                || text.Contains("gradjevinsko") || text.Contains("parcela")
                || text.Contains("čest.zem.") || text.Contains("čest. zem.")
                || text.Contains("čestica zemlje");

            if (hasStrongLand)
                return "građevinsko zemljište";

            // Weak land signal (only k.č. etc.) — check if house/flat is more prominent
            bool hasHouseKeyword = text.Contains("kuća") || text.Contains("kuca")
                || text.Contains("vikendica") || text.Contains("stambena zgrada");
            bool hasFlatKeyword = text.Contains("stan ") || text.Contains("apartman");

            if (!hasHouseKeyword && !hasFlatKeyword)
                return "građevinsko zemljište";
        }

        // ── 3. House ──
        bool looksLikeFlat = text.Contains("stan na") || text.Contains("stan u ");
        if (!looksLikeFlat &&
            (text.Contains("kuća") || text.Contains("kuca") ||
             text.Contains("vikendica") ||
             text.Contains("zgrada mješovite uporabe") ||
             text.Contains("zgrada mjesovite uporabe")))
            return "kuća";

        // ── 4. Default: flat ──
        return "stan";
    }

    /// <summary>
    /// Returns true if detected type is any form of land/parcel.
    /// </summary>
    public static bool IsLandType(string detectedType) =>
        detectedType.Contains("zemljišt", StringComparison.OrdinalIgnoreCase) ||
        detectedType.Contains("parcela", StringComparison.OrdinalIgnoreCase) ||
        detectedType.Contains("oranica", StringComparison.OrdinalIgnoreCase);

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
    NoComparables = 6,
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

    // V8.2: AI-extracted features (populated by ValuationService batch call)
    public bool? HasGarage { get; set; }
    public bool? HasRenovation { get; set; }
    public bool? HasYard { get; set; }
    public string? AiCondition { get; set; }      // "new", "renovated", "good", "original", "needs_work"
    public double? AiYardAreaM2 { get; set; }      // AI-detected yard area
    public double? AiSizeM2 { get; set; }          // AI-detected property size
    public string? AiPropertyType { get; set; }    // AI-detected type

    // V9 Deep Scraping: enrichment booleans
    public bool? IsRenovated { get; set; }          // True if renovated/new (YearReno>2015 or description)
    public bool? NeedsRenovation { get; set; }      // True if "za adaptaciju", "izvorno stanje", etc.
    public int? YearLastRenovation { get; set; }     // Parsed from detail page attributes
}

public class MarketResearchResult
{
    public List<ComparableSearchResult> ActiveListings { get; set; } = [];
    public List<ComparableSearchResult> RegionalData { get; set; } = [];
    public string RegionName { get; set; } = "";
    public string DetectedPropertyType { get; set; } = "";
    public double TargetSizeM2 { get; set; }
    public string Note { get; set; } = "";
    public ConfidenceScore Confidence { get; set; } = new();
    public string SearchPhaseUsed { get; set; } = "";
    public int TotalApiCalls { get; set; }

    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];
}
