using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 7. toč. 2 i 3 — jednokratna nagrada.
///
/// Tarifa, Tbr. 7. toč. 2: "Odvjetniku pripada jednokratna nagrada u visini od 250
/// bodova, koja obuhvaća najviše do četiri radnje za svaki prvostupanjski postupak
/// (...) Za svaku daljnju radnju u navedenim postupcima odvjetniku pripada nagrada
/// u visini od 50 bodova."
///
/// Tbr. 7. toč. 3 je isti mehanizam s osnovicom od 200 bodova.
///
/// Prije popravka obje su se stavke množile količinom (kol. 6 × 250 = 1.500 bodova).
/// </summary>
public class TarifaTbr7Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, int kolicina, decimal vps = 0)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    // ── Tbr. 7. toč. 2 — smetanje posjeda, služnosti, stambeni i radni odnosi ──

    [Theory]
    [InlineData(1, 250)]
    [InlineData(3, 250)]
    [InlineData(4, 250)]   // "najviše do četiri radnje"
    [InlineData(5, 300)]   // 250 + 1 × 50
    [InlineData(6, 350)]   // 250 + 2 × 50
    [InlineData(10, 550)]  // 250 + 6 × 50
    public void Tbr7Toc2_JednokratnaNagrada_NeMnoziSeKolicinom(int kolicina, int ocekivanoBodova)
    {
        var rezultat = Izracunaj("par_tuzba_smetanje", kolicina);

        Assert.Equal(ocekivanoBodova, rezultat.UkupnoBodova);
        Assert.Equal(ocekivanoBodova * 2.00m, rezultat.UkupnoEurBezPdv);
    }

    // ── Tbr. 7. toč. 3 — razvod, poništenje braka, očinstvo, uzdržavanje ──

    [Theory]
    [InlineData(1, 200)]
    [InlineData(4, 200)]   // "najviše do četiri radnje"
    [InlineData(5, 250)]   // 200 + 1 × 50
    [InlineData(6, 300)]   // 200 + 2 × 50
    public void Tbr7Toc3_JednokratnaNagrada_NeMnoziSeKolicinom(int kolicina, int ocekivanoBodova)
    {
        var rezultat = Izracunaj("par_tuzba_obiteljski", kolicina);

        Assert.Equal(ocekivanoBodova, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Toč. 2 i toč. 3 imaju RAZLIČITE osnovice — 250 odnosno 200 bodova.
    /// </summary>
    [Fact]
    public void Toc2ITock3_ImajuRazliciteOsnovice()
    {
        Assert.Equal(250, Izracunaj("par_tuzba_smetanje", 1).UkupnoBodova);
        Assert.Equal(200, Izracunaj("par_tuzba_obiteljski", 1).UkupnoBodova);
    }

    /// <summary>
    /// Jednokratna nagrada ne ovisi o vrijednosti predmeta spora — toč. 2 i 3 su
    /// samostalne u odnosu na tablicu iz toč. 1.
    /// </summary>
    [Fact]
    public void JednokratnaNagrada_NeOvisiOVps()
    {
        Assert.Equal(250, Izracunaj("par_tuzba_smetanje", 1, vps: 1_000_000m).UkupnoBodova);
        Assert.Equal(200, Izracunaj("par_tuzba_obiteljski", 1, vps: 1_000_000m).UkupnoBodova);
    }

    /// <summary>
    /// Redak troškovnika mora sam reći da je nagrada jednokratna, inače sučelje
    /// prikazuje "6 × 250 bod." što je pogrešno.
    /// </summary>
    [Fact]
    public void RedakTroskovnika_OznacenJeKaoJednokratan()
    {
        var stavka = Assert.Single(Izracunaj("par_tuzba_smetanje", 6).Stavke);

        Assert.True(stavka.IsJednokratna);
        Assert.Equal(250, stavka.BodovaPoJedinici);   // osnovica
        Assert.Equal(350, stavka.UkupnoBodova);       // osnovica + 2 daljnje radnje
    }

    /// <summary>
    /// Tbr. 7. toč. 2 al. 4: radni odnosi ulaze u jednokratnu nagradu samo ako spor
    /// nije procjenjiv ("osim u procjenjivim predmetima na koje se primjenjuje
    /// Tbr. 7. toč. 1."). Uvjet mora biti vidljiv korisniku u opisu stavke.
    /// </summary>
    [Fact]
    public void OpisStavke_NavodiIznimkuZaProcjenjiveRadneSporove()
    {
        var def = TroskovnikService.DohvatiKatalog().Single(s => s.Id == "par_tuzba_smetanje");

        Assert.Contains("procjenjiv", def.Opis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tbr. 7. toč. 1", def.Opis);
    }
}
