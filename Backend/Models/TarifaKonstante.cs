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

    /// <summary>Uredba koja propisuje sudske pristojbe — na snazi od 1.6.2026.</summary>
    public const string NazivUredbeOPristojbama =
        "Uredba o Tarifi sudskih pristojbi (NN 50/2026)";

    /// <summary>
    /// Uredba koja je prestala važiti 1.6.2026. Navodi se uz retke koje čl. 2 nove
    /// Uredbe zadržava na starom propisu — odgovor na tužbu podnesen prije tog dana i
    /// upravni spor pokrenut prije tog dana.
    /// </summary>
    public const string NazivStareUredbeOPristojbama =
        "Uredba o Tarifi sudskih pristojbi (NN 37/2023)";

    /// <summary>
    /// Zakon koji uređuje tko plaća pristojbu, tko je oslobođen i kako se utvrđuje
    /// vrijednost predmeta spora. Iznose propisuje Uredba, ne ovaj Zakon.
    /// </summary>
    public const string NazivZakonaOPristojbama =
        "Zakon o sudskim pristojbama (NN 118/18, 51/23)";

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
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 1:
    /// "Nagrada za rad odvjetnika prilikom obrana po službenoj dužnosti i obrana na teret
    /// proračunskih sredstava određuje se u visini od 50% od nagrade za rad koja bi
    /// odvjetniku pripadala za pojedine radnje u postupku prema Tarifi (...)"
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    /// </summary>
    public const decimal KazneniReductionRate = 0.50m;

    /// <summary>
    /// Opunomoćenik djeteta žrtve kaznenog djela — 70% pune tarife.
    ///
    /// Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, ČL. 2:
    /// "Nagrada za rad odvjetnika kao opunomoćenika djeteta žrtve kaznenog djela određuje
    /// se u visini od 70% od nagrade za rad koja bi odvjetniku pripadala za pojedine
    /// radnje u postupku prema Tarifi (...)"
    /// https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
    ///
    /// Osnovica je ista kao u čl. 1 — nagrada za pojedinu radnju prema Tarifi. Čl. 2 se
    /// ne poziva na Tbr. 4. toč. 8, pa se te dvije stope ne množe: 70% je 70%.
    /// Umanjenje iz Tbr. 4. toč. 8 (zastupanje oštećenika) nosi sama stavka u katalogu.
    /// </summary>
    public const decimal ZastupnikDjetetaRate = 0.70m;
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
