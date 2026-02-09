using System.Diagnostics;
using Backend.Models;
using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// V6 Valuation Pipeline — The Master Orchestrator.
/// 
/// Executes the full 8-phase AVM (Automated Valuation Model) pipeline:
/// 
///   Phase 0: Canonical Modeling &amp; Enrichment
///            PDF property → CanonicalPropertyModel with geo/demographic data
///   
///   Phase 1: Candidate Generation (High Recall)
///            Broad Tavily search → ~50 raw candidates with prices
///   
///   Phase 2: Controlled Deep Fetch
///            Top 30 candidates → HTTP GET for full descriptions
///   
///   Phase 3: Structured Extraction (NLP + Embeddings)
///            Raw text → structured features + vector embeddings
///   
///   Phase 4: Multi-Dimensional Similarity Engine
///            Score = w₁·S_loc + w₂·S_area + w₃·S_cond + w₄·S_feat + w₅·S_sem
///   
///   Phase 5: Hard Gating &amp; Adaptive Sampling
///            Keep only Score ≥ 0.75, require N ≥ 15, relax if needed
///   
///   Phase 6: DBSCAN Clustering (Market Segmentation)
///            Identify peer group, select cluster nearest to subject
///   
///   Phase 7: Valuation Logic
///            IQR outlier removal, weighted median, attribute adjustments
///   
///   Phase 8: Explainability (XAI)
///            Shapley-like impact factors, confidence scoring, cluster labels
/// </summary>
public class V6ValuationPipeline : IValuationPipeline
{
    private readonly ICandidateGenerator _candidateGenerator;
    private readonly IDeepFetcher _deepFetcher;
    private readonly INlpExtractor _nlpExtractor;
    private readonly ISimilarityEngine _similarityEngine;
    private readonly ILogger<V6ValuationPipeline> _logger;

    // ── Phase 5 adaptive parameters ──
    private const double InitialScoreThreshold = 0.75;
    private const double MinScoreThreshold = 0.45;
    private const double ThresholdRelaxStep = 0.05;
    private const int MinRequiredComparables = 15;
    private const int AbsoluteMinComparables = 5;

    // ── Phase 6 DBSCAN parameters ──
    private const double DbscanEps = 0.30;      // distance threshold (1 - 0.70 similarity)
    private const int DbscanMinPts = 3;          // min cluster size

    // ── Phase 7 adjustment coefficients ──
    private static readonly Dictionary<string, double> AdjustmentCoefficients = new()
    {
        { "sea_view", 0.15 },     // +15% for sea view
        { "first_row", 0.25 },    // +25% for first row to sea
        { "pool", 0.10 },         // +10% for pool
        { "garden", 0.05 },       // +5% for garden
        { "garage", 0.05 },       // +5% for garage
        { "parking", 0.03 },      // +3% for parking
        { "elevator", 0.03 },     // +3% for elevator
        { "balcony", 0.02 },      // +2% for balcony/terrace
        { "terrace", 0.02 },
        { "loggia", 0.02 },
        { "air_conditioning", 0.02 },
        { "central_heating", 0.02 },
        { "furnished", 0.03 },
        { "basement", 0.01 },
        { "attic", 0.01 },
    };

    // ── Condition adjustment per ordinal step ──
    // e.g., subject=Renovated(3) vs cluster median=Good(2) → +8%
    private const double ConditionStepAdjustment = 0.08;

    // ── Realization gap (asking → realized price) ──
    private const double DefaultRealizationGap = -0.12;  // -12% baseline

    // ══════════════════════════════════════════════════════════
    //  REGIONAL BENCHMARKS (fallback when pipeline yields 0)
    // ══════════════════════════════════════════════════════════
    private static readonly Dictionary<string, double> RegionalBenchmarks =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Zagreb", 2800 }, { "Split", 3400 }, { "Dubrovnik", 4500 },
        { "Rijeka", 2200 }, { "Zadar", 2800 }, { "Osijek", 1200 },
        { "Pula", 2400 }, { "Šibenik", 2500 }, { "Rovinj", 3800 },
        { "Opatija", 3500 }, { "Hvar", 4000 }, { "Korčula", 2600 },
        { "Makarska", 3200 }, { "Trogir", 3000 }, { "Varaždin", 1400 },
    };

    public V6ValuationPipeline(
        ICandidateGenerator candidateGenerator,
        IDeepFetcher deepFetcher,
        INlpExtractor nlpExtractor,
        ISimilarityEngine similarityEngine,
        ILogger<V6ValuationPipeline> logger)
    {
        _candidateGenerator = candidateGenerator;
        _deepFetcher = deepFetcher;
        _nlpExtractor = nlpExtractor;
        _similarityEngine = similarityEngine;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT — ExecuteAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<V6ValuationReport> ExecuteAsync(
        string pdfText,
        ExtractedPropertyInfo extractedProperty,
        CancellationToken cancellationToken = default)
    {
        var totalTimer = Stopwatch.StartNew();
        var report = new V6ValuationReport();
        var pipeline = new List<string>();
        var phaseDurations = new Dictionary<string, double>();

        try
        {
            // ────────────────────────────────────────────────────
            // PHASE 0: Canonical Modeling & Enrichment
            // ────────────────────────────────────────────────────
            var phase0Timer = Stopwatch.StartNew();
            var subject = BuildCanonicalSubject(extractedProperty, pdfText);
            report.Subject = subject;

            // Select similarity weights based on property type / location
            var weights = SelectWeights(subject);

            phase0Timer.Stop();
            phaseDurations["Phase0_Enrichment"] = phase0Timer.ElapsedMilliseconds;
            pipeline.Add($"Phase 0: Subject canonicalized — {subject.PropertyType}, " +
                $"{subject.SizeM2:F0} m², {subject.Location}, condition={subject.ConditionOrdinal}");
            pipeline.Add($"  Weights: Loc={weights.LocationWeight}, Area={weights.AreaWeight}, " +
                $"Cond={weights.ConditionWeight}, Feat={weights.FeaturesWeight}, Sem={weights.SemanticWeight}");

            // ────────────────────────────────────────────────────
            // PHASE 1: Candidate Generation (High Recall)
            // ────────────────────────────────────────────────────
            var phase1Timer = Stopwatch.StartNew();
            var rawCandidates = await _candidateGenerator.GenerateCandidatesAsync(subject, cancellationToken);
            phase1Timer.Stop();
            phaseDurations["Phase1_CandidateGeneration"] = phase1Timer.ElapsedMilliseconds;
            report.CandidatesGenerated = rawCandidates.Count;
            pipeline.Add($"Phase 1: {rawCandidates.Count} candidates generated ({phase1Timer.ElapsedMilliseconds}ms)");

            if (rawCandidates.Count == 0)
            {
                pipeline.Add("Phase 1: NO candidates found — using benchmark fallback");
                return BuildBenchmarkFallback(report, subject, pipeline, phaseDurations, totalTimer);
            }

            // ────────────────────────────────────────────────────
            // PHASE 2: Controlled Deep Fetch
            // ────────────────────────────────────────────────────
            var phase2Timer = Stopwatch.StartNew();
            var deepFetchedListings = await _deepFetcher.DeepFetchAsync(rawCandidates, 30, cancellationToken);
            phase2Timer.Stop();
            phaseDurations["Phase2_DeepFetch"] = phase2Timer.ElapsedMilliseconds;
            report.DeepFetched = deepFetchedListings.Count(l => l.DeepFetched);
            pipeline.Add($"Phase 2: {report.DeepFetched}/{deepFetchedListings.Count} " +
                $"deep-fetched ({phase2Timer.ElapsedMilliseconds}ms)");

            // ────────────────────────────────────────────────────
            // PHASE 3: Structured Extraction (NLP + Embeddings)
            // ────────────────────────────────────────────────────
            var phase3Timer = Stopwatch.StartNew();
            var canonicalCandidates = await _nlpExtractor.ExtractAndEmbedAsync(deepFetchedListings, cancellationToken);
            phase3Timer.Stop();
            phaseDurations["Phase3_NlpExtraction"] = phase3Timer.ElapsedMilliseconds;
            report.NlpParsed = canonicalCandidates.Count;
            pipeline.Add($"Phase 3: {canonicalCandidates.Count} canonical models created " +
                $"({phase3Timer.ElapsedMilliseconds}ms)");

            if (canonicalCandidates.Count == 0)
            {
                pipeline.Add("Phase 3: NO valid models — using benchmark fallback");
                return BuildBenchmarkFallback(report, subject, pipeline, phaseDurations, totalTimer);
            }

            // ── Also generate embedding for subject ──
            // (done in Phase 3 context for consistency)

            // ────────────────────────────────────────────────────
            // PHASE 4: Multi-Dimensional Similarity Engine
            // ────────────────────────────────────────────────────
            var phase4Timer = Stopwatch.StartNew();

            foreach (var candidate in canonicalCandidates)
            {
                var simVector = _similarityEngine.CalculateSimilarity(subject, candidate, weights);
                candidate.SimilarityScore = simVector.WeightedTotal;
                candidate.SimilarityDetail = simVector;
            }

            // Sort by similarity descending
            canonicalCandidates = canonicalCandidates
                .OrderByDescending(c => c.SimilarityScore)
                .ToList();

            phase4Timer.Stop();
            phaseDurations["Phase4_Similarity"] = phase4Timer.ElapsedMilliseconds;
            report.SimilarityScored = canonicalCandidates.Count;
            pipeline.Add($"Phase 4: Similarity scored — top={canonicalCandidates[0].SimilarityScore:F3}, " +
                $"bottom={canonicalCandidates[^1].SimilarityScore:F3} ({phase4Timer.ElapsedMilliseconds}ms)");

            // ────────────────────────────────────────────────────
            // PHASE 5: Hard Gating & Adaptive Sampling
            // ────────────────────────────────────────────────────
            var phase5Timer = Stopwatch.StartNew();
            double currentThreshold = InitialScoreThreshold;
            List<CanonicalPropertyModel> gatedCandidates;

            do
            {
                gatedCandidates = canonicalCandidates
                    .Where(c => c.SimilarityScore >= currentThreshold)
                    .ToList();

                if (gatedCandidates.Count >= MinRequiredComparables)
                    break;

                pipeline.Add($"Phase 5: Only {gatedCandidates.Count} at threshold {currentThreshold:F2} " +
                    $"— relaxing to {currentThreshold - ThresholdRelaxStep:F2}");
                currentThreshold -= ThresholdRelaxStep;

            } while (currentThreshold >= MinScoreThreshold);

            // If still not enough, take all available with minimum threshold
            if (gatedCandidates.Count < AbsoluteMinComparables)
            {
                gatedCandidates = canonicalCandidates
                    .OrderByDescending(c => c.SimilarityScore)
                    .Take(Math.Max(AbsoluteMinComparables, canonicalCandidates.Count))
                    .ToList();
                pipeline.Add($"Phase 5: Fallback — took top {gatedCandidates.Count} regardless of threshold");
            }

            phase5Timer.Stop();
            phaseDurations["Phase5_Gating"] = phase5Timer.ElapsedMilliseconds;
            report.PassedGating = gatedCandidates.Count;
            pipeline.Add($"Phase 5: {gatedCandidates.Count} passed gating " +
                $"(threshold={currentThreshold:F2}, {phase5Timer.ElapsedMilliseconds}ms)");

            // ────────────────────────────────────────────────────
            // PHASE 6: DBSCAN Clustering (Market Segmentation)
            // ────────────────────────────────────────────────────
            var phase6Timer = Stopwatch.StartNew();

            var (selectedCluster, clusterLabel, totalClusters) =
                PerformClustering(subject, gatedCandidates, weights);

            phase6Timer.Stop();
            phaseDurations["Phase6_Clustering"] = phase6Timer.ElapsedMilliseconds;
            report.ClustersFound = totalClusters;
            report.SelectedClusterSize = selectedCluster.Count;
            report.ClusterLabel = clusterLabel;
            report.PeerGroup = selectedCluster;
            pipeline.Add($"Phase 6: {totalClusters} clusters found, selected '{clusterLabel}' " +
                $"with {selectedCluster.Count} members ({phase6Timer.ElapsedMilliseconds}ms)");

            // ────────────────────────────────────────────────────
            // PHASE 7: Valuation Logic
            // ────────────────────────────────────────────────────
            var phase7Timer = Stopwatch.StartNew();

            var (estimatedValue, pricePerM2, medianPpsqm, adjustments,
                totalAdjPct, outlierStats) = CalculateValuation(subject, selectedCluster);

            phase7Timer.Stop();
            phaseDurations["Phase7_Valuation"] = phase7Timer.ElapsedMilliseconds;

            report.EstimatedValueEur = estimatedValue;
            report.PricePerM2 = pricePerM2;
            report.ClusterMedianPricePerM2 = medianPpsqm;
            report.Adjustments = adjustments;
            report.TotalAdjustmentPercent = totalAdjPct;
            report.Q1 = outlierStats.Q1;
            report.Q3 = outlierStats.Q3;
            report.IQR = outlierStats.IQR;
            report.OutliersRemoved = outlierStats.Removed;
            report.AfterOutlierRemoval = outlierStats.Remaining;

            pipeline.Add($"Phase 7: Median €/m²={medianPpsqm:F0}, adj={totalAdjPct:+0.0;-0.0}%, " +
                $"final={pricePerM2:F0} €/m² → {estimatedValue:F0} € ({phase7Timer.ElapsedMilliseconds}ms)");

            // ────────────────────────────────────────────────────
            // PHASE 8: Explainability (XAI)
            // ────────────────────────────────────────────────────
            var phase8Timer = Stopwatch.StartNew();

            var (confidence, confidenceLevel, confidenceExplanation, impactFactors) =
                GenerateExplainability(subject, selectedCluster, report);

            phase8Timer.Stop();
            phaseDurations["Phase8_XAI"] = phase8Timer.ElapsedMilliseconds;

            report.ConfidenceScore = confidence;
            report.ConfidenceLevel = confidenceLevel;
            report.ConfidenceExplanation = confidenceExplanation;
            report.ImpactFactors = impactFactors;
            pipeline.Add($"Phase 8: Confidence={confidence}/100 ({confidenceLevel}) ({phase8Timer.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "V6 Pipeline error");
            pipeline.Add($"PIPELINE ERROR: {ex.Message}");
        }

        totalTimer.Stop();
        report.TotalDurationMs = totalTimer.ElapsedMilliseconds;
        report.PhaseDurationsMs = phaseDurations;
        report.Pipeline = pipeline;
        pipeline.Add($"===== V6 Pipeline complete: {totalTimer.ElapsedMilliseconds}ms =====");

        return report;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 0: BUILD CANONICAL SUBJECT
    // ══════════════════════════════════════════════════════════════

    private CanonicalPropertyModel BuildCanonicalSubject(
        ExtractedPropertyInfo extracted, string pdfText)
    {
        var subject = new CanonicalPropertyModel
        {
            PropertyType = InferPropertyType(extracted.Description),
            SizeM2 = extracted.SizeM2,
            Location = extracted.Location,
            ConditionOrdinal = MathHelpers.ParseConditionOrdinal(extracted.Condition),
            ConditionRaw = extracted.Condition,
            RawText = pdfText,
            CadastralParcel = ExtractCadastralParcel(pdfText),
            CadastralMunicipality = ExtractCadastralMunicipality(pdfText)
        };

        // Enrich with features from raw text
        subject.Features = ExtractSubjectFeatures(pdfText);

        // Try to detect if it's a land plot and set LandSizeM2
        if (subject.PropertyType.Contains("zemlji", StringComparison.OrdinalIgnoreCase)
            || subject.PropertyType.Contains("parcel", StringComparison.OrdinalIgnoreCase))
        {
            subject.LandSizeM2 = subject.SizeM2;
        }

        // Geo-enrichment: lookup approximate coordinates
        var coords = LookupCityCoords(subject.Location);
        if (coords.HasValue)
        {
            subject.Latitude = coords.Value.Lat;
            subject.Longitude = coords.Value.Lon;
        }

        return subject;
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 6: CLUSTERING LOGIC
    // ══════════════════════════════════════════════════════════════

    private (List<CanonicalPropertyModel> Cluster, string Label, int TotalClusters)
        PerformClustering(
            CanonicalPropertyModel subject,
            List<CanonicalPropertyModel> candidates,
            V6SimilarityWeights weights)
    {
        if (candidates.Count < DbscanMinPts * 2)
        {
            // Too few for clustering — treat all as one cluster
            string label = GenerateClusterLabel(subject, candidates);
            return (candidates, label, 1);
        }

        // Build pairwise distance matrix
        var distanceMatrix = _similarityEngine.BuildPairwiseDistanceMatrix(candidates, weights);

        // Run DBSCAN
        int[] labels = MathHelpers.DBSCAN(distanceMatrix, DbscanEps, DbscanMinPts);

        // Group candidates by cluster
        var clusters = new Dictionary<int, List<CanonicalPropertyModel>>();
        for (int i = 0; i < labels.Length; i++)
        {
            int clusterId = labels[i];
            if (!clusters.ContainsKey(clusterId))
                clusters[clusterId] = new List<CanonicalPropertyModel>();
            clusters[clusterId].Add(candidates[i]);
            candidates[i].ClusterId = clusterId;
        }

        int totalClusters = clusters.Keys.Count(k => k >= 0); // exclude noise (-1)

        _logger.LogInformation("V6 Phase 6: DBSCAN found {Clusters} clusters + {Noise} noise points",
            totalClusters, clusters.ContainsKey(-1) ? clusters[-1].Count : 0);

        if (totalClusters == 0)
        {
            // All noise — fall back to all candidates
            string fallbackLabel = GenerateClusterLabel(subject, candidates);
            return (candidates, fallbackLabel, 0);
        }

        // Select the cluster whose centroid is nearest to the subject
        int bestClusterId = -1;
        double bestAvgSimilarity = -1;

        foreach (var kvp in clusters.Where(k => k.Key >= 0))
        {
            double avgSim = kvp.Value.Average(c => c.SimilarityScore);
            if (avgSim > bestAvgSimilarity)
            {
                bestAvgSimilarity = avgSim;
                bestClusterId = kvp.Key;
            }
        }

        var selectedCluster = clusters[bestClusterId];
        string clusterLabel = GenerateClusterLabel(subject, selectedCluster);

        return (selectedCluster, clusterLabel, totalClusters);
    }

    /// <summary>
    /// Generate a human-readable label for a cluster.
    /// </summary>
    private string GenerateClusterLabel(
        CanonicalPropertyModel subject,
        List<CanonicalPropertyModel> clusterMembers)
    {
        // Determine dominant condition
        var conditionCounts = clusterMembers
            .GroupBy(c => c.ConditionOrdinal)
            .OrderByDescending(g => g.Count())
            .First();
        string conditionLabel = conditionCounts.Key switch
        {
            0 => "Ruins",
            1 => "Old",
            2 => "Standard",
            3 => "Renovated",
            4 => "New Build",
            _ => "Mixed"
        };

        // Check for coastal features
        bool isCoastal = clusterMembers.Any(c =>
            c.Features.Contains("sea_view") || c.Features.Contains("first_row"))
            || (subject.Location?.ToLower().Contains("more") ?? false);

        // Check price tier
        double avgPpsqm = clusterMembers
            .Where(c => c.PricePerM2 > 0)
            .Select(c => c.PricePerM2!.Value)
            .DefaultIfEmpty(0)
            .Average();

        string priceTier = avgPpsqm switch
        {
            > 4000 => "Premium",
            > 3000 => "Upper",
            > 2000 => "Mid-Range",
            > 1000 => "Affordable",
            _ => "Budget"
        };

        string locationSuffix = isCoastal ? "Coastal" : "Continental";

        return $"{conditionLabel} {locationSuffix} — {priceTier}";
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 7: VALUATION CALCULATION
    // ══════════════════════════════════════════════════════════════

    private (double EstimatedValue, double PricePerM2, double MedianPpsqm,
        List<V6Adjustment> Adjustments, double TotalAdjPct,
        (double Q1, double Q3, double IQR, int Removed, int Remaining) OutlierStats)
        CalculateValuation(
            CanonicalPropertyModel subject,
            List<CanonicalPropertyModel> cluster)
    {
        // ── Extract price/m² from cluster ──
        var ppsqmValues = cluster
            .Where(c => c.PricePerM2.HasValue && c.PricePerM2.Value > 0)
            .Select(c => c.PricePerM2!.Value)
            .OrderBy(v => v)
            .ToList();

        if (ppsqmValues.Count == 0)
        {
            // No prices → benchmark fallback
            double benchmark = FindBenchmark(subject.Location);
            double fallbackValue = benchmark * Math.Max(subject.SizeM2, 50);
            return (fallbackValue, benchmark, benchmark, [], 0,
                (0, 0, 0, 0, 0));
        }

        // ── IQR Outlier Removal ──
        double q1 = MathHelpers.Percentile(ppsqmValues, 25);
        double q3 = MathHelpers.Percentile(ppsqmValues, 75);
        double iqr = q3 - q1;
        double lowerBound = q1 - 1.5 * iqr;
        double upperBound = q3 + 1.5 * iqr;

        // Safety for zero IQR
        if (iqr < 1.0)
        {
            double median = ppsqmValues[ppsqmValues.Count / 2];
            lowerBound = median * 0.70;
            upperBound = median * 1.30;
        }

        var cleanValues = ppsqmValues
            .Where(v => v >= lowerBound && v <= upperBound)
            .ToList();

        int removed = ppsqmValues.Count - cleanValues.Count;

        // Fallback if too aggressive
        if (cleanValues.Count < 3 && ppsqmValues.Count >= 3)
        {
            cleanValues = ppsqmValues; // use all
            removed = 0;
        }

        // ── Weighted Median (by SimilarityScore) ──
        var weightedItems = cluster
            .Where(c => c.PricePerM2.HasValue && c.PricePerM2.Value > 0
                && cleanValues.Contains(c.PricePerM2.Value))
            .Select(c => (value: c.PricePerM2!.Value, weight: Math.Max(c.SimilarityScore, 0.1)))
            .ToList();

        double medianPpsqm = weightedItems.Count > 0
            ? MathHelpers.WeightedMedian(weightedItems)
            : cleanValues.Count > 0 ? cleanValues[cleanValues.Count / 2] : 0;

        // ── Attribute Adjustments ──
        var adjustments = new List<V6Adjustment>();
        double totalAdjPct = 0;

        // Condition adjustment
        double avgClusterCondition = cluster.Average(c => (double)c.ConditionOrdinal);
        int clusterCondition = (int)Math.Round(avgClusterCondition);
        int conditionDiff = subject.ConditionOrdinal - clusterCondition;
        if (conditionDiff != 0)
        {
            double condAdj = conditionDiff * ConditionStepAdjustment;
            condAdj = Math.Clamp(condAdj, -0.20, 0.20);
            totalAdjPct += condAdj;
            adjustments.Add(new V6Adjustment
            {
                Attribute = "Condition",
                SubjectValue = ConditionLabel(subject.ConditionOrdinal),
                ClusterMedianValue = ConditionLabel(clusterCondition),
                AdjustmentPercent = condAdj * 100,
                Reasoning = $"Subject is {Math.Abs(conditionDiff)} level(s) " +
                    $"{(conditionDiff > 0 ? "better" : "worse")} than cluster median"
            });
        }

        // Feature-based adjustments (Shapley-like)
        foreach (var kvp in AdjustmentCoefficients)
        {
            bool subjectHas = subject.Features.Contains(kvp.Key);
            double clusterPct = cluster.Count > 0
                ? (double)cluster.Count(c => c.Features.Contains(kvp.Key)) / cluster.Count
                : 0;

            if (subjectHas && clusterPct < 0.5)
            {
                // Subject has feature that <50% of cluster has → premium
                double adj = kvp.Value * (1.0 - clusterPct);
                totalAdjPct += adj;
                adjustments.Add(new V6Adjustment
                {
                    Attribute = kvp.Key,
                    SubjectValue = "Yes",
                    ClusterMedianValue = $"{clusterPct * 100:F0}% of cluster",
                    AdjustmentPercent = adj * 100,
                    Reasoning = $"Subject has {kvp.Key}, only {clusterPct * 100:F0}% of peers do"
                });
            }
            else if (!subjectHas && clusterPct > 0.5)
            {
                // Subject lacks feature that >50% of cluster has → discount
                double adj = -kvp.Value * clusterPct;
                totalAdjPct += adj;
                adjustments.Add(new V6Adjustment
                {
                    Attribute = kvp.Key,
                    SubjectValue = "No",
                    ClusterMedianValue = $"{clusterPct * 100:F0}% of cluster",
                    AdjustmentPercent = adj * 100,
                    Reasoning = $"Subject lacks {kvp.Key}, but {clusterPct * 100:F0}% of peers have it"
                });
            }
        }

        // ── Hard cap ±40% total adjustment ──
        totalAdjPct = Math.Clamp(totalAdjPct, -0.40, 0.40);

        // ── Apply realization gap (asking → realized) ──
        double realizationGap = CalculateRealizationGap(cleanValues, subject);
        totalAdjPct += realizationGap;

        adjustments.Add(new V6Adjustment
        {
            Attribute = "Realization Gap",
            SubjectValue = "Asking → Realized",
            ClusterMedianValue = $"{realizationGap * 100:F1}%",
            AdjustmentPercent = realizationGap * 100,
            Reasoning = "Discount from asking prices to realized transaction values"
        });

        // ── Final calculation ──
        double adjustedPpsqm = medianPpsqm * (1.0 + totalAdjPct);
        double area = subject.SizeM2 > 0 ? subject.SizeM2 : 50;
        double estimatedValue = adjustedPpsqm * area;

        // Sanity check against benchmark
        double sanityBenchmark = FindBenchmark(subject.Location);
        if (adjustedPpsqm > sanityBenchmark * 2.0) adjustedPpsqm = sanityBenchmark * 1.5;
        if (adjustedPpsqm < sanityBenchmark * 0.3) adjustedPpsqm = sanityBenchmark * 0.5;
        estimatedValue = adjustedPpsqm * area;

        return (
            Math.Round(estimatedValue),
            Math.Round(adjustedPpsqm),
            Math.Round(medianPpsqm),
            adjustments,
            totalAdjPct * 100,
            (q1, q3, iqr, removed, cleanValues.Count)
        );
    }

    /// <summary>
    /// Adaptive realization gap based on market conditions.
    /// </summary>
    private double CalculateRealizationGap(List<double> cleanValues, CanonicalPropertyModel subject)
    {
        double cv = MathHelpers.CoefficientOfVariation(cleanValues);

        // Low CV = stable market → smaller gap
        // High CV = volatile → larger gap
        double gap = DefaultRealizationGap;

        if (cv > 0.30) gap -= 0.05;      // volatile: -17%
        else if (cv > 0.20) gap -= 0.03; // moderate: -15%
        else if (cv < 0.10) gap += 0.04; // very stable: -8%

        // Premium locations have smaller gaps
        string loc = subject.Location?.ToLowerInvariant() ?? "";
        if (loc.Contains("dubrovnik") || loc.Contains("rovinj") || loc.Contains("hvar"))
            gap += 0.03; // -9% for premium

        return Math.Clamp(gap, -0.25, -0.05);
    }

    // ══════════════════════════════════════════════════════════════
    //  PHASE 8: EXPLAINABILITY (XAI)
    // ══════════════════════════════════════════════════════════════

    private (int Score, string Level, string Explanation, List<V6ImpactFactor> Factors)
        GenerateExplainability(
            CanonicalPropertyModel subject,
            List<CanonicalPropertyModel> cluster,
            V6ValuationReport report)
    {
        int score = 100;
        var explanations = new List<string>();

        // ── Factor 1: Sample size ──
        if (cluster.Count < 5) { score -= 30; explanations.Add($"Very small peer group ({cluster.Count})"); }
        else if (cluster.Count < 10) { score -= 15; explanations.Add($"Small peer group ({cluster.Count})"); }
        else if (cluster.Count < 15) { score -= 5; explanations.Add($"Moderate peer group ({cluster.Count})"); }
        else { explanations.Add($"Good peer group size ({cluster.Count})"); }

        // ── Factor 2: Price variance ──
        var ppsqmValues = cluster
            .Where(c => c.PricePerM2.HasValue && c.PricePerM2.Value > 0)
            .Select(c => c.PricePerM2!.Value)
            .ToList();

        if (ppsqmValues.Count > 1)
        {
            double cv = MathHelpers.CoefficientOfVariation(ppsqmValues);
            if (cv > 0.40) { score -= 25; explanations.Add($"High price variance (CV={cv:F2})"); }
            else if (cv > 0.25) { score -= 15; explanations.Add($"Moderate price variance (CV={cv:F2})"); }
            else if (cv > 0.15) { score -= 5; explanations.Add($"Acceptable price variance (CV={cv:F2})"); }
            else { explanations.Add($"Low price variance (CV={cv:F2})"); }
        }
        else
        {
            score -= 35;
            explanations.Add("Insufficient price data points");
        }

        // ── Factor 3: Similarity quality ──
        double avgSimilarity = cluster.Average(c => c.SimilarityScore);
        if (avgSimilarity < 0.50) { score -= 20; explanations.Add($"Low avg similarity ({avgSimilarity:F2})"); }
        else if (avgSimilarity < 0.65) { score -= 10; explanations.Add($"Moderate avg similarity ({avgSimilarity:F2})"); }
        else { explanations.Add($"Good avg similarity ({avgSimilarity:F2})"); }

        // ── Factor 4: Embedding coverage ──
        double embeddingCoverage = (double)cluster.Count(c => c.DescriptionEmbedding != null) / cluster.Count;
        if (embeddingCoverage < 0.3) { score -= 10; explanations.Add("Low embedding coverage (limited semantic comparison)"); }

        // ── Factor 5: Outlier removal impact ──
        if (report.OutliersRemoved > cluster.Count * 0.3)
        {
            score -= 10;
            explanations.Add($"Heavy outlier removal ({report.OutliersRemoved} removed)");
        }

        score = Math.Clamp(score, 0, 100);

        string level = score switch
        {
            >= 80 => "Very High",
            >= 60 => "High",
            >= 40 => "Medium",
            >= 20 => "Low",
            _ => "Very Low"
        };

        // ── Build Shapley-like impact factors ──
        var factors = new List<V6ImpactFactor>();

        // Decompose the weighted score into dimension contributions
        if (cluster.Count > 0)
        {
            double avgLocScore = cluster.Average(c => c.SimilarityDetail?.LocationScore ?? 0);
            double avgAreaScore = cluster.Average(c => c.SimilarityDetail?.AreaScore ?? 0);
            double avgCondScore = cluster.Average(c => c.SimilarityDetail?.ConditionScore ?? 0);
            double avgFeatScore = cluster.Average(c => c.SimilarityDetail?.FeaturesScore ?? 0);
            double avgSemScore = cluster.Average(c => c.SimilarityDetail?.SemanticScore ?? 0);

            // Impact = deviation from neutral (0.5) in each dimension
            factors.Add(new V6ImpactFactor
            {
                Dimension = "Location",
                ImpactPercent = (avgLocScore - 0.5) * 20, // scaled to ±10%
                Explanation = $"Average location match: {avgLocScore:F2}"
            });
            factors.Add(new V6ImpactFactor
            {
                Dimension = "Area",
                ImpactPercent = (avgAreaScore - 0.5) * 20,
                Explanation = $"Average area match: {avgAreaScore:F2}"
            });
            factors.Add(new V6ImpactFactor
            {
                Dimension = "Condition",
                ImpactPercent = (avgCondScore - 0.5) * 20,
                Explanation = $"Average condition match: {avgCondScore:F2}"
            });
            factors.Add(new V6ImpactFactor
            {
                Dimension = "Features",
                ImpactPercent = (avgFeatScore - 0.5) * 20,
                Explanation = $"Average feature overlap: {avgFeatScore:F2}"
            });
            factors.Add(new V6ImpactFactor
            {
                Dimension = "Semantic",
                ImpactPercent = (avgSemScore - 0.5) * 20,
                Explanation = $"Average semantic match: {avgSemScore:F2}"
            });
        }

        // Add adjustment impacts
        foreach (var adj in report.Adjustments.Where(a => Math.Abs(a.AdjustmentPercent) > 0.5))
        {
            factors.Add(new V6ImpactFactor
            {
                Dimension = adj.Attribute,
                ImpactPercent = adj.AdjustmentPercent,
                Explanation = adj.Reasoning
            });
        }

        string explanation = string.Join(". ", explanations);
        return (score, level, explanation, factors);
    }

    // ══════════════════════════════════════════════════════════════
    //  UTILITY METHODS
    // ══════════════════════════════════════════════════════════════

    private static string InferPropertyType(string description)
    {
        string lower = description.ToLowerInvariant();
        if (lower.Contains("stan") || lower.Contains("apartman")) return "stan";
        if (lower.Contains("kuća") || lower.Contains("kuca") || lower.Contains("vila")) return "kuća";
        if (lower.Contains("zemlji") || lower.Contains("parcel") || lower.Contains("oranica")) return "zemljište";
        if (lower.Contains("poslovni") || lower.Contains("ured") || lower.Contains("lokal")) return "poslovni prostor";
        return "nekretnina";
    }

    private static string ExtractCadastralParcel(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:č\.?zem\.?|kč|k\.č\.|čestica)\s*[:/]?\s*(\d+(?:/\d+)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string ExtractCadastralMunicipality(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:k\.?o\.?|katastarska\s+općina)\s*[:/]?\s*([A-Za-zÀ-ŽščćžđŠČĆŽĐ\s]+?)(?:\s*[,\n\r]|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static HashSet<string> ExtractSubjectFeatures(string text)
    {
        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string lower = text.ToLowerInvariant();

        var keywordMap = new Dictionary<string, string>
        {
            { "parking", "parking" }, { "garaž", "garage" }, { "balkon", "balcony" },
            { "loža", "loggia" }, { "terasa", "terrace" }, { "vrt", "garden" },
            { "bazen", "pool" }, { "dizalo", "elevator" }, { "lift", "elevator" },
            { "klima", "air_conditioning" }, { "centralno grijanje", "central_heating" },
            { "pogled na more", "sea_view" }, { "prvi red", "first_row" },
            { "podrum", "basement" }, { "potkrovlje", "attic" },
        };

        foreach (var kvp in keywordMap)
        {
            if (lower.Contains(kvp.Key))
                features.Add(kvp.Value);
        }

        return features;
    }

    private static V6SimilarityWeights SelectWeights(CanonicalPropertyModel subject)
    {
        string type = subject.PropertyType?.ToLowerInvariant() ?? "";
        string loc = subject.Location?.ToLowerInvariant() ?? "";

        // Land/agricultural → area and location dominate
        if (type.Contains("zemlji") || type.Contains("oranica") || type.Contains("parcel"))
            return V6SimilarityWeights.Rural;

        // Coastal properties
        string[] coastalKeywords = ["more", "coast", "obala", "hvar", "dubrovnik", "split",
            "zadar", "šibenik", "makarska", "rovinj", "poreč", "umag", "opatija",
            "brač", "korčula", "vis", "krk", "rab", "trogir", "omiš"];
        if (coastalKeywords.Any(k => loc.Contains(k)))
            return V6SimilarityWeights.Coastal;

        return V6SimilarityWeights.Default;
    }

    private static string ConditionLabel(int ordinal) => ordinal switch
    {
        0 => "Ruins",
        1 => "Old",
        2 => "Good",
        3 => "Renovated",
        4 => "New",
        _ => "Unknown"
    };

    private static double FindBenchmark(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return 2000;

        if (RegionalBenchmarks.TryGetValue(location.Trim(), out double val))
            return val;

        string firstWord = location.Split(',', ' ', '-')[0].Trim();
        if (RegionalBenchmarks.TryGetValue(firstWord, out double val2))
            return val2;

        foreach (var kvp in RegionalBenchmarks)
        {
            if (location.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return 2000; // Croatia-wide average fallback
    }

    private V6ValuationReport BuildBenchmarkFallback(
        V6ValuationReport report,
        CanonicalPropertyModel subject,
        List<string> pipeline,
        Dictionary<string, double> phaseDurations,
        Stopwatch totalTimer)
    {
        double benchmark = FindBenchmark(subject.Location);
        double area = subject.SizeM2 > 0 ? subject.SizeM2 : 50;

        report.EstimatedValueEur = Math.Round(benchmark * area * 0.90); // -10% conservative
        report.PricePerM2 = benchmark * 0.90;
        report.ClusterMedianPricePerM2 = benchmark;
        report.ConfidenceScore = 15;
        report.ConfidenceLevel = "Very Low";
        report.ConfidenceExplanation = "Benchmark-only valuation — no market data found. " +
            "Value based on regional average prices.";
        report.ClusterLabel = "Benchmark Fallback";

        pipeline.Add($"FALLBACK: Using benchmark {benchmark:F0} €/m² for {subject.Location}");
        pipeline.Add($"Estimated: {report.EstimatedValueEur:F0} € ({area:F0} m² × {report.PricePerM2:F0} €/m²)");

        totalTimer.Stop();
        report.TotalDurationMs = totalTimer.ElapsedMilliseconds;
        report.PhaseDurationsMs = phaseDurations;
        report.Pipeline = pipeline;

        return report;
    }

    private static readonly Dictionary<string, (double Lat, double Lon)> CityCoords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Zagreb", (45.8150, 15.9819) }, { "Split", (43.5081, 16.4402) },
        { "Rijeka", (45.3271, 14.4422) }, { "Osijek", (45.5511, 18.6939) },
        { "Zadar", (44.1194, 15.2314) }, { "Dubrovnik", (42.6507, 18.0944) },
        { "Pula", (44.8666, 13.8496) }, { "Šibenik", (43.7350, 15.8952) },
        { "Rovinj", (45.0811, 13.6387) }, { "Opatija", (45.3372, 14.3054) },
        { "Hvar", (43.1729, 16.4412) }, { "Makarska", (43.2969, 17.0182) },
    };

    private static (double Lat, double Lon)? LookupCityCoords(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;

        if (CityCoords.TryGetValue(location.Trim(), out var exact))
            return exact;

        foreach (var kvp in CityCoords)
        {
            if (location.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
