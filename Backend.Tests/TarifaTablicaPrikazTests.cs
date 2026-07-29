using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tablica razreda mora se prikazivati točno kako stoji u Tbr. 7. toč. 1:
///
///   OD EURA      DO EURA        BODOVA
///   0            332,00         25
///   332,01       664,00         50
///   664,01       1.327,00       75
///   ...
///
/// Granice se ne preklapaju — sljedeći razred počinje jedan cent iznad prethodnog.
/// Prije popravka prikazivalo se "0 – 332,00" pa "332,00 – 664,00", pa je 332,00
/// izgledalo kao da pripada u oba razreda.
/// </summary>
public class TarifaTablicaPrikazTests
{
    [Fact]
    public void GraniceSeNePreklapaju()
    {
        var razredi = TroskovnikDocumentService.RazrediZaPrikaz;

        for (var i = 1; i < razredi.Count; i++)
        {
            var prethodniDo = razredi[i - 1].Do;
            Assert.NotNull(prethodniDo);

            Assert.Equal(prethodniDo!.Value + 0.01m, razredi[i].Od);
        }
    }

    [Fact]
    public void PrviRazredPocinjeNaNuli()
    {
        Assert.Equal(0m, TroskovnikDocumentService.RazrediZaPrikaz[0].Od);
    }

    [Fact]
    public void PosljednjiRazredJeNeogranicen()
    {
        var zadnji = TroskovnikDocumentService.RazrediZaPrikaz[^1];

        Assert.Null(zadnji.Do);
        Assert.Equal(1_327_228.01m, zadnji.Od);
        Assert.Contains("10.000", zadnji.Bodovi);
    }

    /// <summary>
    /// Prikazane granice moraju odgovarati onome što motor doista računa.
    /// </summary>
    [Fact]
    public void PrikazaneGraniceOdgovarajuIzracunu()
    {
        var razredi = TroskovnikDocumentService.RazrediZaPrikaz;

        // Prvih šest razreda ima jedan broj bodova — provjeri oba ruba svakog.
        for (var i = 0; i < 6; i++)
        {
            var (od, doIznos, bodovi) = razredi[i];
            var ocekivano = int.Parse(bodovi);

            Assert.Equal(ocekivano, TroskovnikService.Tbr71Bodovi(od));
            Assert.Equal(ocekivano, TroskovnikService.Tbr71Bodovi(doIznos!.Value));
        }
    }

    /// <summary>
    /// Progresivni razredi ispisuju formulu iz Tarife, ne izmišljen broj.
    /// </summary>
    [Theory]
    [InlineData(6, "133,00")]
    [InlineData(7, "265,00")]
    [InlineData(8, "664,00")]
    public void ProgresivniRazredi_NavodeKorakIzTarife(int indeks, string korak)
    {
        Assert.Contains(korak, TroskovnikDocumentService.RazrediZaPrikaz[indeks].Bodovi);
    }
}
