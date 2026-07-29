using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 34. toč. 2 — pisani pravni savjet.
///
/// Tarifa: "Za sastavljanje pisanih pravnih savjeta i pisanih pravnih mišljenja s
/// činjeničnim i pravnim obrazloženjima odvjetniku pripada nagrada iz Tbr. 7. toč. 1. i 4.
///
/// Za sastavljanje pisanih pravnih savjeta i pisanih pravnih mišljenja s činjeničnim i
/// pravnim obrazloženjima u NEPROCJENJIVIM predmetima odvjetniku pripada nagrada ne
/// manja od 200 bodova."
///
/// Minimum od 200 bodova stoji samo u drugom odlomku — dakle samo za neprocjenjive
/// predmete. Prije popravka primjenjivao se na sve, pa je savjet u sporu vrijednom
/// 500 € naplaćivan 200 bodova umjesto 50.
/// </summary>
public class TarifaPisaniSavjetTests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    /// <summary>
    /// U procjenjivom predmetu savjet ide po Tbr. 7. toč. 1, bez ikakvog minimuma.
    /// </summary>
    [Theory]
    [InlineData(300, 25)]
    [InlineData(500, 50)]
    [InlineData(1_000, 75)]
    [InlineData(20_000, 250)]
    [InlineData(50_000, 500)]
    public void ProcjenjivPredmet_NemaMinimuma(decimal vps, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj("raz_pisani_savjet", vps).UkupnoBodova);
    }

    /// <summary>
    /// Slučaj iz audita: pri VPS-u 500 € savjet je 50 bodova, ne 200.
    /// </summary>
    [Fact]
    public void ProcjenjivPredmet_Vps500_Daje50Bodova_NeNe200()
    {
        var rezultat = Izracunaj("raz_pisani_savjet", 500m);

        Assert.Equal(50m, rezultat.UkupnoBodova);
        Assert.NotEqual(200m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// U neprocjenjivom predmetu nagrada je "ne manja od 200 bodova".
    /// </summary>
    [Fact]
    public void NeprocjenjivPredmet_Daje200Bodova()
    {
        Assert.Equal(200m, Izracunaj("raz_pisani_savjet_neproc", 0m).UkupnoBodova);
    }

    [Fact]
    public void NeprocjenjivPredmet_NeOvisiOVps()
    {
        Assert.Equal(
            Izracunaj("raz_pisani_savjet_neproc", 0m).UkupnoBodova,
            Izracunaj("raz_pisani_savjet_neproc", 500_000m).UkupnoBodova);
    }

    /// <summary>
    /// Procjenjiva varijanta više ne smije nositi MinBodova.
    /// </summary>
    [Fact]
    public void ProcjenjivaVarijanta_NemaMinBodova()
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == "raz_pisani_savjet");

        Assert.Null(def.MinBodova);
    }
}
