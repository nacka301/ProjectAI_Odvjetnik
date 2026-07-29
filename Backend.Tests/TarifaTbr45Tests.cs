using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Tbr. 45 — neprocjenjivi predmeti.
///
/// Tarifa, Tbr. 45: "U neprocjenjivim predmetima koji nisu predviđeni tarifnim odredbama
/// Tarife (npr. tužbe na utvrđenje), a niti se vrijednost predmeta spora može na drugi
/// način utvrditi, odvjetnik ima pravo za svaku poduzetu radnju na nagradu:
///   1. U postupku pred općinskim sudom i tijelima iste razine u visini 100 bodova.
///   2. U postupku pred županijskim i trgovačkim sudovima te tijelima iste razine
///      u visini od 200 bodova.
///   3. U postupku pred Vrhovnim sudom Republike Hrvatske i tijelima iste razine
///      u visini od 500 bodova."
///
/// Prije popravka neprocjenjiv predmet (VPS = 0) padao je na Tbr71Bodovi(0) = 25 bodova —
/// greška do dvadeseterostruke (25 umjesto 500 pred Vrhovnim sudom).
/// </summary>
public class TarifaTbr45Tests
{
    private static TroskovnikResponse Izracunaj(
        string itemId, string razinaSuda, string nacin, int kolicina = 1)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 0m,
            OsnovicaTbr7 = OsnovicaTbr7.Tbr45Neprocjenjiv,
            RazinaSuda = razinaSuda,
            Tbr45Nacin = nacin,
            Stavke = [new TroskovnikItemRequest { ItemId = itemId, Kolicina = kolicina }]
        });

    [Theory]
    [InlineData(RazinaSuda.Opcinski,            100)]
    [InlineData(RazinaSuda.ZupanijskiTrgovacki, 200)]
    [InlineData(RazinaSuda.Vrhovni,             500)]
    public void RazinaSuda_OdredujeOsnovicu_NePadaNa25(string razina, decimal ocekivano)
    {
        var r = Izracunaj("par_odgovor_tuzba", razina, Tbr45Nacin.KaoOsnovica);

        Assert.Equal(ocekivano, r.OsnovicaBodovi);
        Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
        Assert.NotEqual(25m, r.OsnovicaBodovi);
    }

    /// <summary>
    /// NEJASNO U TARIFI: Tbr. 45 daje iznos „za svaku poduzetu radnju", ali ne kaže
    /// primjenjuju li se na njega postoci iz Tbr. 8 (50 % / 25 %) i Tbr. 9. Oba su čitanja
    /// obranjiva, pa korisnik bira — sustav ne pretpostavlja.
    ///
    /// RUČNI IZRAČUN, općinski sud (100 bodova):
    ///   kao_osnovica     → Tbr. 8.3 = 50 % od 100 = 50 bodova
    ///                    → Tbr. 8.4 = 25 % od 100 = 25 bodova (strop 25 ne djeluje)
    ///   fiksno_po_radnji → Tbr. 8.3 = 100, Tbr. 8.4 = 100 bodova
    ///
    /// RUČNI IZRAČUN, županijski/trgovački sud (200 bodova):
    ///   kao_osnovica     → Tbr. 8.4 = 25 % od 200 = 50 → strop 25 → 25 bodova
    ///   fiksno_po_radnji → Tbr. 8.4 = 200 bodova (strop se ne primjenjuje)
    /// </summary>
    [Theory]
    [InlineData(RazinaSuda.Opcinski,            Tbr45Nacin.KaoOsnovica,    "par_obrazlozeni_podnesak",  50)]
    [InlineData(RazinaSuda.Opcinski,            Tbr45Nacin.KaoOsnovica,    "par_ostali_podnesci",       25)]
    [InlineData(RazinaSuda.Opcinski,            Tbr45Nacin.FiksnoPoRadnji, "par_obrazlozeni_podnesak", 100)]
    [InlineData(RazinaSuda.Opcinski,            Tbr45Nacin.FiksnoPoRadnji, "par_ostali_podnesci",      100)]
    [InlineData(RazinaSuda.ZupanijskiTrgovacki, Tbr45Nacin.KaoOsnovica,    "par_ostali_podnesci",       25)]
    [InlineData(RazinaSuda.ZupanijskiTrgovacki, Tbr45Nacin.FiksnoPoRadnji, "par_ostali_podnesci",      200)]
    [InlineData(RazinaSuda.Vrhovni,             Tbr45Nacin.KaoOsnovica,    "par_rociste_presuda",       50)]
    [InlineData(RazinaSuda.Vrhovni,             Tbr45Nacin.FiksnoPoRadnji, "par_rociste_presuda",      500)]
    public void ObaCitanjaDajuRazliciteIznose(
        string razina, string nacin, string itemId, decimal ocekivano)
    {
        var r = Izracunaj(itemId, razina, nacin);

        Assert.Equal(ocekivano, r.Stavke.Single().BodovaPoJedinici);
    }

    [Fact]
    public void ApsolutniIznosiOstajuNetaknutiIUFiksnomNacinu()
    {
        // Tbr. 7. toč. 10 daje 250 bodova izravno — Tbr. 45 ga ne dira.
        var r = Izracunaj("par_zahtjev_haska", RazinaSuda.Opcinski, Tbr45Nacin.FiksnoPoRadnji);

        Assert.Equal(250m, r.Stavke.Single().BodovaPoJedinici);
    }

    [Fact]
    public void BezRazineSuda_BacaGresku_NePadaTihoNa100()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() =>
            new TroskovnikService().Izracunaj(new TroskovnikRequest
            {
                OsnovicaTbr7 = OsnovicaTbr7.Tbr45Neprocjenjiv,
                Tbr45Nacin = Tbr45Nacin.KaoOsnovica,
                RazinaSuda = null,
                Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
            }));

        Assert.Contains("razin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BezOdabranogNacina_BacaGresku_NePretpostavlja()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() =>
            new TroskovnikService().Izracunaj(new TroskovnikRequest
            {
                OsnovicaTbr7 = OsnovicaTbr7.Tbr45Neprocjenjiv,
                RazinaSuda = RazinaSuda.Opcinski,
                Tbr45Nacin = null,
                Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
            }));

        Assert.Contains("način", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
