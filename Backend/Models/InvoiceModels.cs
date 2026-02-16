// ═══════════════════════════════════════════════════════════
//  V47: Fiskalizacija 2.0 — e-Račun Models
//  UBL 2.1 compliant electronic invoice models for Croatian B2B e-invoicing.
//  Effective 01.01.2026 per Zakon o elektroničkom izdavanju računa (NN xx/25).
// ═══════════════════════════════════════════════════════════

namespace Backend.Models;

/// <summary>
/// Request to generate an e-invoice linked to a PravoTek analysis.
/// Lawyers can auto-generate invoices from analyzed cases.
/// </summary>
public class InvoiceRequest
{
    /// <summary>UUID of the linked analysis (evaluation, contract, legal_strategy, etc.)</summary>
    public string? AnalysisId { get; set; }

    /// <summary>Module that generated the analysis: "valuation", "contract", "chronology", "legal"</summary>
    public string Module { get; set; } = "legal";

    /// <summary>Short description of the service performed</summary>
    public string ServiceDescription { get; set; } = "";

    /// <summary>Client/buyer information</summary>
    public InvoiceParty Client { get; set; } = new();

    /// <summary>Supplier/issuer (the law firm) information</summary>
    public InvoiceParty Issuer { get; set; } = new();

    /// <summary>Net amount in EUR (without PDV)</summary>
    public decimal NetAmount { get; set; }

    /// <summary>PDV rate as percentage (25, 13, 5, or 0)</summary>
    public decimal PdvRate { get; set; } = 25;

    /// <summary>Payment due days from invoice date</summary>
    public int PaymentDueDays { get; set; } = 15;

    /// <summary>Optional case reference / predmet number</summary>
    public string? CaseReference { get; set; }

    /// <summary>Optional notes to appear on the invoice</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Party information for an invoice (issuer or client).
/// </summary>
public class InvoiceParty
{
    /// <summary>Full legal name of the entity</summary>
    public string Name { get; set; } = "";

    /// <summary>OIB — Croatian tax identification number (11 digits)</summary>
    public string Oib { get; set; } = "";

    /// <summary>Full address</summary>
    public string Address { get; set; } = "";

    /// <summary>City</summary>
    public string City { get; set; } = "";

    /// <summary>Postal code</summary>
    public string PostalCode { get; set; } = "";

    /// <summary>Country code (ISO 3166-1 alpha-2, default: HR)</summary>
    public string CountryCode { get; set; } = "HR";

    /// <summary>Email address for e-delivery</summary>
    public string? Email { get; set; }

    /// <summary>IBAN for payment</summary>
    public string? Iban { get; set; }
}

/// <summary>
/// Result of e-invoice generation — the full invoice record.
/// </summary>
public class InvoiceResult
{
    /// <summary>Internal invoice ID (UUID)</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-readable invoice number: YYYY-NNNNN (e.g. 2026-00042)</summary>
    public string InvoiceNumber { get; set; } = "";

    /// <summary>Invoice issue date in ISO 8601</summary>
    public string IssueDate { get; set; } = "";

    /// <summary>Payment due date in ISO 8601</summary>
    public string DueDate { get; set; } = "";

    /// <summary>Supplier/issuer party</summary>
    public InvoiceParty Issuer { get; set; } = new();

    /// <summary>Client/buyer party</summary>
    public InvoiceParty Client { get; set; } = new();

    /// <summary>Invoice line items</summary>
    public List<InvoiceLineItem> LineItems { get; set; } = [];

    /// <summary>Net amount (before PDV)</summary>
    public decimal NetAmount { get; set; }

    /// <summary>PDV rate applied (%)</summary>
    public decimal PdvRate { get; set; }

    /// <summary>PDV amount</summary>
    public decimal PdvAmount { get; set; }

    /// <summary>Total amount (net + PDV)</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Currency code (always EUR since 01.01.2023)</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Linked analysis module</summary>
    public string Module { get; set; } = "";

    /// <summary>Linked analysis ID</summary>
    public string? AnalysisId { get; set; }

    /// <summary>Optional case reference</summary>
    public string? CaseReference { get; set; }

    /// <summary>Invoice status: "DRAFT", "ISSUED", "SENT", "PAID", "CANCELLED"</summary>
    public string Status { get; set; } = "DRAFT";

    /// <summary>UBL 2.1 XML content (the actual e-invoice document)</summary>
    public string UblXml { get; set; } = "";

    /// <summary>Optional notes</summary>
    public string? Notes { get; set; }

    /// <summary>Legal disclaimer for AI-generated invoices</summary>
    public string Disclaimer { get; set; } =
        "Ovaj e-račun generiran je automatski putem PravoTek sustava u UBL 2.1 formatu " +
        "sukladno Zakonu o elektroničkom izdavanju računa. Korisnik je dužan provjeriti " +
        "točnost podataka prije slanja putem sustava e-Račun.";
}

/// <summary>
/// Single line item on an invoice.
/// </summary>
public class InvoiceLineItem
{
    /// <summary>Line number (sequential)</summary>
    public int LineNumber { get; set; }

    /// <summary>Description of the service</summary>
    public string Description { get; set; } = "";

    /// <summary>Quantity (usually 1 for legal services)</summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>Unit of measure</summary>
    public string Unit { get; set; } = "kom";

    /// <summary>Unit price (net, without PDV)</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Line net amount (quantity × unit price)</summary>
    public decimal LineNetAmount { get; set; }

    /// <summary>PDV rate for this line (%)</summary>
    public decimal PdvRate { get; set; } = 25;
}

/// <summary>
/// Module-to-service description mapping for automatic invoice generation.
/// </summary>
public static class InvoiceServiceDescriptions
{
    public static readonly Dictionary<string, string> Descriptions = new()
    {
        ["valuation"] = "Procjena tržišne vrijednosti nekretnine — AI analiza",
        ["contract"] = "Pravna analiza ugovora o kupoprodaji nekretnine",
        ["chronology"] = "Izrada kronologije spisa — obrada dokumenata",
        ["legal"] = "Analiza sudske prakse i procjena ishoda spora",
    };

    public static string GetDescription(string module)
        => Descriptions.TryGetValue(module, out var desc) ? desc : "Pravna usluga — AI analiza";
}
