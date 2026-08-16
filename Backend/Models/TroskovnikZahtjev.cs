using System.Text.Json.Serialization;

namespace Backend.Models;

// Ulazni oblik: sve sto klijent salje na /izracunaj i /spremi.

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

    /// <summary>
    /// Broj osoba koje odvjetnik brani ili zastupa u istom postupku (Tbr. 40. toč. 1).
    /// Vrijednosti manje od 2 znače da nema povišenja.
    /// </summary>
    [JsonPropertyName("broj_zastupanih_osoba")]
    public int BrojZastupanihOsoba { get; set; } = 1;

    // ── Osnovica po Tbr. 7. toč. 1., 4., 7. i 8. ──────────────────

    /// <summary>
    /// Koja se varijanta osnovice primjenjuje. Vidi <see cref="Models.OsnovicaTbr7"/>.
    /// Zadano toč. 1 — isto ponašanje kao prije uvođenja ovog polja.
    /// </summary>
    [JsonPropertyName("osnovica_tbr7")]
    public string OsnovicaTbr7 { get; set; } = Models.OsnovicaTbr7.Toc1;

    /// <summary>Tbr. 45 — razina suda. Obavezno kad je osnovica `tbr45_neprocjenjiv`.</summary>
    [JsonPropertyName("razina_suda")]
    public string? RazinaSuda { get; set; }

    /// <summary>Tbr. 45 — koje se čitanje primjenjuje. Obavezno kad je osnovica `tbr45_neprocjenjiv`.</summary>
    [JsonPropertyName("tbr45_nacin")]
    public string? Tbr45Nacin { get; set; }

    /// <summary>
    /// Tbr. 7. toč. 4 al. 2 — periodični iznos rente / zakupa / najma.
    /// Uz odabrano razdoblje daje petogodišnji iznos kao vrijednost predmeta spora.
    /// </summary>
    [JsonPropertyName("renta_iznos")]
    public decimal RentaIznos { get; set; }

    /// <summary>Razdoblje uz <see cref="RentaIznos"/> — `mjesecno` (× 60) ili `godisnje` (× 5).</summary>
    [JsonPropertyName("renta_razdoblje")]
    public string? RentaRazdoblje { get; set; }

    // ── Tbr. 41 — povisivanje i snižavanje tarifnih stavaka ───────

    /// <summary>
    /// Tbr. 41. toč. 1 — povišenje za posebno stručno i specijalističko znanje, 0–100 %.
    /// Zadano 0: Tarifa kaže „mogu se povisiti", dakle nikad automatski.
    /// </summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal Tbr41PovisenjePostotak { get; set; }

    /// <summary>
    /// Tbr. 41. toč. 2 — sniženje pojedinih tarifnih stavaka, 0–50 %. Zadano 0.
    /// </summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal Tbr41SnizenjePostotak { get; set; }

    // ── Naknada troškova (Tbr. 48.-51.) i predujam ─────────────────

    /// <summary>
    /// Stvarni izdaci odvjetnika (prijevoz, dnevnica, hotel, ostali izdaci) — vlastiti
    /// trošak obavljanja usluge, ULAZI u PDV osnovicu. Vidi <see cref="Models.VrstaTroska"/>.
    /// </summary>
    [JsonPropertyName("naknada_troskova_stavke")]
    public List<NaknadaTroskovaStavkaRequest> NaknadaTroskovaStavke { get; set; } = [];

    /// <summary>
    /// Predujmovi (npr. za vještačenje) — plaćaju se u ime i za račun stranke, PROLAZNA
    /// stavka po čl. 33. Zakona o PDV-u, ne ulazi u osnovicu niti se oporezuje.
    /// </summary>
    [JsonPropertyName("predujmovi")]
    public List<PredujamStavkaRequest> Predujmovi { get; set; } = [];
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

    /// <summary>
    /// Tbr. 41. toč. 1 — povišenje ove stavke, 0–100 %. Null znači „koristi postotak
    /// s razine zahtjeva" (prečac „primijeni na sve stavke").
    /// </summary>
    [JsonPropertyName("tbr41_povisenje_postotak")]
    public decimal? Tbr41PovisenjePostotak { get; set; }

    /// <summary>
    /// Tbr. 41. toč. 2 — sniženje ove stavke, 0–50 %. Riječ „pojedine" u toč. 2 znači
    /// da se sniženje odnosi na pojedinačne stavke, ne na ukupan zbroj.
    /// </summary>
    [JsonPropertyName("tbr41_snizenje_postotak")]
    public decimal? Tbr41SnizenjePostotak { get; set; }
}

/// <summary>Jedan stvarni izdatak iz naknade troškova (Tbr. 48.-51.).</summary>
public sealed class NaknadaTroskovaStavkaRequest
{
    /// <summary>Jedna od vrijednosti u <see cref="VrstaTroska.Sve"/>.</summary>
    [JsonPropertyName("vrsta")]
    public string Vrsta { get; set; } = "";

    /// <summary>Slobodan opis (npr. "Put Zagreb-Split, 340 km"). Neobavezno.</summary>
    [JsonPropertyName("opis")]
    public string? Opis { get; set; }

    /// <summary>Stvarni iznos u EUR — Tarifa ne propisuje bodove za ove izdatke.</summary>
    [JsonPropertyName("iznos")]
    public decimal Iznos { get; set; }
}

/// <summary>Jedan predujam (npr. za vještačenje) — prolazna stavka.</summary>
public sealed class PredujamStavkaRequest
{
    /// <summary>Slobodan opis (npr. "Predujam za financijsko vještačenje"). Neobavezno.</summary>
    [JsonPropertyName("opis")]
    public string? Opis { get; set; }

    /// <summary>Stvarni iznos u EUR.</summary>
    [JsonPropertyName("iznos")]
    public decimal Iznos { get; set; }
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
