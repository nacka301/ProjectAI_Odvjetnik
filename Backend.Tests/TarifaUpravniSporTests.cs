using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 27 — upravni spor.
///
/// Tbr. 27. toč. 1: "Za sastavljanje tužbe, odgovora na tužbu (...) u prvostupanjskom
/// upravnom sporu: — u procjenjivim predmetima odvjetniku pripada nagrada iz Tbr. 7.
/// toč. 1., — u neprocjenjivim predmetima odvjetniku pripada nagrada od 500 bodova."
///
/// Toč. 2 (rasprava), toč. 3 (žalba, +25%) i toč. 4 (zahtjev za izvršenje, 50%) sve
/// upućuju na toč. 1, pa i one imaju neprocjenjivu varijantu. Bez nje su u
/// neprocjenjivom predmetu davale 25 bodova (VPS = 0 → dno tablice iz Tbr. 7. toč. 1)
/// umjesto 500.
/// </summary>
public class TarifaUpravniSporTests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps = 0m)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    [Theory]
    [InlineData("usp_tuzba_neproc", 500)]      // Tbr. 27.1 al. 2 — 500 bodova
    [InlineData("usp_rasprava_neproc", 500)]   // Tbr. 27.2 → toč. 1
    [InlineData("usp_zalba_neproc", 625)]      // Tbr. 27.3 — toč. 1 uvećana za 25%: 500 × 1,25
    [InlineData("usp_izvrsenje_neproc", 250)]  // Tbr. 27.4 — 50% nagrade iz toč. 1
    public void NeprocjenjiviPredmeti_ImajuFiksnuOsnovicuOd500Bodova(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId).UkupnoBodova);
    }

    /// <summary>
    /// Neprocjenjive varijante ne smiju ovisiti o VPS-u — nemaju vrijednost predmeta.
    /// </summary>
    [Theory]
    [InlineData("usp_tuzba_neproc")]
    [InlineData("usp_rasprava_neproc")]
    [InlineData("usp_zalba_neproc")]
    [InlineData("usp_izvrsenje_neproc")]
    public void NeprocjenjiviPredmeti_NeOviseOVps(string itemId)
    {
        Assert.Equal(Izracunaj(itemId, 0m).UkupnoBodova, Izracunaj(itemId, 500_000m).UkupnoBodova);
    }

    /// <summary>
    /// Procjenjive varijante zadržavaju postojeće ID-eve, da spremljeni troškovnici
    /// ne izgube stavke, i dalje idu po Tbr. 7. toč. 1.
    /// </summary>
    [Theory]
    [InlineData("usp_tuzba_proc", 500)]      // VPS 50.000 → 500
    [InlineData("usp_rasprava", 500)]        // Tbr. 27.2 → toč. 1
    [InlineData("usp_zalba", 625)]           // + 25%
    [InlineData("usp_izvrsenje", 250)]       // 50%
    public void ProcjenjiviPredmeti_IduPoTbr71(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId, 50_000m).UkupnoBodova);
    }

    /// <summary>
    /// Točka 7 audita: usp_rasprava je u neprocjenjivom predmetu davala 25 bodova.
    /// Sada za takve predmete postoji vlastita stavka s 500 bodova.
    /// </summary>
    [Fact]
    public void RaspravaUNeprocjenjivomPredmetu_Daje500NeNe25()
    {
        var rezultat = Izracunaj("usp_rasprava_neproc");

        Assert.Equal(500, rezultat.UkupnoBodova);
        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
    }
}
