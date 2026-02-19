using System.Text.Json;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Comprehensive tests for the Legal Intelligence (Sudska Praksa) module:
/// - BuildCaseContext: text assembly and truncation
/// - ComputeWinProbability: deterministic formula (60% GPT + 40% precedent)
/// - All model defaults and serialization
/// - Win probability labels (Visoka/Srednja/Niska)  
/// - Score breakdown accuracy
/// - Precedent alignment counting
/// - Edge cases: no precedents, all favorable/unfavorable
/// - Realistic Croatian court case scenarios
/// </summary>
public class LegalIntelligenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // ══════════════════════════════════════════════════════════
    //  § 1 — Model Defaults
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void CourtPrecedent_DefaultValues()
    {
        var p = new CourtPrecedent();
        Assert.Equal("", p.CaseNumber);
        Assert.Equal("", p.Court);
        Assert.Equal("", p.Date);
        Assert.Equal("", p.Summary);
        Assert.Equal("", p.LegalPrinciple);
        Assert.Equal("", p.RelevanceToCase);
        Assert.Equal("", p.Alignment);
        Assert.Equal("", p.LegalBasis);
        Assert.Equal("", p.Stability);
        Assert.Equal(0, p.SimilarityScore);
    }

    [Fact]
    public void LegalStrategyResult_DefaultValues()
    {
        var r = new LegalStrategyResult();
        Assert.Equal("", r.KeyLegalIssue);
        Assert.Empty(r.SubIssues);
        Assert.Empty(r.Precedents);
        Assert.Equal(0, r.WinProbability);
        Assert.Equal(0, r.WinProbabilityMin);
        Assert.Equal(0, r.WinProbabilityMax);
        Assert.Equal("", r.WinProbabilityLabel);
        Assert.Equal("", r.ProbabilityReasoning);
        Assert.Null(r.ScoreBreakdown);
        Assert.Empty(r.ConfidenceFactors);
        Assert.Contains("AI model", r.DataProvenance);
        Assert.Empty(r.Risks);
        Assert.Empty(r.Strengths);
        Assert.Empty(r.Recommendations);
        Assert.Equal("", r.StrategySummary);
        Assert.Equal("", r.EstimatedDuration);
        Assert.Equal("", r.EstimatedCost);
        Assert.Empty(r.CitedNorms);
        Assert.Empty(r.AuditTrail);
        Assert.Contains("AI analiza", r.Disclaimer);
    }

    [Fact]
    public void ScoreBreakdown_DefaultValues()
    {
        var sb = new ScoreBreakdown();
        Assert.Equal(0, sb.FavorableCount);
        Assert.Equal(0, sb.NeutralCount);
        Assert.Equal(0, sb.UnfavorableCount);
        Assert.Equal("", sb.PracticeStability);
        Assert.Equal("", sb.LegalQualification);
        Assert.Equal(0, sb.PrecedentScore);
        Assert.Equal(0, sb.QualitativeScore);
        Assert.Equal("", sb.Methodology);
    }

    [Fact]
    public void ConfidenceFactor_DefaultValues()
    {
        var cf = new ConfidenceFactor();
        Assert.Equal("", cf.Factor);
        Assert.Equal("", cf.Direction);
        Assert.Equal("", cf.Explanation);
    }

    [Fact]
    public void LegalRisk_DefaultValues()
    {
        var r = new LegalRisk();
        Assert.Equal("", r.Title);
        Assert.Equal("", r.Description);
        Assert.Equal("", r.Severity);
        Assert.Equal("", r.Mitigation);
        Assert.Equal("", r.CaseElement);
    }

    [Fact]
    public void StrategicRecommendation_DefaultValues()
    {
        var r = new StrategicRecommendation();
        Assert.Equal("", r.Action);
        Assert.Equal("", r.Rationale);
        Assert.Equal("", r.Priority);
        Assert.Equal("", r.Timing);
    }

    [Fact]
    public void AuditLogEntry_DefaultValues()
    {
        var e = new AuditLogEntry();
        Assert.Equal(0, e.Step);
        Assert.Equal("", e.Action);
        Assert.Equal("", e.LegalBasis);
        Assert.Equal("", e.Citation);
        Assert.Equal("", e.Reasoning);
        Assert.Equal("SREDNJE", e.Confidence); // default
        Assert.Equal("", e.Timestamp);
    }

    [Fact]
    public void LegalFeedItem_DefaultValues()
    {
        var f = new LegalFeedItem();
        Assert.Equal("", f.CaseNumber);
        Assert.Equal("", f.Court);
        Assert.Equal("", f.Date);
        Assert.Equal("", f.Topic);
        Assert.Equal("", f.Headline);
        Assert.Equal("", f.Takeaway);
        Assert.Equal("", f.Impact);
        Assert.Equal("", f.AreaOfLaw);
        Assert.True(f.IsAiGenerated); // default = true
    }

    [Fact]
    public void LegalStrategyRequest_DefaultValues()
    {
        var req = new LegalStrategyRequest();
        Assert.Equal("", req.CaseDescription);
        Assert.Null(req.ContractText);
        Assert.Null(req.ChronologySummary);
        Assert.Null(req.ZkSummary);
        Assert.Equal("kupac", req.ClientPosition); // default
        Assert.Equal("", req.DesiredOutcome);
    }

    // ══════════════════════════════════════════════════════════
    //  § 2 — BuildCaseContext: Basic Assembly
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildCaseContext_AlwaysIncludesClientPosition()
    {
        var req = new LegalStrategyRequest { ClientPosition = "kupac" };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("POZICIJA KLIJENTA: kupac", ctx);
    }

    [Fact]
    public void BuildCaseContext_IncludesDesiredOutcome()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            DesiredOutcome = "Raskid ugovora i povrat kapare"
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("ŽELJENI ISHOD: Raskid ugovora i povrat kapare", ctx);
    }

    [Fact]
    public void BuildCaseContext_EmptyOutcome_Excluded()
    {
        var req = new LegalStrategyRequest { ClientPosition = "kupac", DesiredOutcome = "" };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.DoesNotContain("ŽELJENI ISHOD", ctx);
    }

    [Fact]
    public void BuildCaseContext_IncludesCaseDescription()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = "Kupio sam stan u Zagrebu, prodavatelj odbija predati ključeve."
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("OPIS SLUČAJA:", ctx);
        Assert.Contains("odbija predati ključeve", ctx);
    }

    [Fact]
    public void BuildCaseContext_IncludesContractText()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ContractText = "Članak 1. Prodavatelj prodaje stan..."
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("TEKST UGOVORA:", ctx);
        Assert.Contains("Prodavatelj prodaje stan", ctx);
    }

    [Fact]
    public void BuildCaseContext_IncludesChronologySummary()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ChronologySummary = "15.01.2023. — potpisan predugovor"
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("KRONOLOGIJA SPISA:", ctx);
        Assert.Contains("potpisan predugovor", ctx);
    }

    [Fact]
    public void BuildCaseContext_IncludesZkSummary()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ZkSummary = "ZK uložak 12345, bez tereta"
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("ZK IZVADAK (ZEMLJIŠNOKNJIŽNI PODACI):", ctx);
        Assert.Contains("bez tereta", ctx);
    }

    // ══════════════════════════════════════════════════════════
    //  § 3 — BuildCaseContext: Truncation
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildCaseContext_CaseDescription_TruncatedAt5000()
    {
        var longText = new string('A', 6000);
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = longText
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);

        // The case description part should be truncated
        Assert.True(ctx.Length < 6000 + 100); // with headers
        Assert.Contains("OPIS SLUČAJA:", ctx);
    }

    [Fact]
    public void BuildCaseContext_ContractText_TruncatedAt6000()
    {
        var longText = new string('B', 8000);
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ContractText = longText
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("TEKST UGOVORA:", ctx);
        // Should not contain full 8000 chars of B
        int bCount = ctx.Count(c => c == 'B');
        Assert.True(bCount <= 6000, $"Expected ≤6000 B chars, got {bCount}");
    }

    [Fact]
    public void BuildCaseContext_ChronologySummary_TruncatedAt3000()
    {
        var longText = new string('Q', 4000);
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ChronologySummary = longText
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        int qCount = ctx.Count(c => c == 'Q');
        Assert.True(qCount <= 3000, $"Expected ≤3000 Q chars, got {qCount}");
    }

    [Fact]
    public void BuildCaseContext_ZkSummary_TruncatedAt2000()
    {
        var longText = new string('X', 3000);
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            ZkSummary = longText
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        int xCount = ctx.Count(c => c == 'X');
        Assert.True(xCount <= 2000, $"Expected ≤2000 X chars, got {xCount}");
    }

    [Fact]
    public void BuildCaseContext_ShortTexts_NotTruncated()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = "Kratki opis",
            ContractText = "Kratki ugovor",
            ChronologySummary = "Kratka kronologija",
            ZkSummary = "Kratki ZK"
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);
        Assert.Contains("Kratki opis", ctx);
        Assert.Contains("Kratki ugovor", ctx);
        Assert.Contains("Kratka kronologija", ctx);
        Assert.Contains("Kratki ZK", ctx);
    }

    // ══════════════════════════════════════════════════════════
    //  § 4 — BuildCaseContext: Null/Empty Handling
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void BuildCaseContext_AllNullOptional_OnlyPosition()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "prodavatelj",
            CaseDescription = "",
            ContractText = null,
            ChronologySummary = null,
            ZkSummary = null,
            DesiredOutcome = ""
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);

        Assert.Contains("POZICIJA KLIJENTA: prodavatelj", ctx);
        Assert.DoesNotContain("OPIS SLUČAJA", ctx);
        Assert.DoesNotContain("TEKST UGOVORA", ctx);
        Assert.DoesNotContain("KRONOLOGIJA", ctx);
        Assert.DoesNotContain("ZK IZVADAK", ctx);
    }

    [Fact]
    public void BuildCaseContext_WhitespaceOnly_Excluded()
    {
        var req = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = "   ",
            ContractText = "\t\n"
        };
        var ctx = LegalIntelligenceService.BuildCaseContext(req);

        Assert.DoesNotContain("OPIS SLUČAJA", ctx);
        Assert.DoesNotContain("TEKST UGOVORA", ctx);
    }

    // ══════════════════════════════════════════════════════════
    //  § 5 — ComputeWinProbability: No Precedents
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_NoPrecedents_DefaultPrecedentScore50()
    {
        var result = new LegalStrategyResult();
        // gptProbability=60, gptMin=50, gptMax=70
        LegalIntelligenceService.ComputeWinProbability(result, 60, 50, 70);

        // precedentScore = 50 (no precedents)
        // final = round(60*0.6 + 50*0.4) = round(36+20) = 56
        Assert.Equal(56, result.WinProbability);
        Assert.Equal("Srednja", result.WinProbabilityLabel);
        Assert.NotNull(result.ScoreBreakdown);
        Assert.Equal(50, result.ScoreBreakdown!.PrecedentScore);
        Assert.Equal(60, result.ScoreBreakdown.QualitativeScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 6 — ComputeWinProbability: All Favorable Precedents
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_AllFavorable_HighScore()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "POVOLJNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 80, 70, 90);

        // precedentScore = round((3*100+0*50)/3) = 100
        // final = round(80*0.6 + 100*0.4) = round(48+40) = 88
        Assert.Equal(88, result.WinProbability);
        Assert.Equal("Visoka", result.WinProbabilityLabel);
        Assert.Equal(3, result.ScoreBreakdown!.FavorableCount);
        Assert.Equal(0, result.ScoreBreakdown.UnfavorableCount);
        Assert.Equal(0, result.ScoreBreakdown.NeutralCount);
        Assert.Equal(100, result.ScoreBreakdown.PrecedentScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 7 — ComputeWinProbability: All Unfavorable
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_AllUnfavorable_LowScore()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "NEPOVOLJNA" },
                new CourtPrecedent { Alignment = "NEPOVOLJNA" },
                new CourtPrecedent { Alignment = "NEPOVOLJNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 30, 20, 40);

        // precedentScore = round((0+0)/3) = 0
        // final = round(30*0.6 + 0*0.4) = 18
        Assert.Equal(18, result.WinProbability);
        Assert.Equal("Niska", result.WinProbabilityLabel);
        Assert.Equal(0, result.ScoreBreakdown!.PrecedentScore);
    }

    // ══════════════════════════════════════════════════════════
    //  § 8 — ComputeWinProbability: Mixed Precedents
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_MixedPrecedents()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "NEUTRALNA" },
                new CourtPrecedent { Alignment = "NEPOVOLJNA" },
                new CourtPrecedent { Alignment = "POVOLJNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 55, 45, 65);

        // favorable=2, neutral=1, unfavorable=1, total=4
        // precedentScore = round((2*100 + 1*50) / 4) = round(250/4) = round(62.5) = 63 (MidpointRounding.ToEven = 62)
        int expectedPrecedent = (int)Math.Round((2 * 100.0 + 1 * 50.0) / 4);
        int expectedFinal = (int)Math.Round(55 * 0.6 + expectedPrecedent * 0.4);

        Assert.Equal(expectedFinal, result.WinProbability);
        Assert.Equal(2, result.ScoreBreakdown!.FavorableCount);
        Assert.Equal(1, result.ScoreBreakdown.NeutralCount);
        Assert.Equal(1, result.ScoreBreakdown.UnfavorableCount);
    }

    // ══════════════════════════════════════════════════════════
    //  § 9 — ComputeWinProbability: Labels
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(100, "Visoka")]   // GPT=100, no prec → 100*0.6+50*0.4=80
    [InlineData(90, "Visoka")]    // 90*0.6+50*0.4 = 54+20 = 74
    [InlineData(50, "Srednja")]   // 50*0.6+50*0.4 = 50
    [InlineData(20, "Niska")]     // 20*0.6+50*0.4 = 12+20 = 32
    [InlineData(0, "Niska")]      // 0*0.6+50*0.4 = 20
    public void ComputeWinProbability_Labels_NoPrecedents(int gptScore, string expectedLabel)
    {
        var result = new LegalStrategyResult();
        LegalIntelligenceService.ComputeWinProbability(result, gptScore, 
            Math.Max(0, gptScore - 10), Math.Min(100, gptScore + 10));

        Assert.Equal(expectedLabel, result.WinProbabilityLabel);
    }

    // ══════════════════════════════════════════════════════════
    //  § 10 — ComputeWinProbability: Min ≤ Mid ≤ Max
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_MinLeMidLeMax()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "NEPOVOLJNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 50, 30, 70);

        Assert.True(result.WinProbabilityMin <= result.WinProbability,
            $"Min {result.WinProbabilityMin} > Mid {result.WinProbability}");
        Assert.True(result.WinProbability <= result.WinProbabilityMax,
            $"Mid {result.WinProbability} > Max {result.WinProbabilityMax}");
    }

    [Fact]
    public void ComputeWinProbability_ExtremeValues_Clamped()
    {
        var result = new LegalStrategyResult();
        LegalIntelligenceService.ComputeWinProbability(result, 200, -50, 300);

        Assert.True(result.WinProbability >= 0 && result.WinProbability <= 100);
        Assert.True(result.WinProbabilityMin >= 0 && result.WinProbabilityMin <= 100);
        Assert.True(result.WinProbabilityMax >= 0 && result.WinProbabilityMax <= 100);
    }

    // ══════════════════════════════════════════════════════════
    //  § 11 — ComputeWinProbability: Score Breakdown Methodology
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_Methodology_ContainsInfo()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "NEUTRALNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 65, 55, 75);

        Assert.Contains("60%", result.ScoreBreakdown!.Methodology);
        Assert.Contains("40%", result.ScoreBreakdown.Methodology);
        Assert.Contains("1 povoljnih", result.ScoreBreakdown.Methodology);
        Assert.Contains("1 neutralnih", result.ScoreBreakdown.Methodology);
        Assert.Contains("0 nepovoljnih", result.ScoreBreakdown.Methodology);
    }

    // ══════════════════════════════════════════════════════════
    //  § 12 — ComputeWinProbability: Case-Insensitive Alignment
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_AlignmentCaseInsensitive()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "povoljna" },     // lowercase
                new CourtPrecedent { Alignment = "Povoljna" },     // mixed
                new CourtPrecedent { Alignment = "NEPOVOLJNA" }    // upper
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 50, 40, 60);

        Assert.Equal(2, result.ScoreBreakdown!.FavorableCount);
        Assert.Equal(1, result.ScoreBreakdown.UnfavorableCount);
    }

    // ══════════════════════════════════════════════════════════
    //  § 13 — ComputeWinProbability: All Neutral = 50 Precedent
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_AllNeutral_PrecedentScore50()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "NEUTRALNA" },
                new CourtPrecedent { Alignment = "NEUTRALNA" },
                new CourtPrecedent { Alignment = "NEUTRALNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 50, 40, 60);

        // precedentScore = round((0+3*50)/3) = 50
        Assert.Equal(50, result.ScoreBreakdown!.PrecedentScore);
        // final = round(50*0.6 + 50*0.4) = 50
        Assert.Equal(50, result.WinProbability);
    }

    // ══════════════════════════════════════════════════════════
    //  § 14 — Serialization: LegalStrategyResult
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LegalStrategyResult_Serialization_SnakeCase()
    {
        var result = new LegalStrategyResult
        {
            KeyLegalIssue = "Pravo vlasništva",
            WinProbability = 65,
            WinProbabilityMin = 55,
            WinProbabilityMax = 75,
            WinProbabilityLabel = "Srednja"
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Contains("\"key_legal_issue\"", json);
        Assert.Contains("\"win_probability\"", json);
        Assert.Contains("\"win_probability_min\"", json);
        Assert.Contains("\"win_probability_max\"", json);
        Assert.Contains("\"win_probability_label\"", json);
        Assert.Contains("\"probability_reasoning\"", json);
        Assert.Contains("\"score_breakdown\"", json);
        Assert.Contains("\"confidence_factors\"", json);
        Assert.Contains("\"data_provenance\"", json);
        Assert.Contains("\"strategy_summary\"", json);
        Assert.Contains("\"estimated_duration\"", json);
        Assert.Contains("\"estimated_cost\"", json);
        Assert.Contains("\"cited_norms\"", json);
        Assert.Contains("\"audit_trail\"", json);
    }

    [Fact]
    public void CourtPrecedent_Serialization()
    {
        var p = new CourtPrecedent
        {
            CaseNumber = "Rev-1234/20-2",
            Court = "Vrhovni sud RH",
            Alignment = "POVOLJNA",
            SimilarityScore = 85,
            Stability = "STABILNA"
        };

        var json = JsonSerializer.Serialize(p, JsonOptions);

        Assert.Contains("\"case_number\"", json);
        Assert.Contains("\"legal_principle\"", json);
        Assert.Contains("\"relevance_to_case\"", json);
        Assert.Contains("\"similarity_score\"", json);
    }

    [Fact]
    public void LegalFeedItem_Serialization()
    {
        var f = new LegalFeedItem
        {
            CaseNumber = "Gž-123/2024",
            IsAiGenerated = true,
            AreaOfLaw = "Obvezno pravo"
        };

        var json = JsonSerializer.Serialize(f, JsonOptions);

        Assert.Contains("\"is_ai_generated\"", json);
        Assert.Contains("\"area_of_law\"", json);
    }

    [Fact]
    public void AuditLogEntry_Serialization()
    {
        var e = new AuditLogEntry
        {
            Step = 1,
            Action = "Identificiran glavni pravni problem",
            LegalBasis = "čl. 368. ZOO",
            Confidence = "VISOKO"
        };

        var json = JsonSerializer.Serialize(e, JsonOptions);
        Assert.Contains("\"legal_basis\"", json);
    }

    // ══════════════════════════════════════════════════════════
    //  § 15 — LegalStrategyResult Roundtrip
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LegalStrategyResult_Roundtrip()
    {
        var original = new LegalStrategyResult
        {
            KeyLegalIssue = "Raskid ugovora",
            SubIssues = ["Povrat kapare", "Naknada štete"],
            WinProbability = 72,
            WinProbabilityMin = 62,
            WinProbabilityMax = 82,
            WinProbabilityLabel = "Visoka",
            Precedents =
            [
                new CourtPrecedent { CaseNumber = "Rev-100/20", Alignment = "POVOLJNA", SimilarityScore = 90 }
            ],
            Risks = [new LegalRisk { Title = "Zastara", Severity = "VISOK" }],
            Recommendations = [new StrategicRecommendation { Action = "Tužba", Priority = "HITNO" }],
            AuditTrail = [new AuditLogEntry { Step = 1, Action = "Analiza" }]
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var d = JsonSerializer.Deserialize<LegalStrategyResult>(json, JsonOptions)!;

        Assert.Equal("Raskid ugovora", d.KeyLegalIssue);
        Assert.Equal(2, d.SubIssues.Count);
        Assert.Equal(72, d.WinProbability);
        Assert.Single(d.Precedents);
        Assert.Equal("POVOLJNA", d.Precedents[0].Alignment);
        Assert.Equal(90, d.Precedents[0].SimilarityScore);
        Assert.Single(d.Risks);
        Assert.Single(d.Recommendations);
        Assert.Single(d.AuditTrail);
    }

    // ══════════════════════════════════════════════════════════
    //  § 16 — Realistic Scenario: Raskid Kupoprodajnog Ugovora
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_ContractTermination_CaseAnalysis()
    {
        // Kupac želi raskinuti ugovor jer prodavatelj nije brisao hipoteku
        var request = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = "Kupio sam stan u Zagrebu za 150.000 EUR. Prodavatelj je " +
                "trebao izbrisati hipoteku u roku 30 dana ali to nije učinio. " +
                "Prošlo je 6 mjeseci.",
            ContractText = "Članak 5. Prodavatelj se obvezuje brisati hipoteku u roku " +
                "od 30 dana od potpisa ugovora.",
            DesiredOutcome = "Raskid ugovora i povrat kapare u dvostrukom iznosu"
        };

        var ctx = LegalIntelligenceService.BuildCaseContext(request);
        Assert.Contains("kupac", ctx);
        Assert.Contains("hipoteku", ctx);
        Assert.Contains("Članak 5", ctx);
        Assert.Contains("Raskid ugovora", ctx);

        // Simulate favorable precedents
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent
                {
                    CaseNumber = "Rev-1234/20-2",
                    Court = "Vrhovni sud RH",
                    Alignment = "POVOLJNA",
                    SimilarityScore = 85,
                    Stability = "STABILNA",
                    Summary = "Kupac ima pravo na raskid ako prodavatelj ne ispuni ugovornu obvezu brisanja tereta"
                },
                new CourtPrecedent
                {
                    CaseNumber = "Gž-456/22",
                    Court = "Županijski sud u Zagrebu",
                    Alignment = "POVOLJNA",
                    SimilarityScore = 78,
                    Stability = "STABILNA"
                },
                new CourtPrecedent
                {
                    CaseNumber = "Gž-789/21",
                    Court = "Županijski sud u Splitu",
                    Alignment = "NEUTRALNA",
                    SimilarityScore = 60,
                    Stability = "PODIJELJENA"
                }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 75, 65, 85);

        // precedentScore = round((2*100 + 1*50) / 3) = round(250/3) = round(83.33) = 83
        int expectedPrecedent = (int)Math.Round((2 * 100.0 + 1 * 50.0) / 3);
        int expectedFinal = (int)Math.Round(75 * 0.6 + expectedPrecedent * 0.4);

        Assert.Equal(expectedFinal, result.WinProbability);
        Assert.True(result.WinProbability >= 70, "Expected Visoka probability for strong case");
        Assert.Equal("Visoka", result.WinProbabilityLabel);
    }

    // ══════════════════════════════════════════════════════════
    //  § 17 — Realistic Scenario: Weak Case — All Unfavorable
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_WeakCase_LowProbability()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "NEPOVOLJNA", SimilarityScore = 90 },
                new CourtPrecedent { Alignment = "NEPOVOLJNA", SimilarityScore = 85 },
                new CourtPrecedent { Alignment = "NEPOVOLJNA", SimilarityScore = 80 },
                new CourtPrecedent { Alignment = "NEUTRALNA", SimilarityScore = 40 }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 25, 15, 35);

        // precedentScore = round((0+1*50)/4) = round(12.5) = 12 (MidpointRounding.ToEven)
        int expectedPrecedent = (int)Math.Round((0 * 100.0 + 1 * 50.0) / 4);
        int expectedFinal = (int)Math.Round(25 * 0.6 + expectedPrecedent * 0.4);

        Assert.Equal(expectedFinal, result.WinProbability);
        Assert.Equal("Niska", result.WinProbabilityLabel);
        Assert.Equal(3, result.ScoreBreakdown!.UnfavorableCount);
    }

    // ══════════════════════════════════════════════════════════
    //  § 18 — Realistic Scenario: Služnost (Easement) Case
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_EasementDispute()
    {
        var request = new LegalStrategyRequest
        {
            ClientPosition = "kupac",
            CaseDescription = "Susjed koristi pristupni put preko moje nekretnine " +
                "bez upisane služnosti. Zahtijevam zabranu korištenja.",
            ZkSummary = "K.o. Trnje, k.č.br. 1234/1, vlasnik: Ivan Horvat, " +
                "nema upisanih služnosti",
            DesiredOutcome = "Zabrana korištenja pristupnog puta od strane susjeda"
        };

        var ctx = LegalIntelligenceService.BuildCaseContext(request);
        Assert.Contains("pristupni put", ctx);
        Assert.Contains("nema upisanih služnosti", ctx);
        Assert.Contains("ZK IZVADAK", ctx);
    }

    // ══════════════════════════════════════════════════════════
    //  § 19 — Edge: Single Precedent
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_SingleFavorable()
    {
        var result = new LegalStrategyResult
        {
            Precedents = [new CourtPrecedent { Alignment = "POVOLJNA" }]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 70, 60, 80);

        // precedentScore = 100
        // final = round(70*0.6 + 100*0.4) = round(42+40) = 82
        Assert.Equal(82, result.WinProbability);
        Assert.Equal("Visoka", result.WinProbabilityLabel);
    }

    [Fact]
    public void ComputeWinProbability_SingleUnfavorable()
    {
        var result = new LegalStrategyResult
        {
            Precedents = [new CourtPrecedent { Alignment = "NEPOVOLJNA" }]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 40, 30, 50);

        // precedentScore = 0
        // final = round(40*0.6 + 0*0.4) = 24
        Assert.Equal(24, result.WinProbability);
        Assert.Equal("Niska", result.WinProbabilityLabel);
    }

    // ══════════════════════════════════════════════════════════
    //  § 20 — ComputeWinProbability: 50/50 GPT + 50/50 Precedents = 50
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ComputeWinProbability_PerfectBalance_50()
    {
        var result = new LegalStrategyResult
        {
            Precedents =
            [
                new CourtPrecedent { Alignment = "POVOLJNA" },
                new CourtPrecedent { Alignment = "NEPOVOLJNA" }
            ]
        };

        LegalIntelligenceService.ComputeWinProbability(result, 50, 40, 60);

        // precedentScore = round((100+0)/2) = 50
        // final = round(50*0.6+50*0.4) = 50
        Assert.Equal(50, result.WinProbability);
        Assert.Equal("Srednja", result.WinProbabilityLabel);
    }

    // ══════════════════════════════════════════════════════════
    //  § 21 — LegalStrategyRequest Serialization
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LegalStrategyRequest_Serialization()
    {
        var req = new LegalStrategyRequest
        {
            CaseDescription = "Opis",
            ClientPosition = "kupac",
            ContractText = "Ugovor",
            ChronologySummary = "Kronologija",
            ZkSummary = "ZK",
            DesiredOutcome = "Raskid"
        };

        var json = JsonSerializer.Serialize(req, JsonOptions);

        Assert.Contains("\"case_description\"", json);
        Assert.Contains("\"client_position\"", json);
        Assert.Contains("\"contract_text\"", json);
        Assert.Contains("\"chronology_summary\"", json);
        Assert.Contains("\"zk_summary\"", json);
        Assert.Contains("\"desired_outcome\"", json);
    }
}
