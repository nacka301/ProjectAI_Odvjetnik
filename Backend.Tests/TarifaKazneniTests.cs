using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// U kaznenom se postupku sve veže na zapriječenu kaznu (Tbr. 4. toč. 1), nikad na
/// vrijednost predmeta spora. VPS je institut parničnog postupka i u kaznenom toku
/// ne smije ući ni u jedan izračun ni u spremljeni zapis.
///
/// Jedina iznimka je izričito propisana u Tbr. 4. toč. 1 al. 8: prekršaj s
/// oduzimanjem predmeta ide po Tbr. 7. toč. 1, ali prema vrijednosti ODUZETOG
/// PREDMETA — ne prema vrijednosti spora.
/// </summary>
public class TarifaKazneniTests
{
    private const string KategorijaKazneni = "Kazneni postupak";

    private static TroskovnikRequest Zahtjev(decimal vps, params string[] itemIds)
        => new()
        {
            VrijednostSpora = vps,
            ZaprijecenaKazna = "do_5",
            Stavke = [.. itemIds.Select(id => new TroskovnikItemRequest { ItemId = id, Kolicina = 1 })]
        };

    /// <summary>
    /// Nijedna kaznena stavka ne smije promijeniti broj bodova kad se promijeni VPS.
    /// </summary>
    [Fact]
    public void NijednaKaznenaStavka_NeOvisiOVps()
    {
        var servis = new TroskovnikService();
        var kaznene = TroskovnikService.DohvatiKatalog()
            .Where(s => s.Kategorija == KategorijaKazneni)
            .ToList();

        Assert.NotEmpty(kaznene);

        foreach (var def in kaznene)
        {
            var bezVps = servis.Izracunaj(Zahtjev(0m, def.Id));
            var sVps = servis.Izracunaj(Zahtjev(1_000_000m, def.Id));

            Assert.Equal(bezVps.UkupnoBodova, sVps.UkupnoBodova);
        }
    }

    /// <summary>
    /// Kad su odabrane samo kaznene stavke, VPS se izbacuje iz rezultata — da ne
    /// završi u spremljenom troškovniku ni na ispisu kao "vrijednost predmeta spora".
    /// </summary>
    [Fact]
    public void SamoKazneniPredmet_NeNosiVps()
    {
        var rezultat = new TroskovnikService().Izracunaj(Zahtjev(50_000m, "kaz_obrana", "kaz_zalba_presuda"));

        Assert.True(rezultat.IsSamoKazneni);
        Assert.Equal(0m, rezultat.VrijednostSpora);
    }

    /// <summary>
    /// Mješoviti troškovnik (kazneni + parnični) i dalje je parnični predmet — VPS ostaje.
    /// </summary>
    [Fact]
    public void MjesovitiPredmet_ZadrzavaVps()
    {
        var rezultat = new TroskovnikService().Izracunaj(Zahtjev(50_000m, "kaz_obrana", "par_tuzba"));

        Assert.False(rezultat.IsSamoKazneni);
        Assert.Equal(50_000m, rezultat.VrijednostSpora);
    }

    /// <summary>
    /// Tbr. 4. toč. 1 al. 8 — prekršaj s oduzimanjem veže se na vrijednost oduzetog
    /// predmeta, a ne na VPS. Ta iznimka mora preživjeti izbacivanje VPS-a.
    /// </summary>
    [Fact]
    public void PrekrsajSOduzimanjem_KoristiVrijednostOduzimanja_NeVps()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 1_000_000m,          // mora se ignorirati
            IsOduzimanjePrekrsaj = true,
            VrijednostOduzimanja = 20_000m,        // Tbr. 7. toč. 1 → 250 bodova
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_prekrsaj_oduzimanje", Kolicina = 1 }]
        });

        Assert.Equal(250, rezultat.UkupnoBodova);
        Assert.Equal(0m, rezultat.VrijednostSpora);
    }

    // ── Popis nedostataka koji je iznijela odvjetnica ──────────────
    //
    // Sve su stavke otad dodane u katalog. Ovo je brava, ne nova funkcionalnost:
    // ako neka od njih nestane iz TarifaKatalog.Sve, test pada i pokazuje koju je
    // odredbu izmjena ostavila bez pokrića.

    [Theory]
    [InlineData("kaz_podnesak_obrana", "Tbr. 1.2")]              // podnesci i pisana obrana
    [InlineData("kaz_zahtjev_ostecenik", "Tbr. 1.3")]            // zahtjev oštećenika
    [InlineData("kaz_odgoda_izvrsenja", "Tbr. 2.1")]             // Tbr. 2 uopće
    [InlineData("kaz_rehabilitacija", "Tbr. 2.2")]
    [InlineData("kaz_pomilovanje", "Tbr. 2.3")]
    [InlineData("kaz_opoziv_uvjetne", "Tbr. 2.4")]
    [InlineData("kaz_sudjelovanje_istraga", "Tbr. 3.1")]         // Tbr. 3 uopće
    [InlineData("kaz_istraga_izvan_sjedista", "Tbr. 3.2")]
    [InlineData("kaz_istraga_ostecenik", "Tbr. 3.3")]
    [InlineData("kaz_istraga_odgoda", "Tbr. 3.4")]
    [InlineData("kaz_prekrsaj_oduzimanje", "Tbr. 4.1")]          // al. 8 — po Tbr. 7. toč. 1
    [InlineData("kaz_obrana_pravna_osoba", "Tbr. 4.1")]          // al. 9 — po kazni za odgovornu osobu
    [InlineData("kaz_predstavnik_sud", "Tbr. 4.1")]              // al. 10 — kad ga postavi sud
    [InlineData("kaz_predstavnik_pravna_osoba", "Tbr. 4.1")]     // al. 10 → al. 9
    public void KazneniKatalog_ImaSveStavkeSPopisaOdvjetnice(string itemId, string tarifniBroj)
    {
        var stavka = TarifaKatalog.Sve.SingleOrDefault(s => s.Id == itemId);

        Assert.NotNull(stavka);
        Assert.Equal(KategorijaKazneni, stavka.Kategorija);
        Assert.Equal(tarifniBroj, stavka.TarifniBroj);
    }

    /// <summary>
    /// Tbr. 4. toč. 1 al. 10: „100 bodova za prvi sat svakog dana rasprave, 50 bodova
    /// za svaki sljedeći započeti sat" — kad predstavnika pravne osobe postavi sud.
    /// Taj iznos ne ovisi o zapriječenoj kazni.
    /// </summary>
    [Fact]
    public void PredstavnikPravneOsobe_KadGaPostaviSud_StoBodovaBezObziraNaKaznu()
    {
        var stavka = TarifaKatalog.Sve.Single(s => s.Id == "kaz_predstavnik_sud");

        Assert.Equal(100, stavka.FiksniBody);
        Assert.False(stavka.KazneniBase);
    }

    /// <summary>
    /// Tbr. 4. toč. 1 al. 6 — stjecaj ne uvodi množitelj nego određuje KOJI se razred
    /// uzima. Nagrada ostaje jedna, a razlog odabira razreda mora se vidjeti.
    /// </summary>
    [Fact]
    public void Stjecaj_NeMijenjaIznos_NegoObrazlazeOdabraniRazred()
    {
        var bez = new TroskovnikService().Izracunaj(Zahtjev(0m, "kaz_obrana"));

        var zahtjev = Zahtjev(0m, "kaz_obrana");
        zahtjev.IsStjecaj = true;
        var sa = new TroskovnikService().Izracunaj(zahtjev);

        Assert.Equal(bez.UkupnoBodova, sa.UkupnoBodova);
        Assert.Null(bez.NapomenaStjecaj);
        Assert.NotNull(sa.NapomenaStjecaj);
    }
}
