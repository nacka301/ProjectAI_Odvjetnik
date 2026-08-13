using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Uredba o Tarifi sudskih pristojbi, NN 50/2026 — na snazi od 1.6.2026.
///
/// Testovi provjeravaju ono što je u prijelazu bilo strukturno krivo, a ne samo brojčano:
///  • Tar. br. 1 vrijedi SAMO u Poglavlju I. (upravni spor je fiksan, ne po VPS-u),
///  • pravni lijekovi imaju vlastiti Tar. br. 3 (protiv presude dvostruko),
///  • čl. 2 zadržava staru Uredbu za dva slučaja, pa se u istom troškovniku mogu naći
///    retci po objema.
///
/// Sami iznosi po odredbama žive u <see cref="SudskePristojbeParitetTests"/>, koji ih
/// dijeli s frontendom.
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

    private static List<TarifnaStavka> Stavke(params string[] itemIds)
        => [.. itemIds.Select(id => TarifaKatalog.Sve.Single(s => s.Id == id))];

    private static PristojbenaStavkaDto Redak(TroskovnikResponse odgovor, string odredbaId)
        => Assert.Single(odgovor.PristojbeneStavke, s => s.OdredbaId == odredbaId);

    // ── Tar. br. 1 — podnesci u parničnom i ovršnom postupku ─────

    [Theory]
    [InlineData(300, 30.00)]
    [InlineData(500, 60.00)]
    [InlineData(1_000, 80.00)]
    [InlineData(1_500, 110.00)]
    [InlineData(1_900, 140.00)]
    [InlineData(3_000, 150.00)]
    [InlineData(50_000, 620.00)]     // prije prelaska: 546,45
    [InlineData(500_000, 1_500.00)]  // kapa; prije prelaska: 663,61
    public void Tuzba_PlacaPunuPristojbuIzStavka1(decimal vps, decimal ocekivano)
    {
        var odgovor = Izracunaj(vps, "par_tuzba");

        Assert.Equal(ocekivano, odgovor.SudskaPristojba);
        Assert.Equal("Tar. br. 1 st. 1", Redak(odgovor, "tbr1_st1").TarifniBroj);
    }

    /// <summary>
    /// Prijedlog za ovrhu je u nabrajanju iz st. 2 — polovica pristojbe iz st. 1.
    /// </summary>
    [Fact]
    public void PrijedlogZaOvrhu_PlacaPolovicu()
    {
        var odgovor = Izracunaj(50_000m, "ovr_prijedlog");

        Assert.Equal(310.00m, odgovor.SudskaPristojba);
        Assert.Equal("Tar. br. 1 st. 2", Redak(odgovor, "tbr1_st2").TarifniBroj);
    }

    /// <summary>
    /// Ovrha uz parničnu stavku daje DVIJE pristojbe, a ne jednu „višu" — tužba po st. 1
    /// i prijedlog za ovrhu po st. 2 dva su podneska i Uredba naplaćuje oba.
    /// Prije prelaska je takav troškovnik davao jedan iznos od 546,45 €.
    /// </summary>
    [Fact]
    public void TuzbaIOvrha_PlacajuSeObjePristojbe()
    {
        var odgovor = Izracunaj(50_000m, "par_tuzba", "ovr_prijedlog");

        Assert.Equal(620.00m, Redak(odgovor, "tbr1_st1").Iznos);
        Assert.Equal(310.00m, Redak(odgovor, "tbr1_st2").Iznos);
        Assert.Equal(930.00m, odgovor.SudskaPristojba);
    }

    /// <summary>
    /// Dvije tarifne stavke koje vode na istu odredbu i dalje su jedan podnesak —
    /// tužba zbog smetanja posjeda ne naplaćuje se povrh obične tužbe.
    /// </summary>
    [Fact]
    public void DvijeStavkeIstaOdredba_NaplacujeSeJednom()
    {
        var odgovor = Izracunaj(50_000m, "par_tuzba", "par_tuzba_smetanje");

        Assert.Single(odgovor.PristojbeneStavke);
        Assert.Equal(620.00m, odgovor.SudskaPristojba);
    }

    // ── Tar. br. 3 — pravni lijekovi ─────────────────────────────

    /// <summary>
    /// Tar. br. 3 st. 1: za žalbu, prijedlog za dopuštenje revizije i reviziju protiv
    /// PRESUDE plaća se pristojba iz Tar. br. 1 st. 1 uvećana za 100 %.
    ///
    /// Zatečeni je kod žalbu vodio kroz Tar. br. 1 st. 2 (polovica), pa je na VPS-u
    /// 50.000 € davao 273,23 € umjesto 1.240,00 € — četiri i pol puta premalo.
    /// </summary>
    [Theory]
    [InlineData("par_zalba_presuda")]
    [InlineData("par_revizija")]
    [InlineData("par_zalba_jednokratna")]
    public void PravniLijekProtivPresude_UvecanZa100Posto(string itemId)
    {
        var odgovor = Izracunaj(50_000m, itemId);

        Assert.Equal(1_240.00m, odgovor.SudskaPristojba);
        Assert.Equal("Tar. br. 3 st. 1", Redak(odgovor, "tbr3_st1").TarifniBroj);
    }

    /// <summary>
    /// Tar. br. 3 st. 2: isti lijekovi protiv RJEŠENJA — puna pristojba iz st. 1,
    /// bez uvećanja. Razlika prema st. 1 je točno dvostruka.
    /// </summary>
    [Fact]
    public void ZalbaProtivRjesenja_PunaPristojbaBezUvecanja()
    {
        var odgovor = Izracunaj(50_000m, "par_zalba_rjesenje");

        Assert.Equal(620.00m, odgovor.SudskaPristojba);
        Assert.Equal("Tar. br. 3 st. 2", Redak(odgovor, "tbr3_st2").TarifniBroj);
    }

    /// <summary>
    /// Uvećanje po st. 1 primjenjuje se na VEĆ OGRANIČENI iznos iz Tar. br. 1 st. 1 —
    /// kapa od 1.500,00 dio je st. 1, pa žalba pri kapi iznosi 3.000,00, a ne 1.500,00.
    /// </summary>
    [Fact]
    public void ZalbaPriKapi_DvostrukoOdOgranicenogIznosa()
    {
        Assert.Equal(3_000.00m,
            SudskePristojbeCalculator.Izracunaj("tbr3_st1", 500_000m, UredbaVerzija.Nn50_2026));
    }

    /// <summary>
    /// Odgovor na žalbu NIJE pravni lijek. Nabrajanje u Tar. br. 1 st. 2 završava
    /// popisom odgovora — „odgovor na … tužbu, žalbu, prijedlog za dopuštenje revizije
    /// i reviziju" — pa se za njega plaća polovica, a ne dvostruko.
    /// </summary>
    [Fact]
    public void OdgovorNaZalbu_PlacaPolovicu_NeDvostruko()
    {
        var odgovor = Izracunaj(50_000m, "par_odgovor_zalba");

        Assert.Equal(310.00m, odgovor.SudskaPristojba);
        Assert.Equal("Tar. br. 1 st. 2", Redak(odgovor, "tbr1_st2").TarifniBroj);
    }

    // ── Poglavlja izvan Tar. br. 1 ───────────────────────────────

    /// <summary>
    /// Poglavlje VIII. propisuje fiksne iznose i Tar. br. 1 se ondje ne primjenjuje.
    /// Zatečeni je kod na tužbu u upravnom sporu s VPS-om 50.000 € računao 546,45 €
    /// umjesto 110,00 €.
    /// </summary>
    [Theory]
    [InlineData(5_000)]
    [InlineData(50_000)]
    [InlineData(5_000_000)]
    public void UpravniSpor_FiksnaPristojbaBezObziraNaVps(decimal vps)
    {
        var odgovor = Izracunaj(vps, "usp_tuzba_proc");

        Assert.Equal(110.00m, odgovor.SudskaPristojba);
        Assert.Equal("VIII. Upravni spor", Redak(odgovor, "tbr29_tuzba").Poglavlje);
    }

    [Fact]
    public void UpravniSpor_ZalbaJeFiksnih140()
    {
        Assert.Equal(140.00m, Izracunaj(50_000m, "usp_zalba").SudskaPristojba);
    }

    /// <summary>
    /// Izvanparnična dioba ide po Tar. br. 5 (ljestvica sa stepenicom od 1.300,00),
    /// a ne po Tar. br. 1: 60,00 + 30,00 × ⌈3.400/1.300⌉ = 150,00.
    /// </summary>
    [Fact]
    public void IzvanparnicnaDioba_IdePoTarBr5()
    {
        var odgovor = Izracunaj(10_000m, "izv_dioba");

        Assert.Equal(150.00m, odgovor.SudskaPristojba);
        Assert.Equal("II. Izvanparnični postupak", Redak(odgovor, "tbr5").Poglavlje);
    }

    /// <summary>Zemljišnoknjižni prijedlog je 10,00 €, koliko god vrijedila nekretnina.</summary>
    [Fact]
    public void ZemljisnoknjizniPrijedlog_FiksnihDesetEura()
    {
        Assert.Equal(10.00m, Izracunaj(300_000m, "zk_redoviti").SudskaPristojba);
    }

    /// <summary>Prijava potraživanja u stečaju — 2 % tražbine, najviše 140,00.</summary>
    [Fact]
    public void PrijavaPotrazivanja_DvaPostoTrazbine()
    {
        Assert.Equal(100.00m, Izracunaj(5_000m, "ste_prijava_potrazivanja").SudskaPristojba);
        Assert.Equal(140.00m, Izracunaj(50_000m, "ste_prijava_potrazivanja").SudskaPristojba);
    }

    /// <summary>Prijava za upis u sudski registar — Poglavlje VII., fiksnih 30,00.</summary>
    [Fact]
    public void SudskiRegistar_FiksnaPrijava()
    {
        var odgovor = Izracunaj(1_000_000m, "reg_upis");

        Assert.Equal(30.00m, odgovor.SudskaPristojba);
        Assert.Equal("VII. Sudski registar", Redak(odgovor, "tbr25_prijava").Poglavlje);
    }

    // ── Kad Uredba pristojbu ne propisuje ────────────────────────

    /// <summary>
    /// Obrana u kaznenom postupku po javnoj optužbi u Uredbi nema pristojbu — Poglavlje V.
    /// pokriva samo postupak po privatnoj tužbi. Ponašanje je nepromijenjeno.
    /// </summary>
    [Fact]
    public void KazneniPostupak_NemaPristojbe()
    {
        var odgovor = Izracunaj(50_000m, "kaz_obrana");

        Assert.Empty(odgovor.PristojbeneStavke);
        Assert.Equal(0m, odgovor.SudskaPristojba);
        Assert.Equal(PristojbaOsnova.Nema, odgovor.PristojbaOsnova);
    }

    /// <summary>
    /// Upravni POSTUPAK teče pred tijelom, a ne pred sudom — Uredba o sudskim
    /// pristojbama za njega nema što propisati.
    /// </summary>
    [Fact]
    public void UpravniPostupak_NemaPristojbe()
    {
        Assert.Empty(Izracunaj(50_000m, "upr_procjenjiv").PristojbeneStavke);
    }

    /// <summary>
    /// Razlog zbog kojeg je pristojba nula mora se vidjeti u odgovoru: oslobođena
    /// stranka zadržava retke s tarifnim brojevima, a predmet bez pristojbe ih nema.
    /// Oboje daje 0,00 €, iz posve različitih razloga.
    /// </summary>
    [Fact]
    public void Oslobodjenje_NuliIznosAliZadrzavaRetke()
    {
        var oslobodjen = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsOslobodjenPristojbi = true,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        var kazneni = Izracunaj(50_000m, "kaz_obrana");

        Assert.Equal(0m, oslobodjen.SudskaPristojba);
        Assert.Equal(0m, kazneni.SudskaPristojba);

        // Oslobođena stranka i dalje vidi po čemu bi se pristojba obračunala.
        var redak = Assert.Single(oslobodjen.PristojbeneStavke);
        Assert.Equal("Tar. br. 1 st. 1", redak.TarifniBroj);
        Assert.Equal(0m, redak.Iznos);

        Assert.Empty(kazneni.PristojbeneStavke);
    }

    // ── Čl. 2 — prijelazni slučajevi ─────────────────────────────

    private static TroskovnikResponse SDatumima(
        decimal vps, DateOnly? odgovorNaTuzbu, DateOnly? upravniSpor, params string[] itemIds)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            DatumPodnosenjaOdgovoraNaTuzbu = odgovorNaTuzbu,
            DatumPokretanjaUpravnogSpora = upravniSpor,
            Stavke = [.. itemIds.Select(id => new TroskovnikItemRequest { ItemId = id, Kolicina = 1 })]
        });

    /// <summary>
    /// Čl. 2, prvi slučaj: pristojba za odgovor na tužbu podnesen prije 1.6.2026.
    /// utvrđuje se po staroj Uredbi — 273,23 €, a ne 310,00 €.
    /// </summary>
    [Fact]
    public void OdgovorNaTuzbuPrijeStupanjaNaSnagu_IdePoStarojUredbi()
    {
        var odgovor = SDatumima(50_000m, new DateOnly(2026, 3, 12), null, "par_odgovor_tuzba");

        Assert.Equal(273.23m, odgovor.SudskaPristojba);
        Assert.Equal("NN 37/2023", Redak(odgovor, "tbr1_st2").Uredba);
    }

    [Theory]
    [InlineData(2026, 7, 12)]  // nakon stupanja na snagu
    [InlineData(2026, 6, 1)]   // na sam dan stupanja na snagu — čl. 2 traži "prije"
    public void OdgovorNaTuzbuOdStupanjaNaSnagu_IdePoNovojUredbi(int g, int m, int d)
    {
        var odgovor = SDatumima(50_000m, new DateOnly(g, m, d), null, "par_odgovor_tuzba");

        Assert.Equal(310.00m, odgovor.SudskaPristojba);
        Assert.Equal("NN 50/2026", Redak(odgovor, "tbr1_st2").Uredba);
    }

    [Fact]
    public void OdgovorNaTuzbuBezDatuma_IdePoNovojUredbi()
    {
        Assert.Equal(310.00m, Izracunaj(50_000m, "par_odgovor_tuzba").SudskaPristojba);
    }

    /// <summary>
    /// Čl. 2, drugi slučaj: pristojbe u upravnom sporu pokrenutom prije 1.6.2026.
    /// utvrđuju se po staroj Uredbi — tužba 53,09 € umjesto 110,00 €.
    /// </summary>
    [Fact]
    public void UpravniSporPokrenutPrijeStupanjaNaSnagu_IdePoStarojUredbi()
    {
        var odgovor = SDatumima(50_000m, null, new DateOnly(2026, 3, 12), "usp_tuzba_proc");

        Assert.Equal(53.09m, odgovor.SudskaPristojba);
        Assert.Equal("NN 37/2023", Redak(odgovor, "tbr29_tuzba").Uredba);
    }

    [Fact]
    public void UpravniSporPokrenutNakonStupanjaNaSnagu_IdePoNovojUredbi()
    {
        var odgovor = SDatumima(50_000m, null, new DateOnly(2026, 9, 1), "usp_tuzba_proc");

        Assert.Equal(110.00m, odgovor.SudskaPristojba);
    }

    /// <summary>
    /// Prijelazna odredba vezuje se uz odgovor na TUŽBU. Odgovor na žalbu u čl. 2 nije
    /// spomenut, pa mu stara Uredba ne pripada ni kad je podnesen prije 1.6.2026.
    /// </summary>
    [Fact]
    public void OdgovorNaZalbu_NijePrijelazniSlucaj()
    {
        var odgovor = SDatumima(50_000m, new DateOnly(2026, 3, 12), null, "par_odgovor_zalba");

        Assert.Equal(310.00m, odgovor.SudskaPristojba);
        Assert.Equal("NN 50/2026", Redak(odgovor, "tbr1_st2").Uredba);
    }

    /// <summary>
    /// Verzija se bira po RETKU, ne po troškovniku: predmet u kojem je odgovor na tužbu
    /// podnesen u ožujku, a žalba izjavljena poslije 1.6.2026., nosi jedan redak po
    /// staroj i jedan po novoj Uredbi.
    /// </summary>
    [Fact]
    public void JedanTroskovnik_MozeNositiObjeUredbe()
    {
        var odgovor = SDatumima(
            50_000m, new DateOnly(2026, 3, 12), null, "par_odgovor_tuzba", "par_zalba_presuda");

        Assert.Equal("NN 37/2023", Redak(odgovor, "tbr1_st2").Uredba);
        Assert.Equal(273.23m, Redak(odgovor, "tbr1_st2").Iznos);

        Assert.Equal("NN 50/2026", Redak(odgovor, "tbr3_st1").Uredba);
        Assert.Equal(1_240.00m, Redak(odgovor, "tbr3_st1").Iznos);

        Assert.Equal(1_513.23m, odgovor.SudskaPristojba);
    }

    // ── Ručne izmjene liste ──────────────────────────────────────

    /// <summary>
    /// Nekoliko stavaka kataloga pokriva dvije odredbe odjednom — `izv_dioba` nosi i
    /// diobu (Tar. br. 5) i ostavinu (Tar. br. 11), a to su različita poglavlja. Zato
    /// automatski izveden redak mora biti moguće zamijeniti.
    /// </summary>
    [Fact]
    public void IskljucivanjeIRucnoDodavanje_ZamjenjujuIzvedeniRedak()
    {
        var odgovor = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 10_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "izv_dioba", Kolicina = 1 }],
            PristojbeIskljucene = ["tbr5"],
            PristojbeDodane = [new RucnaPristojbaRequest { OdredbaId = "tbr11" }]
        });

        var redak = Assert.Single(odgovor.PristojbeneStavke);
        Assert.Equal("tbr11", redak.OdredbaId);
        Assert.Equal(120.00m, odgovor.SudskaPristojba);
        Assert.False(redak.Automatski);
    }

    /// <summary>
    /// Ručni redak nosi vlastitu osnovicu — ostavina se ne izvodi iz vrijednosti
    /// predmeta spora, nego iz čiste vrijednosti ostavine.
    /// </summary>
    [Fact]
    public void RucniRedak_KoristiVlastituOsnovicu()
    {
        var odgovor = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 0m,
            Stavke = [],
            PristojbeDodane = [new RucnaPristojbaRequest { OdredbaId = "tbr11", Osnovica = 10_000m }]
        });

        Assert.Equal(120.00m, odgovor.SudskaPristojba);
    }

    /// <summary>
    /// Nepoznat ključ odredbe je greška, ne prazan redak. Tiho ispuštanje značilo bi da
    /// odvjetnik doda pristojbu, ne vidi je i preda troškovnik koji je manjkav.
    /// </summary>
    [Fact]
    public void NepoznataOdredba_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() =>
            new TroskovnikService().Izracunaj(new TroskovnikRequest
            {
                Stavke = [],
                PristojbeDodane = [new RucnaPristojbaRequest { OdredbaId = "tbr99_izmisljeno" }]
            }));

        Assert.Contains("tbr99_izmisljeno", greska.Message);
    }

    /// <summary>
    /// Stara je tablica namjerno djelomična. Redak koji čl. 2 ne zadržava na staroj
    /// Uredbi ostaje na novoj i ulazi u zbroj — ne ispada iz njega. Ovdje je odabran
    /// upravni spor pokrenut prije 1.6.2026., ali odredba (Tar. br. 30 st. 1) postoji u
    /// objema; kontrolna je točka da ukupan zbroj sadrži oba retka.
    /// </summary>
    [Fact]
    public void PrijelazniDatum_NeIspustaRetkeIzZbroja()
    {
        var odgovor = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            DatumPokretanjaUpravnogSpora = new DateOnly(2026, 3, 12),
            Stavke = [new TroskovnikItemRequest { ItemId = "usp_tuzba_proc", Kolicina = 1 }],
            // Tar. br. 33 postoji samo u novoj Uredbi — mora ostati u zbroju.
            PristojbeDodane = [new RucnaPristojbaRequest { OdredbaId = "tbr33" }]
        });

        Assert.Equal(2, odgovor.PristojbeneStavke.Count);
        Assert.Equal(53.09m, Redak(odgovor, "tbr29_tuzba").Iznos);   // stara Uredba
        Assert.Equal(30.00m, Redak(odgovor, "tbr33").Iznos);         // nova Uredba
        Assert.Equal(83.09m, odgovor.SudskaPristojba);
    }

    /// <summary>Količina množi iznos — Tar. br. 28: preslika se plaća po stranici.</summary>
    [Fact]
    public void RucniRedak_KolicinaMnoziIznos()
    {
        var odgovor = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            Stavke = [],
            PristojbeDodane =
                [new RucnaPristojbaRequest { OdredbaId = "tbr28_preslika", Kolicina = 12 }]
        });

        Assert.Equal(12.00m, odgovor.SudskaPristojba);
    }

    // ── Veza s ostatkom troškovnika ──────────────────────────────

    /// <summary>
    /// Sudska pristojba nije odvjetnička nagrada: ne ulazi u osnovicu za PDV, nego se
    /// pribraja tek na kraju.
    /// </summary>
    [Fact]
    public void Pristojba_NeUlaziUOsnovicuZaPdv()
    {
        var odgovor = Izracunaj(50_000m, "par_tuzba");

        Assert.Equal(620.00m, odgovor.SudskaPristojba);
        Assert.Equal(odgovor.UkupnoEurSPdv + 620.00m, odgovor.SveukupniTrosak);
        Assert.Equal(Math.Round(odgovor.UkupnoEurBezPdv * 0.25m, 2), odgovor.Pdv);
    }

    /// <summary>
    /// <see cref="SudskePristojbeCalculator.Obracunaj"/> se poziva i izravno; ishod mora
    /// biti isti kao kroz cijeli troškovnik.
    /// </summary>
    [Fact]
    public void Obracunaj_IzravnoDajeIsteRetke()
    {
        var rezultat = SudskePristojbeCalculator.Obracunaj(
            50_000m, Stavke("par_tuzba", "par_zalba_presuda"), oslobodjen: false);

        Assert.Equal(2, rezultat.Stavke.Count);
        Assert.Equal(1_860.00m, rezultat.Ukupno);
        Assert.False(rezultat.Oslobodjen);
    }

    /// <summary>Retci se prikazuju redom kojim odredbe stoje u Prilogu Uredbe.</summary>
    [Fact]
    public void Retci_SlazuSePoRedosljeduIzUredbe()
    {
        var odgovor = Izracunaj(50_000m, "par_zalba_presuda", "par_tuzba");

        Assert.Equal(["tbr1_st1", "tbr3_st1"],
            odgovor.PristojbeneStavke.Select(s => s.OdredbaId));
    }

    /// <summary>
    /// Fiksni iznos ne ovisi o vrijednosti predmeta spora, pa se uz njega VPS ne
    /// prikazuje — inače bi uz 110,00 € stajalo 50.000 € i sugeriralo da je iznos iz
    /// njega izveden.
    /// </summary>
    [Fact]
    public void FiksniIznos_NePrikazujeOsnovicu()
    {
        Assert.Equal(0m, Redak(Izracunaj(50_000m, "usp_tuzba_proc"), "tbr29_tuzba").Osnovica);
        Assert.Equal(50_000m, Redak(Izracunaj(50_000m, "par_tuzba"), "tbr1_st1").Osnovica);
    }
}
