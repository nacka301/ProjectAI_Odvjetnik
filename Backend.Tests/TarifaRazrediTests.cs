using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Regresija koja mora ostati zelena kroz sve popravke tarife.
///
/// Tbr. 7. toč. 1 je temelj cijelog parničnog obračuna. Njegove granice razreda
/// ne diramo — ovaj paket ih zaključava, obje vrijednosti na svakoj granici.
///
/// Tablica doslovno iz Tarife:
///
///   OD EURA      DO EURA        BODOVA
///   0            332,00         25
///   332,01       664,00         50
///   664,01       1.327,00       75
///   1.327,01     13.272,00      100
///   13.272,01    33.181,00      250
///   33.181,01    66.361,00      500
///
/// "Ako vrijednost spora prelazi iznos od 66.361,00 eura do iznosa od 663.614,00
/// eura, odvjetnik pored nagrade od 500 bodova ima pravo obračunati nagradu od
/// 1 boda za svakih započetih 133,00 eura. Iznad vrijednost spora od 663.614,00
/// eura do iznosa od 1.327.228,00 eura, odvjetnik pored nagrade od 4991 boda ima
/// pravo obračunati nagradu od 1 boda za svakih započetih 265,00 eura. Iznad
/// vrijednosti spora od 1.327.228,00 eura, odvjetnik pored nagrade od 7496 bodova
/// ima pravo obračunati nagradu od 1 boda za svakih započetih 664,00 eura, ali
/// ne više od 10000 bodova."
/// </summary>
public class TarifaRazrediTests
{
    // ── Fiksni razredi: obje vrijednosti na svakoj granici ──

    [Theory]
    // razred 1 — do 332,00 €
    [InlineData(0.00, 25)]
    [InlineData(0.01, 25)]
    [InlineData(331.99, 25)]
    [InlineData(332.00, 25)]
    // razred 2 — 332,01 do 664,00 €
    [InlineData(332.01, 50)]
    [InlineData(663.99, 50)]
    [InlineData(664.00, 50)]
    // razred 3 — 664,01 do 1.327,00 €
    [InlineData(664.01, 75)]
    [InlineData(1_326.99, 75)]
    [InlineData(1_327.00, 75)]
    // razred 4 — 1.327,01 do 13.272,00 €
    [InlineData(1_327.01, 100)]
    [InlineData(13_271.99, 100)]
    [InlineData(13_272.00, 100)]
    // razred 5 — 13.272,01 do 33.181,00 €
    [InlineData(13_272.01, 250)]
    [InlineData(33_180.99, 250)]
    [InlineData(33_181.00, 250)]
    // razred 6 — 33.181,01 do 66.361,00 €
    [InlineData(33_181.01, 500)]
    [InlineData(66_360.99, 500)]
    [InlineData(66_361.00, 500)]
    public void FiksniRazredi(decimal vps, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, TroskovnikService.Tbr71Bodovi(vps));
    }

    // ── Progresivni razredi ──

    [Theory]
    // 1 bod za svakih započetih 133,00 € iznad 66.361,00 €
    [InlineData(66_361.01, 501)]
    [InlineData(66_494.00, 501)]
    [InlineData(66_494.01, 502)]
    [InlineData(663_613.99, 4991)]
    [InlineData(663_614.00, 4991)]
    // 1 bod za svakih započetih 265,00 € iznad 663.614,00 €
    [InlineData(663_614.01, 4992)]
    [InlineData(663_879.00, 4992)]
    [InlineData(663_879.01, 4993)]
    [InlineData(1_327_227.99, 7496)]
    [InlineData(1_327_228.00, 7496)]
    // 1 bod za svakih započetih 664,00 € iznad 1.327.228,00 €
    [InlineData(1_327_228.01, 7497)]
    [InlineData(1_327_892.00, 7497)]
    [InlineData(1_327_892.01, 7498)]
    public void ProgresivniRazredi(decimal vps, int ocekivanoBodova)
    {
        Assert.Equal(ocekivanoBodova, TroskovnikService.Tbr71Bodovi(vps));
    }

    // ── Rubovi ──

    [Theory]
    [InlineData(-1)]
    [InlineData(-1_000_000)]
    public void NegativnaVrijednost_PadaNaDnoTablice(decimal vps)
    {
        Assert.Equal(25, TroskovnikService.Tbr71Bodovi(vps));
    }

    [Theory]
    [InlineData(2_990_000)]
    [InlineData(10_000_000)]
    [InlineData(1_000_000_000)]
    public void NikadPreko10000Bodova(decimal vps)
    {
        Assert.Equal(10_000, TroskovnikService.Tbr71Bodovi(vps));
    }

    [Fact]
    public void TablicaJeMonotona()
    {
        var prosli = 0;

        foreach (var vps in new decimal[]
        {
            0, 332, 332.01m, 664, 664.01m, 1_327, 1_327.01m, 13_272, 13_272.01m,
            33_181, 33_181.01m, 66_361, 66_361.01m, 663_614, 663_614.01m,
            1_327_228, 1_327_228.01m, 5_000_000, 100_000_000
        })
        {
            var bodovi = TroskovnikService.Tbr71Bodovi(vps);
            Assert.True(bodovi >= prosli, $"pad bodova na VPS-u {vps}: {prosli} → {bodovi}");
            prosli = bodovi;
        }
    }

    // ── Standardna procjenjiva parnica, od kraja do kraja ──

    /// <summary>
    /// Tipičan parnični troškovnik pri VPS-u od 50.000 €:
    ///   tužba (Tbr. 7.1)              500 bodova
    ///   dva ročišta (Tbr. 9.1)      2 × 500 bodova
    ///   žalba (Tbr. 10.1, +25%)       625 bodova
    ///   ──────────────────────────────────────────
    ///   ukupno                      2.125 bodova = 4.250,00 €
    ///   PDV 25%                                    1.062,50 €
    ///   ──────────────────────────────────────────
    /// Sudske pristojbe po Uredbi NN 50/2026 — dvije, jer Uredba naplaćuje po radnji:
    ///   tužba (Tar. br. 1 st. 1)                     620,00 €
    ///   žalba protiv presude (Tar. br. 3 st. 1)    1.240,00 €
    ///   ──────────────────────────────────────────
    ///   sveukupno                                  7.172,50 €
    ///
    /// Po staroj je Uredbi cijeli troškovnik nosio jednu pristojbu od 546,45 €: žalba
    /// se vodila kao polovica iz Tar. br. 1 st. 2 i utapala se u pristojbu za tužbu.
    /// </summary>
    [Fact]
    public void StandardnaProcjenjivaParnica()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = true,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 2 },
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }
            ]
        });

        Assert.Equal(3, rezultat.Stavke.Count);

        Assert.Equal(500m, rezultat.Stavke[0].UkupnoBodova);
        Assert.Equal(1_000m, rezultat.Stavke[1].UkupnoBodova);
        Assert.Equal(625m, rezultat.Stavke[2].UkupnoBodova);

        Assert.Equal(2_125m, rezultat.UkupnoBodova);
        Assert.Equal(4_250.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(1_062.50m, rezultat.Pdv);
        Assert.Equal(5_312.50m, rezultat.UkupnoEurSPdv);

        Assert.Equal(PristojbaOsnova.Puna, rezultat.PristojbaOsnova);
        Assert.Equal(
            [("tbr1_st1", 620.00m), ("tbr3_st1", 1_240.00m)],
            rezultat.PristojbeneStavke.Select(s => (s.OdredbaId, s.Iznos)));
        Assert.Equal(1_860.00m, rezultat.SudskaPristojba);
        Assert.Equal(7_172.50m, rezultat.SveukupniTrosak);

        Assert.False(rezultat.IsSamoKazneni);
        Assert.Equal(50_000m, rezultat.VrijednostSpora);
    }

    /// <summary>
    /// Ista parnica, ali odvjetnik zastupa tri osobe (Tbr. 40 → +20%).
    /// 2.125 × 1,20 = 2.550 bodova = 5.100,00 €.
    /// </summary>
    [Fact]
    public void StandardnaParnica_SPovisenjemZaTriOsobe()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            BrojZastupanihOsoba = 3,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 2 },
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }
            ]
        });

        Assert.Equal(0.20m, rezultat.PovisenjeViseOsobaPostotak);
        Assert.Equal(425m, rezultat.PovisenjeViseOsobaBodovi);
        Assert.Equal(2_550m, rezultat.UkupnoBodova);
        Assert.Equal(5_100.00m, rezultat.UkupnoEurBezPdv);
    }
}
