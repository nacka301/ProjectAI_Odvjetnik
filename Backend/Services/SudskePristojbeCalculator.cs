using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Kalkulator sudskih pristojbi prema Uredbi o tarifi sudskih pristojbi
/// (NN 37/2023, objavljena 31. ožujka 2023.).
/// Tar. br. 1, stavak 1 — pristojba na tužbu / prijedlog.
///
/// FIXED: Prethodni kod sadržavao je 26 razreda konvertiranih iz HRK tarife
/// (NN 95/2015, NN 156/2022) što je bilo potpuno pogrešno.
/// Ispravna tarifa (NN 37/2023) ima samo 5 fiksnih razreda + progresivnu formulu.
/// Maksimalna pristojba: 663,61 EUR (ne 3.981,68 EUR).
/// </summary>
public static class SudskePristojbeCalculator
{
    // ── Tar. br. 1, stavak 1 — fiksni razredi (EUR → EUR) ──────
    // Izvor: Uredba o tarifi sudskih pristojbi, NN 37/2023, str. 1
    //
    //   iznad (EUR)    do (EUR)      pristojba (EUR)
    //   0,00           398,17        13,27
    //   398,18         796,34        26,54
    //   796,35         1.194,51      39,82
    //   1.194,52       1.592,67      53,09
    //   1.592,68       1.990,84      66,36
    //
    // Preko 1.990,84 EUR: 66,36 + 1% × (VPS − 1.990,84), ali ne više od 663,61 EUR.

    private static readonly (decimal MaxVps, decimal Fee)[] Brackets =
    [
        (   398.17m,  13.27m),  // FIXED: razred 1 — do 398,17 EUR
        (   796.34m,  26.54m),  // FIXED: razred 2 — do 796,34 EUR
        ( 1_194.51m,  39.82m),  // FIXED: razred 3 — do 1.194,51 EUR (bilo 1.327,23 — HALLUCINATION)
        ( 1_592.67m,  53.09m),  // FIXED: razred 4 — do 1.592,67 EUR (bilo 1.990,84 — HALLUCINATION)
        ( 1_990.84m,  66.36m),  // FIXED: razred 5 — do 1.990,84 EUR (bilo 3.318,07 — HALLUCINATION)
        // FIXED: razredi 6-26 iz starog koda su POTPUNO IZBRISANI — ne postoje u NN 37/2023.
    ];

    /// <summary>
    /// Maksimalna sudska pristojba: 663,61 EUR.
    /// FIXED: prethodno bilo 3.981,68 EUR (= 30.000 HRK) — pogrešna tarifa.
    /// Izvor: NN 37/2023, Tar. br. 1, stavak 1, zadnji odlomak.
    /// </summary>
    public const decimal MaxFee = 663.61m;

    // Konstante za progresivnu formulu iznad 1.990,84 EUR
    private const decimal ProgressiveThreshold = 1_990.84m;
    private const decimal ProgressiveBase = 66.36m;
    private const decimal ProgressiveRate = 0.01m; // 1%

    /// <summary>
    /// Izračunava sudsku pristojbu (Tar. br. 1) prema VPS-u i osnovi obračuna.
    ///
    /// <para><see cref="PristojbaOsnova.Nema"/> znači da Uredba za tu vrstu postupka
    /// uopće ne propisuje pristojbu — POGLAVLJE I. Priloga nosi naslov
    /// "PARNIČNI I OVRŠNI POSTUPAK", pa kazneni postupak u njega ne ulazi.</para>
    ///
    /// Ako je VPS ≤ 0, vraća 0.
    /// </summary>
    public static decimal Izracunaj(decimal vrijednostSpora, PristojbaOsnova osnova = PristojbaOsnova.Puna)
    {
        if (osnova == PristojbaOsnova.Nema)
            return 0m;

        if (vrijednostSpora <= 0)
            return 0m;

        var iznos = IzracunajNezaokruzeno(vrijednostSpora);

        // Tar. br. 1 st. 2 — "(...) plaća se polovica pristojbe iz stavka 1. ovoga Tar. br."
        if (osnova == PristojbaOsnova.Polovica)
            iznos *= 0.5m;

        // Jedno zaokruživanje, na samom kraju. AwayFromZero, ne bankarsko:
        // polovica od 76,4516 je 38,2258 → 38,23; ToEven bi na 38,225 dao 38,22.
        return Math.Round(iznos, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Tar. br. 1 st. 1 — nezaokruženi iznos, da se zaokruživanje dogodi samo jednom,
    /// nakon eventualnog polovljenja po st. 2.
    /// </summary>
    private static decimal IzracunajNezaokruzeno(decimal vrijednostSpora)
    {
        // Fiksni razredi (5 razreda prema NN 37/2023)
        foreach (var (maxVps, fee) in Brackets)
        {
            if (vrijednostSpora <= maxVps)
                return fee;
        }

        // Progresivna formula za VPS > 1.990,84 EUR
        // "Preko 1.990,84 eura plaća se pristojba u iznosu od 66,36 eura i još 1% na
        //  razliku iznad 1.990,84 eura, ali ne više od 663,61 euro."
        var progressive = ProgressiveBase + ProgressiveRate * (vrijednostSpora - ProgressiveThreshold);
        return Math.Min(progressive, MaxFee);
    }
}
