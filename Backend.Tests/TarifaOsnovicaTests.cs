using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Osnovica „Tbr. 7. toč. 1., 4., 7. i 8."
///
/// Tarifa na devet mjesta upućuje na taj skup točaka (Tbr. 8. toč. 1, 3, 4;
/// Tbr. 9. toč. 1, 2, 3, 5, 6; Tbr. 10.). To NISU četiri različita iznosa nego
/// četiri varijante ISTE osnovice:
///
///   toč. 1 — tablica vrijednost spora → bodovi
///   toč. 4 — po toč. 1, ali ne manje od 100 bodova (autorsko / industrijsko vlasništvo)
///   toč. 7 — po toč. 1 (domaća arbitraža); po toč. 1 + 100 % (međunarodna)
///   toč. 8 — po toč. 1 (mirno rješavanje sporova)
///
/// Prije popravka osnovica je uvijek bila toč. 1, pa Tbr. 8. toč. 1 nije davao
/// ispravan iznos u autorskopravnim sporovima ni u međunarodnoj arbitraži.
/// </summary>
public class TarifaOsnovicaTests
{
    private static TroskovnikResponse Izracunaj(
        string itemId, decimal vps, string osnovica, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            OsnovicaTbr7 = osnovica,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    // ── Tbr. 8. toč. 1 na sve četiri upućene točke ──
    //
    // RUČNI IZRAČUN — VPS 20.000 EUR → toč. 1 daje 250 bodova:
    //   toč. 1                  → 250
    //   toč. 4 (prag ne djeluje)→ max(250, 100) = 250
    //   toč. 7 domaća           → 250
    //   toč. 7 međunarodna      → 250 × 2 = 500
    //   toč. 8 mirenje          → 250
    //
    // RUČNI IZRAČUN — VPS 1.000 EUR → toč. 1 daje 75 bodova (razred 664,01–1.327,00):
    //   toč. 4 (prag DJELUJE)   → max(75, 100) = 100
    // Bez tog retka toč. 1 i toč. 4 daju isto pa se razlika ne bi vidjela.

    [Theory]
    [InlineData(OsnovicaTbr7.Toc1,                     20_000, 250)]
    [InlineData(OsnovicaTbr7.Toc4Autorsko,             20_000, 250)]
    [InlineData(OsnovicaTbr7.Toc4Autorsko,              1_000, 100)]  // prag iz toč. 4 DJELUJE
    [InlineData(OsnovicaTbr7.Toc7DomacaArbitraza,      20_000, 250)]
    [InlineData(OsnovicaTbr7.Toc7MedunarodnaArbitraza, 20_000, 500)]  // +100 %
    [InlineData(OsnovicaTbr7.Toc8Mirenje,              20_000, 250)]
    public void Tbr8Toc1_RadiNaSveCetiriUputneTocke(string osnovica, decimal vps, decimal ocekivano)
    {
        var r = Izracunaj("par_odgovor_tuzba", vps, osnovica);

        Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
        Assert.Equal(ocekivano, r.OsnovicaBodovi);
    }

    /// <summary>
    /// Tbr. 8. toč. 3 i 4 upućuju na ISTI skup točaka kao toč. 1, pa moraju slijediti
    /// osnovicu. Prije popravka uvijek su računali po toč. 1.
    ///
    /// RUČNI IZRAČUN — VPS 20.000, međunarodna arbitraža → osnovica 500 bodova:
    ///   Tbr. 8.3   50 % od 500 = 250 → strop 100 → 100 bodova
    ///   Tbr. 8.4   25 % od 500 = 125 → strop  25 →  25 bodova
    ///   Tbr. 9.1  100 % od 500       = bez stropa → 500 bodova
    /// </summary>
    [Theory]
    [InlineData("par_obrazlozeni_podnesak", 100)]
    [InlineData("par_ostali_podnesci",       25)]
    [InlineData("par_rociste_glavno",       500)]
    public void Osnovica_PrimjenjujeSeINaTbr8t3_Tbr8t4_iTbr9(string itemId, decimal ocekivano)
    {
        var r = Izracunaj(itemId, 20_000m, OsnovicaTbr7.Toc7MedunarodnaArbitraza);

        Assert.Equal(500m, r.OsnovicaBodovi);
        Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
    }

    [Fact]
    public void Osnovica_ZadanaJeToc1_PaJeIznosNepromijenjen()
    {
        var r = Izracunaj("par_tuzba", 20_000m, OsnovicaTbr7.Toc1);

        Assert.Equal(OsnovicaTbr7.Toc1, r.OsnovicaTbr7);
        Assert.Equal(250m, r.OsnovicaBodovi);
        Assert.Contains("Tbr. 7. toč. 1", r.OsnovicaPravniIzvor);
    }

    [Fact]
    public void Osnovica_NepoznataVrijednost_BacaGresku_NePadaTihoNaToc1()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(
            () => Izracunaj("par_tuzba", 20_000m, "izmisljena_osnovica"));

        Assert.Contains("Nepoznata osnovica", ex.Message);
    }

    // ── Tbr. 7. toč. 4 al. 2 — petogodišnji iznos ──

    /// <summary>
    /// Tbr. 7. toč. 4 al. 2: „kao vrijednost spora uzima se petogodišnji iznos rente,
    /// zakupa, najma i drugih sličnih davanja."
    ///
    /// RUČNI IZRAČUN: najam 400 EUR mjesečno × 60 mjeseci = 24.000 EUR
    ///   → razred 13.272,01–33.181,00 → 250 bodova.
    /// Bez preračuna VPS bi bio 400 EUR → 50 bodova. Razlika je peterostruka.
    /// </summary>
    [Theory]
    [InlineData(400,   RentaRazdoblje.Mjesecno, 250)]  //   400 × 60 = 24.000
    [InlineData(5_000, RentaRazdoblje.Godisnje, 250)]  // 5.000 ×  5 = 25.000
    [InlineData(100,   RentaRazdoblje.Mjesecno, 100)]  //   100 × 60 =  6.000 → razred 100 bod.
    public void Tbr7Toc4_Renta_PreracunavaSeNaPetGodina(
        decimal iznos, string razdoblje, decimal ocekivano)
    {
        var r = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 0m,
            OsnovicaTbr7 = OsnovicaTbr7.Toc4Renta,
            RentaIznos = iznos,
            RentaRazdoblje = razdoblje,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        Assert.Equal(ocekivano, r.OsnovicaBodovi);
    }

    [Fact]
    public void Tbr7Toc4_Renta_BezOdabranogRazdoblja_NePreracunavaTiho()
    {
        // Bez razdoblja se koristi uneseni VPS — ništa se ne izvodi na svoju ruku.
        var r = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 1_000m,
            OsnovicaTbr7 = OsnovicaTbr7.Toc4Renta,
            RentaIznos = 400m,
            RentaRazdoblje = null,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        Assert.Equal(75m, r.OsnovicaBodovi);
    }

    // ── Tbr. 7. toč. 6 — vrijednosni papiri ──

    /// <summary>
    /// Tbr. 7. toč. 6 daje PUNU nagradu iz toč. 1, za razliku od toč. 5 (platni nalog
    /// na osnovi računa), koja daje 50 % uz strop od 100 bodova.
    ///
    /// RUČNI IZRAČUN — VPS 20.000 → osnovica 250:
    ///   Tbr. 7.6  100 %                    → 250 bodova
    ///   Tbr. 7.5   50 % = 125 → strop 100  → 100 bodova
    /// </summary>
    [Fact]
    public void Tbr7Toc6_VrijednosniPapiri_PunaNagrada_ZaRazlikuOdToc5()
    {
        Assert.Equal(250m, Izracunaj("par_tuzba_vrijednosni_papiri", 20_000m, OsnovicaTbr7.Toc1)
            .Stavke.Single().BodovaPoJedinici);
        Assert.Equal(100m, Izracunaj("par_tuzba_platni_nalog", 20_000m, OsnovicaTbr7.Toc1)
            .Stavke.Single().BodovaPoJedinici);
    }

    // ── Apsolutni iznosi — Tbr. 7.9, 7.10, 8.5, 9.8, 10.2, 10.3 ──

    /// <summary>
    /// Ove odredbe daju iznos u bodovima izravno, pa ne smiju ovisiti o vrijednosti spora.
    /// </summary>
    [Theory]
    [InlineData("par_mali_spor",                 50)]   // Tbr. 7. toč. 9
    [InlineData("par_zahtjev_haska",            250)]   // Tbr. 7. toč. 10
    [InlineData("par_podnesak_haska",           250)]   // Tbr. 8. toč. 5
    [InlineData("par_ostali_podnesak_haska",    125)]   // Tbr. 8. toč. 5
    [InlineData("par_zastupanje_haska",         250)]   // Tbr. 9. toč. 8
    [InlineData("par_zalba_jednokratna",        100)]   // Tbr. 10. toč. 2
    [InlineData("par_zalba_mali_spor",           30)]   // Tbr. 10. toč. 3
    [InlineData("par_revizija_jednokratna",     150)]   // Tbr. 10. toč. 5 st. 2
    [InlineData("par_revizija_mali_spor",       100)]   // Tbr. 10. toč. 5 st. 2
    [InlineData("par_odgovor_zalba_jednokratno", 30)]   // Tbr. 10. toč. 6 st. 2
    public void ApsolutniIznosi_NeOviseOVrijednostiSpora(string itemId, decimal ocekivano)
    {
        foreach (var vps in new[] { 500m, 500_000m })
        {
            var r = Izracunaj(itemId, vps, OsnovicaTbr7.Toc1);
            Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
        }
    }

    /// <summary>
    /// Tbr. 8. toč. 2: „50% nagrade iz toč. 1. ovog tarifnog broja, ali ne više od 100 bodova."
    ///
    /// RUČNI IZRAČUN:
    ///   VPS  1.327 → osnovica  75 → 50 % = 37,5 bodova (strop ne djeluje)
    ///   VPS 20.000 → osnovica 250 → 50 % = 125 → strop 100 bodova
    /// </summary>
    [Theory]
    [InlineData( 1_327,  37.5)]
    [InlineData(20_000, 100)]
    public void Tbr8Toc2_PovratUPrijasnjeStanje(decimal vps, decimal ocekivano)
    {
        var r = Izracunaj("par_povrat_u_prijasnje_stanje", vps, OsnovicaTbr7.Toc1);

        Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
    }

    // ── Tbr. 25 — ukinut, ne smije postojati ──

    /// <summary>
    /// Tbr. 25 (žalba u postupcima javne nabave) ukinut je presudom Visokog upravnog suda
    /// Republike Hrvatske Usoz-55/2024-21 od 18. srpnja 2025. Stoji u pročišćenom tekstu
    /// Tarife, ali nije na snazi — obračun po njemu nije naplativ.
    /// </summary>
    [Fact]
    public void Tbr25_JavnaNabava_NeSmijePostojatiUKatalogu()
    {
        var pogodci = TroskovnikService.GetKatalog()
            .Where(s => s.TarifniBroj.StartsWith("Tbr. 25"))
            .Select(s => s.Id)
            .ToList();

        Assert.Empty(pogodci);
    }
}
