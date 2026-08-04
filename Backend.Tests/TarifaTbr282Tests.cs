using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 28. toč. 2 — isprave potrebne za upis u registar.
///
/// "Za sastavljanje isprava (izjava o osnivanju društva, društveni ugovor, statut, te
/// ostale isprave) potrebnih za upis osnivanja ili promjena u registru, odvjetniku
/// pripada nagrada prema Tbr. 33. toč. 1. i 2."
///
/// Tbr. 33. toč. 1: "Za sastavljanje ugovora odvjetniku pripada nagrada iz Tbr. 7.
/// toč. 1. uvećana za 25%."
/// Tbr. 33. toč. 2 al. 3: "ako u ugovoru nije naznačena vrijednost ugovora odvjetniku
/// pripada nagrada od 200 bodova."
///
/// Katalog je imao 28.1, 28.3 i 28.4, ali ne i 28.2 — sastavljanje osnivačkih isprava
/// nije se moglo obračunati.
/// </summary>
public class TarifaTbr282Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps = 0m)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    /// <summary>
    /// Tbr. 33. toč. 1 — Tbr. 7. toč. 1 uvećan za 25%.
    /// Ručni izračun: VPS 50.000 € → razred 33.181,01–66.361,00 → 500 bodova;
    /// 500 × 1,25 = 625 bodova = 1.250,00 €.
    /// </summary>
    [Fact]
    public void ProcjenjivaIsprava_IdePoTbr331_Tbr71Uvecan25Posto()
    {
        var rezultat = Izracunaj("reg_isprave_proc", 50_000m);

        Assert.Equal(625, rezultat.UkupnoBodova);
        Assert.Equal(1_250.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>
    /// Tbr. 33. toč. 2 al. 3 — 200 bodova kad vrijednost nije naznačena.
    /// Ručni izračun: 200 bod. × 2,00 €/bod = 400,00 €.
    /// </summary>
    [Fact]
    public void NeprocjenjivaIsprava_Daje200Bodova()
    {
        var rezultat = Izracunaj("reg_isprave_neproc");

        Assert.Equal(200, rezultat.UkupnoBodova);
        Assert.Equal(400.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Neprocjenjiva varijanta nema vrijednost — VPS je ne smije pomaknuti.</summary>
    [Fact]
    public void NeprocjenjivaIsprava_NeOvisiOVps()
    {
        Assert.Equal(
            Izracunaj("reg_isprave_neproc", 0m).UkupnoBodova,
            Izracunaj("reg_isprave_neproc", 500_000m).UkupnoBodova);
    }

    /// <summary>
    /// Iznosi moraju biti identični postojećim stavkama po Tbr. 33, jer Tbr. 28. toč. 2
    /// upravo na njih upućuje. Razilaženje bi značilo da je jedna od dvije krivo upisana.
    /// </summary>
    [Theory]
    [InlineData("reg_isprave_proc", "raz_ugovor")]
    [InlineData("reg_isprave_neproc", "raz_ugovor_neproc")]
    public void Tbr282_DajeIstoStoIPripadajucaStavkaPoTbr33(string tbr282Item, string tbr33Item)
    {
        Assert.Equal(
            Izracunaj(tbr33Item, 50_000m).UkupnoBodova,
            Izracunaj(tbr282Item, 50_000m).UkupnoBodova);
    }

    /// <summary>Obje stavke postoje, nose Tbr. 28.2 i registarsku kategoriju.</summary>
    [Theory]
    [InlineData("reg_isprave_proc")]
    [InlineData("reg_isprave_neproc")]
    public void Stavke_PostojeURegistarskojKategoriji(string itemId)
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == itemId);

        Assert.Equal("Tbr. 28.2", def.TarifniBroj);
        Assert.Equal("Registarski postupak", def.Kategorija);
    }
}
