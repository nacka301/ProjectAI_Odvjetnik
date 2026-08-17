using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Predujam (npr. za vještačenje) plaća se u ime i za račun stranke — PROLAZNA stavka
/// po čl. 33. Zakona o PDV-u, analogno sudskoj pristojbi. Nikad se ne oporezuje i ne
/// ulazi u osnovicu; dodaje se u sveukupni trošak tek nakon PDV-a i sudske pristojbe.
/// </summary>
public class TarifaPredujamTests
{
    private static TroskovnikResponse Izracunaj(
        bool? pdvUkljucen,
        List<TroskovnikTrosakRequest>? predujmovi = null,
        List<NaknadaTroskovaStavkaRequest>? naknadaTroskova = null)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,   // Tbr. 7. toč. 1 → 500 bodova = 1.000,00 €
            IsPdvUkljucen = pdvUkljucen,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            Predujmovi = predujmovi ?? [],
            NaknadaTroskovaStavke = naknadaTroskova ?? []
        });

    [Fact]
    public void Predujam_NikadSeNeOporezuje()
    {
        var bezPredujma = Izracunaj(pdvUkljucen: true);
        var sPredujmom = Izracunaj(pdvUkljucen: true, predujmovi: [new TroskovnikTrosakRequest { Opis = "Predujam za vještačenje", Iznos = 500m }]);

        Assert.Equal(bezPredujma.Pdv, sPredujmom.Pdv);
        Assert.Equal(bezPredujma.UkupnoEurBezPdv, sPredujmom.UkupnoEurBezPdv);
        Assert.Equal(bezPredujma.UkupnoEurSPdv, sPredujmom.UkupnoEurSPdv);
    }

    [Fact]
    public void Predujam_UlaziSamoUSveukupniTrosak()
    {
        var rezultat = Izracunaj(pdvUkljucen: true, predujmovi: [new TroskovnikTrosakRequest { Opis = "Predujam za vještačenje", Iznos = 500m }]);

        Assert.Equal(500.00m, rezultat.UkupnoPredujmovi);
        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(1_250.00m, rezultat.UkupnoEurSPdv);
        // 1.250 (odvjetnički trošak s PDV) + 620,00 (sudska pristojba, NN 50/2026) + 500 (predujam)
        Assert.Equal(2_370.00m, rezultat.SveukupniTrosak);
    }

    /// <summary>
    /// Kombinacija svih slojeva: nagrada + naknada troškova ulaze u osnovicu i PDV;
    /// sudska pristojba i predujam su prolazne stavke, dodaju se nakon PDV-a.
    /// </summary>
    [Fact]
    public void Kombinacija_NagradaNaknadaPdvPristojbaPredujam()
    {
        var rezultat = Izracunaj(
            pdvUkljucen: true,
            predujmovi: [new TroskovnikTrosakRequest { Opis = "Predujam", Iznos = 100m }],
            naknadaTroskova: [new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.Prijevoz, Iznos = 200m }]);

        Assert.Equal(1_200.00m, rezultat.UkupnoEurBezPdv);   // 1.000 + 200
        Assert.Equal(300.00m, rezultat.Pdv);                 // 25% od 1.200
        Assert.Equal(1_500.00m, rezultat.UkupnoEurSPdv);
        Assert.Equal(620.00m, rezultat.SudskaPristojba);
        Assert.Equal(100.00m, rezultat.UkupnoPredujmovi);
        // 1.500 + 620,00 + 100 = 2.220,00
        Assert.Equal(2_220.00m, rezultat.SveukupniTrosak);
    }

    [Fact]
    public void NegativanIznos_BacaIznimku()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() =>
            Izracunaj(pdvUkljucen: true, predujmovi: [new TroskovnikTrosakRequest { Opis = "Predujam", Iznos = -50m }]));

        Assert.Contains("ne može biti negativan", ex.Message);
    }
}
