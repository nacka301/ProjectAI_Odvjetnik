using System.Text.RegularExpressions;

namespace Backend.Services;

/// <summary>
/// V75: Dynamic Few-Shot Routing for contract analysis.
/// 
/// Detects the contract type from text using keyword matching,
/// then returns type-specific few-shot examples and ideal templates
/// so the AI receives calibrated guidance for each contract type.
/// 
/// Supported types:
///   - Kupoprodajni (sale/purchase — existing default)
///   - Najam (lease/rent)
///   - Građenje (construction)
///   - Doživotno uzdržavanje (lifetime maintenance)
///   - Ostalo (fallback — uses kupoprodajni examples)
/// </summary>
public static class ContractFewShotRouter
{
    // ══════════════════════════════════════════════════════════
    //  CONTRACT TYPE ENUM
    // ══════════════════════════════════════════════════════════

    public enum ContractType
    {
        Kupoprodajni,
        Najam,
        Gradnja,
        DozivotnoUzdrzavanje,
        Ostalo
    }

    // ══════════════════════════════════════════════════════════
    //  DETECTION — keyword-based, no LLM call needed
    // ══════════════════════════════════════════════════════════

    // Ordered by specificity: more specific patterns first
    private static readonly (ContractType Type, Regex Pattern, int Weight)[] DetectionRules =
    [
        // Doživotno uzdržavanje — most specific, check first
        (ContractType.DozivotnoUzdrzavanje, new Regex(
            @"do[zž]ivotno\s+uzdr[zž]avanje|ugovor\s+o\s+do[zž]ivotnom|" +
            @"primatelj\s+uzdr[zž]avanja|davatelj\s+uzdr[zž]avanja|" +
            @"obveza\s+uzdr[zž]avanja|smrt\s+primatelja|" +
            @"do[zž]ivotna\s+renta|uzdr[zž]avanje\s+do\s+smrti",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), 10),

        // Građenje (construction contract)
        (ContractType.Gradnja, new Regex(
            @"ugovor\s+o\s+gra[đd]enju|ugovor\s+o\s+izgradnji|" +
            @"izvo[đd]a[čc]\s+radova|naru[čc]itelj\s+radova|" +
            @"gra[đd]evinsk[aeiou]\s+(radov|dozvol)|" +
            @"rok\s+završetka\s+radova|uporabna\s+dozvola\s+.{0,30}gra[đd]|" +
            @"gra[đd]evinski\s+projekt|glavni\s+projekt|izvedbeni\s+projekt|" +
            @"privremena\s+situacija|okončana\s+situacija|primopredaj[aeu]\s+radova",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), 10),

        // Najam (lease/rent)
        (ContractType.Najam, new Regex(
            @"ugovor\s+o\s+najmu|ugovor\s+o\s+zakupu|najmod[ao]v[aeiou]c|" +
            @"najmoprim[aeiou]c|zakupod[ao]v[aeiou]c|zakupnik|" +
            @"mjese[čc]na\s+najamnina|mjese[čc]na\s+zakupnina|" +
            @"trajanje\s+najma|trajanje\s+zakupa|" +
            @"otkaz\s+najma|otkaz\s+zakupa|otkazni\s+rok|" +
            @"najamnin[aeu]|zakupnin[aeu]|podnajam",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), 10),

        // Kupoprodajni (sale/purchase) — broadest, last
        (ContractType.Kupoprodajni, new Regex(
            @"kupoprodajni|ugovor\s+o\s+kupoprodaji|ugovor\s+o\s+prodaji|" +
            @"prodavatelj|kupac|kupoprodajna\s+cijena|" +
            @"clausula\s+intabulandi|uknji[zž]ba\s+prava\s+vlasništva|" +
            @"kapara|odustatnina|prijenos\s+vlasni[šs]tva",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), 5),
    ];

    /// <summary>
    /// Detect contract type from text using keyword pattern matching.
    /// Returns the type with the most keyword hits, weighted by specificity.
    /// No LLM call — runs in microseconds.
    /// </summary>
    internal static ContractType DetectContractType(string contractText)
    {
        if (string.IsNullOrWhiteSpace(contractText))
            return ContractType.Ostalo;

        // Score each type by counting weighted keyword matches
        var scores = new Dictionary<ContractType, int>();

        foreach (var (type, pattern, weight) in DetectionRules)
        {
            int matchCount = pattern.Matches(contractText).Count;
            if (matchCount > 0)
                scores[type] = matchCount * weight;
        }

        if (scores.Count == 0)
            return ContractType.Ostalo;

        // Return the type with highest weighted score
        return scores.OrderByDescending(kv => kv.Value).First().Key;
    }

    /// <summary>
    /// Get the human-readable Croatian name for the contract type.
    /// </summary>
    internal static string GetContractTypeName(ContractType type) => type switch
    {
        ContractType.Kupoprodajni => "Kupoprodajni ugovor",
        ContractType.Najam => "Ugovor o najmu / zakupu",
        ContractType.Gradnja => "Ugovor o građenju",
        ContractType.DozivotnoUzdrzavanje => "Ugovor o doživotnom uzdržavanju",
        ContractType.Ostalo => "Ostalo (generički ugovor)",
        _ => "Nepoznat"
    };

    // ══════════════════════════════════════════════════════════
    //  FEW-SHOT EXAMPLES — type-specific
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a type-specific few-shot examples block for injection into the system prompt.
    /// Each block contains ISPRAVAN + NEISPRAVAN examples calibrated for that contract type.
    /// </summary>
    internal static string GetFewShotExamples(ContractType type) => type switch
    {
        ContractType.Kupoprodajni => FewShotKupoprodajni,
        ContractType.Najam => FewShotNajam,
        ContractType.Gradnja => FewShotGradnja,
        ContractType.DozivotnoUzdrzavanje => FewShotDozivotnoUzdrzavanje,
        ContractType.Ostalo => FewShotKupoprodajni, // Fallback to kupoprodajni
        _ => FewShotKupoprodajni
    };

    /// <summary>
    /// Returns a type-specific ideal contract template for RAG comparison.
    /// </summary>
    internal static string GetIdealContractTemplate(ContractType type) => type switch
    {
        ContractType.Kupoprodajni => IdealKupoprodajni,
        ContractType.Najam => IdealNajam,
        ContractType.Gradnja => IdealGradnja,
        ContractType.DozivotnoUzdrzavanje => IdealDozivotnoUzdrzavanje,
        ContractType.Ostalo => IdealKupoprodajni, // Fallback
        _ => IdealKupoprodajni
    };

    // ══════════════════════════════════════════════════════════
    //  KUPOPRODAJNI — existing baseline (moved from ContractAnalysisService)
    // ══════════════════════════════════════════════════════════

    private const string FewShotKupoprodajni = """
        DIO E: FEW-SHOT PRIMJER — KAKO ANALIZIRATI KUPOPRODAJNI UGOVOR

        TIP UGOVORA: Kupoprodajni ugovor za nekretninu.
        Ključni elementi: cijena, kapara, clausula intabulandi, tereti, predaja posjeda.

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
        """;

    private const string IdealKupoprodajni = """
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
           - Zemljišnoknjižni uložak (ZK uložak)
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

        4. TERETI I HIPOTEKE
           - Izjava o postojećim teretima (hipoteka, služnost, plodouživanje)
           - Mehanizam brisanja s KONKRETNIM rokom
           - Isplata UVJETOVANA brisanjem tereta — ne obrnuto
           - Brisovnica: obveza prodavatelja da ishodi brisovnicu

        5. CLAUSULA INTABULANDI
           - Bezuvjetna clausula intabulandi
           - Ovjerena kod javnog bilježnika
           - Bez dodatnih uvjeta osim plaćanja cijene

        6. PRIJENOS POSJEDA
           - Rok predaje nekretnine u posjed kupcu
           - Obveza ispražnjenja nekretnine
           - Razgraničenje režijskih troškova (datum prijelaza)
           - Primopredajni zapisnik

        7. ZAŠTITNE KLAUZULE
           - Raskidni uvjet
           - Ugovorna kazna za zakašnjenje
           - Klauzula o naknadi štete
           - Energetski certifikat
           - Izjava o legalnosti gradnje (uporabna dozvola)

        8. ZAVRŠNE ODREDBE
           - Mjerodavno pravo: RH
           - Nadležni sud
           - Broj primjeraka
           - Troškovi javnog bilježnika i poreza
        """;

    // ══════════════════════════════════════════════════════════
    //  NAJAM — lease/rent contract
    // ══════════════════════════════════════════════════════════

    private const string FewShotNajam = """
        DIO E: FEW-SHOT PRIMJER — KAKO ANALIZIRATI UGOVOR O NAJMU / ZAKUPU

        TIP UGOVORA: Ugovor o najmu ili zakupu nekretnine.
        Ključni elementi: najamnina, trajanje, otkazni rok, jamčevina (depozit),
        obveze održavanja, vraćanje nekretnine.

        PRIMJER: Standardni ugovor o najmu stana na 12 mjeseci, s jasnom najamninom,
        depozitom od 2 najamnine, otkaznim rokom 60 dana i inventarnom listom.

        ISPRAVAN ODGOVOR za takav ugovor:
        - document_type: "Ugovor o najmu"
        - summary: "Ugovor o najmu stana u XY na 12 mj., najamnina Z EUR/mj."
        - checklist: uređenost najamnine, depozit, otkazni rok, primopredaja, održavanje
        - red_flags: PRAZAN ARRAY [] — standardni najam nema crvenih zastavica
        - warnings: eventualno "nedostaje inventarna lista" ili "nema energetskog certifikata"
        - mandatory_clauses: trajanje, otkazni rok, depozit, obveze održavanja
        - logical_traps: PRAZAN ARRAY [] — standardni najam nema zamki
        - ideal_contract_deviations: minor devijacije severity="NIZAK"
        - Ukupni rizik: NIZAK (0-29)

        NEISPRAVAN ODGOVOR (klasične greške kod najma):
        ❌ Tražiti clausulu intabulandi u ugovoru o najmu — NIJE RELEVANTNO za najam
        ❌ Tražiti kaparu — kod najma postoji JAMČEVINA (depozit), ne kapara
        ❌ Označiti odsutnost escrow računa kao rizik — najam ne zahtijeva escrow
        ❌ Prijaviti "zamku" za standardni otkazni rok od 30-90 dana
        ❌ Zahtijevati brisovnicu — tereti na nekretnini ne utječu na najam kao kod kupoprodaje
        ❌ Koristiti kriterije kupoprodajnog ugovora za evaluaciju najma
        """;

    private const string IdealNajam = """
        IDEALNI UGOVOR O NAJMU NEKRETNINE — REFERENTNI PREDLOŽAK

        Ovaj predložak sadrži sve elemente koje kvalitetan ugovor o najmu
        u Republici Hrvatskoj MORA sadržavati radi zaštite obiju stranaka.

        1. IDENTIFIKACIJA STRANAKA
           - Najmodavac: ime/prezime/naziv, OIB, adresa
           - Najmoprimac: ime/prezime/naziv, OIB, adresa
           - Dokaz vlasništva ili ovlaštenja za davanje u najam

        2. PREDMET NAJMA
           - Precizna oznaka nekretnine (adresa, kat, površina)
           - Namjena (stanovanje / poslovni prostor)
           - Opis postojećeg stanja pri useljenju
           - Inventarna lista s fotografijama (prilog ugovoru)

        3. TRAJANJE NAJMA
           - Određeno ili neodređeno vrijeme
           - Datum početka najma
           - Mogućnost produženja i uvjeti
           - Primjena čl. 575. ZOO za određeno, čl. 576. ZOO za neodređeno

        4. NAJAMNINA I TROŠKOVI
           - Iznos mjesečne najamnine (EUR)
           - Rok plaćanja (npr. do 5. u mjesecu)
           - Što je uključeno u najamninu (režije da/ne)
           - Mehanizam usklađivanja (inflacija, HICP indeks)
           - Zabrana jednostranog povećanja bez ugovorene formule

        5. JAMČEVINA (DEPOZIT)
           - Iznos (uobičajeno 1-3 mjesečne najamnine)
           - Uvjeti vraćanja pri isteku najma
           - Rok vraćanja (npr. 15 dana od primopredaje)
           - U koje svrhe se može koristiti (šteta, neplaćena najamnina)

        6. OTKAZNI ROK I RASKID
           - Otkazni rok (min. 30 dana za stanove, čl. 576. st. 3. ZOO)
           - Razlozi za izvanredni otkaz bez otkaznog roka
           - Pravo najmodavca na raskid: neplaćanje (2+ mjeseca), oštećenje,
             neovlašteni podnajam
           - Pravo najmoprimca na raskid: neodržavanje, smetanje posjeda

        7. OBVEZE STRANAKA
           - Najmodavac: predaja u ispravnom stanju, velike popravke,
             mirno uživanje (čl. 557.-559. ZOO)
           - Najmoprimac: plaćanje najamnine, redovno održavanje,
             čuvanje nekretnine, nepoduzimanje preinaka bez pristanka

        8. VRAĆANJE NEKRETNINE
           - Stanje pri vraćanju (uobičajeno trošenje dozvoljeno)
           - Primopredajni zapisnik
           - Obračun depozita i eventualnih šteta
           - Uklanjanje osobnih stvari

        9. ZAVRŠNE ODREDBE
           - Mjerodavno pravo: RH
           - Nadležni sud
           - Zabrana podnajma bez suglasnosti (čl. 569. ZOO)
           - Prijava najma Poreznoj upravi (čl. 62. Zakona o porezu na dohodak)
        """;

    // ══════════════════════════════════════════════════════════
    //  GRAĐENJE — construction contract
    // ══════════════════════════════════════════════════════════

    private const string FewShotGradnja = """
        DIO E: FEW-SHOT PRIMJER — KAKO ANALIZIRATI UGOVOR O GRAĐENJU

        TIP UGOVORA: Ugovor o građenju (izvođenje građevinskih radova).
        Ključni elementi: opseg radova, cijena (fiksa/troškovnička), rokovi,
        građevinska dozvola, situacije, jamstvo za kvalitetu.

        PRIMJER: Ugovor o gradnji obiteljske kuće sa fiksnom cijenom,
        rokom završetka 12 mjeseci, retenucijskim jamstvom 5%, penalima za kašnjenje.

        ISPRAVAN ODGOVOR za takav ugovor:
        - document_type: "Ugovor o građenju"
        - summary: "Ugovor o građenju kuće u XY, cijena Z EUR, rok 12 mj."
        - checklist: građevinska dozvola, opseg radova, dinamički plan, primopredaja
        - red_flags: PRAZAN ARRAY [] — ugovor s jasnim rokovima, penalima i jamstvom je uredan
        - warnings: eventualno "nedostaje detaljan troškovnik" ili "nepreciziran postupak za višak radova"
        - mandatory_clauses: rok završetka, ugovorna kazna za kašnjenje, jamstvo za kvalitetu,
          retenucija, primopredajni zapisnik, uporabna dozvola
        - logical_traps: PRAZAN ARRAY [] — standardni ugovor nema zamki
        - Ukupni rizik: NIZAK (0-29)

        NEISPRAVAN ODGOVOR (klasične greške kod građenja):
        ❌ Tražiti clausulu intabulandi — NIJE relevantno za ugovor o izvođenju radova
        ❌ Tražiti kaparu — kod građenja postoji AVANS ili SITUACIJE, ne kapara
        ❌ Primijeniti kriterije kupoprodaje na ugovor o građenju
        ❌ Ignorirati jamstveni rok za skrivene nedostatke (čl. 633. ZOO — 10 godina za građevine)
        ❌ Ne prepoznati razliku između fiksne i troškovničke cijene
        ❌ Označiti ugovornu kaznu za kašnjenje kao "zamku za izvođača" —
           to je standardna zaštitna klauzula naručitelja
        """;

    private const string IdealGradnja = """
        IDEALNI UGOVOR O GRAĐENJU — REFERENTNI PREDLOŽAK

        Ovaj predložak sadrži sve elemente koje kvalitetan ugovor o građenju
        u Republici Hrvatskoj MORA sadržavati (čl. 620.-636. ZOO).

        1. IDENTIFIKACIJA STRANAKA
           - Naručitelj: ime/prezime/naziv, OIB, adresa
           - Izvođač: naziv tvrtke, OIB, adresa sjedišta
           - Registracija izvođača za obavljanje građevinskih djelatnosti
           - Odgovorna osoba izvođača (voditelj gradnje)

        2. PREDMET UGOVORA — OPSEG RADOVA
           - Detaljan opis radova sa tehničkom specifikacijom
           - Pozivanje na glavni/izvedbeni projekt
           - Troškovnik (prilog ugovoru) s jediničnim cijenama
           - Jasna razlika: "ključ u ruke" vs. troškovničko obračunavanje
           - Postupak za više/manje radova i nepredviđene radove

        3. CIJENA I PLAĆANJE
           - Ukupna ugovorena cijena (fiksna ili troškovnička)
           - Dinamika plaćanja: avansna uplata + privremene situacije + okončana situacija
           - Rok plaćanja situacija (npr. 15 dana od ovjere)
           - PDV klauzula (25% ili prijenos porezne obveze)
           - Retenucija: 5-10% zadržavanja do završnog pregleda

        4. ROKOVI
           - Rok početka radova
           - Dinamički plan (milestoneovi) s međurokovima
           - Rok završetka radova (konačni)
           - Uvjeti za produženje roka (viša sila, nepredviđeni uvjeti, kašnjenje naručitelja)

        5. UGOVORNA KAZNA ZA KAŠNJENJE
           - Dnevni/tjedni penali za prekoračenje roka (npr. 0,1-0,5% dnevno)
           - Maksimalni iznos penala (uobičajeno 10% ukupne cijene)
           - Pravo naručitelja na raskid ako kašnjenje > X dana

        6. GRAĐEVINSKA DOZVOLA I DOKUMENTACIJA
           - Tko pribavlja dozvolu (naručitelj ili izvođač)
           - Izvođač odgovoran za građevinski dnevnik (čl. 58. Zakona o gradnji)
           - Obveza izvođača za ispitivanje kvalitete materijala
           - Atesti i certifikati za ugrađeni materijal

        7. JAMSTVO ZA KVALITETU (GARANCIJA)
           - Jamstveni rok: min. 2 godine za vidljive nedostatke
           - Skriveni nedostatci: 10 godina (čl. 633. ZOO)
           - Bankarska garancija ili retenucija kao osiguranje jamstva
           - Postupak prijave i otklanjanja nedostataka u jamstvenom roku

        8. PRIMOPREDAJA RADOVA
           - Tehnički pregled i uporabna dozvola
           - Primopredajni zapisnik s popisom nedostataka
           - Rok za otklanjanje nedostataka
           - Okončana situacija nakon otklanjanja svih nedostataka
           - Predaja tehničke dokumentacije naručitelju

        9. RASKID UGOVORA
           - Pravo naručitelja na raskid u svako doba (uz naknadu, čl. 629. ZOO)
           - Pravo izvođača na raskid: neplaćanje, neizvršne dozvole
           - Postupak i rok obračuna izvršenih radova pri raskidu

        10. ZAVRŠNE ODREDBE
            - Mjerodavno pravo: RH
            - Nadležni sud
            - Osiguranje (polica profesionalne odgovornosti izvođača)
            - Podugovaranje: uvjeti za angažiranje podizvođača
        """;

    // ══════════════════════════════════════════════════════════
    //  DOŽIVOTNO UZDRŽAVANJE
    // ══════════════════════════════════════════════════════════

    private const string FewShotDozivotnoUzdrzavanje = """
        DIO E: FEW-SHOT PRIMJER — KAKO ANALIZIRATI UGOVOR O DOŽIVOTNOM UZDRŽAVANJU

        TIP UGOVORA: Ugovor o doživotnom uzdržavanju (čl. 579.-585. ZOO).
        Ključni elementi: obveze uzdržavanja, prijenos imovine, sudska ovjera,
        zaštita primatelja, nužni nasljednici.

        PRIMJER: Ugovor o doživotnom uzdržavanju s jasno definiranim obvezama
        (stanovanje, hrana, njega, liječenje, pokop), sudski ovjeren,
        s precizno opisanom nekretninom koja prelazi na davatelja nakon smrti.

        ISPRAVAN ODGOVOR za takav ugovor:
        - document_type: "Ugovor o doživotnom uzdržavanju"
        - summary: "Ugovor o doživotnom uzdržavanju — primatelj XY (god. X),
          nekretnina Z, obveza skrbi do smrti"
        - checklist: sudska ovjera, identifikacija stranaka, obveze uzdržavanja,
          opis nekretnine, prava nužnih nasljednika
        - red_flags: PRAZAN ARRAY [] ako je sudski ovjeren i ima jasne obveze
        - warnings: eventualno "nema mehanizma nadzora ispunjavanja obveza"
          ili "nije regulirano smještanje primatelja u dom"
        - mandatory_clauses: sudska ovjera, precizne obveze uzdržavanja,
          pravo primatelja na raskid, upis zabilježbe u ZK
        - logical_traps: PRAZAN ARRAY [] za uredan ugovor
        - Ukupni rizik: NIZAK (0-29)

        NEISPRAVAN ODGOVOR (klasične greške kod doživotnog uzdržavanja):
        ❌ Tražiti kaparu ili escrow — NIJE RELEVANTNO za doživotno uzdržavanje
        ❌ Prijaviti "zamku" za prijenos vlasništva nakon smrti — to je SUŠTINA ovog ugovora
        ❌ Koristiti kriterije kupoprodajnog ugovora (cijena, dinamika plaćanja)
        ❌ Ignorirati obvezu sudske ovjere (čl. 580. ZOO) — to je konstitutivni element
        ❌ Ne prepoznati rizik za primatelja ako obveze nisu dovoljno precizirane
        ❌ Zanemariti pravo primatelja na raskid ako davatelj ne ispunjava obveze (čl. 583. ZOO)
        ❌ Ne upozoriti na prava nužnih nasljednika (čl. 69.-81. Zakona o nasljeđivanju)
        """;

    private const string IdealDozivotnoUzdrzavanje = """
        IDEALNI UGOVOR O DOŽIVOTNOM UZDRŽAVANJU — REFERENTNI PREDLOŽAK

        Ovaj predložak sadrži sve elemente koje kvalitetan ugovor o doživotnom
        uzdržavanju u RH MORA sadržavati (čl. 579.-585. ZOO, NN 35/05).

        1. IDENTIFIKACIJA STRANAKA
           - Primatelj uzdržavanja: ime, OIB, datum rođenja, adresa
           - Davatelj uzdržavanja: ime/naziv, OIB, adresa
           - Poslovna sposobnost obiju stranaka (potvrda liječnika za primatelja
             ako je stariji od 75 godina — best practice)

        2. FORMA — SUDSKA OVJERA (KONSTITUTIVNI ELEMENT)
           - Ovjera od strane suca nadležnog suda (čl. 580. ZOO)
           - Sud upozorava primatelja na posljedice ugovora
           - Bez sudske ovjere: UGOVOR JE NIŠTAVAN
           - Zabilježba ugovora u zemljišnoj knjizi

        3. PREDMET — IMOVINA KOJA SE PRENOSI
           - Precizna oznaka nekretnine (k.o., k.č.br., ZK uložak)
           - Površina i opis nekretnine
           - Izjava o teretima (hipoteke, služnosti)
           - Trenutak prijenosa: NAKON SMRTI primatelja (čl. 579. ZOO)
             NAPOMENA: Razlika od ugovora o dosmrtnom uzdržavanju
             (prijenos odmah, čl. 586. ZOO)

        4. OBVEZE DAVATELJA UZDRŽAVANJA
           - PRECIZNO definirane obveze (ne općenito "uzdržavanje"):
             a) Stanovanje: primatelj ostaje u nekretnini / osigurati smještaj
             b) Prehrana: dnevni obroci, posebna prehrana po potrebi
             c) Odjeća i obuća: sezonska nabava
             d) Njega: osobna higijena, pomoć pri kretanju
             e) Liječenje: troškovi liječenja, lijekovi, bolničko liječenje
             f) Prijevoz: do liječnika, za osobne potrebe
             g) Pokop: dostojan pokop prema prilike i želje primatelja
           - Minimalni mjesečni standard (preporučeno u EUR)
           - Što ako primatelj treba profesionalnu njegu (dom)

        5. ZAŠTITA PRIMATELJA
           - Pravo na raskid ako davatelj ne ispunjava obveze (čl. 583. ZOO)
           - Pravo na raskid ako se odnos toliko pogorša da je zajednica nepodnošljiva
           - Zaštita od otuđenja nekretnine za života primatelja
           - Zabrana opterećenja nekretnine (hipoteka) bez pristanka primatelja
           - Pravo služnosti stanovanja za primatelja

        6. NUŽNI NASLJEDNICI
           - Upozorenje o pravima nužnih nasljednika (čl. 69-81. ZN)
           - Mogućnost pobijanja ugovora od strane nužnih nasljednika
           - Preporuka: pisana izjava nužnih nasljednika o upoznatosti s ugovorom

        7. OSOBNI NADZOR I EVIDENCIJA
           - Mehanizam nadzora ispunjavanja obveza (npr. godišnji izvještaj sudu)
           - Evidencija troškova liječenja i njege
           - Pravo primatelja na imenovanje nadzorne osobe

        8. RASKID I PRESTANAK
           - Smrt primatelja: obveze prestaju, nekretnina prelazi na davatelja
           - Smrt davatelja: obveze prelaze na nasljednike davatelja (čl. 584. ZOO)
           - Sporazumni raskid: moguć uz sudsku ovjeru
           - Sudski raskid: na zahtjev primatelja ili davatelja (čl. 583. ZOO)

        9. ZAVRŠNE ODREDBE
           - Mjerodavno pravo: RH
           - Nadležni sud
           - Upis zabilježbe u zemljišnu knjigu
           - Izjave stranaka o slobodnoj volji i razumijevanju posljedica
        """;
}
