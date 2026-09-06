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
        var r = Izracunaj("par_tuzba", z => z.PodnesciElektronicki = ["tbr1_st1"]);

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
        var r = Izracunaj("par_zalba_presuda", z => z.PodnesciElektronicki = ["tbr3_st1"]);

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
            z.PodnesciElektronicki = ["tbr1_st2"];
        });

        Assert.Equal(37.50m, r.SudskaPristojba);
    }

    /// <summary>Upravni spor je fiksnih 110,00 € — podnesak je, pa se i on prepolovljuje.</summary>
    [Fact]
    public void St1_UpravniSpor_TakodjerPodnesak()
    {
        var r = Izracunaj("usp_tuzba_proc", z => z.PodnesciElektronicki = ["tbr29_tuzba"]);

        Assert.Equal(55.00m, r.SudskaPristojba);
    }

    /// <summary>
    /// Zašto po retku, a ne po troškovniku: u istom predmetu žalba može biti predana
    /// elektronički, a tužba na papiru. VPS 45.300 → tužba (tbr1_st1) 573,00 €, žalba
    /// (tbr3_st1) 1.146,00 €. S oznakom samo na žalbi: tužba ostaje 573,00, žalba 573,00.
    /// </summary>
    [Fact]
    public void St1_JedanPodnesakElektronicki_DrugiNije()
    {
        var zahtjev = new TroskovnikRequest
        {
            VrijednostSpora = 45_300m,
            IsPdvUkljucen = false,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }
            ],
            PodnesciElektronicki = ["tbr3_st1"]
        };

        var r = new TroskovnikService().Izracunaj(zahtjev);

        Assert.Equal(573.00m, Redak(r, "tbr1_st1").Iznos);   // tužba, bez oznake — puni iznos
        Assert.False(Redak(r, "tbr1_st1").UmanjenoElektronicki);
        Assert.Equal(573.00m, Redak(r, "tbr3_st1").Iznos);   // žalba, s oznakom — polovica od 1.146,00
        Assert.True(Redak(r, "tbr3_st1").UmanjenoElektronicki);
        Assert.Equal(1_146.00m, r.SudskaPristojba);
    }

    // ── St. 2 — odluke ────────────────────────────────────────────

    /// <summary>
    /// Odluka se ne umanjuje prekidačem za podneske: uvjet je da ju je SUD dostavio
    /// elektronički i da je plaćena u roku od tri dana.
    /// </summary>
    [Fact]
    public void St2_PrekidacZaPodneskeNeDiraOdluku()
    {
        // Oznaka za podneske stoji i na ključu odluke — čl. 7 st. 1 vrijedi samo za
        // podneske, pa presuda svejedno ostaje na punom iznosu.
        var r = Izracunaj("par_rociste_presuda", z => z.PodnesciElektronicki = ["tbr2_st1"]);

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
        var r = Izracunaj("zk_redoviti", z => z.PodnesciElektronicki = ["tbr14_upis"]);

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
                    PodnesciElektronicki: new HashSet<string> { "tbr17_18" },
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
                    PodnesciElektronicki: new HashSet<string> { "tbr26_fizicki" }))
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
            z.PodnesciElektronicki = ["tbr1_st2"];
        });

        Assert.Equal(136.61m, r.SudskaPristojba);
        Assert.Equal("NN 37/2023", Redak(r, "tbr1_st2").Uredba);
    }

    /// <summary>Umanjeni redak mora na ispisu reći po kojem je stavku umanjen.</summary>
    [Fact]
    public void UmanjeniRedak_NosiPravniIzvorUmanjenja()
    {
        var izvor = Redak(Izracunaj("par_tuzba", z => z.PodnesciElektronicki = ["tbr1_st1"]), "tbr1_st1")
            .PravniIzvor;

        Assert.Contains("čl. 7", izvor);
        Assert.Contains("st. 1", izvor);
    }

    // ── Sudar s umanjenjima koja Uredba sama daje ─────────────────
    //
    // Uredba na tri mjesta već propisuje polovicu ZBOG ELEKTRONIČKOG PODNOŠENJA. To je
    // isti razlog zbog kojeg umanjuje i čl. 7, pa bi drugo umanjenje spustilo iznos na
    // 25 % propisanog. Prije popravka: prijava preko e-Notara 30 → 15 → 7,50 €.

    /// <summary>Tar. br. 25 st. 3 — prijava preko e-Notara i START-a ostaje 15,00 €.</summary>
    [Fact]
    public void VecElektronicka_Tbr25_NeUmanjujeSeDrugiPut()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m, [], oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr25_enotar")],
                    PodnesciElektronicki: new HashSet<string> { "tbr25_enotar" }))
            .Ukupno;

        Assert.Equal(15.00m, iznos);
    }

    /// <summary>Tar. br. 26 st. 6 — odluka dostavljena e-Notarom ostaje 40,00 €.</summary>
    [Fact]
    public void VecElektronicka_Tbr26_NeUmanjujeSeDrugiPut()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m, [], oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr26_enotar")],
                    OdlukeElektronicki: new HashSet<string> { "tbr26_enotar" }))
            .Ukupno;

        Assert.Equal(40.00m, iznos);
    }

    /// <summary>Tar. br. 31 st. 3 — uvjerenje kroz e-Građane ostaje 3,00 €.</summary>
    [Fact]
    public void VecElektronicka_Tbr31_NeUmanjujeSeDrugiPut()
    {
        var iznos = SudskePristojbeCalculator.Obracunaj(
                0m, [], oslobodjen: false,
                new PristojbeOpcije(
                    Dodane: [new RucnaPristojba("tbr31_egradani")],
                    PodnesciElektronicki: new HashSet<string> { "tbr31_egradani" },
                    OdlukeElektronicki: new HashSet<string> { "tbr31_egradani" }))
            .Ukupno;

        Assert.Equal(3.00m, iznos);
    }

    /// <summary>
    /// Suprotan slučaj, koji se NE smije zahvatiti istom zaštitom: polovica iz Tar. br. 1
    /// st. 2 dana je zbog VRSTE podneska, ne zbog oblika, pa se s čl. 7 legitimno slaže.
    /// Odgovor na tužbu uz VPS 50.000: 620,00 → 310,00 (st. 2) → 155,00 (čl. 7).
    /// </summary>
    [Fact]
    public void PolovicaZbogVrstePodneska_SlazeSeSCl7()
    {
        var bez = Izracunaj("par_odgovor_tuzba", _ => { });
        var sCl7 = Izracunaj("par_odgovor_tuzba", z => z.PodnesciElektronicki = ["tbr1_st2"]);

        Assert.Equal(310.00m, bez.SudskaPristojba);
        Assert.Equal(155.00m, sCl7.SudskaPristojba);
    }

    /// <summary>
    /// Zaštita se veže uz oznaku, ne uz popis ključeva u testu: svaka odredba s
    /// oznakom mora ostati na punom iznosu uz sve tri oznake čl. 7 uključene.
    /// </summary>
    [Fact]
    public void SvakaVecElektronickaOdredba_OstajeNepromijenjena()
    {
        var vecElektronicke = PristojbeTarifaNn50_2026.Sve
            .Where(o => o.VecImaElektronickoUmanjenje)
            .ToList();

        Assert.NotEmpty(vecElektronicke);

        foreach (var odredba in vecElektronicke)
        {
            decimal Iznos(PristojbeOpcije opcije)
                => SudskePristojbeCalculator.Obracunaj(0m, [], oslobodjen: false, opcije).Ukupno;

            var bez = Iznos(new PristojbeOpcije(Dodane: [new RucnaPristojba(odredba.Id)]));
            var sve = Iznos(new PristojbeOpcije(
                Dodane: [new RucnaPristojba(odredba.Id)],
                PodnesciElektronicki: new HashSet<string> { odredba.Id },
                ZkUpisElektronicki: true,
                OdlukeElektronicki: new HashSet<string> { odredba.Id }));

            Assert.Equal(bez, sve);
        }
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
