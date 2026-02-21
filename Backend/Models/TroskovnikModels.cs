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
    public decimal VrijednostSpora { get; set; }

    /// <summary>Odabrane stavke s količinama.</summary>
    public List<TroskovnikItemRequest> Stavke { get; set; } = [];

    /// <summary>Podaci o klijentu (za ispis).</summary>
    public TroskovnikParty? Klijent { get; set; }

    /// <summary>Podaci o odvjetniku (za ispis).</summary>
    public TroskovnikParty? Odvjetnik { get; set; }

    /// <summary>Opis predmeta / napomena.</summary>
    public string? OpisPredmeta { get; set; }
}

public sealed class TroskovnikItemRequest
{
    /// <summary>Identifikator stavke iz kataloga (npr. "tbr7_tuzba").</summary>
    public string ItemId { get; set; } = "";

    /// <summary>Količina (broj radnji, sati, itd.).</summary>
    public int Kolicina { get; set; } = 1;
}

public sealed class TroskovnikParty
{
    public string Ime { get; set; } = "";
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Grad { get; set; }
}

public sealed class TroskovnikResponse
{
    public List<TroskovnikLineItem> Stavke { get; set; } = [];
    public int UkupnoBodova { get; set; }
    public decimal UkupnoEurBezPdv { get; set; }
    public decimal Pdv { get; set; }
    public decimal UkupnoEurSPdv { get; set; }
    public decimal VrijednostBoda { get; set; } = TarifaConstants.BodValue;
    public TroskovnikParty? Klijent { get; set; }
    public TroskovnikParty? Odvjetnik { get; set; }
    public string? OpisPredmeta { get; set; }
    public DateTime DatumIzrade { get; set; } = DateTime.UtcNow;
}

public sealed class TroskovnikLineItem
{
    public int Rbr { get; set; }
    public string ItemId { get; set; } = "";
    public string Opis { get; set; } = "";
    public string TarifniBroj { get; set; } = "";
    public int Kolicina { get; set; }
    public int BodovaPoJedinici { get; set; }
    public int UkupnoBodova { get; set; }
    public decimal IznosEur { get; set; }
}

// ── Catalog item definition ──────────────────────────────────

public sealed class TarifnaStavka
{
    public string Id { get; set; } = "";
    public string Kategorija { get; set; } = "";
    public string Opis { get; set; } = "";
    public string TarifniBroj { get; set; } = "";

    /// <summary>Fiksni bodovi (null = koristi Tbr71 lookup).</summary>
    public int? FiksniBody { get; set; }

    /// <summary>Postotak od Tbr. 7.1 (1.0 = 100%, 0.5 = 50%, itd.).</summary>
    public decimal? Tbr71Postotak { get; set; }

    /// <summary>Dodatno povećanje na Tbr. 7.1 rezultat (0.25 = +25%).</summary>
    public decimal? Tbr71Povecanje { get; set; }

    /// <summary>Maksimalni broj bodova.</summary>
    public int? MaxBodova { get; set; }

    /// <summary>Minimalni broj bodova.</summary>
    public int? MinBodova { get; set; }

    /// <summary>Jedinica mjere (komad, sat, stranica).</summary>
    public string Jedinica { get; set; } = "komad";

    /// <summary>Redoslijed za prikaz.</summary>
    public int SortOrder { get; set; }
}
