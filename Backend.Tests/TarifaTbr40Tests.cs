using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 40. toč. 1 — zastupanje više osoba.
///
/// Tarifa: "Kad odvjetnik u istom postupku brani ili zastupa više osoba, pripada mu
/// pravo na povišenje tarifnih stavaka za DRUGU I SVAKU SLJEDEĆU osobu za 10% — uz
/// uvjet da to povišenje može iznositi NAJVIŠE 50%."
///
/// Prva osoba, dakle, ne ulazi u postotak, a povišenje je ograničeno na 50%.
///
/// Prije popravka Tbr. 40 je u katalogu postojao kao stavka s FiksniBody = 0,
/// pa je tiho doprinosio nulu bez obzira na broj osoba.
/// </summary>
public class TarifaTbr40Tests
{
    /// <summary>Tužba pri VPS-u 50.000 € = 500 bodova po Tbr. 7. toč. 1.</summary>
    private static TroskovnikResponse ZaOsoba(int brojOsoba)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            BrojZastupanihOsoba = brojOsoba,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

    [Theory]
    [InlineData(1, 0.00, 0)]     // jedna osoba — nema povišenja
    [InlineData(2, 0.10, 50)]    // druga osoba → +10%
    [InlineData(3, 0.20, 100)]
    [InlineData(4, 0.30, 150)]
    [InlineData(5, 0.40, 200)]
    [InlineData(6, 0.50, 250)]   // šesta osoba doseže kapu
    [InlineData(8, 0.50, 250)]   // kapa drži
    [InlineData(20, 0.50, 250)]
    public void Povisenje_ZaDruguISvakuSljedecuOsobu(int brojOsoba, decimal ocekivanPostotak, decimal ocekivanoBodova)
    {
        var rezultat = ZaOsoba(brojOsoba);

        Assert.Equal(ocekivanPostotak, rezultat.PovisenjeViseOsobaPostotak);
        Assert.Equal(ocekivanoBodova, rezultat.PovisenjeViseOsobaBodovi);
        Assert.Equal(500m + ocekivanoBodova, rezultat.UkupnoBodova);
        Assert.Equal((500m + ocekivanoBodova) * 2.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>
    /// Neispravan ili nepostavljen broj osoba tretira se kao jedna osoba.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NevaljanBrojOsoba_NemaPovisenja(int brojOsoba)
    {
        var rezultat = ZaOsoba(brojOsoba);

        Assert.Equal(0m, rezultat.PovisenjeViseOsobaPostotak);
        Assert.Equal(500m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Povišenje se odnosi na "tarifne stavke", dakle na cijelu osnovicu troškovnika,
    /// a ne na pojedinu stavku.
    /// </summary>
    [Fact]
    public void Povisenje_PrimjenjujeSeNaCijeluOsnovicu()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            BrojZastupanihOsoba = 3,   // +20%
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },        // 500
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 2 } // 2 × 500
            ]
        });

        Assert.Equal(300m, rezultat.PovisenjeViseOsobaBodovi);   // 20% od 1.500
        Assert.Equal(1_800m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Stara stavka zaj_vise_osoba s FiksniBody = 0 više ne smije postojati u katalogu.
    /// </summary>
    [Fact]
    public void StaraStavkaSNulaBodova_JeUklonjena()
    {
        Assert.DoesNotContain(TroskovnikService.GetKatalog(), s => s.Id == "zaj_vise_osoba");
    }

    /// <summary>
    /// Sudska pristojba se ne dira povišenjem — Tbr. 40 je odredba Tarife, ne Uredbe.
    /// </summary>
    [Fact]
    public void Povisenje_NeUtjeceNaSudskuPristojbu()
    {
        Assert.Equal(ZaOsoba(1).SudskaPristojba, ZaOsoba(6).SudskaPristojba);
    }
}
