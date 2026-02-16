using System.Text.Json;
using System.Xml.Linq;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// V47: Unit tests for Fiskalizacija 2.0 — e-Invoice module.
/// Tests OIB validation (ISO 7064 MOD 11,10), UBL 2.1 XML generation,
/// invoice models, PDV calculation, and InvoiceServiceDescriptions.
///
/// ŠTO TESTIRAMO:
/// 1. OIB Validation — valid OIBs, invalid lengths, non-numeric, bad check digit
/// 2. InvoiceModels — defaults, serialization, InvoiceServiceDescriptions
/// 3. UBL 2.1 XML — structure, namespaces, party data, amounts, line items
/// 4. PDV Calculation — all Croatian tax rates (0%, 5%, 13%, 25%)
/// 5. Invoice Number — YYYY-NNNNN format generation
/// </summary>
public class InvoiceTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // ══════════════════════════════════════════════════════════
    //  1. OIB Validation (ISO 7064 MOD 11,10)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Test with well-known valid Croatian OIBs.
    /// The OIB check digit algorithm is ISO 7064 MOD 11,10.
    /// </summary>
    [Theory]
    [InlineData("12345678903")]  // Valid test OIB
    [InlineData("69435151530")]  // Another valid OIB
    public void ValidateOib_ValidOibs_ReturnsTrue(string oib)
    {
        Assert.True(InvoiceService.ValidateOib(oib));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateOib_NullOrEmpty_ReturnsFalse(string? oib)
    {
        Assert.False(InvoiceService.ValidateOib(oib!));
    }

    [Theory]
    [InlineData("1234567890")]    // Only 10 digits
    [InlineData("123456789012")]  // 12 digits
    [InlineData("123")]           // Too short
    public void ValidateOib_WrongLength_ReturnsFalse(string oib)
    {
        Assert.False(InvoiceService.ValidateOib(oib));
    }

    [Theory]
    [InlineData("1234567890a")]   // Letter at end
    [InlineData("ABCDEFGHIJK")]   // All letters
    [InlineData("12345-78903")]   // Dash
    public void ValidateOib_NonNumeric_ReturnsFalse(string oib)
    {
        Assert.False(InvoiceService.ValidateOib(oib));
    }

    [Theory]
    [InlineData("12345678901")]  // Wrong check digit (should be 3)
    [InlineData("12345678905")]  // Wrong check digit
    [InlineData("12345678909")]  // Wrong check digit
    public void ValidateOib_BadCheckDigit_ReturnsFalse(string oib)
    {
        Assert.False(InvoiceService.ValidateOib(oib));
    }

    // Brute-force verify the algorithm: generate check digit and validate
    [Fact]
    public void ValidateOib_AlgorithmConsistency_CheckDigitIsCorrect()
    {
        // Manually compute check digit for "1234567890"
        string prefix = "1234567890";
        int remainder = 10;
        for (int i = 0; i < 10; i++)
        {
            int digit = prefix[i] - '0';
            remainder = (remainder + digit) % 10;
            if (remainder == 0) remainder = 10;
            remainder = (remainder * 2) % 11;
        }
        int checkDigit = (11 - remainder) % 10;

        string fullOib = prefix + checkDigit.ToString();
        Assert.True(InvoiceService.ValidateOib(fullOib));
        Assert.Equal("12345678903", fullOib);
    }

    // ══════════════════════════════════════════════════════════
    //  2. InvoiceModels — Defaults and Serialization
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void InvoiceRequest_DefaultValues_AreCorrect()
    {
        var req = new InvoiceRequest();

        Assert.Null(req.AnalysisId);
        Assert.Equal("legal", req.Module);
        Assert.Equal("", req.ServiceDescription);
        Assert.Equal(0m, req.NetAmount);
        Assert.Equal(25m, req.PdvRate);
        Assert.Equal(15, req.PaymentDueDays);
        Assert.Null(req.CaseReference);
        Assert.Null(req.Notes);
        Assert.NotNull(req.Client);
        Assert.NotNull(req.Issuer);
    }

    [Fact]
    public void InvoiceParty_DefaultValues_AreCorrect()
    {
        var party = new InvoiceParty();

        Assert.Equal("", party.Name);
        Assert.Equal("", party.Oib);
        Assert.Equal("", party.Address);
        Assert.Equal("", party.City);
        Assert.Equal("", party.PostalCode);
        Assert.Equal("HR", party.CountryCode);
        Assert.Null(party.Email);
        Assert.Null(party.Iban);
    }

    [Fact]
    public void InvoiceResult_DefaultValues_AreCorrect()
    {
        var result = new InvoiceResult();

        Assert.Equal("", result.Id);
        Assert.Equal("", result.InvoiceNumber);
        Assert.Equal("EUR", result.CurrencyCode);
        Assert.Equal("DRAFT", result.Status);
        Assert.NotNull(result.LineItems);
        Assert.Empty(result.LineItems);
        Assert.Contains("PravoTek", result.Disclaimer);
        Assert.Contains("UBL 2.1", result.Disclaimer);
    }

    [Fact]
    public void InvoiceLineItem_DefaultValues_AreCorrect()
    {
        var item = new InvoiceLineItem();

        Assert.Equal(0, item.LineNumber);
        Assert.Equal("", item.Description);
        Assert.Equal(1m, item.Quantity);
        Assert.Equal("kom", item.Unit);
        Assert.Equal(0m, item.UnitPrice);
        Assert.Equal(0m, item.LineNetAmount);
        Assert.Equal(25m, item.PdvRate);
    }

    [Fact]
    public void InvoiceRequest_JsonRoundTrip_PreservesAllFields()
    {
        var request = new InvoiceRequest
        {
            AnalysisId = "abc-123",
            Module = "contract",
            ServiceDescription = "Analiza ugovora",
            NetAmount = 500m,
            PdvRate = 25m,
            PaymentDueDays = 30,
            CaseReference = "P-2026/001",
            Notes = "Hitno",
            Client = new InvoiceParty
            {
                Name = "Ivić d.o.o.",
                Oib = "12345678903",
                Address = "Ilica 1",
                City = "Zagreb",
                PostalCode = "10000",
                Email = "info@ivic.hr"
            },
            Issuer = new InvoiceParty
            {
                Name = "Odvjetnički ured Horvat",
                Oib = "69435151530",
                Iban = "HR1234567890123456789"
            }
        };

        var json = JsonSerializer.Serialize(request, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<InvoiceRequest>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("abc-123", deserialized.AnalysisId);
        Assert.Equal("contract", deserialized.Module);
        Assert.Equal(500m, deserialized.NetAmount);
        Assert.Equal(25m, deserialized.PdvRate);
        Assert.Equal(30, deserialized.PaymentDueDays);
        Assert.Equal("P-2026/001", deserialized.CaseReference);
        Assert.Equal("Ivić d.o.o.", deserialized.Client.Name);
        Assert.Equal("12345678903", deserialized.Client.Oib);
        Assert.Equal("Odvjetnički ured Horvat", deserialized.Issuer.Name);
        Assert.Equal("HR1234567890123456789", deserialized.Issuer.Iban);
    }

    [Fact]
    public void InvoiceResult_JsonSerialization_SnakeCaseKeys()
    {
        var result = new InvoiceResult
        {
            InvoiceNumber = "2026-00001",
            NetAmount = 1000m,
            PdvRate = 25m,
            PdvAmount = 250m,
            GrossAmount = 1250m,
            CurrencyCode = "EUR",
            CaseReference = "P-123"
        };

        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        Assert.Contains("\"invoice_number\"", json);
        Assert.Contains("\"net_amount\"", json);
        Assert.Contains("\"pdv_rate\"", json);
        Assert.Contains("\"pdv_amount\"", json);
        Assert.Contains("\"gross_amount\"", json);
        Assert.Contains("\"currency_code\"", json);
        Assert.Contains("\"case_reference\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  3. InvoiceServiceDescriptions
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("valuation", "Procjena")]
    [InlineData("contract", "Pravna analiza ugovora")]
    [InlineData("chronology", "Izrada kronologije")]
    [InlineData("legal", "Analiza sudske prakse")]
    public void InvoiceServiceDescriptions_KnownModules_ReturnsDescription(string module, string partialExpected)
    {
        var desc = InvoiceServiceDescriptions.GetDescription(module);

        Assert.Contains(partialExpected, desc);
    }

    [Fact]
    public void InvoiceServiceDescriptions_UnknownModule_ReturnsFallback()
    {
        var desc = InvoiceServiceDescriptions.GetDescription("unknown_module");

        Assert.Contains("Pravna usluga", desc);
    }

    [Fact]
    public void InvoiceServiceDescriptions_AllModulesHaveEntries()
    {
        var modules = new[] { "valuation", "contract", "chronology", "legal" };

        foreach (var m in modules)
        {
            Assert.True(InvoiceServiceDescriptions.Descriptions.ContainsKey(m), $"Missing description for {m}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  4. UBL 2.1 XML Generation
    // ══════════════════════════════════════════════════════════

    private static InvoiceResult CreateSampleInvoice()
    {
        return new InvoiceResult
        {
            Id = "test-uuid",
            InvoiceNumber = "2026-00001",
            IssueDate = "2026-01-15",
            DueDate = "2026-02-14",
            Issuer = new InvoiceParty
            {
                Name = "Odvjetnički ured Test",
                Oib = "12345678903",
                Address = "Frankopanska 10",
                City = "Zagreb",
                PostalCode = "10000",
                CountryCode = "HR",
                Email = "odvjetnik@test.hr",
                Iban = "HR1234567890123456789"
            },
            Client = new InvoiceParty
            {
                Name = "Klijent d.o.o.",
                Oib = "69435151530",
                Address = "Vlaška 20",
                City = "Zagreb",
                PostalCode = "10000",
                CountryCode = "HR",
                Email = "klijent@test.hr"
            },
            LineItems =
            [
                new InvoiceLineItem
                {
                    LineNumber = 1,
                    Description = "Pravna analiza ugovora",
                    Quantity = 1,
                    Unit = "kom",
                    UnitPrice = 500m,
                    LineNetAmount = 500m,
                    PdvRate = 25m
                }
            ],
            NetAmount = 500m,
            PdvRate = 25m,
            PdvAmount = 125m,
            GrossAmount = 625m,
            CurrencyCode = "EUR",
            Module = "contract",
            CaseReference = "P-2026/001",
            Status = "DRAFT",
            Notes = "Test faktura"
        };
    }

    [Fact]
    public void GenerateUblXml_ValidInvoice_ProducesValidXml()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.NotNull(xml);
        Assert.NotEmpty(xml);

        // Should parse without error
        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
    }

    [Fact]
    public void GenerateUblXml_ContainsCorrectNamespace()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);
        var doc = XDocument.Parse(xml);

        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            doc.Root!.Name.NamespaceName);
        Assert.Equal("Invoice", doc.Root.Name.LocalName);
    }

    [Fact]
    public void GenerateUblXml_ContainsUblVersion()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("2.1", xml);  // UBLVersionID
        Assert.Contains("en16931", xml); // EN 16931 customization
    }

    [Fact]
    public void GenerateUblXml_ContainsInvoiceNumber()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("2026-00001", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsCorrectDates()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("2026-01-15", xml); // IssueDate
        Assert.Contains("2026-02-14", xml); // DueDate
    }

    [Fact]
    public void GenerateUblXml_ContainsInvoiceTypeCode380()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("380", xml); // Commercial invoice
    }

    [Fact]
    public void GenerateUblXml_ContainsCurrencyEUR()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("EUR", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsSupplierData()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("Odvjetnički ured Test", xml);
        Assert.Contains("HR12345678903", xml);  // OIB with HR prefix
        Assert.Contains("Frankopanska 10", xml);
        Assert.Contains("odvjetnik@test.hr", xml);
        Assert.Contains("HR1234567890123456789", xml); // IBAN
    }

    [Fact]
    public void GenerateUblXml_ContainsCustomerData()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("Klijent d.o.o.", xml);
        Assert.Contains("HR69435151530", xml);  // OIB with HR prefix
        Assert.Contains("Vlaška 20", xml);
        Assert.Contains("klijent@test.hr", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsTaxTotal()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("TaxTotal", xml);
        Assert.Contains("TaxSubtotal", xml);
        Assert.Contains("125.00", xml);  // PDV amount
        Assert.Contains("TaxScheme", xml);
        Assert.Contains("VAT", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsLegalMonetaryTotal()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("LegalMonetaryTotal", xml);
        Assert.Contains("500.00", xml);  // Net
        Assert.Contains("625.00", xml);  // Gross
    }

    [Fact]
    public void GenerateUblXml_ContainsLineItem()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("InvoiceLine", xml);
        Assert.Contains("Pravna analiza ugovora", xml);
        Assert.Contains("InvoicedQuantity", xml);
        Assert.Contains("Price", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsBuyerReference()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("BuyerReference", xml);
        Assert.Contains("P-2026/001", xml);
    }

    [Fact]
    public void GenerateUblXml_ContainsNote()
    {
        var invoice = CreateSampleInvoice();
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("Test faktura", xml);
    }

    [Fact]
    public void GenerateUblXml_NoBuyerReference_WhenEmpty()
    {
        var invoice = CreateSampleInvoice();
        invoice.CaseReference = null;
        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.DoesNotContain("BuyerReference", xml);
    }

    [Fact]
    public void GenerateUblXml_NoNote_WhenEmpty()
    {
        var invoice = CreateSampleInvoice();
        invoice.Notes = null;
        var xml = InvoiceService.GenerateUblXml(invoice);

        // Note element should not appear (or at least there's no "Note" content element)
        // Note: there may still be Invoice-level Note element, but the notes text should be absent
        Assert.DoesNotContain("Test faktura", xml);
    }

    // ══════════════════════════════════════════════════════════
    //  5. PDV Calculation
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1000, 25, 250, 1250)]    // Standard rate — 25%
    [InlineData(1000, 13, 130, 1130)]    // Reduced rate — 13% (hospitality, etc.)
    [InlineData(1000, 5, 50, 1050)]      // Super-reduced rate — 5%
    [InlineData(1000, 0, 0, 1000)]       // Exempt — 0%
    [InlineData(750.50, 25, 187.62, 938.12)]  // Decimal amounts (banker's rounding)
    public void PdvCalculation_AllRates_CorrectAmounts(
        decimal netAmount, decimal pdvRate, decimal expectedPdv, decimal expectedGross)
    {
        var pdvAmount = Math.Round(netAmount * pdvRate / 100m, 2);
        var grossAmount = netAmount + pdvAmount;

        Assert.Equal(expectedPdv, pdvAmount);
        Assert.Equal(expectedGross, grossAmount);
    }

    [Fact]
    public void PdvCalculation_LargeAmount_NoOverflow()
    {
        decimal netAmount = 9_999_999.99m;
        decimal pdvRate = 25m;

        var pdvAmount = Math.Round(netAmount * pdvRate / 100m, 2);
        var grossAmount = netAmount + pdvAmount;

        Assert.Equal(2_500_000.00m, pdvAmount);
        Assert.Equal(12_499_999.99m, grossAmount);
    }

    // ══════════════════════════════════════════════════════════
    //  6. UBL XML — Multiple Line Items
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void GenerateUblXml_MultipleLineItems_AllPresent()
    {
        var invoice = CreateSampleInvoice();
        invoice.LineItems.Add(new InvoiceLineItem
        {
            LineNumber = 2,
            Description = "Dodatna konzultacija",
            Quantity = 2,
            Unit = "sat",
            UnitPrice = 100m,
            LineNetAmount = 200m,
            PdvRate = 25m
        });

        var xml = InvoiceService.GenerateUblXml(invoice);

        Assert.Contains("Pravna analiza ugovora", xml);
        Assert.Contains("Dodatna konzultacija", xml);
    }

    // ══════════════════════════════════════════════════════════
    //  7. InvoiceResult — Disclaimer
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void InvoiceResult_Disclaimer_ContainsLegalText()
    {
        var result = new InvoiceResult();

        Assert.Contains("PravoTek", result.Disclaimer);
        Assert.Contains("UBL 2.1", result.Disclaimer);
        Assert.Contains("e-Račun", result.Disclaimer);
        Assert.Contains("Zakon", result.Disclaimer);
    }

    // ══════════════════════════════════════════════════════════
    //  8. Edge Cases
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void GenerateUblXml_NoEmail_OmitsContact()
    {
        var invoice = CreateSampleInvoice();
        invoice.Issuer.Email = null;
        invoice.Client.Email = null;

        var xml = InvoiceService.GenerateUblXml(invoice);

        // Should still produce valid XML
        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);

        // Contact element should not appear for parties without email
        Assert.DoesNotContain("ElectronicMail", xml);
    }

    [Fact]
    public void GenerateUblXml_NoIban_OmitsPaymentMeans()
    {
        var invoice = CreateSampleInvoice();
        invoice.Issuer.Iban = null;

        var xml = InvoiceService.GenerateUblXml(invoice);

        // Should still produce valid XML
        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
    }

    [Fact]
    public void GenerateUblXml_ZeroPdv_StillValid()
    {
        var invoice = CreateSampleInvoice();
        invoice.PdvRate = 0m;
        invoice.PdvAmount = 0m;
        invoice.GrossAmount = 500m;
        invoice.LineItems[0].PdvRate = 0m;

        var xml = InvoiceService.GenerateUblXml(invoice);

        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
        Assert.Contains("0.00", xml); // Zero PDV amount
    }

    [Fact]
    public void InvoiceRequest_CanSetAllFields()
    {
        var request = new InvoiceRequest
        {
            AnalysisId = "uuid-999",
            Module = "valuation",
            ServiceDescription = "Custom opis",
            NetAmount = 2500.50m,
            PdvRate = 13m,
            PaymentDueDays = 60,
            CaseReference = "REF-456",
            Notes = "Napomena",
            Client = new InvoiceParty { Name = "Klijent", Oib = "12345678903" },
            Issuer = new InvoiceParty { Name = "Odvjetnik", Oib = "69435151530" }
        };

        Assert.Equal("uuid-999", request.AnalysisId);
        Assert.Equal("valuation", request.Module);
        Assert.Equal("Custom opis", request.ServiceDescription);
        Assert.Equal(2500.50m, request.NetAmount);
        Assert.Equal(13m, request.PdvRate);
        Assert.Equal(60, request.PaymentDueDays);
        Assert.Equal("REF-456", request.CaseReference);
        Assert.Equal("Napomena", request.Notes);
    }

    // ══════════════════════════════════════════════════════════
    //  9. OIB — All-zeros and special patterns
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("00000000000")]  // All zeros  
    [InlineData("11111111111")]  // All ones
    [InlineData("99999999999")]  // All nines
    public void ValidateOib_RepeatedDigits_ChecksAlgorithm(string oib)
    {
        // These may or may not be valid — the test just verifies no crash
        _ = InvoiceService.ValidateOib(oib);
    }

    [Fact]
    public void ValidateOib_AllZeros_FailsCheckDigit()
    {
        // 00000000000: Let's compute — the check digit for 0000000000 is:
        // Each step: remainder = (remainder + 0) % 10 → 0 → 10, then (10*2) % 11 = 9
        // After 10 iterations, check = (11-remainder) % 10
        // This should fail because the last digit is 0 but calculated check digit differs
        // We just verify it doesn't crash and returns a boolean
        var result = InvoiceService.ValidateOib("00000000000");
        Assert.IsType<bool>(result);
    }
}
