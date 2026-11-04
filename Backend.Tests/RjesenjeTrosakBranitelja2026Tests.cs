using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Stvarno rješenje o trošku branitelja po službenoj dužnosti — Općinski sud u Novom
/// Zagrebu, 2026. (primio ga je korisnik, odvjetnica Gabriela Banić, 4. 11. 2026.).
///
/// Prvi pravi primjer iz prakse za Pravilnik NN 22/22, 100/22, 119/25. Ovaj test je SIDRO:
/// ako alat dobije drugi broj, ne prilagođava se test, nego se traži koja stavka odstupa.
///
/// Predmet: KZ čl. 326 st. 1, zapriječena kazna do 8 godina → Tbr. 4 t. 1 al. 3 (do 10
/// godina) = 300 bodova. Vrijednost boda 2,00 €, PDV 25 %, umanjenje 50 % po Pravilniku.
///
/// Važno: sud umanjuje za 50 % SVE stavke nagrade, i fiksne — razmatranje spisa (Tbr. 36,
/// 25 → 12,5 b) i razgovor s okrivljenikom u istražnom zatvoru (Tbr. 4 t. 5, 50 → 25 b).
/// </summary>
public class RjesenjeTrosakBranitelja2026Tests
{
    private static readonly TroskovnikService Servis = new();

    private static TroskovnikItemRequest Stavka(string itemId, int kolicina = 1)
        => new() { ItemId = itemId, Kolicina = kolicina };

    /// <summary>Radnje redoslijedom iz rješenja; uz svaku bodovi po Tarifi i priznato (−50 %).</summary>
    private static readonly (string ItemId, int Kolicina, decimal PoTarifi, decimal Priznato, string Opis)[] Radnje =
    [
        ("raz_razmatranje_spisa",    1, 25m,  12.5m,  "razmatranje spisa (Tbr. 36)"),
        ("kaz_sudjelovanje_istraga", 1, 300m, 150m,   "obrana, prvo ispitivanje pred policijom (Tbr. 3 t. 1 u vezi s Tbr. 4 t. 1 al. 3)"),
        ("kaz_sudjelovanje_istraga", 1, 300m, 150m,   "obrana, prvo ispitivanje pred ODO"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana, ročište o istražnom zatvoru — prvi sat"),
        ("kaz_dodatni_sat",          1, 50m,  25m,    "obrana, ročište o istražnom zatvoru — 1 h 38 min, +1 započeti sat"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana, dokazno ročište"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana, dokazno ročište"),
        ("kaz_zalba_istrazni_zatvor",1, 300m, 150m,   "žalba na rješenje o istražnom zatvoru (Tbr. 5 t. 3)"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana, ročište o produljenju istražnog zatvora (Tbr. 4 t. 1)"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana, sjednica optužnog vijeća"),
        ("kaz_zalba_istrazni_zatvor",1, 300m, 150m,   "žalba na rješenje o produljenju istražnog zatvora"),
        ("kaz_odgovor_optuznica",    1, 300m, 150m,   "odgovor na optužnicu (Tbr. 5 t. 6)"),
        ("kaz_obrana",               1, 300m, 150m,   "obrana na raspravi"),
        ("kaz_razgovor_pritvor",     1, 50m,  25m,    "razgovor s okrivljenikom u istražnom zatvoru (Tbr. 4 t. 5)"),
    ];

    private static TroskovnikRequest Zahtjev() => new()
    {
        UlogaZastupanja = "BRANITELJ",
        IsPoSluzbenojDuznosti = true,
        IsPdvUkljucen = true,
        ZaprijecenaKazna = "do_10", // kazna do 8 godina → Tbr. 4 t. 1 al. 3 (do 10 godina) = 300 b
        Stavke = [.. Radnje.Select(r => Stavka(r.ItemId, r.Kolicina))],
    };

    [Fact]
    public void Rjesenje_UkupnoPoTarifi_Je3425Bodova()
    {
        // Provjera same tablice iz rješenja: 3.425 b po Tarifi, 1.712,5 b priznato.
        Assert.Equal(3425m, Radnje.Sum(r => r.PoTarifi));
        Assert.Equal(1712.5m, Radnje.Sum(r => r.Priznato));
    }

    [Fact]
    public void Rjesenje_SvakaRadnja_DajePriznateBodoveIzRjesenja()
    {
        var odgovor = Servis.Izracunaj(Zahtjev());

        Assert.Equal(Radnje.Length, odgovor.Stavke.Count);
        for (var i = 0; i < Radnje.Length; i++)
        {
            Assert.True(
                Radnje[i].Priznato == odgovor.Stavke[i].UkupnoBodova,
                $"{Radnje[i].Opis}: sud je priznao {Radnje[i].Priznato} b, alat daje " +
                $"{odgovor.Stavke[i].UkupnoBodova} b ({odgovor.Stavke[i].ItemId}, {odgovor.Stavke[i].TarifniBroj}).");
        }
    }

    [Fact]
    public void Rjesenje_Ukupno_Je4281_25Eura()
    {
        var odgovor = Servis.Izracunaj(Zahtjev());

        Assert.Equal(1712.5m, odgovor.UkupnoBodova);
        Assert.Equal(3425.00m, odgovor.UkupnoEurBezPdv);   // 1.712,5 b × 2,00 €
        Assert.Equal(856.25m, odgovor.Pdv);                // 25 %
        Assert.Equal(4281.25m, odgovor.UkupnoEurSPdv);     // iznos koji je sud dosudio
    }

    /// <summary>
    /// Pravilnik umanjuje i fiksne stavke — tvrdi se zasebno jer je to bio izvor odstupanja:
    /// razmatranje spisa je u katalogu u kategoriji „Razno", ne „Kazneni postupak".
    /// </summary>
    [Theory]
    [InlineData("raz_razmatranje_spisa", 12.5)]   // Tbr. 36: 25 → 12,5
    [InlineData("kaz_razgovor_pritvor", 25)]      // Tbr. 4 t. 5: 50 → 25
    [InlineData("kaz_dodatni_sat", 25)]           // Tbr. 4 t. 1: sljedeći sat 50 → 25
    public void Pravilnik_UmanjujeIFiksneStavke(string itemId, double ocekivano)
    {
        // Uz kaznenu stavku, kao u pravom troškovniku iz rješenja.
        var odgovor = Servis.Izracunaj(new TroskovnikRequest
        {
            UlogaZastupanja = "BRANITELJ",
            IsPoSluzbenojDuznosti = true,
            ZaprijecenaKazna = "do_10",
            Stavke = [Stavka("kaz_obrana"), Stavka(itemId)],
        });

        Assert.Equal((decimal)ocekivano, odgovor.Stavke[1].UkupnoBodova);
    }

    /// <summary>
    /// Zaštita: zaostala zastavica „po službenoj dužnosti" u troškovniku BEZ ijedne kaznene
    /// stavke ne smije tiho prepoloviti tuđu nagradu (Tbr. 36 u parničnom/ovršnom predmetu).
    /// </summary>
    [Fact]
    public void Pravilnik_NeDiraRaznoUTroskovnikuBezKaznenihStavki()
    {
        var odgovor = Servis.Izracunaj(new TroskovnikRequest
        {
            IsPoSluzbenojDuznosti = true,
            Stavke = [Stavka("raz_razmatranje_spisa")],
        });

        Assert.Equal(25m, odgovor.UkupnoBodova);
    }
}
