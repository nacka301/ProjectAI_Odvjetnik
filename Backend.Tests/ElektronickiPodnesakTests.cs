using Backend.Models;
using Backend.Services;
using Backend.Services.Pristojbe;

namespace Backend.Tests;

/// <summary>
/// Čl. 7 Zakona o sudskim pristojbama (NN 118/18, 51/23) — umanjenje na polovicu za
/// elektronički oblik.
///
/// Tri stavka, tri različita uvjeta:
///   st. 1 — stranka je PODNIJELA elektronički i platila pri podnošenju
///   st. 2 — SUD je dostavio odluku elektronički i plaćeno je u roku od 3 dana
///   st. 3 — stranka je predložila ZK UPIS elektronički i platila pri podnošenju;
///           tada se prepolovljuju i podnesci i odluke u tom postupku
///   st. 4 — propušten rok vraća puni iznos iz Tarife
///
/// Kontrolni izračun: tužba uz VPS 50.000 € je 620,00 € (NN 50/2026, Tar. br. 1 st. 1),
/// pa je polovica 310,00 €.
/// </summary>
public class ElektronickiPodnesakTests
{
    private static TroskovnikResponse Izracunaj(string itemId, Action<TroskovnikRequest> podesi)
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        };

        podesi(zahtjev);
        return new TroskovnikService().Izracunaj(zahtjev);
    }

    private static PristojbenaStavkaDto Redak(TroskovnikResponse odgovor, string odredbaId)
        => Assert.Single(odgovor.PristojbeneStavke, s => s.OdredbaId == odredbaId);

    // ── St. 1 — podnesci ──────────────────────────────────────────

    /// <summary>Kontrolni izračun F iz revizije: 620,00 → 310,00.</summary>
    [Fact]
    public void St1_Tuzba_Polovica()
    {
        var r = Izracunaj("par_tuzba", z => z.PodnesciElektronicki = true);

        Assert.Equal(310.00m, r.SudskaPristojba);
        Assert.True(Redak(r, "tbr1_st1").UmanjenoElektronicki);
    }

    /// <summary>Bez oznake ostaje puni iznos — to je i st. 4 kad rok nije ispunjen.</summary>
    [Fact]
    public void St1_BezOznake_PuniIznos()
    {
        var r = Izracunaj("par_tuzba", _ => { });

        Assert.Equal(620.00m, r.SudskaPristojba);
        Assert.False(Redak(r, "tbr1_st1").UmanjenoElektronicki);
    }

    /// <summary>
    /// Umanjenje ide na PROPISANI iznos, dakle nakon uvećanja iz Tar. br. 3 st. 1:
    /// 1.240,00 → 620,00. Ne obrnutim redom, gdje bi ispalo 310,00 × 2.
    /// </summary>
    [Fact]
    public void St1_ZalbaProtivPresude_PolovicaOdUvecanog()
    {
        var r = Izracunaj("par_zalba_presuda", z => z.PodnesciElektronicki = true);

        Assert.Equal(620.00m, r.SudskaPristojba);
    }

    /// <summary>
    /// Umanjenje se slaže s lancem uputa iz Uredbe: VPS 3.000 € → st. 1 je 150,00 €,
    /// st. 2 je polovica toga (75,00 €), a čl. 7 polovica od toga — 37,50 €.
    /// Slučaj u kojem zaokruživanje stvarno bira između dva iznosa pokriva
    /// <see cref="StaraUredba_TakodjerSeUmanjuje"/>.
    /// </summary>
    [Fact]
    public void St1_ZaokruzujeSeJednom()
    {
        var r = Izracunaj("par_odgovor_tuzba", z =>
        {
            z.VrijednostSpora = 3_000m;   // st. 1 = 150,00 → st. 2 = 75,00
            z.PodnesciElektronicki = true;
        });

        Assert.Equal(37.50m, r.SudskaPristojba);
    }

    /// <summary>Upravni spor je fiksnih 110,00 € — podnesak je, pa se i on prepolovljuje.</summary>
    [Fact]
    public void St1_UpravniSpor_TakodjerPodnesak()
    {
        var r = Izracunaj("usp_tuzba_proc", z => z.PodnesciElektronicki = true);

        Assert.Equal(55.00m, r.SudskaPristojba);
    }

    // ── St. 2 — odluke ────────────────────────────────────────────

    /// <summary>
    /// Odluka se ne umanjuje prekidačem za podneske: uvjet je da ju je SUD dostavio
    /// elektronički i da je plaćena u roku od tri dana.
    /// </summary>
    [Fact]
    public void St2_PrekidacZaPodneskeNeDiraOdluku()
    {
        var r = Izracunaj("par_rociste_presuda", z => z.PodnesciElektronicki = true);

        Assert.Equal(620.00m, r.SudskaPristojba);
        Assert.False(Redak(r, "tbr2_st1").UmanjenoElektronicki);
    }

    /// <summary>Uz oznaku po retku odluka se prepolovljuje.</summary>
    [Fact]
    public void St2_OznakaPoRetku_Polovica()
    {
        var r = Izracunaj("par_rociste_presuda", z => z.OdlukeElektronicki = ["tbr2_st1"]);

        Assert.Equal(310.00m, r.SudskaPristojba);
        Assert.True(Redak(r, "tbr2_st1").UmanjenoElektronicki);
    }

    /// <summary>
    /// Zašto po retku, a ne po troškovniku: rok od tri dana može se ispuniti za jednu
    /// odluku, a za drugu ne. Tužba (podnesak) ostaje na svom prekidaču.
    /// </summary>
    [Fact]
    public void St2_JednaOdlukaUmanjena_DrugaNije()
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_rociste_presuda", Kolicina = 1 }
            ],
            OdlukeElektronicki = ["tbr2_st1"]
        };

        var r = new TroskovnikService().Izracunaj(zahtjev);

        Assert.Equal(620.00m, Redak(r, "tbr1_st1").Iznos);   // podnesak, bez oznake
        Assert.Equal(310.00m, Redak(r, "tbr2_st1").Iznos);   // odluka, s oznakom
        Assert.Equal(930.00m, r.SudskaPristojba);
    }

    // ── St. 3 — zemljišnoknjižni ──────────────────────────────────

    /// <summary>
    /// Zk prijedlog se ne umanjuje prekidačem za podneske: st. 3 traži da je sam UPIS
    /// predložen elektronički, što je zaseban uvjet.
    /// </summary>
    [Fact]
    public void St3_PrekidacZaPodneskeNeDiraZk()
    {
        var r = Izracunaj("zk_redoviti", z => z.PodnesciElektronicki = true);

        Assert.Equal(10.00m, r.SudskaPristojba);
    }

    [Fact]
    public void St3_ZkUpisElektronicki_Polovica()
    {
        var r = Izracunaj("zk_redoviti", z => z.ZkUpisElektronicki = true);

        Assert.Equal(5.00m, r.SudskaPristojba);
        Assert.True(Redak(r, "tbr14_upis").UmanjenoElektronicki);
    }

    /// <summary>
    /// St. 3 pokriva i odluke u zk postupku — za razliku od st. 1, koji vrijedi samo
    /// za podneske. Rješenje o uknjižbi je 30,00 € → 15,00 €.
    /// </summary>
    [Fact]
    public void St3_PokrivaIOdlukeUZkPostupku()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m,
                [],
                oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr15_uknjizba")],
                    ZkUpisElektronicki: true))
            .Ukupno;

        Assert.Equal(15.00m, iznos);
    }

    // ── Doseg ─────────────────────────────────────────────────────

    /// <summary>
    /// Ovjera preslike (Tar. br. 17 i 18) stoji u zk poglavlju, ali nije ni podnesak ni
    /// odluka, pa je čl. 7 ne dira ni uz uključen zk prekidač.
    /// </summary>
    [Fact]
    public void OvjeraPreslike_NijePodnesakNiOdluka()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m,
                [],
                oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr17_18")],
                    PodnesciElektronicki: true,
                    ZkUpisElektronicki: true))
            .Ukupno;

        Assert.Equal(5.00m, iznos);
    }

    /// <summary>
    /// Doplata za podnesak u FIZIČKOM obliku (Tar. br. 26) ne smije se umanjiti za
    /// elektronički oblik — odredba postoji upravo zato što podnesak nije elektronički.
    /// </summary>
    [Fact]
    public void FizickiOblik_NeUmanjujeSe()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m,
                [],
                oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr26_fizicki")],
                    PodnesciElektronicki: true))
            .Ukupno;

        Assert.Equal(30.00m, iznos);
    }

    /// <summary>
    /// Prijelazni retci po staroj Uredbi (čl. 2 NN 50/2026) također se umanjuju —
    /// čl. 7 je u Zakonu, ne u Uredbi, pa ne ovisi o tome koja se Uredba primjenjuje.
    /// Odgovor na tužbu prije 1.6.2026. je 273,23 € → 136,62 €.
    /// </summary>
    [Fact]
    public void StaraUredba_TakodjerSeUmanjuje()
    {
        var r = Izracunaj("par_odgovor_tuzba", z =>
        {
            z.DatumPodnosenjaOdgovoraNaTuzbu = new DateOnly(2026, 3, 12);
            z.PodnesciElektronicki = true;
        });

        Assert.Equal(136.61m, r.SudskaPristojba);
        Assert.Equal("NN 37/2023", Redak(r, "tbr1_st2").Uredba);
    }

    /// <summary>Umanjeni redak mora na ispisu reći po kojem je stavku umanjen.</summary>
    [Fact]
    public void UmanjeniRedak_NosiPravniIzvorUmanjenja()
    {
        var izvor = Redak(Izracunaj("par_tuzba", z => z.PodnesciElektronicki = true), "tbr1_st1")
            .PravniIzvor;

        Assert.Contains("čl. 7", izvor);
        Assert.Contains("st. 1", izvor);
    }

    // ── Zaštita razvrstavanja ─────────────────────────────────────

    /// <summary>
    /// Svaka odredba mora biti izrijekom razvrstana. Nerazvrstana tiho pada na `Ostalo`
    /// i nikad se ne umanjuje — što je za novu odredbu gotovo sigurno pogrešno, a ne
    /// vidi se ni u jednom drugom testu.
    /// </summary>
    [Fact]
    public void SvakaOdredbaJeRazvrstana()
    {
        // Odredbe koje stvarno nisu ni podnesak ni odluka; popis je namjerno doslovan,
        // da dodavanje nove odredbe u tablicu sruši ovaj test dok se ne razvrsta.
        var stvarnoOstalo = new HashSet<string>
        {
            "tbr2_st9", "tbr8_tijelo", "tbr8_tumac", "tbr13_sastavljanje", "tbr13_cuvanje",
            "tbr17_18", "tbr24", "tbr26_fizicki", "tbr28_preslika", "tbr28_potvrda",
            "tbr31_uvjerenje", "tbr31_egradani", "tbr32", "tbr33", "tbr35_pohrana"
        };

        var nerazvrstane = PristojbeTarifaNn50_2026.Sve
            .Where(o => o.Vrsta == VrstaRadnje.Ostalo && !stvarnoOstalo.Contains(o.Id))
            .Select(o => o.Id)
            .ToList();

        Assert.Empty(nerazvrstane);
    }

    /// <summary>Poglavlje IV je čl. 7 st. 3 — i za podneske i za odluke.</summary>
    [Fact]
    public void ZemljisnoknjizniPodnesciIOdluke_IduPoSt3()
    {
        var zk = PristojbeTarifaNn50_2026.Sve
            .Where(o => o.Poglavlje == PristojbaPoglavlje.Zemljisnoknjizni
                        && o.Vrsta != VrstaRadnje.Ostalo);

        Assert.All(zk, o => Assert.Equal(Cl7Rezim.St3Zemljisnoknjizni, o.Cl7Rezim));
    }
}
