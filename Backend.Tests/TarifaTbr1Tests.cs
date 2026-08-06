using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 1 — sastavljanje podnesaka u kaznenom i prekršajnom postupku.
///
/// Toč. 1, 2 i 3 vežu se na Tbr. 4. toč. 1, dakle na zapriječenu kaznu.
/// Toč. 4 i 5 imaju vlastite fiksne iznose i NE ovise o kazni:
///   toč. 4: "Za sastavljanje podneska kojim se postavlja imovinskopravni zahtjev
///           oštećenika odvjetniku pripada nagrada od 75 bodova."
///   toč. 5: "Za sastavljanje ostalih podnesaka odvjetniku pripada nagrada od 50 bodova."
/// </summary>
public class TarifaTbr1Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, string? kazna = "do_5", bool poSluzbenoj = false)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = kazna,
            IsPoSluzbenojDuznosti = poSluzbenoj,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    // ── Toč. 1, 2 i 3 — nagrada po Tbr. 4. toč. 1 ──

    [Theory]
    [InlineData("kaz_privatna_tuzba")]
    [InlineData("kaz_podnesak_obrana")]
    [InlineData("kaz_zahtjev_ostecenik")]
    public void Toc1Do3_PrateZaprijecenuKaznu(string itemId)
    {
        Assert.Equal(100m, Izracunaj(itemId, "novcana_do3").UkupnoBodova);
        Assert.Equal(200m, Izracunaj(itemId, "do_5").UkupnoBodova);
        Assert.Equal(300m, Izracunaj(itemId, "do_10").UkupnoBodova);
        Assert.Equal(400m, Izracunaj(itemId, "preko_10").UkupnoBodova);
        Assert.Equal(500m, Izracunaj(itemId, "dugotrajni").UkupnoBodova);
    }

    // ── Toč. 4 i 5 — fiksni iznosi, ne ovise o kazni ──

    [Fact]
    public void Toc4_ImovinskopravniZahtjev_Je75Bodova()
    {
        var rezultat = Izracunaj("kaz_imovinskopravni", kazna: null);

        Assert.Equal(75m, rezultat.UkupnoBodova);
        Assert.Equal(150.00m, rezultat.UkupnoEurBezPdv);   // 75 × 2,00 €
    }

    [Fact]
    public void Toc5_OstaliPodnesci_Je50Bodova()
    {
        var rezultat = Izracunaj("kaz_ostali_podnesci", kazna: null);

        Assert.Equal(50m, rezultat.UkupnoBodova);
        Assert.Equal(100.00m, rezultat.UkupnoEurBezPdv);
    }

    [Theory]
    [InlineData("kaz_imovinskopravni", 75)]
    [InlineData("kaz_ostali_podnesci", 50)]
    public void Toc4I5_NeOviseOZaprijecenojKazni(string itemId, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj(itemId, "novcana_do3").UkupnoBodova);
        Assert.Equal(ocekivano, Izracunaj(itemId, "dugotrajni").UkupnoBodova);
    }

    // ── Neizvorni minimum ──

    /// <summary>
    /// Tbr. 1. toč. 1 glasi samo "odvjetniku pripada nagrada po Tbr. 4. toč. 1." —
    /// nikakav minimum se ne spominje. Zatečeni MinBodova = 100 poništavao je
    /// umanjenje od 50% za branitelja po službenoj dužnosti (Pravilnik NN 22/2022
    /// br. 271): 100 × 0,50 = 50, pa clamp natrag na 100.
    /// </summary>
    [Fact]
    public void Toc1_NemaMinimumaKojiPonistavaUmanjenje()
    {
        var def = TroskovnikService.DohvatiKatalog().Single(s => s.Id == "kaz_privatna_tuzba");
        Assert.Null(def.MinBodova);

        // novčana kazna / do 3 godine = 100 bodova; po službenoj dužnosti → 50
        Assert.Equal(50m, Izracunaj("kaz_privatna_tuzba", "novcana_do3", poSluzbenoj: true).UkupnoBodova);
    }
}
