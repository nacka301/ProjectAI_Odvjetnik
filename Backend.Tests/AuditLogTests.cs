using System.Text.Json;
using Backend.Models;

namespace Backend.Tests;

/// <summary>
/// V45: Unit tests for AI Act Compliance Audit Log models.
/// Tests AuditLogEntry creation, default values, serialization/deserialization,
/// and integration with LegalStrategyResult.
/// 
/// ŠTO TESTIRAMO:
/// 1. AuditLogEntry — default values, field assignment, confidence levels
/// 2. JSON serialization/deserialization sa snake_case
/// 3. LegalStrategyResult.AuditTrail — list integration, round-trip JSON
/// 4. Edge cases — empty audit trail, multiple entries, all confidence levels
/// </summary>
public class AuditLogTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // ══════════════════════════════════════════════════════════
    //  1. AuditLogEntry — Default Values
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AuditLogEntry_DefaultValues_AreCorrect()
    {
        var entry = new AuditLogEntry();

        Assert.Equal(0, entry.Step);
        Assert.Equal("", entry.Action);
        Assert.Equal("", entry.LegalBasis);
        Assert.Equal("", entry.Citation);
        Assert.Equal("", entry.Reasoning);
        Assert.Equal("SREDNJE", entry.Confidence);  // Default confidence is SREDNJE
        Assert.Equal("", entry.Timestamp);
    }

    [Fact]
    public void AuditLogEntry_CanBeCreatedWithValues()
    {
        var entry = new AuditLogEntry
        {
            Step = 1,
            Action = "Utvrđivanje zastare potraživanja",
            LegalBasis = "čl. 368. ZOO",
            Citation = "Zakon o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18, 126/21, 114/22, 156/23)",
            Reasoning = "Opći zastarni rok za potraživanja iz ugovornih odnosa iznosi 5 godina prema čl. 368. ZOO",
            Confidence = "VISOKO",
            Timestamp = "2025-01-15T10:30:00Z"
        };

        Assert.Equal(1, entry.Step);
        Assert.Equal("Utvrđivanje zastare potraživanja", entry.Action);
        Assert.Equal("čl. 368. ZOO", entry.LegalBasis);
        Assert.Contains("NN 35/05", entry.Citation);
        Assert.Contains("5 godina", entry.Reasoning);
        Assert.Equal("VISOKO", entry.Confidence);
        Assert.Equal("2025-01-15T10:30:00Z", entry.Timestamp);
    }

    // ══════════════════════════════════════════════════════════
    //  2. Confidence Levels
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("VISOKO")]
    [InlineData("SREDNJE")]
    [InlineData("NISKO")]
    public void AuditLogEntry_AcceptsAllConfidenceLevels(string confidence)
    {
        var entry = new AuditLogEntry { Confidence = confidence };
        Assert.Equal(confidence, entry.Confidence);
    }

    // ══════════════════════════════════════════════════════════
    //  3. JSON Serialization — snake_case
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AuditLogEntry_SerializesToSnakeCase()
    {
        var entry = new AuditLogEntry
        {
            Step = 2,
            Action = "Primjena instituta zastare",
            LegalBasis = "čl. 214. ZPP",
            Citation = "Zakon o parničnom postupku (NN 53/91, 91/92)",
            Reasoning = "Sud ispituje zastaru samo na prigovor tuženika",
            Confidence = "VISOKO",
            Timestamp = "2025-01-15T10:30:00Z"
        };

        var json = JsonSerializer.Serialize(entry, SnakeCaseOptions);

        Assert.Contains("\"step\":2", json);
        Assert.Contains("\"action\":", json);
        Assert.Contains("\"legal_basis\":", json);
        Assert.Contains("\"citation\":", json);
        Assert.Contains("\"reasoning\":", json);
        Assert.Contains("\"confidence\":\"VISOKO\"", json);
        Assert.Contains("\"timestamp\":", json);
        // Ensure PascalCase is NOT present
        Assert.DoesNotContain("\"Step\"", json);
        Assert.DoesNotContain("\"LegalBasis\"", json);
        Assert.DoesNotContain("\"Confidence\"", json);
        // Verify round-trip preserves Croatian chars (č in čl.)
        var rt = JsonSerializer.Deserialize<AuditLogEntry>(json, SnakeCaseOptions);
        Assert.Equal("čl. 214. ZPP", rt!.LegalBasis);
    }

    [Fact]
    public void AuditLogEntry_DeserializesFromSnakeCase()
    {
        var json = """
        {
            "step": 3,
            "action": "Analiza sudske prakse",
            "legal_basis": "čl. 10. ZPP",
            "citation": "NN 53/91",
            "reasoning": "Relevantna praksa Vrhovnog suda potvrđuje pravni stav",
            "confidence": "NISKO",
            "timestamp": "2025-01-15T11:00:00Z"
        }
        """;

        var entry = JsonSerializer.Deserialize<AuditLogEntry>(json, SnakeCaseOptions);

        Assert.NotNull(entry);
        Assert.Equal(3, entry!.Step);
        Assert.Equal("Analiza sudske prakse", entry.Action);
        Assert.Equal("čl. 10. ZPP", entry.LegalBasis);
        Assert.Equal("NN 53/91", entry.Citation);
        Assert.Contains("Vrhovnog suda", entry.Reasoning);
        Assert.Equal("NISKO", entry.Confidence);
        Assert.Equal("2025-01-15T11:00:00Z", entry.Timestamp);
    }

    [Fact]
    public void AuditLogEntry_RoundTrip_PreservesAllFields()
    {
        var original = new AuditLogEntry
        {
            Step = 5,
            Action = "Zaključak o osnovanosti tužbenog zahtjeva",
            LegalBasis = "čl. 1045. ZOO",
            Citation = "Zakon o obveznim odnosima (NN 35/05, 41/08)",
            Reasoning = "Tuženik je odgovoran za štetu po osnovi objektivne odgovornosti",
            Confidence = "VISOKO",
            Timestamp = "2025-06-15T14:22:33Z"
        };

        var json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<AuditLogEntry>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Step, deserialized!.Step);
        Assert.Equal(original.Action, deserialized.Action);
        Assert.Equal(original.LegalBasis, deserialized.LegalBasis);
        Assert.Equal(original.Citation, deserialized.Citation);
        Assert.Equal(original.Reasoning, deserialized.Reasoning);
        Assert.Equal(original.Confidence, deserialized.Confidence);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
    }

    // ══════════════════════════════════════════════════════════
    //  4. LegalStrategyResult — AuditTrail Integration
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void LegalStrategyResult_AuditTrail_DefaultsToEmptyList()
    {
        var result = new LegalStrategyResult();
        Assert.NotNull(result.AuditTrail);
        Assert.Empty(result.AuditTrail);
    }

    [Fact]
    public void LegalStrategyResult_AuditTrail_CanHoldMultipleEntries()
    {
        var result = new LegalStrategyResult
        {
            KeyLegalIssue = "Zastara potraživanja iz ugovora o djelu",
            StrategySummary = "Tužitelj ima solidnu pravnu poziciju",
            AuditTrail =
            [
                new AuditLogEntry
                {
                    Step = 1,
                    Action = "Identifikacija primjenjivog propisa",
                    LegalBasis = "čl. 368. ZOO",
                    Citation = "NN 35/05",
                    Reasoning = "Ugovor o djelu potpada pod opći zastarni rok",
                    Confidence = "VISOKO"
                },
                new AuditLogEntry
                {
                    Step = 2,
                    Action = "Provjera prekida zastare",
                    LegalBasis = "čl. 380. ZOO",
                    Citation = "NN 35/05",
                    Reasoning = "Opomena nije prekinula zastaru jer nije priznanje duga",
                    Confidence = "SREDNJE"
                },
                new AuditLogEntry
                {
                    Step = 3,
                    Action = "Analiza tužbenog zahtjeva",
                    LegalBasis = "čl. 186. ZPP",
                    Citation = "NN 53/91",
                    Reasoning = "Tužbeni zahtjev je jasno formuliran i dopušten",
                    Confidence = "VISOKO"
                }
            ]
        };

        Assert.Equal(3, result.AuditTrail.Count);
        Assert.Equal("čl. 368. ZOO", result.AuditTrail[0].LegalBasis);
        Assert.Equal("čl. 380. ZOO", result.AuditTrail[1].LegalBasis);
        Assert.Equal("čl. 186. ZPP", result.AuditTrail[2].LegalBasis);
    }

    [Fact]
    public void LegalStrategyResult_WithAuditTrail_SerializesToSnakeCase()
    {
        var result = new LegalStrategyResult
        {
            KeyLegalIssue = "Test pravno pitanje",
            AuditTrail =
            [
                new AuditLogEntry
                {
                    Step = 1,
                    Action = "Test akcija",
                    LegalBasis = "čl. 1. ZOO",
                    Confidence = "VISOKO"
                }
            ]
        };

        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        Assert.Contains("\"audit_trail\":", json);
        Assert.Contains("\"legal_basis\":", json);
        Assert.Contains("\"key_legal_issue\":", json);
        Assert.DoesNotContain("\"AuditTrail\"", json);
        Assert.DoesNotContain("\"KeyLegalIssue\"", json);
        // Verify round-trip preserves values
        var rt = JsonSerializer.Deserialize<LegalStrategyResult>(json, SnakeCaseOptions);
        Assert.Equal("čl. 1. ZOO", rt!.AuditTrail[0].LegalBasis);
        Assert.Equal("Test pravno pitanje", rt.KeyLegalIssue);
    }

    [Fact]
    public void LegalStrategyResult_WithAuditTrail_RoundTrip()
    {
        var original = new LegalStrategyResult
        {
            KeyLegalIssue = "Naknada štete iz prometne nesreće",
            StrategySummary = "Tužitelj ima osnovu za naknadu štete",
            WinProbability = 72,
            WinProbabilityMin = 65,
            WinProbabilityMax = 80,
            AuditTrail =
            [
                new AuditLogEntry
                {
                    Step = 1,
                    Action = "Utvrđivanje krivnje",
                    LegalBasis = "čl. 1045. ZOO",
                    Citation = "NN 35/05",
                    Reasoning = "Vozač je prošao kroz crveno svjetlo",
                    Confidence = "VISOKO",
                    Timestamp = "2025-01-15T10:00:00Z"
                },
                new AuditLogEntry
                {
                    Step = 2,
                    Action = "Izračun štete",
                    LegalBasis = "čl. 1085. ZOO",
                    Citation = "NN 35/05",
                    Reasoning = "Materijalna šteta + nematerijalna šteta za bol i strah",
                    Confidence = "SREDNJE",
                    Timestamp = "2025-01-15T10:00:01Z"
                }
            ]
        };

        var json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<LegalStrategyResult>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.KeyLegalIssue, deserialized!.KeyLegalIssue);
        Assert.Equal(original.WinProbability, deserialized.WinProbability);
        Assert.Equal(2, deserialized.AuditTrail.Count);
        Assert.Equal("čl. 1045. ZOO", deserialized.AuditTrail[0].LegalBasis);
        Assert.Equal("čl. 1085. ZOO", deserialized.AuditTrail[1].LegalBasis);
        Assert.Equal("VISOKO", deserialized.AuditTrail[0].Confidence);
        Assert.Equal("SREDNJE", deserialized.AuditTrail[1].Confidence);
    }

    // ══════════════════════════════════════════════════════════
    //  5. Edge Cases
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AuditLogEntry_EmptyFields_SerializeCorrectly()
    {
        var entry = new AuditLogEntry();
        var json = JsonSerializer.Serialize(entry, SnakeCaseOptions);

        Assert.Contains("\"step\":0", json);
        Assert.Contains("\"action\":\"\"", json);
        Assert.Contains("\"legal_basis\":\"\"", json);
        Assert.Contains("\"confidence\":\"SREDNJE\"", json); // default
    }

    [Fact]
    public void AuditLogEntry_WithCroatianSpecialChars_HandledCorrectly()
    {
        var entry = new AuditLogEntry
        {
            Action = "Provjera čl. 368. st. 1. — zastara potraživanja (šest mjeseci)",
            LegalBasis = "čl. 368. ZOO — Zakon o obveznim odnosima",
            Reasoning = "Članak regulira opći zastarni rok za potraživanja ključnih obveza"
        };

        var json = JsonSerializer.Serialize(entry, SnakeCaseOptions);
        var deserialized = JsonSerializer.Deserialize<AuditLogEntry>(json, SnakeCaseOptions);

        Assert.NotNull(deserialized);
        Assert.Contains("čl. 368.", deserialized!.Action);
        Assert.Contains("šest", deserialized.Action);
        Assert.Contains("Članak", deserialized.Reasoning);
    }

    [Fact]
    public void LegalStrategyResult_EmptyAuditTrail_SerializesAsEmptyArray()
    {
        var result = new LegalStrategyResult { KeyLegalIssue = "Test" };
        var json = JsonSerializer.Serialize(result, SnakeCaseOptions);

        Assert.Contains("\"audit_trail\":[]", json);
    }

    [Fact]
    public void AuditLogEntry_DeserializesFromMinimalJson()
    {
        // Only step and action — everything else falls to defaults
        var json = """{"step":1,"action":"Test"}""";

        var entry = JsonSerializer.Deserialize<AuditLogEntry>(json, SnakeCaseOptions);

        Assert.NotNull(entry);
        Assert.Equal(1, entry!.Step);
        Assert.Equal("Test", entry.Action);
        Assert.Equal("", entry.LegalBasis);      // default
        Assert.Equal("SREDNJE", entry.Confidence);  // default
    }

    [Fact]
    public void LegalStrategyResult_DeserializesAuditTrailFromJson()
    {
        var json = """
        {
            "key_legal_issue": "Raskid ugovora o najmu",
            "strategy_summary": "Najmodavac ima osnovu za raskid",
            "win_probability": 68,
            "win_probability_min": 60,
            "win_probability_max": 75,
            "audit_trail": [
                {
                    "step": 1,
                    "action": "Provjera valjanosti ugovora",
                    "legal_basis": "čl. 550. ZOO",
                    "citation": "NN 35/05",
                    "reasoning": "Ugovor je sklopljen u pisanoj formi što je uvjet valjanosti",
                    "confidence": "VISOKO",
                    "timestamp": "2025-01-15T10:00:00Z"
                },
                {
                    "step": 2,
                    "action": "Provjera otkaznog roka",
                    "legal_basis": "čl. 576. ZOO",
                    "citation": "NN 35/05",
                    "reasoning": "Otkazni rok od 30 dana je poštivan",
                    "confidence": "VISOKO",
                    "timestamp": "2025-01-15T10:00:01Z"
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<LegalStrategyResult>(json, SnakeCaseOptions);

        Assert.NotNull(result);
        Assert.Equal("Raskid ugovora o najmu", result!.KeyLegalIssue);
        Assert.Equal(68, result.WinProbability);
        Assert.Equal(2, result.AuditTrail.Count);
        Assert.Equal("čl. 550. ZOO", result.AuditTrail[0].LegalBasis);
        Assert.Equal("čl. 576. ZOO", result.AuditTrail[1].LegalBasis);
        Assert.All(result.AuditTrail, e => Assert.Equal("VISOKO", e.Confidence));
    }
}
