using Backend.Models;
using Backend.Models.V6;
using Backend.Services;
using Backend.Services.V6;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// V6 Evaluation endpoint — Next-generation AVM pipeline.
/// 
/// POST /api/v6/evaluate
///   Input: PDF file (ZK izvadak)
///   Output: V6ValuationReport with full explainability
/// 
/// Pipeline: Phase 0 (Enrich) → Phase 1 (Generate ~50 candidates) →
///           Phase 2 (Deep Fetch top 30) → Phase 3 (NLP + Embeddings) →
///           Phase 4 (Multi-Dim Similarity) → Phase 5 (Gating) →
///           Phase 6 (DBSCAN Clustering) → Phase 7 (IQR + Valuation) →
///           Phase 8 (XAI Explainability)
/// </summary>
[ApiController]
[Route("api/v6")]
public class V6EvaluateController : ControllerBase
{
    private readonly IValuationPipeline _pipeline;
    private readonly PdfService _pdfService;
    private readonly ValuationService _valuationService;
    private readonly ILogger<V6EvaluateController> _logger;

    public V6EvaluateController(
        IValuationPipeline pipeline,
        PdfService pdfService,
        ValuationService valuationService,
        ILogger<V6EvaluateController> logger)
    {
        _pipeline = pipeline;
        _pdfService = pdfService;
        _valuationService = valuationService;
        _logger = logger;
    }

    /// <summary>
    /// Full V6 evaluation pipeline.
    /// </summary>
    [HttpPost("evaluate")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Evaluate(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Nije učitana datoteka." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Samo PDF datoteke su prihvaćene." });
        }

        _logger.LogInformation("=== V6 Evaluate: {FileName} ({Size} bytes) ===",
            file.FileName, file.Length);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // ── Stage 1: Extract text from PDF ──
            string pdfText;
            ExtractedPropertyInfo property;
            try
            {
                pdfText = _pdfService.ExtractText(file);
                property = await _valuationService.ExtractPropertyInfoAsync(pdfText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "V6: PDF extraction failed");
                return StatusCode(500, new { error = $"PDF ekstrakcija: {ex.Message}" });
            }

            _logger.LogInformation("V6: Extracted — {Desc}, {Size} m², {Location}",
                property.Description, property.SizeM2, property.Location);

            // ── Execute V6 Pipeline (Phases 0–8) ──
            V6ValuationReport report;
            try
            {
                report = await _pipeline.ExecuteAsync(pdfText, property, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "V6: Pipeline execution failed");
                return StatusCode(500, new { error = $"V6 Pipeline greška: {ex.Message}" });
            }

            sw.Stop();
            _logger.LogInformation("V6: Complete — {Value:F0} €, confidence={Score}/100, {Duration}ms",
                report.EstimatedValueEur, report.ConfidenceScore, sw.ElapsedMilliseconds);

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "V6: Unexpected error");
            return StatusCode(500, new { error = $"Neočekivana greška: {ex.Message}" });
        }
    }

    /// <summary>
    /// Health check for V6 services.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "V6 Pipeline operational",
            version = "6.0",
            timestamp = DateTime.UtcNow,
            phases = new[]
            {
                "Phase 0: Canonical Modeling & Enrichment",
                "Phase 1: Candidate Generation (Tavily, ~50 candidates)",
                "Phase 2: Controlled Deep Fetch (top 30)",
                "Phase 3: NLP Extraction + Embeddings (OpenAI)",
                "Phase 4: Multi-Dimensional Similarity (5 dimensions)",
                "Phase 5: Hard Gating & Adaptive Sampling",
                "Phase 6: DBSCAN Clustering",
                "Phase 7: IQR + Weighted Median Valuation",
                "Phase 8: XAI Explainability Report"
            }
        });
    }
}
