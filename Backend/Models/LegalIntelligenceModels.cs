namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  V33: Legal Intelligence — RAG-simulated Court Practice
//  Sudska Praksa + AI Prognoza Ishoda + Precedent Analysis
// ══════════════════════════════════════════════════════════════

/// <summary>
/// A single court precedent (presuda) — either from the embedded knowledge base
/// or synthesized by GPT from its training data on Croatian court practice.
/// </summary>
public class CourtPrecedent
{
    /// <summary>Case number (e.g., "Rev-1234/20-2", "Gž-456/2023")</summary>
    public string CaseNumber { get; set; } = "";

    /// <summary>Court name (e.g., "Vrhovni sud RH", "Županijski sud u Splitu")</summary>
    public string Court { get; set; } = "";

    /// <summary>Date of the ruling (e.g., "15.03.2022.")</summary>
    public string Date { get; set; } = "";

    /// <summary>Short summary of what the court decided</summary>
    public string Summary { get; set; } = "";

    /// <summary>Key legal principle established (ratio decidendi)</summary>
    public string LegalPrinciple { get; set; } = "";

    /// <summary>How this precedent relates to the current case</summary>
    public string RelevanceToCase { get; set; } = "";

    /// <summary>Whether this precedent supports or contradicts the client's position</summary>
    public string Alignment { get; set; } = "";  // "POVOLJNA", "NEPOVOLJNA", "NEUTRALNA"

    /// <summary>Legal basis — relevant law articles</summary>
    public string LegalBasis { get; set; } = "";
}

/// <summary>
/// Full legal strategy analysis result — the "Provjeri ishod na sudu" output.
/// </summary>
public class LegalStrategyResult
{
    /// <summary>The core legal issue identified from the case materials</summary>
    public string KeyLegalIssue { get; set; } = "";

    /// <summary>Sub-issues or additional legal questions</summary>
    public List<string> SubIssues { get; set; } = [];

    /// <summary>Relevant court precedents found</summary>
    public List<CourtPrecedent> Precedents { get; set; } = [];

    /// <summary>Win probability percentage (0-100)</summary>
    public int WinProbability { get; set; }

    /// <summary>Win probability label: "Visoka", "Srednja", "Niska"</summary>
    public string WinProbabilityLabel { get; set; } = "";

    /// <summary>Detailed reasoning for the probability assessment</summary>
    public string ProbabilityReasoning { get; set; } = "";

    /// <summary>Identified risks that could lower chances</summary>
    public List<LegalRisk> Risks { get; set; } = [];

    /// <summary>Identified strengths that support the case</summary>
    public List<string> Strengths { get; set; } = [];

    /// <summary>Concrete strategic recommendations</summary>
    public List<StrategicRecommendation> Recommendations { get; set; } = [];

    /// <summary>Overall strategy summary in professional Croatian</summary>
    public string StrategySummary { get; set; } = "";

    /// <summary>Estimated duration of proceedings</summary>
    public string EstimatedDuration { get; set; } = "";

    /// <summary>Estimated cost range</summary>
    public string EstimatedCost { get; set; } = "";

    /// <summary>Legal disclaimer</summary>
    public string Disclaimer { get; set; } =
        "Ova AI analiza temelji se na općim načelima sudske prakse i ne zamjenjuje pravni savjet ovlaštenog odvjetnika. " +
        "Konkretni ishod ovisi o specifičnostima slučaja, dokaznoj građi i načinu vođenja postupka.";
}

/// <summary>
/// A specific legal risk identified in the case.
/// </summary>
public class LegalRisk
{
    /// <summary>Short risk title</summary>
    public string Title { get; set; } = "";

    /// <summary>Detailed description</summary>
    public string Description { get; set; } = "";

    /// <summary>Severity: "VISOK", "SREDNJI", "NIZAK"</summary>
    public string Severity { get; set; } = "";

    /// <summary>Mitigation strategy</summary>
    public string Mitigation { get; set; } = "";
}

/// <summary>
/// Strategic recommendation for the lawyer.
/// </summary>
public class StrategicRecommendation
{
    /// <summary>Action to take</summary>
    public string Action { get; set; } = "";

    /// <summary>Why this action is recommended</summary>
    public string Rationale { get; set; } = "";

    /// <summary>Priority: "HITNO", "VAŽNO", "PREPORUČENO"</summary>
    public string Priority { get; set; } = "";

    /// <summary>Deadline or timing guidance</summary>
    public string Timing { get; set; } = "";
}

/// <summary>
/// Legal Intelligence Feed item — a recent relevant court decision for the dashboard.
/// </summary>
public class LegalFeedItem
{
    /// <summary>Case number</summary>
    public string CaseNumber { get; set; } = "";

    /// <summary>Court name</summary>
    public string Court { get; set; } = "";

    /// <summary>Date of ruling</summary>
    public string Date { get; set; } = "";

    /// <summary>Topic category (e.g., "Kapara", "Raskid ugovora", "Služnost")</summary>
    public string Topic { get; set; } = "";

    /// <summary>Short headline summary</summary>
    public string Headline { get; set; } = "";

    /// <summary>Key takeaway for practitioners</summary>
    public string Takeaway { get; set; } = "";

    /// <summary>Impact level: "VISOK", "SREDNJI", "NIZAK"</summary>
    public string Impact { get; set; } = "";

    /// <summary>Relevant area of law</summary>
    public string AreaOfLaw { get; set; } = "";
}

/// <summary>
/// Request model for legal strategy analysis.
/// Accepts case context from various sources (contract text, chronology, ZK, free text).
/// </summary>
public class LegalStrategyRequest
{
    /// <summary>Free-form case description / facts of the case</summary>
    public string CaseDescription { get; set; } = "";

    /// <summary>Contract text (if available)</summary>
    public string? ContractText { get; set; }

    /// <summary>Chronology summary (if available)</summary>
    public string? ChronologySummary { get; set; }

    /// <summary>ZK analysis summary (if available)</summary>
    public string? ZkSummary { get; set; }

    /// <summary>Client's position: "kupac", "prodavatelj", "zakupac", etc.</summary>
    public string ClientPosition { get; set; } = "kupac";

    /// <summary>What the client wants to achieve</summary>
    public string DesiredOutcome { get; set; } = "";
}
