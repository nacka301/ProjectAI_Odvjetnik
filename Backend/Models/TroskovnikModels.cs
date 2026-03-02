using System.Text.Json.Serialization;

namespace Backend.Models;

/// <summary>
/// Tarifa o nagradama i naknadi troškova za rad odvjetnika (NN 2023)
/// 1 bod = 2.00 EUR (Tbr. 54)
/// </summary>
public static class TarifaConstants
{
    public const decimal BodValue = 2.00m;
    public const decimal PdvRate = 0.25m; // 25% PDV
    // AUDIT NOTE: Tbr. 4.7 u Tarifi kaže "određuje se Pravilnikom ministarstva nadležnog za
    // poslove pravosuđa" — 50% stopa dolazi iz tog Pravilnika, NE iz same Tarife.
    public const decimal KazneniReductionRate = 0.50m;   // Branitelj po službenoj dužnosti / na teret proračuna (izvor: Pravilnik, ne Tarifa Tbr. 4.7)
    public const decimal Tbr4t8Rate = 0.50m;             // Tbr. 4.8 — zastupanje oštećenika = 50% Tbr. 4 ✓ (potvrđeno u PDF-u)
    // AUDIT NOTE: ZastupnikDjetetaRate od 0.70 NIJE pronađen ni u Tarifi ni u Uredbi o pristojbama.
    // Izvor ove stope treba potvrditi iz Zakona o zaštiti djece žrtava kaznenih djela ili sl.
    public const decimal ZastupnikDjetetaRate = 0.70m;   // Opunomoćenik djeteta žrtve = 70% od Tbr. 4.8 — IZVOR NEPOZNAT U PRILOŽENIM PDF-ovima
    // Efektivni postotak za dijete žrtve: Tbr4t8Rate × ZastupnikDjetetaRate = 35%
}

/// <summary>Zapriječena kazna → bodovi (Tbr. 4)</summary>
/// <summary>
/// Tbr. 4. toč. 1 — bodovi po zapriječenoj kazni.
/// Novčana kazna ili kazna do 3 god. zatvora = 100,
/// do 5 = 200, do 10 = 300, preko 10 = 400, dugotrajni = 500.
/// </summary>
public static class ZaprijecenaKaznaTiers
{
    public static readonly Dictionary<string, int> Bodovi = new()
    {
        ["novcana_do3"] = 100,   // novčana kazna ili kazna do 3 godine zatvora
        ["do_5"]        = 200,   // do 5 godina zatvora
        ["do_10"]       = 300,   // do 10 godina zatvora
        ["preko_10"]    = 400,   // preko 10 godina zatvora
        ["dugotrajni"]  = 500    // dugotrajni zatvor
    };
}

// ── Request / Response ────────────────────────────────────────

public sealed class TroskovnikRequest
{
    /// <summary>Vrijednost predmeta spora (EUR) – koristi se za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_spora")]
    public decimal VrijednostSpora { get; set; }

    /// <summary>Odabrane stavke s količinama.</summary>
    [JsonPropertyName("stavke")]
    public List<TroskovnikItemRequest> Stavke { get; set; } = [];

    /// <summary>Podaci o klijentu (za ispis).</summary>
    [JsonPropertyName("klijent")]
    public TroskovnikParty? Klijent { get; set; }

    /// <summary>Nested odvjetnik object: { naziv, oib, adresa, iban }</summary>
    [JsonPropertyName("odvjetnik")]
    public OdvjetnikRequest? Odvjetnik { get; set; }

    /// <summary>Nested predmet object: { sud, poslovni_broj }</summary>
    [JsonPropertyName("predmet")]
    public PredmetRequest? Predmet { get; set; }

    /// <summary>Nested stranke object: { zastupana_stranka, protustranka }</summary>
    [JsonPropertyName("stranke")]
    public StrankeRequest? Stranke { get; set; }

    /// <summary>Backwards-compatible protustranka_polozaj (still accepted).</summary>
    [JsonPropertyName("protustranka_polozaj")]
    public string? ProtustrankaPolozaj { get; set; }

    /// <summary>Obračunaj PDV (true = obračunaj, false = bez PDV-a).</summary>
    /// <summary>
    /// Preferirani flag iz frontenda: kad je `isPdvUkljucen` poslan, koristi se on.
    /// Ostaje `HasVat` zbog kompatibilnosti s postojećim klijentima.
    /// </summary>
    [JsonPropertyName("has_vat")]
    public bool HasVat { get; set; } = true;

    /// <summary>
    /// Novi flag (preferred): `isPdvUkljucen` — default null kada nije poslan.
    /// Ako je true => obračunaj PDV; ako false => bez PDV.
    /// </summary>
    [JsonPropertyName("is_pdv_ukljucen")]
    public bool? IsPdvUkljucen { get; set; } = null;

    /// <summary>Zapriječena kazna – tier key (novcana, do_1, do_3, do_5, do_10, preko_10, dugotrajni).</summary>
    [JsonPropertyName("zaprijecena_kazna")]
    public string? ZaprijecenaKazna { get; set; }

    /// <summary>Obrana po službenoj dužnosti – 50% umanjenje.</summary>
    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    /// <summary>Obrana na teret proračuna – 50% umanjenje.</summary>
    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    /// <summary>Zastupnik djeteta – 70% od Tbr. 4.</summary>
    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    /// <summary>Stjecaj / produljeno KD – korisnik bira tier prema najtežoj kazni.</summary>
    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    /// <summary>Prekršaj s oduzimanjem predmeta – nagrada po Tbr. 7.1 umjesto fiksnih 100.</summary>
    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    /// <summary>Vrijednost oduzetog predmeta / imovinske koristi (EUR) za Tbr. 7.1 lookup.</summary>
    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

    /// <summary>Opis predmeta / napomena.</summary>
    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    /// <summary>ID postojeće stranke (opcija — za povezivanje s klijentom).</summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>ID predmeta (opcija — za povezivanje s predmetom).</summary>
    [JsonPropertyName("case_id")]
    public string? CaseId { get; set; }

    /// <summary>If true and no ClientId, backend will auto-create a client from stranke data.</summary>
    [JsonPropertyName("auto_create_client")]
    public bool AutoCreateClient { get; set; }

    /// <summary>Grad for client auto-creation (not in nested stranke object).</summary>
    [JsonPropertyName("client_grad")]
    public string? ClientGrad { get; set; }

    /// <summary>Oslobođenje od plaćanja sudskih pristojbi (čl. 11. Zakona).</summary>
    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }
}

// New request DTOs for nested schema
public sealed class OdvjetnikRequest
{
    [JsonPropertyName("naziv")]
    public string? Naziv { get; set; }

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }
}

public sealed class PredmetRequest
{
    [JsonPropertyName("sud")]
    public string? Sud { get; set; }

    [JsonPropertyName("poslovni_broj")]
    public string? PoslovniBroj { get; set; }
}

public sealed class StrankaRequest
{
    [JsonPropertyName("polozaj")]
    public string? Polozaj { get; set; }

    [JsonPropertyName("naziv")]
    public string? Naziv { get; set; }

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }
}

public sealed class StrankeRequest
{
    [JsonPropertyName("zastupana_stranka")]
    public StrankaRequest? ZastupanaStranka { get; set; }

    [JsonPropertyName("protustranka")]
    public StrankaRequest? Protustranka { get; set; }
}

public sealed class TroskovnikItemRequest
{
    /// <summary>Identifikator stavke iz kataloga (npr. "tbr7_tuzba").</summary>
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    /// <summary>Količina (broj radnji, sati, itd.).</summary>
    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; } = 1;
}

public sealed class TroskovnikParty
{
    [JsonPropertyName("ime")]
    public string Ime { get; set; } = "";

    [JsonPropertyName("oib")]
    public string? Oib { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }

    [JsonPropertyName("grad")]
    public string? Grad { get; set; }
}

public sealed class TroskovnikResponse
{
    [JsonPropertyName("stavke")]
    public List<TroskovnikLineItem> Stavke { get; set; } = [];

    [JsonPropertyName("ukupno_bodova")]
    public int UkupnoBodova { get; set; }

    [JsonPropertyName("ukupno_eur_bez_pdv")]
    public decimal UkupnoEurBezPdv { get; set; }

    [JsonPropertyName("pdv")]
    public decimal Pdv { get; set; }

    [JsonPropertyName("ukupno_eur_s_pdv")]
    public decimal UkupnoEurSPdv { get; set; }

    [JsonPropertyName("vrijednost_boda")]
    public decimal VrijednostBoda { get; set; } = TarifaConstants.BodValue;

    [JsonPropertyName("klijent")]
    public TroskovnikParty? Klijent { get; set; }

    [JsonPropertyName("odvjetnik")]
    public TroskovnikParty? Odvjetnik { get; set; }

    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    [JsonPropertyName("datum_izrade")]
    public DateTime DatumIzrade { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("protustranka")]
    public TroskovnikParty? Protustranka { get; set; }

    [JsonPropertyName("protustranka_polozaj")]
    public string? ProtustrankaPolozaj { get; set; }

    [JsonPropertyName("is_pdv_ukljucen")]
    public bool? IsPdvUkljucen { get; set; }

    [JsonPropertyName("has_vat")]
    public bool HasVat { get; set; }

    [JsonPropertyName("sud")]
    public string? Sud { get; set; }

    [JsonPropertyName("broj_predmeta")]
    public string? BrojPredmeta { get; set; }

    // Nested objects (new schema) — carried alongside legacy flat fields for gradual migration
    [JsonPropertyName("odvjetnik_info")]
    public OdvjetnikRequest? OdvjetnikInfo { get; set; }

    [JsonPropertyName("predmet_info")]
    public PredmetRequest? PredmetInfo { get; set; }

    [JsonPropertyName("stranke_info")]
    public StrankeRequest? StrankeInfo { get; set; }

    // Kazneni echo fields
    [JsonPropertyName("zaprijecena_kazna")]
    public string? ZaprijecenaKazna { get; set; }

    [JsonPropertyName("is_po_sluzbenoj_duznosti")]
    public bool IsPoSluzbenojDuznosti { get; set; }

    [JsonPropertyName("is_na_teret_proracuna")]
    public bool IsNaTeretProracuna { get; set; }

    [JsonPropertyName("is_zastupnik_djeteta")]
    public bool IsZastupnikDjeteta { get; set; }

    [JsonPropertyName("is_stjecaj")]
    public bool IsStjecaj { get; set; }

    [JsonPropertyName("is_oduzimanje_prekrsaj")]
    public bool IsOduzimanjePrekrsaj { get; set; }

    [JsonPropertyName("vrijednost_oduzimanja")]
    public decimal VrijednostOduzimanja { get; set; }

    // Sudske pristojbe
    [JsonPropertyName("sudska_pristojba")]
    public decimal SudskaPristojba { get; set; }

    [JsonPropertyName("is_oslobodjen_pristojbi")]
    public bool IsOslobodjenPristojbi { get; set; }

    /// <summary>Grand total = odvjetnički trošak (s PDV) + sudska pristojba.</summary>
    [JsonPropertyName("sveukupni_trosak")]
    public decimal SveukupniTrosak { get; set; }
}

public sealed class TroskovnikLineItem
{
    [JsonPropertyName("rbr")]
    public int Rbr { get; set; }

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    [JsonPropertyName("kolicina")]
    public int Kolicina { get; set; }

    [JsonPropertyName("bodova_po_jedinici")]
    public int BodovaPoJedinici { get; set; }

    [JsonPropertyName("ukupno_bodova")]
    public int UkupnoBodova { get; set; }

    [JsonPropertyName("iznos_eur")]
    public decimal IznosEur { get; set; }
}

// ── Catalog item definition ──────────────────────────────────

public sealed class TarifnaStavka
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kategorija")]
    public string Kategorija { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    /// <summary>Fiksni bodovi (null = koristi Tbr71 lookup).</summary>
    [JsonPropertyName("fiksni_body")]
    public int? FiksniBody { get; set; }

    /// <summary>Postotak od Tbr. 7.1 (1.0 = 100%, 0.5 = 50%, itd.).</summary>
    [JsonPropertyName("tbr71_postotak")]
    public decimal? Tbr71Postotak { get; set; }

    /// <summary>Dodatno povećanje na Tbr. 7.1 rezultat (0.25 = +25%).</summary>
    [JsonPropertyName("tbr71_povecanje")]
    public decimal? Tbr71Povecanje { get; set; }

    /// <summary>Maksimalni broj bodova.</summary>
    [JsonPropertyName("max_bodova")]
    public int? MaxBodova { get; set; }

    /// <summary>Minimalni broj bodova.</summary>
    [JsonPropertyName("min_bodova")]
    public int? MinBodova { get; set; }

    /// <summary>Jedinica mjere (komad, sat, stranica).</summary>
    [JsonPropertyName("jedinica")]
    public string Jedinica { get; set; } = "komad";

    /// <summary>Redoslijed za prikaz.</summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>True = bodovi dolaze iz ZaprijecenaKaznaTiers lookup-a (kazneni postupak).</summary>
    [JsonPropertyName("kazneni_base")]
    public bool KazneniBase { get; set; }

    /// <summary>Dodatno povećanje na kazneni base bodove (npr. 0.5 = +50% za žalbu).</summary>
    [JsonPropertyName("kazneni_povecanje")]
    public decimal? KazneniPovecanje { get; set; }

    /// <summary>True = prekršajni stavka s mogućnošću oduzimanja; kad je oduzimanje aktivno, koristi Tbr. 7.1 po vrijednosti.</summary>
    [JsonPropertyName("prekrsaj_oduzimanje")]
    public bool PrekrsajOduzimanje { get; set; }
}
