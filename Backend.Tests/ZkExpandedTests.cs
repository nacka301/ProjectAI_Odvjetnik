using System.Text.Json;
using Backend.Models;

namespace Backend.Tests;

/// <summary>
/// Expanded ZK (Zemljišna Knjiga) tests covering models not tested in ZkAnalysisModelsTests:
/// - KatastarComparison — cadastre vs land registry discrepancy detection
/// - KatastarParcelDiscrepancy — individual parcel-level mismatches
/// - HistoricalEncumbrance — pre-1991 and legacy burden analysis
/// - UnifiedZkValuationResult — combined ZK + valuation pipeline
/// - Additional EncumbranceRiskSummary edge cases
/// - ZkParsedResult field coverage and serialization
/// </summary>
public class ZkExpandedTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // ══════════════════════════════════════════════════════════
    //  § 1 — KatastarComparison Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void KatastarComparison_DefaultValues()
    {
        var kc = new KatastarComparison();

        Assert.Equal("USKLAĐENO", kc.Status); // default
        Assert.Equal(0, kc.AreaDifferenceM2);
        Assert.Equal(0, kc.AreaDifferencePercent);
        Assert.Equal(0, kc.KatastarAreaM2);
        Assert.Equal(0, kc.ZkAreaM2);
        Assert.Empty(kc.ParcelDiscrepancies);
        Assert.Equal("", kc.Summary);
        Assert.Equal(0, kc.DiscrepancyCount);
        Assert.False(kc.SurveyRecommended);
    }

    // ══════════════════════════════════════════════════════════
    //  § 2 — KatastarComparison: Perfect Match
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void KatastarComparison_PerfectMatch()
    {
        var kc = new KatastarComparison
        {
            Status = "USKLAĐENO",
            KatastarAreaM2 = 500,
            ZkAreaM2 = 500,
            AreaDifferenceM2 = 0,
            AreaDifferencePercent = 0,
            DiscrepancyCount = 0,
            SurveyRecommended = false,
            Summary = "Katastar i ZK potpuno usklađeni"
        };

        Assert.Equal("USKLAĐENO", kc.Status);
        Assert.Equal(0, kc.AreaDifferenceM2);
        Assert.False(kc.SurveyRecommended);
    }

    // ══════════════════════════════════════════════════════════
    //  § 3 — KatastarComparison: Area Mismatch
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void KatastarComparison_AreaMismatch_LargeDiscrepancy()
    {
        var kc = new KatastarComparison
        {
            Status = "NESKLAD",
            KatastarAreaM2 = 650,
            ZkAreaM2 = 500,
            AreaDifferenceM2 = 150,
            AreaDifferencePercent = 30.0,
            DiscrepancyCount = 1,
            SurveyRecommended = true,
            Summary = "Razlika površina: katastar 650 m², ZK 500 m² (30%). Preporuka: geodetski elaborat.",
            ParcelDiscrepancies =
            [
                new KatastarParcelDiscrepancy
                {
                    Type = "AREA_MISMATCH",
                    ZkParcelNumber = "1234/5",
                    KatastarParcelNumber = "1234/5",
                    ZkAreaM2 = 500,
                    KatastarAreaM2 = 650,
                    Severity = "HIGH",
                    Description = "Razlika od 150 m² (30%)"
                }
            ]
        };

        Assert.Equal("NESKLAD", kc.Status);
        Assert.Equal(150, kc.AreaDifferenceM2);
        Assert.Equal(30.0, kc.AreaDifferencePercent);
        Assert.True(kc.SurveyRecommended);
        Assert.Single(kc.ParcelDiscrepancies);
        Assert.Equal("AREA_MISMATCH", kc.ParcelDiscrepancies[0].Type);
    }

    [Fact]
    public void KatastarComparison_SmallDifference_PartialMatch()
    {
        var kc = new KatastarComparison
        {
            Status = "DJELOMIČNO",
            KatastarAreaM2 = 510,
            ZkAreaM2 = 500,
            AreaDifferenceM2 = 10,
            AreaDifferencePercent = 2.0,
            DiscrepancyCount = 1,
            SurveyRecommended = false,
            Summary = "Manja razlika od 10 m² (2%) — u okviru tolerancije"
        };

        Assert.Equal("DJELOMIČNO", kc.Status);
        Assert.False(kc.SurveyRecommended);
    }

    // ══════════════════════════════════════════════════════════
    //  § 4 — KatastarParcelDiscrepancy Types
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("AREA_MISMATCH")]
    [InlineData("PARCEL_MISSING")]
    [InlineData("LAND_USE_MISMATCH")]
    [InlineData("PARCEL_NUMBER_MISMATCH")]
    public void KatastarParcelDiscrepancy_ValidTypes(string type)
    {
        var d = new KatastarParcelDiscrepancy { Type = type };
        Assert.Equal(type, d.Type);
    }

    [Fact]
    public void KatastarParcelDiscrepancy_DefaultValues()
    {
        var d = new KatastarParcelDiscrepancy();

        Assert.Equal("", d.Type);
        Assert.Equal("", d.ZkParcelNumber);
        Assert.Equal("", d.KatastarParcelNumber);
        Assert.Equal(0, d.ZkAreaM2);
        Assert.Equal(0, d.KatastarAreaM2);
        Assert.Equal("", d.ZkLandUse);
        Assert.Equal("", d.KatastarLandUse);
        Assert.Equal("", d.Description);
        Assert.Equal("MEDIUM", d.Severity); // default
    }

    [Fact]
    public void KatastarParcelDiscrepancy_LandUseMismatch()
    {
        var d = new KatastarParcelDiscrepancy
        {
            Type = "LAND_USE_MISMATCH",
            ZkParcelNumber = "567/8",
            ZkLandUse = "oranica",
            KatastarLandUse = "dvorište",
            Severity = "HIGH",
            Description = "ZK navodi oranicu, katastar dvorište — potrebna uskladba"
        };

        Assert.Equal("LAND_USE_MISMATCH", d.Type);
        Assert.Equal("oranica", d.ZkLandUse);
        Assert.Equal("dvorište", d.KatastarLandUse);
    }

    [Fact]
    public void KatastarParcelDiscrepancy_MissingParcel()
    {
        var d = new KatastarParcelDiscrepancy
        {
            Type = "PARCEL_MISSING",
            ZkParcelNumber = "890/1",
            KatastarParcelNumber = "",
            Severity = "HIGH",
            Description = "Čestica 890/1 postoji u ZK ali ne u katastru"
        };

        Assert.Equal("PARCEL_MISSING", d.Type);
        Assert.Equal("", d.KatastarParcelNumber);
    }

    // ══════════════════════════════════════════════════════════
    //  § 5 — KatastarComparison Serialization
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void KatastarComparison_Serialization_SnakeCase()
    {
        var kc = new KatastarComparison
        {
            Status = "NESKLAD",
            AreaDifferenceM2 = 100,
            AreaDifferencePercent = 20,
            KatastarAreaM2 = 600,
            ZkAreaM2 = 500,
            SurveyRecommended = true,
            DiscrepancyCount = 2
        };

        var json = JsonSerializer.Serialize(kc, JsonOptions);

        Assert.Contains("\"area_difference_m2\"", json);
        Assert.Contains("\"area_difference_percent\"", json);
        Assert.Contains("\"katastar_area_m2\"", json);
        Assert.Contains("\"zk_area_m2\"", json);
        Assert.Contains("\"survey_recommended\"", json);
        Assert.Contains("\"discrepancy_count\"", json);
        Assert.Contains("\"parcel_discrepancies\"", json);
    }

    [Fact]
    public void KatastarComparison_Roundtrip()
    {
        var original = new KatastarComparison
        {
            Status = "NESKLAD",
            AreaDifferenceM2 = 75.5,
            AreaDifferencePercent = 15.1,
            KatastarAreaM2 = 575.5,
            ZkAreaM2 = 500.0,
            DiscrepancyCount = 1,
            SurveyRecommended = true,
            Summary = "Razlika 75.5 m²",
            ParcelDiscrepancies =
            [
                new KatastarParcelDiscrepancy
                {
                    Type = "AREA_MISMATCH",
                    ZkParcelNumber = "1234",
                    KatastarParcelNumber = "1234",
                    ZkAreaM2 = 500,
                    KatastarAreaM2 = 575.5,
                    Severity = "HIGH"
                }
            ]
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var d = JsonSerializer.Deserialize<KatastarComparison>(json, JsonOptions)!;

        Assert.Equal("NESKLAD", d.Status);
        Assert.Equal(75.5, d.AreaDifferenceM2);
        Assert.Equal(15.1, d.AreaDifferencePercent);
        Assert.Equal(575.5, d.KatastarAreaM2);
        Assert.Equal(500.0, d.ZkAreaM2);
        Assert.True(d.SurveyRecommended);
        Assert.Single(d.ParcelDiscrepancies);
        Assert.Equal("AREA_MISMATCH", d.ParcelDiscrepancies[0].Type);
    }

    // ══════════════════════════════════════════════════════════
    //  § 6 — HistoricalEncumbrance Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void HistoricalEncumbrance_DefaultValues()
    {
        var h = new HistoricalEncumbrance();

        Assert.Equal("", h.Type);
        Assert.Equal("", h.OriginalText);
        Assert.Equal("", h.Interpretation);
        Assert.Equal(0, h.EstimatedYear);
        Assert.False(h.StillLegallyRelevant);
        Assert.Equal("MOGUĆE", h.BlocksSale); // default
        Assert.Equal("NE", h.BlocksConstruction); // default
        Assert.Equal("", h.Resolution);
        Assert.Equal("", h.LegalBasis);
    }

    // ══════════════════════════════════════════════════════════
    //  § 7 — HistoricalEncumbrance Types
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("FIDUCIJA")]
    [InlineData("STARI_TERET")]
    [InlineData("SOCIJALIZAM")]
    [InlineData("NEIDENTIFICIRANO")]
    public void HistoricalEncumbrance_ValidTypes(string type)
    {
        var h = new HistoricalEncumbrance { Type = type };
        Assert.Equal(type, h.Type);
    }

    [Fact]
    public void HistoricalEncumbrance_FiducijaScenarij()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "FIDUCIJA",
            OriginalText = "Fiducijarna prisvojba u korist XY banke d.d., Z-1234/1998",
            Interpretation = "Stara fiducijarna hipoteka — banka je prestala postojati",
            EstimatedYear = 1998,
            StillLegallyRelevant = true,
            BlocksSale = "DA",
            BlocksConstruction = "NE",
            Resolution = "Pokrenuti postupak brisanja na temelju čl. 373. ZV",
            LegalBasis = "čl. 373. Zakona o vlasništvu (NN 91/96)"
        };

        Assert.Equal("FIDUCIJA", h.Type);
        Assert.Equal(1998, h.EstimatedYear);
        Assert.True(h.StillLegallyRelevant);
        Assert.Equal("DA", h.BlocksSale);
    }

    [Fact]
    public void HistoricalEncumbrance_SocijalizmScenarij()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "SOCIJALIZAM",
            OriginalText = "Pravo korištenja u korist SOUR 'Napredak' Zagreb",
            Interpretation = "Socijalističko pravo korištenja — pravni sljednik nepoznat",
            EstimatedYear = 1975,
            StillLegallyRelevant = false,
            BlocksSale = "MOGUĆE",
            Resolution = "Provjeriti pravnog sljednika SOUR-a. Ako ne postoji, pokrenuti brisanje."
        };

        Assert.Equal("SOCIJALIZAM", h.Type);
        Assert.Equal(1975, h.EstimatedYear);
        Assert.False(h.StillLegallyRelevant);
    }

    // ══════════════════════════════════════════════════════════
    //  § 8 — HistoricalEncumbrance Serialization
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void HistoricalEncumbrance_Serialization_SnakeCase()
    {
        var h = new HistoricalEncumbrance
        {
            Type = "STARI_TERET",
            EstimatedYear = 1989,
            StillLegallyRelevant = true,
            BlocksSale = "DA"
        };

        var json = JsonSerializer.Serialize(h, JsonOptions);

        Assert.Contains("\"original_text\"", json);
        Assert.Contains("\"estimated_year\"", json);
        Assert.Contains("\"still_legally_relevant\"", json);
        Assert.Contains("\"blocks_sale\"", json);
        Assert.Contains("\"blocks_construction\"", json);
        Assert.Contains("\"legal_basis\"", json);
    }

    [Fact]
    public void HistoricalEncumbrance_Roundtrip()
    {
        var original = new HistoricalEncumbrance
        {
            Type = "FIDUCIJA",
            OriginalText = "Fiducija Z-999/1995",
            Interpretation = "Stara hipoteka",
            EstimatedYear = 1995,
            StillLegallyRelevant = true,
            BlocksSale = "DA",
            BlocksConstruction = "MOGUĆE",
            Resolution = "Brisanje temeljem čl. 373. ZV",
            LegalBasis = "ZV čl. 373."
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var d = JsonSerializer.Deserialize<HistoricalEncumbrance>(json, JsonOptions)!;

        Assert.Equal("FIDUCIJA", d.Type);
        Assert.Equal(1995, d.EstimatedYear);
        Assert.True(d.StillLegallyRelevant);
        Assert.Equal("DA", d.BlocksSale);
        Assert.Equal("MOGUĆE", d.BlocksConstruction);
    }

    // ══════════════════════════════════════════════════════════
    //  § 9 — EncumbranceRiskSummary: Extended Edge Cases
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EncumbranceRiskSummary_FullyBlocked()
    {
        var m = new EncumbranceRiskSummary
        {
            TotalActive = 5,
            BlockingSale = 3,
            BlockingConstruction = 2,
            FiduciaryCount = 1,
            PersonalServitudeCount = 1,
            HistoricalCount = 2,
            EnforcementCount = 1,
            OverallRisk = "BLOKIRANO",
            Summary = "Nekretnina blokirana — 3 tereta sprečavaju prodaju, 1 ovrha u tijeku"
        };

        Assert.Equal(5, m.TotalActive);
        Assert.Equal(3, m.BlockingSale);
        Assert.Equal("BLOKIRANO", m.OverallRisk);
        Assert.Contains("ovrha", m.Summary);
    }

    [Fact]
    public void EncumbranceRiskSummary_ConditionalSale()
    {
        var m = new EncumbranceRiskSummary
        {
            TotalActive = 2,
            BlockingSale = 0,
            BlockingConstruction = 0,
            FiduciaryCount = 0,
            PersonalServitudeCount = 0,
            HistoricalCount = 2,
            EnforcementCount = 0,
            OverallRisk = "UVJETNO",
            Summary = "2 povijesna tereta — prodaja moguća uz prethodnu provjeru"
        };

        Assert.Equal("UVJETNO", m.OverallRisk);
        Assert.Equal(2, m.HistoricalCount);
        Assert.Equal(0, m.BlockingSale);
    }

    // ══════════════════════════════════════════════════════════
    //  § 10 — UnifiedZkValuationResult
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void UnifiedZkValuationResult_DefaultValues()
    {
        var r = new UnifiedZkValuationResult();

        Assert.NotNull(r.ZkAnalysis);
        Assert.Null(r.Valuation);
        Assert.False(r.ValuationAttempted);
        Assert.Null(r.ValuationSkipReason);
    }

    [Fact]
    public void UnifiedZkValuationResult_ValuationSkipped()
    {
        var r = new UnifiedZkValuationResult
        {
            ZkAnalysis = new ZkParsedResult
            {
                PropertyType = "zemljište",
                PropertyId = "k.o. Trnje, k.č. 1234",
                LandAreaM2 = 2000,
                RiskScore = "LOW"
            },
            ValuationAttempted = false,
            ValuationSkipReason = "Površina zgrade nepoznata"
        };

        Assert.False(r.ValuationAttempted);
        Assert.Equal("Površina zgrade nepoznata", r.ValuationSkipReason);
        Assert.Null(r.Valuation);
    }

    [Fact]
    public void UnifiedZkValuationResult_Serialization_SnakeCase()
    {
        var r = new UnifiedZkValuationResult
        {
            ValuationAttempted = true,
            ValuationSkipReason = null
        };

        var json = JsonSerializer.Serialize(r, JsonOptions);

        Assert.Contains("\"zk_analysis\"", json);
        Assert.Contains("\"valuation_attempted\"", json);
        Assert.Contains("\"valuation_skip_reason\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  § 11 — KatastarComparison: Multiple Discrepancies
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void KatastarComparison_MultipleDiscrepancies()
    {
        var kc = new KatastarComparison
        {
            Status = "NESKLAD",
            DiscrepancyCount = 3,
            SurveyRecommended = true,
            ParcelDiscrepancies =
            [
                new KatastarParcelDiscrepancy { Type = "AREA_MISMATCH", Severity = "HIGH" },
                new KatastarParcelDiscrepancy { Type = "LAND_USE_MISMATCH", Severity = "MEDIUM" },
                new KatastarParcelDiscrepancy { Type = "PARCEL_MISSING", Severity = "HIGH" }
            ]
        };

        Assert.Equal(3, kc.ParcelDiscrepancies.Count);
        Assert.Equal(2, kc.ParcelDiscrepancies.Count(d => d.Severity == "HIGH"));
        Assert.Equal(1, kc.ParcelDiscrepancies.Count(d => d.Type == "LAND_USE_MISMATCH"));
    }

    // ══════════════════════════════════════════════════════════
    //  § 12 — ZkParsedResult with KatastarComparison
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_WithKatastarComparison()
    {
        var result = new ZkParsedResult
        {
            PropertyId = "k.o. Dubrava, k.č. 5678/3",
            CadastralMunicipality = "Dubrava",
            ParcelNumber = "5678/3",
            PropertyType = "kuća",
            LandAreaM2 = 800,
            KatastarUskladjenost = new KatastarComparison
            {
                Status = "DJELOMIČNO",
                KatastarAreaM2 = 820,
                ZkAreaM2 = 800,
                AreaDifferenceM2 = 20,
                AreaDifferencePercent = 2.5
            }
        };

        Assert.NotNull(result.KatastarUskladjenost);
        Assert.Equal("DJELOMIČNO", result.KatastarUskladjenost.Status);
        Assert.Equal(20, result.KatastarUskladjenost.AreaDifferenceM2);
    }

    [Fact]
    public void ZkParsedResult_WithHistoricalEncumbrances()
    {
        var result = new ZkParsedResult
        {
            PropertyId = "k.o. Maksimir, k.č. 999",
            HistoricalEncumbrances =
            [
                new HistoricalEncumbrance { Type = "FIDUCIJA", EstimatedYear = 1997, BlocksSale = "DA" },
                new HistoricalEncumbrance { Type = "SOCIJALIZAM", EstimatedYear = 1980, BlocksSale = "MOGUĆE" }
            ],
            EncumbranceRiskMatrix = new EncumbranceRiskSummary
            {
                TotalActive = 2,
                FiduciaryCount = 1,
                HistoricalCount = 1,
                OverallRisk = "UVJETNO"
            }
        };

        Assert.Equal(2, result.HistoricalEncumbrances.Count);
        Assert.NotNull(result.EncumbranceRiskMatrix);
        Assert.Equal("UVJETNO", result.EncumbranceRiskMatrix.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 13 — ZkParsedResult: All Property Types
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("stan")]
    [InlineData("kuća")]
    [InlineData("zemljište")]
    [InlineData("poslovni_prostor")]
    public void ZkParsedResult_PropertyType_Valid(string type)
    {
        var r = new ZkParsedResult { PropertyType = type };
        Assert.Equal(type, r.PropertyType);
    }

    [Fact]
    public void ZkParsedResult_UnknownPropertyType_AcceptsAnyString()
    {
        var r = new ZkParsedResult { PropertyType = "garaža" };
        Assert.Equal("garaža", r.PropertyType);
    }

    // ══════════════════════════════════════════════════════════
    //  § 14 — ZkParsedResult: OverallStatus Values
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("ČISTO")]
    [InlineData("UVJETNO")]
    [InlineData("RIZIČNO")]
    public void ZkParsedResult_OverallStatus_Valid(string status)
    {
        var r = new ZkParsedResult { OverallStatus = status };
        Assert.Equal(status, r.OverallStatus);
    }

    // ══════════════════════════════════════════════════════════
    //  § 15 — ZkParsedResult: Ownership Shares
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_OwnershipShares_Correct()
    {
        var r = new ZkParsedResult
        {
            OwnershipStatus = "SUVLASNIŠTVO",
            SharesSumCorrect = true,
            SharesSumDisplay = "1/4 + 1/4 + 2/4 = 4/4 ✓"
        };

        Assert.True(r.SharesSumCorrect);
        Assert.Contains("✓", r.SharesSumDisplay);
    }

    [Fact]
    public void ZkParsedResult_OwnershipShares_Incorrect()
    {
        var r = new ZkParsedResult
        {
            OwnershipStatus = "NERAZJAŠNJENO",
            SharesSumCorrect = false,
            SharesSumDisplay = "1/3 + 1/3 = 2/3 ✗"
        };

        Assert.False(r.SharesSumCorrect);
        Assert.Contains("✗", r.SharesSumDisplay);
    }

    // ══════════════════════════════════════════════════════════
    //  § 16 — Realistic Scenario: Clean Property
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_CleanProperty_ReadyForSale()
    {
        var result = new ZkParsedResult
        {
            PropertyId = "k.o. Trešnjevka, k.č. 2345/1",
            CadastralMunicipality = "Trešnjevka",
            ParcelNumber = "2345/1",
            ZkNumber = "ZK.ul. 789",
            PropertyType = "stan",
            LandAreaM2 = 1200,
            BuildingAreaM2 = 65,
            EtazniUdio = "E-5, 12/100",
            LandUse = "kuća i dvor",
            Owners = [new OwnerInfo { Name = "Ana Kovačić", Share = "1/1" }],
            ActiveMortgages = [],
            ActiveDisputes = [],
            OtherEncumbrances = [],
            ActivePlombe = [],
            RiskScore = "LOW",
            OverallStatus = "ČISTO",
            OwnershipStatus = "1/1 ČISTO",
            SharesSumCorrect = true,
            HasUsagePermitIssue = false,
            KatastarUskladjenost = new KatastarComparison { Status = "USKLAĐENO" },
            EncumbranceRiskMatrix = new EncumbranceRiskSummary { OverallRisk = "ČISTO", TotalActive = 0 }
        };

        Assert.Equal("LOW", result.RiskScore);
        Assert.Equal("ČISTO", result.OverallStatus);
        Assert.Empty(result.ActiveMortgages);
        Assert.Empty(result.ActivePlombe);
        Assert.Equal("ČISTO", result.EncumbranceRiskMatrix!.OverallRisk);
        Assert.Equal("USKLAĐENO", result.KatastarUskladjenost!.Status);
    }

    // ══════════════════════════════════════════════════════════
    //  § 17 — Realistic Scenario: Risky Property
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_RiskyProperty_MultipleIssues()
    {
        var result = new ZkParsedResult
        {
            PropertyId = "k.o. Podsused, k.č. 789/2",
            PropertyType = "kuća",
            RiskScore = "HIGH",
            OverallStatus = "RIZIČNO",
            OwnershipStatus = "SUVLASNIŠTVO",
            SharesSumCorrect = false,
            HasUsagePermitIssue = true,
            LandUseMismatch = "ZK navodi 'oranica', katastar navodi 'dvorište'",

            ActiveMortgages =
            [
                new MortgageInfo
                {
                    Amount = "100.000,00",
                    Currency = "EUR",
                    Year = "2020",
                    Details = "Hipoteka u korist XY Banke d.d."
                }
            ],

            ActivePlombe =
            [
                new PlombaInfo
                {
                    ZNumber = "Z-9999/2024",
                    Description = "Prijedlog za upis hipoteke",
                    BlocksSale = true
                }
            ],

            HistoricalEncumbrances =
            [
                new HistoricalEncumbrance { Type = "FIDUCIJA", EstimatedYear = 1999, BlocksSale = "DA" }
            ],

            KatastarUskladjenost = new KatastarComparison
            {
                Status = "NESKLAD",
                AreaDifferencePercent = 25,
                SurveyRecommended = true
            }
        };

        Assert.Equal("HIGH", result.RiskScore);
        Assert.Equal("RIZIČNO", result.OverallStatus);
        Assert.False(result.SharesSumCorrect);
        Assert.True(result.HasUsagePermitIssue);
        Assert.NotEmpty(result.LandUseMismatch);
        Assert.Single(result.ActiveMortgages);
        Assert.Single(result.ActivePlombe);
        Assert.True(result.ActivePlombe[0].BlocksSale);
        Assert.Single(result.HistoricalEncumbrances);
        Assert.Equal("NESKLAD", result.KatastarUskladjenost!.Status);
        Assert.True(result.KatastarUskladjenost.SurveyRecommended);
    }

    // ══════════════════════════════════════════════════════════
    //  § 18 — ZkParsedResult Full Serialization Roundtrip
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZkParsedResult_FullSerialization_Roundtrip()
    {
        var original = new ZkParsedResult
        {
            PropertyId = "k.o. Test, k.č. 1/1",
            CadastralMunicipality = "Test",
            ParcelNumber = "1/1",
            ZkNumber = "ZK.ul. 1",
            PropertyType = "stan",
            PropertyDescription = "Stan u prizemlju",
            LandAreaM2 = 500,
            BuildingAreaM2 = 75,
            LandUse = "kuća i dvor",
            EtazniUdio = "E-1, 1/4",
            RiskScore = "LOW",
            OverallStatus = "ČISTO",
            OwnershipStatus = "1/1 ČISTO",
            SharesSumCorrect = true,
            DueDiligenceSummary = "Nekretnina čista."
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var d = JsonSerializer.Deserialize<ZkParsedResult>(json, JsonOptions)!;

        Assert.Equal("k.o. Test, k.č. 1/1", d.PropertyId);
        Assert.Equal("Test", d.CadastralMunicipality);
        Assert.Equal("stan", d.PropertyType);
        Assert.Equal(75, d.BuildingAreaM2);
        Assert.Equal("E-1, 1/4", d.EtazniUdio);
        Assert.Equal("ČISTO", d.OverallStatus);
        Assert.True(d.SharesSumCorrect);
    }
}
