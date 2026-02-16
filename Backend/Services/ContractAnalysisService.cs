using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V49: AI Contract Auditor — "The Legal Auditor"
/// Two-pass architecture:
///   Pass 1 (AI): Structured audit checklist — mandatory elements, cross-article analysis, missing clauses
///   Pass 2 (Engine): Deterministic risk scoring from audit findings
/// 
/// Replaces the old "free thinking" approach with a strict legal auditor methodology.
/// </summary>
public class ContractAnalysisService
{
    private readonly ChatClient _chatClient;  // Heavy model — legal reasoning
    private readonly ILogger<ContractAnalysisService> _logger;

    private const string Disclaimer =
        "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika sukladno " +
        "čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11). " +
        "Obrada osobnih podataka iz ugovora provodi se sukladno Uredbi (EU) 2016/679 (GDPR). " +
        "Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu i odgovoran je za " +
        "anonimizaciju osjetljivih podataka osim ako je izričita privola dana. " +
        "Prije potpisa konzultirajte se sa stručnjakom.";

    // ══════════════════════════════════════════════════════════
    //  V49: AUDITOR PROMPT — Strict legal checklist methodology
    // ══════════════════════════════════════════════════════════
    private const string AuditorPrompt = """
        PERSONA:
        Ti si PRAVNI REVIZOR ugovora o nekretninama s doktoratom iz pravnih znanosti (dr.sc.iur.)
        i 25 godina iskustva u hrvatskom pravosuđu. Radiš kao forenzički auditor ugovora —
        ne "čitaš" ugovor, nego ga KONTROLIRAŠ prema striktnoj metodologiji.

        PRAVNA PRECIZNOST:
        - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno",
          "pretpostavlja se", "postoji mogućnost".
        - UMJESTO toga koristi: "Na temelju dostavljenog ugovora utvrđeno je...",
          "Sukladno čl. X. ZOO...", "Iz ugovora proizlazi...",
          "Na temelju dostavljenog dokumenta nije moguće utvrditi...".
        - UVIJEK uz zakonski članak navedi NN oznaku.

        ZAKONSKI OKVIR (NN reference):
        - ZOO = Zakon o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)
        - ZZK = Zakon o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17)
        - ZV = Zakon o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14)
        - ObZ = Obiteljski zakon (NN 103/15, 98/19, 47/20)
        - Zakon o osobnom identifikacijskom broju (NN 60/08, 0/12)
        - Zakon o gradnji (NN 153/13, 20/17, 39/19, 125/19)

        ████████████████████████████████████████████████████████
        METODOLOGIJA — OBVEZNA 4 KORAKA REVIZIJE
        ████████████████████████████████████████████████████████

        KORAK 1: PROVJERA TERETA I OSIGURANJA
        Za svaku od sljedećih točaka odgovori sa STATUS i NALAZ:
        a) Postoji li teret na nekretnini (hipoteka, služnost, plodouživanje)?
        b) Ako postoji teret — postoji li mehanizam brisanja s rokom?
        c) Postoji li mehanizam osiguranja kupca (escrow, fiducija, uvjetna isplata)?
        d) Jesu li rokovi plaćanja i brisanja tereta međusobno povezani i usklađeni?
        e) Je li isplata uvjetovana brisanjem tereta?

        KORAK 2: PROVJERA KAPARE I PLAĆANJA
        a) Je li kapara simetrično uređena za obje strane (dvostruko vraćanje)?
        b) Je li jasno je li kapara ili odustatnina (čl. 303. vs čl. 306. ZOO)?
        c) Postoje li posljedice neispunjenja za OBJE strane?
        d) Ide li isplata direktno prodavatelju ili kroz zaštićeni mehanizam?
        e) Postoji li zaštita kupca prije brisanja tereta?

        KORAK 3: PROVJERA PRIJENOSA VLASNIŠTVA
        a) Je li clausula intabulandi bezuvjetna?
        b) Je li prijenos moguć bez ikakvih tereta?
        c) Postoji li OIB obiju stranaka?
        d) Postoji li precizna oznaka nekretnine (k.o., k.č.br., ZK uložak)?
        e) Ako je nekretnina bračna stečevina — postoji li suglasnost supružnika?

        KORAK 4: PROVJERA ZAŠTITNIH KLAUZULA
        a) Postoji li izjava o ispražnjenju nekretnine i rok?
        b) Postoji li ugovorna kazna za zakašnjenje?
        c) Postoji li klauzula o naknadi štete?
        d) Postoji li raskidni uvjet?
        e) Postoji li energetski certifikat?
        f) Postoji li razgraničenje režijskih troškova?

        ████████████████████████████████████████████████████████
        OBVEZNA CROSS-ARTICLE ANALIZA
        ████████████████████████████████████████████████████████

        Ne analiziraj članke izolirano!
        Obavezno provjeri jesu li sljedeći članci međusobno usklađeni:
        - Članci o plaćanju ↔ članci o hipoteci/teretu
        - Članci o plaćanju ↔ clausula intabulandi
        - Članci o hipoteci ↔ clausula intabulandi
        - Rokovi plaćanja ↔ rokovi predaje u posjed ↔ rokovi brisanja tereta
        Ako NISU usklađeni, označi to kao STRUKTURNI RIZIK.

        ████████████████████████████████████████████████████████
        OBVEZNE ZAŠTITNE KLAUZULE — PROVJERA PRISUTNOSTI
        ████████████████████████████████████████████████████████

        Provjeri postoje li sljedeće zaštitne klauzule u ugovoru:
        1. Escrow ili uvjetna isplata — KRITIČNO
        2. Mehanizam brisanja hipoteke s rokom — KRITIČNO (ako postoji hipoteka)
        3. Dvostruka kapara (čl. 304. st. 2. ZOO) — KRITIČNO
        4. Raskidni uvjet — PREPORUČENO
        5. Klauzula o naknadi štete — PREPORUČENO
        6. Izjava o ispražnjenju nekretnine — PREPORUČENO
        7. Ugovorna kazna za zakašnjenje — PREPORUČENO
        8. Energetski certifikat — PREPORUČENO
        9. Razgraničenje režijskih troškova — PREPORUČENO
        10. Suglasnost supružnika (ako primjenjivo) — KRITIČNO

        Ako neka nedostaje, navedi je kao PROPUST — čak i ako ugovor izgleda uredno.

        ████████████████████████████████████████████████████████
        ZABRANA GENERIČKIH RIZIKA
        ████████████████████████████████████████████████████████

        Zabranjeno je navoditi generičke rizike poput:
        - "mogući sudski spor"
        - "nejasnoće u ugovoru"
        - "potencijalni problemi"
        - "možda postoji rizik"

        Svaki rizik MORA biti:
        1. Vezan uz KONKRETAN članak ugovora (citiraj ga)
        2. Citirati TOČAN dio teksta iz ugovora (quoted_text)
        3. Objasniti KONKRETNU pravnu posljedicu (ne apstraktnu)
        4. Koristiti Syllogismus Iuridicus: MAIOR (norma) → MINOR (činjenica) → CONCLUSIO (posljedica)

        ████████████████████████████████████████████████████████

        GDPR NAPOMENA:
        Obrada osobnih podataka sadržanih u ugovoru (OIB, imena stranaka, adrese)
        zahtijeva izričitu privolu korisnika sukladno Uredbi (EU) 2016/679 (GDPR).

        VAŽNO — FORMAT ODGOVORA:
        Vrati ISKLJUČIVO čisti JSON (bez markdown fencing-a) u ovoj strukturi:
        {
          "document_type": "Predugovor / Glavni Ugovor / Aneks",
          "summary": "Kratki opis ugovora u jednoj rečenici",

          "checklist": [
            {
              "category": "Tereti i hipoteke",
              "check": "Postoji li teret na nekretnini?",
              "status": "UREĐENO | DJELOMIČNO | NIJE_UREĐENO",
              "risk": "NIZAK | SREDNJI | VISOK",
              "finding": "Konkretno što je utvrđeno u ugovoru",
              "article_ref": "čl. 7",
              "recommendation": "Preporuka — obavezno ako status ≠ UREĐENO, null ako UREĐENO"
            }
          ],

          "cross_article_issues": [
            {
              "title": "Naslov strukturnog rizika",
              "linked_articles": ["čl. 3", "čl. 7", "čl. 10"],
              "description": "Opis neusklađenosti s pravnim silogizmom",
              "risk": "NIZAK | SREDNJI | VISOK"
            }
          ],

          "mandatory_clauses": [
            {
              "clause": "Escrow / uvjetna isplata",
              "present": false,
              "importance": "KRITIČNO | PREPORUČENO",
              "description": "Objašnjenje zašto je ova klauzula važna ili što nedostaje"
            }
          ],

          "red_flags": [
            {
              "title": "Kratki naziv rizika",
              "description": "MAIOR: [pravna norma]. MINOR: [činjenica iz ugovora]. CONCLUSIO: [pravna posljedica].",
              "quoted_text": "Citirani tekst iz ugovora (OBAVEZNO)",
              "legal_basis": "čl. 303. st. 1. ZOO"
            }
          ],
          "warnings": [
            {
              "title": "Kratki naziv upozorenja",
              "description": "Objašnjenje s pravnom osnovom",
              "quoted_text": "Citirani tekst (OBAVEZNO)",
              "legal_basis": "čl. XX. ZOO"
            }
          ],
          "positives": ["Pozitivna strana 1", "Pozitivna strana 2"],
          "suggested_amendments": [
            {
              "article_reference": "Članak X.",
              "current_text": "Trenutni tekst članka",
              "proposed_text": "Predloženi tekst koji štiti kupca",
              "rationale": "Ratio legis — zašto je ova izmjena pravno nužna"
            }
          ]
        }
        """;

    public ContractAnalysisService(ChatClientProvider clients, ILogger<ContractAnalysisService> logger)
    {
        _chatClient = clients.Heavy;  // gpt-5.2 — legal contract analysis
        _logger = logger;
    }

    /// <summary>
    /// V49: Two-pass contract analysis.
    /// Pass 1: AI audit — structured checklist + cross-article + mandatory clauses
    /// Pass 2: Deterministic risk scoring from audit findings
    /// </summary>
    public async Task<ContractAnalysisResult> AnalyzeContractAsync(string contractText)
    {
        if (string.IsNullOrWhiteSpace(contractText) || contractText.Length < 50)
        {
            return new ContractAnalysisResult
            {
                DocumentType = "Nepoznato",
                Summary = "Tekst je prekratak za analizu.",
                OverallRisk = "Nepoznat",
                RiskScore = 0,
                Disclaimer = Disclaimer
            };
        }

        // Truncate very large documents to stay within token limits
        string text = contractText.Length > 15000 ? contractText[..15000] : contractText;

        try
        {
            _logger.LogInformation("V49 Contract audit: {Len} chars", text.Length);

            // ── PASS 1: AI AUDIT ──
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(AuditorPrompt),
                new UserChatMessage(
                    $"""
                    PROVEDI STRUČNU PRAVNU REVIZIJU ovog ugovora.
                    Slijedi sva 4 koraka revizije, provjeri cross-article usklađenost,
                    i provjeri sve obvezne zaštitne klauzule.
                    Svaki rizik MORA citirati konkretan tekst iz ugovora.

                    TEKST UGOVORA:
                    {text}
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,  // Law does not tolerate creativity!
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            _logger.LogDebug("V49 Contract AI audit response: {Len} chars", json.Length);

            // ── Parse AI audit output ──
            var result = ParseAuditResponse(json);

            // ── PASS 2: DETERMINISTIC RISK SCORING ──
            ComputeRiskScore(result);

            _logger.LogInformation(
                "V49 Contract audit complete: type={Type}, risk={Risk}({Score}), " +
                "checklist={CL}, cross={CA}, mandatory={MC}, flags={Flags}, warnings={Warn}",
                result.DocumentType, result.OverallRisk, result.RiskScore,
                result.Checklist.Count, result.CrossArticleIssues.Count,
                result.MandatoryClauses.Count, result.RedFlags.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "V49 Contract audit failed");
            return new ContractAnalysisResult
            {
                DocumentType = "Greška",
                Summary = $"AI analiza nije uspjela: {ex.Message}",
                OverallRisk = "Nepoznat",
                RiskScore = 0,
                Disclaimer = Disclaimer
            };
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PASS 2: DETERMINISTIC RISK SCORING ENGINE
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// V49: Compute risk score mathematically from audit findings.
    /// No AI guessing — pure formula.
    /// </summary>
    private void ComputeRiskScore(ContractAnalysisResult result)
    {
        int score = 0;
        var breakdown = new List<string>();

        // ── Checklist items ──
        foreach (var item in result.Checklist)
        {
            switch (item.Risk?.ToUpperInvariant())
            {
                case "VISOK":
                    score += 15;
                    breakdown.Add($"+15 Checklist VISOK: {item.Check}");
                    break;
                case "SREDNJI":
                    score += 7;
                    breakdown.Add($"+7 Checklist SREDNJI: {item.Check}");
                    break;
                // NIZAK = 0 points
            }
        }

        // ── Mandatory clauses missing ──
        foreach (var mc in result.MandatoryClauses)
        {
            if (!mc.Present)
            {
                int pts = mc.Importance?.ToUpperInvariant() == "KRITIČNO" ? 15 : 5;
                score += pts;
                breakdown.Add($"+{pts} Nedostaje: {mc.Clause} ({mc.Importance})");
            }
        }

        // ── Cross-article structural issues ──
        foreach (var ca in result.CrossArticleIssues)
        {
            int pts = ca.Risk?.ToUpperInvariant() == "VISOK" ? 12 : 5;
            score += pts;
            breakdown.Add($"+{pts} Strukturni: {ca.Title}");
        }

        // ── Red flags ──
        score += result.RedFlags.Count * 10;
        if (result.RedFlags.Count > 0)
            breakdown.Add($"+{result.RedFlags.Count * 10} Crvene zastavice ({result.RedFlags.Count}×10)");

        // ── Warnings ──
        score += result.Warnings.Count * 3;
        if (result.Warnings.Count > 0)
            breakdown.Add($"+{result.Warnings.Count * 3} Upozorenja ({result.Warnings.Count}×3)");

        // ── Positives reduce risk slightly ──
        int bonus = Math.Min(result.Positives.Count * 2, 10);
        if (bonus > 0)
        {
            score -= bonus;
            breakdown.Add($"-{bonus} Pozitivne strane ({result.Positives.Count}×2, max -10)");
        }

        // Clamp to 0-100
        score = Math.Clamp(score, 0, 100);

        // Determine risk level
        result.RiskScore = score;
        result.OverallRisk = score switch
        {
            >= 60 => "Visok",
            >= 30 => "Srednji",
            _ => "Nizak"
        };
        result.RiskScoreBreakdown = breakdown;
        result.Disclaimer = Disclaimer;

        _logger.LogInformation("V49 Risk score: {Score}/100 ({Level}) from {Items} breakdown items",
            score, result.OverallRisk, breakdown.Count);
    }

    // ══════════════════════════════════════════════════════════
    //  JSON PARSER — AI audit output → structured models
    // ══════════════════════════════════════════════════════════
    private ContractAnalysisResult ParseAuditResponse(string json)
    {
        var result = new ContractAnalysisResult { Disclaimer = Disclaimer };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Document metadata
        result.DocumentType = GetString(root, "document_type", "Nepoznato");
        result.Summary = GetString(root, "summary", "");

        // ── V49: Checklist ──
        if (root.TryGetProperty("checklist", out var clArray) && clArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in clArray.EnumerateArray())
            {
                result.Checklist.Add(new ChecklistItem
                {
                    Category = GetString(item, "category"),
                    Check = GetString(item, "check"),
                    Status = GetString(item, "status", "NIJE_UREĐENO"),
                    Risk = GetString(item, "risk", "SREDNJI"),
                    Finding = GetString(item, "finding"),
                    ArticleRef = GetStringOrNull(item, "article_ref"),
                    Recommendation = GetStringOrNull(item, "recommendation")
                });
            }
        }

        // ── V49: Cross-article issues ──
        if (root.TryGetProperty("cross_article_issues", out var caArray) && caArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in caArray.EnumerateArray())
            {
                var issue = new CrossArticleIssue
                {
                    Title = GetString(item, "title"),
                    Description = GetString(item, "description"),
                    Risk = GetString(item, "risk", "SREDNJI")
                };
                if (item.TryGetProperty("linked_articles", out var laArray) && laArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var la in laArray.EnumerateArray())
                    {
                        if (la.ValueKind == JsonValueKind.String)
                            issue.LinkedArticles.Add(la.GetString() ?? "");
                    }
                }
                result.CrossArticleIssues.Add(issue);
            }
        }

        // ── V49: Mandatory clauses ──
        if (root.TryGetProperty("mandatory_clauses", out var mcArray) && mcArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in mcArray.EnumerateArray())
            {
                result.MandatoryClauses.Add(new MandatoryClauseCheck
                {
                    Clause = GetString(item, "clause"),
                    Present = item.TryGetProperty("present", out var p) && p.ValueKind == JsonValueKind.True,
                    Importance = GetString(item, "importance", "PREPORUČENO"),
                    Description = GetString(item, "description")
                });
            }
        }

        // ── Red flags ──
        if (root.TryGetProperty("red_flags", out var rfArray) && rfArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rfArray.EnumerateArray())
            {
                result.RedFlags.Add(new ContractIssue
                {
                    Title = GetString(item, "title"),
                    Description = GetString(item, "description"),
                    QuotedText = GetStringOrNull(item, "quoted_text"),
                    LegalBasis = GetStringOrNull(item, "legal_basis")
                });
            }
        }

        // ── Warnings ──
        if (root.TryGetProperty("warnings", out var wArray) && wArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in wArray.EnumerateArray())
            {
                result.Warnings.Add(new ContractIssue
                {
                    Title = GetString(item, "title"),
                    Description = GetString(item, "description"),
                    QuotedText = GetStringOrNull(item, "quoted_text"),
                    LegalBasis = GetStringOrNull(item, "legal_basis")
                });
            }
        }

        // ── Positives ──
        if (root.TryGetProperty("positives", out var pArray) && pArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    result.Positives.Add(item.GetString() ?? "");
            }
        }

        // ── Suggested amendments ──
        if (root.TryGetProperty("suggested_amendments", out var saArray) && saArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in saArray.EnumerateArray())
            {
                result.SuggestedAmendments.Add(new SuggestedAmendment
                {
                    ArticleReference = GetString(item, "article_reference"),
                    CurrentText = GetString(item, "current_text"),
                    ProposedText = GetString(item, "proposed_text"),
                    Rationale = GetString(item, "rationale")
                });
            }
        }

        _logger.LogInformation(
            "V49 Audit parsed: type={Type}, checklist={CL}, cross={CA}, mandatory={MC}, flags={RF}, warnings={W}",
            result.DocumentType, result.Checklist.Count, result.CrossArticleIssues.Count,
            result.MandatoryClauses.Count, result.RedFlags.Count, result.Warnings.Count);

        return result;
    }

    private static string GetString(JsonElement el, string prop, string fallback = "")
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;
    }

    private static string? GetStringOrNull(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
    }
}
