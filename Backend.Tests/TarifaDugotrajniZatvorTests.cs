using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 4. toč. 2, al. 3 — rasprava pred višim sudom.
///
/// Tarifa: "— na raspravi pred sudom drugog stupnja odvjetniku pripada nagrada kao
/// za obranu pred sudom prvog stupnja s povišenjem od 25%, a na raspravi pred sudom
/// DRUGOG I TREĆEG STUPNJA za kaznena djela za koja je predviđena kazna DUGOTRAJNOG
/// ZATVORA s povišenjem od 50%."
///
/// Prije popravka koristilo se 25% za sve, uključujući dugotrajni zatvor.
/// </summary>
public class TarifaDugotrajniZatvorTests
{
    private static TroskovnikResponse Izracunaj(string itemId, string zaprijecenaKazna)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = zaprijecenaKazna,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    /// <summary>
    /// Dugotrajni zatvor: osnovica 500 bodova (Tbr. 4. toč. 1 al. 5), + 50% = 750.
    /// </summary>
    [Fact]
    public void DugotrajniZatvor_Povisenje50Posto()
    {
        Assert.Equal(750, Izracunaj("kaz_rasprava_drugostupanjska", "dugotrajni").UkupnoBodova);
    }

    /// <summary>
    /// Sve ostale kazne zadržavaju povišenje od 25% iz iste alineje.
    /// </summary>
    [Theory]
    [InlineData("novcana_do3", 125)]  // 100 × 1,25
    [InlineData("do_5", 250)]         // 200 × 1,25
    [InlineData("do_10", 375)]        // 300 × 1,25
    [InlineData("preko_10", 500)]     // 400 × 1,25
    public void OstaleKazne_Povisenje25Posto(string zaprijecenaKazna, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj("kaz_rasprava_drugostupanjska", zaprijecenaKazna).UkupnoBodova);
    }

    /// <summary>
    /// Povišeni postotak vrijedi samo za raspravu. Sjednica vijeća drugog stupnja ide
    /// "kao za obranu pred sudom prvog stupnja" — bez ikakvog povišenja.
    /// </summary>
    [Fact]
    public void SjednicaVijeca_NemaPovisenja_NiZaDugotrajniZatvor()
    {
        Assert.Equal(500, Izracunaj("kaz_sjednica_drugostupanjska", "dugotrajni").UkupnoBodova);
    }

    /// <summary>
    /// Žalba po Tbr. 5. toč. 1 ima svoje povišenje od 50% neovisno o vrsti kazne —
    /// ta stavka ne smije dobiti dodatno povišenje za dugotrajni zatvor.
    /// </summary>
    [Theory]
    [InlineData("do_5", 300)]         // 200 × 1,5
    [InlineData("dugotrajni", 750)]   // 500 × 1,5 — isti množitelj
    public void ZalbaProtivPresude_ZadrzavaSvojih50Posto(string zaprijecenaKazna, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj("kaz_zalba_presuda", zaprijecenaKazna).UkupnoBodova);
    }

    /// <summary>
    /// Alineja govori o raspravi pred sudom drugog I trećeg stupnja — opis stavke
    /// mora to reći, inače odvjetnik ne zna gdje je smjestiti.
    /// </summary>
    [Fact]
    public void OpisStavke_SpominjeIDrugiITreciStupanj()
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == "kaz_rasprava_drugostupanjska");

        Assert.Contains("trećeg", def.Opis);
    }
}
