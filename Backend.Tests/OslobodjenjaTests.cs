using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Oslobođenja od plaćanja sudskih pristojbi — Zakon o sudskim pristojbama
/// (NN 118/18, 51/23), čl. 10–13.
///
/// Prije ovoga aplikacija je imala goli prekidač koji je nulio iznos: bez osnove
/// oslobođenja (čl. 11 ima 21 točku), bez razmjernog dijela kad je oslobođen samo
/// dio suparničara (čl. 12) i bez pristojbe na teret protivne stranke (čl. 13).
/// </summary>
public class OslobodjenjaTests
{
    private static TroskovnikResponse Izracunaj(decimal vps, Action<TroskovnikRequest> dopuni)
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        };

        dopuni(zahtjev);

        return new TroskovnikService().Izracunaj(zahtjev);
    }

    // RUČNI IZRAČUN — NN 50/2026, Tar. br. 1 st. 1, progresija iznad 2.000,00 €:
    //   20.000 € → 140,00 + 1 % × 18.000,00 = 320,00 €
    //   15.000 € → 140,00 + 1 % × 13.000,00 = 270,00 €
    //   10.000 € → 140,00 + 1 % ×  8.000,00 = 220,00 €
    //    5.000 € → 140,00 + 1 % ×  3.000,00 = 170,00 €
    //
    // Po staroj Uredbi (NN 37/2023) isti su VPS-ovi davali 246,45 / 196,45 / 146,45 /
    // 96,45 €. Oslobođenja se nisu promijenila — promijenila se tarifa ispod njih.

    private const decimal PristojbaNa20000 = 320.00m;
    private const decimal PristojbaNa10000 = 220.00m;

    // ── Čl. 11 — oslobođenje po sili zakona ──

    [Fact]
    public void PotpunoOslobodjenje_NulaBezObziraNaVps()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.IsOslobodjenPristojbi = true;
            z.OslobodjenjeKategorija = OslobodjenjeKategorija.Radnici;
        });

        Assert.Equal(0m, r.SudskaPristojba);
    }

    /// <summary>
    /// Osnova oslobođenja mora se vidjeti u odgovoru: „0,00 €" bez nje izgleda isto kao
    /// pristojba koju Uredba uopće ne propisuje.
    /// </summary>
    [Fact]
    public void Kategorija_IzlaziUOdgovoruSTekstomTocke()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.IsOslobodjenPristojbi = true;
            z.OslobodjenjeKategorija = OslobodjenjeKategorija.MirovinskoZdravstveno;
        });

        Assert.Equal(OslobodjenjeKategorija.MirovinskoZdravstveno, r.OslobodjenjeKategorija);
        Assert.Contains("čl. 11. t. 16", r.OslobodjenjeNaziv);
    }

    /// <summary>Čl. 11 ima 21 točku; uz njih stoji i oslobođenje na zahtjev po čl. 10.</summary>
    [Fact]
    public void Katalog_ImaSveTockeCl11IZahtjevPoCl10()
    {
        Assert.Equal(22, OslobodjenjeKategorija.Sve.Length);
        Assert.Contains(OslobodjenjeKategorija.ZahtjevCl10, OslobodjenjeKategorija.Sve);
        Assert.All(OslobodjenjeKategorija.Sve,
            k => Assert.False(string.IsNullOrWhiteSpace(OslobodjenjeKategorija.Naziv(k))));
    }

    [Fact]
    public void NepoznataKategorija_Greska()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(20_000m, z => z.OslobodjenjeKategorija = "izmisljena"));

        Assert.Contains("Nepoznata osnova oslobođenja", greska.Message);
    }

    // ── Čl. 12 — dio suparničara oslobođen ──

    /// <summary>
    /// „Neoslobođena plaća samo prema svom dijelu vrijednosti predmeta spora."
    ///
    /// Umanjuje se OSNOVICA, ne gotov iznos. Dvoje od četvero oslobođeno → pristojba se
    /// računa na 10.000 €, što daje 220,00 € — a ne polovicu od 320,00 € (160,00 €).
    /// Tarifa je progresivna, pa polovica VPS-a nije polovica pristojbe.
    /// </summary>
    [Fact]
    public void Cl12_UmanjujeOsnovicu_NeGotovIznos()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.BrojZastupanihOsoba = 4;
            z.BrojOslobodjenihOsoba = 2;
            z.OslobodjenjeKategorija = OslobodjenjeKategorija.Radnici;
        });

        Assert.Equal(10_000m, r.PristojbaVpsUdio);
        Assert.Equal(PristojbaNa10000, r.SudskaPristojba);
        Assert.NotEqual(PristojbaNa20000 / 2m, r.SudskaPristojba);
    }

    /// <summary>Čl. 12 st. 2 — bez upisanog udjela vrijednost se dijeli brojem osoba.</summary>
    [Theory]
    [InlineData(4, 1, 15_000, 270.00)]
    [InlineData(4, 2, 10_000, 220.00)]
    [InlineData(4, 3,  5_000, 170.00)]
    public void Cl12_BezUpisanogUdjela_DijeliBrojemOsoba(
        int ukupno, int oslobodjeni, decimal ocekivaniUdio, decimal ocekivanaPristojba)
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.BrojZastupanihOsoba = ukupno;
            z.BrojOslobodjenihOsoba = oslobodjeni;
        });

        Assert.Equal(ocekivaniUdio, r.PristojbaVpsUdio);
        Assert.Equal(ocekivanaPristojba, r.SudskaPristojba);
    }

    /// <summary>Izričito upisan udio ima prednost pred diobom po broju osoba.</summary>
    [Fact]
    public void Cl12_UpisaniUdio_ImaPrednostPredDiobom()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.BrojZastupanihOsoba = 4;
            z.BrojOslobodjenihOsoba = 1;   // dioba bi dala 15.000 €
            z.UdioUVps = 5_000m;
        });

        Assert.Equal(5_000m, r.PristojbaVpsUdio);
        Assert.Equal(170.00m, r.SudskaPristojba);
    }

    /// <summary>Kad su oslobođeni svi, rezultat je isti kao kod golog prekidača.</summary>
    [Fact]
    public void Cl12_SviOslobodjeni_Nula()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.BrojZastupanihOsoba = 3;
            z.BrojOslobodjenihOsoba = 3;
        });

        Assert.Equal(0m, r.SudskaPristojba);
        Assert.Equal(0m, r.PristojbaVpsUdio);
    }

    [Fact]
    public void Cl12_ViseOslobodjenihNegoZastupanih_Greska()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(20_000m, z =>
            {
                z.BrojZastupanihOsoba = 2;
                z.BrojOslobodjenihOsoba = 3;
            }));

        Assert.Contains("Čl. 12", greska.Message);
    }

    // ── Čl. 13 — pristojba na teret protivne stranke ──

    /// <summary>
    /// „Ako oslobođena stranka uspije, pristojbe koje bi platila plaća protivna stranka,
    /// u omjeru uspjeha." Potpuno oslobođena stranka s 60 % uspjeha: 320,00 × 0,6 = 192,00 €.
    /// </summary>
    [Fact]
    public void Cl13_PunoOslobodjenje_RazmjernoUspjehu()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.IsOslobodjenPristojbi = true;
            z.PostotakUspjeha = 60m;
        });

        Assert.Equal(192.00m, r.PristojbaNaTeretProtustranke);
    }

    /// <summary>
    /// Obveza nastaje po pravomoćnosti i tereti drugu stranu, pa iznos ne smije ući u
    /// sveukupni trošak zastupane stranke.
    /// </summary>
    [Fact]
    public void Cl13_NeUlaziUSveukupniTrosak()
    {
        var bez = Izracunaj(20_000m, z => z.IsOslobodjenPristojbi = true);
        var sa = Izracunaj(20_000m, z =>
        {
            z.IsOslobodjenPristojbi = true;
            z.PostotakUspjeha = 60m;
        });

        Assert.Equal(bez.SveukupniTrosak, sa.SveukupniTrosak);
        Assert.True(sa.PristojbaNaTeretProtustranke > 0m);
    }

    /// <summary>
    /// Kod djelomičnog oslobođenja čl. 13 se veže samo uz dio koji otpada na oslobođene:
    /// pristojba na njihovih 10.000 € (220,00 €) × 60 % = 132,00 €.
    /// </summary>
    [Fact]
    public void Cl13_DjelomicnoOslobodjenje_SamoDioOslobodjenih()
    {
        var r = Izracunaj(20_000m, z =>
        {
            z.BrojZastupanihOsoba = 4;
            z.BrojOslobodjenihOsoba = 2;
            z.PostotakUspjeha = 60m;
        });

        Assert.Equal(132.00m, r.PristojbaNaTeretProtustranke);
    }

    /// <summary>Bez ijednog oslobođenja čl. 13 nema što premjestiti.</summary>
    [Fact]
    public void Cl13_BezOslobodjenja_Nula()
    {
        var r = Izracunaj(20_000m, z => z.PostotakUspjeha = 100m);

        Assert.Equal(0m, r.PristojbaNaTeretProtustranke);
        Assert.Equal(PristojbaNa20000, r.SudskaPristojba);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Cl13_OmjerIzvanRaspona_Greska(decimal postotak)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(20_000m, z =>
            {
                z.IsOslobodjenPristojbi = true;
                z.PostotakUspjeha = postotak;
            }));

        Assert.Contains("čl. 13", greska.Message);
    }

    // ── Regresija: bez ijednog novog polja ništa se ne mijenja ──

    [Fact]
    public void BezOslobodjenja_PristojbaKaoIPrije()
    {
        var r = Izracunaj(20_000m, _ => { });

        Assert.Equal(PristojbaNa20000, r.SudskaPristojba);
        Assert.Equal(20_000m, r.PristojbaVpsUdio);
        Assert.Equal(0m, r.PristojbaNaTeretProtustranke);
    }
}
