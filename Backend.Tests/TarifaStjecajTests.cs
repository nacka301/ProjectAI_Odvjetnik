using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 4. toč. 1 al. 6 — kaznena djela u stjecaju i produljena kaznena djela.
///
/// "za kaznena djela u stjecaju i za produljena kaznena djela nagrada se određuje
/// sukladno rasponima iz alineja od 1. do 5. ovog tarifnog broja, pri čemu se kao
/// predviđena kazna smatra NAJTEŽA KAZNA koja se u postupku može izreći primjenom
/// odredbi kaznenog zakonodavstva o izricanju kazne za kaznena djela u stjecaju i
/// za produljena kaznena djela."
///
/// Alineja ne uvodi nikakav množitelj ni broj radnji — ona samo govori KOJI se
/// razred iz alineja 1–5 uzima. Nagrada ostaje jedna, po tom razredu.
/// </summary>
public class TarifaStjecajTests
{
    private static TroskovnikResponse Izracunaj(string kazna, bool stjecaj)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = kazna,
            IsStjecaj = stjecaj,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        });

    /// <summary>
    /// Stjecaj ne mijenja iznos — mijenja samo koji razred odvjetnik mora odabrati.
    /// Ako bi zastavica množila nagradu, uveli bismo broj kojeg u Tarifi nema.
    /// </summary>
    [Theory]
    [InlineData("novcana_do3", 100)]
    [InlineData("do_5", 200)]
    [InlineData("do_10", 300)]
    [InlineData("preko_10", 400)]
    [InlineData("dugotrajni", 500)]
    public void Stjecaj_NeMnoziNagradu(string kazna, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj(kazna, stjecaj: false).UkupnoBodova);
        Assert.Equal(ocekivano, Izracunaj(kazna, stjecaj: true).UkupnoBodova);
    }

    /// <summary>
    /// Odabir razreda je ono što stjecaj mijenja: dva djela do 5 godina mogu, uz
    /// primjenu pravila o stjecaju, dati najtežu kaznu preko 10 godina — pa odvjetnik
    /// bira razred "preko 10", a ne "do 5".
    /// </summary>
    [Fact]
    public void Stjecaj_RazredOdredujeNajtezaKazna()
    {
        Assert.Equal(200m, Izracunaj("do_5", stjecaj: true).UkupnoBodova);
        Assert.Equal(400m, Izracunaj("preko_10", stjecaj: true).UkupnoBodova);
    }

    /// <summary>
    /// Zastavica mora putovati u odgovor, da se u troškovniku vidi po čemu je razred
    /// odabran — inače je odabir "preko 10 godina" za dva lakša djela neobjašnjiv.
    /// </summary>
    [Fact]
    public void Stjecaj_PutujeUOdgovorSObrazlozenjem()
    {
        var rezultat = Izracunaj("preko_10", stjecaj: true);

        Assert.True(rezultat.IsStjecaj);
        Assert.False(string.IsNullOrWhiteSpace(rezultat.NapomenaStjecaj));
        Assert.Contains("najtež", rezultat.NapomenaStjecaj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tbr. 4. toč. 1", rezultat.NapomenaStjecaj);
    }

    [Fact]
    public void BezStjecaja_NemaNapomene()
    {
        var rezultat = Izracunaj("do_5", stjecaj: false);

        Assert.False(rezultat.IsStjecaj);
        Assert.Null(rezultat.NapomenaStjecaj);
    }
}
