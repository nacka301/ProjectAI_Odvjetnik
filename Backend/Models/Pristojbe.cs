namespace Backend.Models;

// Uredba o Tarifi sudskih pristojbi kao podatak, ne kao jedna tablica.
//
// Uredba ima devet poglavlja i 35 tarifnih brojeva. Svako poglavlje pokriva jednu
// vrstu postupka i nosi vlastite iznose — upravni spor je fiksan, ostavinski i
// izvanparnični imaju svoje ljestvice, a Tar. br. 1 (ljestvica po VPS-u) vrijedi
// SAMO u Poglavlju I. Model zato opisuje pojedinu odredbu, a ne jedan način računanja.

/// <summary>
/// Koja se Uredba primjenjuje na pojedinu pristojbenu radnju.
///
/// NN 50/2026 je na snazi od 1.6.2026. i tog je dana NN 37/2023 prestala važiti.
/// Obje su potrebne istodobno — vidi <see cref="UredbaGranice.NovaUredbaNaSnazi"/>.
/// </summary>
public enum UredbaVerzija
{
    /// <summary>Uredba o Tarifi sudskih pristojbi, NN 50/2026 — na snazi od 1.6.2026.</summary>
    Nn50_2026 = 0,

    /// <summary>Uredba o Tarifi sudskih pristojbi, NN 37/2023 — prijelazni slučajevi iz čl. 2.</summary>
    Nn37_2023 = 1
}

/// <summary>Granice u vremenu koje Uredba sama postavlja.</summary>
public static class UredbaGranice
{
    /// <summary>
    /// Dan stupanja na snagu NN 50/2026. Čl. 2 vezuje uz taj dan oba prijelazna
    /// slučaja: odgovor na tužbu podnesen PRIJE tog dana i upravni spor pokrenut
    /// PRIJE tog dana idu po staroj Uredbi.
    /// </summary>
    public static readonly DateOnly NovaUredbaNaSnazi = new(2026, 6, 1);
}

/// <summary>
/// Poglavlja Priloga Uredbe. Poglavlje određuje koji se tarifni brojevi uopće
/// mogu primijeniti — Tar. br. 1 postoji samo u Poglavlju I.
/// </summary>
public enum PristojbaPoglavlje
{
    /// <summary>Uredba za ovu vrstu posla ne propisuje pristojbu.</summary>
    Nema = 0,

    /// <summary>I. — Parnični i ovršni postupak (Tar. br. 1–3).</summary>
    ParnicniOvrsni = 1,

    /// <summary>II. — Izvanparnični postupak (Tar. br. 4–9).</summary>
    Izvanparnicni = 2,

    /// <summary>III. — Ostavinski postupak (Tar. br. 11–13).</summary>
    Ostavinski = 3,

    /// <summary>IV. — Zemljišnoknjižni postupak (Tar. br. 14–18).</summary>
    Zemljisnoknjizni = 4,

    /// <summary>V. — Kazneni postupak po privatnoj tužbi (Tar. br. 19–21).</summary>
    KazneniPrivatnaTuzba = 5,

    /// <summary>VI. — Stečaj i likvidacija (Tar. br. 22–24).</summary>
    Stecaj = 6,

    /// <summary>VII. — Sudski registar (Tar. br. 25–28).</summary>
    SudskiRegistar = 7,

    /// <summary>VIII. — Upravni spor (Tar. br. 29–30). Fiksni iznosi, bez VPS-a.</summary>
    UpravniSpor = 8,

    /// <summary>IX. — Ostali slučajevi (Tar. br. 31–35).</summary>
    OstaliSlucajevi = 9
}

// ── Načini izračuna ──────────────────────────────────────────────
//
// Zatvoren skup: svih 35 tarifnih brojeva svodi se na ovih sedam oblika.
// Zatvoren je namjerno — nova odredba koja se ne uklapa mora se vidjeti kao
// promjena modela, a ne kao još jedan `if` u kalkulatoru.

/// <summary>Kako se iznos pristojbe izvodi iz osnovice.</summary>
public abstract record PristojbaIzracun;

/// <summary>Iznos ne ovisi ni o čemu (npr. Tar. br. 29 — tužba u upravnom sporu 110,00).</summary>
public sealed record Fiksno(decimal Iznos) : PristojbaIzracun;

/// <summary>Uredba izrijekom kaže da se pristojba ne plaća (Tar. br. 2 st. 9 — sudske nagodbe).</summary>
public sealed record NePlacaSe : PristojbaIzracun;

/// <summary>
/// Udio druge odredbe: „polovica pristojbe iz stavka 1.", „uvećan za 100 %",
/// „peterostruko", „dvostruko od st. 2", „10 % propisane pristojbe".
/// </summary>
/// <param name="OdredbaId">Odredba na koju se upućuje.</param>
/// <param name="Faktor">0,5 za polovicu; 2,0 za uvećanje od 100 %; 5,0 za peterostruko.</param>
/// <param name="Max">Gornja granica NAKON množenja (Tar. br. 3 st. 5 — najviše 50,00).</param>
public sealed record UdioOd(string OdredbaId, decimal Faktor, decimal? Max = null) : PristojbaIzracun;

/// <summary>Jedan razred ljestvice: sve do <paramref name="DoIznosa"/> plaća <paramref name="Pristojba"/>.</summary>
public readonly record struct Razred(decimal DoIznosa, decimal Pristojba);

/// <summary>
/// Ljestvica s postotnim nastavkom — Tar. br. 1 st. 1:
/// razredi, a preko praga „140,00 eura i još 1 % na razliku iznad 2.000,00,
/// ali ne više od 1.500,00".
/// </summary>
public sealed record LjestvicaPostotak(
    IReadOnlyList<Razred> Razredi,
    decimal Prag,
    decimal Baza,
    decimal Stopa,
    decimal Max) : PristojbaIzracun;

/// <summary>
/// Ljestvica sa stepenastim nastavkom — Tar. br. 5 (dioba) i Tar. br. 11 (ostavina):
/// preko praga „60,00 i još 30,00 na svakih ZAPOČETIH 1.300,00".
///
/// „Započetih" znači zaokruživanje broja koraka naviše, pa i 1,00 € preko praga
/// nosi puni korak.
/// </summary>
public sealed record LjestvicaKorak(
    IReadOnlyList<Razred> Razredi,
    decimal Prag,
    decimal Baza,
    decimal Korak,
    decimal PoKoraku,
    decimal Max) : PristojbaIzracun;

/// <summary>
/// Postotak vrijednosti s kapom — Tar. br. 23 st. 2 (2 % tražbine, najviše 140,00)
/// i Tar. br. 24 (2 % diobne mase, najviše 560,00).
/// </summary>
public sealed record PostotakVrijednosti(decimal Stopa, decimal Max) : PristojbaIzracun;

/// <summary>
/// Stepenice po broju, ne po novcu — Tar. br. 15, etažiranje: do 10 dijelova 30,00,
/// 11–100 → 80,00, 101–500 → 140,00, preko 500 → 280,00.
/// </summary>
/// <param name="Stepenice">Uzlazno po gornjoj granici; zadnja se primjenjuje na sve iznad.</param>
public sealed record Stepenasto(IReadOnlyList<Razred> Stepenice) : PristojbaIzracun;

// ── Odredba i rezultat ───────────────────────────────────────────

/// <summary>
/// Jedan tarifni broj (ili njegov stavak) iz Uredbe — jedinica koja se može naplatiti.
/// </summary>
/// <param name="Id">Stabilan ključ, npr. „tbr1_st1"; putuje kroz API i sprema se uz troškovnik.</param>
/// <param name="Poglavlje">Poglavlje Priloga iz kojeg odredba dolazi.</param>
/// <param name="TarifniBroj">Oznaka za prikaz, npr. „Tar. br. 3 st. 1".</param>
/// <param name="Opis">Radnja koja se oporezuje, riječima Uredbe.</param>
/// <param name="Izracun">Kako se iznos izvodi iz osnovice.</param>
/// <param name="OsnovicaOpis">
/// Što je osnovica ove odredbe („vrijednost predmeta spora", „čista ostavina",
/// „broj etažnih dijelova"). Prazno kad odredba osnovicu ne koristi.
/// </param>
public sealed record PristojbenaOdredba(
    string Id,
    PristojbaPoglavlje Poglavlje,
    string TarifniBroj,
    string Opis,
    PristojbaIzracun Izracun,
    string OsnovicaOpis = "");

// ── Izlaz ────────────────────────────────────────────────────────

/// <summary>
/// Jedan naplaćeni redak pristojbe. Uredba naplaćuje po RADNJI — tužba, presuda i
/// žalba u istom predmetu tri su zasebne pristojbe, pa ih troškovnik nosi kao listu,
/// a ne kao jedan zbroj bez traga o tome odakle je došao.
/// </summary>
/// <param name="Osnovica">Vrijednost na koju je odredba primijenjena; 0 kod fiksnih iznosa.</param>
/// <param name="Kolicina">Broj istovrsnih radnji (npr. 12 preslika po Tar. br. 28).</param>
/// <param name="IznosPoJedinici">Iznos jedne radnje, zaokružen.</param>
/// <param name="Iznos">IznosPoJedinici × Kolicina.</param>
/// <param name="Verzija">Koja je Uredba primijenjena na OVAJ redak — čl. 2 dopušta miješanje.</param>
/// <param name="Automatski">
/// True kad je redak izveden iz odabranih stavki tarife, false kad ga je korisnik dodao ručno.
/// Sučelje po tome zna koji se retci smiju tiho promijeniti kad se promijeni odabir stavki.
/// </param>
public sealed record PristojbenaStavka(
    string OdredbaId,
    string TarifniBroj,
    PristojbaPoglavlje Poglavlje,
    string Opis,
    decimal Osnovica,
    int Kolicina,
    decimal IznosPoJedinici,
    decimal Iznos,
    UredbaVerzija Verzija,
    string PravniIzvor,
    bool Automatski);

/// <summary>
/// Ishod obračuna sudskih pristojbi za cijeli troškovnik.
///
/// Oslobođenje po čl. 11 Zakona nuli iznose, ali retke ostavlja — iz istog razloga iz
/// kojeg je i prije oslobođenje zadržavalo osnovu: „0,00 €" bez obrazloženja izgleda
/// kao greška u računu.
/// </summary>
/// <param name="VpsUdio">
/// Vrijednost predmeta spora na koju je pristojba stvarno obračunata. Razlikuje se od
/// ukupnog VPS-a samo kad je dio suparničara oslobođen — čl. 12 Zakona o sudskim
/// pristojbama.
/// </param>
/// <param name="NaTeretProtustranke">
/// Čl. 13 — pristojba koju bi platila oslobođena stranka, razmjerno njezinu uspjehu u
/// postupku, a koju plaća protivna stranka. Obveza nastaje po pravomoćnosti, pa iznos
/// stoji zasebno i ne ulazi u ukupan trošak zastupane stranke. 0 kad se ne obračunava.
/// </param>
/// <param name="OslobodjenjeKategorija">
/// Po čemu je stranka oslobođena (ključ iz <see cref="Backend.Models.OslobodjenjeKategorija"/>);
/// null kad nije navedeno. Iznos ne mijenja — troškovnik mora pokazati osnovu, kao što
/// svaka tarifna stavka nosi svoj pravni izvor.
/// </param>
public sealed record PristojbeRezultat(
    IReadOnlyList<PristojbenaStavka> Stavke,
    decimal Ukupno,
    bool Oslobodjen,
    decimal VpsUdio = 0m,
    decimal NaTeretProtustranke = 0m,
    string? OslobodjenjeKategorija = null)
{
    /// <summary>Prazan ishod — nijedna odabrana stavka ne povlači pristojbu.</summary>
    public static readonly PristojbeRezultat Prazan = new([], 0m, false);
}

// ── Ulaz ─────────────────────────────────────────────────────────

/// <summary>
/// Pristojba koju je korisnik dodao sam, izvan onoga što se izvodi iz tarifnih stavaka.
///
/// Postoji jer se velik dio Uredbe ne da izvesti iz odvjetničke tarife: ovjere,
/// etažiranje, upis broda, polog, e-Notar. Te radnje odvjetnik obavlja, ali za njih
/// nema tarifne stavke iz koje bi se pristojba pročitala.
/// </summary>
/// <param name="Osnovica">
/// Vrijednost na koju se odredba primjenjuje; kad je null, uzima se vrijednost
/// predmeta spora iz troškovnika.
/// </param>
public sealed record RucnaPristojba(string OdredbaId, decimal? Osnovica = null, int Kolicina = 1);

/// <summary>
/// Sve što obračun pristojbi treba, a ne proizlazi iz odabranih tarifnih stavaka.
/// </summary>
/// <param name="DatumPodnosenjaOdgovoraNaTuzbu">
/// Čl. 2 NN 50/2026, prvi slučaj — odgovor na tužbu podnesen prije 1.6.2026. ide po
/// staroj Uredbi. Null znači „nije prijelazni slučaj".
/// </param>
/// <param name="DatumPokretanjaUpravnogSpora">
/// Čl. 2 NN 50/2026, drugi slučaj — upravni spor pokrenut prije 1.6.2026. ide po
/// staroj Uredbi, i to cijeli, ne samo tužba.
/// </param>
public sealed record PristojbeOpcije(
    DateOnly? DatumPodnosenjaOdgovoraNaTuzbu = null,
    DateOnly? DatumPokretanjaUpravnogSpora = null,
    IReadOnlyList<RucnaPristojba>? Dodane = null,
    IReadOnlySet<string>? Iskljucene = null)
{
    /// <summary>Nema prijelaznih datuma ni ručnih izmjena — sve po NN 50/2026.</summary>
    public static readonly PristojbeOpcije Zadano = new();
}

/// <summary>Naziv poglavlja za prikaz — enum sam nije za oči korisnika.</summary>
public static class PoglavljeNazivi
{
    public static string Naziv(PristojbaPoglavlje poglavlje) => poglavlje switch
    {
        PristojbaPoglavlje.ParnicniOvrsni => "I. Parnični i ovršni postupak",
        PristojbaPoglavlje.Izvanparnicni => "II. Izvanparnični postupak",
        PristojbaPoglavlje.Ostavinski => "III. Ostavinski postupak",
        PristojbaPoglavlje.Zemljisnoknjizni => "IV. Zemljišnoknjižni postupak",
        PristojbaPoglavlje.KazneniPrivatnaTuzba => "V. Kazneni postupak po privatnoj tužbi",
        PristojbaPoglavlje.Stecaj => "VI. Stečaj i likvidacija",
        PristojbaPoglavlje.SudskiRegistar => "VII. Sudski registar",
        PristojbaPoglavlje.UpravniSpor => "VIII. Upravni spor",
        PristojbaPoglavlje.OstaliSlucajevi => "IX. Ostali slučajevi",
        _ => ""
    };
}
