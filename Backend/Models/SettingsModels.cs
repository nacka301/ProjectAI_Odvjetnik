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
    public string Role { get; set; } = "admin";
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
}

public class LawFirmUpdateRequest
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Oib { get; set; }
    public string? Address { get; set; }
    public string? Iban { get; set; }
}

// ── Subscription ──

public class SubscriptionResponse
{
    public string Id { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ValidUntil { get; set; }
    public string? LawFirmId { get; set; }
}

// ── Registration Extension ──

public class RegisterFirmRequest
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string FirmName { get; set; } = "";
    public string? FirmOib { get; set; }
}
