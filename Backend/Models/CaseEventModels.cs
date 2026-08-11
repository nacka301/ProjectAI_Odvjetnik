namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Case Events / Upisnik — DTOs
//
//  Svaka radnja u spisu: podnesak, ročište, pravni savjet, itd.
//  Povezuje se s Odvjetničkom tarifom za automatski izračun troškova.
// ═══════════════════════════════════════════════════════════

/// <summary>Write model for INSERT (no Id/CreatedAt — DB generates those).</summary>
public sealed class CaseEventRecord : IFirmScoped
{
    public string? CaseId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? LawFirmId { get; set; }
    public string? EventDate { get; set; }
    public string EventType { get; set; } = "Ostalo";
    public string Description { get; set; } = "";
    public string? TariffItem { get; set; }
    public decimal TariffPoints { get; set; }
    public decimal PointValue { get; set; } = 0.50m;
    public bool IsBillable { get; set; } = true;
    public string BillingStatus { get; set; } = "pending";
}

/// <summary>Read model for SELECT (includes DB-generated fields).</summary>
public sealed class CaseEventReadRecord
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string CaseId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string EventDate { get; set; } = "";
    public string EventType { get; set; } = "Ostalo";
    public string Description { get; set; } = "";
    public string? TariffItem { get; set; }
    public decimal TariffPoints { get; set; }
    public decimal PointValue { get; set; } = 0.50m;
    public decimal? TotalEur { get; set; }
    public bool IsBillable { get; set; } = true;
    public string BillingStatus { get; set; } = "pending";
}

/// <summary>Request DTO for creating a new case event (no UserId/TenantId — injected by controller).</summary>
public sealed class CreateCaseEventRequest
{
    public string CaseId { get; set; } = "";
    public string? EventDate { get; set; }
    public string EventType { get; set; } = "Ostalo";
    public string Description { get; set; } = "";
    public string? TariffItem { get; set; }
    public decimal TariffPoints { get; set; }
    public decimal PointValue { get; set; } = 0.50m;
    public bool IsBillable { get; set; } = true;
}

/// <summary>Request DTO for updating an existing case event (all fields nullable for partial update).</summary>
public sealed class UpdateCaseEventRequest
{
    public string? EventDate { get; set; }
    public string? EventType { get; set; }
    public string? Description { get; set; }
    public string? TariffItem { get; set; }
    public decimal? TariffPoints { get; set; }
    public decimal? PointValue { get; set; }
    public bool? IsBillable { get; set; }
    public string? BillingStatus { get; set; }
}

/// <summary>Allowed event types for validation.</summary>
public static class CaseEventTypes
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Podnesak", "Ročište", "Pravni savjet", "Pristup sudu",
        "Pregled spisa", "Sastanak s klijentom", "Dopis",
        "Žalba", "Prigovor", "Nagodba", "Ovrha",
        "Uvid u dokaze", "Vještačenje", "Ostalo"
    };

    /// <summary>Default tariff points for each event type (Odvjetnička tarifa NN 142/2012).</summary>
    public static readonly Dictionary<string, decimal> DefaultPoints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Podnesak"]              = 50,
        ["Ročište"]               = 100,
        ["Pravni savjet"]         = 50,
        ["Pristup sudu"]          = 25,
        ["Pregled spisa"]         = 25,
        ["Sastanak s klijentom"]  = 50,
        ["Dopis"]                 = 25,
        ["Žalba"]                 = 100,
        ["Prigovor"]              = 75,
        ["Nagodba"]               = 100,
        ["Ovrha"]                 = 75,
        ["Uvid u dokaze"]         = 25,
        ["Vještačenje"]           = 50,
        ["Ostalo"]                = 25,
    };
}
