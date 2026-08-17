namespace Backend.Models;

// Oslobodjenja od placanja sudskih pristojbi, Zakon o sudskim pristojbama cl. 10-13.

/// <summary>
/// Čl. 11 — osobe oslobođene plaćanja pristojbi po sili zakona, sve 21 točka.
/// Uz njih stoji i oslobođenje na zahtjev stranke po čl. 10, koje nije po sili
/// zakona nego o njemu odlučuje sud.
///
/// Kategorija ne mijenja iznos — oslobođenje je oslobođenje. Bilježi se zato što
/// troškovnik mora pokazati PO ČEMU je stranka oslobođena, kao što svaka tarifna
/// stavka nosi svoj pravni izvor.
/// </summary>
public static class OslobodjenjeKategorija
{
    public const string RhTijela = "rh_tijela";
    public const string JavneOvlasti = "javne_ovlasti";
    public const string Radnici = "radnici";
    public const string Sluzbenici = "sluzbenici";
    public const string Invaliditet = "invaliditet";
    public const string ObiteljiBranitelja = "obitelji_branitelja";
    public const string ObiteljiDomovinskiRat = "obitelji_domovinski_rat";
    public const string Prognanici = "prognanici";
    public const string SocijalnaSkrb = "socijalna_skrb";
    public const string Humanitarne = "humanitarne";
    public const string DjecaUzdrzavanje = "djeca_uzdrzavanje";
    public const string MajcinstvoOcinstvo = "majcinstvo_ocinstvo";
    public const string PoslovnaSposobnost = "poslovna_sposobnost";
    public const string MaloljetniciBrak = "maloljetnici_brak";
    public const string PredajaDjeteta = "predaja_djeteta";
    public const string MirovinskoZdravstveno = "mirovinsko_zdravstveno";
    public const string UstavnaPrava = "ustavna_prava";
    public const string StetaOkolis = "steta_okolis";
    public const string Sindikati = "sindikati";
    public const string Potrosaci = "potrosaci";
    public const string PosebniZakon = "posebni_zakon";

    /// <summary>Čl. 10 — oslobođenje na zahtjev stranke; o njemu odlučuje sud, ne zakon.</summary>
    public const string ZahtjevCl10 = "zahtjev_cl_10";

    /// <summary>
    /// Tekst svake točke i članak iz kojeg proizlazi. Redoslijed je onaj iz čl. 11,
    /// jer ga odvjetnik traži po broju točke.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Nazivi =
        new Dictionary<string, string>
        {
            [RhTijela] = "čl. 11. t. 1 — Republika Hrvatska i tijela državne vlasti",
            [JavneOvlasti] = "čl. 11. t. 2 — osobe i tijela s javnim ovlastima, u postupcima iz tih ovlasti",
            [Radnici] = "čl. 11. t. 3 — radnici u sporovima o pravima iz radnog odnosa",
            [Sluzbenici] = "čl. 11. t. 4 — službenici i namještenici u upravnim sporovima o službeničkim pravima",
            [Invaliditet] = "čl. 11. t. 5 — osobe s invaliditetom",
            [ObiteljiBranitelja] = "čl. 11. t. 6 — supružnici, djeca i roditelji branitelja smrtno stradalih, nestalih i zatočenih",
            [ObiteljiDomovinskiRat] = "čl. 11. t. 7 — supružnici, djeca i roditelji smrtno stradalih, nestalih i zatočenih u Domovinskom ratu",
            [Prognanici] = "čl. 11. t. 8 — prognanici, izbjeglice i povratnici",
            [SocijalnaSkrb] = "čl. 11. t. 9 — korisnici socijalne skrbi koji primaju zajamčenu minimalnu naknadu",
            [Humanitarne] = "čl. 11. t. 10 — humanitarne organizacije i organizacije osoba s invaliditetom",
            [DjecaUzdrzavanje] = "čl. 11. t. 11 — djeca u postupcima radi uzdržavanja",
            [MajcinstvoOcinstvo] = "čl. 11. t. 12 — postupci radi utvrđivanja majčinstva ili očinstva i troškova trudnoće i poroda",
            [PoslovnaSposobnost] = "čl. 11. t. 13 — postupci radi vraćanja poslovne sposobnosti",
            [MaloljetniciBrak] = "čl. 11. t. 14 — maloljetnici u postupcima dopuštenja za sklapanje braka",
            [PredajaDjeteta] = "čl. 11. t. 15 — postupci radi predaje djeteta i ostvarivanja osobnih odnosa s djetetom",
            [MirovinskoZdravstveno] = "čl. 11. t. 16 — postupci o pravima iz mirovinskog i zdravstvenog osiguranja, osiguranja za slučaj nezaposlenosti i socijalne skrbi",
            [UstavnaPrava] = "čl. 11. t. 17 — zaštita ustavom zajamčenih ljudskih prava protiv konačnih pojedinačnih akata",
            [StetaOkolis] = "čl. 11. t. 18 — naknada štete zbog onečišćenja okoliša",
            [Sindikati] = "čl. 11. t. 19 — sindikati u kolektivnim radnim sporovima",
            [Potrosaci] = "čl. 11. t. 20 — potrošači kao stečajni dužnici i tužitelji u kolektivnoj zaštiti",
            [PosebniZakon] = "čl. 11. t. 21 — drugi kada je propisano posebnim zakonom",
            [ZahtjevCl10] = "čl. 10 — oslobođenje na zahtjev stranke koja ne može platiti bez štete za uzdržavanje"
        };

    /// <summary>
    /// Čl. 11 st. 5 — oslobođenje iz t. 1 i 2 ne obuhvaća jedinice lokalne i područne
    /// (regionalne) samouprave, osim kad im je preneseno obavljanje javnih ovlasti.
    /// </summary>
    public const string NapomenaLokalnaSamouprava =
        "Čl. 11. st. 5: oslobođenje se ne odnosi na jedinice lokalne i područne (regionalne) " +
        "samouprave, osim kada im je preneseno obavljanje javnih ovlasti.";

    public static readonly string[] Sve = [.. Nazivi.Keys];

    /// <summary>Tekst uz kategoriju; prazan string kad kategorija nije poznata ili nije zadana.</summary>
    public static string Naziv(string? kljuc)
        => kljuc is not null && Nazivi.TryGetValue(kljuc, out var naziv) ? naziv : "";
}

/// <summary>
/// Sve što izračun pristojbe treba znati o oslobođenju — čl. 11, 12 i 13 na jednom mjestu.
/// </summary>
/// <param name="Oslobodjen">
/// Zastupana stranka je oslobođena u cijelosti. Odgovara starom prekidaču
/// <c>is_oslobodjen_pristojbi</c>, koji je i dalje u uporabi.
/// </param>
/// <param name="Kategorija">Ključ iz <see cref="OslobodjenjeKategorija"/>; null kad nije naveden.</param>
/// <param name="BrojOsoba">Ukupan broj osoba koje zajedno podnose podnesak (čl. 12).</param>
/// <param name="BrojOslobodjenih">Koliko ih je od toga oslobođeno (čl. 12).</param>
/// <param name="UdioVps">
/// Dio vrijednosti predmeta spora koji otpada na neoslobođene. Null znači da se dio ne
/// može utvrditi, pa se VPS dijeli brojem osoba — čl. 12 st. 2.
/// </param>
/// <param name="PostotakUspjeha">
/// Čl. 13 — omjer u kojem je oslobođena stranka uspjela u postupku, 0–100. Null kad se
/// ne obračunava.
/// </param>
public readonly record struct OslobodjenjeUlaz(
    bool Oslobodjen,
    string? Kategorija,
    int BrojOsoba,
    int BrojOslobodjenih,
    decimal? UdioVps,
    decimal? PostotakUspjeha)
{
    /// <summary>Ulaz bez ijednog oslobođenja — pristojba se računa na puni VPS.</summary>
    public static readonly OslobodjenjeUlaz Nema = new(false, null, 1, 0, null, null);

    /// <summary>Stari trojni oblik: sve ili ništa, bez kategorije i bez suparničara.</summary>
    public static OslobodjenjeUlaz Jednostavni(bool oslobodjen)
        => new(oslobodjen, null, 1, 0, null, null);
}
