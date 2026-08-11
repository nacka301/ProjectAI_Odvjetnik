using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Karakterizacijski testovi pred uvođenje datuma uz radnju.
///
/// Datum uz radnju traži da se ista tarifna stavka može poslati VIŠE PUTA — tri
/// ročišta na tri datuma su tri retka, a ne jedan redak s količinom 3. Prije nego se
/// model dirne, ovdje se zaključava iznos koji troškovnik daje danas, da se poslije
/// vidi je li ga promjena pomaknula.
///
/// Iznosi zabilježeni prije izmjene ostali su ovdje netaknuti i dalje ih drže isti
/// testovi. Dva testa koja su bilježila umnožavanje jednokratne nagrade obrnuta su u
/// tvrdnju kad je grupiranje uvedeno — jer to ponašanje više nije zatečeno, nego
/// popravljeno.
/// </summary>
public class TroskovnikRedakPoRadnjiKarakterizacijaTests
{
    /// <summary>VPS 50.000 € pada u razred 33.181,01 – 66.361,00 → 500 bodova (Tbr. 7. toč. 1).</summary>
    private const decimal Vps = 50_000m;
    private const decimal BodoviTbr71 = 500m;

    private static TroskovnikResponse Izracunaj(params TroskovnikItemRequest[] stavke)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = Vps,
            Stavke = [.. stavke]
        });

    private static TroskovnikItemRequest Stavka(string itemId, int kolicina = 1)
        => new() { ItemId = itemId, Kolicina = kolicina };

    // ── Zlatni troškovnik ─────────────────────────────────────────

    /// <summary>
    /// Mješoviti troškovnik kakav odvjetnik stvarno slaže: tužba, tri ročišta i
    /// jednokratna nagrada s dvije radnje preko uključenih četiri.
    ///
    /// 500 (tužba) + 3 × 500 (ročišta) + 350 (250 + 2 × 50) = 2.350 bodova.
    /// Ovaj broj ne smije se pomaknuti ni nakon jedne izmjene u ovom zadatku.
    /// </summary>
    [Fact]
    public void ZlatniTroskovnik_DajeZatecenIznos()
    {
        var rezultat = Izracunaj(
            Stavka("par_tuzba"),
            Stavka("par_rociste_glavno", 3),
            Stavka("par_tuzba_smetanje", 6));

        Assert.Equal(2_350m, rezultat.UkupnoBodova);
        Assert.Equal(4_700.00m, rezultat.UkupnoEurBezPdv);

        Assert.Collection(rezultat.Stavke,
            r => Assert.Equal(BodoviTbr71, r.UkupnoBodova),
            r => Assert.Equal(3 * BodoviTbr71, r.UkupnoBodova),
            r => Assert.Equal(350m, r.UkupnoBodova));
    }

    // ── Obične stavke: količina je množitelj ──────────────────────

    /// <summary>
    /// Za stavku koja nije jednokratna nagrada bodovi su umnožak, pa je svejedno
    /// šalje li se jedan redak s količinom tri ili tri retka s količinom jedan.
    ///
    /// Ovo je uvjet bez kojeg redak-po-radnji ne bi bio moguć, i mora vrijediti i
    /// nakon izmjene — za ovakve stavke datum ne mijenja ništa u računu.
    /// </summary>
    [Fact]
    public void ObicnaStavka_TriRetkaPoJedan_JednakoJednomRetkuSTri()
    {
        var jedanRedak = Izracunaj(Stavka("par_rociste_glavno", 3));

        var triRetka = Izracunaj(
            Stavka("par_rociste_glavno"),
            Stavka("par_rociste_glavno"),
            Stavka("par_rociste_glavno"));

        Assert.Equal(jedanRedak.UkupnoBodova, triRetka.UkupnoBodova);
        Assert.Equal(jedanRedak.UkupnoEurBezPdv, triRetka.UkupnoEurBezPdv);
        Assert.Equal(3 * BodoviTbr71, triRetka.UkupnoBodova);

        // Retci se ne stapaju — svaki dolazi sa svojim rednim brojem.
        Assert.Equal(3, triRetka.Stavke.Count);
        Assert.Equal([1, 2, 3], triRetka.Stavke.Select(r => r.Rbr));
    }

    // ── Jednokratne nagrade: ovdje je zamka ───────────────────────

    /// <summary>
    /// Tbr. 7. toč. 2 propisuje JEDNU nagradu od 250 bodova za cijeli prvostupanjski
    /// postupak, "koja obuhvaća najviše do četiri radnje". Kako te četiri radnje stižu —
    /// kao jedan redak s količinom četiri ili kao četiri retka s vlastitim datumima —
    /// Tarifu se ne tiče, pa iznos mora biti isti.
    /// </summary>
    [Fact]
    public void JednokratnaNagrada_CetiriRetka_JednakoJednomRetkuSCetiri()
    {
        var jedanRedak = Izracunaj(Stavka("par_tuzba_smetanje", 4));
        var cetiriRetka = Izracunaj(
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"));

        Assert.Equal(250m, jedanRedak.UkupnoBodova);
        Assert.Equal(250m, cetiriRetka.UkupnoBodova);

        // Sve četiri radnje ostaju vidljive — nagradu nosi prva, ostale su obuhvaćene.
        Assert.Equal(4, cetiriRetka.Stavke.Count);
        Assert.Equal(250m, cetiriRetka.Stavke[0].UkupnoBodova);
        Assert.All(cetiriRetka.Stavke.Skip(1), redak =>
        {
            Assert.True(redak.ObuhvacenoJednokratnom);
            Assert.Equal(0m, redak.UkupnoBodova);
            Assert.Equal(0m, redak.BodovaPoJedinici);
            Assert.Equal(0m, redak.IznosEur);
        });
        Assert.False(cetiriRetka.Stavke[0].ObuhvacenoJednokratnom);
    }

    /// <summary>
    /// Radnje preko uključene četiri nose svojih 50 bodova i kad stignu kao zasebni
    /// retci: šest radnji je 250 + 2 × 50 = 350, isto kao jedan redak s količinom šest.
    /// </summary>
    [Fact]
    public void JednokratnaNagrada_DaljnjeRadnje_BrojeSeKrozCijeluGrupu()
    {
        var sestRedaka = Izracunaj(Enumerable
            .Range(0, 6)
            .Select(_ => Stavka("par_tuzba_smetanje"))
            .ToArray());

        Assert.Equal(350m, sestRedaka.UkupnoBodova);
        Assert.Equal(Izracunaj(Stavka("par_tuzba_smetanje", 6)).UkupnoBodova, sestRedaka.UkupnoBodova);
    }

    /// <summary>
    /// Jednokratna nagrada bez „daljnjih radnji" (Tbr. 7. toč. 9, 50 bodova za cijeli
    /// postupak): ni količina ni broj redaka ne mijenjaju ništa.
    /// </summary>
    [Fact]
    public void JednokratnaNagradaBezDaljnjihRadnji_NiKolicinaNiBrojRedakaNeMijenjajuNista()
    {
        Assert.Equal(50m, Izracunaj(Stavka("par_mali_spor", 1)).UkupnoBodova);
        Assert.Equal(50m, Izracunaj(Stavka("par_mali_spor", 7)).UkupnoBodova);
        Assert.Equal(50m, Izracunaj(Stavka("par_mali_spor"), Stavka("par_mali_spor")).UkupnoBodova);
    }

    /// <summary>
    /// Grupira se po tarifnoj stavci, ne po svim jednokratnima zajedno — dvije različite
    /// jednokratne nagrade u istom troškovniku ostaju dvije nagrade.
    /// </summary>
    [Fact]
    public void DvijeRazliciteJednokratneStavke_OstajuDvijeNagrade()
    {
        var rezultat = Izracunaj(
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_obiteljski"),
            Stavka("par_tuzba_smetanje"));

        Assert.Equal(450m, rezultat.UkupnoBodova);   // 250 + 200
    }

    /// <summary>
    /// Retci grupe ne moraju biti jedan do drugoga: nagradu nosi PRVI redak grupe, a
    /// stavka između njih računa se posve neovisno.
    /// </summary>
    [Fact]
    public void RetciGrupe_NeMorajuBitiSusjedni()
    {
        var rezultat = Izracunaj(
            Stavka("par_tuzba_smetanje"),
            Stavka("par_rociste_glavno"),
            Stavka("par_tuzba_smetanje"));

        Assert.Equal(250m + BodoviTbr71, rezultat.UkupnoBodova);
        Assert.False(rezultat.Stavke[0].ObuhvacenoJednokratnom);
        Assert.False(rezultat.Stavke[1].ObuhvacenoJednokratnom);
        Assert.True(rezultat.Stavke[2].ObuhvacenoJednokratnom);
    }

    // ── Zbrojevi koji vise o bodovima ─────────────────────────────

    /// <summary>
    /// Tbr. 40 (zastupanje više osoba) i PDV računaju se iz ukupnih bodova, pa svaka
    /// promjena u zbrajanju redaka odmah pomakne i njih. Zaključava se cijeli lanac.
    /// </summary>
    [Fact]
    public void PovisenjeIPdv_RacunajuSeIzUkupnihBodova()
    {
        var rezultat = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = Vps,
            BrojZastupanihOsoba = 3,
            IsPdvUkljucen = true,
            Stavke = [Stavka("par_rociste_glavno", 2)]
        });

        // Osnovica 1.000 bodova, Tbr. 40 daje +20% za drugu i treću osobu.
        Assert.Equal(0.20m, rezultat.PovisenjeViseOsobaPostotak);
        Assert.Equal(200m, rezultat.PovisenjeViseOsobaBodovi);
        Assert.Equal(1_200m, rezultat.UkupnoBodova);
        Assert.Equal(2_400.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(600.00m, rezultat.Pdv);
        Assert.Equal(3_000.00m, rezultat.UkupnoEurSPdv);
    }
}
