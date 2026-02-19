using System.Text.Json;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Comprehensive tests for the Contract Analysis module:
/// - ContractAnalysisResult model and all sub-models
/// - ComputeRiskScore deterministic engine (exact point values)
/// - Risk level classification (Visok/Srednji/Nizak)
/// - Score clamping (0-100)
/// - Risk breakdown tracking
/// - All model serialization (snake_case)
/// - Realistic Croatian contract scenarios
/// </summary>
public class ContractAnalysisTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // ══════════════════════════════════════════════════════════
    //  § 1 — ContractAnalysisResult Model Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ContractAnalysisResult_DefaultValues()
    {
        var r = new ContractAnalysisResult();

        Assert.Equal("", r.DocumentType);
        Assert.Equal("", r.Summary);
        Assert.Empty(r.RedFlags);
        Assert.Empty(r.Warnings);
        Assert.Empty(r.Positives);
        Assert.Empty(r.SuggestedAmendments);
        Assert.Equal("", r.OverallRisk);
        Assert.Equal(0, r.RiskScore);
        Assert.Empty(r.Checklist);
        Assert.Empty(r.CrossArticleIssues);
        Assert.Empty(r.MandatoryClauses);
        Assert.Empty(r.RiskScoreBreakdown);
        Assert.Empty(r.LogicalTraps);
        Assert.Empty(r.IdealContractDeviations);
        Assert.Empty(r.CitedNorms);
        Assert.Equal("", r.Disclaimer);
    }

    // ══════════════════════════════════════════════════════════
    //  § 2 — Sub-Model Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LogicalTrap_DefaultValues()
    {
        var trap = new LogicalTrap();
        Assert.Equal("", trap.Title);
        Assert.Empty(trap.Articles);
        Assert.Equal("", trap.QuotedText);
        Assert.Equal("", trap.Explanation);
        Assert.Equal("", trap.LegalBasis);
        Assert.Equal("VISOK", trap.Severity); // default
    }

    [Fact]
    public void IdealContractDeviation_DefaultValues()
    {
        var dev = new IdealContractDeviation();
        Assert.Equal("", dev.Element);
        Assert.Equal("", dev.IdealText);
        Assert.Equal("", dev.ActualText);
        Assert.Equal("SREDNJI", dev.Severity); // default
        Assert.Equal("", dev.Recommendation);
    }

    [Fact]
    public void ContractIssue_DefaultValues()
    {
        var issue = new ContractIssue();
        Assert.Equal("", issue.Title);
        Assert.Equal("", issue.Description);
        Assert.Null(issue.QuotedText);
        Assert.Null(issue.LegalBasis);
    }

    [Fact]
    public void ChecklistItem_DefaultValues()
    {
        var item = new ChecklistItem();
        Assert.Equal("", item.Category);
        Assert.Equal("", item.Check);
        Assert.Equal("", item.Status);
        Assert.Equal("", item.Risk);
        Assert.Equal("", item.Finding);
        Assert.Null(item.ArticleRef);
        Assert.Null(item.Recommendation);
    }

    [Fact]
    public void CrossArticleIssue_DefaultValues()
    {
        var issue = new CrossArticleIssue();
        Assert.Equal("", issue.Title);
        Assert.Empty(issue.LinkedArticles);
        Assert.Equal("", issue.Description);
        Assert.Equal("", issue.Risk);
    }

    [Fact]
    public void MandatoryClauseCheck_DefaultValues()
    {
        var mc = new MandatoryClauseCheck();
        Assert.Equal("", mc.Clause);
        Assert.False(mc.Present);
        Assert.Equal("PREPORUČENO", mc.Importance); // default
        Assert.Equal("", mc.Description);
    }

    [Fact]
    public void SuggestedAmendment_DefaultValues()
    {
        var a = new SuggestedAmendment();
        Assert.Equal("", a.ArticleReference);
        Assert.Equal("", a.CurrentText);
        Assert.Equal("", a.ProposedText);
        Assert.Equal("", a.Rationale);
    }

    // ══════════════════════════════════════════════════════════
    //  § 3 — ComputeRiskScore: Empty Result = 0 / Nizak
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_EmptyResult_ZeroScore_Nizak()
    {
        var result = new ContractAnalysisResult();
        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(0, result.RiskScore);
        Assert.Equal("Nizak", result.OverallRisk);
        Assert.NotNull(result.RiskScoreBreakdown);
    }

    // ══════════════════════════════════════════════════════════
    //  § 4 — ComputeRiskScore: Checklist Points
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_ChecklistVisok_Plus15()
    {
        var result = new ContractAnalysisResult
        {
            Checklist =
            [
                new ChecklistItem { Check = "Tereti i hipoteke", Risk = "VISOK" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(15, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_ChecklistSrednji_Plus7()
    {
        var result = new ContractAnalysisResult
        {
            Checklist =
            [
                new ChecklistItem { Check = "Kapara", Risk = "SREDNJI" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(7, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_ChecklistNizak_NoPoints()
    {
        var result = new ContractAnalysisResult
        {
            Checklist =
            [
                new ChecklistItem { Check = "Identifikacija stranaka", Risk = "NIZAK" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(0, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_MultipleChecklistItems()
    {
        var result = new ContractAnalysisResult
        {
            Checklist =
            [
                new ChecklistItem { Check = "A", Risk = "VISOK" },   // +15
                new ChecklistItem { Check = "B", Risk = "SREDNJI" }, // +7
                new ChecklistItem { Check = "C", Risk = "VISOK" },   // +15
                new ChecklistItem { Check = "D", Risk = "NIZAK" }    // +0
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(37, result.RiskScore); // 15+7+15 = 37
        Assert.Equal("Srednji", result.OverallRisk); // 30-59 = Srednji
    }

    // ══════════════════════════════════════════════════════════
    //  § 5 — ComputeRiskScore: Mandatory Clauses
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_MandatoryKriticno_Missing_Plus15()
    {
        var result = new ContractAnalysisResult
        {
            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Clausula intabulandi", Present = false, Importance = "KRITIČNO" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(15, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_MandatoryPreporuceno_Missing_Plus5()
    {
        var result = new ContractAnalysisResult
        {
            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Escrow", Present = false, Importance = "PREPORUČENO" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(5, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_MandatoryPresent_NoPoints()
    {
        var result = new ContractAnalysisResult
        {
            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Clausula intabulandi", Present = true, Importance = "KRITIČNO" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(0, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 6 — ComputeRiskScore: Cross-Article Issues
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_CrossArticleVisok_Plus12()
    {
        var result = new ContractAnalysisResult
        {
            CrossArticleIssues =
            [
                new CrossArticleIssue { Title = "Rok plaćanja vs. predaja", Risk = "VISOK" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(12, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_CrossArticleOther_Plus5()
    {
        var result = new ContractAnalysisResult
        {
            CrossArticleIssues =
            [
                new CrossArticleIssue { Title = "Neusklađenost", Risk = "SREDNJI" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(5, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 7 — ComputeRiskScore: Red Flags (+10 each)
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(5, 50)]
    public void ComputeRiskScore_RedFlags_10PerFlag(int count, int expectedScore)
    {
        var result = new ContractAnalysisResult();
        for (int i = 0; i < count; i++)
            result.RedFlags.Add(new ContractIssue { Title = $"Flag {i}" });

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(expectedScore, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 8 — ComputeRiskScore: Warnings (+3 each)
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 9)]
    [InlineData(10, 30)]
    public void ComputeRiskScore_Warnings_3PerWarning(int count, int expectedScore)
    {
        var result = new ContractAnalysisResult();
        for (int i = 0; i < count; i++)
            result.Warnings.Add(new ContractIssue { Title = $"Warning {i}" });

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(expectedScore, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 9 — ComputeRiskScore: Logical Traps (o1-preview)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_LogicalTrap_Kriticno_Plus18()
    {
        var result = new ContractAnalysisResult
        {
            LogicalTraps =
            [
                new LogicalTrap { Title = "Zamka", Severity = "KRITIČNO" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(18, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_LogicalTrap_Visok_Plus12()
    {
        var result = new ContractAnalysisResult
        {
            LogicalTraps =
            [
                new LogicalTrap { Title = "Zamka", Severity = "VISOK" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(12, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_LogicalTrap_Other_Plus6()
    {
        var result = new ContractAnalysisResult
        {
            LogicalTraps =
            [
                new LogicalTrap { Title = "Zamka", Severity = "SREDNJI" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(6, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 10 — ComputeRiskScore: Ideal Contract Deviations
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("KRITIČNO", 10)]
    [InlineData("VISOK", 6)]
    [InlineData("SREDNJI", 3)]
    [InlineData("NIZAK", 1)]
    [InlineData("NEPOZNATO", 1)] // fallback
    public void ComputeRiskScore_IdealDeviation_BySeverity(string severity, int expectedPoints)
    {
        var result = new ContractAnalysisResult
        {
            IdealContractDeviations =
            [
                new IdealContractDeviation { Element = "Test", Severity = severity }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(expectedPoints, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 11 — ComputeRiskScore: Positives (-2 each, max -10)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_Positives_Reduce2Each()
    {
        var result = new ContractAnalysisResult
        {
            // Start with some score  
            RedFlags = [new ContractIssue { Title = "F1" }], // +10
            Positives = ["P1", "P2", "P3"] // -6
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(4, result.RiskScore); // 10 - 6 = 4
    }

    [Fact]
    public void ComputeRiskScore_Positives_MaxMinus10()
    {
        var result = new ContractAnalysisResult
        {
            RedFlags = [new ContractIssue { Title = "F1" }, new ContractIssue { Title = "F2" }], // +20
            Positives = ["P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8"] // 8×2=16 but capped at -10
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(10, result.RiskScore); // 20 - 10(cap) = 10
    }

    [Fact]
    public void ComputeRiskScore_PositivesOnly_ClampTo0()
    {
        var result = new ContractAnalysisResult
        {
            Positives = ["P1", "P2", "P3"] // -6 but clamped to 0
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(0, result.RiskScore); // Clamped to 0
        Assert.Equal("Nizak", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 12 — ComputeRiskScore: Risk Level Thresholds
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_Score29_Nizak()
    {
        var result = new ContractAnalysisResult
        {
            // 15 + 7 + 7 = 29
            Checklist =
            [
                new ChecklistItem { Check = "A", Risk = "VISOK" },
                new ChecklistItem { Check = "B", Risk = "SREDNJI" },
                new ChecklistItem { Check = "C", Risk = "SREDNJI" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(29, result.RiskScore);
        Assert.Equal("Nizak", result.OverallRisk);
    }

    [Fact]
    public void ComputeRiskScore_Score30_Srednji()
    {
        var result = new ContractAnalysisResult
        {
            // 15 + 15 = 30
            Checklist =
            [
                new ChecklistItem { Check = "A", Risk = "VISOK" },
                new ChecklistItem { Check = "B", Risk = "VISOK" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(30, result.RiskScore);
        Assert.Equal("Srednji", result.OverallRisk);
    }

    [Fact]
    public void ComputeRiskScore_Score59_Srednji()
    {
        var result = new ContractAnalysisResult
        {
            // 15+15+15 + 7+7 = 59
            Checklist =
            [
                new ChecklistItem { Check = "A", Risk = "VISOK" },
                new ChecklistItem { Check = "B", Risk = "VISOK" },
                new ChecklistItem { Check = "C", Risk = "VISOK" },
                new ChecklistItem { Check = "D", Risk = "SREDNJI" },
                new ChecklistItem { Check = "E", Risk = "SREDNJI" }
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(59, result.RiskScore);
        Assert.Equal("Srednji", result.OverallRisk);
    }

    [Fact]
    public void ComputeRiskScore_Score60_Visok()
    {
        var result = new ContractAnalysisResult
        {
            // 6 red flags × 10 = 60
            RedFlags = Enumerable.Range(0, 6).Select(i => new ContractIssue { Title = $"F{i}" }).ToList()
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(60, result.RiskScore);
        Assert.Equal("Visok", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 13 — ComputeRiskScore: Clamping to 100
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_ExtremeFindings_ClampedTo100()
    {
        var result = new ContractAnalysisResult
        {
            // Deliberately overloaded
            RedFlags = Enumerable.Range(0, 5).Select(i => new ContractIssue { Title = $"F{i}" }).ToList(), // +50
            LogicalTraps =
            [
                new LogicalTrap { Title = "T1", Severity = "KRITIČNO" }, // +18
                new LogicalTrap { Title = "T2", Severity = "KRITIČNO" }, // +18
                new LogicalTrap { Title = "T3", Severity = "VISOK" }     // +12
            ],
            IdealContractDeviations =
            [
                new IdealContractDeviation { Element = "D1", Severity = "KRITIČNO" } // +10
            ]
            // Total unclamped = 50+18+18+12+10 = 108
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(100, result.RiskScore); // Clamped
        Assert.Equal("Visok", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 14 — ComputeRiskScore: Breakdown Tracking
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_Breakdown_ContainsAllItems()
    {
        var result = new ContractAnalysisResult
        {
            Checklist = [new ChecklistItem { Check = "Kapara", Risk = "VISOK" }],
            MandatoryClauses = [new MandatoryClauseCheck { Clause = "Escrow", Present = false, Importance = "KRITIČNO" }],
            CrossArticleIssues = [new CrossArticleIssue { Title = "Konflikt", Risk = "VISOK" }],
            RedFlags = [new ContractIssue { Title = "Flag" }],
            Warnings = [new ContractIssue { Title = "Warn" }],
            LogicalTraps = [new LogicalTrap { Title = "Zamka", Severity = "VISOK" }],
            IdealContractDeviations = [new IdealContractDeviation { Element = "Dev", Severity = "SREDNJI" }],
            Positives = ["Dobro"]
        };

        ContractAnalysisService.ComputeRiskScore(result);

        // 15 + 15 + 12 + 10 + 3 + 12 + 3 - 2 = 68
        Assert.Equal(68, result.RiskScore);
        Assert.Equal("Visok", result.OverallRisk);

        // Verify breakdown has entries for each category
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Checklist"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Nedostaje"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Strukturni"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Crvene zastavice"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Upozorenja"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Zamka"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Devijacija"));
        Assert.Contains(result.RiskScoreBreakdown, b => b.Contains("Pozitivne"));
    }

    // ══════════════════════════════════════════════════════════
    //  § 15 — ComputeRiskScore: Case-Insensitive Severity
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("VISOK", 15)]
    [InlineData("visok", 15)]
    [InlineData("Visok", 15)]
    public void ComputeRiskScore_Checklist_CaseInsensitive(string risk, int expectedScore)
    {
        var result = new ContractAnalysisResult
        {
            Checklist = [new ChecklistItem { Check = "Test", Risk = risk }]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(expectedScore, result.RiskScore);
    }

    [Theory]
    [InlineData("KRITIČNO", 15)]
    [InlineData("kritično", 15)]
    [InlineData("Kritično", 15)]
    public void ComputeRiskScore_MandatoryClause_CaseInsensitive(string importance, int expected)
    {
        var result = new ContractAnalysisResult
        {
            MandatoryClauses = [new MandatoryClauseCheck { Clause = "X", Present = false, Importance = importance }]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(expected, result.RiskScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 16 — ComputeRiskScore: Null Severity Handling
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_NullChecklistRisk_NoPoints()
    {
        var result = new ContractAnalysisResult
        {
            Checklist = [new ChecklistItem { Check = "Test", Risk = null! }]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(0, result.RiskScore);
    }

    [Fact]
    public void ComputeRiskScore_NullMandatoryImportance_DefaultsTo5()
    {
        var result = new ContractAnalysisResult
        {
            MandatoryClauses = [new MandatoryClauseCheck { Clause = "X", Present = false, Importance = null! }]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(5, result.RiskScore); // null != "KRITIČNO" → 5
    }

    [Fact]
    public void ComputeRiskScore_NullTrapSeverity_Fallback6()
    {
        var result = new ContractAnalysisResult
        {
            LogicalTraps = [new LogicalTrap { Title = "Test", Severity = null! }]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(6, result.RiskScore); // null → default case = 6
    }

    // ══════════════════════════════════════════════════════════
    //  § 17 — Serialization: ContractAnalysisResult
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ContractAnalysisResult_Serialization_SnakeCase()
    {
        var result = new ContractAnalysisResult
        {
            DocumentType = "Kupoprodajni ugovor",
            OverallRisk = "Srednji",
            RiskScore = 45
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Contains("\"document_type\"", json);
        Assert.Contains("\"overall_risk\"", json);
        Assert.Contains("\"risk_score\"", json);
        Assert.Contains("\"red_flags\"", json);
        Assert.Contains("\"suggested_amendments\"", json);
        Assert.Contains("\"cross_article_issues\"", json);
        Assert.Contains("\"mandatory_clauses\"", json);
        Assert.Contains("\"risk_score_breakdown\"", json);
        Assert.Contains("\"logical_traps\"", json);
        Assert.Contains("\"ideal_contract_deviations\"", json);
        Assert.Contains("\"cited_norms\"", json);
    }

    [Fact]
    public void LogicalTrap_Serialization()
    {
        var trap = new LogicalTrap
        {
            Title = "Rok za prijavu nedostataka",
            Articles = ["čl. 3", "čl. 7"],
            QuotedText = "Kupac se odriče prava na...",
            Severity = "KRITIČNO",
            LegalBasis = "čl. 400. ZOO"
        };

        var json = JsonSerializer.Serialize(trap, JsonOptions);

        Assert.Contains("\"quoted_text\"", json);
        Assert.Contains("\"legal_basis\"", json);
    }

    [Fact]
    public void IdealContractDeviation_Serialization()
    {
        var dev = new IdealContractDeviation
        {
            Element = "Clausula intabulandi",
            IdealText = "Bezuvjetna clausula intabulandi",
            ActualText = "NEDOSTAJE"
        };

        var json = JsonSerializer.Serialize(dev, JsonOptions);

        Assert.Contains("\"ideal_text\"", json);
        Assert.Contains("\"actual_text\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  § 18 — Realistic Scenario: Simple Safe Contract
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_SafeContract_LowRisk()
    {
        var result = new ContractAnalysisResult
        {
            DocumentType = "Kupoprodajni ugovor",
            Summary = "Standardni kupoprodajni ugovor za stan u Zagrebu",
            Checklist =
            [
                new ChecklistItem { Check = "Identifikacija stranaka", Risk = "NIZAK", Status = "UREĐENO" },
                new ChecklistItem { Check = "Predmet ugovora", Risk = "NIZAK", Status = "UREĐENO" },
                new ChecklistItem { Check = "Cijena i plaćanje", Risk = "NIZAK", Status = "UREĐENO" }
            ],
            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Clausula intabulandi", Present = true, Importance = "KRITIČNO" },
                new MandatoryClauseCheck { Clause = "Escrow", Present = true, Importance = "PREPORUČENO" }
            ],
            Positives = ["Clausula intabulandi bezuvjetna", "Escrow plaćanje", "OIB obiju stranaka naveden",
                         "Katastarska oznaka precizna", "Energetski certifikat priložen"]
        };

        ContractAnalysisService.ComputeRiskScore(result);

        Assert.Equal(0, result.RiskScore); // 0 - 10(5×2 capped) → clamped to 0
        Assert.Equal("Nizak", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 19 — Realistic Scenario: Dangerous Contract
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_DangerousContract_HighRisk()
    {
        var result = new ContractAnalysisResult
        {
            DocumentType = "Kupoprodajni ugovor",
            Summary = "Opasan ugovor — nedostaje zaštita kupca",

            RedFlags =
            [
                new ContractIssue { Title = "Nekretnina pod hipotekom", Description = "Hipoteka nije ugovorom riješena" },
                new ContractIssue { Title = "Nema clausulae intabulandi", Description = "Kupac ne može uknjižiti vlasništvo" }
            ],

            Warnings =
            [
                new ContractIssue { Title = "Rok predaje neprecizan" },
                new ContractIssue { Title = "Nedostaje energetski certifikat" }
            ],

            Checklist =
            [
                new ChecklistItem { Check = "Tereti", Risk = "VISOK" },
                new ChecklistItem { Check = "Clausula intabulandi", Risk = "VISOK" }
            ],

            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Clausula intabulandi", Present = false, Importance = "KRITIČNO" },
                new MandatoryClauseCheck { Clause = "Escrow", Present = false, Importance = "PREPORUČENO" }
            ],

            CrossArticleIssues =
            [
                new CrossArticleIssue { Title = "Rok plaćanja vs. brisanje hipoteke", Risk = "VISOK" }
            ],

            LogicalTraps =
            [
                new LogicalTrap { Title = "Odricanje od prava na prigovor", Severity = "KRITIČNO" }
            ],

            IdealContractDeviations =
            [
                new IdealContractDeviation { Element = "Clausula intabulandi", Severity = "KRITIČNO" },
                new IdealContractDeviation { Element = "Escrow plaćanje", Severity = "VISOK" }
            ],

            Positives = ["OIB stranaka naveden"]
        };

        ContractAnalysisService.ComputeRiskScore(result);

        // 20 + 6 + 30 + 20 + 12 + 18 + 10 + 6 - 2 = 120 → clamped to 100
        // Breakdown: RedFlags=2×10=20, Warnings=2×3=6, Checklist=2×15=30,
        //            Mandatory=15+5=20, CrossArticle=12, Trap=18, Devs=10+6=16, Positives=-2
        Assert.Equal(100, result.RiskScore);
        Assert.Equal("Visok", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 20 — Realistic Scenario: Medium Risk Contract
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_MediumRiskContract()
    {
        var result = new ContractAnalysisResult
        {
            DocumentType = "Predugovor",
            Summary = "Predugovor za kupnju kuće — djelomično zaštićen",

            Warnings =
            [
                new ContractIssue { Title = "Rok za pribavljanje uporabne dozvole nije definiran" },
                new ContractIssue { Title = "Kapara definirana kao odustatnina" }
            ],

            Checklist =
            [
                new ChecklistItem { Check = "Kapara", Risk = "SREDNJI" },
                new ChecklistItem { Check = "Uporabna dozvola", Risk = "SREDNJI" }
            ],

            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "Clausula intabulandi", Present = true, Importance = "KRITIČNO" },
                new MandatoryClauseCheck { Clause = "Raskidni uvjet", Present = false, Importance = "PREPORUČENO" }
            ],

            IdealContractDeviations =
            [
                new IdealContractDeviation { Element = "Kapara vs. Odustatnina", Severity = "SREDNJI" }
            ],

            Positives = ["Clausula intabulandi bezuvjetna", "Identifikacija stranaka kompletna"]
        };

        ContractAnalysisService.ComputeRiskScore(result);

        // Warnings=2×3=6, Checklist=2×7=14, Mandatory=5, Devs=3, Positives=-4
        // Total = 6+14+5+3-4 = 24
        Assert.Equal(24, result.RiskScore);
        Assert.Equal("Nizak", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 21 — Combined Score: All Finding Types
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_AllFindingTypes_AccurateSum()
    {
        var result = new ContractAnalysisResult
        {
            Checklist =
            [
                new ChecklistItem { Check = "A", Risk = "VISOK" },   // +15
                new ChecklistItem { Check = "B", Risk = "SREDNJI" }  // +7
            ],
            MandatoryClauses =
            [
                new MandatoryClauseCheck { Clause = "C1", Present = false, Importance = "KRITIČNO" },     // +15
                new MandatoryClauseCheck { Clause = "C2", Present = false, Importance = "PREPORUČENO" }  // +5
            ],
            CrossArticleIssues =
            [
                new CrossArticleIssue { Title = "X1", Risk = "VISOK" }  // +12
            ],
            RedFlags =
            [
                new ContractIssue { Title = "RF" }  // +10
            ],
            Warnings =
            [
                new ContractIssue { Title = "W1" },
                new ContractIssue { Title = "W2" }  // 2×3 = +6
            ],
            LogicalTraps =
            [
                new LogicalTrap { Title = "T", Severity = "VISOK" }  // +12
            ],
            IdealContractDeviations =
            [
                new IdealContractDeviation { Element = "D", Severity = "SREDNJI" }  // +3
            ],
            Positives = ["P1", "P2"]  // 2×2 = -4
        };

        ContractAnalysisService.ComputeRiskScore(result);

        // 15+7+15+5+12+10+6+12+3-4 = 81
        Assert.Equal(81, result.RiskScore);
        Assert.Equal("Visok", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 22 — Serialization: Roundtrip Full Result
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ContractAnalysisResult_Roundtrip_Serialization()
    {
        var original = new ContractAnalysisResult
        {
            DocumentType = "Glavni Ugovor",
            Summary = "Standard",
            OverallRisk = "Nizak",
            RiskScore = 5,
            RedFlags = [new ContractIssue { Title = "Test", Description = "Desc", QuotedText = "\"čl. 3\"" }],
            Warnings = [new ContractIssue { Title = "W" }],
            Positives = ["OK"],
            LogicalTraps = [new LogicalTrap { Title = "T", Articles = ["čl.1"], Severity = "VISOK" }],
            IdealContractDeviations = [new IdealContractDeviation { Element = "E", Severity = "NIZAK" }],
            Checklist = [new ChecklistItem { Category = "C", Check = "Ch", Status = "UREĐENO", Risk = "NIZAK" }],
            CrossArticleIssues = [new CrossArticleIssue { Title = "CA", LinkedArticles = ["čl.2"], Risk = "SREDNJI" }],
            MandatoryClauses = [new MandatoryClauseCheck { Clause = "CI", Present = true, Importance = "KRITIČNO" }],
            SuggestedAmendments = [new SuggestedAmendment { ArticleReference = "čl. 5", ProposedText = "Dodati..." }]
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ContractAnalysisResult>(json, JsonOptions)!;

        Assert.Equal("Glavni Ugovor", deserialized.DocumentType);
        Assert.Equal("Nizak", deserialized.OverallRisk);
        Assert.Equal(5, deserialized.RiskScore);
        Assert.Single(deserialized.RedFlags);
        Assert.Single(deserialized.Warnings);
        Assert.Single(deserialized.Positives);
        Assert.Single(deserialized.LogicalTraps);
        Assert.Single(deserialized.IdealContractDeviations);
        Assert.Single(deserialized.Checklist);
        Assert.Single(deserialized.CrossArticleIssues);
        Assert.Single(deserialized.MandatoryClauses);
        Assert.Single(deserialized.SuggestedAmendments);
        Assert.Equal("Test", deserialized.RedFlags[0].Title);
        Assert.Equal("\"čl. 3\"", deserialized.RedFlags[0].QuotedText);
    }

    // ══════════════════════════════════════════════════════════
    //  § 23 — Edge Case: Exact Boundary Scores
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_ExactlyAt30_IsSrednji()
    {
        // 3 red flags = 30
        var result = new ContractAnalysisResult
        {
            RedFlags = Enumerable.Range(0, 3).Select(i => new ContractIssue { Title = $"F{i}" }).ToList()
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(30, result.RiskScore);
        Assert.Equal("Srednji", result.OverallRisk);
    }

    [Fact]
    public void ComputeRiskScore_ExactlyAt59_IsSrednji()
    {
        // 5 red flags + 3 warnings = 50 + 9 = 59
        var result = new ContractAnalysisResult
        {
            RedFlags = Enumerable.Range(0, 5).Select(i => new ContractIssue { Title = $"F{i}" }).ToList(),
            Warnings = Enumerable.Range(0, 3).Select(i => new ContractIssue { Title = $"W{i}" }).ToList()
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(59, result.RiskScore);
        Assert.Equal("Srednji", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 24 — Edge Case: Multiple Logical Traps Mixed Severity
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_MixedTraps()
    {
        var result = new ContractAnalysisResult
        {
            LogicalTraps =
            [
                new LogicalTrap { Title = "T1", Severity = "KRITIČNO" },  // +18
                new LogicalTrap { Title = "T2", Severity = "VISOK" },     // +12
                new LogicalTrap { Title = "T3", Severity = "SREDNJI" },   // +6
                new LogicalTrap { Title = "T4", Severity = "NIZAK" }      // +6 (default)
            ]
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(42, result.RiskScore); // 18+12+6+6 = 42
        Assert.Equal("Srednji", result.OverallRisk);
    }

    // ══════════════════════════════════════════════════════════
    //  § 25 — Edge Case: Many Positives Can't Go Negative
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeRiskScore_ManyPositives_StillClamped()
    {
        var result = new ContractAnalysisResult
        {
            Warnings = [new ContractIssue { Title = "W" }], // +3
            Positives = Enumerable.Range(0, 20).Select(i => $"P{i}").ToList() // 20×2=40 but capped at -10
        };

        ContractAnalysisService.ComputeRiskScore(result);
        Assert.Equal(0, result.RiskScore); // 3 - 10 = -7 → clamped to 0
    }

    // ══════════════════════════════════════════════════════════
    //  § 26 — Croatian Legal Document Types
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Predugovor")]
    [InlineData("Glavni Ugovor")]
    [InlineData("Aneks")]
    [InlineData("Kupoprodajni ugovor")]
    public void ContractResult_AcceptsDocumentTypes(string docType)
    {
        var result = new ContractAnalysisResult { DocumentType = docType };
        Assert.Equal(docType, result.DocumentType);
    }
}
