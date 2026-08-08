namespace Backend.Models;

// Osnovica po Tbr. 7. i Tbr. 45 — dopustene vrijednosti koje zahtjev smije poslati
// i rezultat njihova razrjesavanja.

/// <summary>
/// Osnovica po Tbr. 7. Tarifa na devet mjesta upućuje na „Tbr. 7. toč. 1., 4., 7. i 8."
/// (Tbr. 8. toč. 1, 3, 4; Tbr. 9. toč. 1, 2, 3, 5, 6; Tbr. 10.). To NISU četiri različita
/// iznosa nego četiri varijante ISTE osnovice — koja se primjenjuje ovisi o vrsti postupka,
/// a ne o pojedinoj stavci.
/// </summary>
public static class OsnovicaTbr7
{
    /// <summary>Tbr. 7. toč. 1 — tablica vrijednost spora → bodovi. Zadano.</summary>
    public const string Toc1 = "toc1";

    /// <summary>Tbr. 7. toč. 4 al. 1 — autorsko / industrijsko vlasništvo: po toč. 1, ali ne manje od 100 bodova.</summary>
    public const string Toc4Autorsko = "toc4_autorsko";

    /// <summary>Tbr. 7. toč. 4 al. 2 — renta / zakup / najam: kao VPS uzima se petogodišnji iznos.</summary>
    public const string Toc4Renta = "toc4_renta";

    /// <summary>Tbr. 7. toč. 7 st. 1 — domaća arbitraža: po toč. 1.</summary>
    public const string Toc7DomacaArbitraza = "toc7_domaca_arbitraza";

    /// <summary>Tbr. 7. toč. 7 st. 2 — međunarodna arbitraža: po toč. 1 s povišenjem od 100 %.</summary>
    public const string Toc7MedunarodnaArbitraza = "toc7_medunarodna_arbitraza";

    /// <summary>Tbr. 7. toč. 8 — mirno rješavanje sporova: po toč. 1.</summary>
    public const string Toc8Mirenje = "toc8_mirenje";

    /// <summary>Tbr. 45 — neprocjenjivi predmeti: 100 / 200 / 500 bodova prema razini suda.</summary>
    public const string Tbr45Neprocjenjiv = "tbr45_neprocjenjiv";

    public static readonly string[] Sve =
    [
        Toc1, Toc4Autorsko, Toc4Renta, Toc7DomacaArbitraza,
        Toc7MedunarodnaArbitraza, Toc8Mirenje, Tbr45Neprocjenjiv
    ];
}

/// <summary>
/// Tbr. 45 — razina suda određuje bodove u neprocjenjivim predmetima.
/// Namjerno BEZ zadane vrijednosti: razlika između razina je do peterostruka,
/// pa se ne smije pretpostaviti.
/// </summary>
public static class RazinaSuda
{
    /// <summary>Tbr. 45. toč. 1 — općinski sud i tijela iste razine.</summary>
    public const string Opcinski = "opcinski";

    /// <summary>Tbr. 45. toč. 2 — županijski i trgovački sudovi te tijela iste razine.</summary>
    public const string ZupanijskiTrgovacki = "zupanijski_trgovacki";

    /// <summary>Tbr. 45. toč. 3 — Vrhovni sud Republike Hrvatske i tijela iste razine.</summary>
    public const string Vrhovni = "vrhovni";

    /// <summary>Bodovi po razini suda — doslovno iz Tbr. 45. toč. 1–3.</summary>
    public static readonly Dictionary<string, int> Bodovi = new()
    {
        [Opcinski]            = 100,
        [ZupanijskiTrgovacki] = 200,
        [Vrhovni]             = 500
    };
}

/// <summary>
/// NEJASNO U TARIFI — Tbr. 45 daje 100/200/500 bodova „za svaku poduzetu radnju", ali ne
/// kaže primjenjuju li se na taj iznos postoci iz Tbr. 8 (50 % / 25 %) i Tbr. 9. Oba su
/// čitanja obranjiva, pa Tarifa ne daje odgovor — korisnik bira, sustav ne izmišlja.
/// </summary>
public static class Tbr45Nacin
{
    /// <summary>100/200/500 ulazi kao osnovica; postoci iz Tbr. 8./9. se primjenjuju na nju.</summary>
    public const string KaoOsnovica = "kao_osnovica";

    /// <summary>Svaka radnja nosi puni iznos; postoci i stropovi iz Tbr. 8./9. se ne primjenjuju.</summary>
    public const string FiksnoPoRadnji = "fiksno_po_radnji";

    public static readonly string[] Sve = [KaoOsnovica, FiksnoPoRadnji];
}

/// <summary>Razdoblje za preračun petogodišnjeg iznosa iz Tbr. 7. toč. 4 al. 2.</summary>
public static class RentaRazdoblje
{
    public const string Mjesecno = "mjesecno";  // × 60
    public const string Godisnje = "godisnje";  // × 5
}

/// <summary>Rezultat razrješavanja osnovice po Tbr. 7. / Tbr. 45.</summary>
/// <param name="Bodovi">Broj bodova osnovice.</param>
/// <param name="PravniIzvor">Odredba iz koje osnovica proizlazi.</param>
/// <param name="FiksnoPoRadnji">
/// True samo kod Tbr. 45 uz način „fiksno_po_radnji": svaka radnja dobiva puni iznos,
/// bez postotaka i stropova iz Tbr. 8./9.
/// </param>
public readonly record struct OsnovicaRezultat(decimal Bodovi, string PravniIzvor, bool FiksnoPoRadnji);
