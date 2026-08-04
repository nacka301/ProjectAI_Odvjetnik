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
:ITroskovnikDocumentService {
    private readonly ILogger<TroskovnikDocumentService> _logger;
    private readonly CultureInfo _hr = new("hr-HR");

    public TroskovnikDocumentService(ILogger<TroskovnikDocumentService> logger) => _logger = logger;

    private string FormatCurrency(decimal value) => value.ToString("N2", _hr);
    private string FormatThreshold(decimal value) => value.ToString("N2", _hr);

    /// <summary>
    /// Bodovi su decimalni (Tarifa ne propisuje zaokruživanje), ali cijeli broj bodova
    /// ne treba pisati s decimalama: 100 ostaje "100", 46,875 ostaje "46,875".
    /// </summary>
    private string FormatBodovi(decimal value) => value.ToString("0.###", _hr);

    /// <summary>Isto zaokruživanje kao u motoru: novac, dvije decimale, od nule.</summary>
    private static decimal ZaokruziNovac(decimal iznos)
        => Math.Round(iznos, 2, MidpointRounding.AwayFromZero);

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

        // Stjecaj — obrazloženje odabranog razreda (Tbr. 4. toč. 1 al. 6)
        if (!string.IsNullOrWhiteSpace(resp.NapomenaStjecaj))
        {
            sb.AppendLine($"<p style=\"margin-top:12px;font-size:11px;color:#555\"><em>{System.Net.WebUtility.HtmlEncode(resp.NapomenaStjecaj)}</em></p>");
        }

        // Line items (if any)
        sb.AppendLine("<h3>Stavke</h3>");
        sb.AppendLine("<table><tr><th>#</th><th>Opis</th><th>Tarifni broj</th><th>Pravni izvor</th><th>Količina</th><th>Bod/jed</th><th>Ukupno bodova</th><th>Iznos (EUR)</th></tr>");

        foreach (var l in resp.Stavke ?? new List<TroskovnikLineItem>())
        {
            var izvor = string.IsNullOrWhiteSpace(l.PravniIzvor) ? l.TarifniBroj : l.PravniIzvor;

            // Tbr. 41 — uz prilagođenu stavku ide postotak i citat odredbe, da se svaki
            // odmak od tarifne stavke može provjeriti u izvoru.
            var tbr41 = "";
            if (!string.IsNullOrWhiteSpace(l.Tbr41PravniIzvor))
            {
                var predznak = l.Tbr41Bodovi >= 0 ? "+" : "−";
                var postotak = l.Tbr41PovisenjePostotak > 0 ? l.Tbr41PovisenjePostotak : l.Tbr41SnizenjePostotak;

                tbr41 = $"<br/><span style=\"font-size:10px;color:#555\">Tbr. 41: {predznak}{FormatBodovi(postotak)} % " +
                        $"({predznak}{FormatBodovi(Math.Abs(l.Tbr41Bodovi))} bod.) — " +
                        $"{System.Net.WebUtility.HtmlEncode(l.Tbr41PravniIzvor)}</span>";
            }

            var ukupnoSTbr41 = l.UkupnoBodova + l.Tbr41Bodovi;
            var iznosSTbr41 = ZaokruziNovac(ukupnoSTbr41 * TarifaConstants.BodValue);

            sb.AppendLine($"<tr><td>{l.Rbr}</td><td>{System.Net.WebUtility.HtmlEncode(l.Opis)}{tbr41}</td><td>{System.Net.WebUtility.HtmlEncode(l.TarifniBroj)}</td><td style=\"font-size:10px;color:#555\">{System.Net.WebUtility.HtmlEncode(izvor)}</td><td>{l.Kolicina}</td><td>{FormatBodovi(l.BodovaPoJedinici)}</td><td>{FormatBodovi(ukupnoSTbr41)}</td><td style=\"text-align:right\">{FormatCurrency(iznosSTbr41)}</td></tr>");
        }

        // Tbr. 40 — povišenje za zastupanje više osoba, kao vlastiti redak.
        if (resp.PovisenjeViseOsobaPostotak > 0)
        {
            var povisenjeIzvor = $"{TarifaConstants.NazivTarife}, Tbr. 40. toč. 1 — 10% za drugu i svaku sljedeću osobu, najviše 50%";
            sb.AppendLine($"<tr><td></td><td>Povišenje za zastupanje {resp.BrojZastupanihOsoba} osoba</td><td>Tbr. 40</td><td style=\"font-size:10px;color:#555\">{System.Net.WebUtility.HtmlEncode(povisenjeIzvor)}</td><td></td><td>{resp.PovisenjeViseOsobaPostotak:P0}</td><td>{FormatBodovi(resp.PovisenjeViseOsobaBodovi)}</td><td style=\"text-align:right\">{FormatCurrency(resp.PovisenjeViseOsobaBodovi * TarifaConstants.BodValue)}</td></tr>");
        }

        sb.AppendLine("</table>");

        // Totals
        sb.AppendLine("<h3>Ukupno</h3>");
        sb.AppendLine("<table style=\"width:320px;float:right;\">");

        if (includeVat)
        {
            sb.AppendLine($"<tr><td>Osnovica (bez PDV)</td><td style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurBezPdv)}</td></tr>");
            sb.AppendLine($"<tr><td>PDV (25%)</td><td style=\"text-align:right\">{FormatCurrency(resp.Pdv)}</td></tr>");
            sb.AppendLine($"<tr><th>Odvjetnički trošak s PDV-om</th><th style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurSPdv)}</th></tr>");
        }
        else
        {
            sb.AppendLine($"<tr><th>Odvjetnički trošak</th><th style=\"text-align:right\">{FormatCurrency(resp.UkupnoEurBezPdv)}</th></tr>");
        }

        // Sudska pristojba — s razlogom kad je nema, da "0,00 €" ne izgleda kao greška.
        var pristojbaObrazlozenje = resp.PristojbaOsnova switch
        {
            PristojbaOsnova.Nema =>
                $"{TarifaConstants.NazivUredbeOPristojbama} ne propisuje pristojbu za kazneni postupak (Poglavlje I. — parnični i ovršni postupak)",
            PristojbaOsnova.Polovica =>
                $"{TarifaConstants.NazivUredbeOPristojbama}, Tar. br. 1 st. 2 — polovica iznosa iz st. 1",
            _ =>
                $"{TarifaConstants.NazivUredbeOPristojbama}, Tar. br. 1 st. 1"
        };

        sb.AppendLine($"<tr><td>Sudska pristojba{(resp.IsOslobodjenPristojbi ? " (oslobođeno)" : "")}<br/><span style=\"font-size:10px;color:#555\">{System.Net.WebUtility.HtmlEncode(pristojbaObrazlozenje)}</span></td><td style=\"text-align:right\">{FormatCurrency(resp.SudskaPristojba)}</td></tr>");
        sb.AppendLine($"<tr><th>SVEUKUPNI TROŠAK</th><th style=\"text-align:right\">{FormatCurrency(resp.SveukupniTrosak)}</th></tr>");

        // Tbr. 40. toč. 1, drugi odlomak — "Iznos povišene nagrade dijeli se na jednake
        // dijelove prema broju branjenika, odnosno zastupanih osoba."
        if (resp.BrojZastupanihOsoba > 1)
        {
            sb.AppendLine($"<tr><td>Nagrada po osobi ({resp.BrojZastupanihOsoba} branjenika)<br/><span style=\"font-size:10px;color:#555\">Tbr. 40. toč. 1 — dioba na jednake dijelove; {FormatBodovi(resp.BodoviPoOsobi)} bodova, bez PDV-a i sudske pristojbe</span></td><td style=\"text-align:right\">{FormatCurrency(resp.NagradaPoOsobi)}</td></tr>");
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
    /// <summary>
    /// Tablica iz Tbr. 7. toč. 1 onako kako stoji u Tarifi — s decimalama i bez
    /// preklapanja granica: 0,00–332,00 pa 332,01–664,00.
    ///
    /// Progresivni razredi nemaju jedan broj nego formulu, pa se ispisuju tekstom
    /// kojim ih Tarifa opisuje ("1 boda za svakih započetih 133,00 eura").
    /// </summary>
    private static readonly (decimal Od, decimal? Do, string Bodovi)[] Tbr71Razredi =
    [
        (            0m,           332.00m, "25"),
        (       332.01m,           664.00m, "50"),
        (       664.01m,         1_327.00m, "75"),
        (     1_327.01m,        13_272.00m, "100"),
        (    13_272.01m,        33_181.00m, "250"),
        (    33_181.01m,        66_361.00m, "500"),
        (    66_361.01m,       663_614.00m, "500 + 1 bod / započetih 133,00 €"),
        (   663_614.01m,     1_327_228.00m, "4.991 + 1 bod / započetih 265,00 €"),
        ( 1_327_228.01m,              null, "7.496 + 1 bod / započetih 664,00 €, najviše 10.000")
    ];

    private void AppendVpsRangeTable(StringBuilder sb)
    {
        sb.AppendLine("<h3 style=\"margin-top:24px;\">Tbr. 7. toč. 1 — tablica bodova prema vrijednosti predmeta spora</h3>");
        sb.AppendLine("<table style=\"width:480px;\">");
        sb.AppendLine("<tr><th>Vrijednost predmeta spora (EUR)</th><th style=\"text-align:right\">Bodovi</th></tr>");

        foreach (var (od, doIznos, bodovi) in Tbr71Razredi)
        {
            var raspon = doIznos is null
                ? $"iznad {FormatThreshold(od)}"
                : $"{FormatThreshold(od)} – {FormatThreshold(doIznos.Value)}";

            sb.AppendLine($"<tr><td>{raspon}</td><td style=\"text-align:right\">{bodovi}</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    /// <summary>Razredi za prikaz — izloženo radi provjere u testovima.</summary>
    public static IReadOnlyList<(decimal Od, decimal? Do, string Bodovi)> RazrediZaPrikaz => Tbr71Razredi;
}
