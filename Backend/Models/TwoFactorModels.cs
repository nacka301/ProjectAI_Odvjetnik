namespace Backend.Models;

// ═══════════════════════════════════════════════════════════
//  TOTP 2FA Models — Request/Response DTOs
//  RFC 6238 compliant, Google Authenticator compatible
// ═══════════════════════════════════════════════════════════

/// <summary>Response from POST /api/2fa/setup — returns QR data for authenticator app.</summary>
public class TwoFactorSetupResponse
{
    /// <summary>The otpauth:// URI for QR code generation (client-side).</summary>
    public string OtpauthUrl { get; set; } = "";

    /// <summary>Base32-encoded secret for manual entry in authenticator app.</summary>
    public string ManualEntryKey { get; set; } = "";
}

/// <summary>Request body for POST /api/2fa/setup — provides user email for QR label.</summary>
public class TwoFactorSetupRequest
{
    public string Email { get; set; } = "";
}

/// <summary>Request body for POST /api/2fa/verify-setup — confirms first token after QR scan.</summary>
public class TwoFactorVerifySetupRequest
{
    /// <summary>Supabase user ID.</summary>
    public string UserId { get; set; } = "";

    /// <summary>User email (for logging).</summary>
    public string Email { get; set; } = "";

    /// <summary>6-digit TOTP code from the authenticator app.</summary>
    public string Token { get; set; } = "";

    /// <summary>The base32 secret returned during setup (client sends back to confirm pairing).</summary>
    public string Secret { get; set; } = "";
}

/// <summary>Request body for POST /api/2fa/validate — validates token during login.</summary>
public class TwoFactorValidateRequest
{
    /// <summary>6-digit TOTP code from the authenticator app.</summary>
    public string Token { get; set; } = "";

    /// <summary>User ID to look up the stored secret.</summary>
    public string UserId { get; set; } = "";
}

/// <summary>Generic 2FA operation response.</summary>
public class TwoFactorResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string[]? RecoveryCodes { get; set; }
}

/// <summary>Response from GET /api/2fa/status — check if 2FA is enabled.</summary>
public class TwoFactorStatusResponse
{
    public bool Enabled { get; set; }
}

/// <summary>Database record for user_profiles table (2FA columns).</summary>
public class UserTwoFactorRecord
{
    public string UserId { get; set; } = "";
    public string? TotpSecretEncrypted { get; set; }
    public bool TotpEnabled { get; set; }
    public string? RecoveryCodesEncrypted { get; set; }
    public string? TotpEnabledAt { get; set; }
}
