using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 4 — odredbe koje u katalogu nisu postojale.
///
/// toč. 2 al. 4: "na sjednici povodom izvanrednih pravnih lijekova odvjetniku pripada
///                nagrada kao za obranu pred sudom drugog stupnja."
/// toč. 3:       "U postupku prema maloljetniku i u postupku prema osobama s duševnim
///                smetnjama, odvjetniku kao branitelju pripada nagrada kao za obranu
///                po toč. 1. ovog tarifnog broja."
/// toč. 4:       "(…) došlo do obustave postupka, ili do odgode rasprave, odvjetniku
///                pripada 50% nagrade iz toč. 1. ovog tarifnog broja (…)"
/// toč. 5:       "(…) nagrada od 50 bodova, te za drugi i svaki sljedeći započeti sat
///                nagrada od 50 bodova."
/// toč. 6:       "Za prisustvovanje očevidu, rekonstrukciji, kao i ostalim radnjama,
///                koje (…) provode izvan zgrade svog sjedišta, odvjetniku pripada
///                nagrada iz toč. 1. ovog tarifnog broja kao i naknada po Tbr. 49."
/// toč. 8:       "Odvjetniku koji zastupa oštećenika (…) pripada 50% nagrade iz ovog
///                tarifnog broja."
/// </summary>
public class TarifaTbr4DopuneTests
{
    private static TroskovnikResponse Izracunaj(string itemId, string kazna, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = kazna,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    // ── toč. 2 al. 4 — sjednica povodom izvanrednih pravnih lijekova ──

    /// <summary>
    /// "Kao za obranu pred sudom drugog stupnja" — a to je po al. 3 nagrada iz toč. 1
    /// uvećana za 25%, odnosno za 50% kod dugotrajnog zatvora.
    /// do 5 godina: 200 × 1,25 = 250 bodova = 500,00 €.
    /// </summary>
    [Theory]
    [InlineData("novcana_do3", 125)]   // 100 × 1,25
    [InlineData("do_5", 250)]          // 200 × 1,25
    [InlineData("do_10", 375)]         // 300 × 1,25
    [InlineData("preko_10", 500)]      // 400 × 1,25
    [InlineData("dugotrajni", 750)]    // 500 × 1,50
    public void Toc2Al4_SjednicaIzvanredniPravniLijekovi(string kazna, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj("kaz_sjednica_izvanredni", kazna).UkupnoBodova);
    }

    [Fact]
    public void Toc2Al4_JednakaJeRaspraviPredDrugimStupnjem()
    {
        Assert.Equal(
            Izracunaj("kaz_rasprava_drugostupanjska", "do_10").UkupnoBodova,
            Izracunaj("kaz_sjednica_izvanredni", "do_10").UkupnoBodova);
    }

    // ── toč. 3 — maloljetnici i osobe s duševnim smetnjama ──

    [Theory]
    [InlineData("novcana_do3", 100)]
    [InlineData("do_5", 200)]
    [InlineData("do_10", 300)]
    [InlineData("preko_10", 400)]
    [InlineData("dugotrajni", 500)]
    public void Toc3_Maloljetnik_JeKaoObranaPoToc1(string kazna, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj("kaz_maloljetnik", kazna).UkupnoBodova);
    }

    [Fact]
    public void Toc3_JednakaJeObraniNaRaspravi()
    {
        Assert.Equal(
            Izracunaj("kaz_obrana", "do_5").UkupnoBodova,
            Izracunaj("kaz_maloljetnik", "do_5").UkupnoBodova);
    }

    // ── toč. 4 — obustava postupka ili odgoda rasprave ──

    /// <summary>do 10 godina: 50% od 300 = 150 bodova = 300,00 €.</summary>
    [Theory]
    [InlineData("novcana_do3", 50)]
    [InlineData("do_5", 100)]
    [InlineData("do_10", 150)]
    [InlineData("preko_10", 200)]
    [InlineData("dugotrajni", 250)]
    public void Toc4_ObustavaIliOdgoda_Je50Posto(string kazna, decimal ocekivano)
    {
        var rezultat = Izracunaj("kaz_obustava_odgoda", kazna);

        Assert.Equal(ocekivano, rezultat.UkupnoBodova);
        Assert.Equal(ocekivano * 2.00m, rezultat.UkupnoEurBezPdv);
    }

    // ── toč. 5 — razgovor u pritvoru, svaki sljedeći započeti sat ──

    [Fact]
    public void Toc5_PrviRazgovor_Je50Bodova()
    {
        Assert.Equal(50m, Izracunaj("kaz_razgovor_pritvor", "dugotrajni").UkupnoBodova);
    }

    /// <summary>Dva dodatna sata × 50 = 100 bodova = 200,00 €.</summary>
    [Fact]
    public void Toc5_SvakiSljedeciSat_Je50Bodova()
    {
        var rezultat = Izracunaj("kaz_pritvor_dodatni_sat", "dugotrajni", kolicina: 2);

        Assert.Equal(100m, rezultat.UkupnoBodova);
        Assert.Equal(200.00m, rezultat.UkupnoEurBezPdv);
    }

    [Fact]
    public void Toc5_DodatniSat_NeOvisiOZaprijecenojKazni()
    {
        Assert.Equal(
            Izracunaj("kaz_pritvor_dodatni_sat", "novcana_do3").UkupnoBodova,
            Izracunaj("kaz_pritvor_dodatni_sat", "dugotrajni").UkupnoBodova);
    }

    // ── toč. 6 — očevid, rekonstrukcija i ostale radnje izvan zgrade ──

    [Theory]
    [InlineData("do_5", 200)]
    [InlineData("dugotrajni", 500)]
    public void Toc6_Ocevid_JeNagradaIzToc1(string kazna, decimal ocekivano)
    {
        Assert.Equal(ocekivano, Izracunaj("kaz_ocevid_izvan_zgrade", kazna).UkupnoBodova);
    }

    [Fact]
    public void Toc6_UpucujeNaTbr49()
    {
        var def = TroskovnikService.GetKatalog().Single(s => s.Id == "kaz_ocevid_izvan_zgrade");

        Assert.Contains("Tbr. 49", def.PravniIzvor);
        Assert.Contains("Tbr. 49", def.Opis);
    }

    // ── toč. 8 — zastupanje oštećenika ──

    /// <summary>preko 10 godina: 50% od 400 = 200 bodova = 400,00 €.</summary>
    [Theory]
    [InlineData("novcana_do3", 50)]
    [InlineData("do_5", 100)]
    [InlineData("do_10", 150)]
    [InlineData("preko_10", 200)]
    [InlineData("dugotrajni", 250)]
    public void Toc8_ZastupanjeOstecenika_Je50Posto(string kazna, decimal ocekivano)
    {
        var rezultat = Izracunaj("kaz_zastupanje_ostecenika", kazna);

        Assert.Equal(ocekivano, rezultat.UkupnoBodova);
        Assert.Equal(ocekivano * 2.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>
    /// Opunomoćenik djeteta žrtve dobiva 70% od te nagrade (Pravilnik NN 22/2022
    /// br. 271), dakle 35% pune tarife — ta se putanja i dalje uključuje zastavicom
    /// i ne smije se pomiješati s ovom stavkom.
    /// </summary>
    [Fact]
    public void Toc8_RazlikujeSeOdOpunomocenikaDjeteta()
    {
        var ostecenik = Izracunaj("kaz_zastupanje_ostecenika", "dugotrajni");

        var dijete = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = "dugotrajni",
            IsZastupnikDjeteta = true,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        });

        Assert.Equal(250m, ostecenik.UkupnoBodova);   // 50% od 500
        Assert.Equal(175m, dijete.UkupnoBodova);      // 35% od 500
    }

    // ── Pokrivenost ──

    /// <summary>
    /// Sve točke Tbr. 4 koje propisuju nagradu moraju imati barem jednu stavku.
    /// toč. 7 (branitelj po službenoj dužnosti) je zastavica zahtjeva, ne stavka.
    /// </summary>
    [Theory]
    [InlineData("Tbr. 4.1")]
    [InlineData("Tbr. 4.2")]
    [InlineData("Tbr. 4.3")]
    [InlineData("Tbr. 4.4")]
    [InlineData("Tbr. 4.5")]
    [InlineData("Tbr. 4.6")]
    [InlineData("Tbr. 4.8")]
    public void SvakaTockaTbr4_ImaBaremJednuStavku(string tarifniBroj)
    {
        Assert.Contains(TroskovnikService.GetKatalog(), s => s.TarifniBroj == tarifniBroj);
    }

    /// <summary>
    /// Deset alineja iz Tbr. 4. toč. 1 — svaka mora biti dohvatljiva.
    /// Alineje 1–5 su razredi kazne, al. 6 je stjecaj (zastavica), al. 7–10 su stavke.
    /// </summary>
    [Theory]
    [InlineData("kaz_obrana")]                     // al. 1–5, razredi kazne
    [InlineData("kaz_obrana_prekrsaj")]            // al. 7, 100 bodova
    [InlineData("kaz_prekrsaj_oduzimanje")]        // al. 8, po Tbr. 7. toč. 1
    [InlineData("kaz_obrana_pravna_osoba")]        // al. 9
    [InlineData("kaz_predstavnik_sud")]            // al. 10, prvi dio
    [InlineData("kaz_predstavnik_pravna_osoba")]   // al. 10, drugi dio
    [InlineData("kaz_dodatni_sat")]                // završna rečenica toč. 1
    public void SvakaAlinejaToc1_ImaStavku(string itemId)
    {
        Assert.Contains(TroskovnikService.GetKatalog(), s => s.Id == itemId);
    }
}
