using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 40. toč. 1, drugi odlomak:
/// "Iznos povišene nagrade dijeli se na jednake dijelove prema broju branjenika,
/// odnosno zastupanih osoba."
///
/// Dioba ne mijenja ukupnu nagradu — određuje koliko od nje otpada na svaku
/// zastupanu osobu. Bez toga troškovnik ne pokazuje što pojedini branjenik duguje.
/// </summary>
public class TarifaTbr40DiobaTests
{
    /// <summary>Tužba pri VPS-u 50.000 € = 500 bodova po Tbr. 7. toč. 1.</summary>
    private static TroskovnikResponse ZaOsoba(int brojOsoba, bool pdv = false)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            BrojZastupanihOsoba = brojOsoba,
            IsPdvUkljucen = pdv,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

    [Theory]
    // 1 osoba: bez povišenja, 500 bodova = 1.000,00 €
    [InlineData(1, 500, 500, 1_000.00)]
    // 2 osobe: +10 % → 550 bodova = 1.100,00 €; po osobi 275 bod. = 550,00 €
    [InlineData(2, 550, 275, 550.00)]
    // 4 osobe: +30 % → 650 bodova = 1.300,00 €; po osobi 162,5 bod. = 325,00 €
    [InlineData(4, 650, 162.5, 325.00)]
    // 5 osoba: +40 % → 700 bodova = 1.400,00 €; po osobi 140 bod. = 280,00 €
    [InlineData(5, 700, 140, 280.00)]
    // 8 osoba: kapa +50 % → 750 bodova = 1.500,00 €; po osobi 93,75 bod. = 187,50 €
    [InlineData(8, 750, 93.75, 187.50)]
    public void PovisenaNagradaSeDijeliNaJednakeDijelove(
        int brojOsoba, decimal ukupnoBodova, decimal bodoviPoOsobi, decimal nagradaPoOsobi)
    {
        var rezultat = ZaOsoba(brojOsoba);

        Assert.Equal(ukupnoBodova, rezultat.UkupnoBodova);
        Assert.Equal(bodoviPoOsobi, rezultat.BodoviPoOsobi);
        Assert.Equal(nagradaPoOsobi, rezultat.NagradaPoOsobi);
    }

    /// <summary>Dioba ne smije promijeniti ukupan iznos — samo ga raspoređuje.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(8)]
    public void Dioba_NeMijenjaUkupanIznos(int brojOsoba)
    {
        var rezultat = ZaOsoba(brojOsoba);

        Assert.Equal(rezultat.UkupnoEurBezPdv, rezultat.NagradaPoOsobi * brojOsoba);
        Assert.Equal(rezultat.UkupnoBodova, rezultat.BodoviPoOsobi * brojOsoba);
    }

    /// <summary>
    /// Dijeli se nagrada, ne sveukupni trošak — PDV i sudska pristojba nisu
    /// odvjetnička nagrada i ne ulaze u diobu po Tbr. 40.
    /// </summary>
    [Fact]
    public void Dioba_ObuhvacaSamoNagradu_NePdvNiPristojbu()
    {
        var rezultat = ZaOsoba(2, pdv: true);

        Assert.Equal(1_100.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(275.00m, rezultat.Pdv);
        Assert.Equal(620.00m, rezultat.SudskaPristojba);   // Tar. br. 1 st. 1, NN 50/2026

        // Po osobi je polovica nagrade, ne polovica sveukupnog troška.
        Assert.Equal(550.00m, rezultat.NagradaPoOsobi);
        Assert.NotEqual(rezultat.SveukupniTrosak / 2m, rezultat.NagradaPoOsobi);
    }

    /// <summary>Jedna osoba — dioba je bespredmetna, iznos po osobi je cijeli iznos.</summary>
    [Fact]
    public void JednaOsoba_NagradaPoOsobiJeCijelaNagrada()
    {
        var rezultat = ZaOsoba(1);

        Assert.Equal(rezultat.UkupnoEurBezPdv, rezultat.NagradaPoOsobi);
    }

    /// <summary>
    /// Tbr. 41 mijenja ukupnu nagradu, pa mijenja i iznos po osobi.
    /// 500 bodova, +10 % (Tbr. 40) i +100 % (Tbr. 41) = 500 + 50 + 500 = 1.050 bodova;
    /// 2.100,00 € podijeljeno na dvoje = 1.050,00 € po osobi.
    /// </summary>
    [Fact]
    public void Tbr41_UlaziUIznosPoOsobi()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            BrojZastupanihOsoba = 2,
            Tbr41PovisenjePostotak = 100m,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        Assert.Equal(1_050m, rezultat.UkupnoBodova);
        Assert.Equal(2_100.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(1_050.00m, rezultat.NagradaPoOsobi);
    }
}
