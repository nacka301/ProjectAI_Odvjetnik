using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// „Priznati bodovi" po retku — paralelni prikaz „Po Tarifi" vs „Priznato (rješenje
/// suda o troškovima)". Tetka je iz presuda pokazala da sud ne dosuđuje uvijek pune
/// tarifne bodove; odvjetnik treba moći zabilježiti iznvan-tarifnu brojku.
///
/// Ključno: priznato je čista agregacija povrh tarifnog izračuna. Kad nijedan redak
/// nema unos, `Priznato` je null i nijedan tarifni iznos se ne mijenja.
///
/// VPS 50.000 € → par_tuzba 500 bod., par_rociste_glavno 500 bod., par_zalba_presuda 625 bod.
/// bod = 2 €.
/// </summary>
public class TroskovnikPriznatiBodoviTests
{
    private static TroskovnikRequest Zahtjev(params TroskovnikItemRequest[] stavke)
        => new()
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke = [.. stavke]
        };

    private static TroskovnikItemRequest Stavka(
        string id, int kolicina = 1, decimal? priznati = null,
        decimal? povisenje = null, decimal? snizenje = null)
        => new()
        {
            ItemId = id,
            Kolicina = kolicina,
            PriznatiBodovi = priznati,
            Tbr41PovisenjePostotak = povisenje,
            Tbr41SnizenjePostotak = snizenje
        };

    private static TroskovnikResponse Izracunaj(TroskovnikRequest zahtjev)
        => new TroskovnikService().Izracunaj(zahtjev);

    // ── Bez unosa — ništa se ne mijenja ──

    [Fact]
    public void BezIjednogPriznatog_PriznatoJeNull_TarifaNepromijenjena()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba"),
            Stavka("par_rociste_glavno")));

        Assert.Null(rezultat.Priznato);
        Assert.Equal(1_000m, rezultat.UkupnoBodova);
        Assert.Equal(2_000.00m, rezultat.UkupnoEurBezPdv);
        // Redak i dalje nosi efektivni doprinos (puni tarifni), za slučaj da se drugdje čita.
        Assert.All(rezultat.Stavke, s => Assert.Null(s.PriznatiBodovi));
        Assert.Equal(500m, rezultat.Stavke.Single(s => s.ItemId == "par_tuzba").PriznatoUkupnoBodova);
    }

    // ── Jedan redak prikovan niže od Tarife ──

    [Fact]
    public void JedanRedakPrikovan_PriznatoZbrajaPinPlusPuniDoprinosOstalih()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba", priznati: 300m),   // Tarifa 500 → sud priznao 300
            Stavka("par_rociste_glavno")));        // bez unosa → puni doprinos 500

        // Tarifni izračun netaknut
        Assert.Equal(1_000m, rezultat.UkupnoBodova);
        Assert.Equal(2_000.00m, rezultat.UkupnoEurBezPdv);

        // Po retku
        var tuzba = rezultat.Stavke.Single(s => s.ItemId == "par_tuzba");
        Assert.Equal(300m, tuzba.PriznatiBodovi);
        Assert.Equal(300m, tuzba.PriznatoUkupnoBodova);
        Assert.Equal(500m, rezultat.Stavke.Single(s => s.ItemId == "par_rociste_glavno").PriznatoUkupnoBodova);

        // Priznati zbroj
        Assert.NotNull(rezultat.Priznato);
        Assert.Equal(800m, rezultat.Priznato!.OsnovicaBodova);
        Assert.Equal(0m, rezultat.Priznato.PovisenjeBodovi);
        Assert.Equal(800m, rezultat.Priznato.UkupnoBodova);
        Assert.Equal(1_600.00m, rezultat.Priznato.Nagrada);
        Assert.Equal(1_600.00m, rezultat.Priznato.OsnovicaSaNaknadom);
        Assert.Equal(0m, rezultat.Priznato.Pdv);
        // Sveukupno = priznata nagrada + PDV + pristojbe + predujmovi. Pristojba na tužbu
        // (Tar. br. 1 st. 1, VPS 50.000 € → 620 €) identična je tarifnoj strani.
        Assert.Equal(rezultat.UkupnoPristojbe, rezultat.Priznato.Sveukupno - rezultat.Priznato.OsnovicaSaNaknadom);
        Assert.Equal(1_600.00m + rezultat.UkupnoPristojbe, rezultat.Priznato.Sveukupno);
    }

    // ── Tbr. 40 (više osoba) primjenjuje se i na priznatu osnovicu ──

    [Fact]
    public void SaTbr40_PriznatoPovisenjeKoristiIstiPostotak()
    {
        var zahtjev = Zahtjev(
            Stavka("par_tuzba", priznati: 300m),
            Stavka("par_rociste_glavno"));
        zahtjev.BrojZastupanihOsoba = 2; // Tbr. 40 → +10 %

        var rezultat = Izracunaj(zahtjev);

        Assert.Equal(0.10m, rezultat.PovisenjeViseOsobaPostotak);
        Assert.NotNull(rezultat.Priznato);
        Assert.Equal(800m, rezultat.Priznato!.OsnovicaBodova);
        Assert.Equal(80m, rezultat.Priznato.PovisenjeBodovi);   // 800 × 10 %
        Assert.Equal(880m, rezultat.Priznato.UkupnoBodova);
        Assert.Equal(1_760.00m, rezultat.Priznato.Nagrada);
        Assert.Equal(880.00m, rezultat.Priznato.NagradaPoOsobi); // 1.760 / 2
    }

    // ── Prikovani redak: Tbr. 41 tog retka se za priznato zanemaruje ──

    [Fact]
    public void PrikovaniRedakSTbr41_ZaPriznatoUlaziTocnoPrikovanomBrojkom()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba", priznati: 300m, povisenje: 100m), // Tbr41 +100 % se za priznato ignorira
            Stavka("par_rociste_glavno", povisenje: 100m)));       // bez pina → 500 + 500 = 1.000

        // Tarifa: tužba 500 + 500 (Tbr41) + ročište 500 + 500 = 2.000 bod.
        Assert.Equal(2_000m, rezultat.UkupnoBodova);

        var tuzba = rezultat.Stavke.Single(s => s.ItemId == "par_tuzba");
        Assert.Equal(300m, tuzba.PriznatoUkupnoBodova); // ne 600

        Assert.NotNull(rezultat.Priznato);
        Assert.Equal(1_300m, rezultat.Priznato!.OsnovicaBodova); // 300 + 1.000
        Assert.Equal(2_600.00m, rezultat.Priznato.Nagrada);
    }

    // ── PDV se obračunava i na priznatu nagradu ──

    [Fact]
    public void SaPdv_PriznatiPdvNaPriznatuNagradu()
    {
        var zahtjev = Zahtjev(
            Stavka("par_tuzba", priznati: 300m),
            Stavka("par_rociste_glavno"));
        zahtjev.IsPdvUkljucen = true;

        var rezultat = Izracunaj(zahtjev);

        Assert.NotNull(rezultat.Priznato);
        Assert.Equal(1_600.00m, rezultat.Priznato!.OsnovicaSaNaknadom);
        Assert.Equal(400.00m, rezultat.Priznato.Pdv);          // 25 % × 1.600
        Assert.Equal(2_000.00m, rezultat.Priznato.UkupnoSPdv);
        Assert.Equal(2_000.00m + rezultat.UkupnoPristojbe, rezultat.Priznato.Sveukupno);
    }

    // ── Validacija ──

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(10_001)]
    public void PriznatiBodoviIzvanRaspona_BacaGresku(int priznati)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(Zahtjev(Stavka("par_tuzba", priznati: priznati))));

        Assert.Contains("priznat", greska.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
