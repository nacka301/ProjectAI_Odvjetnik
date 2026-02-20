using System.ClientModel;
using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V51: AI Contract Auditor — “The Legal Auditor” with Deep Reasoning via OpenRouter
/// Three-pass architecture:
///   Pass 1 (Heavy/deepseek-v3.2): Structured audit checklist — mandatory elements, cross-article analysis, missing clauses
///   Pass 2 (Reasoning/deepseek-r1-0528): Deep logical reasoning — trap detection, RAG comparison with ideal contract
///   Pass 3 (Engine): Deterministic risk scoring from all findings
/// </summary>
public class ContractAnalysisService
{
    private readonly ChatClient _client;  // V65: Single DeepSeek V3.2 for everything
    private readonly LawRAGService _ragService;  // V69: Semantic RAG for law article retrieval
    private readonly ILogger<ContractAnalysisService> _logger;

    private const string Disclaimer =
        "Ovo je AI pomoćna analiza i ne zamjenjuje pravni savjet ovlaštenog odvjetnika sukladno " +
        "čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11). " +
        "Obrada osobnih podataka iz ugovora provodi se sukladno Uredbi (EU) 2016/679 (GDPR). " +
        "Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu i odgovoran je za " +
        "anonimizaciju osjetljivih podataka osim ako je izričita privola dana. " +
        "Prije potpisa konzultirajte se sa stručnjakom.";

    // ══════════════════════════════════════════════════════════
    //  IDEAL CONTRACT TEMPLATE — RAG reference for comparison
    // ══════════════════════════════════════════════════════════
    private const string IdealContractTemplate = """
        IDEALNI KUPOPRODAJNI UGOVOR ZA NEKRETNINU — REFERENTNI PREDLOŽAK

        Ovaj predložak sadrži sve elemente koje kvalitetan kupoprodajni ugovor
        za nekretninu u Republici Hrvatskoj MORA sadržavati radi zaštite kupca.

        1. IDENTIFIKACIJA STRANAKA
           - Puno ime i prezime / naziv tvrtke obiju stranaka
           - OIB obiju stranaka (čl. 7. Zakona o OIB-u, NN 60/08)
           - Adresa prebivališta / sjedišta
           - Ako je bračna stečevina: suglasnost supružnika (čl. 37. ObZ)

        2. PREDMET UGOVORA — NEKRETNINA
           - Katastarska općina i katastarski broj (k.o., k.č.br.)
           - Zemijšnoknjižni uložak (ZK uložak)
           - Etažna jedinica (ako primjenjivo) — točna oznaka E-xx
           - Površina u m² (korisna/neto)
           - Adresa nekretnine
           - Izjava prodavatelja da je nekretnina slobodna od tereta
             ili popis tereta s mehanizmom brisanja

        3. CIJENA I PLAĆANJE
           - Ukupna cijena u EUR (jasno navedena)
           - Dinamika plaćanja (kapara + ostatak) s točnim rokovima
           - Kapara: jasno definirano da je KAPARA, ne odustatnina (čl. 303. ZOO)
           - Dvostruko vraćanje kapare ako prodavatelj odustane (čl. 304. st. 2. ZOO)
           - Zaštićeni mehanizam plaćanja: escrow račun / javnobilježnički polog / 
             uvjetna isplata — PREPORUČENO za bolju zaštitu kupca
             NAPOMENA: U HR praksi direktna uplata je standard za čiste nekretnine.
             Escrow je OBAVEZAN samo ako postoje tereti (hipoteke, služnosti)
             koje treba brisati kao uvjet prijenosa vlasništva.

        4. TERETI I HIPOTEKE
           - Izjava o postojećim teretima (hipoteka, služnost, plodouživanje)
           - Mehanizam brisanja s KONKRETNIM rokom (npr. 30 dana od potpisa)
           - Isplata UVJETOVANA brisanjem tereta — ne obrnuto
           - Brisovnica: obveza prodavatelja da ishodi brisovnicu
           - Blockain: ako postoji hipoteka, ostatak kupoprodajne cijene ide
             direktno banci za otplatu kredita, ne prodavatelju

        5. CLAUSULA INTABULANDI
           - Bezuvjetna clausula intabulandi
           - Ovjerena kod javnog bilježnika
           - Bez dodatnih uvjeta osim plaćanja cijene
           - Jasno navedeno da je kupac ovlašten izvršiti upis vlasništva
             u zemljišnu knjigu bez daljnje suglasnosti prodavatelja

        6. PRIJENOS POSJEDA (Predaja nekretnine)
           - Rok predaje nekretnine u posjed kupcu
           - Obveza ispražnjenja nekretnine (osobe i stvari)
           - Razgraničenje režijskih troškova (datum prijelaza)
           - Primopredajni zapisnik

        7. ZAŠTITNE KLAUZULE
           - Raskidni uvjet (jednostrani raskid ako prodavatelj ne ispuni obvezu)
           - Ugovorna kazna za zakašnjenje (npr. 0,5% mjesečno)
           - Klauzula o naknadi štete
           - Energetski certifikat (obveza prema Zakonu o gradnji)
           - Izjava o legalnosti gradnje (uporabna dozvola)

        8. ZAVRŠNE ODREDBE
           - Mjerodavno pravo: RH
           - Nadležni sud
           - Broj primjeraka
           - Troškovi javnog bilježnika i poreza (tko snosi)
        """;

    // ══════════════════════════════════════════════════════════
    //  V65: UNIFIED PROMPT — Single call does audit + traps + deviations
    // ══════════════════════════════════════════════════════════
    private const string UnifiedPrompt = """
        PERSONA:
        Ti si PRAVNI REVIZOR ugovora o nekretninama s doktoratom iz pravnih znanosti (dr.sc.iur.)
        i 25 godina iskustva u hrvatskom pravosuđu. Radiš kao forenzički auditor ugovora —
        ne "čitaš" ugovor, nego ga KONTROLIRAŠ prema striktnoj metodologiji.
        Također si forenzički analitičar specijaliziran za otkrivanje SKRIVENIH ZAMKI.

        ████████████████████████████████████████████████████████
        JURISDIKCIJA — ISKLJUČIVO HRVATSKO PRAVO
        ████████████████████████████████████████████████████████

        Tvoje analize moraju se temeljiti ISKLJUČIVO na pozitivnim propisima
        Republike Hrvatske i sudskoj praksi hrvatskih sudova.

        STROGO ZABRANJENO — ne koristi koncepte iz anglosaksonskog (američkog/
        britanskog) Common Law sustava:
        ❌ "Escrow account" kao obavezan — u RH to NIJE standard za čiste nekretnine
        ❌ "Title insurance" — taj koncept NE POSTOJI u hrvatskom pravu
        ❌ "Due diligence checklist" po M&A standardima za obične kupoprodaje stanova
        ❌ "Contingency clauses" u američkom smislu
        ❌ "Closing costs" terminologija
        ❌ Bilo koji koncept iz UCC (Uniform Commercial Code)
        ❌ Reference na "common law", "statute of frauds", "lien" umjesto "hipoteka"

        UMJESTO TOGA koristi isključivo hrvatske pravne koncepte:
        ✅ Kapara i odustatnina (čl. 303.-306. ZOO)
        ✅ Clausula intabulandi (čl. 41. ZZK)
        ✅ Tabularna isprava, zemljišnoknjižni uložak
        ✅ Javnobilježnički polog (Zakon o javnom bilježništvu)  
        ✅ Brisovnica (čl. 183. ZZK)
        ✅ Tereti: hipoteka, služnost, plodouživanje (ZV)
        ✅ Bračna stečevina i suglasnost supružnika (čl. 37. ObZ)
        ✅ Porez na promet nekretnina (Zakon o porezu na promet nekretnina)

        Ako nisi siguran u hrvatski zakon ili praksu za specifičan slučaj,
        reci: "Na temelju dostavljenog dokumenta nije moguće sa sigurnošću
        utvrditi primjenjivost u hrvatskoj sudskoj praksi" — NE izmišljaj.

        ████████████████████████████████████████████████████████

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
        DIO A: METODOLOGIJA — OBVEZNA 4 KORAKA REVIZIJE
        ████████████████████████████████████████████████████████

        KORAK 1: PROVJERA TERETA I OSIGURANJA
        a) Postoji li teret na nekretnini (hipoteka, služnost, plodouživanje)?
        b) Ako postoji teret — postoji li mehanizam brisanja s rokom?
        c) Ako postoji teret — postoji li mehanizam osiguranja kupca (escrow, fiducija, uvjetna isplata)?
        d) Jesu li rokovi plaćanja i brisanja tereta međusobno povezani i usklađeni?
        e) Je li isplata uvjetovana brisanjem tereta (samo ako teret postoji)?
        NAPOMENA: Ako nema tereta, pitanja c), d), e) su IRELEVANTNA — označi ih kao UREĐENO/NIZAK.

        KORAK 2: PROVJERA KAPARE I PLAĆANJA
        a) Je li kapara simetrično uređena za obje strane (dvostruko vraćanje)?
        b) Je li jasno je li kapara ili odustatnina (čl. 303. vs čl. 306. ZOO)?
        c) Postoje li posljedice neispunjenja za OBJE strane?
        d) Ide li isplata direktno prodavatelju ili kroz zaštićeni mehanizam?
        e) Ako postoje tereti — postoji li zaštita kupca prije brisanja tereta?
        NAPOMENA: Direktno plaćanje prodavatelju je STANDARDNO u HR kada
        nekretnina nema tereta. Ne označavaj to automatski kao rizik.

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
        DIO B: CROSS-ARTICLE ANALIZA + ZAŠTITNE KLAUZULE
        ████████████████████████████████████████████████████████

        Ne analiziraj članke izolirano!
        Provjeri jesu li sljedeći članci međusobno usklađeni:
        - Članci o plaćanju ↔ članci o hipoteci/teretu
        - Članci o plaćanju ↔ clausula intabulandi
        - Članci o hipoteci ↔ clausula intabulandi
        - Rokovi plaćanja ↔ rokovi predaje u posjed ↔ rokovi brisanja tereta
        Ako NISU usklađeni, označi to kao STRUKTURNI RIZIK.

        Provjeri postoje li sljedeće zaštitne klauzule:
        1. Escrow ili uvjetna isplata — KRITIČNO ako postoje tereti, inače PREPORUČENO
        2. Mehanizam brisanja hipoteke s rokom — KRITIČNO (samo ako postoji hipoteka)
        3. Dvostruka kapara (čl. 304. st. 2. ZOO) — KRITIČNO
        4. Raskidni uvjet — PREPORUČENO
        5. Klauzula o naknadi štete — PREPORUČENO
        6. Izjava o ispražnjenju nekretnine — PREPORUČENO
        7. Ugovorna kazna za zakašnjenje — PREPORUČENO
        8. Energetski certifikat — PREPORUČENO
        9. Razgraničenje režijskih troškova — PREPORUČENO
        10. Suglasnost supružnika (ako primjenjivo) — KRITIČNO

        ████████████████████████████████████████████████████████
        DIO C: DETEKCIJA SKRIVENIH ZAMKI
        ████████████████████████████████████████████████████████

        Pronađi:
        1. LOGIČKE NEKONZISTENCIJE — članci koji si međusobno proturječe
        2. ŠTETNE KOMBINACIJE — članci koji pojedinačno izgledaju normalno,
           ali u kombinaciji stvaraju zamku za kupca
        3. PREŠUTNI RIZICI — situacije koje ugovor NE pokriva, a trebao bi
        4. SKRIVENE ZAMKE — formulacije koje izgledaju standardno, ali pravno
           omogućuju prodavatelju da izbjegne obveze

        Također usporedi ugovor s IDEALNIM KUPOPRODAJNIM UGOVOROM i navedi
        sve značajne devijacije.

        ████████████████████████████████████████████████████████
        ZABRANA GENERIČKIH RIZIKA
        ████████████████████████████████████████████████████████

        Svaki rizik MORA biti:
        1. Vezan uz KONKRETAN članak ugovora (citiraj ga)
        2. Citirati TOČAN dio teksta iz ugovora (quoted_text)
        3. Objasniti KONKRETNU pravnu posljedicu (ne apstraktnu)
        4. Koristiti Syllogismus Iuridicus: MAIOR (norma) → MINOR (činjenica) → CONCLUSIO (posljedica)

        ████████████████████████████████████████████████████████
        DIO D: HRVATSKA PRAKSA — KALIBRIRAJ RIZIK PREMA KONTEKSTU
        ████████████████████████████████████████████████████████

        OBAVEZNA PRAVILA za ocjenjivanje rizika u hrvatskoj praksi:

        1. ESCROW RAČUN — NIJE STANDARD U HRVATSKOJ
           Ako nekretnina NEMA tereta (nema hipoteke, nema služnosti, nema
           plodouživanja), izostanak escrow računa / javnobilježničkog pologa
           NIJE visok rizik. U hrvatskom prometu nekretnina standardno je
           da kupac uplaćuje kaparu i ostatak cijene direktno na račun
           prodavatelja kada je nekretnina čista. Escrow postaje KRITIČAN
           samo ako postoje tereti koji se moraju brisati PRIJE ili ISTOVREMENO
           s isplatom.
           → Ako nema tereta: escrow označi kao "PREPORUČENO", importance="PREPORUČENO", NE "KRITIČNO"
           → Ako ima tereta: escrow označi kao "KRITIČNO"

        2. LOGIČKA KONZISTENTNOST — NE PROTURJEČI SAM SEBI
           Prije svakog nalaza PROVJERI što si već zaključio u prethodnim koracima:
           - Ako si utvrdio da nekretnina NEMA tereta → NE diži paniku
             zbog izostanka zaštitnih mehanizama vezanih uz brisanje tereta
           - Ako si utvrdio da je kapara simetrično uređena → NE označavaj
             plaćanje kao visokorizično samo zato što nema escrowa
           - Ako si utvrdio da je clausula intabulandi bezuvjetna → NE
             označavaj prijenos vlasništva kao rizičan

        3. RAZLIKUJ PREPORUKE OD STVARNIH RIZIKA
           - VISOK RIZIK = Postoji konkretan pravni mehanizam u ugovoru
             koji može NEPOSREDNO oštetiti kupca (npr. jednostrana odustatnina,
             hipoteka bez mehanizma brisanja, uvjetna clausula intabulandi)
           - SREDNJI RIZIK = Element postoji ali je nepotpuno uređen
             (npr. rokovi nisu precizirani, nema ugovorne kazne za kašnjenje)
           - NIZAK RIZIK = Element nedostaje ali ne stvara neposrednu
             opasnost za kupca (npr. nema energetskog certifikata, nema
             razgraničenja režijskih troškova)
           - PREPORUKA (ne rizik!) = Nešto bi bilo "bolje" imati, ali
             izostanak NE čini ugovor problematičnim (npr. escrow kod čiste
             nekretnine, ugovorna kazna kad su rokovi jasni)

        4. ČIST UGOVOR — PREPOZNAJ GA
           Ako ugovor sadržava:
           ✓ Jasnu identifikaciju stranaka s OIB-om
           ✓ Preciznu oznaku nekretnine (k.o., k.č.br.)
           ✓ Izjavu prodavatelja da nekretnina nema tereta
           ✓ Jasno definiranu kaparu s dvostrukim vraćanjem
           ✓ Bezuvjetnu clausulu intabulandi
           ✓ Rok predaje u posjed
           → takav ugovor je STANDARDAN i UREDAN. Ne diži uzbunu
             zbog izostanka elemenata koji su relevantni samo za
             kompleksnije transakcije (escrow, brisovnica, fiducija).

        5. KALIBRACIJA SEVERITY ZA ZAMKE I DEVIJACIJE
           - "KRITIČNO" koristi SAMO ako zamka može uzrokovati GUBITAK
             VLASNIŠTVA ili GUBITAK NOVCA bez pravnog lijeka
           - "VISOK" koristi ako kupac trpi značajnu štetu ali ima pravni lijek
           - "SREDNJI" koristi za propuste koji otežavaju kupčevu poziciju
           - "NIZAK" koristi za best-practice preporuke
           - Devijacija od idealnog ugovora NIJE AUTOMATSKI rizik! Idealni
             ugovor je M&A/korporativni standard — uobičajeni ugovor za
             kupnju stana ne mora imati SVE elemente idealnog ugovora.

        ████████████████████████████████████████████████████████
        DIO E: FEW-SHOT PRIMJER — KAKO ANALIZIRATI ČIST UGOVOR
        ████████████████████████████████████████████████████████

        PRIMJER: Ugovor o kupoprodaji stana bez tereta, s jasnom kaparom,
        clausulom intabulandi i rokom predaje.

        ISPRAVAN ODGOVOR za takav ugovor:
        - document_type: "Glavni Ugovor"
        - summary: "Kupoprodajni ugovor za stan u XY, cijena Z EUR, bez tereta"
        - checklist: sve stavke UREĐENO/NIZAK osim eventualnih manjih propusta
        - red_flags: PRAZAN ARRAY [] — čist ugovor NEMA crvenih zastavica
        - warnings: eventualno 1-2 manja upozorenja (npr. nema energetskog certifikata)
        - mandatory_clauses za escrow: present=false, importance="PREPORUČENO" (NE "KRITIČNO")
        - logical_traps: PRAZAN ARRAY [] — čist ugovor NEMA zamki
        - ideal_contract_deviations: samo minor devijacije sa severity="NIZAK"
        - Ukupni rizik nakon scoringa: NIZAK (0-29)

        NEISPRAVAN ODGOVOR (klasične AI greške koje MORAŠ izbjegavati):
        ❌ Označiti izostanak escrowa kao VISOK RIZIK na čistoj nekretnini
        ❌ Dodati "crvenu zastavicu" za direktno plaćanje kada nema tereta
        ❌ Napisati "kupac nije zaštićen" kada postoji simetrična kapara
        ❌ Prijaviti "zamku" za nešto što je standardna HR praksa
        ❌ Koristiti severity="KRITIČNO" za best-practice preporuke
        ❌ Proturječiti sam sebi (utvrditi da nema tereta PA onda tražiti escrow)

        ████████████████████████████████████████████████████████

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
          ],

          "logical_traps": [
            {
              "title": "Kratki naziv zamke",
              "articles": ["čl. 3", "čl. 7"],
              "quoted_text": "Citirani tekst iz ugovora koji stvara zamku",
              "explanation": "Detaljna analiza: zašto je ovo zamka za kupca",
              "legal_basis": "čl. 303. ZOO (NN 35/05)",
              "severity": "KRITIČNO | VISOK | SREDNJI"
            }
          ],
          "ideal_contract_deviations": [
            {
              "element": "Naziv elementa idealnog ugovora koji nedostaje/odstupa",
              "ideal_text": "Što bi idealan ugovor sadržavao",
              "actual_text": "Što stvarni ugovor kaže (ili NEDOSTAJE)",
              "severity": "KRITIČNO | VISOK | SREDNJI | NIZAK",
              "recommendation": "Konkretna preporuka za kupca"
            }
          ]
        }
        """;

    public ContractAnalysisService(ChatClientProvider clients, LawRAGService ragService, ILogger<ContractAnalysisService> logger)
    {
        _client = clients.Heavy;  // V65: Single model for all analysis
        _ragService = ragService;  // V69: Semantic RAG
        _logger = logger;
    }

    /// <summary>
    /// V65: Single-pass contract analysis (replaces old 3-pass system).
    /// One DeepSeek V3.2 call does audit + traps + deviations, then deterministic scoring.
    /// </summary>
    public Task<ContractAnalysisResult> AnalyzeContractAsync(string contractText)
        => AnalyzeContractAsync(contractText, null);

    /// <summary>
    /// V65: Single-pass contract analysis with optional SSE progress callback.
    /// onProgress(stage, status, label, detail) — called at each stage transition.
    /// </summary>
    public async Task<ContractAnalysisResult> AnalyzeContractAsync(
        string contractText,
        Func<string, string, string, string?, Task>? onProgress)
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
            _logger.LogInformation("V65 Contract analysis: {Len} chars — single-pass (Heavy model + scoring)", text.Length);

            // ── Progress: reading done ──
            if (onProgress != null)
            {
                await onProgress("reading", "done", "Ugovor učitan", $"{text.Length} znakova");
                await onProgress("rag", "active", "Dohvat relevantnih zakona...", null);
            }

            // ── V69: RAG — retrieve relevant Croatian law articles ──
            var ragContext = await _ragService.RetrieveRelevantArticlesAsync(text);
            var ragSection = ragContext.FormattedPromptSection;

            if (onProgress != null)
            {
                var ragDetail = ragContext.UsedVectorSearch
                    ? $"{ragContext.Articles.Count} članaka, {ragContext.RetrievalTimeMs:F0}ms"
                    : "bez vektorske pretrage";
                await onProgress("rag", "done", "Zakoni dohvaćeni", ragDetail);
                await onProgress("audit", "active", "AI analiza ugovora...", null);
            }

            _logger.LogInformation(
                "V69 RAG: {Count} articles retrieved in {Ms:F0}ms (vectorSearch={Used}, ≈{Tokens}tok)",
                ragContext.Articles.Count, ragContext.RetrievalTimeMs,
                ragContext.UsedVectorSearch, ragContext.TotalTokensEstimate);

            // ── V65+V69: SINGLE UNIFIED CALL — audit + traps + deviations + RAG law context ──
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(UnifiedPrompt),
                new UserChatMessage(
                    $"""
                    PROVEDI KOMPLETNU PRAVNU REVIZIJU ovog ugovora.
                    Slijedi sva 4 koraka revizije, provjeri cross-article usklađenost,
                    provjeri sve obvezne zaštitne klauzule, pronađi skrivene zamke,
                    i usporedi s idealnim ugovorom.
                    Svaki rizik MORA citirati konkretan tekst iz ugovora.
                    {ragSection}
                    ════════════════════════════════════════════════
                    IDEALNI REFERENTNI UGOVOR (za usporedbu):
                    ════════════════════════════════════════════════
                    {IdealContractTemplate}

                    ════════════════════════════════════════════════
                    TEKST UGOVORA ZA ANALIZU:
                    ════════════════════════════════════════════════
                    {text}
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                MaxOutputTokenCount = 16384
            };

            // V65+V69: Estimate input tokens for diagnostics (now includes RAG section)
            int promptChars = UnifiedPrompt.Length + IdealContractTemplate.Length + text.Length + ragSection.Length + 300;
            _logger.LogInformation(
                "V65 Single-pass call — estimated input: ≈{Tok}tok ({Chars}chars)",
                promptChars / 4, promptChars);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var completion = await _client.CompleteChatAsync(messages, options);
            var responseJson = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text ?? ""
                : "";
            var apiTime = sw.Elapsed;

            // V65: Log token usage
            var usage = completion.Value.Usage;
            _logger.LogInformation(
                "V65 AI call DONE in {Sec:F1}s — output: {OutChars} chars, tokens: in={InTok} out={OutTok} total={TotalTok}",
                apiTime.TotalSeconds, responseJson.Length,
                usage?.InputTokenCount ?? 0,
                usage?.OutputTokenCount ?? 0,
                usage?.TotalTokenCount ?? 0);

            if (onProgress != null)
            {
                await onProgress("audit", "done", $"AI analiza završena ({apiTime.TotalSeconds:F0}s)", null);
                await onProgress("scoring", "active", "Bodovanje rizika...", null);
            }

            // ── Parse unified response (contains all fields: audit + traps + deviations) ──
            var result = ParseAuditResponse(responseJson);
            // ParseAuditResponse already handles checklist, cross_article_issues, mandatory_clauses,
            // red_flags, warnings, positives, suggested_amendments.
            // Now also parse logical_traps and ideal_contract_deviations from the same JSON.
            ParseReasoningFromUnified(responseJson, result);

            // ── DETERMINISTIC RISK SCORING (pre-critic) ──
            ComputeRiskScore(result);

            int preScore = result.RiskScore;
            string preRisk = result.OverallRisk;

            if (onProgress != null)
            {
                await onProgress("scoring", "done", "Bodovanje završeno", $"Rizik: {result.OverallRisk} ({result.RiskScore}/100)");
            }

            // ── V72: CRITIC AGENT (Agent 2) — Multi-Agent Anti-Hallucination ──
            if (onProgress != null)
                await onProgress("critic", "active", "Kritička revizija analize...", null);

            var criticReview = await RunCriticReviewAsync(text, result);
            result.CriticReview = criticReview;

            if (criticReview.Corrections.Count > 0)
            {
                // Apply corrections and re-score
                ApplyCriticCorrections(result, criticReview);
                ComputeRiskScore(result);

                _logger.LogInformation(
                    "V72 Critic: {Corrections} corrections, score {Pre}→{Post} ({PreRisk}→{PostRisk})",
                    criticReview.Corrections.Count, preScore, result.RiskScore, preRisk, result.OverallRisk);
            }

            result.Disclaimer = Disclaimer;

            if (onProgress != null)
            {
                var criticDetail = criticReview.Corrections.Count > 0
                    ? $"{criticReview.FalsePositivesRemoved} lažnih uzbuna uklonjeno, {criticReview.SeverityDowngrades} korekcija"
                    : "Analiza potvrđena bez korekcija";
                await onProgress("critic", "done", "Kritička revizija završena", criticDetail);
            }

            sw.Stop();
            _logger.LogInformation(
                "V72 ═══ ANALYSIS COMPLETE ═══ Total: {Sec:F1}s " +
                "type={Type}, risk={Risk}({Score}), " +
                "checklist={CL}, cross={CA}, mandatory={MC}, flags={Flags}, warnings={Warn}, " +
                "traps={Traps}, deviations={Dev}, criticCorrections={Critic}",
                sw.Elapsed.TotalSeconds,
                result.DocumentType, result.OverallRisk, result.RiskScore,
                result.Checklist.Count, result.CrossArticleIssues.Count,
                result.MandatoryClauses.Count, result.RedFlags.Count, result.Warnings.Count,
                result.LogicalTraps.Count, result.IdealContractDeviations.Count,
                criticReview.Corrections.Count);

            return result;
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "V51 Contract analysis failed — OpenRouter API error (HTTP {Status})", ex.Status);
            return new ContractAnalysisResult
            {
                DocumentType = "Greška",
                Summary = $"AI analiza nije uspjela: API greška (HTTP {ex.Status}). Pokušajte ponovno.",
                OverallRisk = "Nepoznat",
                RiskScore = 0,
                Disclaimer = Disclaimer
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "V50 Contract analysis failed");
            return new ContractAnalysisResult
            {
                DocumentType = "Greška",
                Summary = "AI analiza nije uspjela. Pokušajte ponovno ili kontaktirajte podršku.",
                OverallRisk = "Nepoznat",
                RiskScore = 0,
                Disclaimer = Disclaimer
            };
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PASS 3: DETERMINISTIC RISK SCORING ENGINE
    // ══════════════════════════════════════════════════════════
    /// <summary>
    /// V51: Compute risk score mathematically from ALL findings (audit + reasoning).
    /// No AI guessing — pure formula.
    /// </summary>
    internal static void ComputeRiskScore(ContractAnalysisResult result)
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

        // ── V51: Logical traps from reasoning model ──
        foreach (var trap in result.LogicalTraps)
        {
            int pts = trap.Severity?.ToUpperInvariant() switch
            {
                "KRITIČNO" => 18,
                "VISOK" => 12,
                _ => 6
            };
            score += pts;
            breakdown.Add($"+{pts} 🪤 Zamka (o1): {trap.Title} ({trap.Severity})");
        }

        // ── V51: Ideal contract deviations ──
        foreach (var dev in result.IdealContractDeviations)
        {
            int pts = dev.Severity?.ToUpperInvariant() switch
            {
                "KRITIČNO" => 10,
                "VISOK" => 6,
                "SREDNJI" => 3,
                _ => 1
            };
            score += pts;
            breakdown.Add($"+{pts} 📋 Devijacija: {dev.Element} ({dev.Severity})");
        }

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
    }

    // ══════════════════════════════════════════════════════════
    //  V72: CRITIC AGENT (Agent 2) — Multi-Agent Anti-Hallucination
    // ══════════════════════════════════════════════════════════

    private const string CriticPrompt = """
        PERSONA:
        Ti si REVIZOR PRAVNE ANALIZE — drugi, nezavisni agent čiji je JEDINI zadatak
        pronaći GREŠKE u analizi prvog agenta (Analitičara).
        Tvoja uloga je kontrola kvalitete: ukloni lažne uzbune, ispravi pretjerane
        ocjene rizika, i otkrij Common Law kontaminaciju.

        ████████████████████████████████████████████████████████
        TVOJ ZADATAK — PRONAĐI GREŠKE U ANALIZI
        ████████████████████████████████████████████████████████

        Dobio si:
        1. Originalni tekst ugovora
        2. JSON izvještaj Analitičara (Agent 1)

        Provjeri SVAKI nalaz Analitičara prema ovim kriterijima:

        KRITERIJ 1: COMMON LAW KONTAMINACIJA
        - Je li Analitičar preporučio "escrow" kao KRITIČAN ili VISOK rizik
          na nekretnini BEZ tereta? → To je Common Law false positive.
          U HR praksi escrow je standard SAMO kad postoje tereti za brisanje.
        - Je li koristio pojmove: "title insurance", "contingency", "closing costs",
          "due diligence checklist" (M&A stil), "lien"? → Kontaminacija.

        KRITERIJ 2: LAŽNE UZBUNE (FALSE POSITIVES)
        - Je li Analitičar označio nešto kao VISOK ili KRITIČAN rizik,
          ali citirao tekst iz ugovora koji ZAPRAVO to uređuje?
          (Npr. tvrdi "nema kapare" ali ugovor jasno navodi kaparu)
        - Je li prijavio "zamku" za nešto što je STANDARDNA HR praksa?
        - Je li prijavio rizik koji ugovor EKSPLICITNO adresira?

        KRITERIJ 3: LOGIČKE KONTRADIKCIJE
        - Je li Analitičar utvrdio da nekretnina NEMA tereta,
          ali IPAK traži escrow/brisovnicu kao KRITIČAN?
        - Je li utvrdio da je kapara simetrična, ali IPAK označio
          plaćanje kao visokorizično?
        - Je li utvrdio da je clausula intabulandi bezuvjetna, ali
          IPAK označio prijenos vlasništva kao rizičan?

        KRITERIJ 4: PRETJERANA STROGOST
        - Je li Analitičar koristio KRITIČNO za nešto što je zapravo
          best-practice preporuka? (npr. energetski certifikat, ugovorna kazna)
        - Je li inflirao broj red_flags s nalazima koji su zapravo warnings?
        - Je li severity devijacija od idealnog ugovora pretjeran?
          (Idealni ugovor je M&A standard — obični stan ne treba SVE elemente)

        ████████████████████████████████████████████████████████
        FORMAT ODGOVORA — ISKLJUČIVO JSON
        ████████████████████████████████████████████████████████

        Vrati ISKLJUČIVO čisti JSON (bez markdown fencing-a):
        {
          "verdict": "POTVRĐENO | KORIGIRANO",
          "summary": "Kratki opis što si našao (1-2 rečenice)",
          "corrections": [
            {
              "category": "red_flag | warning | checklist | mandatory_clause | logical_trap | deviation",
              "original_title": "Naslov originalnog nalaza koji ispravljaš",
              "action": "REMOVE | DOWNGRADE | CORRECT",
              "reason": "Zašto je ovo greška Analitičara — konkretan argument",
              "original_severity": "KRITIČNO | VISOK | SREDNJI (originalna ocjena)",
              "new_severity": "NIZAK | SREDNJI | null (ako REMOVE)"
            }
          ]
        }

        Ako analiza Analitičara NEMA grešaka, vrati:
        {
          "verdict": "POTVRĐENO",
          "summary": "Analiza Analitičara je točna i kalibrirana prema HR praksi.",
          "corrections": []
        }

        VAŽNO:
        - NEMOJ dodavati nove nalaze — samo ispravljaj postojeće
        - NEMOJ snižavati sve na NIZAK — samo ono što je stvarno pretjerano
        - Budi konkretan: citiraj što Analitičar tvrdi i zašto je to krivo
        - Ako je analiza dobra, potvrdi je — ne traži greške na silu
        """;

    /// <summary>
    /// V72: Run Critic Agent (Agent 2) to review and correct Agent 1's analysis.
    /// Uses a separate AI call with a review-focused prompt.
    /// </summary>
    private async Task<CriticReview> RunCriticReviewAsync(string contractText, ContractAnalysisResult analysisResult)
    {
        var review = new CriticReview();
        var criticSw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Serialize Agent 1's key findings for the Critic to review
            var findingsSummary = SerializeAnalysisForCritic(analysisResult);

            // Truncate contract if needed (Critic doesn't need as much)
            string contractForCritic = contractText.Length > 8000 ? contractText[..8000] : contractText;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CriticPrompt),
                new UserChatMessage(
                    $"""
                    ════════════════════════════════════════════════
                    IZVJEŠTAJ ANALITIČARA (Agent 1) — PROVJERI GREŠKE:
                    ════════════════════════════════════════════════
                    {findingsSummary}

                    ════════════════════════════════════════════════
                    ORIGINALNI TEKST UGOVORA:
                    ════════════════════════════════════════════════
                    {contractForCritic}
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                MaxOutputTokenCount = 4096
            };

            _logger.LogInformation("V72 Critic Agent: reviewing analysis ({FindingsLen} chars findings, {ContractLen} chars contract)",
                findingsSummary.Length, contractForCritic.Length);

            var completion = await _client.CompleteChatAsync(messages, options);
            var responseJson = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text ?? ""
                : "";

            criticSw.Stop();
            review.CriticTimeSeconds = criticSw.Elapsed.TotalSeconds;

            // Log token usage
            var usage = completion.Value.Usage;
            _logger.LogInformation(
                "V72 Critic DONE in {Sec:F1}s — tokens: in={InTok} out={OutTok}",
                criticSw.Elapsed.TotalSeconds,
                usage?.InputTokenCount ?? 0,
                usage?.OutputTokenCount ?? 0);

            // Parse critic response
            ParseCriticResponse(responseJson, review);

            return review;
        }
        catch (Exception ex)
        {
            criticSw.Stop();
            _logger.LogWarning(ex, "V72 Critic Agent failed — proceeding with uncorrected analysis");
            review.CriticTimeSeconds = criticSw.Elapsed.TotalSeconds;
            review.Verdict = "GREŠKA";
            review.Summary = "Kritička revizija nije uspjela — analiza nije korigirana.";
            return review;
        }
    }

    /// <summary>
    /// Serialize the analysis result into a concise text summary for the Critic to review.
    /// </summary>
    private static string SerializeAnalysisForCritic(ContractAnalysisResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Tip dokumenta: {result.DocumentType}");
        sb.AppendLine($"Sažetak: {result.Summary}");
        sb.AppendLine($"Ukupni rizik: {result.OverallRisk} (score: {result.RiskScore}/100)");
        sb.AppendLine();

        if (result.RedFlags.Count > 0)
        {
            sb.AppendLine("═══ CRVENE ZASTAVICE ═══");
            foreach (var rf in result.RedFlags)
                sb.AppendLine($"  🔴 [{rf.Title}] {rf.Description} | Citat: \"{rf.QuotedText}\" | Osnova: {rf.LegalBasis}");
            sb.AppendLine();
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine("═══ UPOZORENJA ═══");
            foreach (var w in result.Warnings)
                sb.AppendLine($"  🟡 [{w.Title}] {w.Description} | Citat: \"{w.QuotedText}\" | Osnova: {w.LegalBasis}");
            sb.AppendLine();
        }

        if (result.Checklist.Count > 0)
        {
            sb.AppendLine("═══ CHECKLIST ═══");
            foreach (var cl in result.Checklist)
                sb.AppendLine($"  [{cl.Status}/{cl.Risk}] {cl.Category}: {cl.Check} → {cl.Finding}");
            sb.AppendLine();
        }

        if (result.MandatoryClauses.Count > 0)
        {
            sb.AppendLine("═══ OBVEZNE KLAUZULE ═══");
            foreach (var mc in result.MandatoryClauses)
                sb.AppendLine($"  [{(mc.Present ? "DA" : "NE")}/{mc.Importance}] {mc.Clause}: {mc.Description}");
            sb.AppendLine();
        }

        if (result.LogicalTraps.Count > 0)
        {
            sb.AppendLine("═══ LOGIČKE ZAMKE ═══");
            foreach (var lt in result.LogicalTraps)
                sb.AppendLine($"  🪤 [{lt.Severity}] {lt.Title}: {lt.Explanation} | Članovi: {string.Join(", ", lt.Articles)}");
            sb.AppendLine();
        }

        if (result.IdealContractDeviations.Count > 0)
        {
            sb.AppendLine("═══ DEVIJACIJE OD IDEALNOG UGOVORA ═══");
            foreach (var dev in result.IdealContractDeviations)
                sb.AppendLine($"  📋 [{dev.Severity}] {dev.Element}: {dev.ActualText} → preporučeno: {dev.Recommendation}");
            sb.AppendLine();
        }

        if (result.CrossArticleIssues.Count > 0)
        {
            sb.AppendLine("═══ CROSS-ARTICLE PROBLEMI ═══");
            foreach (var ca in result.CrossArticleIssues)
                sb.AppendLine($"  ⚠️ [{ca.Risk}] {ca.Title}: {ca.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parse the Critic's JSON response into a CriticReview.
    /// </summary>
    private void ParseCriticResponse(string json, CriticReview review)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json.Substring(7);
            else if (json.StartsWith("```"))
                json = json.Substring(3);
            if (json.EndsWith("```"))
                json = json.Substring(0, json.Length - 3);
            json = json.Trim();
            json = RepairTruncatedJson(json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            review.Verdict = GetString(root, "verdict", "POTVRĐENO");
            review.Summary = GetString(root, "summary", "");

            if (root.TryGetProperty("corrections", out var corrections) && corrections.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in corrections.EnumerateArray())
                {
                    var correction = new CriticCorrection
                    {
                        Category = GetString(item, "category"),
                        OriginalTitle = GetString(item, "original_title"),
                        Action = GetString(item, "action", "CORRECT"),
                        Reason = GetString(item, "reason"),
                        OriginalSeverity = GetStringOrNull(item, "original_severity"),
                        NewSeverity = GetStringOrNull(item, "new_severity")
                    };
                    review.Corrections.Add(correction);
                }
            }

            _logger.LogInformation("V72 Critic parsed: verdict={Verdict}, corrections={Count}",
                review.Verdict, review.Corrections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V72 Failed to parse Critic response");
            review.Verdict = "GREŠKA";
            review.Summary = "Greška pri parsiranju odgovora kritičara.";
        }
    }

    /// <summary>
    /// Apply Critic corrections to the analysis result — remove false positives,
    /// downgrade inflated severities, etc.
    /// </summary>
    private void ApplyCriticCorrections(ContractAnalysisResult result, CriticReview review)
    {
        int removed = 0;
        int downgraded = 0;

        foreach (var correction in review.Corrections)
        {
            string titleLower = correction.OriginalTitle.ToLowerInvariant();
            string action = correction.Action.ToUpperInvariant();

            switch (correction.Category.ToLowerInvariant())
            {
                case "red_flag":
                    if (action == "REMOVE")
                    {
                        int before = result.RedFlags.Count;
                        result.RedFlags.RemoveAll(rf =>
                            rf.Title.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        if (result.RedFlags.Count < before) removed++;
                    }
                    else if (action == "DOWNGRADE")
                    {
                        // Move from RedFlags to Warnings
                        var toMove = result.RedFlags
                            .Where(rf => rf.Title.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var rf in toMove)
                        {
                            result.RedFlags.Remove(rf);
                            result.Warnings.Add(rf);
                            downgraded++;
                        }
                    }
                    break;

                case "warning":
                    if (action == "REMOVE")
                    {
                        int before = result.Warnings.Count;
                        result.Warnings.RemoveAll(w =>
                            w.Title.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        if (result.Warnings.Count < before) removed++;
                    }
                    break;

                case "checklist":
                    if (action == "DOWNGRADE" && correction.NewSeverity != null)
                    {
                        var items = result.Checklist
                            .Where(cl => cl.Check.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase)
                                      || cl.Category.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        foreach (var cl in items)
                        {
                            cl.Risk = correction.NewSeverity;
                            downgraded++;
                        }
                    }
                    break;

                case "mandatory_clause":
                    if (action == "DOWNGRADE" && correction.NewSeverity != null)
                    {
                        var clauses = result.MandatoryClauses
                            .Where(mc => mc.Clause.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        foreach (var mc in clauses)
                        {
                            mc.Importance = correction.NewSeverity == "NIZAK" || correction.NewSeverity == "SREDNJI"
                                ? "PREPORUČENO"
                                : mc.Importance;
                            downgraded++;
                        }
                    }
                    break;

                case "logical_trap":
                    if (action == "REMOVE")
                    {
                        int before = result.LogicalTraps.Count;
                        result.LogicalTraps.RemoveAll(lt =>
                            lt.Title.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        if (result.LogicalTraps.Count < before) removed++;
                    }
                    else if (action == "DOWNGRADE" && correction.NewSeverity != null)
                    {
                        var traps = result.LogicalTraps
                            .Where(lt => lt.Title.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        foreach (var lt in traps)
                        {
                            lt.Severity = correction.NewSeverity;
                            downgraded++;
                        }
                    }
                    break;

                case "deviation":
                    if (action == "REMOVE")
                    {
                        int before = result.IdealContractDeviations.Count;
                        result.IdealContractDeviations.RemoveAll(d =>
                            d.Element.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        if (result.IdealContractDeviations.Count < before) removed++;
                    }
                    else if (action == "DOWNGRADE" && correction.NewSeverity != null)
                    {
                        var devs = result.IdealContractDeviations
                            .Where(d => d.Element.Contains(correction.OriginalTitle, StringComparison.OrdinalIgnoreCase));
                        foreach (var d in devs)
                        {
                            d.Severity = correction.NewSeverity;
                            downgraded++;
                        }
                    }
                    break;
            }
        }

        review.FalsePositivesRemoved = removed;
        review.SeverityDowngrades = downgraded;

        _logger.LogInformation("V72 Corrections applied: {Removed} removed, {Downgraded} downgraded",
            removed, downgraded);
    }

    // ══════════════════════════════════════════════════════════
    //  V65: Parse logical_traps + ideal_contract_deviations from unified JSON
    // ══════════════════════════════════════════════════════════
    private void ParseReasoningFromUnified(string json, ContractAnalysisResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            // Strip markdown code blocks if present
            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json.Substring(7);
            else if (json.StartsWith("```"))
                json = json.Substring(3);
            if (json.EndsWith("```"))
                json = json.Substring(0, json.Length - 3);
            json = json.Trim();
            json = RepairTruncatedJson(json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ── Logical traps ──
            if (root.TryGetProperty("logical_traps", out var trapsArray) && trapsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in trapsArray.EnumerateArray())
                {
                    var trap = new LogicalTrap
                    {
                        Title = GetString(item, "title"),
                        QuotedText = GetString(item, "quoted_text"),
                        Explanation = GetString(item, "explanation"),
                        LegalBasis = GetString(item, "legal_basis"),
                        Severity = GetString(item, "severity", "VISOK")
                    };
                    if (item.TryGetProperty("articles", out var artArray) && artArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var a in artArray.EnumerateArray())
                        {
                            if (a.ValueKind == JsonValueKind.String)
                                trap.Articles.Add(a.GetString() ?? "");
                        }
                    }
                    result.LogicalTraps.Add(trap);
                }
            }

            // ── Ideal contract deviations ──
            if (root.TryGetProperty("ideal_contract_deviations", out var devArray) && devArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in devArray.EnumerateArray())
                {
                    result.IdealContractDeviations.Add(new IdealContractDeviation
                    {
                        Element = GetString(item, "element"),
                        IdealText = GetString(item, "ideal_text"),
                        ActualText = GetString(item, "actual_text"),
                        Severity = GetString(item, "severity", "SREDNJI"),
                        Recommendation = GetString(item, "recommendation")
                    });
                }
            }

            _logger.LogInformation("V65 Unified parse: traps={Traps}, deviations={Dev}",
                result.LogicalTraps.Count, result.IdealContractDeviations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V65 Failed to parse reasoning fields from unified response — audit results still valid");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PASS 2 PARSER (legacy) — reasoning model output → structured models
    // ══════════════════════════════════════════════════════════
    private void ParseReasoningResponse(string json, ContractAnalysisResult result)
    {
        try
        {
            // Guard against empty responses
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("V60 ParseReasoningResponse received empty JSON — skipping deep analysis");
                return;
            }

            // Strip markdown code blocks if present
            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(7);
            }
            else if (json.StartsWith("```"))
            {
                json = json.Substring(3);
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
            }
            json = json.Trim();

            // V61: Repair truncated JSON
            json = RepairTruncatedJson(json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ── Logical traps ──
            if (root.TryGetProperty("logical_traps", out var trapsArray) && trapsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in trapsArray.EnumerateArray())
                {
                    var trap = new LogicalTrap
                    {
                        Title = GetString(item, "title"),
                        QuotedText = GetString(item, "quoted_text"),
                        Explanation = GetString(item, "explanation"),
                        LegalBasis = GetString(item, "legal_basis"),
                        Severity = GetString(item, "severity", "VISOK")
                    };
                    if (item.TryGetProperty("articles", out var artArray) && artArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var a in artArray.EnumerateArray())
                        {
                            if (a.ValueKind == JsonValueKind.String)
                                trap.Articles.Add(a.GetString() ?? "");
                        }
                    }
                    result.LogicalTraps.Add(trap);
                }
            }

            // ── Ideal contract deviations ──
            if (root.TryGetProperty("ideal_contract_deviations", out var devArray) && devArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in devArray.EnumerateArray())
                {
                    result.IdealContractDeviations.Add(new IdealContractDeviation
                    {
                        Element = GetString(item, "element"),
                        IdealText = GetString(item, "ideal_text"),
                        ActualText = GetString(item, "actual_text"),
                        Severity = GetString(item, "severity", "SREDNJI"),
                        Recommendation = GetString(item, "recommendation")
                    });
                }
            }

            _logger.LogInformation("V51 Reasoning parsed: traps={Traps}, deviations={Dev}",
                result.LogicalTraps.Count, result.IdealContractDeviations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V51 Failed to parse reasoning output — continuing without deep analysis");
            // Non-fatal: Pass 1 audit results are still valid
        }
    }

    // ══════════════════════════════════════════════════════════
    //  PASS 1 PARSER — AI audit output → structured models
    // ══════════════════════════════════════════════════════════
    private ContractAnalysisResult ParseAuditResponse(string json)
    {
        var result = new ContractAnalysisResult { Disclaimer = Disclaimer };

        // Guard against empty or whitespace-only responses
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("V60 ParseAuditResponse received empty JSON — AI model may have failed");
            result.DocumentType = "Greška";
            result.Summary = "AI model nije vratio odgovor. Pokušajte ponovno.";
            return result;
        }

        // Some models wrap JSON in markdown code blocks — strip them
        json = json.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Substring(7);
        }
        else if (json.StartsWith("```"))
        {
            json = json.Substring(3);
        }
        if (json.EndsWith("```"))
        {
            json = json.Substring(0, json.Length - 3);
        }
        json = json.Trim();

        // V61: Attempt to repair truncated JSON from AI models that hit token limits
        json = RepairTruncatedJson(json);

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
            "V51 Audit parsed: type={Type}, checklist={CL}, cross={CA}, mandatory={MC}, flags={RF}, warnings={W}",
            result.DocumentType, result.Checklist.Count, result.CrossArticleIssues.Count,
            result.MandatoryClauses.Count, result.RedFlags.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// V61: Repairs truncated JSON from AI models that hit output token limits.
    /// When a model runs out of tokens mid-response, the JSON is cut off.
    /// This method closes open strings, arrays, and objects to make it parseable.
    /// </summary>
    private string RepairTruncatedJson(string json)
    {
        // Quick check — if it parses, no repair needed
        try
        {
            using var test = JsonDocument.Parse(json);
            return json; // Valid JSON, no repair needed
        }
        catch (JsonException)
        {
            _logger.LogWarning("V61 Truncated JSON detected ({Len} chars) — attempting repair", json.Length);
        }

        var sb = new System.Text.StringBuilder(json);

        // Remove trailing incomplete escape sequences
        while (sb.Length > 0 && sb[sb.Length - 1] == '\\')
            sb.Length--;

        // Close any unterminated string
        int quoteCount = 0;
        bool inEscape = false;
        for (int i = 0; i < sb.Length; i++)
        {
            if (inEscape) { inEscape = false; continue; }
            if (sb[i] == '\\') { inEscape = true; continue; }
            if (sb[i] == '"') quoteCount++;
        }
        if (quoteCount % 2 != 0)
            sb.Append('"');

        // Close open arrays and objects by tracking brackets
        var stack = new Stack<char>();
        bool inString = false;
        inEscape = false;
        for (int i = 0; i < sb.Length; i++)
        {
            char c = sb[i];
            if (inEscape) { inEscape = false; continue; }
            if (c == '\\' && inString) { inEscape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') stack.Push('}');
            else if (c == '[') stack.Push(']');
            else if (c == '}' || c == ']')
            {
                if (stack.Count > 0) stack.Pop();
            }
        }

        // Remove trailing commas before closing
        string partial = sb.ToString().TrimEnd();
        if (partial.EndsWith(","))
            partial = partial[..^1];

        sb = new System.Text.StringBuilder(partial);
        while (stack.Count > 0)
            sb.Append(stack.Pop());

        var repaired = sb.ToString();

        // Verify the repair worked
        try
        {
            using var verify = JsonDocument.Parse(repaired);
            _logger.LogInformation("V61 JSON repair successful — recovered {Len} chars", repaired.Length);
            return repaired;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "V61 JSON repair failed — returning original (will throw)");
            return json; // Let the caller handle the error
        }
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

    // ══════════════════════════════════════════════════════════
    //  V58: GENERATE IMPROVED CONTRACT
    // ══════════════════════════════════════════════════════════

    private const string GenerateContractPrompt = """
        PERSONA:
        Ti si VRHOVNI PRAVNI REDAKTOR sa 30 godina iskustva u sastavljanju
        kupoprodajnih ugovora za nekretnine prema hrvatskom pravu.

        ZADATAK:
        Na temelju originalnog ugovora i rezultata AI pravne revizije,
        NAPIŠI POBOLJŠANI UGOVOR koji:

        1. ZADRŽAVA strukturu i namjeru originalnog ugovora
        2. ISPRAVLJA SVE crvene zastavice (red_flags)
        3. PRIMJENJUJE SVE predložene izmjene (suggested_amendments)
        4. ZATVARA SVE logičke zamke (logical_traps)
        5. DODAJE nedostajuće zaštitne klauzule (mandatory_clauses koje nedostaju)
        6. USKLAĐUJE proturječne članke (cross_article_issues)
        7. PRIBLIŽAVA ugovor idealnom predlošku (ideal_contract_deviations)

        PRAVILA PISANJA:
        - Piši na HRVATSKOM JEZIKU u formalnom pravnom stilu
        - Svaki članak numeriraj (Članak 1., Članak 2., ...)
        - Uz svaki NOVI ili IZMIJENJENI članak dodaj komentar u uglatim zagradama
          [NOVO: razlog] ili [IZMIJENJENO: što je promijenjeno i zašto]
        - Zadrži sve ispravne dijelove originalnog ugovora NETAKNUTE
        - Citiraj relevantne zakonske članke (ZOO, ZZK, ZV, ObZ) uz svaku izmjenu
        - NE dodaj ništa što nije potkrijepljeno analizom ili zakonskim okvirom

        ZAKONSKI OKVIR:
        - ZOO = Zakon o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)
        - ZZK = Zakon o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17)
        - ZV = Zakon o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14)
        - ObZ = Obiteljski zakon (NN 103/15, 98/19, 47/20)

        FORMAT ODGOVORA — ISKLJUČIVO čisti JSON (bez markdown fencing-a):
        {
          "improved_contract": "Puni tekst poboljšanog ugovora s komentarima u uglatim zagradama",
          "changes_summary": [
            {
              "article": "Članak X.",
              "change_type": "IZMIJENJENO | NOVO | UKLONJENO",
              "description": "Kratki opis što je promijenjeno i zašto",
              "legal_basis": "čl. XX. ZOO"
            }
          ],
          "disclaimer": "Ovaj ugovor je generiran AI alatom i ne zamjenjuje pravni savjet ovlaštenog odvjetnika."
        }
        """;

    /// <summary>
    /// V58: Generate an improved version of the contract based on the analysis results.
    /// Uses the Heavy model to rewrite the contract with all amendments, fixes, and missing clauses.
    /// </summary>
    public async Task<GeneratedContractResult> GenerateImprovedContractAsync(
        string originalContractText,
        ContractAnalysisResult analysisResult,
        Func<string, string, string, string?, Task>? onProgress = null)
    {
        if (string.IsNullOrWhiteSpace(originalContractText))
        {
            return new GeneratedContractResult
            {
                ImprovedContract = "",
                ChangesSummary = [],
                Disclaimer = Disclaimer
            };
        }

        string text = originalContractText.Length > 15000 ? originalContractText[..15000] : originalContractText;

        try
        {
            _logger.LogInformation("V58 Generating improved contract from {Len} chars + analysis", text.Length);

            if (onProgress != null)
                await onProgress("preparing", "active", "Pripremam analizu...", null);

            // Build a concise summary of all findings for the AI
            var findingsSummary = BuildFindingsSummary(analysisResult);

            if (onProgress != null)
            {
                await onProgress("preparing", "done", "Analiza pripremljena", null);
                await onProgress("generating", "active", "Generiram poboljšani ugovor...", "AI pravni redaktor radi");
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GenerateContractPrompt),
                new UserChatMessage(
                    $"""
                    GENERIRAJ POBOLJŠANI UGOVOR na temelju ove analize.

                    ════════════════════════════════════════════════
                    ORIGINALNI UGOVOR:
                    ════════════════════════════════════════════════
                    {text}

                    ════════════════════════════════════════════════
                    REZULTATI PRAVNE REVIZIJE:
                    ════════════════════════════════════════════════
                    {findingsSummary}

                    ════════════════════════════════════════════════
                    IDEALNI REFERENTNI PREDLOŽAK (za usporedbu):
                    ════════════════════════════════════════════════
                    {IdealContractTemplate}
                    """)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                MaxOutputTokenCount = 16384  // Contracts can be long
            };

            var completion = await _client.CompleteChatAsync(messages, options);
            var responseJson = completion.Value.Content[0].Text;

            if (onProgress != null)
            {
                await onProgress("generating", "done", "Ugovor generiran", null);
                await onProgress("reviewing", "active", "Završna provjera...", null);
            }

            _logger.LogDebug("V58 Generated contract response: {Len} chars", responseJson.Length);

            var result = ParseGeneratedContract(responseJson);

            if (onProgress != null)
                await onProgress("reviewing", "done", "Provjera završena", null);

            _logger.LogInformation("V58 Improved contract generated: {ContractLen} chars, {Changes} changes",
                result.ImprovedContract.Length, result.ChangesSummary.Count);

            return result;
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "V58 Generate contract failed — OpenRouter API error (HTTP {Status})", ex.Status);
            return new GeneratedContractResult
            {
                ImprovedContract = "",
                ChangesSummary = [],
                Disclaimer = Disclaimer,
                Error = $"AI generiranje nije uspjelo: API greška (HTTP {ex.Status}). Pokušajte ponovno."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "V58 Generate contract failed");
            return new GeneratedContractResult
            {
                ImprovedContract = "",
                ChangesSummary = [],
                Disclaimer = Disclaimer,
                Error = "AI generiranje nije uspjelo. Pokušajte ponovno ili kontaktirajte podršku."
            };
        }
    }

    /// <summary>
    /// Build a concise text summary of all analysis findings for the contract generation prompt.
    /// </summary>
    private static string BuildFindingsSummary(ContractAnalysisResult analysis)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Vrsta dokumenta: {analysis.DocumentType}");
        sb.AppendLine($"Ukupni rizik: {analysis.OverallRisk} ({analysis.RiskScore}/100)");
        sb.AppendLine($"Sažetak: {analysis.Summary}");
        sb.AppendLine();

        if (analysis.RedFlags.Count > 0)
        {
            sb.AppendLine("CRVENE ZASTAVICE:");
            foreach (var f in analysis.RedFlags)
                sb.AppendLine($"  • {f.Title}: {f.Description} [Pravna osnova: {f.LegalBasis}]");
            sb.AppendLine();
        }

        if (analysis.Warnings.Count > 0)
        {
            sb.AppendLine("UPOZORENJA:");
            foreach (var w in analysis.Warnings)
                sb.AppendLine($"  • {w.Title}: {w.Description}");
            sb.AppendLine();
        }

        if (analysis.LogicalTraps.Count > 0)
        {
            sb.AppendLine("LOGIČKE ZAMKE:");
            foreach (var t in analysis.LogicalTraps)
                sb.AppendLine($"  • [{t.Severity}] {t.Title}: {t.Explanation} (članci: {string.Join(", ", t.Articles)})");
            sb.AppendLine();
        }

        if (analysis.SuggestedAmendments.Count > 0)
        {
            sb.AppendLine("PREDLOŽENE IZMJENE:");
            foreach (var a in analysis.SuggestedAmendments)
                sb.AppendLine($"  • {a.ArticleReference}: Trenutno: \"{a.CurrentText}\" → Predloženo: \"{a.ProposedText}\" Razlog: {a.Rationale}");
            sb.AppendLine();
        }

        if (analysis.MandatoryClauses.Count > 0)
        {
            var missing = analysis.MandatoryClauses.Where(mc => !mc.Present).ToList();
            if (missing.Count > 0)
            {
                sb.AppendLine("NEDOSTAJUĆE ZAŠTITNE KLAUZULE:");
                foreach (var mc in missing)
                    sb.AppendLine($"  • [{mc.Importance}] {mc.Clause}: {mc.Description}");
                sb.AppendLine();
            }
        }

        if (analysis.CrossArticleIssues.Count > 0)
        {
            sb.AppendLine("STRUKTURNI PROBLEMI (cross-article):");
            foreach (var ca in analysis.CrossArticleIssues)
                sb.AppendLine($"  • {ca.Title} (članci: {string.Join(", ", ca.LinkedArticles)}): {ca.Description}");
            sb.AppendLine();
        }

        if (analysis.IdealContractDeviations.Count > 0)
        {
            sb.AppendLine("DEVIJACIJE OD IDEALNOG UGOVORA:");
            foreach (var d in analysis.IdealContractDeviations)
                sb.AppendLine($"  • [{d.Severity}] {d.Element}: Idealno: \"{d.IdealText}\" — Stvarno: \"{d.ActualText}\" — Preporuka: {d.Recommendation}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parse the AI-generated improved contract JSON response.
    /// </summary>
    private GeneratedContractResult ParseGeneratedContract(string json)
    {
        var result = new GeneratedContractResult { Disclaimer = Disclaimer };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            result.ImprovedContract = GetString(root, "improved_contract");

            if (root.TryGetProperty("changes_summary", out var csArray) && csArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in csArray.EnumerateArray())
                {
                    result.ChangesSummary.Add(new ContractChange
                    {
                        Article = GetString(item, "article"),
                        ChangeType = GetString(item, "change_type"),
                        Description = GetString(item, "description"),
                        LegalBasis = GetStringOrNull(item, "legal_basis") ?? ""
                    });
                }
            }

            if (root.TryGetProperty("disclaimer", out var disc) && disc.ValueKind == JsonValueKind.String)
            {
                result.Disclaimer = disc.GetString() ?? Disclaimer;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V58 Failed to parse generated contract JSON");
            // Try to extract just the text if JSON parsing fails
            result.ImprovedContract = json;
        }

        return result;
    }
}
