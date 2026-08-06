using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Zatvaranje preostalih rupa u katalogu: Tbr. 31. toč. 4 i Tbr. 32. toč. 3, 4 i 5.
/// Obje su skupine izvedene iz točaka koje su već bile u katalogu, pa su im iznosi
/// fiksni i provjerljivi izravno iz teksta.
///
/// Sadrži i negativni test za Tbr. 38 — vidi obrazloženje uz njega.
/// </summary>
public class TarifaKatalogDopunaTests
{
    private static TroskovnikResponse Izracunaj(string itemId, decimal vps = 0m)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = 1 }]
        });

    // ── Tbr. 31. toč. 4 — Ustavni sud / ECHR / Sud EU ────────────────

    /// <summary>
    /// Tbr. 31. toč. 4: "Za zastupanje i sudjelovanje na raspravama na kojima se
    /// raspravljalo samo o procesnim pitanjima, ili je postupak obustavljen prije
    /// raspravljanja o glavnoj stvari odvjetniku pripada 50% nagrade iz toč. 1. ovog
    /// tarifnog broja."
    ///
    /// Toč. 1 daje 1000 bodova. Ručni izračun: 50% × 1000 = 500 bod. = 1.000,00 €.
    /// Katalog je imao 31.1, 31.2 i 31.3, ali ne i 31.4.
    /// </summary>
    [Fact]
    public void Tbr314_ProcesnaRasprava_Daje50PostoOdTocke1()
    {
        var rezultat = Izracunaj("ust_rasprava_procesna");

        Assert.Equal(500, rezultat.UkupnoBodova);
        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Izraženo kao odnos: toč. 4 mora biti točno polovica toč. 1.</summary>
    [Fact]
    public void Tbr314_JeTocnoPolovicaTocke1()
    {
        Assert.Equal(
            Izracunaj("ust_pokretanje").UkupnoBodova * 0.5m,
            Izracunaj("ust_rasprava_procesna").UkupnoBodova);
    }

    /// <summary>Postupci pred Ustavnim sudom nisu procjenjivi — VPS ih ne pomiče.</summary>
    [Fact]
    public void Tbr314_NeOvisiOVps()
    {
        Assert.Equal(
            Izracunaj("ust_rasprava_procesna", 0m).UkupnoBodova,
            Izracunaj("ust_rasprava_procesna", 500_000m).UkupnoBodova);
    }

    // ── Tbr. 32. toč. 3, 4 i 5 — stegovni postupak ───────────────────

    /// <summary>
    /// Sve tri točke izvode se iz toč. 2, koja daje 100 bodova:
    ///
    /// Toč. 3: "Za sastavljanje ostalih podnesaka u tijeku postupka iz toč. 2. ovog
    /// tarifnog broja odvjetniku pripada 25 % nagrade iz toč. 2."
    ///   → 25% × 100 = 25 bod. = 50,00 €
    ///
    /// Toč. 4: "Za zastupanje i sudjelovanje na raspravama u postupku iz toč. 2. ovog
    /// tarifnog broja odvjetniku pripada nagrada iz toč. 2."
    ///   → 100 bod. = 200,00 €
    ///
    /// Toč. 5: "Za zastupanje na raspravi u postupku iz toč. 2. (...) na kojoj se
    /// raspravljalo samo o procesnim pitanjima, ili je postupak obustavljen prije
    /// raspravljanja o glavnoj stvari, odvjetniku pripada 25% nagrade iz toč. 2."
    ///   → 25% × 100 = 25 bod. = 50,00 €
    /// </summary>
    [Theory]
    [InlineData("ost_stegovni_ostali", 25, 50.00)]              // Tbr. 32.3
    [InlineData("ost_stegovni_rasprava", 100, 200.00)]          // Tbr. 32.4
    [InlineData("ost_stegovni_rasprava_procesna", 25, 50.00)]   // Tbr. 32.5
    public void Tbr32_IzvedeneTockeSuPostotakTocke2(string itemId, int bodovi, decimal eur)
    {
        var rezultat = Izracunaj(itemId);

        Assert.Equal(bodovi, rezultat.UkupnoBodova);
        Assert.Equal(eur, rezultat.UkupnoEurBezPdv);
    }

    /// <summary>Izraženo kao odnos prema toč. 2 (ost_stegovni, 100 bodova).</summary>
    [Theory]
    [InlineData("ost_stegovni_ostali", 0.25)]
    [InlineData("ost_stegovni_rasprava", 1.0)]
    [InlineData("ost_stegovni_rasprava_procesna", 0.25)]
    public void Tbr32_OdnosPremaTocki2(string itemId, decimal udio)
    {
        Assert.Equal(
            Izracunaj("ost_stegovni").UkupnoBodova * udio,
            Izracunaj(itemId).UkupnoBodova);
    }

    /// <summary>Stegovni postupak nije procjenjiv — VPS ga ne pomiče.</summary>
    [Theory]
    [InlineData("ost_stegovni_ostali")]
    [InlineData("ost_stegovni_rasprava")]
    [InlineData("ost_stegovni_rasprava_procesna")]
    public void Tbr32_IzvedeneTockeNeOviseOVps(string itemId)
    {
        Assert.Equal(Izracunaj(itemId, 0m).UkupnoBodova, Izracunaj(itemId, 500_000m).UkupnoBodova);
    }

    // ── Tbr. 38 — rezidualno pravilo, NE stavka ──────────────────────

    /// <summary>
    /// Tbr. 38: "Ako pojedinim tarifnim odredbama, prema kojima se određeni poslovi
    /// naknađuju po utrošenom satu, nije drugačije određeno, nagrada za utrošeni sat
    /// iznosi 25 bodova."
    ///
    /// Tbr. 38 je REZIDUALNO PRAVILO, ne obračunska stavka. Svaka odredba Tarife koja
    /// posao naknađuje po satu propisuje vlastitu satnicu:
    ///   Tbr. 2 (50), Tbr. 4. toč. 2 (50), Tbr. 4. toč. 3 (50), Tbr. 29. toč. 4 (100),
    ///   Tbr. 34. toč. 1 (50), Tbr. 35 (50), Tbr. 36 (25), Tbr. 39 (25).
    ///
    /// Ne postoji dakle nijedna odredba na koju bi se Tbr. 38 danas primijenio. Stavka
    /// "25 bodova po satu" izmislila bi mu primjenu i omogućila obračun rada koji Tarifa
    /// ne priznaje kao zasebnu radnju.
    ///
    /// Isti obrazac kao Tbr25_JavnaNabava_NeSmijePostojatiUKatalogu — razlog je drugi
    /// (Tbr. 25 je ukinut sudskom presudom, Tbr. 38 je na snazi ali nije stavka), ali
    /// posljedica je ista: u katalogu ga ne smije biti.
    /// </summary>
    [Fact]
    public void Tbr38_NijeStavkaKataloga()
    {
        var pogodci = TroskovnikService.DohvatiKatalog()
            .Where(s => s.TarifniBroj.StartsWith("Tbr. 38"))
            .Select(s => s.Id)
            .ToList();

        Assert.Empty(pogodci);
    }

    /// <summary>
    /// Zaključava nalaz iz prethodnog testa: svaka stavka s jedinicom "sat" mora svoj iznos
    /// izvoditi iz vlastite odredbe — bilo fiksnim bodovima, postotkom Tbr. 7. toč. 1,
    /// kaznenom osnovicom, ili vrijednošću oduzetog predmeta po Tbr. 4. toč. 1 al. 8.
    ///
    /// Satna stavka bez ijednog od tih izvora značila bi da je netko posegnuo za rezidualom
    /// iz Tbr. 38 umjesto da nađe odredbu koja satnicu propisuje.
    /// </summary>
    [Fact]
    public void SatneStavke_IzvodeIznosIzVlastiteOdredbe()
    {
        var satne = TroskovnikService.DohvatiKatalog()
            .Where(s => s.Jedinica == "sat")
            .ToList();

        Assert.NotEmpty(satne);
        Assert.All(satne, s => Assert.True(
            s.FiksniBody.HasValue || s.Tbr71Postotak.HasValue || s.KazneniBase || s.PrekrsajOduzimanje,
            $"Stavka \"{s.Opis}\" ({s.TarifniBroj}) obračunava se po satu, a iznos ne izvodi " +
            "ni iz jedne odredbe. Tbr. 38 nije zamjena — svaka odredba po satu propisuje svoj iznos."));
    }
}
