using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V4 — Robust Estimator Valuation Engine
/// 
/// Statistical overhaul:
///   1. IQR outlier detection (fallback ±30% benchmark)
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
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)\s*/\s*m[�?2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SizeRegex = new(
        @"(\d{1,6}(?:[,\.]\d+)?)\s*m[�?2]",
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
        ["zemljiL?te"]         = new(StringComparer.OrdinalIgnoreCase) { "građevinsko zemljiL?te", "parcela", "oranica", "teren" },
        ["poslovni prostor"]  = new(StringComparer.OrdinalIgnoreCase) { "ured", "lokal", "trgovina", "skladiL?te" },
    };

    // ══════════════════════════════════════════════════════════
    //  FIRST ROW LOCATIONS (1. red do mora)
    // ══════════════════════════════════════════════════════════
    private static readonly HashSet<string> FirstRowKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        "prvi red", "1. red", "1.red", "first row", "uz more", "na moru",
        "pogled na more", "sea view", "beach", "plaLla", "waterfront"
    };

    // ══════════════════════════════════════════════════════════
    //  SALES-ONLY GATEKEEPER — Dynamic Price Floors
    //  Buildings: aggressive (800€/m�?, 35k€ total) — kills rentals
    //  Land: permissive (15€/m�?, 5k€ total) — land is 30-150€/m�?
    // ══════════════════════════════════════════════════════════
    private const double MinSalePriceEur_Building = 35_000;
    private const double MinSalePriceEur_Land     =  5_000;
    private const double MinPricePerM2_Building   =    800;
    private const double MinPricePerM2_Land       =     15;
    private const double HighClusterCvThreshold   =   40.0;  // CV% above which we trust high cluster

    private static readonly string[] RentalKeywords =
    [
        "najam", "iznajmljivanje", "iznajmi", "rent", "rental",
        "mjese�Tno", "mjesecno", "monthly", "polog", "deposit",
        "za najam", "u najam", "for rent", "do iznajmljivanja",
        "kratkoro�Tni", "kratkorocni", "dugoro�Tni", "dugorocni",
        "najamnina", "rezije", "reLlije"
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
        pipeline.Add($"Region: {result.RegionName} — no hardcoded benchmarks, pure search data");

        // ── 2. Extract all prices from listings ──
        bool isLand = MarketResearchService.IsLandType(marketData.DetectedPropertyType);
        double minPricePerM2 = isLand ? MinPricePerM2_Land : MinPricePerM2_Building;
        double minSalePrice  = isLand ? MinSalePriceEur_Land : MinSalePriceEur_Building;
        var rawPrices = ExtractPricesFromListings(marketData.AllResults, property.SizeM2, isLand);
        pipeline.Add($"Izvu�Teno {rawPrices.Count} cijena iz {result.TotalListingsFound} oglasa");

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
            outlierLog.Add($"Bounds: [{lowerBound:F0} – {upperBound:F0}] €/m�?");
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
                outlierMethod = "IQR→Median±80%";
                int removedFallback = rawPrices.Count - cleanPrices.Count;
                outlierLog.Add($"IQR previL?e agresivan — fallback na median ±80% ({rawMedian:F0} €/m�?)");
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
            outlierMethod = "Median±80%";
            outlierLog.Add($"Premalo za IQR ({rawPrices.Count}) — median filter ±80% ({median:F0} €/m�?)");
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
        // For buildings: 800 €/m�? kills rentals. For land: 15 €/m�? allows real parcels.
        {
            int beforeGatekeeper = cleanPrices.Count;
            cleanPrices = cleanPrices.Where(p => p.PricePerM2 >= minPricePerM2).ToList();
            int gatekeeperRemoved = beforeGatekeeper - cleanPrices.Count;
            if (gatekeeperRemoved > 0)
            {
                outlierLog.Add($"Sales-Only Gatekeeper: removed {gatekeeperRemoved} listings below €{minPricePerM2}/m�?");
                pipeline.Add($"Gatekeeper: {cleanPrices.Count} remain after enforcing €{minPricePerM2}/m�? floor");
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
                    outlierLog.Add($"10x sanity: removed {sanityRemoved} extreme low values (floor=€{sanityFloor:F0}/m�?)");
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
                    outlierLog.Add($"High-Cluster Trust: CV={cv:F1}% > {HighClusterCvThreshold}% → discarded {splitRemoved} low-cluster listings (split at €{medianSplit:F0}/m�?)");
                    pipeline.Add($"High-Cluster Trust: kept {cleanPrices.Count} upper-cluster listings (CV was {cv:F1}%)");
                }
            }
        }

        result.OutliersRemoved = rawPrices.Count - cleanPrices.Count;
        result.UsablePricePoints = cleanPrices.Count;

        // ── 4. Similarity Scoring ──
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

        // ── 5. Weighted Median / Valuation ──
        double basePricePerM2;
        string priceSource;

        if (cleanPrices.Count >= 2)
        {
            // Build weight-value pairs
            var weightedPairs = cleanPrices.Select(p =>
            {
                var match = compDetails
                    .FirstOrDefault(c => c.Url == p.SourceUrl && !c.IsOutlier);
                double sim = match?.SimilarityScore ?? 0.5;
                double weight = Math.Max(sim, 0.1); // minimum weight
                return (Value: p.PricePerM2, Weight: weight);
            }).ToList();

            basePricePerM2 = WeightedMedian(weightedPairs);
            priceSource = $"Weighted Median ({cleanPrices.Count} comparables)";
            pipeline.Add($"Weighted Median = {basePricePerM2:F0} €/m�?");
        }
        else if (cleanPrices.Count == 1)
        {
            // Single comparable — use 100% of that price (no benchmark blend)
            basePricePerM2 = cleanPrices[0].PricePerM2;
            priceSource = "Single Comparable";
            pipeline.Add($"Single comp = {basePricePerM2:F0} €/m�?");
        }
        else
        {
            // No data — engine cannot produce a reliable estimate
            basePricePerM2 = 0;
            priceSource = "Nedovoljno podataka";
            pipeline.Add("Nema ekstrahiranih cijena — deterministic engine ne moLle procijeniti");
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
            pipeline.Add($"StdDev = {stdDev:F0} €/m�? (CV = {coeffVar:F1}%)");
        }

        result.StdDevPerM2 = Math.Round(stdDev, 0);
        result.CoefficientOfVariation = Math.Round(coeffVar, 1);

        // ── 7. Adaptive Realization Gap ──
        var searchPhase = ParseSearchPhase(marketData.SearchPhaseUsed);
        double realizationGap = CalculateAdaptiveGap(coeffVar, searchPhase, cleanPrices.Count, priceSource);
        result.RealizationDiscountPercent = Math.Round(realizationGap * 100, 1);

        double afterGap = basePricePerM2 * (1 + realizationGap);
        pipeline.Add($"Realization gap: {realizationGap * 100:F1}% → {afterGap:F0} €/m�?");

        // ── 8. Contextual Adjustments (with hard cap ±40%) ──
        var adjustments = CalculateAdjustments(property, marketData);
        double totalAdjPercent = adjustments.Sum(a => a.Percent);

        // Hard cap ±40%
        if (totalAdjPercent > 40) totalAdjPercent = 40;
        if (totalAdjPercent < -40) totalAdjPercent = -40;

        double adjustedPricePerM2 = afterGap * (1 + totalAdjPercent / 100.0);
        pipeline.Add($"Adjustments: {totalAdjPercent:+0.0;-0.0}% → {adjustedPricePerM2:F0} €/m�?");

        result.CalculatedPricePerM2 = Math.Round(adjustedPricePerM2, 0);
        result.Adjustments = adjustments;
        result.TotalAdjustmentPercent = Math.Round(totalAdjPercent, 1);

        // ── 9. Final Value ──
        double sizeM2 = property.SizeM2 > 0 ? property.SizeM2 : EstimateSizeFromContext(property);
        int finalValue = (int)Math.Round(adjustedPricePerM2 * sizeM2);

        result.EstimatedValueEur = finalValue;
        result.PriceSource = priceSource;
        pipeline.Add($"Final: {adjustedPricePerM2:F0} €/m�? A� {sizeM2:F0} m�? = {finalValue:N0} €");

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

        // ── Location (max 0.35) ──
        maxScore += 0.35;
        string compText = $"{comp.SourceTitle} {comp.SourceUrl}".ToLowerInvariant();
        string targetLoc = target.Location.ToLowerInvariant().Trim();

        // Extract city/street parts
        var locParts = targetLoc.Split(new[] { ',', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 2)
            .ToList();

        if (locParts.Count >= 2 && compText.Contains(locParts[^1]) && compText.Contains(locParts[0]))
        {
            score += 0.35; // Same street + city
        }
        else if (locParts.Any(part => compText.Contains(part)))
        {
            score += 0.25; // Same city/area
        }
        else if (!string.IsNullOrEmpty(regionName) && compText.Contains(regionName.ToLowerInvariant()))
        {
            score += 0.15; // Same region
        }
        else
        {
            score += 0.05; // At least from Croatia
        }

        // ── Property Type (max 0.25) ──
        maxScore += 0.25;
        string type = detectedType.ToLowerInvariant();
        if (compText.Contains(type))
        {
            score += 0.25; // Exact type match
        }
        else if (IsSimilarType(type, compText))
        {
            score += 0.17; // Similar type
        }
        else
        {
            score += 0.05; // Unknown match
        }

        // ── Size Proximity (max 0.25) ──
        maxScore += 0.25;
        if (target.SizeM2 > 0)
        {
            double compSize = ExtractSize(compText);
            if (compSize > 0)
            {
                double sizeRatio = target.SizeM2 > compSize
                    ? compSize / target.SizeM2
                    : target.SizeM2 / compSize;

                // 1.0 = same size, decays toward 0 at 2x difference
                double sizeScore = Math.Max(0, sizeRatio);
                score += 0.25 * sizeScore;
            }
            else
            {
                score += 0.10; // Can't determine size — neutral
            }
        }
        else
        {
            score += 0.12; // No target size — neutral
        }

        // ── Condition / Freshness (max 0.15) ──
        maxScore += 0.15;
        string condition = target.Condition.ToLowerInvariant();

        bool compIsNew = compText.Contains("novograd") || compText.Contains("novogradnja")
                      || compText.Contains("novo") || compText.Contains("novouređ");
        bool compIsOld = compText.Contains("adaptac") || compText.Contains("renovir")
                      || compText.Contains("starograd") || compText.Contains("potrebna obnova");

        bool targetIsNew = condition.Contains("nov") || condition.Contains("novograd");
        bool targetIsOld = condition.Contains("star") || condition.Contains("adapt")
                        || condition.Contains("renovir") || condition.Contains("obnov");

        if ((targetIsNew && compIsNew) || (targetIsOld && compIsOld))
        {
            score += 0.15; // Condition matches
        }
        else if (!compIsNew && !compIsOld)
        {
            score += 0.08; // Unknown condition — neutral
        }
        else
        {
            score += 0.03; // Condition mismatch
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
    //  ADAPTIVE REALIZATION GAP
    // ══════════════════════════════════════════════════════════
    private double CalculateAdaptiveGap(
        double coeffVar,
        SearchPhase searchPhase,
        int compCount,
        string priceSource)
    {
        // Base gap: asking vs. realized transaction price
        double gap;

        if (priceSource == "Benchmark Only")
        {
            // Benchmark only — higher uncertainty
            gap = -0.20;
        }
        else if (coeffVar < 10)
        {
            // Very consistent data → small gap
            gap = -0.08;
        }
        else if (coeffVar < 20)
        {
            // Moderate spread
            gap = -0.12;
        }
        else if (coeffVar < 35)
        {
            // Wide spread
            gap = -0.15;
        }
        else
        {
            // Very high variation
            gap = -0.18;
        }

        // Penalty for late search phases (data from wider region = less precise)
        if (searchPhase >= SearchPhase.SubLocationDrill)
        {
            gap -= 0.04; // Extra -4% for phase 4/5
        }
        else if (searchPhase >= SearchPhase.SynonymBlitz)
        {
            gap -= 0.02; // Extra -2% for phase 3
        }

        // Penalty for very few comps
        if (compCount == 1)
        {
            gap -= 0.03; // Extra -3% for single comp
        }
        else if (compCount == 2)
        {
            gap -= 0.01; // Extra -1% for only 2
        }

        // Clamp: never less than -25%, never positive
        return Math.Clamp(gap, -0.25, 0.0);
    }

    private string BuildGapReasoning(double coeffVar, SearchPhase phase, int compCount, double gap)
    {
        var parts = new List<string>();

        if (compCount == 0)
            parts.Add("Nema usporedivih oglasa — koriL?ten samo benchmark (+20% nesigurnost)");
        else if (coeffVar < 10)
            parts.Add($"Nizak CV ({coeffVar:F1}%) — podaci su konzistentni, manji gap");
        else if (coeffVar < 20)
            parts.Add($"Umjeren CV ({coeffVar:F1}%) — standardni gap");
        else
            parts.Add($"Visok CV ({coeffVar:F1}%) — velika rasprL?enost, veći gap");

        if (phase >= SearchPhase.SubLocationDrill)
            parts.Add("Kasna faza pretraLlivanja — podaci iz L?ire regije");
        if (compCount <= 2 && compCount > 0)
            parts.Add($"Samo {compCount} usporediv(a) — ograni�Tena pouzdanost");

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

        // Penalty: benchmark only (no real comparables)
        if (priceSource == "Benchmark Only")
        {
            score -= 30;
            penalties.Add("�?�30: samo benchmark (nema usporedivih oglasa)");
        }

        // Penalty: few comparables
        if (compCount == 0)
        {
            score -= 20;
            penalties.Add("�?�20: nema cijena iz oglasa");
        }
        else if (compCount < 3)
        {
            score -= 15;
            penalties.Add($"�?�15: malo usporedivih ({compCount})");
        }
        else if (compCount < 5)
        {
            score -= 5;
            penalties.Add($"�?�5: umjereno usporedivih ({compCount})");
        }

        // Penalty: high variation
        if (coeffVar > 35)
        {
            score -= 15;
            penalties.Add($"�?�15: vrlo visok CV ({coeffVar:F1}%)");
        }
        else if (coeffVar > 20)
        {
            score -= 10;
            penalties.Add($"�?�10: visok CV ({coeffVar:F1}%)");
        }
        else if (coeffVar > 10)
        {
            score -= 3;
            penalties.Add($"�?�3: umjeren CV ({coeffVar:F1}%)");
        }

        // Penalty: late search phase
        if (searchPhase >= SearchPhase.RegionalFallback)
        {
            score -= 15;
            penalties.Add("�?�15: regionalni fallback faza");
        }
        else if (searchPhase >= SearchPhase.SubLocationDrill)
        {
            score -= 10;
            penalties.Add("�?�10: kasna faza pretraLlivanja");
        }
        else if (searchPhase >= SearchPhase.SynonymBlitz)
        {
            score -= 5;
            penalties.Add("�?�5: synonym blitz faza");
        }

        // Penalty: IQR fallback (means IQR was too aggressive)
        if (outlierMethod.Contains("→"))
        {
            score -= 5;
            penalties.Add("�?�5: IQR fallback koriL?ten");
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
            : $"Score {score}/100 — izvrsna pouzdanost.";

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
            || allText.Contains("ruL?evin") || allText.Contains("zapuL?ten"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Potrebna adaptacija",
                Description = "Nekretnina zahtijeva zna�Tajnu adaptaciju",
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
        if (allText.Contains("otok") || allText.Contains("oto�Tn"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Oto�Tna lokacija",
                Description = "Oto�Tna nekretnina — ograni�Tena ponuda",
                Percent = 8.0
            });
        }

        // ── Small apartment (<35m�?) ──
        if (property.SizeM2 > 0 && property.SizeM2 < 35
            && marketData.DetectedPropertyType.Contains("stan", StringComparison.OrdinalIgnoreCase))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Mali stan",
                Description = "Stanovi <35 m�? imaju veću cijenu po m�?",
                Percent = 7.0
            });
        }

        // ── Very large property (>200m�?) ──
        if (property.SizeM2 > 200)
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Velika nekretnina",
                Description = "Nekretnine >200 m�? obi�Tno imaju niLli €/m�?",
                Percent = -7.0
            });
        }

        // ── Bad infrastructure ──
        if (allText.Contains("neasfalt") || allText.Contains("bez pristup")
            || allText.Contains("makadamsk") || allText.Contains("L?umski put"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "LoL?a infrastruktura",
                Description = "Neasfaltirani pristup / loL?a infrastruktura",
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
                Description = "NerijeL?ena legalizacija — pravni rizik",
                Percent = -12.0
            });
        }

        // ── Parking / garage ──
        if (allText.Contains("garaLla") || allText.Contains("garaza")
            || allText.Contains("parking") || allText.Contains("garaLlno mjesto"))
        {
            adj.Add(new ValuationAdjustment
            {
                Factor = "Parking/garaLla",
                Description = "Uklju�Ten parking ili garaLla",
                Percent = 5.0
            });
        }

        // ── Land — agricultural vs building ──
        if (marketData.DetectedPropertyType.Contains("zemljiL?t", StringComparison.OrdinalIgnoreCase)
            || marketData.DetectedPropertyType.Contains("parcela", StringComparison.OrdinalIgnoreCase))
        {
            if (allText.Contains("poljopriv") || allText.Contains("oranica")
                || allText.Contains("L?uma") || allText.Contains("paL?njak"))
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Poljoprivredno zemljiL?te",
                    Description = "Poljoprivredno/L?umsko — zna�Tajno niLla cijena od građevinskog",
                    Percent = -35.0
                });
            }
            else if (allText.Contains("građevinsk") || allText.Contains("urbaniziran"))
            {
                adj.Add(new ValuationAdjustment
                {
                    Factor = "Građevinsko zemljiL?te",
                    Description = "Građevinsko zemljiL?te s dozvolom",
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
        double maxPpm2 = isLand ? 1500 : 15000;  // Land rarely above 1500 €/m�?

        foreach (var listing in listings)
        {
            string text = $"{listing.Title} {listing.Content}";

            // ── RENT-TRAP: Skip rental listings ──
            if (IsRentalListing(text))
                continue;

            // Try price per m�? first (most reliable)
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
                        SourceUrl = listing.Url
                    });
                    continue;
                }
            }

            // Try absolute price + size → calculate per m�?
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
                        // Below €1000: treat as price/m�? only if �A floor
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
                            SourceUrl = listing.Url
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
}

internal class MathConfidence
{
    public int Score { get; set; }
    public string Level { get; set; } = "";
    public string Explanation { get; set; } = "";
}
