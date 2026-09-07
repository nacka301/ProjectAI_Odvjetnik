using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// 10 realnih troškovnika iz objavljenih presuda — regresijsko sidro za izračun
/// odvjetničke nagrade. Ulazi i očekivane brojke: <c>test-troskovnici-realne-presude.md</c>
/// u rootu repoa (svaka presuda ima link na izvor).
///
/// Što se provjerava (KORAK 3 iz zadatka):
///  A) ARITMETIKA — nagrada = Σ priznatih bodova × 2 €; PDV = 25 % SAMO na nagradu
///     (+ naknada troškova) i samo kad je odvjetnik u sustavu PDV-a; predujmovi i
///     pristojbe BEZ PDV-a; sveukupno štima.
///  B) RAZRED VRIJEDNOSTI za sastav tužbe (Tbr. 7. toč. 1).
///  C) PRISTOJBE — po Uredbi NN 50/2026, NE po presudama (presude su po staroj uredbi).
///
/// Bodovi se smiju uspoređivati s presudama: Tarifa (NN 138/23, 1 bod = 2,00 €) nije se
/// mijenjala. Sud ne dosuđuje uvijek pune tarifne bodove po radnji, a motor nema slobodan
/// unos „bodova po radnji" u tarifnom izračunu (bodovi = katalog + VPS). Priznatu brojku
/// suda nosi <see cref="TroskovnikItemRequest.PriznatiBodovi"/> po retku → paralelni
/// <see cref="TroskovnikResponse.Priznato"/> zbroj. Njega ovdje provjeravamo.
///
/// Postojeći paketi već pokrivaju susjedno gradivo i ostaju izvor istine za granice:
/// <see cref="TarifaRazrediTests"/> (sve granice Tbr. 7.1), <see cref="KontrolniIzracuniTests"/>
/// (PDV osnovica, predujam), <see cref="ElektronickiPodnesakTests"/> (čl. 7).
/// </summary>
public class PresudeRealneTests
{
    private static readonly TroskovnikService Servis = new();

    /// <summary>Radnja s brojem bodova koji je sud priznao u rješenju o troškovima.</summary>
    private static TroskovnikItemRequest Priznata(decimal bodovi, string itemId = "par_rociste_glavno")
        => new() { ItemId = itemId, Kolicina = 1, PriznatiBodovi = bodovi };

    private static TroskovnikRequest Zahtjev(
        decimal vps,
        bool pdv,
        IEnumerable<TroskovnikItemRequest> stavke,
        decimal[]? predujmovi = null,
        decimal[]? naknadaTroskova = null)
        => new()
        {
            VrijednostSpora = vps,
            IsPdvUkljucen = pdv,
            Stavke = [.. stavke],
            Predujmovi = [.. (predujmovi ?? []).Select((iznos, i) => new TroskovnikTrosakRequest { Opis = $"predujam {i + 1}", Iznos = iznos })],
            NaknadaTroskovaStavke = [.. (naknadaTroskova ?? []).Select(iznos => new NaknadaTroskovaStavkaRequest { Vrsta = VrstaTroska.OstaliIzdaci, Iznos = iznos })],
        };

    /// <summary>Priznati zbroj koji svaka presuda mora imati (bar jedan redak s priznatim bodovima).</summary>
    private static PriznatoZbroj Priznato(TroskovnikResponse r)
    {
        Assert.NotNull(r.Priznato);
        return r.Priznato!;
    }

    private static decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    // ═══════════════════════════════════════════════════════════════
    //  A) ARITMETIKA — 10 presuda
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 1 · Naknada štete na vozilu — Trgovački sud u Zagrebu, P-2064/2022-41 (28.5.2025.).
    /// Tužba 100, podnesak 100, podnesak (Tbr. 8.4) 25, 4× ročište 100 → Σ 625 bod.
    /// Nagrada 1.250,00 € + PDV 312,50 €. Predujam vještačenja 400,00 € — bez PDV-a.
    /// </summary>
    [Fact]
    public void Presuda01_NaknadaStete_TSZagreb()
    {
        var bodovi = new[] { 100m, 100m, 25m, 100m, 100m, 100m, 100m };
        var r = Servis.Izracunaj(Zahtjev(
            vps: 13_000m, pdv: true,
            stavke: bodovi.Select(b => Priznata(b)),
            predujmovi: [400m]));

        var p = Priznato(r);
        Assert.Equal(625m, p.OsnovicaBodova);
        Assert.Equal(1_250.00m, p.Nagrada);          // Σ × 2 €
        Assert.Equal(312.50m, p.Pdv);                // 25 % × 1.250
        Assert.Equal(1_250.00m, p.OsnovicaSaNaknadom); // predujam NE ulazi u PDV osnovicu
        Assert.Equal(400.00m, r.UkupnoPredujmovi);
        Assert.Equal(p.OsnovicaSaNaknadom + p.Pdv + r.UkupnoPristojbe + r.UkupnoPredujmovi, p.Sveukupno);
    }

    /// <summary>
    /// 2 · Parnica s vještačenjem — Općinski sud u Koprivnici, P-445/2023-24 (30.9.2025.).
    /// Isti tip radnje s RAZLIČITIM bodovima: 50 / 100 / 25. Σ 775 → nagrada 1.550,00 € +
    /// PDV 387,50 €. Predujam vještačenja 398,17 €.
    /// </summary>
    [Fact]
    public void Presuda02_ParnicaVjestacenje_OSKoprivnica()
    {
        var bodovi = new[] { 100m, 100m, 100m, 100m, 50m, 100m, 100m, 100m, 25m }; // Σ 775
        var r = Servis.Izracunaj(Zahtjev(
            vps: 20_000m, pdv: true,
            stavke: bodovi.Select(b => Priznata(b)),
            predujmovi: [398.17m]));

        var p = Priznato(r);
        Assert.Equal(775m, p.OsnovicaBodova);
        Assert.Equal(1_550.00m, p.Nagrada);
        Assert.Equal(387.50m, p.Pdv);
        Assert.Equal(398.17m, r.UkupnoPredujmovi);
        Assert.Equal(p.OsnovicaSaNaknadom + p.Pdv + r.UkupnoPristojbe + r.UkupnoPredujmovi, p.Sveukupno);
    }

    /// <summary>
    /// 3 · Parnica male vrijednosti — Općinski sud u Splitu, P-4036/2024-12 (15.7.2025.),
    /// VPS 463,53 €. Razred 332,01–664,00 € → sastav tužbe 50 bod. 3 podneska ×50 +
    /// 6 ročišta ×50 → Σ 500 bod. Predujam vještačenja 265,45 €.
    ///
    /// NALAZ: sažetak u izvornom dokumentu („500 bod → nagrada 500 € + PDV 125 €") je
    /// proturječan — 500 bodova × 2 € = 1.000,00 €. Vjerojatno je u presudi bilo 250
    /// bodova; provjeriti original. Ovdje se drži aritmetika (1 bod = 2 €).
    /// </summary>
    [Fact]
    public void Presuda03_MalaVrijednost_OSSplit()
    {
        Assert.Equal(50, TroskovnikService.Tbr71Bodovi(463.53m)); // B — razred

        var r = Servis.Izracunaj(Zahtjev(
            vps: 463.53m, pdv: true,
            stavke: Enumerable.Range(0, 10).Select(_ => Priznata(50m)),
            predujmovi: [265.45m]));

        var p = Priznato(r);
        Assert.Equal(500m, p.OsnovicaBodova);
        Assert.Equal(1_000.00m, p.Nagrada);
        Assert.Equal(250.00m, p.Pdv);
    }

    /// <summary>
    /// 4 · Ovrha → parnica — Trgovački sud u Zadru, Povrv 80/2023-12 (22.3.2024.).
    /// Parnični dio: 2 obrazložena podneska ×100, pripremno ročište 25, glavna rasprava 50
    /// → Σ 275 bod → nagrada 550,00 €. Javnobilježnička naknada 42,05 € ULAZI u PDV
    /// osnovicu (vlastiti trošak, čl. 33 Zakona o PDV-u).
    /// </summary>
    [Fact]
    public void Presuda04_OvrhaParnica_TSZadar()
    {
        var bodovi = new[] { 100m, 100m, 25m, 50m }; // Σ 275
        var r = Servis.Izracunaj(Zahtjev(
            vps: 10_000m, pdv: true,
            stavke: bodovi.Select(b => Priznata(b)),
            naknadaTroskova: [42.05m]));

        var p = Priznato(r);
        Assert.Equal(275m, p.OsnovicaBodova);
        Assert.Equal(550.00m, p.Nagrada);
        Assert.Equal(592.05m, p.OsnovicaSaNaknadom);          // 550 + 42,05
        Assert.Equal(Round2(592.05m * 0.25m), p.Pdv);          // 148,01
    }

    /// <summary>
    /// 5 · Ovršni postupak — Općinski građanski sud u Zagrebu, Povrv-2030/2022-15 (30.9.2025.).
    /// Prijedlog za ovrhu 100, pripremna radnja 100, podnesak 25, ročište 100 → Σ 325 bod →
    /// nagrada 650,00 € + PDV 162,50 €. Predujmovi: javnobilježnička naknada 12,44 € +
    /// vještačenje 200,00 €. Sud je odbio jednu (nedokazanu) stavku dostave — nije unesena.
    /// </summary>
    [Fact]
    public void Presuda05_OvrsniPostupak_OGSZagreb()
    {
        var bodovi = new[] { 100m, 100m, 25m, 100m }; // Σ 325
        var r = Servis.Izracunaj(Zahtjev(
            vps: 8_000m, pdv: true,
            stavke: bodovi.Select(b => Priznata(b)),
            predujmovi: [12.44m, 200m]));

        var p = Priznato(r);
        Assert.Equal(325m, p.OsnovicaBodova);
        Assert.Equal(650.00m, p.Nagrada);
        Assert.Equal(162.50m, p.Pdv);
        Assert.Equal(212.44m, r.UkupnoPredujmovi);
        Assert.Equal(650.00m, p.OsnovicaSaNaknadom); // predujmovi izvan PDV osnovice
    }

    /// <summary>
    /// 6 · Potrošački spor — Općinski sud u Sisku, P-324/2025-20 (23.4.2026.), VPS 1.327,22 €.
    /// Granica razreda: 1.327,22 € > 1.327,00 € → 100 bodova (ne 75). Puni tarifni izračun
    /// sastava tužbe: 100 bod = 200,00 € + PDV 50,00 €.
    /// </summary>
    [Fact]
    public void Presuda06_PotrosackiSpor_OSSisak_GranicaRazreda()
    {
        Assert.Equal(100, TroskovnikService.Tbr71Bodovi(1_327.22m));

        var r = Servis.Izracunaj(Zahtjev(
            vps: 1_327.22m, pdv: true,
            stavke: [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]));

        Assert.Null(r.Priznato); // bez priznatih bodova — čist tarifni izračun
        Assert.Equal(100m, r.Stavke.Single().UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);
    }

    /// <summary>
    /// 7 · STRESS TEST — Trgovački sud u Varaždinu, P 4/2024-18 (27.6.2024.),
    /// djelomični uspjeh 74,54 % / 25,46 %. Honorari punomoćnika 12.627,10 € + PDV
    /// 3.156,78 € (točno 25 %). Tri vještačenja: 1.832,64 € + 515,50 € + 250,00 €.
    ///
    /// NALAZ: motor NEMA opću „podjelu troška po postotku uspjeha". <c>PostotakUspjeha</c>
    /// postoji samo uz oslobođenje od pristojbi (čl. 13 Zakona o sudskim pristojbama), ne
    /// kao opći množitelj odvjetničke nagrade. Podjelu po uspjehu odvjetnik za sada radi
    /// ručno (ili unosom već podijeljenih priznatih bodova).
    /// </summary>
    [Fact]
    public void Presuda07_StressTest_TSVarazdin()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 60_000m, pdv: true,
            stavke: [Priznata(6_313.55m)],           // 12.627,10 € / 2 €
            predujmovi: [1_832.64m, 515.50m, 250m]));

        var p = Priznato(r);
        Assert.Equal(12_627.10m, p.Nagrada);
        Assert.Equal(3_156.78m, p.Pdv);              // točno 25 %
        Assert.Equal(12_627.10m, p.OsnovicaSaNaknadom); // predujmovi ne diraju PDV osnovicu
        Assert.Equal(2_598.14m, r.UkupnoPredujmovi);
        Assert.Equal(p.OsnovicaSaNaknadom + p.Pdv + r.UkupnoPristojbe + r.UkupnoPredujmovi, p.Sveukupno);
    }

    /// <summary>
    /// 8 · Visoka vrijednost (u kunama) — Županijski sud u Slavonskom Brodu, Gž 186/2023-6
    /// (4.9.2023.). Stara Tarifa u kunama — aritmetika se ne uspoređuje 1:1. Provjerava se
    /// samo LOGIKA rasta bodova u progresivnom razredu i da agregacija ne puca na velikim
    /// brojevima. 5.991.708 kn ≈ 795.247 € (÷ 7,53450).
    /// </summary>
    [Fact]
    public void Presuda08_VisokaVrijednostKn_ZSSlavonskiBrod_SamoLogika()
    {
        var bodoviVeliki = TroskovnikService.Tbr71Bodovi(795_247m);
        Assert.InRange(bodoviVeliki, 5_001, 9_999);                       // progresivni razred, ispod kape
        Assert.True(bodoviVeliki > TroskovnikService.Tbr71Bodovi(700_000m)); // raste s vrijednošću

        var r = Servis.Izracunaj(Zahtjev(
            vps: 795_247m, pdv: true,
            stavke: [Priznata(bodoviVeliki)]));

        var p = Priznato(r);
        Assert.Equal(bodoviVeliki * 2m, p.Nagrada);
        Assert.Equal(Round2(p.Nagrada * 0.25m), p.Pdv);
    }

    /// <summary>
    /// 9 · Radni spor, odbijeno ročište (u kunama) — Općinski sud u Osijeku, Pr-1107/2021-20
    /// (15.2.2023.). Tužba 100, dva podneska 100+100, ročište SNIŽENO na 50 (sud: raspravljalo
    /// se samo o procesnim pitanjima) → Σ 350 bod → nagrada 700,00 € + PDV 175,00 €.
    /// Sniženje ročišta nosi priznati unos po retku.
    /// </summary>
    [Fact]
    public void Presuda09_RadniSpor_OSOsijek_SnizenoRociste()
    {
        var bodovi = new[] { 100m, 100m, 100m, 50m }; // Σ 350
        var r = Servis.Izracunaj(Zahtjev(
            vps: 5_000m, pdv: true,
            stavke: bodovi.Select(b => Priznata(b))));

        var p = Priznato(r);
        Assert.Equal(350m, p.OsnovicaBodova);
        Assert.Equal(700.00m, p.Nagrada);
        Assert.Equal(175.00m, p.Pdv);
    }

    /// <summary>
    /// 10 · Baseline — Općinski građanski sud u Zagrebu, P-3260/2025-12 (26.1.2026.),
    /// VPS 7.500 €. Sastav tužbe (Tbr. 7.1) 100 bod = 200,00 € + PDV 50,00 €. Najjednostavniji
    /// slučaj, čist tarifni izračun (bez priznatih bodova) — poklapa se s presudom u cijelosti
    /// osim pristojbi (v. C).
    /// </summary>
    [Fact]
    public void Presuda10_Baseline_OGSZagreb()
    {
        Assert.Equal(100, TroskovnikService.Tbr71Bodovi(7_500m));

        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: true,
            stavke: [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]));

        Assert.Null(r.Priznato);
        Assert.Equal(100m, r.Stavke.Single().UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);
        Assert.Equal(250.00m, r.UkupnoEurSPdv);
    }

    // ═══════════════════════════════════════════════════════════════
    //  A) PDV samo kad je odvjetnik u sustavu PDV-a
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BezPdva_PriznatiPdvJeNula()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: false,
            stavke: [Priznata(300m, "par_tuzba")],
            predujmovi: [100m]));

        var p = Priznato(r);
        Assert.Equal(600.00m, p.Nagrada);
        Assert.Equal(0m, p.Pdv);
        Assert.Equal(0m, r.Pdv);
        Assert.Equal(p.OsnovicaSaNaknadom + r.UkupnoPristojbe + 100m, p.Sveukupno);
    }

    // ═══════════════════════════════════════════════════════════════
    //  B) RAZRED VRIJEDNOSTI — točke iz presuda (granice: TarifaRazrediTests)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(463.53, 50)]      // presuda 3
    [InlineData(1_327.22, 100)]   // presuda 6 — tik iznad granice 1.327,00
    [InlineData(7_500, 100)]      // presuda 10
    [InlineData(664.00, 50)]      // granica
    [InlineData(664.01, 75)]      // granica
    [InlineData(1_327.00, 75)]    // granica
    [InlineData(1_327.01, 100)]   // granica
    public void B_RazredVrijednostiZaSastavTuzbe(decimal vps, int ocekivanoBodova)
        => Assert.Equal(ocekivanoBodova, TroskovnikService.Tbr71Bodovi(vps));

    // ═══════════════════════════════════════════════════════════════
    //  C) SUDSKE PRISTOJBE — po NN 50/2026, ne po presudama
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Presuda 10 (VPS 7.500 €): pristojba na tužbu po Tar. br. 1 st. 1 NN 50/2026 je
    /// 140,00 + 1 % × (7.500 − 2.000) = 195,00 €. Presuda navodi 60,73 € (stara Uredba) —
    /// to NIJE ono što app daje, i namjerno je tako (čl. 2 NN 50/2026: nova uredba za sve
    /// osim odgovora na tužbu / upravnog spora pokrenutog prije 1.6.2026.).
    /// </summary>
    [Fact]
    public void C_Presuda10_PristojbaPoNn50_NePoPresudi()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: true,
            stavke: [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]));

        Assert.Equal(195.00m, r.SudskaPristojba);
        Assert.NotEqual(60.73m, r.SudskaPristojba); // stara uredba iz presude
        Assert.Equal("tbr1_st1", Assert.Single(r.PristojbeneStavke).OdredbaId);
    }

    /// <summary>
    /// Elektronički podnesak → pristojba upola (čl. 7 st. 1 Zakona o sudskim pristojbama).
    /// 195,00 € → 97,50 €. Puni doseg čl. 7 pokriva <see cref="ElektronickiPodnesakTests"/>.
    /// </summary>
    [Fact]
    public void C_ElektronickiPodnesak_PristojbaUpola()
    {
        var zahtjev = Zahtjev(
            vps: 7_500m, pdv: true,
            stavke: [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]);
        zahtjev.PodnesciElektronicki = ["tbr1_st1"];

        var r = Servis.Izracunaj(zahtjev);

        Assert.Equal(97.50m, r.SudskaPristojba);
        Assert.True(Assert.Single(r.PristojbeneStavke).UmanjenoElektronicki);
    }
}
