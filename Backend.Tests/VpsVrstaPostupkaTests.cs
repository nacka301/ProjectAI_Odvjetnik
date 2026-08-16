using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Vrijednost predmeta spora prema Zakonu o sudskim pristojbama (NN 118/18, 51/23),
/// čl. 14–26.
///
/// Za niz postupaka VPS nije stvar unosa nego zakonske odredbe. Prije ovih pravila
/// korisnik je mogao upisati 50.000 € za smetanje posjeda i dobiti pristojbu od
/// 546,45 €, iako Zakon za taj postupak propisuje vrijednost od 663,61 € — a time i
/// pristojbu od 26,54 €.
/// </summary>
public class VpsVrstaPostupkaTests
{
    private static TroskovnikResponse Izracunaj(
        decimal vps, string vrsta, Action<TroskovnikRequest>? dopuni = null)
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = vps,
            VrstaPostupka = vrsta,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        };

        dopuni?.Invoke(zahtjev);

        return new TroskovnikService().Izracunaj(zahtjev);
    }

    // ── Fiksne vrijednosti — uneseni iznos se ne primjenjuje ──

    [Theory]
    [InlineData(VrstaPostupka.SmetanjePosjeda,       663.61)]   // čl. 21 st. 2
    [InlineData(VrstaPostupka.OcinstvoMajcinstvo,    663.61)]   // čl. 21 st. 3
    [InlineData(VrstaPostupka.Brak,                  663.61)]   // čl. 21 st. 4
    [InlineData(VrstaPostupka.OsiguranjeDokaza,      663.61)]   // čl. 21 st. 3, osiguranje dokaza
    [InlineData(VrstaPostupka.NeutvrdivaVrijednost, 1_327.22)]  // čl. 25
    [InlineData(VrstaPostupka.PrestanakArbitraze,   6_634.14)]  // čl. 20 st. 2
    public void FiksniIznos_GaziUneseni(string vrsta, decimal ocekivano)
    {
        var r = Izracunaj(50_000m, vrsta);

        Assert.Equal(ocekivano, r.VpsZakonski);
        Assert.True(r.VpsPrilagodjen);
        Assert.False(string.IsNullOrEmpty(r.VpsNapomena));
    }

    /// <summary>
    /// Fiksan iznos vrijedi i kad je uneseno manje — nije riječ o donjoj granici.
    /// </summary>
    [Fact]
    public void FiksniIznos_DizeIPreniskiUnos()
    {
        var r = Izracunaj(100m, VrstaPostupka.SmetanjePosjeda);

        Assert.Equal(663.61m, r.VpsZakonski);
    }

    /// <summary>Kad je uneseno točno ono što Zakon propisuje, nema što objašnjavati.</summary>
    [Fact]
    public void FiksniIznos_JednakUnesenom_NijePrilagodjen()
    {
        var r = Izracunaj(663.61m, VrstaPostupka.SmetanjePosjeda);

        Assert.Equal(663.61m, r.VpsZakonski);
        Assert.False(r.VpsPrilagodjen);
        Assert.Null(r.VpsNapomena);
    }

    // ── Donje granice ──

    [Theory]
    // čl. 19 — nenovčani zahtjev, minimum 1.327,22 €
    [InlineData(VrstaPostupka.NenovcaniZahtjev,        500, 1_327.22)]
    [InlineData(VrstaPostupka.NenovcaniZahtjev,     80_000,    80_000)]
    // čl. 21 st. 1 — otkaz najma ili zakupa, minimum 2.654,45 €
    [InlineData(VrstaPostupka.OtkazNajmaZakupa,      1_200, 2_654.45)]
    [InlineData(VrstaPostupka.OtkazNajmaZakupa,     12_000,    12_000)]
    // čl. 20 st. 1 — poništenje pravorijeka arbitraže, minimum 6.634,14 €
    [InlineData(VrstaPostupka.PonistenjePravorijeka, 1_000, 6_634.14)]
    [InlineData(VrstaPostupka.PonistenjePravorijeka, 90_000,   90_000)]
    public void DonjaGranica_DizeSamoOnoStoJeIspod(string vrsta, decimal uneseni, decimal ocekivano)
    {
        var r = Izracunaj(uneseni, vrsta);

        Assert.Equal(ocekivano, r.VpsZakonski);
    }

    // ── Gornje granice ──

    [Theory]
    // čl. 21 st. 5 — red prvenstva tražbina u ovrsi, najviše 1.327,22 €
    [InlineData(VrstaPostupka.RedPrvenstvaOvrha,      40_000, 1_327.22)]
    [InlineData(VrstaPostupka.RedPrvenstvaOvrha,         800,       800)]
    // čl. 21 st. 6 — prethodna pravna pitanja, najviše 1.327,22 €
    [InlineData(VrstaPostupka.PrethodnoPravnoPitanje, 40_000, 1_327.22)]
    [InlineData(VrstaPostupka.PrethodnoPravnoPitanje,     50,        50)]
    public void GornjaGranica_SpustaSamoOnoStoJeIznad(string vrsta, decimal uneseni, decimal ocekivano)
    {
        var r = Izracunaj(uneseni, vrsta);

        Assert.Equal(ocekivano, r.VpsZakonski);
    }

    // ── Čl. 15 — uzdržavanje ──

    /// <summary>„Zbroj davanja za tri mjeseca" — 400 € mjesečno daje 1.200 €.</summary>
    [Fact]
    public void Uzdrzavanje_TriMjesecnaDavanja()
    {
        var r = Izracunaj(0m, VrstaPostupka.Uzdrzavanje, z => z.UzdrzavanjeMjesecno = 400m);

        Assert.Equal(1_200m, r.VpsZakonski);
    }

    /// <summary>Bez mjesečnog iznosa nema se što izvesti — greška, ne tiha nula.</summary>
    [Fact]
    public void Uzdrzavanje_BezMjesecnogIznosa_Greska()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(5_000m, VrstaPostupka.Uzdrzavanje));

        Assert.Contains("tri mjeseca", greska.Message);
    }

    // ── Čl. 14, 17, 18 — Zakon kaže ŠTO se upisuje, ne koliko ──

    [Theory]
    [InlineData(VrstaPostupka.Opci)]
    [InlineData(VrstaPostupka.DiobaImovine)]
    [InlineData(VrstaPostupka.Nasljedivanje)]
    public void SlobodnaVrijednost_OstajeUnesena(string vrsta)
    {
        var r = Izracunaj(37_500m, vrsta);

        Assert.Equal(37_500m, r.VpsZakonski);
        Assert.False(r.VpsPrilagodjen);
    }

    [Fact]
    public void NepoznataVrstaPostupka_Greska()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(1_000m, "izmisljena_vrsta"));

        Assert.Contains("Nepoznata vrsta postupka", greska.Message);
    }

    // ── Čl. 16 — više zahtjeva ──

    private static TroskovnikResponse SaZahtjevima(
        string nacin, params (decimal Iznos, bool Primarni)[] zahtjevi)
        => Izracunaj(999_999m, VrstaPostupka.Opci, z =>
        {
            z.ZahtjeviNacin = nacin;
            z.Zahtjevi = [.. zahtjevi.Select(s => new ZahtjevStavka
            {
                Iznos = s.Iznos,
                Primarni = s.Primarni
            })];
        });

    /// <summary>„Više zahtjeva protiv istog tuženika" → zbroj.</summary>
    [Fact]
    public void Cl16_IstiTuzenik_Zbroj()
    {
        var r = SaZahtjevima(ZahtjeviNacin.Zbroj, (1_000m, false), (2_000m, false), (3_000m, false));

        Assert.Equal(6_000m, r.VpsZakonski);
    }

    /// <summary>Povezani (eventualni) zahtjevi → onaj čija je vrijednost veća.</summary>
    [Fact]
    public void Cl16_EventualniZahtjevi_Veci()
    {
        var r = SaZahtjevima(ZahtjeviNacin.Eventualni, (1_000m, false), (2_000m, false), (3_000m, false));

        Assert.Equal(3_000m, r.VpsZakonski);
    }

    /// <summary>
    /// Suparničarstvo: uzima se vrijednost SVAKOG POJEDINOG zahtjeva, pa se ne zbraja.
    /// Troškovnik pokriva označeni zahtjev, a ostali izlaze kao zasebne pristojbe.
    /// </summary>
    [Fact]
    public void Cl16_Suparnicarstvo_NeZbrajaNegoUzimaOznaceni()
    {
        var r = SaZahtjevima(ZahtjeviNacin.Zasebno, (1_000m, false), (2_000m, true), (3_000m, false));

        Assert.Equal(2_000m, r.VpsZakonski);
        Assert.Equal([1_000m, 3_000m], r.ZahtjeviInformativno.Select(z => z.Iznos));
    }

    [Theory]
    [InlineData(false, false)]  // nijedan označen
    [InlineData(true, true)]    // dva označena
    public void Cl16_Suparnicarstvo_BezTocnoJednogOznacenog_Greska(bool prvi, bool drugi)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => SaZahtjevima(ZahtjeviNacin.Zasebno, (1_000m, prvi), (2_000m, drugi)));

        Assert.Contains("točno jedan", greska.Message);
    }

    [Fact]
    public void Cl16_BezOdabranogNacina_Greska()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(1_000m, VrstaPostupka.Opci, z =>
                z.Zahtjevi = [new ZahtjevStavka { Iznos = 500m }]));

        Assert.Contains("Čl. 16", greska.Message);
    }

    // ── Čl. 24 — pravni lijekovi ──

    /// <summary>Cijela odluka → vrijednost iz prvostupanjskog postupka, bez promjene.</summary>
    [Fact]
    public void Cl24_CijelaOdluka_ZadrzavaPrvostupanjskiVps()
    {
        var r = Izracunaj(20_000m, VrstaPostupka.Opci, z => z.PravniLijek = PravniLijek.CijelaOdluka);

        Assert.Equal(20_000m, r.VpsZakonski);
    }

    [Theory]
    [InlineData(PravniLijek.PobijaniDio)]
    [InlineData(PravniLijek.SamoTroskovi)]
    public void Cl24_DioIliTroskovi_UzimaUpisaniIznos(string lijek)
    {
        var r = Izracunaj(20_000m, VrstaPostupka.Opci, z =>
        {
            z.PravniLijek = lijek;
            z.PravniLijekIznos = 800m;
        });

        Assert.Equal(800m, r.VpsZakonski);
        Assert.True(r.VpsPrilagodjen);
    }

    [Theory]
    [InlineData(PravniLijek.PobijaniDio)]
    [InlineData(PravniLijek.SamoTroskovi)]
    public void Cl24_BezIznosa_Greska(string lijek)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(20_000m, VrstaPostupka.Opci, z => z.PravniLijek = lijek));

        Assert.Contains("čl. 24", greska.Message);
    }

    /// <summary>
    /// Redoslijed je čl. 16 → pravilo vrste postupka → čl. 24. Kad se pobija dio odluke,
    /// mjerodavan je taj dio, a ne zbroj zahtjeva ni zakonska granica iz čl. 21.
    /// </summary>
    [Fact]
    public void Redoslijed_Cl24_NadglasavaCl16IPraviloVrste()
    {
        var r = Izracunaj(0m, VrstaPostupka.RedPrvenstvaOvrha, z =>
        {
            z.ZahtjeviNacin = ZahtjeviNacin.Zbroj;
            z.Zahtjevi = [new ZahtjevStavka { Iznos = 40_000m }, new ZahtjevStavka { Iznos = 10_000m }];
            z.PravniLijek = PravniLijek.PobijaniDio;
            z.PravniLijekIznos = 5_000m;
        });

        Assert.Equal(5_000m, r.VpsZakonski);
    }

    /// <summary>
    /// Bez čl. 24 isti bi zahtjev prošao kroz čl. 16 (zbroj 50.000 €) pa kroz gornju
    /// granicu iz čl. 21 st. 5 (1.327,22 €). Ovo drži taj redoslijed na mjestu.
    /// </summary>
    [Fact]
    public void Redoslijed_Cl16_PaGranicaIzCl21()
    {
        var r = Izracunaj(0m, VrstaPostupka.RedPrvenstvaOvrha, z =>
        {
            z.ZahtjeviNacin = ZahtjeviNacin.Zbroj;
            z.Zahtjevi = [new ZahtjevStavka { Iznos = 40_000m }, new ZahtjevStavka { Iznos = 10_000m }];
        });

        Assert.Equal(1_327.22m, r.VpsZakonski);
    }

    // ── Doseg: pristojba da, nagrada samo na izričit zahtjev ──

    /// <summary>
    /// Zakonski VPS obavezno određuje sudsku pristojbu. Smetanje posjeda s upisanih
    /// 50.000 €: pristojba ide po 663,61 € → razred do 796,34 € → 26,54 €.
    /// </summary>
    [Fact]
    public void ZakonskiVps_ObavezanZaSudskuPristojbu()
    {
        var r = Izracunaj(50_000m, VrstaPostupka.SmetanjePosjeda);

        Assert.Equal(26.54m, r.SudskaPristojba);
    }

    /// <summary>
    /// Čl. 14–26 pisani su za pristojbe, ne za Tarifu, pa nagrada po Tbr. 7. toč. 1
    /// ostaje na unesenoj vrijednosti dok odvjetnik izričito ne odabere prijenos.
    /// Tbr. 7. toč. 1: 50.000 € → 500 bodova (razred do 66.361 €); 663,61 € → 50 bodova
    /// (razred do 664,00 €).
    /// </summary>
    [Fact]
    public void ZakonskiVps_NeDiraNagraduBezIzricitogOdabira()
    {
        var r = Izracunaj(50_000m, VrstaPostupka.SmetanjePosjeda);

        Assert.Equal(50_000m, r.VrijednostSpora);
        Assert.Equal(500m, r.OsnovicaBodovi);
    }

    [Fact]
    public void ZakonskiVps_PrimijenjenNaNagraduKadSeOdabere()
    {
        var r = Izracunaj(50_000m, VrstaPostupka.SmetanjePosjeda, z => z.VpsPrimijeniNaNagradu = true);

        Assert.Equal(663.61m, r.VrijednostSpora);
        Assert.Equal(50m, r.OsnovicaBodovi);
    }

    /// <summary>
    /// VPS je institut parničnog postupka. U kaznenom troškovniku vrsta postupka ne
    /// smije ništa tražiti ni mijenjati — sve ide po zapriječenoj kazni (Tbr. 4. toč. 1).
    /// </summary>
    [Fact]
    public void KazneniPostupak_VpsPravilaSePreskacu()
    {
        var r = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            VrstaPostupka = VrstaPostupka.Uzdrzavanje,   // bez mjesečnog iznosa: inače greška
            ZaprijecenaKazna = "do_5",
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        });

        Assert.Equal(0m, r.VpsZakonski);
        Assert.Equal(0m, r.SudskaPristojba);
        Assert.True(r.IsSamoKazneni);
    }
}
