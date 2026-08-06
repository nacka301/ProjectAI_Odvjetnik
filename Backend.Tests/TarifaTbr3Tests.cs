using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 3 — zastupanje i obrana pred policijom, državnim odvjetništvom, Uredom
/// europskog javnog tužitelja i sucem istrage.
///
/// toč. 1: "(…) odvjetniku kao branitelju ili opunomoćeniku oštećenika kao tužitelja
///          pripada nagrada iz Tbr. 4. toč. 1., te za drugi i svaki sljedeći započeti
///          sat prisutnosti u postupku naknada od 50 bodova."
/// toč. 2: "Za radnje iz toč. 1 (…) koje (…) provode izvan zgrade svog sjedišta
///          odvjetniku pripada, osim nagrade iz toč. 1. ovog tarifnog broja i naknada
///          po Tbr. 49."
/// toč. 3: "Odvjetniku kao punomoćniku oštećenika odnosno žrtve pripada 50% nagrade
///          iz ovog tarifnog broja."
/// toč. 4: "(…) došlo do odgode obavljanja radnje, odvjetniku pripada 50% nagrade iz
///          toč. 1. ovog tarifnog broja kao i naknada po Tbr. 49."
///
/// Tbr. 49 je naknada stvarnih putnih troškova, ne bodovi — obračunava se zasebno.
/// </summary>
public class TarifaTbr3Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, string kazna, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = kazna,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    // ── toč. 1 — prvi sat po Tbr. 4. toč. 1, svaki sljedeći 50 bodova ──

    [Theory]
    [InlineData("novcana_do3", 100)]
    [InlineData("do_5", 200)]
    [InlineData("do_10", 300)]
    [InlineData("preko_10", 400)]
    [InlineData("dugotrajni", 500)]
    public void Toc1_PrviSat_PoTbr4Toc1(string kazna, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj("kaz_sudjelovanje_istraga", kazna).UkupnoBodova);
    }

    [Fact]
    public void Toc1_SvakiSljedeciSat_Je50Bodova()
    {
        // tri dodatna sata × 50 = 150 bodova = 300,00 €
        var rezultat = Izracunaj("kaz_istraga_dodatni_sat", "dugotrajni", kolicina: 3);

        Assert.Equal(150m, rezultat.UkupnoBodova);
        Assert.Equal(300.00m, rezultat.UkupnoEurBezPdv);
    }

    // ── toč. 2 — radnje izvan zgrade sjedišta ──

    /// <summary>
    /// Bodovna nagrada je ista kao po toč. 1; Tbr. 49 (stvarni putni troškovi) dolazi
    /// povrh toga i ne izražava se u bodovima.
    /// </summary>
    [Theory]
    [InlineData("novcana_do3", 100)]
    [InlineData("do_5", 200)]
    [InlineData("dugotrajni", 500)]
    public void Toc2_IzvanSjedista_JednakaNagradiIzToc1(string kazna, decimal ocekivano)
    {
        var rezultat = Izracunaj("kaz_istraga_izvan_sjedista", kazna);

        Assert.Equal(ocekivano, rezultat.UkupnoBodova);
        Assert.Equal(ocekivano, Izracunaj("kaz_sudjelovanje_istraga", kazna).UkupnoBodova);
    }

    [Fact]
    public void Toc2_UpucujeNaTbr49()
    {
        var def = TroskovnikService.DohvatiKatalog().Single(s => s.Id == "kaz_istraga_izvan_sjedista");

        Assert.Contains("Tbr. 49", def.PravniIzvor);
        Assert.Contains("Tbr. 49", def.Opis);
    }

    // ── toč. 3 i 4 — 50% ──

    /// <summary>Punomoćnik oštećenika: 50% od 200 = 100 bodova = 200,00 €.</summary>
    [Fact]
    public void Toc3_PunomocnikOstecenika_Je50Posto()
    {
        var rezultat = Izracunaj("kaz_istraga_ostecenik", "do_5");

        Assert.Equal(100m, rezultat.UkupnoBodova);
        Assert.Equal(200.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Odgoda radnje: 50% od 500 = 250 bodova = 500,00 €.</summary>
    [Fact]
    public void Toc4_OdgodaRadnje_Je50Posto()
    {
        var rezultat = Izracunaj("kaz_istraga_odgoda", "dugotrajni");

        Assert.Equal(250m, rezultat.UkupnoBodova);
        Assert.Equal(500.00m, rezultat.UkupnoEurBezPdv);
    }

    [Fact]
    public void Toc4_UpucujeNaTbr49()
    {
        var def = TroskovnikService.DohvatiKatalog().Single(s => s.Id == "kaz_istraga_odgoda");

        Assert.Contains("Tbr. 49", def.PravniIzvor);
    }
}
