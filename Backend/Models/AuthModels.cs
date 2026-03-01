using System.Text.Json.Serialization;

namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  Registration DTOs
// ═══════════════════════════════════════════════════════════

public sealed class RegisterRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("firm_name")]
    public string? FirmName { get; set; }

    [JsonPropertyName("firm_oib")]
    public string? FirmOib { get; set; }
}

public sealed class RegisterResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════
//  Email Verification DTOs
// ═══════════════════════════════════════════════════════════

public sealed class VerifyEmailRequest
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
}

public sealed class VerifyEmailResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════
//  SMTP Options (bound via Options pattern)
// ═══════════════════════════════════════════════════════════

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp-relay.brevo.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SenderName { get; set; } = "PravoTek";
    public string SenderEmail { get; set; } = "noreply@pravotek.hr";
    public bool UseSsl { get; set; } = false; // STARTTLS on port 587
}
