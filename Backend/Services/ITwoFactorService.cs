using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Models;
using OtpNet;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TwoFactorService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITwoFactorService
{
/// <summary>
    /// Generates a new TOTP secret and returns the otpauth:// URI for QR code generation.
    /// The secret is NOT saved yet — it's only persisted after successful verify-setup.
    /// </summary>
    TwoFactorSetupResponse GenerateSetup(string userEmail) ;

/// <summary>
    /// Verifies the first TOTP token to confirm the user has successfully paired
    /// their authenticator app. On success, encrypts and saves the secret to DB.
    /// Returns recovery codes.
    ///
    /// Refuses an account that already has 2FA on — enrolling overwrites the
    /// secret, so allowing it would turn a stolen session into a device takeover.
    /// </summary>
    /// <exception cref="TwoFactorStoreUnavailableException">The MFA store could not be read.</exception>
    Task<TwoFactorResponse> VerifySetupAsync(string userId, string userEmail, string base32Secret, string token) ;

/// <summary>
    /// Validates a 6-digit TOTP token during login.
    /// Also accepts recovery codes as fallback.
    /// </summary>
    /// <exception cref="TwoFactorStoreUnavailableException">The MFA store could not be read.</exception>
    Task<TwoFactorResponse> ValidateTokenAsync(string userId, string token) ;

    /// <summary>
    /// True only when this account demonstrably has TOTP switched on.
    /// </summary>
    /// <exception cref="TwoFactorStoreUnavailableException">
    /// The MFA store could not be read. Deliberately not folded into <c>false</c>:
    /// callers use this to decide whether to let someone in, and "unknown" must
    /// not be answered as "no 2FA here".
    /// </exception>
    Task<bool> IsTwoFactorEnabledAsync(string userId) ;

    /// <exception cref="TwoFactorStoreUnavailableException">The MFA store could not be read.</exception>
    Task<TwoFactorResponse> DisableTwoFactorAsync(string userId, string token) ;
}