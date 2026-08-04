using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 23 — zastupanje i obrana u prvostupanjskom upravnom postupku.
///
/// Toč. 1: "Za zastupanje u ispitnom postupku, u postupku osiguranja dokaza, na usmenoj
/// raspravi te očevidu u prvostupanjskom upravnom postupku odvjetniku pripada nagrada
/// iz Tbr. 22. te naknada po Tbr. 39."
///
/// Toč. 2: "Za obranu u postupcima radi poreznih, carinskih ili deviznih prekršaja
/// odvjetniku pripada nagrada po Tbr. 22., te naknada po Tbr. 39."
///
/// Tbr. 22. toč. 1 ima dvije alineje — neprocjenjivo 50 bodova, procjenjivo po
/// Tbr. 7. toč. 1 — pa svaka točka Tbr. 23 daje dvije stavke.
///
/// "naknada po Tbr. 39" NIJE uključena u ove stavke: Tbr. 39 (čekanje na raspravu i
/// izbivanje iz pisarnice) već je zasebna stavka kataloga `zaj_cekanje` i obračunava se
/// odvojeno, po satu. Tarifa je veže uz utrošeno vrijeme, koje stavka ne poznaje.
/// </summary>
public class TarifaTbr23Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps = 0m)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    /// <summary>
    /// Tbr. 22. toč. 1 al. 1: "u neprocjenjivim predmetima odvjetniku pripada nagrada
    /// od 50 bodova." Ručni izračun: 50 bod. × 2,00 €/bod = 100,00 €.
    /// </summary>
    [Theory]
    [InlineData("upr_zastupanje_neproc")]        // Tbr. 23. toč. 1 → Tbr. 22. toč. 1 al. 1
    [InlineData("upr_obrana_prekrsaj_neproc")]   // Tbr. 23. toč. 2 → Tbr. 22. toč. 1 al. 1
    public void NeprocjenjiviPredmeti_Daju50Bodova(string itemId)
    {
        var rezultat = Izracunaj(itemId);

        Assert.Equal(50, rezultat.UkupnoBodova);
        Assert.Equal(100.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Neprocjenjiv predmet nema vrijednost spora — VPS ga ne smije pomaknuti.</summary>
    [Theory]
    [InlineData("upr_zastupanje_neproc")]
    [InlineData("upr_obrana_prekrsaj_neproc")]
    public void NeprocjenjiviPredmeti_NeOviseOVps(string itemId)
    {
        Assert.Equal(Izracunaj(itemId, 0m).UkupnoBodova, Izracunaj(itemId, 500_000m).UkupnoBodova);
    }

    /// <summary>
    /// Tbr. 22. toč. 1 al. 2: "odvjetniku pripada nagrada iz Tbr. 7. toč. 1."
    /// Ručni izračun: VPS 50.000 € pada u razred 33.181,01–66.361,00 → 500 bodova.
    /// </summary>
    [Theory]
    [InlineData("upr_zastupanje_proc")]        // Tbr. 23. toč. 1 → Tbr. 22. toč. 1 al. 2
    [InlineData("upr_obrana_prekrsaj_proc")]   // Tbr. 23. toč. 2 → Tbr. 22. toč. 1 al. 2
    public void ProcjenjiviPredmeti_IduPoTbr71(string itemId)
    {
        Assert.Equal(500, Izracunaj(itemId, 50_000m).UkupnoBodova);
    }

    /// <summary>Sve četiri stavke moraju postojati i nositi svoj tarifni broj.</summary>
    [Theory]
    [InlineData("upr_zastupanje_neproc", "Tbr. 23.1")]
    [InlineData("upr_zastupanje_proc", "Tbr. 23.1")]
    [InlineData("upr_obrana_prekrsaj_neproc", "Tbr. 23.2")]
    [InlineData("upr_obrana_prekrsaj_proc", "Tbr. 23.2")]
    public void Stavke_PostojeSTocnimTarifnimBrojem(string itemId, string tarifniBroj)
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == itemId);

        Assert.Equal(tarifniBroj, def.TarifniBroj);
    }

    /// <summary>
    /// Tbr. 23. toč. 2 govori o obrani u prekršajnim postupcima, ali Tarifa je smješta u
    /// glavu VII. UPRAVNI POSTUPAK i nagradu veže uz Tbr. 22, ne uz Tbr. 4.
    ///
    /// Kategorija je ovdje obračunski bitna, ne kozmetička: kad su SVE odabrane stavke u
    /// kategoriji "Kazneni postupak", sudska pristojba pada na nulu (Uredba NN 37/2023 ne
    /// poznaje kazneni postupak). Svrstavanje Tbr. 23. toč. 2 u kazneni dio tiho bi ukinulo
    /// pristojbu u upravnom postupku.
    /// </summary>
    [Theory]
    [InlineData("upr_zastupanje_neproc")]
    [InlineData("upr_zastupanje_proc")]
    [InlineData("upr_obrana_prekrsaj_neproc")]
    [InlineData("upr_obrana_prekrsaj_proc")]
    public void Tbr23_NijeUKazneojKategoriji(string itemId)
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == itemId);

        Assert.Equal("Upravni postupak", def.Kategorija);
        Assert.NotEqual(TroskovnikService.KategorijaKazneni, def.Kategorija);
    }

    /// <summary>
    /// Posljedica prethodnog: obrana po Tbr. 23. toč. 2 ne smije ugasiti sudsku pristojbu.
    /// </summary>
    [Fact]
    public void ObranaUPrekrsajnomPostupku_NeGasiSudskuPristojbu()
    {
        var rezultat = Izracunaj("upr_obrana_prekrsaj_neproc", 3_000m);

        Assert.False(rezultat.IsSamoKazneni);
    }
}
