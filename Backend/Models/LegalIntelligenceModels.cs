namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  V34: Legal Intelligence — Hardened & Transparent
//  Probability Ranges, Score Breakdown, Stability Indicators,
//  Data Provenance, Contextual Risks
// ══════════════════════════════════════════════════════════════

/// <summary>
/// A single court precedent (presuda) — either from the embedded knowledge base
/// or synthesized by GPT from its training data on Croatian court practice.
/// V34: Added Stability + SimilarityScore for transparency.
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

    /// <summary>V34: Practice stability — how settled the legal position is</summary>
    public string Stability { get; set; } = "";  // "STABILNA", "PODIJELJENA", "NOVA"

    /// <summary>V34: AI-estimated similarity to the current case (0-100)</summary>
    public int SimilarityScore { get; set; }
}

/// <summary>
/// Full legal strategy analysis result — the "Provjeri ishod na sudu" output.
/// V34: Added probability range, score breakdown, confidence factors, data provenance.
/// </summary>
public class LegalStrategyResult
{
    /// <summary>The core legal issue identified from the case materials</summary>
    public string KeyLegalIssue { get; set; } = "";

    /// <summary>Sub-issues or additional legal questions</summary>
    public List<string> SubIssues { get; set; } = [];

    /// <summary>Relevant court precedents found</summary>
    public List<CourtPrecedent> Precedents { get; set; } = [];

    /// <summary>Win probability percentage — midpoint (0-100)</summary>
    public int WinProbability { get; set; }

    /// <summary>V34: Lower bound of probability range (0-100)</summary>
    public int WinProbabilityMin { get; set; }

    /// <summary>V34: Upper bound of probability range (0-100)</summary>
    public int WinProbabilityMax { get; set; }

    /// <summary>Win probability label: "Visoka", "Srednja", "Niska"</summary>
    public string WinProbabilityLabel { get; set; } = "";

    /// <summary>Detailed reasoning for the probability assessment</summary>
    public string ProbabilityReasoning { get; set; } = "";

    /// <summary>V34: Structured score breakdown explaining how the probability was derived</summary>
    public ScoreBreakdown? ScoreBreakdown { get; set; }

    /// <summary>V34: Factors that increase or decrease confidence in the assessment</summary>
    public List<ConfidenceFactor> ConfidenceFactors { get; set; } = [];

    /// <summary>V34: Data provenance disclaimer — where the AI info comes from</summary>
    public string DataProvenance { get; set; } =
        "Rezultati se temelje na GPT-4o modelu koji analizira kontekst slučaja u usporedbi s " +
        "općim načelima sudske prakse. Citirane presude su ILUSTRATIVNE — generirane na temelju " +
        "ustaljenih pravnih stajališta, a ne pretragom stvarne baze sudskih odluka.";

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

    /// <summary>V42: All norms cited in this analysis — structured for validity badges</summary>
    public List<CitedNorm> CitedNorms { get; set; } = [];

    /// <summary>V45: AI Act Audit Trail — transparent log showing which articles drove each conclusion</summary>
    public List<AuditLogEntry> AuditTrail { get; set; } = [];

    /// <summary>Legal disclaimer</summary>
    public string Disclaimer { get; set; } =
        "Ova AI analiza temelji se na općim načelima sudske prakse i ne zamjenjuje pravni savjet ovlaštenog odvjetnika. " +
        "Konkretni ishod ovisi o specifičnostima slučaja, dokaznoj građi i načinu vođenja postupka.";
}

/// <summary>
/// V34: Transparent score breakdown — shows exactly how the win probability was computed.
/// Combines deterministic counting of precedent alignment with AI qualitative assessment.
/// </summary>
public class ScoreBreakdown
{
    /// <summary>Number of precedents favoring the client's position</summary>
    public int FavorableCount { get; set; }

    /// <summary>Number of neutral precedents</summary>
    public int NeutralCount { get; set; }

    /// <summary>Number of precedents against the client's position</summary>
    public int UnfavorableCount { get; set; }

    /// <summary>Practice stability: "STABILNA" (settled law) / "PODIJELJENA" (divided) / "NOVA" (emerging)</summary>
    public string PracticeStability { get; set; } = "";

    /// <summary>Legal qualification of the case type</summary>
    public string LegalQualification { get; set; } = "";

    /// <summary>Precedent-based probability computed from alignment counts (deterministic)</summary>
    public int PrecedentScore { get; set; }

    /// <summary>AI qualitative assessment (GPT's judgment based on facts)</summary>
    public int QualitativeScore { get; set; }

    /// <summary>Human-readable explanation of the scoring methodology</summary>
    public string Methodology { get; set; } = "";
}

/// <summary>
/// V34: A factor that influences the confidence level of the probability assessment.
/// </summary>
public class ConfidenceFactor
{
    /// <summary>Factor name (e.g., "Bogata činjenična podloga")</summary>
    public string Factor { get; set; } = "";

    /// <summary>Direction: "POZITIVNO" (increases confidence) or "NEGATIVNO" (decreases confidence)</summary>
    public string Direction { get; set; } = "";

    /// <summary>Brief explanation of this factor's impact</summary>
    public string Explanation { get; set; } = "";
}

/// <summary>
/// A specific legal risk identified in the case.
/// V34: Added CaseElement for contextual specificity.
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

    /// <summary>V34: Specific element of the case that triggers this risk (contextual anchor)</summary>
    public string CaseElement { get; set; } = "";
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
/// V45: AI Act Compliance Audit Log Entry.
/// Each entry documents exactly which legal article drove a specific AI conclusion,
/// fulfilling EU AI Act (Uredba EU 2024/1689) transparency requirements for high-risk AI.
/// </summary>
public class AuditLogEntry
{
    /// <summary>Sequential step number in the reasoning chain</summary>
    public int Step { get; set; }

    /// <summary>The AI action/conclusion at this step</summary>
    public string Action { get; set; } = "";

    /// <summary>The specific legal article that grounds this conclusion (e.g. "čl. 368. ZOO")</summary>
    public string LegalBasis { get; set; } = "";

    /// <summary>Full citation with NN reference</summary>
    public string Citation { get; set; } = "";

    /// <summary>Why this article applies to the specific facts</summary>
    public string Reasoning { get; set; } = "";

    /// <summary>Confidence in this reasoning step: "VISOKO", "SREDNJE", "NISKO"</summary>
    public string Confidence { get; set; } = "SREDNJE";

    /// <summary>ISO 8601 timestamp of when this step was generated</summary>
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// Legal Intelligence Feed item — a recent relevant court decision for the dashboard.
/// V34: Added IsAiGenerated flag for provenance.
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

    /// <summary>V34: Flag indicating this content is AI-generated, not from a live database</summary>
    public bool IsAiGenerated { get; set; } = true;
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
