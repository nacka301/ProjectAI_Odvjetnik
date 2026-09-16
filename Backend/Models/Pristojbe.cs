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
/// Puni nazivi Uredbi, onako kako se zapisuju uz spremljeni troškovnik.
///
/// Uz troškovnik se sprema NAZIV, a ne enum: kad jednom dođe nova Uredba, stari zapis
/// mora i dalje nositi naziv propisa po kojem je stvarno izračunat.
/// </summary>
public static class UredbaNazivi
{
    public static string PuniNaziv(UredbaVerzija verzija) => verzija switch
    {
        UredbaVerzija.Nn37_2023 => TarifaConstants.NazivStareUredbeOPristojbama,
        _ => TarifaConstants.NazivUredbeOPristojbama,
    };

    /// <summary>
    /// Različite Uredbe primijenjene na retke, starija prije novije. Prazno kad
    /// troškovnik nema nijednu pristojbenu stavku.
    /// </summary>
    public static List<string> Primijenjene(IEnumerable<UredbaVerzija> verzije) =>
        [.. verzije
            .Distinct()
            .OrderBy(v => v == UredbaVerzija.Nn37_2023 ? 0 : 1)
            .Select(PuniNaziv)];
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

// ── Čl. 7 Zakona o sudskim pristojbama — elektronički oblik ──────
//
// Doslovan tekst (NN 118/18, 51/23), https://www.zakon.hr/z/224/zakon-o-sudskim-pristojbama:
//
//  (1) Za podneske koji se podnose u elektroničkom obliku sukladno posebnim propisima
//      putem informacijskog sustava u primjeni u poslovanju suda pristojba se plaća u
//      trenutku njihova podnošenja u visini polovice propisanog iznosa pristojbe
//      utvrđene Tarifom.
//  (2) Na odluke koje sud dostavlja u elektroničkom obliku sukladno posebnim propisima
//      putem informacijskog sustava u primjeni u poslovanju suda pristojba se plaća u
//      visini polovice propisanog iznosa pristojbe utvrđene Tarifom ako je uplaćena u
//      roku od tri dana od dana elektroničke dostave odluke.
//  (3) Za podneske i odluke u zemljišnoknjižnom postupku za koje je zemljišnoknjižni
//      upis predložen u elektroničkom obliku sukladno posebnim propisima putem
//      informacijskog sustava u primjeni u poslovanju suda pristojba se plaća u
//      trenutku podnošenja u visini polovice propisanog iznosa pristojbe utvrđene
//      Tarifom.
//  (4) Ako se sudska pristojba sukladno odredbama ovoga članka ne plati u utvrđenim
//      rokovima, sudska pristojba plaća se u iznosima utvrđenim Tarifom.
//
// Tri stavka, tri različita uvjeta, pa ni jedan prekidač ne pokriva sva tri:
//   st. 1 — stranka je PODNIJELA elektronički i platila pri podnošenju
//   st. 2 — SUD je dostavio odluku elektronički i plaćeno je u roku od 3 dana
//   st. 3 — stranka je predložila ZK UPIS elektronički i platila pri podnošenju;
//           tada se prepolovljuju i podnesci i odluke u tom postupku
// St. 4 nema svoje polje: propušten rok znači da uvjet nije ispunjen, a to je isto
// što i isključena oznaka.

/// <summary>
/// Što se pod odredbom naplaćuje: nešto što stranka PODNOSI, nešto što sud DONOSI,
/// ili radnja koja nije ni jedno ni drugo (ovjera, preslika, potvrda, pohrana, paušal).
///
/// Razvrstavanje je PODATAK uz odredbu, a ne pogađanje iz opisa — „Prijedlog za ovrhu"
/// i „Rješenje o ovrsi" razlikuju se jednom riječju, a nose suprotan stavak čl. 7.
/// </summary>
public enum VrstaRadnje
{
    /// <summary>Ovjere, preslike, potvrde, pohrana, paušalne pristojbe — čl. 7 ih ne dira.</summary>
    Ostalo = 0,

    /// <summary>Stranka podnosi sudu: tužba, prijedlog, žalba, revizija, prijava, odgovor.</summary>
    Podnesak = 1,

    /// <summary>Sud donosi ili upisuje: presuda, rješenje, odluka, upis.</summary>
    Odluka = 2
}

/// <summary>Koji stavak čl. 7 upravlja umanjenjem za pojedinu odredbu.</summary>
public enum Cl7Rezim
{
    /// <summary>Nijedan — odredba nije ni podnesak ni odluka.</summary>
    Nema = 0,

    /// <summary>St. 1 — podnesak predan elektronički, plaćeno pri podnošenju.</summary>
    St1Podnesak = 1,

    /// <summary>St. 2 — sud dostavio odluku elektronički, plaćeno u roku od 3 dana.</summary>
    St2Odluka = 2,

    /// <summary>St. 3 — zk upis predložen elektronički; pokriva i podneske i odluke.</summary>
    St3Zemljisnoknjizni = 3
}

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
/// <param name="Vrsta">
/// Podnosi li se ili donosi — vidi <see cref="VrstaRadnje"/>. Zadano
/// <see cref="VrstaRadnje.Ostalo"/>, jer nerazvrstana odredba ne smije tiho dobiti
/// umanjenje po čl. 7; da nijedna ne ostane nerazvrstana slučajno, brine test
/// <c>SvakaOdredbaJeRazvrstana</c>.
/// </param>
/// <param name="VecImaElektronickoUmanjenje">
/// Iznos ove odredbe VEĆ je polovica, i to dana iz istog razloga zbog kojeg umanjuje i
/// čl. 7 — zbog elektroničkog oblika. Takav se redak ne umanjuje drugi put.
///
/// <para>Kriterij je RAZLOG, ne to što u odredbi piše „polovica". Polovica iz Tar. br. 1
/// st. 2 dana je zbog VRSTE podneska (odgovor na tužbu), ne zbog oblika, pa se s čl. 7
/// legitimno slaže: odgovor na tužbu predan elektronički ide Tar. br. 1 st. 1 → polovica
/// po st. 2 → pa još polovica po čl. 7.</para>
/// </param>
public sealed record PristojbenaOdredba(
    string Id,
    PristojbaPoglavlje Poglavlje,
    string TarifniBroj,
    string Opis,
    PristojbaIzracun Izracun,
    string OsnovicaOpis = "",
    VrstaRadnje Vrsta = VrstaRadnje.Ostalo,
    bool VecImaElektronickoUmanjenje = false)
{
    /// <summary>
    /// Koji stavak čl. 7 upravlja ovom odredbom. Izvodi se, a ne sprema: st. 3 je
    /// izuzetak po POSTUPKU (zemljišnoknjižni), st. 1 i st. 2 po VRSTI radnje, pa bi
    /// zasebno polje bilo treći podatak koji se mora slagati s prva dva.
    ///
    /// Ovjera preslike (Tar. br. 17 i 18) stoji u zk poglavlju, ali nije ni podnesak ni
    /// odluka — zato provjera vrste ide prije provjere poglavlja.
    /// </summary>
    public Cl7Rezim Cl7Rezim => Vrsta switch
    {
        VrstaRadnje.Ostalo => Cl7Rezim.Nema,
        _ when Poglavlje == PristojbaPoglavlje.Zemljisnoknjizni => Cl7Rezim.St3Zemljisnoknjizni,
        VrstaRadnje.Podnesak => Cl7Rezim.St1Podnesak,
        VrstaRadnje.Odluka => Cl7Rezim.St2Odluka,
        _ => Cl7Rezim.Nema
    };
}

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
/// <param name="Cl7Rezim">
/// Koji stavak čl. 7 uopće može umanjiti ovaj redak. Sučelje po tome zna smije li uz
/// redak ponuditi kvačicu za elektroničku dostavu odluke.
/// </param>
/// <param name="UmanjenoZaElektronickiOblik">
/// Je li umanjenje stvarno primijenjeno. Ide u ispis: prepolovljena pristojba bez
/// obrazloženja ne razlikuje se od greške u računu.
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
    bool Automatski,
    Cl7Rezim Cl7Rezim = Cl7Rezim.Nema,
    bool UmanjenoZaElektronickiOblik = false);

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
/// <param name="PodnesciElektronicki">
/// Čl. 7 st. 1 — ključevi odredbi (podnesaka) predanih elektronički i za koje je
/// pristojba plaćena u trenutku podnošenja. PO RETKU, jednako kao <paramref name="OdlukeElektronicki"/>:
/// jedan podnesak (npr. žalba) može ispuniti uvjet, a drugi (npr. tužba) ne. Oba
/// uvjeta zajedno; st. 4 propušten rok vraća na puni iznos, pa izostanak ključa
/// znači i „nije plaćeno na vrijeme".
/// </param>
/// <param name="ZkUpisElektronicki">
/// Čl. 7 st. 3 — zemljišnoknjižni upis je predložen elektronički I plaćeno je pri
/// podnošenju. Zaseban od st. 1 jer uvjet nije oblik pojedinog podneska nego oblik
/// prijedloga za upis; kad je ispunjen, prepolovljuju se i podnesci i odluke u tom
/// postupku.
/// </param>
/// <param name="OdlukeElektronicki">
/// Čl. 7 st. 2 — ključevi odredbi (odluka) koje je sud dostavio elektronički i za koje
/// je pristojba uplaćena u roku od tri dana. PO RETKU, jer se rok može ispuniti za
/// jednu odluku, a za drugu ne. Ista se odredba u troškovniku pojavljuje najviše
/// jednom, pa je ključ odredbe dovoljan.
/// </param>
public sealed record PristojbeOpcije(
    DateOnly? DatumPodnosenjaOdgovoraNaTuzbu = null,
    DateOnly? DatumPokretanjaUpravnogSpora = null,
    IReadOnlyList<RucnaPristojba>? Dodane = null,
    IReadOnlySet<string>? Iskljucene = null,
    IReadOnlySet<string>? PodnesciElektronicki = null,
    bool ZkUpisElektronicki = false,
    IReadOnlySet<string>? OdlukeElektronicki = null)
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
