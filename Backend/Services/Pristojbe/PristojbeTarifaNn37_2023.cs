using Backend.Models;

namespace Backend.Services.Pristojbe;

/// <summary>
/// Uredba o Tarifi sudskih pristojbi, NN 37/2023 — prestala važiti 1.6.2026.
///
/// Namjerno NIJE cijela stara Uredba. Čl. 2 NN 50/2026 zadržava staru Uredbu samo za
/// dvije situacije, pa se ovdje drži samo ono što te dvije situacije traže:
///
///   • pristojba za ODGOVOR NA TUŽBU podnesen prije 1.6.2026. → Tar. br. 1 st. 2,
///     a on upućuje na st. 1, pa je i st. 1 ovdje;
///   • pristojba u UPRAVNOM SPORU pokrenutom prije 1.6.2026. → cijelo Poglavlje VIII.
///
/// Ostatak stare Uredbe ne dodavati bez odredbe koja bi ga tražila — mrtav propis u
/// kodu izgleda kao živ.
///
/// Iznosi su prepisani iz <c>Uredba-sudske-pristojbe.pdf</c> (NN 37/2023 od 31.3.2023.),
/// Poglavlje I. i Poglavlje VIII.
/// </summary>
public static class PristojbeTarifaNn37_2023
{
    private static readonly PristojbenaOdredba[] Odredbe =
    [
        // ── Poglavlje I., Tar. br. 1 ──
        //
        //   iznad        do eura      eura
        //   0,00           398,17     13,27
        //   398,18         796,34     26,54
        //   796,35       1.194,51     39,82
        //   1.194,52     1.592,67     53,09
        //   1.592,68     1.990,84     66,36
        //
        // "Preko 1.990,84 eura plaća se pristojba u iznosu od 66,36 eura i još 1 % na
        //  razliku iznad 1.990,84 eura, ali ne više od 663,61 euro."
        new(PristojbeTarifaNn50_2026.Tbr1St1, PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 1 st. 1",
            "Tužba, protutužba, prigovor protiv platnog naloga i prigovor protiv rješenja o ovrsi",
            new LjestvicaPostotak(
                Razredi:
                [
                    new(  398.17m, 13.27m),
                    new(  796.34m, 26.54m),
                    new(1_194.51m, 39.82m),
                    new(1_592.67m, 53.09m),
                    new(1_990.84m, 66.36m)
                ],
                Prag: 1_990.84m, Baza: 66.36m, Stopa: 0.01m, Max: 663.61m),
            "vrijednost predmeta spora"),

        new(PristojbeTarifaNn50_2026.Tbr1St2, PristojbaPoglavlje.ParnicniOvrsni, "Tar. br. 1 st. 2",
            "Odgovor na tužbu i ostali podnesci iz st. 2 — polovica pristojbe iz st. 1",
            new UdioOd(PristojbeTarifaNn50_2026.Tbr1St1, 0.5m),
            "vrijednost predmeta spora"),

        // ── Poglavlje VIII. — postupak u upravnim sporovima ──
        // Ključevi su isti kao u NN 50/2026 da ista pristojbena radnja nosi isti
        // OdredbaId bez obzira na verziju; razlikuju se samo iznosi.
        new(PristojbeTarifaNn50_2026.Tbr29Tuzba, PristojbaPoglavlje.UpravniSpor, "Tar. br. 29 st. 1",
            "Tužba u upravnom sporu", new Fiksno(53.09m)),

        new("tbr29_obnova", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29 st. 2",
            "Prijedlog za obnovu upravnog spora", new Fiksno(66.36m)),

        new("tbr29_opci_akti", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29 st. 3",
            "Prijedlog o ocjeni zakonitosti općih akata", new Fiksno(265.45m)),

        new("tbr29_vus", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29 st. 4",
            "Podnesak kojim se pokreće postupak pred Visokim upravnim sudom", new Fiksno(66.36m)),

        new("tbr29_zalba", PristojbaPoglavlje.UpravniSpor, "Tar. br. 29 st. 5",
            "Žalba protiv prvostupanjske presude ili rješenja u upravnom sporu", new Fiksno(66.36m)),

        new("tbr30_odluka", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30 st. 1",
            "Presuda i rješenje doneseno u upravnom sporu", new Fiksno(66.36m)),

        new("tbr30_obnova", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30 st. 2",
            "Odluka o prijedlogu za obnovu upravnog spora", new Fiksno(66.36m)),

        new("tbr30_vus", PristojbaPoglavlje.UpravniSpor, "Tar. br. 30 st. 3",
            "Prvostupanjska odluka Visokog upravnog suda", new Fiksno(66.36m))
    ];

    /// <summary>
    /// Odredbe stare Uredbe po ključu.
    ///
    /// Razvrstavanje (podnesak / odluka) uzima se iz nove tablice: ključevi su isti jer
    /// je riječ o istoj pristojbenoj radnji, a čl. 7 Zakona o sudskim pristojbama ne
    /// ovisi o tome koja se Uredba na redak primjenjuje.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PristojbenaOdredba> PoId =
        Odredbe
            .Select(o => o with
            {
                Vrsta = PristojbeTarifaNn50_2026.VrstaZa(o.Id),
                VecImaElektronickoUmanjenje =
                    PristojbeTarifaNn50_2026.VecElektronickoUmanjenjeZa(o.Id)
            })
            .ToDictionary(o => o.Id);
}
