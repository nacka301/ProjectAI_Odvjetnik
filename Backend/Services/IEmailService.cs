namespace Backend.Services;

/// <summary>
/// General-purpose transactional email service (Resend SMTP via MailKit).
/// Replaces the legacy Brevo HTTP-based EmailService for all outbound emails
/// except authentication flows (handled by IAuthEmailService).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an HTML email. Optionally attach a single file (e.g. PDF invoice).
    /// Returns true on success; never throws — failures are logged internally.
    /// </summary>
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachment = null,
        string? attachmentName = null);

    /// <summary>Indicates whether the SMTP credentials are configured.</summary>
    bool IsConfigured { get; }
}
