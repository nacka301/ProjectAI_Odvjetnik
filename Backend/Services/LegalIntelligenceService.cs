using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V33: Legal Intelligence Service — RAG-simulated Court Practice Analysis.
/// 
/// Uses GPT-4o's extensive training data on Croatian legal texts as a "virtual vector DB".
/// The AI acts as both the retrieval engine (finding relevant precedents) and the synthesis
/// engine (analyzing how they apply to the current case).
/// 
/// Two main capabilities:
/// 1. GetLegalFeedAsync() — "Legal Intelligence Feed" for the dashboard
/// 2. AnalyzeLegalStrategyAsync() — "Provjeri ishod na sudu" killer feature
/// 3. GetRelevantPrecedentsAsync() — Precedent sidebar for any case context
/// </summary>
public class LegalIntelligenceService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<LegalIntelligenceService> _logger;

    // ── System Prompt: Legal Feed Generator ──
    private const string FeedSystemPrompt = """
        TI SI: Pravni informacijski sustav specijaliziran za praćenje sudske prakse u Republici Hrvatskoj, 
        s naglaskom na nekretnine, obvezno pravo i zemljišnoknjižno pravo.

        ZADATAK: Generiraj listu najvažnijih recentnih presuda i pravnih stajališta iz prakse 
        Vrhovnog suda RH (VSRH), Visokog trgovačkog suda RH (VTS) i županijskih sudova, 
        koje su relevantne za odvjetnike koji se bave prometom nekretnina.

        PODRUČJA KOJA POKRIVAJ:
        - Kupoprodaja nekretnina (kapara, odustatnina, raskid ugovora)
        - Zemljišnoknjižno pravo (uknjižba, brisanje tereta, plombe)
        - Služnosti (stvarne i osobne)
        - Hipoteke i ovrhe na nekretninama
        - Zakup i najam
        - Građevinski sporovi (skriveni nedostaci, jamstvo)
        - Nezakonita gradnja i legalizacija
        - Suvlasničke zajednice i razvrgnuće

        PRAVILA:
        - Generiraj realistične presude temeljene na USTALJENIM pravnim stajalištima hrvatskih sudova
        - Svaka presuda mora imati pravnu osnovu (članak zakona)
        - Koristi STVARNE formate oznaka presuda (Rev-xxx/yy-z, Gž-xxx/yyyy, Pž-xxx/yy)
        - Budi praktičan — navedi zašto je ovo važno za odvjetnika u praksi
        - Koristi profesionalni hrvatski pravni jezik

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON:
        {
          "feed_items": [
            {
              "case_number": "Rev-1234/22-2",
              "court": "Vrhovni sud RH",
              "date": "15.03.2024.",
              "topic": "Kapara kao odustatnina",
              "headline": "Kratki naslov presude",
              "takeaway": "Što ovo znači za odvjetnika u praksi",
              "impact": "VISOK / SREDNJI / NIZAK",
              "area_of_law": "Obvezno pravo"
            }
          ]
        }
        """;

    // ── System Prompt: Legal Strategy Analyzer ──
    private const string StrategySystemPrompt = """
        TI SI: Iskusni hrvatski odvjetnik-strateg s 25 godina iskustva pred hrvatskim sudovima.
        Specijaliziran si za nekretnine, obvezno pravo i stvarno pravo.
        Tvoj pristup je analitičan, pragmatičan i temeljen na sudskoj praksi.

        TVOJ ZADATAK:
        Na temelju priloženog konteksta predmeta (činjenice, ugovor, kronologija, ZK izvadak):

        1. IDENTIFICIRAJ KLJUČNO PRAVNO PITANJE
           - Što je bit spora? (npr. "ništetnost odredbe o zateznim kamatama", "raskid zbog neispunjenja")
           - Koji zakoni su relevantni? (ZOO, ZZK, ZV, OZ)

        2. PRONAĐI RELEVANTNE PRESUDE
           - Citiraj presude VSRH, VTS i županijskih sudova koje se tiču istog pravnog pitanja
           - Za svaku presudu navedi: broj, sud, datum, sažetak, ključno pravno načelo
           - Označi je li presuda POVOLJNA, NEPOVOLJNA ili NEUTRALNA za klijenta
           - Koristi REALISTIČNE formate oznaka (Rev-xxx/yy-z, Gž-xxx/yyyy)

        3. PROCIJENI VJEROJATNOST USPJEHA
           - Na temelju sličnosti s citiranom praksom, procijeni postotak uspjeha (0-100%)
           - Uzmi u obzir regionalne razlike (npr. sudovi u Dalmaciji vs. u Zagrebu)
           - Budi konzervativan — bolje podcijeniti šanse nego dati lažnu nadu

        4. IDENTIFICIRAJ RIZIKE
           - Procesni rizici (zastara, nedostatak dokaza, troškovi)
           - Materijalni rizici (slaba pravna pozicija, protupresude)
           - Praktični rizici (trajanje postupka, naplativost)

        5. DAVANJE STRATEŠKIH PREPORUKA
           - Konkretni koraci (nagodba, tužba, žalba, prijedlog za osiguranje)
           - Rokovi i prioriteti
           - Kad preporučiti nagodbu vs. parnicu

        UPUTE ZA RAZMIŠLJANJE:
        - Budi iskren — ako je slučaj slab, reci to direktno
        - Procjena troškova u EUR (odvjetnički, sudski, vještačenje)
        - Procjena trajanja (prvostupanjski + žalbeni postupak)
        - Ako nemaš dovoljno informacija, navedi što još treba istražiti
        - UVIJEK navedi disclaimer da ovo nije zamjena za formalni pravni savjet

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON:
        {
          "key_legal_issue": "Opis ključnog pravnog pitanja",
          "sub_issues": ["Potpitanje 1", "Potpitanje 2"],
          "precedents": [
            {
              "case_number": "Rev-1234/22-2",
              "court": "Vrhovni sud RH",
              "date": "15.03.2022.",
              "summary": "Sažetak odluke",
              "legal_principle": "Ključno pravno načelo",
              "relevance_to_case": "Kako se odnosi na ovaj slučaj",
              "alignment": "POVOLJNA / NEPOVOLJNA / NEUTRALNA",
              "legal_basis": "čl. 368. ZOO"
            }
          ],
          "win_probability": 65,
          "win_probability_label": "Srednja",
          "probability_reasoning": "Obrazloženje procjene",
          "risks": [
            {
              "title": "Naziv rizika",
              "description": "Opis",
              "severity": "VISOK / SREDNJI / NIZAK",
              "mitigation": "Kako umanjiti rizik"
            }
          ],
          "strengths": ["Snaga pozicije 1", "Snaga pozicije 2"],
          "recommendations": [
            {
              "action": "Što poduzeti",
              "rationale": "Zašto",
              "priority": "HITNO / VAŽNO / PREPORUČENO",
              "timing": "Rok ili trenutak"
            }
          ],
          "strategy_summary": "Profesionalni sažetak strategije",
          "estimated_duration": "12-18 mjeseci (prvostupanjski) + 6-12 mjeseci (žalba)",
          "estimated_cost": "3.000 - 8.000 EUR (odvjetnički + sudski troškovi)"
        }
        """;

    // ── System Prompt: Precedent Finder (for sidebar) ──
    private const string PrecedentSystemPrompt = """
        TI SI: Pravni istraživač specijaliziran za pretragu sudske prakse u RH.
        
        ZADATAK: Na temelju priloženog pravnog konteksta, pronađi 3-5 najrelevantnijih 
        presuda hrvatskih sudova (VSRH, VTS, županijski sudovi) koje se tiču istog ili 
        sličnog pravnog pitanja.

        PRAVILA:
        - Koristi REALISTIČNE formate oznaka presuda
        - Budi koncizan i praktičan
        - Fokusiraj se na presude koje bi odvjetniku pomogle u argumentaciji
        - Navedi za svaku je li povoljna ili nepovoljna za tipičnu poziciju kupca

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON:
        {
          "legal_issue": "Identificirano pravno pitanje",
          "precedents": [
            {
              "case_number": "Rev-xxx/yy-z",
              "court": "Vrhovni sud RH",
              "date": "DD.MM.YYYY.",
              "summary": "Sažetak",
              "legal_principle": "Ključno načelo",
              "relevance_to_case": "Relevantnost",
              "alignment": "POVOLJNA / NEPOVOLJNA / NEUTRALNA",
              "legal_basis": "čl. XX. ZOO/ZZK/ZV"
            }
          ]
        }
        """;

    public LegalIntelligenceService(ChatClient chatClient, ILogger<LegalIntelligenceService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    //  1. LEGAL FEED — Dashboard widget
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Generate the Legal Intelligence Feed for the dashboard.
    /// Returns recent relevant court decisions for real estate lawyers.
    /// </summary>
    public async Task<List<LegalFeedItem>> GetLegalFeedAsync(int count = 5)
    {
        try
        {
            _logger.LogInformation("Generating Legal Intelligence Feed ({Count} items)", count);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(FeedSystemPrompt),
                new UserChatMessage(
                    $"""
                    Generiraj {count} najvažnijih recentnih presuda i pravnih stajališta 
                    iz prakse hrvatskih sudova koje su relevantne za odvjetnike u prometu nekretninama.
                    
                    Fokusiraj se na raznolikost tema:
                    - Barem jedna presuda o kupoprodaji (kapara, raskid, ništetnost)
                    - Barem jedna presuda o zemljišnoknjižnom pravu (uknjižba, tereti)
                    - Barem jedna presuda o služnostima ili ovrhama
                    - Ostale po relevantnosti
                    
                    Presude trebaju biti iz razdoblja 2022.-2025.
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.3f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            _logger.LogDebug("Legal feed response: {Len} chars", json.Length);

            return ParseFeedResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legal feed generation failed");
            return [];
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  2. LEGAL STRATEGY — "Provjeri ishod na sudu"
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// The killer feature: Analyze a complete case and predict court outcome.
    /// Combines contract analysis, chronology, ZK data, and court practice.
    /// </summary>
    public async Task<LegalStrategyResult> AnalyzeLegalStrategyAsync(LegalStrategyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaseDescription) &&
            string.IsNullOrWhiteSpace(request.ContractText) &&
            string.IsNullOrWhiteSpace(request.ChronologySummary) &&
            string.IsNullOrWhiteSpace(request.ZkSummary))
        {
            return new LegalStrategyResult
            {
                KeyLegalIssue = "Nedovoljno informacija",
                StrategySummary = "Molimo unesite opis slučaja, tekst ugovora, kronologiju ili ZK podatke za analizu.",
                WinProbability = 0,
                WinProbabilityLabel = "Nepoznato"
            };
        }

        try
        {
            // Build comprehensive case context
            var caseContext = BuildCaseContext(request);

            _logger.LogInformation("Legal strategy analysis: {Len} chars context", caseContext.Length);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(StrategySystemPrompt),
                new UserChatMessage(caseContext)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,  // Legal analysis demands precision
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            _logger.LogDebug("Legal strategy response: {Len} chars", json.Length);

            return ParseStrategyResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legal strategy analysis failed");
            return new LegalStrategyResult
            {
                KeyLegalIssue = "Greška u analizi",
                StrategySummary = $"AI analiza nije uspjela: {ex.Message}",
                WinProbability = 0,
                WinProbabilityLabel = "Greška"
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  3. PRECEDENT SIDEBAR — Find precedents for any context
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Find relevant court precedents for a given legal context.
    /// Used by the Precedent Sidebar component.
    /// </summary>
    public async Task<(string LegalIssue, List<CourtPrecedent> Precedents)> GetRelevantPrecedentsAsync(string caseContext)
    {
        if (string.IsNullOrWhiteSpace(caseContext) || caseContext.Length < 20)
        {
            return ("Nedovoljno konteksta", []);
        }

        try
        {
            string truncated = caseContext.Length > 8000 ? caseContext[..8000] : caseContext;

            _logger.LogInformation("Precedent search: {Len} chars context", truncated.Length);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(PrecedentSystemPrompt),
                new UserChatMessage(
                    $"""
                    Pronađi relevantne presude za sljedeći pravni kontekst:

                    {truncated}

                    Pronađi 3-5 presuda koje su najrelevantnije za pravna pitanja u ovom kontekstu.
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            return ParsePrecedentResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Precedent search failed");
            return ("Greška u pretrazi", []);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════

    private static string BuildCaseContext(LegalStrategyRequest request)
    {
        var parts = new List<string>();

        parts.Add($"POZICIJA KLIJENTA: {request.ClientPosition}");

        if (!string.IsNullOrWhiteSpace(request.DesiredOutcome))
            parts.Add($"ŽELJENI ISHOD: {request.DesiredOutcome}");

        if (!string.IsNullOrWhiteSpace(request.CaseDescription))
        {
            string desc = request.CaseDescription.Length > 5000
                ? request.CaseDescription[..5000]
                : request.CaseDescription;
            parts.Add($"OPIS SLUČAJA:\n{desc}");
        }

        if (!string.IsNullOrWhiteSpace(request.ContractText))
        {
            string contract = request.ContractText.Length > 6000
                ? request.ContractText[..6000]
                : request.ContractText;
            parts.Add($"TEKST UGOVORA:\n{contract}");
        }

        if (!string.IsNullOrWhiteSpace(request.ChronologySummary))
        {
            string chrono = request.ChronologySummary.Length > 3000
                ? request.ChronologySummary[..3000]
                : request.ChronologySummary;
            parts.Add($"KRONOLOGIJA SPISA:\n{chrono}");
        }

        if (!string.IsNullOrWhiteSpace(request.ZkSummary))
        {
            string zk = request.ZkSummary.Length > 2000
                ? request.ZkSummary[..2000]
                : request.ZkSummary;
            parts.Add($"ZK IZVADAK (ZEMLJIŠNOKNJIŽNI PODACI):\n{zk}");
        }

        return string.Join("\n\n", parts);
    }

    private List<LegalFeedItem> ParseFeedResponse(string json)
    {
        var items = new List<LegalFeedItem>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("feed_items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    items.Add(new LegalFeedItem
                    {
                        CaseNumber = GetStr(item, "case_number"),
                        Court = GetStr(item, "court"),
                        Date = GetStr(item, "date"),
                        Topic = GetStr(item, "topic"),
                        Headline = GetStr(item, "headline"),
                        Takeaway = GetStr(item, "takeaway"),
                        Impact = GetStr(item, "impact", "SREDNJI"),
                        AreaOfLaw = GetStr(item, "area_of_law")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse legal feed JSON");
        }

        return items;
    }

    private LegalStrategyResult ParseStrategyResponse(string json)
    {
        var result = new LegalStrategyResult();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            result.KeyLegalIssue = GetStr(root, "key_legal_issue");
            result.WinProbability = root.TryGetProperty("win_probability", out var wp) && wp.ValueKind == JsonValueKind.Number
                ? wp.GetInt32() : 50;
            result.WinProbabilityLabel = GetStr(root, "win_probability_label", "Srednja");
            result.ProbabilityReasoning = GetStr(root, "probability_reasoning");
            result.StrategySummary = GetStr(root, "strategy_summary");
            result.EstimatedDuration = GetStr(root, "estimated_duration");
            result.EstimatedCost = GetStr(root, "estimated_cost");

            // Sub-issues
            if (root.TryGetProperty("sub_issues", out var si) && si.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in si.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        result.SubIssues.Add(item.GetString() ?? "");
                }
            }

            // Precedents
            if (root.TryGetProperty("precedents", out var pa) && pa.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pa.EnumerateArray())
                {
                    result.Precedents.Add(new CourtPrecedent
                    {
                        CaseNumber = GetStr(item, "case_number"),
                        Court = GetStr(item, "court"),
                        Date = GetStr(item, "date"),
                        Summary = GetStr(item, "summary"),
                        LegalPrinciple = GetStr(item, "legal_principle"),
                        RelevanceToCase = GetStr(item, "relevance_to_case"),
                        Alignment = GetStr(item, "alignment", "NEUTRALNA"),
                        LegalBasis = GetStr(item, "legal_basis")
                    });
                }
            }

            // Risks
            if (root.TryGetProperty("risks", out var ra) && ra.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ra.EnumerateArray())
                {
                    result.Risks.Add(new LegalRisk
                    {
                        Title = GetStr(item, "title"),
                        Description = GetStr(item, "description"),
                        Severity = GetStr(item, "severity", "SREDNJI"),
                        Mitigation = GetStr(item, "mitigation")
                    });
                }
            }

            // Strengths
            if (root.TryGetProperty("strengths", out var sa) && sa.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in sa.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        result.Strengths.Add(item.GetString() ?? "");
                }
            }

            // Recommendations
            if (root.TryGetProperty("recommendations", out var recs) && recs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in recs.EnumerateArray())
                {
                    result.Recommendations.Add(new StrategicRecommendation
                    {
                        Action = GetStr(item, "action"),
                        Rationale = GetStr(item, "rationale"),
                        Priority = GetStr(item, "priority", "PREPORUČENO"),
                        Timing = GetStr(item, "timing")
                    });
                }
            }

            _logger.LogInformation(
                "Legal strategy parsed: issue='{Issue}', win={Win}%, precedents={P}, risks={R}",
                result.KeyLegalIssue.Length > 50 ? result.KeyLegalIssue[..50] + "…" : result.KeyLegalIssue,
                result.WinProbability, result.Precedents.Count, result.Risks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse legal strategy JSON");
            result.KeyLegalIssue = "Greška u parsiranju";
            result.StrategySummary = "AI je vratio odgovor koji nije bilo moguće obraditi.";
        }

        return result;
    }

    private (string LegalIssue, List<CourtPrecedent> Precedents) ParsePrecedentResponse(string json)
    {
        var precedents = new List<CourtPrecedent>();
        string legalIssue = "";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            legalIssue = GetStr(root, "legal_issue");

            if (root.TryGetProperty("precedents", out var pa) && pa.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pa.EnumerateArray())
                {
                    precedents.Add(new CourtPrecedent
                    {
                        CaseNumber = GetStr(item, "case_number"),
                        Court = GetStr(item, "court"),
                        Date = GetStr(item, "date"),
                        Summary = GetStr(item, "summary"),
                        LegalPrinciple = GetStr(item, "legal_principle"),
                        RelevanceToCase = GetStr(item, "relevance_to_case"),
                        Alignment = GetStr(item, "alignment", "NEUTRALNA"),
                        LegalBasis = GetStr(item, "legal_basis")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse precedent JSON");
        }

        return (legalIssue, precedents);
    }

    private static string GetStr(JsonElement el, string prop, string fallback = "")
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;
    }
}
