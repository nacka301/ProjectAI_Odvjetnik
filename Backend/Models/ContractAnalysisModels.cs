namespace Backend.Models;

/// <summary>
/// Result of AI contract analysis — structured legal review
/// of a Croatian real estate contract (Predugovor / Glavni Ugovor).
/// </summary>
public class ContractAnalysisResult
{
    /// <summary>Type of document detected: "Predugovor", "Glavni Ugovor", "Aneks", etc.</summary>
    public string DocumentType { get; set; } = "";

    /// <summary>Brief one-line summary of the contract.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Critical deal-breaker issues (🔴)</summary>
    public List<ContractIssue> RedFlags { get; set; } = [];

    /// <summary>Warnings and negotiation points (🟡)</summary>
    public List<ContractIssue> Warnings { get; set; } = [];

    /// <summary>Positive aspects of the contract (✅)</summary>
    public List<string> Positives { get; set; } = [];

    /// <summary>Concrete suggested article amendments</summary>
    public List<SuggestedAmendment> SuggestedAmendments { get; set; } = [];

    /// <summary>Overall risk level: "Visok", "Srednji", "Nizak"</summary>
    public string OverallRisk { get; set; } = "";

    /// <summary>Risk score 0-100 (100 = extremely risky)</summary>
    public int RiskScore { get; set; }

    /// <summary>V42: All norms cited in this analysis — structured for validity badges</summary>
    public List<CitedNorm> CitedNorms { get; set; } = [];

    /// <summary>Legal disclaimer (always present)</summary>
    public string Disclaimer { get; set; } = "";
}

public class ContractIssue
{
    /// <summary>Short issue title</summary>
    public string Title { get; set; } = "";

    /// <summary>Detailed explanation of the risk</summary>
    public string Description { get; set; } = "";

    /// <summary>Quoted text from the contract (if applicable)</summary>
    public string? QuotedText { get; set; }

    /// <summary>Legal basis — relevant law article (e.g., "čl. 316. ZOO")</summary>
    public string? LegalBasis { get; set; }
}

public class SuggestedAmendment
{
    /// <summary>Which article/clause this amends</summary>
    public string ArticleReference { get; set; } = "";

    /// <summary>What the current text says (or is missing)</summary>
    public string CurrentText { get; set; } = "";

    /// <summary>Proposed replacement text</summary>
    public string ProposedText { get; set; } = "";

    /// <summary>Why this change protects the buyer</summary>
    public string Rationale { get; set; } = "";
}
