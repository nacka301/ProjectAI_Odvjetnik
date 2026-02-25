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
    public decimal VrijednostSpora { get; set; }
    public int UkupnoBodova { get; set; }
    public decimal UkupnoEurBezPdv { get; set; }
    public decimal Pdv { get; set; }
    public decimal UkupnoEurSPdv { get; set; }
    public string? KlijentIme { get; set; }
    public string? KlijentOib { get; set; }
    public string? OdvjetnikIme { get; set; }
    public string? OdvjetnikOib { get; set; }
    public string? OdvjetnikAdresa { get; set; }
    public string? OdvjetnikIban { get; set; }
    public string? ProtustrankaIme { get; set; }
    public string? ProtustrankaOib { get; set; }
    public string? ProtustrankaAdresa { get; set; }
    public string? ProtustrankaPolozaj { get; set; }
    public bool HasVat { get; set; }
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
    public decimal VrijednostSpora { get; set; }
    public int UkupnoBodova { get; set; }
    public decimal UkupnoEurBezPdv { get; set; }
    public decimal Pdv { get; set; }
    public decimal UkupnoEurSPdv { get; set; }
    public string? KlijentIme { get; set; }
    public string? KlijentOib { get; set; }
    public string? OdvjetnikIme { get; set; }
    public string? OdvjetnikOib { get; set; }
    public string? OdvjetnikAdresa { get; set; }
    public string? OdvjetnikIban { get; set; }
    public string? ProtustrankaIme { get; set; }
    public string? ProtustrankaOib { get; set; }
    public string? ProtustrankaAdresa { get; set; }
    public string? ProtustrankaPolozaj { get; set; }
    public bool HasVat { get; set; }
    public object? RawJsonOutput { get; set; }
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
