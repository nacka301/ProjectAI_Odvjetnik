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
        var kaznene = TroskovnikService.GetKatalog()
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
}
