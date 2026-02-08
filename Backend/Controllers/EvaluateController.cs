using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// V2 Evaluation endpoint: PDF → Extract → Market Research → 
/// Deterministic Engine → AI Opinion → Supabase Save.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluateController : ControllerBase
{
    private readonly ValuationService _valuationService;
    private readonly PdfService _pdfService;
    private readonly MarketResearchService _marketResearchService;
    private readonly ValuationEngine _valuationEngine;
    private readonly SupabaseService _supabaseService;
    private readonly ILogger<EvaluateController> _logger;

    public EvaluateController(
        ValuationService valuationService,
        PdfService pdfService,
        MarketResearchService marketResearchService,
        ValuationEngine valuationEngine,
        SupabaseService supabaseService,
        ILogger<EvaluateController> logger)
    {
        _valuationService = valuationService;
        _pdfService = pdfService;
        _marketResearchService = marketResearchService;
        _valuationEngine = valuationEngine;
        _supabaseService = supabaseService;
        _logger = logger;
    }

    /// <summary>
    /// Full 3-stage evaluation pipeline:
    /// 1. Extract property info from PDF
    /// 2. Market research (Njuškalo + benchmarks)
    /// 3. Deterministic engine + AI opinion
    /// 4. Save to Supabase
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Evaluate(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Nije učitana datoteka." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Samo PDF datoteke su prihvaćene." });
        }

        _logger.LogInformation("=== V2 Evaluate: {FileName} ({Size} bytes) ===",
            file.FileName, file.Length);

        try
        {
            // ────────────────────────────────────────────────────
            // STAGE 1: Extract property info from PDF
            // ────────────────────────────────────────────────────
            var pdfText = _pdfService.ExtractText(file);
            var property = await _valuationService.ExtractPropertyInfoAsync(pdfText);

            _logger.LogInformation(
                "Stage 1 done: {Desc}, {Size} m², {Location}",
                property.Description, property.SizeM2, property.Location);

            // ────────────────────────────────────────────────────
            // STAGE 2: Market research (Njuškalo stealth + benchmarks)
            // ────────────────────────────────────────────────────
            var marketData = await _marketResearchService
                .SearchComparables(property.Location, property.SizeM2, pdfText);

            _logger.LogInformation(
                "Stage 2 done: {Active} active listings, benchmark={Benchmark} €/m² ({Region})",
                marketData.ActiveListings.Count,
                marketData.RegionalBenchmarkPerM2,
                marketData.RegionName);

            // ────────────────────────────────────────────────────
            // STAGE 3A: Deterministic engine (server-side math)
            // ────────────────────────────────────────────────────
            var engineResult = _valuationEngine.Calculate(property, marketData);

            _logger.LogInformation(
                "Stage 3A done: Engine says {Value} € (confidence: {Conf})",
                engineResult.EstimatedValueEur, engineResult.ConfidenceLevel);

            // ────────────────────────────────────────────────────
            // STAGE 3B: AI opinion (legal text + comparable selection)
            // The AI receives both the raw data AND our engine numbers
            // ────────────────────────────────────────────────────
            var aiResult = await _valuationService
                .GenerateAutoValuationAsync(property, marketData);

            _logger.LogInformation(
                "Stage 3B done: AI says {Value} €", aiResult.EstimatedValueEur);

            // ────────────────────────────────────────────────────
            // MERGE: Engine numbers take priority, AI provides text
            // If AI value deviates >40% from engine, use engine value
            // ────────────────────────────────────────────────────
            int finalValue = engineResult.EstimatedValueEur;
            if (aiResult.EstimatedValueEur > 0)
            {
                double deviation = Math.Abs(aiResult.EstimatedValueEur - engineResult.EstimatedValueEur)
                    / (double)engineResult.EstimatedValueEur;

                if (deviation <= 0.40)
                {
                    // AI is within ±40% — use average of both
                    finalValue = (int)Math.Round(
                        (aiResult.EstimatedValueEur + engineResult.EstimatedValueEur) / 2.0);
                    _logger.LogInformation(
                        "Values within 40% — using average: {Value} €", finalValue);
                }
                else
                {
                    _logger.LogWarning(
                        "AI value ({AiValue}) deviates {Dev:P0} from engine ({EngineValue}). Using engine value.",
                        aiResult.EstimatedValueEur, deviation, engineResult.EstimatedValueEur);
                }
            }

            // ────────────────────────────────────────────────────
            // BUILD RESPONSE
            // ────────────────────────────────────────────────────
            var response = new FullEvaluationResult
            {
                // AI fields
                TargetPropertySummary = aiResult.TargetPropertySummary,
                SelectedComparables = aiResult.SelectedComparables,
                EstimatedValueEur = finalValue,
                ValueDrivers = aiResult.ValueDrivers,
                ValuationReasoning = aiResult.ValuationReasoning,

                // Engine fields
                ConfidenceLevel = engineResult.ConfidenceLevel,
                RegionalBenchmarkPerM2 = engineResult.RegionalBenchmarkPerM2,
                MarketAverageAsking = engineResult.MarketAverageAsking,
                CalculatedPricePerM2 = engineResult.CalculatedPricePerM2,
                RealizationDiscountPercent = engineResult.RealizationDiscountPercent,
                TotalListingsFound = engineResult.TotalListingsFound,
                UsablePricePoints = engineResult.UsablePricePoints,
                OutliersRemoved = engineResult.OutliersRemoved,
                PriceSource = engineResult.PriceSource,
                RegionName = engineResult.RegionName,
                Note = string.IsNullOrEmpty(marketData.Note) ? null : marketData.Note
            };

            // ────────────────────────────────────────────────────
            // STAGE 4: Save to Supabase (non-blocking)
            // ────────────────────────────────────────────────────
            try
            {
                var evaluationId = await _supabaseService.SaveEvaluationAsync(new EvaluationRecord
                {
                    PropertyType = marketData.DetectedPropertyType,
                    Location = property.Location,
                    SizeM2 = property.SizeM2 > 0 ? property.SizeM2 : null,
                    EstimatedValue = finalValue,
                    RawJsonOutput = response
                });

                response.EvaluationId = evaluationId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save to Supabase (non-critical).");
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in V2 evaluation pipeline.");
            return StatusCode(500, new { error = "Greška pri evaluaciji nekretnine." });
        }
    }
}
