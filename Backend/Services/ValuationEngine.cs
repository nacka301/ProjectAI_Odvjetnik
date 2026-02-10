using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V4 — Robust Estimator Valuation Engine (with Intelligent Adjuster)
/// 
/// Statistical overhaul:
///   1. IQR outlier detection (fallback ±80% median)
///   2. SimilarityScore per comparable (0.0–1.0)
///   3. Weighted Median (by SimilarityScore)
///   4. Adaptive Realization Gap (based on StdDev + search phase)
///   5. Mathematical Confidence (0–100, penalty system)
///   6. Hard cap ±40% on total adjustments
///   7. Full explainability JSON
///
/// Zero AI — pure deterministic math.
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
        ["renoviran", "adaptiran", "obnovljen", "novoure\u0111", "novoured", "kompletno ure\u0111"];
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

        // ── 4a+. FEATURE-BASED PRICE ADJUSTMENTS ──
        // Garage, renovation, yard — adjust comp price to Subject's level
        if (cleanPrices.Count > 0)
        {
            bool isApartment = marketData.DetectedPropertyType.Contains("stan", StringComparison.OrdinalIgnoreCase)
                            || marketData.DetectedPropertyType.Contains("apartman", StringComparison.OrdinalIgnoreCase);
            bool subjectHasGarage = DetectFeature(
                $"{property.Description} {property.Condition} {property.RawText}", GarageKeywords);
            bool subjectHasReno = targetState == PropertyCondition.Renovated
                              || targetState == PropertyCondition.New
                              || DetectFeature(
                                  $"{property.Description} {property.Condition} {property.RawText}", RenovationKeywords);
            bool subjectHasYard = property.YardAreaM2 > 0
                              || DetectFeature(
                                  $"{property.Description} {property.Condition} {property.RawText}", YardKeywords);

            int featureAdjCount = 0;
            var featureLog = new List<string>();

            foreach (var p in cleanPrices)
            {
                double originalPrice = p.PricePerM2;
                var reasons = new List<string>();

                // ── GARAGE ──
                if (subjectHasGarage && !p.HasGarage)
                {
                    // Subject has garage, comp doesn't → comp is cheaper than it should be
                    // Add garage value: +5% for apartments, flat EUR 15k/sizeM2 for houses
                    double garageBoost = isApartment
                        ? 0.05
                        : (property.SizeM2 > 0 ? 15_000.0 / (originalPrice * property.SizeM2) : 0.05);
                    garageBoost = Math.Min(garageBoost, 0.10); // cap at 10%
                    p.PricePerM2 *= (1 + garageBoost);
                    reasons.Add($"+{garageBoost * 100:F0}% (nema gara\u017eu)");
                }
                else if (!subjectHasGarage && p.HasGarage)
                {
                    // Comp has garage, subject doesn't → comp price includes garage premium
                    double garagePenalty = isApartment
                        ? 0.05
                        : (property.SizeM2 > 0 ? 15_000.0 / (originalPrice * property.SizeM2) : 0.05);
                    garagePenalty = Math.Min(garagePenalty, 0.10);
                    p.PricePerM2 *= (1 - garagePenalty);
                    reasons.Add($"-{garagePenalty * 100:F0}% (ima gara\u017eu)");
                }

                // ── RENOVATION ──
                // V9: Construction Cost Deduction — if subject needs renovation and comp is renovated,
                // deduct estimated renovation cost (600 €/m²) from comp's per-m² price.
                bool subjectNeedsWork = targetState == PropertyCondition.NeedsWork
                    || targetState == PropertyCondition.Original;
                bool compIsRenovated = p.HasRenovation
                    || p.DetectedCondition == PropertyCondition.Renovated
                    || p.DetectedCondition == PropertyCondition.New;

                if (subjectNeedsWork && compIsRenovated)
                {
                    // V10: Construction cost deduction uses SUBJECT's context.
                    // Renovation cost per m² is a fixed estimate; deduction is capped
                    // at 30% of comp value (you don't deduct 60k reno on a 100k property).
                    const double RenovationCostPerM2 = 600.0;
                    double deduction = RenovationCostPerM2 / p.PricePerM2;
                    deduction = Math.Min(deduction, 0.30); // cap at -30% (V10: was 40%)
                    p.PricePerM2 *= (1 - deduction);
                    reasons.Add($"-{deduction * 100:F0}% (trošak adaptacije {RenovationCostPerM2:F0}€/m²)");
                }
                else if (!subjectHasReno && p.HasRenovation)
                {
                    // Comp is renovated, subject needs work → heavy penalty
                    p.PricePerM2 *= 0.80; // -20%
                    reasons.Add("-20% (comp renoviran, subjekt nije)");
                }
                else if (subjectHasReno && !p.HasRenovation
                         && p.DetectedCondition != PropertyCondition.Renovated
                         && p.DetectedCondition != PropertyCondition.New)
                {
                    // Subject renovated, comp old → bonus
                    p.PricePerM2 *= 1.15; // +15%
                    reasons.Add("+15% (subjekt renoviran, comp star)");
                }

                // ── YARD (houses only) ──
                if (!isApartment && property.YardAreaM2 > 0 && p.DetectedYardM2 > 0)
                {
                    double yardDiff = property.YardAreaM2 - p.DetectedYardM2;
                    if (Math.Abs(yardDiff) > 200)
                    {
                        // +7% per 200m\u00b2 of extra land subject has (or -7% if less)
                        double yardAdj = (yardDiff / 200.0) * 0.07;
                        yardAdj = Math.Clamp(yardAdj, -0.25, 0.25); // cap \u00b125%
                        p.PricePerM2 *= (1 + yardAdj);
                        reasons.Add($"{yardAdj * 100:+0;-0}% (oku\u0107nica razlika {yardDiff:+0;-0}m\u00b2)");
                    }
                }

                if (reasons.Count > 0)
                {
                    featureAdjCount++;
                    string label = p.SourceTitle.Length > 35 ? p.SourceTitle[..35] : p.SourceTitle;
                    featureLog.Add(
                        $"Comp '{label}': {originalPrice:F0}\u2192{p.PricePerM2:F0} EUR/m\u00b2 [{string.Join(", ", reasons)}]");
                }
            }

            if (featureAdjCount > 0)
            {
                pipeline.Add($"Feature adj: {featureAdjCount} comps adjusted (garage/renovation/yard)");
                foreach (var log in featureLog)
                    pipeline.Add($"  \u2192 {log}");
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

                    if (inCity && !inNeighborhood)
                    {
                        double original = p.PricePerM2;
                        p.PricePerM2 *= 0.80; // -20% location drift
                        drifted++;
                        _logger.LogDebug(
                            "Neighborhood drift: {Title} {Old:F0}->{New:F0} EUR/m2 (city={City}, hood={Hood} missing)",
                            p.SourceTitle.Length > 40 ? p.SourceTitle[..40] : p.SourceTitle,
                            original, p.PricePerM2, city, neighborhood);
                    }
                }

                if (drifted > 0)
                    pipeline.Add($"Neighborhood drift: {drifted} comps penalized -20% (not in {neighborhood})");
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

        // ── 4c. Similarity Scoring ──
        foreach (var price in cleanPrices)
        {
            double simScore = CalculateSimilarityScore(
                price, property, marketData.DetectedPropertyType, marketData.RegionName);
            compDetails.Add(new UsedComparable
            {
                Title = price.SourceTitle,
                Url = price.SourceUrl,
                PricePerM2 = Math.Round(price.PricePerM2, 0),
                SimilarityScore = Math.Round(simScore, 2),
                IsOutlier = false
            });
        }

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

        pipeline.Add($"Scored {cleanPrices.Count} comparables with SimilarityScore");

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

        // ── 9b. V10: SAFETY FLOOR — prevent algorithmic underflow ──
        // Final valuation cannot be lower than 65% of the average comparable asking price.
        if (cleanPrices.Count >= 2)
        {
            double averageCompPricePerM2 = cleanPrices.Average(c => c.PricePerM2);
            double averageCompTotal = averageCompPricePerM2 * sizeM2;
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

        _logger.LogInformation(
            "Engine V4: {Comps} comps, WM={WM:F0}, gap={Gap:F1}%, adj={Adj:+0;-0}%, final={Val:N0} €, confidence={Conf}/100",
            cleanPrices.Count, basePricePerM2, realizationGap * 100, totalAdjPercent,
            finalValue, mathConfidence.Score);

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  SIMILARITY SCORE (0.0 – 1.0)
    // ══════════════════════════════════════════════════════════
    private double CalculateSimilarityScore(
        ExtractedPrice comp,
        ExtractedPropertyInfo target,
        string detectedType,
        string regionName)
    {
        double score = 0;
        double maxScore = 0;
        bool isHouseOrLand = detectedType.Contains("kuć", StringComparison.OrdinalIgnoreCase)
                          || detectedType.Contains("kuc", StringComparison.OrdinalIgnoreCase)
                          || detectedType.Contains("vila", StringComparison.OrdinalIgnoreCase)
                          || MarketResearchService.IsLandType(detectedType);

        // ── Location (max 0.30) ──
        maxScore += 0.30;
        string compText = $"{comp.SourceTitle} {comp.SourceUrl}".ToLowerInvariant();
        string targetLoc = target.Location.ToLowerInvariant().Trim();

        var locParts = targetLoc.Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 2)
            .ToList();

        // V8: Check if comp is in the exact neighborhood (first loc part)
        bool isExactNeighborhood = locParts.Count >= 2
            && compText.Contains(locParts[0]); // e.g. "retfala"

        if (locParts.Count >= 2 && compText.Contains(locParts[^1]) && compText.Contains(locParts[0]))
        {
            score += 0.30; // Same neighborhood + city = full score
        }
        else if (isExactNeighborhood)
        {
            score += 0.25; // Neighborhood match without city context
        }
        else if (locParts.Any(part => compText.Contains(part)))
        {
            // V8: Same city but WRONG neighborhood → hard cap at 0.12
            // (was 0.22 — allowed Donji Grad to score 67% for Retfala subject)
            score += 0.12;
        }
        else if (!string.IsNullOrEmpty(regionName) && compText.Contains(regionName.ToLowerInvariant()))
        {
            score += 0.08; // Same region only
        }
        else
        {
            score += 0.03; // At least from Croatia
        }

        // ── Property Type (max 0.20) ──
        maxScore += 0.20;
        string type = detectedType.ToLowerInvariant();
        if (compText.Contains(type))
        {
            score += 0.20;
        }
        else if (IsSimilarType(type, compText))
        {
            score += 0.14;
        }
        else
        {
            score += 0.04;
        }

        // ── Size Proximity (max 0.20) ──
        maxScore += 0.20;
        if (target.SizeM2 > 0)
        {
            double compSize = ExtractSize(compText);
            if (compSize > 0)
            {
                double sizeRatio = target.SizeM2 > compSize
                    ? compSize / target.SizeM2
                    : target.SizeM2 / compSize;

                double sizeScore = Math.Max(0, sizeRatio);
                score += 0.20 * sizeScore;
            }
            else
            {
                score += 0.08;
            }
        }
        else
        {
            score += 0.10;
        }

        // ── Condition Match (max 0.15) ──
        maxScore += 0.15;
        var targetCond = ParseTargetState(target);
        var compCond = comp.DetectedCondition;

        if (targetCond != PropertyCondition.Unknown && compCond != PropertyCondition.Unknown)
        {
            int rankDiff = Math.Abs(StateRank(targetCond) - StateRank(compCond));
            double condScore = rankDiff switch
            {
                0 => 0.15,   // Perfect match
                1 => 0.10,   // Close
                2 => 0.05,   // Different
                _ => 0.02    // Very different
            };
            score += condScore;
        }
        else
        {
            score += 0.07; // Unknown — neutral
        }

        // ── Yard Area Proximity (max 0.15) — only for houses/land ──
        maxScore += 0.15;
        if (isHouseOrLand && target.YardAreaM2 > 0 && comp.DetectedYardM2 > 0)
        {
            double yardRatio = target.YardAreaM2 > comp.DetectedYardM2
                ? comp.DetectedYardM2 / target.YardAreaM2
                : target.YardAreaM2 / comp.DetectedYardM2;

            if (yardRatio >= 0.80)
            {
                // Within ±20% → full bonus
                score += 0.15;
            }
            else if (yardRatio >= 0.40)
            {
                // Moderate difference → partial
                score += 0.15 * yardRatio;
            }
            else
            {
                // Massive plot difference (e.g., 2000m² vs 300m²)
                // → treat as different asset class → heavily penalize
                score += 0.02;
            }
        }
        else if (isHouseOrLand && target.YardAreaM2 > 0 && comp.DetectedYardM2 <= 0)
        {
            score += 0.05; // Can't compare — slight penalty
        }
        else
        {
            // Not a house/no yard info — distribute neutrally
            score += 0.08;
        }

        // Normalize to 0.0–1.0
        return maxScore > 0 ? Math.Clamp(score / maxScore, 0.0, 1.0) : 0.5;
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
        if (allText.Contains("potrebna adaptac") || allText.Contains("potrebna obnova")
            || allText.Contains("ruševin") || allText.Contains("zapušten"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Potrebna adaptacija",
                Description = "Nekretnina zahtijeva značajnu adaptaciju",
                Percent = -20.0
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

        // ── Parking / garage ──
        if (allText.Contains("garaža") || allText.Contains("garaza")
            || allText.Contains("parking") || allText.Contains("garažno mjesto"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Parking/garaža",
                Description = "Uključen parking ili garaža",
                Percent = 5.0
            });
        }

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
                        HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords)
                    });
                    continue;
                }
            }

            // Try absolute price + size → calculate per m²
            var priceMatches = PriceRegex.Matches(text);
            var sizeMatch = SizeRegex.Match(text);

            foreach (Match pm in priceMatches)
            {
                double price = ParseCroatianNumber(pm.Groups[1].Value);

                // Determine size
                double size = 0;
                if (sizeMatch.Success)
                    size = ParseCroatianNumber(sizeMatch.Groups[1].Value);
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
                            HasYard = listing.HasYard ?? DetectFeature(text, YardKeywords)
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

        // Per-rank: -15% when target worse, +10% when target better
        return targetRank < compRank
            ? -0.15 * diff   // e.g. Original(1) vs Renovated(3) → -30%
            :  0.10 * diff;  // e.g. Renovated(3) vs Original(1) → +20%
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
