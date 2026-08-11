using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Datum uz radnju (gggg-mm-dd) — prijenos kroz izračun, provjera valjanosti i
/// razdoblje koje troškovnik pokriva.
///
/// Troškovnik koji se predaje sudu mora uz svaku radnju imati datum kada je poduzeta.
/// Datum ne ulazi ni u jedan izračun: ne mijenja bodove, iznose ni sudsku pristojbu.
/// </summary>
public class TroskovnikDatumRadnjeTests
{
    private const decimal Vps = 50_000m;

    private static TroskovnikResponse Izracunaj(params TroskovnikItemRequest[] stavke)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = Vps,
            Stavke = [.. stavke]
        });

    private static TroskovnikItemRequest Stavka(string? datum, string itemId = "par_rociste_glavno", int kolicina = 1)
        => new() { ItemId = itemId, Kolicina = kolicina, Datum = datum };

    // ── Prijenos ──────────────────────────────────────────────────

    [Fact]
    public void Datum_StizeDoRetkaTroskovnika()
    {
        var rezultat = Izracunaj(Stavka("2025-03-12"), Stavka("2025-04-04"));

        Assert.Equal("2025-03-12", rezultat.Stavke[0].Datum);
        Assert.Equal("2025-04-04", rezultat.Stavke[1].Datum);
    }

    [Fact]
    public void BezDatuma_RedakGaNema_IIzracunProlazi()
    {
        var rezultat = Izracunaj(Stavka(datum: null));

        Assert.Null(rezultat.Stavke[0].Datum);
        Assert.Equal(500m, rezultat.UkupnoBodova);
    }

    /// <summary>Prazan niz i sami razmaci nisu datum — na redak ide null, ne "   ".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PrazanDatum_NaRetkuJeNull(string datum)
    {
        Assert.Null(Izracunaj(Stavka(datum)).Stavke[0].Datum);
    }

    // ── Provjera valjanosti ───────────────────────────────────────

    /// <summary>
    /// Datum koji se ne da pročitati ne smije tiho proći — završio bi na troškovniku
    /// koji se predaje sudu.
    /// </summary>
    [Theory]
    [InlineData("12.03.2025.")]   // hrvatski oblik ne ide u prijenos
    [InlineData("2025-13-01")]    // trinaesti mjesec
    [InlineData("2025-02-30")]    // veljača nema 30 dana
    [InlineData("2025-3-4")]      // bez vodećih nula
    [InlineData("jučer")]
    public void NeispravanDatum_BacaValidacijskuIznimku(string datum)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(Stavka(datum)));

        Assert.Contains("neispravan datum", greska.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(datum, greska.Message);
        Assert.Contains("gggg-mm-dd", greska.Message);
    }

    /// <summary>Poruka imenuje stavku, da odvjetnik zna koji redak popraviti.</summary>
    [Fact]
    public void PorukaOGresci_ImenujeStavku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(Stavka("2025-03-12"), Stavka("nije datum", "par_tuzba")));

        Assert.Contains("Tužba", greska.Message);
    }

    /// <summary>
    /// Datum u budućnosti je dopušten — troškovnik se legitimno slaže unaprijed za već
    /// zakazano ročište.
    /// </summary>
    [Fact]
    public void DatumUBuducnosti_Prolazi()
    {
        var sutra = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString(TroskovnikService.OblikDatuma);

        Assert.Equal(sutra, Izracunaj(Stavka(sutra)).Stavke[0].Datum);
    }

    [Fact]
    public void PrijestupnaGodina_29Veljace_Prolazi()
    {
        Assert.Equal("2024-02-29", Izracunaj(Stavka("2024-02-29")).Stavke[0].Datum);
    }

    // ── Razdoblje koje troškovnik pokriva ─────────────────────────

    /// <summary>
    /// Uzima se najmanji i najveći datum, a ne prvi i zadnji redak — korisnik zadržava
    /// svoj redoslijed, pa zadnji redak ne mora biti najkasniji.
    /// </summary>
    [Fact]
    public void Raspon_JeNajmanjiINajveciDatum_NePrviIZadnjiRedak()
    {
        var rezultat = Izracunaj(
            Stavka("2025-05-19"),
            Stavka("2025-03-12"),
            Stavka("2025-04-04"));

        Assert.Equal("2025-03-12", rezultat.DatumOd);
        Assert.Equal("2025-05-19", rezultat.DatumDo);
    }

    [Fact]
    public void Raspon_JednaRadnja_OdIDoSuIsti()
    {
        var rezultat = Izracunaj(Stavka("2025-03-12"));

        Assert.Equal("2025-03-12", rezultat.DatumOd);
        Assert.Equal("2025-03-12", rezultat.DatumDo);
    }

    [Fact]
    public void Raspon_BezIjednogDatuma_JeNull()
    {
        var rezultat = Izracunaj(Stavka(datum: null), Stavka(datum: null));

        Assert.Null(rezultat.DatumOd);
        Assert.Null(rezultat.DatumDo);
    }

    /// <summary>Radnje bez datuma ne ruše raspon — računa se iz onih koje ga imaju.</summary>
    [Fact]
    public void Raspon_DijelomPopunjenihDatuma_KoristiSamoUnesene()
    {
        var rezultat = Izracunaj(
            Stavka(datum: null),
            Stavka("2025-04-04"),
            Stavka(datum: null),
            Stavka("2025-03-12"));

        Assert.Equal("2025-03-12", rezultat.DatumOd);
        Assert.Equal("2025-04-04", rezultat.DatumDo);
    }

    /// <summary>Raspon prelazi godinu bez ispadanja iz redoslijeda (usporedba je datumska, ne tekstualna).</summary>
    [Fact]
    public void Raspon_PrekoGranicaGodine()
    {
        var rezultat = Izracunaj(Stavka("2026-01-08"), Stavka("2025-12-31"));

        Assert.Equal("2025-12-31", rezultat.DatumOd);
        Assert.Equal("2026-01-08", rezultat.DatumDo);
    }

    // ── Datum ne dira izračun ─────────────────────────────────────

    /// <summary>
    /// Cijela je izmjena aditivna: isti troškovnik s datumima i bez njih mora dati
    /// isti iznos, bodove i sudsku pristojbu.
    /// </summary>
    [Fact]
    public void Datum_NeMijenjaNijedanIznos()
    {
        var sDatumima = Izracunaj(
            Stavka("2025-03-12", "par_tuzba"),
            Stavka("2025-04-04"),
            Stavka("2025-05-19", "par_tuzba_smetanje"));

        var bezDatuma = Izracunaj(
            Stavka(null, "par_tuzba"),
            Stavka(null),
            Stavka(null, "par_tuzba_smetanje"));

        Assert.Equal(bezDatuma.UkupnoBodova, sDatumima.UkupnoBodova);
        Assert.Equal(bezDatuma.UkupnoEurBezPdv, sDatumima.UkupnoEurBezPdv);
        Assert.Equal(bezDatuma.SudskaPristojba, sDatumima.SudskaPristojba);
        Assert.Equal(bezDatuma.SveukupniTrosak, sDatumima.SveukupniTrosak);
    }

    /// <summary>
    /// Jednokratna nagrada i dalje se računa jednom za sve retke, a svaka radnja
    /// zadržava svoj datum — to je cijela svrha ove značajke.
    /// </summary>
    [Fact]
    public void JednokratnaNagrada_SvakiRedakZadrzavaSvojDatum()
    {
        var rezultat = Izracunaj(
            Stavka("2025-03-12", "par_tuzba_smetanje"),
            Stavka("2025-04-04", "par_tuzba_smetanje"),
            Stavka("2025-05-19", "par_tuzba_smetanje"));

        Assert.Equal(250m, rezultat.UkupnoBodova);
        Assert.Equal(["2025-03-12", "2025-04-04", "2025-05-19"], rezultat.Stavke.Select(r => r.Datum));
        Assert.Equal("2025-03-12", rezultat.DatumOd);
        Assert.Equal("2025-05-19", rezultat.DatumDo);
    }
}
