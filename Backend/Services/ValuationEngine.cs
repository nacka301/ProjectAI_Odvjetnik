using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V4 — Robust Estimator Valuation Engine (with Intelligent Adjuster)
/// 
/// Statistical overhaul:
///   1. IQR outlier detection (no hardcoded median fallbacks)
///   2. SimilarityScore per comparable (AI-scored, math fallback)
///   3. Weighted Median (by SimilarityScore)
///   4. Adaptive Realization Gap (based on StdDev + search phase)
///   5. Mathematical Confidence (0–100, penalty system)
///   6. Hard cap ±40% on total adjustments
///   7. Full explainability JSON
///
/// AI provides similarity scores; math engine handles everything else deterministically.
/// </summary>
public class ValuationEngine
{
    private readonly ILogger<ValuationEngine> _logger;

    // ══════════════════════════════════════════════════════════
    //  REGEX — price extraction from listing snippets
    // ══════════════════════════════════════════════════════════
    // V24: Negative lookahead — do NOT match "981 €/m²" as a total price
    private static readonly Regex PriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)(?!\s*/\s*m[²2])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PricePerM2Regex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)\s*/\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // V17: Croatian thousands-separator support (e.g. "4.327,00 m²" → 4327)
    private static readonly Regex SizeRegex = new(
        @"(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ══════════════════════════════════════════════════════════
    //  NOTE: Regional benchmarks REMOVED.
    //  Engine works ONLY with real extracted prices from search data.
    // ══════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════
    //  TYPE SIMILARITY MAP
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, HashSet<string>> SimilarTypes =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["stan"]              = new(StringComparer.OrdinalIgnoreCase) { "apartman", "garsonijera", "dvosoban", "trosoban", "jednosoban" },
        ["kuća"]              = new(StringComparer.OrdinalIgnoreCase) { "vila", "vikendica", "obiteljska kuća", "dvojna kuća" },
        ["zemljište"]         = new(StringComparer.OrdinalIgnoreCase) { "građevinsko zemljište", "parcela", "oranica", "teren" },
        ["poslovni prostor"]  = new(StringComparer.OrdinalIgnoreCase) { "ured", "lokal", "trgovina", "skladište" },
    };

    // ══════════════════════════════════════════════════════════
    //  CONDITION KEYWORDS — detect property state from listing text
    // ══════════════════════════════════════════════════════════
    private static readonly string[] NewKeywords =
        ["novogradnja", "novo izgrad", "nova zgrada", "novoizgrad"];
    private static readonly string[] RenovatedKeywords =
        ["renoviran", "adaptiran", "novoure\u0111", "novoured", "kompletno ure\u0111",
         "potpuno obnovljen", "kompletno obnovljen", "u potpunosti obnovljen"];
    // NOTE V11: Removed bare "obnovljen" — it false-matches "potrebna obnova" → wrong condition.
    private static readonly string[] NeedsWorkKeywords =
        ["potrebna adaptac", "potrebna obnov", "ru\u0161evin", "rusevin",
         "zapu\u0161ten", "zapusten", "treba obnov", "devastiran",
         "potrebno ulaganj", "za ru\u0161enje", "za rusenje",
         // V6: degradation / moisture / wear keywords
         "vla\u017en", "vlazn", "stara stolari", "dotrajal",
         "plijes", "pljes", "buđ", "budj",
         "prokisnjav", "curenj", "probija vlaga",
         "trulo", "rđ", "rdj", "napukl",
         "oronul", "neuseljiv"];
    private static readonly string[] GoodKeywords =
        ["odr\u017eavan", "odrzavan", "uredno", "dobro stanje", "useljiv"];

    // ══════════════════════════════════════════════════════════
    //  FEATURE KEYWORDS — detect value-adding features from listing text
    // ══════════════════════════════════════════════════════════
    private static readonly string[] GarageKeywords =
        ["gara\u017ea", "garaza", "gara\u017eno", "garazno", "gara\u017eno mjesto",
         "garazno mjesto", "garage", "parking mjesto", "parkirno mjesto"];

    private static readonly string[] RenovationKeywords =
        ["nova fasada", "novi krov", "termoizolacija", "energetska obnova",
         "potpuno renoviran", "kompletno renoviran", "novoure\u0111en",
         "novouredjen", "kompletno ure\u0111", "kompletno uredj",
         "zamijenjeni prozor", "novi prozor", "nova stolarija"];

    private static readonly string[] YardKeywords =
        ["dvori\u0161te", "dvoriste", "oku\u0107nica", "okucnica", "vrt", "ba\u0161ta", "basta"];

    // ══════════════════════════════════════════════════════════
    //  V12: ORIENTATION & VIEW KEYWORDS
    // ══════════════════════════════════════════════════════════
    private static readonly string[] SeaViewKeywords =
        ["pogled na more", "sea view", "morski pogled", "pogled more",
         "panoramski pogled", "panoramic view", "ocean view"];

    private static readonly string[] ParkingKeywords =
        ["parking", "parkirno", "parkirali\u0161te", "parkiraliste",
         "parking mjesto", "pm ", "p.m."];

    private static readonly string[] NorthFacingKeywords =
        ["sjever", "north", "sjeverna strana", "orijentacija sjever",
         "sjeverna orijentacija"];

    private static readonly string[] SouthFacingKeywords =
        ["jug", "south", "ju\u017ena strana", "orijentacija jug",
         "ju\u017ena orijentacija", "ju\u017eno", "jugozapad", "southwest",
         "zapad", "west", "zapadna strana"];

    // ══════════════════════════════════════════════════════════
    //  V12: COASTAL CITY LIST
    // ══════════════════════════════════════════════════════════
    private static readonly HashSet<string> CoastalCities =
        new(StringComparer.OrdinalIgnoreCase)
    {
        "split", "rijeka", "dubrovnik", "zadar", "šibenik", "sibenik",
        "pula", "rovinj", "poreč", "porec", "opatija", "makarska",
        "trogir", "kaštela", "kastela", "solin", "omiš", "omis",
        "biograd", "vodice", "primošten", "primosten", "senj",
        "crikvenica", "novi vinodolski", "novalja", "rab",
        "korčula", "korcula", "hvar", "vis", "bol", "supetar",
        "baška voda", "baska voda", "brela", "podgora", "gradac",
        "cavtat", "mlini", "orebić", "orebic"
    };

    private static readonly Regex YardRegex = new(
        @"(?:oku\u0107nica|okucnica|dvori\u0161te|dvoriste|vrt|ba\u0161ta|basta|\bparcela\b)[:\s]*(?:od\s+)?(?:cca\.?\s+)?(?:ca\.?\s+)?(?:oko\s+)?(\d[\d.,]*)\s*m[\u00b22]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ══════════════════════════════════════════════════════════
    //  FIRST ROW LOCATIONS (1. red do mora)
    // ══════════════════════════════════════════════════════════
    private static readonly HashSet<string> FirstRowKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        "prvi red", "1. red", "1.red", "first row", "uz more", "na moru",
        "pogled na more", "sea view", "beach", "plaža", "waterfront"
    };

    // ══════════════════════════════════════════════════════════
    //  V22: CENTER / PREMIUM LOCATION KEYWORDS
    //  Detect when user's additional info indicates the property is in
    //  a city center, historic core, or premium micro-location.
    // ══════════════════════════════════════════════════════════
    private static readonly string[] CenterLocationKeywords =
        ["centar grada", "centar", "stari grad", "uži centar", "strogi centar",
         "strogom centru", "historijska jezgra", "povijesna jezgra",
         "širem centru", "širi centar", "city center", "old town"];

    private static readonly string[] PremiumLandmarkKeywords =
        ["dioklecijanova palača", "dioklecijanova", "riva", "marjan",
         "peristil", "prokurative", "katedrala", "bazilika",
         "lučica", "marina", "ACI marina",
         "ban jelačić", "gornji grad", "kaptol", "dolac",
         "knežev dvor", "stradun", "pile"];

    private static readonly string[] PeripheryKeywords =
        ["periferija", "predgrađe", "predgradje", "rubno područje",
         "rubno podrucje", "industrijska zona", "prigradsko",
         "daleko od centra", "udaljen"];

    private static readonly string[] PremiumAmenityKeywords =
        ["blizu škole", "blizu skole", "blizu fakulteta", "blizu bolnice",
         "blizu tramvaja", "blizu autobusne", "javni prijevoz",
         "blizu parka", "mirna ulica", "mirno naselje",
         "blizu trgovačkog centra", "blizu shopping centra",
         "turistička zona", "turisticka zona", "elitna lokacija",
         "atraktivna lokacija", "top lokacija"];

    // V26: Known SUBURB neighborhoods — when subject is in Prime Zone (centar/stari grad),
    // comparables from these neighborhoods are EXCLUDED ("Anti-Barutana Rule").
    // These are peripheral/suburban locations in major Croatian cities.
    private static readonly string[] SuburbKeywords =
        ["barutana", "mejaši", "mejasi", "sirobuja", "kman", "visoka",
         "neslanovac", "kopilica", "skalice", "žnjan", "znjan", "pazdigrad",
         "kila", "brda", "mertojak", "ravne njive", "pujanke", "trstenik",
         "dubrava", "sesvete", "velika gorica", "zaprešić", "zapresic",
         "spansko", "sloboština", "slobostina", "kozari bok", "kozari putevi",
         "novi zagreb", "žitnjak", "pescenica", "peščenica",
         "mokošica", "mokosica", "župa dubrovačka", "gruda",
         "srdoči", "srdoci", "zamet", "marinići", "marinici",
         "pehlin", "drenova"];

    // V25: Investment / commercial-potential property keywords
    // When these appear in the subject description, the property has commercial
    // conversion potential — renovation penalty must be CAPPED because location
    // value dominates (e.g. Split center house → mini hotel conversion).
    private static readonly string[] InvestmentKeywords =
        ["investicij", "mini hotel", "hotel", "hostel", "apartmani",
         "turistički", "turisticka", "turistickih", "turističkih",
         "komercijalni", "komercijaln", "poslovno-stambeni", "poslovno stambeni",
         "poslovni potencijal", "investicijski", "iznajmljivanj",
         "turizam", "booking", "airbnb", "pješačka zona", "pjesačka zona",
         "pješačkoj zoni", "ekskluzivn", "luksuzn",
         "rekonstrukcij", "prenamjen", "pretvorit", "konverzij"];

    // ══════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════
    //  SALES-ONLY GATEKEEPER — EXTREME SAFETY NET ONLY
    //  These floors exist ONLY to filter out obvious garbage (monthly rents,
    //  corrupt data). They are NOT valuation benchmarks. Real comparable
    //  prices from actual ads are ALWAYS the primary data source.
    //  Floor values are set very low intentionally so they never kill real listings.
    // ══════════════════════════════════════════════════════════
    private const double MinSalePriceEur_Building    = 35_000;
    private const double MinSalePriceEur_BuildLand   =  5_000;
    private const double MinSalePriceEur_AgriLand    =    500;
    private const double MinPricePerM2_Building      =    800;
    private const double MinPricePerM2_BuildLand     =     15;
    private const double MinPricePerM2_AgriLand      =    0.5;

    private static readonly string[] RentalKeywords =
    [
        "najam", "iznajmljivanje", "iznajmi", "rent", "rental",
        "mjesečno", "mjesecno", "monthly", "polog", "deposit",
        "za najam", "u najam", "for rent", "do iznajmljivanja",
        "kratkoročni", "kratkorocni", "dugoročni", "dugorocni",
        "najamnina", "rezije", "režije"
    ];

    /// <summary>
    /// Returns TRUE if the listing text contains rental keywords.
    /// </summary>
    private static bool IsRentalListing(string text)
    {
        string lower = text.ToLowerInvariant();
        foreach (var kw in RentalKeywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    public ValuationEngine(ILogger<ValuationEngine> logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT
    // ══════════════════════════════════════════════════════════════
    public DeterministicValuation Calculate(
        ExtractedPropertyInfo property,
        MarketResearchResult marketData)
    {
        var result = new DeterministicValuation();
        var pipeline = new List<string>();
        var outlierLog = new List<string>();
        var compDetails = new List<UsedComparable>();

        // ── 1. Context ──
        result.RegionName = marketData.RegionName;
        result.TotalListingsFound = marketData.ActiveListings.Count + marketData.RegionalData.Count;
        pipeline.Add($"Region: {result.RegionName} - PURE SEARCH DATA (no hardcoded benchmarks)");

        // ── 2. Extract all prices from REAL listings (primary data source) ──
        bool isLand = MarketResearchService.IsLandType(marketData.DetectedPropertyType);
        bool isAgriLand = IsAgriculturalLand(marketData.DetectedPropertyType, property);
        double minPricePerM2 = isAgriLand ? MinPricePerM2_AgriLand : (isLand ? MinPricePerM2_BuildLand : MinPricePerM2_Building);
        double minSalePrice  = isAgriLand ? MinSalePriceEur_AgriLand : (isLand ? MinSalePriceEur_BuildLand : MinSalePriceEur_Building);
        var rawPrices = ExtractPricesFromListings(marketData.AllResults, property.SizeM2, isLand, isAgriLand);
        pipeline.Add($"Izvučeno {rawPrices.Count} cijena iz {result.TotalListingsFound} oglasa (agri={isAgriLand})");

        // ── 3. IQR Outlier Detection ──
        string outlierMethod;
        List<ExtractedPrice> cleanPrices;

        if (rawPrices.Count >= 5)
        {
            // Full IQR
            var ppsqmValues = rawPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double q1 = Percentile(ppsqmValues, 25);
            double q3 = Percentile(ppsqmValues, 75);
            double iqr = q3 - q1;
            double lowerBound = q1 - 1.5 * iqr;
            double upperBound = q3 + 1.5 * iqr;

            // Safety: if IQR is 0 (all same price), use ±20% of median
            if (iqr < 1.0)
            {
                double median = ppsqmValues[ppsqmValues.Count / 2];
                lowerBound = median * 0.80;
                upperBound = median * 1.20;
            }

            cleanPrices = rawPrices
                .Where(p => p.PricePerM2 >= lowerBound && p.PricePerM2 <= upperBound)
                .ToList();

            int removed = rawPrices.Count - cleanPrices.Count;
            outlierMethod = "IQR";
            outlierLog.Add($"IQR: Q1={q1:F0}, Q3={q3:F0}, IQR={iqr:F0}");
            outlierLog.Add($"Bounds: [{lowerBound:F0} - {upperBound:F0}] EUR/m2");
            outlierLog.Add($"Uklonjeno {removed} outliera od {rawPrices.Count}");
            pipeline.Add($"IQR filter: {cleanPrices.Count} ostalo, {removed} uklonjeno");

            // V24: If IQR was too aggressive and <3 remain, restore ALL raw prices
            // (V23 location-aware clustering will handle mixed populations later)
            if (cleanPrices.Count < 3 && rawPrices.Count >= 3)
            {
                cleanPrices = rawPrices.ToList();
                outlierMethod = "IQR->Restored";
                outlierLog.Add($"IQR previse agresivan — vraćeni svi ({rawPrices.Count}) za V23 clustering");
                pipeline.Add($"IQR fallback: restored all {rawPrices.Count} for location clustering");
            }
        }
        else if (rawPrices.Count >= 2)
        {
            // V24: 2-4 prices — NO hardcoded median filter.
            // Pass all through; V23 location-aware clustering (step 3e) will handle
            // mixed populations (center vs periphery) much better than blind percentages.
            cleanPrices = rawPrices.ToList();
            outlierMethod = "SmallSample-PassThrough";
            outlierLog.Add($"2-4 cijene ({rawPrices.Count}) — bez median filtera, V23 clustering će obraditi");
            pipeline.Add($"Small sample: {cleanPrices.Count} prices passed through for clustering");
        }
        else if (rawPrices.Count == 1)
        {
            // Single price — pass through (gatekeeper will catch rentals)
            cleanPrices = rawPrices.ToList();
            outlierMethod = "Single Price";
            outlierLog.Add("Samo 1 cijena — prolazi bez filtera");
            pipeline.Add("Single price mode");
        }
        else
        {
            // No prices at all
            cleanPrices = [];
            outlierMethod = "No Data";
            outlierLog.Add("Nema cijena iz oglasa — nedovoljno podataka");
            pipeline.Add("No price data available");
        }

        // ── 3b. SALES-ONLY GATEKEEPER (extreme safety net) ──
        // Only removes obvious garbage (monthly rents misclassified as sales).
        // Floors are intentionally very low so real ad data is NEVER filtered out.
        {
            int beforeGatekeeper = cleanPrices.Count;
            cleanPrices = cleanPrices.Where(p => p.PricePerM2 >= minPricePerM2).ToList();
            int gatekeeperRemoved = beforeGatekeeper - cleanPrices.Count;
            if (gatekeeperRemoved > 0)
            {
                outlierLog.Add($"Sales-Only Gatekeeper: removed {gatekeeperRemoved} listings below EUR {minPricePerM2}/m2");
                pipeline.Add($"Gatekeeper: {cleanPrices.Count} remain after enforcing EUR {minPricePerM2}/m2 floor");
            }
        }

        // ── 3c. 10x Ratio Sanity Check ──
        // If max/min ratio > 10, rental contamination likely persists.
        if (cleanPrices.Count >= 3)
        {
            var sortedPpm2 = cleanPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double minVal = sortedPpm2.First();
            double maxVal = sortedPpm2.Last();
            if (minVal > 0 && maxVal / minVal > 10)
            {
                double medianVal = sortedPpm2[sortedPpm2.Count / 2];
                double sanityFloor = medianVal / 5.0;
                int beforeCount = cleanPrices.Count;
                cleanPrices = cleanPrices.Where(p => p.PricePerM2 >= sanityFloor).ToList();
                int sanityRemoved = beforeCount - cleanPrices.Count;
                if (sanityRemoved > 0)
                {
                    outlierLog.Add($"10x sanity: removed {sanityRemoved} extreme low values (floor=EUR {sanityFloor:F0}/m2)");
                    pipeline.Add($"10x sanity: {cleanPrices.Count} remain after removing {sanityRemoved} suspect entries");
                }
            }
        }

        // ── 3d. (REMOVED in V24) ──
        // High-Cluster Trust (CV>40% → keep upper half) was removed because:
        // - It ALWAYS picked the upper cluster, which is wrong for peripheral subjects
        // - V23 location-aware clustering (3e) does the same thing but with location context

        // ── 3e. V23: LOCATION-AWARE PRICE CLUSTERING ──
        // When there's a HUGE spread in prices (e.g., 833 vs 6500 €/m² in Split),
        // the data contains properties from different micro-locations (center vs periphery).
        // Instead of blindly averaging, detect 2 clusters and pick the one that matches
        // the subject's location characteristics (center/premium vs peripheral).
        if (cleanPrices.Count >= 2)
        {
            var sortedPrices = cleanPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double overallMin = sortedPrices.First();
            double overallMax = sortedPrices.Last();

            // Only cluster if max/min ratio > 2.5 (significant price disparity)
            if (overallMin > 0 && overallMax / overallMin > 2.5)
            {
                // Find the biggest gap between consecutive sorted prices
                double maxGap = 0;
                int splitIdx = 0;
                for (int i = 1; i < sortedPrices.Count; i++)
                {
                    double gap = sortedPrices[i] - sortedPrices[i - 1];
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                        splitIdx = i;
                    }
                }

                // Gap must be >50% of the lower cluster's max to be a real cluster boundary
                double lowerMax = sortedPrices[splitIdx - 1];
                if (maxGap > lowerMax * 0.50 && splitIdx > 0 && splitIdx < sortedPrices.Count)
                {
                    double splitPoint = (sortedPrices[splitIdx - 1] + sortedPrices[splitIdx]) / 2.0;
                    var lowerCluster = cleanPrices.Where(p => p.PricePerM2 < splitPoint).ToList();
                    var upperCluster = cleanPrices.Where(p => p.PricePerM2 >= splitPoint).ToList();

                    // Only act if both clusters have at least 1 member
                    if (lowerCluster.Count >= 1 && upperCluster.Count >= 1)
                    {
                        double lowerAvg = lowerCluster.Average(p => p.PricePerM2);
                        double upperAvg = upperCluster.Average(p => p.PricePerM2);

                        // Determine if subject is premium/center based on V22 fields
                        string subjectContext = $"{property.AdditionalNotes} {property.MicroLocation} " +
                            $"{string.Join(" ", property.KeyFeatures ?? [])} {property.Location} {property.RawText}"
                            .ToLowerInvariant();

                        bool isPremiumSubject =
                            CenterLocationKeywords.Any(kw => subjectContext.Contains(kw)) ||
                            PremiumLandmarkKeywords.Any(kw => subjectContext.Contains(kw)) ||
                            InvestmentKeywords.Any(kw => subjectContext.Contains(kw)) ||
                            subjectContext.Contains("elitna") || subjectContext.Contains("prestižn") ||
                            subjectContext.Contains("pješačka zona") || subjectContext.Contains("pjesacka zona");

                        bool isPeripheralSubject =
                            PeripheryKeywords.Any(kw => subjectContext.Contains(kw)) ||
                            subjectContext.Contains("rural") || subjectContext.Contains("selo");

                        if (isPremiumSubject && !isPeripheralSubject && upperCluster.Count >= 1)
                        {
                            int removed = lowerCluster.Count;
                            cleanPrices = upperCluster;
                            outlierLog.Add($"V23 Location Clustering: Subject is PREMIUM/CENTER → kept {upperCluster.Count} upper cluster " +
                                $"(avg {upperAvg:F0} €/m²), discarded {removed} lower cluster (avg {lowerAvg:F0} €/m²). " +
                                $"Split at {splitPoint:F0} €/m².");
                            pipeline.Add($"V23 Location Cluster: PREMIUM → upper cluster ({upperCluster.Count} @ avg {upperAvg:F0} €/m²)");
                        }
                        else if (isPeripheralSubject && !isPremiumSubject && lowerCluster.Count >= 1)
                        {
                            int removed = upperCluster.Count;
                            cleanPrices = lowerCluster;
                            outlierLog.Add($"V23 Location Clustering: Subject is PERIPHERAL → kept {lowerCluster.Count} lower cluster " +
                                $"(avg {lowerAvg:F0} €/m²), discarded {removed} upper cluster (avg {upperAvg:F0} €/m²). " +
                                $"Split at {splitPoint:F0} €/m².");
                            pipeline.Add($"V23 Location Cluster: PERIPHERAL → lower cluster ({lowerCluster.Count} @ avg {lowerAvg:F0} €/m²)");
                        }
                        else
                        {
                            // No clear location signal — log but don't filter
                            pipeline.Add($"V23 Location Cluster: detected 2 clusters " +
                                $"(lower={lowerCluster.Count}@{lowerAvg:F0}, upper={upperCluster.Count}@{upperAvg:F0}) " +
                                $"but no clear premium/peripheral signal → keeping all");
                        }
                    }
                }
            }
        }

        result.OutliersRemoved = rawPrices.Count - cleanPrices.Count;
        result.UsablePricePoints = cleanPrices.Count;

        // ── 4a. Condition-Based Price Adjustment ──
        // Adjust each comparable's price based on condition difference vs target
        // V10: MUTUALLY EXCLUSIVE with renovation cost deduction in 4a+.
        //   IF renovation cost (fixed amount) will be applied → OldBuildPenalty (%) = 0.
        //   You cannot subtract renovation cost AND penalize for building age simultaneously.
        var targetState = ParseTargetState(property);
        bool subjectNeedsWorkFlag = targetState == PropertyCondition.NeedsWork
                                 || targetState == PropertyCondition.Original;
        if (targetState != PropertyCondition.Unknown && cleanPrices.Count > 0)
        {
            int adjusted = 0;
            int skippedForReno = 0;
            foreach (var p in cleanPrices)
            {
                // V10: If renovation cost deduction will apply in 4a+, skip condition penalty
                bool compIsRenovatedOrNew = p.DetectedCondition == PropertyCondition.Renovated
                                         || p.DetectedCondition == PropertyCondition.New;
                if (subjectNeedsWorkFlag && compIsRenovatedOrNew)
                {
                    skippedForReno++;
                    continue; // Renovation cost deduction in 4a+ takes priority
                }

                double factor = ConditionAdjustmentFactor(targetState, p.DetectedCondition);
                if (Math.Abs(factor) > 0.001)
                {
                    double original = p.PricePerM2;
                    p.PricePerM2 = original * (1 + factor);
                    adjusted++;
                    _logger.LogDebug(
                        "Condition adj: {Title} {OldPrice:F0}->{NewPrice:F0} EUR/m2 ({Factor:+0%;-0%}) " +
                        "[target={Target}, comp={Comp}]",
                        p.SourceTitle.Length > 40 ? p.SourceTitle[..40] : p.SourceTitle,
                        original, p.PricePerM2, factor,
                        targetState, p.DetectedCondition);
                }
            }
            if (adjusted > 0)
                pipeline.Add($"Condition adj: {adjusted} comps adjusted (target={targetState})");
            if (skippedForReno > 0)
                pipeline.Add($"Condition adj: {skippedForReno} comps SKIPPED (renovation cost deduction takes priority — mutual exclusion)");
        }

        // ── 4a+. V12: TWIN COMPARISON — Data-Derived Feature Adjustments ──
        // Instead of hardcoded %, derive feature values from the dataset.
        // Phase 1: Detect market context (Coastal/Inland/Rural)
        // Phase 2: Find "twin pairs" to calculate feature values
        // Phase 3: Apply derived adjustments to each comparable
        if (cleanPrices.Count > 0)
        {
            var marketContext = DetectMarketContext(property.Location, marketData.RegionName);
            var derived = DeriveAdjustmentFactors(cleanPrices, marketContext, property.SizeM2);
            pipeline.Add($"V12 Twin Comparison: context={marketContext}");
            foreach (var dl in derived.DerivationLog)
                pipeline.Add($"  → {dl}");

            bool isApartment = marketData.DetectedPropertyType.Contains("stan", StringComparison.OrdinalIgnoreCase)
                            || marketData.DetectedPropertyType.Contains("apartman", StringComparison.OrdinalIgnoreCase);

            // Detect subject features from description
            // V22: Include AdditionalNotes so user-provided features (garaža, parking, etc.) are detected
            string subjectText = $"{property.Description} {property.Condition} {property.RawText} {property.AdditionalNotes}".ToLowerInvariant();
            bool subjectHasGarage = DetectFeature(subjectText, GarageKeywords);
            bool subjectHasParking = subjectHasGarage || DetectFeature(subjectText, ParkingKeywords);
            bool subjectHasReno = targetState == PropertyCondition.Renovated
                              || targetState == PropertyCondition.New
                              || DetectFeature(subjectText, RenovationKeywords);
            bool subjectHasYard = property.YardAreaM2 > 0
                              || DetectFeature(subjectText, YardKeywords);
            bool subjectHasSeaView = DetectFeature(subjectText, SeaViewKeywords);
            bool subjectIsNorth = DetectFeature(subjectText, NorthFacingKeywords);
            bool subjectIsSouth = DetectFeature(subjectText, SouthFacingKeywords);

            // V25: Detect investment/commercial properties — cap renovation penalties
            // A 300m² house in Split center pedestrian zone needing reconstruction still
            // has enormous location value. For investment properties the value is in the
            // LOCATION + POTENTIAL, not in the current building condition.
            bool isInvestmentProperty = InvestmentKeywords.Any(kw => subjectText.Contains(kw));
            bool isPremiumLocation =
                CenterLocationKeywords.Any(kw => subjectText.Contains(kw)) ||
                PremiumLandmarkKeywords.Any(kw => subjectText.Contains(kw));
            // Investment property = explicitly flagged OR premium center location
            bool capRenovationPenalty = isInvestmentProperty || (isPremiumLocation && subjectNeedsWorkFlag);
            if (capRenovationPenalty)
            {
                pipeline.Add("V25 Investment/Premium: renovation deductions CAPPED (location value dominates)");
            }

            int featureAdjCount = 0;
            var featureLog = new List<string>();

            foreach (var p in cleanPrices)
            {
                double originalPrice = p.PricePerM2;
                var reasons = new List<string>();
                double compTotalPrice = p.PricePerM2 * (property.SizeM2 > 0 ? property.SizeM2 : 60);

                // ── GARAGE (flat EUR deduction, derived from dataset) ──
                if (subjectHasGarage && !p.HasGarage)
                {
                    double boost = derived.GarageValueEur / compTotalPrice;
                    boost = Math.Min(boost, 0.15); // cap at 15%
                    p.PricePerM2 *= (1 + boost);
                    reasons.Add($"+{boost * 100:F0}% (comp nema garažu, derived={derived.GarageValueEur:N0}€)");
                }
                else if (!subjectHasGarage && p.HasGarage)
                {
                    double penalty = derived.GarageValueEur / compTotalPrice;
                    penalty = Math.Min(penalty, 0.15);
                    p.PricePerM2 *= (1 - penalty);
                    reasons.Add($"-{penalty * 100:F0}% (comp ima garažu, oduzeto={derived.GarageValueEur:N0}€)");
                }

                // ── PARKING (flat EUR deduction, derived) ──
                if (!subjectHasParking && p.HasParking && !p.HasGarage)
                {
                    double penalty = derived.ParkingValueEur / compTotalPrice;
                    penalty = Math.Min(penalty, 0.10);
                    p.PricePerM2 *= (1 - penalty);
                    reasons.Add($"-{penalty * 100:F0}% (comp ima parking, oduzeto={derived.ParkingValueEur:N0}€)");
                }
                else if (subjectHasParking && !p.HasParking && !subjectHasGarage)
                {
                    double boost = derived.ParkingValueEur / compTotalPrice;
                    boost = Math.Min(boost, 0.10);
                    p.PricePerM2 *= (1 + boost);
                    reasons.Add($"+{boost * 100:F0}% (comp nema parking, dodano={derived.ParkingValueEur:N0}€)");
                }

                // ── RENOVATION (derived % premium) ──
                bool subjectNeedsWork = targetState == PropertyCondition.NeedsWork
                    || targetState == PropertyCondition.Original;
                bool compIsFullyRenovated = p.DetectedCondition == PropertyCondition.Renovated
                                         || p.DetectedCondition == PropertyCondition.New;
                bool compHasPartialReno = p.HasRenovation && !compIsFullyRenovated;

                // V25: For investment/premium properties, cap renovation deduction to max 10%
                // The location value dominates — a ruin in Split center pedestrian zone
                // is worth more than a renovated house in the suburbs.
                double maxRenoPenalty = capRenovationPenalty ? 0.10 : 0.30;
                double maxPartialRenoPenalty = capRenovationPenalty ? 0.05 : 0.15;
                double maxGenericRenoPenalty = capRenovationPenalty ? 0.08 : 0.25;

                if (subjectNeedsWork && compIsFullyRenovated)
                {
                    double deduction = Math.Min(derived.RenovationPremiumPct, maxRenoPenalty);
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add(capRenovationPenalty
                        ? $"-{deduction * 100:F0}% (adaptacija — CAPPED, investicijski potencijal)"
                        : $"-{deduction * 100:F0}% (trošak adaptacije, derived reno premium={derived.RenovationPremiumPct * 100:F0}%)");
                }
                else if (subjectNeedsWork && compHasPartialReno)
                {
                    double deduction = Math.Min(derived.RenovationPremiumPct * 0.4, maxPartialRenoPenalty);
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add($"-{deduction * 100:F0}% (parcijalna reno)");
                }
                else if (!subjectHasReno && p.HasRenovation)
                {
                    double deduction = Math.Min(derived.RenovationPremiumPct * 0.8, maxGenericRenoPenalty);
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add($"-{deduction * 100:F0}% (comp renoviran, subjekt nije)");
                }
                else if (subjectHasReno && !p.HasRenovation
                         && p.DetectedCondition != PropertyCondition.Renovated
                         && p.DetectedCondition != PropertyCondition.New)
                {
                    double boost = Math.Min(derived.RenovationPremiumPct * 0.6, 0.20);
                    p.PricePerM2 *= (1 + boost);
                    reasons.Add($"+{boost * 100:F0}% (subjekt renoviran, comp star)");
                }

                // ── SEA VIEW (derived % premium, coastal only) ──
                if (marketContext == MarketContext.Coastal)
                {
                    if (!subjectHasSeaView && p.HasSeaView)
                    {
                        // Comp has sea view, subject doesn't → deduct view premium
                        double penalty = Math.Min(derived.SeaViewPremiumPct, 0.25);
                        p.PricePerM2 *= (1 - penalty);
                        reasons.Add($"-{penalty * 100:F0}% (comp ima pogled na more, subjekt ne)");
                    }
                    else if (subjectHasSeaView && !p.HasSeaView)
                    {
                        double boost = Math.Min(derived.SeaViewPremiumPct, 0.25);
                        p.PricePerM2 *= (1 + boost);
                        reasons.Add($"+{boost * 100:F0}% (subjekt ima pogled na more)");
                    }
                }

                // ── ORIENTATION (derived % delta, coastal only) ──
                // "Sun & Sea" logic: north-facing in coastal city = significant penalty
                if (marketContext == MarketContext.Coastal)
                {
                    if (subjectIsNorth && p.IsSouthFacing)
                    {
                        // Subject north, comp south → comp overpriced for us
                        double penalty = Math.Min(derived.OrientationDeltaPct, 0.20);
                        p.PricePerM2 *= (1 - penalty);
                        reasons.Add($"-{penalty * 100:F0}% (subjekt sjever, comp jug/zapad)");
                    }
                    else if (subjectIsSouth && p.IsNorthFacing)
                    {
                        double boost = Math.Min(derived.OrientationDeltaPct, 0.20);
                        p.PricePerM2 *= (1 + boost);
                        reasons.Add($"+{boost * 100:F0}% (subjekt jug/zapad, comp sjever)");
                    }
                    // Note: orientation + view penalties STACK
                    // North apartment without view vs south with view = -12% + -15% = -27%
                }

                // ── YARD (houses only) ──
                if (!isApartment && property.YardAreaM2 > 0 && p.DetectedYardM2 > 0)
                {
                    double yardDiff = property.YardAreaM2 - p.DetectedYardM2;
                    if (Math.Abs(yardDiff) > 200)
                    {
                        double yardAdj = (yardDiff / 200.0) * 0.07;
                        yardAdj = Math.Clamp(yardAdj, -0.25, 0.25);
                        p.PricePerM2 *= (1 + yardAdj);
                        reasons.Add($"{yardAdj * 100:+0;-0}% (okućnica razlika {yardDiff:+0;-0}m²)");
                    }
                }

                if (reasons.Count > 0)
                {
                    featureAdjCount++;
                    string label = p.SourceTitle.Length > 35 ? p.SourceTitle[..35] : p.SourceTitle;
                    featureLog.Add(
                        $"Comp '{label}': {originalPrice:F0}→{p.PricePerM2:F0} EUR/m² [{string.Join(", ", reasons)}]");
                }
            }

            if (featureAdjCount > 0)
            {
                pipeline.Add($"V12 Feature adj: {featureAdjCount} comps adjusted (garage/parking/reno/view/orientation/yard)");
                foreach (var log in featureLog)
                    pipeline.Add($"  → {log}");
            }
        }

        // ── 4b. Neighborhood Drift Penalty + Exact-Match Tagging ──
        // Same city but different neighborhood → -20% on comp price
        // Also tag each comp with IsExactNeighborhoodMatch for weight boosting & sanity check
        if (cleanPrices.Count > 0)
        {
            var locParts = property.Location.ToLowerInvariant()
                .Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 2)
                .ToList();

            if (locParts.Count >= 2)
            {
                string neighborhood = locParts[0]; // Most specific (kvart)
                string city = locParts[^1];         // Broadest (grad)
                int drifted = 0;
                int exactMatches = 0;

                foreach (var p in cleanPrices)
                {
                    string compText = $"{p.SourceTitle} {p.SourceUrl}".ToLowerInvariant();
                    bool inCity = compText.Contains(city);
                    bool inNeighborhood = compText.Contains(neighborhood);

                    // Tag exact neighborhood match
                    p.IsExactNeighborhoodMatch = inNeighborhood;
                    if (inNeighborhood) exactMatches++;

                    // V11: Removed -20% price drift penalty.
                    // Location mismatch is already handled by:
                    //   - SimilarityScore (0.12 vs 0.30 for location component)
                    //   - 3x weight boost for exact-match in weighted median
                    //   - Neighborhood purge (≥3 exact → discard rest)
                    // Stacking a -20% price cut on top caused triple-penalty.
                    if (inCity && !inNeighborhood)
                        drifted++;
                }

                if (drifted > 0)
                    pipeline.Add($"Neighborhood drift: {drifted} comps from other neighborhoods in {city} (handled via similarity scoring)");
                if (exactMatches > 0)
                    pipeline.Add($"Exact neighborhood match: {exactMatches} comps tagged for '{neighborhood}'");

                // ── 4b+. STRICT NEIGHBORHOOD PURGE ──
                // If we have ≥3 exact neighborhood matches, DISCARD everything else.
                // 3 perfect comps > 15 mixed comps from wrong neighborhoods.
                if (exactMatches >= 3)
                {
                    int beforePurge = cleanPrices.Count;
                    cleanPrices = cleanPrices.Where(p => p.IsExactNeighborhoodMatch).ToList();
                    int purged = beforePurge - cleanPrices.Count;
                    if (purged > 0)
                    {
                        pipeline.Add($"NEIGHBORHOOD PURGE: {purged} non-{neighborhood} comps discarded " +
                            $"({cleanPrices.Count} exact-match comps kept)");
                        _logger.LogInformation(
                            "Neighborhood purge: kept {Kept} exact-match, discarded {Purged} from other neighborhoods",
                            cleanPrices.Count, purged);
                    }
                }
            }
        }

        // ── 4b-ZONE. V26: PRIME ZONE LOCK ("Anti-Barutana Rule") ──
        // If subject is in a prime zone (Centar, Stari Grad, uži centar),
        // EXCLUDE any comparable located in a known suburb.
        // Reason: A house in Split Center near Diocletian's Palace CANNOT be compared
        // with a house in Barutana or Mejaši — completely different micro-markets.
        if (cleanPrices.Count > 0)
        {
            string subjectCtx = $"{property.AdditionalNotes} {property.MicroLocation} " +
                $"{string.Join(" ", property.KeyFeatures ?? [])} {property.Location} " +
                $"{property.RawText} {property.Description}"
                .ToLowerInvariant();

            bool subjectInPrimeZone =
                CenterLocationKeywords.Any(kw => subjectCtx.Contains(kw)) ||
                PremiumLandmarkKeywords.Any(kw => subjectCtx.Contains(kw));

            if (subjectInPrimeZone)
            {
                var zoneExcluded = new List<ExtractedPrice>();
                foreach (var p in cleanPrices)
                {
                    string compText = $"{p.SourceTitle} {p.SourceUrl}".ToLowerInvariant();
                    // Check if comp is in a known suburb
                    bool compIsSuburb = SuburbKeywords.Any(kw => compText.Contains(kw));
                    // Also check: if subject has center/landmark keywords, comp should too
                    bool compHasCenterSignal =
                        CenterLocationKeywords.Any(kw => compText.Contains(kw)) ||
                        PremiumLandmarkKeywords.Any(kw => compText.Contains(kw)) ||
                        compText.Contains("centar") || compText.Contains("center") ||
                        compText.Contains("stari grad") || compText.Contains("old town");

                    if (compIsSuburb && !compHasCenterSignal)
                        zoneExcluded.Add(p);
                }

                if (zoneExcluded.Count > 0)
                {
                    // Only exclude if we'd still have ≥1 comp remaining
                    int remaining = cleanPrices.Count - zoneExcluded.Count;
                    if (remaining >= 1)
                    {
                        var excludeUrls = new HashSet<string>(
                            zoneExcluded.Select(e => e.SourceUrl), StringComparer.OrdinalIgnoreCase);
                        cleanPrices = cleanPrices.Where(p => !excludeUrls.Contains(p.SourceUrl)).ToList();

                        // Mark as outliers in compDetails
                        foreach (var cd in compDetails.Where(c => excludeUrls.Contains(c.Url)))
                            cd.IsOutlier = true;

                        var excludedNames = string.Join(", ",
                            zoneExcluded.Select(e => e.SourceTitle.Length > 30 ? e.SourceTitle[..30] : e.SourceTitle));
                        pipeline.Add($"V26 PRIME ZONE LOCK: {zoneExcluded.Count} suburban comps EXCLUDED " +
                            $"(subject is in prime zone). Removed: {excludedNames}");
                        _logger.LogInformation(
                            "V26 Prime Zone Lock: excluded {Count} suburban comps, {Remaining} remain",
                            zoneExcluded.Count, cleanPrices.Count);
                    }
                    else
                    {
                        pipeline.Add($"V26 PRIME ZONE LOCK: {zoneExcluded.Count} suburban comps detected " +
                            $"but NOT excluded (would leave 0 comps)");
                    }
                }
            }
        }

        // ── 4b++. V13: CROSS-TYPE HARD EXCLUSION ──
        // Build URL→ComparableSearchResult lookup for enriched fields (needed by Sherlock + similarity)
        var listingLookup = marketData.ActiveListings
            .Where(l => !string.IsNullOrEmpty(l.Url))
            .GroupBy(l => l.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Stan ≠ Kuća ≠ Zemljište — NIKAD ne uspoređivati različite tipove.
        // If subject is House, ALL apartment comps are EXCLUDED (and vice versa).
        // V15: Also excludes land sub-type mismatch (poljopriv. vs građevinsko).
        // This is a hard block, not a penalty.
        {
            string subjectType = marketData.DetectedPropertyType.ToLowerInvariant();
            bool subjectIsHouse = subjectType.Contains("kuć") || subjectType.Contains("kuc")
                               || subjectType.Contains("vila") || subjectType.Contains("vikend");
            bool subjectIsApt   = subjectType.Contains("stan") || subjectType.Contains("apartman")
                               || subjectType.Contains("garsonije");
            bool subjectIsLand  = MarketResearchService.IsLandType(subjectType);
            bool subjectIsAgriLand = subjectIsLand && (subjectType.Contains("poljopriv")
                || subjectType.Contains("oranica") || subjectType.Contains("pašnjak")
                || subjectType.Contains("livada") || subjectType.Contains("šuma")
                || subjectType.Contains("vinograd") || subjectType.Contains("voćnjak")
                || subjectType.Contains("mješovit"));
            bool subjectIsBuildLand = subjectIsLand && (subjectType.Contains("građevinsk")
                || subjectType.Contains("gradjevinsk"));

            var toExclude = new List<ExtractedPrice>();
            foreach (var p in cleanPrices)
            {
                string compText = $"{p.SourceTitle} {p.SourceUrl}".ToLowerInvariant();
                bool compIsHouse = compText.Contains("kuć") || compText.Contains("kuc")
                                || compText.Contains("vila") || compText.Contains("vikend");
                bool compIsApt   = compText.Contains("stan") || compText.Contains("apartman")
                                || compText.Contains("garsonije");
                bool compIsLand  = compText.Contains("zemljišt") || compText.Contains("zemljist")
                                || compText.Contains("parcela") || compText.Contains("oranica");
                bool compIsAgriLand = compIsLand && (compText.Contains("poljopriv")
                    || compText.Contains("oranica") || compText.Contains("pašnjak")
                    || compText.Contains("livada") || compText.Contains("šuma")
                    || compText.Contains("vinograd") || compText.Contains("voćnjak"));
                bool compIsBuildLand = compIsLand && (compText.Contains("građevinsk")
                    || compText.Contains("gradjevinsk"));

                // V29 Sherlock: "Text Authority Rule" — Sherlock overrides scraper's land type
                ComparableSearchResult? sherlockListing = null;
                if (!string.IsNullOrEmpty(p.SourceUrl))
                    listingLookup.TryGetValue(p.SourceUrl, out sherlockListing);

                if (sherlockListing?.SherlockAttributes != null)
                {
                    var landType = sherlockListing.SherlockAttributes.ActualLandType;
                    if (landType == Models.ActualLandType.Agricultural)
                    {
                        compIsAgriLand = true;
                        compIsBuildLand = false;
                        compIsLand = true;
                    }
                    else if (landType == Models.ActualLandType.Building)
                    {
                        compIsBuildLand = true;
                        compIsAgriLand = false;
                        compIsLand = true;
                    }
                }

                bool mismatch = false;
                string reason = "";

                // House vs Apartment — FORBIDDEN
                if ((subjectIsHouse && compIsApt) || (subjectIsApt && compIsHouse))
                { mismatch = true; reason = subjectIsHouse ? "kuća vs stan" : "stan vs kuća"; }
                // House vs Land — FORBIDDEN
                else if ((subjectIsHouse && compIsLand) || (subjectIsLand && compIsHouse))
                { mismatch = true; reason = subjectIsHouse ? "kuća vs zemljište" : "zemljište vs kuća"; }
                // Apartment vs Land — FORBIDDEN
                else if ((subjectIsApt && compIsLand) || (subjectIsLand && compIsApt))
                { mismatch = true; reason = subjectIsApt ? "stan vs zemljište" : "zemljište vs stan"; }
                // V15: Agricultural vs Construction land — FORBIDDEN
                else if (subjectIsAgriLand && compIsBuildLand)
                { mismatch = true; reason = "poljopriv. vs građevinsko zemljište"; }
                else if (subjectIsBuildLand && compIsAgriLand)
                { mismatch = true; reason = "građevinsko vs poljopriv. zemljište"; }

                if (mismatch)
                {
                    toExclude.Add(p);
                    _logger.LogInformation(
                        "CROSS-TYPE EXCLUDED: {Title} ({Reason})",
                        p.SourceTitle.Length > 40 ? p.SourceTitle[..40] : p.SourceTitle, reason);
                }
            }

            if (toExclude.Count > 0)
            {
                var excludeUrls = new HashSet<string>(toExclude.Select(e => e.SourceUrl), StringComparer.OrdinalIgnoreCase);
                cleanPrices = cleanPrices.Where(p => !excludeUrls.Contains(p.SourceUrl)).ToList();
                pipeline.Add($"V13 Cross-Type EXCLUSION: {toExclude.Count} comps REMOVED (different property type — hard block)");
            }
        }

        // ── 4c. Similarity Scoring (AI-first, math fallback) ──
        // Build URL→AiSimilarityScore lookup from market data
        var aiScoreLookup = marketData.ActiveListings
            .Where(l => l.AiSimilarityScore.HasValue && !string.IsNullOrEmpty(l.Url))
            .ToDictionary(l => l.Url, l => l.AiSimilarityScore!.Value, StringComparer.OrdinalIgnoreCase);

        int aiScored = 0, mathFallbackScored = 0;

        foreach (var price in cleanPrices)
        {
            double simScore;
            if (aiScoreLookup.TryGetValue(price.SourceUrl, out var aiScore))
            {
                simScore = Math.Clamp(aiScore, 0.0, 1.0);
                aiScored++;
            }
            else
            {
                // Fallback: math-based scoring if AI didn't provide a score
                simScore = CalculateSimilarityScore(
                    price, property, marketData.DetectedPropertyType, marketData.RegionName);
                mathFallbackScored++;
            }

            // Lookup enriched data from BrightData/AI for this listing
            ComparableSearchResult? listing = null;
            if (!string.IsNullOrEmpty(price.SourceUrl))
                listingLookup.TryGetValue(price.SourceUrl, out listing);

            // V29 Sherlock: Similarity Boosting/Penalizing based on text analysis
            if (listing?.SherlockAttributes != null)
            {
                var attrs = listing.SherlockAttributes;

                // Garage match bonus: subject has garage AND comp has garage → +5%
                bool subjectHasGarage = property.Description != null &&
                    (property.Description.Contains("garaž", StringComparison.OrdinalIgnoreCase) ||
                     property.Description.Contains("garaz", StringComparison.OrdinalIgnoreCase));
                if (subjectHasGarage && attrs.HasGarage)
                    simScore = Math.Min(simScore + 0.05, 1.0);

                // Condition mismatch penalty: subject is renovated AND comp is ruin → -30%
                bool subjectIsRenovated = property.PropertyState is "renovated" or "new" or "good";
                if (subjectIsRenovated && attrs.RenovationStatus == Models.RenovationStatus.Ruin)
                    simScore = Math.Max(simScore - 0.30, 0.0);

                // Urgent listing penalty: urgent sales often have below-market prices → -5%
                if (attrs.IsUrgent)
                    simScore = Math.Max(simScore - 0.05, 0.0);

                // Super-Extractor: Major damages → strong penalty (unreliable comp)
                if (attrs.MajorDamages.Any(d => d is "StaticIssue" or "RoofLeak"))
                    simScore = Math.Max(simScore - 0.20, 0.0);

                // Super-Extractor: Legal risk → comps with legalization issues are less comparable
                if (attrs.LegalStatus == Models.LegalStatus.LegalizationNeeded)
                    simScore = Math.Max(simScore - 0.15, 0.0);
            }
            simScore = Math.Clamp(simScore, 0.0, 1.0);

            // V20: Per-comparable correction (individual adjustment)
            var (corrPct, corrReason) = CalculatePerCompCorrection(property, listing, price, marketData.DetectedPropertyType);

            // V24: Prefer ListedPriceEur (actual ad price) for TotalPrice in the table
            double? totalPrice = listing?.ListedPriceEur > 0
                ? listing.ListedPriceEur
                : listing?.AiSizeM2 > 0
                    ? Math.Round(price.PricePerM2 * listing.AiSizeM2!.Value, 0)
                    : price.DetectedSizeM2 > 0 ? Math.Round(price.PricePerM2 * price.DetectedSizeM2, 0) : null;

            compDetails.Add(new UsedComparable
            {
                Title = price.SourceTitle,
                Url = price.SourceUrl,
                PricePerM2 = Math.Round(price.PricePerM2, 0),
                SimilarityScore = Math.Round(simScore, 2),
                IsOutlier = false,
                // Enriched fields from BrightData / AI extraction
                AreaM2 = listing?.AiSizeM2 ?? (price.DetectedSizeM2 > 0 ? price.DetectedSizeM2 : null),
                TotalPrice = totalPrice,
                Rooms = listing?.AiRooms,
                Floor = listing?.AiFloor,
                YearBuilt = listing?.AiYearBuilt,
                Condition = listing?.AiCondition,
                EnergyClass = listing?.AiEnergyClass,
                Heating = listing?.AiHeating,
                Location = listing?.Title,
                HasGarage = listing?.HasGarage ?? (price.HasGarage ? true : null),
                HasParking = price.HasParking ? true : null,
                YardAreaM2 = listing?.AiYardAreaM2 > 0 ? listing.AiYardAreaM2 : (price.DetectedYardM2 > 0 ? price.DetectedYardM2 : null),
                SimilarityReason = listing?.AiSimilarityReason,
                DeepDiveStatus = listing?.DeepDiveStatus,
                // V20: Per-comparable correction
                CorrectionPercent = corrPct,
                CorrectionReason = corrReason,
            });
        }

        pipeline.Add($"Similarity: {aiScored} AI-scored, {mathFallbackScored} math-fallback");

        // Also log the removed outliers
        var outlierPrices = rawPrices.Except(cleanPrices);
        foreach (var price in outlierPrices)
        {
            ComparableSearchResult? olisting = null;
            if (!string.IsNullOrEmpty(price.SourceUrl))
                listingLookup.TryGetValue(price.SourceUrl, out olisting);

            compDetails.Add(new UsedComparable
            {
                Title = price.SourceTitle,
                Url = price.SourceUrl,
                PricePerM2 = Math.Round(price.PricePerM2, 0),
                SimilarityScore = 0,
                IsOutlier = true,
                AreaM2 = olisting?.AiSizeM2 ?? (price.DetectedSizeM2 > 0 ? price.DetectedSizeM2 : null),
                Rooms = olisting?.AiRooms,
                Floor = olisting?.AiFloor,
                YearBuilt = olisting?.AiYearBuilt,
                Condition = olisting?.AiCondition,
                EnergyClass = olisting?.AiEnergyClass,
                Heating = olisting?.AiHeating,
            });
        }

        // ── 4c½. SIMILARITY FLOOR ──
        // Remove any comparable below 50% similarity — too dissimilar to be useful.
        // V28: Raised from 0.50 to 0.55 — below 55% the comp is too dissimilar
        // to contribute meaningful data. A 55% comp on Trešnjevka-Sjever at 1,864 €/m²
        // was dragging down center averages from 3,500 to 3,045.
        const double MinSimilarityThreshold = 0.55;
        {
            var belowFloor = compDetails.Where(c => !c.IsOutlier && c.SimilarityScore < MinSimilarityThreshold).ToList();
            if (belowFloor.Count > 0)
            {
                var belowFloorUrls = new HashSet<string>(belowFloor.Select(c => c.Url), StringComparer.OrdinalIgnoreCase);
                foreach (var cd in belowFloor)
                    cd.IsOutlier = true;

                cleanPrices = cleanPrices
                    .Where(p => !belowFloorUrls.Contains(p.SourceUrl))
                    .ToList();

                pipeline.Add($"Similarity floor ({MinSimilarityThreshold:P0}): removed {belowFloor.Count} comps below threshold, {cleanPrices.Count} remaining");
                _logger.LogInformation(
                    "Similarity floor: removed {Removed} comps below {Threshold:P0}, {Remaining} remaining",
                    belowFloor.Count, MinSimilarityThreshold, cleanPrices.Count);
            }
        }

        // ── 4c+. TOP-N LIMITER ──
        // Never let a long tail of weak comps drag down the median.
        // After scoring, keep only the top 7 by SimilarityScore.
        const int MaxCompsForValuation = 7;
        if (cleanPrices.Count > MaxCompsForValuation)
        {
            // Sort compDetails (non-outliers) by similarity descending, mark excess as outliers
            var ranked = compDetails
                .Where(c => !c.IsOutlier)
                .OrderByDescending(c => c.SimilarityScore)
                .ToList();

            var keepUrls = new HashSet<string>(
                ranked.Take(MaxCompsForValuation).Select(c => c.Url),
                StringComparer.OrdinalIgnoreCase);

            // Mark excess comps as outliers so they don't participate in valuation
            int trimmed = 0;
            foreach (var cd in ranked.Skip(MaxCompsForValuation))
            {
                cd.IsOutlier = true;
                trimmed++;
            }

            // Also remove from cleanPrices so weighted median ignores them
            cleanPrices = cleanPrices
                .Where(p => keepUrls.Contains(p.SourceUrl))
                .ToList();

            pipeline.Add($"Top-N limiter: kept top {cleanPrices.Count} comps, trimmed {trimmed} low-similarity tail");
        }

        // ── 4d. Reality Check Cap (HIGH + LOW) ──
        // V28: Tightened from ±50% to ±40% of median — catches cheap outliers sooner.
        // A comp at 1,864 €/m² when median is 3,500 = 53% → now caught (< 60% floor).
        if (cleanPrices.Count >= 3)
        {
            var rcPrices = cleanPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double rcMedian = rcPrices[rcPrices.Count / 2];
            double rcCap = rcMedian * 1.40;    // V28: was 1.50
            double rcFloor = rcMedian * 0.60;  // V28: was 0.50
            int capped = 0;

            var cappedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cd in compDetails.Where(c => !c.IsOutlier))
            {
                if (cd.PricePerM2 > rcCap)
                {
                    cd.IsOutlier = true;
                    cappedUrls.Add(cd.Url);
                    capped++;
                    _logger.LogDebug(
                        "Reality cap HIGH: {Title} {Price:F0} EUR/m2 > cap {Cap:F0} -> EXCLUDED",
                        cd.Title.Length > 40 ? cd.Title[..40] : cd.Title,
                        cd.PricePerM2, rcCap);
                }
                else if (cd.PricePerM2 < rcFloor)
                {
                    cd.IsOutlier = true;
                    cappedUrls.Add(cd.Url);
                    capped++;
                    _logger.LogDebug(
                        "Reality cap LOW: {Title} {Price:F0} EUR/m2 < floor {Floor:F0} -> EXCLUDED",
                        cd.Title.Length > 40 ? cd.Title[..40] : cd.Title,
                        cd.PricePerM2, rcFloor);
                }
            }

            if (capped > 0)
            {
                // V21: Only remove if we still have ≥2 comps after removal
                var remainingPrices = cleanPrices
                    .Where(p => !cappedUrls.Contains(p.SourceUrl))
                    .ToList();
                if (remainingPrices.Count >= 2)
                {
                    cleanPrices = remainingPrices;
                    pipeline.Add($"Reality check: {capped} comps EXCLUDED as outliers " +
                        $"(floor={rcFloor:F0}, cap={rcCap:F0}, median={rcMedian:F0})");
                }
                else
                {
                    // Can't remove — would leave <2 comps. Reset outlier flags.
                    foreach (var cd in compDetails.Where(c => cappedUrls.Contains(c.Url)))
                        cd.IsOutlier = false;
                    pipeline.Add($"Reality check: {capped} potential outliers found but NOT removed (would leave <2 comps)");
                }
            }
        }

        // ── 4d+. V26: ZONE-AWARE OUTLIER PURGE ──
        // When 2 comps have a price gap >100% (e.g. 900€ vs 2600€), identify zone mismatch.
        // If subject is in a superior zone (prime/center), discard the cheaper one.
        // Use median of the TOP tier only — don't average "palace" and "shack".
        if (cleanPrices.Count >= 2)
        {
            var activeComps = compDetails.Where(c => !c.IsOutlier).OrderBy(c => c.PricePerM2).ToList();
            double lowestPrice = activeComps.First().PricePerM2;
            double highestPrice = activeComps.Last().PricePerM2;

            if (lowestPrice > 0 && highestPrice / lowestPrice > 2.0)
            {
                // >100% gap detected — check if subject is in a prime zone
                string subjectCtx2 = $"{property.AdditionalNotes} {property.MicroLocation} " +
                    $"{string.Join(" ", property.KeyFeatures ?? [])} {property.Location} " +
                    $"{property.RawText} {property.Description}"
                    .ToLowerInvariant();

                bool subjectIsPrime =
                    CenterLocationKeywords.Any(kw => subjectCtx2.Contains(kw)) ||
                    PremiumLandmarkKeywords.Any(kw => subjectCtx2.Contains(kw));

                if (subjectIsPrime)
                {
                    // Find the split point (median) and discard comps in the lower tier
                    double midPoint = (lowestPrice + highestPrice) / 2.0;
                    var cheapComps = activeComps.Where(c => c.PricePerM2 < midPoint).ToList();
                    var premiumComps = activeComps.Where(c => c.PricePerM2 >= midPoint).ToList();

                    if (cheapComps.Count > 0 && premiumComps.Count >= 1)
                    {
                        var cheapUrls = new HashSet<string>(
                            cheapComps.Select(c => c.Url), StringComparer.OrdinalIgnoreCase);
                        foreach (var cd in cheapComps)
                            cd.IsOutlier = true;
                        cleanPrices = cleanPrices
                            .Where(p => !cheapUrls.Contains(p.SourceUrl)).ToList();

                        pipeline.Add($"V26 ZONE OUTLIER PURGE: {cheapComps.Count} cheap comps discarded " +
                            $"(gap >{highestPrice / lowestPrice:F1}x, subject in prime zone). " +
                            $"Kept {premiumComps.Count} premium-tier comps (≥{midPoint:F0} €/m²)");
                    }
                }
            }
        }

        // ── 5. Weighted Median / Valuation ──
        double basePricePerM2;
        string priceSource;

        if (cleanPrices.Count >= 2)
        {
            // Build weight-value pairs (3x boost for exact neighborhood match)
            // V20: Use per-comparable corrected prices for weighted median
            var weightedPairs = cleanPrices.Select(p =>
            {
                var match = compDetails
                    .FirstOrDefault(c => c.Url == p.SourceUrl && !c.IsOutlier);
                double sim = match?.SimilarityScore ?? 0.5;
                double weight = Math.Max(sim, 0.1); // minimum weight

                // V7: 300% weight boost for exact neighborhood match
                if (p.IsExactNeighborhoodMatch)
                    weight *= 3.0;

                // V20: Apply per-comparable correction to price
                double correctedPrice = p.PricePerM2;
                if (match != null && match.CorrectionPercent != 0)
                    correctedPrice = p.PricePerM2 * (1 + match.CorrectionPercent / 100.0);

                return (Value: correctedPrice, Weight: weight);
            }).ToList();

            basePricePerM2 = WeightedMedian(weightedPairs);
            priceSource = $"Weighted Median ({cleanPrices.Count} comparables)";
            pipeline.Add($"Weighted Median = {basePricePerM2:F0} EUR/m2");

            // ── 5b. Sanity Check: exact-match anchor ──
            // If final price >40% above lowest exact-match comp, re-evaluate using only exact-match comps.
            var exactMatchComps = cleanPrices.Where(p => p.IsExactNeighborhoodMatch).ToList();
            if (exactMatchComps.Count >= 2)
            {
                double lowestExactMatch = exactMatchComps.Min(p => p.PricePerM2);
                double sanityThreshold = lowestExactMatch * 1.40;

                if (basePricePerM2 > sanityThreshold)
                {
                    // Re-run weighted median using ONLY exact-match comps
                    var exactPairs = exactMatchComps.Select(p =>
                    {
                        var m = compDetails.FirstOrDefault(c => c.Url == p.SourceUrl && !c.IsOutlier);
                        double s = m?.SimilarityScore ?? 0.5;
                        double w = Math.Max(s, 0.1) * 3.0; // keep the 3x boost
                        return (Value: p.PricePerM2, Weight: w);
                    }).ToList();

                    double corrected = WeightedMedian(exactPairs);
                    pipeline.Add(
                        $"SANITY CHECK: WM {basePricePerM2:F0} > {sanityThreshold:F0} (140% of lowest exact-match {lowestExactMatch:F0}) " +
                        $"-> re-evaluated using {exactMatchComps.Count} exact-match comps: {corrected:F0} EUR/m2");
                    basePricePerM2 = corrected;
                    priceSource = $"Sanity-Corrected WM ({exactMatchComps.Count} exact-match comps)";
                }
            }
        }
        else if (cleanPrices.Count == 1)
        {
            // Single comparable — use 100% of that price (no benchmark blend)
            basePricePerM2 = cleanPrices[0].PricePerM2;
            priceSource = "Single Comparable";
            pipeline.Add($"Single comp = {basePricePerM2:F0} EUR/m2");
        }
        else
        {
            // No data — engine cannot produce a reliable estimate
            basePricePerM2 = 0;
            priceSource = "Nedovoljno podataka";
            pipeline.Add("Nema ekstrahiranih cijena — deterministic engine ne može procijeniti");
        }

        result.MarketAverageAsking = Math.Round(basePricePerM2, 0);

        // ── 6. Standard Deviation (on clean prices) ──
        double stdDev = 0;
        double coeffVar = 0;
        if (cleanPrices.Count >= 2)
        {
            var values = cleanPrices.Select(p => p.PricePerM2).ToList();
            double mean = values.Average();
            double sumSqDiff = values.Sum(v => (v - mean) * (v - mean));
            stdDev = Math.Sqrt(sumSqDiff / values.Count);
            coeffVar = mean > 0 ? (stdDev / mean) * 100 : 0;
            pipeline.Add($"StdDev = {stdDev:F0} EUR/m2 (CV = {coeffVar:F1}%)");
        }

        result.StdDevPerM2 = Math.Round(stdDev, 0);
        result.CoefficientOfVariation = Math.Round(coeffVar, 1);

        // ── 7. Adaptive Realization Gap ──
        var searchPhase = ParseSearchPhase(marketData.SearchPhaseUsed);

        // V26: Detect if subject is prime zone + investment property → cap realization gap
        string gapCtx = $"{property.AdditionalNotes} {property.MicroLocation} " +
            $"{string.Join(" ", property.KeyFeatures ?? [])} {property.Location} " +
            $"{property.RawText} {property.Description}"
            .ToLowerInvariant();
        bool isPrimeZoneInvestment =
            (CenterLocationKeywords.Any(kw => gapCtx.Contains(kw)) ||
             PremiumLandmarkKeywords.Any(kw => gapCtx.Contains(kw))) &&
            InvestmentKeywords.Any(kw => gapCtx.Contains(kw));

        double realizationGap = CalculateAdaptiveGap(coeffVar, searchPhase, cleanPrices.Count, priceSource, isPrimeZoneInvestment);
        result.RealizationDiscountPercent = Math.Round(realizationGap * 100, 1);

        double afterGap = basePricePerM2 * (1 + realizationGap);
        result.AfterDiscountPerM2 = Math.Round(afterGap, 0);  // V27: store for UI sync
        pipeline.Add($"Realization gap: {realizationGap * 100:F1}% -> {afterGap:F0} EUR/m2");

        // ── 8. Contextual Adjustments (with hard cap ±40%) ──
        var adjustments = CalculateAdjustments(property, marketData);
        double totalAdjPercent = adjustments.Sum(a => a.Percent);

        // Hard cap ±40%
        if (totalAdjPercent > 40) totalAdjPercent = 40;
        if (totalAdjPercent < -40) totalAdjPercent = -40;

        double adjustedPricePerM2 = afterGap * (1 + totalAdjPercent / 100.0);
        pipeline.Add($"Adjustments: {totalAdjPercent:+0.0;-0.0}% -> {adjustedPricePerM2:F0} EUR/m2");

        // V27: DEBUG — full transparent math chain
        pipeline.Add($"V27 MATH CHAIN: Base(WM)={basePricePerM2:F0} × (1{realizationGap:+0.0%;-0.0%})={afterGap:F0} × (1{totalAdjPercent/100.0:+0.0%;-0.0%})={adjustedPricePerM2:F0} EUR/m2");
        _logger.LogInformation(
            "V27 Math: Base={Base:F0} × Discount={Gap:P1} = {AfterGap:F0} × Adj={Adj:+0.0;-0.0}% = {Final:F0} EUR/m2",
            basePricePerM2, realizationGap, afterGap, totalAdjPercent, adjustedPricePerM2);

        result.CalculatedPricePerM2 = Math.Round(adjustedPricePerM2, 0);
        result.Adjustments = adjustments;
        result.TotalAdjustmentPercent = Math.Round(totalAdjPercent, 1);

        // ── 9. Final Value ──
        double sizeM2 = property.SizeM2 > 0 ? property.SizeM2 : EstimateSizeFromContext(property);
        int finalValue = (int)Math.Round(adjustedPricePerM2 * sizeM2);

        // ── 9b. V11: SAFETY FLOOR — prevent algorithmic underflow ──
        // Final valuation cannot be lower than 50% of the average CLEAN comparable price.
        // V12: Only activate when there are actual usable comparables.
        // V13: Use cleanPrices (post-outlier) not rawPrices — raw includes outliers
        // and wrong-type listings that inflate the floor unrealistically.
        if (cleanPrices.Count >= 2)
        {
            double cleanAvgPricePerM2 = cleanPrices.Average(c => c.PricePerM2);
            double averageCompTotal = cleanAvgPricePerM2 * sizeM2;
            double floorPrice = averageCompTotal * 0.50;
            if (finalValue < floorPrice)
            {
                int oldValue = finalValue;
                finalValue = (int)Math.Round(floorPrice);
                pipeline.Add($"SAFETY FLOOR: {oldValue:N0} EUR < 65% of avg comp ({averageCompTotal:N0} EUR) " +
                    $"→ clamped to {finalValue:N0} EUR");
                _logger.LogWarning(
                    "Safety floor activated: {Old:N0} -> {New:N0} EUR (65% of avg comp {Avg:N0})",
                    oldValue, finalValue, averageCompTotal);
            }
        }

        result.EstimatedValueEur = finalValue;
        result.PriceSource = priceSource;
        pipeline.Add($"Final: {adjustedPricePerM2:F0} EUR/m2 x {sizeM2:F0} m2 = {finalValue:N0} EUR");

        // ── 10. Mathematical Confidence Score ──
        var mathConfidence = CalculateMathConfidence(
            cleanPrices.Count, coeffVar, priceSource, searchPhase, outlierMethod);
        result.MathConfidenceScore = mathConfidence.Score;
        result.MathConfidenceLevel = mathConfidence.Level;
        result.MathConfidenceExplanation = mathConfidence.Explanation;
        pipeline.Add($"Math Confidence: {mathConfidence.Score}/100 ({mathConfidence.Level})");

        // ── 11. Confidence Level (legacy compat) ──
        result.ConfidenceLevel = mathConfidence.Level;

        // ── 12. Explainability ──
        result.Pipeline = pipeline;
        result.OutlierMethod = outlierMethod;
        result.OutlierLog = outlierLog;
        result.UsedComparables = compDetails;
        result.RealizationGapReasoning = BuildGapReasoning(coeffVar, searchPhase, cleanPrices.Count, realizationGap);

        // ── 13. Sync: Record URLs of non-outlier comps for LLM filtering ──
        result.FinalUsedUrls = new HashSet<string>(
            compDetails.Where(c => !c.IsOutlier && !string.IsNullOrEmpty(c.Url))
                       .Select(c => c.Url),
            StringComparer.OrdinalIgnoreCase);
        pipeline.Add($"LLM sync: {result.FinalUsedUrls.Count} clean URLs exported for AI prompt");

        // ── 14. V13: Sherlock Insight — explain city integrity & quality controls ──
        result.SherlockInsight = BuildSherlockInsight(
            marketData, cleanPrices.Count, compDetails, result.OutliersRemoved);

        _logger.LogInformation(
            "Engine V4: {Comps} comps, WM={WM:F0}, gap={Gap:F1}%, adj={Adj:+0;-0}%, final={Val:N0} €, confidence={Conf}/100",
            cleanPrices.Count, basePricePerM2, realizationGap * 100, totalAdjPercent,
            finalValue, mathConfidence.Score);

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  SIMILARITY SCORE (0.0 – 1.0)
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// V13: Location-First Similarity Score.
    /// Weight distribution: Location 70% | Type+Size 20% | Condition+Features 10%
    /// Hard floor: if score < 0.50, listing is excluded from valuation.
    /// </summary>
    private double CalculateSimilarityScore(
        ExtractedPrice comp,
        ExtractedPropertyInfo target,
        string detectedType,
        string regionName)
    {
        // V14: Simplified weights — Location 70%, Size 20%, Condition 10%
        // Type matching is now a HARD BLOCK (step 4b++) not a soft score.
        double score = 0;

        string compText = $"{comp.SourceTitle} {comp.SourceUrl}".ToLowerInvariant();
        string targetLoc = target.Location.ToLowerInvariant().Trim();

        var locParts = targetLoc.Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 2)
            .ToList();

        // ── Location (max 0.70) — 70% of total ──
        bool isExactNeighborhood = locParts.Count >= 2
            && compText.Contains(locParts[0]);

        if (locParts.Count >= 2 && compText.Contains(locParts[^1]) && compText.Contains(locParts[0]))
        {
            score += 0.70; // Same neighborhood + city = full score
        }
        else if (isExactNeighborhood)
        {
            score += 0.60; // Neighborhood match without city context
        }
        else if (locParts.Any(part => compText.Contains(part)))
        {
            score += 0.25; // Same city but WRONG neighborhood
        }
        else if (!string.IsNullOrEmpty(regionName) && compText.Contains(regionName.ToLowerInvariant()))
        {
            score += 0.15; // Same region only
        }
        else
        {
            score += 0.03; // Unknown location — near-zero
        }

        // ── Size Proximity (max 0.20) — 20% of total ──
        if (target.SizeM2 > 0)
        {
            double compSize = ExtractSize(compText);
            if (compSize > 0)
            {
                double sizeRatio = target.SizeM2 > compSize
                    ? compSize / target.SizeM2
                    : target.SizeM2 / compSize;

                score += 0.20 * Math.Max(0, sizeRatio);
            }
            else
            {
                score += 0.08; // Unknown size — neutral
            }
        }
        else
        {
            score += 0.10; // No target size — neutral
        }

        // ── Condition Match (max 0.10) — 10% of total ──
        var targetCond = ParseTargetState(target);
        var compCond = comp.DetectedCondition;

        if (targetCond != PropertyCondition.Unknown && compCond != PropertyCondition.Unknown)
        {
            int rankDiff = Math.Abs(StateRank(targetCond) - StateRank(compCond));
            double condScore = rankDiff switch
            {
                0 => 0.10,   // Perfect match
                1 => 0.07,   // Close
                2 => 0.03,   // Different
                _ => 0.01    // Very different
            };
            score += condScore;
        }
        else
        {
            score += 0.05; // Unknown — neutral
        }

        // Total max = 0.70 + 0.20 + 0.10 = 1.00
        return Math.Clamp(score, 0.0, 1.0);
    }

    private bool IsSimilarType(string targetType, string compText)
    {
        foreach (var (key, similar) in SimilarTypes)
        {
            if (targetType.Contains(key, StringComparison.OrdinalIgnoreCase)
                || similar.Contains(targetType))
            {
                // Check if comp matches any synonym
                if (compText.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (similar.Any(s => compText.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        return false;
    }

    // ══════════════════════════════════════════════════════════
    //  WEIGHTED MEDIAN
    // ══════════════════════════════════════════════════════════
    private static double WeightedMedian(List<(double Value, double Weight)> pairs)
    {
        if (pairs.Count == 0) return 0;
        if (pairs.Count == 1) return pairs[0].Value;

        // Sort by value
        var sorted = pairs.OrderBy(p => p.Value).ToList();
        double totalWeight = sorted.Sum(p => p.Weight);
        double halfWeight = totalWeight / 2.0;

        double cumulative = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            cumulative += sorted[i].Weight;
            if (cumulative >= halfWeight)
            {
                // If we're at exactly half, average with next
                if (i + 1 < sorted.Count && Math.Abs(cumulative - halfWeight) < 0.001)
                {
                    return (sorted[i].Value + sorted[i + 1].Value) / 2.0;
                }
                return sorted[i].Value;
            }
        }

        return sorted[^1].Value;
    }

    // ══════════════════════════════════════════════════════════
    //  ADAPTIVE REALIZATION GAP (V5 — Liquidity-Based)
    //  High data + low CV = liquid market → small gap (asking ≈ selling)
    //  Low data + high CV = illiquid/specific → large gap (big negotiation)
    // ══════════════════════════════════════════════════════════
    private double CalculateAdaptiveGap(
        double coeffVar,
        SearchPhase searchPhase,
        int compCount,
        string priceSource,
        bool isPrimeZoneInvestment = false)
    {
        double gap;

        // ── Liquidity-based core logic ──
        if (compCount >= 8 && coeffVar < 15)
        {
            // HIGH liquidity — active market, consistent prices (e.g. Zagreb/Rijeka apartments)
            gap = -0.08 - (coeffVar / 100.0 * 0.2); // -8% to -10%
        }
        else if (compCount >= 5 && coeffVar < 20)
        {
            // MEDIUM-HIGH liquidity — good data, moderate spread
            gap = -0.10 - (coeffVar / 100.0 * 0.1); // -10% to -12%
        }
        else if (compCount >= 3 && coeffVar < 30)
        {
            // MEDIUM liquidity — enough data, some spread
            gap = -0.12 - (coeffVar / 100.0 * 0.1); // -12% to -15%
        }
        else if (compCount >= 3 && coeffVar >= 30)
        {
            // LOW liquidity, high spread — specific/niche property
            gap = -0.15 - (Math.Min(coeffVar, 50) / 100.0 * 0.2); // -15% to -25%
        }
        else if (compCount < 3 && compCount > 0)
        {
            // VERY LOW liquidity — almost no comparable sales
            gap = -0.18 - (Math.Min(coeffVar, 50) / 100.0 * 0.14); // -18% to -25%
        }
        else
        {
            // No data at all
            gap = -0.20;
        }

        // ── Phase penalty — wider region data = less precise ──
        if (searchPhase >= SearchPhase.SubLocationDrill)
        {
            gap -= 0.04;
        }
        else if (searchPhase >= SearchPhase.SynonymBlitz)
        {
            gap -= 0.02;
        }

        // V26: Prime zone investment properties — cap realization gap at -10%.
        // Commercial properties in city centers (Centar, Stari Grad) generate yield;
        // asking prices are closer to transaction prices in these premium micro-markets.
        if (isPrimeZoneInvestment)
        {
            gap = Math.Max(gap, -0.10); // Never more than -10% for prime zone investments
            _logger.LogInformation("V26: Prime zone investment — realization gap CAPPED at {Gap:P1}", gap);
        }

        // Clamp: never less than -25%, minimum -5%
        return Math.Clamp(gap, -0.25, -0.05);
    }

    private string BuildGapReasoning(double coeffVar, SearchPhase phase, int compCount, double gap)
    {
        var parts = new List<string>();

        if (compCount == 0)
            parts.Add("Nema usporedivih oglasa - nedovoljno podataka za pouzdanu procjenu");
        else if (compCount >= 8 && coeffVar < 15)
            parts.Add($"Visoka likvidnost: {compCount} usporedivih, CV={coeffVar:F1}% - manji diskont (aktivno trziste)");
        else if (compCount >= 5 && coeffVar < 20)
            parts.Add($"Srednje-visoka likvidnost: {compCount} usporedivih, CV={coeffVar:F1}%");
        else if (compCount >= 3 && coeffVar < 30)
            parts.Add($"Srednja likvidnost: {compCount} usporedivih, CV={coeffVar:F1}%");
        else if (compCount >= 3)
            parts.Add($"Niska likvidnost: visok CV ({coeffVar:F1}%) - specificna nekretnina, veci diskont");
        else
            parts.Add($"Vrlo niska likvidnost: samo {compCount} usporediv(a), CV={coeffVar:F1}%");

        if (phase >= SearchPhase.SubLocationDrill)
            parts.Add("Kasna faza pretrazivanja - podaci iz sire regije");
        if (compCount <= 2 && compCount > 0)
            parts.Add($"Samo {compCount} usporediv(a) - ogranicena pouzdanost");

        parts.Add($"Ukupni realizacijski gap: {gap * 100:F1}%");
        return string.Join(". ", parts);
    }

    // ══════════════════════════════════════════════════════════
    //  MATHEMATICAL CONFIDENCE (0–100)
    // ══════════════════════════════════════════════════════════
    private MathConfidence CalculateMathConfidence(
        int compCount, double coeffVar, string priceSource,
        SearchPhase searchPhase, string outlierMethod)
    {
        int score = 100;
        var penalties = new List<string>();

        // Penalty: few comparables
        if (compCount == 0)
        {
            score -= 20;
            penalties.Add("-20: nema cijena iz oglasa");
        }
        else if (compCount < 3)
        {
            score -= 15;
            penalties.Add($"-15: malo usporedivih ({compCount})");
        }
        else if (compCount < 5)
        {
            score -= 5;
            penalties.Add($"-5: umjereno usporedivih ({compCount})");
        }

        // Penalty: high variation
        if (coeffVar > 35)
        {
            score -= 15;
            penalties.Add($"-15: vrlo visok CV ({coeffVar:F1}%)");
        }
        else if (coeffVar > 20)
        {
            score -= 10;
            penalties.Add($"-10: visok CV ({coeffVar:F1}%)");
        }
        else if (coeffVar > 10)
        {
            score -= 3;
            penalties.Add($"-3: umjeren CV ({coeffVar:F1}%)");
        }

        // Penalty: late search phase
        if (searchPhase >= SearchPhase.RegionalFallback)
        {
            score -= 15;
            penalties.Add("-15: regionalni fallback faza");
        }
        else if (searchPhase >= SearchPhase.SubLocationDrill)
        {
            score -= 10;
            penalties.Add("-10: kasna faza pretrazivanja");
        }
        else if (searchPhase >= SearchPhase.SynonymBlitz)
        {
            score -= 5;
            penalties.Add("-5: synonym blitz faza");
        }

        // Penalty: IQR fallback (means IQR was too aggressive)
        if (outlierMethod.Contains("->"))
        {
            score -= 5;
            penalties.Add("-5: IQR fallback koristen");
        }

        // Bonus: many high-quality comps
        if (compCount >= 8 && coeffVar < 15)
        {
            score += 5;
            penalties.Add("+5: bonus za mnogo kvalitetnih usporedivih");
        }

        score = Math.Clamp(score, 0, 100);

        string level = score switch
        {
            >= 75 => "Visoka",
            >= 50 => "Srednja",
            >= 25 => "Niska",
            _ => "Vrlo niska"
        };

        string explanation = penalties.Count > 0
            ? $"Score {score}/100. " + string.Join("; ", penalties)
            : $"Score {score}/100 - izvrsna pouzdanost.";

        return new MathConfidence
        {
            Score = score,
            Level = level,
            Explanation = explanation
        };
    }

    // ══════════════════════════════════════════════════════════
    //  ADJUSTMENTS (with hard cap ±40%)
    // ══════════════════════════════════════════════════════════
    private List<ValuationAdjustment> CalculateAdjustments(
        ExtractedPropertyInfo property,
        MarketResearchResult marketData)
    {
        var adj = new List<ValuationAdjustment>();
        // V22: Include AdditionalNotes and KeyFeatures in the text scan so user-provided
        // info (centar grada, blizu Dioklecijanove palače, etc.) triggers adjustments
        string keyFeaturesText = property.KeyFeatures?.Count > 0
            ? string.Join(" ", property.KeyFeatures) : "";
        string allText = $"{property.Description} {property.Location} {property.Condition} {property.RawText} {property.AdditionalNotes} {keyFeaturesText} {property.MicroLocation}"
            .ToLowerInvariant();

        // V12: Detect market context for context-aware adjustments
        var context = DetectMarketContext(property.Location, marketData.RegionName);
        var targetState = ParseTargetState(property);
        bool isLandProperty = MarketResearchService.IsLandType(marketData.DetectedPropertyType);

        // V25: Detect investment / commercial-potential properties
        bool hasInvestmentPotential = InvestmentKeywords.Any(kw => allText.Contains(kw));

        // ── V22: Center / Premium location ──
        bool isCenter = CenterLocationKeywords.Any(kw => allText.Contains(kw));
        bool hasPremiumLandmark = PremiumLandmarkKeywords.Any(kw => allText.Contains(kw));
        bool isPeriphery = PeripheryKeywords.Any(kw => allText.Contains(kw));
        bool hasPremiumAmenities = PremiumAmenityKeywords.Count(kw => allText.Contains(kw)) >= 2;

        if (isCenter && hasPremiumLandmark)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Premium centar grada",
                Description = "Nekretnina u strogom centru blizu značajnog orijentira — vrhunska mikrolokacija",
                Percent = 20.0
            });
        }
        else if (isCenter)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Centar grada",
                Description = "Nekretnina u centru grada — viša potražnja i cijena",
                Percent = 12.0
            });
        }
        else if (hasPremiumLandmark)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Blizina orijentira",
                Description = "Nekretnina u blizini značajnog gradskog orijentira",
                Percent = 10.0
            });
        }

        if (isPeriphery && !isCenter)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Periferija",
                Description = "Nekretnina na periferiji grada — niža potražnja",
                Percent = -8.0
            });
        }

        if (hasPremiumAmenities && !isPeriphery)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Premium sadržaji u blizini",
                Description = "Više premium sadržaja u neposrednoj blizini (škola, park, javni prijevoz...)",
                Percent = 5.0
            });
        }

        if (allText.Contains("turistič") || allText.Contains("turistic"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Turistička zona",
                Description = "Nekretnina u turističkoj zoni — pojačana turističko-rentalna potražnja",
                Percent = 8.0
            });
        }

        // V25: Investment / commercial-potential property premium
        if (hasInvestmentPotential && !isLandProperty)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Investicijski potencijal",
                Description = "Nekretnina s komercijalnim/turističkim potencijalom (hotel, apartmani, prenamjena) — vrijednost lokacije dominira nad stanjem objekta",
                Percent = 15.0
            });
        }

        // ── First row to sea ──
        if (FirstRowKeywords.Any(kw => allText.Contains(kw, StringComparison.OrdinalIgnoreCase)))
        {
            bool isDubrovnikOrHvar = allText.Contains("dubrovnik") || allText.Contains("hvar");
            double seaAdj = isDubrovnikOrHvar ? 50.0 : 25.0;
            adj.Add(new ValuationAdjustment
            {
                Factor = "Prvi red do mora",
                Description = isDubrovnikOrHvar
                    ? "Premium lokacija — Dubrovnik/Hvar, 1. red"
                    : "Nekretnina u prvom redu do mora",
                Percent = seaAdj
            });
        }
        else if (allText.Contains("blizu mora") || allText.Contains("blizina mora")
              || allText.Contains("200m") || allText.Contains("300m"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Blizina mora",
                Description = "Nekretnina blizu mora (ne prvi red)",
                Percent = 10.0
            });
        }

        // ── Novogradnja (buildings only — land has no condition) ──
        if (!isLandProperty
            && (allText.Contains("novograd") || allText.Contains("novogradnja")
                || allText.Contains("novouređ") || allText.Contains("novo izgrad")))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Novogradnja",
                Description = "Novogradnja — premium cijena",
                Percent = 15.0
            });
        }

        // ── Needs renovation (buildings only) ──
        // V11: Only apply if subject is NOT NeedsWork/Original — those cases are
        // already handled by the Twin Comparison renovation deduction.
        bool conditionAlreadyAdjusted = targetState == PropertyCondition.NeedsWork
                                      || targetState == PropertyCondition.Original;
        if (!isLandProperty && !conditionAlreadyAdjusted
            && (allText.Contains("potrebna adaptac") || allText.Contains("potrebna obnova")
                || allText.Contains("ruševin") || allText.Contains("zapušten")))
        {
            // V25: Cap renovation penalty for investment/premium properties
            double renoPenalty = hasInvestmentPotential ? -5.0 : -15.0;
            adj.Add(new ValuationAdjustment
            {
                Factor = "Potrebna adaptacija",
                Description = hasInvestmentPotential
                    ? "Nekretnina zahtijeva adaptaciju (umanjeno – investicijski potencijal)"
                    : "Nekretnina zahtijeva značajnu adaptaciju",
                Percent = renoPenalty
            });
        }
        else if (!isLandProperty
              && (allText.Contains("adaptiran") || allText.Contains("renoviran")
                  || allText.Contains("obnovljen")))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Adaptirana nekretnina",
                Description = "Nekretnina je adaptirana/renovirana",
                Percent = 8.0
            });
        }

        // ── V12: Orientation penalty (coastal only, complements per-comp adjustment) ──
        // This is a SUBJECT-LEVEL adjustment, not per-comp. Applied when subject has
        // unfavorable orientation and the comps are a MIX (not all same orientation).
        if (context == MarketContext.Coastal)
        {
            bool isNorth = allText.Contains("sjever") || allText.Contains("orijentacija: sjever")
                        || allText.Contains("sjeverna");
            bool noView = !allText.Contains("pogled na more") && !allText.Contains("sea view")
                        && !allText.Contains("panoram");
            bool isStreetView = allText.Contains("pogled na ulicu") || allText.Contains("ulični")
                             || allText.Contains("bez pogleda") || allText.Contains("dvorište");

            if (isNorth && noView)
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Sjever bez pogleda",
                    Description = "Sjeverna orijentacija bez pogleda u obalnom gradu — značajna penalizacija",
                    Percent = -10.0
                });
            }
            else if (isNorth)
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Sjeverna orijentacija",
                    Description = "Sjeverna orijentacija — manje sunca, niža potražnja na obali",
                    Percent = -6.0
                });
            }
            else if (isStreetView && noView)
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Bez pogleda",
                    Description = "Pogled na ulicu/dvorište bez mora — penalizacija u obalnom gradu",
                    Percent = -5.0
                });
            }
        }

        // ── Island premium ──
        if (allText.Contains("otok") || allText.Contains("otočn"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Otočna lokacija",
                Description = "Otočna nekretnina — ograničena ponuda",
                Percent = 8.0
            });
        }

        // ── Small apartment (<35m²) ──
        if (property.SizeM2 > 0 && property.SizeM2 < 35
            && marketData.DetectedPropertyType.Contains("stan", StringComparison.OrdinalIgnoreCase))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Mali stan",
                Description = "Stanovi <35 m² imaju veću cijenu po m²",
                Percent = 7.0
            });
        }

        // ── Very large property (>200m²) — NOT for land ──
        // V26: Skip for investment/commercial properties — they generate yield, not discount.
        bool isLandType = MarketResearchService.IsLandType(marketData.DetectedPropertyType);
        if (property.SizeM2 > 200 && !isLandType && !hasInvestmentPotential)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Velika nekretnina",
                Description = "Nekretnine >200 m² obično imaju niži €/m²",
                Percent = -7.0
            });
        }

        // ── Bad infrastructure ──
        if (allText.Contains("neasfalt") || allText.Contains("bez pristup")
            || allText.Contains("makadamsk") || allText.Contains("šumski put"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Loša infrastruktura",
                Description = "Neasfaltirani pristup / loša infrastruktura",
                Percent = -10.0
            });
        }

        // ── Legalization issues ──
        if (allText.Contains("legaliz") || allText.Contains("bespravna")
            || allText.Contains("nelegalizir") || allText.Contains("bez dozvol"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Legalizacija",
                Description = "Neriješena legalizacija — pravni rizik",
                Percent = -12.0
            });
        }

        // ── V12: Parking/Garaža — REMOVED from contextual adjustments ──
        // Garage and parking are now handled per-comp in Twin Comparison (section 4a+).
        // Adding a blanket +5% here would DOUBLE-DIP with the per-comp flat EUR deduction.

        // ── Land — agricultural vs building ──
        // V14: Only apply the -35% agricultural discount when comparables include
        // building-land prices. When the search already found ag-land comparables
        // (median < 15 €/m²), the prices are already agricultural — no need to discount.
        if (marketData.DetectedPropertyType.Contains("zemljišt", StringComparison.OrdinalIgnoreCase)
            || marketData.DetectedPropertyType.Contains("parcela", StringComparison.OrdinalIgnoreCase))
        {
            bool isAgri = allText.Contains("poljopriv") || allText.Contains("oranica")
                || allText.Contains("šuma") || allText.Contains("pašnjak")
                || allText.Contains("vinograd") || allText.Contains("voćnjak")
                || allText.Contains("livada") || allText.Contains("mješovit");

            if (isAgri)
            {
                // Check if comps are already ag-priced (median < 15 €/m²)
                // If so, skip the discount — it would double-dip.
                // Marker: median is read from MarketAverageAsking set during Calculate().
                // We pass a note so the pipeline knows.
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Poljoprivredno zemljište",
                    Description = "Podatci iz oglasa već odražavaju poljoprivredne cijene — bez dodatnog diskonta",
                    Percent = 0.0
                });
            }
            else if (allText.Contains("građevinsk") || allText.Contains("urbaniziran"))
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Građevinsko zemljište",
                    Description = "Građevinsko zemljište s dozvolom",
                    Percent = 5.0
                });
            }
        }

        return adj;
    }

    // ══════════════════════════════════════════════════════════
    //  V20: PER-COMPARABLE CORRECTIONS (usporedna metoda)
    //  Each comparable is individually adjusted based on how it
    //  differs from the subject property. This is the standard
    //  Croatian comparative valuation methodology.
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate the correction percentage for a single comparable relative to the subject.
    /// Positive = comparable is WORSE than subject → its price is adjusted UP.
    /// Negative = comparable is BETTER than subject → its price is adjusted DOWN.
    /// </summary>
    private static (double percent, string reason) CalculatePerCompCorrection(
        ExtractedPropertyInfo subject,
        ComparableSearchResult? listing,
        ExtractedPrice price,
        string detectedType)
    {
        double correction = 0;
        var reasons = new List<string>();
        bool isLand = MarketResearchService.IsLandType(detectedType);

        // ── 1. SIZE ADJUSTMENT ──
        // Larger properties tend to have lower €/m², smaller → higher €/m².
        // If comp is bigger than subject, its €/m² is naturally lower → adjust UP.
        double subjectSize = subject.SizeM2;
        double compSize = listing?.AiSizeM2 ?? price.DetectedSizeM2;
        if (subjectSize > 10 && compSize > 10)
        {
            double sizeRatio = compSize / subjectSize;
            if (sizeRatio > 1.25)
            {
                // Comp is >25% larger → lower €/m² → adjust UP
                double adj = Math.Min((sizeRatio - 1.0) * 10, 8);
                correction += adj;
                reasons.Add($"Veći +{adj:F0}%");
            }
            else if (sizeRatio < 0.75)
            {
                // Comp is >25% smaller → higher €/m² → adjust DOWN
                double adj = Math.Max((sizeRatio - 1.0) * 10, -8);
                correction += adj;
                reasons.Add($"Manji {adj:F0}%");
            }
        }

        // ── 2. CONDITION ADJUSTMENT (buildings only) ──
        // V29 Sherlock: "Text Authority Rule" — Sherlock's RenovationStatus overrides scraper's AiCondition
        if (!isLand)
        {
            string? effectiveCondition = listing?.AiCondition;
            if (listing?.SherlockAttributes != null &&
                listing.SherlockAttributes.RenovationStatus != Models.RenovationStatus.Unknown)
            {
                effectiveCondition = listing.SherlockAttributes.RenovationStatus switch
                {
                    Models.RenovationStatus.New => "new",
                    Models.RenovationStatus.Renovated => "renovated",
                    Models.RenovationStatus.Livable => "good",
                    Models.RenovationStatus.NeedsAdaptation => "needs_work",
                    Models.RenovationStatus.Ruin => "needs_work",
                    _ => effectiveCondition
                };
            }

            int subjectCond = ParseConditionLevel(subject.PropertyState);
            int compCond = ParseConditionLevel(effectiveCondition);
            if (subjectCond > 0 && compCond > 0 && subjectCond != compCond)
            {
                int condDiff = compCond - subjectCond; // positive = comp is BETTER
                double adj = -condDiff * 5.0; // better comp → negative correction (reduce price)
                adj = Math.Clamp(adj, -15, 15);
                correction += adj;
                string compCondLabel = ConditionLabel(listing?.AiCondition);
                reasons.Add(adj > 0
                    ? $"Lošije stanje ({compCondLabel}) +{adj:F0}%"
                    : $"Bolje stanje ({compCondLabel}) {adj:F0}%");
            }
        }

        // ── 3. AGE ADJUSTMENT (buildings only) ──
        if (!isLand && listing?.AiYearBuilt > 0)
        {
            // Estimate subject year from description
            int subjectYear = EstimateYearBuilt(subject);
            if (subjectYear > 0)
            {
                int yearDiff = (listing.AiYearBuilt ?? 0) - subjectYear; // positive = comp is NEWER
                if (Math.Abs(yearDiff) >= 15)
                {
                    double adj = -(yearDiff / 10.0) * 3.0; // newer comp → negative correction
                    adj = Math.Clamp(adj, -8, 8);
                    correction += adj;
                    reasons.Add(adj > 0
                        ? $"Starije ({listing.AiYearBuilt}) +{adj:F0}%"
                        : $"Novije ({listing.AiYearBuilt}) {adj:F0}%");
                }
            }
        }

        // ── 4. GARAGE ADJUSTMENT (buildings only) ──
        // V29 Sherlock: SherlockAttributes.HasGarage overrides scraper's HasGarage
        if (!isLand)
        {
            bool subjectHasGarage = subject.Description != null &&
                (subject.Description.Contains("garaž", StringComparison.OrdinalIgnoreCase) ||
                 subject.Description.Contains("garaz", StringComparison.OrdinalIgnoreCase));
            bool compHasGarage = listing?.SherlockAttributes?.HasGarage == true
                || listing?.HasGarage == true || price.HasGarage;

            if (compHasGarage && !subjectHasGarage)
            {
                correction -= 3;
                reasons.Add("Garaža -3%");
            }
            else if (!compHasGarage && subjectHasGarage)
            {
                correction += 3;
                reasons.Add("Bez garaže +3%");
            }
        }

        // ── 5. YARD ADJUSTMENT ──
        if (!isLand)
        {
            double subjectYard = subject.YardAreaM2;
            double compYard = listing?.AiYardAreaM2 ?? price.DetectedYardM2;
            bool subjectHasYard = subjectYard > 20;
            bool compHasYard = compYard > 20;

            if (compHasYard && !subjectHasYard)
            {
                correction -= 3;
                reasons.Add("Okućnica -3%");
            }
            else if (!compHasYard && subjectHasYard)
            {
                correction += 3;
                reasons.Add("Bez okućnice +3%");
            }
        }

        // ── 6. COMMERCIAL POTENTIAL ADJUSTMENT (Super-Extractor) ──
        if (listing?.SherlockAttributes != null)
        {
            var sa = listing.SherlockAttributes;

            // Commercial potential → comp has commercial value the subject doesn't → adjust DOWN
            if (sa.CommercialPotential != "None" && !string.IsNullOrEmpty(sa.CommercialPotential))
            {
                correction -= 15;
                reasons.Add($"Komercijalni potencijal ({sa.CommercialPotential}) -15%");
            }

            // Major damages → structural problems devalue the comp → adjust UP (comp is worse)
            if (sa.MajorDamages.Any(d => d is "StaticIssue" or "RoofLeak"))
            {
                correction += 20;
                reasons.Add("Oštećenja +20%");
            }

            // Legal risk: legalization needed → significant discount
            if (sa.LegalStatus == Models.LegalStatus.LegalizationNeeded)
            {
                correction += 30;
                reasons.Add("Legalizacija +30%");
            }
        }

        // ── 7. ELEVATOR ADJUSTMENT (apartments, Floor > 3 without elevator) ──
        if (!isLand && listing?.SherlockAttributes != null)
        {
            var sa = listing.SherlockAttributes;
            // Parse comp floor from AiFloor string
            int compFloor = ParseFloorNumber(listing.AiFloor);
            if (compFloor > 3 && !sa.HasElevator)
            {
                correction += 10;
                reasons.Add($"Bez dizala ({compFloor}. kat) +10%");
            }
        }

        // ── CAP: ±35% (raised from ±25% for Super-Extractor rules) ──
        correction = Math.Clamp(correction, -35, 35);
        string reasonStr = reasons.Count > 0 ? string.Join(", ", reasons) : "";
        return (Math.Round(correction, 1), reasonStr);
    }

    /// <summary>
    /// Parse condition string to numeric level for comparison.
    /// 1=needs_work, 2=original, 3=good, 4=renovated, 5=new.
    /// Returns 0 if unknown.
    /// </summary>
    private static int ParseConditionLevel(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return 0;
        var lower = condition.ToLowerInvariant();
        return lower switch
        {
            "needs_work" or "needs_adaptation" or "za adaptaciju" or "zapušteno" => 1,
            "original" or "izvorno" or "izvorno stanje" => 2,
            "good" or "dobro" or "održavano" or "useljivo" => 3,
            "renovated" or "adaptirano" or "renovirano" or "obnovljeno" => 4,
            "new" or "novogradnja" or "novo" or "novoizgrađeno" => 5,
            _ => 0
        };
    }

    /// <summary>Human-readable Croatian condition label.</summary>
    private static string ConditionLabel(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return "nepoznato";
        return condition.ToLowerInvariant() switch
        {
            "needs_work" => "za adaptaciju",
            "original" => "izvorno",
            "good" => "dobro",
            "renovated" => "renovirano",
            "new" => "novogradnja",
            _ => condition
        };
    }

    /// <summary>
    /// Parse floor number from AiFloor string (e.g. "3", "4. kat", "prizemlje" → 0).
    /// Returns 0 if unknown or ground floor.
    /// </summary>
    private static int ParseFloorNumber(string? floor)
    {
        if (string.IsNullOrWhiteSpace(floor)) return 0;
        var lower = floor.Trim().ToLowerInvariant();
        if (lower is "prizemlje" or "suteren" or "ground" or "p" or "0") return 0;
        // Extract first number from strings like "3", "4. kat", "5/8"
        var match = System.Text.RegularExpressions.Regex.Match(lower, @"\d+");
        return match.Success && int.TryParse(match.Value, out var n) ? n : 0;
    }

    /// <summary>Try to estimate the subject property's year of construction from description text.</summary>
    private static int EstimateYearBuilt(ExtractedPropertyInfo property)
    {
        // V22: Include AdditionalNotes for year detection from user notes
        var text = $"{property.Description} {property.RawText} {property.AdditionalNotes}";
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:god(?:ina)?\.?\s*(?:izgradnje|gradnje)?[:\s]*|izgrađen[ao]?\s+)(\d{4})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int year) && year >= 1900 && year <= 2030)
            return year;
        return 0;
    }

    // ══════════════════════════════════════════════════════════
    //  AGRICULTURAL LAND DETECTION
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// Returns true if the property is agricultural, forest, pasture, or mixed land
    /// (as opposed to building/građevinsko land). These have radically different
    /// price ranges: 1-10 €/m² vs 30-150+ €/m² for building land.
    /// </summary>
    private static bool IsAgriculturalLand(string detectedType, ExtractedPropertyInfo property)
    {
        // V22: Include AdditionalNotes for agricultural land detection
        string combined = $"{detectedType} {property.Description} {property.Condition} {property.RawText} {property.AdditionalNotes}"
            .ToLowerInvariant();

        // Definite agricultural indicators
        bool hasAgriKeyword = combined.Contains("poljopriv") || combined.Contains("oranica")
            || combined.Contains("pašnjak") || combined.Contains("pasnjak")
            || combined.Contains("šumsko") || combined.Contains("sumsko")
            || combined.Contains("šuma") || combined.Contains("voćnjak") || combined.Contains("vocnjak")
            || combined.Contains("vinograd") || combined.Contains("mješovit") || combined.Contains("mjesovit")
            || combined.Contains("livada") || combined.Contains("neplodno");

        // Definite building land indicators (override)
        bool isBuildingLand = combined.Contains("građevinsk") || combined.Contains("gradjevinsk")
            || combined.Contains("urbaniziran") || combined.Contains("građevinska dozvola")
            || combined.Contains("gradska zona") || combined.Contains("stambena zona");

        return hasAgriKeyword && !isBuildingLand;
    }

    // ══════════════════════════════════════════════════════════
    //  PRICE EXTRACTION
    // ══════════════════════════════════════════════════════════
    private List<ExtractedPrice> ExtractPricesFromListings(
        List<ComparableSearchResult> listings,
        double targetSizeM2,
        bool isLand = false,
        bool isAgriLand = false)
    {
        var prices = new List<ExtractedPrice>();
        double minPpm2 = isAgriLand ? MinPricePerM2_AgriLand : (isLand ? MinPricePerM2_BuildLand : MinPricePerM2_Building);
        double minTotal = isAgriLand ? MinSalePriceEur_AgriLand : (isLand ? MinSalePriceEur_BuildLand : MinSalePriceEur_Building);
        double maxPpm2 = isLand ? 1500 : 15000;  // Land rarely above 1500 €/m²

        foreach (var listing in listings)
        {
            string text = $"{listing.Title} {listing.Content}";

            // ── RENT-TRAP: Skip rental listings ──
            if (IsRentalListing(text))
                continue;

            // ── V28: ALWAYS compute PricePerM2 = ListedPriceEur / Size when both are available.
            // This ensures the table's TotalPrice / AreaM2 = RawPricePerM2 (no math disconnect).
            // Only fall back to ListedPricePerM2 when total price is unknown.
            // Previous V24 Tier 1 used ListedPricePerM2 directly from the portal, but that
            // was computed using the LISTING's own area (e.g. 225m²), which doesn't match
            // the AreaM2 shown in the table → client sees 599,000 / 169 ≠ 2,665. ──

            // Tier 1: ListedPriceEur + known size → compute PricePerM2
            if (listing.ListedPriceEur > 0)
            {
                double sizeForCalc = listing.AiSizeM2 > 0 ? listing.AiSizeM2.Value : ExtractMainSize(text);
                if (sizeForCalc <= 0 && targetSizeM2 > 0) sizeForCalc = targetSizeM2;

                if (sizeForCalc > 0)
                {
                    double calcPpm2 = listing.ListedPriceEur.Value / sizeForCalc;
                    if (calcPpm2 >= minPpm2 && calcPpm2 <= maxPpm2 && listing.ListedPriceEur.Value >= minTotal)
                    {
                        prices.Add(new ExtractedPrice
                        {
                            PricePerM2 = calcPpm2,
                            SourceTitle = listing.Title,
                            SourceUrl = listing.Url,
                            DetectedCondition = ParseAiCondition(listing.AiCondition) ?? DetectConditionFromText(text),
                            DetectedYardM2 = listing.AiYardAreaM2 > 0 ? listing.AiYardAreaM2.Value : ExtractYardArea(text),
                            HasGarage = listing.HasGarage ?? DetectFeature(text, GarageKeywords),
                            HasRenovation = listing.HasRenovation ?? listing.IsRenovated ?? DetectFeature(text, RenovationKeywords),
                            HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords),
                            HasParking = DetectFeature(text, ParkingKeywords),
                            HasSeaView = DetectFeature(text, SeaViewKeywords),
                            IsNorthFacing = DetectFeature(text, NorthFacingKeywords),
                            IsSouthFacing = DetectFeature(text, SouthFacingKeywords),
                            DetectedSizeM2 = sizeForCalc
                        });
                        continue;
                    }
                }
            }

            // Tier 2: ListedPricePerM2 only (no total price available) — use portal's €/m²
            if (listing.ListedPricePerM2 > 0 && listing.ListedPricePerM2 >= minPpm2 && listing.ListedPricePerM2 <= maxPpm2)
            {
                prices.Add(new ExtractedPrice
                {
                    PricePerM2 = listing.ListedPricePerM2.Value,
                    SourceTitle = listing.Title,
                    SourceUrl = listing.Url,
                    DetectedCondition = ParseAiCondition(listing.AiCondition) ?? DetectConditionFromText(text),
                    DetectedYardM2 = listing.AiYardAreaM2 > 0 ? listing.AiYardAreaM2.Value : ExtractYardArea(text),
                    HasGarage = listing.HasGarage ?? DetectFeature(text, GarageKeywords),
                    HasRenovation = listing.HasRenovation ?? listing.IsRenovated ?? DetectFeature(text, RenovationKeywords),
                    HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords),
                    HasParking = DetectFeature(text, ParkingKeywords),
                    HasSeaView = DetectFeature(text, SeaViewKeywords),
                    IsNorthFacing = DetectFeature(text, NorthFacingKeywords),
                    IsSouthFacing = DetectFeature(text, SouthFacingKeywords),
                    DetectedSizeM2 = listing.AiSizeM2 > 0 ? listing.AiSizeM2.Value : 0
                });
                continue;
            }

            // ── FALLBACK: Regex extraction from content text (original behavior) ──
            // Try price per m² first (most reliable)
            var ppm2Match = PricePerM2Regex.Match(text);
            if (ppm2Match.Success)
            {
                double ppm2 = ParseCroatianNumber(ppm2Match.Groups[1].Value);
                if (ppm2 >= minPpm2 && ppm2 <= maxPpm2)
                {
                    prices.Add(new ExtractedPrice
                    {
                        PricePerM2 = ppm2,
                        SourceTitle = listing.Title,
                        SourceUrl = listing.Url,
                        DetectedCondition = ParseAiCondition(listing.AiCondition) ?? DetectConditionFromText(text),
                        DetectedYardM2 = listing.AiYardAreaM2 > 0 ? listing.AiYardAreaM2.Value : ExtractYardArea(text),
                        HasGarage = listing.HasGarage ?? DetectFeature(text, GarageKeywords),
                        HasRenovation = listing.HasRenovation ?? listing.IsRenovated ?? DetectFeature(text, RenovationKeywords),
                        HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords),
                        HasParking = DetectFeature(text, ParkingKeywords),
                        HasSeaView = DetectFeature(text, SeaViewKeywords),
                        IsNorthFacing = DetectFeature(text, NorthFacingKeywords),
                        IsSouthFacing = DetectFeature(text, SouthFacingKeywords),
                        DetectedSizeM2 = listing.AiSizeM2 > 0 ? listing.AiSizeM2.Value : 0
                    });
                    continue;
                }
            }

            // Try absolute price + size → calculate per m²
            var priceMatches = PriceRegex.Matches(text);

            // V11: Smart size extraction — skip garage/parking/terrace sizes.
            // Find the LARGEST plausible m² in the text (the main living area),
            // not the first one (which could be "garaža 15 m²").
            double extractedSize = ExtractMainSize(text);

            foreach (Match pm in priceMatches)
            {
                double price = ParseCroatianNumber(pm.Groups[1].Value);

                // Determine size
                double size = extractedSize;
                if (size <= 0 && targetSizeM2 > 0)
                    size = targetSizeM2;

                if (size > 0 && price > 0)
                {
                    // ── RENT-TRAP: Reject total price below floor ──
                    // For ag land, floor is 500€ — small parcels are valid sales.
                    // For buildings, floor is 35k€ — anything below is likely rent.
                    if (price >= minTotal * 0.5 && price < minTotal)
                        continue; // Likely monthly rent or garbage data

                    double ppm2;
                    if (!isLand && price < 1000)
                    {
                        // Buildings only: below €1000 treat as price/m² (not total price)
                        ppm2 = price;
                    }
                    else if (isLand && price < minTotal)
                    {
                        // Land: below minTotal, skip — too low to be a real sale
                        continue;
                    }
                    else
                    {
                        ppm2 = price / size;
                    }

                    if (ppm2 >= minPpm2 && ppm2 <= maxPpm2)
                    {
                        prices.Add(new ExtractedPrice
                        {
                            PricePerM2 = ppm2,
                            SourceTitle = listing.Title,
                            SourceUrl = listing.Url,
                            DetectedCondition = ParseAiCondition(listing.AiCondition) ?? DetectConditionFromText(text),
                            DetectedYardM2 = listing.AiYardAreaM2 > 0 ? listing.AiYardAreaM2.Value : ExtractYardArea(text),
                            HasGarage = listing.HasGarage ?? DetectFeature(text, GarageKeywords),
                            HasRenovation = listing.HasRenovation ?? listing.IsRenovated ?? DetectFeature(text, RenovationKeywords),
                            HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords),
                            HasParking = DetectFeature(text, ParkingKeywords),
                            HasSeaView = DetectFeature(text, SeaViewKeywords),
                            IsNorthFacing = DetectFeature(text, NorthFacingKeywords),
                            IsSouthFacing = DetectFeature(text, SouthFacingKeywords),
                            DetectedSizeM2 = listing.AiSizeM2 > 0 ? listing.AiSizeM2.Value : extractedSize
                        });
                        break; // One price per listing
                    }
                }
            }
        }

        // ── V28: DEDUPLICATION — remove duplicate listings (same property, different agencies) ──
        // If two comps have the same €/m² (within 3%) AND same area (within 5%), keep only the first.
        // This prevents the same property listed by 2 agencies from artificially inflating the sample.
        if (prices.Count >= 2)
        {
            var deduped = new List<ExtractedPrice>();
            foreach (var p in prices)
            {
                bool isDuplicate = deduped.Any(existing =>
                {
                    // Same €/m² within 3%
                    double priceDiff = Math.Abs(existing.PricePerM2 - p.PricePerM2);
                    bool priceClose = existing.PricePerM2 > 0 && priceDiff / existing.PricePerM2 < 0.03;

                    // Same area within 5%
                    bool areaClose = false;
                    if (existing.DetectedSizeM2 > 0 && p.DetectedSizeM2 > 0)
                    {
                        double areaDiff = Math.Abs(existing.DetectedSizeM2 - p.DetectedSizeM2);
                        areaClose = areaDiff / existing.DetectedSizeM2 < 0.05;
                    }

                    return priceClose && areaClose;
                });

                if (!isDuplicate)
                    deduped.Add(p);
                else
                    _logger.LogInformation("V28 Dedup: removed duplicate listing '{Title}' ({Ppm2:F0} €/m², {Size:F0} m²)",
                        p.SourceTitle.Length > 40 ? p.SourceTitle[..40] : p.SourceTitle, p.PricePerM2, p.DetectedSizeM2);
            }

            if (deduped.Count < prices.Count)
                _logger.LogInformation("V28 Dedup: {Removed} duplicates removed, {Remaining} unique comps",
                    prices.Count - deduped.Count, deduped.Count);

            prices = deduped;
        }

        return prices;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER: Croatian number parsing
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// Croatian format: dot = thousands separator, comma = decimal.
    ///   "1.088"   → 1088  (thousands separator)
    ///   "150.000" → 150000
    ///   "2.500,50" → 2500.50
    ///   "1088"    → 1088  (no separator)
    /// </summary>
    internal static double ParseCroatianNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        raw = raw.Trim().Replace(" ", "").Replace("€", "").Replace("EUR", "");

        bool hasComma = raw.Contains(',');
        bool hasDot = raw.Contains('.');

        if (hasComma && hasDot)
        {
            // 2.500,50 → dot is thousands, comma is decimal
            raw = raw.Replace(".", "").Replace(",", ".");
        }
        else if (hasDot)
        {
            // Check: "1.088" or "1088.5"?
            var dotParts = raw.Split('.');
            if (dotParts.Length == 2 && dotParts[1].Length == 3)
            {
                // "1.088" → thousands separator → 1088
                raw = raw.Replace(".", "");
            }
            // else "1088.5" stays as-is (decimal dot)
        }
        else if (hasComma)
        {
            raw = raw.Replace(",", ".");
        }

        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double val) ? val : 0;
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER: Percentile (linear interpolation)
    // ══════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════
    //  V13: SHERLOCK INSIGHT — Explain quality controls to the user
    // ══════════════════════════════════════════════════════════
    private static string BuildSherlockInsight(
        MarketResearchResult marketData,
        int usedCompsCount,
        List<UsedComparable> compDetails,
        int outliersRemoved)
    {
        var parts = new List<string>();

        // 1. City Integrity report
        if (marketData.CityIntegrityRejections > 0 && marketData.RejectedCityNames.Count > 0)
        {
            string cities = string.Join(", ", marketData.RejectedCityNames.Select(c =>
                char.ToUpper(c[0]) + c[1..]));
            parts.Add(
                $"Sustav je automatski odbacio {marketData.CityIntegrityRejections} oglas(a) iz okolnih gradova " +
                $"({cities}) jer ne zadovoljavaju kriterij mikrolokacije, osiguravajući da se procjena " +
                $"temelji isključivo na tržištu {marketData.RegionName}.");
        }

        // 2. Outlier removal
        if (outliersRemoved > 0)
        {
            parts.Add(
                $"Statistički filter uklonio je {outliersRemoved} oglasa sa cijenama koje " +
                "značajno odstupaju od lokalnog tržišta (IQR metoda).");
        }

        // 3. Similarity floor
        int belowFloor = compDetails.Count(c => c.IsOutlier && c.SimilarityScore < 0.50);
        if (belowFloor > 0)
        {
            parts.Add(
                $"Dodatno je {belowFloor} oglas(a) isključeno jer sličnost s predmetnom " +
                "nekretninom ne prelazi minimalni prag od 50%.");
        }

        // 4. Final comp count
        if (usedCompsCount > 0)
        {
            parts.Add(
                $"Konačna procjena temelji se na {usedCompsCount} pažljivo odabran(ih) " +
                "usporedivih nekretnina s iste mikrolokacije.");
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Svi oglasi prošli su kontrolu kvalitete i lokacije bez potrebe za posebnim intervencijama.";
    }

    private static double Percentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        double index = (percentile / 100.0) * (sortedValues.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);

        if (lower == upper || upper >= sortedValues.Count)
            return sortedValues[lower];

        double fraction = index - lower;
        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }

    // ══════════════════════════════════════════════════════════
    //  HELPER: Extract size from text
    // ══════════════════════════════════════════════════════════
    private double ExtractSize(string text)
    {
        var match = SizeRegex.Match(text);
        if (match.Success)
            return ParseCroatianNumber(match.Groups[1].Value);
        return 0;
    }

    /// <summary>
    /// V11: Smart size extraction — finds the MAIN living area from listing text,
    /// skipping garage/parking/terrace/balcony sizes that often appear first.
    /// Strategy: collect ALL m² numbers, discard those preceded by garage/parking keywords,
    /// then return the LARGEST plausible one (the main property area).
    /// </summary>
    // V17: Croatian thousands-separator support (e.g. "4.327,00 m²" → 4327)
    private static readonly Regex SizeContextRegex = new(
        @"(?<prefix>.{0,40}?)(\d{1,6}(?:\.\d{3})*(?:,\d+)?)\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] GarageSizePrefixes =
        ["garaž", "garaz", "parking", "pm ", "p.m.", "gpm", "garsonj",
         "terasa", "balkon", "lođa", "lodja", "spremišt", "spremist",
         "podrum", "tavan"];

    private double ExtractMainSize(string text)
    {
        var matches = SizeContextRegex.Matches(text);
        double bestSize = 0;

        foreach (Match m in matches)
        {
            string prefix = m.Groups["prefix"].Value.ToLowerInvariant();
            double size = ParseCroatianNumber(m.Groups[2].Value);

            // Skip sizes that are clearly not the main property area
            if (size <= 0 || size > 50_000) continue;

            // Skip if prefix mentions garage/parking/terrace/etc.
            bool isAncillary = GarageSizePrefixes.Any(kw => prefix.Contains(kw));
            if (isAncillary && size < 100) continue; // Only skip if small (<100m²)

            // Take the largest non-ancillary size (the main living area)
            if (size > bestSize)
                bestSize = size;
        }

        // Fallback: if nothing found with context, use simple first match
        if (bestSize <= 0)
            bestSize = ExtractSize(text);

        return bestSize;
    }

    private double EstimateSizeFromContext(ExtractedPropertyInfo property)
    {
        // V22: Include AdditionalNotes for size detection from user notes
        string text = $"{property.Description} {property.RawText} {property.AdditionalNotes}";
        double size = ExtractSize(text);
        return size > 0 ? size : 50; // Default assumption
    }

    private static SearchPhase ParseSearchPhase(string phaseStr)
    {
        return Enum.TryParse<SearchPhase>(phaseStr, true, out var phase)
            ? phase
            : SearchPhase.Primary;
    }

    // ══════════════════════════════════════════════════════════
    //  CONDITION DETECTION from listing text
    // ══════════════════════════════════════════════════════════
    private static PropertyCondition DetectConditionFromText(string text)
    {
        var lower = text.ToLowerInvariant();
        if (NewKeywords.Any(kw => lower.Contains(kw))) return PropertyCondition.New;
        if (RenovatedKeywords.Any(kw => lower.Contains(kw))) return PropertyCondition.Renovated;
        if (NeedsWorkKeywords.Any(kw => lower.Contains(kw))) return PropertyCondition.NeedsWork;
        if (GoodKeywords.Any(kw => lower.Contains(kw))) return PropertyCondition.Good;
        return PropertyCondition.Unknown;
    }

    /// <summary>
    /// Convert AI-extracted condition string to PropertyCondition enum.
    /// Returns null if the string is unrecognized so the caller can fall back.
    /// </summary>
    private static PropertyCondition? ParseAiCondition(string? aiCondition)
    {
        if (string.IsNullOrWhiteSpace(aiCondition)) return null;
        return aiCondition.Trim().ToLowerInvariant() switch
        {
            "new"        => PropertyCondition.New,
            "renovated"  => PropertyCondition.Renovated,
            "good"       => PropertyCondition.Good,
            "original"   => PropertyCondition.Original,
            "needs_work" => PropertyCondition.NeedsWork,
            _            => null
        };
    }

    private static PropertyCondition ParseTargetState(ExtractedPropertyInfo property)
    {
        // Use the AI-extracted property_state first, fall back to condition text
        if (!string.IsNullOrWhiteSpace(property.PropertyState))
        {
            return property.PropertyState.ToLowerInvariant() switch
            {
                "new" => PropertyCondition.New,
                "renovated" => PropertyCondition.Renovated,
                "good" => PropertyCondition.Good,
                "original" => PropertyCondition.Original,
                "needs_adaptation" => PropertyCondition.NeedsWork,
                _ => DetectConditionFromText(property.Condition + " " + property.Description)
            };
        }
        // V22: Include AdditionalNotes for condition detection from user notes
        return DetectConditionFromText(
            property.Condition + " " + property.Description + " " + property.RawText + " " + property.AdditionalNotes);
    }

    private static double ExtractYardArea(string text)
    {
        var match = YardRegex.Match(text);
        if (match.Success)
            return ParseCroatianNumber(match.Groups[1].Value);
        return 0;
    }

    /// <summary>
    /// Detect if a listing contains ANY keyword from the given set.
    /// </summary>
    private static bool DetectFeature(string text, string[] keywords)
    {
        var lower = text.ToLowerInvariant();
        foreach (var kw in keywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    /// <summary>
    /// V7 Per-Rank Condition Adjustment.
    /// Subject worse than comp → -15% per rank difference (comp price too high for us).
    /// Same condition → 0%.
    /// Subject better than comp → +10% per rank difference (comp understates our value).
    /// Scale: NeedsWork=0, Original=1, Good=2, Renovated=3, New=4 (5 ranks).
    /// </summary>
    private static double ConditionAdjustmentFactor(
        PropertyCondition targetState, PropertyCondition compState)
    {
        if (targetState == compState) return 0;
        if (targetState == PropertyCondition.Unknown || compState == PropertyCondition.Unknown)
            return 0;

        int targetRank = StateRank(targetState);
        int compRank = StateRank(compState);
        int diff = Math.Abs(targetRank - compRank);

        // V11: -10% per rank when target worse, +7% when better (was -15/+10 — too aggressive)
        // e.g. Good(2) vs New(4) = -20%, NeedsWork(0) vs Renovated(3) = -30% (but this path
        // is skipped via mutual exclusion when renovation deduction applies in 4a+)
        return targetRank < compRank
            ? -0.10 * diff   // e.g. Original(1) vs Renovated(3) → -20%
            :  0.07 * diff;  // e.g. Renovated(3) vs Original(1) → +14%
    }

    private static int StateRank(PropertyCondition c) => c switch
    {
        PropertyCondition.NeedsWork => 0,
        PropertyCondition.Original => 1,
        PropertyCondition.Good => 2,
        PropertyCondition.Renovated => 3,
        PropertyCondition.New => 4,
        _ => 2 // Unknown → treat as Good
    };

    // ══════════════════════════════════════════════════════════
    //  V12: MARKET CONTEXT DETECTION
    // ══════════════════════════════════════════════════════════
    private static MarketContext DetectMarketContext(string location, string regionName)
    {
        string combined = $"{location} {regionName}".ToLowerInvariant();

        if (CoastalCities.Any(c => combined.Contains(c.ToLowerInvariant())))
            return MarketContext.Coastal;

        // Inland major cities
        string[] inlandCities = ["zagreb", "osijek", "varaždin", "varazdin",
            "čakovec", "cakovec", "koprivnica", "bjelovar", "sisak",
            "karlovac", "slavonski brod", "vukovar", "vinkovci",
            "požega", "pozega", "virovitica", "križevci", "krizevci",
            "velika gorica", "samobor", "zaprešić", "zapresic"];

        if (inlandCities.Any(c => combined.Contains(c)))
            return MarketContext.Inland;

        return MarketContext.Rural;
    }

    // ══════════════════════════════════════════════════════════
    //  V12: TWIN COMPARISON — Derive feature values from dataset
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// Phase 2: Find "twin pairs" in the dataset — listings similar in
    /// size/location but differing in one feature — to calculate the
    /// market value of each feature dynamically.
    /// </summary>
    private DerivedFactors DeriveAdjustmentFactors(
        List<ExtractedPrice> cleanPrices,
        MarketContext context,
        double targetSizeM2)
    {
        var factors = new DerivedFactors { Context = context };
        var log = factors.DerivationLog;

        if (cleanPrices.Count < 3)
        {
            log.Add("Premalo podataka za Twin Comparison (<3 comps) — koristim fallback faktore");
            ApplyFallbackFactors(factors, context, targetSizeM2);
            return factors;
        }

        // ── 1. GARAGE VALUE (flat EUR) ──
        var withGarage = cleanPrices.Where(p => p.HasGarage).ToList();
        var withoutGarage = cleanPrices.Where(p => !p.HasGarage && !p.HasParking).ToList();

        if (withGarage.Count >= 1 && withoutGarage.Count >= 1)
        {
            // Use median price/m² from each group, multiply by average size to get flat EUR
            double medianWith = MedianValue(withGarage.Select(p => p.PricePerM2));
            double medianWithout = MedianValue(withoutGarage.Select(p => p.PricePerM2));
            double avgSize = targetSizeM2 > 0 ? targetSizeM2 : 60;

            double garageValuePerM2 = medianWith - medianWithout;
            double garageValueFlat = Math.Max(garageValuePerM2 * avgSize, 0);

            // Sanity clamp: garage can't be worth more than 60k or less than 0
            garageValueFlat = Math.Clamp(garageValueFlat, 0, 60_000);
            factors.GarageValueEur = garageValueFlat;
            factors.GaragePairCount = Math.Min(withGarage.Count, withoutGarage.Count);
            log.Add($"Garaža: derived {garageValueFlat:N0} EUR " +
                $"(median s garažom={medianWith:F0}, bez={medianWithout:F0} EUR/m², " +
                $"pairs: {factors.GaragePairCount})");
        }
        else
        {
            // Fallback based on market context
            factors.GarageValueEur = context == MarketContext.Coastal ? 35_000 : 20_000;
            factors.GaragePairCount = 0;
            log.Add($"Garaža: nema parova u datasetu — fallback {factors.GarageValueEur:N0} EUR ({context})");
        }

        // ── 2. PARKING VALUE (flat EUR) ──
        var withParking = cleanPrices.Where(p => p.HasParking && !p.HasGarage).ToList();
        if (withParking.Count >= 1 && withoutGarage.Count >= 1)
        {
            double medianP = MedianValue(withParking.Select(p => p.PricePerM2));
            double medianNone = MedianValue(withoutGarage.Select(p => p.PricePerM2));
            double avgSize = targetSizeM2 > 0 ? targetSizeM2 : 60;
            double parkingValue = Math.Clamp((medianP - medianNone) * avgSize, 0, 30_000);
            factors.ParkingValueEur = parkingValue;
            factors.ParkingPairCount = Math.Min(withParking.Count, withoutGarage.Count);
            log.Add($"Parking: derived {parkingValue:N0} EUR (pairs: {factors.ParkingPairCount})");
        }
        else
        {
            factors.ParkingValueEur = context == MarketContext.Coastal ? 15_000 : 8_000;
            factors.ParkingPairCount = 0;
            log.Add($"Parking: nema parova — fallback {factors.ParkingValueEur:N0} EUR ({context})");
        }

        // ── 3. RENOVATION PREMIUM (%) ──
        var renovated = cleanPrices.Where(p =>
            p.DetectedCondition == PropertyCondition.Renovated
            || p.DetectedCondition == PropertyCondition.New).ToList();
        var notRenovated = cleanPrices.Where(p =>
            p.DetectedCondition == PropertyCondition.Original
            || p.DetectedCondition == PropertyCondition.NeedsWork
            || p.DetectedCondition == PropertyCondition.Good).ToList();

        if (renovated.Count >= 1 && notRenovated.Count >= 1)
        {
            double medianReno = MedianValue(renovated.Select(p => p.PricePerM2));
            double medianOld = MedianValue(notRenovated.Select(p => p.PricePerM2));
            double premium = medianOld > 0 ? (medianReno - medianOld) / medianOld : 0;
            premium = Math.Clamp(premium, 0, 0.50); // Cap at 50%
            factors.RenovationPremiumPct = premium;
            factors.RenovationPairCount = Math.Min(renovated.Count, notRenovated.Count);
            log.Add($"Renovacija: derived {premium * 100:F1}% premium " +
                $"(reno median={medianReno:F0}, stari={medianOld:F0}, pairs: {factors.RenovationPairCount})");
        }
        else
        {
            factors.RenovationPremiumPct = 0.25; // 25% default
            factors.RenovationPairCount = 0;
            log.Add("Renovacija: nema parova — fallback 25%");
        }

        // ── 4. SEA VIEW PREMIUM (%) ──
        if (context == MarketContext.Coastal)
        {
            var withView = cleanPrices.Where(p => p.HasSeaView).ToList();
            var withoutView = cleanPrices.Where(p => !p.HasSeaView).ToList();

            if (withView.Count >= 1 && withoutView.Count >= 1)
            {
                double medianView = MedianValue(withView.Select(p => p.PricePerM2));
                double medianNoView = MedianValue(withoutView.Select(p => p.PricePerM2));
                double premium = medianNoView > 0 ? (medianView - medianNoView) / medianNoView : 0;
                premium = Math.Clamp(premium, 0, 0.40); // Cap at 40%
                factors.SeaViewPremiumPct = premium;
                factors.SeaViewPairCount = Math.Min(withView.Count, withoutView.Count);
                log.Add($"Pogled na more: derived {premium * 100:F1}% premium (pairs: {factors.SeaViewPairCount})");
            }
            else
            {
                factors.SeaViewPremiumPct = 0.15; // 15% default for coastal
                factors.SeaViewPairCount = 0;
                log.Add("Pogled na more: nema parova — fallback 15%");
            }
        }

        // ── 5. ORIENTATION DELTA (%) ──
        if (context == MarketContext.Coastal)
        {
            var southComps = cleanPrices.Where(p => p.IsSouthFacing).ToList();
            var northComps = cleanPrices.Where(p => p.IsNorthFacing).ToList();

            if (southComps.Count >= 1 && northComps.Count >= 1)
            {
                double medianSouth = MedianValue(southComps.Select(p => p.PricePerM2));
                double medianNorth = MedianValue(northComps.Select(p => p.PricePerM2));
                double delta = medianNorth > 0 ? (medianSouth - medianNorth) / medianNorth : 0;
                delta = Math.Clamp(delta, 0, 0.30); // Cap at 30%
                factors.OrientationDeltaPct = delta;
                factors.OrientationPairCount = Math.Min(southComps.Count, northComps.Count);
                log.Add($"Orijentacija: derived {delta * 100:F1}% jug vs sjever (pairs: {factors.OrientationPairCount})");
            }
            else
            {
                factors.OrientationDeltaPct = 0.12; // 12% default penalty for north vs south
                factors.OrientationPairCount = 0;
                log.Add("Orijentacija: nema parova — fallback 12% (sjever vs jug)");
            }
        }

        return factors;
    }

    /// <summary>
    /// Fallback factors when not enough data for twin comparison.
    /// Based on market context (coastal cities have higher garage/view premiums).
    /// </summary>
    private static void ApplyFallbackFactors(DerivedFactors factors, MarketContext context, double targetSizeM2)
    {
        switch (context)
        {
            case MarketContext.Coastal:
                factors.GarageValueEur = 35_000;
                factors.ParkingValueEur = 15_000;
                factors.RenovationPremiumPct = 0.25;
                factors.SeaViewPremiumPct = 0.15;
                factors.OrientationDeltaPct = 0.12;
                factors.DerivationLog.Add("Coastal fallback: garaža=35k, parking=15k, reno=25%, view=15%, orijentacija=12%");
                break;
            case MarketContext.Inland:
                factors.GarageValueEur = 20_000;
                factors.ParkingValueEur = 8_000;
                factors.RenovationPremiumPct = 0.20;
                factors.SeaViewPremiumPct = 0;
                factors.OrientationDeltaPct = 0.03; // Minimal in inland
                factors.DerivationLog.Add("Inland fallback: garaža=20k, parking=8k, reno=20%, orijentacija=3%");
                break;
            case MarketContext.Rural:
                factors.GarageValueEur = 10_000;
                factors.ParkingValueEur = 3_000;
                factors.RenovationPremiumPct = 0.15;
                factors.SeaViewPremiumPct = 0;
                factors.OrientationDeltaPct = 0;
                factors.DerivationLog.Add("Rural fallback: garaža=10k, parking=3k, reno=15%");
                break;
        }
    }

    private static double MedianValue(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}

// ══════════════════════════════════════════════════════════
//  V12: MARKET CONTEXT & DERIVED FACTORS
// ══════════════════════════════════════════════════════════

public enum MarketContext { Coastal, Inland, Rural }

/// <summary>
/// Factors derived from "twin comparison" in the dataset.
/// Each factor has a derived EUR or % value and a pair count (confidence).
/// </summary>
public class DerivedFactors
{
    public double GarageValueEur { get; set; }       // Flat EUR value of garage in this market
    public int GaragePairCount { get; set; }
    public double ParkingValueEur { get; set; }      // Flat EUR value of parking space
    public int ParkingPairCount { get; set; }
    public double RenovationPremiumPct { get; set; } // % premium renovated vs original
    public int RenovationPairCount { get; set; }
    public double SeaViewPremiumPct { get; set; }    // % premium for sea view
    public int SeaViewPairCount { get; set; }
    public double OrientationDeltaPct { get; set; }  // % delta south vs north
    public int OrientationPairCount { get; set; }
    public MarketContext Context { get; set; }
    public List<string> DerivationLog { get; set; } = [];
}

// ══════════════════════════════════════════════════════════
//  OUTPUT MODELS
// ══════════════════════════════════════════════════════════

public class DeterministicValuation
{
    // ── Core values ──
    public int EstimatedValueEur { get; set; }
    public double CalculatedPricePerM2 { get; set; }
    public double MarketAverageAsking { get; set; }   // Weighted median (base)
    public double AfterDiscountPerM2 { get; set; }    // V27: After realization gap, before adjustments
    public double RealizationDiscountPercent { get; set; }
    public string ConfidenceLevel { get; set; } = "";
    public string PriceSource { get; set; } = "";
    public string RegionName { get; set; } = "";

    // ── Data quality ──
    public int TotalListingsFound { get; set; }
    public int UsablePricePoints { get; set; }
    public int OutliersRemoved { get; set; }

    // ── V4: Robust Estimator fields ──
    public double StdDevPerM2 { get; set; }
    public double CoefficientOfVariation { get; set; }
    public string OutlierMethod { get; set; } = "";
    public List<string> OutlierLog { get; set; } = [];
    public double TotalAdjustmentPercent { get; set; }
    public string RealizationGapReasoning { get; set; } = "";

    // ── Comparables detail ──
    public List<UsedComparable> UsedComparables { get; set; } = [];
    public List<ValuationAdjustment> Adjustments { get; set; } = [];

    // ── Math Confidence ──
    public int MathConfidenceScore { get; set; }
    public string MathConfidenceLevel { get; set; } = "";
    public string MathConfidenceExplanation { get; set; } = "";

    // ── Pipeline trace ──
    public List<string> Pipeline { get; set; } = [];

    // ── Filtered URL list for LLM sync (V6.3) ──
    public HashSet<string> FinalUsedUrls { get; set; } = [];

    // ── V13: Sherlock Insight ──
    public string SherlockInsight { get; set; } = "";
}

public class UsedComparable
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public double PricePerM2 { get; set; }
    public double SimilarityScore { get; set; }
    public bool IsOutlier { get; set; }

    // V10: BrightData-enriched fields for frontend display
    public double? TotalPrice { get; set; }
    public double? AreaM2 { get; set; }
    public int? Rooms { get; set; }
    public string? Floor { get; set; }
    public int? YearBuilt { get; set; }
    public string? Condition { get; set; }
    public string? EnergyClass { get; set; }
    public string? Heating { get; set; }
    public string? Location { get; set; }
    public bool? HasGarage { get; set; }
    public bool? HasParking { get; set; }

    // V18: Yard area + similarity reason
    public double? YardAreaM2 { get; set; }
    public string? SimilarityReason { get; set; }

    // V20: Deep dive status — what happened when we tried to scrape this listing
    public string? DeepDiveStatus { get; set; }

    // V20: Per-comparable correction % (usporedna metoda — individual adjustment)
    public double CorrectionPercent { get; set; }
    public string? CorrectionReason { get; set; }
}

public class ValuationAdjustment
{
    public string Factor { get; set; } = "";
    public string Description { get; set; } = "";
    public double Percent { get; set; }
}

public class ExtractedPrice
{
    public double PricePerM2 { get; set; }
    public string SourceTitle { get; set; } = "";
    public string SourceUrl { get; set; } = "";

    // V5: Condition & yard data extracted from listing text
    public PropertyCondition DetectedCondition { get; set; } = PropertyCondition.Unknown;
    public double DetectedYardM2 { get; set; }

    // V7: Exact neighborhood match flag for weighting & sanity check
    public bool IsExactNeighborhoodMatch { get; set; }

    // V8.1: Feature booleans — detected from listing text
    public bool HasGarage { get; set; }
    public bool HasRenovation { get; set; }
    public bool HasYard { get; set; }

    // V12: Additional feature detection for Twin Comparison
    public bool HasParking { get; set; }    // parking space (not garage)
    public bool HasSeaView { get; set; }    // pogled na more
    public bool IsNorthFacing { get; set; } // north orientation detected
    public bool IsSouthFacing { get; set; } // south/west orientation detected
    public double DetectedSizeM2 { get; set; } // extracted main area
}

internal class MathConfidence
{
    public int Score { get; set; }
    public string Level { get; set; } = "";
    public string Explanation { get; set; } = "";
}
