using Backend.Services;

namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Perplexity AI Test Validation — input/output → AI verdict
// ═══════════════════════════════════════════════════════════

/// <summary>
/// AI verdict for a single test scenario: does the engine output
/// make sense for the given input, according to real-world Croatian market data?
/// </summary>
public class TestScenarioVerdict
{
    public string ScenarioName { get; set; } = "";
    public string Verdict { get; set; } = "";           // "PASS", "WARN", "FAIL"
    public int ConfidenceScore { get; set; }             // 0-100
    public string Reasoning { get; set; } = "";          // Detailed explanation in Croatian
    public string MarketReference { get; set; } = "";    // Real market data Perplexity found
    public string PriceAssessment { get; set; } = "";    // Is the price/m² realistic?
    public string MethodologyAssessment { get; set; } = ""; // Is the methodology sound?
    public List<string> Issues { get; set; } = [];       // Any problems found
    public List<string> SourceUrls { get; set; } = [];   // Perplexity citations
}

/// <summary>
/// Complete result of running all test scenarios through Perplexity validation.
/// </summary>
public class TestValidationReport
{
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    public string ModelUsed { get; set; } = "";
    public int TotalScenarios { get; set; }
    public int Passed { get; set; }
    public int Warnings { get; set; }
    public int Failed { get; set; }
    public int TotalTokensUsed { get; set; }
    public List<TestScenarioVerdict> Scenarios { get; set; } = [];
}

/// <summary>
/// Defines a test scenario: property input + market data + engine result.
/// Passed to Perplexity for validation.
/// </summary>
public class TestScenarioInput
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ExtractedPropertyInfo Property { get; set; } = new();
    public MarketResearchResult Market { get; set; } = new();
    public DeterministicValuationSnapshot Result { get; set; } = new();
}

/// <summary>
/// Lightweight snapshot of engine output for Perplexity to evaluate.
/// </summary>
public class DeterministicValuationSnapshot
{
    public int EstimatedValueEur { get; set; }
    public double CalculatedPricePerM2 { get; set; }
    public double MarketAverageAsking { get; set; }
    public double RealizationDiscountPercent { get; set; }
    public double TotalAdjustmentPercent { get; set; }
    public int UsablePricePoints { get; set; }
    public int OutliersRemoved { get; set; }
    public double CoefficientOfVariation { get; set; }
    public double StdDevPerM2 { get; set; }
    public string ConfidenceLevel { get; set; } = "";
    public string SherlockInsight { get; set; } = "";
    public List<string> Adjustments { get; set; } = [];
    public List<string> Pipeline { get; set; } = [];
}
