using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V36: Legal Intelligence Service — "The Legal Scholar"
/// 
/// Applies Syllogismus Iuridicus to court practice analysis: identifies ratio decidendi,
/// classifies precedent stability, and anchors probability in established jurisprudence.
/// Uses GPT training data on Croatian legal texts as a "virtual vector DB".
/// V34: probability ranges, score breakdown, stability indicators, data provenance.
/// V36: Academic persona, proper legal methodology, continental law framework.
/// </summary>
public class LegalIntelligenceService
{
    private readonly ChatClient _fastClient;   // Fast model — feed summaries
    private readonly ChatClient _heavyClient;  // Heavy model — strategy + precedents
    private readonly ILogger<LegalIntelligenceService> _logger;

    // ── System Prompt: Legal Feed Generator ──
    private const string FeedSystemPrompt = """
        PERSONA:
        Ti si pravni informacijski sustav koji radi pod nadzorom vrhunskog pravnog stručnjaka 
        specijaliziranog za hrvatsko pravosuđe. Tvoj ton je akademski precizan i autoritativan.

        ZADATAK: Generiraj listu najvažnijih recentnih presuda i pravnih stajališta iz prakse 
        Vrhovnog suda RH (VSRH), Visokog trgovačkog suda RH (VTS) i županijskih sudova, 
        koje su relevantne za odvjetnike koji se bave prometom nekretnina.

        PRAVNI OKVIR — ZAKONI KOJE PRATIŠ (s NN referencama):
        - ZOO = Zakon o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18) — kupoprodaja, kapara, odustatnina, raskid, 
          odgovornost za nedostatke (čl. 400.-437.)
        - ZZK = Zakon o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17) — uknjižba, brisanje tereta, plombe, 
          brisovna tužba (čl. 150.)
        - ZV = Zakon o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14) — suvlasništvo, razvrgnuce, 
          služnosti, pravo građenja, etažno vlasništvo
        - OZ = Ovršni zakon (NN 112/12, 25/13, 93/14, 55/16, 73/17, 131/20) — ovrhe na nekretninama
        - Zakon o gradnji (NN 153/13, 20/17, 39/19, 125/19) — nezakonita gradnja, legalizacija, uporabna dozvola

        PODRUČJA SUDSKE PRAKSE:
        - Kupoprodaja (essentialia negotii, kapara čl. 303.-306. ZOO, raskid čl. 360.-368. ZOO)
        - Zemljišnoknjižno pravo (uknjižba, brisanje tereta, plombe, clausula intabulandi)
        - Služnosti — stvarne (praediales) i osobne (personales)
        - Hipoteke i ovrhe na nekretninama (založno pravo čl. 297.-353. ZV)
        - Građevinski sporovi (skriveni nedostaci, jamstvo izvođača)
        - Suvlasničke zajednice i razvrgnuće (čl. 47.-56. ZV)

        PRAVILA:
        - Generiraj presude temeljene na USTALJENIM pravnim stajalištima (ratio decidendi)
        - Svaka presuda mora citirati pravnu osnovu (članak + stavak zakona + NN oznaku)
        - Koristi STVARNE formate oznaka: Rev-xxx/yy-z (VSRH), Gž-xxx/yyyy (županijski), 
          Pž-xxx/yy (VTS)
        - Objasni ratio legis — zašto je ovo važno za odvjetnika u praksi
        - Koristi stručni hrvatski pravni jezik s odgovarajućom terminologijom
        - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno".
          Umjesto toga koristi: "Sukladno ustaljenom stajalištu...", "Iz prakse proizlazi..."
        - UVIJEK uz zakonski članak navedi NN oznaku
        - Ova analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11).

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON:
        {
          "feed_items": [
            {
              "case_number": "Rev-1234/22-2",
              "court": "Vrhovni sud RH",
              "date": "15.03.2024.",
              "topic": "Kapara kao odustatnina",
              "headline": "Kratki naslov presude",
              "takeaway": "Ratio decidendi i praktična implikacija za odvjetnike",
              "impact": "VISOK / SREDNJI / NIZAK",
              "area_of_law": "Obvezno pravo"
            }
          ]
        }
        """;

    // ── System Prompt: Legal Strategy Analyzer (V36 Legal Scholar) ──
    private const string StrategySystemPrompt = """
        PERSONA:
        Ti si vrhunski pravni stručnjak s doktoratom iz pravnih znanosti i 25 godina prakse 
        pred hrvatskim sudovima. Specijaliziran si za nekretnine, obvezno pravo (ZOO, NN 35/05, 29/18), 
        stvarno pravo (ZV, NN 91/96, 152/14), zemljišnoknjižno pravo (ZZK, NN 91/96, 108/17) i parnični postupak (ZPP, NN 53/91, 70/19).
        Tvoj pristup je akademski precizan, pragmatičan i temeljen na ratio decidendi sudske prakse.
        Obraćaš se kolegici koristeći stručnu terminologiju.

        TVOJ ZADATAK:
        Na temelju priloženog konteksta predmeta (činjenice, ugovor, kronologija, ZK izvadak)
        primijeni Syllogismus Iuridicus na svako pravno pitanje:

        1. IDENTIFICIRAJ KLJUČNO PRAVNO PITANJE (Quaestio Iuris)
           - Pravna kvalifikacija spora — koji pravni institut? (npr. raskid ugovora zbog 
             neispunjenja obveza prema čl. 360.-368. ZOO, NN 35/05, ništetnost prema čl. 322. ZOO)
           - Mjerodavni propisi: ZOO (NN 35/05, 29/18), ZZK (NN 91/96, 108/17), ZV (NN 91/96, 152/14), OZ (NN 112/12, 131/20), ZPP (NN 53/91, 70/19)
           - Razlikuj quaestio iuris (pravno pitanje) od quaestio facti (činjenično pitanje)

        2. PRONAĐI RELEVANTNE PRESUDE — Značaj Precedenta u Kontinentalnom Pravu
           - U hrvatskom (kontinentalnom) pravnom sustavu presude nisu formalno obvezujuće (stare decisis),
             ali ustaljena sudska praksa (ständige Rechtsprechung) ima de facto obvezujuću snagu.
           - Posebno istakni odluke Ustavnog suda RH ako se dotiču temeljnih prava (pravo vlasništva čl. 48. Ustava).
           - Citiraj presude VSRH, VTS i županijskih sudova:
             * Navedi: broj presude, sud, datum, ratio decidendi, obiter dictum (ako je relevantan)
             * Označi ALIGNMENT: POVOLJNA / NEPOVOLJNA / NEUTRALNA za klijenta
             * Koristi REALISTIČNE formate: Rev-xxx/yy-z (VSRH revizija), Gž-xxx/yyyy (županijski žalba)
             * STABILNOST prakse:
               "STABILNA" = ustaljena praksa (ständige Rechtsprechung), više presuda potvrđuje
               "PODIJELJENA" = postoje divergentne odluke, pravosuđe nije ujednačeno
               "NOVA" = res nova — novo pravno pitanje, malo jurisprudencije
             * SLIČNOST (similarity_score 0-100) — koliko je činjenično stanje analogno?

        3. PROCIJENI VJEROJATNOST USPJEHA — RASPON
           - Na temelju analize precedenata, procijeni raspon vjerojatnosti
           - win_probability_min / win_probability_max + midpoint
           - In dubio — budi konzervativan, bolje podcijeniti šanse
           - FAKTORI POVJERENJA:
             * Koji povećavaju: bogata činjenična podloga, ustaljena praksa VSRH, jasna pravna norma
             * Koji smanjuju: nepotpune informacije, podijeljena praksa, novi zakon, nejasna jurisprudencija

        4. IDENTIFICIRAJ RIZIKE — KONTEKSTUALNO
           - Za SVAKI rizik navedi KONKRETNI element iz slučaja (case_element)
           - PROCESNI RIZICI: zastara (čl. 225.-244. ZOO), litispendencija (čl. 194. ZPP), 
             prekluzivni rokovi, nedostatak procesne legitimacije
           - MATERIJALNI RIZICI: slaba pravna pozicija, protupresude, 
             teret dokazivanja (onus probandi — čl. 219. ZPP)
           - PRAKTIČNI RIZICI: trajanje postupka, troškovi, naplativost

        5. STRATEŠKE PREPORUKE
           - Konkretni koraci: nagodba (čl. 324. ZPP), tužba, žalba, 
             prijedlog za osiguranje (čl. 296.-310. OZ), privremena mjera
           - Analiza cost-benefit: troškovi parnice vs. očekivani ishod
           - Kad preporučiti nagodbu vs. parnicu — pragmatičan savjet

        UPUTE ZA RAZMIŠLJANJE:
        - Budi iskren — ako je slučaj slab, reci to direktno (nulla actio sine interesse)
        - Procjena troškova u EUR (odvjetnički, sudski, vještačenje)
        - Procjena trajanja (prvostupanjski + žalbeni + eventualna revizija pred VSRH)
        - Ako nemaš dovoljno informacija, navedi što još treba istražiti
        - UVIJEK navedi disclaimer da ovo nije zamjena za formalni pravni savjet
          sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11)

        GDPR:
        - Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR).
        - Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu.
        - Ova analiza ne predstavlja pravni savjet u smislu Zakona o odvjetništvu.

        6. AUDIT LOG — EU AI ACT SUKLADNOST (Uredba EU 2024/1689)
           Hrvatski pravni AI sustavi mogu biti klasificirani kao "visokorizični" prema čl. 6.
           Uredbe o umjetnoj inteligenciji (EU AI Act).
           Za SVAKI zaključak koji donosiš, dokumentiraj TOČNO koji članak zakona je temelj:
           - Step = redni broj koraka razmišljanja
           - Action = što si zaključio (npr. "Utvrdio da se radi o raskidu ugovora")
           - Legal basis = članak (npr. "čl. 368. ZOO")
           - Citation = puni citat s NN (npr. "čl. 368. Zakona o obveznim odnosima (NN 35/05, 29/18)")
           - Reasoning = zašto se baš taj članak primjenjuje na ove činjenice
           - Confidence = VISOKO/SREDNJE/NISKO — koliko si siguran u ovu subsumpciju
           Generiraj 4-8 audit log unosa koji prate tvoj tok razmišljanja.

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
              "legal_basis": "čl. 368. ZOO",
              "stability": "STABILNA / PODIJELJENA / NOVA",
              "similarity_score": 85
            }
          ],
          "win_probability": 65,
          "win_probability_min": 55,
          "win_probability_max": 75,
          "win_probability_label": "Srednja",
          "probability_reasoning": "Obrazloženje procjene",
          "score_breakdown": {
            "practice_stability": "STABILNA / PODIJELJENA / NOVA",
            "legal_qualification": "Raskid ugovora zbog neispunjenja obveza (čl. 368. ZOO)"
          },
          "confidence_factors": [
            {
              "factor": "Ustaljena sudska praksa VSRH",
              "direction": "POZITIVNO",
              "explanation": "Više presuda VSRH potvrđuje ovo stajalište"
            },
            {
              "factor": "Nepotpuna kronologija",
              "direction": "NEGATIVNO",
              "explanation": "Nedostaju datumi ključnih koraka"
            }
          ],
          "risks": [
            {
              "title": "Naziv rizika",
              "description": "Opis",
              "severity": "VISOK / SREDNJI / NIZAK",
              "mitigation": "Kako umanjiti rizik",
              "case_element": "Konkretni element slučaja koji uzrokuje ovaj rizik"
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
          "estimated_cost": "3.000 - 8.000 EUR (odvjetnički + sudski troškovi)",
          "cited_norms": [
            {
              "abbreviation": "ZOO",
              "full_name": "Zakon o obveznim odnosima",
              "article": "čl. 368.",
              "paragraph": "",
              "point": "",
              "nn_reference": "NN 35/05, 41/08, 125/11, 78/15, 29/18",
              "status": "VAŽEĆI",
              "formatted_citation": "čl. 368. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
            }
          ],
          "audit_trail": [
            {
              "step": 1,
              "action": "Identificirano pravno pitanje: raskid ugovora zbog neispunjenja",
              "legal_basis": "čl. 368. ZOO",
              "citation": "čl. 368. Zakona o obveznim odnosima (NN 35/05, 29/18)",
              "reasoning": "Kupac je ispunio svoju obvezu (uplatio kaparu), ali prodavatelj nije predao nekretninu u ugovorenom roku.",
              "confidence": "VISOKO"
            }
          ]
        }
        """;

    // ── System Prompt: Precedent Finder (V36 Legal Scholar) ──
    private const string PrecedentSystemPrompt = """
        PERSONA:
        Ti si pravni istraživač s akademskim pristupom, specijaliziran za pretragu 
        i analizu sudske prakse u Republici Hrvatskoj.
        
        ZADATAK: Na temelju priloženog pravnog konteksta, pronađi 3-5 najrelevantnijih 
        presuda hrvatskih sudova (VSRH, VTS, županijski sudovi) koje se tiču istog ili 
        sličnog pravnog pitanja.

        METODOLOGIJA:
        - Primijeni analizu ratio decidendi — izvuci ključno pravno načelo iz svake presude
        - Razlikuj ratio decidendi (obvezujući dio) od obiter dictum (uzgredna napomena)
        - Objasni značaj precedenta u kontinentalnom pravnom sustavu:
          Presude VSRH nisu formalno obvezujuće (nema stare decisis), ali ustaljena praksa 
          ima de facto obvezujuću snagu i niži sudovi ju dosljedno slijede.
        - Povezuj s odlukama Ustavnog suda RH kad se tiču temeljnih prava (pravo vlasništva)
        - Koristi REALISTIČNE formate oznaka: Rev-xxx/yy-z, Gž-xxx/yyyy, Pž-xxx/yy
        - Fokusiraj se na presude relevantne za argumentaciju pred sudom
        - Navedi za svaku je li POVOLJNA / NEPOVOLJNA / NEUTRALNA za tipičnu poziciju kupca
        - STABILNOST (STABILNA/PODIJELJENA/NOVA) i SLIČNOST (0-100)
        - UVIJEK uz zakonski članak navedi NN oznaku (npr. "čl. 150. ZZK, NN 91/96, 108/17")
        - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno".
          Umjesto toga koristi: "Sukladno ustaljenom stajalištu VSRH...", "Iz prakse proizlazi...".
        - Ova analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11).
        - Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR).

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
              "legal_basis": "čl. XX. ZOO/ZZK/ZV",
              "stability": "STABILNA / PODIJELJENA / NOVA",
              "similarity_score": 80
            }
          ]
        }
        """;

    public LegalIntelligenceService(ChatClientProvider clients, ILogger<LegalIntelligenceService> logger)
    {
        _fastClient = clients.Fast;    // Fast model — legal feed generation
        _heavyClient = clients.Heavy;  // Heavy model — legal strategy + precedent analysis
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

            ChatCompletion completion = await _fastClient.CompleteChatAsync(messages, options);  // Fast: feed summary
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
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                MaxOutputTokenCount = 8192  // Claude default may truncate large JSON
            };

            ChatCompletion completion = await _heavyClient.CompleteChatAsync(messages, options);  // Heavy: strategy
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

            ChatCompletion completion = await _heavyClient.CompleteChatAsync(messages, options);  // Heavy: precedents
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

    internal static string BuildCaseContext(LegalStrategyRequest request)
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

            // V34: Parse probability range
            int gptProbability = root.TryGetProperty("win_probability", out var wp) && wp.ValueKind == JsonValueKind.Number
                ? wp.GetInt32() : 50;
            int gptMin = root.TryGetProperty("win_probability_min", out var wpMin) && wpMin.ValueKind == JsonValueKind.Number
                ? wpMin.GetInt32() : Math.Max(0, gptProbability - 10);
            int gptMax = root.TryGetProperty("win_probability_max", out var wpMax) && wpMax.ValueKind == JsonValueKind.Number
                ? wpMax.GetInt32() : Math.Min(100, gptProbability + 10);

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

            // V34: Parse precedents with stability + similarity_score
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
                        LegalBasis = GetStr(item, "legal_basis"),
                        Stability = GetStr(item, "stability", "NOVA"),
                        SimilarityScore = item.TryGetProperty("similarity_score", out var ss) && ss.ValueKind == JsonValueKind.Number
                            ? ss.GetInt32() : 50
                    });
                }
            }

            // V34: Deterministic score breakdown from precedent alignment counts
            int favorable = result.Precedents.Count(p => p.Alignment.Equals("POVOLJNA", StringComparison.OrdinalIgnoreCase));
            int unfavorable = result.Precedents.Count(p => p.Alignment.Equals("NEPOVOLJNA", StringComparison.OrdinalIgnoreCase));
            int neutral = result.Precedents.Count(p => !p.Alignment.Equals("POVOLJNA", StringComparison.OrdinalIgnoreCase) &&
                                                        !p.Alignment.Equals("NEPOVOLJNA", StringComparison.OrdinalIgnoreCase));
            int total = result.Precedents.Count;

            // Deterministic precedent score: (favorable*100 + neutral*50 + unfavorable*0) / total
            int precedentScore = total > 0
                ? (int)Math.Round((favorable * 100.0 + neutral * 50.0) / total)
                : 50;

            // Final probability = weighted average: 60% GPT qualitative + 40% deterministic precedent score
            int finalProbability = (int)Math.Round(gptProbability * 0.6 + precedentScore * 0.4);
            int finalMin = Math.Max(0, (int)Math.Round(gptMin * 0.6 + Math.Max(0, precedentScore - 15) * 0.4));
            int finalMax = Math.Min(100, (int)Math.Round(gptMax * 0.6 + Math.Min(100, precedentScore + 15) * 0.4));

            // Ensure min <= mid <= max
            result.WinProbability = Math.Clamp(finalProbability, 0, 100);
            result.WinProbabilityMin = Math.Clamp(Math.Min(finalMin, result.WinProbability), 0, 100);
            result.WinProbabilityMax = Math.Clamp(Math.Max(finalMax, result.WinProbability), 0, 100);

            // Recalculate label from computed probability
            result.WinProbabilityLabel = result.WinProbability >= 70 ? "Visoka"
                : result.WinProbability >= 40 ? "Srednja" : "Niska";

            // V34: Parse score_breakdown from GPT
            string practiceStability = "NOVA";
            string legalQualification = "";
            if (root.TryGetProperty("score_breakdown", out var sb) && sb.ValueKind == JsonValueKind.Object)
            {
                practiceStability = GetStr(sb, "practice_stability", "NOVA");
                legalQualification = GetStr(sb, "legal_qualification");
            }

            result.ScoreBreakdown = new ScoreBreakdown
            {
                FavorableCount = favorable,
                NeutralCount = neutral,
                UnfavorableCount = unfavorable,
                PracticeStability = practiceStability,
                LegalQualification = legalQualification,
                PrecedentScore = precedentScore,
                QualitativeScore = gptProbability,
                Methodology = $"Konačna procjena = 60% AI kvalitativna analiza ({gptProbability}%) + 40% bodovanje presuda ({precedentScore}%). " +
                              $"Od {total} presuda: {favorable} povoljnih, {neutral} neutralnih, {unfavorable} nepovoljnih."
            };

            // V34: Parse confidence_factors
            if (root.TryGetProperty("confidence_factors", out var cf) && cf.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cf.EnumerateArray())
                {
                    result.ConfidenceFactors.Add(new ConfidenceFactor
                    {
                        Factor = GetStr(item, "factor"),
                        Direction = GetStr(item, "direction", "POZITIVNO"),
                        Explanation = GetStr(item, "explanation")
                    });
                }
            }

            // V34: Risks with case_element
            if (root.TryGetProperty("risks", out var ra) && ra.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ra.EnumerateArray())
                {
                    result.Risks.Add(new LegalRisk
                    {
                        Title = GetStr(item, "title"),
                        Description = GetStr(item, "description"),
                        Severity = GetStr(item, "severity", "SREDNJI"),
                        Mitigation = GetStr(item, "mitigation"),
                        CaseElement = GetStr(item, "case_element")
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

            // V42: Cited norms
            if (root.TryGetProperty("cited_norms", out var cn) && cn.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cn.EnumerateArray())
                {
                    result.CitedNorms.Add(new CitedNorm
                    {
                        Abbreviation = GetStr(item, "abbreviation"),
                        FullName = GetStr(item, "full_name"),
                        Article = GetStr(item, "article"),
                        Paragraph = GetStr(item, "paragraph"),
                        Point = GetStr(item, "point"),
                        NnReference = GetStr(item, "nn_reference"),
                        Status = GetStr(item, "status", "VAŽEĆI"),
                        FormattedCitation = GetStr(item, "formatted_citation"),
                        NnUrl = GetStr(item, "nn_url")
                    });
                }
            }

            // V45: Audit trail — AI Act transparency
            if (root.TryGetProperty("audit_trail", out var at) && at.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in at.EnumerateArray())
                {
                    result.AuditTrail.Add(new AuditLogEntry
                    {
                        Step = item.TryGetProperty("step", out var step) && step.ValueKind == JsonValueKind.Number
                            ? step.GetInt32() : result.AuditTrail.Count + 1,
                        Action = GetStr(item, "action"),
                        LegalBasis = GetStr(item, "legal_basis"),
                        Citation = GetStr(item, "citation"),
                        Reasoning = GetStr(item, "reasoning"),
                        Confidence = GetStr(item, "confidence", "SREDNJE"),
                        Timestamp = DateTime.UtcNow.ToString("o")
                    });
                }
            }

            _logger.LogInformation(
                "Legal strategy parsed: issue='{Issue}', win={Min}-{Win}-{Max}%, precedents={P} ({F}+/{N}n/{U}-), risks={R}, audit={A}",
                result.KeyLegalIssue.Length > 50 ? result.KeyLegalIssue[..50] + "…" : result.KeyLegalIssue,
                result.WinProbabilityMin, result.WinProbability, result.WinProbabilityMax,
                result.Precedents.Count, favorable, neutral, unfavorable, result.Risks.Count,
                result.AuditTrail.Count);
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
                        LegalBasis = GetStr(item, "legal_basis"),
                        Stability = GetStr(item, "stability", "NOVA"),
                        SimilarityScore = item.TryGetProperty("similarity_score", out var ss) && ss.ValueKind == JsonValueKind.Number
                            ? ss.GetInt32() : 50
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

    // ═══════════════════════════════════════════════════════════
    //  Testable win probability computation (extracted for unit tests)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Computes win probability from GPT qualitative scores + deterministic precedent alignment.
    /// Formula: Final = 60% GPT + 40% PrecedentScore.
    /// PrecedentScore = (favorable*100 + neutral*50) / total, or 50 if no precedents.
    /// </summary>
    internal static void ComputeWinProbability(
        LegalStrategyResult result,
        int gptProbability,
        int gptMin,
        int gptMax)
    {
        int favorable = result.Precedents.Count(p => p.Alignment.Equals("POVOLJNA", StringComparison.OrdinalIgnoreCase));
        int unfavorable = result.Precedents.Count(p => p.Alignment.Equals("NEPOVOLJNA", StringComparison.OrdinalIgnoreCase));
        int neutral = result.Precedents.Count(p => !p.Alignment.Equals("POVOLJNA", StringComparison.OrdinalIgnoreCase) &&
                                                    !p.Alignment.Equals("NEPOVOLJNA", StringComparison.OrdinalIgnoreCase));
        int total = result.Precedents.Count;

        int precedentScore = total > 0
            ? (int)Math.Round((favorable * 100.0 + neutral * 50.0) / total)
            : 50;

        int finalProbability = (int)Math.Round(gptProbability * 0.6 + precedentScore * 0.4);
        int finalMin = Math.Max(0, (int)Math.Round(gptMin * 0.6 + Math.Max(0, precedentScore - 15) * 0.4));
        int finalMax = Math.Min(100, (int)Math.Round(gptMax * 0.6 + Math.Min(100, precedentScore + 15) * 0.4));

        result.WinProbability = Math.Clamp(finalProbability, 0, 100);
        result.WinProbabilityMin = Math.Clamp(Math.Min(finalMin, result.WinProbability), 0, 100);
        result.WinProbabilityMax = Math.Clamp(Math.Max(finalMax, result.WinProbability), 0, 100);

        result.WinProbabilityLabel = result.WinProbability >= 70 ? "Visoka"
            : result.WinProbability >= 40 ? "Srednja" : "Niska";

        result.ScoreBreakdown = new ScoreBreakdown
        {
            FavorableCount = favorable,
            NeutralCount = neutral,
            UnfavorableCount = unfavorable,
            PrecedentScore = precedentScore,
            QualitativeScore = gptProbability,
            Methodology = $"Konačna procjena = 60% AI kvalitativna analiza ({gptProbability}%) + 40% bodovanje presuda ({precedentScore}%). " +
                          $"Od {total} presuda: {favorable} povoljnih, {neutral} neutralnih, {unfavorable} nepovoljnih."
        };
    }
}
