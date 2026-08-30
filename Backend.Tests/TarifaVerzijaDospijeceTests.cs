using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 52 — mjerodavna Tarifa i vrijednost boda određuju se prema DOSPIJEĆU potraživanja,
/// ne prema datumu radnje. Danas postoji jedna verzija (HOK, 14.10.2023.), pa svaki datum
/// razrješava na nju; odabir po dospijeću provjerava se i sa sintetičkim popisom verzija,
/// da se logika selekcije testira neovisno o tome što je registar (za sada) jednočlan.
/// </summary>
public class TarifaVerzijaDospijeceTests
{
    // ── Čista selekcija: RazrijesiIz nad sintetičkim popisom od dvije verzije ──

    private static readonly IReadOnlyList<TarifaVerzija> DvijeVerzije =
    [
        new TarifaVerzija("Stara Tarifa", 1.50m, new DateOnly(2019, 1, 1)),
        new TarifaVerzija("Nova Tarifa", 2.00m, new DateOnly(2023, 10, 14)),
    ];

    [Fact]
    public void Dospijece_NakonNoveVerzije_BiraNovu()
    {
        var v = TarifaVerzije.RazrijesiIz(DvijeVerzije, new DateOnly(2024, 5, 1));
        Assert.Equal("Nova Tarifa", v.Naziv);
        Assert.Equal(2.00m, v.BodValue);
    }

    [Fact]
    public void Dospijece_IzmedjuVerzija_BiraStaru()
    {
        // Dospijeće nakon stare, prije nove → mjerodavna je stara verzija.
        var v = TarifaVerzije.RazrijesiIz(DvijeVerzije, new DateOnly(2020, 6, 1));
        Assert.Equal("Stara Tarifa", v.Naziv);
        Assert.Equal(1.50m, v.BodValue);
    }

    [Fact]
    public void Dospijece_PrijeNajstarije_BiraNajstariju()
    {
        var v = TarifaVerzije.RazrijesiIz(DvijeVerzije, new DateOnly(2000, 1, 1));
        Assert.Equal("Stara Tarifa", v.Naziv);
    }

    [Fact]
    public void Dospijece_Null_BiraTrenutnu()
    {
        var v = TarifaVerzije.RazrijesiIz(DvijeVerzije, null);
        Assert.Equal("Nova Tarifa", v.Naziv);
    }

    [Fact]
    public void NaGranici_VrijediOd_BiraTuVerziju()
    {
        // Na sam dan stupanja na snagu vrijedi već ta (nova) verzija.
        var v = TarifaVerzije.RazrijesiIz(DvijeVerzije, new DateOnly(2023, 10, 14));
        Assert.Equal("Nova Tarifa", v.Naziv);
    }

    // ── Stvarni registar: jedna verzija, bod 2,00 € (Tbr. 54) ──

    [Fact]
    public void Registar_ImaTrenutnuVerziju_Bod2Eura()
    {
        Assert.Equal(2.00m, TarifaVerzije.Trenutna.BodValue);
        Assert.Equal(TarifaConstants.BodValue, TarifaVerzije.Trenutna.BodValue);
    }

    [Fact]
    public void SvakiDatum_RazrjesavaNaJedinuVerziju()
    {
        Assert.Equal(TarifaVerzije.Trenutna, TarifaVerzije.Razrijesi(new DateOnly(2024, 3, 1)));
        Assert.Equal(TarifaVerzije.Trenutna, TarifaVerzije.Razrijesi(null));
    }

    // ── Integracija: dospijeće ne mijenja iznos dok postoji jedna verzija ──

    private static TroskovnikResponse Izracunaj(DateOnly? dospijece)
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            DatumDospijeca = dospijece,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        };
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    [Fact]
    public void DospijeceSeVracaKaoVrijednostBoda()
    {
        var rezultat = Izracunaj(new DateOnly(2024, 1, 1));
        Assert.Equal(2.00m, rezultat.VrijednostBoda);
    }

    [Fact]
    public void RazliciteDospijece_IstiIznos_JednaVerzija()
    {
        // Dok registar ima jednu verziju, promjena dospijeća ne mijenja iznos — ali kriterij
        // JE dospijeće (vidi čiste testove selekcije gore), pa je seam spreman za novu verziju.
        var a = Izracunaj(new DateOnly(2024, 1, 1)).UkupnoEurBezPdv;
        var b = Izracunaj(new DateOnly(2026, 9, 1)).UkupnoEurBezPdv;
        var c = Izracunaj(null).UkupnoEurBezPdv;
        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }
}
