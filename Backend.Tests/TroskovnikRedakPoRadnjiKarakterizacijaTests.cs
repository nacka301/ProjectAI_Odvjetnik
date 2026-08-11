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
/// Karakterizacijski test bilježi ZATEČENO ponašanje, kakvo god bilo. Jedan test
/// ovdje bilježi ponašanje koje je pogrešno i koje se u sljedećem koraku namjerno
/// mijenja — takav je izričito označen.
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
    /// ZATEČENO PONAŠANJE, MIJENJA SE U SLJEDEĆEM KORAKU.
    ///
    /// Tbr. 7. toč. 2 propisuje JEDNU nagradu od 250 bodova za cijeli prvostupanjski
    /// postupak. `UkupnoBodovaZaStavku` tu nagradu vraća po SVAKOM retku, pa četiri
    /// retka daju 1.000 bodova umjesto 250. Dok je stavka mogla postojati samo jednom
    /// (sučelje je držalo mapu item_id → količina) to se nije moglo dogoditi.
    ///
    /// Uvođenjem retka po radnji postaje dostižno, pa se ovaj test u koraku
    /// „grupiranje jednokratnih nagrada" obrće u tvrdnju da četiri retka daju 250.
    /// </summary>
    [Fact]
    public void JednokratnaNagrada_CetiriRetka_ZasadUmnozavaNagradu()
    {
        var jedanRedak = Izracunaj(Stavka("par_tuzba_smetanje", 4));
        var cetiriRetka = Izracunaj(
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"),
            Stavka("par_tuzba_smetanje"));

        Assert.Equal(250m, jedanRedak.UkupnoBodova);
        Assert.Equal(1_000m, cetiriRetka.UkupnoBodova);
        Assert.NotEqual(jedanRedak.UkupnoBodova, cetiriRetka.UkupnoBodova);
    }

    /// <summary>
    /// Isti mehanizam s jednokratnom nagradom bez „daljnjih radnji" (Tbr. 7. toč. 9,
    /// 50 bodova za cijeli postupak): količina ne mijenja ništa, ali broj redaka hoće.
    /// </summary>
    [Fact]
    public void JednokratnaNagradaBezDaljnjihRadnji_KolicinaNeMijenjaNista()
    {
        Assert.Equal(50m, Izracunaj(Stavka("par_mali_spor", 1)).UkupnoBodova);
        Assert.Equal(50m, Izracunaj(Stavka("par_mali_spor", 7)).UkupnoBodova);

        // ZATEČENO: dva retka daju dvostruko. Mijenja se u sljedećem koraku.
        Assert.Equal(100m, Izracunaj(Stavka("par_mali_spor"), Stavka("par_mali_spor")).UkupnoBodova);
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
