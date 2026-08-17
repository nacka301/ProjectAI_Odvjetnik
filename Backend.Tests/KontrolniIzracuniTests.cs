using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Kontrolni izračuni iz revizije, na SPOJENOM kodu.
///
/// Svaka od pet grana provjeravala je samo svoj dio. Ovi testovi provjeravaju ono što
/// nijedna grana nije mogla: iznos koji nastaje kad se pristojbe, VPS, oslobođenja,
/// PDV i struktura ispisa nađu u istom troškovniku.
/// </summary>
public class KontrolniIzracuniTests
{
    private static TroskovnikResponse Izracunaj(Action<TroskovnikRequest> podesi)
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        };

        podesi(zahtjev);
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    /// <summary>A — tužba, VPS 50.000: 140,00 + 1 % × 48.000,00 = 620,00 €.</summary>
    [Fact]
    public void A_Tuzba50000_Daje620()
        => Assert.Equal(620.00m, Izracunaj(_ => { }).SudskaPristojba);

    /// <summary>B — VPS 200.000 dao bi 2.120,00, ali Tar. br. 1 st. 1 kapa na 1.500,00.</summary>
    [Fact]
    public void B_Maksimum_Kapa1500()
        => Assert.Equal(1_500.00m, Izracunaj(z => z.VrijednostSpora = 200_000m).SudskaPristojba);

    /// <summary>C — upravni spor je Poglavlje VIII, fiksnih 110,00 bez obzira na VPS.</summary>
    [Fact]
    public void C_UpravniSpor_Fiksnih110()
    {
        var r = Izracunaj(z =>
            z.Stavke = [new TroskovnikItemRequest { ItemId = "usp_tuzba_proc", Kolicina = 1 }]);

        Assert.Equal(110.00m, r.SudskaPristojba);
    }

    /// <summary>D — žalba protiv presude, Tar. br. 3 st. 1: 620,00 × 2 = 1.240,00.</summary>
    [Fact]
    public void D_ZalbaProtivPresude_Daje1240()
    {
        var r = Izracunaj(z =>
            z.Stavke = [new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }]);

        Assert.Equal(1_240.00m, r.SudskaPristojba);
    }

    /// <summary>E — žalba protiv rješenja, Tar. br. 3 st. 2: puna pristojba, 620,00.</summary>
    [Fact]
    public void E_ZalbaProtivRjesenja_Daje620()
    {
        var r = Izracunaj(z =>
            z.Stavke = [new TroskovnikItemRequest { ItemId = "par_zalba_rjesenje", Kolicina = 1 }]);

        Assert.Equal(620.00m, r.SudskaPristojba);
    }

    /// <summary>
    /// G — PDV, odvjetnik u sustavu. Nagrada 1.000,00 + putni 100,00 → osnovica 1.100,00;
    /// PDV 275,00; sudska pristojba 620,00 IZVAN osnovice. Ukupno 1.995,00.
    /// </summary>
    [Fact]
    public void G_Pdv_PutniUlazeUOsnovicu_PristojbaNe()
    {
        var r = Izracunaj(z =>
        {
            z.IsPdvUkljucen = true;
            z.NaknadaTroskovaStavke =
                [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Prijevoz, Iznos = 100m }];
        });

        Assert.Equal(1_100.00m, r.UkupnoEurBezPdv);
        Assert.Equal(275.00m, r.Pdv);
        Assert.Equal(620.00m, r.SudskaPristojba);
        Assert.Equal(1_995.00m, r.SveukupniTrosak);
    }

    /// <summary>H — prekidač isključen: PDV je 0 i ukupno je 1.720,00.</summary>
    [Fact]
    public void H_BezPdva_Ukupno1720()
    {
        var r = Izracunaj(z =>
        {
            z.IsPdvUkljucen = false;
            z.NaknadaTroskovaStavke =
                [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Prijevoz, Iznos = 100m }];
        });

        Assert.Equal(0m, r.Pdv);
        Assert.Equal(1_100.00m, r.UkupnoEurBezPdv);
        Assert.Equal(1_720.00m, r.SveukupniTrosak);
    }

    /// <summary>
    /// I — opunomoćenik djeteta žrtve: 70 % nagrade po Tarifi, ne 35 %.
    /// Provjerava se omjer prema punoj nagradi za istu radnju, ne apsolutni iznos.
    /// </summary>
    [Fact]
    public void I_OpunomocenikDjeteta_Sedamdeset_NeTridesetPet()
    {
        static decimal Nagrada(Action<TroskovnikRequest> podesi) => Izracunaj(z =>
        {
            z.Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }];
            z.ZaprijecenaKazna = "do_5";
            podesi(z);
        }).UkupnoEurBezPdv;

        var puna = Nagrada(_ => { });
        var dijete = Nagrada(z => z.IsZastupnikDjeteta = true);

        Assert.Equal(puna * 0.70m, dijete);
        Assert.NotEqual(puna * 0.35m, dijete);
    }

    /// <summary>
    /// J — obje uloge istodobno. Zahtjev s obje zastavice odbija se na ulazu
    /// (<c>TroskovnikControllerKarakterizacijaTests.Izracunaj_ObranaIZastupanjeDjeteta_Vraca400</c>);
    /// ovdje se provjerava druga polovica te obrane — da sam izračun, i kad mu obje
    /// zastavice ipak stignu, NIKAD ne pomnoži stope. Prije popravka bilo je
    /// 0,50 × 0,70 = 35 % pune tarife.
    /// </summary>
    [Fact]
    public void J_ObjeUloge_StopeSeNeMnoze()
    {
        static decimal Nagrada(Action<TroskovnikRequest> podesi) => Izracunaj(z =>
        {
            z.Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }];
            z.ZaprijecenaKazna = "do_5";
            podesi(z);
        }).UkupnoEurBezPdv;

        var puna = Nagrada(_ => { });
        var obje = Nagrada(z =>
        {
            z.IsZastupnikDjeteta = true;
            z.IsPoSluzbenojDuznosti = true;
        });

        Assert.Equal(puna * 0.70m, obje);
        Assert.NotEqual(puna * 0.35m, obje);
    }

    /// <summary>
    /// K — propisani VPS. Smetanje posjeda i razvod su zakonskih 663,61 € bez obzira na
    /// uneseno; nenovčani zahtjev ne može ispod 1.327,22 €; neutvrdiva vrijednost je
    /// fiksnih 1.327,22 €.
    /// </summary>
    [Theory]
    [InlineData(VrstaPostupka.SmetanjePosjeda, 50_000, 663.61)]
    [InlineData(VrstaPostupka.Brak, 50_000, 663.61)]
    [InlineData(VrstaPostupka.NenovcaniZahtjev, 500, 1_327.22)]
    [InlineData(VrstaPostupka.NeutvrdivaVrijednost, 500, 1_327.22)]
    public void K_PropisaniVps(string vrsta, decimal uneseno, decimal ocekivano)
    {
        var r = Izracunaj(z =>
        {
            z.VrijednostSpora = uneseno;
            z.VrstaPostupka = vrsta;
        });

        Assert.Equal(ocekivano, r.VpsZakonski);
    }

    /// <summary>
    /// N — redoslijed odjeljaka na ispisu: radnje → zbroj nagrade → PDV → pristojbe →
    /// predujmovi → napomena.
    /// </summary>
    [Fact]
    public void N_RedoslijedIspisa()
    {
        var odgovor = Izracunaj(z =>
        {
            z.IsPdvUkljucen = true;
            z.Napomena = TarifaConstants.ZadanaNapomena;
        });

        var html = new TroskovnikDocumentService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TroskovnikDocumentService>.Instance)
            .RenderHtml(new TroskovnikReadRecord { RawJsonOutput = odgovor });

        var redom = new[]
        {
            "I. Odvjetničke radnje", "Zbroj nagrade", "PDV (25%)",
            "II. Sudske pristojbe", "III. Predujmovi", "Napomena"
        };

        var zadnji = -1;
        foreach (var naslov in redom)
        {
            var pozicija = html.IndexOf(naslov, StringComparison.Ordinal);
            Assert.True(pozicija > zadnji, $"Odjeljak \"{naslov}\" nije na svom mjestu.");
            zadnji = pozicija;
        }

        Assert.Contains("članku 29. Zakona o obveznim odnosima", html);
    }

    /// <summary>
    /// O — čl. 2: odgovor na tužbu podnesen prije 1.6.2026. ide po staroj Uredbi
    /// (273,23 €), a od tog dana po novoj (310,00 €).
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 12, 273.23)]
    [InlineData(2026, 6, 1, 310.00)]
    public void O_PrijelazneOdredbe(int g, int m, int d, decimal ocekivano)
    {
        var r = Izracunaj(z =>
        {
            z.Stavke = [new TroskovnikItemRequest { ItemId = "par_odgovor_tuzba", Kolicina = 1 }];
            z.DatumPodnosenjaOdgovoraNaTuzbu = new DateOnly(g, m, d);
        });

        Assert.Equal(ocekivano, r.SudskaPristojba);
    }
}
