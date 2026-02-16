namespace Backend.Models;

// ══════════════════════════════════════════════════════════
//  ZK Izvadak — Full parsed land registry extract
//  Covers all 3 sheets: A (Posjedovnica), B (Vlastovnica),
//  C (Teretovnica) + derived property info for valuation.
// ══════════════════════════════════════════════════════════

/// <summary>
/// Complete parsed result of a ZK izvadak.
/// This merges the old AnalysisResult (risk) with property info (valuation).
/// </summary>
public class ZkParsedResult
{
    // ── Sheet A: Posjedovnica (parcels, areas, land use) ──
    public string PropertyId { get; set; } = string.Empty;           // k.o. + čestica (e.g. "k.o. Kukunjevac, k.č. 1234/5")
    public string CadastralMunicipality { get; set; } = string.Empty; // k.o. name (e.g. "Kukunjevac")
    public string ParcelNumber { get; set; } = string.Empty;          // čestica (e.g. "1234/5")
    public string ZkNumber { get; set; } = string.Empty;              // ZK uložak (e.g. "ZK.ul. 567")
    public string PropertyType { get; set; } = string.Empty;          // "stan", "kuća", "zemljište", "poslovni_prostor"
    public string PropertyDescription { get; set; } = string.Empty;   // Full description from register
    public double LandAreaM2 { get; set; }                            // Total čestica area
    public double BuildingAreaM2 { get; set; }                        // Building/living area if found (0 if not)
    public string LandUse { get; set; } = string.Empty;               // "oranica", "livada", "dvorište", "kuća i dvor" etc.
    public string EtazniUdio { get; set; } = string.Empty;            // Etažni udio if applicable (e.g. "E-3, 3/100")
    public List<ParcelInfo> Parcels { get; set; } = [];                // All parcels listed

    // ── Sheet B: Vlastovnica (owners) ──
    public List<OwnerInfo> Owners { get; set; } = [];

    // ── Sheet C: Teretovnica (encumbrances) ──
    public List<MortgageInfo> ActiveMortgages { get; set; } = [];
    public List<string> ActiveDisputes { get; set; } = [];
    public List<EncumbranceInfo> OtherEncumbrances { get; set; } = [];

    // ── Risk Assessment ──
    public string RiskScore { get; set; } = string.Empty;             // "LOW", "MEDIUM", "HIGH"
    public string LawyerComment { get; set; } = string.Empty;         // Legal risk summary in Croatian

    // ── V31: Due Diligence — Structured Legal Analysis ──
    public List<PlombaInfo> ActivePlombe { get; set; } = [];           // Active plombe (Z-xxxx/yyyy)
    public List<DueDiligenceFinding> Findings { get; set; } = [];      // All due diligence findings
    public string OverallStatus { get; set; } = string.Empty;          // "ČISTO", "UVJETNO", "RIZIČNO"
    public string OwnershipStatus { get; set; } = string.Empty;        // "1/1 ČISTO", "SUVLASNIŠTVO", "NERAZJAŠNJENO"
    public bool SharesSumCorrect { get; set; } = true;                 // Do ownership shares add up to 1/1?
    public string SharesSumDisplay { get; set; } = string.Empty;       // e.g. "1/4 + 1/4 + 2/4 = 4/4 ✓"
    public string LandUseMismatch { get; set; } = string.Empty;        // Warning if land use ≠ property type
    public bool HasUsagePermitIssue { get; set; }                      // "Nije priložena uporabna dozvola"
    public string DueDiligenceSummary { get; set; } = string.Empty;    // Professional report summary

    // ── V44: Advanced Encumbrance Risk Analysis ──
    public List<HistoricalEncumbrance> HistoricalEncumbrances { get; set; } = [];  // Pre-1991, old fiduciary, legacy burdens
    public EncumbranceRiskSummary? EncumbranceRiskMatrix { get; set; }             // Aggregated risk matrix

    // ── V42: Norm Validity Engine — cited norms with validity status ──
    public List<CitedNorm> CitedNorms { get; set; } = [];

    // ── V40: Katastar vs ZK Usklađenost ──
    public KatastarComparison? KatastarUskladjenost { get; set; }      // Cadastre vs land registry discrepancies

    // ── Location (extracted or inferred) ──
    public string Location { get; set; } = string.Empty;              // "Naselje, Općina/Grad"
    public string Address { get; set; } = string.Empty;               // Street address if found

    // ── Condition hints (from ZK text) ──
    public string Condition { get; set; } = "Nije navedeno";
    public string PropertyState { get; set; } = "good";               // "new","renovated","good","original","needs_adaptation"
    public double YardAreaM2 { get; set; }                            // Okućnica if applicable
}

/// <summary>
/// Individual parcel within a ZK izvadak (A sheet can have multiple).
/// </summary>
public class ParcelInfo
{
    public string ParcelNumber { get; set; } = string.Empty;    // e.g. "1234/5"
    public string LandUse { get; set; } = string.Empty;         // e.g. "oranica", "dvorište"
    public double AreaM2 { get; set; }                           // Area in m²
    public string Description { get; set; } = string.Empty;     // Additional description
}

/// <summary>
/// Owner information from B sheet (Vlastovnica).
/// </summary>
public class OwnerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Share { get; set; } = string.Empty;            // e.g. "1/1", "1/2", "3/100"
    public string Oib { get; set; } = string.Empty;              // Croatian personal ID number (OIB)
    public string BasisOfAcquisition { get; set; } = string.Empty; // "kupoprodajni ugovor", "nasljeđivanje" etc.
}

/// <summary>
/// Non-mortgage encumbrances from C sheet.
/// V44: Enhanced with structured risk classification.
/// </summary>
public class EncumbranceInfo
{
    public string Type { get; set; } = string.Empty;        // "služnost", "pravo građenja", "plomba" etc.
    public string Description { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;  // Korisnik prava
    public bool IsActive { get; set; } = true;
    /// <summary>V44: Specific risk type — "FIDUCIJA", "OSOBNA_SLUZNOST", "STVARNA_SLUZNOST", 
    /// "ZABRANA_OTUDENJA", "PRAVO_GRADENJA", "KONCESIJA", "OVRHA", "PRAVO_PRVOKUPA", "OSTALO"</summary>
    public string RiskType { get; set; } = string.Empty;
    /// <summary>V44: Legal basis with NN reference</summary>
    public string LegalBasis { get; set; } = string.Empty;
    /// <summary>V44: Whether this encumbrance blocks a sale transaction</summary>
    public bool BlocksSale { get; set; }
    /// <summary>V44: Whether this encumbrance prevents construction/building permits</summary>
    public bool BlocksConstruction { get; set; }
    /// <summary>V44: Year the encumbrance was registered (0 if unknown)</summary>
    public int YearRegistered { get; set; }
    /// <summary>V44: Whether this is a historical/legacy encumbrance (pre-1991, socialist era, etc.)</summary>
    public bool IsHistorical { get; set; }
}

/// <summary>
/// Active plomba (pending registration request) — the #1 critical risk.
/// V44: Enhanced with type classification and risk scoring.
/// </summary>
public class PlombaInfo
{
    public string ZNumber { get; set; } = string.Empty;       // e.g. "Z-1234/2024"
    public string Description { get; set; } = string.Empty;   // What the request is about
    public string Date { get; set; } = string.Empty;           // When it was filed
    /// <summary>V44: Type of plomba — "VLASNISTVO", "HIPOTEKA", "OVRHA", "BRISANJE", "ZABILJEŽBA", "OSTALO"</summary>
    public string PlombaType { get; set; } = string.Empty;
    /// <summary>V44: Risk level — "CRITICAL" (blocks sale), "WARNING" (delays), "INFO" (routine)</summary>
    public string RiskLevel { get; set; } = "CRITICAL";
    /// <summary>V44: Whether this plomba blocks the sale outright</summary>
    public bool BlocksSale { get; set; } = true;
}

/// <summary>
/// A single due diligence finding — structured legal risk item.
/// Severity: "CRITICAL" (deal-breaker), "WARNING" (negotiate), "INFO" (neutral/positive).
/// Category: "PLOMBA", "VLASNISTVO", "HIPOTEKA", "SLUZNOST", "OVRHA", "LEGALIZACIJA", "OSTALO".
/// </summary>
public class DueDiligenceFinding
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;      // CRITICAL, WARNING, INFO
    public string Title { get; set; } = string.Empty;          // Short title
    public string Description { get; set; } = string.Empty;    // Detailed explanation
    public string Advice { get; set; } = string.Empty;         // Actionable legal advice
}

// ══════════════════════════════════════════════════════════
//  V40: Katastar vs ZK Usklađenost (Cadastre ↔ Land Registry)
// ══════════════════════════════════════════════════════════

/// <summary>
/// Overall comparison between cadastre (katastar) and land registry (ZK) records.
/// Detects discrepancies in areas, parcel numbers, and land use descriptions.
/// </summary>
public class KatastarComparison
{
    /// <summary>Overall status: "USKLAĐENO", "NESKLAD", "DJELOMIČNO"</summary>
    public string Status { get; set; } = "USKLAĐENO";

    /// <summary>Total area difference (katastar vs ZK) in m²</summary>
    public double AreaDifferenceM2 { get; set; }

    /// <summary>Percentage difference ((katastar - ZK) / ZK * 100)</summary>
    public double AreaDifferencePercent { get; set; }

    /// <summary>Area recorded in katastar (m²)</summary>
    public double KatastarAreaM2 { get; set; }

    /// <summary>Area recorded in ZK (m²)</summary>
    public double ZkAreaM2 { get; set; }

    /// <summary>Individual parcel-level discrepancies</summary>
    public List<KatastarParcelDiscrepancy> ParcelDiscrepancies { get; set; } = [];

    /// <summary>Human-readable summary of all discrepancies in Croatian</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Number of discrepancies found</summary>
    public int DiscrepancyCount { get; set; }

    /// <summary>Whether a survey (geodetski elaborat) is recommended</summary>
    public bool SurveyRecommended { get; set; }
}

/// <summary>
/// A single parcel-level discrepancy between katastar and ZK records.
/// </summary>
public class KatastarParcelDiscrepancy
{
    /// <summary>Type: "AREA_MISMATCH", "PARCEL_MISSING", "LAND_USE_MISMATCH", "PARCEL_NUMBER_MISMATCH"</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Parcel number in ZK (e.g. "1234/5")</summary>
    public string ZkParcelNumber { get; set; } = string.Empty;

    /// <summary>Parcel number in katastar (may differ after parcellation)</summary>
    public string KatastarParcelNumber { get; set; } = string.Empty;

    /// <summary>Area in ZK (m²)</summary>
    public double ZkAreaM2 { get; set; }

    /// <summary>Area in katastar (m²)</summary>
    public double KatastarAreaM2 { get; set; }

    /// <summary>Land use description in ZK</summary>
    public string ZkLandUse { get; set; } = string.Empty;

    /// <summary>Land use description in katastar</summary>
    public string KatastarLandUse { get; set; } = string.Empty;

    /// <summary>Human-readable description of the discrepancy</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Severity: "HIGH", "MEDIUM", "LOW"</summary>
    public string Severity { get; set; } = "MEDIUM";
}

// ══════════════════════════════════════════════════════════
//  V44: Advanced Encumbrance Risk Analysis — "OCR 2.0"
//  Patterns: fiducije, historical burdens, old socialist-era entries
// ══════════════════════════════════════════════════════════

/// <summary>
/// Historical or legacy encumbrance — pre-1991, old fiduciary transfers,
/// socialist-era entries (JNA, SOUR, RO), or unclear entries that may
/// block modern transactions despite appearing "dead" in the register.
/// </summary>
public class HistoricalEncumbrance
{
    /// <summary>Type: "FIDUCIJA", "STARI_TERET", "SOCIJALIZAM", "NEIDENTIFICIRANO"</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Original text as found in ZK izvadak</summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>AI interpretation of what this entry means</summary>
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>Estimated year of registration (0 if unknown)</summary>
    public int EstimatedYear { get; set; }

    /// <summary>Whether this encumbrance could still have legal effect today</summary>
    public bool StillLegallyRelevant { get; set; }

    /// <summary>Whether it blocks sale: "DA", "NE", "MOGUĆE"</summary>
    public string BlocksSale { get; set; } = "MOGUĆE";

    /// <summary>Whether it blocks construction: "DA", "NE", "MOGUĆE"</summary>
    public string BlocksConstruction { get; set; } = "NE";

    /// <summary>Recommended action to resolve this encumbrance</summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>Legal basis with NN reference</summary>
    public string LegalBasis { get; set; } = string.Empty;
}

/// <summary>
/// Aggregated risk matrix for all encumbrances on a property.
/// Provides a quick overview: how many block sale, block construction, etc.
/// </summary>
public class EncumbranceRiskSummary
{
    /// <summary>Total active encumbrances</summary>
    public int TotalActive { get; set; }

    /// <summary>Number that block sale transactions</summary>
    public int BlockingSale { get; set; }

    /// <summary>Number that block construction permits</summary>
    public int BlockingConstruction { get; set; }

    /// <summary>Number of fiduciary transfers detected</summary>
    public int FiduciaryCount { get; set; }

    /// <summary>Number of personal servitudes (deal-breakers)</summary>
    public int PersonalServitudeCount { get; set; }

    /// <summary>Number of historical/legacy entries requiring investigation</summary>
    public int HistoricalCount { get; set; }

    /// <summary>Number of active enforcement/execution proceedings</summary>
    public int EnforcementCount { get; set; }

    /// <summary>Overall encumbrance risk: "ČISTO", "UVJETNO", "BLOKIRANO"</summary>
    public string OverallRisk { get; set; } = "ČISTO";

    /// <summary>1-sentence summary of the encumbrance situation</summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Unified response: ZK analysis + valuation in one go.
/// Extends FullEvaluationResult with ZK-specific data.
/// </summary>
public class UnifiedZkValuationResult
{
    // ── ZK Analysis ──
    public ZkParsedResult ZkAnalysis { get; set; } = new();

    // ── Valuation (same shape as FullEvaluationResult) ──
    public FullEvaluationResult? Valuation { get; set; }

    // ── Pipeline metadata ──
    public bool ValuationAttempted { get; set; }
    public string? ValuationSkipReason { get; set; }  // e.g. "Površina zgrade nepoznata"
}
