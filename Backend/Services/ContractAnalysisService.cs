using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// AI Contract Analyzer — "Paranoid Croatian Lawyer"
/// Analyzes real estate contracts (Predugovor / Ugovor) for legal risks
/// using GPT-4o with a surgically precise legal prompt.
/// 
/// Unlike valuation (guessing prices), contract analysis is pure text + logic —
/// exactly where LLMs excel.
/// </summary>
public class ContractAnalysisService
{
    private readonly ChatClient _chatClient;  // Heavy model — legal reasoning
    private readonly ILogger<ContractAnalysisService> _logger;

    private const string SystemPrompt = """
        TI SI: Iskusni hrvatski odvjetnik specijaliziran za stvarno i obvezno pravo, s 20 godina iskustva u prometu nekretninama.
        Tvoj stav je "paranoidni zaštitnik kupca". Tvoj cilj je pronaći svaku, pa i najmanju pravnu zamku koja bi mogla oštetiti tvog klijenta.

        TVOJ ZADATAK:
        Detaljno analiziraj priloženi tekst (Nacrt Ugovora ili Predugovora) i identificiraj rizike prema Zakonu o obveznim odnosima (ZOO) i Zakonu o zemljišnim knjigama (ZZK).

        STRUKTURA ANALIZE (Moraš vratiti odgovor u JSON formatu):

        1. **VRSTA DOKUMENTA:**
           - Prepoznaj je li ovo "Predugovor" (samo kapara i rok) ili "Glavni Ugovor" (isplata i prijenos vlasništva), "Aneks ugovora", ili nešto drugo.

        2. **🔴 KRITIČNE "CRVENE ZASTAVICE" (Deal-Breakers):**
           - Nedostatak "Tabularne izjave" (Clausula Intabulandi) ili uvjetna tabularna izjava koja nije vezana isključivo uz isplatu cijene.
           - Nejasno definiran rok isplate ili rok predaje u posjed (stupanje u posjed).
           - Nedostatak jamstva prodavatelja da je nekretnina slobodna od tereta i prava trećih osoba (zaštita od evikcije).
           - Ako je nekretnina bračna stečevina, nedostaje li suglasnost supružnika?
           - Nedostatak OIB-a stranaka.
           - Nedostatak točne oznake čestice (katastarska općina, broj čestice, broj uloška/ZK tijela) — bez toga se ugovor ne može provesti u gruntovnici.
           - Nejasna cijena ili način plaćanja.

        3. **🟡 UPOZORENJA I NEDOSTACI (Negotiation Points):**
           - Definiranje kapare: Je li jasno da je to "odustatnina"? Što ako prodavatelj odustane (vraća li dvostruko)?
           - Tko snosi troškove ovjere i poreza (Porez na promet nekretnina)? (Standard je kupac, ali provjeri piše li drugačije).
           - Energetski certifikat: Spominje li se predaja certifikata?
           - Režije: Je li definiran "rez" režija na dan stupanja u posjed?
           - Nedostatak ugovorne kazne za kašnjenje s predajom.
           - Rokovi — jesu li razumni ili predugi/prekratki?

        4. **✅ POZITIVNE STRANE:**
           - Što je dobro i jasno definirano u korist kupca.

        5. **📝 PRIJEDLOG IZMJENA (Konkretni članci):**
           - Za svaki rizik, napiši točan tekst članka kako bi trebao glasiti da zaštiti kupca.

        UPUTE ZA RAZMIŠLJANJE:
        - Ako je tekst nejasan zbog lošeg skeniranja (OCR), upozori korisnika.
        - Ako nedostaje OIB ili točna oznaka čestice (katastarska općina, broj čestice, broj uloška), označi to kao kritičan rizik jer se ugovor neće moći provesti u gruntovnici.
        - Budi strog. Bolje da klijent odustane nego da izgubi novac.
        - Koristi profesionalni, ali razumljiv hrvatski jezik.
        - Citiraj sporne dijelove ugovora kad god je moguće.

        VAŽNO — FORMAT ODGOVORA:
        Vrati ISKLJUČIVO čisti JSON (bez markdown fencing-a), u ovoj strukturi:
        {
          "document_type": "Predugovor / Glavni Ugovor / ...",
          "summary": "Kratki opis ugovora u jednoj rečenici",
          "overall_risk": "Visok / Srednji / Nizak",
          "risk_score": 75,
          "red_flags": [
            {
              "title": "Kratki naziv rizika",
              "description": "Detaljno objašnjenje zašto je ovo opasno",
              "quoted_text": "Citirani tekst iz ugovora (ako postoji)",
              "legal_basis": "čl. 316. ZOO"
            }
          ],
          "warnings": [
            {
              "title": "Kratki naziv upozorenja",
              "description": "Objašnjenje",
              "quoted_text": "Citirani tekst (opcionalno)",
              "legal_basis": "čl. XX. ZZK (opcionalno)"
            }
          ],
          "positives": ["Pozitivna strana 1", "Pozitivna strana 2"],
          "suggested_amendments": [
            {
              "article_reference": "Članak X.",
              "current_text": "Trenutni tekst članka",
              "proposed_text": "Predloženi tekst koji štiti kupca",
              "rationale": "Zašto je ova izmjena bitna"
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
    /// Analyze a contract text and return structured legal analysis.
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
                Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika. Prije potpisa konzultirajte se sa stručnjakom."
            };
        }

        // Truncate very large documents to stay within token limits
        string text = contractText.Length > 15000 ? contractText[..15000] : contractText;

        try
        {
            _logger.LogInformation("Contract analysis: {Len} chars", text.Length);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(
                    $"""
                    Molim te, napravi stručnu pravnu analizu ovog teksta ugovora.
                    Ako nađeš rizičan članak, citiraj ga i objasni zašto je opasan.
                    Budi posebno pažljiv na tabularne izjave, rokove, oznake čestica i OIB-eve.
                    
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

            _logger.LogDebug("Contract AI response: {Json}", json.Length > 500 ? json[..500] + "…" : json);

            return ParseResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contract analysis failed");
            return new ContractAnalysisResult
            {
                DocumentType = "Greška",
                Summary = $"AI analiza nije uspjela: {ex.Message}",
                OverallRisk = "Nepoznat",
                RiskScore = 0,
                Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika. Prije potpisa konzultirajte se sa stručnjakom."
            };
        }
    }

    private ContractAnalysisResult ParseResponse(string json)
    {
        var result = new ContractAnalysisResult
        {
            Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika. Prije potpisa konzultirajte se sa stručnjakom."
        };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Document type
        result.DocumentType = GetString(root, "document_type", "Nepoznato");
        result.Summary = GetString(root, "summary", "");
        result.OverallRisk = GetString(root, "overall_risk", "Srednji");
        result.RiskScore = root.TryGetProperty("risk_score", out var rs) && rs.ValueKind == JsonValueKind.Number
            ? rs.GetInt32() : 50;

        // Red flags
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

        // Warnings
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

        // Positives
        if (root.TryGetProperty("positives", out var pArray) && pArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pArray.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    result.Positives.Add(item.GetString() ?? "");
            }
        }

        // Suggested amendments
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

        _logger.LogInformation("Contract parsed: type={Type}, risk={Risk}({Score}), flags={Flags}, warnings={Warnings}",
            result.DocumentType, result.OverallRisk, result.RiskScore,
            result.RedFlags.Count, result.Warnings.Count);

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
