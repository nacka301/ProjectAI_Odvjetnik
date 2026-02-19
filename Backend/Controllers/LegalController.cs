using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// V33: Legal Intelligence Controller — Court Practice &amp; Strategy Analysis.
/// 
/// Endpoints:
/// - GET  /api/legal/feed          — Legal Intelligence Feed (dashboard widget)
/// - POST /api/legal/strategy      — "Provjeri ishod na sudu" (killer feature)
/// - POST /api/legal/precedents    — Precedent sidebar search
/// - POST /api/legal/strategy/stream — SSE streaming version of strategy analysis
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LegalController : ControllerBase
{
    private readonly LegalIntelligenceService _legalService;
    private readonly PdfService _pdfService;
    private readonly SupabaseService _supabaseService;
    private readonly ILogger<LegalController> _logger;

    public LegalController(
        LegalIntelligenceService legalService,
        PdfService pdfService,
        SupabaseService supabaseService,
        ILogger<LegalController> logger)
    {
        _legalService = legalService;
        _pdfService = pdfService;
        _supabaseService = supabaseService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    //  1. LEGAL FEED — Dashboard widget
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/legal/feed — Get recent relevant court decisions for the dashboard.
    /// </summary>
    [HttpGet("feed")]
    [EnableRateLimiting("general")]
    public async Task<ActionResult<List<LegalFeedItem>>> GetLegalFeed([FromQuery] int count = 5)
    {
        if (count < 1 || count > 10)
            count = 5;

        _logger.LogInformation("Legal feed requested: {Count} items", count);

        var items = await _legalService.GetLegalFeedAsync(count);
        return Ok(items);
    }

    // ═══════════════════════════════════════════════════════════
    //  2. LEGAL STRATEGY — "Provjeri ishod na sudu"
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/legal/strategy — Full legal strategy analysis with court outcome prediction.
    /// Accepts case context from text fields or combination of prior analyses.
    /// </summary>
    [HttpPost("strategy")]
    [EnableRateLimiting("valuation")]
    public async Task<ActionResult<LegalStrategyResult>> AnalyzeStrategy([FromBody] LegalStrategyRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Molimo pošaljite podatke o predmetu." });

        bool hasContent = !string.IsNullOrWhiteSpace(request.CaseDescription)
            || !string.IsNullOrWhiteSpace(request.ContractText)
            || !string.IsNullOrWhiteSpace(request.ChronologySummary)
            || !string.IsNullOrWhiteSpace(request.ZkSummary);

        if (!hasContent)
            return BadRequest(new { error = "Molimo unesite barem opis slučaja ili tekst ugovora." });

        // Input length validation — prevent token abuse
        const int MaxFieldLen = 100_000;
        if ((request.CaseDescription?.Length ?? 0) > MaxFieldLen
            || (request.ContractText?.Length ?? 0) > MaxFieldLen
            || (request.ChronologySummary?.Length ?? 0) > MaxFieldLen
            || (request.ZkSummary?.Length ?? 0) > MaxFieldLen)
            return BadRequest(new { error = $"Tekst je predugačak (max {MaxFieldLen:N0} znakova po polju)." });

        _logger.LogInformation(
            "\n╔══════════════════════════════════════════════════════════════╗\n" +
            "║          ⚖️  PROVJERA ISHODA NA SUDU                        ║\n" +
            "║  Pozicija: {Position}                                       ║\n" +
            "╚══════════════════════════════════════════════════════════════╝",
            request.ClientPosition);

        var result = await _legalService.AnalyzeLegalStrategyAsync(request);

        // Save to Supabase (inline await — reliable audit trail)
        try
        {
            await SaveStrategyAsync(result, request.ClientPosition);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase save failed for legal strategy");
        }

        return Ok(result);
    }

    /// <summary>Helper: persist strategy result to Supabase + audit log.</summary>
    private async Task SaveStrategyAsync(LegalStrategyResult result, string clientPosition)
    {
        var id = await _supabaseService.SaveLegalStrategyAsync(new LegalStrategyRecord
        {
            KeyLegalIssue = result.KeyLegalIssue,
            ClientPosition = clientPosition,
            WinProbability = result.WinProbability,
            WinProbabilityMin = result.WinProbabilityMin,
            WinProbabilityMax = result.WinProbabilityMax,
            PrecedentsCount = result.Precedents.Count,
            RawJsonOutput = result
        });

        // Audit log — one entry per analysis step
        var auditLogs = new List<AuditLogRecord>
        {
            new()
            {
                Module = "legal_strategy",
                AnalysisId = id,
                Step = 1,
                Action = $"Pravna strategija: {result.KeyLegalIssue}",
                LegalBasis = "Zakon o parničnom postupku (NN 53/91, 25/13)",
                Citation = "čl. 2., čl. 7., čl. 219. ZPP",
                Reasoning = $"Pozicija: {clientPosition}. Vjerojatnost uspjeha: {result.WinProbability}% ({result.WinProbabilityMin}-{result.WinProbabilityMax}%). Presedana: {result.Precedents.Count}.",
                Confidence = result.WinProbability >= 60 ? "VISOKO" : result.WinProbability >= 35 ? "SREDNJE" : "NISKO",
                ModelUsed = "deepseek/deepseek-v3.2",
                ModelTemperature = 0.10
            }
        };

        // Add one audit entry per cited precedent
        int step = 2;
        foreach (var p in result.Precedents.Take(5))
        {
            auditLogs.Add(new AuditLogRecord
            {
                Module = "legal_strategy",
                AnalysisId = id,
                Step = step++,
                Action = $"Presuda: {p.CaseNumber}",
                LegalBasis = p.Court ?? "",
                Citation = p.CaseNumber ?? "",
                Reasoning = p.RelevanceToCase ?? p.Summary ?? "",
                Confidence = "SREDNJE",
                ModelUsed = "deepseek/deepseek-v3.2",
                ModelTemperature = 0.10
            });
        }

        await _supabaseService.SaveAuditLogBatchAsync(auditLogs);
    }

    /// <summary>
    /// POST /api/legal/strategy/stream — SSE streaming version of legal strategy analysis.
    /// Reports progress stages: preparing → analyzing → precedents → strategy → done.
    /// </summary>
    [HttpPost("strategy/stream")]
    [EnableRateLimiting("valuation")]
    public async Task AnalyzeStrategyStream([FromBody] LegalStrategyRequest request)
    {
        SseHelpers.SetupSSEHeaders(Response);
        var ct = HttpContext.RequestAborted;

        if (request == null)
        {
            await SseHelpers.SendError(Response, "validation", "Molimo pošaljite podatke o predmetu.");
            return;
        }

        bool hasContent = !string.IsNullOrWhiteSpace(request.CaseDescription)
            || !string.IsNullOrWhiteSpace(request.ContractText)
            || !string.IsNullOrWhiteSpace(request.ChronologySummary)
            || !string.IsNullOrWhiteSpace(request.ZkSummary);

        if (!hasContent)
        {
            await SseHelpers.SendError(Response, "validation", "Molimo unesite barem opis slučaja ili tekst ugovora.");
            return;
        }

        _logger.LogInformation(
            "\n╔══════════════════════════════════════════════════════════════╗\n" +
            "║          ⚖️  PROVJERA ISHODA NA SUDU (SSE)                  ║\n" +
            "║  Pozicija: {Position}                                       ║\n" +
            "╚══════════════════════════════════════════════════════════════╝",
            request.ClientPosition);

        try
        {
            // Stage 1: Preparing
            await SseHelpers.SendProgress(Response, "preparing", "active",
                "Pripremam kontekst predmeta...", "Analiza unesenih podataka");
            ct.ThrowIfCancellationRequested();

            await Task.Delay(300, ct); // Brief delay for UX

            await SseHelpers.SendProgress(Response, "preparing", "done",
                "Kontekst pripremljen", "Podaci učitani");

            // Stage 2: Analyzing
            await SseHelpers.SendProgress(Response, "analyzing", "active",
                "AI analizira pravna pitanja...", "Identificiranje ključnog spora");
            ct.ThrowIfCancellationRequested();

            // Stage 3: Get the actual result
            await SseHelpers.SendProgress(Response, "precedents", "active",
                "Pretražujem sudsku praksu...", "VSRH, VTS, županijski sudovi");

            var result = await _legalService.AnalyzeLegalStrategyAsync(request);

            await SseHelpers.SendProgress(Response, "precedents", "done",
                $"Pronađeno {result.Precedents.Count} relevantnih presuda",
                "Praksa analizirana");

            // Stage 4: Strategy
            await SseHelpers.SendProgress(Response, "strategy", "active",
                "Formuliram strategiju...",
                $"Vjerojatnost uspjeha: {result.WinProbability}%");

            await Task.Delay(200, ct);

            await SseHelpers.SendProgress(Response, "strategy", "done",
                "Strategija formulirana", result.WinProbabilityLabel);

            // Final result
            await SseHelpers.SendSSE(Response, "result", result);

            // Save to Supabase (inline await — reliable audit trail)
            try
            {
                await SaveStrategyAsync(result, request.ClientPosition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase save failed for legal strategy (SSE)");
            }

            await SseHelpers.SendSSE(Response, "done", new
            {
                win_probability = result.WinProbability,
                precedents_found = result.Precedents.Count,
                risks_identified = result.Risks.Count
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected during legal strategy analysis");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legal strategy stream failed");
            if (!HttpContext.RequestAborted.IsCancellationRequested)
                await SseHelpers.SendError(Response, "analysis", "Analiza nije uspjela. Pokušajte ponovno.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  3. PRECEDENT SIDEBAR — Quick precedent search
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/legal/precedents — Find relevant court precedents for given context.
    /// </summary>
    [HttpPost("precedents")]
    [EnableRateLimiting("general")]
    public async Task<ActionResult> FindPrecedents([FromBody] PrecedentSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Context))
            return BadRequest(new { error = "Molimo unesite pravni kontekst za pretragu." });

        if (request.Context.Length < 20)
            return BadRequest(new { error = "Kontekst je prekratak (min. 20 znakova)." });

        _logger.LogInformation("Precedent search: {Len} chars", request.Context.Length);

        var (legalIssue, precedents) = await _legalService.GetRelevantPrecedentsAsync(request.Context);

        return Ok(new
        {
            legal_issue = legalIssue,
            precedents
        });
    }

    /// <summary>
    /// POST /api/legal/strategy-pdf — Upload case documents (PDFs) for strategy analysis.
    /// </summary>
    [HttpPost("strategy-pdf")]
    [EnableRateLimiting("valuation")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB max for multiple files
    public async Task<ActionResult<LegalStrategyResult>> AnalyzeStrategyFromPdfs(
        [FromForm] List<IFormFile> files,
        [FromForm] string? clientPosition,
        [FromForm] string? desiredOutcome,
        [FromForm] string? additionalContext)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "Molimo uploadajte barem jednu datoteku." });

        var pdfFiles = files.Where(f =>
            f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && f.Length > 0).ToList();

        if (pdfFiles.Count == 0)
            return BadRequest(new { error = "Nijedna PDF datoteka nije pronađena." });

        if (pdfFiles.Count > 10)
            return BadRequest(new { error = "Maksimalno 10 PDF datoteka po zahtjevu." });

        _logger.LogInformation("Strategy PDF analysis: {Count} files", pdfFiles.Count);

        // Extract text from all PDFs
        var allText = new List<string>();
        foreach (var file in pdfFiles)
        {
            try
            {
                var text = _pdfService.ExtractText(file);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                    allText.Add($"--- {file.FileName} ---\n{text}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read PDF '{File}'", file.FileName);
            }
        }

        if (allText.Count == 0)
            return BadRequest(new { error = "Nije moguće pročitati tekst iz uploadanih PDF-ova." });

        var combinedText = string.Join("\n\n", allText);

        var request = new LegalStrategyRequest
        {
            CaseDescription = combinedText,
            ClientPosition = clientPosition ?? "kupac",
            DesiredOutcome = desiredOutcome ?? ""
        };

        if (!string.IsNullOrWhiteSpace(additionalContext))
            request.CaseDescription = $"{additionalContext}\n\n{combinedText}";

        var result = await _legalService.AnalyzeLegalStrategyAsync(request);

        // Save to Supabase (inline await — reliable audit trail)
        try
        {
            await SaveStrategyAsync(result, request.ClientPosition);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase save failed for legal strategy (PDF)");
        }

        return Ok(result);
    }
}

/// <summary>
/// Request model for precedent sidebar search.
/// </summary>
public class PrecedentSearchRequest
{
    /// <summary>Legal context to search precedents for</summary>
    public string Context { get; set; } = "";
}
