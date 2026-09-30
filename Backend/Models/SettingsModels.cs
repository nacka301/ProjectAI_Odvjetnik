using Backend.Services;

namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Settings API — Request / Response DTOs
// ═══════════════════════════════════════════════════════════

// ── Profile ──

public class ProfileResponse
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = "lawyer";
    public string? LawFirmId { get; set; }
}

public class ProfileUpdateRequest
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
}

// ── Law Firm ──

public class LawFirmResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Oib { get; set; }
    public string? Address { get; set; }
    public string? Iban { get; set; }
    /// <summary>Telefon ureda — za zaglavlje ispisa/izvoza troškovnika, ne korisnikov broj.</summary>
    public string? Phone { get; set; }
    /// <summary>E-mail ureda — za zaglavlje ispisa/izvoza troškovnika, ne korisnikov login mail.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Je li ured u sustavu PDV-a. Zadana vrijednost za novi troškovnik — korisnik je
    /// i dalje može ručno promijeniti po dokumentu (vidi TroskovnikRequest.IsPdvUkljucen).
    /// </summary>
    public bool PdvObveznik { get; set; }

    /// <summary>
    /// Tbr. 50 — cijena litre bezolovnog benzina 100 oktana (EUR), za izračun naknade
    /// za automobil (30% cijene po km, vidi TarifaConstants.Tbr50FaktorGoriva). Tržišna
    /// vrijednost, nije regulirana — ured je sam unosi i ažurira. Null = nije postavljeno;
    /// tada se naknada za prijevoz ne može automatski izračunati, samo unijeti ručno.
    /// </summary>
    public decimal? CijenaGorivaEurL { get; set; }

    /// <summary>
    /// Tbr. 51 — dnevnica, u visini dnevnice suca Županijskog suda (EUR/dan). Pravno
    /// izveden ali NEPOTVRĐEN iznos (vidi TarifaConstants) — ured ga potvrđuje i unosi
    /// sam. Null = nije postavljeno; naknada za dnevnicu se tada unosi samo ručno.
    /// </summary>
    public decimal? DnevnicaEur { get; set; }
}

public class LawFirmUpdateRequest
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Oib { get; set; }
    public string? Address { get; set; }
    public string? Iban { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool PdvObveznik { get; set; }
    public decimal? CijenaGorivaEurL { get; set; }
    public decimal? DnevnicaEur { get; set; }
}

// ── Subscription ──

public class SubscriptionResponse
{
    public string Id { get; set; } = "";
    public string PlanId { get; set; } = SubscriptionPlans.PlanId;
    public string Status { get; set; } = "";
    public string? ValidFrom { get; set; }
    public string? ValidUntil { get; set; }
    public decimal PriceEur { get; set; }
    public int MaxCases { get; set; }
    public int MaxInvoices { get; set; }
    public int AiCreditsLimit { get; set; }
    public string? LawFirmId { get; set; }

    // ── What the user is actually shown ──
    //
    // Filled by the controller from SubscriptionGate, never computed in the
    // browser. The badge in the header and the middleware that refuses the API
    // therefore read the same verdict; when they were computed separately, the
    // interface could say "everything is fine" while the API answered 402.
    //
    // Status above is the raw database value and stays for the admin screen.

    /// <summary>"proba" | "pretplata" | "isteklo".</summary>
    public string State { get; set; } = SubscriptionState.Paid;

    /// <summary>Whole days of trial left. 0 means the last day. Null when not on trial.</summary>
    public int? DaysRemaining { get; set; }

    /// <summary>When access ends. Null means open-ended.</summary>
    public DateTimeOffset? AccessUntil { get; set; }
}

// ── Registration Extension ──

public class RegisterFirmRequest
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FirmName { get; set; } = "";
    public string? FirmOib { get; set; }
}

// ── Beta Credit Usage ──

public class FirmUsageResponse
{
    public string Id { get; set; } = "";
    public string LawFirmId { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public int UsedCredits { get; set; }
    public int MaxCredits { get; set; }
    public string? ResetDate { get; set; }
}

/// <summary>Result of a credit consumption check.</summary>
public class CreditCheckResult
{
    public bool Allowed { get; set; }
    public string? Message { get; set; }
    public int? Used { get; set; }
    public int? Max { get; set; }

    public static CreditCheckResult Allow(int? used = null, int? max = null)
        => new() { Allowed = true, Used = used, Max = max };

    public static CreditCheckResult Deny(string message)
        => new() { Allowed = false, Message = message };
}

public class CreditConsumeRequest
{
    public string UserId { get; set; } = "";
    public string ModuleName { get; set; } = "";
}
