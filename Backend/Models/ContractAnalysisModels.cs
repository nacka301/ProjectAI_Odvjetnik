namespace Backend.Models;

/// <summary>
/// Result of AI contract analysis — structured legal audit
/// of a Croatian real estate contract (Predugovor / Glavni Ugovor).
/// V49: Two-pass architecture — AI audit → deterministic scoring.
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

    /// <summary>Risk score 0-100 (100 = extremely risky). V49: computed deterministically.</summary>
    public int RiskScore { get; set; }

    /// <summary>V49: Structured checklist — mandatory legal elements audit</summary>
    public List<ChecklistItem> Checklist { get; set; } = [];

    /// <summary>V49: Cross-article structural analysis</summary>
    public List<CrossArticleIssue> CrossArticleIssues { get; set; } = [];

    /// <summary>V49: Mandatory protective clauses detector</summary>
    public List<MandatoryClauseCheck> MandatoryClauses { get; set; } = [];

    /// <summary>V49: Breakdown of how risk score was computed</summary>
    public List<string> RiskScoreBreakdown { get; set; } = [];

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

/// <summary>V49: Structured checklist item — auditor-style binary check.</summary>
public class ChecklistItem
{
    /// <summary>Category name (e.g., "Tereti i hipoteke", "Kapara", "Prijenos vlasništva")</summary>
    public string Category { get; set; } = "";

    /// <summary>Specific check question</summary>
    public string Check { get; set; } = "";

    /// <summary>UREĐENO / DJELOMIČNO / NIJE_UREĐENO</summary>
    public string Status { get; set; } = "";

    /// <summary>NIZAK / SREDNJI / VISOK</summary>
    public string Risk { get; set; } = "";

    /// <summary>What was found in the contract</summary>
    public string Finding { get; set; } = "";

    /// <summary>Reference to contract article(s)</summary>
    public string? ArticleRef { get; set; }

    /// <summary>Mandatory recommendation if status ≠ UREĐENO</summary>
    public string? Recommendation { get; set; }
}

/// <summary>V49: Cross-article structural analysis — linked articles that conflict.</summary>
public class CrossArticleIssue
{
    /// <summary>Issue title</summary>
    public string Title { get; set; } = "";

    /// <summary>List of linked articles (e.g., ["čl. 3", "čl. 7", "čl. 10"])</summary>
    public List<string> LinkedArticles { get; set; } = [];

    /// <summary>Detailed explanation of the inconsistency</summary>
    public string Description { get; set; } = "";

    /// <summary>NIZAK / SREDNJI / VISOK</summary>
    public string Risk { get; set; } = "";
}

/// <summary>V49: Mandatory clause detector — checks for protective clauses presence.</summary>
public class MandatoryClauseCheck
{
    /// <summary>Clause name (e.g., "Escrow / uvjetna isplata")</summary>
    public string Clause { get; set; } = "";

    /// <summary>Whether the clause is present in the contract</summary>
    public bool Present { get; set; }

    /// <summary>Importance: "KRITIČNO" or "PREPORUČENO"</summary>
    public string Importance { get; set; } = "PREPORUČENO";

    /// <summary>Explanation of why this clause matters or what's missing</summary>
    public string Description { get; set; } = "";
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
