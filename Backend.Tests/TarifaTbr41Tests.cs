using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 41 — povisivanje i snižavanje tarifnih stavaka.
///
/// Toč. 1: "Za posebno stručno i specijalističko znanje, koje zahtijeva obrađeni predmet,
/// osobitu težinu predmeta ili posebnu odgovornost za obavljeni rad, tarifne stavke mogu
/// se povisiti za 100%."
///
/// Toč. 2: "Uzimajući u obzir sve okolnosti obavljenog posla i koristi koje ima stranka,
/// pojedine tarifne stavke odvjetnik može sniziti za 50%."
///
/// „Mogu se povisiti" — dakle nikad automatski. Oba postotka su zadano 0, pa je bez
/// korisnikova zahvata iznos identičan onome prije uvođenja ove odredbe.
/// </summary>
public class TarifaTbr41Tests
{
    private static TroskovnikResponse Izracunaj(
        decimal povisenje, decimal snizenje, decimal vps = 20_000m, int brojOsoba = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Tbr41PovisenjePostotak = povisenje,
            Tbr41SnizenjePostotak = snizenje,
            BrojZastupanihOsoba = brojOsoba,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

    /// <summary>
    /// RUČNI IZRAČUN — osnovica 250 bodova (VPS 20.000), jedna tužba:
    ///     0 % →  250
    ///   +50 % →  375
    ///  +100 % →  500
    ///   −50 % →  125
    ///   −25 % →  187,5   (bodovi ostaju decimalni — v. TarifaZaokruzivanjeTests)
    /// </summary>
    [Theory]
    [InlineData(  0,  0, 250)]
    [InlineData( 50,  0, 375)]
    [InlineData(100,  0, 500)]
    [InlineData(  0, 50, 125)]
    [InlineData(  0, 25, 187.5)]
    public void PovisenjeISnizenje(decimal povisenje, decimal snizenje, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj(povisenje, snizenje).UkupnoBodova);
    }

    [Fact]
    public void ZadanoJeNula_PaIznosOstajeNepromijenjen()
    {
        var r = Izracunaj(0, 0);

        Assert.Equal(0m, r.Tbr41PovisenjePostotak);
        Assert.Equal(0m, r.Tbr41SnizenjePostotak);
        Assert.Equal(0m, r.Tbr41Bodovi);
        Assert.Equal(250m, r.UkupnoBodova);
    }

    /// <summary>
    /// Tarifa ne dopušta povišenje iznad 100 % ni sniženje ispod 50 % — vrijednosti
    /// izvan raspona se ograničavaju, a ne primjenjuju.
    /// </summary>
    [Theory]
    [InlineData(500,  0, 500)]   // povišenje ograničeno na 100 %
    [InlineData(  0, 90, 125)]   // sniženje ograničeno na 50 %
    [InlineData(-50,  0, 250)]   // negativno povišenje se ignorira
    public void IzvanRasponaSeOgranicava(decimal povisenje, decimal snizenje, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj(povisenje, snizenje).UkupnoBodova);
    }

    /// <summary>
    /// Tbr. 40 i Tbr. 41 djeluju na isti zbroj i zbrajaju se — Tarifa ih navodi kao
    /// povišenja odnosno sniženja tarifnih stavaka, ne kao uzastopne množitelje.
    ///
    /// RUČNI IZRAČUN: 250 × (1 + 0,20 + 1,00 − 0,50) = 250 × 1,70 = 425 bodova.
    ///   Tbr. 40: 3 osobe → +20 %
    ///   Tbr. 41. toč. 1  → +100 %
    ///   Tbr. 41. toč. 2  → −50 %
    /// </summary>
    [Fact]
    public void Tbr40_iTbr41_SeZbrajaju()
    {
        var r = Izracunaj(povisenje: 100, snizenje: 50, brojOsoba: 3);

        Assert.Equal(425m, r.UkupnoBodova);
    }

    /// <summary>
    /// Sudska pristojba nije odvjetnička nagrada, pa je Tbr. 41 ne dira.
    /// </summary>
    [Fact]
    public void NeUvecavaSudskuPristojbu()
    {
        var bez = Izracunaj(0, 0);
        var sa = Izracunaj(100, 0);

        Assert.Equal(bez.SudskaPristojba, sa.SudskaPristojba);
        Assert.True(sa.UkupnoEurBezPdv > bez.UkupnoEurBezPdv);
    }
}
