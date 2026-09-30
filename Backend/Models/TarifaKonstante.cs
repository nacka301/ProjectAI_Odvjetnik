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

    /// <summary>Ponuđeni tekst napomene; korisnik ga smije izmijeniti ili obrisati.</summary>
    public const string ZadanaNapomena =
        "Na traženi iznos obračunava se zakonska zatezna kamata sukladno članku 29. " +
        "Zakona o obveznim odnosima, po kamatnoj stopi koja se određuje za svako " +
        "polugodište uvećanjem kamatne stope koju je Europska središnja banka " +
        "primijenila na svoje posljednje glavne operacije refinanciranja prije " +
        "prvog kalendarskog dana tekućeg polugodišta za tri postotna poena.";

    /// <summary>
    /// Tbr. 50 — naknada za automobil: 30% cijene litre bezolovnog benzina 100 oktana
    /// po prijeđenom kilometru (+ cestarina, zasebno). Ovo je jedini dio Tbr. 50 koji je
    /// propisan brojkom — CIJENA goriva je tržišna, nije regulirana Uredbom o najvišim
    /// cijenama (ta pokriva samo 95/dizel), pa NIJE konstanta ovdje nego postavka ureda
    /// (<c>LawFirmResponse.CijenaGorivaEurL</c>) koju ured sam unosi i ažurira.
    /// </summary>
    public const decimal Tbr50FaktorGoriva = 0.30m;

    // Tbr. 51 — dnevnica, u visini dnevnice suca Županijskog suda. Iznos NIJE ovdje:
    // pravno je izveden, ali nepotvrđen (najbolji nađeni izvor 19,91 € iz NN 117/2012,
    // bez potvrde postoji li noviji propis), pa ga ured potvrđuje i unosi sam u postavke
    // ureda (LawFirmResponse.DnevnicaEur) — kod ga ne zakucava.
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

/// <summary>
/// Jedna vremenska verzija Tarife o nagradama: naziv propisa, vrijednost boda i datum
/// od kojeg je na snazi.
/// </summary>
public sealed record TarifaVerzija(string Naziv, decimal BodValue, DateOnly VrijediOd);

/// <summary>
/// Registar verzija Tarife o nagradama i odabir mjerodavne verzije po <b>dospijeću</b>
/// (Tbr. 52), a ne po datumu radnje.
///
/// Tbr. 52: „Za obračun nagrade i naknade troškova mjerodavna je Tarifa i vrijednost boda
/// koja je na snazi u trenutku dospijeća potraživanja."
///
/// Danas postoji jedna verzija (HOK, 14.10.2023.), pa svaki datum razrješava na nju.
/// Registar je popis, a ne konstanta, da bi buduća verzija bila dodavanje jednog retka —
/// ne izmjena logike izračuna. Stvarne brojke (vrijednost boda, razredi) neke ranije
/// verzije NISU ovdje jer nisu poznate; kad zatrebaju, dodaju se kao novi <see cref="TarifaVerzija"/>.
/// </summary>
public static class TarifaVerzije
{
    /// <summary>Sve poznate verzije, poredane rastuće po datumu stupanja na snagu.</summary>
    public static readonly IReadOnlyList<TarifaVerzija> Sve =
    [
        new TarifaVerzija(TarifaConstants.NazivTarife, TarifaConstants.BodValue, new DateOnly(2023, 10, 14)),
    ];

    /// <summary>Trenutno važeća (najnovija) verzija.</summary>
    public static TarifaVerzija Trenutna => Sve[^1];

    /// <summary>
    /// Bira verziju Tarife koja je na snazi na dan dospijeća. Null dospijeće → trenutna.
    /// Dospijeće prije najstarije poznate verzije vraća najstariju (starije nisu u registru).
    /// </summary>
    public static TarifaVerzija Razrijesi(DateOnly? datumDospijeca)
        => RazrijesiIz(Sve, datumDospijeca);

    /// <summary>
    /// Čista selekcija nad proizvoljnim popisom verzija — izdvojena da se odabir po
    /// dospijeću može testirati i s više od jedne verzije.
    /// </summary>
    public static TarifaVerzija RazrijesiIz(IReadOnlyList<TarifaVerzija> verzije, DateOnly? datumDospijeca)
    {
        if (verzije.Count == 0)
            throw new InvalidOperationException("Registar verzija Tarife je prazan.");

        var poredane = verzije.OrderBy(v => v.VrijediOd).ToList();

        if (datumDospijeca is null)
            return poredane[^1];

        TarifaVerzija? odabrana = null;
        foreach (var v in poredane)
            if (v.VrijediOd <= datumDospijeca.Value)
                odabrana = v;

        return odabrana ?? poredane[0];
    }
}
