namespace Backend.Models;

// --- Auto-Valuation Response ---

public class AutoValuationResult
{
    public string TargetPropertySummary { get; set; } = string.Empty;
    public List<SelectedComparable> SelectedComparables { get; set; } = [];
    public double EstimatedValueEur { get; set; }
    public List<ValueDriver> ValueDrivers { get; set; } = [];
    public string ValuationReasoning { get; set; } = string.Empty;
}

public class ValueDriver
{
    public string Factor { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public double AdjustmentPercent { get; set; }
}

// --- Structured Valuation Output (V14) ---

/// <summary>Row in the transparent comparison table — computed server-side from engine data.</summary>
public class ComparisonTableRow
{
    public int Index { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public double AreaM2 { get; set; }
    public double TotalPrice { get; set; }
    public double RawPricePerM2 { get; set; }
    public double CorrectionPercent { get; set; }
    public double AdjustedPricePerM2 { get; set; }
    public double SimilarityScore { get; set; }

    // V18: Yard area + similarity reason
    public double? YardAreaM2 { get; set; }
    public string? SimilarityReason { get; set; }

    // V20: Deep dive status — what happened when we tried to scrape this listing
    public string? DeepDiveStatus { get; set; }

    // V20: Per-comparable correction reason
    public string? CorrectionReason { get; set; }

    // ── V39: Elaborat-style per-comparable correction coefficients ──
    // Based on methodology from Procjembeni elaborat (Pravilnik NN 105/2015)
    /// <summary>Koeficijent stanja (condition): 0.70–1.10 (FK Matrica inspired)</summary>
    public double Kc { get; set; } = 1.0;
    /// <summary>Koeficijent lokacije (micro-location): 0.80–1.20</summary>
    public double Kl { get; set; } = 1.0;
    /// <summary>Koeficijent veličine/površine (size): 0.90–1.10</summary>
    public double Ks { get; set; } = 1.0;
    /// <summary>Koeficijent orijentacije (orientation/floor/view): 0.90–1.10</summary>
    public double Ko { get; set; } = 1.0;
    /// <summary>Product of all correction coefficients: Kc × Kl × Ks × Ko</summary>
    public double KTotal { get; set; } = 1.0;
}

/// <summary>
/// V39: Elaborat-style statistical summary for comparison method.
/// Mirrors "Statistička obrada" section from professional elaborats.
/// </summary>
public class ElaboratStatistics
{
    /// <summary>Aritmetička sredina korigiranih cijena (€/m²)</summary>
    public double ArithmeticMeanPerM2 { get; set; }
    /// <summary>Standardno odstupanje (€/m²)</summary>
    public double StdDevPerM2 { get; set; }
    /// <summary>Relativno standardno odstupanje (%)</summary>
    public double RelativeStdDevPercent { get; set; }
    /// <summary>Per-comparable deviations from mean (%)</summary>
    public List<double> DeviationsPercent { get; set; } = [];
    /// <summary>True if all comparables pass the 2σ test</summary>
    public bool TwoSigmaTestPassed { get; set; }
    /// <summary>Number of comparables that failed 2σ and were excluded</summary>
    public int TwoSigmaExcluded { get; set; }
    /// <summary>Human-readable explanation in Croatian</summary>
    public string Explanation { get; set; } = "";
}

/// <summary>
/// V39: Market adjustment factors applied AFTER the comparison table averaging.
/// Based on Pravilnik čl. 61/62 methodology.
/// </summary>
public class MarketAdjustmentFactors
{
    /// <summary>Fp — Faktor površine/veličine. Houses &lt;100m² = +0.05, Land &gt;5000m² = -0.05, etc.</summary>
    public double Fp { get; set; }
    /// <summary>Ft — Faktor tržišta (ponuda/potražnja, tip lokacije). Rural = -0.20, Coast = +0.10, etc.</summary>
    public double Ft { get; set; }
    /// <summary>Combined = 1 + Fp + Ft</summary>
    public double Combined { get; set; } = 1.0;
    /// <summary>Human-readable explanation of each factor</summary>
    public string FpReason { get; set; } = "";
    public string FtReason { get; set; } = "";
}

/// <summary>Three-tier price range: conservative / realistic / optimistic.</summary>
public class PriceRangeEstimate
{
    // Per m²
    public double ConservativePricePerM2 { get; set; }
    public double RealisticPricePerM2 { get; set; }
    public double OptimisticPricePerM2 { get; set; }
    // Total value
    public int ConservativeTotal { get; set; }
    public int RealisticTotal { get; set; }
    public int OptimisticTotal { get; set; }
}

public class SelectedComparable
{
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ReasonForSelection { get; set; } = string.Empty;
}

// --- PDF Property Extraction (Step 1 helper) ---

public class ExtractedPropertyInfo
{
    public string Description { get; set; } = string.Empty;
    public double SizeM2 { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;

    // V5: Fine-tuning fields
    public string PropertyState { get; set; } = "";   // "new", "renovated", "good", "original", "needs_adaptation"
    public double YardAreaM2 { get; set; }             // Okucnica / yard size for houses

    // V22: Additional information fields — user-provided notes are critical for valuation
    public string AdditionalNotes { get; set; } = "";          // Verbatim user notes from wizard
    public List<string> KeyFeatures { get; set; } = [];        // AI-extracted features: ["centar grada", "blizu mora", ...]
    public string MicroLocation { get; set; } = "";            // Specific neighborhood/micro-location if detected
}

// --- Full Evaluation Response (V2 — deterministic + AI) ---

/// <summary>
/// Complete evaluation result returned by POST /api/evaluate.
/// Contains the AI-generated opinion AND the server-computed deterministic numbers.
/// Frontend-compatible: extends AutoValuationResult shape.
/// </summary>
public class FullEvaluationResult
{
    // ── AI-generated fields (same shape as AutoValuationResult) ──
    public string TargetPropertySummary { get; set; } = string.Empty;
    public List<SelectedComparable> SelectedComparables { get; set; } = [];
    public int EstimatedValueEur { get; set; }
    public List<ValueDriver> ValueDrivers { get; set; } = [];
    public string ValuationReasoning { get; set; } = string.Empty;

    // ── Deterministic engine fields ──
    public string ConfidenceLevel { get; set; } = "";
    public double MarketAverageAsking { get; set; }      // Weighted median (base)
    public double AfterDiscountPerM2 { get; set; }       // V27: After gap, before adjustments
    public double CalculatedPricePerM2 { get; set; }
    public double RealizationDiscountPercent { get; set; }
    public int TotalListingsFound { get; set; }
    public int UsablePricePoints { get; set; }
    public int OutliersRemoved { get; set; }
    public string PriceSource { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string? Note { get; set; }

    // ── V4 Robust Estimator fields ──
    public double StdDevPerM2 { get; set; }
    public double CoefficientOfVariation { get; set; }
    public string OutlierMethod { get; set; } = "";
    public List<string> OutlierLog { get; set; } = [];
    public double TotalAdjustmentPercent { get; set; }
    public string RealizationGapReasoning { get; set; } = "";
    public List<UsedComparable> UsedComparables { get; set; } = [];
    public List<ValuationAdjustment> Adjustments { get; set; } = [];
    public List<string> Pipeline { get; set; } = [];

    // ── V4 Math Confidence (replaces V3 market-only confidence) ──
    public int MathConfidenceScore { get; set; }
    public string MathConfidenceLevel { get; set; } = "";
    public string MathConfidenceExplanation { get; set; } = "";

    // ── V3 Confidence Scoring (market research) ──
    public int ConfidenceScore { get; set; }
    public string ConfidenceExplanation { get; set; } = "";
    public string ConfidenceColor { get; set; } = "";
    public string SearchPhaseUsed { get; set; } = "";

    // ── Supabase ──
    public string? EvaluationId { get; set; }

    // ── V13: Sherlock Insight ──
    public string SherlockInsight { get; set; } = "";

    // ── V14: Structured Valuation Output ──
    public List<ComparisonTableRow> ComparisonTable { get; set; } = [];
    public PriceRangeEstimate? PriceRange { get; set; }
    public string BenchmarkSummary { get; set; } = "";

    // ── V39: Elaborat-style output (Pravilnik NN 105/2015) ──
    /// <summary>Statistical analysis of comparables (arit. mean, StdDev, 2σ test)</summary>
    public ElaboratStatistics? ElaboratStats { get; set; }
    /// <summary>Market adjustment factors Fp + Ft applied after comparison table</summary>
    public MarketAdjustmentFactors? MarketFactors { get; set; }
    /// <summary>Valuation method used: "Poredbena" / "Troškovna" / "Kombinirana"</summary>
    public string ValuationMethod { get; set; } = "Poredbena";
}

