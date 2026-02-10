using Backend.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Globalization;

namespace Backend.Services;

/// <summary>
/// Sends valuation result emails via Gmail SMTP.
/// Requires GMAIL_USER and GMAIL_APP_PASSWORD environment variables.
/// Generate App Password at: https://myaccount.google.com/apppasswords
/// </summary>
public class EmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string? _gmailUser;
    private readonly string? _gmailAppPassword;

    public EmailService(ILogger<EmailService> logger, IConfiguration config)
    {
        _logger = logger;
        _gmailUser = config["Gmail:User"]
            ?? Environment.GetEnvironmentVariable("GMAIL_USER");
        _gmailAppPassword = config["Gmail:AppPassword"]
            ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_gmailUser) && !string.IsNullOrWhiteSpace(_gmailAppPassword);

    /// <summary>
    /// Send valuation result to the specified email address.
    /// </summary>
    public async Task<bool> SendValuationResultAsync(
        string recipientEmail,
        FullEvaluationResult result,
        string propertyDescription)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("EmailService: Gmail credentials not configured — skipping.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
        {
            _logger.LogWarning("EmailService: Invalid recipient email: {Email}", recipientEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AI Procjenitelj", _gmailUser));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = $"Procjena nekretnine — {FormatEur(result.EstimatedValueEur)} €";

            var html = BuildEmailHtml(result, propertyDescription);
            var bodyBuilder = new BodyBuilder { HtmlBody = html };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_gmailUser, _gmailAppPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("EmailService: Sent valuation result to {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailService: Failed to send email to {Email}", recipientEmail);
            return false;
        }
    }

    private static string BuildEmailHtml(FullEvaluationResult r, string propertyDescription)
    {
        var ci = new CultureInfo("hr-HR");
        var compsHtml = "";

        if (r.UsedComparables?.Count > 0)
        {
            compsHtml = "<h3 style=\"color:#1e40af;\">📊 Korištene usporedne nekretnine</h3><table style=\"border-collapse:collapse;width:100%;font-size:14px;\">"
                + "<tr style=\"background:#eff6ff;\"><th style=\"padding:8px;text-align:left;border-bottom:2px solid #bfdbfe;\">Nekretnina</th>"
                + "<th style=\"padding:8px;text-align:right;border-bottom:2px solid #bfdbfe;\">€/m²</th>"
                + "<th style=\"padding:8px;text-align:right;border-bottom:2px solid #bfdbfe;\">Sličnost</th>"
                + "<th style=\"padding:8px;text-align:center;border-bottom:2px solid #bfdbfe;\">Status</th></tr>";

            foreach (var c in r.UsedComparables)
            {
                var title = TruncateText(c.Title, 50);
                var link = !string.IsNullOrEmpty(c.Url)
                    ? $"<a href=\"{c.Url}\" style=\"color:#2563eb;text-decoration:none;\">{title}</a>"
                    : title;

                compsHtml += $"<tr style=\"border-bottom:1px solid #e5e7eb;\">"
                    + $"<td style=\"padding:6px 8px;\">{link}</td>"
                    + $"<td style=\"padding:6px 8px;text-align:right;\">{c.PricePerM2:N0} €/m²</td>"
                    + $"<td style=\"padding:6px 8px;text-align:right;\">{c.SimilarityScore:P0}</td>"
                    + $"<td style=\"padding:6px 8px;text-align:center;\">{(c.IsOutlier ? "❌" : "✅")}</td>"
                    + "</tr>";
            }
            compsHtml += "</table>";
        }

        var adjustmentsHtml = "";
        if (r.Adjustments?.Count > 0)
        {
            adjustmentsHtml = "<h3 style=\"color:#1e40af;\">🔧 Korekcije</h3><ul style=\"font-size:14px;line-height:1.8;\">";
            foreach (var adj in r.Adjustments)
            {
                var sign = adj.Percent >= 0 ? "+" : "";
                adjustmentsHtml += $"<li><strong>{adj.Factor}</strong>: {sign}{adj.Percent:N1}% — {adj.Description}</li>";
            }
            adjustmentsHtml += "</ul>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif; max-width: 700px; margin: 0 auto; padding: 20px; color: #1f2937;'>

<div style='background: linear-gradient(135deg, #1e40af, #3b82f6); border-radius: 16px; padding: 32px; color: white; text-align: center; margin-bottom: 24px;'>
  <h1 style='margin: 0 0 8px; font-size: 28px;'>🏠 AI Procjena Nekretnine</h1>
  <p style='margin: 0; opacity: 0.9; font-size: 14px;'>{propertyDescription}</p>
  <div style='margin-top: 20px; font-size: 42px; font-weight: bold;'>{FormatEur(r.EstimatedValueEur)} €</div>
  <p style='margin: 4px 0 0; opacity: 0.85; font-size: 14px;'>{r.CalculatedPricePerM2:N0} €/m²</p>
</div>

<div style='background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 12px; padding: 16px; margin-bottom: 20px;'>
  <table style='width:100%;font-size:14px;'>
    <tr>
      <td style='padding:4px 0;'><strong>Pouzdanost (Math):</strong></td>
      <td style='text-align:right;'>{r.MathConfidenceScore}/100 — {r.MathConfidenceLevel}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Pouzdanost (Tržište):</strong></td>
      <td style='text-align:right;'>{r.ConfidenceScore}/100 — {r.ConfidenceLevel}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Regija:</strong></td>
      <td style='text-align:right;'>{r.RegionName}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Pronađeno oglasa:</strong></td>
      <td style='text-align:right;'>{r.TotalListingsFound}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Korisnih cijena:</strong></td>
      <td style='text-align:right;'>{r.UsablePricePoints}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Outlieri uklonjeni:</strong></td>
      <td style='text-align:right;'>{r.OutliersRemoved}</td>
    </tr>
    <tr>
      <td style='padding:4px 0;'><strong>Ukupna korekcija:</strong></td>
      <td style='text-align:right;'>{r.TotalAdjustmentPercent:+0.0;-0.0}%</td>
    </tr>
  </table>
</div>

{compsHtml}

{adjustmentsHtml}

<div style='background: #fffbeb; border: 1px solid #fde68a; border-radius: 12px; padding: 16px; margin-top: 20px;'>
  <h3 style='color:#92400e; margin-top:0;'>📝 AI Obrazloženje</h3>
  <p style='font-size:14px; line-height:1.6; white-space:pre-line;'>{r.ValuationReasoning}</p>
</div>

<hr style='border:none; border-top:1px solid #e5e7eb; margin:24px 0;'>
<p style='font-size:12px; color:#9ca3af; text-align:center;'>
  Generirano automatski putem AI Procjenitelja · {DateTime.Now:dd.MM.yyyy. HH:mm}
  <br>Ovo nije službena procjena i ne zamjenjuje rad ovlaštenog procjenitelja.
</p>

</body>
</html>";
    }

    private static string FormatEur(double value) =>
        value.ToString("N0", new CultureInfo("hr-HR"));

    private static string FormatEur(int value) =>
        value.ToString("N0", new CultureInfo("hr-HR"));

    private static string TruncateText(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
}
