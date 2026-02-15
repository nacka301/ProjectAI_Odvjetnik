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
}
