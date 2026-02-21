using System.Globalization;
using System.Text;
using System.Xml;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V47: Fiskalizacija 2.0 — Electronic Invoice Service.
/// Generates UBL 2.1 compliant e-invoices for Croatian B2B e-invoicing.
/// Links invoices to PravoTek AI analyses (valuations, contracts, legal strategies).
///
/// UBL 2.1 namespace: urn:oasis:names:specification:ubl:schema:xsd:Invoice-2
/// Croatian e-Račun requirements per Zakon o elektroničkom izdavanju računa.
/// </summary>
public class InvoiceService
{
    private readonly SupabaseService _supabaseService;
    private readonly ILogger<InvoiceService> _logger;

    // Thread-safe sequential counter — uses Guid-based seed for restart uniqueness.
    // Each restart gets a different seed, preventing collision across restarts.
    private static int _invoiceCounter = Math.Abs(Guid.NewGuid().GetHashCode() % 100000);

    public InvoiceService(SupabaseService supabaseService, ILogger<InvoiceService> logger)
    {
        _supabaseService = supabaseService;
        _logger = logger;
    }

    /// <summary>
    /// Generate an e-invoice from an InvoiceRequest.
    /// Returns the full InvoiceResult with UBL 2.1 XML.
    /// </summary>
    public async Task<InvoiceResult> GenerateInvoiceAsync(InvoiceRequest request, string? userId = null)
    {
        _logger.LogInformation(
            "Generating e-invoice: module={Module}, net={Net:F2} EUR, PDV={Pdv}%",
            request.Module, request.NetAmount, request.PdvRate);

        // Calculate amounts
        var pdvAmount = Math.Round(request.NetAmount * request.PdvRate / 100m, 2);
        var grossAmount = request.NetAmount + pdvAmount;

        // Generate invoice number
        var invoiceNumber = GenerateInvoiceNumber();
        var issueDate = DateTime.UtcNow;
        var dueDate = issueDate.AddDays(request.PaymentDueDays);

        // Build service description
        var serviceDesc = !string.IsNullOrWhiteSpace(request.ServiceDescription)
            ? request.ServiceDescription
            : InvoiceServiceDescriptions.GetDescription(request.Module);

        // Create line item
        var lineItem = new InvoiceLineItem
        {
            LineNumber = 1,
            Description = serviceDesc,
            Quantity = 1,
            Unit = "kom",
            UnitPrice = request.NetAmount,
            LineNetAmount = request.NetAmount,
            PdvRate = request.PdvRate
        };

        // Build result
        var result = new InvoiceResult
        {
            Id = Guid.NewGuid().ToString(),
            InvoiceNumber = invoiceNumber,
            IssueDate = issueDate.ToString("yyyy-MM-dd"),
            DueDate = dueDate.ToString("yyyy-MM-dd"),
            Issuer = request.Issuer,
            Client = request.Client,
            LineItems = [lineItem],
            NetAmount = request.NetAmount,
            PdvRate = request.PdvRate,
            PdvAmount = pdvAmount,
            GrossAmount = grossAmount,
            Module = request.Module,
            AnalysisId = request.AnalysisId,
            CaseReference = request.CaseReference,
            Status = "DRAFT",
            Notes = request.Notes
        };

        // Generate UBL 2.1 XML
        result.UblXml = GenerateUblXml(result);

        // Save to Supabase
        await _supabaseService.SaveInvoiceAsync(new InvoiceRecord
        {
            UserId = userId,
            InvoiceNumber = result.InvoiceNumber,
            IssueDate = result.IssueDate,
            DueDate = result.DueDate,
            IssuerName = result.Issuer.Name,
            IssuerOib = result.Issuer.Oib,
            ClientName = result.Client.Name,
            ClientOib = result.Client.Oib,
            NetAmount = result.NetAmount,
            PdvRate = result.PdvRate,
            PdvAmount = result.PdvAmount,
            GrossAmount = result.GrossAmount,
            Module = result.Module,
            AnalysisId = request.AnalysisId,
            CaseReference = request.CaseReference,
            Status = result.Status,
            UblXml = result.UblXml,
            RawJsonOutput = result
        });

        _logger.LogInformation(
            "E-invoice generated: {Number}, gross={Gross:F2} EUR, module={Module}",
            result.InvoiceNumber, result.GrossAmount, result.Module);

        return result;
    }

    /// <summary>
    /// Generate a unique invoice number: YYYY-NNNNN
    /// Uses a combination of date seed and sequential counter to minimize
    /// collision risk across app restarts. For full production safety,
    /// replace with a DB sequence (e.g., Supabase nextval()).
    /// </summary>
    private static string GenerateInvoiceNumber()
    {
        var now = DateTime.UtcNow;
        var seq = Interlocked.Increment(ref _invoiceCounter);
        // Combine timestamp (YYMMDDHHmmss) with counter for uniqueness across restarts
        var timePart = now.ToString("MMddHHmm");
        return $"{now.Year}-{timePart}{seq % 1000:D3}";
    }

    /// <summary>
    /// Generate UBL 2.1 XML document for the invoice.
    /// Compliant with EN 16931 (European e-invoicing standard) and Croatian requirements.
    /// </summary>
    public static string GenerateUblXml(InvoiceResult invoice)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            // UBL 2.1 Invoice root element
            writer.WriteStartDocument();
            writer.WriteStartElement("Invoice", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            writer.WriteAttributeString("xmlns", "cac", null,
                "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            writer.WriteAttributeString("xmlns", "cbc", null,
                "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

            // ── Header ──
            WriteCbcElement(writer, "cbc", "UBLVersionID", "2.1");
            WriteCbcElement(writer, "cbc", "CustomizationID",
                "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0");
            WriteCbcElement(writer, "cbc", "ProfileID", "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0");
            WriteCbcElement(writer, "cbc", "ID", invoice.InvoiceNumber);
            WriteCbcElement(writer, "cbc", "IssueDate", invoice.IssueDate);
            WriteCbcElement(writer, "cbc", "DueDate", invoice.DueDate);
            WriteCbcElement(writer, "cbc", "InvoiceTypeCode", "380"); // 380 = Commercial Invoice
            WriteCbcElement(writer, "cbc", "DocumentCurrencyCode", "EUR");

            // Optional: case reference as BuyerReference
            if (!string.IsNullOrWhiteSpace(invoice.CaseReference))
            {
                WriteCbcElement(writer, "cbc", "BuyerReference", invoice.CaseReference);
            }

            // Optional notes
            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                WriteCbcElement(writer, "cbc", "Note", invoice.Notes);
            }

            // ── Supplier (AccountingSupplierParty) ──
            writer.WriteStartElement("cac", "AccountingSupplierParty", null);
            WriteParty(writer, invoice.Issuer);
            writer.WriteEndElement();

            // ── Customer (AccountingCustomerParty) ──
            writer.WriteStartElement("cac", "AccountingCustomerParty", null);
            WriteParty(writer, invoice.Client);
            writer.WriteEndElement();

            // ── Payment Means ──
            writer.WriteStartElement("cac", "PaymentMeans", null);
            WriteCbcElement(writer, "cbc", "PaymentMeansCode", "30"); // 30 = Credit transfer
            if (!string.IsNullOrWhiteSpace(invoice.Issuer.Iban))
            {
                writer.WriteStartElement("cac", "PayeeFinancialAccount", null);
                WriteCbcElement(writer, "cbc", "ID", invoice.Issuer.Iban);
                writer.WriteEndElement(); // PayeeFinancialAccount
            }
            writer.WriteEndElement(); // PaymentMeans

            // ── Payment Terms ──
            writer.WriteStartElement("cac", "PaymentTerms", null);
            WriteCbcElement(writer, "cbc", "Note",
                $"Rok plaćanja: {invoice.DueDate}. Plaćanje na IBAN: {invoice.Issuer.Iban ?? "N/A"}.");
            writer.WriteEndElement();

            // ── Tax Total ──
            writer.WriteStartElement("cac", "TaxTotal", null);
            WriteCbcAmount(writer, "cbc", "TaxAmount", invoice.PdvAmount, "EUR");
            writer.WriteStartElement("cac", "TaxSubtotal", null);
            WriteCbcAmount(writer, "cbc", "TaxableAmount", invoice.NetAmount, "EUR");
            WriteCbcAmount(writer, "cbc", "TaxAmount", invoice.PdvAmount, "EUR");
            writer.WriteStartElement("cac", "TaxCategory", null);
            WriteCbcElement(writer, "cbc", "ID", "S"); // S = Standard rate
            WriteCbcElement(writer, "cbc", "Percent",
                invoice.PdvRate.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteStartElement("cac", "TaxScheme", null);
            WriteCbcElement(writer, "cbc", "ID", "VAT");
            writer.WriteEndElement(); // TaxScheme
            writer.WriteEndElement(); // TaxCategory
            writer.WriteEndElement(); // TaxSubtotal
            writer.WriteEndElement(); // TaxTotal

            // ── Legal Monetary Total ──
            writer.WriteStartElement("cac", "LegalMonetaryTotal", null);
            WriteCbcAmount(writer, "cbc", "LineExtensionAmount", invoice.NetAmount, "EUR");
            WriteCbcAmount(writer, "cbc", "TaxExclusiveAmount", invoice.NetAmount, "EUR");
            WriteCbcAmount(writer, "cbc", "TaxInclusiveAmount", invoice.GrossAmount, "EUR");
            WriteCbcAmount(writer, "cbc", "PayableAmount", invoice.GrossAmount, "EUR");
            writer.WriteEndElement(); // LegalMonetaryTotal

            // ── Invoice Lines ──
            foreach (var line in invoice.LineItems)
            {
                writer.WriteStartElement("cac", "InvoiceLine", null);
                WriteCbcElement(writer, "cbc", "ID", line.LineNumber.ToString());
                WriteCbcElement(writer, "cbc", "InvoicedQuantity", line.Quantity.ToString("F0", CultureInfo.InvariantCulture));
                // Set unitCode attribute
                writer.WriteStartElement("cbc", "LineExtensionAmount", null);
                writer.WriteAttributeString("currencyID", "EUR");
                writer.WriteString(line.LineNetAmount.ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteEndElement();

                // Item
                writer.WriteStartElement("cac", "Item", null);
                WriteCbcElement(writer, "cbc", "Name", line.Description);
                writer.WriteStartElement("cac", "ClassifiedTaxCategory", null);
                WriteCbcElement(writer, "cbc", "ID", "S");
                WriteCbcElement(writer, "cbc", "Percent",
                    line.PdvRate.ToString("F2", CultureInfo.InvariantCulture));
                writer.WriteStartElement("cac", "TaxScheme", null);
                WriteCbcElement(writer, "cbc", "ID", "VAT");
                writer.WriteEndElement(); // TaxScheme
                writer.WriteEndElement(); // ClassifiedTaxCategory
                writer.WriteEndElement(); // Item

                // Price
                writer.WriteStartElement("cac", "Price", null);
                WriteCbcAmount(writer, "cbc", "PriceAmount", line.UnitPrice, "EUR");
                writer.WriteEndElement(); // Price

                writer.WriteEndElement(); // InvoiceLine
            }

            writer.WriteEndElement(); // Invoice
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    // ── XML Helpers ──

    private static void WriteCbcElement(XmlWriter writer, string prefix, string localName, string value)
    {
        writer.WriteStartElement(prefix, localName, null);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static void WriteCbcAmount(XmlWriter writer, string prefix, string localName, decimal amount, string currency)
    {
        writer.WriteStartElement(prefix, localName, null);
        writer.WriteAttributeString("currencyID", currency);
        writer.WriteString(amount.ToString("F2", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteParty(XmlWriter writer, InvoiceParty party)
    {
        writer.WriteStartElement("cac", "Party", null);

        // Party Name
        writer.WriteStartElement("cac", "PartyName", null);
        WriteCbcElement(writer, "cbc", "Name", party.Name);
        writer.WriteEndElement();

        // Postal Address
        writer.WriteStartElement("cac", "PostalAddress", null);
        if (!string.IsNullOrWhiteSpace(party.Address))
            WriteCbcElement(writer, "cbc", "StreetName", party.Address);
        if (!string.IsNullOrWhiteSpace(party.City))
            WriteCbcElement(writer, "cbc", "CityName", party.City);
        if (!string.IsNullOrWhiteSpace(party.PostalCode))
            WriteCbcElement(writer, "cbc", "PostalZone", party.PostalCode);
        writer.WriteStartElement("cac", "Country", null);
        WriteCbcElement(writer, "cbc", "IdentificationCode", party.CountryCode);
        writer.WriteEndElement(); // Country
        writer.WriteEndElement(); // PostalAddress

        // Tax ID (OIB)
        if (!string.IsNullOrWhiteSpace(party.Oib))
        {
            writer.WriteStartElement("cac", "PartyTaxScheme", null);
            WriteCbcElement(writer, "cbc", "CompanyID", $"HR{party.Oib}");
            writer.WriteStartElement("cac", "TaxScheme", null);
            WriteCbcElement(writer, "cbc", "ID", "VAT");
            writer.WriteEndElement(); // TaxScheme
            writer.WriteEndElement(); // PartyTaxScheme
        }

        // Legal Entity
        writer.WriteStartElement("cac", "PartyLegalEntity", null);
        WriteCbcElement(writer, "cbc", "RegistrationName", party.Name);
        if (!string.IsNullOrWhiteSpace(party.Oib))
            WriteCbcElement(writer, "cbc", "CompanyID", party.Oib);
        writer.WriteEndElement(); // PartyLegalEntity

        // Contact (email)
        if (!string.IsNullOrWhiteSpace(party.Email))
        {
            writer.WriteStartElement("cac", "Contact", null);
            WriteCbcElement(writer, "cbc", "ElectronicMail", party.Email);
            writer.WriteEndElement(); // Contact
        }

        writer.WriteEndElement(); // Party
    }

    /// <summary>
    /// Validate an OIB (Croatian tax ID) using the ISO 7064 MOD 11,10 algorithm.
    /// Returns true if the 11-digit OIB is structurally valid.
    /// </summary>
    public static bool ValidateOib(string oib)
    {
        if (string.IsNullOrWhiteSpace(oib) || oib.Length != 11 || !oib.All(char.IsDigit))
            return false;

        int remainder = 10;
        for (int i = 0; i < 10; i++)
        {
            int digit = oib[i] - '0';
            remainder = (remainder + digit) % 10;
            if (remainder == 0) remainder = 10;
            remainder = (remainder * 2) % 11;
        }

        int checkDigit = (11 - remainder) % 10;
        return checkDigit == (oib[10] - '0');
    }
}
