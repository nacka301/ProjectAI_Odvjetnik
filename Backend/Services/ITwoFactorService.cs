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
    /// </summary>
    Task<TwoFactorResponse> VerifySetupAsync(string userId, string userEmail, string base32Secret, string token) ;

/// <summary>
    /// Validates a 6-digit TOTP token during login.
    /// Also accepts recovery codes as fallback.
    /// </summary>
    Task<TwoFactorResponse> ValidateTokenAsync(string userId, string token) ;

    Task<bool> IsTwoFactorEnabledAsync(string userId) ;

    Task<TwoFactorResponse> DisableTwoFactorAsync(string userId, string token) ;
}