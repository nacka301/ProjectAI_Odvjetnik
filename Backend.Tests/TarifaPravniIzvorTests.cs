using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Svaka stavka kataloga mora navesti svoj pravni izvor, i taj se izvor mora vidjeti
/// u sučelju — uz stavku i uz svaki redak troškovnika. Troškovnik koji ide sudu mora
/// biti provjerljiv, stavku po stavku.
/// </summary>
public class TarifaPravniIzvorTests
{
    [Fact]
    public void SvakaStavkaKataloga_ImaPravniIzvor()
    {
        var bezIzvora = TroskovnikService.DohvatiKatalog()
            .Where(s => string.IsNullOrWhiteSpace(s.PravniIzvor))
            .Select(s => s.Id)
            .ToList();

        Assert.True(bezIzvora.Count == 0,
            "Stavke bez pravnog izvora: " + string.Join(", ", bezIzvora));
    }

    /// <summary>
    /// Katalog piše tarifni broj skraćeno ("Tbr. 4.1"), a izvor punim oblikom
    /// ("Tbr. 4. toč. 1"). Uspoređuju se u normaliziranom obliku.
    /// </summary>
    private static string Normaliziraj(string tarifniBroj)
        => tarifniBroj.Replace("toč.", "").Replace(" ", "");

    [Fact]
    public void PravniIzvor_ImenujePropisIVlastitiTarifniBroj()
    {
        foreach (var stavka in TroskovnikService.DohvatiKatalog())
        {
            Assert.Contains("Tarifa o nagradama", stavka.PravniIzvor);
            Assert.Contains(Normaliziraj(stavka.TarifniBroj), Normaliziraj(stavka.PravniIzvor));
        }
    }

    /// <summary>
    /// Kad se nagrada izvodi iz drugog tarifnog broja, izvor to mora reći — inače
    /// odvjetnik ne može provjeriti odakle broj dolazi.
    /// </summary>
    [Theory]
    [InlineData("kaz_privatna_tuzba", "Tbr. 4. toč. 1")]        // Tbr. 1.1 → Tbr. 4.1
    [InlineData("kaz_prekrsaj_oduzimanje", "Tbr. 7. toč. 1")]   // Tbr. 4.1 al. 8 → Tbr. 7.1
    [InlineData("par_odgovor_tuzba", "Tbr. 7. toč. 1")]         // Tbr. 8.1 → Tbr. 7.1
    [InlineData("usp_rasprava_neproc", "Tbr. 27. toč. 1")]      // Tbr. 27.2 → toč. 1
    [InlineData("izv_pravni_lijek", "Tbr. 16")]                 // Tbr. 18.1 → Tbr. 16
    public void IzvedeneStavke_NavodeIzvorniTarifniBroj(string itemId, string ocekivaniUput)
    {
        var stavka = TroskovnikService.DohvatiKatalog().Single(s => s.Id == itemId);

        Assert.Contains(ocekivaniUput, stavka.PravniIzvor);
    }

    /// <summary>
    /// Izvor putuje u svaki redak troškovnika, ne samo u katalog.
    /// </summary>
    [Fact]
    public void SvakiRedakTroskovnika_NosiPravniIzvor()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 2 },
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }
            ]
        });

        Assert.Equal(3, rezultat.Stavke.Count);
        Assert.All(rezultat.Stavke, s => Assert.False(string.IsNullOrWhiteSpace(s.PravniIzvor)));
    }
}
