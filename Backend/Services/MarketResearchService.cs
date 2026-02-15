using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

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
    private readonly string? _brightDataWsEndpoint;
    private readonly ILogger<MarketResearchService> _logger;

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
        ["građevinsko zemljište"]  = ["građevinsko zemljište", "zemljište", "parcela", "građevinski teren"],
        ["poljoprivredno zemljište"] = ["poljoprivredno zemljište", "oranica", "pašnjak", "livada", "šuma", "voćnjak", "vinograd", "poljoprivredno"],
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
        ["vikendica"]             = "prodaja-vikendica",
        ["vila"]                  = "prodaja-kuca",
        ["građevinsko zemljište"]  = "prodaja-zemljista",
        ["gradjevinsko zemljiste"] = "prodaja-zemljista",
        ["poljoprivredno zemljište"] = "prodaja-zemljista",
        ["poslovni prostor"]      = "prodaja-poslovnih-prostora",
    };

    private static readonly Regex NjuskaloListingPattern = new(
        @"###\s*\[(?<title>[^\]]+)\]\((?<url>https://www\.njuskalo\.hr/[^)]+)\)",
        RegexOptions.Compiled);

    // V24: Negative lookahead — do NOT match "981 €/m²" as a total price.
    //      Only matches total prices like "386.000 €".
    private static readonly Regex NjuskaloPricePattern = new(
        @"(?<price>\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*€(?!\s*/\s*m[²2])",
        RegexOptions.Compiled);

    // V24: Explicit price-per-m² pattern for search results
    private static readonly Regex NjuskaloPricePerM2Pattern = new(
        @"(?<price>\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*€\s*/\s*m[²2]",
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
        { "Manuš",       ["Meje", "Spinut", "Varoš", "Grad"] },
        { "Varoš",       ["Manuš", "Meje", "Grad", "Lučac"] },
        // Split — center / historic core (V23: premium center neighborhoods)
        { "Grad",          ["Varoš", "Manuš", "Lučac", "Bačvice"] },
        { "Lučac",         ["Grad", "Varoš", "Manuš", "Bačvice"] },
        { "centar Split",  ["Grad", "Varoš", "Manuš", "Lučac", "Bačvice"] },
        { "stari grad",    ["Grad", "Varoš", "Manuš", "Lučac"] },
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
        { "Opatija", "Rijeka" },
        // NOTE: Crikvenica intentionally NOT mapped to Rijeka (Location Lock — City Integrity).
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
        // V23: Split center/historic core neighborhoods
        { "Grad", "Split" }, { "centar", "Split" }, { "centar Split", "Split" }, { "stari grad", "Split" },
        { "Trstenik", "Split" },
        { "Pujanke", "Split" }, { "Ravne njive", "Split" },
        { "Mejaši", "Split" }, { "Mejasi", "Split" },
    };

    // ══════════════════════════════════════════════════════════
    //  SUB-DISTRICTS — nearby places to expand to when <3 local results
    //  Key: city name → sub-districts / nearby small towns within ~5 km.
    //  This is used INSTEAD of jumping to a completely different city.
    //  Maintains City Integrity: Crikvenica → Dramalj/Selce, NOT → Rijeka.
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> SubDistricts =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Crikvenica", ["Dramalj", "Selce", "Jadranovo", "Novi Vinodolski"] },
        { "Opatija",    ["Lovran", "Ičići", "Ika", "Matulji", "Medveja"] },
        { "Makarska",   ["Tučepi", "Podgora", "Brela", "Baška Voda", "Gradac"] },
        { "Vodice",     ["Tribunj", "Srima", "Prvić"] },
        { "Biograd na Moru", ["Sv. Filip i Jakov", "Pakoštane", "Turanj"] },
        { "Omiš",       ["Dugi Rat", "Mimice", "Pisak"] },
        { "Nin",        ["Privlaka", "Vrsi", "Zaton"] },
        { "Senj",       ["Sv. Juraj", "Klada", "Lukovo"] },
        { "Cavtat",     ["Plat", "Mlini", "Srebreno"] },
        { "Novalja",    ["Stara Novalja", "Zubovići", "Caska"] },
    };

    // ══════════════════════════════════════════════════════════
    //  FORBIDDEN CITY PAIRS — hard block cross-city contamination
    //  If subject is in city A, listings from city B are FORBIDDEN.
    //  Bidirectional: both directions are blocked.
    // ══════════════════════════════════════════════════════════
    private static readonly HashSet<(string, string)> ForbiddenCityPairs =
        new()
    {
        ("crikvenica", "rijeka"), ("rijeka", "crikvenica"),
        ("opatija", "rijeka"),    ("rijeka", "opatija"),
        ("makarska", "split"),    ("split", "makarska"),
        ("omiš", "split"),        ("split", "omiš"),
        ("omis", "split"),        ("split", "omis"),
        ("vodice", "šibenik"),    ("šibenik", "vodice"),
        ("vodice", "sibenik"),    ("sibenik", "vodice"),
        ("cavtat", "dubrovnik"),  ("dubrovnik", "cavtat"),
        ("nin", "zadar"),         ("zadar", "nin"),
    };

    // ══════════════════════════════════════════════════════════
    //  V38: MAJOR CITIES — universal cross-city contamination block
    //  Any listing from a DIFFERENT major city is automatically rejected.
    //  This prevents absurdities like comparing Zagreb with Split.
    // ══════════════════════════════════════════════════════════
    private static readonly string[] MajorCities =
    [
        "zagreb", "split", "rijeka", "osijek", "zadar", "pula",
        "slavonski brod", "karlovac", "varaždin", "varazdin",
        "šibenik", "sibenik", "dubrovnik", "bjelovar", "vukovar",
        "vinkovci", "sisak", "čakovec", "cakovec", "koprivnica",
        "požega", "pozega", "virovitica", "gospić", "gospic",
    ];

    /// <summary>
    /// V38: Resolve which major city a region/neighborhood belongs to.
    /// Uses BroaderRegion, VillageFallback, and PrestigePeers to trace
    /// neighborhoods/suburbs back to their parent major city.
    /// Returns null if not resolvable to a major city.
    /// </summary>
    internal static string? ResolveMajorCity(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName)) return null;
        var lower = regionName.ToLowerInvariant().Trim();

        // Direct match — the region IS a major city
        if (MajorCities.Contains(lower)) return lower;

        // Check BroaderRegion mapping (neighborhood → city)
        if (BroaderRegion.TryGetValue(regionName, out var parent))
        {
            var parentLower = parent.ToLowerInvariant();
            if (MajorCities.Contains(parentLower)) return parentLower;
            // One more hop for deep nesting (e.g. "Manuš" → "Split")
            if (BroaderRegion.TryGetValue(parent, out var grandparent))
            {
                var gpLower = grandparent.ToLowerInvariant();
                if (MajorCities.Contains(gpLower)) return gpLower;
            }
        }

        // Check VillageFallback mapping
        if (VillageFallback.TryGetValue(regionName, out var fallbackTown))
        {
            var ftLower = fallbackTown.ToLowerInvariant();
            if (MajorCities.Contains(ftLower)) return ftLower;
            // One more hop
            if (BroaderRegion.TryGetValue(fallbackTown, out var fbParent))
            {
                var fpLower = fbParent.ToLowerInvariant();
                if (MajorCities.Contains(fpLower)) return fpLower;
            }
        }

        // Check PrestigePeers — if it's a key, try to resolve via BroaderRegion
        if (PrestigePeers.ContainsKey(regionName))
        {
            if (BroaderRegion.TryGetValue(regionName, out var peerParent))
            {
                var ppLower = peerParent.ToLowerInvariant();
                if (MajorCities.Contains(ppLower)) return ppLower;
            }
        }

        // Substring check — "Zagreb" inside "Zagreb, Centar" etc.
        foreach (var city in MajorCities)
        {
            if (lower.Contains(city)) return city;
        }

        return null;
    }

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

    public MarketResearchService(HttpClient httpClient, IConfiguration config, ILogger<MarketResearchService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Tavily:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Tavily__ApiKey")
            ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
            ?? Environment.GetEnvironmentVariable("TAVILY_APIKEY")
            ?? throw new InvalidOperationException(
                "Tavily API key is not configured. Set Tavily__ApiKey or TAVILY_API_KEY environment variable.");

        // ── Bright Data Scraping Browser (Playwright CDP) ──
        // Env var takes priority so Render can override the appsettings.json placeholder
        _brightDataWsEndpoint = Environment.GetEnvironmentVariable("BRIGHTDATA_BROWSER_WS")
            ?? config["BrightData:BrowserWsEndpoint"];
        // Detect placeholder values that were never replaced
        if (_brightDataWsEndpoint != null &&
            (_brightDataWsEndpoint.Contains("CUSTOMER_ID") ||
             _brightDataWsEndpoint.Contains("ZONE_NAME") ||
             _brightDataWsEndpoint.Contains("PASSWORD")))
        {
            _brightDataWsEndpoint = null;
        }
        if (string.IsNullOrWhiteSpace(_brightDataWsEndpoint))
        {
            _brightDataWsEndpoint = null;
            logger.LogWarning("BrightData Scraping Browser not configured — cookie wall fallback will use Tavily Search only");
        }
        else
        {
            logger.LogInformation("BrightData Scraping Browser configured (CDP endpoint)");
        }
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT — V4 "SHOTGUN"
    // ══════════════════════════════════════════════════════════

    public async Task<MarketResearchResult> SearchComparables(
        string location, double sizeM2, string pdfText = "",
        string microLocation = "", List<string>? keyFeatures = null)
    {
        var detectedType = DetectPropertyType(pdfText);
        _currentDetectedType = detectedType;   // Set for PriceFirstFilter gates
        _currentSubjectRegion = ExtractRegionName(location);  // Set for City Integrity gate
        _cityIntegrityRejections = 0;          // Reset per-call counters
        _rejectedCityNames.Clear();
        _currentSubjectMajorCity = ResolveMajorCity(location) ?? ResolveMajorCity(_currentSubjectRegion);  // V38
        var regionName = ExtractRegionName(location);
        var subLocations = ExtractLocationHierarchy(location);
        bool isLand = IsLandType(detectedType);
        var synonyms = GetTypeSynonyms(detectedType);
        var now = DateTime.Now;
        var searchPhase = SearchPhase.Primary;
        int totalApiCalls = 0;

        // Master list — all price-verified results across all phases
        var allVerified = new List<ComparableSearchResult>();
        var searchLog = new List<SearchLogEntry>();

        _logger.LogInformation(
            "V4 Shotgun: Type={Type}, Synonyms=[{Syns}], Region={Region}, SubLocs=[{Subs}], MicroLoc={MicroLoc}",
            detectedType, string.Join(", ", synonyms), regionName,
            string.Join(" → ", subLocations), microLocation);

        // Log village fallback and size cap decisions
        if (VillageFallback.TryGetValue(regionName, out var logFallback))
            _logger.LogInformation("Village detected: {Village} → fallback town: {Town}", regionName, logFallback);
        if (!isLand && sizeM2 > 300)
            _logger.LogWarning("Size {Size:0}m² too large for living area ({Type}) — skipping livingArea filter (likely plot area)",
                sizeM2, detectedType);

        // ─────────────────────────────────────────────────────
        // V23 PHASE 0M: MICRO-LOCATION NJUŠKALO SEARCH
        //   When user provides a micro-location (e.g., "centar", "Varoš", "Grad"),
        //   search Njuškalo with neighborhood-specific slugs FIRST.
        //   This ensures center-of-city properties find center-level comparables
        //   rather than being diluted by cheap suburban listings.
        // ─────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(microLocation))
        {
            _logger.LogInformation("V23 Phase 0M: Micro-location search for '{MicroLoc}' in {Region}",
                microLocation, regionName);

            // Try to map micro-location to a Njuškalo neighborhood slug
            // Njuškalo uses city-neighborhood format: split-varos, split-spinut, etc.
            var microSlug = NormalizeLocationSlug(microLocation);
            var citySlug = NormalizeLocationSlug(regionName);

            // Also check if micro-location IS a known neighborhood in BroaderRegion
            string? microCity = null;
            if (BroaderRegion.TryGetValue(microLocation, out var mc))
                microCity = mc;
            else if (BroaderRegion.TryGetValue(regionName, out var rc2))
                microCity = rc2;

            var microLocs = new List<string> { regionName, microLocation };
            if (microCity != null) microLocs.Add(microCity);

            // Search 1: Direct Njuškalo neighborhood URL
            var microResults = await FetchNjuskaloDirectListings(detectedType, microLocation, sizeM2);
            if (microResults.Count == 0 && microCity != null)
            {
                // Try city-neighborhood format as location
                microResults = await FetchNjuskaloDirectListings(detectedType,
                    $"{microCity}-{microLocation}", sizeM2);
            }
            totalApiCalls += (microResults.Count > 0 ? 1 : 0);

            var microFiltered = PriceFirstFilter(microResults, "Phase0M-MicroLoc", microLocs);
            MergeInto(allVerified, microFiltered);

            _logger.LogInformation("Phase 0M (Micro-location '{MicroLoc}'): {New}/{Raw} passed → total={Total}",
                microLocation, microFiltered.Count, microResults.Count, allVerified.Count);
            searchLog.Add(new SearchLogEntry
            {
                Phase = "0M-MicroLocation",
                Query = $"Njuškalo micro: {microLocation} in {regionName}",
                ResultCount = microResults.Count,
                PassedFilter = microFiltered.Count
            });

            // Search 2: Tavily search with micro-location keywords for premium coverage
            if (allVerified.Count < 8)
            {
                var microQueries = new List<string>
                {
                    $"site:njuskalo.hr prodaja {detectedType} \"{regionName}\" \"{microLocation}\" € {now.Year}",
                    $"site:njuskalo.hr {detectedType} \"{regionName} {microLocation}\" cijena €",
                };

                // Add key features as search terms (e.g., "Dioklecijanova palača")
                if (keyFeatures?.Count > 0)
                {
                    var landmark = keyFeatures.FirstOrDefault(f =>
                        f.Contains("palač", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("riva", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("marjan", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("katedr", StringComparison.OrdinalIgnoreCase));
                    if (landmark != null)
                    {
                        microQueries.Add(
                            $"site:njuskalo.hr {detectedType} \"{regionName}\" \"{landmark}\" €");
                    }
                }

                var tavilyMicroResults = await FireParallelSearches(microQueries, 15, "Phase0M-Tavily");
                totalApiCalls += microQueries.Count;

                var tavilyMicroFiltered = PriceFirstFilter(tavilyMicroResults, "Phase0M-Tavily", microLocs);
                MergeInto(allVerified, tavilyMicroFiltered);

                _logger.LogInformation("Phase 0M Tavily (MicroLoc): +{New} → total={Total}",
                    tavilyMicroFiltered.Count, allVerified.Count);
            }

            // If micro-location found decent results, add prestige peers for that neighborhood
            if (allVerified.Count >= 3 && allVerified.Count < 8
                && PrestigePeers.TryGetValue(microLocation, out var microPeers))
            {
                var peerQueries = microPeers.Take(3)
                    .Select(p => $"site:njuskalo.hr prodaja {detectedType} \"{p}\" cijena € {now.Year}")
                    .ToList();

                var peerResults = await FireParallelSearches(peerQueries, 10, "Phase0M-Peers");
                totalApiCalls += peerQueries.Count;

                var peerAllowed = new List<string>(microLocs);
                peerAllowed.AddRange(microPeers);
                var peerFiltered = PriceFirstFilter(peerResults, "Phase0M-Peers", peerAllowed);
                MergeInto(allVerified, peerFiltered);

                _logger.LogInformation("Phase 0M Peers: +{New} → total={Total}",
                    peerFiltered.Count, allVerified.Count);
            }
        }

        // ─────────────────────────────────────────────────────
        // PHASE 0A: Njuškalo DIRECT — ALL params (type + location + SIZE)
        //   Constructs njuskalo.hr/prodaja-zemljista/vir?livingArea[min]=X&[max]=Y
        //   Uses Tavily Extract API (their browser infra bypasses CAPTCHA)
        //   Gets 25+ listings per page vs. 3-5 from search
        // ─────────────────────────────────────────────────────
        // Location gate: allow both the neighborhood AND parent city.
        var phase0Locs = new List<string> { regionName };
        if (BroaderRegion.TryGetValue(regionName, out var parentCity))
            phase0Locs.Add(parentCity);
        else if (VillageFallback.TryGetValue(regionName, out var fallbackTown0))
            phase0Locs.Add(fallbackTown0);

        {
            var njuskaloUrl0A = BuildNjuskaloSearchUrl(detectedType, regionName, sizeM2);
            var njuskaloResults = await FetchNjuskaloDirectListings(detectedType, regionName, sizeM2);
            totalApiCalls += (njuskaloResults.Count > 0 ? 1 : 0);

            var filtered = PriceFirstFilter(njuskaloResults, "Phase0A-Full", phase0Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 0A (Njuškalo Full: type+loc+size): {New}/{Raw} passed → total={Total}",
                filtered.Count, njuskaloResults.Count, allVerified.Count);
            searchLog.Add(new SearchLogEntry { Phase = "0A-Njuškalo(type+loc+size)", Query = njuskaloUrl0A ?? "(no URL)", ResultCount = njuskaloResults.Count, PassedFilter = filtered.Count });
        }

        // ─────────────────────────────────────────────────────
        // PHASE 0B: Njuškalo DIRECT — SIMPLIFIED (type + location, NO size)
        //   If Phase 0A didn't find enough, re-search WITHOUT size filter.
        //   Njuškalo always has listings when you drop size constraints.
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 5)
        {
            var njuskaloUrl0B = BuildNjuskaloSearchUrl(detectedType, regionName, 0);
            var njuskaloResults = await FetchNjuskaloDirectListings(detectedType, regionName, 0);
            totalApiCalls += (njuskaloResults.Count > 0 ? 1 : 0);

            var filtered = PriceFirstFilter(njuskaloResults, "Phase0B-NoSize", phase0Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 0B (Njuškalo NoSize: type+loc): {New}/{Raw} passed → total={Total}",
                filtered.Count, njuskaloResults.Count, allVerified.Count);
            searchLog.Add(new SearchLogEntry { Phase = "0B-Njuškalo(type+loc)", Query = njuskaloUrl0B ?? "(no URL)", ResultCount = njuskaloResults.Count, PassedFilter = filtered.Count });
        }

        // ─────────────────────────────────────────────────────
        // PHASE 0C: Njuškalo DIRECT — MINIMAL (type + city only)
        //   If still not enough, search broader city URL (no neighborhood).
        //   E.g. /prodaja-stanova/zagreb instead of /prodaja-stanova/zagreb-srebrnjak
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 5)
        {
            string? cityForMinimal = null;
            if (BroaderRegion.TryGetValue(regionName, out var pc))
                cityForMinimal = pc;
            else if (VillageFallback.TryGetValue(regionName, out var ft))
                cityForMinimal = ft;

            if (cityForMinimal != null)
            {
                var njuskaloUrl0C = BuildNjuskaloSearchUrl(detectedType, cityForMinimal, 0);
                var njuskaloResults = await FetchNjuskaloDirectListings(detectedType, cityForMinimal, 0);
                totalApiCalls += (njuskaloResults.Count > 0 ? 1 : 0);

                // Accept listings from the whole city — relaxed location gate
                var minimalLocs = new List<string>(phase0Locs) { cityForMinimal };
                var filtered = PriceFirstFilter(njuskaloResults, "Phase0C-CityOnly", minimalLocs);
                MergeInto(allVerified, filtered);

                _logger.LogInformation("Phase 0C (Njuškalo Minimal: type+city={City}): {New}/{Raw} passed → total={Total}",
                    cityForMinimal, filtered.Count, njuskaloResults.Count, allVerified.Count);
                searchLog.Add(new SearchLogEntry { Phase = "0C-Njuškalo(type+city)", Query = njuskaloUrl0C ?? $"(city: {cityForMinimal})", ResultCount = njuskaloResults.Count, PassedFilter = filtered.Count });
            }
        }

        // ─────────────────────────────────────────────────────
        // PHASE 1: Tavily search — exact neighborhood in quotes
        //   V14: ALWAYS runs (even if Phase 0 found some) for maximum coverage.
        //   Fires 4-6 parallel queries using "exact quotes" for neighborhood.
        //   For kuća: also targets vikendica/vila categories.
        //   For villages, also includes fallback town queries.
        //   Location gate: STRICT for cities, relaxed for villages.
        // ─────────────────────────────────────────────────────────────
        if (allVerified.Count < 12)
        {
            // Determine if we have a village fallback — add extra queries for the town
            // V15-fix: Only treat as village when Phase 0 found very few results,
            // so small cities like Daruvar that have Njuškalo listings aren't
            // unnecessarily expanded to a bigger city like Bjelovar.
            string searchLoc = regionName;
            string? villageFallbackTown1 = null;
            bool isVillage = allVerified.Count < 2
                          && VillageFallback.TryGetValue(regionName, out villageFallbackTown1);

            // V14: Build broad multi-category queries
            bool isHouseType = detectedType.Contains("kuć", StringComparison.OrdinalIgnoreCase)
                            || detectedType.Contains("kuc", StringComparison.OrdinalIgnoreCase)
                            || detectedType.Contains("vila", StringComparison.OrdinalIgnoreCase);
            string orClause = isHouseType
                ? "(kuća OR vikendica OR vila OR obiteljska kuća)"
                : detectedType;

            var queries = new List<string>
            {
                // Q1: Exact neighborhood + type
                $"site:njuskalo.hr prodaja {detectedType} \"{searchLoc}\" cijena € {now.Year}",
                // Q2: Synonym variation
                $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} \"{searchLoc}\" € {now.Year}",
                // Q3: Size-based or generic
                sizeM2 > 0
                    ? $"site:njuskalo.hr {detectedType} \"{searchLoc}\" {sizeM2:0}m2 €"
                    : $"site:njuskalo.hr nekretnina \"{searchLoc}\" prodaja € {now.Year}",
                // Q4 (V14): Multi-category OR query for broader coverage
                $"site:njuskalo.hr \"{searchLoc}\" {orClause} prodaja € {now.Year}",
            };

            // For villages: also search the fallback town explicitly
            if (isVillage)
            {
                queries.Add($"site:njuskalo.hr prodaja {detectedType} \"{villageFallbackTown1}\" cijena € {now.Year}");
                queries.Add($"site:njuskalo.hr \"{villageFallbackTown1}\" {orClause} prodaja €");
            }

            var phase1Results = await FireParallelSearches(queries, 20, "Phase1");
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
            foreach (var q in queries)
                searchLog.Add(new SearchLogEntry { Phase = "1-Tavily", Query = q, ResultCount = phase1Results.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
        } // end Phase 1 (if <5)

        // ─────────────────────────────────────────────────────
        // PHASE 2: Multi-portal spread — same location, different portals
        //   Fires 3 parallel queries (only if <5 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 8)
        {
            searchPhase = SearchPhase.MultiPortal;

            var queries = new List<string>
            {
                $"site:index.hr/oglasi prodaja {detectedType} \"{regionName}\" €",
                $"site:crozilla.com {detectedType} \"{regionName}\" prodaja €",
                $"site:nekretnine.hr {detectedType} \"{regionName}\" cijena €",
            };

            var phase2Results = await FireParallelSearches(queries, 15, "Phase2-Portals");
            totalApiCalls += queries.Count;
            // For villages, also accept the fallback town.
            var phase2Locs = new List<string> { regionName };
            if (VillageFallback.TryGetValue(regionName, out var fallbackTown2))
                phase2Locs.Add(fallbackTown2);
            var filtered = PriceFirstFilter(phase2Results, "Phase2", phase2Locs);
            MergeInto(allVerified, filtered);

            _logger.LogInformation("Phase 2 (multi-portal): +{New} → total={Total}",
                filtered.Count, allVerified.Count);
            foreach (var q in queries)
                searchLog.Add(new SearchLogEntry { Phase = "2-MultiPortal", Query = q, ResultCount = phase2Results.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
        }

        // ─────────────────────────────────────────────────────
        // PHASE 3: PRESTIGE PEERS — search only peer neighborhoods
        //   e.g. Srebrnjak → [Pantovčak, Tuškanac, Šalata, Jurjevska]
        //   If no peers defined, falls back to BroaderRegion as before.
        //   Fires 1 query per peer (max 4) — only if <3 results
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 6)
        {
            searchPhase = SearchPhase.WiderRegion;

            var hasPeers = PrestigePeers.TryGetValue(regionName, out var peers);

            if (hasPeers && peers is { Length: > 0 })
            {
                // Search each peer neighborhood individually (exact quotes)
                var queries = peers.Take(4)
                    .Select(peer => $"site:njuskalo.hr prodaja {detectedType} \"{peer}\" cijena € {now.Year}")
                    .ToList();

                var peerResults = await FireParallelSearches(queries, 15, "Phase3-Peers");
                totalApiCalls += queries.Count;

                // Only allow listings from the peer neighborhoods (or regionName itself)
                var allowedLocs = new List<string> { regionName };
                allowedLocs.AddRange(peers);
                var filtered = PriceFirstFilter(peerResults, "Phase3-Peers", allowedLocs);
                MergeInto(allVerified, filtered);

                _logger.LogInformation(
                    "Phase 3 (peers of {Region}: [{Peers}]): +{New} → total={Total}",
                    regionName, string.Join(", ", peers), filtered.Count, allVerified.Count);
                foreach (var q in queries)
                    searchLog.Add(new SearchLogEntry { Phase = "3-Peers", Query = q, ResultCount = peerResults.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
            }
            else
            {
                // V13: Try SUB-DISTRICTS first (nearby places <5km) before broader region
                var subs = GetSubDistricts(regionName);
                if (subs is { Length: > 0 })
                {
                    // Search each sub-district individually
                    var subQueries = subs.Take(4)
                        .Select(sd => $"site:njuskalo.hr prodaja {detectedType} \"{sd}\" cijena € {now.Year}")
                        .ToList();

                    var subResults = await FireParallelSearches(subQueries, 15, "Phase3-SubDistricts");
                    totalApiCalls += subQueries.Count;

                    // Allow listings from sub-districts + regionName itself
                    var subAllowed = new List<string> { regionName };
                    subAllowed.AddRange(subs);
                    var subFiltered = PriceFirstFilter(subResults, "Phase3-SubDistricts", subAllowed);
                    MergeInto(allVerified, subFiltered);

                    _logger.LogInformation(
                        "Phase 3 (sub-districts of {Region}: [{Subs}]): +{New} → total={Total}",
                        regionName, string.Join(", ", subs), subFiltered.Count, allVerified.Count);
                    foreach (var q in subQueries)
                        searchLog.Add(new SearchLogEntry { Phase = "3-SubDistricts", Query = q, ResultCount = subResults.Count / Math.Max(subQueries.Count, 1), PassedFilter = subFiltered.Count });
                }

                // If still <3 after sub-districts, try broader region (but City Integrity blocks forbidden cities)
                if (allVerified.Count < 5)
                {
                    var widerRegion = GetBroaderRegion(regionName);

                    var queries = new List<string>
                    {
                        $"site:njuskalo.hr prodaja {detectedType} \"{widerRegion}\" cijena € {now.Year}",
                        $"site:njuskalo.hr {synonyms.ElementAtOrDefault(1) ?? detectedType} \"{widerRegion}\" €",
                        $"prodaja {detectedType} \"{widerRegion}\" cijena € site:njuskalo.hr OR site:index.hr/oglasi",
                        $"site:njuskalo.hr {detectedType} \"{widerRegion}\" {now.Year}",
                    };

                    var phase3Results = await FireParallelSearches(queries, 15, "Phase3-Wider");
                    totalApiCalls += queries.Count;
                    var filtered = PriceFirstFilter(phase3Results, "Phase3", [regionName, widerRegion]);
                    MergeInto(allVerified, filtered);

                    _logger.LogInformation("Phase 3 (wider: {Region}->{Wider}): +{New} → total={Total}",
                        regionName, widerRegion, filtered.Count, allVerified.Count);
                    foreach (var q in queries)
                        searchLog.Add(new SearchLogEntry { Phase = "3-WiderRegion", Query = q, ResultCount = phase3Results.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // LAST RESORT RETRY — if prestige neighborhood with <3 listings,
        //   do one more Njuškalo scrape with MINIMAL params (type + city).
        //   Njuškalo always has listings at city level.
        // ─────────────────────────────────────────────────────
        bool hasPrestigePeers = PrestigePeers.ContainsKey(regionName);
        if (hasPrestigePeers && allVerified.Count < 3)
        {
            _logger.LogWarning(
                "LAST RESORT: {Region} has prestige peers but only {Count} listings. " +
                "Doing final Njuškalo city-level scrape.",
                regionName, allVerified.Count);

            // Find the parent city for this prestige neighborhood
            string retryCity = BroaderRegion.TryGetValue(regionName, out var rc) ? rc : regionName;

            var retryResults = await FetchNjuskaloDirectListings(detectedType, retryCity, 0);
            totalApiCalls += (retryResults.Count > 0 ? 1 : 0);

            var retryLocs = new List<string> { regionName, retryCity };
            if (PrestigePeers.TryGetValue(regionName, out var retryPeers))
                retryLocs.AddRange(retryPeers);
            var retryFiltered = PriceFirstFilter(retryResults, "LastResort-CityLevel", retryLocs);
            MergeInto(allVerified, retryFiltered);

            _logger.LogInformation("Last Resort (city={City}): +{New}/{Raw} → total={Total}",
                retryCity, retryFiltered.Count, retryResults.Count, allVerified.Count);
            var retryUrl = BuildNjuskaloSearchUrl(detectedType, retryCity, 0);
            searchLog.Add(new SearchLogEntry { Phase = "LastResort-City", Query = retryUrl ?? $"Njuškalo city={retryCity}", ResultCount = retryResults.Count, PassedFilter = retryFiltered.Count });
        }

        // ─────────────────────────────────────────────────────
        // PHASE 4: Synonym blitz — try ALL synonyms across portals
        //   Fires up to 4 parallel queries (only if <3 results)
        //   Location gate: regionName + peers (if any) — NEVER generic city
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 5)
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
                var phase4Results = await FireParallelSearches(queries, 15, "Phase4-Synonyms");
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
                foreach (var q in queries)
                    searchLog.Add(new SearchLogEntry { Phase = "4-SynonymBlitz", Query = q, ResultCount = phase4Results.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
            }
        }

        // ─────────────────────────────────────────────────────
        // PHASE 5: Sub-location drill — try every part of the address
        //   "Vir, Torovi, k.o. Vir" → try "Torovi", "Vir Torovi"
        //   Fires 1 query per sub-location (only if <2 results)
        // ─────────────────────────────────────────────────────
        if (allVerified.Count < 3 && subLocations.Count > 1)
        {
            searchPhase = SearchPhase.SubLocationDrill;

            var queries = subLocations
                .Where(loc => loc != regionName && loc.Length >= 3)
                .Take(3)
                .Select(loc => $"site:njuskalo.hr prodaja {detectedType} {loc} cijena €")
                .ToList();

            if (queries.Count > 0)
            {
                var phase5Results = await FireParallelSearches(queries, 10, "Phase5-SubLoc");
                totalApiCalls += queries.Count;
                var filtered = PriceFirstFilter(phase5Results, "Phase5", subLocations);
                MergeInto(allVerified, filtered);

                _logger.LogInformation("Phase 5 (sub-location): +{New} → total={Total}",
                    filtered.Count, allVerified.Count);
                foreach (var q in queries)
                    searchLog.Add(new SearchLogEntry { Phase = "5-SubLocation", Query = q, ResultCount = phase5Results.Count / Math.Max(queries.Count, 1), PassedFilter = filtered.Count });
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
            foreach (var q in queries)
                searchLog.Add(new SearchLogEntry { Phase = "6-MarketIntel", Query = q, ResultCount = phase6Results.Count / Math.Max(queries.Count, 1), PassedFilter = regionalData.Count });
        }

        // ─────────────────────────────────────────────────────
        // ASSEMBLE RESULT
        // ─────────────────────────────────────────────────────
        var distinctListings = allVerified
            .DistinctBy(x => x.Url)
            .Take(20)
            .ToList();

        // ─────────────────────────────────────────────────────
        // DEEP DIVE — visit top 16 listing pages for rich data
        //   Only for Njuškalo URLs (we have the DOM selectors).
        //   Enriches Content with full description, attributes,
        //   equipment list, renovation year, yard size, etc.
        //   Discards vanknjižno, reclassifies partial-floor sales.
        //   ~80s total (16 pages × ~5s each, sequential to avoid bans).
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

            // NOTE: Type filtering after DeepDive is now handled by AI classification
            // in ValuationService.FilterByAiPropertyType() — called after ExtractListingFeaturesAsync.
            // This is more reliable than keyword matching because AI reads the full listing text.
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
            CityIntegrityRejections = _cityIntegrityRejections,
            RejectedCityNames = [.. _rejectedCityNames],
            SearchLog = searchLog,
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
    /// Constructs the Njuškalo search URL and extracts listings via Tavily Extract API.
    /// V14: For house types, also scrapes "prodaja-vikendica" category for broader coverage.
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

            // V14: MULTI-CATEGORY — for kuća, also scrape vikendica category
            bool isHouseType = detectedType.Contains("kuć", StringComparison.OrdinalIgnoreCase)
                            || detectedType.Contains("kuc", StringComparison.OrdinalIgnoreCase)
                            || detectedType.Contains("vila", StringComparison.OrdinalIgnoreCase);
            if (isHouseType)
            {
                var vikendUrl = BuildNjuskaloSearchUrl("vikendica", regionName, sizeM2);
                if (vikendUrl != null && vikendUrl != njuskaloUrl)
                {
                    _logger.LogInformation("Phase0: Multi-category → also scraping vikendice: {Url}", vikendUrl);
                    var vikendResults = await TryScrapeSingleUrl(vikendUrl);
                    results.AddRange(vikendResults);
                }
            }

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
    /// V39: Extract listings from a Njuškalo category URL.
    /// Tries Tavily Extract first (fast, cheap). If it fails or returns 0 listings
    /// (cookie wall), falls back to BrightData Playwright browser which handles
    /// JavaScript rendering and cookie consent.
    /// </summary>
    private async Task<List<ComparableSearchResult>> TryScrapeSingleUrl(string njuskaloUrl)
    {
        _logger.LogInformation("Phase0: Tavily Extract → {Url}", njuskaloUrl);

        var extractedText = await ExecuteTavilyExtract(njuskaloUrl);
        List<ComparableSearchResult> listings = [];

        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            listings = ParseNjuskaloExtractedText(extractedText);
        }

        if (listings.Count > 0)
        {
            _logger.LogInformation("Phase0: Tavily parsed {Count} listings from {Url}", listings.Count, njuskaloUrl);
            return listings;
        }

        // ── V39: BrightData fallback — Tavily failed (cookie wall / empty) ──
        if (_brightDataWsEndpoint == null)
        {
            _logger.LogWarning("Phase0: Tavily returned 0 listings and BrightData not configured — {Url}", njuskaloUrl);
            return [];
        }

        _logger.LogInformation("Phase0: Tavily returned 0 listings, trying BrightData for {Url}", njuskaloUrl);
        try
        {
            var bdListings = await BrightDataScrapeSearchPage(njuskaloUrl);
            _logger.LogInformation("Phase0: BrightData extracted {Count} listings from {Url}", bdListings.Count, njuskaloUrl);
            return bdListings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phase0: BrightData fallback failed for {Url}", njuskaloUrl);
            return [];
        }
    }

    /// <summary>
    /// V39: Scrape a Njuškalo CATEGORY/SEARCH page via BrightData Playwright.
    /// Unlike BrightDataScrape (which extracts data from a single listing page),
    /// this method extracts individual listing LINKS from a search results page
    /// (e.g. /prodaja-stanova/zagreb) and returns them as ComparableSearchResult objects.
    /// Uses JavaScript DOM queries to extract listing titles, URLs, and prices.
    /// </summary>
    private async Task<List<ComparableSearchResult>> BrightDataScrapeSearchPage(string searchUrl)
    {
        if (string.IsNullOrEmpty(_brightDataWsEndpoint))
            return [];

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.ConnectOverCDPAsync(_brightDataWsEndpoint);

            var context = browser.Contexts.Count > 0
                ? browser.Contexts[0]
                : await browser.NewContextAsync();

            var page = await context.NewPageAsync();

            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            await page.WaitForTimeoutAsync(2000);

            // Inject consent cookies
            await page.EvaluateAsync(@"
                document.cookie = 'didomi_token=eyJ1c2VyX2lkIjoiIiwiY3JlYXRlZCI6IjIwMjQtMDEtMDFUMDA6MDA6MDAuMDAwWiIsInVwZGF0ZWQiOiIyMDI0LTAxLTAxVDAwOjAwOjAwLjAwMFoiLCJ2ZW5kb3JzIjp7ImVuYWJsZWQiOlsiZ29vZ2xlIl19LCJwdXJwb3NlcyI6eyJlbmFibGVkIjpbImdlb2xvY2F0aW9uX2RhdGEiLCJkZXZpY2VfY2hhcmFjdGVyaXN0aWNzIl19LCJ2ZXJzaW9uIjoyfQ==; domain=.njuskalo.hr; path=/; max-age=31536000';
                document.cookie = 'euconsent-v2=CQIRuQAQIRuQAAHABBENBOFgAAAAAAAAAChQAAAAAAFBIIQACAAFwAUABUADgAIAAeABIAC4AMAAZABDACSAFAALIAXQAvgBhADIAG0ANgAfgBEACLAEiAJcAUYArgBaADEAGiANoAdIA8AB6gD7ATCAmoE1AAA.flgAAAAAAAAA; domain=.njuskalo.hr; path=/; max-age=31536000';
                document.cookie = 'njuskalo_privacy_policy=accepted; domain=.njuskalo.hr; path=/; max-age=31536000';
            ");

            await page.ReloadAsync(new PageReloadOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            await page.WaitForTimeoutAsync(3000);

            // Try clicking consent button if still showing
            try
            {
                var consentSelectors = new[] {
                    "button:has-text('Slažem se')", "button:has-text('Prihvaćam')",
                    "button:has-text('Prihvati')", "button:has-text('Accept')",
                    "#didomi-notice-agree-button"
                };
                foreach (var selector in consentSelectors)
                {
                    try
                    {
                        var btn = page.Locator(selector).First;
                        if (await btn.IsVisibleAsync())
                        {
                            await btn.ClickAsync();
                            _logger.LogInformation("Phase0-BD: clicked consent '{Selector}' for {Url}", selector, searchUrl);
                            await page.WaitForTimeoutAsync(2000);
                            break;
                        }
                    }
                    catch { /* try next */ }
                }
            }
            catch { /* no consent dialog */ }

            // Extract listing data via JavaScript — query Njuškalo's DOM structure
            var listingData = await page.EvaluateAsync<List<BdListingEntry>>(@"
                (() => {
                    const results = [];
                    // Njuškalo uses .EntityList--Standard or .entity-body for listing cards
                    const cards = document.querySelectorAll('.EntityList--Standard .EntityList-item--Regular, .entity-body, li.EntityList-item--Regular');
                    for (const card of cards) {
                        try {
                            // Find the listing link
                            const link = card.querySelector('a.entity-description-title, a.link, h3 a, .entity-title a');
                            if (!link) continue;
                            const url = link.href || '';
                            const title = (link.textContent || '').trim();
                            if (!url.includes('njuskalo.hr/nekretnine/') && !url.includes('oglas-')) continue;
                            if (!title || title.length < 5) continue;

                            // Find price
                            const priceEl = card.querySelector('.price--eur .price--main, .entity-price .price--main, .price-item--main');
                            const priceText = priceEl ? (priceEl.textContent || '').trim() : '';

                            // Find size
                            const sizeEl = card.querySelector('.entity-description-main, .entity-features');
                            const sizeText = sizeEl ? (sizeEl.textContent || '').trim() : '';

                            results.push({ url, title, price: priceText, details: sizeText });
                        } catch(e) { /* skip card */ }
                    }
                    return results;
                })()
            ");

            await page.CloseAsync();

            var listings = new List<ComparableSearchResult>();
            if (listingData == null || listingData.Count == 0)
            {
                _logger.LogWarning("Phase0-BD: no listing cards found in DOM for {Url}", searchUrl);
                return [];
            }

            foreach (var entry in listingData)
            {
                if (string.IsNullOrWhiteSpace(entry.Url) || string.IsNullOrWhiteSpace(entry.Title))
                    continue;

                // Ensure absolute URL
                var url = entry.Url.StartsWith("http") ? entry.Url : $"https://www.njuskalo.hr{entry.Url}";

                // Parse price
                double? listedPrice = null;
                if (!string.IsNullOrWhiteSpace(entry.Price))
                {
                    var priceClean = entry.Price.Replace(".", "").Replace(",", ".");
                    var priceMatch = Regex.Match(priceClean, @"(\d+(?:\.\d+)?)");
                    if (priceMatch.Success && double.TryParse(priceMatch.Groups[1].Value,
                        System.Globalization.CultureInfo.InvariantCulture, out var p) && p > 1000)
                        listedPrice = p;
                }

                var content = $"{entry.Title}. {entry.Details}";
                if (listedPrice.HasValue)
                    content += $" Cijena: {listedPrice.Value:N0} €";

                listings.Add(new ComparableSearchResult
                {
                    Url = url,
                    Title = entry.Title,
                    Content = content,
                    ListedPriceEur = listedPrice,
                });
            }

            _logger.LogInformation("Phase0-BD: extracted {Count} listings via Playwright from {Url}",
                listings.Count, searchUrl);
            return listings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phase0-BD: Playwright scrape failed for {Url}", searchUrl);
            return [];
        }
        finally
        {
            if (browser != null) try { await browser.CloseAsync(); } catch { }
            playwright?.Dispose();
        }
    }

    /// <summary>V39: DTO for BrightData search page listing extraction.</summary>
    private class BdListingEntry
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Price { get; set; } = "";
        public string Details { get; set; } = "";
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
    /// V20: Deep dive ALL listings — visit each detail page and enrich with scraped data.
    /// Every listing gets a deep dive attempt. If it fails, DeepDiveStatus explains why.
    /// Discards listings with "vanknjižno vlasništvo" and reclassifies
    /// "prodaje se samo gornji kat" as apartment.
    /// </summary>
    private async Task DeepDiveTopListingsAsync(List<ComparableSearchResult> listings, string regionName = "")
    {
        if (listings.Count == 0)
        {
            _logger.LogInformation("DeepDive: no listings to visit");
            return;
        }

        // ── Step 1: Separate Njuškalo and non-Njuškalo listings ──
        var njuskaloListings = listings
            .Where(l => l.Url.Contains("njuskalo.hr", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var otherListings = listings
            .Where(l => !l.Url.Contains("njuskalo.hr", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // ── Step 1b: Mark non-Njuškalo listings — attempt Tavily Extract for them too ──
        foreach (var listing in otherListings)
        {
            try
            {
                var rawText = await ExecuteTavilyExtract(listing.Url);
                if (!string.IsNullOrWhiteSpace(rawText) && rawText.Length > 300)
                {
                    listing.Content = $"[DEEP DIVE] {(rawText.Length > 3000 ? rawText[..3000] : rawText)} [ORIGINAL] {listing.Content}";
                    listing.DeepDiveStatus = $"✓ Tavily Extract OK ({rawText.Length:N0} znakova)";
                    _logger.LogInformation("DeepDive: Tavily Extract OK for non-Njuškalo {Url} — {Len} chars",
                        listing.Url, rawText.Length);
                }
                else
                {
                    listing.DeepDiveStatus = "✗ Tavily Extract prazan — nije Njuškalo, samo osnovni snippet";
                }
            }
            catch (Exception ex)
            {
                listing.DeepDiveStatus = $"✗ Tavily Extract greška: {ex.Message[..Math.Min(ex.Message.Length, 60)]}";
                _logger.LogWarning(ex, "DeepDive: Tavily Extract failed for non-Njuškalo {Url}", listing.Url);
            }
        }

        if (njuskaloListings.Count == 0)
        {
            _logger.LogInformation("DeepDive: no Njuškalo URLs to visit ({Other} non-Njuškalo attempted)", otherListings.Count);
            return;
        }

        // ── Step 2: Sort by priority — neighborhood match first, then by content needs ──
        //    V20: NO LIMIT — deep dive ALL Njuškalo listings
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
            // V20: NO .Take() — deep dive ALL listings
            .Select(x => x.Listing)
            .ToList();

        // V20d: Safety cap — max 25 Njuškalo listings (prevents runaway deep dives)
        if (scored.Count > 25)
        {
            _logger.LogInformation("DeepDive V20d: capping from {All} to 25 Njuškalo listings", scored.Count);
            // Mark the rest as not deep-dived
            foreach (var overflow in scored.Skip(25))
                overflow.DeepDiveStatus = $"⚠ Preskočen — limit 25 deep dive-ova ({scored.Count} ukupno)";
            scored = scored.Take(25).ToList();
        }

        _logger.LogInformation("DeepDive V20: deep-diving {Count} Njuškalo listings in parallel (+ {Other} non-Njuškalo attempted)",
            scored.Count, otherListings.Count);

        // ── Step 3: Visit listings in PARALLEL (V20d — 4 concurrent BrightData scrapes) ──
        // For Njuškalo URLs: use BrightData FIRST (Tavily Extract always hits cookie wall).
        // For other portals: use Tavily Extract as before.
        var toDiscard = new System.Collections.Concurrent.ConcurrentBag<ComparableSearchResult>();
        var semaphore = new SemaphoreSlim(4); // Max 4 concurrent BrightData connections

        var tasks = scored.Select(listing => Task.Run(async () =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await DeepDiveOneListingAsync(listing, toDiscard);
            }
            finally
            {
                semaphore.Release();
            }
        }));

        var results = await Task.WhenAll(tasks);
        int enriched = results.Count(r => r);

        semaphore.Dispose();

        // ── Remove discarded listings from the main list ──
        if (!toDiscard.IsEmpty)
        {
            foreach (var bad in toDiscard)
                listings.Remove(bad);
            _logger.LogInformation("DeepDive: discarded {Count} listings (vanknjižno/invalid)", toDiscard.Count);
        }

        _logger.LogInformation("DeepDive V20: enriched {Enriched}/{Total} Njuškalo + {Other} non-Njuškalo listings",
            enriched, scored.Count, otherListings.Count);

        // ── V20: Ensure every listing has a DeepDiveStatus ──
        foreach (var listing in listings)
        {
            listing.DeepDiveStatus ??= "⚠ Nije obrađen — nema status";
        }

        // ── V19: Title-based enrichment for ALL listings that lack structured data ──
        // Many listings have informative titles like "Kuća: Daruvar, 2 stana, dupla garaža, dvorište!"
        // Extract features from title + content for listings that didn't get deep-dived or got cookie wall.
        EnrichFromTitleAndSnippet(listings);
    }

    /// <summary>
    /// V20d: Deep-dive a single listing — extracted from the loop for parallel execution.
    /// Each listing gets BrightData → Tavily Extract → Tavily Search fallback chain.
    /// Thread-safe: only modifies its own listing; uses ConcurrentBag for discard list.
    /// </summary>
    private async Task<bool> DeepDiveOneListingAsync(
        ComparableSearchResult listing,
        System.Collections.Concurrent.ConcurrentBag<ComparableSearchResult> toDiscard)
    {
        try
        {
            bool isNjuskalo = listing.Url.Contains("njuskalo.hr", StringComparison.OrdinalIgnoreCase);
            string rawText = "";
            bool hasRawText = false;

            // ── NJUŠKALO: Try BrightData FIRST (Tavily always hits cookie wall) ──
            if (isNjuskalo && _brightDataWsEndpoint != null)
            {
                rawText = await BrightDataScrape(listing.Url);
                if (!string.IsNullOrWhiteSpace(rawText) && rawText.Length > 300)
                {
                    listing.DeepDiveStatus = $"✓ BrightData OK ({rawText.Length:N0} znakova)";
                    _logger.LogInformation("DeepDive: BRIGHT DATA OK for {Url} — {Len} chars",
                        listing.Url, rawText.Length);
                    hasRawText = true;
                }
                else
                {
                    _logger.LogInformation("DeepDive: BrightData failed for {Url}, trying Tavily Extract",
                        listing.Url);
                }
            }
            else if (isNjuskalo && _brightDataWsEndpoint == null)
            {
                _logger.LogInformation("DeepDive: BrightData not configured, trying Tavily for {Url}", listing.Url);
            }

            if (!hasRawText)
            {
                // ── Tavily Extract (works for non-Njuškalo, or as Njuškalo fallback) ──
                rawText = await ExecuteTavilyExtract(listing.Url);
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    listing.DeepDiveStatus = _brightDataWsEndpoint != null
                        ? "✗ BrightData neuspješan + Tavily Extract prazan"
                        : "✗ BrightData nije konfiguriran + Tavily Extract prazan";
                    return false;
                }

                // ── DETECT: Cookie consent wall — Njuškalo blocks Tavily scraper ──
                var rawLower = rawText.ToLowerInvariant();
                if (rawLower.Contains("koristimo kolačiće") || rawLower.Contains("koristimo kolacice") ||
                    rawLower.Contains("cookie") && rawLower.Contains("pristanak") ||
                    rawLower.Contains("shieldsquare") || rawLower.Contains("captcha") ||
                    rawLower.Contains("ispričavam se zbog neugodnosti") ||
                    (rawText.Length < 500 && rawLower.Contains("kolačić")))
                {
                    _logger.LogInformation("DeepDive: COOKIE WALL for {Url} — trying fallbacks", listing.Url);

                    // ── BrightData fallback (only if not already tried above for Njuškalo) ──
                    if (_brightDataWsEndpoint != null && !isNjuskalo)
                    {
                        var bdText = await BrightDataScrape(listing.Url);
                        if (!string.IsNullOrWhiteSpace(bdText) && bdText.Length > 300)
                        {
                            rawText = bdText;
                            _logger.LogInformation("DeepDive: BRIGHT DATA OK for {Url} — {Len} chars",
                                listing.Url, rawText.Length);
                            hasRawText = true;
                        }
                        else
                        {
                            _logger.LogInformation("DeepDive: BrightData failed for {Url}, falling back to Tavily Search",
                                listing.Url);
                        }
                    }

                    if (!hasRawText)
                    {
                        // ── Fallback 2: use Tavily Search to get a snippet about the listing ──
                        string fallbackSnippet = "";
                        try
                        {
                            var fallbackResults = await ExecuteTavilySearch($"site:njuskalo.hr {listing.Url}", 1);
                            if (fallbackResults.Count > 0)
                            {
                                fallbackSnippet = fallbackResults[0].Content ?? "";
                                if (fallbackSnippet.Length > 50)
                                {
                                    _logger.LogInformation("DeepDive: SEARCH FALLBACK enriched {Url} ({Len} chars)",
                                        listing.Url, fallbackSnippet.Length);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "DeepDive: search fallback failed for {Url}", listing.Url);
                        }

                        // ── Structured extraction from fallback + URL (even on cookie wall) ──
                        var fallbackStructured = new List<string>();
                        string? fbDetectedLandType = null;

                        if (!string.IsNullOrEmpty(fallbackSnippet))
                        {
                            var fbLandType = Regex.Match(fallbackSnippet,
                                @"Tip\s+zemlji[šs]ta[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
                            if (fbLandType.Success)
                            {
                                var ltVal = fbLandType.Groups[1].Value.Trim();
                                fallbackStructured.Add($"Tip zemljišta: {ltVal}");
                                var ltLower = ltVal.ToLowerInvariant();
                                if (ltLower.Contains("poljoprivredno") || ltLower.Contains("oranica") ||
                                    ltLower.Contains("šuma") || ltLower.Contains("pašnjak") || ltLower.Contains("vinograd"))
                                    fbDetectedLandType = "poljoprivredno zemljište";
                                else if (ltLower.Contains("građevinsko") || ltLower.Contains("gradevinsko"))
                                    fbDetectedLandType = "građevinsko zemljište";
                            }
                        }

                        if (fbDetectedLandType == null)
                        {
                            var textOnly = $"{listing.Title} {listing.Content} {fallbackSnippet}".ToLowerInvariant();
                            if (textOnly.Contains("poljoprivredno") || textOnly.Contains("oranica") ||
                                textOnly.Contains("pašnjak") || textOnly.Contains("vinograd"))
                                fbDetectedLandType = "poljoprivredno zemljište";
                            else if (textOnly.Contains("građevinsko") || textOnly.Contains("gradevinsko"))
                                fbDetectedLandType = "građevinsko zemljište";
                        }

                        if (fbDetectedLandType == null)
                        {
                            var urlLower = listing.Url.ToLowerInvariant();
                            if (urlLower.Contains("poljoprivredno"))
                                fbDetectedLandType = "poljoprivredno zemljište";
                            else if (urlLower.Contains("građevinsko") || urlLower.Contains("gradevinsko"))
                                fbDetectedLandType = "građevinsko zemljište";
                        }

                        if (fbDetectedLandType != null)
                            fallbackStructured.Add($"DetectedLandType: {fbDetectedLandType}");

                        if (!string.IsNullOrEmpty(fallbackSnippet))
                        {
                            var fbLokacija = Regex.Match(fallbackSnippet, @"Lokacija[:\s]+([^\n\r]{5,80})", RegexOptions.IgnoreCase);
                            if (fbLokacija.Success) fallbackStructured.Add($"Lokacija: {fbLokacija.Groups[1].Value.Trim()}");

                            var fbPovrsina = Regex.Match(fallbackSnippet, @"(?:Površina|Stambena površina|Korisna površina)[:\s]+([^\n\r]{3,30})", RegexOptions.IgnoreCase);
                            if (fbPovrsina.Success) fallbackStructured.Add($"Površina: {fbPovrsina.Groups[1].Value.Trim()}");

                            var fbSobe = Regex.Match(fallbackSnippet, @"(?:Broj\s+soba|Sobe)[:\s]+([^\n\r,]{1,15})", RegexOptions.IgnoreCase);
                            if (fbSobe.Success) fallbackStructured.Add($"Broj soba: {fbSobe.Groups[1].Value.Trim()}");

                            var fbStanje = Regex.Match(fallbackSnippet, @"Stanje[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
                            if (fbStanje.Success) fallbackStructured.Add($"Stanje: {fbStanje.Groups[1].Value.Trim()}");

                            var fbGodina = Regex.Match(fallbackSnippet, @"Godina\s+(?:izgradnje|gradnje)[:\s]+(\d{4})", RegexOptions.IgnoreCase);
                            if (fbGodina.Success) fallbackStructured.Add($"Godina izgradnje: {fbGodina.Groups[1].Value}");

                            var fbKat = Regex.Match(fallbackSnippet, @"(?:Kat|Eta[žz]a)[:\s]+([^\n\r,]{2,20})", RegexOptions.IgnoreCase);
                            if (fbKat.Success) fallbackStructured.Add($"Kat: {fbKat.Groups[1].Value.Trim()}");
                        }

                        string fbPrefix = fallbackStructured.Count > 0
                            ? $"[STRUCTURED] {string.Join(" | ", fallbackStructured)} "
                            : "";
                        if (fallbackStructured.Count > 0)
                            _logger.LogInformation("DeepDive: fallback structured data for {Url}: {Data}",
                                listing.Url, string.Join(" | ", fallbackStructured));

                        listing.Content = $"{fbPrefix}[SEARCH-FALLBACK] {fallbackSnippet} [ORIGINAL] {listing.Content}";
                        if (fallbackStructured.Count > 0 || fallbackSnippet.Length > 50)
                            listing.DeepDiveStatus = $"⚠ Cookie wall → Tavily Search fallback ({fallbackSnippet.Length} znakova, {fallbackStructured.Count} polja)";
                        else
                            listing.DeepDiveStatus = "✗ Cookie wall → svi fallbackovi neuspješni — samo osnovni snippet";
                        return false;
                    }
                }
                else
                {
                    // ── Tavily Extract succeeded (no cookie wall) ──
                    listing.DeepDiveStatus = $"✓ Tavily Extract OK ({rawText.Length:N0} znakova)";
                    hasRawText = true;
                }
            }

            // ── Process the raw text (from BrightData or Tavily Extract) ──
            var descLower = rawText.ToLowerInvariant();

            // ── DISCARD: "vanknjižno vlasništvo" — unreliable property rights ──
            if (descLower.Contains("vanknjižno vlasništvo") || descLower.Contains("vanknjižno"))
            {
                _logger.LogInformation("DeepDive: DISCARDING {Url} — vanknjižno vlasništvo", listing.Url);
                listing.DeepDiveStatus = "✗ Odbačeno — vanknjižno vlasništvo";
                toDiscard.Add(listing);
                return false;
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

            // ── Extract structured data from FULL raw text BEFORE truncation ──
            var structuredInfo = new List<string>();

            var lokacijaMatch = Regex.Match(rawText, @"Lokacija[:\s]+([^\n\r]{5,80})", RegexOptions.IgnoreCase);
            if (lokacijaMatch.Success) structuredInfo.Add($"Lokacija: {lokacijaMatch.Groups[1].Value.Trim()}");

            var povrsinaMatch = Regex.Match(rawText, @"(?:Površina|Stambena površina|Korisna površina|Ukupna površina)[:\s]+([^\n\r]{3,30})", RegexOptions.IgnoreCase);
            if (povrsinaMatch.Success) structuredInfo.Add($"Površina: {povrsinaMatch.Groups[1].Value.Trim()}");

            var stanjeMatch = Regex.Match(rawText, @"Stanje[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
            if (stanjeMatch.Success) structuredInfo.Add($"Stanje: {stanjeMatch.Groups[1].Value.Trim()}");

            var namjenaMatch = Regex.Match(rawText, @"Namjena[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
            if (namjenaMatch.Success) structuredInfo.Add($"Namjena: {namjenaMatch.Groups[1].Value.Trim()}");

            var godinaMatch = Regex.Match(rawText, @"Godina\s+(?:izgradnje|gradnje)[:\s]+(\d{4})", RegexOptions.IgnoreCase);
            if (godinaMatch.Success) structuredInfo.Add($"Godina izgradnje: {godinaMatch.Groups[1].Value}");

            var energyMatch = Regex.Match(rawText, @"Energetski\s+(?:razred|certifikat)[:\s]+([A-G][+]?)", RegexOptions.IgnoreCase);
            if (energyMatch.Success) structuredInfo.Add($"Energetski razred: {energyMatch.Groups[1].Value}");

            var katMatch = Regex.Match(rawText, @"(?:Kat|Eta[žz]a)[:\s]+([^\n\r,]{2,20})", RegexOptions.IgnoreCase);
            if (katMatch.Success) structuredInfo.Add($"Kat: {katMatch.Groups[1].Value.Trim()}");

            var sobeMatch = Regex.Match(rawText, @"(?:Broj\s+soba|Sobe)[:\s]+([^\n\r,]{1,15})", RegexOptions.IgnoreCase);
            if (sobeMatch.Success) structuredInfo.Add($"Broj soba: {sobeMatch.Groups[1].Value.Trim()}");

            var grijanjeMatch = Regex.Match(rawText, @"Grijanje[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
            if (grijanjeMatch.Success) structuredInfo.Add($"Grijanje: {grijanjeMatch.Groups[1].Value.Trim()}");

            var parkingMatch = Regex.Match(rawText, @"(?:Parking|Gara[žz]a|Garažno mjesto)[:\s]+([^\n\r,]{2,30})", RegexOptions.IgnoreCase);
            if (parkingMatch.Success) structuredInfo.Add($"Parking: {parkingMatch.Groups[1].Value.Trim()}");

            var okucnicaMatch = Regex.Match(rawText, @"(?:Okućnica|Dvorišt|Površina\s+(?:parcele|zemljišta|okućnice))[:\s]+([^\n\r,]{3,30})", RegexOptions.IgnoreCase);
            if (okucnicaMatch.Success) structuredInfo.Add($"Okućnica: {okucnicaMatch.Groups[1].Value.Trim()}");

            var katoviMatch = Regex.Match(rawText, @"Broj\s+katova[:\s]+([^\n\r,]{1,10})", RegexOptions.IgnoreCase);
            if (katoviMatch.Success) structuredInfo.Add($"Broj katova: {katoviMatch.Groups[1].Value.Trim()}");

            // ── LAND-SPECIFIC FIELDS ──
            var landTypeMatch = Regex.Match(rawText, @"Tip\s+zemlji[šs]ta[:\s]+([^\n\r,]{3,40})", RegexOptions.IgnoreCase);
            if (landTypeMatch.Success) structuredInfo.Add($"Tip zemljišta: {landTypeMatch.Groups[1].Value.Trim()}");

            string? deepDetectedLandType = null;
            var tipZemljistaEntry = structuredInfo.FirstOrDefault(s => s.StartsWith("Tip zemljišta:"));
            if (tipZemljistaEntry != null)
            {
                var tzLower = tipZemljistaEntry.ToLowerInvariant();
                if (tzLower.Contains("poljoprivredno") || tzLower.Contains("oranica") ||
                    tzLower.Contains("šuma") || tzLower.Contains("pašnjak") || tzLower.Contains("vinograd"))
                    deepDetectedLandType = "poljoprivredno zemljište";
                else if (tzLower.Contains("građevinsko") || tzLower.Contains("gradevinsko"))
                    deepDetectedLandType = "građevinsko zemljište";
            }

            if (deepDetectedLandType == null)
            {
                if (descLower.Contains("poljoprivredno zemljište") || descLower.Contains("poljoprivredno zemljiste") ||
                    descLower.Contains("poljoprivredno zeml"))
                    deepDetectedLandType = "poljoprivredno zemljište";
                else if (descLower.Contains("građevinsko zemljište") || descLower.Contains("gradevinsko zemljiste") ||
                         descLower.Contains("građevinsko zeml") || descLower.Contains("gradevinsko zeml"))
                    deepDetectedLandType = "građevinsko zemljište";
            }

            if (deepDetectedLandType != null)
                structuredInfo.Add($"DetectedLandType: {deepDetectedLandType}");

            var opisMatch = Regex.Match(rawText, @"Opis\s+oglasa[:\s]*\n?([\s\S]{10,500}?)(?:\n\n|\r\n\r\n|$)", RegexOptions.IgnoreCase);
            if (opisMatch.Success) structuredInfo.Add($"Opis: {opisMatch.Groups[1].Value.Trim()}");

            string structuredPrefix = structuredInfo.Count > 0
                ? $"[STRUCTURED] {string.Join(" | ", structuredInfo)} "
                : "";

            if (structuredInfo.Count > 0)
                _logger.LogInformation("DeepDive: structured data for {Url}: {Data}",
                    listing.Url, string.Join(" | ", structuredInfo));

            // ── V24: Extract definitive price from deep dive structured data ──
            // The deep dive page has the actual listing price — use it to correct initial extraction.
            // Look for "Cijena:" key in the structured data first
            var cijenaEntry = structuredInfo.FirstOrDefault(s =>
                s.StartsWith("Cijena:", StringComparison.OrdinalIgnoreCase) &&
                !s.Contains("/m", StringComparison.OrdinalIgnoreCase));
            if (cijenaEntry != null)
            {
                var ddPriceMatch = Regex.Match(cijenaEntry, @"(\d{1,3}(?:[\.,]\d{3})*(?:,\d+)?)\s*€", RegexOptions.IgnoreCase);
                if (ddPriceMatch.Success)
                {
                    double ddPrice = ValuationEngine.ParseCroatianNumber(ddPriceMatch.Groups[1].Value);
                    if (ddPrice > 1000) // Sanity: real listing prices > €1000
                    {
                        listing.ListedPriceEur = ddPrice;
                        _logger.LogInformation("DeepDive: extracted ListedPriceEur={Price:N0} from structured data for {Url}",
                            ddPrice, listing.Url);
                    }
                }
            }

            // Also check for price/m² in structured data
            var cijenaPm2Entry = structuredInfo.FirstOrDefault(s =>
                s.Contains("€/m", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Cijena po m", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Cijena/m", StringComparison.OrdinalIgnoreCase));
            if (cijenaPm2Entry != null)
            {
                var ddPpm2Match = Regex.Match(cijenaPm2Entry, @"(\d{1,3}(?:[\.,]\d{3})*(?:,\d+)?)\s*€", RegexOptions.IgnoreCase);
                if (ddPpm2Match.Success)
                {
                    double ddPpm2 = ValuationEngine.ParseCroatianNumber(ddPpm2Match.Groups[1].Value);
                    if (ddPpm2 >= 10 && ddPpm2 <= 30000) // Sanity for €/m²
                    {
                        listing.ListedPricePerM2 = ddPpm2;
                        _logger.LogInformation("DeepDive: extracted ListedPricePerM2={Ppm2:N0} from structured data for {Url}",
                            ddPpm2, listing.Url);
                    }
                }
            }

            // If no price found in structured data, try targeted regex on raw text
            if (listing.ListedPriceEur == null || listing.ListedPriceEur <= 0)
            {
                // Look for "Cijena: X €" or "Ukupna cijena: X €" in raw text (NOT €/m²)
                var rawPriceMatch = Regex.Match(rawText,
                    @"(?:Cijena|Ukupna cijena|Tražena cijena)[:\s]+(\d{1,3}(?:[\.,]\d{3})*(?:,\d+)?)\s*€(?!\s*/\s*m)",
                    RegexOptions.IgnoreCase);
                if (rawPriceMatch.Success)
                {
                    double rawPrice = ValuationEngine.ParseCroatianNumber(rawPriceMatch.Groups[1].Value);
                    if (rawPrice > 1000)
                    {
                        listing.ListedPriceEur = rawPrice;
                        _logger.LogInformation("DeepDive: extracted ListedPriceEur={Price:N0} from raw text for {Url}",
                            rawPrice, listing.Url);
                    }
                }
            }

            if (listing.ListedPricePerM2 == null || listing.ListedPricePerM2 <= 0)
            {
                var rawPpm2Match = Regex.Match(rawText,
                    @"(?:Cijena\s*(?:po\s*)?(?:/\s*)?m[²2]|€/m[²2])[:\s]*(\d{1,3}(?:[\.,]\d{3})*(?:,\d+)?)",
                    RegexOptions.IgnoreCase);
                if (rawPpm2Match.Success)
                {
                    double rawPpm2 = ValuationEngine.ParseCroatianNumber(rawPpm2Match.Groups[1].Value);
                    if (rawPpm2 >= 10 && rawPpm2 <= 30000)
                    {
                        listing.ListedPricePerM2 = rawPpm2;
                        _logger.LogInformation("DeepDive: extracted ListedPricePerM2={Ppm2:N0} from raw text for {Url}",
                            rawPpm2, listing.Url);
                    }
                }
            }

            var enrichedText = rawText.Length > 3000 ? rawText[..3000] : rawText;
            listing.Content = $"{structuredPrefix}[DEEP DIVE] {enrichedText} [ORIGINAL] {listing.Content}";

            // ── Extract renovation year from raw text ──
            int? yearRenovation = null;
            var renovYearMatch = Regex.Match(rawText,
                @"(?:renovir|adaptir|obnovljen|zadnja?\s*(?:renovacija|adaptacija))[^\d]{0,30}(\d{4})",
                RegexOptions.IgnoreCase);
            if (renovYearMatch.Success && int.TryParse(renovYearMatch.Groups[1].Value, out var ry) && ry >= 1990 && ry <= 2030)
                yearRenovation = ry;

            if (!yearRenovation.HasValue)
            {
                var yearKvMatch = Regex.Match(rawText, @"(?:godina\s+(?:zadnje\s+)?renovacije|godina\s+adaptacije)[:\s]+(\d{4})", RegexOptions.IgnoreCase);
                if (yearKvMatch.Success && int.TryParse(yearKvMatch.Groups[1].Value, out var ry2) && ry2 >= 1990 && ry2 <= 2030)
                    yearRenovation = ry2;
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
            else if (yearRenovation.HasValue && yearRenovation >= 2020)
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
            else if (yearRenovation.HasValue && yearRenovation >= 2015)
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
                listing.IsRenovated ??= false;
                listing.NeedsRenovation ??= false;
            }

            if (yearRenovation.HasValue)
                listing.YearLastRenovation = yearRenovation;

            // ── Extract yard area ──
            var yardMatch = Regex.Match(rawText,
                @"(?:okućnica|dvorišt|zemljišt|parcela)[^\d]{0,20}(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m",
                RegexOptions.IgnoreCase);
            if (yardMatch.Success)
            {
                double yardArea = ValuationEngine.ParseCroatianNumber(yardMatch.Groups[1].Value);
                if (yardArea > 0) listing.AiYardAreaM2 ??= yardArea;
            }

            // ── Extract size ──
            var sizeMatch = Regex.Match(rawText,
                @"(?:površina|stambena|korisna)[^\d]{0,20}(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m",
                RegexOptions.IgnoreCase);
            if (sizeMatch.Success)
            {
                double sizeVal = ValuationEngine.ParseCroatianNumber(sizeMatch.Groups[1].Value);
                if (sizeVal > 0) listing.AiSizeM2 ??= sizeVal;
            }

            // ── Equipment-based feature detection ──
            if (descLower.Contains("garaž") || descLower.Contains("garaz") || descLower.Contains("parking"))
                listing.HasGarage ??= true;

            if (descLower.Contains("bazen") || descLower.Contains("pool"))
                listing.Content += " [Oprema: Bazen]";

            if (descLower.Contains("plinsko grijanje") || descLower.Contains("centralno grijanje"))
            {
                var heatingMatch = Regex.Match(rawText, @"(?:grijanje)[:\s]+([^\n,.]{3,30})", RegexOptions.IgnoreCase);
                listing.Content += $" [Grijanje: {(heatingMatch.Success ? heatingMatch.Groups[1].Value.Trim() : "plinsko")}]";
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeepDive: failed for individual listing {Url}", listing.Url);
            listing.DeepDiveStatus ??= $"✗ Iznimka: {ex.Message[..Math.Min(ex.Message.Length, 80)]}";
            return false;
        }
    }

    /// <summary>
    /// V19: Pre-AI enrichment from listing titles and snippets.
    /// Many Njuškalo titles contain structured info like "Kuća: Daruvar, 2 stana, garaža, dvorište, 127 m²"
    /// For listings that didn't get full deep-dive data, we extract what we can from title+content
    /// and prepend a [TITLE-ENRICHED] prefix so the AI has more to work with.
    /// </summary>
    private void EnrichFromTitleAndSnippet(List<ComparableSearchResult> listings)
    {
        int enriched = 0;
        foreach (var listing in listings)
        {
            // Skip listings that already have [STRUCTURED] or [DEEP DIVE] data
            if (listing.Content.Contains("[STRUCTURED]") || listing.Content.Contains("[DEEP DIVE]"))
                continue;

            var titleLower = listing.Title.ToLowerInvariant();
            var combinedText = $"{listing.Title} {listing.Content}";
            var combinedLower = combinedText.ToLowerInvariant();
            var titleExtracted = new List<string>();

            // ── Size from title: "127 m²" or "(120 m²)" ──
            var titleSizeMatch = Regex.Match(combinedText,
                @"(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m[²2]",
                RegexOptions.IgnoreCase);
            if (titleSizeMatch.Success)
            {
                double sz = ValuationEngine.ParseCroatianNumber(titleSizeMatch.Groups[1].Value);
                if (sz > 0)
                    titleExtracted.Add($"Površina: {titleSizeMatch.Groups[1].Value} m²");
            }

            // ── Rooms from title: "2 stana", "3 sobe", "4-sobni" ──
            var roomsMatch = Regex.Match(combinedText,
                @"(\d+)\s*(?:sob[aei]|stan[ao]?|room|soba\b)", RegexOptions.IgnoreCase);
            if (roomsMatch.Success)
                titleExtracted.Add($"Broj soba: {roomsMatch.Groups[1].Value}");

            // ── Garage from title ──
            if (combinedLower.Contains("garaž") || combinedLower.Contains("garaz"))
            {
                titleExtracted.Add("Parking: Da (garaža)");
                listing.HasGarage ??= true;
            }

            // ── Yard / dvorište from title ──
            if (combinedLower.Contains("dvori") || combinedLower.Contains("dvoris") ||
                combinedLower.Contains("okućnic") || combinedLower.Contains("okucnic") ||
                combinedLower.Contains("dvor,") || combinedLower.Contains("dvor!") || combinedLower.Contains("dvor "))
            {
                titleExtracted.Add("Okućnica: Da");
                listing.HasYard ??= true;
            }

            // ── Location from title: "Kuća: Daruvar" or after comma ──
            // Njuškalo titles often have format "Type: City, features"
            var locMatch = Regex.Match(listing.Title,
                @"(?:Kuća|Stan|Zemlji[šs]te|Apartman|Poslovni\s+prostor)[:\s]+([A-ZČĆŽŠĐ][a-zčćžšđ]+(?:\s+[A-ZČĆŽŠĐ][a-zčćžšđ]+)*)",
                RegexOptions.None);
            if (locMatch.Success)
                titleExtracted.Add($"Lokacija: {locMatch.Groups[1].Value.Trim()}");

            // ── Property type indicators ──
            if (titleLower.Contains("kuća") || titleLower.Contains("kuca"))
                titleExtracted.Add("Tip: kuća");
            else if (titleLower.Contains("stan") && !titleLower.Contains("stana"))
                titleExtracted.Add("Tip: stan");
            else if (titleLower.Contains("zemlji"))
                titleExtracted.Add("Tip: zemljište");

            // ── Year from title: "(1985)" or "god. 2010" ──
            var yearMatch = Regex.Match(combinedText,
                @"(?:god(?:ina)?\.?\s*|izgrađen[ao]?\s+)(\d{4})\b");
            if (!yearMatch.Success)
                yearMatch = Regex.Match(listing.Title, @"\((\d{4})\)");
            if (yearMatch.Success)
            {
                int yr = int.Parse(yearMatch.Groups[1].Value);
                if (yr >= 1900 && yr <= 2030)
                    titleExtracted.Add($"Godina izgradnje: {yr}");
            }

            // ── Hectares to m² ──
            var hectareMatch = Regex.Match(combinedText,
                @"(\d+(?:[,.]\d+)?)\s*hektar", RegexOptions.IgnoreCase);
            if (hectareMatch.Success)
            {
                double ha = ValuationEngine.ParseCroatianNumber(hectareMatch.Groups[1].Value);
                if (ha > 0)
                    titleExtracted.Add($"Površina: {(int)(ha * 10000)} m²");
            }

            // ── Condition hints from title ──
            if (combinedLower.Contains("novogradnja") || combinedLower.Contains("novo izgrađ"))
                titleExtracted.Add("Stanje: novogradnja");
            else if (combinedLower.Contains("renoviran") || combinedLower.Contains("adaptiran") || combinedLower.Contains("uređen"))
                titleExtracted.Add("Stanje: renovirano");
            else if (combinedLower.Contains("za adaptac") || combinedLower.Contains("za rušenj") || combinedLower.Contains("zapušten"))
                titleExtracted.Add("Stanje: potrebna adaptacija");

            // ── Prepend extracted data to content ──
            if (titleExtracted.Count > 0)
            {
                var prefix = $"[TITLE-ENRICHED] {string.Join(" | ", titleExtracted)} ";
                listing.Content = $"{prefix}{listing.Content}";
                enriched++;
            }
        }

        if (enriched > 0)
            _logger.LogInformation("V19 TitleEnrich: enriched {Count}/{Total} listings from title/snippet",
                enriched, listings.Count);
    }

    /// <summary>
    /// Build the Njuškalo category URL from property type + location.
    /// e.g. "građevinsko zemljište" + "Vir" → "https://www.njuskalo.hr/prodaja-zemljista/vir"
    ///      "stan" + "Srebrnjak" (→Zagreb) → "https://www.njuskalo.hr/prodaja-stanova/zagreb-srebrnjak"
    /// IMPORTANT: Uses category URLs (/prodaja-stanova/...) NOT /search/?keywords=
    /// because /search/ is JavaScript-rendered and Tavily Extract cannot scrape it.
    /// Category pages are server-side rendered and reliably return listing blocks.
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
        // NOTE: VillageFallback is NOT used here anymore.
        // BuildNjuskaloSearchUrl always tries the original regionName first.
        // FetchNjuskaloDirectListings handles the fallback to the parent city
        // only when the primary URL returns empty results.

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

        return queryParams.Count > 0
            ? $"{baseUrl}?{string.Join("&", queryParams)}"
            : baseUrl;
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
    /// Scrape a URL using Bright Data's Scraping Browser via Playwright CDP.
    /// Connects to Bright Data's remote headless Chrome which renders JavaScript,
    /// handles captchas, and bypasses anti-bot protection (ShieldSquare etc.).
    /// Returns the page text content, or empty string on failure.
    /// Docs: https://docs.brightdata.com/scraping-automation/scraping-browser/introduction
    /// </summary>
    private async Task<string> BrightDataScrape(string url)
    {
        if (string.IsNullOrEmpty(_brightDataWsEndpoint))
            return "";

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.ConnectOverCDPAsync(_brightDataWsEndpoint);

            var context = browser.Contexts.Count > 0
                ? browser.Contexts[0]
                : await browser.NewContextAsync();

            var page = await context.NewPageAsync();

            // Navigate to the URL first (AddCookiesAsync is FORBIDDEN by Bright Data)
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            // Wait for initial load
            await page.WaitForTimeoutAsync(2000);

            // ── Inject consent cookies via JavaScript (bypasses Bright Data CDP restriction) ──
            await page.EvaluateAsync(@"
                document.cookie = 'didomi_token=eyJ1c2VyX2lkIjoiIiwiY3JlYXRlZCI6IjIwMjQtMDEtMDFUMDA6MDA6MDAuMDAwWiIsInVwZGF0ZWQiOiIyMDI0LTAxLTAxVDAwOjAwOjAwLjAwMFoiLCJ2ZW5kb3JzIjp7ImVuYWJsZWQiOlsiZ29vZ2xlIl19LCJwdXJwb3NlcyI6eyJlbmFibGVkIjpbImdlb2xvY2F0aW9uX2RhdGEiLCJkZXZpY2VfY2hhcmFjdGVyaXN0aWNzIl19LCJ2ZXJzaW9uIjoyfQ==; domain=.njuskalo.hr; path=/; max-age=31536000';
                document.cookie = 'euconsent-v2=CQIRuQAQIRuQAAHABBENBOFgAAAAAAAAAChQAAAAAAFBIIQACAAFwAUABUADgAIAAeABIAC4AMAAZABDACSAFAALIAXQAvgBhADIAG0ANgAfgBEACLAEiAJcAUYArgBaADEAGiANoAdIA8AB6gD7ATCAmoE1AAA.flgAAAAAAAAA; domain=.njuskalo.hr; path=/; max-age=31536000';
                document.cookie = 'njuskalo_privacy_policy=accepted; domain=.njuskalo.hr; path=/; max-age=31536000';
            ");

            // Reload page — now cookies are set, cookie wall should be gone
            await page.ReloadAsync(new PageReloadOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });

            // Wait for content to render
            await page.WaitForTimeoutAsync(3000);

            // ── If cookie wall still showing, try clicking consent ──
            try
            {
                // Try multiple possible consent button selectors
                var consentSelectors = new[]
                {
                    "button:has-text('Slažem se')",
                    "button:has-text('Prihvaćam')",
                    "button:has-text('Prihvati')",
                    "button:has-text('Accept')",
                    "button:has-text('Agree')",
                    "#didomi-notice-agree-button",
                    ".didomi-continue-without-agreeing",
                    "[id*='consent'] button",
                    "[class*='consent'] button",
                    "[id*='cookie'] button",
                    "[class*='cookie-consent'] button"
                };

                foreach (var selector in consentSelectors)
                {
                    try
                    {
                        var btn = page.Locator(selector).First;
                        var visible = await btn.IsVisibleAsync();
                        if (visible)
                        {
                            await btn.ClickAsync();
                            _logger.LogInformation("BrightData: clicked consent button '{Selector}' for {Url}", selector, url);
                            await page.WaitForTimeoutAsync(3000);
                            break;
                        }
                    }
                    catch { /* selector not found — try next */ }
                }
            }
            catch (Exception)
            {
                // No consent dialog found — that's fine, continue
            }

            // Get the full rendered HTML after potential consent click
            var html = await page.ContentAsync();

            await page.CloseAsync();

            if (string.IsNullOrWhiteSpace(html) || html.Length < 500)
                return "";

            // ── Check for captcha/block ──
            var htmlLower = html.ToLowerInvariant();
            bool hasCookieText = htmlLower.Contains("koristimo kolačiće") || htmlLower.Contains("ispričavam se");
            bool hasRealContent = htmlLower.Contains("opis oglasa") || htmlLower.Contains("cijena") ||
                                  htmlLower.Contains("površina") || htmlLower.Contains("lokacija") ||
                                  htmlLower.Contains("tip zemljišta") || htmlLower.Contains("broj soba") ||
                                  htmlLower.Contains("m²") || htmlLower.Contains("€") ||
                                  html.Length > 50000; // Real Njuškalo pages are 200K+

            // If we have real content, the page loaded fine — don't block it
            // (Njuškalo embeds "shieldsquare" JS references even on properly loaded pages)
            if (hasRealContent)
            {
                _logger.LogInformation("BrightData: OK for {Url} — {Len} chars, hasRealContent=true", url, html.Length);
            }
            else if (hasCookieText || htmlLower.Contains("shieldsquare captcha") || htmlLower.Contains("robot"))
            {
                _logger.LogInformation("BrightData: still blocked for {Url} (html={Len} chars, hasCookie={Cookie}, hasContent={Content})",
                    url, html.Length, hasCookieText, hasRealContent);
                return "";
            }

            // ── Extract text from HTML ──
            var cleaned = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<style[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<nav[^>]*>[\s\S]*?</nav>", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<footer[^>]*>[\s\S]*?</footer>", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<header[^>]*>[\s\S]*?</header>", " ", RegexOptions.IgnoreCase);

            // Extract structured data from meta tags
            var structuredParts = new List<string>();

            var ogTitle = Regex.Match(html, @"<meta\s+property=""og:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (ogTitle.Success) structuredParts.Add($"Naslov: {ogTitle.Groups[1].Value}");

            var ogDesc = Regex.Match(html, @"<meta\s+property=""og:description""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (ogDesc.Success) structuredParts.Add($"Opis: {ogDesc.Groups[1].Value}");

            // Njuškalo structured detail pairs (th/td, dt/dd)
            var kvPairs = Regex.Matches(cleaned,
                @"(?:<th[^>]*>|<dt[^>]*>)\s*([^<]{2,40})\s*(?:</th>|</dt>)\s*(?:<td[^>]*>|<dd[^>]*>)\s*([^<]{1,80})\s*(?:</td>|</dd>)",
                RegexOptions.IgnoreCase);
            foreach (Match kv in kvPairs)
            {
                var key = kv.Groups[1].Value.Trim();
                var val = kv.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                    structuredParts.Add($"{key}: {val}");
            }

            // Njuškalo ClassifiedDetailBasicDetails pattern
            var detailPairs = Regex.Matches(html,
                @"ClassifiedDetailBasicDetails-label[^>]*>([^<]+)<.*?ClassifiedDetailBasicDetails-value[^>]*>([^<]+)<",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match dp in detailPairs)
            {
                var key = dp.Groups[1].Value.Trim();
                var val = dp.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                    structuredParts.Add($"{key}: {val}");
            }

            // Strip HTML → plain text
            var plainText = Regex.Replace(cleaned, @"<[^>]+>", " ");
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();
            plainText = System.Net.WebUtility.HtmlDecode(plainText);

            var structuredHeader = structuredParts.Count > 0
                ? string.Join(" | ", structuredParts) + "\n\n"
                : "";

            var result = structuredHeader + plainText;

            _logger.LogInformation("BrightData: OK for {Url} — {Len} chars, {KvCount} structured pairs",
                url, result.Length, structuredParts.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BrightData: failed for {Url}", url);
            return "";
        }
        finally
        {
            if (browser != null)
            {
                try { await browser.CloseAsync(); } catch { /* ignore */ }
            }
            if (playwright != null)
            {
                playwright.Dispose();
            }
        }
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

            // Extract price: number + € (V24: negative lookahead excludes €/m²)
            var priceMatch = NjuskaloPricePattern.Match(block);
            string priceStr = priceMatch.Success ? priceMatch.Groups["price"].Value : "";

            // V24: Also extract price per m² separately
            var ppm2Match = NjuskaloPricePerM2Pattern.Match(block);
            string ppm2Str = ppm2Match.Success ? ppm2Match.Groups["price"].Value : "";

            // V24: Parse numeric values for ListedPrice fields
            double? listedPriceEur = null;
            double? listedPricePerM2 = null;

            if (!string.IsNullOrEmpty(priceStr))
            {
                double parsed = ValuationEngine.ParseCroatianNumber(priceStr);
                if (parsed > 0) listedPriceEur = parsed;
            }
            if (!string.IsNullOrEmpty(ppm2Str))
            {
                double parsedPpm2 = ValuationEngine.ParseCroatianNumber(ppm2Str);
                if (parsedPpm2 > 0) listedPricePerM2 = parsedPpm2;
            }

            // V24: If only €/m² found and no total price, try to infer total from size × €/m²
            if (listedPriceEur == null && listedPricePerM2 != null)
            {
                var sizeInBlock = Regex.Match(block, @"(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m[²2]", RegexOptions.IgnoreCase);
                if (sizeInBlock.Success)
                {
                    double blockSize = ValuationEngine.ParseCroatianNumber(sizeInBlock.Groups[1].Value);
                    if (blockSize > 5 && blockSize < 50000)
                        listedPriceEur = Math.Round(listedPricePerM2.Value * blockSize, 0);
                }
            }

            // Build content string with all available data
            var contentParts = new List<string> { title };

            // ── Detect property type from block text ──
            // Njuškalo search results pages include category labels in or near each listing block.
            // This enables the Type Gate to cross-exclude (e.g. reject kuća when searching stan).
            var blockLower = block.ToLowerInvariant();
            var urlLower = url.ToLowerInvariant();

            // Land subtypes
            if (blockLower.Contains("građevinsko") || blockLower.Contains("gradjevinsko") || blockLower.Contains("gradevinsko"))
                contentParts.Add("Tip: građevinsko zemljište");
            else if (blockLower.Contains("poljoprivredno") || blockLower.Contains("poljopriv"))
                contentParts.Add("Tip: poljoprivredno zemljište");
            else if (blockLower.Contains("oranica") || blockLower.Contains("pašnjak") || blockLower.Contains("livada") || blockLower.Contains("šuma"))
                contentParts.Add("Tip: poljoprivredno zemljište");
            // Apartments
            else if (urlLower.Contains("prodaja-stanova") || blockLower.Contains("garsonijera") ||
                     blockLower.Contains("jednosoban") || blockLower.Contains("dvosoban") || blockLower.Contains("trosoban") ||
                     blockLower.Contains("apartman"))
                contentParts.Add("Tip: stan");
            // Houses
            else if (urlLower.Contains("prodaja-kuca") || blockLower.Contains("obiteljska kuća") ||
                     blockLower.Contains("dvojna kuća") || blockLower.Contains("vikendica") ||
                     blockLower.Contains("vila "))
                contentParts.Add("Tip: kuća");
            // Commercial
            else if (urlLower.Contains("prodaja-poslovnih") || blockLower.Contains("poslovni prostor") ||
                     blockLower.Contains("uredski") || blockLower.Contains("lokal"))
                contentParts.Add("Tip: poslovni prostor");

            // Extract location if present: "Lokacija: City, Area"
            var locMatch = Regex.Match(block, @"Lokacija:\s*([^\n]+)");
            if (locMatch.Success)
                contentParts.Add($"Lokacija: {locMatch.Groups[1].Value.Trim()}");

            // Extract date if present ("Objavljen:DD.MM.YYYY")
            var dateMatch = Regex.Match(block, @"Objavljen:\s*(\d{2}\.\d{2}\.\d{4})");
            if (dateMatch.Success)
                contentParts.Add($"Objavljen:{dateMatch.Groups[1].Value}");

            if (!string.IsNullOrEmpty(priceStr))
                contentParts.Add($"Cijena: {priceStr} €");
            if (!string.IsNullOrEmpty(ppm2Str))
                contentParts.Add($"Cijena/m²: {ppm2Str} €/m²");

            listings.Add(new ComparableSearchResult
            {
                Url = url,
                Title = title,
                Content = string.Join(". ", contentParts),
                ListedPriceEur = listedPriceEur,
                ListedPricePerM2 = listedPricePerM2,
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
    private const double MinSalePriceEur_Building = 15_000;   // V14: lowered from 35k to avoid data starvation
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
    // V13: HARD EXCLUSION — stan ≠ kuća ≠ zemljište ≠ poslovni. NIKAD ne uspoređivati.
    private static readonly Dictionary<string, string[]> WrongTypeKeywords = new()
    {
        ["građevinsko zemljište"] = ["stan", "apartman", "garsonijera", "soba",
            "kuća", "kuca", "vila", "vikendica", "etaža", "etaza",
            "poslovni prostor", "ured", "lokal",
            "oranica", "pašnjak", "livada", "šuma", "voćnjak", "vinograd",
            "poljoprivredno", "poljopriv"],
        ["poljoprivredno zemljište"] = ["stan", "apartman", "garsonijera", "soba",
            "kuća", "kuca", "vila", "vikendica", "etaža", "etaza",
            "poslovni prostor", "ured", "lokal",
            "građevinsko", "gradjevinsko", "gradevinsko",
            "građevinski", "građevinska", "building land"],
        ["stan"]                  = ["građevinsko zemljište", "poljoprivredno zemljište", "parcela", "oranica",
            "pašnjak", "livada", "šuma",
            "kuća", "kuca", "vila", "vikendica", "obiteljska kuća",
            "dvojna kuća", "poslovni prostor", "ured", "lokal"],
        ["kuća"]                  = ["građevinsko zemljište", "poljoprivredno zemljište", "parcela", "oranica",
            "pašnjak", "livada", "šuma",
            "stan", "apartman", "garsonijera", "jednosoban", "dvosoban", "trosoban",
            "poslovni prostor", "ured", "lokal"],
        ["poslovni prostor"]      = ["parcela", "oranica", "livada",
            "stan", "apartman", "garsonijera",
            "kuća", "kuca", "vila", "vikendica"],
    };

    private static readonly string[] RentalKeywords =
    [
        "najam", "iznajmljivanje", "iznajmi", "rent", "rental",
        "mjesečno", "mjesecno", "monthly", "polog", "deposit",
        "za najam", "u najam", "for rent", "do iznajmljivanja",
        "kratkoročni", "kratkorocni", "dugoročni", "dugorocni",
        "najamnina", "rezije", "režije"
    ];

    // V24: Negative lookahead — ensure we extract TOTAL price, not €/m²
    private static readonly Regex PriceAmountRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)(?!\s*/\s*m[²2])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The currently detected property type — set per search call.</summary>
    private string _currentDetectedType = "stan";

    /// <summary>The subject's region name — set per search call for City Integrity checks.</summary>
    private string _currentSubjectRegion = "";

    /// <summary>V38: The subject's resolved major city — for universal cross-city blocking.</summary>
    private string? _currentSubjectMajorCity;

    /// <summary>Track how many listings were rejected by city integrity. Thread-local per search call.</summary>
    private int _cityIntegrityRejections;
    private readonly List<string> _rejectedCityNames = [];

    private List<ComparableSearchResult> PriceFirstFilter(
        List<ComparableSearchResult> results, string phaseName,
        IReadOnlyList<string>? allowedLocations = null)
    {
        var passed = new List<ComparableSearchResult>();
        var locationsLower = allowedLocations?
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.ToLowerInvariant())
            .ToList();

        // Resolve the subject region for City Integrity check
        string subjectLower = _currentSubjectRegion.ToLowerInvariant();

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

            // Gate 0.5: CITY INTEGRITY — hard block forbidden city cross-contamination
            // If subject is "Crikvenica" and listing mentions "Rijeka", reject immediately.
            if (!string.IsNullOrEmpty(subjectLower))
            {
                bool forbidden = false;
                foreach (var (cityA, cityB) in ForbiddenCityPairs)
                {
                    if (subjectLower.Contains(cityA) &&
                        (combinedLower.Contains(cityB) ||
                         r.Url.Contains(cityB, StringComparison.OrdinalIgnoreCase)))
                    {
                        // But allow if listing ALSO mentions the subject city
                        if (combinedLower.Contains(subjectLower) ||
                            r.Url.Contains(subjectLower, StringComparison.OrdinalIgnoreCase))
                            continue; // Listing mentions both — probably OK

                        _logger.LogDebug(
                            "[{Phase}] REJECTED (City Integrity: subject={Subject}, blocked={Blocked}): {Title}",
                            phaseName, subjectLower, cityB, Truncate(r.Title, 60));
                        forbidden = true;
                        _cityIntegrityRejections++;
                        if (!_rejectedCityNames.Contains(cityB, StringComparer.OrdinalIgnoreCase))
                            _rejectedCityNames.Add(cityB);
                        break;
                    }
                }
                if (forbidden) continue;
            }

            // Gate 0.6: V39 CATEGORY PAGE FILTER — reject Njuškalo search/listing pages
            // URLs like /prodaja-stanova, /prodaja-stanova/osijek, /prodaja-kuca/zagreb
            // are NOT individual listings — they're search result pages that get deep-dived
            // and produce data from random listings on that page.
            {
                var urlLower = r.Url.ToLowerInvariant();
                bool isCategoryPage = urlLower.Contains("njuskalo.hr/") &&
                    !urlLower.Contains("/nekretnine/") &&
                    !urlLower.Contains("oglas-") &&
                    (urlLower.Contains("/prodaja-stanova") ||
                     urlLower.Contains("/prodaja-kuca") ||
                     urlLower.Contains("/prodaja-zemljista") ||
                     urlLower.Contains("/iznajmljivanje") ||
                     urlLower.Contains("/poslovni-prostori") ||
                     // Bare /prodaja-stanova or /prodaja-stanova/city without listing ID
                     Regex.IsMatch(urlLower, @"njuskalo\.hr/prodaja-[a-z]+(/[a-z\-]+)?/?$"));
                if (isCategoryPage)
                {
                    _logger.LogInformation(
                        "[{Phase}] REJECTED (V39 Category Page — not an individual listing): {Url}",
                        phaseName, Truncate(r.Url, 80));
                    continue;
                }
            }

            // Gate 0.7: V38/V39 UNIVERSAL CROSS-CITY BLOCK
            // If the subject is in a known major city, reject any listing that
            // mentions a DIFFERENT major city in its URL. Tavily content snippets
            // often include cross-city navigation text ("Zagreb, Split, Rijeka...")
            // so we ONLY trust the URL for cross-city detection.
            if (_currentSubjectMajorCity != null)
            {
                bool crossCityForbidden = false;
                var urlLowerForCity = r.Url.ToLowerInvariant();
                foreach (var city in MajorCities)
                {
                    if (city == _currentSubjectMajorCity) continue;

                    // V39: Check URL ONLY — content is unreliable (Tavily includes navigation text)
                    bool urlMentionsForeignCity = urlLowerForCity.Contains(city);
                    if (!urlMentionsForeignCity) continue;

                    // V39: If URL mentions a foreign city, reject UNCONDITIONALLY.
                    // Previously we allowed if content also mentioned subject city,
                    // but that let through Njuškalo pages with cross-city navigation.
                    _logger.LogInformation(
                        "[{Phase}] REJECTED (V39 Cross-City URL: subject={Subject}, url-city={Foreign}): {Title}",
                        phaseName, _currentSubjectMajorCity, city, Truncate(r.Title, 60));
                    crossCityForbidden = true;
                    _cityIntegrityRejections++;
                    if (!_rejectedCityNames.Contains(city, StringComparer.OrdinalIgnoreCase))
                        _rejectedCityNames.Add(city);
                    break;
                }
                if (crossCityForbidden) continue;

                // V39: Secondary content check — if URL is clean but TITLE mentions
                // a different major city (e.g., "Split, Mejaši - stan"), also reject.
                // Title is more reliable than Content (which has Tavily snippets).
                var titleLower = r.Title.ToLowerInvariant();
                foreach (var city in MajorCities)
                {
                    if (city == _currentSubjectMajorCity) continue;
                    if (titleLower.Contains(city))
                    {
                        _logger.LogInformation(
                            "[{Phase}] REJECTED (V39 Cross-City Title: subject={Subject}, title-city={Foreign}): {Title}",
                            phaseName, _currentSubjectMajorCity, city, Truncate(r.Title, 60));
                        crossCityForbidden = true;
                        _cityIntegrityRejections++;
                        if (!_rejectedCityNames.Contains(city, StringComparer.OrdinalIgnoreCase))
                            _rejectedCityNames.Add(city);
                        break;
                    }
                }
                if (crossCityForbidden) continue;
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

            // ── GATE 6: TYPE GATE — reject OBVIOUS wrong property types ──
            // Only catches clear mismatches (stan vs kuća, building vs land).
            // Subtle distinctions (građevinsko vs poljoprivredno) are handled by
            // AI classification in ValuationService.FilterByAiPropertyType().
            if (WrongTypeKeywords.TryGetValue(_currentDetectedType, out var wrongKws))
            {
                string titleLower = r.Title.ToLowerInvariant();
                string urlLower = r.Url.ToLowerInvariant();
                // Only check title + URL (strong signals). Content is too noisy for keywords.
                string checkText = $"{titleLower} {urlLower}";
                var matchedWk = wrongKws.FirstOrDefault(wk => checkText.Contains(wk));
                if (matchedWk != null)
                {
                    // Before rejecting: does the title/URL also mention the CORRECT type?
                    var correctKws = GetTypeSynonyms(_currentDetectedType);
                    if (correctKws.Any(ck => checkText.Contains(ck.ToLowerInvariant())))
                    {
                        // e.g. "Poljoprivredno zemljište s kućicom" — keep
                    }
                    else
                    {
                        _logger.LogDebug("[{Phase}] REJECTED (wrong type '{WrongType}' for '{Wanted}'): {Title}",
                            phaseName, matchedWk, _currentDetectedType, Truncate(r.Title, 60));
                        continue;
                    }
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

    /// <summary>Get the next broader region for an island/suburb/village.
    /// V13: SubDistricts are tried FIRST — if Crikvenica has sub-districts,
    /// we return "Crikvenica" itself (the sub-districts are searched separately).
    /// We NEVER jump to a forbidden city (e.g. Rijeka for Crikvenica).</summary>
    private static string GetBroaderRegion(string regionName)
    {
        // V13: If the place has sub-districts, stay local — do NOT jump to a bigger city
        if (SubDistricts.ContainsKey(regionName))
            return regionName;

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

    /// <summary>Check whether listingCity is FORBIDDEN for the given subjectRegion.
    /// Returns true if the listing should be REJECTED.</summary>
    private static bool IsForbiddenCity(string subjectRegion, string listingCity)
    {
        return ForbiddenCityPairs.Contains(
            (subjectRegion.ToLowerInvariant(), listingCity.ToLowerInvariant()));
    }

    /// <summary>Get sub-districts for a city (if defined). Used for local expansion.</summary>
    internal static string[]? GetSubDistricts(string regionName)
    {
        return SubDistricts.TryGetValue(regionName, out var subs) ? subs : null;
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
            // Agricultural keywords → poljoprivredno
            bool isAgricultural = text.Contains("oranica") || text.Contains("pašnjak")
                || text.Contains("pasnjak") || text.Contains("livada")
                || text.Contains("šuma") || text.Contains("suma")
                || text.Contains("vinograd") || text.Contains("voćnjak")
                || text.Contains("vocnjak") || text.Contains("neplodno")
                || text.Contains("poljoprivredno");

            if (isAgricultural)
                return "poljoprivredno zemljište";

            // Construction land keywords → građevinsko
            bool isConstructionLand = text.Contains("građevinsko zemljište")
                || text.Contains("gradjevinsko") || text.Contains("građevinski teren")
                || text.Contains("neizgrađeno") || text.Contains("neizgradjeno")
                || text.Contains("parcela")
                || text.Contains("čest.zem.") || text.Contains("čest. zem.")
                || text.Contains("čestica zemlje");

            if (isConstructionLand)
                return "građevinsko zemljište";

            // Weak land signal (only k.č. etc.) — check if house/flat is more prominent
            bool hasHouseKeyword = text.Contains("kuća") || text.Contains("kuca")
                || text.Contains("katnica") || text.Contains("višekatni") || text.Contains("visekatni")
                || text.Contains("okućnica") || text.Contains("okucnica")
                || text.Contains("vikendica") || text.Contains("stambena zgrada");
            bool hasFlatKeyword = text.Contains("stan ") || text.Contains("apartman");

            if (!hasHouseKeyword && !hasFlatKeyword)
                return "građevinsko zemljište";
        }

        // ── 3. House ──
        bool looksLikeFlat = text.Contains("stan na") || text.Contains("stan u ");
        if (!looksLikeFlat &&
            (text.Contains("kuća") || text.Contains("kuca") ||
             text.Contains("katnica") || text.Contains("višekatni") ||
             text.Contains("visekatni") || text.Contains("prizemna kuća") ||
             text.Contains("vikendica") ||
             text.Contains("okućnica") || text.Contains("okucnica") ||
             text.Contains("stambena površina") ||
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
    public double? AiSimilarityScore { get; set; }  // AI-scored similarity to target (0.0–1.0)
    public string? AiSimilarityReason { get; set; }  // V18: AI-generated one-sentence Croatian reason

    // V24: Explicit listed prices — extracted from source, not regex on mixed content
    public double? ListedPriceEur { get; set; }       // Total price from ad (e.g. 386.000 €)
    public double? ListedPricePerM2 { get; set; }     // Price per m² from ad (e.g. 2.572 €/m²)

    // V20: Deep dive status tracking — what happened when we tried to scrape this listing
    public string? DeepDiveStatus { get; set; }

    // V9 Deep Scraping: enrichment booleans
    public bool? IsRenovated { get; set; }          // True if renovated/new (YearReno>2015 or description)
    public bool? NeedsRenovation { get; set; }      // True if "za adaptaciju", "izvorno stanje", etc.
    public int? YearLastRenovation { get; set; }     // Parsed from detail page attributes

    // V10: AI-extracted structured fields from BrightData
    public int? AiYearBuilt { get; set; }            // Godina izgradnje
    public string? AiEnergyClass { get; set; }       // Energetski razred (A-G)
    public string? AiHeating { get; set; }           // Grijanje type
    public string? AiFloor { get; set; }             // Kat / Etaža
    public int? AiRooms { get; set; }                // Broj soba

    // V29 Sherlock: Structured attributes extracted from description text via LLM
    public Models.ListingAttributes? SherlockAttributes { get; set; }
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

    // V13: City Integrity stats
    public int CityIntegrityRejections { get; set; }
    public List<string> RejectedCityNames { get; set; } = [];

    // V16: Search debug log — all queries/URLs fired
    public List<SearchLogEntry> SearchLog { get; set; } = [];

    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];
}

public class SearchLogEntry
{
    public string Phase { get; set; } = "";
    public string Query { get; set; } = "";
    public int ResultCount { get; set; }
    public int PassedFilter { get; set; }
}
