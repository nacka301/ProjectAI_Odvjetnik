using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// 10 realnih troškovnika iz objavljenih presuda — regresijsko sidro za izračun
/// odvjetničke nagrade. Ulazi i očekivane brojke: <c>test-troskovnici-realne-presude.md</c>
/// u rootu repoa (svaka presuda ima link na izvor).
///
/// Što se provjerava (KORAK 3 iz zadatka):
///  A) ARITMETIKA — nagrada = Σ bodova po retku × 2 €; PDV = 25 % SAMO na nagradu
///     (+ naknada troškova) i samo kad je odvjetnik u sustavu PDV-a; predujmovi i
///     pristojbe BEZ PDV-a; sveukupno štima.
///  B) RAZRED VRIJEDNOSTI za sastav tužbe (Tbr. 7. toč. 1).
///  C) PRISTOJBE — po Uredbi NN 50/2026, NE po presudama.
///
/// Bodovi se smiju uspoređivati s presudama: Tarifa (NN 138/23, 1 bod = 2,00 €) nije se
/// mijenjala. Sud ne dosuđuje uvijek pune tarifne bodove po radnji; bodovi po retku su
/// uređivi kroz izbor kataloške stavke (Tbr. 7.1 100 % / Tbr. 9.2 50 % / Tbr. 8.4 25 %)
/// i kroz Tbr. 41 (−25 % / −50 %) po retku — to ovdje modeliramo.
///
/// Susjedno gradivo pokrivaju: <see cref="TarifaRazrediTests"/> (sve granice Tbr. 7.1),
/// <see cref="KontrolniIzracuniTests"/> (PDV osnovica, predujam), <see cref="ElektronickiPodnesakTests"/>.
/// </summary>
public class PresudeRealneTests
{
    private static readonly TroskovnikService Servis = new();

    /// <summary>Puna nagrada po Tbr. 7.1 (100 %).</summary>
    private static TroskovnikItemRequest Puna(string itemId = "par_rociste_glavno")
        => new() { ItemId = itemId, Kolicina = 1 };

    /// <summary>Procesno ročište — 50 % Tbr. 7.1 (Tbr. 9.2).</summary>
    private static TroskovnikItemRequest Pola(string itemId = "par_rociste_procesno")
        => new() { ItemId = itemId, Kolicina = 1 };

    /// <summary>Ostali podnesak — 25 % Tbr. 7.1, najviše 25 bodova (Tbr. 8.4).</summary>
    private static TroskovnikItemRequest Cetvrt(string itemId = "par_ostali_podnesci")
        => new() { ItemId = itemId, Kolicina = 1 };

    /// <summary>Puna stavka sniženja po Tbr. 41. toč. 2 (0–50 %) na tom retku.</summary>
    private static TroskovnikItemRequest Snizeno(decimal postotak, string itemId = "par_rociste_glavno")
        => new() { ItemId = itemId, Kolicina = 1, Tbr41SnizenjePostotak = postotak };

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

    private static decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    /// <summary>Aritmetički invarijanti koji moraju vrijediti za SVAKI troškovnik (KORAK 3-A).</summary>
    private static void ProvjeriInvarijante(TroskovnikResponse r, bool pdv, decimal[]? predujmovi = null, decimal[]? naknada = null)
    {
        var naknadaZbroj = Round2((naknada ?? []).Sum());
        var predujmoviZbroj = (predujmovi ?? []).Sum();

        // nagrada = ukupno bodova × 2 €
        Assert.Equal(Round2(r.UkupnoBodova * TarifaConstants.BodValue), r.UkupnoEurBezPdv - r.NaknadaTroskovaUkupno);
        Assert.Equal(naknadaZbroj, r.NaknadaTroskovaUkupno);
        // PDV samo na nagradu + naknadu troškova, samo kad je odvjetnik u sustavu PDV-a
        Assert.Equal(pdv ? Round2(r.UkupnoEurBezPdv * TarifaConstants.PdvRate) : 0m, r.Pdv);
        // predujmovi NISU u PDV osnovici, ali JESU u sveukupnom
        Assert.Equal(predujmoviZbroj, r.UkupnoPredujmovi);
        Assert.Equal(r.UkupnoEurSPdv + r.UkupnoPristojbe + r.UkupnoPredujmovi, r.SveukupniTrosak);
    }

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
        var r = Servis.Izracunaj(Zahtjev(
            vps: 13_000m, pdv: true,
            stavke:
            [
                Puna("par_tuzba"), Puna(), Cetvrt(),
                Puna(), Puna(), Puna(), Puna(),
            ],
            predujmovi: [400m]));

        Assert.Equal(625m, r.UkupnoBodova);
        Assert.Equal(1_250.00m, r.UkupnoEurBezPdv);   // Σ × 2 €
        Assert.Equal(312.50m, r.Pdv);
        Assert.Equal(400.00m, r.UkupnoPredujmovi);
        ProvjeriInvarijante(r, pdv: true, predujmovi: [400m]);
    }

    /// <summary>
    /// 2 · Parnica s vještačenjem — Općinski sud u Koprivnici, P-445/2023-24 (30.9.2025.).
    /// Isti tip radnje s RAZLIČITIM bodovima: 100 ×7, 50 ×1, 25 ×1 → Σ 775 → nagrada
    /// 1.550,00 € + PDV 387,50 €. Predujam vještačenja 398,17 €.
    /// </summary>
    [Fact]
    public void Presuda02_ParnicaVjestacenje_OSKoprivnica()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 10_000m, pdv: true, // razred 1.327,01–13.272 € → puna radnja = 100 bod
            stavke:
            [
                Puna("par_tuzba"), Puna(), Puna(), Puna(),
                Pola(), Puna(), Puna(), Puna(), Cetvrt(),
            ],
            predujmovi: [398.17m]));

        Assert.Equal(775m, r.UkupnoBodova);
        Assert.Equal(1_550.00m, r.UkupnoEurBezPdv);
        Assert.Equal(387.50m, r.Pdv);
        ProvjeriInvarijante(r, pdv: true, predujmovi: [398.17m]);
    }

    /// <summary>
    /// 3 · Parnica male vrijednosti — Općinski sud u Splitu, P-4036/2024-12 (15.7.2025.),
    /// VPS 463,53 €. Razred 332,01–664,00 € → svaka „puna" radnja nosi 50 bod.
    /// 10 radnji → Σ 500 bod. Predujam vještačenja 265,45 €.
    ///
    /// NALAZ: sažetak u izvornom dokumentu („500 bod → nagrada 500 € + PDV 125 €") je
    /// proturječan — 500 bodova × 2 € = 1.000,00 €. Ovdje se drži aritmetika.
    /// </summary>
    [Fact]
    public void Presuda03_MalaVrijednost_OSSplit()
    {
        Assert.Equal(50, TroskovnikService.Tbr71Bodovi(463.53m)); // B — razred

        var r = Servis.Izracunaj(Zahtjev(
            vps: 463.53m, pdv: true,
            stavke: [Puna("par_tuzba"), .. Enumerable.Range(0, 9).Select(_ => Puna())],
            predujmovi: [265.45m]));

        Assert.Equal(500m, r.UkupnoBodova);
        Assert.Equal(1_000.00m, r.UkupnoEurBezPdv);
        Assert.Equal(250.00m, r.Pdv);
        ProvjeriInvarijante(r, pdv: true, predujmovi: [265.45m]);
    }

    /// <summary>
    /// 4 · Ovrha → parnica — Trgovački sud u Zadru, Povrv 80/2023-12 (22.3.2024.).
    /// Parnični dio: 2 podneska ×100, pripremno ročište 25, glavna rasprava 50 → Σ 275 bod →
    /// nagrada 550,00 €. Javnobilježnička naknada 42,05 € ULAZI u PDV osnovicu (čl. 33 ZPDV).
    /// </summary>
    [Fact]
    public void Presuda04_OvrhaParnica_TSZadar()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 10_000m, pdv: true,
            stavke: [Puna("par_tuzba"), Puna(), Cetvrt(), Pola()],
            naknadaTroskova: [42.05m]));

        Assert.Equal(275m, r.UkupnoBodova);
        Assert.Equal(592.05m, r.UkupnoEurBezPdv);       // 550 + 42,05 (naknada u osnovici)
        Assert.Equal(Round2(592.05m * 0.25m), r.Pdv);    // 148,01
        ProvjeriInvarijante(r, pdv: true, naknada: [42.05m]);
    }

    /// <summary>
    /// 5 · Ovršni postupak — Općinski građanski sud u Zagrebu, Povrv-2030/2022-15 (30.9.2025.).
    /// Prijedlog 100, pripremna radnja 100, podnesak 25, ročište 100 → Σ 325 bod → nagrada
    /// 650,00 € + PDV 162,50 €. Predujmovi: javnobilježnička naknada 12,44 € + vještačenje 200,00 €.
    /// </summary>
    [Fact]
    public void Presuda05_OvrsniPostupak_OGSZagreb()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 8_000m, pdv: true,
            stavke: [Puna("par_tuzba"), Puna(), Cetvrt(), Puna()],
            predujmovi: [12.44m, 200m]));

        Assert.Equal(325m, r.UkupnoBodova);
        Assert.Equal(650.00m, r.UkupnoEurBezPdv);
        Assert.Equal(162.50m, r.Pdv);
        Assert.Equal(212.44m, r.UkupnoPredujmovi);
        Assert.Equal(650.00m + 162.50m + 212.44m, r.SveukupniTrosak - r.UkupnoPristojbe);
        ProvjeriInvarijante(r, pdv: true, predujmovi: [12.44m, 200m]);
    }

    /// <summary>
    /// 6 · Potrošački spor — Općinski sud u Sisku, P-324/2025-20 (23.4.2026.), VPS 1.327,22 €.
    /// Granica razreda: 1.327,22 € > 1.327,00 € → 100 bodova (ne 75). Sastav tužbe:
    /// 100 bod = 200,00 € + PDV 50,00 €.
    /// </summary>
    [Fact]
    public void Presuda06_PotrosackiSpor_OSSisak_GranicaRazreda()
    {
        Assert.Equal(100, TroskovnikService.Tbr71Bodovi(1_327.22m));

        var r = Servis.Izracunaj(Zahtjev(
            vps: 1_327.22m, pdv: true,
            stavke: [Puna("par_tuzba")]));

        Assert.Equal(100m, r.Stavke.Single().UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);
        ProvjeriInvarijante(r, pdv: true);
    }

    /// <summary>
    /// 7 · STRESS TEST — Trgovački sud u Varaždinu, P 4/2024-18 (27.6.2024.),
    /// djelomični uspjeh 74,54 % / 25,46 %. Dugačak troškovnik: tužba + 8 ročišta +
    /// dva retka sniženih po Tbr. 41, tri vještačenja. Aritmetika mora štimati bez obzira
    /// na sastav.
    ///
    /// NALAZ: motor NEMA opću „podjelu troška po postotku uspjeha". <c>PostotakUspjeha</c>
    /// postoji samo uz oslobođenje od pristojbi (čl. 13), ne kao opći množitelj nagrade.
    /// </summary>
    [Fact]
    public void Presuda07_StressTest_TSVarazdin()
    {
        var stavke = new List<TroskovnikItemRequest> { Puna("par_tuzba") };
        stavke.AddRange(Enumerable.Range(0, 8).Select(_ => Puna()));
        stavke.Add(Snizeno(25m));
        stavke.Add(Snizeno(50m));

        var r = Servis.Izracunaj(Zahtjev(
            vps: 60_000m, pdv: true,
            stavke: stavke,
            predujmovi: [1_832.64m, 515.50m, 250m]));

        ProvjeriInvarijante(r, pdv: true, predujmovi: [1_832.64m, 515.50m, 250m]);
        Assert.Equal(2_598.14m, r.UkupnoPredujmovi);
        Assert.Equal(0m, r.NaknadaTroskovaUkupno); // predujmovi ne diraju PDV osnovicu
    }

    /// <summary>
    /// 8 · Visoka vrijednost (u kunama) — Županijski sud u Slavonskom Brodu, Gž 186/2023-6
    /// (4.9.2023.). Stara Tarifa u kunama — aritmetika se ne uspoređuje 1:1. Provjerava se
    /// LOGIKA rasta bodova u progresivnom razredu i da agregacija ne puca na velikim
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
            stavke: [Puna("par_tuzba")]));

        Assert.Equal(bodoviVeliki, r.UkupnoBodova);
        Assert.Equal(Round2(bodoviVeliki * 2m), r.UkupnoEurBezPdv);
        ProvjeriInvarijante(r, pdv: true);
    }

    /// <summary>
    /// 9 · Radni spor, sniženo ročište (u kunama) — Općinski sud u Osijeku, Pr-1107/2021-20
    /// (15.2.2023.). Tužba 100, dva podneska 100+100, ročište SNIŽENO na 50 (procesna pitanja)
    /// → Σ 350 bod → nagrada 700,00 € + PDV 175,00 €.
    /// </summary>
    [Fact]
    public void Presuda09_RadniSpor_OSOsijek_SnizenoRociste()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 5_000m, pdv: true,
            stavke: [Puna("par_tuzba"), Puna(), Puna(), Pola()]));

        Assert.Equal(350m, r.UkupnoBodova);
        Assert.Equal(700.00m, r.UkupnoEurBezPdv);
        Assert.Equal(175.00m, r.Pdv);
        ProvjeriInvarijante(r, pdv: true);
    }

    /// <summary>
    /// 10 · Baseline — Općinski građanski sud u Zagrebu, P-3260/2025-12 (26.1.2026.),
    /// VPS 7.500 €. Sastav tužbe (Tbr. 7.1) 100 bod = 200,00 € + PDV 50,00 €. Poklapa se s
    /// presudom u cijelosti osim pristojbi (v. C).
    /// </summary>
    [Fact]
    public void Presuda10_Baseline_OGSZagreb()
    {
        Assert.Equal(100, TroskovnikService.Tbr71Bodovi(7_500m));

        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: true,
            stavke: [Puna("par_tuzba")]));

        Assert.Equal(100m, r.Stavke.Single().UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);
        Assert.Equal(250.00m, r.UkupnoEurSPdv);
    }

    // ═══════════════════════════════════════════════════════════════
    //  A) PDV samo kad je odvjetnik u sustavu PDV-a
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BezPdva_PdvJeNula()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: false,
            stavke: [Puna("par_tuzba"), Pola()],
            predujmovi: [100m]));

        Assert.Equal(300.00m, r.UkupnoEurBezPdv); // (100 + 50) × 2
        Assert.Equal(0m, r.Pdv);
        Assert.Equal(300.00m + 100m, r.SveukupniTrosak - r.UkupnoPristojbe);
        ProvjeriInvarijante(r, pdv: false, predujmovi: [100m]);
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
    /// to NIJE ono što app daje, i namjerno je tako (čl. 2 NN 50/2026).
    /// </summary>
    [Fact]
    public void C_Presuda10_PristojbaPoNn50_NePoPresudi()
    {
        var r = Servis.Izracunaj(Zahtjev(
            vps: 7_500m, pdv: true,
            stavke: [Puna("par_tuzba")]));

        Assert.Equal(195.00m, r.SudskaPristojba);
        Assert.NotEqual(60.73m, r.SudskaPristojba);
        Assert.Equal("tbr1_st1", Assert.Single(r.PristojbeneStavke).OdredbaId);
    }

    /// <summary>
    /// Elektronički podnesak → pristojba upola (čl. 7 st. 1 Zakona o sudskim pristojbama).
    /// 195,00 € → 97,50 €. Puni doseg čl. 7 pokriva <see cref="ElektronickiPodnesakTests"/>.
    /// </summary>
    [Fact]
    public void C_ElektronickiPodnesak_PristojbaUpola()
    {
        var zahtjev = Zahtjev(vps: 7_500m, pdv: true, stavke: [Puna("par_tuzba")]);
        zahtjev.PodnesciElektronicki = ["tbr1_st1"];

        var r = Servis.Izracunaj(zahtjev);

        Assert.Equal(97.50m, r.SudskaPristojba);
        Assert.True(Assert.Single(r.PristojbeneStavke).UmanjenoElektronicki);
    }
}
