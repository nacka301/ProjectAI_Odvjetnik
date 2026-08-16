namespace Backend.Models;

// Vrijednost predmeta spora prema Zakonu o sudskim pristojbama, cl. 14-26.
// Za niz postupaka VPS nije stvar unosa nego zakonske odredbe: fiksan iznos,
// donja granica, gornja granica ili izvedena vrijednost.

/// <summary>
/// Kako Zakon ograničava vrijednost predmeta spora za pojedinu vrstu postupka.
/// </summary>
public enum VpsNacin
{
    /// <summary>Zakon ne propisuje iznos — vrijedi ono što je uneseno (čl. 14).</summary>
    Slobodan = 0,

    /// <summary>Zakon propisuje točan iznos; uneseni se ne primjenjuje.</summary>
    Fiksan = 1,

    /// <summary>Zakon propisuje donju granicu; uneseni se diže na nju ako je manji.</summary>
    Minimalan = 2,

    /// <summary>Zakon propisuje gornju granicu; uneseni se spušta na nju ako je veći.</summary>
    Maksimalan = 3,

    /// <summary>Čl. 15 — zbroj davanja za tri mjeseca, izveden iz mjesečnog iznosa.</summary>
    IzvedenUzdrzavanje = 4
}

/// <summary>
/// Iznosi koje Zakon o sudskim pristojbama navodi u čl. 19–25. Svaki se pojavljuje na
/// više mjesta, pa mora biti jedna konstanta, a ne isti broj prepisan četiri puta.
/// </summary>
public static class VpsIznosi
{
    /// <summary>1.327,22 € — čl. 19 (donja granica), čl. 25 (fiksno), čl. 21 st. 5 i 6 (gornja granica).</summary>
    public const decimal Prag1327 = 1_327.22m;

    /// <summary>663,61 € — čl. 21 st. 2, 3 i 4 te osiguranje dokaza prije parnice.</summary>
    public const decimal Prag663 = 663.61m;

    /// <summary>2.654,45 € — čl. 21 st. 1, donja granica kod otkaza najma ili zakupa.</summary>
    public const decimal Prag2654 = 2_654.45m;

    /// <summary>6.634,14 € — čl. 20 st. 1 (donja granica) i st. 2 (fiksno).</summary>
    public const decimal Prag6634 = 6_634.14m;

    /// <summary>Čl. 15 — „zbroj davanja za tri mjeseca".</summary>
    public const int UzdrzavanjeMjeseci = 3;
}

/// <summary>Jedno pravilo o vrijednosti predmeta spora iz Zakona o sudskim pristojbama.</summary>
/// <param name="Kljuc">Vrijednost koju zahtjev šalje u polju <c>vrsta_postupka</c>.</param>
/// <param name="Nacin">Kako se pravilo primjenjuje na uneseni iznos.</param>
/// <param name="Iznos">Iznos iz Zakona; null kod <see cref="VpsNacin.Slobodan"/> i uzdržavanja.</param>
/// <param name="Clanak">Odredba iz koje pravilo proizlazi, za navođenje na ispisu.</param>
/// <param name="Naziv">Naziv vrste postupka za padajući izbornik.</param>
/// <param name="Naputak">
/// Što Zakon kaže da treba upisati kad ne propisuje iznos (čl. 17, 18). Prazan kad
/// pravilo samo određuje broj.
/// </param>
public sealed record VpsPravilo(
    string Kljuc,
    VpsNacin Nacin,
    decimal? Iznos,
    string Clanak,
    string Naziv,
    string Naputak = "");

/// <summary>
/// Vrste postupaka za koje Zakon o sudskim pristojbama posebno uređuje vrijednost
/// predmeta spora (čl. 15–25).
/// </summary>
public static class VrstaPostupka
{
    /// <summary>Čl. 14 — opće pravilo, vrijedi uneseni iznos. Zadano.</summary>
    public const string Opci = "opci";

    /// <summary>Čl. 19 — nenovčani zahtjev: činjenje, trpljenje, utvrđenje prava, istinitost isprave.</summary>
    public const string NenovcaniZahtjev = "nenovcani_zahtjev";

    /// <summary>Čl. 25 — vrijednost se uopće ne može utvrditi.</summary>
    public const string NeutvrdivaVrijednost = "neutvrdiva_vrijednost";

    /// <summary>Čl. 21 st. 1 — otkaz ugovora o najmu ili zakupu.</summary>
    public const string OtkazNajmaZakupa = "otkaz_najma_zakupa";

    /// <summary>Čl. 21 st. 2 — smetanje posjeda.</summary>
    public const string SmetanjePosjeda = "smetanje_posjeda";

    /// <summary>Čl. 21 st. 3 — utvrđivanje ili osporavanje očinstva odnosno majčinstva.</summary>
    public const string OcinstvoMajcinstvo = "ocinstvo_majcinstvo";

    /// <summary>Čl. 21 st. 4 — postojanje ili nepostojanje braka, poništaj, razvod.</summary>
    public const string Brak = "brak";

    /// <summary>Čl. 21 st. 5 — red prvenstva tražbina u ovrsi.</summary>
    public const string RedPrvenstvaOvrha = "red_prvenstva_ovrha";

    /// <summary>Čl. 21 st. 6 — prethodna pravna pitanja bez restitucije ili naknade.</summary>
    public const string PrethodnoPravnoPitanje = "prethodno_pravno_pitanje";

    /// <summary>Čl. 21 st. 3 posljednji odlomak — osiguranje dokaza prije pokretanja parnice.</summary>
    public const string OsiguranjeDokaza = "osiguranje_dokaza";

    /// <summary>Čl. 15 — uzdržavanje: zbroj davanja za tri mjeseca.</summary>
    public const string Uzdrzavanje = "uzdrzavanje";

    /// <summary>Čl. 17 — dioba imovine.</summary>
    public const string DiobaImovine = "dioba_imovine";

    /// <summary>Čl. 18 — nasljeđivanje cijele ostavine.</summary>
    public const string Nasljedivanje = "nasljedivanje";

    /// <summary>Čl. 20 st. 1 — poništenje pravorijeka arbitraže.</summary>
    public const string PonistenjePravorijeka = "ponistenje_pravorijeka";

    /// <summary>Čl. 20 st. 2 — prestanak važnosti ugovora o arbitraži.</summary>
    public const string PrestanakArbitraze = "prestanak_arbitraze";

    /// <summary>
    /// Sva pravila, ključem. Redoslijed je onaj kojim se prikazuju u izborniku:
    /// opće pravilo prvo, zatim odredbe redom po vrsti postupka.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, VpsPravilo> Pravila =
        new Dictionary<string, VpsPravilo>
        {
            [Opci] = new(
                Opci, VpsNacin.Slobodan, null, "čl. 14",
                "Opće pravilo — vrijednost u vrijeme podnošenja tužbe"),

            [NenovcaniZahtjev] = new(
                NenovcaniZahtjev, VpsNacin.Minimalan, VpsIznosi.Prag1327, "čl. 19",
                "Nenovčani zahtjev (činjenje, trpljenje, utvrđenje prava, istinitost isprave)"),

            [NeutvrdivaVrijednost] = new(
                NeutvrdivaVrijednost, VpsNacin.Fiksan, VpsIznosi.Prag1327, "čl. 25",
                "Vrijednost se ne može utvrditi"),

            [OtkazNajmaZakupa] = new(
                OtkazNajmaZakupa, VpsNacin.Minimalan, VpsIznosi.Prag2654, "čl. 21 st. 1",
                "Otkaz ugovora o najmu ili zakupu",
                "Upisuje se jednogodišnja najamnina odnosno zakupnina."),

            [SmetanjePosjeda] = new(
                SmetanjePosjeda, VpsNacin.Fiksan, VpsIznosi.Prag663, "čl. 21 st. 2",
                "Smetanje posjeda"),

            [OcinstvoMajcinstvo] = new(
                OcinstvoMajcinstvo, VpsNacin.Fiksan, VpsIznosi.Prag663, "čl. 21 st. 3",
                "Utvrđivanje ili osporavanje očinstva odnosno majčinstva"),

            [Brak] = new(
                Brak, VpsNacin.Fiksan, VpsIznosi.Prag663, "čl. 21 st. 4",
                "Brak — postojanje, nepostojanje, poništaj, razvod"),

            [RedPrvenstvaOvrha] = new(
                RedPrvenstvaOvrha, VpsNacin.Maksimalan, VpsIznosi.Prag1327, "čl. 21 st. 5",
                "Red prvenstva tražbina u ovrsi",
                "Upisuje se visina potraživanja; iznad zakonske granice se ne obračunava."),

            [PrethodnoPravnoPitanje] = new(
                PrethodnoPravnoPitanje, VpsNacin.Maksimalan, VpsIznosi.Prag1327, "čl. 21 st. 6",
                "Prethodna pravna pitanja bez restitucije ili naknade"),

            [OsiguranjeDokaza] = new(
                OsiguranjeDokaza, VpsNacin.Fiksan, VpsIznosi.Prag663, "čl. 21 st. 3",
                "Osiguranje dokaza prije pokretanja parnice"),

            [Uzdrzavanje] = new(
                Uzdrzavanje, VpsNacin.IzvedenUzdrzavanje, null, "čl. 15",
                "Uzdržavanje",
                "Upisuje se mjesečno davanje; vrijednost je zbroj davanja za tri mjeseca."),

            [DiobaImovine] = new(
                DiobaImovine, VpsNacin.Slobodan, null, "čl. 17",
                "Dioba imovine",
                "Upisuje se vrijednost dijela koji tužitelj traži izdvojiti, ne cijele imovine."),

            [Nasljedivanje] = new(
                Nasljedivanje, VpsNacin.Slobodan, null, "čl. 18",
                "Nasljeđivanje cijele ostavine",
                "Upisuje se vrijednost nasljednog dijela, ne cijele ostavine."),

            [PonistenjePravorijeka] = new(
                PonistenjePravorijeka, VpsNacin.Minimalan, VpsIznosi.Prag6634, "čl. 20 st. 1",
                "Poništenje pravorijeka arbitraže",
                "Upisuje se dosuđeni iznos."),

            [PrestanakArbitraze] = new(
                PrestanakArbitraze, VpsNacin.Fiksan, VpsIznosi.Prag6634, "čl. 20 st. 2",
                "Prestanak važnosti ugovora o arbitraži")
        };

    /// <summary>Dopušteni ključevi, redoslijedom prikaza.</summary>
    public static readonly string[] Sve = [.. Pravila.Keys];
}

/// <summary>
/// Čl. 16 — kako se sastavlja vrijednost kad tužba sadrži više zahtjeva.
/// </summary>
public static class ZahtjeviNacin
{
    /// <summary>„Više zahtjeva protiv istog tuženika" → zbroj vrijednosti.</summary>
    public const string Zbroj = "zbroj";

    /// <summary>
    /// Suparničarstvo, istovrsni zahtjevi → „vrijednost svakog pojedinog zahtjeva".
    /// Zahtjevi se ne zbrajaju; ovaj troškovnik pokriva onaj označen kao primarni,
    /// a za ostale ide posebna pristojba.
    /// </summary>
    public const string Zasebno = "zasebno";

    /// <summary>Povezani (eventualni) zahtjevi → „zahtjev čija je vrijednost veća".</summary>
    public const string Eventualni = "eventualni";

    public static readonly string[] Sve = [Zbroj, Zasebno, Eventualni];
}

/// <summary>
/// Čl. 24 — po kojoj se vrijednosti plaća pristojba na pravni lijek.
/// </summary>
public static class PravniLijek
{
    /// <summary>Nije riječ o pravnom lijeku. Zadano.</summary>
    public const string Nema = "nema";

    /// <summary>Pobija se cijela odluka → VPS iz prvostupanjskog postupka.</summary>
    public const string CijelaOdluka = "cijela_odluka";

    /// <summary>Pobija se samo dio odluke → vrijednost pobijanog dijela.</summary>
    public const string PobijaniDio = "pobijani_dio";

    /// <summary>Lijek samo protiv odluke o troškovima ili sporednim potraživanjima.</summary>
    public const string SamoTroskovi = "samo_troskovi";

    public static readonly string[] Sve = [Nema, CijelaOdluka, PobijaniDio, SamoTroskovi];
}

/// <summary>Razriješena vrijednost predmeta spora i put kojim se do nje došlo.</summary>
/// <param name="Vps">Vrijednost koja se primjenjuje na sudsku pristojbu.</param>
/// <param name="Uneseni">Vrijednost prije primjene zakonskih pravila.</param>
/// <param name="Kljuc">Ključ primijenjene vrste postupka.</param>
/// <param name="PravniIzvor">Propis i članak iz kojeg vrijednost proizlazi.</param>
/// <param name="Prilagodjen">True kad se <paramref name="Vps"/> razlikuje od unesenog.</param>
/// <param name="Napomena">Rečenica za sučelje i ispis; null kad nema što objasniti.</param>
/// <param name="OstaliZahtjevi">
/// Čl. 16 uz „zasebno" — zahtjevi koje ovaj troškovnik ne pokriva, za koje ide posebna
/// pristojba. Prazno u svim ostalim slučajevima.
/// </param>
public sealed record VpsRezultat(
    decimal Vps,
    decimal Uneseni,
    string Kljuc,
    string PravniIzvor,
    bool Prilagodjen,
    string? Napomena,
    IReadOnlyList<ZahtjevStavka> OstaliZahtjevi);
