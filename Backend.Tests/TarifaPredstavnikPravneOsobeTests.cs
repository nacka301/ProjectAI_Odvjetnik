using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 4. toč. 1 al. 10 — predstavnik pravne osobe kojeg u kaznenom postupku postavi sud:
/// 100 bodova za PRVI SAT SVAKOG DANA rasprave, a svaki sljedeći započeti sat 50 bodova.
///
/// „Prvi sat svakog dana" znači da se 100 bodova naplaćuje po DANU rasprave (jedinica je
/// „dan rasprave", količina = broj dana), pa dva dana rasprave nose 200 bodova, ne 100.
/// Sljedeći sati idu preko `kaz_dodatni_sat` (50 bodova po započetom satu).
/// </summary>
public class TarifaPredstavnikPravneOsobeTests
{
    private static TroskovnikResponse Izracunaj(params TroskovnikItemRequest[] stavke)
    {
        var zahtjev = new TroskovnikRequest
        {
            IsPdvUkljucen = false,
            Stavke = [.. stavke]
        };
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    [Fact]
    public void PredstavnikSud_JedinicaJeDanRasprave()
    {
        var stavka = TarifaKatalog.Sve.Single(s => s.Id == "kaz_predstavnik_sud");
        Assert.Equal("dan rasprave", stavka.Jedinica);
        Assert.Equal(100, stavka.FiksniBody);
    }

    [Fact]
    public void JedanDan_PrviSat_Je100Bodova()
    {
        var rezultat = Izracunaj(new TroskovnikItemRequest { ItemId = "kaz_predstavnik_sud", Kolicina = 1 });
        Assert.Equal(100m, rezultat.UkupnoBodova);
        Assert.Equal(200m, rezultat.UkupnoEurBezPdv);   // 100 × 2,00 €
    }

    [Fact]
    public void DvaDanaRasprave_Je200Bodova()
    {
        // 100 bodova za prvi sat svakog od dva dana rasprave.
        var rezultat = Izracunaj(new TroskovnikItemRequest { ItemId = "kaz_predstavnik_sud", Kolicina = 2 });
        Assert.Equal(200m, rezultat.UkupnoBodova);
    }

    [Fact]
    public void DvaDana_IJedanDodatniSat_Je250Bodova_500Eura()
    {
        // 2 dana rasprave (2 × 100) + 1 sljedeći započeti sat (1 × 50) = 250 bodova = 500,00 €.
        var rezultat = Izracunaj(
            new TroskovnikItemRequest { ItemId = "kaz_predstavnik_sud", Kolicina = 2 },
            new TroskovnikItemRequest { ItemId = "kaz_dodatni_sat", Kolicina = 1 });

        Assert.Equal(250m, rezultat.UkupnoBodova);
        Assert.Equal(500m, rezultat.UkupnoEurBezPdv);
    }
}
