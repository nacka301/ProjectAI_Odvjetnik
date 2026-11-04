using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Prekršajni postupak ne traži zapriječenu kaznu (Gabriela Banić, 4. 11. 2026.: „ne otvara
/// mi kaznu pa ne mogu izračunati").
///
/// Tarifa o nagradama (NN 138/23), Tbr. 4. toč. 1 al. 7: obrana u prekršajnom postupku je
/// FIKSNIH 100 bodova, svaki sljedeći započeti sat 50 bodova. Tbr. 1 t. 1 i 2 te Tbr. 5 t. 3
/// (prigovor protiv prekršajnog naloga) upućuju na Tbr. 4. toč. 1 — dakle isto 100.
/// Iznimke: oduzimanje imovinske koristi (al. 8 → Tbr. 7 t. 1 prema vrijednosti) i obrana
/// pravne osobe (al. 9 → prema kazni za odgovornu osobu).
/// </summary>
public class TarifaPrekrsajniPostupakTests
{
    private static readonly TroskovnikService Servis = new();

    private static TroskovnikRequest Prekrsajni(params (string ItemId, int Kolicina)[] stavke) => new()
    {
        UlogaZastupanja = "BRANITELJ",
        IsPrekrsajniPostupak = true,
        IsPdvUkljucen = true,
        // Namjerno bez ZaprijecenaKazna — u prekršajnom je ne tražimo.
        Stavke = [.. stavke.Select(s => new TroskovnikItemRequest { ItemId = s.ItemId, Kolicina = s.Kolicina })],
    };

    [Fact]
    public void Obrana1Sat_Je100Bodova_BezKazne()
    {
        var r = Servis.Izracunaj(Prekrsajni(("kaz_obrana", 1)));

        Assert.Equal(100m, r.UkupnoBodova);
        Assert.Equal(200.00m, r.UkupnoEurBezPdv);
        Assert.Equal(50.00m, r.Pdv);
        Assert.Equal(250.00m, r.UkupnoEurSPdv);
    }

    [Fact]
    public void Obrana2h10min_Je100Plus50Plus50()
    {
        // 2 h 10 min = prvi sat + dva sljedeća započeta sata
        var r = Servis.Izracunaj(Prekrsajni(("kaz_obrana", 1), ("kaz_dodatni_sat", 2)));

        Assert.Equal(200m, r.UkupnoBodova);
        Assert.Equal(400.00m, r.UkupnoEurBezPdv);
    }

    [Theory]
    [InlineData("kaz_privatna_tuzba")]            // Tbr. 1 t. 1 — optužni prijedlog
    [InlineData("kaz_podnesak_obrana")]           // Tbr. 1 t. 2 — pisana obrana
    [InlineData("kaz_zalba_istrazni_zatvor")]     // Tbr. 5 t. 3 — prigovor protiv prekršajnog naloga
    [InlineData("kaz_odgovor_optuznica")]         // Tbr. 5 t. 6
    [InlineData("kaz_sudjelovanje_istraga")]      // Tbr. 3 t. 1
    public void StavkeKojeUpucujuNaTbr4Toc1_UPrekrsajnom_SuStotinuBodova(string itemId)
    {
        var r = Servis.Izracunaj(Prekrsajni((itemId, 1)));

        Assert.Equal(100m, r.UkupnoBodova);
    }

    [Fact]
    public void FiksnaPrekrsajnaStavka_RadiBezKazne_IBezOznakePostupka()
    {
        // Stari pohranjeni zahtjevi: samo `kaz_obrana_prekrsaj`, bez zastavice postupka.
        var r = Servis.Izracunaj(new TroskovnikRequest
        {
            UlogaZastupanja = "BRANITELJ",
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana_prekrsaj", Kolicina = 1 }],
        });

        Assert.Equal(100m, r.UkupnoBodova);
    }

    [Fact]
    public void Prekrsajni_PoSluzbenojDuznosti_UmanjujeSe50Posto()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1), ("kaz_dodatni_sat", 1));
        zahtjev.IsPoSluzbenojDuznosti = true;

        var r = Servis.Izracunaj(zahtjev);

        Assert.Equal(75m, r.UkupnoBodova);     // (100 + 50) × 50 %
    }

    [Fact]
    public void ZaprijecenaKazna_UPrekrsajnom_SeIgnorira()
    {
        // Stari klijent može poslati kaznu iz prethodnog odabira — prekršaj je ne smije čitati.
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.ZaprijecenaKazna = "preko_10";

        Assert.Equal(100m, Servis.Izracunaj(zahtjev).UkupnoBodova);
    }

    [Fact]
    public void Prekrsajni_PovisenjeIzTbr5_ZadrzavaSe()
    {
        // Žalba protiv presude = Tbr. 4 t. 1 + 50 %. Osnovica prekršajnog je 100, ne kazna.
        var r = Servis.Izracunaj(Prekrsajni(("kaz_zalba_presuda", 1)));

        Assert.Equal(150m, r.UkupnoBodova);
    }

    // ── Iznimka: oduzimanje imovinske koristi (al. 8) ──────────────

    [Fact]
    public void Oduzimanje_ObranaIdePoTbr71PremaVrijednosti()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.IsOduzimanjePrekrsaj = true;
        zahtjev.VrijednostOduzimanja = 20_000m;     // Tbr. 7 t. 1 → 250 bodova

        Assert.Equal(250m, Servis.Izracunaj(zahtjev).UkupnoBodova);
    }

    [Fact]
    public void Oduzimanje_BezVrijednosti_TrazaVrijednost()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.IsOduzimanjePrekrsaj = true;

        var ex = Assert.Throws<TroskovnikValidationException>(() => Servis.Izracunaj(zahtjev));
        Assert.Contains("vrijednost", ex.Message);
    }

    // ── Iznimka: obrana pravne osobe (al. 9) ───────────────────────

    [Fact]
    public void PravnaOsoba_IdePoKaziZaOdgovornuOsobu()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.IsPrekrsajPravnaOsoba = true;
        zahtjev.ZaprijecenaKazna = "do_5";          // kazna za odgovornu osobu → 200

        Assert.Equal(200m, Servis.Izracunaj(zahtjev).UkupnoBodova);
    }

    [Fact]
    public void PravnaOsoba_BezKazneZaOdgovornuOsobu_TrazaKaznu()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.IsPrekrsajPravnaOsoba = true;

        var ex = Assert.Throws<TroskovnikValidationException>(() => Servis.Izracunaj(zahtjev));
        Assert.Contains("odgovornu osobu", ex.Message);
    }

    [Fact]
    public void PravnaOsobaIOduzimanje_Zajedno_AlatNeNagadja()
    {
        var zahtjev = Prekrsajni(("kaz_obrana", 1));
        zahtjev.IsPrekrsajPravnaOsoba = true;
        zahtjev.IsOduzimanjePrekrsaj = true;
        zahtjev.ZaprijecenaKazna = "do_5";
        zahtjev.VrijednostOduzimanja = 20_000m;

        Assert.Throws<TroskovnikValidationException>(() => Servis.Izracunaj(zahtjev));
    }

    // ── Kazneni postupak se NE smije promijeniti ───────────────────

    [Fact]
    public void Kazneni_I_Dalje_TrazaZaprijecenuKaznu()
    {
        var ex = Assert.Throws<TroskovnikValidationException>(() => Servis.Izracunaj(new TroskovnikRequest
        {
            UlogaZastupanja = "BRANITELJ",
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }],
        }));

        Assert.Contains("zapriječena kazna", ex.Message);
    }
}
