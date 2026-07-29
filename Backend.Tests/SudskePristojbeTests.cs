using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Uredba o tarifi sudskih pristojbi, NN 37/2023.
///
/// Prilog "TARIFA SUDSKIH PRISTOJBI", POGLAVLJE I. nosi naslov
/// "PARNIČNI I OVRŠNI POSTUPAK" — kazneni postupak u njemu ne postoji, pa se
/// pristojba po Tar. br. 1 u kaznenom predmetu uopće ne obračunava.
/// </summary>
public class SudskePristojbeTests
{
    private static TroskovnikResponse Izracunaj(decimal vps, params string[] itemIds)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            ZaprijecenaKazna = "do_5",
            Stavke = [.. itemIds.Select(id => new TroskovnikItemRequest { ItemId = id, Kolicina = 1 })]
        });

    // ── Tar. br. 1 st. 1 — puna pristojba u parničnom postupku ──

    [Theory]
    [InlineData(300, 13.27)]      // razred 1: do 398,17 €
    [InlineData(500, 26.54)]      // razred 2: do 796,34 €
    [InlineData(1_000, 39.82)]    // razred 3: do 1.194,51 €
    [InlineData(1_500, 53.09)]    // razred 4: do 1.592,67 €
    [InlineData(1_900, 66.36)]    // razred 5: do 1.990,84 €
    [InlineData(3_000, 76.45)]    // 66,36 + 1% × (3.000 − 1.990,84)
    [InlineData(50_000, 546.45)]  // 66,36 + 1% × 48.009,16
    [InlineData(500_000, 663.61)] // kapa iz Tar. br. 1 st. 1
    public void ParnicniPostupak_PunaPristojba(decimal vps, decimal ocekivano)
    {
        var rezultat = Izracunaj(vps, "par_tuzba");

        Assert.Equal(PristojbaOsnova.Puna, rezultat.PristojbaOsnova);
        Assert.Equal(ocekivano, rezultat.SudskaPristojba);
    }

    // ── Kazneni postupak — pristojba se ne obračunava ──

    /// <summary>
    /// Kazneni predmet s VPS-om 50.000 € prije popravka je davao 546,45 € pristojbe,
    /// iako Uredba za kazneni postupak nikakvu pristojbu ne propisuje.
    /// </summary>
    [Fact]
    public void KazneniPostupak_NemaPristojbe()
    {
        var rezultat = Izracunaj(50_000m, "kaz_obrana");

        Assert.Equal(PristojbaOsnova.Nema, rezultat.PristojbaOsnova);
        Assert.Equal(0m, rezultat.SudskaPristojba);
    }

    /// <summary>
    /// Razlog zbog kojeg je pristojba nula mora biti vidljiv u odgovoru — inače
    /// "0,00 €" izgleda kao greška ili kao oslobođenje po čl. 11. Zakona.
    /// </summary>
    [Fact]
    public void KazneniPostupak_RazlikujeSeOdOslobodjenja()
    {
        var kazneni = Izracunaj(50_000m, "kaz_obrana");

        var oslobodjen = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsOslobodjenPristojbi = true,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        Assert.Equal(0m, kazneni.SudskaPristojba);
        Assert.Equal(0m, oslobodjen.SudskaPristojba);

        Assert.Equal(PristojbaOsnova.Nema, kazneni.PristojbaOsnova);
        Assert.Equal(PristojbaOsnova.Puna, oslobodjen.PristojbaOsnova);  // osnova postoji, samo je oslobođen
        Assert.True(oslobodjen.IsOslobodjenPristojbi);
        Assert.False(kazneni.IsOslobodjenPristojbi);
    }

    // ── Tar. br. 1 st. 2 — polovica pristojbe u ovrsi ──

    /// <summary>
    /// Uredba NN 37/23, Tar. br. 1 st. 2: "Za tužbu s prijedlogom za izdavanje platnog
    /// naloga, prijedlog za ovrhu, protuovrhu i osiguranje, rješenje o ovrsi, protuovrsi
    /// i osiguranju, prijedlog za povrat u prijašnje stanje, prijedlog za osiguranje
    /// dokaza i odgovor na prijedlog za ponavljanje postupka, tužbu, žalbu, prijedlog za
    /// dopuštenje revizije i reviziju plaća se POLOVICA pristojbe iz stavka 1."
    /// </summary>
    [Theory]
    [InlineData(3_000, 38.23)]    // st. 1 = 76,4516 → polovica 38,2258 → 38,23
    [InlineData(300, 6.64)]       // st. 1 = 13,27 → polovica 6,635 → 6,64 (ne 6,63)
    [InlineData(50_000, 273.23)]  // st. 1 = 546,4516 → polovica 273,2258
    [InlineData(500_000, 331.81)] // st. 1 = kapa 663,61 → polovica 331,805 → 331,81
    public void OvrsniPostupak_PolovicaPristojbe(decimal vps, decimal ocekivano)
    {
        var rezultat = Izracunaj(vps, "ovr_prijedlog");

        Assert.Equal(PristojbaOsnova.Polovica, rezultat.PristojbaOsnova);
        Assert.Equal(ocekivano, rezultat.SudskaPristojba);
    }

    /// <summary>
    /// Kontrolni izračun testa iz audita, korak po korak:
    /// 66,36 + 1% × (3.000 − 1.990,84) = 66,36 + 10,0916 = 76,4516;
    /// polovica = 38,2258; zaokruženo standardno = 38,23.
    /// Bankarsko zaokruživanje (MidpointRounding.ToEven) dalo bi 38,22 — zato AwayFromZero.
    /// </summary>
    [Fact]
    public void Ovrha3000Eura_JeTocno3823()
    {
        Assert.Equal(76.45m, SudskePristojbeCalculator.Izracunaj(3_000m, PristojbaOsnova.Puna));
        Assert.Equal(38.23m, SudskePristojbeCalculator.Izracunaj(3_000m, PristojbaOsnova.Polovica));
    }

    /// <summary>
    /// Ovrha uz parničnu stavku u istom troškovniku nije čisti ovršni postupak —
    /// pristojba se tada obračunava po st. 1.
    /// </summary>
    [Fact]
    public void OvrhaSParnicnomStavkom_PlacaPunuPristojbu()
    {
        var rezultat = Izracunaj(3_000m, "ovr_prijedlog", "par_tuzba");

        Assert.Equal(PristojbaOsnova.Puna, rezultat.PristojbaOsnova);
        Assert.Equal(76.45m, rezultat.SudskaPristojba);
    }

    [Fact]
    public void MjesovitiPredmet_ImaPristojbu()
    {
        var rezultat = Izracunaj(50_000m, "kaz_obrana", "par_tuzba");

        Assert.Equal(PristojbaOsnova.Puna, rezultat.PristojbaOsnova);
        Assert.Equal(546.45m, rezultat.SudskaPristojba);
    }
}
