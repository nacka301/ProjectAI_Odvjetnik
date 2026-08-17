using Backend.Models;

namespace Backend.Services.Pristojbe;

/// <summary>
/// Uredba o Tarifi sudskih pristojbi, NN 50/2026 — svih 35 tarifnih brojeva,
/// složenih po devet poglavlja Priloga.
///
/// Na snazi od 1.6.2026.; tog je dana NN 37/2023 prestala važiti. Izvor iz kojeg je
/// tablica prepisana čuva se uz kod: <c>docs/uredba-pristojbe-NN-50-2026.md</c>
/// (https://narodne-novine.nn.hr/clanci/sluzbeni/2026_05_50_624.html).
///
/// Ovdje su samo brojke i upute; kako se koja odredba računa opisuje
/// <see cref="PristojbaIzracun"/>, a računa <see cref="SudskePristojbeCalculator"/>.
/// </summary>
public static class PristojbeTarifaNn50_2026
{
    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE I. — PARNIČNI I OVRŠNI POSTUPAK (Tar. br. 1–3)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Tar. br. 1 st. 1 — jedina ljestvica po VPS-u u cijeloj Uredbi.</summary>
    public const string Tbr1St1 = "tbr1_st1";

    /// <summary>Tar. br. 1 st. 2 — polovica st. 1.</summary>
    public const string Tbr1St2 = "tbr1_st2";

    private const string VpsOsnovica = "vrijednost predmeta spora";

    private static readonly PristojbenaOdredba[] PoglavljeI =
    [
        // ── Tar. br. 1 — podnesci ──
        //
        //   iznad        do eura      pristojba
        //   0,00           400,00        30,00
        //   400,01         800,00        60,00
        //   800,01       1.200,00        80,00
        //   1.200,01     1.600,00       110,00
        //   1.600,01     2.000,00       140,00
        //
        // "Preko 2.000,00 plaća se 140,00 i još 1 % na razliku iznad 2.000,00,
        //  ali ne više od 1.500,00."
        new(Tbr1St1, PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 1 st. 1",
            "Tužba, protutužba, prigovor protiv platnog naloga i prigovor protiv rješenja o ovrsi",
            new LjestvicaPostotak(
                Razredi:
                [
                    new(  400.00m,  30.00m),
                    new(  800.00m,  60.00m),
                    new(1_200.00m,  80.00m),
                    new(1_600.00m, 110.00m),
                    new(2_000.00m, 140.00m)
                ],
                Prag: 2_000.00m, Baza: 140.00m, Stopa: 0.01m, Max: 1_500.00m),
            VpsOsnovica),

        // Nabrajanje u st. 2 završava popisom ODGOVORA: "odgovor na prijedlog za
        // ponavljanje postupka, tužbu, žalbu, prijedlog za dopuštenje revizije i
        // reviziju". Riječ je o odgovoru na žalbu, a ne o samoj žalbi — sama žalba
        // ide po Tar. br. 3 i ondje je skuplja, ne jeftinija.
        new(Tbr1St2, PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 1 st. 2",
            "Tužba s prijedlogom za platni nalog, prijedlog za ovrhu/protuovrhu/osiguranje, "
            + "prijedlog za povrat u prijašnje stanje, prijedlog za osiguranje dokaza, "
            + "odgovor na tužbu, žalbu, prijedlog za dopuštenje revizije i reviziju",
            new UdioOd(Tbr1St1, 0.5m),
            VpsOsnovica),

        new("tbr1_st3", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 1 st. 3",
            "Prijedlog za priznanje odluke stranog suda; ovršenikov prijedlog za odgodu ovrhe; "
            + "prijedlog za odgodu prijenosa zaplijenjenih sredstava (ako je odbijen)",
            new Fiksno(40.00m)),

        // ── Tar. br. 2 — odluke ──
        new("tbr2_st1", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 1",
            "Prvostupanjska presuda; rješenje u sporu zbog smetanja posjeda",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr2_st2", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 2",
            "Presuda zbog ogluhe, bez rasprave, zbog izostanka, na temelju odricanja ili priznanja",
            new UdioOd(Tbr1St2, 1.0m), VpsOsnovica),

        new("tbr2_st3", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 3",
            "Rješenje o odbačaju nedopuštenog prijedloga za reviziju i o odbačaju revizije",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr2_st4", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 4",
            "Rješenje o izdavanju platnog naloga",
            new UdioOd(Tbr1St2, 1.0m), VpsOsnovica),

        new("tbr2_st5", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 5",
            "Rješenje o ovrsi, protuovrsi, osiguranju dokaza i osiguranju",
            new UdioOd(Tbr1St2, 1.0m), VpsOsnovica),

        new("tbr2_st5_strane", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 5",
            "Rješenje o ovrsi na temelju stranih ovršnih isprava",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr2_st6", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 6",
            "Rješenje o povratu u prijašnje stanje",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr2_st7", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 7",
            "Rješenje o priznanju strane odluke", new Fiksno(30.00m)),

        new("tbr2_st8", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 8",
            "Rješenje o odgodi ili obustavi ovrhe", new Fiksno(30.00m)),

        new("tbr2_st9", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 2 st. 9",
            "Sudske nagodbe — pristojba se ne plaća", new NePlacaSe()),

        // ── Tar. br. 3 — pravni lijekovi ──
        //
        // Ovo je odredba koje u zatečenom kodu uopće nije bilo: pravni su se lijekovi
        // vodili kroz Tar. br. 1 st. 2 (polovica), a Uredba za lijek protiv PRESUDE
        // traži dvostruko. Razlika je četverostruka.
        new("tbr3_st1", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 3 st. 1",
            "Žalba, prijedlog za dopuštenje revizije i revizija protiv presude; "
            + "žalba protiv rješenja u sporu zbog smetanja posjeda — uvećano za 100 %",
            new UdioOd(Tbr1St1, 2.0m), VpsOsnovica),

        new("tbr3_st2", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 3 st. 2",
            "Žalba, prijedlog za dopuštenje revizije i revizija protiv rješenja; "
            + "prijedlog za ponavljanje postupka",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr3_st3", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 3 st. 3",
            "Prijedlog za proglašenje pljenidbe i prijenosa nedopuštenim",
            new UdioOd(Tbr1St1, 1.0m), VpsOsnovica),

        new("tbr3_st5", PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 3 st. 5",
            "Žalba protiv rješenja o povratu u prijašnje stanje ili o priznanju strane odluke "
            + "— najviše 50,00",
            new UdioOd(Tbr1St2, 1.0m, Max: 50.00m), VpsOsnovica)
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE II. — IZVANPARNIČNI POSTUPAK (Tar. br. 4–9)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeII =
    [
        new("tbr4_prijedlog", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 4",
            "Prijedlog za pokretanje izvanparničnog postupka ili za povrat u prijašnje stanje",
            new Fiksno(30.00m)),

        new("tbr4_odluka", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 4",
            "Odluka prvostupanjskog suda o osnovanosti prijedloga", new Fiksno(40.00m)),

        new("tbr4_zalba", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 4",
            "Žalba protiv odluke o osnovanosti prijedloga", new Fiksno(60.00m)),

        // Tar. br. 5 — dioba suvlasništva i zajedničke imovine.
        // "Preko 6.600,00: 60,00 i još 30,00 na svakih započetih 1.300,00, najviše 840,00."
        new("tbr5", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 5",
            "Prijedlog za diobu suvlasništva i zajedničke imovine",
            new LjestvicaKorak(
                Razredi:
                [
                    new(2_700.00m, 30.00m),
                    new(4_000.00m, 40.00m),
                    new(6_600.00m, 60.00m)
                ],
                Prag: 6_600.00m, Baza: 60.00m, Korak: 1_300.00m, PoKoraku: 30.00m, Max: 840.00m),
            "vrijednost imovine koja se dijeli"),

        new("tbr5_odluka", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 5",
            "Odluka o diobi suvlasništva i zajedničke imovine",
            new UdioOd("tbr5", 1.0m), "vrijednost imovine koja se dijeli"),

        new("tbr5_zalba", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 5",
            "Žalba u postupku diobe suvlasništva i zajedničke imovine",
            new UdioOd("tbr5", 1.0m), "vrijednost imovine koja se dijeli"),

        new("tbr6_prijedlog", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 6",
            "Prijedlog za uređenje odnosa između suvlasnika", new Fiksno(30.00m)),

        new("tbr6_odluka", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 6",
            "Odluka o uređenju odnosa između suvlasnika", new Fiksno(80.00m)),

        new("tbr6_zalba", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 6",
            "Žalba u postupku uređenja odnosa između suvlasnika", new Fiksno(80.00m)),

        new("tbr7_prijedlog", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 7",
            "Prijedlog u postupku uređenja međe, nužnog prolaza i služnosti", new Fiksno(80.00m)),

        new("tbr7_odluka", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 7",
            "Odluka u postupku uređenja međe, nužnog prolaza i služnosti", new Fiksno(170.00m)),

        new("tbr7_zalba", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 7",
            "Žalba u postupku uređenja međe, nužnog prolaza i služnosti", new Fiksno(170.00m)),

        new("tbr8_tijelo", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 8",
            "Ovjera isprave za inozemstvo — potpis i pečat tijela ili pravne osobe",
            new Fiksno(15.00m)),

        new("tbr8_tumac", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 8",
            "Ovjera isprave za inozemstvo — potpis i pečat sudskog tumača ili vještaka",
            new Fiksno(20.00m)),

        new("tbr9_prvi_upis", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 9 st. 1",
            "Prvi upis broda", new Fiksno(840.00m)),

        new("tbr9_rjesenje", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 9",
            "Rješenje o upisu broda — polovica", new UdioOd("tbr9_prvi_upis", 0.5m)),

        new("tbr9_daljnji_upis", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 9",
            "Daljnji upisi broda — polovica", new UdioOd("tbr9_prvi_upis", 0.5m)),

        new("tbr9_zalba", PristojbaPoglavlje.Izvanparnicni, "Tar. br. 9",
            "Žalba u postupku upisa broda — dvostruko od st. 1", new UdioOd("tbr9_prvi_upis", 2.0m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE III. — OSTAVINSKI POSTUPAK (Tar. br. 11–13)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeIII =
    [
        // "Preko 8.000,00: 60,00 i još 30,00 na svakih započetih 1.300,00, najviše 700,00."
        new("tbr11", PristojbaPoglavlje.Ostavinski, "Tar. br. 11",
            "Rješenje o nasljeđivanju",
            new LjestvicaKorak(
                Razredi:
                [
                    new(3_300.00m, 30.00m),
                    new(4_600.00m, 40.00m),
                    new(8_000.00m, 60.00m)
                ],
                Prag: 8_000.00m, Baza: 60.00m, Korak: 1_300.00m, PoKoraku: 30.00m, Max: 700.00m),
            "čista vrijednost ostavine"),

        new("tbr12", PristojbaPoglavlje.Ostavinski, "Tar. br. 12",
            "Žalba u ostavinskom postupku", new Fiksno(140.00m)),

        new("tbr13_sastavljanje", PristojbaPoglavlje.Ostavinski, "Tar. br. 13",
            "Sastavljanje sudske oporuke ili opoziv oporuke pred sudom", new Fiksno(140.00m)),

        new("tbr13_cuvanje", PristojbaPoglavlje.Ostavinski, "Tar. br. 13",
            "Čuvanje i vraćanje oporuke", new Fiksno(30.00m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE IV. — ZEMLJIŠNOKNJIŽNI POSTUPAK (Tar. br. 14–18)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeIV =
    [
        new("tbr14_upis", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 14",
            "Prijedlog za upis ili brisanje u zemljišnoj knjizi", new Fiksno(10.00m)),

        new("tbr14_ispravni", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 14",
            "Prijedlog u pojedinačnom ispravnom postupku", new Fiksno(70.00m)),

        new("tbr14_zakasnjeli", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 14",
            "Prijedlog podnesen nakon 60 dana od stjecanja uvjeta — peterostruko",
            new UdioOd("tbr14_upis", 5.0m)),

        new("tbr15_uknjizba", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 15",
            "Rješenje o uknjižbi ili predbilježbi", new Fiksno(30.00m)),

        new("tbr15_zabiljezba", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 15",
            "Rješenje o zabilježbi", new Fiksno(10.00m)),

        new("tbr15_poljoprivredni", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 15",
            "Upis za poljoprivredne kredite — 10 % propisane pristojbe",
            new UdioOd("tbr15_uknjizba", 0.10m)),

        // "Etažiranje: do 10 dijelova 30,00 · 11–100 → 80,00 · 101–500 → 140,00 · >500 → 280,00"
        new("tbr15_etaziranje", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 15",
            "Upis etažnog vlasništva",
            new Stepenasto(
            [
                new( 10m,  30.00m),
                new(100m,  80.00m),
                new(500m, 140.00m),
                new(decimal.MaxValue, 280.00m)
            ]),
            "broj etažnih dijelova"),

        new("tbr15_zakasnjelo", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 15",
            "Odluka po prijedlogu podnesenom nakon 60 dana — peterostruko",
            new UdioOd("tbr15_uknjizba", 5.0m)),

        new("tbr16", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 16",
            "Prigovor i žalba u zemljišnoknjižnom postupku", new Fiksno(70.00m)),

        new("tbr17_18", PristojbaPoglavlje.Zemljisnoknjizni, "Tar. br. 17 i 18",
            "Ovjera preslike", new Fiksno(5.00m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE V. — KAZNENI POSTUPAK PO PRIVATNOJ TUŽBI (Tar. br. 19–21)
    // ═══════════════════════════════════════════════════════════════
    //
    // Poglavlje pokriva SAMO postupak po privatnoj tužbi. Obrana po javnoj optužbi
    // u Uredbi nema svoju pristojbu, pa se za nju i dalje ne obračunava ništa —
    // vidi PristojbeneRadnjeMapa.
    private static readonly PristojbenaOdredba[] PoglavljeV =
    [
        new("tbr19_tuzba", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 19",
            "Privatna tužba i protutužba", new Fiksno(70.00m)),

        new("tbr19_povrat", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 19",
            "Prijedlog za povrat u prijašnje stanje", new Fiksno(40.00m)),

        new("tbr20", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 20",
            "Prvostupanjska presuda ili sudska opomena", new Fiksno(140.00m)),

        new("tbr21_zalba", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 21",
            "Žalba", new Fiksno(170.00m)),

        new("tbr21_odgovor", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 21",
            "Odgovor na žalbu — polovica od Tar. br. 20", new UdioOd("tbr20", 0.5m)),

        new("tbr21_izvanredni", PristojbaPoglavlje.KazneniPrivatnaTuzba, "Tar. br. 21",
            "Zahtjev za izvanredno preispitivanje i prijedlog za obnovu — kao žalba",
            new UdioOd("tbr21_zalba", 1.0m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE VI. — STEČAJ I LIKVIDACIJA (Tar. br. 22–24)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeVI =
    [
        new("tbr22_prijedlog", PristojbaPoglavlje.Stecaj, "Tar. br. 22",
            "Prijedlog za otvaranje predstečajnog postupka", new Fiksno(280.00m)),

        new("tbr22_zalba", PristojbaPoglavlje.Stecaj, "Tar. br. 22",
            "Žalba u predstečajnom postupku", new Fiksno(560.00m)),

        new("tbr23_st1", PristojbaPoglavlje.Stecaj, "Tar. br. 23 st. 1",
            "Prijedlog za otvaranje stečajnog ili likvidacijskog postupka", new Fiksno(30.00m)),

        new("tbr23_st2", PristojbaPoglavlje.Stecaj, "Tar. br. 23 st. 2",
            "Prijava potraživanja — 2 % vrijednosti tražbine, najviše 140,00",
            new PostotakVrijednosti(0.02m, 140.00m), "vrijednost tražbine"),

        new("tbr23_zalba", PristojbaPoglavlje.Stecaj, "Tar. br. 23",
            "Žalba — dvostruko od st. 2", new UdioOd("tbr23_st2", 2.0m), "vrijednost tražbine"),

        new("tbr24", PristojbaPoglavlje.Stecaj, "Tar. br. 24",
            "Paušalna pristojba — 2 % diobne mase, najviše 560,00",
            new PostotakVrijednosti(0.02m, 560.00m), "diobna masa")
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE VII. — SUDSKI REGISTAR (Tar. br. 25–28)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeVII =
    [
        new("tbr25_prijava", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 25",
            "Prijava za upis u sudski registar", new Fiksno(30.00m)),

        new("tbr25_jdoo", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 25",
            "Prijava za upis — j.d.o.o.", new Fiksno(10.00m)),

        new("tbr25_enotar", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 25",
            "Prijava putem sustava e-Notar ili START — polovica",
            new UdioOd("tbr25_prijava", 0.5m)),

        new("tbr26_osnivanje", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Upis osnivanja", new Fiksno(80.00m)),

        new("tbr26_osnivanje_jdoo", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Upis osnivanja — j.d.o.o.", new Fiksno(10.00m)),

        new("tbr26_promjena", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Upis promjene podataka", new Fiksno(40.00m)),

        new("tbr26_promjena_jdoo", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Upis promjene podataka — j.d.o.o.", new Fiksno(10.00m)),

        new("tbr26_fizicki", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Podnesak u fizičkom obliku", new Fiksno(30.00m)),

        new("tbr26_enotar", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 26",
            "Upis putem sustava e-Notar ili START — polovica",
            new UdioOd("tbr26_osnivanje", 0.5m)),

        new("tbr27_prijava", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 27",
            "Prijava za upis podružnice", new Fiksno(30.00m)),

        new("tbr27_upis", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 27",
            "Upis podružnice", new Fiksno(70.00m)),

        new("tbr27_promjena", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 27",
            "Upis promjene podataka o podružnici", new Fiksno(30.00m)),

        new("tbr28_preslika", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 28",
            "Preslika iz registra — po stranici", new Fiksno(1.00m)),

        new("tbr28_potvrda", PristojbaPoglavlje.SudskiRegistar, "Tar. br. 28",
            "Potvrda iz registra", new Fiksno(3.00m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE VIII. — UPRAVNI SPOR (Tar. br. 29–30)
    // ═══════════════════════════════════════════════════════════════
    //
    // Fiksni iznosi. Tar. br. 1 se ovdje NE primjenjuje — upravo je to zatečeni kod
    // radio, pa je tužba s VPS-om 50.000 € nosila 546,45 € umjesto 110,00 €.

    /// <summary>Tar. br. 29 — tužba u upravnom sporu; polazište prijelazne odredbe iz čl. 2.</summary>
    public const string Tbr29Tuzba = "tbr29_tuzba";

    private static readonly PristojbenaOdredba[] PoglavljeVIII =
    [
        new(Tbr29Tuzba, PristojbaPoglavlje.UpravniSpor, "Tar. br. 29",
            "Tužba u upravnom sporu", new Fiksno(110.00m)),

        new("tbr29_obnova", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29",
            "Prijedlog za obnovu upravnog spora", new Fiksno(140.00m)),

        new("tbr29_opci_akti", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29",
            "Prijedlog za ocjenu zakonitosti općih akata", new Fiksno(560.00m)),

        new("tbr29_vus", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29",
            "Podnesak pred Visokim upravnim sudom", new Fiksno(140.00m)),

        new("tbr29_zalba", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29",
            "Žalba protiv prvostupanjske presude ili rješenja", new Fiksno(140.00m)),

        new("tbr30_odluka", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30",
            "Presuda i rješenje u upravnom sporu", new Fiksno(140.00m)),

        new("tbr30_obnova", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30",
            "Odluka o obnovi upravnog spora", new Fiksno(140.00m)),

        new("tbr30_vus", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30",
            "Prvostupanjska odluka Visokog upravnog suda", new Fiksno(140.00m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // POGLAVLJE IX. — OSTALI SLUČAJEVI (Tar. br. 31–35)
    // ═══════════════════════════════════════════════════════════════

    private static readonly PristojbenaOdredba[] PoglavljeIX =
    [
        new("tbr31_podnesak", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 31",
            "Podnesak kojim se traži uvjerenje", new Fiksno(3.00m)),

        new("tbr31_uvjerenje", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 31",
            "Uvjerenje", new Fiksno(6.00m)),

        // St. 3 daje polovicu za OBOJE — i za podnesak i za uvjerenje — pa su i ovdje
        // dva retka, kao što su dva i u punom iznosu.
        new("tbr31_podnesak_egradani", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 31 st. 3",
            "Podnesak kojim se traži uvjerenje, kroz sustav e-Građani i e-Ovlaštenja — polovica",
            new UdioOd("tbr31_podnesak", 0.5m)),

        new("tbr31_egradani", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 31 st. 3",
            "Uvjerenje zatraženo kroz sustav e-Građani i e-Ovlaštenja — polovica",
            new UdioOd("tbr31_uvjerenje", 0.5m)),

        new("tbr32", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 32",
            "Ovjera ugovora o doživotnom ili dosmrtnom uzdržavanju", new Fiksno(80.00m)),

        new("tbr33", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 33",
            "Razmatranje sudskog spisa", new Fiksno(30.00m)),

        // "Prijedlog o pologu 30,00 i još 6,00 na svakih započetih 130,00 vrijednosti
        //  pologa, najviše 560,00." Kapa se čita na ukupan iznos, kao kod Tar. br. 5 i 11.
        new("tbr34", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 34",
            "Prijedlog o pologu",
            new LjestvicaKorak(
                Razredi: [],
                Prag: 0m, Baza: 30.00m, Korak: 130.00m, PoKoraku: 6.00m, Max: 560.00m),
            "vrijednost pologa"),

        new("tbr35_pohrana", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 35",
            "Pohrana opće punomoći", new Fiksno(30.00m)),

        new("tbr35_odluka", PristojbaPoglavlje.OstaliSlucajevi, "Tar. br. 35",
            "Odluka o pohrani opće punomoći", new Fiksno(60.00m))
    ];

    // ═══════════════════════════════════════════════════════════════
    // RAZVRSTAVANJE — podnosi li stranka, ili sud donosi
    // ═══════════════════════════════════════════════════════════════
    //
    // Stoji u jednom bloku, odvojeno od iznosa, da se može pročitati uz Prilog Uredbe
    // odjednom. Od toga ovisi koji stavak čl. 7 Zakona umanjuje redak, pa razlika od
    // jedne riječi mijenja iznos: „Prijedlog za ovrhu" je podnesak, „Rješenje o ovrsi"
    // nije.
    //
    // Odredba koje ovdje nema ostaje `Ostalo` i NIKAD se ne umanjuje. Da nijedna ne
    // ispadne slučajno, brine test SvakaOdredbaJeRazvrstana.

    private static readonly IReadOnlyDictionary<string, VrstaRadnje> Vrste =
        new Dictionary<string, VrstaRadnje>
        {
            // ── I. Parnični i ovršni ──
            [Tbr1St1] = VrstaRadnje.Podnesak,
            [Tbr1St2] = VrstaRadnje.Podnesak,
            ["tbr1_st3"] = VrstaRadnje.Podnesak,
            ["tbr2_st1"] = VrstaRadnje.Odluka,
            ["tbr2_st2"] = VrstaRadnje.Odluka,
            ["tbr2_st3"] = VrstaRadnje.Odluka,
            ["tbr2_st4"] = VrstaRadnje.Odluka,
            ["tbr2_st5"] = VrstaRadnje.Odluka,
            ["tbr2_st5_strane"] = VrstaRadnje.Odluka,
            ["tbr2_st6"] = VrstaRadnje.Odluka,
            ["tbr2_st7"] = VrstaRadnje.Odluka,
            ["tbr2_st8"] = VrstaRadnje.Odluka,
            ["tbr2_st9"] = VrstaRadnje.Ostalo,        // nagodba — pristojba se ne plaća
            ["tbr3_st1"] = VrstaRadnje.Podnesak,
            ["tbr3_st2"] = VrstaRadnje.Podnesak,
            ["tbr3_st3"] = VrstaRadnje.Podnesak,
            ["tbr3_st5"] = VrstaRadnje.Podnesak,

            // ── II. Izvanparnični ──
            ["tbr4_prijedlog"] = VrstaRadnje.Podnesak,
            ["tbr4_odluka"] = VrstaRadnje.Odluka,
            ["tbr4_zalba"] = VrstaRadnje.Podnesak,
            ["tbr5"] = VrstaRadnje.Podnesak,
            ["tbr5_odluka"] = VrstaRadnje.Odluka,
            ["tbr5_zalba"] = VrstaRadnje.Podnesak,
            ["tbr6_prijedlog"] = VrstaRadnje.Podnesak,
            ["tbr6_odluka"] = VrstaRadnje.Odluka,
            ["tbr6_zalba"] = VrstaRadnje.Podnesak,
            ["tbr7_prijedlog"] = VrstaRadnje.Podnesak,
            ["tbr7_odluka"] = VrstaRadnje.Odluka,
            ["tbr7_zalba"] = VrstaRadnje.Podnesak,
            ["tbr8_tijelo"] = VrstaRadnje.Ostalo,     // ovjera isprave za inozemstvo
            ["tbr8_tumac"] = VrstaRadnje.Ostalo,
            ["tbr9_prvi_upis"] = VrstaRadnje.Odluka,
            ["tbr9_rjesenje"] = VrstaRadnje.Odluka,
            ["tbr9_daljnji_upis"] = VrstaRadnje.Odluka,
            ["tbr9_zalba"] = VrstaRadnje.Podnesak,

            // ── III. Ostavinski ──
            ["tbr11"] = VrstaRadnje.Odluka,           // rješenje o nasljeđivanju
            ["tbr12"] = VrstaRadnje.Podnesak,
            ["tbr13_sastavljanje"] = VrstaRadnje.Ostalo,
            ["tbr13_cuvanje"] = VrstaRadnje.Ostalo,

            // ── IV. Zemljišnoknjižni — čl. 7 st. 3 ──
            ["tbr14_upis"] = VrstaRadnje.Podnesak,    // PRIJEDLOG za upis, ne sam upis
            ["tbr14_ispravni"] = VrstaRadnje.Podnesak,
            ["tbr14_zakasnjeli"] = VrstaRadnje.Podnesak,
            ["tbr15_uknjizba"] = VrstaRadnje.Odluka,
            ["tbr15_zabiljezba"] = VrstaRadnje.Odluka,
            ["tbr15_poljoprivredni"] = VrstaRadnje.Odluka,
            ["tbr15_etaziranje"] = VrstaRadnje.Odluka,
            ["tbr15_zakasnjelo"] = VrstaRadnje.Odluka,
            ["tbr16"] = VrstaRadnje.Podnesak,
            ["tbr17_18"] = VrstaRadnje.Ostalo,        // ovjera preslike — ni podnesak ni odluka

            // ── V. Kazneni po privatnoj tužbi ──
            ["tbr19_tuzba"] = VrstaRadnje.Podnesak,
            ["tbr19_povrat"] = VrstaRadnje.Podnesak,
            ["tbr20"] = VrstaRadnje.Odluka,
            ["tbr21_zalba"] = VrstaRadnje.Podnesak,
            ["tbr21_odgovor"] = VrstaRadnje.Podnesak,
            ["tbr21_izvanredni"] = VrstaRadnje.Podnesak,

            // ── VI. Stečaj i likvidacija ──
            ["tbr22_prijedlog"] = VrstaRadnje.Podnesak,
            ["tbr22_zalba"] = VrstaRadnje.Podnesak,
            ["tbr23_st1"] = VrstaRadnje.Podnesak,
            ["tbr23_st2"] = VrstaRadnje.Podnesak,      // prijava potraživanja
            ["tbr23_zalba"] = VrstaRadnje.Podnesak,
            ["tbr24"] = VrstaRadnje.Ostalo,            // paušal na diobnu masu

            // ── VII. Sudski registar ──
            ["tbr25_prijava"] = VrstaRadnje.Podnesak,
            ["tbr25_jdoo"] = VrstaRadnje.Podnesak,
            ["tbr25_enotar"] = VrstaRadnje.Podnesak,
            ["tbr26_osnivanje"] = VrstaRadnje.Odluka,
            ["tbr26_osnivanje_jdoo"] = VrstaRadnje.Odluka,
            ["tbr26_promjena"] = VrstaRadnje.Odluka,
            ["tbr26_promjena_jdoo"] = VrstaRadnje.Odluka,
            ["tbr26_fizicki"] = VrstaRadnje.Ostalo,    // doplata upravo ZA fizički oblik
            ["tbr26_enotar"] = VrstaRadnje.Odluka,
            ["tbr27_prijava"] = VrstaRadnje.Podnesak,
            ["tbr27_upis"] = VrstaRadnje.Odluka,
            ["tbr27_promjena"] = VrstaRadnje.Odluka,
            ["tbr28_preslika"] = VrstaRadnje.Ostalo,
            ["tbr28_potvrda"] = VrstaRadnje.Ostalo,

            // ── VIII. Upravni spor ──
            [Tbr29Tuzba] = VrstaRadnje.Podnesak,
            ["tbr29_obnova"] = VrstaRadnje.Podnesak,
            ["tbr29_opci_akti"] = VrstaRadnje.Podnesak,
            ["tbr29_vus"] = VrstaRadnje.Podnesak,
            ["tbr29_zalba"] = VrstaRadnje.Podnesak,
            ["tbr30_odluka"] = VrstaRadnje.Odluka,
            ["tbr30_obnova"] = VrstaRadnje.Odluka,
            ["tbr30_vus"] = VrstaRadnje.Odluka,

            // ── IX. Ostali slučajevi ──
            ["tbr31_podnesak"] = VrstaRadnje.Podnesak,
            ["tbr31_podnesak_egradani"] = VrstaRadnje.Podnesak,
            ["tbr31_uvjerenje"] = VrstaRadnje.Ostalo,
            ["tbr31_egradani"] = VrstaRadnje.Ostalo,
            ["tbr32"] = VrstaRadnje.Ostalo,
            ["tbr33"] = VrstaRadnje.Ostalo,
            ["tbr34"] = VrstaRadnje.Podnesak,          // prijedlog o pologu
            ["tbr35_pohrana"] = VrstaRadnje.Ostalo,
            ["tbr35_odluka"] = VrstaRadnje.Odluka
        };

    /// <summary>
    /// Odredbe čiji je iznos VEĆ polovica, i to dana zbog elektroničkog oblika — isti
    /// razlog zbog kojeg umanjuje i čl. 7 Zakona, pa se ne umanjuju drugi put.
    ///
    /// <list type="bullet">
    /// <item>Tar. br. 25 st. 3 — prijava za upis preko sustava e-Notar i START</item>
    /// <item>Tar. br. 26 st. 6 — odluke koje sud dostavlja preko e-Notara i START-a</item>
    /// <item>Tar. br. 31 st. 3 — podnesak i uvjerenje kroz e-Građane i e-Ovlaštenja</item>
    /// </list>
    ///
    /// Kriterij je RAZLOG, ne riječ „polovica" u tekstu: polovica iz Tar. br. 1 st. 2
    /// dana je zbog vrste podneska, ne zbog oblika, pa se s čl. 7 legitimno slaže.
    /// </summary>
    private static readonly IReadOnlySet<string> VecElektronicke =
        new HashSet<string>
        {
            "tbr25_enotar", "tbr26_enotar", "tbr31_podnesak_egradani", "tbr31_egradani"
        };

    /// <summary>Poglavlja spojena redoslijedom iz Priloga, prije razvrstavanja.</summary>
    private static readonly PristojbenaOdredba[] PoRedu =
    [
        .. PoglavljeI, .. PoglavljeII, .. PoglavljeIII, .. PoglavljeIV, .. PoglavljeV,
        .. PoglavljeVI, .. PoglavljeVII, .. PoglavljeVIII, .. PoglavljeIX
    ];

    /// <summary>
    /// Sve odredbe NN 50/2026, redoslijedom poglavlja iz Priloga, s razvrstavanjem po
    /// vrsti radnje (za čl. 7 Zakona).
    /// </summary>
    public static readonly IReadOnlyList<PristojbenaOdredba> Sve =
    [
        .. PoRedu.Select(o => o with
        {
            Vrsta = VrstaZa(o.Id),
            VecImaElektronickoUmanjenje = VecElektronickoUmanjenjeZa(o.Id)
        })
    ];

    /// <summary>Odredbe po ključu, za razrješavanje uputa iz <see cref="UdioOd"/>.</summary>
    public static readonly IReadOnlyDictionary<string, PristojbenaOdredba> PoId =
        Sve.ToDictionary(o => o.Id);

    /// <summary>
    /// Razvrstavanje po ključu odredbe. Stara Uredba dijeli ključeve s novom (ista
    /// pristojbena radnja, drugi iznos), pa dijeli i razvrstavanje — čl. 7 Zakona ne
    /// ovisi o tome koja se Uredba na redak primjenjuje.
    /// </summary>
    public static VrstaRadnje VrstaZa(string odredbaId)
        => Vrste.GetValueOrDefault(odredbaId, VrstaRadnje.Ostalo);

    /// <summary>Nosi li odredba već elektroničko umanjenje — vidi <see cref="VecElektronicke"/>.</summary>
    public static bool VecElektronickoUmanjenjeZa(string odredbaId)
        => VecElektronicke.Contains(odredbaId);
}
