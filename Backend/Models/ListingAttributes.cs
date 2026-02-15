namespace Backend.Models;

/// <summary>
/// V29 "Sherlock Mode" — Structured attributes extracted from listing description text
/// via LLM analysis. These override scraper-provided fields when they conflict.
/// "Super-Extractor" — Deep Valuation Profile covering Premium, Risk, and Context factors.
/// </summary>
public class ListingAttributes
{
    // ── ORIGINAL 6 FIELDS ──

    /// <summary>
    /// Renovation/condition status inferred from description text.
    /// Text mentions like "potrebna adaptacija" override scraper's "Stanje: Izvrsno".
    /// </summary>
    public RenovationStatus RenovationStatus { get; set; } = RenovationStatus.Unknown;

    /// <summary>Is the listing marked as urgent? ("Hitno", "Iseljenje", "Prilika", "Sniženo")</summary>
    public bool IsUrgent { get; set; }

    /// <summary>Does the description mention a garage/parking space?</summary>
    public bool HasGarage { get; set; }

    /// <summary>View type detected from description text.</summary>
    public ViewType ViewType { get; set; } = ViewType.Unknown;

    /// <summary>
    /// Legal status inferred from text ("Vlasništvo 1/1", "Uredni papiri", "Suvlasništvo").
    /// </summary>
    public LegalStatus LegalStatus { get; set; } = LegalStatus.Unknown;

    /// <summary>
    /// CRITICAL: Actual land type from description text. Agents often miscategorize land
    /// on Njuškalo (e.g., "Building" when it's actually "Agricultural").
    /// </summary>
    public ActualLandType ActualLandType { get; set; } = ActualLandType.Unknown;

    // ── SUPER-EXTRACTOR: PREMIUM FACTORS ──

    /// <summary>
    /// Commercial potential detected from text.
    /// "None", "Apartments", "Restaurant", "Office", "Mixed"
    /// </summary>
    public string CommercialPotential { get; set; } = "None";

    /// <summary>Is this a poslovni prostor / business premise?</summary>
    public bool IsBusinessPremise { get; set; }

    /// <summary>Sea/beach distance if mentioned: e.g. "50m", "1. red", "200m do plaže". Null if not mentioned.</summary>
    public string? SeaDistance { get; set; }

    /// <summary>Does the description mention a pool / bazen?</summary>
    public bool HasPool { get; set; }

    // ── SUPER-EXTRACTOR: LOCATION & CONTEXT ──

    /// <summary>
    /// Microlocation quality: "Quiet", "BusyRoad", "Isolated", "CityCenter", "Unknown"
    /// </summary>
    public string Microlocation { get; set; } = "Unknown";

    /// <summary>
    /// Road/access type: "Asphalt", "Macadam", "FootpathOnly", "Unknown"
    /// </summary>
    public string AccessType { get; set; } = "Unknown";

    // ── SUPER-EXTRACTOR: CONDITION & TECH ──

    /// <summary>
    /// Major structural/condition damages detected from text.
    /// Values: "Damp", "RoofLeak", "StaticIssue", etc.
    /// Empty list = no damages mentioned.
    /// </summary>
    public List<string> MajorDamages { get; set; } = new();

    /// <summary>Energy class if mentioned: A+, A, B, C, D, E, F, G. Null if not mentioned.</summary>
    public string? EnergyClass { get; set; }

    /// <summary>Does the building have an elevator? (relevant for multi-story buildings)</summary>
    public bool HasElevator { get; set; }

    /// <summary>
    /// Heating system type: "HeatPump", "Gas", "Wood", "Electric", "CentralHeating", "Unknown"
    /// </summary>
    public string HeatingSystem { get; set; } = "Unknown";
}

public enum RenovationStatus
{
    Unknown,
    New,          // Novogradnja, novo
    Renovated,    // Renovirano, adaptirano, obnovljeno
    Livable,      // Useljivo, u dobrom stanju
    NeedsAdaptation, // Potrebna adaptacija, za uređenje
    Ruin          // Ruševina, za rušenje, zapušteno
}

public enum LegalStatus
{
    Unknown,
    Clean,              // 1/1, uredni papiri, čisto vlasništvo
    Issues,             // Teret, hipoteka, spor
    Share,              // Suvlasništvo, idealni dio
    LegalizationNeeded, // Legalizacija u tijeku, nelegalan objekt, bez dozvole
    Vanknjizno          // Vanknjižno, nije upisano u zemljišne knjige
}

public enum ActualLandType
{
    Unknown,
    Building,      // Građevinsko
    Agricultural,  // Poljoprivredno (oranica, pašnjak, livada, šuma, voćnjak, vinograd)
    Mixed          // Mješovito — partly building, partly agricultural
}
