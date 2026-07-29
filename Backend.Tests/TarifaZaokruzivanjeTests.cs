using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Zaokruživanje.
///
/// Tarifa nigdje ne propisuje zaokruživanje bodova. Bodovi su mjerna jedinica, ne
/// novac, i ostaju decimalni kroz cijeli lanac. Zaokružuje se samo novac, jednom,
/// na kraju, na dvije decimale, standardno (od nule).
///
/// Prije popravka svaki je korak lanca prolazio kroz Math.Ceiling, pa se greška
/// zbrajala: 75 × 0,5 = 37,5 → 38; 38 × 1,25 = 47,5 → 48, iako je točan umnožak
/// 75 × 0,5 × 1,25 = 46,875.
///
/// Jedina iznimka je Tbr. 7. toč. 1, gdje Tarifa sama kaže "za svakih ZAPOČETIH
/// 133,00 eura" — to je zaokruživanje propisano tekstom, ne aritmetička greška.
/// </summary>
public class TarifaZaokruzivanjeTests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    /// <summary>
    /// Test iz audita. Tbr. 18. toč. 1 (pravni lijek u izvanparnici) = 50% nagrade
    /// iz Tbr. 16 uvećano za 25%. Pri VPS-u 1.000 € osnovica je 75 bodova:
    ///   75 × 0,5 × 1,25 = 46,875.
    /// Stari lanac je davao 48.
    /// </summary>
    [Fact]
    public void IzvanparnicniPravniLijek_Daje46875_NeNe48()
    {
        var rezultat = Izracunaj("izv_pravni_lijek", vps: 1_000m);

        Assert.Equal(46.875m, rezultat.UkupnoBodova);
        Assert.NotEqual(48m, rezultat.UkupnoBodova);
        Assert.Equal(93.75m, rezultat.UkupnoEurBezPdv);   // 46,875 × 2,00 €
    }

    /// <summary>
    /// Standardno zaokružena vrijednost tih bodova je 47 — nikako 48, kako je
    /// audit i tražio.
    /// </summary>
    [Fact]
    public void StandardnoZaokruzenoJe47()
    {
        var bodovi = Izracunaj("izv_pravni_lijek", vps: 1_000m).UkupnoBodova;

        Assert.Equal(47m, Math.Round(bodovi, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Množenje količinom radi na decimalnim bodovima, bez usputnog zaokruživanja.
    /// </summary>
    [Fact]
    public void Kolicina_MnoziDecimalneBodove()
    {
        var rezultat = Izracunaj("izv_pravni_lijek", vps: 1_000m, kolicina: 4);

        Assert.Equal(187.5m, rezultat.UkupnoBodova);      // 4 × 46,875
        Assert.Equal(375.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>
    /// Tbr. 7. toč. 1 se NE mijenja: "za svakih započetih 133,00 eura" je
    /// zaokruživanje koje Tarifa izričito propisuje.
    /// </summary>
    [Theory]
    [InlineData(66_361.01, 501)]
    [InlineData(66_494.00, 501)]
    [InlineData(66_494.01, 502)]
    public void Tbr71_ZadrzavaZaokruzivanjeNaZapocetiIznos(decimal vps, int ocekivano)
    {
        Assert.Equal(ocekivano, TroskovnikService.Tbr71Bodovi(vps));
    }

    /// <summary>
    /// Novac se zaokružuje jednom, na kraju, i to standardno — ne prema gore.
    /// </summary>
    [Fact]
    public void Novac_ZaokruzujeSeStandardno_NePremaGore()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 1_000m,
            IsPdvUkljucen = true,
            Stavke = [new TroskovnikItemRequest { ItemId = "izv_pravni_lijek", Kolicina = 1 }]
        });

        Assert.Equal(93.75m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(23.44m, rezultat.Pdv);              // 93,75 × 0,25 = 23,4375 → 23,44
        Assert.Equal(117.19m, rezultat.UkupnoEurSPdv);   // 93,75 + 23,44
    }

    /// <summary>
    /// Kazneni lanac također ostaje decimalan: Tbr. 5. toč. 2 daje 50% nagrade iz
    /// toč. 1, a toč. 1 je Tbr. 4. toč. 1 uvećan za 50% → 0,75 osnovice.
    /// </summary>
    [Fact]
    public void KazneniLanac_OstajeDecimalan()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = "novcana_do3",   // 100 bodova
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_istraga_ostecenik", Kolicina = 1 }]
        });

        Assert.Equal(50m, rezultat.UkupnoBodova);   // 100 × 0,5 — Tbr. 3. toč. 3
    }

    /// <summary>
    /// Opunomoćenik djeteta žrtve pri osnovici od 100 bodova daje 35 bodova —
    /// bez Math.Ceiling koji bi 35,0 ostavio, ali 17,5 podigao na 18.
    /// </summary>
    [Fact]
    public void ZastupnikDjeteta_NaNeparnojOsnovici_OstajeDecimalan()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = "do_10",          // 300 bodova
            IsZastupnikDjeteta = true,
            IsPoSluzbenojDuznosti = true,        // dodatnih 50%
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        });

        // 300 × 0,50 × 0,70 = 105; × 0,50 = 52,5
        Assert.Equal(52.5m, rezultat.UkupnoBodova);
    }
}
