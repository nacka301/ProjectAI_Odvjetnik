using System.Globalization;
using System.Text;
using System.Text.Json;
using Backend.Models;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

/// <summary>
/// Generates a printable representation (HTML) of a saved troškovnik.
/// Maps the new nested fields (`Odvjetnik`, `Predmet`, `Stranke`) and
/// formats numbers using the Croatian locale (comma decimal, two decimals).
/// The service produces HTML which can be converted to PDF/Word by external tools.
/// </summary>
public sealed class TroskovnikDocumentService
{
    private readonly ILogger<TroskovnikDocumentService> _logger;
    private readonly CultureInfo _hr = new("hr-HR");

    public TroskovnikDocumentService(ILogger<TroskovnikDocumentService> logger) => _logger = logger;

    private string FormatCurrency(decimal value) => value.ToString("N2", _hr);
    private string FormatThreshold(decimal value) => value.ToString("N2", _hr);

    /// <summary>
    /// Render HTML for a troškovnik from a read record. If the record contains
    /// the full response JSON in `RawJsonOutput`, the method will attempt to
    /// deserialize it and include line items; otherwise a minimal document is returned.
    /// </summary>
    public string RenderHtml(TroskovnikReadRecord record)
    {
        TroskovnikResponse? resp = null;

        if (record.RawJsonOutput is TroskovnikResponse r)
        {
            resp = r;
        }
        else if (record.RawJsonOutput != null)
        {
            try
            {
                // RawJsonOutput may be a JsonElement or already-serialized object
                var s = JsonSerializer.Serialize(record.RawJsonOutput);
                resp = JsonSerializer.Deserialize<TroskovnikResponse>(s);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to deserialize RawJsonOutput to TroskovnikResponse");
            }
        }

        // If we couldn't recover a full response, build a minimal one from the record
        if (resp == null)
        {
            resp = new TroskovnikResponse
            {
                Stavke = new List<TroskovnikLineItem>(),
                UkupnoBodova = record.UkupnoBodova,
                UkupnoEurBezPdv = record.UkupnoEurBezPdv,
                Pdv = record.Pdv,
                UkupnoEurSPdv = record.UkupnoEurSPdv,
                HasVat = record.HasVat,
                IsPdvUkljucen = record.IsPdvUkljucen,
                Klijent = null,
                Odvjetnik = null,
                Protustranka = null,
                Sud = record.Predmet?.Sud,
                BrojPredmeta = record.Predmet?.PoslovniBroj
            };
        }

        // Prefer values from the persisted nested objects if available
        var odvjetnikNaziv = resp.Odvjetnik?.Ime ?? record.Odvjetnik?.Naziv ?? "";
        var odvjetnikOib = resp.Odvjetnik?.Oib ?? record.Odvjetnik?.Oib ?? "";
        var odvjetnikAdresa = resp.Odvjetnik?.Adresa ?? record.Odvjetnik?.Adresa ?? "";
        var odvjetnikIban = record.Odvjetnik?.Iban ?? string.Empty;

        var predmetSud = resp.Sud ?? record.Predmet?.Sud ?? string.Empty;
        var predmetBroj = resp.BrojPredmeta ?? record.Predmet?.PoslovniBroj ?? record.CaseId ?? string.Empty;

        // Zastupana (the represented party) — prefer Klijent in response, else stored stranke
        var zastNaziv = resp.Klijent?.Ime ?? record.Stranke?.ZastupanaStranka?.Naziv ?? string.Empty;
        var zastOib = resp.Klijent?.Oib ?? record.Stranke?.ZastupanaStranka?.Oib ?? string.Empty;
        var zastAdresa = resp.Klijent?.Adresa ?? record.Stranke?.ZastupanaStranka?.Adresa ?? string.Empty;
        var zastPolozaj = record.Stranke?.ZastupanaStranka?.Polozaj ?? "zastupana";

        // Protustranka — prefer response.protustranka when present
        var protNaziv = resp.Protustranka?.Ime ?? record.Stranke?.Protustranka?.Naziv ?? string.Empty;
        var protOib = resp.Protustranka?.Oib ?? record.Stranke?.Protustranka?.Oib ?? string.Empty;
        var protAdresa = resp.Protustranka?.Adresa ?? record.Stranke?.Protustranka?.Adresa ?? string.Empty;
        // Prefer explicit polozaj from persisted nested object, then response-level protustranka_polozaj
        var protPolozaj = record.Stranke?.Protustranka?.Polozaj ?? resp.ProtustrankaPolozaj ?? string.Empty;

        // Prefer the new IsPdvUkljucen flag; fall back to legacy HasVat; finally check persisted record
        var includeVat = resp.IsPdvUkljucen ?? (resp.HasVat || record.IsPdvUkljucen || record.HasVat);

        var sb = new StringBuilder();

        sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>body{font-family:Segoe UI,Arial,sans-serif;font-size:12px}table{border-collapse:collapse;width:100%}th,td{padding:6px;border:1px solid #ddd;text-align:left}th{background:#f3f3f3}</style></head><body>");

        // Header
        sb.AppendLine("<h2>Troškovnik</h2>");
        sb.AppendLine("<table><tr><td style=\"width:60%\">");
        sb.AppendLine($"<strong>Odvjetnik:</strong><br/>{System.Net.WebUtility.HtmlEncode(odvjetnikNaziv)}<br/>{System.Net.WebUtility.HtmlEncode(odvjetnikAdresa)}<br/>OIB: {System.Net.WebUtility.HtmlEncode(odvjetnikOib)}<br/>IBAN: {System.Net.WebUtility.HtmlEncode(odvjetnikIban)}");
        sb.AppendLine("</td><td>");
        sb.AppendLine($"<strong>Predmet:</strong><br/>Sud: {System.Net.WebUtility.HtmlEncode(predmetSud)}<br/>Broj predmeta: {System.Net.WebUtility.HtmlEncode(predmetBroj)}");
        sb.AppendLine("</td></tr></table>");

        // Parties block
        sb.AppendLine("<h3>Stranke</h3>");
        sb.AppendLine("<table><tr><th>Položaj</th><th>Naziv</th><th>OIB</th><th>Adresa</th></tr>");
        sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(zastPolozaj)}</td><td>{System.Net.WebUtility.HtmlEncode(zastNaziv)}</td><td>{System.Net.WebUtility.HtmlEncode(zastOib)}</td><td>{System.Net.WebUtility.HtmlEncode(zastAdresa)}</td></tr>");
        sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(protPolozaj)}</td><td>{System.Net.WebUtility.HtmlEncode(protNaziv)}</td><td>{System.Net.WebUtility.HtmlEncode(protOib)}</td><td>{System.Net.WebUtility.HtmlEncode(protAdresa)}</td></tr>");
        sb.AppendLine("</table>");

        // Line items (if any)
        sb.AppendLine("<h3>Stavke</h3>");
        sb.AppendLine("<table><tr><th>#</th><th>Opis</th><th>Tarifni broj</th><th>Količina</th><th>Bod/jed</th><th>Ukupno bodova</th><th>Iznos (EUR)</th></tr>");

        foreach (var l in resp.Stavke ?? new List<TroskovnikLineItem>())
        {
            sb.AppendLine($"<tr><td>{l.Rbr}</td><td>{System.Net.WebUtility.HtmlEncode(l.Opis)}</td><td>{System.Net.WebUtility.HtmlEncode(l.TarifniBroj)}</td><td>{l.Kolicina}</td><td>{l.BodovaPoJedinici}</td><td>{l.UkupnoBodova}</td><td style=\"text-align:right\">{FormatCurrency(l.IznosEur)}</td></tr>");
        }

        sb.AppendLine("</table>");

        // Totals
        sb.AppendLine("<h3>Ukupno</h3>");
        sb.AppendLine("<table style=\"width:320px;float:right;\">");

        if (includeVat)
        {
            sb.AppendLine($"<tr><td>Osnovica (bez PDV)</td><td style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurBezPdv)}</td></tr>");
            sb.AppendLine($"<tr><td>PDV (25%)</td><td style=\"text-align:right\">{FormatCurrency(resp.Pdv)}</td></tr>");
            sb.AppendLine($"<tr><th>SVEUKUPNO s PDV-om</th><th style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurSPdv)}</th></tr>");
        }
        else
        {
            sb.AppendLine($"<tr><th>UKUPNO</th><th style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurBezPdv)}</th></tr>");
        }

        sb.AppendLine("</table>");

        sb.AppendLine("<div style=\"clear:both;\"></div>");

        // Tbr. 7.1 — VPS range reference table
        AppendVpsRangeTable(sb);

        sb.AppendLine($"<p style=\"margin-top:32px;\">Datum izrade: {resp.DatumIzrade.ToString("g", _hr)}</p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    /// <summary>
    /// Appends a Tbr. 7.1 quick-reference table with VPS ranges formatted
    /// using FormatThreshold (hr-HR, exactly 2 decimal places).
    /// </summary>
    private void AppendVpsRangeTable(StringBuilder sb)
    {
        sb.AppendLine("<h3 style=\"margin-top:24px;\">Tbr. 7.1 — Tablica bodova (VPS rasponi)</h3>");
        sb.AppendLine("<table style=\"width:420px;\">");
        sb.AppendLine("<tr><th>Vrijednost predmeta spora (EUR)</th><th style=\"text-align:right\">Bodovi</th></tr>");

        var bounds = new[] { 0m, 332m, 664m, 1_327m, 13_272m, 33_181m, 66_361m, 663_614m, 1_327_228m };
        var points = new[] { "25", "50", "75", "100", "250", "500", "500+", "4.991+" };

        for (int i = 0; i < bounds.Length - 1; i++)
        {
            sb.AppendLine($"<tr><td>{FormatThreshold(bounds[i])} – {FormatThreshold(bounds[i + 1])}</td><td style=\"text-align:right\">{points[i]}</td></tr>");
        }

        sb.AppendLine($"<tr><td>&gt; {FormatThreshold(bounds[^1])}</td><td style=\"text-align:right\">max 10.000</td></tr>");
        sb.AppendLine("</table>");
    }
}
