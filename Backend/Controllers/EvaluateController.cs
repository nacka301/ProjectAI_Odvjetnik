using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
    private readonly EmailService _emailService;
    private readonly ILogger<EvaluateController> _logger;

    public EvaluateController(
        ValuationService valuationService,
        PdfService pdfService,
        MarketResearchService marketResearchService,
        ValuationEngine valuationEngine,
        SupabaseService supabaseService,
        EmailService emailService,
        ILogger<EvaluateController> logger)
    {
        _valuationService = valuationService;
        _pdfService = pdfService;
        _marketResearchService = marketResearchService;
        _valuationEngine = valuationEngine;
        _supabaseService = supabaseService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Full 3-stage evaluation pipeline:
    /// 1. Extract property info from PDF
    /// 2. Market research (Njuškalo search)
    /// 3. Deterministic engine + AI opinion
    /// 4. Save to Supabase
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Evaluate(
        IFormFile file,
        [FromForm] string? city = null,
        [FromForm] string? neighborhood = null)
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
            string pdfText;
            ExtractedPropertyInfo property;
            try
            {
                pdfText = _pdfService.ExtractText(file);
                property = await _valuationService.ExtractPropertyInfoAsync(pdfText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 1 FAILED: PDF extraction / AI property parsing.");
                return StatusCode(500, new { error = $"Stage 1 greška (PDF/AI ekstrakcija): {ex.Message}" });
            }

            _logger.LogInformation(
                "Stage 1 done: {Desc}, {Size} m², {Location}",
                property.Description, property.SizeM2, property.Location);

            // LOCATION OVERRIDE: If frontend sent structured location fields,
            // use those instead of the AI-extracted location from the PDF.
            ApplyLocationOverride(property, city, neighborhood);

            // ────────────────────────────────────────────────────
            // STAGE 2: Market research (Njuškalo stealth search)
            // ────────────────────────────────────────────────────
            MarketResearchResult marketData;
            try
            {
                marketData = await _marketResearchService
                    .SearchComparables(property.Location, property.SizeM2, pdfText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 2 FAILED: Market research.");
                return StatusCode(500, new { error = $"Stage 2 greška (pretraga tržišta): {ex.Message}" });
            }

            _logger.LogInformation(
                "Stage 2 done: {Active} active listings ({Region})",
                marketData.ActiveListings.Count,
                marketData.RegionName);

            // ────────────────────────────────────────────────────
            // STAGE 2.5: AI Feature Extraction (batch GPT call)
            // Populates HasGarage, HasRenovation, HasYard, etc.
            // on each ComparableSearchResult BEFORE the engine.
            // ────────────────────────────────────────────────────
            try
            {
                await _valuationService.ExtractListingFeaturesAsync(marketData.ActiveListings, property);
                _valuationService.FilterByAiPropertyType(
                    marketData.ActiveListings, marketData.DetectedPropertyType);
                _logger.LogInformation("Stage 2.5 done: AI features + similarity extracted for {Count} listings",
                    marketData.ActiveListings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stage 2.5 non-fatal: AI feature extraction failed — engine will use keyword fallback");
            }

            // ────────────────────────────────────────────────────
            // STAGE 3A: Deterministic engine (server-side math)
            // ────────────────────────────────────────────────────
            DeterministicValuation engineResult;
            try
            {
                engineResult = _valuationEngine.Calculate(property, marketData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 3A FAILED: Valuation engine.");
                return StatusCode(500, new { error = $"Stage 3A greška (engine): {ex.Message}" });
            }

            _logger.LogInformation(
                "Stage 3A done: Engine says {Value} € (confidence: {Conf}), {Used} clean URLs",
                engineResult.EstimatedValueEur, engineResult.ConfidenceLevel,
                engineResult.FinalUsedUrls.Count);

            // ────────────────────────────────────────────────────
            // STAGE 3A.5: SYNC — Filter ActiveListings for LLM
            // Only let the LLM see listings that the engine approved.
            // This prevents the AI from picking rental/junk comparables
            // that the math engine already rejected.
            // ────────────────────────────────────────────────────
            if (engineResult.FinalUsedUrls.Count > 0)
            {
                int beforeSync = marketData.ActiveListings.Count;
                var filteredListings = marketData.ActiveListings
                    .Where(l => engineResult.FinalUsedUrls.Contains(l.Url))
                    .ToList();

                // Only replace if we still have at least 1 listing
                if (filteredListings.Count > 0)
                {
                    marketData.ActiveListings = filteredListings;
                    _logger.LogInformation(
                        "LLM Sync: filtered ActiveListings {Before} → {After} (engine-approved only)",
                        beforeSync, filteredListings.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "LLM Sync: No URL overlap between engine and ActiveListings — keeping all {Count} for LLM",
                        beforeSync);
                }
            }

            // ────────────────────────────────────────────────────
            // STAGE 3B: AI opinion (legal text + comparable selection)
            // ────────────────────────────────────────────────────
            AutoValuationResult aiResult;
            try
            {
                aiResult = await _valuationService
                    .GenerateAutoValuationAsync(property, marketData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 3B FAILED: AI valuation opinion.");
                return StatusCode(500, new { error = $"Stage 3B greška (AI procjena): {ex.Message}" });
            }

            _logger.LogInformation(
                "Stage 3B done: AI says {Value} €", aiResult.EstimatedValueEur);

            // ────────────────────────────────────────────────────
            // MERGE: Engine numbers take priority, AI provides text
            // If AI value deviates >40% from engine, use engine value
            // ────────────────────────────────────────────────────
            int finalValue = engineResult.EstimatedValueEur;
            string finalReasoning = aiResult.ValuationReasoning;
            if (aiResult.EstimatedValueEur > 0 && engineResult.EstimatedValueEur > 0)
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

                    // PATCH the reasoning text: replace AI's wrong final value with the engine value
                    // so the displayed text doesn't contradict the displayed number
                    finalReasoning = PatchReasoningValue(
                        aiResult.ValuationReasoning,
                        aiResult.EstimatedValueEur,
                        engineResult.EstimatedValueEur);
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
                ValuationReasoning = finalReasoning,

                // Engine core fields
                ConfidenceLevel = engineResult.ConfidenceLevel,
                MarketAverageAsking = engineResult.MarketAverageAsking,
                CalculatedPricePerM2 = engineResult.CalculatedPricePerM2,
                RealizationDiscountPercent = engineResult.RealizationDiscountPercent,
                TotalListingsFound = engineResult.TotalListingsFound,
                UsablePricePoints = engineResult.UsablePricePoints,
                OutliersRemoved = engineResult.OutliersRemoved,
                PriceSource = engineResult.PriceSource,
                RegionName = engineResult.RegionName,
                Note = string.IsNullOrEmpty(marketData.Note) ? null : marketData.Note,

                // V4 Robust Estimator fields
                StdDevPerM2 = engineResult.StdDevPerM2,
                CoefficientOfVariation = engineResult.CoefficientOfVariation,
                OutlierMethod = engineResult.OutlierMethod,
                OutlierLog = engineResult.OutlierLog,
                TotalAdjustmentPercent = engineResult.TotalAdjustmentPercent,
                RealizationGapReasoning = engineResult.RealizationGapReasoning,
                UsedComparables = engineResult.UsedComparables,
                Adjustments = engineResult.Adjustments,
                Pipeline = engineResult.Pipeline,

                // V4 Math Confidence
                MathConfidenceScore = engineResult.MathConfidenceScore,
                MathConfidenceLevel = engineResult.MathConfidenceLevel,
                MathConfidenceExplanation = engineResult.MathConfidenceExplanation,

                // V3 Market Research Confidence
                ConfidenceScore = marketData.Confidence.Score,
                ConfidenceExplanation = marketData.Confidence.Explanation,
                ConfidenceColor = marketData.Confidence.Color,
                SearchPhaseUsed = marketData.SearchPhaseUsed
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

    // ══════════════════════════════════════════════════════════
    //  LOCATION OVERRIDE — use frontend-supplied city/neighborhood
    //  instead of AI-guessed location from the PDF/text.
    // ══════════════════════════════════════════════════════════
    private void ApplyLocationOverride(
        ExtractedPropertyInfo property, string? city, string? neighborhood)
    {
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(neighborhood))
            return; // No override — keep AI-extracted location

        string overrideLocation;
        if (!string.IsNullOrWhiteSpace(neighborhood) && !string.IsNullOrWhiteSpace(city))
            overrideLocation = $"{city.Trim()}, {neighborhood.Trim()}";
        else if (!string.IsNullOrWhiteSpace(city))
            overrideLocation = city.Trim();
        else
            overrideLocation = neighborhood!.Trim();

        _logger.LogInformation(
            "LOCATION OVERRIDE: AI extracted \"{AiLocation}\" → user specified \"{Override}\"",
            property.Location, overrideLocation);

        property.Location = overrideLocation;
    }

    // ══════════════════════════════════════════════════════════
    //  PATCH REASONING — replace the AI's wrong final value
    //  with the engine's authoritative value in the text body
    // ══════════════════════════════════════════════════════════
    private static string PatchReasoningValue(
        string reasoning, double aiValue, int engineValue)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
            return reasoning;

        int aiInt = (int)Math.Round(aiValue);

        // Format both values in Croatian style (dot thousands separator)
        string[] aiFormats =
        [
            aiInt.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("hr-HR")),   // 221.000
            aiInt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),           // 221,000
            aiInt.ToString(),                                                                   // 221000
        ];

        string engineFormatted = engineValue.ToString("N0",
            System.Globalization.CultureInfo.GetCultureInfo("hr-HR")); // 382.500

        foreach (var fmt in aiFormats)
        {
            if (reasoning.Contains(fmt))
            {
                reasoning = reasoning.Replace(fmt, engineFormatted);
            }
        }

        // Also try replacing "X €" / "X EUR" patterns
        foreach (var fmt in aiFormats)
        {
            reasoning = reasoning
                .Replace($"{fmt} €", $"{engineFormatted} €")
                .Replace($"{fmt} EUR", $"{engineFormatted} EUR")
                .Replace($"{fmt} eur", $"{engineFormatted} EUR");
        }

        return reasoning;
    }

    /// <summary>
    /// Text-based evaluation: accepts raw property text instead of PDF.
    /// Skips PDF extraction — goes straight into the pipeline.
    /// </summary>
    [HttpPost("text")]
    public async Task<IActionResult> EvaluateText([FromBody] TextEvaluationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return BadRequest(new { error = "Tekst nekretnine je prazan." });

        if (request.Text.Length < 20)
            return BadRequest(new { error = "Tekst je prekratak. Unesite barem opis lokacije i tipa nekretnine." });

        _logger.LogInformation("=== Text Evaluate: {Length} chars ===", request.Text.Length);

        try
        {
            // STAGE 1: Extract property info from text (same AI as PDF path)
            string pdfText = request.Text;
            ExtractedPropertyInfo property;
            try
            {
                property = await _valuationService.ExtractPropertyInfoAsync(pdfText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 1 FAILED: AI property parsing from text.");
                return StatusCode(500, new { error = $"Stage 1 greška (AI ekstrakcija): {ex.Message}" });
            }

            _logger.LogInformation(
                "Stage 1 done (text): {Desc}, {Size} m2, {Location}",
                property.Description, property.SizeM2, property.Location);

            // LOCATION OVERRIDE from structured fields
            ApplyLocationOverride(property, request.City, request.Neighborhood);

            // STAGE 2: Market research
            MarketResearchResult marketData;
            try
            {
                marketData = await _marketResearchService
                    .SearchComparables(property.Location, property.SizeM2, pdfText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 2 FAILED: Market research.");
                return StatusCode(500, new { error = $"Stage 2 greška (pretraga tržišta): {ex.Message}" });
            }

            // STAGE 2.5: AI Feature Extraction
            try
            {
                await _valuationService.ExtractListingFeaturesAsync(marketData.ActiveListings);
                _valuationService.FilterByAiPropertyType(
                    marketData.ActiveListings, marketData.DetectedPropertyType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stage 2.5 non-fatal: AI feature extraction failed");
            }

            // STAGE 3A: Deterministic engine
            DeterministicValuation engineResult;
            try
            {
                engineResult = _valuationEngine.Calculate(property, marketData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 3A FAILED: Valuation engine.");
                return StatusCode(500, new { error = $"Stage 3A greška (engine): {ex.Message}" });
            }

            // STAGE 3A.5: Sync — filter for LLM
            if (engineResult.FinalUsedUrls.Count > 0)
            {
                var filteredListings = marketData.ActiveListings
                    .Where(l => engineResult.FinalUsedUrls.Contains(l.Url))
                    .ToList();
                if (filteredListings.Count > 0)
                    marketData.ActiveListings = filteredListings;
            }

            // STAGE 3B: AI opinion
            AutoValuationResult aiResult;
            try
            {
                aiResult = await _valuationService
                    .GenerateAutoValuationAsync(property, marketData, engineResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stage 3B FAILED: AI valuation opinion.");
                return StatusCode(500, new { error = $"Stage 3B greška (AI procjena): {ex.Message}" });
            }

            // MERGE
            int finalValue = engineResult.EstimatedValueEur;
            string finalReasoning = aiResult.ValuationReasoning;
            if (aiResult.EstimatedValueEur > 0 && engineResult.EstimatedValueEur > 0)
            {
                double deviation = Math.Abs(aiResult.EstimatedValueEur - engineResult.EstimatedValueEur)
                    / (double)engineResult.EstimatedValueEur;
                if (deviation <= 0.40)
                    finalValue = (int)Math.Round(
                        (aiResult.EstimatedValueEur + engineResult.EstimatedValueEur) / 2.0);
                else
                    finalReasoning = PatchReasoningValue(
                        aiResult.ValuationReasoning,
                        aiResult.EstimatedValueEur,
                        engineResult.EstimatedValueEur);
            }

            // BUILD RESPONSE
            var response = new FullEvaluationResult
            {
                TargetPropertySummary = aiResult.TargetPropertySummary,
                SelectedComparables = aiResult.SelectedComparables,
                EstimatedValueEur = finalValue,
                ValueDrivers = aiResult.ValueDrivers,
                ValuationReasoning = finalReasoning,
                ConfidenceLevel = engineResult.ConfidenceLevel,
                MarketAverageAsking = engineResult.MarketAverageAsking,
                CalculatedPricePerM2 = engineResult.CalculatedPricePerM2,
                RealizationDiscountPercent = engineResult.RealizationDiscountPercent,
                TotalListingsFound = engineResult.TotalListingsFound,
                UsablePricePoints = engineResult.UsablePricePoints,
                OutliersRemoved = engineResult.OutliersRemoved,
                PriceSource = engineResult.PriceSource,
                RegionName = engineResult.RegionName,
                Note = string.IsNullOrEmpty(marketData.Note) ? null : marketData.Note,
                StdDevPerM2 = engineResult.StdDevPerM2,
                CoefficientOfVariation = engineResult.CoefficientOfVariation,
                OutlierMethod = engineResult.OutlierMethod,
                OutlierLog = engineResult.OutlierLog,
                TotalAdjustmentPercent = engineResult.TotalAdjustmentPercent,
                RealizationGapReasoning = engineResult.RealizationGapReasoning,
                UsedComparables = engineResult.UsedComparables,
                Adjustments = engineResult.Adjustments,
                Pipeline = engineResult.Pipeline,
                MathConfidenceScore = engineResult.MathConfidenceScore,
                MathConfidenceLevel = engineResult.MathConfidenceLevel,
                MathConfidenceExplanation = engineResult.MathConfidenceExplanation,
                ConfidenceScore = marketData.Confidence.Score,
                ConfidenceExplanation = marketData.Confidence.Explanation,
                ConfidenceColor = marketData.Confidence.Color,
                SearchPhaseUsed = marketData.SearchPhaseUsed
            };

            // Save to Supabase
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
            _logger.LogError(ex, "Error in text evaluation pipeline.");
            return StatusCode(500, new { error = "Greška pri evaluaciji nekretnine." });
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SSE STREAMING ENDPOINTS — real-time progress to frontend
    // ══════════════════════════════════════════════════════════

    private static readonly JsonSerializerOptions _sseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private async Task SendSSE(string type, object? payload)
    {
        var json = JsonSerializer.Serialize(new { type, data = payload }, _sseJson);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private Task SendProgress(string stage, string status, string label, string? detail = null)
        => SendSSE("progress", new { stage, status, label, detail });

    private Task SendError(string stage, string message)
        => SendSSE("error", new { stage, message });

    private Task SendResult(FullEvaluationResult result)
        => SendSSE("result", result);

    private void SetupSSEHeaders()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");
    }

    /// <summary>
    /// SSE streaming PDF evaluation — sends real-time stage progress.
    /// </summary>
    [HttpPost("stream")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task EvaluateStream(
        IFormFile? file,
        [FromForm] string? city = null,
        [FromForm] string? neighborhood = null,
        [FromForm] string? email = null)
    {
        SetupSSEHeaders();

        if (file is null || file.Length == 0)
        {
            await SendError("validation", "Nije učitana datoteka.");
            return;
        }

        try
        {
            // ── Stage 1: PDF extraction ──
            await SendProgress("reading", "active", "Čitam PDF i ekstrahiram podatke...");

            string pdfText;
            ExtractedPropertyInfo property;
            try
            {
                pdfText = _pdfService.ExtractText(file);
                property = await _valuationService.ExtractPropertyInfoAsync(pdfText);
                ApplyLocationOverride(property, city, neighborhood);
            }
            catch (Exception ex)
            {
                await SendProgress("reading", "error", "Čitanje PDF-a", ex.Message);
                await SendError("reading", $"Greška: {ex.Message}");
                return;
            }

            await SendProgress("reading", "done", "PDF pročitan",
                $"{property.Description}, {property.SizeM2} m², {property.Location}");

            await RunStreamingPipelineAsync(property, pdfText, email);
        }
        catch (Exception ex)
        {
            await SendError("unknown", $"Neočekivana greška: {ex.Message}");
        }
    }

    /// <summary>
    /// SSE streaming text evaluation — sends real-time stage progress.
    /// </summary>
    [HttpPost("text/stream")]
    public async Task EvaluateTextStream([FromBody] TextEvaluationRequest request)
    {
        SetupSSEHeaders();

        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            await SendError("validation", "Tekst nekretnine je prazan.");
            return;
        }

        try
        {
            await SendProgress("reading", "active", "Analiziram uneseni tekst...");

            ExtractedPropertyInfo property;
            try
            {
                property = await _valuationService.ExtractPropertyInfoAsync(request.Text);
                ApplyLocationOverride(property, request.City, request.Neighborhood);
            }
            catch (Exception ex)
            {
                await SendProgress("reading", "error", "Analiza teksta", ex.Message);
                await SendError("reading", $"Greška: {ex.Message}");
                return;
            }

            await SendProgress("reading", "done", "Tekst analiziran",
                $"{property.Description}, {property.SizeM2} m², {property.Location}");

            await RunStreamingPipelineAsync(property, request.Text, request.Email);
        }
        catch (Exception ex)
        {
            await SendError("unknown", $"Neočekivana greška: {ex.Message}");
        }
    }

    /// <summary>
    /// Shared streaming pipeline: Stage 2 through result.
    /// </summary>
    private async Task RunStreamingPipelineAsync(ExtractedPropertyInfo property, string pdfText, string? email = null)
    {
        // ── Stage 2: Market research ──
        await SendProgress("searching", "active", "Pretražujem Njuškalo i tržište nekretnina...");

        MarketResearchResult marketData;
        try
        {
            marketData = await _marketResearchService
                .SearchComparables(property.Location, property.SizeM2, pdfText);
        }
        catch (Exception ex)
        {
            await SendProgress("searching", "error", "Pretraga tržišta", ex.Message);
            await SendError("searching", $"Greška pretrage: {ex.Message}");
            return;
        }

        var searchDetail = $"{marketData.ActiveListings.Count} oglasa pronađeno";
        if (!string.IsNullOrEmpty(marketData.SearchPhaseUsed))
            searchDetail += $" • Faza: {marketData.SearchPhaseUsed}";
        if (marketData.Confidence.Score > 0)
            searchDetail += $" • Pouzdanost: {marketData.Confidence.Score}/100";

        await SendProgress("searching", "done", "Pretraga završena", searchDetail);

        // ── Stage 2.5: AI feature extraction ──
        await SendProgress("ai_features", "active", "AI analizira značajke oglasa...");
        try
        {
            await _valuationService.ExtractListingFeaturesAsync(marketData.ActiveListings);
            _valuationService.FilterByAiPropertyType(
                marketData.ActiveListings, marketData.DetectedPropertyType);

            int withGarage = marketData.ActiveListings.Count(l => l.HasGarage == true);
            int withReno = marketData.ActiveListings.Count(l => l.HasRenovation == true);
            int withYard = marketData.ActiveListings.Count(l => l.HasYard == true);

            await SendProgress("ai_features", "done", "AI značajke ekstrahirane",
                $"Garaža: {withGarage}, Renovirano: {withReno}, Dvorište: {withYard} od {marketData.ActiveListings.Count}");
        }
        catch
        {
            await SendProgress("ai_features", "skipped", "AI ekstrakcija preskočena",
                "Engine koristi keyword fallback");
        }

        // ── Stage 3A: Engine ──
        await SendProgress("engine", "active", "Engine izračunava tržišnu vrijednost...");

        DeterministicValuation engineResult;
        try
        {
            engineResult = _valuationEngine.Calculate(property, marketData);
        }
        catch (Exception ex)
        {
            await SendProgress("engine", "error", "Engine izračun", ex.Message);
            await SendError("engine", $"Greška engine-a: {ex.Message}");
            return;
        }

        await SendProgress("engine", "done", "Engine izračun gotov",
            $"{engineResult.UsablePricePoints} korištenih • {engineResult.CalculatedPricePerM2:N0} €/m² • Conf: {engineResult.ConfidenceLevel}");

        // ── Sync filter ──
        if (engineResult.FinalUsedUrls.Count > 0)
        {
            var filtered = marketData.ActiveListings
                .Where(l => engineResult.FinalUsedUrls.Contains(l.Url)).ToList();
            if (filtered.Count > 0)
                marketData.ActiveListings = filtered;
        }

        // ── Stage 3B: AI opinion ──
        await SendProgress("ai_opinion", "active", "AI generira stručno mišljenje...");

        AutoValuationResult aiResult;
        try
        {
            aiResult = await _valuationService.GenerateAutoValuationAsync(property, marketData, engineResult);
        }
        catch (Exception ex)
        {
            await SendProgress("ai_opinion", "error", "AI mišljenje", ex.Message);
            await SendError("ai_opinion", $"Greška AI procjene: {ex.Message}");
            return;
        }

        await SendProgress("ai_opinion", "done", "AI mišljenje generirano",
            $"AI procjena: {aiResult.EstimatedValueEur:N0} €");

        // ── Merge ──
        int finalValue = engineResult.EstimatedValueEur;
        string finalReasoning = aiResult.ValuationReasoning;
        if (aiResult.EstimatedValueEur > 0 && engineResult.EstimatedValueEur > 0)
        {
            double deviation = Math.Abs(aiResult.EstimatedValueEur - engineResult.EstimatedValueEur)
                / (double)engineResult.EstimatedValueEur;
            if (deviation <= 0.40)
                finalValue = (int)Math.Round(
                    (aiResult.EstimatedValueEur + engineResult.EstimatedValueEur) / 2.0);
            else
                finalReasoning = PatchReasoningValue(
                    aiResult.ValuationReasoning, aiResult.EstimatedValueEur, engineResult.EstimatedValueEur);
        }

        // ── Build response ──
        var response = new FullEvaluationResult
        {
            TargetPropertySummary = aiResult.TargetPropertySummary,
            SelectedComparables = aiResult.SelectedComparables,
            EstimatedValueEur = finalValue,
            ValueDrivers = aiResult.ValueDrivers,
            ValuationReasoning = finalReasoning,
            ConfidenceLevel = engineResult.ConfidenceLevel,
            MarketAverageAsking = engineResult.MarketAverageAsking,
            CalculatedPricePerM2 = engineResult.CalculatedPricePerM2,
            RealizationDiscountPercent = engineResult.RealizationDiscountPercent,
            TotalListingsFound = engineResult.TotalListingsFound,
            UsablePricePoints = engineResult.UsablePricePoints,
            OutliersRemoved = engineResult.OutliersRemoved,
            PriceSource = engineResult.PriceSource,
            RegionName = engineResult.RegionName,
            Note = string.IsNullOrEmpty(marketData.Note) ? null : marketData.Note,
            StdDevPerM2 = engineResult.StdDevPerM2,
            CoefficientOfVariation = engineResult.CoefficientOfVariation,
            OutlierMethod = engineResult.OutlierMethod,
            OutlierLog = engineResult.OutlierLog,
            TotalAdjustmentPercent = engineResult.TotalAdjustmentPercent,
            RealizationGapReasoning = engineResult.RealizationGapReasoning,
            UsedComparables = engineResult.UsedComparables,
            Adjustments = engineResult.Adjustments,
            Pipeline = engineResult.Pipeline,
            MathConfidenceScore = engineResult.MathConfidenceScore,
            MathConfidenceLevel = engineResult.MathConfidenceLevel,
            MathConfidenceExplanation = engineResult.MathConfidenceExplanation,
            ConfidenceScore = marketData.Confidence.Score,
            ConfidenceExplanation = marketData.Confidence.Explanation,
            ConfidenceColor = marketData.Confidence.Color,
            SearchPhaseUsed = marketData.SearchPhaseUsed,
            SherlockInsight = engineResult.SherlockInsight,
        };

        // ── Save to Supabase ──
        await SendProgress("saving", "active", "Spremam rezultate...");
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
            await SendProgress("saving", "done", "Rezultati spremljeni", $"ID: {evaluationId}");
        }
        catch
        {
            await SendProgress("saving", "skipped", "Spremanje preskočeno", "Supabase nedostupan");
        }

        // ── Send email notification ──
        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var sent = await _emailService.SendValuationResultAsync(
                    email, response, $"{property.Description}, {property.SizeM2} m², {property.Location}");
                if (sent)
                    _logger.LogInformation("Email sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email to {Email}", email);
            }
        }

        // ── Final result ──
        await SendResult(response);
    }
}

public class TextEvaluationRequest
{
    public string Text { get; set; } = "";
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? Email { get; set; }
}
