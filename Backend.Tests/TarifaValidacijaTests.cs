using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Ništa ne smije tiho vratiti nulu ni tiho nestati iz troškovnika.
///
/// Prije popravka izračun je nepoznatu stavku i količinu ≤ 0 preskakao naredbom
/// `continue`, pa je odvjetnik dobio troškovnik bez stavke koju je odabrao i bez
/// ijedne poruke. Kaznena stavka bez odabrane zapriječene kazne davala je 0 bodova,
/// dakle 0,00 € — iznos koji izgleda kao valjan rezultat.
/// </summary>
public class TarifaValidacijaTests
{
    private static TroskovnikResponse Izracunaj(TroskovnikRequest zahtjev)
        => new TroskovnikService().Izracunaj(zahtjev);

    [Fact]
    public void NepoznataStavka_BacaGresku_NeNestajeTiho()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "ne_postoji", Kolicina = 1 }]
        }));

        Assert.Contains("ne_postoji", greska.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NevaljanaKolicina_BacaGresku(int kolicina)
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = kolicina }]
        }));

        Assert.Contains("količina", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kaznena stavka bez zapriječene kazne nema osnovicu — Tbr. 4. toč. 1 je vezuje
    /// isključivo uz visinu kazne.
    /// </summary>
    [Fact]
    public void KaznenaStavkaBezZaprijeceneKazne_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        }));

        Assert.Contains("zapriječen", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NepoznataZaprijecenaKazna_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            ZaprijecenaKazna = "do_1",   // stari, nepostojeći ključ
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        }));

        Assert.Contains("do_1", greska.Message);
    }

    /// <summary>
    /// Uključeno oduzimanje bez unesene vrijednosti nema po čemu računati Tbr. 7. toč. 1.
    /// </summary>
    [Fact]
    public void OduzimanjeBezVrijednosti_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            IsOduzimanjePrekrsaj = true,
            VrijednostOduzimanja = 0m,
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_prekrsaj_oduzimanje", Kolicina = 1 }]
        }));

        Assert.Contains("oduzimanj", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bez uključenog oduzimanja stavka pada na 100 bodova iz Tbr. 4. toč. 1 al. 7 —
    /// to je valjan iznos i ne smije se odbiti.
    /// </summary>
    [Fact]
    public void PrekrsajBezOduzimanja_Prolazi()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_prekrsaj_oduzimanje", Kolicina = 1 }]
        });

        Assert.Equal(100m, rezultat.UkupnoBodova);
    }

    /// <summary>
    /// Zaštitna mreža: nijedan redak troškovnika ne smije završiti s nula bodova.
    /// </summary>
    [Fact]
    public void NijedanRedak_NemaNulaBodova()
    {
        var servis = new TroskovnikService();

        foreach (var def in TroskovnikService.GetKatalog())
        {
            var zahtjev = new TroskovnikRequest
            {
                VrijednostSpora = 50_000m,
                ZaprijecenaKazna = "do_5",
                IsOduzimanjePrekrsaj = true,
                VrijednostOduzimanja = 10_000m,
                Stavke = [new TroskovnikItemRequest { ItemId = def.Id, Kolicina = 1 }]
            };

            var rezultat = servis.Izracunaj(zahtjev);

            Assert.All(rezultat.Stavke,
                s => Assert.True(s.UkupnoBodova > 0, $"{def.Id} daje 0 bodova"));
        }
    }
}
