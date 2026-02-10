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
/// </summary>
public class EncumbranceInfo
{
    public string Type { get; set; } = string.Empty;        // "služnost", "pravo građenja", "plomba" etc.
    public string Description { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty;  // Korisnik prava
    public bool IsActive { get; set; } = true;
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
