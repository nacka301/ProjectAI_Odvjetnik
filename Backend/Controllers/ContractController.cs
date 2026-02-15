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
    private readonly ILogger<ContractController> _logger;

    public ContractController(
        ContractAnalysisService contractService,
        PdfService pdfService,
        SupabaseService supabaseService,
        ILogger<ContractController> logger)
    {
        _contractService = contractService;
        _pdfService = pdfService;
        _supabaseService = supabaseService;
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
            return BadRequest(new { error = $"Nije moguće pročitati PDF: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(pdfText) || pdfText.Length < 50)
        {
            return BadRequest(new { error = "PDF ne sadrži dovoljno teksta. Možda je skeniran bez OCR-a?" });
        }

        _logger.LogInformation("Contract text extracted: {Len} chars", pdfText.Length);

        var result = await _contractService.AnalyzeContractAsync(pdfText);

        // Save to Supabase (fire-and-forget)
        _ = _supabaseService.SaveContractAnalysisAsync(new ContractAnalysisRecord
        {
            DocumentType = result.DocumentType,
            Summary = result.Summary,
            OverallRisk = result.OverallRisk,
            RiskScore = result.RiskScore,
            RedFlagsCount = result.RedFlags.Count,
            RawJsonOutput = result
        });

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

        // Save to Supabase (fire-and-forget)
        _ = _supabaseService.SaveContractAnalysisAsync(new ContractAnalysisRecord
        {
            DocumentType = result.DocumentType,
            Summary = result.Summary,
            OverallRisk = result.OverallRisk,
            RiskScore = result.RiskScore,
            RedFlagsCount = result.RedFlags.Count,
            RawJsonOutput = result
        });

        return Ok(result);
    }
}

public class ContractTextRequest
{
    public string Text { get; set; } = "";
}
