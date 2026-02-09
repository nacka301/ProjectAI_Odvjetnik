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
    public double RegionalBenchmarkPerM2 { get; set; }
    public double MarketAverageAsking { get; set; }
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
    public List<Backend.Services.UsedComparable> UsedComparables { get; set; } = [];
    public List<Backend.Services.ValuationAdjustment> Adjustments { get; set; } = [];
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
}
