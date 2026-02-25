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

    /// <summary>Opis predmeta / napomena.</summary>
    [JsonPropertyName("opis_predmeta")]
    public string? OpisPredmeta { get; set; }

    /// <summary>ID postojeće stranke (opcija — za povezivanje s klijentom).</summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>ID predmeta (opcija — za povezivanje s predmetom).</summary>
    [JsonPropertyName("case_id")]
    public string? CaseId { get; set; }
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
}
