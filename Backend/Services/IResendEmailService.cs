using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ResendEmailService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IResendEmailService
{
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachment = null,
        string? attachmentName = null) ;
}