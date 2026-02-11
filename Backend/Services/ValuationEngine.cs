using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V4 — Robust Estimator Valuation Engine (with Intelligent Adjuster)
/// 
/// Statistical overhaul:
///   1. IQR outlier detection (fallback ±80% median)
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
    private static readonly Regex PriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PricePerM2Regex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)\s*/\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SizeRegex = new(
        @"(\d{1,6}(?:[,\.]\d+)?)\s*m[²2]",
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

    private static readonly HashSet<string> PremiumParkingCities =
        new(StringComparer.OrdinalIgnoreCase)
    {
        "split", "dubrovnik", "rijeka", "zadar", "rovinj",
        "hvar", "trogir", "šibenik", "sibenik"
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
    //  SALES-ONLY GATEKEEPER — Dynamic Price Floors
    //  Buildings: aggressive (800€/m², 35k€ total) — kills rentals
    //  Land: permissive (15€/m², 5k€ total) — land is 30-150€/m²
    // ══════════════════════════════════════════════════════════
    private const double MinSalePriceEur_Building = 35_000;
    private const double MinSalePriceEur_Land     =  5_000;
    private const double MinPricePerM2_Building   =    800;
    private const double MinPricePerM2_Land       =     15;
    private const double HighClusterCvThreshold   =   40.0;  // CV% above which we trust high cluster

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
        pipeline.Add($"Region: {result.RegionName} - no hardcoded benchmarks, pure search data");

        // ── 2. Extract all prices from listings ──
        bool isLand = MarketResearchService.IsLandType(marketData.DetectedPropertyType);
        double minPricePerM2 = isLand ? MinPricePerM2_Land : MinPricePerM2_Building;
        double minSalePrice  = isLand ? MinSalePriceEur_Land : MinSalePriceEur_Building;
        var rawPrices = ExtractPricesFromListings(marketData.AllResults, property.SizeM2, isLand);
        pipeline.Add($"Izvučeno {rawPrices.Count} cijena iz {result.TotalListingsFound} oglasa");

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

            // Fallback: if IQR was too aggressive and <3 remain → use median ±80%
            if (cleanPrices.Count < 3 && rawPrices.Count >= 3)
            {
                double rawMedian = ppsqmValues[ppsqmValues.Count / 2];
                cleanPrices = rawPrices
                    .Where(p => p.PricePerM2 >= rawMedian * 0.20
                             && p.PricePerM2 <= rawMedian * 1.80)
                    .ToList();
                outlierMethod = "IQR->Median+/-80%";
                int removedFallback = rawPrices.Count - cleanPrices.Count;
                outlierLog.Add($"IQR previse agresivan - fallback na median +/-80% ({rawMedian:F0} EUR/m2)");
                outlierLog.Add($"Fallback: {cleanPrices.Count} ostalo, {removedFallback} uklonjeno");
                pipeline.Add($"IQR fallback (median-based): {cleanPrices.Count} ostalo");
            }
        }
        else if (rawPrices.Count >= 2)
        {
            // 2-4 prices: use median ±80% as filter
            var sorted = rawPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double median = sorted[sorted.Count / 2];
            cleanPrices = rawPrices
                .Where(p => p.PricePerM2 >= median * 0.20
                         && p.PricePerM2 <= median * 1.80)
                .ToList();
            int removed = rawPrices.Count - cleanPrices.Count;
            outlierMethod = "Median+/-80%";
            outlierLog.Add($"Premalo za IQR ({rawPrices.Count}) - median filter +/-80% ({median:F0} EUR/m2)");
            outlierLog.Add($"Uklonjeno {removed} outliera od {rawPrices.Count}");
            pipeline.Add($"Median filter: {cleanPrices.Count} ostalo");
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

        // ── 3b. SALES-ONLY GATEKEEPER (dynamic floor) ──
        // For buildings: 800 €/m² kills rentals. For land: 15 €/m² allows real parcels.
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

        // ── 3d. HIGH-CLUSTER TRUST (CV > 40%) ──
        // If CV is huge, we likely have two populations (rentals vs sales).
        // Trust the HIGHER cluster — those are the real sales.
        if (cleanPrices.Count >= 4)
        {
            var sortedVals = cleanPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double mean = sortedVals.Average();
            double sumSq = sortedVals.Sum(v => (v - mean) * (v - mean));
            double cv = mean > 0 ? Math.Sqrt(sumSq / sortedVals.Count) / mean * 100 : 0;

            if (cv > HighClusterCvThreshold)
            {
                // Split at median — keep only upper half
                double medianSplit = sortedVals[sortedVals.Count / 2];
                int beforeSplit = cleanPrices.Count;
                cleanPrices = cleanPrices.Where(p => p.PricePerM2 >= medianSplit).ToList();
                int splitRemoved = beforeSplit - cleanPrices.Count;

                if (splitRemoved > 0)
                {
                    outlierLog.Add($"High-Cluster Trust: CV={cv:F1}% > {HighClusterCvThreshold}% -> discarded {splitRemoved} low-cluster listings (split at EUR {medianSplit:F0}/m2)");
                    pipeline.Add($"High-Cluster Trust: kept {cleanPrices.Count} upper-cluster listings (CV was {cv:F1}%)");
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
            string subjectText = $"{property.Description} {property.Condition} {property.RawText}".ToLowerInvariant();
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

                if (subjectNeedsWork && compIsFullyRenovated)
                {
                    // Use derived renovation premium, but cap at 30%
                    double deduction = Math.Min(derived.RenovationPremiumPct, 0.30);
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add($"-{deduction * 100:F0}% (trošak adaptacije, derived reno premium={derived.RenovationPremiumPct * 100:F0}%)");
                }
                else if (subjectNeedsWork && compHasPartialReno)
                {
                    double deduction = Math.Min(derived.RenovationPremiumPct * 0.4, 0.15); // 40% of full reno
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add($"-{deduction * 100:F0}% (parcijalna reno)");
                }
                else if (!subjectHasReno && p.HasRenovation)
                {
                    double deduction = Math.Min(derived.RenovationPremiumPct * 0.8, 0.25);
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

        // ── 4b++. V13: CROSS-TYPE HARD EXCLUSION ──
        // Stan ≠ Kuća ≠ Zemljište — NIKAD ne uspoređivati različite tipove.
        // If subject is House, ALL apartment comps are EXCLUDED (and vice versa).
        // This is a hard block, not a penalty.
        {
            string subjectType = marketData.DetectedPropertyType.ToLowerInvariant();
            bool subjectIsHouse = subjectType.Contains("kuć") || subjectType.Contains("kuc")
                               || subjectType.Contains("vila") || subjectType.Contains("vikend");
            bool subjectIsApt   = subjectType.Contains("stan") || subjectType.Contains("apartman")
                               || subjectType.Contains("garsonije");
            bool subjectIsLand  = MarketResearchService.IsLandType(subjectType);

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

            compDetails.Add(new UsedComparable
            {
                Title = price.SourceTitle,
                Url = price.SourceUrl,
                PricePerM2 = Math.Round(price.PricePerM2, 0),
                SimilarityScore = Math.Round(simScore, 2),
                IsOutlier = false
            });
        }

        pipeline.Add($"Similarity: {aiScored} AI-scored, {mathFallbackScored} math-fallback");

        // Also log the removed outliers
        var outlierPrices = rawPrices.Except(cleanPrices);
        foreach (var price in outlierPrices)
        {
            compDetails.Add(new UsedComparable
            {
                Title = price.SourceTitle,
                Url = price.SourceUrl,
                PricePerM2 = Math.Round(price.PricePerM2, 0),
                SimilarityScore = 0,
                IsOutlier = true
            });
        }

        // ── 4c½. SIMILARITY FLOOR ──
        // Remove any comparable below 50% similarity — too dissimilar to be useful.
        const double MinSimilarityThreshold = 0.50;
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

        // ── 4d. Reality Check Cap ──
        // Any comp priced >50% above median → weight crushed to 0.1
        if (cleanPrices.Count >= 3)
        {
            var rcPrices = cleanPrices.Select(p => p.PricePerM2).OrderBy(v => v).ToList();
            double rcMedian = rcPrices[rcPrices.Count / 2];
            double rcCap = rcMedian * 1.50;
            int capped = 0;

            foreach (var cd in compDetails.Where(c => !c.IsOutlier))
            {
                if (cd.PricePerM2 > rcCap)
                {
                    cd.SimilarityScore = 0.10; // Nearly zero weight
                    capped++;
                    _logger.LogDebug(
                        "Reality cap: {Title} {Price:F0} EUR/m2 > cap {Cap:F0} -> weight=0.1",
                        cd.Title.Length > 40 ? cd.Title[..40] : cd.Title,
                        cd.PricePerM2, rcCap);
                }
            }

            if (capped > 0)
                pipeline.Add($"Reality check: {capped} comps capped to weight 0.1 (price > {rcCap:F0} = 150% of median {rcMedian:F0})");
        }

        // ── 5. Weighted Median / Valuation ──
        double basePricePerM2;
        string priceSource;

        if (cleanPrices.Count >= 2)
        {
            // Build weight-value pairs (3x boost for exact neighborhood match)
            var weightedPairs = cleanPrices.Select(p =>
            {
                var match = compDetails
                    .FirstOrDefault(c => c.Url == p.SourceUrl && !c.IsOutlier);
                double sim = match?.SimilarityScore ?? 0.5;
                double weight = Math.Max(sim, 0.1); // minimum weight

                // V7: 300% weight boost for exact neighborhood match
                if (p.IsExactNeighborhoodMatch)
                    weight *= 3.0;

                return (Value: p.PricePerM2, Weight: weight);
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
        double realizationGap = CalculateAdaptiveGap(coeffVar, searchPhase, cleanPrices.Count, priceSource);
        result.RealizationDiscountPercent = Math.Round(realizationGap * 100, 1);

        double afterGap = basePricePerM2 * (1 + realizationGap);
        pipeline.Add($"Realization gap: {realizationGap * 100:F1}% -> {afterGap:F0} EUR/m2");

        // ── 8. Contextual Adjustments (with hard cap ±40%) ──
        var adjustments = CalculateAdjustments(property, marketData);
        double totalAdjPercent = adjustments.Sum(a => a.Percent);

        // Hard cap ±40%
        if (totalAdjPercent > 40) totalAdjPercent = 40;
        if (totalAdjPercent < -40) totalAdjPercent = -40;

        double adjustedPricePerM2 = afterGap * (1 + totalAdjPercent / 100.0);
        pipeline.Add($"Adjustments: {totalAdjPercent:+0.0;-0.0}% -> {adjustedPricePerM2:F0} EUR/m2");

        result.CalculatedPricePerM2 = Math.Round(adjustedPricePerM2, 0);
        result.Adjustments = adjustments;
        result.TotalAdjustmentPercent = Math.Round(totalAdjPercent, 1);

        // ── 9. Final Value ──
        double sizeM2 = property.SizeM2 > 0 ? property.SizeM2 : EstimateSizeFromContext(property);
        int finalValue = (int)Math.Round(adjustedPricePerM2 * sizeM2);

        // ── 9b. V11: SAFETY FLOOR — prevent algorithmic underflow ──
        // Final valuation cannot be lower than 65% of the average RAW comparable price.
        // Uses rawPrices (pre-adjustment) so adjustments can't crater below a sane floor.
        if (rawPrices.Count >= 2)
        {
            double rawAvgPricePerM2 = rawPrices.Average(c => c.PricePerM2);
            double averageCompTotal = rawAvgPricePerM2 * sizeM2;
            double floorPrice = averageCompTotal * 0.65;
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
        string priceSource)
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
        string allText = $"{property.Description} {property.Location} {property.Condition} {property.RawText}"
            .ToLowerInvariant();

        // V12: Detect market context for context-aware adjustments
        var context = DetectMarketContext(property.Location, marketData.RegionName);
        var targetState = ParseTargetState(property);

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

        // ── Novogradnja ──
        if (allText.Contains("novograd") || allText.Contains("novogradnja")
            || allText.Contains("novouređ") || allText.Contains("novo izgrad"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Novogradnja",
                Description = "Novogradnja — premium cijena",
                Percent = 15.0
            });
        }

        // ── Needs renovation ──
        // V11: Only apply if subject is NOT NeedsWork/Original — those cases are
        // already handled by the Twin Comparison renovation deduction.
        bool conditionAlreadyAdjusted = targetState == PropertyCondition.NeedsWork
                                      || targetState == PropertyCondition.Original;
        if (!conditionAlreadyAdjusted
            && (allText.Contains("potrebna adaptac") || allText.Contains("potrebna obnova")
                || allText.Contains("ruševin") || allText.Contains("zapušten")))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Potrebna adaptacija",
                Description = "Nekretnina zahtijeva značajnu adaptaciju",
                Percent = -15.0
            });
        }
        else if (allText.Contains("adaptiran") || allText.Contains("renoviran")
              || allText.Contains("obnovljen"))
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

        // ── Very large property (>200m²) ──
        if (property.SizeM2 > 200)
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
        if (marketData.DetectedPropertyType.Contains("zemljišt", StringComparison.OrdinalIgnoreCase)
            || marketData.DetectedPropertyType.Contains("parcela", StringComparison.OrdinalIgnoreCase))
        {
            if (allText.Contains("poljopriv") || allText.Contains("oranica")
                || allText.Contains("šuma") || allText.Contains("pašnjak"))
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Poljoprivredno zemljište",
                    Description = "Poljoprivredno/šumsko — značajno niža cijena od građevinskog",
                    Percent = -35.0
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
    //  PRICE EXTRACTION
    // ══════════════════════════════════════════════════════════
    private List<ExtractedPrice> ExtractPricesFromListings(
        List<ComparableSearchResult> listings,
        double targetSizeM2,
        bool isLand = false)
    {
        var prices = new List<ExtractedPrice>();
        double minPpm2 = isLand ? MinPricePerM2_Land : MinPricePerM2_Building;
        double minTotal = isLand ? MinSalePriceEur_Land : MinSalePriceEur_Building;
        double maxPpm2 = isLand ? 1500 : 15000;  // Land rarely above 1500 €/m²

        foreach (var listing in listings)
        {
            string text = $"{listing.Title} {listing.Content}";

            // ── RENT-TRAP: Skip rental listings ──
            if (IsRentalListing(text))
                continue;

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
                    if (price >= 1000 && price < minTotal)
                        continue; // Almost certainly monthly rent or land scrap

                    double ppm2;
                    if (price < 1000)
                    {
                        // Below €1000: treat as price/m² only if ≥ floor
                        ppm2 = price;
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
    private static readonly Regex SizeContextRegex = new(
        @"(?<prefix>.{0,40}?)(\d{1,6}(?:[,\.]\d+)?)\s*m[²2]",
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
        string text = $"{property.Description} {property.RawText}";
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
        return DetectConditionFromText(
            property.Condition + " " + property.Description + " " + property.RawText);
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
    public double MarketAverageAsking { get; set; }
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

/// <summary>Property condition states for adjustment logic.</summary>
public enum PropertyCondition
{
    Unknown,
    New,          // novogradnja, novo
    Renovated,    // renovirano, adaptirano, obnovljeno
    Good,         // odrzavano, uredna
    Original,     // izvorno stanje
    NeedsWork     // potrebna adaptacija, rusevina, zapusteno
}

internal class MathConfidence
{
    public int Score { get; set; }
    public string Level { get; set; } = "";
    public string Explanation { get; set; } = "";
}
