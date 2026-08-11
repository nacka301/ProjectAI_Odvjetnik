namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  Client (Stranka) — Central entity for client management
// ══════════════════════════════════════════════════════════════

public sealed class ClientRecord
{
    public string? UserId { get; set; }
    public string ImeNaziv { get; set; } = "";
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Grad { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string Tip { get; set; } = "fizicka";
    public string? Napomena { get; set; }
}

public sealed class ClientReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ImeNaziv { get; set; } = "";
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Grad { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string Tip { get; set; } = "fizicka";
    public string? Napomena { get; set; }
}

// ══════════════════════════════════════════════════════════════
//  Case (Predmet) — Legal cases linked to clients
// ══════════════════════════════════════════════════════════════

public sealed class CaseRecord
{
    public string? UserId { get; set; }
    public string? ClientId { get; set; }
    public string BrojPredmeta { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string? Opis { get; set; }
    public string Vrsta { get; set; } = "parnica";
    public string Status { get; set; } = "aktivan";
    public decimal? VrijednostSpora { get; set; }
    public string? Sud { get; set; }
    public string? Sudac { get; set; }
    public string? DatumPokretanja { get; set; }
    public string? DatumZatvaranja { get; set; }
}

public sealed class CaseReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? ClientId { get; set; }
    public string BrojPredmeta { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string? Opis { get; set; }
    public string Vrsta { get; set; } = "parnica";
    public string Status { get; set; } = "aktivan";
    public decimal? VrijednostSpora { get; set; }
    public string? Sud { get; set; }
    public string? Sudac { get; set; }
    public string? DatumPokretanja { get; set; }
    public string? DatumZatvaranja { get; set; }
    public bool IsDeleted { get; set; }
}

// ══════════════════════════════════════════════════════════════
//  Troškovnik persistence record
// ══════════════════════════════════════════════════════════════

public sealed class TroskovnikRecord
{
    public string? UserId { get; set; }
    public string? ClientId { get; set; }
    public string? CaseId { get; set; }
    public string? OpisPredmeta { get; set; }

    /// <summary>
    /// Razdoblje koje troškovnik pokriva — najraniji i najkasniji datum radnje
    /// (<c>gggg-mm-dd</c>). Pojedinačni datumi ostaju u <see cref="RawJsonOutput"/>.
    /// </summary>
    public string? DatumOd { get; set; }

    /// <inheritdoc cref="DatumOd"/>
    public string? DatumDo { get; set; }
    public decimal VrijednostSpora { get; set; }
    public decimal UkupnoBodova { get; set; }
    public decimal UkupnoEurBezPdv { get; set; }
    public decimal Pdv { get; set; }
    public decimal UkupnoEurSPdv { get; set; }
    // Nested objects per requested schema
    public OdvjetnikInfo? Odvjetnik { get; set; }
    public PredmetInfo? Predmet { get; set; }
    public StrankeInfo? Stranke { get; set; }

    public bool HasVat { get; set; }
    public bool IsPdvUkljucen { get; set; }
    public string? ZaprijecenaKazna { get; set; }
    public bool IsPoSluzbenojDuznosti { get; set; }
    public bool IsNaTeretProracuna { get; set; }
    public bool IsZastupnikDjeteta { get; set; }
    public bool IsStjecaj { get; set; }
    public bool IsOduzimanjePrekrsaj { get; set; }
    public decimal VrijednostOduzimanja { get; set; }
    public object? RawJsonOutput { get; set; }
}

public sealed class TroskovnikReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? ClientId { get; set; }
    public string? CaseId { get; set; }
    public string? OpisPredmeta { get; set; }

    /// <summary>Razdoblje koje troškovnik pokriva; NULL za zapise od prije uvođenja datuma uz radnju.</summary>
    public string? DatumOd { get; set; }

    /// <inheritdoc cref="DatumOd"/>
    public string? DatumDo { get; set; }
    public decimal VrijednostSpora { get; set; }
    public decimal UkupnoBodova { get; set; }
    public decimal UkupnoEurBezPdv { get; set; }
    public decimal Pdv { get; set; }
    public decimal UkupnoEurSPdv { get; set; }
    // Nested objects for read record
    public OdvjetnikInfo? Odvjetnik { get; set; }
    public PredmetInfo? Predmet { get; set; }
    public StrankeInfo? Stranke { get; set; }

    public bool HasVat { get; set; }
    public bool IsPdvUkljucen { get; set; }
    public string? ZaprijecenaKazna { get; set; }
    public bool IsPoSluzbenojDuznosti { get; set; }
    public bool IsNaTeretProracuna { get; set; }
    public bool IsZastupnikDjeteta { get; set; }
    public bool IsStjecaj { get; set; }
    public bool IsOduzimanjePrekrsaj { get; set; }
    public decimal VrijednostOduzimanja { get; set; }
    public object? RawJsonOutput { get; set; }
}

// Nested DTOs for new schema
public sealed class OdvjetnikInfo
{
    public string? Naziv { get; set; }
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Iban { get; set; }
}

public sealed class PredmetInfo
{
    public string? Sud { get; set; }
    public string? PoslovniBroj { get; set; }
}

public sealed class StrankaInfo
{
    public string? Polozaj { get; set; }
    public string? Naziv { get; set; }
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
}

public sealed class StrankeInfo
{
    public StrankaInfo? ZastupanaStranka { get; set; }
    public StrankaInfo? Protustranka { get; set; }
}

// ══════════════════════════════════════════════════════════════
//  API Request/Response DTOs
// ══════════════════════════════════════════════════════════════

public sealed class CreateClientRequest
{
    public string ImeNaziv { get; set; } = "";
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Grad { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string Tip { get; set; } = "fizicka";
    public string? Napomena { get; set; }
}

public sealed class UpdateClientRequest
{
    public string? ImeNaziv { get; set; }
    public string? Oib { get; set; }
    public string? Adresa { get; set; }
    public string? Grad { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Tip { get; set; }
    public string? Napomena { get; set; }
}

public sealed class CreateCaseRequest
{
    public string? ClientId { get; set; }
    public string BrojPredmeta { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string? Opis { get; set; }
    public string Vrsta { get; set; } = "parnica";
    public string Status { get; set; } = "aktivan";
    public decimal? VrijednostSpora { get; set; }
    public string? Sud { get; set; }
    public string? Sudac { get; set; }
    public string? DatumPokretanja { get; set; }
    public string? DatumZatvaranja { get; set; }
}

public sealed class UpdateCaseRequest
{
    public string? ClientId { get; set; }
    public string? BrojPredmeta { get; set; }
    public string? Naziv { get; set; }
    public string? Opis { get; set; }
    public string? Vrsta { get; set; }
    public string? Status { get; set; }
    public decimal? VrijednostSpora { get; set; }
    public string? Sud { get; set; }
    public string? Sudac { get; set; }
    public string? DatumPokretanja { get; set; }
    public string? DatumZatvaranja { get; set; }
}

/// <summary>Full client profile with linked cases and troškovnici.</summary>
public sealed class ClientProfileResponse
{
    public ClientReadRecord Client { get; set; } = new();
    public List<CaseReadRecord> Cases { get; set; } = [];
    public List<TroskovnikReadRecord> Troskovnici { get; set; } = [];
}
