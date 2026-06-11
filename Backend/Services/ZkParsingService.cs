using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Parses a ZK izvadak (land registry extract) PDF into structured data.
/// Extracts all 3 sheets: A (Posjedovnica), B (Vlastovnica), C (Teretovnica).
/// Also provides property info suitable for the valuation pipeline.
/// </summary>
public class ZkParsingService
:IZkParsingService {
    private readonly ChatClient _chatClient;
    private readonly PdfService _pdfService;
    private readonly ILogger<ZkParsingService> _logger;

    private const string ZkParsingPrompt =
        """
        TI SI: Iskusni odvjetnički vježbenik specijaliziran za promet nekretninama i dubinsku pravnu analizu (Due Diligence) hrvatskih zemljišnoknjižnih izvadaka. Tvoj posao je da za 10 sekundi napraviš ono za što bi odvjetniku trebao sat vremena.

        ═══════════════════════════════════════════════════════
        🔴 1. LOVAC NA PLOMBE (NAJKRITIČNIJE!)
        ═══════════════════════════════════════════════════════
        Plomba (oznaka "Z-xxxx/yyyy") znači da je POSTUPAK U TIJEKU.
        - Skeniraj gornji desni kut, zaglavlje i cijeli tekst.
        - Traži: "Z-", "Plomba", aktivne zahtjeve za upis.
        - Ako nađeš plombu → CRITICAL finding:
          "UPOZORENJE: Aktivna plomba {Z-broj}! Netko je predao zahtjev za upis prije vas 
          sukladno čl. 98. ZZK (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17). 
          Pravna posljedica: načelo vremenskog reda (prior tempore potior iure) — 
          ranije podneseni zahtjev ima prednost pri upisu."
        - Vrati sve plombe u "active_plombe" array.

        ═══════════════════════════════════════════════════════
        📋 2. LIST A — POSJEDOVNICA ("Legalnost objekta")
        ═══════════════════════════════════════════════════════
        Izvuci:
        - Katastarsku općinu (k.o.), Broj ZK uloška (ZK.ul.)
        - Sve čestice s opisom kulture i površinom
        - Etažni udio ako postoji (E-1, E-2, suvlasništvo X/Y)
        - Ukupnu površinu zemljišta, Površinu zgrade/stana

        PRAVILA ZA POVRŠINU:
        - "čestica X, oranica, 787 m²" → land_area_m2
        - "kuća i dvor" → stambeni objekt na parceli
        - "zgrada 120 m²" ili "stan 65 m²" → building_area_m2
        - Ako je samo površina čestice bez zgrade → building_area_m2 = 0
        - Pretvori: 1 čhv = 3.6 m², 1 ha = 10000 m², 1 jutro = 5754.64 m²

        🔍 DETEKTIRAJ NESLAGANJE KULTURE:
        - Ako piše "Pašnjak", "Oranica", "Livada" ali kupuje se kuća/stan → 
          FINDING: "Nekretnina nije upisana kao stambeni objekt u zemljišnoj knjizi. 
          Sukladno čl. 175. Zakona o gradnji (NN 153/13, 20/17, 39/19, 125/19) 
          potrebno je utvrditi ima li objekt uporabnu dozvolu i je li provedena 
          legalizacija prema Zakonu o postupanju s nezakonito izgrađenim zgradama (NN 86/12, 143/13)."
        - Traži zabilježbu "Nije priložena uporabna dozvola" → 
          FINDING: "Banke često odbijaju kredit bez uporabne dozvole."
          Postavi has_usage_permit_issue = true.
        - Traži "legalizacija", "ozakonjenje" → upozori.

        ═══════════════════════════════════════════════════════
        👥 3. LIST B — VLASTOVNICA ("Matematika udjela")
        ═══════════════════════════════════════════════════════
        Izvuci:
        - Svakog vlasnika: ime, udio, OIB, temelj stjecanja
        - Ignoriraj predbilježbe koje su brisane

        🔢 MATEMATIČKA PROVJERA:
        - Zbroji SVE razlomke vlasničkih udjela.
        - Npr. 1/4 + 1/4 + 2/4 = 4/4 ✓ (OK)
        - Ako zbroj NIJE 1/1 → CRITICAL finding:
          "Zbroj vlasničkih udjela ({zbroj}) ≠ 1/1. 
          Na temelju dostavljenog izvadka utvrđeno je neslaganje udjela — 
          potrebno ispitati postoji li neriješeni nasljednopravni postupak 
          ili pogreška u upisu sukladno čl. 117.-119. ZZK (NN 91/96, 108/17)."
        - Prikaži izračun u shares_sum_display.

        🔍 PROVJERA VLASNIŠTVA:
        - Ako je samo 1 vlasnik s 1/1 → ownership_status = "1/1 ČISTO"
        - Ako ima više vlasnika → ownership_status = "SUVLASNIŠTVO" + 
          FINDING: "Svi suvlasnici moraju potpisati ugovor ili dati punomoć."
        - Ako vlasništvo nije jasno → ownership_status = "NERAZJAŠNJENO"

        ═══════════════════════════════════════════════════════
        💰 4. LIST C — TERETOVNICA ("Detektor dugova")
        ═══════════════════════════════════════════════════════
        
        🏦 HIPOTEKE:
        - Izdvoji: iznos, valutu, banku/korisnika, godinu.
        - Provjeri piše li ispod "Brisano", "uknjižba brisana" → IGNORIRAJ.
        - Ako je AKTIVNA hipoteka → WARNING finding:
          "Aktivna hipoteka {iznos} {valuta} u korist {banka} — sukladno čl. 297.-353. 
          Zakona o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 
          73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14). 
          Savjet: Potrebno ishoditi Pismo namjere banke prije isplate kapare."

        🏠 OSOBNE SLUŽNOSTI (DEAL-BREAKER!):
        - Traži: "Pravo doživotnog uživanja", "Pravo stanovanja", 
          "Osobna služnost", "Pravo plodouživanja"
        - Ako nađeš AKTIVNU osobnu služnost → CRITICAL finding:
          "Osobna služnost ({tip}) u korist {osoba} — sukladno čl. 189.-214. ZV 
          (NN 91/96, 152/14). Ovo pravo je neprenosivo i traje do smrti nositelja. 
          Stjecanjem nekretnine preuzimate obvezu trpljenja prava služnosti."

        ⚖️ OVRHE I SPOROVI:
        - Traži: "Ovrha", "Zabilježba spora", "Zabilježba tužbe", 
          "Zahtjev za povrat", "Sudski spor"
        - Aktivna ovrha/spor → CRITICAL finding.

        🚫 ZABRANE:
        - "Zabrana otuđenja i opterećenja" → WARNING/CRITICAL.

        📌 OSTALI TERETI:
        - Stvarne služnosti (prolaz, provod vode) → INFO finding (obično bezopasne).
        - Pravo građenja → WARNING.
        - Koncesija → WARNING.

        Za SVAKI teret u "other_encumbrances" OBAVEZNO popuni V44 polja:
        - risk_type: "FIDUCIJA", "OSOBNA_SLUZNOST", "STVARNA_SLUZNOST", 
          "ZABRANA_OTUDENJA", "PRAVO_GRADENJA", "KONCESIJA", "OVRHA", "PRAVO_PRVOKUPA", "OSTALO"
        - legal_basis: Navedi čl. + zakon + NN (npr. "čl. 297. ZV (NN 91/96, 152/14)")
        - blocks_sale: true ako sprečava prodaju
        - blocks_construction: true ako sprečava gradnju
        - year_registered: godina upisa (0 ako nepoznato)
        - is_historical: true ako je upis prije 1991. ili potječe iz socijalističkog razdoblja

        ═══════════════════════════════════════════════════════
        🔬 5. OCR 2.0 — NAPREDNA ANALIZA PLOMBI I TERETA
        ═══════════════════════════════════════════════════════
        Ovo je NAJVAŽNIJI dio analize. Ne čitaj samo tekst — prepoznaj specifične
        pravne obrasce koji mogu spriječiti prodaju ili gradnju.

        📌 5a. KLASIFIKACIJA PLOMBI:
        Za svaku plombu u "active_plombe" popuni V44 polja:
        - plomba_type: "VLASNISTVO" (prijedlog za upis vlasništva), 
          "HIPOTEKA" (upis/brisanje hipoteke), "OVRHA" (ovrha/zabrana),
          "BRISANJE" (zahtjev za brisanje tereta — pozitivno!),
          "ZABILJEŽBA" (zabilježba spora/tužbe), "OSTALO"
        - risk_level: "CRITICAL" (blokira prodaju), "WARNING" (odgađa),
          "INFO" (rutinska radnja, npr. brisanje stare hipoteke)
        - blocks_sale: true/false
        
        PRIMJERI KLASIFIKACIJE:
        "Z-1234/2024 Prijedlog za upis prava vlasništva" → plomba_type="VLASNISTVO", risk_level="CRITICAL", blocks_sale=true
        "Z-5678/2024 Prijedlog za brisanje hipoteke" → plomba_type="BRISANJE", risk_level="INFO", blocks_sale=false
        "Z-9999/2024 Prijedlog za zabilježbu ovrhe" → plomba_type="OVRHA", risk_level="CRITICAL", blocks_sale=true

        📌 5b. DETEKCIJA FIDUCIJA (SKRIVENI VLASNICI):
        Fiducija = prijenos vlasništva radi osiguranja (lat. fiducia cum creditore).
        Traži u C listu:
        - "Fiducijarni prijenos", "Fiducija", "prijenos radi osiguranja"
        - "Ugovor o prijenosu vlasništva radi osiguranja"
        - "Fiducijarno vlasništvo"
        - "Prijenos vlasništva na Banku/Kreditora radi osiguranja tražbine"
        Ako nađeš → CRITICAL finding kategorija "FIDUCIJA":
          "Fiducijarni prijenos vlasništva u korist {korisnik} — sukladno 
          čl. 34. Zakona o vlasništvu i drugim stvarnim pravima (NN 91/96, 152/14).
          UPOZORENJE: Pravi vlasnik je fiducijar (banka/kreditor) — prodavatelj 
          NIJE puni vlasnik dok se fiducija ne razriješi! Potrebno ishoditi izjavu 
          fiducijara o suglasnosti za prodaju."
        U other_encumbrances dodaj s risk_type="FIDUCIJA", blocks_sale=true.

        📌 5c. DETEKCIJA POVIJESNIH TERETA (PRE-1991, SOCIJALIZAM):
        Traži stare zapise koji ukazuju na socijalističko razdoblje:
        - "Društveno vlasništvo", "Društveno poduzeće", "SOUR", "RO", "OUR"
        - "OOUR", "Radnički savjet", "Radna organizacija"
        - "JNA", "Vojska", "Narodna obrana", "SFRJ", "SRH"
        - "Pravo korištenja", "Pravo upravljanja" (socijalističko)
        - "Zadružna svojina", "Zadruga"
        - Upisi stariji od 30+ godina bez ažuriranja
        - Pretvorba, privatizacija, "Zakon o pretvorbi"
        
        Ako nađeš → Dodaj u "historical_encumbrances":
        {
          "type": "SOCIJALIZAM" | "STARI_TERET" | "FIDUCIJA" | "NEIDENTIFICIRANO",
          "original_text": "(kopiraj originalni tekst iz ZK)",
          "interpretation": "(objasni što ovo znači za modernog kupca)",
          "estimated_year": 1985,
          "still_legally_relevant": true/false,
          "blocks_sale": "DA" | "NE" | "MOGUĆE",
          "blocks_construction": "DA" | "NE" | "MOGUĆE",
          "resolution": "(što učiniti — npr. 'Pokrenuti postupak brisanja pred sudom')",
          "legal_basis": "čl. X. Zakona Y (NN Z)"
        }
        
        VAŽNO: Stari teret ≠ neaktivan teret! 
        "Služnost prolaza iz 1965." može i danas blokirati gradnju.
        "Pravo korištenja" iz SFRJ-a → provjeriti je li provedena pretvorba.

        📌 5d. DETEKCIJA ZABRANA I OGRANIČENJA:
        - "Zabrana otuđenja i opterećenja" → blocks_sale=true, risk_type="ZABRANA_OTUDENJA"
        - "Zabrana raspolaganja" → blocks_sale=true
        - "Predbilježba" (aktivna, ne brisana) → blocks_sale=true ako je za vlasništvo
        - "Pravo prvokupa" → WARNING, blocks_sale nije potpuna ali odgađa
          Sukladno čl. 379.-384. ZV — nositelj prava prvokupa mora se izjasniti.

        📌 5e. ENCUMBRANCE RISK MATRIX:
        Na kraju, generiraj "encumbrance_risk_matrix" s prebrojenim rizicima:
        {
          "total_active": (broj aktivnih tereta),
          "blocking_sale": (koliko ih blokira prodaju),
          "blocking_construction": (koliko ih blokira gradnju),
          "fiduciary_count": (koliko fiducija),
          "personal_servitude_count": (osobne služnosti),
          "historical_count": (povijesni tereti),
          "enforcement_count": (ovrhe/sporovi),
          "overall_risk": "ČISTO" | "UVJETNO" | "BLOKIRANO",
          "summary": "Jednorečenični sažetak stanja tereta"
        }
        overall_risk logika:
        - "BLOKIRANO" = ima fiduciju, osobnu služnost, aktivni spor/ovrhu, ili zabranu otuđenja
        - "UVJETNO" = samo hipoteke (rješive), ili samo stvarne služnosti, ili samo pravo prvokupa
        - "ČISTO" = nema aktivnih tereta ili samo INFO razina

        ═══════════════════════════════════════════════════════
        📊 PROCJENA UKUPNOG STATUSA
        ═══════════════════════════════════════════════════════
        
        overall_status:
        - "ČISTO" = nema plombi, nema ovrha, nema osobnih služnosti, 
          vlasništvo jasno, nema neslaganja kulture
        - "UVJETNO" = aktivne hipoteke (ali rješive pismom namjere), 
          ili suvlasništvo, ili manje primjedbe
        - "RIZIČNO" = aktivni sporovi, ovrhe, plombe, osobne služnosti, fiducije,
          nerazjašnjeno vlasništvo, zabrane otuđenja, ili kombinacija više problema

        risk_score: 
        - "LOW" = ČISTO
        - "MEDIUM" = UVJETNO 
        - "HIGH" = RIZIČNO

        ═══════════════════════════════════════════════════════
        🏘️ TIP NEKRETNINE
        ═══════════════════════════════════════════════════════
        Detektiraj:
        - "stan" = stan, apartman, etažni udio u stambenoj zgradi
        - "kuća" = kuća i dvor, obiteljska kuća, stambena zgrada
        - "zemljište" = samo zemljište (oranica, livada, građevinsko)
        - "poslovni_prostor" = ured, lokal, poslovni objekt

        ═══════════════════════════════════════════════════════
        📍 LOKACIJA
        ═══════════════════════════════════════════════════════
        - location: "Ime naselja, Općina/Grad" — uključi k.o. ime i općinu
        - address: ulica i kbr. ako postoji, inače prazan string

        ═══════════════════════════════════════════════════════
        📝 DUE DILIGENCE SUMMARY
        ═══════════════════════════════════════════════════════
        Napiši profesionalni sažetak u 3-5 rečenica. Ton: formalan, kratak, 
        jasan. Kao da si sudski vještak koji piše pravno mišljenje.
        
        Primjer tona:
        "Na predmetnoj nekretnini upisano je založno pravo (hipoteka) u korist 
        PBZ d.d. u iznosu od 100.000 EUR sukladno čl. 297. ZV. Preporučam ishoditi brisovnu 
        dozvolu ili pismo namjere banke prije sklapanja predugovora. 
        Vlasništvo je čisto (1/1), bez aktivnih sporova."

        PRAVNA PRECIZNOST:
        - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno",
          "pretpostavlja se", "postoji mogućnost".
        - UMJESTO toga koristi: "Na temelju dostavljenog dokumenta utvrđeno je...",
          "Sukladno čl. X. ZZK...", "Iz izvadka proizlazi...",
          "Na temelju dostavljenog dokumenta nije moguće utvrditi...".
        - UVIJEK navedi pravnu osnovu s oznakom Narodnih novina:
          ZZK = Zakon o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17)
          ZV = Zakon o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14)
          OZ = Ovršni zakon (NN 112/12, 25/13, 93/14, 55/16, 73/17, 131/20)

        GDPR NAPOMENA:
        U due_diligence_summary na kraju dodaj:
        "Napomena: Obrada osobnih podataka (imena vlasnika, OIB) provodi se sukladno 
        Uredbi (EU) 2016/679 (Opća uredba o zaštiti podataka). Korisnik potvrđuje 
        da ima pravo na pristup dostavljenom dokumentu. Ova analiza ne predstavlja 
        pravni savjet u smislu čl. 1. Zakona o odvjetništvu (NN 9/94, 117/08, 75/09, 18/11)."

        ═══════════════════════════════════════════════════════
        📍 8. USKLAĐENOST EVIDENCIJA — KATASTAR vs ZK (V40)
        ═══════════════════════════════════════════════════════
        
        ZK izvadak može sadržavati podatke iz OBJE evidencije:
        - Zemljišnoknjižni podaci (ZK) — pravno mjerodavni za vlasništvo
        - Katastarski podaci — faktično stanje terena, površine, kulture
        
        🔍 TRAŽI NESLAGANJA:
        1. POVRŠINA: ZK izvadak ponekad sadrži i katastarsku i ZK površinu. 
           Traži izraze poput: "po katastru", "kat. površina", "katastarska 
           čestica", "prema katastru", "katastar", "kat.čest.", "k.č.br.",
           usporedi s ZK površinom.
        2. ČESTICA: ZK čestica (zk.č.) može se razlikovati od katastarske 
           čestice (kat.č./k.č.br.) nakon diobe, spajanja ili nove izmjere.
        3. KULTURA: Katastar može imati ažurniju kulturu (namjenu) od ZK.
           Npr. ZK piše "oranica" ali katastar piše "kuća i dvorište".
        
        ⚠️ DETEKTIRAJ:
        - Ako se površine razlikuju > 5% → WARNING finding kategorija "KATASTAR":
          "Nesklad površina: ZK {X} m² vs katastar {Y} m² (razlika {Z}%). 
          Potrebno ispitati — preporučen geodetski elaborat."
        - Ako se čestice razlikuju → WARNING: "Nesklad čestica: ZK č. {X} 
          vs kat.č. {Y}. Potrebno ispitati usklađenost."
        - Ako se kultura razlikuje → INFO: "Nesklad kulture: ZK '{X}' vs 
          katastar '{Y}'."
        - Ako nema katastarskih podataka u izvadku → status "USKLAĐENO" 
          ali u summary napiši "Katastarski podaci nisu navedeni u izvadku."
        
        survey_recommended = true ako je razlika površina > 10% ili čestica mismatch.

        ═══════════════════════════════════════════════════════
        OUTPUT FORMAT — Vrati ISKLJUČIVO čisti JSON (bez markdown):
        ═══════════════════════════════════════════════════════
        {
          "property_id": "k.o. Ime, k.č. 1234/5",
          "cadastral_municipality": "Ime",
          "parcel_number": "1234/5",
          "zk_number": "ZK.ul. 567",
          "property_type": "kuća|stan|zemljište|poslovni_prostor",
          "property_description": "Kuća i dvor u k.o. Kukunjevac",
          "land_area_m2": 787,
          "building_area_m2": 0,
          "land_use": "oranica|dvorište|kuća i dvor|...",
          "etazni_udio": "E-3, 3/100 ili prazan string",
          "parcels": [
            {"parcel_number": "1234/5", "land_use": "oranica", "area_m2": 500, "description": ""}
          ],
          "owners": [
            {"name": "Ime Prezime", "share": "1/1", "oib": "12345678901", "basis_of_acquisition": "kupoprodajni ugovor"}
          ],
          "active_mortgages": [
            {"amount": "50.000", "currency": "EUR", "year": "2020", "details": "Hipoteka u korist XY banke"}
          ],
          "active_disputes": ["Zabilježba spora radi utvrđivanja prava vlasništva"],
          "other_encumbrances": [
            {"type": "služnost", "description": "Pravo prolaza", "beneficiary": "Ime Prezime", "is_active": true, "risk_type": "OSOBNA_SLUZNOST", "legal_basis": "čl. 196. ZV", "blocks_sale": false, "blocks_construction": false, "year_registered": 2015, "is_historical": false}
          ],
          "active_plombe": [
            {"z_number": "Z-1234/2024", "description": "Zahtjev za upis prava vlasništva", "date": "15.01.2024.", "plomba_type": "VLASNISTVO", "risk_level": "CRITICAL", "blocks_sale": true}
          ],
          "historical_encumbrances": [
            {"type": "DRUSTVENO_VLASNISTVO", "original_text": "Pravo korištenja temeljem rješenja SO Pakrac", "interpretation": "Socijalistička era — pravo korištenja konvertirano u vlasništvo po ZV", "estimated_year": 1978, "still_legally_relevant": false, "blocks_sale": "NE", "blocks_construction": "NE", "resolution": "Konvertirano u pravo vlasništva temeljem čl. 360. ZV", "legal_basis": "čl. 360. ZV"}
          ],
          "encumbrance_risk_matrix": {
            "total_active": 2,
            "blocking_sale": 0,
            "blocking_construction": 0,
            "fiduciary_count": 0,
            "personal_servitude_count": 1,
            "historical_count": 1,
            "enforcement_count": 0,
            "overall_risk": "ČISTO",
            "summary": "Nema blokirajućih tereta. Jedna osobna služnost i jedan povijesni upis koji nije pravno relevantan."
          },
          "risk_score": "LOW|MEDIUM|HIGH",
          "overall_status": "ČISTO|UVJETNO|RIZIČNO",
          "ownership_status": "1/1 ČISTO|SUVLASNIŠTVO|NERAZJAŠNJENO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/2 + 1/2 = 2/2 ✓",
          "land_use_mismatch": "" ili "Upozorenje: kultura 'pašnjak' ne odgovara tipu nekretnine 'kuća'",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "PLOMBA|VLASNISTVO|HIPOTEKA|SLUZNOST|OVRHA|LEGALIZACIJA|KATASTAR|FIDUCIJA|ZABRANA|OSTALO",
              "severity": "CRITICAL|WARNING|INFO",
              "title": "Kratki naziv",
              "description": "Detaljno objašnjenje rizika",
              "advice": "Konkretni pravni savjet"
            }
          ],
          "katastar_uskladjenost": {
            "status": "USKLAĐENO|NESKLAD|DJELOMIČNO",
            "area_difference_m2": 0,
            "area_difference_percent": 0.0,
            "katastar_area_m2": 787,
            "zk_area_m2": 787,
            "parcel_discrepancies": [
              {
                "type": "AREA_MISMATCH|PARCEL_MISSING|LAND_USE_MISMATCH|PARCEL_NUMBER_MISMATCH",
                "zk_parcel_number": "1234/5",
                "katastar_parcel_number": "1234/5",
                "zk_area_m2": 500,
                "katastar_area_m2": 520,
                "zk_land_use": "oranica",
                "katastar_land_use": "oranica",
                "description": "Opis nesklada",
                "severity": "HIGH|MEDIUM|LOW"
              }
            ],
            "summary": "Opis usklađenosti ili nesklada...",
            "discrepancy_count": 0,
            "survey_recommended": false
          },
          "due_diligence_summary": "Profesionalni sažetak pravnog stanja...",
          "cited_norms": [
            {
              "abbreviation": "ZZK",
              "full_name": "Zakon o zemljišnim knjigama",
              "article": "čl. 98.",
              "paragraph": "",
              "point": "",
              "nn_reference": "NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17",
              "status": "VAŽEĆI",
              "formatted_citation": "čl. 98. Zakona o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17)"
            }
          ],
          "lawyer_comment": "Kratki sažetak za kompatibilnost...",
          "location": "Naselje, Općina/Grad",
          "address": "Ulica 5 ili prazan string",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

    public ZkParsingService(ChatClientProvider clients, PdfService pdfService, ILogger<ZkParsingService> logger)
    {
        _chatClient = clients.Fast;  // Fast model — ZK document extraction
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// Parse a ZK izvadak PDF into structured data.
    /// Returns complete ZkParsedResult with all 3 sheets + risk assessment.
    /// </summary>
    public async Task<(ZkParsedResult Parsed, string RawPdfText)> ParseZkIzvadakAsync(IFormFile file)
    {
        // Step 1: Extract text from PDF
        var pdfText = _pdfService.ExtractText(file);

        if (string.IsNullOrWhiteSpace(pdfText) || pdfText.StartsWith("[UPOZORENJE"))
        {
            _logger.LogWarning("ZK PDF extraction returned empty/warning text");
            throw new InvalidOperationException(
                "PDF ne sadrži čitljiv tekst. Moguće da je skeniran bez OCR sloja.");
        }

        _logger.LogInformation("ZK PDF extracted: {Chars} chars", pdfText.Length);

        // Step 2: Send to GPT for structured parsing
        var result = await ParseZkTextAsync(pdfText);

        return (result, pdfText);
    }

    /// <summary>
    /// Parse ZK izvadak from already-extracted text.
    /// Useful when text is already available.
    /// </summary>
    public async Task<ZkParsedResult> ParseZkTextAsync(string pdfText)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ZkParsingPrompt),
            new UserChatMessage($"Analiziraj ovaj zemljišnoknjižni izvadak:\n\n{pdfText}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("ZK parsing completed by GPT.");

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (result is null)
            throw new InvalidOperationException("Failed to deserialize ZK parsing response.");

        // Post-process: ensure property_id is set
        if (string.IsNullOrEmpty(result.PropertyId) && !string.IsNullOrEmpty(result.CadastralMunicipality))
        {
            result.PropertyId = $"k.o. {result.CadastralMunicipality}, k.č. {result.ParcelNumber}";
        }

        return result;
    }

    /// <summary>
    /// Convert ZkParsedResult into ExtractedPropertyInfo suitable for the valuation pipeline.
    /// Bridges ZK analysis → valuation engine.
    /// </summary>
    public static ExtractedPropertyInfo ToPropertyInfo(ZkParsedResult zk, string rawPdfText)
    {
        // Determine what size to use for valuation:
        // - If building area is known → use that (for kuća/stan)
        // - If only land area → use that (for zemljište)
        // - If property is kuća but only land area → set 0 and note in condition
        double sizeM2;
        string condition = zk.Condition;

        if (zk.PropertyType is "zemljište")
        {
            sizeM2 = zk.LandAreaM2;
        }
        else if (zk.BuildingAreaM2 > 0)
        {
            sizeM2 = zk.BuildingAreaM2;
        }
        else
        {
            // House/apartment but no building area found
            sizeM2 = 0;
            condition = $"Površina zgrade nije navedena u ZK izvadku, navedena je samo površina čestice: {zk.LandAreaM2} m²";
        }

        // Build description from ZK data
        var typeLabel = zk.PropertyType switch
        {
            "stan" => "Stan",
            "kuća" => "Kuća",
            "zemljište" => "Građevinsko zemljište",
            "poslovni_prostor" => "Poslovni prostor",
            _ => "Nekretnina"
        };

        var desc = $"{typeLabel}, {zk.PropertyDescription}";
        if (!string.IsNullOrEmpty(zk.ParcelNumber))
            desc += $", k.č. {zk.ParcelNumber}";
        if (!string.IsNullOrEmpty(zk.EtazniUdio))
            desc += $", {zk.EtazniUdio}";

        return new ExtractedPropertyInfo
        {
            Description = desc,
            SizeM2 = sizeM2,
            Location = zk.Location,
            Condition = condition,
            PropertyState = zk.PropertyState,
            YardAreaM2 = zk.YardAreaM2,
            RawText = rawPdfText.Length > 500 ? rawPdfText[..500] + "..." : rawPdfText
        };
    }
}
