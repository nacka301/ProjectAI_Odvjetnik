using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend.Services;

/// <summary>
/// Sends transactional emails through Resend's SMTP relay (smtp.resend.com:465).
/// Uses MailKit with implicit TLS.
///
/// Config section "Resend" in appsettings.json:
///   { "ApiKey": "re_...", "SenderName": "PravoTek", "SenderEmail": "onboarding@resend.dev" }
///
/// Environment-variable overrides (Render / Docker):
///   RESEND_API_KEY, RESEND_SENDER_EMAIL
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    private const string SmtpHost = "smtp.resend.com";
    private const int SmtpPort = 465;
    private const string SmtpUsername = "resend";           // Resend requires literal "resend"

    private readonly string? _apiKey;
    private readonly string _senderName;
    private readonly string _senderEmail;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _logger = logger;

        _apiKey = config["Resend:ApiKey"]
            ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");

        _senderName = config["Resend:SenderName"] ?? "PravoTek";

        _senderEmail = config["Resend:SenderEmail"]
            ?? Environment.GetEnvironmentVariable("RESEND_SENDER_EMAIL")
            ?? "onboarding@resend.dev";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        byte[]? attachment = null,
        string? attachmentName = null)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("ResendEmailService: API key not configured — skipping email to {Email}. " +
                "Set Resend:ApiKey in appsettings or RESEND_API_KEY env var.", toEmail);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toEmail)
            || !System.Net.Mail.MailAddress.TryCreate(toEmail, out _))
        {
            _logger.LogWarning("ResendEmailService: Invalid recipient email: {Email}", toEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_senderName, _senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };

            if (attachment is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName))
            {
                bodyBuilder.Attachments.Add(attachmentName, attachment);
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(SmtpUsername, _apiKey);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            _logger.LogInformation("ResendEmailService: Sent '{Subject}' to {Email}", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResendEmailService: Failed to send '{Subject}' to {Email}", subject, toEmail);
            return false;
        }
    }
}
