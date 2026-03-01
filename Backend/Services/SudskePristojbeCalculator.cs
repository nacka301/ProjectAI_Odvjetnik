namespace Backend.Services;

/// <summary>
/// Kalkulator sudskih pristojbi prema Uredbi o tarifi sudskih pristojbi
/// (NN 95/2015, EUR konverzija NN 156/2022).
/// Tbr. 1 — I. PARNIČNI POSTUPAK — Tužba (pristojba na tužbu / prijedlog).
///
/// Konverzija: 1 EUR = 7,53450 HRK (fiksni tečaj).
/// Iznosi zaokruženi na 2 decimale.
/// Maksimalna pristojba: 3.981,68 EUR (= 30.000 HRK).
/// </summary>
public static class SudskePristojbeCalculator
{
    // ── Tbr. 1 — Fee brackets ──────────────────────────────────
    // Upper VPS bound (EUR) → Fee (EUR)
    // Original HRK values converted at fixed rate 7.53450.

    private static readonly (decimal MaxVps, decimal Fee)[] Brackets =
    [
        (    398.17m,    13.27m),  //  do   3.000 HRK →    100 HRK
        (    796.34m,    26.54m),  //  do   6.000 HRK →    200 HRK
        (  1_327.23m,    39.82m),  //  do  10.000 HRK →    300 HRK
        (  1_990.84m,    53.09m),  //  do  15.000 HRK →    400 HRK
        (  3_318.07m,    79.63m),  //  do  25.000 HRK →    600 HRK
        (  5_308.91m,   119.45m),  //  do  40.000 HRK →    900 HRK
        (  7_963.37m,   172.54m),  //  do  60.000 HRK →  1.300 HRK
        ( 13_272.28m,   265.45m),  //  do 100.000 HRK →  2.000 HRK
        ( 26_544.56m,   398.17m),  //  do 200.000 HRK →  3.000 HRK
        ( 39_816.84m,   530.89m),  //  do 300.000 HRK →  4.000 HRK
        ( 53_089.12m,   663.61m),  //  do 400.000 HRK →  5.000 HRK
        ( 66_361.40m,   796.34m),  //  do 500.000 HRK →  6.000 HRK
        ( 99_542.10m,   929.06m),  //  do 750.000 HRK →  7.000 HRK
        (132_722.81m, 1_061.78m),  //  do 1.000.000 HRK →  8.000 HRK
        (165_903.51m, 1_194.51m),  //  do 1.250.000 HRK →  9.000 HRK
        (199_084.21m, 1_327.23m),  //  do 1.500.000 HRK → 10.000 HRK
        (265_445.61m, 1_592.67m),  //  do 2.000.000 HRK → 12.000 HRK
        (331_807.02m, 1_858.12m),  //  do 2.500.000 HRK → 14.000 HRK
        (398_168.42m, 2_123.56m),  //  do 3.000.000 HRK → 16.000 HRK
        (464_529.82m, 2_389.01m),  //  do 3.500.000 HRK → 18.000 HRK
        (530_891.22m, 2_654.46m),  //  do 4.000.000 HRK → 20.000 HRK
        (663_614.03m, 2_919.90m),  //  do 5.000.000 HRK → 22.000 HRK
        (796_336.83m, 3_185.35m),  //  do 6.000.000 HRK → 24.000 HRK
        (929_059.64m, 3_450.79m),  //  do 7.000.000 HRK → 26.000 HRK
        (1_061_782.44m, 3_716.24m),//  do 8.000.000 HRK → 28.000 HRK
        (1_327_228.05m, 3_981.68m),//  do 10.000.000 HRK → 30.000 HRK
    ];

    /// <summary>Maximum court fee (30.000 HRK = 3.981,68 EUR).</summary>
    public const decimal MaxFee = 3_981.68m;

    /// <summary>
    /// Izračunava sudsku pristojbu na tužbu (Tbr. 1) prema VPS-u.
    /// Ako je VPS ≤ 0, vraća 0.
    /// </summary>
    public static decimal Izracunaj(decimal vrijednostSpora)
    {
        if (vrijednostSpora <= 0)
            return 0m;

        foreach (var (maxVps, fee) in Brackets)
        {
            if (vrijednostSpora <= maxVps)
                return fee;
        }

        // Over 1.327.228,05 EUR → max fee
        return MaxFee;
    }
}
