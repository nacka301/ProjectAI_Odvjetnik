namespace Backend.Models;

// Brojcane konstante Tarife: vrijednost boda, stope umanjenja i bodovi po
// zaprijecenoj kazni. Sve vrijednosti nose uputu na odredbu iz koje proizlaze.

/// <summary>
/// Tarifa o nagradama i naknadi troškova za rad odvjetnika (NN 2023)
/// 1 bod = 2.00 EUR (Tbr. 54)
/// </summary>
public static class TarifaConstants
{
    /// <summary>Puni naziv propisa, za navođenje uz svaku stavku troškovnika.</summary>
    public const string NazivTarife =
        "Tarifa o nagradama i naknadi troškova za rad odvjetnika (Hrvatska odvjetnička komora, 14. listopada 2023.)";

    /// <summary>Uredba koja propisuje sudske pristojbe.</summary>
    public const string NazivUredbeOPristojbama =
        "Uredba o tarifi sudskih pristojbi (NN 37/2023)";

    /// <summary>Tbr. 54 — vrijednost boda.</summary>
    public const decimal BodValue = 2.00m;

    /// <summary>Zakon o PDV-u — opća stopa.</summary>
    public const decimal PdvRate = 0.25m;

    /// <summary>
    /// Branitelj po službenoj dužnosti ili na teret proračunskih sredstava — 50% tarife.
    ///
    /// Tbr. 4. toč. 7 sam ne određuje iznos nego upućuje dalje: "Nagrada za rad odvjetnika
    /// koji je određen za branitelja po službenoj dužnosti, sukladno odredbama Zakona o
    /// odvjetništvu, određuje se Pravilnikom ministarstva nadležnog za poslove pravosuđa."
    ///
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 1.
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    /// </summary>
    public const decimal KazneniReductionRate = 0.50m;

    /// <summary>
    /// Tbr. 4. toč. 8: "Odvjetniku koji zastupa oštećenika u postupku koji se vodi pred
    /// policijom, državnim odvjetništvom (...) te na raspravi pred sudom, pripada 50%
    /// nagrade iz ovog tarifnog broja."
    /// </summary>
    public const decimal Tbr4t8Rate = 0.50m;

    /// <summary>
    /// Opunomoćenik djeteta žrtve kaznenog djela — 70% od nagrade iz Tbr. 4. toč. 8.
    ///
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 2.
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    ///
    /// Budući da je Tbr. 4. toč. 8 već 50% nagrade iz Tbr. 4, efektivna stopa je
    /// 0,50 × 0,70 = 35% pune tarife — ne 70%. Vidi TarifaUmanjenjaTests.
    /// </summary>
    public const decimal ZastupnikDjetetaRate = 0.70m;

    /// <summary>Ponuđeni tekst napomene; korisnik ga smije izmijeniti ili obrisati.</summary>
    public const string ZadanaNapomena =
        "Na traženi iznos obračunava se zakonska zatezna kamata sukladno članku 29. " +
        "Zakona o obveznim odnosima.";
}

/// <summary>Zapriječena kazna → bodovi (Tbr. 4)</summary>
/// <summary>
/// Tbr. 4. toč. 1 — bodovi po zapriječenoj kazni.
/// Novčana kazna ili kazna do 3 god. zatvora = 100,
/// do 5 = 200, do 10 = 300, preko 10 = 400, dugotrajni = 500.
/// </summary>
public static class ZaprijecenaKaznaTiers
{
    /// <summary>Ključ tiera za kaznu dugotrajnog zatvora (Tbr. 4. toč. 1 al. 5).</summary>
    public const string DugotrajniZatvor = "dugotrajni";

    public static readonly Dictionary<string, int> Bodovi = new()
    {
        ["novcana_do3"] = 100,   // novčana kazna ili kazna do 3 godine zatvora
        ["do_5"]        = 200,   // do 5 godina zatvora
        ["do_10"]       = 300,   // do 10 godina zatvora
        ["preko_10"]    = 400,   // preko 10 godina zatvora
        ["dugotrajni"]  = 500    // dugotrajni zatvor
    };
}
