namespace Backend.Models;

// --- Valuation Request ---

public class ValuationRequest
{
    public TargetPropertyInfo TargetProperty { get; set; } = new();
    public List<string> Comparables { get; set; } = [];
}

public class TargetPropertyInfo
{
    public string Description { get; set; } = string.Empty;
    public double SizeM2 { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
}

// --- Valuation Response ---

public class ValuationResult
{
    public double EstimatedValueEur { get; set; }
    public double PricePerM2 { get; set; }
    public string RationaleText { get; set; } = string.Empty;
}

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

    // ── Deterministic engine fields (NEW) ──
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

    // ── V3 Confidence Scoring ──
    public int ConfidenceScore { get; set; }
    public string ConfidenceExplanation { get; set; } = "";
    public string ConfidenceColor { get; set; } = "";
    public string SearchPhaseUsed { get; set; } = "";

    // ── Supabase ──
    public string? EvaluationId { get; set; }
}
