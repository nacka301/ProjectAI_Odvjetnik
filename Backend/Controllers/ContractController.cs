using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// AI Contract Analyzer endpoint.
/// Upload a PDF or paste text of a real estate contract →
/// get a structured legal risk analysis.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContractController : ControllerBase
{
    private readonly ContractAnalysisService _contractService;
    private readonly PdfService _pdfService;
    private readonly SupabaseService _supabaseService;
    private readonly ChatClientProvider _clients;
    private readonly ILogger<ContractController> _logger;

    public ContractController(
        ContractAnalysisService contractService,
        PdfService pdfService,
        SupabaseService supabaseService,
        ChatClientProvider clients,
        ILogger<ContractController> logger)
    {
        _contractService = contractService;
        _pdfService = pdfService;
        _supabaseService = supabaseService;
        _clients = clients;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/contract/analyze-pdf — Upload a PDF contract for analysis.
    /// </summary>
    [HttpPost("analyze-pdf")]
    [EnableRateLimiting("valuation")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<ActionResult<ContractAnalysisResult>> AnalyzePdf(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Molimo uploadajte PDF datoteku." });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Samo PDF datoteke su podržane." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Datoteka je prevelika (max 10 MB)." });

        _logger.LogInformation("Contract PDF upload: {Name} ({Size:N0} bytes)", file.FileName, file.Length);

        // Extract text from PDF
        string pdfText;
        try
        {
            pdfText = _pdfService.ExtractText(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from contract PDF");
            return BadRequest(new { error = "Nije moguće pročitati PDF. Provjerite da je datoteka ispravna." });
        }

        if (string.IsNullOrWhiteSpace(pdfText) || pdfText.Length < 50)
        {
            return BadRequest(new { error = "PDF ne sadrži dovoljno teksta. Možda je skeniran bez OCR-a?" });
        }

        _logger.LogInformation("Contract text extracted: {Len} chars", pdfText.Length);

        var result = await _contractService.AnalyzeContractAsync(pdfText);

        // Save to Supabase (inline await — reliable audit trail)
        try
        {
            var id = await _supabaseService.SaveContractAnalysisAsync(new ContractAnalysisRecord
            {
                DocumentType = result.DocumentType,
                Summary = result.Summary,
                OverallRisk = result.OverallRisk,
                RiskScore = result.RiskScore,
                RedFlagsCount = result.RedFlags.Count,
                RawJsonOutput = result
            });
            await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
            {
                Module = "contract_analysis",
                AnalysisId = id,
                Step = 1,
                Action = $"Ugovor analiziran: {result.DocumentType}, rizik={result.OverallRisk} ({result.RiskScore}/100), {result.RedFlags.Count} crvenih zastavica",
                LegalBasis = "Zakon o obveznim odnosima (NN 35/05, 29/18)",
                Citation = "čl. 247.–269. ZOO",
                Reasoning = result.Summary,
                Confidence = result.RiskScore >= 70 ? "VISOKO" : result.RiskScore >= 40 ? "SREDNJE" : "NISKO",
                ModelUsed = _clients.HeavyModel,
                ModelTemperature = 0.10
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase save failed for contract analysis (PDF)");
        }

        return Ok(result);
    }

    /// <summary>
    /// POST /api/contract/analyze-text — Paste contract text for analysis.
    /// </summary>
    [HttpPost("analyze-text")]
    [EnableRateLimiting("valuation")]
    public async Task<ActionResult<ContractAnalysisResult>> AnalyzeText([FromBody] ContractTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return BadRequest(new { error = "Molimo unesite tekst ugovora." });

        if (request.Text.Length < 50)
            return BadRequest(new { error = "Tekst je prekratak za smislenu analizu (min. 50 znakova)." });

        if (request.Text.Length > 50000)
            return BadRequest(new { error = "Tekst je predugačak (max 50.000 znakova)." });

        _logger.LogInformation("Contract text analysis: {Len} chars", request.Text.Length);

        var result = await _contractService.AnalyzeContractAsync(request.Text);

        // Save to Supabase (inline await — reliable audit trail)
        try
        {
            var id = await _supabaseService.SaveContractAnalysisAsync(new ContractAnalysisRecord
            {
                DocumentType = result.DocumentType,
                Summary = result.Summary,
                OverallRisk = result.OverallRisk,
                RiskScore = result.RiskScore,
                RedFlagsCount = result.RedFlags.Count,
                RawJsonOutput = result
            });
            await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
            {
                Module = "contract_analysis",
                AnalysisId = id,
                Step = 1,
                Action = $"Ugovor analiziran (tekst): {result.DocumentType}, rizik={result.OverallRisk} ({result.RiskScore}/100), {result.RedFlags.Count} crvenih zastavica",
                LegalBasis = "Zakon o obveznim odnosima (NN 35/05, 29/18)",
                Citation = "čl. 247.–269. ZOO",
                Reasoning = result.Summary,
                Confidence = result.RiskScore >= 70 ? "VISOKO" : result.RiskScore >= 40 ? "SREDNJE" : "NISKO",
                ModelUsed = _clients.HeavyModel,
                ModelTemperature = 0.10
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase save failed for contract analysis (text)");
        }

        return Ok(result);
    }

    // ══════════════════════════════════════════════════════════
    //  V57: SSE STREAMING ENDPOINTS — real-time progress
    // ══════════════════════════════════════════════════════════

    private Task SendProgress(string stage, string status, string label, string? detail = null)
        => SseHelpers.SendProgress(Response, stage, status, label, detail);

    /// <summary>
    /// POST /api/contract/analyze-pdf-stream — Upload a PDF contract for streaming analysis.
    /// Returns SSE events with progress updates, then the final result.
    /// </summary>
    [HttpPost("analyze-pdf-stream")]
    [EnableRateLimiting("valuation")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task AnalyzePdfStream(IFormFile file)
    {
        SseHelpers.SetupSSEHeaders(Response);

        if (file == null || file.Length == 0)
        {
            await SseHelpers.SendError(Response, "reading", "Molimo uploadajte PDF datoteku.");
            return;
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await SseHelpers.SendError(Response, "reading", "Samo PDF datoteke su podržane.");
            return;
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            await SseHelpers.SendError(Response, "reading", "Datoteka je prevelika (max 10 MB).");
            return;
        }

        await SendProgress("reading", "active", "Čitam ugovor...");

        string pdfText;
        try
        {
            pdfText = _pdfService.ExtractText(file);
        }
        catch (Exception ex)
        {
            await SseHelpers.SendSafeError(Response, "reading", "Nije moguće pročitati PDF.", ex, _logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(pdfText) || pdfText.Length < 50)
        {
            await SseHelpers.SendError(Response, "reading", "PDF ne sadrži dovoljno teksta. Možda je skeniran bez OCR-a?");
            return;
        }

        _logger.LogInformation("V57 Contract PDF stream: {Name} ({Size:N0} bytes), {Len} chars extracted", file.FileName, file.Length, pdfText.Length);

        await RunStreamingAnalysis(pdfText, "PDF");
    }

    /// <summary>
    /// POST /api/contract/analyze-text-stream — Paste contract text for streaming analysis.
    /// Returns SSE events with progress updates, then the final result.
    /// </summary>
    [HttpPost("analyze-text-stream")]
    [EnableRateLimiting("valuation")]
    public async Task AnalyzeTextStream([FromBody] ContractTextRequest request)
    {
        SseHelpers.SetupSSEHeaders(Response);

        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            await SseHelpers.SendError(Response, "reading", "Molimo unesite tekst ugovora.");
            return;
        }

        if (request.Text.Length < 50)
        {
            await SseHelpers.SendError(Response, "reading", "Tekst je prekratak za smislenu analizu (min. 50 znakova).");
            return;
        }

        if (request.Text.Length > 50000)
        {
            await SseHelpers.SendError(Response, "reading", "Tekst je predugačak (max 50.000 znakova).");
            return;
        }

        await SendProgress("reading", "active", "Čitam ugovor...");

        _logger.LogInformation("V57 Contract text stream: {Len} chars", request.Text.Length);

        await RunStreamingAnalysis(request.Text, "text");
    }

    /// <summary>
    /// Shared SSE streaming logic — runs three-pass analysis with progress events.
    /// </summary>
    private async Task RunStreamingAnalysis(string contractText, string source)
    {
        try
        {
            // V58: Send extracted text to client so it can be used for generate-stream later
            await SseHelpers.SendSSE(Response, "source_text", new { text = contractText });

            var result = await _contractService.AnalyzeContractAsync(contractText, async (stage, status, label, detail) =>
            {
                await SendProgress(stage, status, label, detail);
            });

            // ── Save to Supabase ──
            await SendProgress("saving", "active", "Spremam rezultate...");
            try
            {
                var id = await _supabaseService.SaveContractAnalysisAsync(new ContractAnalysisRecord
                {
                    DocumentType = result.DocumentType,
                    Summary = result.Summary,
                    OverallRisk = result.OverallRisk,
                    RiskScore = result.RiskScore,
                    RedFlagsCount = result.RedFlags.Count,
                    RawJsonOutput = result
                });
                await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
                {
                    Module = "contract_analysis",
                    AnalysisId = id,
                    Step = 1,
                    Action = $"Ugovor analiziran ({source}): {result.DocumentType}, rizik={result.OverallRisk} ({result.RiskScore}/100), {result.RedFlags.Count} crvenih zastavica",
                    LegalBasis = "Zakon o obveznim odnosima (NN 35/05, 29/18)",
                    Citation = "čl. 247.–269. ZOO",
                    Reasoning = result.Summary,
                    Confidence = result.RiskScore >= 70 ? "VISOKO" : result.RiskScore >= 40 ? "SREDNJE" : "NISKO",
                    ModelUsed = _clients.HeavyModel,
                    ModelTemperature = 0.10
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase save failed for contract analysis stream ({Source})", source);
            }
            await SendProgress("saving", "done", "Rezultati spremljeni");

            // ── Send final result ──
            await SseHelpers.SendSSE(Response, "result", result);
        }
        catch (Exception ex)
        {
            await SseHelpers.SendSafeError(Response, "audit", "AI analiza nije uspjela. Pokušajte ponovno.", ex, _logger);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  V58: GENERATE IMPROVED CONTRACT — SSE streaming
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/contract/generate-stream — Generate an improved contract based on analysis results.
    /// Returns SSE events with progress updates, then the generated contract.
    /// </summary>
    [HttpPost("generate-stream")]
    [EnableRateLimiting("valuation")]
    public async Task GenerateContractStream([FromBody] GenerateContractRequest request)
    {
        SseHelpers.SetupSSEHeaders(Response);

        if (string.IsNullOrWhiteSpace(request?.OriginalText))
        {
            await SseHelpers.SendError(Response, "preparing", "Originalni tekst ugovora je obavezan.");
            return;
        }

        if (request.AnalysisResult == null)
        {
            await SseHelpers.SendError(Response, "preparing", "Rezultat analize je obavezan.");
            return;
        }

        _logger.LogInformation("V58 Generate improved contract: {Len} chars, risk={Risk}",
            request.OriginalText.Length, request.AnalysisResult.OverallRisk);

        try
        {
            var result = await _contractService.GenerateImprovedContractAsync(
                request.OriginalText,
                request.AnalysisResult,
                async (stage, status, label, detail) =>
                {
                    await SendProgress(stage, status, label, detail);
                });

            if (!string.IsNullOrEmpty(result.Error))
            {
                await SseHelpers.SendError(Response, "generating", result.Error);
                return;
            }

            // ── Save audit log ──
            try
            {
                await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
                {
                    Module = "contract_generate",
                    Step = 1,
                    Action = $"Poboljšani ugovor generiran: {result.ChangesSummary.Count} izmjena",
                    LegalBasis = "Zakon o obveznim odnosima (NN 35/05, 29/18)",
                    Citation = "čl. 247.–269. ZOO",
                    Reasoning = $"Originalni ugovor: {request.OriginalText.Length} znakova, " +
                                $"Rizik: {request.AnalysisResult.OverallRisk} ({request.AnalysisResult.RiskScore}/100)",
                    Confidence = "VISOKO",
                    ModelUsed = _clients.HeavyModel,
                    ModelTemperature = 0.10
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase audit log failed for contract generation");
            }

            await SseHelpers.SendSSE(Response, "result", result);
        }
        catch (Exception ex)
        {
            await SseHelpers.SendSafeError(Response, "generating", "Generiranje ugovora nije uspjelo. Pokušajte ponovno.", ex, _logger);
        }
    }
}

public class ContractTextRequest
{
    public string Text { get; set; } = "";
}
