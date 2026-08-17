using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Dvije stope iz Pravilnika o visini nagrade za rad odvjetnika prilikom obrana po
/// službenoj dužnosti (NN 22/2022, br. 271). Obje idu od ISTE osnovice — "nagrade za rad
/// koja bi odvjetniku pripadala za pojedine radnje u postupku prema Tarifi" — pa se ne
/// množe međusobno.
///
/// 1. čl. 1 — obrana po službenoj dužnosti i obrana na teret proračunskih sredstava → 50%.
///    Tbr. 4. toč. 7 sam ne određuje iznos nego upućuje na taj Pravilnik.
///
/// 2. čl. 2 — opunomoćenik djeteta žrtve kaznenog djela → 70%. Čl. 2 se NE poziva na
///    Tbr. 4. toč. 8; ranijih 35% bio je artefakt množenja 0,50 × 0,70.
///
/// Uloge se usto isključuju: čl. 1 je obrana okrivljenika, čl. 2 zastupanje djeteta žrtve.
/// </summary>
public class TarifaUmanjenjaTests
{
    private static TroskovnikResponse Izracunaj(Action<TroskovnikRequest> podesi)
    {
        var zahtjev = new TroskovnikRequest
        {
            ZaprijecenaKazna = "dugotrajni",   // Tbr. 4. toč. 1 al. 5 → 500 bodova
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        };
        podesi(zahtjev);
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    [Fact]
    public void PunaTarifa_Je500Bodova()
    {
        Assert.Equal(500m, Izracunaj(_ => { }).UkupnoBodova);
    }

    // ── Branitelj po službenoj dužnosti / na teret proračuna: 50% ──

    [Fact]
    public void PoSluzbenojDuznosti_Je50PostoTarife()
    {
        Assert.Equal(250m, Izracunaj(z => z.IsPoSluzbenojDuznosti = true).UkupnoBodova);
    }

    [Fact]
    public void NaTeretProracuna_Je50PostoTarife()
    {
        Assert.Equal(250m, Izracunaj(z => z.IsNaTeretProracuna = true).UkupnoBodova);
    }

    [Fact]
    public void StopaUmanjenja_Je50Posto()
    {
        Assert.Equal(0.50m, TarifaConstants.KazneniReductionRate);
    }

    // ── Opunomoćenik djeteta žrtve: 70%, ne 35% ──

    /// <summary>
    /// Čl. 2 Pravilnika daje 70% nagrade po Tarifi za pojedinu radnju, bez ikakvog
    /// međukoraka preko Tbr. 4. toč. 8. 500 × 0,70 = 350 bodova.
    /// </summary>
    [Fact]
    public void OpunomocenikDjeteta_Je70PostoTarife_Ne35()
    {
        var rezultat = Izracunaj(z => z.IsZastupnikDjeteta = true);

        Assert.Equal(350m, rezultat.UkupnoBodova);    // 70% od 500
        Assert.NotEqual(175m, rezultat.UkupnoBodova); // 35% je bio umnožak 0,50 × 0,70
    }

    [Theory]
    [InlineData("novcana_do3", 70)]   // 100 → 70
    [InlineData("do_5", 140)]         // 200 → 140
    [InlineData("do_10", 210)]        // 300 → 210
    [InlineData("preko_10", 280)]     // 400 → 280
    [InlineData("dugotrajni", 350)]   // 500 → 350
    public void OpunomocenikDjeteta_JeUvijek70Posto(string kazna, decimal ocekivano)
    {
        var rezultat = Izracunaj(z =>
        {
            z.ZaprijecenaKazna = kazna;
            z.IsZastupnikDjeteta = true;
        });

        Assert.Equal(ocekivano, rezultat.UkupnoBodova);
    }

    [Fact]
    public void StopaZaDijeteZrtve_Je70Posto()
    {
        Assert.Equal(0.70m, TarifaConstants.ZastupnikDjetetaRate);
    }

    // ── Stope se ne množe ──

    /// <summary>
    /// Zastavice su na API-u i dalje tri odvojena boola, pa proturječan zahtjev može
    /// stići (stari klijent, pohranjeni redak). Servis tada primjenjuje najviše jednu
    /// stopu — nikad umnožak. Prije popravka ovo je davalo 500 × 0,35 × 0,50 = 87,5.
    /// </summary>
    [Fact]
    public void ObjeUloge_NeMnozeStope()
    {
        var rezultat = Izracunaj(z =>
        {
            z.IsZastupnikDjeteta = true;
            z.IsPoSluzbenojDuznosti = true;
        });

        Assert.Equal(350m, rezultat.UkupnoBodova);
        Assert.NotEqual(87.5m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Osnovicu za čl. 2 nosi sama stavka, ne tvrdi faktor u servisu: zastupanje
    /// oštećenika po Tbr. 4. toč. 8 vrijedi 250 bodova, pa je 70% od toga 175.
    /// Isti postotak, druga radnja, drugi iznos.
    /// </summary>
    [Fact]
    public void OpunomocenikDjeteta_UzStavkuTbr4t8_Je70PostoTeStavke()
    {
        var rezultat = Izracunaj(z =>
        {
            z.IsZastupnikDjeteta = true;
            z.Stavke = [new TroskovnikItemRequest { ItemId = "kaz_zastupanje_ostecenika", Kolicina = 1 }];
        });

        Assert.Equal(175m, rezultat.UkupnoBodova);   // 500 × 0,50 (Tbr. 4.8) × 0,70
    }
}
