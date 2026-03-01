namespace Backend.Services;

/// <summary>
/// Interface for sending authentication-related emails (verification, password reset).
/// Decoupled from the existing valuation EmailService to allow independent testing.
/// </summary>
public interface IAuthEmailService
{
    /// <summary>
    /// Send a verification email with the provided link.
    /// Returns true if sent successfully, false otherwise.
    /// Must NOT throw — failures are logged internally.
    /// </summary>
    Task<bool> SendVerificationEmailAsync(string toEmail, string verificationLink);

    /// <summary>
    /// Send a password-reset email with the provided link.
    /// Returns true if sent successfully, false otherwise.
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
}
