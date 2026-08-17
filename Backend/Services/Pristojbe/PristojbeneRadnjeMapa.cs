namespace Backend.Services.Pristojbe;

/// <summary>
/// Koju pristojbenu radnju povlači pojedina stavka odvjetničke tarife.
///
/// Zasebna datoteka, a ne polje na <c>TarifnaStavka</c>: katalog opisuje Tarifu HOK-a
/// (odvjetnička nagrada), a ovo je Uredba o sudskim pristojbama — drugi propis, drugi
/// razlog za izmjenu. <see cref="TarifaKatalog"/> u svom uvodu isto obrazlaže za izračun.
///
/// Stavka koje ovdje nema ne povlači pristojbu. To je namjerno i pokriva veliku većinu
/// kataloga: Tar. br. 1 napomena 5 kaže „ne plaća se pristojba za podneske koji nisu
/// navedeni u ovom Tar. br.", a ročišta, savjeti, ugovori i zastupanje pred upravnim
/// tijelima uopće nisu sudske radnje.
/// </summary>
public static class PristojbeneRadnjeMapa
{
    /// <summary>
    /// Stavka kataloga → odredbe Uredbe koje ona povlači.
    ///
    /// Kad dvije odabrane stavke vode na istu odredbu, pristojba se naplaćuje JEDNOM —
    /// vidi <see cref="Backend.Services.SudskePristojbeCalculator"/>. Jedan podnesak je
    /// jedna pristojba i kad se u troškovniku pojavi kroz više tarifnih stavaka.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> PoStavci =
        new Dictionary<string, string[]>
        {
            // ── Poglavlje I. — parnični postupak ──
            // Tar. br. 1 st. 1: tužba, protutužba, prigovor protiv platnog naloga.
            ["par_tuzba"] = ["tbr1_st1"],
            ["par_tuzba_smetanje"] = ["tbr1_st1"],
            ["par_tuzba_obiteljski"] = ["tbr1_st1"],
            ["par_zahtjev_haska"] = ["tbr1_st1"],

            // Tar. br. 1 st. 2: tužba S PRIJEDLOGOM za izdavanje platnog naloga —
            // polovica, jer se platni nalog izdaje bez rasprave.
            ["par_tuzba_platni_nalog"] = ["tbr1_st2"],
            ["par_tuzba_vrijednosni_papiri"] = ["tbr1_st2"],
            ["par_povrat_u_prijasnje_stanje"] = ["tbr1_st2"],

            // Odgovori iz nabrajanja u st. 2. `par_odgovor_tuzba` je jedan od dva
            // prijelazna slučaja iz čl. 2 — vidi ItemiOdgovorNaTuzbu.
            ["par_odgovor_tuzba"] = ["tbr1_st2"],
            ["par_odgovor_zalba"] = ["tbr1_st2"],
            ["par_odgovor_zalba_jednokratno"] = ["tbr1_st2"],

            // Tar. br. 2 — odluke. Automatski se izvodi samo ona koja se iz stavke
            // nedvojbeno vidi: „ročište — objava presude" znači da je presuda donesena.
            // Rješenje o ovrsi i rješenje o platnom nalogu ostaju za ručno dodavanje,
            // jer prijedlog ne znači da je i usvojen.
            ["par_rociste_presuda"] = ["tbr2_st1"],

            // Tar. br. 3 — pravni lijekovi. Protiv PRESUDE dvostruko, protiv RJEŠENJA
            // jednostruko. Revizija u pravilu ide protiv presude; kad ide protiv
            // rješenja, redak se zamijeni ručno.
            ["par_zalba_presuda"] = ["tbr3_st1"],
            ["par_zalba_jednokratna"] = ["tbr3_st1"],
            ["par_zalba_mali_spor"] = ["tbr3_st1"],
            ["par_revizija"] = ["tbr3_st1"],
            ["par_revizija_jednokratna"] = ["tbr3_st1"],
            ["par_revizija_mali_spor"] = ["tbr3_st1"],
            ["par_zalba_rjesenje"] = ["tbr3_st2"],

            // ── Poglavlje I. — ovršni postupak ──
            ["ovr_prijedlog"] = ["tbr1_st2"],
            ["ovr_odgoda"] = ["tbr1_st3"],
            ["ovr_pravni_lijek"] = ["tbr3_st2"],

            // ── Poglavlje II. — izvanparnični postupak ──
            // `izv_dioba` u katalogu pokriva i diobu i ostavinu. Diobu vodi Tar. br. 5,
            // ostavinu Tar. br. 11 (drugo poglavlje, druga ljestvica) — automatski se
            // uzima dioba, a ostavinski se predmet prebaci ručno na `tbr11`.
            ["izv_dioba"] = ["tbr5"],
            ["izv_meda"] = ["tbr7_prijedlog"],
            ["izv_neprocjenjivi"] = ["tbr4_prijedlog"],
            ["izv_amortizacija"] = ["tbr4_prijedlog"],
            ["izv_pravni_lijek"] = ["tbr4_zalba"],

            // ── Poglavlje IV. — zemljišnoknjižni postupak ──
            ["zk_redoviti"] = ["tbr14_upis"],
            ["zk_redoviti_neproc"] = ["tbr14_upis"],
            ["zk_posebni"] = ["tbr14_ispravni"],
            ["zk_pravni_lijek"] = ["tbr16"],

            // ── Poglavlje VI. — stečaj i likvidacija ──
            // Katalog spaja stečaj i predstečaj u jednu stavku, a Uredba ih razdvaja
            // (30,00 prema 280,00). Automatski ide stečaj; predstečaj se dodaje ručno
            // kao `tbr22_prijedlog`.
            ["ste_prijedlog"] = ["tbr23_st1"],
            ["ste_prijava_potrazivanja"] = ["tbr23_st2"],
            ["ste_pravni_lijek"] = ["tbr23_zalba"],

            // ── Poglavlje VII. — sudski registar ──
            ["reg_upis"] = ["tbr25_prijava"],

            // Tužba na poništaj upisa u SUDSKI registar je parnica, pa ide po Poglavlju I.
            ["reg_ponistaj_tuzba_sudski"] = ["tbr1_st1"],
            ["reg_ponistaj_lijek_presuda_sudski"] = ["tbr3_st1"],
            ["reg_ponistaj_lijek_rjesenje_sudski"] = ["tbr3_st2"],

            // Poništaj upisa u DRUGI registar vodi se u upravnom sporu (Tbr. 29.1 Tarife
            // upućuje na Tbr. 27.1, a to je upravni spor), pa ide po Poglavlju VIII.
            ["reg_ponistaj_tuzba_drugi_proc"] = ["tbr29_tuzba"],
            ["reg_ponistaj_tuzba_drugi_neproc"] = ["tbr29_tuzba"],
            ["reg_ponistaj_lijek_presuda_drugi_proc"] = ["tbr29_zalba"],
            ["reg_ponistaj_lijek_presuda_drugi_neproc"] = ["tbr29_zalba"],
            ["reg_ponistaj_lijek_rjesenje_drugi_proc"] = ["tbr29_zalba"],
            ["reg_ponistaj_lijek_rjesenje_drugi_neproc"] = ["tbr29_zalba"],

            // ── Poglavlje VIII. — upravni spor ──
            // Fiksni iznosi. Zatečeni je kod ovdje primjenjivao Tar. br. 1 po VPS-u, pa
            // je tužba s VPS-om 50.000 € nosila 546,45 € umjesto 110,00 €.
            ["usp_tuzba_proc"] = ["tbr29_tuzba"],
            ["usp_tuzba_neproc"] = ["tbr29_tuzba"],
            ["usp_zalba"] = ["tbr29_zalba"],
            ["usp_zalba_neproc"] = ["tbr29_zalba"]

            // Bez pristojbe, namjerno:
            //  • kaz_*   — Poglavlje V. pokriva samo postupak po PRIVATNOJ tužbi, a
            //              obrana po javnoj optužbi u Uredbi nema pristojbu. Odredbe
            //              Tar. br. 19–21 postoje u tablici i dodaju se ručno.
            //  • upr_*   — upravni POSTUPAK teče pred tijelom, ne pred sudom.
            //  • ust_*   — Ustavni sud, ECHR i Sud EU nisu u ovoj Uredbi.
            //  • pos_*, ost_*, raz_*, zaj_* — savjeti, ugovori, stegovni postupci,
            //              čekanje i putovanje; nijedno nije sudska radnja iz Uredbe.
            //  • ročišta i ostali podnesci (par_rociste_*, par_ostali_podnesci,
            //              ovr_ostali_podnesci, zk_ostali, reg_ostali, izv_rociste …)
            //              — Tar. br. 1 napomena 5.
            //  • arbitraža (par_tuzba_arbitraza, par_tuzba_med_arbitraza) — nije sud.
        };

    /// <summary>
    /// Stavke koje znače „odgovor na tužbu" u smislu čl. 2 NN 50/2026.
    ///
    /// Prijelazna se odredba vezuje uz odgovor na TUŽBU, a ne uz svaki podnesak iz
    /// Tar. br. 1 st. 2 — odgovor na žalbu ondje nije spomenut, pa mu stara Uredba
    /// ne pripada ni kad je podnesen prije 1.6.2026.
    /// </summary>
    public static readonly IReadOnlySet<string> ItemiOdgovorNaTuzbu =
        new HashSet<string> { "par_odgovor_tuzba" };

    /// <summary>Odredbe koje odabrana stavka povlači; prazno kad je nema u mapi.</summary>
    public static IReadOnlyList<string> Za(string itemId) =>
        PoStavci.TryGetValue(itemId, out var odredbe) ? odredbe : [];
}
