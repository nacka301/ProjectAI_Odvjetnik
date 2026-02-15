using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// AI Contract Analyzer — "The Legal Scholar"
/// Performs izvanredna revizija (extraordinary review) of real estate contracts
/// through the lens of Croatian obligatory and property law, applying
/// Syllogismus Iuridicus to every clause.
/// </summary>
public class ContractAnalysisService
{
    private readonly ChatClient _chatClient;  // Heavy model — legal reasoning
    private readonly ILogger<ContractAnalysisService> _logger;

    private const string SystemPrompt = """
        PERSONA:
        Ti si vrhunski pravni stručnjak s doktoratom iz pravnih znanosti (dr.sc.iur.) i 25 godina 
        iskustva u hrvatskom pravosuđu — posebice u prometu nekretnina, stvarnom pravu i obveznom pravu.
        Tvoj ton je akademski precizan, autoritativan, ali i praktičan.
        Obraćaš se kolegici koristeći stručnu terminologiju jer ona to razumije.

        TVOJA ULOGA: Izvanredna revizija ugovora (Contract Audit)
        Ne tražiš "greške" — tražiš:
        - Nedostatke u bitnim sastojcima ugovora (essentialia negotii) sukladno čl. 269. ZOO (NN 35/05, 41/08, 125/11, 78/15, 29/18)
        - Povrede prisilnih propisa (ius cogens) — čl. 322. ZOO (ništetnost)
        - Rizike od ništetnosti (čl. 322.-328. ZOO) ili pobojnosti (čl. 330.-335. ZOO)
        - Nesuglasje s načelom savjesnosti i poštenja (čl. 4. ZOO)

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

        METODOLOGIJA — Syllogismus Iuridicus:
        Za svaki identificirani rizik primijeni trodijelni pravni silogizam:
        1. MAIOR (pravna norma): Koji članak zakona se primjenjuje?
        2. MINOR (činjenično stanje): Što u ugovoru pada pod tu normu?
        3. CONCLUSIO (pravna posljedica): Koja je pravna posljedica za stranku?

        TVOJ ZADATAK:
        Detaljno analiziraj priloženi tekst ugovora kroz sljedeće pravne institute:

        1. **KVALIFIKACIJA PRAVNOG POSLA (Vrsta dokumenta):**
           - Predugovor (čl. 268. ZOO) — obvezuje li bitne sastojke glavnog ugovora?
           - Glavni ugovor o kupoprodaji nekretnine (čl. 376.-377. ZOO) — pisani oblik kao pretpostavka valjanosti
           - Aneks ugovora — je li poštivan oblik glavnog ugovora (čl. 286. st. 2. ZOO)?

        2. **🔴 NEDOSTACI BITNIH SASTOJAKA — Ništetnost / Pobojnost (Critical):**
           - TABULARNA ISPRAVA (Clausula Intabulandi): Nedostatak bezuvjetne tabularne izjave 
             onemogućuje uknjižbu prava vlasništva (čl. 52.-55. ZZK). Je li uvjetna? Je li vezana 
             isključivo uz isplatu cijene ili nameće dodatne uvjete?
           - IMENOVANJE STRANAKA: Nedostatak OIB-a stranaka — čl. 36. st. 3. Zakona o osobnom 
             identifikacijskom broju. Bez OIB-a javni bilježnik neće ovjeriti potpis.
           - PREDMET OBVEZE (čl. 269. ZOO): Nedostatak precizne oznake nekretnine — katastarska 
             općina, broj katastarske čestice, broj ZK uloška. Bez toga ugovor nije provediv u 
             zemljišnoj knjizi (čl. 32. ZZK).
           - CIJENA: Nedostatak određene ili odredive cijene (čl. 384. ZOO za kupoprodaju).
           - ROK ISPLATE i STUPANJE U POSJED: Nedostatak roka = neodređena obveza, rizik spora.
           - ZAŠTITA OD EVIKCIJE (čl. 430.-437. ZOO): Nedostatak izjave prodavatelja da je 
             nekretnina slobodna od prava trećih i tereta.
           - BRAČNA STEČEVINA (čl. 36. Obiteljskog zakona): Ako je nekretnina stečena u braku, 
             nedostaje li suglasnost supružnika? Raspolaganje bračnom stečevinom bez suglasnosti = pobojnost.

        3. **🟡 AKCESORNA PITANJA i DISPOZITIVNE ODREDBE (Warnings):**
           - KAPARA vs. ODUSTATNINA (čl. 303.-306. ZOO): Je li ugovorna kapara pravilno 
             kvalificirana? Razlikovanje kapare (čl. 303.) od odustatnine (čl. 306.) — ključno 
             za posljedice neispunjenja. Pravilo dvostrukog vraćanja (čl. 304. st. 2. ZOO).
           - POREZ NA PROMET NEKRETNINA: Tko snosi financijsku obvezu? (Zakonska je obveza kupca, 
             ali ugovorne strane mogu regulirati ekonomski teret.)
           - ENERGETSKI CERTIFIKAT: Obveza prodavatelja po Zakonu o gradnji (čl. 59., NN 153/13, 20/17, 39/19, 125/19).
           - REZ REŽIJA: Razgraničenje troškova na dan primopredaje (possessio).
           - UGOVORNA KAZNA (čl. 350.-356. ZOO): Nedostatak ugovorne kazne za zakašnjenje s 
             predajom u posjed — preporučljivo ugraditi kao instrument osiguranja ispunjenja.
           - ROKOVI: Jesu li primjereni? Prekluzivni rokovi (rok nakon kojeg se gubi pravo na 
             radnju) moraju biti jasno definirani.
           - SOLIDARNA ODGOVORNOST: Ako postoji više prodavatelja, regulira li ugovor solidarnost?

        4. **✅ POZITIVNE STRANE (Well-Drafted Provisions):**
           - Pravilno definirani elementi koji su u korist kupca i predstavljaju dobru pravnu praksu.

        5. **📝 PRIJEDLOG IZMJENA — KONKRETNE FORMULACIJE (Amendments):**
           - Za svaki rizik predloži exact formulaciju članka kakvu bi iskusan odvjetnik napisao.
           - Objasni ratio legis (smisao zakona) iza svake predložene izmjene.
           - Koristi termine: akcesornost, solidarna odgovornost, tabularna isprava, prekluzivni rok.

        UPUTE ZA RAZMIŠLJANJE:
        - OCR kvaliteta: Ako je tekst nejasan, upozori i označi confidence = Low.
        - Nedostatak OIB-a ili oznake čestice → KRITIČAN rizik (ugovor neprovediv u ZK).
        - Budi strog — in dubio pro emptore (u slučaju sumnje, štiti kupca).
        - Citiraj sporne dijelove ugovora kao dokaz (quoted_text).
        - Koristi stručni hrvatski pravni jezik s latinskim izrazima gdje je primjereno.
        - Uvijek navedi pravnu osnovu: članak zakona + kratica + NN oznaka (npr. "čl. 303. st. 1. ZOO, NN 35/05").

        GDPR NAPOMENA:
        - Obrada osobnih podataka sadržanih u ugovoru (OIB, imena stranaka, adrese) 
          zahtijeva izričitu privolu korisnika sukladno Uredbi (EU) 2016/679 (GDPR).
        - Korisnik je odgovoran za anonimizaciju osjetljivih podataka, 
          osim ako je izričita privola dana.
        - Ova analiza ne predstavlja pravni savjet u smislu 
          čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11).

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
              "description": "Detaljno objašnjenje s pravnim silogizmom (MAIOR-MINOR-CONCLUSIO)",
              "quoted_text": "Citirani tekst iz ugovora (ako postoji)",
              "legal_basis": "čl. 303. st. 1. ZOO"
            }
          ],
          "warnings": [
            {
              "title": "Kratki naziv upozorenja",
              "description": "Objašnjenje s ratio legis",
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
              "rationale": "Ratio legis — zašto je ova izmjena pravno nužna"
            }
          ],
          "cited_norms": [
            {
              "abbreviation": "ZOO",
              "full_name": "Zakon o obveznim odnosima",
              "article": "čl. 322.",
              "paragraph": "st. 1.",
              "point": "",
              "nn_reference": "NN 35/05, 41/08, 125/11, 78/15, 29/18",
              "status": "VAŽEĆI",
              "formatted_citation": "čl. 322. st. 1. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
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
                Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11). Obrada osobnih podataka iz ugovora provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu i odgovoran je za anonimizaciju osjetljivih podataka osim ako je izričita privola dana. Prije potpisa konzultirajte se sa stručnjakom."
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
                Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11). Obrada osobnih podataka iz ugovora provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu i odgovoran je za anonimizaciju osjetljivih podataka osim ako je izričita privola dana. Prije potpisa konzultirajte se sa stručnjakom."
            };
        }
    }

    private ContractAnalysisResult ParseResponse(string json)
    {
        var result = new ContractAnalysisResult
        {
            Disclaimer = "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11). Obrada osobnih podataka iz ugovora provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu i odgovoran je za anonimizaciju osjetljivih podataka osim ako je izričita privola dana. Prije potpisa konzultirajte se sa stručnjakom."
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
