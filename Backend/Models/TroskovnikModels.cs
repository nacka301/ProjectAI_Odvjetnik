using System.Text.Json.Serialization;

namespace Backend.Models;

/// <summary>
/// Tarifa o nagradama i naknadi troškova za rad odvjetnika (NN 2023)
/// 1 bod = 2.00 EUR (Tbr. 54)
/// </summary>
public static class TarifaConstants
{
    /// <summary>Puni naziv propisa, za navođenje uz svaku stavku troškovnika.</summary>
    public const string NazivTarife =
        "Tarifa o nagradama i naknadi troškova za rad odvjetnika (Hrvatska odvjetnička komora, 14. listopada 2023.)";

    /// <summary>Uredba koja propisuje sudske pristojbe.</summary>
    public const string NazivUredbeOPristojbama =
        "Uredba o tarifi sudskih pristojbi (NN 37/2023)";

    /// <summary>Tbr. 54 — vrijednost boda.</summary>
    public const decimal BodValue = 2.00m;

    /// <summary>Zakon o PDV-u — opća stopa.</summary>
    public const decimal PdvRate = 0.25m;

    /// <summary>
    /// Branitelj po službenoj dužnosti ili na teret proračunskih sredstava — 50% tarife.
    ///
    /// Tbr. 4. toč. 7 sam ne određuje iznos nego upućuje dalje: "Nagrada za rad odvjetnika
    /// koji je određen za branitelja po službenoj dužnosti, sukladno odredbama Zakona o
    /// odvjetništvu, određuje se Pravilnikom ministarstva nadležnog za poslove pravosuđa."
    ///
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 1.
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    /// </summary>
    public const decimal KazneniReductionRate = 0.50m;

    /// <summary>
    /// Tbr. 4. toč. 8: "Odvjetniku koji zastupa oštećenika u postupku koji se vodi pred
    /// policijom, državnim odvjetništvom (...) te na raspravi pred sudom, pripada 50%
    /// nagrade iz ovog tarifnog broja."
    /// </summary>
    public const decimal Tbr4t8Rate = 0.50m;

    /// <summary>
    /// Opunomoćenik djeteta žrtve kaznenog djela — 70% od nagrade iz Tbr. 4. toč. 8.
    ///
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 2.
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    ///
    /// Budući da je Tbr. 4. toč. 8 već 50% nagrade iz Tbr. 4, efektivna stopa je
    /// 0,50 × 0,70 = 35% pune tarife — ne 70%. Vidi TarifaUmanjenjaTests.
    /// </summary>
    public const decimal ZastupnikDjetetaRate = 0.70m;
}

/// <summary>Zapriječena kazna → bodovi (Tbr. 4)</summary>
/// <summary>
/// Tbr. 4. toč. 1 — bodovi po zapriječenoj kazni.
/// Novčana kazna ili kazna do 3 god. zatvora = 100,
/// do 5 = 200, do 10 = 300, preko 10 = 400, dugotrajni = 500.
/// </summary>
public static class ZaprijecenaKaznaTiers
{
    /// <summary>Ključ tiera za kaznu dugotrajnog zatvora (Tbr. 4. toč. 1 al. 5).</summary>
    public const string DugotrajniZatvor = "dugotrajni";

    public static readonly Dictionary<string, int> Bodovi = new()
    {
        ["novcana_do3"] = 100,   // novčana kazna ili kazna do 3 godine zatvora
        ["do_5"]        = 200,   // do 5 godina zatvora
        ["do_10"]       = 300,   // do 10 godina zatvora
        ["preko_10"]    = 400,   // preko 10 godina zatvora
        ["dugotrajni"]  = 500    // dugotrajni zatvor
    };
}

/// <summary>
/// Osnova za obračun sudske pristojbe prema Uredbi o tarifi sudskih pristojbi
/// (NN 37/2023). Prilog, POGLAVLJE I. nosi naslov "PARNIČNI I OVRŠNI POSTUPAK" —
/// izvan tog poglavlja Tar. br. 1 se ne primjenjuje.
/// </summary>
public enum PristojbaOsnova
{
    /// <summary>Tar. br. 1 st. 1 — puni iznos.</summary>
    Puna = 0,

    /// <summary>Tar. br. 1 st. 2 — polovica iznosa iz st. 1 (prijedlog za ovrhu, žalba, revizija...).</summary>
    Polovica = 1,

    /// <summary>Uredba ne propisuje pristojbu za ovu vrstu postupka (npr. kazneni).</summary>
    Nema = 2
}

// ── Request / Response ────────────────────────────────────────

public sealed class TroskovnikRequest
{
    /// <summary>Vrijednost predmeta spora (EUR) – koristi se za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_spora")]
    public decimal VrijednostSpora { get; set; }

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

    /// <summary>Obrana po službenoj dužnosti – 50% umanjenje.</summary>
    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    /// <summary>Obrana na teret proračuna – 50% umanjenje.</summary>
    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    /// <summary>Zastupnik djeteta – 70% od Tbr. 4.</summary>
    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    /// <summary>Stjecaj / produljeno KD – korisnik bira tier prema najtežoj kazni.</summary>
    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    /// <summary>Prekršaj s oduzimanjem predmeta – nagrada po Tbr. 7.1 umjesto fiksnih 100.</summary>
    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    /// <summary>Vrijednost oduzetog predmeta / imovinske koristi (EUR) za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

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

    /// <summary>Oslobođenje od plaćanja sudskih pristojbi (čl. 11. Zakona).</summary>
    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }

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
}

/// <summary>
/// Osnovica po Tbr. 7. Tarifa na devet mjesta upućuje na „Tbr. 7. toč. 1., 4., 7. i 8."
/// (Tbr. 8. toč. 1, 3, 4; Tbr. 9. toč. 1, 2, 3, 5, 6; Tbr. 10.). To NISU četiri različita
/// iznosa nego četiri varijante ISTE osnovice — koja se primjenjuje ovisi o vrsti postupka,
/// a ne o pojedinoj stavci.
/// </summary>
public static class OsnovicaTbr7
{
    /// <summary>Tbr. 7. toč. 1 — tablica vrijednost spora → bodovi. Zadano.</summary>
    public const string Toc1 = "toc1";

    /// <summary>Tbr. 7. toč. 4 al. 1 — autorsko / industrijsko vlasništvo: po toč. 1, ali ne manje od 100 bodova.</summary>
    public const string Toc4Autorsko = "toc4_autorsko";

    /// <summary>Tbr. 7. toč. 4 al. 2 — renta / zakup / najam: kao VPS uzima se petogodišnji iznos.</summary>
    public const string Toc4Renta = "toc4_renta";

    /// <summary>Tbr. 7. toč. 7 st. 1 — domaća arbitraža: po toč. 1.</summary>
    public const string Toc7DomacaArbitraza = "toc7_domaca_arbitraza";

    /// <summary>Tbr. 7. toč. 7 st. 2 — međunarodna arbitraža: po toč. 1 s povišenjem od 100 %.</summary>
    public const string Toc7MedunarodnaArbitraza = "toc7_medunarodna_arbitraza";

    /// <summary>Tbr. 7. toč. 8 — mirno rješavanje sporova: po toč. 1.</summary>
    public const string Toc8Mirenje = "toc8_mirenje";

    /// <summary>Tbr. 45 — neprocjenjivi predmeti: 100 / 200 / 500 bodova prema razini suda.</summary>
    public const string Tbr45Neprocjenjiv = "tbr45_neprocjenjiv";

    public static readonly string[] Sve =
    [
        Toc1, Toc4Autorsko, Toc4Renta, Toc7DomacaArbitraza,
        Toc7MedunarodnaArbitraza, Toc8Mirenje, Tbr45Neprocjenjiv
    ];
}

/// <summary>
/// Tbr. 45 — razina suda određuje bodove u neprocjenjivim predmetima.
/// Namjerno BEZ zadane vrijednosti: razlika između razina je do peterostruka,
/// pa se ne smije pretpostaviti.
/// </summary>
public static class RazinaSuda
{
    /// <summary>Tbr. 45. toč. 1 — općinski sud i tijela iste razine.</summary>
    public const string Opcinski = "opcinski";

    /// <summary>Tbr. 45. toč. 2 — županijski i trgovački sudovi te tijela iste razine.</summary>
    public const string ZupanijskiTrgovacki = "zupanijski_trgovacki";

    /// <summary>Tbr. 45. toč. 3 — Vrhovni sud Republike Hrvatske i tijela iste razine.</summary>
    public const string Vrhovni = "vrhovni";

    /// <summary>Bodovi po razini suda — doslovno iz Tbr. 45. toč. 1–3.</summary>
    public static readonly Dictionary<string, int> Bodovi = new()
    {
        [Opcinski]            = 100,
        [ZupanijskiTrgovacki] = 200,
        [Vrhovni]             = 500
    };
}

/// <summary>
/// NEJASNO U TARIFI — Tbr. 45 daje 100/200/500 bodova „za svaku poduzetu radnju", ali ne
/// kaže primjenjuju li se na taj iznos postoci iz Tbr. 8 (50 % / 25 %) i Tbr. 9. Oba su
/// čitanja obranjiva, pa Tarifa ne daje odgovor — korisnik bira, sustav ne izmišlja.
/// </summary>
public static class Tbr45Nacin
{
    /// <summary>100/200/500 ulazi kao osnovica; postoci iz Tbr. 8./9. se primjenjuju na nju.</summary>
    public const string KaoOsnovica = "kao_osnovica";

    /// <summary>Svaka radnja nosi puni iznos; postoci i stropovi iz Tbr. 8./9. se ne primjenjuju.</summary>
    public const string FiksnoPoRadnji = "fiksno_po_radnji";

    public static readonly string[] Sve = [KaoOsnovica, FiksnoPoRadnji];
}

/// <summary>Razdoblje za preračun petogodišnjeg iznosa iz Tbr. 7. toč. 4 al. 2.</summary>
public static class RentaRazdoblje
{
    public const string Mjesecno = "mjesecno";  // × 60
    public const string Godisnje = "godisnje";  // × 5
}

/// <summary>Rezultat razrješavanja osnovice po Tbr. 7. / Tbr. 45.</summary>
/// <param name="Bodovi">Broj bodova osnovice.</param>
/// <param name="PravniIzvor">Odredba iz koje osnovica proizlazi.</param>
/// <param name="FiksnoPoRadnji">
/// True samo kod Tbr. 45 uz način „fiksno_po_radnji": svaka radnja dobiva puni iznos,
/// bez postotaka i stropova iz Tbr. 8./9.
/// </param>
public readonly record struct OsnovicaRezultat(decimal Bodovi, string PravniIzvor, bool FiksnoPoRadnji);

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

public sealed class TroskovnikResponse
{
    [JsonPropertyName("stavke")]
    public List<TroskovnikLineItem> Stavke { get; set; } = [];

    /// <summary>Ukupno bodova, uključujući povišenje po Tbr. 40.</summary>
    [JsonPropertyName("ukupno_bodova")]
    public decimal UkupnoBodova { get; set; }

    /// <summary>Broj zastupanih osoba iz zahtjeva (Tbr. 40. toč. 1).</summary>
    [JsonPropertyName("broj_zastupanih_osoba")]
    public int BrojZastupanihOsoba { get; set; } = 1;

    /// <summary>
    /// Postotak povišenja po Tbr. 40. toč. 1: 10% za drugu i svaku sljedeću osobu,
    /// najviše 50%. Prva osoba ne ulazi u postotak.
    /// </summary>
    [JsonPropertyName("povisenje_vise_osoba_postotak")]
    public decimal PovisenjeViseOsobaPostotak { get; set; }

    /// <summary>Bodovi koje donosi povišenje po Tbr. 40 — prikazuju se kao zaseban redak.</summary>
    [JsonPropertyName("povisenje_vise_osoba_bodovi")]
    public decimal PovisenjeViseOsobaBodovi { get; set; }

    /// <summary>
    /// Tbr. 40. toč. 1, drugi odlomak: "Iznos povišene nagrade dijeli se na jednake
    /// dijelove prema broju branjenika, odnosno zastupanih osoba."
    ///
    /// Dioba ne mijenja ukupnu nagradu — pokazuje koliko od nje otpada na svaku osobu.
    /// </summary>
    [JsonPropertyName("bodovi_po_osobi")]
    public decimal BodoviPoOsobi { get; set; }

    /// <summary>
    /// Nagrada po zastupanoj osobi u EUR. Dijeli se nagrada, ne sveukupni trošak —
    /// PDV i sudska pristojba nisu odvjetnička nagrada.
    /// </summary>
    [JsonPropertyName("nagrada_po_osobi")]
    public decimal NagradaPoOsobi { get; set; }

    // ── Osnovica po Tbr. 7. / Tbr. 45 ─────────────────────────────

    /// <summary>Primijenjena varijanta osnovice.</summary>
    [JsonPropertyName("osnovica_tbr7")]
    public string OsnovicaTbr7 { get; set; } = Models.OsnovicaTbr7.Toc1;

    /// <summary>Bodovi osnovice na koju su primijenjeni postoci iz Tbr. 8. i Tbr. 9.</summary>
    [JsonPropertyName("osnovica_bodovi")]
    public decimal OsnovicaBodovi { get; set; }

    /// <summary>Odredba iz koje osnovica proizlazi (npr. „Tbr. 7. toč. 7 st. 2").</summary>
    [JsonPropertyName("osnovica_pravni_izvor")]
    public string OsnovicaPravniIzvor { get; set; } = "";

    [JsonPropertyName("razina_suda")]
    public string? RazinaSuda { get; set; }

    [JsonPropertyName("tbr45_nacin")]
    public string? Tbr45Nacin { get; set; }

    // ── Tbr. 41 ───────────────────────────────────────────────────

    /// <summary>Tbr. 41. toč. 1 — primijenjeno povišenje u postotku (0–100).</summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>Tbr. 41. toč. 2 — primijenjeno sniženje u postotku (0–50).</summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    /// <summary>Bodovi koje donosi Tbr. 41 (pozitivni za povišenje, negativni za sniženje).</summary>
    [JsonPropertyName("tbr41_bodovi")]
    public decimal Tbr41Bodovi { get; set; }

    /// <summary>
    /// Vrijednost predmeta spora korištena u izračunu. U kaznenom postupku uvijek 0 —
    /// VPS je institut parničnog postupka i tamo nema što tražiti (vidi <see cref="IsSamoKazneni"/>).
    /// </summary>
    [JsonPropertyName("vrijednost_spora")]
    public decimal VrijednostSpora { get; set; }

    /// <summary>
    /// True kad su odabrane isključivo stavke iz kaznenog/prekršajnog postupka.
    /// Tada se ne koristi VPS niti se obračunava sudska pristojba.
    /// </summary>
    [JsonPropertyName("is_samo_kazneni")]
    public bool IsSamoKazneni { get; set; }

    [JsonPropertyName("ukupno_eur_bez_pdv")]
    public decimal UkupnoEurBezPdv { get; set; }

    [JsonPropertyName("pdv")]
    public decimal Pdv { get; set; }

    [JsonPropertyName("ukupno_eur_s_pdv")]
    public decimal UkupnoEurSPdv { get; set; }

    [JsonPropertyName("vrijednost_boda")]
    public decimal VrijednostBoda { get; set; } = TarifaConstants.BodValue;

    [JsonPropertyName("klijent")]
    public TroskovnikParty? Klijent { get; set; }

    [JsonPropertyName("odvjetnik")]
    public TroskovnikParty? Odvjetnik { get; set; }

    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    [JsonPropertyName("datum_izrade")]
    public DateTime DatumIzrade { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("protustranka")]
    public TroskovnikParty? Protustranka { get; set; }

    [JsonPropertyName("protustranka_polozaj")]
    public string? ProtustrankaPolozaj { get; set; }

    [JsonPropertyName("is_pdv_ukljucen")]
    public bool? IsPdvUkljucen { get; set; }

    [JsonPropertyName("has_vat")]
    public bool HasVat { get; set; }

    [JsonPropertyName("sud")]
    public string? Sud { get; set; }

    [JsonPropertyName("broj_predmeta")]
    public string? BrojPredmeta { get; set; }

    // Nested objects (new schema) — carried alongside legacy flat fields for gradual migration
    [JsonPropertyName("odvjetnik_info")]
    public OdvjetnikRequest? OdvjetnikInfo { get; set; }

    [JsonPropertyName("predmet_info")]
    public PredmetRequest? PredmetInfo { get; set; }

    [JsonPropertyName("stranke_info")]
    public StrankeRequest? StrankeInfo { get; set; }

    // Kazneni echo fields
    [JsonPropertyName("zaprijecena_kazna")]
    public string? ZaprijecenaKazna { get; set; }

    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    /// <summary>
    /// Obrazloženje uz odabrani razred kad je riječ o stjecaju ili produljenom
    /// kaznenom djelu (Tbr. 4. toč. 1 al. 6). Null kad stjecaja nema.
    /// </summary>
    [JsonPropertyName("napomena_stjecaj")]
    public string? NapomenaStjecaj { get; set; }

    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

    // Sudske pristojbe
    [JsonPropertyName("sudska_pristojba")]
    public decimal SudskaPristojba { get; set; }

    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }

    /// <summary>
    /// Po kojoj se osnovi obračunava pristojba. Razlikuje "Uredba ovdje ne propisuje
    /// pristojbu" (<see cref="Backend.Models.PristojbaOsnova.Nema"/>) od oslobođenja
    /// stranke — oboje daju 0,00 €, ali iz posve različitih razloga.
    /// </summary>
    [JsonPropertyName("pristojba_osnova")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PristojbaOsnova PristojbaOsnova { get; set; }

    /// <summary>Grand total = odvjetnički trošak (s PDV) + sudska pristojba.</summary>
    [JsonPropertyName("sveukupni_trosak")]
    public decimal SveukupniTrosak { get; set; }
}

public sealed class TroskovnikLineItem
{
    [JsonPropertyName("rbr")]
    public int Rbr { get; set; }

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    /// <summary>Propis i tarifni broj iz kojeg ovaj redak proizlazi — vidi <see cref="TarifnaStavka.PravniIzvor"/>.</summary>
    [JsonPropertyName("pravni_izvor")]
    public string PravniIzvor { get; set; } = "";

    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; }

    /// <summary>Bodovi po jedinici. Decimalno — Tarifa ne propisuje zaokruživanje bodova.</summary>
    [JsonPropertyName("bodova_po_jedinici")]
    public decimal BodovaPoJedinici { get; set; }

    [JsonPropertyName("ukupno_bodova")]
    public decimal UkupnoBodova { get; set; }

    [JsonPropertyName("iznos_eur")]
    public decimal IznosEur { get; set; }

    /// <summary>
    /// True kad je nagrada jednokratna (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1), pa
    /// <see cref="UkupnoBodova"/> NIJE <see cref="BodovaPoJedinici"/> × <see cref="Kolicina"/>.
    /// Sučelje o tome mora obavijestiti korisnika.
    /// </summary>
    [JsonPropertyName("is_jednokratna")]
    public bool IsJednokratna { get; set; }

    // ── Tbr. 41 po pojedinoj stavci ───────────────────────────────

    /// <summary>Tbr. 41. toč. 1 — primijenjeno povišenje ove stavke u postotku (0–100).</summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>Tbr. 41. toč. 2 — primijenjeno sniženje ove stavke u postotku (0–50).</summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    /// <summary>
    /// Bodovi koje Tbr. 41 dodaje ili oduzima ovoj stavci — pozitivno za povišenje,
    /// negativno za sniženje. Zbrajaju se u <see cref="TroskovnikResponse.Tbr41Bodovi"/>.
    /// </summary>
    [JsonPropertyName("tbr41_bodovi")]
    public decimal Tbr41Bodovi { get; set; }

    /// <summary>
    /// Citat odredbe uz prilagođenu stavku — prazan kad Tbr. 41 nije primijenjen.
    /// Vidi se na ispisu, da se svaki odmak od tarifne stavke može provjeriti.
    /// </summary>
    [JsonPropertyName("tbr41_pravni_izvor")]
    public string Tbr41PravniIzvor { get; set; } = "";
}

// ── Catalog item definition ──────────────────────────────────

public sealed class TarifnaStavka
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kategorija")]
    public string Kategorija { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    /// <summary>Fiksni bodovi (null = koristi Tbr71 lookup).</summary>
    [JsonPropertyName("fiksni_body")]
    public int? FiksniBody { get; set; }

    /// <summary>Postotak od Tbr. 7.1 (1.0 = 100%, 0.5 = 50%, itd.).</summary>
    [JsonPropertyName("tbr71_postotak")]
    public decimal? Tbr71Postotak { get; set; }

    /// <summary>Dodatno povećanje na Tbr. 7.1 rezultat (0.25 = +25%).</summary>
    [JsonPropertyName("tbr71_povecanje")]
    public decimal? Tbr71Povecanje { get; set; }

    /// <summary>Maksimalni broj bodova.</summary>
    [JsonPropertyName("max_bodova")]
    public int? MaxBodova { get; set; }

    /// <summary>Minimalni broj bodova.</summary>
    [JsonPropertyName("min_bodova")]
    public int? MinBodova { get; set; }

    /// <summary>Jedinica mjere (komad, sat, stranica).</summary>
    [JsonPropertyName("jedinica")]
    public string Jedinica { get; set; } = "komad";

    /// <summary>Redoslijed za prikaz.</summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    private string? _pravniIzvor;

    /// <summary>
    /// Propis i tarifni broj iz kojeg nagrada proizlazi. Prikazuje se u sučelju uz
    /// stavku i uz svaki redak troškovnika, da se svaki iznos može provjeriti u izvoru.
    ///
    /// Ako nije postavljen, sastavlja se od naziva Tarife i vlastitog tarifnog broja.
    /// Stavke čija se nagrada izvodi iz DRUGOG tarifnog broja postavljaju ga izričito,
    /// s uputom na taj broj.
    /// </summary>
    [JsonPropertyName("pravni_izvor")]
    public string PravniIzvor
    {
        get => _pravniIzvor ?? $"{TarifaConstants.NazivTarife}, {TarifniBroj}";
        set => _pravniIzvor = value;
    }

    /// <summary>True = bodovi dolaze iz ZaprijecenaKaznaTiers lookup-a (kazneni postupak).</summary>
    [JsonPropertyName("kazneni_base")]
    public bool KazneniBase { get; set; }

    /// <summary>Dodatno povećanje na kazneni base bodove (npr. 0.5 = +50% za žalbu).</summary>
    [JsonPropertyName("kazneni_povecanje")]
    public decimal? KazneniPovecanje { get; set; }

    /// <summary>
    /// Povećanje koje se primjenjuje umjesto <see cref="KazneniPovecanje"/> kad je
    /// zapriječena kazna dugotrajni zatvor.
    ///
    /// Tbr. 4. toč. 2 al. 3: "(...) s povišenjem od 25%, a na raspravi pred sudom
    /// drugog i trećeg stupnja za kaznena djela za koja je predviđena kazna
    /// dugotrajnog zatvora s povišenjem od 50%."
    /// </summary>
    [JsonPropertyName("kazneni_povecanje_dugotrajni")]
    public decimal? KazneniPovecanjeDugotrajni { get; set; }

    /// <summary>True = prekršajni stavka s mogućnošću oduzimanja; kad je oduzimanje aktivno, koristi Tbr. 7.1 po vrijednosti.</summary>
    [JsonPropertyName("prekrsaj_oduzimanje")]
    public bool PrekrsajOduzimanje { get; set; }

    // ── Jednokratna nagrada (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) ──
    //
    // Tarifa na više mjesta propisuje JEDNU nagradu za cijeli prvostupanjski
    // postupak, neovisno o broju obavljenih radnji. Takve se stavke ne množe
    // količinom; količina samo određuje koliko radnji prelazi uključeni broj.

    /// <summary>True = jedna nagrada za cijeli postupak, ne množi se količinom.</summary>
    [JsonPropertyName("jednokratna_nagrada")]
    public bool JednokratnaNagrada { get; set; }

    /// <summary>Koliko je radnji obuhvaćeno osnovicom (Tbr. 7. toč. 2 i 3: "najviše do četiri radnje").</summary>
    [JsonPropertyName("ukljucene_radnje")]
    public int? UkljuceneRadnje { get; set; }

    /// <summary>Bodovi za svaku radnju preko <see cref="UkljuceneRadnje"/> ("Za svaku daljnju radnju... 50 bodova").</summary>
    [JsonPropertyName("daljnja_radnja_bodovi")]
    public int? DaljnjaRadnjaBodovi { get; set; }
}
