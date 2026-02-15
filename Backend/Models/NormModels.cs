namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  V42: Norm Validity Engine — Models
//  Structured representation of Croatian legal norms, their
//  amendment history, validity status, and temporal applicability.
//  Enables court-grade citations: "čl. 322. st. 1. ZOO (NN 35/05, 29/18)"
// ══════════════════════════════════════════════════════════════

/// <summary>
/// A single Croatian legal norm (zakon/uredba) with full amendment history.
/// </summary>
public class NormInfo
{
    /// <summary>Standard abbreviation (e.g. "ZOO", "ZZK", "ZV")</summary>
    public string Abbreviation { get; set; } = "";

    /// <summary>Full official name (e.g. "Zakon o obveznim odnosima")</summary>
    public string FullName { get; set; } = "";

    /// <summary>Current validity status</summary>
    public NormStatus Status { get; set; } = NormStatus.Važeći;

    /// <summary>Consolidated NN reference string (e.g. "NN 35/05, 41/08, 125/11, 78/15, 29/18")</summary>
    public string NnReference { get; set; } = "";

    /// <summary>Short NN reference — first + last only (e.g. "NN 35/05, 29/18")</summary>
    public string NnReferenceShort { get; set; } = "";

    /// <summary>Chronological list of all amendments/publications</summary>
    public List<NormAmendment> Amendments { get; set; } = [];

    /// <summary>Date of most recent amendment publication</summary>
    public DateOnly LastAmendmentDate { get; set; }

    /// <summary>Date the original text was published</summary>
    public DateOnly OriginalDate { get; set; }

    /// <summary>Primary subject area for categorization</summary>
    public string Category { get; set; } = "";

    /// <summary>URL template for Narodne novine lookup (uses first NN number)</summary>
    public string NnUrl { get; set; } = "";
}

/// <summary>
/// A single amendment/publication of a norm in Narodne novine.
/// </summary>
public class NormAmendment
{
    /// <summary>NN issue reference (e.g. "35/05", "41/08")</summary>
    public string NnNumber { get; set; } = "";

    /// <summary>Date of publication in NN</summary>
    public DateOnly PublicationDate { get; set; }

    /// <summary>Type of publication</summary>
    public AmendmentType Type { get; set; } = AmendmentType.Izmjena;

    /// <summary>Brief description of what changed</summary>
    public string Description { get; set; } = "";
}

/// <summary>
/// A cited norm in AI-generated output — structured citation with validity info.
/// Used in JSON output from AI prompts (cited_norms array).
/// </summary>
public class CitedNorm
{
    /// <summary>Law abbreviation (e.g. "ZOO")</summary>
    public string Abbreviation { get; set; } = "";

    /// <summary>Full law name</summary>
    public string FullName { get; set; } = "";

    /// <summary>Cited article (e.g. "čl. 322.")</summary>
    public string Article { get; set; } = "";

    /// <summary>Cited paragraph (e.g. "st. 1.") — optional</summary>
    public string Paragraph { get; set; } = "";

    /// <summary>Cited point (e.g. "t. 2.") — optional</summary>
    public string Point { get; set; } = "";

    /// <summary>NN reference (e.g. "NN 35/05, 29/18")</summary>
    public string NnReference { get; set; } = "";

    /// <summary>Validity status: VAŽEĆI, IZMIJENJEN, UKINUT</summary>
    public string Status { get; set; } = "VAŽEĆI";

    /// <summary>Full court-style formatted citation</summary>
    public string FormattedCitation { get; set; } = "";

    /// <summary>URL to Narodne novine</summary>
    public string NnUrl { get; set; } = "";
}

/// <summary>
/// Result of temporal norm lookup — what version applies for a given date.
/// </summary>
public class TemporalNormResult
{
    /// <summary>The norm being looked up</summary>
    public string Abbreviation { get; set; } = "";

    /// <summary>Full name</summary>
    public string FullName { get; set; } = "";

    /// <summary>The NN version applicable at the requested date</summary>
    public string ApplicableNnVersion { get; set; } = "";

    /// <summary>The current (latest) NN version</summary>
    public string CurrentNnVersion { get; set; } = "";

    /// <summary>Full NN reference up to the applicable date</summary>
    public string ApplicableNnReference { get; set; } = "";

    /// <summary>Whether the applicable version differs from the current version</summary>
    public bool IsOutdated { get; set; }

    /// <summary>Warning message if an older version applies</summary>
    public string Warning { get; set; } = "";

    /// <summary>Court-style citation for the applicable version</summary>
    public string FormattedCitation { get; set; } = "";

    /// <summary>Date for which the lookup was performed</summary>
    public DateOnly EventDate { get; set; }
}

/// <summary>
/// Request payload for temporal norm lookup.
/// </summary>
public class TemporalLookupRequest
{
    /// <summary>Date of the legal event (e.g. contract signing date)</summary>
    public DateOnly EventDate { get; set; }

    /// <summary>Law abbreviation to look up (e.g. "ZOO")</summary>
    public string Abbreviation { get; set; } = "";
}

/// <summary>
/// Citation format request — for the /api/norms/cite endpoint.
/// </summary>
public class CitationRequest
{
    public string Abbreviation { get; set; } = "";
    public string Article { get; set; } = "";
    public string Paragraph { get; set; } = "";
    public string Point { get; set; } = "";

    /// <summary>If set, returns the version applicable at this date</summary>
    public DateOnly? EventDate { get; set; }

    /// <summary>Court-style (full) vs abbreviated citation</summary>
    public bool CourtStyle { get; set; } = true;
}

// ── Enums ──

public enum NormStatus
{
    /// <summary>Currently in force</summary>
    Važeći,

    /// <summary>Amended — base text still in force with modifications</summary>
    Izmijenjen,

    /// <summary>Repealed / no longer in force</summary>
    Ukinut
}

public enum AmendmentType
{
    /// <summary>Original text (temeljni tekst)</summary>
    TemeljniTekst,

    /// <summary>Amendment (izmjena i dopuna)</summary>
    Izmjena,

    /// <summary>Correction (ispravak)</summary>
    Ispravak,

    /// <summary>Constitutional Court decision (odluka USRH)</summary>
    OdlukaUSRH,

    /// <summary>Consolidated text (pročišćeni tekst)</summary>
    PročišćeniTekst
}
