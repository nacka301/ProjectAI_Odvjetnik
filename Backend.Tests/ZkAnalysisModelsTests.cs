using System.Text.Json;
using Backend.Models;

namespace Backend.Tests;

/// <summary>
/// Testovi za ZK Analysis modele — provjeravaju da se svi V31 Due Diligence
/// modeli pravilno kreiraju, serializiraju i deserializiraju s snake_case JSON politi-kom.
/// 
/// ŠTO TESTIRAMO:
/// 1. PlombaInfo — model za aktivne plombe (Z-xxxx/yyyy)
/// 2. DueDiligenceFinding — strukturirani pravni nalaz (category/severity/title/description/advice)
/// 3. ZkParsedResult — kompletan rezultat sa svim V31 poljima
/// 4. JSON serialization/deserialization sa snake_case (kao što backend šalje frontendu)
/// </summary>
public class ZkAnalysisModelsTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // ══════════════════════════════════════════════════════════
    //  1. PlombaInfo — model za plombe
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PlombaInfo_DefaultValues_AreEmptyStrings()
    {
        var plomba = new PlombaInfo();

        Assert.Equal(string.Empty, plomba.ZNumber);
        Assert.Equal(string.Empty, plomba.Description);
        Assert.Equal(string.Empty, plomba.Date);
    }

    [Fact]
    public void PlombaInfo_CanBeCreatedWithValues()
    {
        var plomba = new PlombaInfo
        {
            ZNumber = "Z-1234/2024",
            Description = "Zahtjev za upis prava vlasništva",
            Date = "15.01.2024."
        };

        Assert.Equal("Z-1234/2024", plomba.ZNumber);
        Assert.Equal("Zahtjev za upis prava vlasništva", plomba.Description);
        Assert.Equal("15.01.2024.", plomba.Date);
    }

    [Fact]
    public void PlombaInfo_SerializesToSnakeCase()
    {
        var plomba = new PlombaInfo
        {
            ZNumber = "Z-5678/2025",
            Description = "Prijedlog za upis hipoteke",
            Date = "01.03.2025."
        };

        var json = JsonSerializer.Serialize(plomba, SnakeCaseOptions);

        Assert.Contains("\"z_number\":\"Z-5678/2025\"", json);
        Assert.Contains("\"description\":\"Prijedlog za upis hipoteke\"", json);
        Assert.Contains("\"date\":\"01.03.2025.\"", json);
    }

    [Fact]
    public void PlombaInfo_DeserializesFromSnakeCase()
    {
        var json = """{"z_number":"Z-9999/2024","description":"Test plomba","date":"10.05.2024."}""";

        var plomba = JsonSerializer.Deserialize<PlombaInfo>(json, SnakeCaseOptions);

        Assert.NotNull(plomba);
        Assert.Equal("Z-9999/2024", plomba!.ZNumber);
        Assert.Equal("Test plomba", plomba.Description);
        Assert.Equal("10.05.2024.", plomba.Date);
    }

    // ══════════════════════════════════════════════════════════
    //  2. DueDiligenceFinding — strukturirani nalaz
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void DueDiligenceFinding_DefaultValues_AreEmptyStrings()
    {
        var finding = new DueDiligenceFinding();

        Assert.Equal(string.Empty, finding.Category);
        Assert.Equal(string.Empty, finding.Severity);
        Assert.Equal(string.Empty, finding.Title);
        Assert.Equal(string.Empty, finding.Description);
        Assert.Equal(string.Empty, finding.Advice);
    }

    [Theory]
    [InlineData("CRITICAL")]
    [InlineData("WARNING")]
    [InlineData("INFO")]
    public void DueDiligenceFinding_AcceptsAllSeverityLevels(string severity)
    {
        var finding = new DueDiligenceFinding { Severity = severity };
        Assert.Equal(severity, finding.Severity);
    }

    [Theory]
    [InlineData("PLOMBA")]
    [InlineData("VLASNISTVO")]
    [InlineData("HIPOTEKA")]
    [InlineData("SLUZNOST")]
    [InlineData("OVRHA")]
    [InlineData("LEGALIZACIJA")]
    [InlineData("OSTALO")]
    public void DueDiligenceFinding_AcceptsAllCategories(string category)
    {
        var finding = new DueDiligenceFinding { Category = category };
        Assert.Equal(category, finding.Category);
    }

    [Fact]
    public void DueDiligenceFinding_CriticalPlomba_Example()
    {
        var finding = new DueDiligenceFinding
        {
            Category = "PLOMBA",
            Severity = "CRITICAL",
            Title = "Aktivna plomba Z-1234/2024",
            Description = "Netko je predao zahtjev za upis vlasništva prije vas.",
            Advice = "Ne plaćajte kaparu dok se ne razriješi plomba."
        };

        Assert.Equal("PLOMBA", finding.Category);
        Assert.Equal("CRITICAL", finding.Severity);
        Assert.Contains("Z-1234/2024", finding.Title);
        Assert.NotEmpty(finding.Advice);
    }

    [Fact]
    public void DueDiligenceFinding_DealBreakerSluznost_Example()
    {
        var finding = new DueDiligenceFinding
        {
            Category = "SLUZNOST",
            Severity = "CRITICAL",
            Title = "Osobna služnost — Pravo doživotnog uživanja",
            Description = "Osobna služnost u korist Ivana Ivića. DEAL-BREAKER: ne možete iseliti nositelja prava.",
            Advice = "Odustanite od kupnje ili zatražite brisanje služnosti prije transakcije."
        };

        Assert.Equal("CRITICAL", finding.Severity);
        Assert.Contains("DEAL-BREAKER", finding.Description);
    }

    [Fact]
    public void DueDiligenceFinding_SerializesToSnakeCase()
    {
        var finding = new DueDiligenceFinding
        {
            Category = "HIPOTEKA",
            Severity = "WARNING",
            Title = "Aktivna hipoteka",
            Description = "100.000 EUR u korist PBZ d.d.",
            Advice = "Ishodite pismo namjere banke."
        };

        var json = JsonSerializer.Serialize(finding, SnakeCaseOptions);

        Assert.Contains("\"category\":\"HIPOTEKA\"", json);
        Assert.Contains("\"severity\":\"WARNING\"", json);
        Assert.Contains("\"title\":\"Aktivna hipoteka\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  3. ZkParsedResult — V31 Due Diligence polja
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_V31Fields_HaveCorrectDefaults()
    {
        var result = new ZkParsedResult();

        // New V31 fields
        Assert.Empty(result.ActivePlombe);
        Assert.Empty(result.Findings);
        Assert.Equal(string.Empty, result.OverallStatus);
        Assert.Equal(string.Empty, result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect); // default = true
        Assert.Equal(string.Empty, result.SharesSumDisplay);
        Assert.Equal(string.Empty, result.LandUseMismatch);
        Assert.False(result.HasUsagePermitIssue);
        Assert.Equal(string.Empty, result.DueDiligenceSummary);

        // Legacy fields still work
        Assert.Equal(string.Empty, result.RiskScore);
        Assert.Equal(string.Empty, result.LawyerComment);
        Assert.Empty(result.Owners);
        Assert.Empty(result.ActiveMortgages);
        Assert.Empty(result.ActiveDisputes);
        Assert.Empty(result.OtherEncumbrances);
    }

    [Fact]
    public void ZkParsedResult_CleanProperty_StatusIsCisto()
    {
        var result = CreateCleanProperty();

        Assert.Equal("ČISTO", result.OverallStatus);
        Assert.Equal("LOW", result.RiskScore);
        Assert.Equal("1/1 ČISTO", result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect);
        Assert.Empty(result.ActivePlombe);
        Assert.Empty(result.ActiveDisputes);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ZkParsedResult_WithPlomba_StatusIsRizicno()
    {
        var result = CreatePropertyWithPlomba();

        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);
        Assert.Single(result.ActivePlombe);
        Assert.Equal("Z-1234/2024", result.ActivePlombe[0].ZNumber);
        Assert.Contains(result.Findings, f => f.Category == "PLOMBA" && f.Severity == "CRITICAL");
    }

    [Fact]
    public void ZkParsedResult_WithHipoteka_StatusIsUvjetno()
    {
        var result = CreatePropertyWithMortgage();

        Assert.Equal("UVJETNO", result.OverallStatus);
        Assert.Equal("MEDIUM", result.RiskScore);
        Assert.Single(result.ActiveMortgages);
        Assert.Contains(result.Findings, f => f.Category == "HIPOTEKA" && f.Severity == "WARNING");
    }

    [Fact]
    public void ZkParsedResult_WithOsobnaSluznost_IsDealBreaker()
    {
        var result = CreatePropertyWithDealBreaker();

        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.Equal("HIGH", result.RiskScore);
        var finding = Assert.Single(result.Findings, f => f.Category == "SLUZNOST");
        Assert.Equal("CRITICAL", finding.Severity);
        Assert.Contains("doživotnog uživanja", finding.Description);
    }

    [Fact]
    public void ZkParsedResult_Suvlasnistvo_ValidatesShareMath()
    {
        var result = CreatePropertyWithSuvlasnistvo();

        Assert.Equal("SUVLASNIŠTVO", result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect);
        Assert.Equal("1/4 + 1/4 + 2/4 = 4/4 ✓", result.SharesSumDisplay);
        Assert.Equal(3, result.Owners.Count);
    }

    [Fact]
    public void ZkParsedResult_InvalidShares_DetectsError()
    {
        var result = CreatePropertyWithInvalidShares();

        Assert.False(result.SharesSumCorrect);
        Assert.Contains("≠ 1/1", result.SharesSumDisplay);
        Assert.Contains(result.Findings, f => f.Category == "VLASNISTVO" && f.Severity == "CRITICAL");
    }

    [Fact]
    public void ZkParsedResult_LandUseMismatch_Detected()
    {
        var result = CreatePropertyWithLandUseMismatch();

        Assert.NotEmpty(result.LandUseMismatch);
        Assert.Contains("pašnjak", result.LandUseMismatch.ToLower());
        Assert.Contains(result.Findings, f => f.Category == "LEGALIZACIJA");
    }

    [Fact]
    public void ZkParsedResult_UsagePermitIssue_Detected()
    {
        var result = new ZkParsedResult
        {
            HasUsagePermitIssue = true,
            Findings =
            [
                new DueDiligenceFinding
                {
                    Category = "LEGALIZACIJA",
                    Severity = "WARNING",
                    Title = "Uporabna dozvola",
                    Description = "Nije priložena uporabna dozvola.",
                    Advice = "Banke često odbijaju kredit bez uporabne dozvole."
                }
            ]
        };

        Assert.True(result.HasUsagePermitIssue);
        Assert.Single(result.Findings);
    }

    // ══════════════════════════════════════════════════════════
    //  4. Full JSON roundtrip (Backend → Frontend → Backend)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_FullJsonRoundtrip_PreservesAllV31Fields()
    {
        var original = CreateFullDueDiligenceResult();

        // Serialize (as backend sends to frontend)
        var json = JsonSerializer.Serialize(original, SnakeCaseOptions);

        // Deserialize (as if parsing GPT response)
        var deserialized = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);

        // V31 Due Diligence fields
        Assert.Equal(original.OverallStatus, deserialized!.OverallStatus);
        Assert.Equal(original.OwnershipStatus, deserialized.OwnershipStatus);
        Assert.Equal(original.SharesSumCorrect, deserialized.SharesSumCorrect);
        Assert.Equal(original.SharesSumDisplay, deserialized.SharesSumDisplay);
        Assert.Equal(original.LandUseMismatch, deserialized.LandUseMismatch);
        Assert.Equal(original.HasUsagePermitIssue, deserialized.HasUsagePermitIssue);
        Assert.Equal(original.DueDiligenceSummary, deserialized.DueDiligenceSummary);

        // Plombe
        Assert.Equal(original.ActivePlombe.Count, deserialized.ActivePlombe.Count);
        Assert.Equal(original.ActivePlombe[0].ZNumber, deserialized.ActivePlombe[0].ZNumber);

        // Findings
        Assert.Equal(original.Findings.Count, deserialized.Findings.Count);
        for (int i = 0; i < original.Findings.Count; i++)
        {
            Assert.Equal(original.Findings[i].Category, deserialized.Findings[i].Category);
            Assert.Equal(original.Findings[i].Severity, deserialized.Findings[i].Severity);
            Assert.Equal(original.Findings[i].Title, deserialized.Findings[i].Title);
        }

        // Legacy fields
        Assert.Equal(original.RiskScore, deserialized.RiskScore);
        Assert.Equal(original.LawyerComment, deserialized.LawyerComment);
        Assert.Equal(original.PropertyType, deserialized.PropertyType);
        Assert.Equal(original.Owners.Count, deserialized.Owners.Count);
        Assert.Equal(original.ActiveMortgages.Count, deserialized.ActiveMortgages.Count);
    }

    [Fact]
    public void ZkParsedResult_DeserializeFromGptStyleJson_Works()
    {
        // Simulate what GPT-4o would return
        var gptJson = """
        {
          "property_id": "k.o. Trnje, k.č. 4521/3",
          "cadastral_municipality": "Trnje",
          "parcel_number": "4521/3",
          "zk_number": "ZK.ul. 1203",
          "property_type": "stan",
          "property_description": "Stan u k.o. Trnje, E-5",
          "land_area_m2": 1250,
          "building_area_m2": 65,
          "land_use": "stambena zgrada",
          "etazni_udio": "E-5, 65/1000",
          "parcels": [
            {"parcel_number": "4521/3", "land_use": "stambena zgrada", "area_m2": 1250, "description": ""}
          ],
          "owners": [
            {"name": "Ana Anić", "share": "1/1", "oib": "12345678901", "basis_of_acquisition": "kupoprodajni ugovor"}
          ],
          "active_mortgages": [
            {"amount": "80.000", "currency": "EUR", "year": "2019", "details": "Hipoteka u korist Erste banke d.d."}
          ],
          "active_disputes": [],
          "other_encumbrances": [],
          "active_plombe": [],
          "risk_score": "MEDIUM",
          "overall_status": "UVJETNO",
          "ownership_status": "1/1 ČISTO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/1 = 1/1 ✓",
          "land_use_mismatch": "",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "HIPOTEKA",
              "severity": "WARNING",
              "title": "Aktivna hipoteka 80.000 EUR",
              "description": "Aktivna hipoteka u korist Erste banke d.d. u iznosu od 80.000 EUR.",
              "advice": "Potrebno ishoditi brisovnu dozvolu ili pismo namjere banke prije isplate kupovnine."
            }
          ],
          "due_diligence_summary": "Na predmetnoj nekretnini upisano je založno pravo (hipoteka) u korist Erste banke d.d. u iznosu od 80.000 EUR. Vlasništvo je čisto (1/1). Preporučam ishoditi pismo namjere banke.",
          "lawyer_comment": "Hipoteka prisutna, inače čisto vlasništvo.",
          "location": "Trnje, Zagreb",
          "address": "",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(gptJson, SnakeCaseOptions);

        Assert.NotNull(result);
        Assert.Equal("stan", result!.PropertyType);
        Assert.Equal("Trnje", result.CadastralMunicipality);
        Assert.Equal(65, result.BuildingAreaM2);
        Assert.Equal("UVJETNO", result.OverallStatus);
        Assert.Equal("1/1 ČISTO", result.OwnershipStatus);
        Assert.True(result.SharesSumCorrect);
        Assert.Single(result.Owners);
        Assert.Equal("Ana Anić", result.Owners[0].Name);
        Assert.Single(result.ActiveMortgages);
        Assert.Single(result.Findings);
        Assert.Equal("HIPOTEKA", result.Findings[0].Category);
        Assert.Equal("WARNING", result.Findings[0].Severity);
        Assert.Empty(result.ActivePlombe);
        Assert.False(result.HasUsagePermitIssue);
        Assert.Contains("Erste banke", result.DueDiligenceSummary);
    }

    [Fact]
    public void ZkParsedResult_SnakeCaseJson_ContainsAllNewFields()
    {
        var result = CreateFullDueDiligenceResult();
        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        // Verify all V31 field names appear in JSON as snake_case
        Assert.Contains("\"active_plombe\"", json);
        Assert.Contains("\"findings\"", json);
        Assert.Contains("\"overall_status\"", json);
        Assert.Contains("\"ownership_status\"", json);
        Assert.Contains("\"shares_sum_correct\"", json);
        Assert.Contains("\"shares_sum_display\"", json);
        Assert.Contains("\"land_use_mismatch\"", json);
        Assert.Contains("\"has_usage_permit_issue\"", json);
        Assert.Contains("\"due_diligence_summary\"", json);

        // Plomba fields
        Assert.Contains("z_number", json);

        // Finding fields — these are already lowercase so snake_case keeps them
        Assert.Contains("category", json);
        Assert.Contains("severity", json);
        Assert.Contains("advice", json);
    }

    [Fact]
    public void UnifiedZkValuationResult_ContainsZkAnalysis()
    {
        var unified = new UnifiedZkValuationResult
        {
            ZkAnalysis = CreateCleanProperty(),
            ValuationAttempted = false,
            ValuationSkipReason = "Korisnik odabrao samo analizu"
        };

        Assert.NotNull(unified.ZkAnalysis);
        Assert.Equal("ČISTO", unified.ZkAnalysis.OverallStatus);
        Assert.False(unified.ValuationAttempted);
        Assert.Null(unified.Valuation);
    }

    // ══════════════════════════════════════════════════════════
    //  5. Edge Cases
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_EmptyArrays_SerializeCorrectly()
    {
        var result = new ZkParsedResult();
        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        Assert.Contains("\"active_plombe\":[]", json);
        Assert.Contains("\"findings\":[]", json);
        Assert.Contains("\"owners\":[]", json);
        Assert.Contains("\"active_mortgages\":[]", json);
        Assert.Contains("\"active_disputes\":[]", json);
    }

    [Fact]
    public void ZkParsedResult_CroatianCharacters_PreservedInSerialization()
    {
        var result = new ZkParsedResult
        {
            OverallStatus = "ČISTO",
            OwnershipStatus = "SUVLASNIŠTVO",
            DueDiligenceSummary = "Vlasništvo čisto, bez tereta. Služnost prolaza je bezopasna."
        };

        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions);

        Assert.Equal("ČISTO", deserialized!.OverallStatus);
        Assert.Equal("SUVLASNIŠTVO", deserialized.OwnershipStatus);
        Assert.Contains("služnost", deserialized.DueDiligenceSummary.ToLower());
    }

    [Fact]
    public void ZkParsedResult_MultipleFindingsOfSameCategory_Allowed()
    {
        var result = new ZkParsedResult
        {
            Findings =
            [
                new DueDiligenceFinding { Category = "HIPOTEKA", Severity = "WARNING", Title = "Hipoteka 1" },
                new DueDiligenceFinding { Category = "HIPOTEKA", Severity = "WARNING", Title = "Hipoteka 2" },
                new DueDiligenceFinding { Category = "HIPOTEKA", Severity = "WARNING", Title = "Hipoteka 3" }
            ]
        };

        Assert.Equal(3, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal("HIPOTEKA", f.Category));
    }

    // ══════════════════════════════════════════════════════════
    //  V44: OCR 2.0 — PlombaInfo enhanced fields
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PlombaInfo_V44_DefaultValues()
    {
        var plomba = new PlombaInfo();

        Assert.Equal(string.Empty, plomba.PlombaType);
        Assert.Equal("CRITICAL", plomba.RiskLevel);
        Assert.True(plomba.BlocksSale);
    }

    [Theory]
    [InlineData("VLASNISTVO")]
    [InlineData("HIPOTEKA")]
    [InlineData("OVRHA")]
    [InlineData("BRISANJE")]
    [InlineData("ZABILJEŽBA")]
    [InlineData("OSTALO")]
    public void PlombaInfo_V44_AllPlombaTypesValid(string plombaType)
    {
        var plomba = new PlombaInfo
        {
            ZNumber = "Z-100/2024",
            PlombaType = plombaType,
            RiskLevel = "WARNING",
            BlocksSale = false
        };

        Assert.Equal(plombaType, plomba.PlombaType);
    }

    [Fact]
    public void PlombaInfo_V44_SerializesNewFields()
    {
        var plomba = new PlombaInfo
        {
            ZNumber = "Z-999/2025",
            Description = "Fiducija",
            Date = "01.06.2025.",
            PlombaType = "OVRHA",
            RiskLevel = "CRITICAL",
            BlocksSale = true
        };

        var json = JsonSerializer.Serialize(plomba, SnakeCaseOptions);

        Assert.Contains("\"plomba_type\":\"OVRHA\"", json);
        Assert.Contains("\"risk_level\":\"CRITICAL\"", json);
        Assert.Contains("\"blocks_sale\":true", json);
    }

    [Fact]
    public void PlombaInfo_V44_DeserializesFromSnakeCase()
    {
        var json = """{"z_number":"Z-111/2024","description":"Test","date":"","plomba_type":"BRISANJE","risk_level":"INFO","blocks_sale":false}""";
        var plomba = JsonSerializer.Deserialize<PlombaInfo>(json, SnakeCaseOptions);

        Assert.NotNull(plomba);
        Assert.Equal("BRISANJE", plomba!.PlombaType);
        Assert.Equal("INFO", plomba.RiskLevel);
        Assert.False(plomba.BlocksSale);
    }

    // ══════════════════════════════════════════════════════════
    //  V44: OCR 2.0 — EncumbranceInfo enhanced fields
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EncumbranceInfo_V44_DefaultValues()
    {
        var enc = new EncumbranceInfo();

        Assert.Equal(string.Empty, enc.RiskType);
        Assert.Equal(string.Empty, enc.LegalBasis);
        Assert.False(enc.BlocksSale);
        Assert.False(enc.BlocksConstruction);
        Assert.Equal(0, enc.YearRegistered);
        Assert.False(enc.IsHistorical);
    }

    [Theory]
    [InlineData("FIDUCIJA")]
    [InlineData("OSOBNA_SLUZNOST")]
    [InlineData("STVARNA_SLUZNOST")]
    [InlineData("ZABRANA_OTUDENJA")]
    [InlineData("PRAVO_GRADENJA")]
    [InlineData("KONCESIJA")]
    [InlineData("OVRHA")]
    [InlineData("PRAVO_PRVOKUPA")]
    [InlineData("OSTALO")]
    public void EncumbranceInfo_V44_AllRiskTypesValid(string riskType)
    {
        var enc = new EncumbranceInfo
        {
            Type = "test",
            RiskType = riskType
        };

        Assert.Equal(riskType, enc.RiskType);
    }

    [Fact]
    public void EncumbranceInfo_V44_FiducijaScenario()
    {
        var enc = new EncumbranceInfo
        {
            Type = "fiducijarni prijenos",
            Description = "Fiducijarni prijenos vlasništva radi osiguranja tražbine",
            Beneficiary = "XY Kredit d.o.o.",
            IsActive = true,
            RiskType = "FIDUCIJA",
            LegalBasis = "čl. 34. ZV",
            BlocksSale = true,
            BlocksConstruction = false,
            YearRegistered = 2019,
            IsHistorical = false
        };

        Assert.Equal("FIDUCIJA", enc.RiskType);
        Assert.True(enc.BlocksSale);
        Assert.False(enc.BlocksConstruction);
        Assert.Equal(2019, enc.YearRegistered);
    }

    [Fact]
    public void EncumbranceInfo_V44_SerializesNewFields()
    {
        var enc = new EncumbranceInfo
        {
            Type = "zabrana otuđenja",
            RiskType = "ZABRANA_OTUDENJA",
            LegalBasis = "čl. 35. ZV",
            BlocksSale = true,
            BlocksConstruction = true,
            YearRegistered = 2022,
            IsHistorical = false
        };

        var json = JsonSerializer.Serialize(enc, SnakeCaseOptions);

        Assert.Contains("\"risk_type\":\"ZABRANA_OTUDENJA\"", json);
        Assert.Contains("\"legal_basis\":", json);
        Assert.Contains("\"blocks_sale\":true", json);
        Assert.Contains("\"blocks_construction\":true", json);
        Assert.Contains("\"year_registered\":2022", json);
        Assert.Contains("\"is_historical\":false", json);
    }

    // ══════════════════════════════════════════════════════════
    //  V44: OCR 2.0 — HistoricalEncumbrance
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void HistoricalEncumbrance_DefaultValues()
    {
        var h = new HistoricalEncumbrance();

        Assert.Equal(string.Empty, h.Type);
        Assert.Equal(string.Empty, h.OriginalText);
        Assert.Equal(string.Empty, h.Interpretation);
        Assert.Equal(0, h.EstimatedYear);
        Assert.False(h.StillLegallyRelevant);
        Assert.Equal("MOGUĆE", h.BlocksSale);  // default = cautious
        Assert.Equal("NE", h.BlocksConstruction);
        Assert.Equal(string.Empty, h.Resolution);
        Assert.Equal(string.Empty, h.LegalBasis);
    }

    [Fact]
    public void HistoricalEncumbrance_SocialistEraScenario()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "DRUSTVENO_VLASNISTVO",
            OriginalText = "Pravo korištenja temeljem rješenja SO Pakrac br. 123/78",
            Interpretation = "Socijalistička era — pravo korištenja konvertirano u vlasništvo po ZV",
            EstimatedYear = 1978,
            StillLegallyRelevant = false,
            BlocksSale = "NE",
            BlocksConstruction = "NE",
            Resolution = "Konvertirano u pravo vlasništva temeljem čl. 360. ZV",
            LegalBasis = "čl. 360. ZV"
        };

        Assert.Equal("DRUSTVENO_VLASNISTVO", h.Type);
        Assert.Equal(1978, h.EstimatedYear);
        Assert.False(h.StillLegallyRelevant);
        Assert.Equal("NE", h.BlocksSale);
    }

    [Fact]
    public void HistoricalEncumbrance_StillRelevantScenario()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "PRAVO_KORISTENJA",
            OriginalText = "Pravo korištenja — nekonvertirano",
            Interpretation = "Staro pravo korištenja koje nikad nije konvertirano u vlasništvo",
            EstimatedYear = 1985,
            StillLegallyRelevant = true,
            BlocksSale = "MOGUĆE",
            BlocksConstruction = "DA",
            Resolution = "",
            LegalBasis = "čl. 360. ZV"
        };

        Assert.True(h.StillLegallyRelevant);
        Assert.Equal("MOGUĆE", h.BlocksSale);
        Assert.Equal("DA", h.BlocksConstruction);
    }

    [Fact]
    public void HistoricalEncumbrance_SerializesCorrectly()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "JNA_VLASNISTVO",
            OriginalText = "Vojni objekt — JNA",
            EstimatedYear = 1980,
            StillLegallyRelevant = false,
            BlocksSale = "NE"
        };

        var json = JsonSerializer.Serialize(h, SnakeCaseOptions);

        Assert.Contains("\"type\":\"JNA_VLASNISTVO\"", json);
        Assert.Contains("\"original_text\":\"Vojni objekt", json);
        Assert.Contains("\"estimated_year\":1980", json);
        Assert.Contains("\"still_legally_relevant\":false", json);
        Assert.Contains("\"blocks_sale\":\"NE\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  V44: OCR 2.0 — EncumbranceRiskSummary
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EncumbranceRiskSummary_DefaultValues()
    {
        var m = new EncumbranceRiskSummary();

        Assert.Equal(0, m.TotalActive);
        Assert.Equal(0, m.BlockingSale);
        Assert.Equal(0, m.BlockingConstruction);
        Assert.Equal(0, m.FiduciaryCount);
        Assert.Equal(0, m.PersonalServitudeCount);
        Assert.Equal(0, m.HistoricalCount);
        Assert.Equal(0, m.EnforcementCount);
        Assert.Equal("ČISTO", m.OverallRisk);  // default = clean
        Assert.Equal(string.Empty, m.Summary);
    }

    [Fact]
    public void EncumbranceRiskSummary_CleanProperty()
    {
        var m = new EncumbranceRiskSummary
        {
            TotalActive = 0,
            BlockingSale = 0,
            BlockingConstruction = 0,
            OverallRisk = "ČISTO",
            Summary = "Nekretnina nema aktivnih tereta."
        };

        Assert.Equal("ČISTO", m.OverallRisk);
        Assert.Equal(0, m.TotalActive);
    }

    [Fact]
    public void EncumbranceRiskSummary_BlockedProperty()
    {
        var m = new EncumbranceRiskSummary
        {
            TotalActive = 4,
            BlockingSale = 2,
            BlockingConstruction = 1,
            FiduciaryCount = 1,
            PersonalServitudeCount = 1,
            HistoricalCount = 1,
            EnforcementCount = 1,
            OverallRisk = "BLOKIRANO",
            Summary = "Fiducija + ovrha blokiraju prodaju. Osobna služnost i povijesni teret prisutni."
        };

        Assert.Equal("BLOKIRANO", m.OverallRisk);
        Assert.Equal(2, m.BlockingSale);
        Assert.Equal(1, m.FiduciaryCount);
        Assert.Equal(1, m.EnforcementCount);
    }

    [Fact]
    public void EncumbranceRiskSummary_SerializesToSnakeCase()
    {
        var m = new EncumbranceRiskSummary
        {
            TotalActive = 3,
            BlockingSale = 1,
            FiduciaryCount = 1,
            OverallRisk = "UVJETNO",
            Summary = "Jedna fiducija blokira prodaju."
        };

        var json = JsonSerializer.Serialize(m, SnakeCaseOptions);

        Assert.Contains("\"total_active\":3", json);
        Assert.Contains("\"blocking_sale\":1", json);
        Assert.Contains("\"fiduciary_count\":1", json);
        Assert.Contains("\"overall_risk\":\"UVJETNO\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  V44: ZkParsedResult — new fields integration
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_V44_HistoricalEncumbrances_EmptyByDefault()
    {
        var result = new ZkParsedResult();
        Assert.Empty(result.HistoricalEncumbrances);
        Assert.Null(result.EncumbranceRiskMatrix);
    }

    [Fact]
    public void ZkParsedResult_V44_FullScenario_Serialization()
    {
        var result = CreatePropertyWithFiducija();
        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        Assert.Contains("\"historical_encumbrances\"", json);
        Assert.Contains("\"encumbrance_risk_matrix\"", json);
        Assert.Contains("\"risk_type\":\"FIDUCIJA\"", json);
        Assert.Contains("\"blocks_sale\":true", json);
        Assert.Contains("BLOKIRANO", json);
    }

    [Fact]
    public void ZkParsedResult_V44_DeserializesWithNewFields()
    {
        var original = CreatePropertyWithFiducija();
        var json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<ZkParsedResult>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.HistoricalEncumbrances);
        Assert.NotNull(deserialized.EncumbranceRiskMatrix);
        Assert.Equal("BLOKIRANO", deserialized.EncumbranceRiskMatrix!.OverallRisk);
        Assert.Equal(1, deserialized.EncumbranceRiskMatrix.FiduciaryCount);
        Assert.Equal("FIDUCIJA", deserialized.OtherEncumbrances[0].RiskType);
    }

    [Fact]
    public void ZkParsedResult_V44_BackwardCompatible_OldJsonWorks()
    {
        // Old JSON without V44 fields should still deserialize correctly
        var oldJson = """
        {
          "property_id": "k.o. Test, k.č. 1/1",
          "property_type": "kuća",
          "owners": [],
          "active_mortgages": [],
          "active_disputes": [],
          "other_encumbrances": [
            {"type": "služnost", "description": "Prolaz", "beneficiary": "N/A", "is_active": true}
          ],
          "active_plombe": [
            {"z_number": "Z-1/2024", "description": "Test", "date": "01.01.2024."}
          ],
          "findings": [],
          "parcels": [],
          "risk_score": "LOW"
        }
        """;

        var result = JsonSerializer.Deserialize<ZkParsedResult>(oldJson, SnakeCaseOptions);

        Assert.NotNull(result);
        // V44 fields default gracefully
        Assert.Empty(result!.HistoricalEncumbrances);
        Assert.Null(result.EncumbranceRiskMatrix);
        // Old encumbrance/plomba fields still work
        Assert.Single(result.OtherEncumbrances);
        Assert.Equal(string.Empty, result.OtherEncumbrances[0].RiskType);
        Assert.Single(result.ActivePlombe);
        Assert.Equal(string.Empty, result.ActivePlombe[0].PlombaType);
    }

    // ══════════════════════════════════════════════════════════
    //  Test Data Factories — reusable across tests
    // ══════════════════════════════════════════════════════════

    /// <summary>"Čista" nekretnina — 1 vlasnik, bez tereta, bez plombi</summary>
    public static ZkParsedResult CreateCleanProperty() => new()
    {
        PropertyId = "k.o. Trešnjevka, k.č. 1234/1",
        CadastralMunicipality = "Trešnjevka",
        ParcelNumber = "1234/1",
        ZkNumber = "ZK.ul. 500",
        PropertyType = "stan",
        PropertyDescription = "Stan u k.o. Trešnjevka",
        LandAreaM2 = 800,
        BuildingAreaM2 = 55,
        LandUse = "stambena zgrada",
        EtazniUdio = "E-3, 55/800",
        Owners = [new OwnerInfo { Name = "Marko Markić", Share = "1/1", Oib = "11111111111", BasisOfAcquisition = "kupoprodajni ugovor" }],
        RiskScore = "LOW",
        OverallStatus = "ČISTO",
        OwnershipStatus = "1/1 ČISTO",
        SharesSumCorrect = true,
        SharesSumDisplay = "1/1 = 1/1 ✓",
        DueDiligenceSummary = "Vlasništvo čisto, bez tereta, sporova i plombi. Kupnja se može provesti bez dodatnih provjera.",
        LawyerComment = "Čisto vlasništvo.",
        Location = "Trešnjevka, Zagreb"
    };

    /// <summary>Nekretnina s aktivnom plombom — RIZIČNO</summary>
    public static ZkParsedResult CreatePropertyWithPlomba() => new()
    {
        PropertyId = "k.o. Dubrava, k.č. 5555/2",
        PropertyType = "kuća",
        LandAreaM2 = 500,
        Owners = [new OwnerInfo { Name = "Pero Perić", Share = "1/1" }],
        RiskScore = "HIGH",
        OverallStatus = "RIZIČNO",
        OwnershipStatus = "1/1 ČISTO",
        SharesSumCorrect = true,
        ActivePlombe =
        [
            new PlombaInfo
            {
                ZNumber = "Z-1234/2024",
                Description = "Zahtjev za upis prava vlasništva temeljem kupoprodajnog ugovora",
                Date = "15.01.2024."
            }
        ],
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "PLOMBA",
                Severity = "CRITICAL",
                Title = "Aktivna plomba Z-1234/2024",
                Description = "Netko je predao zahtjev za upis prava vlasništva. Postoji rizik nemogućnosti upisa.",
                Advice = "Sačekajte razrješenje plombe prije potpisivanja predugovora."
            }
        ]
    };

    /// <summary>Nekretnina s hipotekom — UVJETNO</summary>
    public static ZkParsedResult CreatePropertyWithMortgage() => new()
    {
        PropertyId = "k.o. Maksimir, k.č. 777/1",
        PropertyType = "stan",
        BuildingAreaM2 = 72,
        Owners = [new OwnerInfo { Name = "Ivan Ivić", Share = "1/1" }],
        RiskScore = "MEDIUM",
        OverallStatus = "UVJETNO",
        OwnershipStatus = "1/1 ČISTO",
        SharesSumCorrect = true,
        ActiveMortgages =
        [
            new MortgageInfo { Amount = "100.000", Currency = "EUR", Year = "2020", Details = "Hipoteka u korist PBZ d.d." }
        ],
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "HIPOTEKA",
                Severity = "WARNING",
                Title = "Aktivna hipoteka 100.000 EUR",
                Description = "Hipoteka u korist PBZ d.d.",
                Advice = "Ishodite brisovnu dozvolu ili pismo namjere banke."
            }
        ]
    };

    /// <summary>Nekretnina s osobnom služnošću — DEAL-BREAKER</summary>
    public static ZkParsedResult CreatePropertyWithDealBreaker() => new()
    {
        PropertyId = "k.o. Črnomerec, k.č. 333/4",
        PropertyType = "kuća",
        LandAreaM2 = 400,
        Owners = [new OwnerInfo { Name = "Ana Anić", Share = "1/1" }],
        RiskScore = "HIGH",
        OverallStatus = "RIZIČNO",
        OtherEncumbrances =
        [
            new EncumbranceInfo
            {
                Type = "osobna služnost",
                Description = "Pravo doživotnog uživanja u korist Marije Marić",
                Beneficiary = "Marija Marić",
                IsActive = true
            }
        ],
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "SLUZNOST",
                Severity = "CRITICAL",
                Title = "Osobna služnost — Pravo doživotnog uživanja",
                Description = "Osobna služnost (pravo doživotnog uživanja) u korist Marije Marić. DEAL-BREAKER.",
                Advice = "Odustanite od kupnje ili zatražite brisanje prije transakcije."
            }
        ]
    };

    /// <summary>Suvlasništvo — 3 vlasnika, ispravni udjeli</summary>
    public static ZkParsedResult CreatePropertyWithSuvlasnistvo() => new()
    {
        PropertyId = "k.o. Stenjevec, k.č. 888/1",
        PropertyType = "kuća",
        Owners =
        [
            new OwnerInfo { Name = "Vlasnik A", Share = "1/4" },
            new OwnerInfo { Name = "Vlasnik B", Share = "1/4" },
            new OwnerInfo { Name = "Vlasnik C", Share = "2/4" }
        ],
        OwnershipStatus = "SUVLASNIŠTVO",
        SharesSumCorrect = true,
        SharesSumDisplay = "1/4 + 1/4 + 2/4 = 4/4 ✓",
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "VLASNISTVO",
                Severity = "WARNING",
                Title = "Suvlasništvo — potrebna suglasnost svih",
                Description = "Nekretnina ima 3 suvlasnika. Svi moraju potpisati ugovor.",
                Advice = "Pribavite punomoći ili organizirajte zajednički potpis."
            }
        ]
    };

    /// <summary>Neispravni vlasniški udjeli — ne zbrajaju se na 1/1</summary>
    public static ZkParsedResult CreatePropertyWithInvalidShares() => new()
    {
        PropertyId = "k.o. Peščenica, k.č. 444/2",
        PropertyType = "stan",
        Owners =
        [
            new OwnerInfo { Name = "Vlasnik X", Share = "1/3" },
            new OwnerInfo { Name = "Vlasnik Y", Share = "1/3" }
        ],
        OwnershipStatus = "NERAZJAŠNJENO",
        SharesSumCorrect = false,
        SharesSumDisplay = "1/3 + 1/3 = 2/3 ≠ 1/1",
        RiskScore = "HIGH",
        OverallStatus = "RIZIČNO",
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "VLASNISTVO",
                Severity = "CRITICAL",
                Title = "Zbroj vlasničkih udjela ≠ 1/1",
                Description = "Zbroj udjela (2/3) ne odgovara cjelini (1/1). Moguća greška ili neriješeno vlasništvo.",
                Advice = "Zatražite uvid u ZK uložak i pojašnjenje od suda."
            }
        ]
    };

    /// <summary>Neslaganje kulture — pašnjak ali tip kuća</summary>
    public static ZkParsedResult CreatePropertyWithLandUseMismatch() => new()
    {
        PropertyId = "k.o. Sesvete, k.č. 999/3",
        PropertyType = "kuća",
        LandUse = "pašnjak",
        LandUseMismatch = "Upozorenje: kultura 'pašnjak' ne odgovara tipu nekretnine 'kuća'",
        HasUsagePermitIssue = true,
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "LEGALIZACIJA",
                Severity = "WARNING",
                Title = "Neslaganje kulture zemljišta",
                Description = "ZK prikazuje pašnjak, ali nekretnina je kuća. Mogući problemi s legalizacijom.",
                Advice = "Provjerite postoji li uporabna dozvola ili rješenje o legalizaciji."
            }
        ]
    };

    /// <summary>Kompleksan slučaj — sve opcije aktivne (za JSON roundtrip test)</summary>
    public static ZkParsedResult CreateFullDueDiligenceResult() => new()
    {
        PropertyId = "k.o. Centar, k.č. 1000/1",
        CadastralMunicipality = "Centar",
        ParcelNumber = "1000/1",
        ZkNumber = "ZK.ul. 42",
        PropertyType = "stan",
        PropertyDescription = "Trosoban stan u centru Zagreba",
        LandAreaM2 = 2000,
        BuildingAreaM2 = 85,
        LandUse = "stambena zgrada",
        EtazniUdio = "E-7, 85/2000",
        Parcels =
        [
            new ParcelInfo { ParcelNumber = "1000/1", LandUse = "stambena zgrada", AreaM2 = 2000 }
        ],
        Owners =
        [
            new OwnerInfo { Name = "Ivan Ivić", Share = "1/2", Oib = "11111111111", BasisOfAcquisition = "nasljeđivanje" },
            new OwnerInfo { Name = "Petra Petrić", Share = "1/2", Oib = "22222222222", BasisOfAcquisition = "nasljeđivanje" }
        ],
        ActiveMortgages =
        [
            new MortgageInfo { Amount = "50.000", Currency = "EUR", Year = "2021", Details = "Hipoteka u korist ZABA d.d." }
        ],
        ActiveDisputes = [],
        OtherEncumbrances =
        [
            new EncumbranceInfo { Type = "služnost prolaza", Description = "Pravo prolaza", Beneficiary = "Grad Zagreb", IsActive = true }
        ],
        ActivePlombe =
        [
            new PlombaInfo { ZNumber = "Z-5678/2025", Description = "Zahtjev za brisanje hipoteke", Date = "01.02.2025." }
        ],
        RiskScore = "HIGH",
        OverallStatus = "RIZIČNO",
        OwnershipStatus = "SUVLASNIŠTVO",
        SharesSumCorrect = true,
        SharesSumDisplay = "1/2 + 1/2 = 2/2 ✓",
        LandUseMismatch = "",
        HasUsagePermitIssue = false,
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "PLOMBA",
                Severity = "CRITICAL",
                Title = "Aktivna plomba Z-5678/2025",
                Description = "Zahtjev za brisanje hipoteke u tijeku.",
                Advice = "Sačekajte razrješenje plombe."
            },
            new DueDiligenceFinding
            {
                Category = "HIPOTEKA",
                Severity = "WARNING",
                Title = "Aktivna hipoteka 50.000 EUR",
                Description = "Hipoteka u korist ZABA d.d.",
                Advice = "Ishodite brisovnu dozvolu."
            }
        ],
        DueDiligenceSummary = "Nekretnina ima aktivnu plombu Z-5678/2025 i hipoteku od 50.000 EUR. Suvlasništvo s dva nasljednika. Preporučam razrješenje plombe i ishoditi brisovnu dozvolu.",
        LawyerComment = "Rizično — plomba + hipoteka + suvlasništvo.",
        Location = "Centar, Zagreb",
        Address = "Ilica 42"
    };

    /// <summary>V44: Nekretnina s fiducijarnim prijenosom i povijesnim teretom</summary>
    public static ZkParsedResult CreatePropertyWithFiducija() => new()
    {
        PropertyId = "k.o. Trnje, k.č. 2222/1",
        PropertyType = "kuća",
        LandAreaM2 = 600,
        Owners = [new OwnerInfo { Name = "Josip Josipović", Share = "1/1" }],
        RiskScore = "HIGH",
        OverallStatus = "RIZIČNO",
        OwnershipStatus = "1/1 ČISTO",
        SharesSumCorrect = true,
        OtherEncumbrances =
        [
            new EncumbranceInfo
            {
                Type = "fiducijarni prijenos",
                Description = "Fiducijarni prijenos vlasništva radi osiguranja tražbine od 200.000 EUR",
                Beneficiary = "Micro Kredit d.o.o.",
                IsActive = true,
                RiskType = "FIDUCIJA",
                LegalBasis = "čl. 34. ZV",
                BlocksSale = true,
                BlocksConstruction = false,
                YearRegistered = 2021,
                IsHistorical = false
            }
        ],
        HistoricalEncumbrances =
        [
            new HistoricalEncumbrance
            {
                Type = "DRUSTVENO_VLASNISTVO",
                OriginalText = "Pravo korištenja — SO Trnje br. 456/82",
                Interpretation = "Socijalističko pravo korištenja konvertirano u vlasništvo",
                EstimatedYear = 1982,
                StillLegallyRelevant = false,
                BlocksSale = "NE",
                BlocksConstruction = "NE",
                Resolution = "Konvertirano temeljem čl. 360. ZV",
                LegalBasis = "čl. 360. ZV"
            }
        ],
        EncumbranceRiskMatrix = new EncumbranceRiskSummary
        {
            TotalActive = 2,
            BlockingSale = 1,
            BlockingConstruction = 0,
            FiduciaryCount = 1,
            PersonalServitudeCount = 0,
            HistoricalCount = 1,
            EnforcementCount = 0,
            OverallRisk = "BLOKIRANO",
            Summary = "Fiducijarni prijenos blokira prodaju. Jedan povijesni teret bez pravne relevantnosti."
        },
        ActivePlombe =
        [
            new PlombaInfo
            {
                ZNumber = "Z-800/2025",
                Description = "Zahtjev za upis fiducije",
                Date = "15.03.2025.",
                PlombaType = "VLASNISTVO",
                RiskLevel = "CRITICAL",
                BlocksSale = true
            }
        ],
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "FIDUCIJA",
                Severity = "CRITICAL",
                Title = "Fiducijarni prijenos vlasništva",
                Description = "Fiducijarni prijenos u korist Micro Kredit d.o.o. — formalno vlasnik nije prodavatelj!",
                Advice = "Bez obavijesti fiducijara i njegova pristanka, kupnja nije moguća. DEAL-BREAKER."
            }
        ],
        DueDiligenceSummary = "Fiducija u korist Micro Kredit d.o.o. blokira prodaju. Povijesni teret iz 1982. konvertiran u vlasništvo."
    };

    /// <summary>V44: Nekretnina s povijesnim teretima iz socijalizma</summary>
    public static ZkParsedResult CreatePropertyWithHistoricalEncumbrances() => new()
    {
        PropertyId = "k.o. Pakrac, k.č. 3333/5",
        PropertyType = "kuća",
        LandAreaM2 = 1200,
        Owners = [new OwnerInfo { Name = "Stipe Stipić", Share = "1/1" }],
        RiskScore = "MEDIUM",
        OverallStatus = "UVJETNO",
        OwnershipStatus = "1/1 ČISTO",
        SharesSumCorrect = true,
        HistoricalEncumbrances =
        [
            new HistoricalEncumbrance
            {
                Type = "DRUSTVENO_VLASNISTVO",
                OriginalText = "Pravo korištenja temeljem rješenja SO Pakrac",
                Interpretation = "Konvertirano u vlasništvo po ZV",
                EstimatedYear = 1978,
                StillLegallyRelevant = false,
                BlocksSale = "NE",
                BlocksConstruction = "NE",
                Resolution = "Konvertirano čl. 360. ZV",
                LegalBasis = "čl. 360. ZV"
            },
            new HistoricalEncumbrance
            {
                Type = "SOUR_VLASNISTVO",
                OriginalText = "SOUR 'Energoinvest' — pravo korištenja",
                Interpretation = "Bivši SOUR — potrebno provjeriti pravni slijed",
                EstimatedYear = 1985,
                StillLegallyRelevant = true,
                BlocksSale = "MOGUĆE",
                BlocksConstruction = "NE",
                Resolution = "",
                LegalBasis = "čl. 360.-366. ZV"
            }
        ],
        EncumbranceRiskMatrix = new EncumbranceRiskSummary
        {
            TotalActive = 2,
            BlockingSale = 0,
            BlockingConstruction = 0,
            FiduciaryCount = 0,
            PersonalServitudeCount = 0,
            HistoricalCount = 2,
            EnforcementCount = 0,
            OverallRisk = "UVJETNO",
            Summary = "Dva povijesna upisa. Jedan konvertiran, drugi zahtijeva provjeru pravnog slijeda."
        },
        Findings =
        [
            new DueDiligenceFinding
            {
                Category = "OSTALO",
                Severity = "WARNING",
                Title = "Nekonvertirano pravo korištenja (SOUR)",
                Description = "Pravo korištenja iz 1985. vezano uz SOUR 'Energoinvest' nije konvertirano.",
                Advice = "Provjerite pravni slijed i pokrenite postupak konverzije ako je primjenjivo."
            }
        ],
        DueDiligenceSummary = "Dva povijesna tereta iz socijalizma. Jedan razriješen, drugi zahtijeva provjeru."
    };
}
