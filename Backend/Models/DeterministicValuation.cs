using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

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
    public bool UsedEstimatedSize { get; set; }      // V71: true when SizeM2 was unknown and estimated
    public double EstimatedSizeM2 { get; set; }       // V71: the estimated size used (0 if real size was available)
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

    // ── V39: Elaborat-style output (Pravilnik NN 105/2015) ──
    public ElaboratStatistics? ElaboratStats { get; set; }
    public MarketAdjustmentFactors? MarketFactors { get; set; }
}
