using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 29 — poništaj upisa u registar.
///
/// Toč. 1: "Za sastavljanje tužbe na poništaj upisa u registar, za odgovore na tužbu i
/// obrazložene podneske o činjeničnim i pravnim navodima, odvjetniku pripada nagrada iz
/// Tbr. 7. toč. 1. kod tužbi na poništaj upisa u sudski registar, a ne manje od 100
/// bodova, odnosno po Tbr. 27. kod tužbi na poništaj upisa u drugi registar."
///
/// Toč. 2: "Za zastupanje na ročištu na kojem se raspravljalo o glavnoj stvari, ili su se
/// izvodili dokazi odvjetniku pripada nagrada iz toč. 1. ovog tarifnog broja, a za sva
/// ostala ročišta na kojima se raspravljalo samo o procesnim pitanjima, ili za ročište na
/// kojem se prije raspravljanja o glavnoj stvari postupak završio povlačenjem tužbe,
/// nagodbom ili presudom na osnovi priznanja odvjetniku pripada 50% nagrade iz toč. 1."
///
/// Toč. 3: "Za sastavljanje pravnih lijekova protiv presuda odvjetniku pripada nagrada iz
/// toč. 1. ovog tarifnog broja uvećana za 25%, a za sastavljanje pravnih lijekova protiv
/// rješenja odvjetniku pripada 50% nagrade iz toč. 1."
///
/// Toč. 1 daje tri osnovice (sudski registar s pragom od 100 bodova; drugi registar
/// procjenjiv i neprocjenjiv, oboje po Tbr. 27), toč. 2 i 3 daju pet radnji — ukupno 15
/// stavki, plus toč. 4 (skupština) = 16.
/// </summary>
public class TarifaTbr29Tests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps = 0m, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    // ── Prag od 100 bodova skalira se s postotkom ────────────────────

    /// <summary>
    /// SRŽ OVOG TARIFNOG BROJA. Prag "ne manje od 100 bodova" iz toč. 1 veže se uz nagradu
    /// iz toč. 1. Toč. 2 i 3 uzimaju POSTOTAK te nagrade, pa se s postotkom skalira i prag:
    /// 50% od "ne manje od 100" je "ne manje od 50", a ne "ne manje od 100".
    ///
    /// Motor primjenjuje MinBodova NAKON postotka (ClampBodovi), pa naivno upisan prag od
    /// 100 na svaku stavku daje krivo. Kako je prag konstanta, vrijedi
    /// max(x, 100) × p ≡ max(p·x, 100p), pa je ispravno upisati skalirani prag.
    ///
    /// Ručni izračun uz VPS 200 € (razred do 332,00 € → 25 bodova po Tbr. 7. toč. 1):
    ///   toč. 1                  → max(25, 100)          = 100 bod. = 200,00 €
    ///   toč. 2, ostala ročišta  → max(25 × 0,50, 50)    =  50 bod. = 100,00 €
    ///   toč. 3, protiv presude  → max(25 × 1,25, 125)   = 125 bod. = 250,00 €
    ///   toč. 3, protiv rješenja → max(25 × 0,50, 50)    =  50 bod. = 100,00 €
    /// </summary>
    [Theory]
    [InlineData("reg_ponistaj_tuzba_sudski", 100)]
    [InlineData("reg_ponistaj_rociste_glavno_sudski", 100)]
    [InlineData("reg_ponistaj_rociste_ostalo_sudski", 50)]
    [InlineData("reg_ponistaj_lijek_presuda_sudski", 125)]
    [InlineData("reg_ponistaj_lijek_rjesenje_sudski", 50)]
    public void SudskiRegistar_PragSeSkaliraSPostotkom(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId, 200m).UkupnoBodova);
    }

    /// <summary>
    /// Isto pravilo, izraženo kao odnos umjesto kao broj: toč. 2 i 3 moraju biti točan
    /// postotak stavke po toč. 1, i ispod praga i iznad njega. Ovo pada ako netko upiše
    /// prag od 100 na sve stavke.
    /// </summary>
    [Theory]
    [InlineData(200)]     // ispod praga — prag veže
    [InlineData(50_000)]  // iznad praga — prag ne veže
    public void SudskiRegistar_OstalaRocistaSuTocno50PostoOdTocke1(decimal vps)
    {
        var toc1 = Izracunaj("reg_ponistaj_tuzba_sudski", vps).UkupnoBodova;
        var ostala = Izracunaj("reg_ponistaj_rociste_ostalo_sudski", vps).UkupnoBodova;

        Assert.Equal(toc1 * 0.5m, ostala);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(50_000)]
    public void SudskiRegistar_PravniLijekProtivPresudeJeTocka1Uvecana25Posto(decimal vps)
    {
        var toc1 = Izracunaj("reg_ponistaj_tuzba_sudski", vps).UkupnoBodova;
        var lijek = Izracunaj("reg_ponistaj_lijek_presuda_sudski", vps).UkupnoBodova;

        Assert.Equal(toc1 * 1.25m, lijek);
    }

    /// <summary>
    /// Iznad praga sudski registar ide čisto po Tbr. 7. toč. 1.
    /// Ručni izračun uz VPS 50.000 € (razred 33.181,01–66.361,00 → 500 bodova):
    ///   toč. 1 = 500 · ostala ročišta = 250 · protiv presude = 625 · protiv rješenja = 250
    /// </summary>
    [Theory]
    [InlineData("reg_ponistaj_tuzba_sudski", 500)]
    [InlineData("reg_ponistaj_rociste_glavno_sudski", 500)]
    [InlineData("reg_ponistaj_rociste_ostalo_sudski", 250)]
    [InlineData("reg_ponistaj_lijek_presuda_sudski", 625)]
    [InlineData("reg_ponistaj_lijek_rjesenje_sudski", 250)]
    public void SudskiRegistar_IznadPragaIdePoTbr71(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId, 50_000m).UkupnoBodova);
    }

    // ── Drugi registar → Tbr. 27 ──────────────────────────────────────

    /// <summary>
    /// "odnosno po Tbr. 27. kod tužbi na poništaj upisa u drugi registar" —
    /// Tbr. 27. toč. 1 al. 1, procjenjivo: nagrada iz Tbr. 7. toč. 1, BEZ praga od 100
    /// (prag toč. 1 vezan je izrijekom uz sudski registar).
    ///
    /// Ručni izračun uz VPS 50.000 € → 500 bodova:
    ///   toč. 1 = 500 · ostala ročišta = 250 · protiv presude = 625 · protiv rješenja = 250
    /// </summary>
    [Theory]
    [InlineData("reg_ponistaj_tuzba_drugi_proc", 500)]
    [InlineData("reg_ponistaj_rociste_glavno_drugi_proc", 500)]
    [InlineData("reg_ponistaj_rociste_ostalo_drugi_proc", 250)]
    [InlineData("reg_ponistaj_lijek_presuda_drugi_proc", 625)]
    [InlineData("reg_ponistaj_lijek_rjesenje_drugi_proc", 250)]
    public void DrugiRegistarProcjenjiv_IdePoTbr71(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId, 50_000m).UkupnoBodova);
    }

    /// <summary>
    /// Prag od 100 bodova NE vrijedi za drugi registar — toč. 1 ga veže uz "tužbi na
    /// poništaj upisa u sudski registar". Uz VPS 200 € (25 bodova) tužba u drugi registar
    /// daje 25, a ne 100.
    /// </summary>
    [Fact]
    public void DrugiRegistarProcjenjiv_NemaPragOd100Bodova()
    {
        Assert.Equal(25, Izracunaj("reg_ponistaj_tuzba_drugi_proc", 200m).UkupnoBodova);
        Assert.Equal(100, Izracunaj("reg_ponistaj_tuzba_sudski", 200m).UkupnoBodova);
    }

    /// <summary>
    /// Tbr. 27. toč. 1 al. 2, neprocjenjivo: 500 bodova.
    /// Ručni izračun: toč. 1 = 500 · ostala ročišta = 250 · protiv presude = 500 × 1,25 =
    /// 625 · protiv rješenja = 250.
    /// </summary>
    [Theory]
    [InlineData("reg_ponistaj_tuzba_drugi_neproc", 500)]
    [InlineData("reg_ponistaj_rociste_glavno_drugi_neproc", 500)]
    [InlineData("reg_ponistaj_rociste_ostalo_drugi_neproc", 250)]
    [InlineData("reg_ponistaj_lijek_presuda_drugi_neproc", 625)]
    [InlineData("reg_ponistaj_lijek_rjesenje_drugi_neproc", 250)]
    public void DrugiRegistarNeprocjenjiv_ImaFiksnuOsnovicuOd500(string itemId, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, Izracunaj(itemId).UkupnoBodova);
    }

    [Theory]
    [InlineData("reg_ponistaj_tuzba_drugi_neproc")]
    [InlineData("reg_ponistaj_rociste_glavno_drugi_neproc")]
    [InlineData("reg_ponistaj_rociste_ostalo_drugi_neproc")]
    [InlineData("reg_ponistaj_lijek_presuda_drugi_neproc")]
    [InlineData("reg_ponistaj_lijek_rjesenje_drugi_neproc")]
    public void DrugiRegistarNeprocjenjiv_NeOvisiOVps(string itemId)
    {
        Assert.Equal(Izracunaj(itemId, 0m).UkupnoBodova, Izracunaj(itemId, 500_000m).UkupnoBodova);
    }

    // ── Toč. 4 — skupština trgovačkog društva ─────────────────────────

    /// <summary>
    /// Toč. 4: "Za zastupanje dioničara ili člana društva na skupštini trgovačkog društva,
    /// odvjetniku pripada nagrada prema dogovoru sa strankom, ali ne manje od 100 bodova
    /// za svaki započeti sat skupštine."
    ///
    /// Tarifa određuje samo DONJU granicu; gornju prepušta dogovoru sa strankom, pa sustav
    /// obračunava minimum. Ručni izračun: 3 sata × 100 bod. = 300 bod. = 600,00 €.
    /// </summary>
    [Fact]
    public void Skupstina_ObracunavaSePoZapocetomSatu()
    {
        var rezultat = Izracunaj("reg_skupstina", kolicina: 3);

        Assert.Equal(300, rezultat.UkupnoBodova);
        Assert.Equal(600.00m, rezultat.UkupnoEurBezPdv);
    }

    [Fact]
    public void Skupstina_ImaJedinicuSat()
    {
        Assert.Equal("sat", TroskovnikService.GetKatalog().Single(s => s.Id == "reg_skupstina").Jedinica);
    }

    // ── Katalog ───────────────────────────────────────────────────────

    /// <summary>Tbr. 29 mora dati točno 16 stavki, sve u registarskoj kategoriji.</summary>
    [Fact]
    public void Tbr29_Ima16StavkiURegistarskojKategoriji()
    {
        var stavke = TroskovnikService.GetKatalog()
            .Where(s => s.TarifniBroj.StartsWith("Tbr. 29"))
            .ToList();

        Assert.Equal(16, stavke.Count);
        Assert.All(stavke, s => Assert.Equal("Registarski postupak", s.Kategorija));
    }

    /// <summary>
    /// Svaka stavka mora u pravnom izvoru imenovati točku Tbr. 29 iz koje proizlazi —
    /// bez toga se iznos ne može provjeriti u Tarifi.
    /// </summary>
    [Fact]
    public void Tbr29_SvakaStavkaImaPravniIzvorSTockom()
    {
        var stavke = TroskovnikService.GetKatalog()
            .Where(s => s.TarifniBroj.StartsWith("Tbr. 29"));

        Assert.All(stavke, s => Assert.Contains("Tbr. 29. toč.", s.PravniIzvor));
    }
}
