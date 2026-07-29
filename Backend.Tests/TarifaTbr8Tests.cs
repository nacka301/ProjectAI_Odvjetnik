using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 8. toč. 1 — odgovor na tužbu i četiri obrazložena podneska.
///
/// Tarifa, Tbr. 8. toč. 1: "Za sastavljanje odgovora na tužbu, prigovora protiv
/// platnog naloga i četiri obrazložena podneska u prvostupanjskom postupku
/// odvjetniku pripada nagrada kao za tužbu po Tbr. 7. toč. 1., 4., 7. i 8."
///
/// Jedna nagrada pokriva odgovor na tužbu I četiri podneska — ne po komadu.
/// Prije popravka ova stavka uopće nije postojala u katalogu.
/// </summary>
public class TarifaTbr8Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, int kolicina, decimal vps)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    [Fact]
    public void Tbr8Toc1_PostojiUKatalogu()
    {
        var def = TroskovnikService.GetKatalog().SingleOrDefault(s => s.Id == "par_odgovor_tuzba");

        Assert.NotNull(def);
        Assert.Equal("Tbr. 8.1", def.TarifniBroj);
        Assert.True(def.JednokratnaNagrada);
    }

    /// <summary>
    /// Nagrada je jednaka nagradi za tužbu po Tbr. 7. toč. 1 pri istom VPS-u.
    /// </summary>
    [Theory]
    [InlineData(300, 25)]
    [InlineData(20_000, 250)]
    [InlineData(50_000, 500)]
    public void Tbr8Toc1_JednakaJeNagradiZaTuzbuPoTbr71(decimal vps, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj("par_odgovor_tuzba", 1, vps).UkupnoBodova);
        Assert.Equal(ocekivanoBodova, Izracunaj("par_tuzba", 1, vps).UkupnoBodova);
    }

    /// <summary>
    /// Jedna nagrada pokriva odgovor na tužbu, prigovor i četiri podneska —
    /// količina ne smije umnožavati iznos.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(20)]
    public void Tbr8Toc1_NeMnoziSeKolicinom(int kolicina)
    {
        var rezultat = Izracunaj("par_odgovor_tuzba", kolicina, vps: 20_000m);

        Assert.Equal(250, rezultat.UkupnoBodova);
        Assert.Equal(500.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>
    /// Tbr. 8. toč. 3 (obrazloženi podnesci IZVAN toč. 1) ostaje netaknut:
    /// 50% nagrade iz Tbr. 7. toč. 1, ali ne više od 100 bodova, i množi se količinom.
    /// </summary>
    [Fact]
    public void Tbr8Toc3_OstajeNetaknut()
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == "par_obrazlozeni_podnesak");

        Assert.Equal(0.5m, def.Tbr71Postotak);
        Assert.Equal(100, def.MaxBodova);
        Assert.False(def.JednokratnaNagrada);

        // VPS 50.000 → Tbr. 7.1 = 500; 50% = 250, ograničeno na 100; × 3 komada = 300
        Assert.Equal(300, Izracunaj("par_obrazlozeni_podnesak", 3, vps: 50_000m).UkupnoBodova);
    }
}
