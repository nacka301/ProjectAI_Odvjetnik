using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Uloga zastupanja u kaznenom i prekršajnom postupku. Vrijednost boda 2,00 €, PDV 25%,
/// zapriječena kazna do 5 god. → Tbr. 4. toč. 1 = 200 bodova za prvi sat.
///
/// Punomoćnik oštećenika (DORH progoni) ima 50% nagrade iz Tbr. 3 (toč. 3) i Tbr. 4 (toč. 8);
/// Tbr. 5 (žalbe) nema umanjenja; branitelj, privatni tužitelj i oštećenik kao tužitelj
/// primaju punu nagradu.
/// </summary>
public class TarifaUlogaZastupanjaTests
{
    private static TroskovnikResponse Izracunaj(
        string uloga, string itemId = "kaz_obrana", int kolicina = 1, Action<TroskovnikRequest>? podesi = null)
    {
        var zahtjev = new TroskovnikRequest
        {
            ZaprijecenaKazna = "do_5",
            UlogaZastupanja = uloga,
            IsPdvUkljucen = true,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        };
        podesi?.Invoke(zahtjev);
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    [Fact]
    public void Branitelj_RaspravaJedanSat_Je500EuraSPdv()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj);
        Assert.Equal(200m, r.UkupnoBodova);
        Assert.Equal(400.00m, r.UkupnoEurBezPdv);
        Assert.Equal(100.00m, r.Pdv);
        Assert.Equal(500.00m, r.UkupnoEurSPdv);
    }

    [Theory]
    [InlineData(UlogaZastupanja.PrivatniTuzitelj)]
    [InlineData(UlogaZastupanja.OstecenikKaoTuzitelj)]
    public void Tuzitelj_RaspravaJedanSat_JePunaNagrada(string uloga)
    {
        var r = Izracunaj(uloga);
        Assert.Equal(200m, r.UkupnoBodova);
        Assert.Equal(500.00m, r.UkupnoEurSPdv);
        Assert.Equal("", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void PunomocnikOstecenika_RaspravaJedanSat_Je250EuraSPdv()
    {
        var r = Izracunaj(UlogaZastupanja.PunomocnikOstecenika);
        Assert.Equal(100m, r.UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);              // PDV na umanjenu nagradu
        Assert.Equal(250.00m, r.UkupnoEurSPdv);
        Assert.Equal("umanjeno 50% (Tbr. 4 t. 8 Tarife)", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void PunomocnikOstecenika_Zalba_JePunaNagrada()
    {
        // Tbr. 5. toč. 1: Tbr. 4.1 + 50% = 300 bodova; Tarifa ne navodi umanjenje za oštećenika.
        var r = Izracunaj(UlogaZastupanja.PunomocnikOstecenika, "kaz_zalba_presuda");
        Assert.Equal(300m, r.UkupnoBodova);
        Assert.Equal("", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void Branitelj_PoSluzbenojDuznosti_Je50PostoPoPravilniku()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, podesi: z => z.IsPoSluzbenojDuznosti = true);
        Assert.Equal(100m, r.UkupnoBodova);
        Assert.Equal(250.00m, r.UkupnoEurSPdv);
    }

    [Fact]
    public void IzabraniBranitelj_NaTeretProracuna_JePunaNagrada()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, podesi: z =>
        {
            z.IsNaTeretProracuna = true;
            z.IsIzabraniOslobodjajuca = true;
        });
        Assert.Equal(200m, r.UkupnoBodova);
        Assert.Equal(500.00m, r.UkupnoEurSPdv);
    }

    [Theory]
    [InlineData(UlogaZastupanja.PunomocnikOstecenika)]
    [InlineData(UlogaZastupanja.PrivatniTuzitelj)]
    [InlineData(UlogaZastupanja.OstecenikKaoTuzitelj)]
    public void PoSluzbenojDuznosti_UzDrugeUlogeOdBranitelja_JeValidacijskaGreska(string uloga)
    {
        Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(uloga, podesi: z => z.IsPoSluzbenojDuznosti = true));
    }

    [Fact]
    public void PunomocnikOstecenika_UzOpunomocenikaZrtve_NemaAutomatskogIznosa()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(UlogaZastupanja.PunomocnikOstecenika, podesi: z => z.IsOpunomocenikZrtve = true));
        Assert.Contains("ne nudi automatski iznos", ex.Message);
    }

    [Fact]
    public void PunomocnikOstecenika_IstragaDrugiSat_Je25Bodova()
    {
        // Tbr. 3. toč. 1: svaki sljedeći započeti sat = 50 bodova; Tbr. 3. toč. 3 → 50%.
        var r = Izracunaj(UlogaZastupanja.PunomocnikOstecenika, "kaz_istraga_dodatni_sat");
        Assert.Equal(25m, r.UkupnoBodova);
        Assert.Equal("umanjeno 50% (Tbr. 3 t. 3 Tarife)", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void PunomocnikOstecenika_StavkaKojaVecNosiUmanjenje_NeUmanjujeSeDvaput()
    {
        // kaz_zastupanje_ostecenika u katalogu već ima −50% (Tbr. 4.8).
        var r = Izracunaj(UlogaZastupanja.PunomocnikOstecenika, "kaz_zastupanje_ostecenika");
        Assert.Equal(100m, r.UkupnoBodova);
    }

    [Fact]
    public void NepoznataUloga_JeValidacijskaGreska()
    {
        Assert.Throws<TroskovnikValidationException>(() => Izracunaj("NEPOZNATO"));
    }

    [Fact]
    public void BezUloge_Servis_RacunaKaoBranitelj()
    {
        var r = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = "do_5",
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        });
        Assert.Equal(200m, r.UkupnoBodova);
    }

    // ── Naziv radnje po ulozi i napomene na retku ──

    [Theory]
    [InlineData(UlogaZastupanja.PunomocnikOstecenika, "Zastupanje oštećenika na raspravi")]
    [InlineData(UlogaZastupanja.OstecenikKaoTuzitelj, "Zastupanje oštećenika kao tužitelja na raspravi")]
    [InlineData(UlogaZastupanja.PrivatniTuzitelj, "Zastupanje privatnog tužitelja na raspravi")]
    public void NazivRadnje_ZaOstecenikaITuzitelja_NijeObrana(string uloga, string pocetak)
    {
        var opis = Izracunaj(uloga).Stavke[0].Opis;
        Assert.StartsWith(pocetak, opis);
        Assert.DoesNotContain("Obrana", opis);
    }

    [Fact]
    public void NazivRadnje_ZaBranitelja_OstajeObrana()
    {
        Assert.StartsWith("Obrana okrivljenika na raspravi", Izracunaj(UlogaZastupanja.Branitelj).Stavke[0].Opis);
    }

    [Theory]
    [InlineData(UlogaZastupanja.Branitelj)]
    [InlineData(UlogaZastupanja.PrivatniTuzitelj)]
    [InlineData(UlogaZastupanja.OstecenikKaoTuzitelj)]
    public void OpunomocenikZrtve_UzDrugeUlogeOdOstecenika_JeValidacijskaGreska(string uloga)
    {
        Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj(uloga, podesi: z => z.IsOpunomocenikZrtve = true));
    }

    [Fact]
    public void NapomenaNaRetku_Pravilnik_ZaSluzbenuDuznost()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, podesi: z => z.IsPoSluzbenojDuznosti = true);
        Assert.Equal("umanjeno 50% (Pravilnik NN 22/22, čl. 1)", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void NapomenaNaRetku_IzabraniBranitelj_NemaNapomene()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, podesi: z =>
        {
            z.IsNaTeretProracuna = true;
            z.IsIzabraniOslobodjajuca = true;
        });
        Assert.Equal("", r.Stavke[0].UmanjenjeUloge);
    }

    // ── Pravilnik NN 22/22 vrijedi na SVAKU radnju, i fiksnu (Miladinović) ──

    [Theory]
    [InlineData("kaz_istraga_dodatni_sat", 25)]
    [InlineData("kaz_dodatni_sat", 25)]
    [InlineData("kaz_razgovor_pritvor", 25)]
    [InlineData("kaz_obrana_prekrsaj", 50)]
    [InlineData("kaz_zalba_troskovi", 25)]
    public void SluzbenaDuznost_Umanjuje_I_FiksneStavke(string itemId, decimal ocekivaniBodovi)
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, itemId, podesi: z => z.IsPoSluzbenojDuznosti = true);
        Assert.Equal(ocekivaniBodovi, r.UkupnoBodova);
        Assert.Equal("umanjeno 50% (Pravilnik NN 22/22, čl. 1)", r.Stavke[0].UmanjenjeUloge);
    }

    [Fact]
    public void IzabraniBranitelj_NaTeretProracuna_FiksnaStavkaOstajePuna()
    {
        var r = Izracunaj(UlogaZastupanja.Branitelj, "kaz_dodatni_sat", podesi: z =>
        {
            z.IsNaTeretProracuna = true;
            z.IsIzabraniOslobodjajuca = true;
        });
        Assert.Equal(50m, r.UkupnoBodova);
    }

    [Fact]
    public void SluzbenaDuznost_ParnicnaStavka_NeDiraSe()
    {
        var r = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 1000m, IsPoSluzbenojDuznosti = true, IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });
        Assert.Equal("", r.Stavke[0].UmanjenjeUloge);
    }
}
