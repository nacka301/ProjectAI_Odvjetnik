namespace Backend.Models.V6;

/// <summary>
/// Complete V6 Valuation Report — output of the full 8-phase pipeline.
/// Designed for maximum explainability (Phase 8 XAI).
/// </summary>
public class V6ValuationReport
{
    // ── Core valuation ───────────────────────────────────────
    public double EstimatedValueEur { get; set; }
    public double PricePerM2 { get; set; }
    public double ClusterMedianPricePerM2 { get; set; }

    // ── Subject summary ──────────────────────────────────────
    public CanonicalPropertyModel Subject { get; set; } = new();

    // ── Pipeline stats ───────────────────────────────────────
    public int CandidatesGenerated { get; set; }       // Phase 1
    public int DeepFetched { get; set; }               // Phase 2
    public int NlpParsed { get; set; }                 // Phase 3
    public int SimilarityScored { get; set; }          // Phase 4
    public int PassedGating { get; set; }              // Phase 5
    public int ClustersFound { get; set; }             // Phase 6
    public int SelectedClusterSize { get; set; }       // Phase 6 → peer group
    public int AfterOutlierRemoval { get; set; }       // Phase 7

    // ── Peer group (Phase 6 result) ──────────────────────────
    public string ClusterLabel { get; set; } = "";     // e.g. "Renovated Coastal"
    public List<CanonicalPropertyModel> PeerGroup { get; set; } = [];

    // ── Attribute adjustments (Phase 7) ─────────────────────
    public List<V6Adjustment> Adjustments { get; set; } = [];
    public double TotalAdjustmentPercent { get; set; }

    // ── Confidence (Phase 8) ─────────────────────────────────
    public int ConfidenceScore { get; set; }           // 0–100
    public string ConfidenceLevel { get; set; } = "";  // Low / Medium / High / Very High
    public string ConfidenceExplanation { get; set; } = "";

    // ── Shapley-like impact factors (Phase 8) ────────────────
    public List<V6ImpactFactor> ImpactFactors { get; set; } = [];

    // ── Full pipeline trace ──────────────────────────────────
    public List<string> Pipeline { get; set; } = [];

    // ── IQR stats ────────────────────────────────────────────
    public double Q1 { get; set; }
    public double Q3 { get; set; }
    public double IQR { get; set; }
    public int OutliersRemoved { get; set; }

    // ── Timing ───────────────────────────────────────────────
    public double TotalDurationMs { get; set; }
    public Dictionary<string, double> PhaseDurationsMs { get; set; } = new();
}

/// <summary>
/// Single attribute adjustment applied in Phase 7.
/// </summary>
public class V6Adjustment
{
    public string Attribute { get; set; } = "";
    public string SubjectValue { get; set; } = "";
    public string ClusterMedianValue { get; set; } = "";
    public double AdjustmentPercent { get; set; }
    public string Reasoning { get; set; } = "";
}

/// <summary>
/// Shapley-like impact factor for XAI output.
/// </summary>
public class V6ImpactFactor
{
    public string Dimension { get; set; } = "";        // Location, Condition, Area, Features, Semantic
    public double ImpactPercent { get; set; }          // positive = price increase vs baseline
    public string Explanation { get; set; } = "";
}

/// <summary>
/// Configuration for the V6 similarity engine weights.
/// Can be overridden per-property-type or per-region.
/// </summary>
public class V6SimilarityWeights
{
    public double LocationWeight { get; set; } = 0.30;
    public double AreaWeight { get; set; } = 0.25;
    public double ConditionWeight { get; set; } = 0.20;
    public double FeaturesWeight { get; set; } = 0.10;
    public double SemanticWeight { get; set; } = 0.15;

    /// <summary>
    /// Verifies weights sum to 1.0 (within tolerance).
    /// </summary>
    public bool IsValid => Math.Abs(LocationWeight + AreaWeight + ConditionWeight
        + FeaturesWeight + SemanticWeight - 1.0) < 0.001;

    public static V6SimilarityWeights Default => new();

    /// <summary>
    /// Coastal properties: location matters more.
    /// </summary>
    public static V6SimilarityWeights Coastal => new()
    {
        LocationWeight = 0.35,
        AreaWeight = 0.20,
        ConditionWeight = 0.15,
        FeaturesWeight = 0.10,
        SemanticWeight = 0.20
    };

    /// <summary>
    /// Rural / land: area and location dominate.
    /// </summary>
    public static V6SimilarityWeights Rural => new()
    {
        LocationWeight = 0.35,
        AreaWeight = 0.35,
        ConditionWeight = 0.05,
        FeaturesWeight = 0.05,
        SemanticWeight = 0.20
    };
}
