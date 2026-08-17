using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// PDV je izbor korisnika, ne pretpostavka. Mnogi odvjetnici nisu u sustavu PDV-a,
/// pa troškovnik mora znati oba slučaja.
///
/// Zastavica `is_pdv_ukljucen` postojala je i prije ovog zadatka; ovi testovi je
/// zaključavaju da se ne izgubi pri budućim izmjenama.
/// </summary>
public class TarifaPdvTests
{
    private static TroskovnikResponse Izracunaj(bool? pdvUkljucen, bool hasVat = false)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,   // Tbr. 7. toč. 1 → 500 bodova = 1.000,00 €
            IsPdvUkljucen = pdvUkljucen,
            HasVat = hasVat,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

    [Fact]
    public void PdvUkljucen_ObracunavaSe()
    {
        var rezultat = Izracunaj(pdvUkljucen: true);

        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(250.00m, rezultat.Pdv);            // 25 %
        Assert.Equal(1_250.00m, rezultat.UkupnoEurSPdv);
    }

    [Fact]
    public void PdvIskljucen_NeObracunavaSe()
    {
        var rezultat = Izracunaj(pdvUkljucen: false);

        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(0m, rezultat.Pdv);
        Assert.Equal(1_000.00m, rezultat.UkupnoEurSPdv);
    }

    /// <summary>
    /// Odvjetnik izvan sustava PDV-a plaća samo sudsku pristojbu povrh nagrade.
    /// 1.000,00 € + 620,00 € = 1.620,00 € (Tar. br. 1 st. 1, NN 50/2026).
    /// </summary>
    [Fact]
    public void IzvanSustavaPdva_SveukupnoJeNagradaPlusPristojba()
    {
        var rezultat = Izracunaj(pdvUkljucen: false);

        Assert.Equal(620.00m, rezultat.SudskaPristojba);
        Assert.Equal(1_620.00m, rezultat.SveukupniTrosak);
    }

    /// <summary>Zastavica se vraća u odgovoru, da je sučelje i ispis mogu prikazati.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ZastavicaPutujeUOdgovor(bool pdvUkljucen)
    {
        Assert.Equal(pdvUkljucen, Izracunaj(pdvUkljucen).IsPdvUkljucen);
    }

    /// <summary>
    /// Kad novi `is_pdv_ukljucen` nije poslan, pada se natrag na stariji `has_vat`.
    /// </summary>
    [Theory]
    [InlineData(true, 250.00)]
    [InlineData(false, 0)]
    public void BezNovogFlaga_KoristiSeStariHasVat(bool hasVat, decimal ocekivaniPdv)
    {
        Assert.Equal(ocekivaniPdv, Izracunaj(pdvUkljucen: null, hasVat: hasVat).Pdv);
    }
}
