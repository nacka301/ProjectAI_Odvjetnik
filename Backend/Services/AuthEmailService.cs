using Backend.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Backend.Services;

/// <summary>
/// MailKit-based email service for authentication flows.
/// Reads SMTP config via Options pattern from appsettings.json / env vars.
/// 
/// Fallback chain for credentials:
///   1. Environment variables (SMTP_HOST, SMTP_PORT, SMTP_USERNAME, SMTP_PASSWORD)
///   2. appsettings.json Smtp section
///   3. Brevo SMTP defaults (smtp-relay.brevo.com:587 with BREVO_SMTP_KEY)
/// </summary>
public sealed class AuthEmailService : IAuthEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<AuthEmailService> _logger;

    public AuthEmailService(IOptions<SmtpSettings> options, ILogger<AuthEmailService> logger)
    {
        _logger = logger;
        _settings = options.Value;

        // Override from environment variables if present (Render/Docker production)
        _settings.Host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? _settings.Host;
        _settings.Username = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? _settings.Username;
        _settings.Password = Environment.GetEnvironmentVariable("SMTP_PASSWORD")
            ?? Environment.GetEnvironmentVariable("BREVO_SMTP_KEY")
            ?? _settings.Password;
        _settings.SenderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? _settings.SenderEmail;

        if (int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port))
            _settings.Port = port;
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Host) &&
        !string.IsNullOrWhiteSpace(_settings.Username) &&
        !string.IsNullOrWhiteSpace(_settings.Password);

    // ═══════════════════════════════════════════════════════════
    //  IAuthEmailService
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> SendVerificationEmailAsync(string toEmail, string verificationLink)
    {
        var subject = "PravoTek — Potvrdite svoju email adresu";
        var html = BuildVerificationHtml(verificationLink);
        return await SendAsync(toEmail, subject, html);
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var subject = "PravoTek — Resetiranje lozinke";
        var html = BuildPasswordResetHtml(resetLink);
        return await SendAsync(toEmail, subject, html);
    }

    // ═══════════════════════════════════════════════════════════
    //  Core MailKit sender
    // ═══════════════════════════════════════════════════════════

    private async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("AuthEmailService: SMTP not configured — skipping email to {Email}. " +
                "Set SMTP_HOST, SMTP_USERNAME, SMTP_PASSWORD environment variables.", toEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Connect with STARTTLS (port 587) or implicit SSL (port 465)
            var tlsOption = _settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_settings.Host, _settings.Port, tlsOption);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            _logger.LogInformation("AuthEmailService: Sent '{Subject}' to {Email}", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            // CRITICAL: Never let email failure crash the API
            _logger.LogError(ex, "AuthEmailService: Failed to send '{Subject}' to {Email}", subject, toEmail);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HTML Templates
    // ═══════════════════════════════════════════════════════════

    private static string BuildVerificationHtml(string link)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="hr">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"></head>
        <body style="margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f4f6f8;">
          <div style="max-width:560px;margin:40px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);">
            <div style="background:#1B2A4A;padding:24px 32px;text-align:center;">
              <h1 style="margin:0;color:#C9A84C;font-size:22px;font-family:Georgia,serif;">PravoTek</h1>
              <p style="margin:4px 0 0;color:rgba(255,255,255,0.7);font-size:12px;">Pravna tehnologija za odvjetnike</p>
            </div>
            <div style="padding:32px;">
              <h2 style="margin:0 0 16px;color:#1B2A4A;font-size:18px;">Dobrodošli u PravoTek!</h2>
              <p style="color:#475569;font-size:14px;line-height:1.6;">
                Zahvaljujemo na registraciji. Kliknite gumb ispod da biste potvrdili svoju email adresu i aktivirali račun.
              </p>
              <div style="text-align:center;margin:28px 0;">
                <a href="{link}" style="display:inline-block;background:#1B2A4A;color:#C9A84C;font-weight:600;text-decoration:none;padding:14px 36px;border-radius:8px;font-size:15px;">
                  Potvrdi email adresu
                </a>
              </div>
              <p style="color:#94a3b8;font-size:12px;line-height:1.5;">
                Ako niste kreirali račun, ignorirajte ovaj email.<br>
                Link istječe za 24 sata.
              </p>
              <hr style="border:none;border-top:1px solid #e2e8f0;margin:24px 0;">
              <p style="color:#cbd5e1;font-size:11px;text-align:center;">
                &copy; 2026 PravoTek. Svi podaci zaštićeni GDPR sukladno.
              </p>
            </div>
          </div>
        </body>
        </html>
        """;
    }

    private static string BuildPasswordResetHtml(string link)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="hr">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1.0"></head>
        <body style="margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f4f6f8;">
          <div style="max-width:560px;margin:40px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);">
            <div style="background:#1B2A4A;padding:24px 32px;text-align:center;">
              <h1 style="margin:0;color:#C9A84C;font-size:22px;font-family:Georgia,serif;">PravoTek</h1>
            </div>
            <div style="padding:32px;">
              <h2 style="margin:0 0 16px;color:#1B2A4A;font-size:18px;">Resetiranje lozinke</h2>
              <p style="color:#475569;font-size:14px;line-height:1.6;">
                Primili smo zahtjev za resetiranje vaše lozinke. Kliknite gumb ispod da biste postavili novu lozinku.
              </p>
              <div style="text-align:center;margin:28px 0;">
                <a href="{link}" style="display:inline-block;background:#1B2A4A;color:#C9A84C;font-weight:600;text-decoration:none;padding:14px 36px;border-radius:8px;font-size:15px;">
                  Resetiraj lozinku
                </a>
              </div>
              <p style="color:#94a3b8;font-size:12px;line-height:1.5;">
                Ako niste zatražili promjenu lozinke, ignorirajte ovaj email.<br>
                Link istječe za 1 sat.
              </p>
            </div>
          </div>
        </body>
        </html>
        """;
    }
}
