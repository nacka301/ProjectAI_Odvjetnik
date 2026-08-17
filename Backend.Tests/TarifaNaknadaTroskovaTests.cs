using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Naknada troškova (Tbr. 48.-51.: prijevoz, dnevnica, hotel, ostali izdaci) je vlastiti
/// trošak odvjetnika, izvan bodovnog sustava — stvarni iznos koji korisnik unosi ručno.
/// Za razliku od sudske pristojbe i predujma, ULAZI u PDV osnovicu (čl. 33. Zakona o
/// PDV-u; čl. 78. Direktive 2006/112/EZ — usputni troškovi koje dobavljač zaračuna kupcu
/// ulaze u osnovicu).
/// </summary>
public class TarifaNaknadaTroskovaTests
{
    private static TroskovnikResponse Izracunaj(
        bool? pdvUkljucen,
        List<NaknadaTroskovaStavkaRequest>? naknadaTroskova = null,
        int brojZastupanihOsoba = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,   // Tbr. 7. toč. 1 → 500 bodova = 1.000,00 €
            IsPdvUkljucen = pdvUkljucen,
            BrojZastupanihOsoba = brojZastupanihOsoba,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            NaknadaTroskovaStavke = naknadaTroskova ?? []
        });

    [Fact]
    public void NaknadaTroskova_UlaziUOsnovicuIUPdv()
    {
        var rezultat = Izracunaj(
            pdvUkljucen: true,
            naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Prijevoz, Iznos = 200.00m }]);

        Assert.Equal(1_200.00m, rezultat.UkupnoEurBezPdv);   // 1.000 nagrada + 200 naknada
        Assert.Equal(300.00m, rezultat.Pdv);                 // 25% od 1.200
        Assert.Equal(1_500.00m, rezultat.UkupnoEurSPdv);
        Assert.Equal(200.00m, rezultat.NaknadaTroskovaUkupno);
    }

    [Fact]
    public void NaknadaTroskova_UlaziUOsnovicuIKadPdvNijeUkljucen()
    {
        var rezultat = Izracunaj(
            pdvUkljucen: false,
            naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Dnevnica, Iznos = 150.00m }]);

        Assert.Equal(1_150.00m, rezultat.UkupnoEurBezPdv);   // naknada troškova nije uvjetovana PDV-om
        Assert.Equal(0m, rezultat.Pdv);
        Assert.Equal(1_150.00m, rezultat.UkupnoEurSPdv);
    }

    [Fact]
    public void PraznaLista_JednakoKaoBezNaknade()
    {
        var rezultat = Izracunaj(pdvUkljucen: true, naknadaTroskova: []);

        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(250.00m, rezultat.Pdv);
        Assert.Equal(0m, rezultat.NaknadaTroskovaUkupno);
    }

    [Fact]
    public void EchoStavke_NosiPravniIzvor()
    {
        var rezultat = Izracunaj(
            pdvUkljucen: true,
            naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.HotelskiSmjestaj, Opis = "Hotel Split", Iznos = 80m }]);

        var stavka = Assert.Single(rezultat.NaknadaTroskovaStavke);
        Assert.Equal(VrstaTroska.HotelskiSmjestaj, stavka.Vrsta);
        Assert.Equal("Hotel Split", stavka.Opis);
        Assert.Equal(80m, stavka.Iznos);
        Assert.Contains("Tbr. 51", stavka.PravniIzvor);
    }

    [Fact]
    public void NepoznataVrsta_BacaIznimku()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(pdvUkljucen: true, naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = "avion_privatni", Iznos = 100m }]));

        Assert.Contains("Nepoznata vrsta troška", ex.Message);
    }

    [Fact]
    public void NegativanIznos_BacaIznimku()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(pdvUkljucen: true, naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.OstaliIzdaci, Iznos = -10m }]));

        Assert.Contains("ne može biti negativan", ex.Message);
    }

    /// <summary>
    /// Tbr. 40. toč. 1: dijeli se NAGRADA, ne osnovica proširena naknadom troškova —
    /// naknada troškova nije odvjetnička nagrada.
    /// </summary>
    [Fact]
    public void NagradaPoOsobi_NeMijenjaSeNaknadomTroskova()
    {
        var bezNaknade = Izracunaj(pdvUkljucen: true, brojZastupanihOsoba: 2);
        var sNaknadom = Izracunaj(
            pdvUkljucen: true,
            brojZastupanihOsoba: 2,
            naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Prijevoz, Iznos = 200m }]);

        Assert.Equal(bezNaknade.NagradaPoOsobi, sNaknadom.NagradaPoOsobi);
        Assert.Equal(bezNaknade.BodoviPoOsobi, sNaknadom.BodoviPoOsobi);
    }
}
