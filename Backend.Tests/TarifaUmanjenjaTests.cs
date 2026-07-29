using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Dvije stope umanjenja čiji je izvor sada potvrđen.
///
/// 1. Branitelj po službenoj dužnosti / na teret proračunskih sredstava → 50% tarife.
///    Tbr. 4. toč. 7 upućuje dalje: "Nagrada za rad odvjetnika koji je određen za
///    branitelja po službenoj dužnosti (...) određuje se Pravilnikom ministarstva
///    nadležnog za poslove pravosuđa."
///    Izvor stope: Pravilnik, NN 22/2022, br. 271.
///
/// 2. Opunomoćenik djeteta žrtve kaznenog djela → 70% od Tbr. 4. toč. 8,
///    a Tbr. 4. toč. 8 je 50% nagrade iz Tbr. 4 ⇒ efektivno 35%, ne 70%.
/// </summary>
public class TarifaUmanjenjaTests
{
    private static TroskovnikResponse Izracunaj(Action<TroskovnikRequest> podesi)
    {
        var zahtjev = new TroskovnikRequest
        {
            ZaprijecenaKazna = "dugotrajni",   // Tbr. 4. toč. 1 al. 5 → 500 bodova
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        };
        podesi(zahtjev);
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    [Fact]
    public void PunaTarifa_Je500Bodova()
    {
        Assert.Equal(500m, Izracunaj(_ => { }).UkupnoBodova);
    }

    // ── Branitelj po službenoj dužnosti / na teret proračuna: 50% ──

    [Fact]
    public void PoSluzbenojDuznosti_Je50PostoTarife()
    {
        Assert.Equal(250m, Izracunaj(z => z.IsPoSluzbenojDuznosti = true).UkupnoBodova);
    }

    [Fact]
    public void NaTeretProracuna_Je50PostoTarife()
    {
        Assert.Equal(250m, Izracunaj(z => z.IsNaTeretProracuna = true).UkupnoBodova);
    }

    [Fact]
    public void StopaUmanjenja_Je50Posto()
    {
        Assert.Equal(0.50m, TarifaConstants.KazneniReductionRate);
    }

    // ── Opunomoćenik djeteta žrtve: 35%, ne 70% ──

    /// <summary>
    /// Točka 11 audita traži izričitu provjeru da kod računa 35%, a ne 70%.
    /// 500 × 0,50 (Tbr. 4. toč. 8) × 0,70 = 175 bodova.
    /// </summary>
    [Fact]
    public void OpunomocenikDjeteta_Je35PostoTarife_NeNe70()
    {
        var rezultat = Izracunaj(z => z.IsZastupnikDjeteta = true);

        Assert.Equal(175m, rezultat.UkupnoBodova);   // 35% od 500
        Assert.NotEqual(350m, rezultat.UkupnoBodova); // 70% bi bilo 350
    }

    [Theory]
    [InlineData("novcana_do3", 35)]   // 100 → 35
    [InlineData("do_5", 70)]          // 200 → 70
    [InlineData("do_10", 105)]        // 300 → 105
    [InlineData("preko_10", 140)]     // 400 → 140
    [InlineData("dugotrajni", 175)]   // 500 → 175
    public void OpunomocenikDjeteta_JeUvijek35Posto(string kazna, decimal ocekivano)
    {
        var rezultat = Izracunaj(z =>
        {
            z.ZaprijecenaKazna = kazna;
            z.IsZastupnikDjeteta = true;
        });

        Assert.Equal(ocekivano, rezultat.UkupnoBodova);
    }

    [Fact]
    public void EfektivnaStopaZaDijeteZrtve_Je35Posto()
    {
        Assert.Equal(0.35m, TarifaConstants.Tbr4t8Rate * TarifaConstants.ZastupnikDjetetaRate);
    }
}
