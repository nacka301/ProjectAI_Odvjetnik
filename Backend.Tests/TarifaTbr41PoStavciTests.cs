using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 41 — povisivanje i snižavanje po POJEDINOJ stavci.
///
/// toč. 1: "Za posebno stručno i specijalističko znanje (…) tarifne stavke mogu se
///          povisiti za 100%."
/// toč. 2: "Uzimajući u obzir sve okolnosti obavljenog posla i koristi koje ima
///          stranka, POJEDINE tarifne stavke odvjetnik može sniziti za 50%."
///
/// Riječ "pojedine" stoji samo u toč. 2. Prije je i povišenje i sniženje djelovalo na
/// cijeli zbroj; sada oboje ide po stavci.
///
/// Kompozicija ostaje ADITIVNA na zbroj: udio se računa iz bodova pojedine stavke, ali
/// se doprinosi zbrajaju i dodaju ukupnom iznosu — isto kao Tbr. 40. Da je umjesto toga
/// multiplikativno po stavci, Tbr. 40 bi se složio na već umanjenu osnovicu i ukupan bi
/// se iznos promijenio (vidi RegresijaSaTbr40).
/// </summary>
public class TarifaTbr41PoStavciTests
{
    /// <summary>VPS 50.000 € → tužba 500 bod., ročište 500 bod., žalba 625 bod.</summary>
    private static TroskovnikRequest Zahtjev(params TroskovnikItemRequest[] stavke)
        => new()
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke = [.. stavke]
        };

    /// <summary>
    /// Postoci su null kad nisu navedeni — tek tada stavka nasljeđuje postotak s razine
    /// zahtjeva (prečac „primijeni na sve stavke").
    /// </summary>
    private static TroskovnikItemRequest Stavka(string id, int kolicina = 1, decimal? povisenje = null, decimal? snizenje = null)
        => new()
        {
            ItemId = id,
            Kolicina = kolicina,
            Tbr41PovisenjePostotak = povisenje,
            Tbr41SnizenjePostotak = snizenje
        };

    private static TroskovnikResponse Izracunaj(TroskovnikRequest zahtjev)
        => new TroskovnikService().Izracunaj(zahtjev);

    // ── Po stavci ──

    /// <summary>
    /// Sniženje na jednoj stavci ne dira ostale.
    /// Tužba 500 − 50 % = −250; ročište 500 netaknuto. Ukupno 1.000 − 250 = 750 bod.
    /// </summary>
    [Fact]
    public void Snizenje_DjelujeSamoNaSvojuStavku()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba", snizenje: 50m),
            Stavka("par_rociste_glavno")));

        var tuzba = rezultat.Stavke.Single(s => s.ItemId == "par_tuzba");
        var rociste = rezultat.Stavke.Single(s => s.ItemId == "par_rociste_glavno");

        Assert.Equal(-250m, tuzba.Tbr41Bodovi);
        Assert.Equal(0m, rociste.Tbr41Bodovi);
        Assert.Equal(750m, rezultat.UkupnoBodova);
        Assert.Equal(1_500.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Povišenje 100 % na jednoj stavci: 500 + 500 = +500; ukupno 1.500 bod.</summary>
    [Fact]
    public void Povisenje_DjelujeSamoNaSvojuStavku()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba", povisenje: 100m),
            Stavka("par_rociste_glavno")));

        Assert.Equal(500m, rezultat.Stavke.Single(s => s.ItemId == "par_tuzba").Tbr41Bodovi);
        Assert.Equal(1_500m, rezultat.UkupnoBodova);
    }

    /// <summary>Različiti postoci na različitim stavkama u istom troškovniku.</summary>
    [Fact]
    public void RazliciteStavke_RazlicitiPostoci()
    {
        var rezultat = Izracunaj(Zahtjev(
            Stavka("par_tuzba", povisenje: 100m),          // 500 → +500
            Stavka("par_rociste_glavno", snizenje: 50m),   // 500 → −250
            Stavka("par_zalba_presuda")));                 // 625 → 0

        Assert.Equal(250m, rezultat.Tbr41Bodovi);          // +500 − 250
        Assert.Equal(1_875m, rezultat.UkupnoBodova);       // 1.625 + 250
    }

    /// <summary>Količina ulazi u izračun: 2 ročišta × 500 = 1.000, −50 % = −500.</summary>
    [Fact]
    public void Kolicina_UlaziUIzracun()
    {
        var rezultat = Izracunaj(Zahtjev(Stavka("par_rociste_glavno", kolicina: 2, snizenje: 50m)));

        Assert.Equal(-500m, rezultat.Stavke.Single().Tbr41Bodovi);
        Assert.Equal(500m, rezultat.UkupnoBodova);
    }

    // ── Međusobno isključivanje ──

    [Fact]
    public void PovisenjeISnizenje_NaIstojStavci_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(Zahtjev(Stavka("par_tuzba", povisenje: 50m, snizenje: 20m))));

        Assert.Contains("Tbr. 41", greska.Message);
        Assert.Contains("istovremeno", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(150, 0)]   // povišenje preko 100 %
    [InlineData(0, 80)]    // sniženje preko 50 %
    public void PostotakIzvanRaspona_BacaGresku(decimal povisenje, decimal snizenje)
    {
        Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(Zahtjev(Stavka("par_tuzba", povisenje: povisenje, snizenje: snizenje))));
    }

    // ── Prečac "primijeni na sve" ──

    /// <summary>
    /// Postoci na razini zahtjeva vrijede za svaku stavku koja nema vlastiti.
    /// </summary>
    [Fact]
    public void PostotakNaRazuniZahtjeva_PrimjenjujeSeNaSveStavke()
    {
        var zahtjev = Zahtjev(Stavka("par_tuzba"), Stavka("par_rociste_glavno"));
        zahtjev.Tbr41SnizenjePostotak = 50m;

        var rezultat = Izracunaj(zahtjev);

        Assert.All(rezultat.Stavke, s => Assert.Equal(50m, s.Tbr41SnizenjePostotak));
        Assert.Equal(-500m, rezultat.Tbr41Bodovi);
    }

    /// <summary>Vlastiti postotak stavke nadjačava prečac.</summary>
    [Fact]
    public void VlastitiPostotakStavke_NadjacavaPrecac()
    {
        var zahtjev = Zahtjev(
            Stavka("par_tuzba", snizenje: 20m),
            Stavka("par_rociste_glavno"));
        zahtjev.Tbr41SnizenjePostotak = 50m;

        var rezultat = Izracunaj(zahtjev);

        Assert.Equal(-100m, rezultat.Stavke.Single(s => s.ItemId == "par_tuzba").Tbr41Bodovi);
        Assert.Equal(-250m, rezultat.Stavke.Single(s => s.ItemId == "par_rociste_glavno").Tbr41Bodovi);
    }

    // ── Regresija ──

    /// <summary>
    /// Traženo: sniženje od 50 % na SVE stavke daje isti ukupan iznos kao dosadašnji
    /// izračun na razini zbroja. 1.625 bodova − 50 % = 812,5.
    /// </summary>
    [Fact]
    public void Regresija_Snizenje50NaSve_JednakoStaromIzracunu()
    {
        var zahtjev = Zahtjev(
            Stavka("par_tuzba", snizenje: 50m),
            Stavka("par_rociste_glavno", snizenje: 50m),
            Stavka("par_zalba_presuda", snizenje: 50m));

        var rezultat = Izracunaj(zahtjev);

        // Stari izračun: osnovica × (1 − 0,5)
        Assert.Equal(1_625m * 0.5m, rezultat.UkupnoBodova);
        Assert.Equal(812.5m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Ista regresija uz Tbr. 40. Zbog aditivne kompozicije iznos ostaje identičan
    /// starom izračunu: 1.625 × (1 + 0,10 − 0,50) = 975.
    ///
    /// Da je Tbr. 41 primijenjen multiplikativno po stavci, dobilo bi se
    /// 812,5 × 1,10 = 893,75 — drukčiji iznos.
    /// </summary>
    [Fact]
    public void RegresijaSaTbr40_OstajeAditivna()
    {
        var zahtjev = Zahtjev(
            Stavka("par_tuzba", snizenje: 50m),
            Stavka("par_rociste_glavno", snizenje: 50m),
            Stavka("par_zalba_presuda", snizenje: 50m));
        zahtjev.BrojZastupanihOsoba = 2;   // Tbr. 40 → +10 %

        var rezultat = Izracunaj(zahtjev);

        Assert.Equal(1_625m * (1m + 0.10m - 0.50m), rezultat.UkupnoBodova);
        Assert.Equal(975m, rezultat.UkupnoBodova);
        Assert.NotEqual(893.75m, rezultat.UkupnoBodova);
    }

    [Fact]
    public void BezPostotaka_NistaSeNeMijenja()
    {
        var rezultat = Izracunaj(Zahtjev(Stavka("par_tuzba"), Stavka("par_rociste_glavno")));

        Assert.Equal(0m, rezultat.Tbr41Bodovi);
        Assert.Equal(1_000m, rezultat.UkupnoBodova);
        Assert.All(rezultat.Stavke, s => Assert.Equal("", s.Tbr41PravniIzvor));
    }

    // ── Pravni izvor uz stavku ──

    [Fact]
    public void SnizenaStavka_NosiCitatToc2()
    {
        var stavka = Izracunaj(Zahtjev(Stavka("par_tuzba", snizenje: 50m))).Stavke.Single();

        Assert.Contains("Tbr. 41. toč. 2", stavka.Tbr41PravniIzvor);
        Assert.Contains("50", stavka.Tbr41PravniIzvor);
        Assert.Contains("pojedine tarifne stavke", stavka.Tbr41PravniIzvor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PovisenaStavka_NosiCitatToc1()
    {
        var stavka = Izracunaj(Zahtjev(Stavka("par_tuzba", povisenje: 100m))).Stavke.Single();

        Assert.Contains("Tbr. 41. toč. 1", stavka.Tbr41PravniIzvor);
        Assert.Contains("100", stavka.Tbr41PravniIzvor);
    }
}
