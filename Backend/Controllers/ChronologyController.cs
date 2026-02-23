using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Case Chronology endpoint — "Kronologija Spisa".
/// Upload multiple PDFs → extract events from each → merge into sorted timeline.
/// Uses SSE streaming to report per-file progress.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChronologyController : ControllerBase
{
    private readonly ChronologyService _chronologyService;
    private readonly PdfService _pdfService;
    private readonly SupabaseService _supabaseService;
    private readonly ChatClientProvider _clients;
    private readonly CreditService _creditService;
    private readonly ILogger<ChronologyController> _logger;

    public ChronologyController(
        ChronologyService chronologyService,
        PdfService pdfService,
        SupabaseService supabaseService,
        ChatClientProvider clients,
        CreditService creditService,
        ILogger<ChronologyController> logger)
    {
        _chronologyService = chronologyService;
        _pdfService = pdfService;
        _supabaseService = supabaseService;
        _clients = clients;
        _creditService = creditService;
        _logger = logger;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    private Task SendSSE(string type, object? payload)
        => SseHelpers.SendSSE(Response, type, payload);

    private Task SendProgress(string stage, string status, string label, string? detail = null)
        => SseHelpers.SendProgress(Response, stage, status, label, detail);

    /// <summary>
    /// POST /api/chronology/stream — Upload multiple PDFs, get SSE progress + chronological timeline.
    /// </summary>
    [HttpPost("stream")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total (multiple files)
    [EnableRateLimiting("valuation")]
    public async Task GenerateChronologyStream(List<IFormFile> files)
    {
        // Credit enforcement — check before SSE headers
        var creditCheck = await _creditService.ConsumeCreditAsync(GetUserId() ?? "", "chronology");
        if (!creditCheck.Allowed)
        {
            Response.StatusCode = 403;
            Response.ContentType = "application/json";
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = creditCheck.Message }));
            return;
        }

        SseHelpers.SetupSSEHeaders(Response);
        var ct = HttpContext.RequestAborted;

        if (files == null || files.Count == 0)
        {
            await SseHelpers.SendError(Response, "validation", "Niste uploadali nijednu datoteku.");
            return;
        }

        // Filter to PDFs only
        var pdfFiles = files.Where(f =>
            f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && f.Length > 0).ToList();

        if (pdfFiles.Count == 0)
        {
            await SseHelpers.SendError(Response, "validation", "Nijedna PDF datoteka nije pronađena.");
            return;
        }

        if (pdfFiles.Count > 50)
        {
            await SseHelpers.SendError(Response, "validation", "Maksimalno 50 PDF datoteka po zahtjevu.");
            return;
        }

        _logger.LogInformation("\n" +
            "╔══════════════════════════════════════════════════════════════╗\n" +
            "║          📜  KRONOLOGIJA SPISA                              ║\n" +
            "║  Datoteke: {Count}                                          ║\n" +
            "╚══════════════════════════════════════════════════════════════╝",
            pdfFiles.Count);

        await SendProgress("upload", "done", "Datoteke primljene",
            $"{pdfFiles.Count} PDF datoteka");

        var allEvents = new List<TimelineEvent>();
        var errors = new List<string>();
        int processed = 0;

        // Process each file sequentially (chunking strategy)
        foreach (var file in pdfFiles)
        {
            ct.ThrowIfCancellationRequested();
            processed++;

            string shortName = file.FileName.Length > 40
                ? file.FileName[..37] + "..."
                : file.FileName;

            await SendProgress("processing", "active",
                $"Obrađujem '{shortName}'... ({processed}/{pdfFiles.Count})",
                $"{file.Length / 1024:N0} KB");

            try
            {
                // 1. Extract text from PDF
                string pdfText;
                try
                {
                    pdfText = _pdfService.ExtractText(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read PDF '{File}'", file.FileName);
                    errors.Add($"{file.FileName}: Nije moguće pročitati PDF — {ex.Message}");
                    await SendProgress("processing", "skipped",
                        $"'{shortName}' — preskočen", "Nije moguće pročitati PDF");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(pdfText) || pdfText.Length < 20)
                {
                    errors.Add($"{file.FileName}: PDF ne sadrži tekst (možda skeniran bez OCR-a).");
                    await SendProgress("processing", "skipped",
                        $"'{shortName}' — preskočen", "Nema teksta (OCR?)");
                    continue;
                }

                // 2. AI extraction
                var events = await _chronologyService.ExtractEventsAsync(file.FileName, pdfText);
                allEvents.AddRange(events);

                await SendProgress("processing", "done",
                    $"'{shortName}' — {events.Count} događaja",
                    $"({processed}/{pdfFiles.Count})");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Client disconnected during chronology generation");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing '{File}' for chronology", file.FileName);
                errors.Add($"{file.FileName}: {ex.Message}");
                await SendProgress("processing", "skipped",
                    $"'{shortName}' — greška", ex.Message);
            }
        }

        // 3. Sort all events chronologically
        allEvents.Sort((a, b) =>
        {
            int cmp = string.Compare(a.DateIso, b.DateIso, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            // Secondary sort by time
            return string.Compare(a.Time ?? "", b.Time ?? "", StringComparison.Ordinal);
        });

        await SendProgress("sorting", "done", "Kronologija sortirana",
            $"{allEvents.Count} događaja iz {processed} dokumenata");

        // 4. Send final result
        var result = new ChronologyResult
        {
            Events = allEvents,
            TotalDocumentsProcessed = processed,
            TotalEventsExtracted = allEvents.Count,
            Errors = errors
        };

        _logger.LogInformation("Chronology complete: {Docs} docs → {Events} events, {Errors} errors",
            processed, allEvents.Count, errors.Count);

        // Save to Supabase (background — safe fire-and-forget)
        string dateRange = "";
        if (allEvents.Count > 0)
        {
            var first = allEvents.First().DisplayDate;
            var last = allEvents.Last().DisplayDate;
            dateRange = first == last ? first : $"{first} — {last}";
        }
        // Save to Supabase (inline await — reliable audit trail)
        try
        {
            var id = await _supabaseService.SaveChronologyAsync(new ChronologyRecord
            {
                UserId = GetUserId(),
                TotalDocuments = processed,
                TotalEvents = allEvents.Count,
                DateRange = dateRange,
                RawJsonOutput = result
            });
            await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
            {
                UserId = GetUserId(),
                Module = "chronology",
                AnalysisId = id,
                Step = 1,
                Action = $"Kronologija generirana: {processed} dokumenata, {allEvents.Count} događaja, raspon: {dateRange}",
                LegalBasis = "Zakon o parničnom postupku (NN 53/91, 25/13)",
                Citation = "čl. 219., čl. 223. ZPP",
                Reasoning = $"AI je obradio {processed} PDF dokumenata i ekstrahirao {allEvents.Count} kronoloških događaja. Greške: {errors.Count}.",
                Confidence = errors.Count == 0 ? "VISOKO" : "SREDNJE",
                ModelUsed = _clients.FastModel,
                ModelTemperature = 0.10
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase save failed for chronology");
        }

        await SendSSE("result", result);
    }
}
