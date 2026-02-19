namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Perplexity AI Audit Layer — Automatic Valuation Verification
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Verdict issued by the Perplexity audit: CONFIRMED / SUSPICIOUS / REJECTED.
/// </summary>
public enum AuditVerdict
{
    /// <summary>Valuation looks reasonable — no manual review needed.</summary>
    CONFIRMED,

    /// <summary>Some concerns found — manual review recommended.</summary>
    SUSPICIOUS,

    /// <summary>Significant issues detected — valuation should not be used as-is.</summary>
    REJECTED
}

/// <summary>
/// Severity of an individual audit finding.
/// </summary>
public enum FindingSeverity
{
    INFO,
    WARNING,
    CRITICAL
}

/// <summary>
/// Individual finding from the audit — e.g. "Price per m² is 40% above market average".
/// </summary>
public class AuditFinding
{
    public string Category { get; set; } = "";
    public FindingSeverity Severity { get; set; } = FindingSeverity.INFO;
    public string Description { get; set; } = "";
    public string? Evidence { get; set; }
}

/// <summary>
/// Score for a specific audit dimension (0-100).
/// </summary>
public class DimensionScore
{
    public int Score { get; set; }
    public string Label { get; set; } = "";
    public string Reasoning { get; set; } = "";
}

/// <summary>
/// Complete result from the Perplexity audit of a single valuation.
/// </summary>
public class ValuationAuditResult
{
    // ── Core verdict ──
    public AuditVerdict Verdict { get; set; } = AuditVerdict.SUSPICIOUS;
    public int OverallConfidence { get; set; }
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";

    // ── Dimension scores ──
    public DimensionScore PriceCheck { get; set; } = new();
    public DimensionScore LocationCheck { get; set; } = new();
    public DimensionScore MethodologyCheck { get; set; } = new();
    public DimensionScore DataQualityCheck { get; set; } = new();

    // ── Decision reasoning — step-by-step explanation of why this verdict was reached ──
    public string DecisionReasoning { get; set; } = "";

    // ── Detailed findings ──
    public List<AuditFinding> Findings { get; set; } = [];

    // ── Market context from Perplexity search ──
    public string MarketContext { get; set; } = "";
    public List<string> SourceUrls { get; set; } = [];

    // ── Metadata ──
    public string EvaluationId { get; set; } = "";
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;
    public string ModelUsed { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

/// <summary>Request to audit a single valuation by its Supabase ID.</summary>
public class AuditRequest
{
    public string EvaluationId { get; set; } = "";
}

/// <summary>Request to audit multiple valuations at once.</summary>
public class BatchAuditRequest
{
    public List<string> EvaluationIds { get; set; } = [];
}

/// <summary>Response for batch audit — maps each evaluation ID to its result.</summary>
public class BatchAuditResponse
{
    public int Total { get; set; }
    public int Confirmed { get; set; }
    public int Suspicious { get; set; }
    public int Rejected { get; set; }
    public int Failed { get; set; }
    public List<BatchAuditItem> Results { get; set; } = [];
}

public class BatchAuditItem
{
    public string EvaluationId { get; set; } = "";
    public bool Success { get; set; }
    public ValuationAuditResult? Result { get; set; }
    public string? Error { get; set; }
}
