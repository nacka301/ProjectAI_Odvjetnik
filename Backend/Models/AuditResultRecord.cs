using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Audit Records
// ═══════════════════════════════════════════════════════════

/// <summary>Record written to valuation_audits table.</summary>
public class AuditResultRecord
{
    public string? UserId { get; set; }
    public string EvaluationId { get; set; } = "";
    public string Verdict { get; set; } = "";
    public int OverallConfidence { get; set; }
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public int PriceScore { get; set; }
    public int LocationScore { get; set; }
    public int MethodologyScore { get; set; }
    public int DataQualityScore { get; set; }
    public int FindingsCount { get; set; }
    public int CriticalCount { get; set; }
    public string ModelUsed { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public object? RawJsonOutput { get; set; }
}
