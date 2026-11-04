using System.Text.Json.Serialization;

namespace Backend.Models;

// Ulazni oblik: sve sto klijent salje na /izracunaj i /spremi.

public sealed class TroskovnikRequest
{
    /// <summary>Vrijednost predmeta spora (EUR) – koristi se za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_spora")]
    public decimal VrijednostSpora { get; set; }

    /// <summary>
    /// Datum dospijeća potraživanja (Tbr. 52). Određuje koja je verzija Tarife i vrijednost
    /// boda mjerodavna — po dospijeću, ne po datumu radnje. Null → trenutno važeća Tarifa.
    /// </summary>
    [JsonPropertyName("datum_dospijeca")]
    public DateOnly? DatumDospijeca { get; set; }

    /// <summary>Odabrane stavke s količinama.</summary>
    [JsonPropertyName("stavke")]
    public List<TroskovnikItemRequest> Stavke { get; set; } = [];

    /// <summary>Podaci o klijentu (za ispis).</summary>
    [JsonPropertyName("klijent")]
    public TroskovnikParty? Klijent { get; set; }

    /// <summary>Nested odvjetnik object: { naziv, oib, adresa, iban }</summary>
    [JsonPropertyName("odvjetnik")]
    public OdvjetnikRequest? Odvjetnik { get; set; }

    /// <summary>Nested predmet object: { sud, poslovni_broj }</summary>
    [JsonPropertyName("predmet")]
    public PredmetRequest? Predmet { get; set; }

    /// <summary>Nested stranke object: { zastupana_stranka, protustranka }</summary>
    [JsonPropertyName("stranke")]
    public StrankeRequest? Stranke { get; set; }

    /// <summary>Backwards-compatible protustranka_polozaj (still accepted).</summary>
    [JsonPropertyName("protustranka_polozaj")]
    public string? ProtustrankaPolozaj { get; set; }

    /// <summary>Obračunaj PDV (true = obračunaj, false = bez PDV-a).</summary>
    /// <summary>
    /// Preferirani flag iz frontenda: kad je `isPdvUkljucen` poslan, koristi se on.
    /// Ostaje `HasVat` zbog kompatibilnosti s postojećim klijentima.
    /// </summary>
    [JsonPropertyName("has_vat")]
    public bool HasVat { get; set; } = true;

    /// <summary>
    /// Novi flag (preferred): `isPdvUkljucen` — default null kada nije poslan.
    /// Ako je true => obračunaj PDV; ako false => bez PDV.
    /// </summary>
    [JsonPropertyName("is_pdv_ukljucen")]
    public bool? IsPdvUkljucen { get; set; } = null;

    /// <summary>Zapriječena kazna – tier key (novcana, do_1, do_3, do_5, do_10, preko_10, dugotrajni).</summary>
    [JsonPropertyName("zaprijecena_kazna")]
    public string? ZaprijecenaKazna { get; set; }

    /// <summary>
    /// Obrana po službenoj dužnosti – 50% nagrade za pojedinu radnju prema Tarifi
    /// (Pravilnik NN 22/2022 br. 271, čl. 1). Isključuje IsZastupnikDjeteta.
    /// </summary>
    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    /// <summary>
    /// Obrana na teret proračunskih sredstava – 50% nagrade za pojedinu radnju prema
    /// Tarifi (isti Pravilnik, čl. 1). Isključuje IsZastupnikDjeteta.
    /// Vrijedi za POSTAVLJENOG branitelja. Za IZABRANOG branitelja čiji trošak padne
    /// na proračun zbog oslobađajuće presude vidi <see cref="IsIzabraniOslobodjajuca"/> —
    /// ta kombinacija ne umanjuje nagradu (puna Tarifa).
    /// </summary>
    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    /// <summary>
    /// Kvalifikator na <see cref="IsNaTeretProracuna"/>: branitelj je IZABRAN (ne
    /// postavljen po službenoj dužnosti), a trošak pada na proračunska sredstva zbog
    /// oslobađajuće presude (ili druge odluke bez osude). Pravilnik NN 22/2022 br. 271
    /// umanjuje nagradu na 50% samo za POSTAVLJENOG branitelja (čl. 1); izabranom
    /// branitelju u ovom slučaju pripada PUNA Tarifa, bez umanjenja.
    /// Bez učinka ako <see cref="IsNaTeretProracuna"/> nije postavljen. Zadano false —
    /// stari klijenti i pohranjeni zahtjevi bez ovog polja zadržavaju dosadašnje
    /// (postavljeni-branitelj) ponašanje.
    /// </summary>
    [JsonPropertyName("is_izabrani_oslobadjajuca")]
    public bool IsIzabraniOslobodjajuca { get; set; }

    /// <summary>
    /// Opunomoćenik djeteta žrtve kaznenog djela – 70% nagrade za pojedinu radnju prema
    /// Tarifi (isti Pravilnik, čl. 2). Isključuje obje zastavice iz čl. 1 — isti odvjetnik
    /// ne može u istom postupku braniti okrivljenika i zastupati dijete žrtvu.
    /// </summary>
    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    /// <summary>
    /// Opunomoćenik žrtve (bilo kojeg uzrasta) — NN 119/25 brisao je riječ „djeteta".
    /// Zamjenjuje <see cref="IsZastupnikDjeteta"/>, koji se zadržava dok klijenti ne prijeđu;
    /// vrijedi ako je postavljen bilo koji od njih (<see cref="JeOpunomocenikZrtve"/>).
    /// </summary>
    [JsonPropertyName("is_opunomocenik_zrtve")]
    public bool IsOpunomocenikZrtve { get; set; }

    [JsonIgnore]
    public bool JeOpunomocenikZrtve => IsOpunomocenikZrtve || IsZastupnikDjeteta;

    /// <summary>
    /// Koga odvjetnik zastupa (vidi <see cref="UlogaZastupanja"/>). Obavezno za kazneni
    /// i prekršajni troškovnik; kontroler odbija zahtjev bez nje. Servis bez nje računa
    /// kao BRANITELJ (stari pohranjeni zahtjevi).
    /// </summary>
    [JsonPropertyName("uloga_zastupanja")]
    public string? UlogaZastupanja { get; set; }

    /// <summary>Stjecaj / produljeno KD – korisnik bira tier prema najtežoj kazni.</summary>
    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    /// <summary>Prekršaj s oduzimanjem predmeta – nagrada po Tbr. 7.1 umjesto fiksnih 100.</summary>
    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    /// <summary>Vrijednost oduzetog predmeta / imovinske koristi (EUR) za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

    /// <summary>
    /// Prekršajni postupak (ne kazneni). Tbr. 4. toč. 1 al. 7: obrana u prekršajnom postupku
    /// je FIKSNIH 100 bodova i ne ovisi o propisanoj kazni, pa se zapriječena kazna ne traži.
    /// Sve stavke koje upućuju na Tbr. 4. toč. 1 (Tbr. 1 t. 1 i 2, Tbr. 5 t. 3...) tada
    /// polaze od 100 bodova. Iznimke: <see cref="IsOduzimanjePrekrsaj"/> (Tbr. 7 t. 1 prema
    /// vrijednosti) i <see cref="IsPrekrsajPravnaOsoba"/> (prema kazni za odgovornu osobu).
    /// </summary>
    [JsonPropertyName("is_prekrsajni_postupak")]
    public bool IsPrekrsajniPostupak { get; set; }

    /// <summary>
    /// Prekršajni postupak se vodi protiv pravne osobe — nagrada ide prema kazni za
    /// odgovornu osobu (Tbr. 4. toč. 1 al. 9), koja se tada šalje u <see cref="ZaprijecenaKazna"/>.
    /// </summary>
    [JsonPropertyName("is_prekrsaj_pravna_osoba")]
    public bool IsPrekrsajPravnaOsoba { get; set; }

    /// <summary>Opis predmeta / napomena.</summary>
    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    /// <summary>ID postojeće stranke (opcija — za povezivanje s klijentom).</summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>ID predmeta (opcija — za povezivanje s predmetom).</summary>
    [JsonPropertyName("case_id")]
    public string? CaseId { get; set; }

    /// <summary>If true and no ClientId, backend will auto-create a client from stranke data.</summary>
    [JsonPropertyName("auto_create_client")]
    public bool AutoCreateClient { get; set; }

    /// <summary>Grad for client auto-creation (not in nested stranke object).</summary>
    [JsonPropertyName("client_grad")]
    public string? ClientGrad { get; set; }

    /// <summary>
    /// Tip stranke ("fizicka" / "pravna") za automatsko otvaranje. Ako izostane,
    /// tip se pogađa iz naziva (v. ClientCaseService.NaslutiTip).
    /// </summary>
    [JsonPropertyName("client_tip")]
    public string? ClientTip { get; set; }

    /// <summary>Oslobođenje od plaćanja sudskih pristojbi (čl. 11. Zakona).</summary>
    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }

    // ── Prijelazna odredba, čl. 2 Uredbe NN 50/2026 ───────────────
    //
    // Dva datuma, a ne jedan zajednički: čl. 2 imenuje točno dvije situacije u kojima
    // se primjenjuje stara Uredba, pa jedan predmet može imati star odgovor na tužbu i
    // novu žalbu istodobno. Null znači „nije prijelazni slučaj" → nova Uredba.

    /// <summary>
    /// Datum podnošenja odgovora na tužbu. Ako je prije 1.6.2026., pristojba za odgovor
    /// na tužbu utvrđuje se po NN 37/2023.
    /// </summary>
    [JsonPropertyName("datum_podnosenja_odgovora_na_tuzbu")]
    public DateOnly? DatumPodnosenjaOdgovoraNaTuzbu { get; set; }

    /// <summary>
    /// Datum pokretanja upravnog spora. Ako je prije 1.6.2026., pristojbe u tom sporu
    /// utvrđuju se po NN 37/2023 — i tužba i odluka, ne samo tužba.
    /// </summary>
    [JsonPropertyName("datum_pokretanja_upravnog_spora")]
    public DateOnly? DatumPokretanjaUpravnogSpora { get; set; }

    /// <summary>
    /// Pristojbe koje korisnik dodaje sam, izvan onih izvedenih iz tarifnih stavaka.
    /// Velik dio Uredbe (ovjere, etažiranje, upis broda, polog, e-Notar) nema svoju
    /// odvjetničku tarifnu stavku iz koje bi se mogao izvesti.
    /// </summary>
    [JsonPropertyName("pristojbe_dodane")]
    public List<RucnaPristojbaRequest> PristojbeDodane { get; set; } = [];

    /// <summary>
    /// Ključevi odredbi koje se ne naplaćuju, iako bi ih odabrane stavke povukle.
    /// Postoji jer je nekoliko stavaka kataloga dvoznačno (dioba/ostavina,
    /// stečaj/predstečaj, revizija protiv presude ili rješenja), pa automatski izvedeni
    /// redak mora biti moguće maknuti.
    /// </summary>
    [JsonPropertyName("pristojbe_iskljucene")]
    public List<string> PristojbeIskljucene { get; set; } = [];

    // ── Čl. 7 Zakona o sudskim pristojbama — elektronički oblik ────
    //
    // Tri stavka, tri različita uvjeta, pa tri različita ulaza. Vidi PristojbeOpcije.

    /// <summary>
    /// Čl. 7 st. 1 — ključevi odredbi (podnesaka) predanih elektronički i za koje je
    /// pristojba plaćena u trenutku podnošenja. Po retku, jednako kao odluke: jedna
    /// žalba može biti predana elektronički, a tužba u istom predmetu na papiru.
    /// </summary>
    [JsonPropertyName("podnesci_elektronicki")]
    public List<string> PodnesciElektronicki { get; set; } = [];

    /// <summary>
    /// Čl. 7 st. 3 — zemljišnoknjižni upis predložen elektronički i plaćeno pri
    /// podnošenju. Zaseban prekidač jer uvjet nije oblik pojedinog podneska nego oblik
    /// prijedloga za upis.
    /// </summary>
    [JsonPropertyName("zk_upis_elektronicki")]
    public bool ZkUpisElektronicki { get; set; }

    /// <summary>
    /// Čl. 7 st. 2 — ključevi odredbi (odluka) koje je sud dostavio elektronički i za
    /// koje je pristojba uplaćena u roku od tri dana. Po retku, jer se rok može ispuniti
    /// za jednu odluku, a za drugu ne.
    /// </summary>
    [JsonPropertyName("odluke_elektronicki")]
    public List<string> OdlukeElektronicki { get; set; } = [];

    /// <summary>
    /// Po kojoj je točki čl. 11 stranka oslobođena. Vidi <see cref="Models.OslobodjenjeKategorija"/>.
    /// Null znači „oslobođena, bez navedene osnove" — staro ponašanje golog prekidača.
    /// </summary>
    [JsonPropertyName("oslobodjenje_kategorija")]
    public string? OslobodjenjeKategorija { get; set; }

    /// <summary>
    /// Čl. 12 — koliko je od <see cref="BrojZastupanihOsoba"/> oslobođeno. Zadano 0.
    /// Kad ih je oslobođeno koliko i ukupno, pristojba je 0 kao i kod golog prekidača.
    /// </summary>
    [JsonPropertyName("broj_oslobodjenih_osoba")]
    public int BrojOslobodjenihOsoba { get; set; }

    /// <summary>
    /// Čl. 12 — dio vrijednosti predmeta spora koji otpada na neoslobođene osobe.
    /// Null znači da se dio ne može utvrditi, pa se VPS dijeli brojem osoba (st. 2).
    /// </summary>
    [JsonPropertyName("udio_u_vps")]
    public decimal? UdioUVps { get; set; }

    /// <summary>
    /// Čl. 13 — omjer u kojem je oslobođena stranka uspjela u postupku, 0–100.
    /// Null kad se ne obračunava.
    /// </summary>
    [JsonPropertyName("postotak_uspjeha")]
    public decimal? PostotakUspjeha { get; set; }

    // ── Vrijednost predmeta spora po Zakonu o sudskim pristojbama ──

    /// <summary>
    /// Vrsta postupka po čl. 15–25. Vidi <see cref="Models.VrstaPostupka"/>.
    /// Zadano opće pravilo iz čl. 14 — vrijedi uneseni iznos.
    /// </summary>
    [JsonPropertyName("vrsta_postupka")]
    public string VrstaPostupka { get; set; } = Models.VrstaPostupka.Opci;

    /// <summary>Čl. 15 — mjesečno davanje za uzdržavanje; vrijednost je zbroj za tri mjeseca.</summary>
    [JsonPropertyName("uzdrzavanje_mjesecno")]
    public decimal UzdrzavanjeMjesecno { get; set; }

    /// <summary>Čl. 16 — pojedinačni zahtjevi iz tužbe. Prazno znači „vrijedi vrijednost_spora".</summary>
    [JsonPropertyName("zahtjevi")]
    public List<ZahtjevStavka> Zahtjevi { get; set; } = [];

    /// <summary>Čl. 16 — kako se zahtjevi sastavljaju. Vidi <see cref="Models.ZahtjeviNacin"/>.</summary>
    [JsonPropertyName("zahtjevi_nacin")]
    public string? ZahtjeviNacin { get; set; }

    /// <summary>Čl. 24 — po čemu se plaća pristojba na pravni lijek. Vidi <see cref="Models.PravniLijek"/>.</summary>
    [JsonPropertyName("pravni_lijek")]
    public string PravniLijek { get; set; } = Models.PravniLijek.Nema;

    /// <summary>
    /// Čl. 24 — vrijednost pobijanog dijela odnosno iznos troškova i sporednih
    /// potraživanja. Obavezan uz `pobijani_dio` i `samo_troskovi`.
    /// </summary>
    [JsonPropertyName("pravni_lijek_iznos")]
    public decimal PravniLijekIznos { get; set; }

    /// <summary>
    /// Prenosi zakonski VPS i na odvjetničku nagradu po Tbr. 7. toč. 1.
    ///
    /// Zadano false: čl. 14–26 Zakona pisani su za sudske pristojbe, ne za Tarifu, pa
    /// se zakonski iznos na nagradu prenosi samo kad odvjetnik to izričito odabere.
    /// </summary>
    [JsonPropertyName("vps_primijeni_na_nagradu")]
    public bool VpsPrimijeniNaNagradu { get; set; }

    /// <summary>
    /// Broj osoba koje odvjetnik brani ili zastupa u istom postupku (Tbr. 40. toč. 1).
    /// Vrijednosti manje od 2 znače da nema povišenja.
    /// </summary>
    [JsonPropertyName("broj_zastupanih_osoba")]
    public int BrojZastupanihOsoba { get; set; } = 1;

    // ── Osnovica po Tbr. 7. toč. 1., 4., 7. i 8. ──────────────────

    /// <summary>
    /// Koja se varijanta osnovice primjenjuje. Vidi <see cref="Models.OsnovicaTbr7"/>.
    /// Zadano toč. 1 — isto ponašanje kao prije uvođenja ovog polja.
    /// </summary>
    [JsonPropertyName("osnovica_tbr7")]
    public string OsnovicaTbr7 { get; set; } = Models.OsnovicaTbr7.Toc1;

    /// <summary>Tbr. 45 — razina suda. Obavezno kad je osnovica `tbr45_neprocjenjiv`.</summary>
    [JsonPropertyName("razina_suda")]
    public string? RazinaSuda { get; set; }

    /// <summary>Tbr. 45 — koje se čitanje primjenjuje. Obavezno kad je osnovica `tbr45_neprocjenjiv`.</summary>
    [JsonPropertyName("tbr45_nacin")]
    public string? Tbr45Nacin { get; set; }

    /// <summary>
    /// Tbr. 7. toč. 4 al. 2 — periodični iznos rente / zakupa / najma.
    /// Uz odabrano razdoblje daje petogodišnji iznos kao vrijednost predmeta spora.
    /// </summary>
    [JsonPropertyName("renta_iznos")]
    public decimal RentaIznos { get; set; }

    /// <summary>Razdoblje uz <see cref="RentaIznos"/> — `mjesecno` (× 60) ili `godisnje` (× 5).</summary>
    [JsonPropertyName("renta_razdoblje")]
    public string? RentaRazdoblje { get; set; }

    // ── Tbr. 41 — povisivanje i snižavanje tarifnih stavaka ───────

    /// <summary>
    /// Tbr. 41. toč. 1 — povišenje za posebno stručno i specijalističko znanje, 0–100 %.
    /// Zadano 0: Tarifa kaže „mogu se povisiti", dakle nikad automatski.
    /// </summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>
    /// Tbr. 41. toč. 2 — sniženje pojedinih tarifnih stavaka, 0–50 %. Zadano 0.
    /// </summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    /// <summary>Ručno dodane sudske pristojbe — ispod automatski izračunate, bez PDV-a.</summary>
    [JsonPropertyName("dodatne_pristojbe")]
    public List<TroskovnikTrosakRequest> DodatnePristojbe { get; set; } = [];

    /// <summary>Predujmovi (vještačenje i sl.) — vlastiti odjeljak, bez PDV-a.</summary>
    [JsonPropertyName("predujmovi")]
    public List<TroskovnikTrosakRequest> Predujmovi { get; set; } = [];

    /// <summary>Slobodni tekst napomene na kraju troškovnika. Prazan/whitespace izostavlja odjeljak.</summary>
    [JsonPropertyName("napomena")]
    public string? Napomena { get; set; }

    /// <summary>
    /// Naknada troškova (Tbr. 48.-51.): prijevoz, dnevnica, hotel, ostali izdaci.
    /// Vlastiti trošak odvjetnika, pa po čl. 33. Zakona o PDV-u ULAZI u poreznu
    /// osnovicu — za razliku od pristojbi i predujmova, koji su prolazne stavke.
    /// Vidi <see cref="Models.VrstaTroska"/>.
    /// </summary>
    [JsonPropertyName("naknada_troskova_stavke")]
    public List<NaknadaTroskovaStavkaRequest> NaknadaTroskovaStavke { get; set; } = [];
}

/// <summary>
/// Jedan ručno unesen trošak (dodatna pristojba ili predujam) — oba popisa dijele isti oblik.
/// </summary>
public sealed class TroskovnikTrosakRequest
{
    /// <summary>Datum troška. Opcionalan, kao i kod radnje.</summary>
    [JsonPropertyName("datum")]
    public DateOnly? Datum { get; set; }

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("iznos")]
    public decimal Iznos { get; set; }
}

// New request DTOs for nested schema
public sealed class OdvjetnikRequest
{
    [JsonPropertyName("naziv")]
    public string? Naziv { get; set; }

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }
}

public sealed class PredmetRequest
{
    [JsonPropertyName("sud")]
    public string? Sud { get; set; }

    [JsonPropertyName("poslovni_broj")]
    public string? PoslovniBroj { get; set; }
}

public sealed class StrankaRequest
{
    [JsonPropertyName("polozaj")]
    public string? Polozaj { get; set; }

    [JsonPropertyName("naziv")]
    public string? Naziv { get; set; }

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }
}

public sealed class StrankeRequest
{
    [JsonPropertyName("zastupana_stranka")]
    public StrankaRequest? ZastupanaStranka { get; set; }

    [JsonPropertyName("protustranka")]
    public StrankaRequest? Protustranka { get; set; }
}

public sealed class TroskovnikItemRequest
{
    /// <summary>Identifikator stavke iz kataloga (npr. "tbr7_tuzba").</summary>
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    /// <summary>Količina (broj radnji, sati, itd.).</summary>
    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; } = 1;

    /// <summary>
    /// Datum kad je radnja poduzeta. Opcionalan — prazan se ne ispisuje i ne blokira
    /// izračun. <c>DateOnly?</c> namjerno umjesto <c>string</c>: neispravan datum tada
    /// pada s greškom umjesto da završi kao smeće u dokumentu.
    /// </summary>
    [JsonPropertyName("datum")]
    public DateOnly? Datum { get; set; }

    /// <summary>
    /// Tbr. 41. toč. 1 — povišenje ove stavke, 0–100 %. Null znači „koristi postotak
    /// s razine zahtjeva" (prečac „primijeni na sve stavke").
    /// </summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal? Tbr41PovisenjePostotak { get; set; }

    /// <summary>
    /// Tbr. 41. toč. 2 — sniženje ove stavke, 0–50 %. Riječ „pojedine" u toč. 2 znači
    /// da se sniženje odnosi na pojedinačne stavke, ne na ukupan zbroj.
    /// </summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal? Tbr41SnizenjePostotak { get; set; }
}

/// <summary>Ručno dodana sudska pristojba — vidi <see cref="RucnaPristojba"/>.</summary>
public sealed class RucnaPristojbaRequest
{
    /// <summary>Ključ odredbe iz Uredbe, npr. „tbr11" ili „tbr15_etaziranje".</summary>
    [JsonPropertyName("odredba_id")]
    public string OdredbaId { get; set; } = "";

    /// <summary>
    /// Vrijednost na koju se odredba primjenjuje (čista ostavina, diobna masa, broj
    /// etažnih dijelova…). Kad je null, uzima se vrijednost predmeta spora.
    /// </summary>
    [JsonPropertyName("osnovica")]
    public decimal? Osnovica { get; set; }

    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; } = 1;
}

/// <summary>Čl. 16 — jedan od više zahtjeva iz iste tužbe.</summary>
public sealed class ZahtjevStavka
{
    /// <summary>Vrijednost ovog zahtjeva u EUR.</summary>
    [JsonPropertyName("iznos")]
    public decimal Iznos { get; set; }

    /// <summary>Opis zahtjeva, samo za ispis.</summary>
    [JsonPropertyName("opis")]
    public string? Opis { get; set; }

    /// <summary>
    /// Uz način „zasebno" (suparničarstvo) — zahtjev koji ovaj troškovnik pokriva.
    /// Točno jedan zahtjev mora ga nositi; za ostale ide posebna pristojba.
    /// </summary>
    [JsonPropertyName("primarni")]
    public bool Primarni { get; set; }
}

/// <summary>Jedan stvarni izdatak iz naknade troškova (Tbr. 48.-51.).</summary>
public sealed class NaknadaTroskovaStavkaRequest
{
    /// <summary>Jedna od vrijednosti u <see cref="VrstaTroska.Sve"/>.</summary>
    [JsonPropertyName("vrsta")]
    public string Vrsta { get; set; } = "";

    /// <summary>Slobodan opis (npr. "Put Zagreb-Split, 340 km"). Neobavezno.</summary>
    [JsonPropertyName("opis")]
    public string? Opis { get; set; }

    /// <summary>
    /// Stvarni iznos u EUR — Tarifa ne propisuje bodove za ove izdatke. Kad je
    /// <see cref="Nacin"/> = "izracun" ovo polje se IGNORIRA — konačni iznos se računa
    /// iz <see cref="Kolicina"/> × <see cref="JedinicniIznos"/> (+ <see cref="Cestarina"/>
    /// za prijevoz) i vraća se u odgovoru.
    /// </summary>
    [JsonPropertyName("iznos")]
    public decimal Iznos { get; set; }

    /// <summary>
    /// "rucno" (zadano, <see cref="Iznos"/> vrijedi kao upisan) ili "izracun" (iznos se
    /// izvodi iz količine × jediničnog iznosa). Samo za Prijevoz (Tbr. 50 — km) i
    /// Dnevnicu (Tbr. 51); za Hotelski smještaj i Ostale izdatke izračun se odbija —
    /// ondje nema propisane jedinične cijene.
    /// </summary>
    [JsonPropertyName("nacin")]
    public string? Nacin { get; set; }

    /// <summary>Količina za način "izracun" — kilometri (Prijevoz) ili dani (Dnevnica).</summary>
    [JsonPropertyName("kolicina")]
    public decimal? Kolicina { get; set; }

    /// <summary>
    /// Jedinični iznos za način "izracun" — €/km (Prijevoz, = cijena goriva × 0,30) ili
    /// €/dan (Dnevnica). Snapshot u trenutku izračuna: spremljeni troškovnik zadržava
    /// povijesnu vrijednost i kad ured kasnije promijeni postavku, isto kao naziv/verzija
    /// Tarife i vrijednost boda.
    /// </summary>
    [JsonPropertyName("jedinicni_iznos")]
    public decimal? JedinicniIznos { get; set; }

    /// <summary>Tbr. 50 — cestarina, dodaje se na izračunatu naknadu za Prijevoz.</summary>
    [JsonPropertyName("cestarina")]
    public decimal? Cestarina { get; set; }
}

public sealed class TroskovnikParty
{
    [JsonPropertyName("ime")]
    public string Ime { get; set; } = "";

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }

    [JsonPropertyName("grad")]
    public string? Grad { get; set; }
}
