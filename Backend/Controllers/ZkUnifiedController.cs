using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Controllers;

/// <summary>
/// Unified ZK → Valuation endpoint.
/// Upload a ZK izvadak PDF → parse all 3 sheets → risk analysis → 
/// automatic valuation pipeline → SSE streaming.
/// 
/// This merges the old /analyze (risk only) and /evaluate (valuation only)
/// into a single flow: one PDF upload, both results.
/// </summary>
[ApiController]
[Route("api/zk")]
public class ZkUnifiedController : ControllerBase
{
    private readonly ZkParsingService _zkParsingService;
    private readonly ValuationService _valuationService;
    private readonly PdfService _pdfService;
    private readonly MarketResearchService _marketResearchService;
    private readonly ValuationEngine _valuationEngine;
    private readonly SupabaseService _supabaseService;
    private readonly EmailService _emailService;
    private readonly ILogger<ZkUnifiedController> _logger;

    public ZkUnifiedController(
        ZkParsingService zkParsingService,
        ValuationService valuationService,
        PdfService pdfService,
        MarketResearchService marketResearchService,
        ValuationEngine valuationEngine,
        SupabaseService supabaseService,
        EmailService emailService,
        ILogger<ZkUnifiedController> logger)
    {
        _zkParsingService = zkParsingService;
        _valuationService = valuationService;
        _pdfService = pdfService;
        _marketResearchService = marketResearchService;
        _valuationEngine = valuationEngine;
        _supabaseService = supabaseService;
        _emailService = emailService;
        _logger = logger;
    }

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

    private void SetupSSEHeaders()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");
    }

    // ══════════════════════════════════════════════════════════
    //  POST /api/zk/analyze — ZK parse only (returns JSON, no valuation)
    //  Quick replacement for old /api/analyze with richer data.
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Parse a ZK izvadak PDF and return structured analysis.
    /// No valuation — just risk + legal analysis.
    /// </summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AnalyzeZk(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Nije učitana datoteka." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Samo PDF datoteke su prihvaćene." });
        }

        _logger.LogInformation("ZK Analyze: {FileName} ({Size} bytes)", file.FileName, file.Length);

        try
        {
            var (parsed, _) = await _zkParsingService.ParseZkIzvadakAsync(file);
            return Ok(parsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZK analysis failed.");
            return StatusCode(500, new { error = $"Greška pri analizi ZK izvadka: {ex.Message}" });
        }
    }

    // ══════════════════════════════════════════════════════════
    //  POST /api/zk/stream — UNIFIED: ZK parse + full valuation (SSE)
    //  One PDF → ZK analysis → automatic valuation pipeline.
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Unified SSE streaming: upload ZK PDF → parse → risk → valuation.
    /// Frontend gets real-time progress for all stages.
    /// </summary>
    [HttpPost("stream")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task UnifiedStream(
        IFormFile? file,
        [FromForm] string? city = null,
        [FromForm] string? neighborhood = null,
        [FromForm] string? email = null,
        [FromForm] bool skipValuation = false)
    {
        SetupSSEHeaders();

        if (file is null || file.Length == 0)
        {
            await SendError("validation", "Nije učitana datoteka.");
            return;
        }

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await SendError("validation", "Samo PDF datoteke su prihvaćene.");
            return;
        }

        _logger.LogInformation("=== ZK Unified Stream: {FileName} ({Size} bytes) ===",
            file.FileName, file.Length);

        try
        {
            // ────────────────────────────────────────────────────
            // STAGE 0: Parse ZK izvadak (all 3 sheets)
            // ────────────────────────────────────────────────────
            await SendProgress("zk_parsing", "active", "Parsiram ZK izvadak (A/B/C listovi)...");

            ZkParsedResult zkResult;
            string rawPdfText;
            try
            {
                (zkResult, rawPdfText) = await _zkParsingService.ParseZkIzvadakAsync(file);
            }
            catch (Exception ex)
            {
                await SendProgress("zk_parsing", "error", "Parsiranje ZK-a", ex.Message);
                await SendError("zk_parsing", $"Greška: {ex.Message}");
                return;
            }

            var zkSummary = $"{zkResult.PropertyId} • {zkResult.PropertyType} • " +
                $"Vlasnici: {zkResult.Owners.Count} • Rizik: {zkResult.RiskScore}";
            await SendProgress("zk_parsing", "done", "ZK izvadak parsiran", zkSummary);

            // Send ZK analysis result as intermediate data
            await SendSSE("zk_analysis", zkResult);

            // ── Check if valuation should proceed ──
            if (skipValuation)
            {
                _logger.LogInformation("Valuation skipped by request");
                await SendSSE("complete", new UnifiedZkValuationResult
                {
                    ZkAnalysis = zkResult,
                    ValuationAttempted = false,
                    ValuationSkipReason = "Korisnik odabrao samo analizu"
                });
                return;
            }

            // ────────────────────────────────────────────────────
            // STAGE 1: Convert ZK → ExtractedPropertyInfo
            // ────────────────────────────────────────────────────
            await SendProgress("reading", "active", "Pripremam podatke za procjenu...");

            ExtractedPropertyInfo property;
            try
            {
                property = ZkParsingService.ToPropertyInfo(zkResult, rawPdfText);

                // Location override if provided
                if (!string.IsNullOrWhiteSpace(city) || !string.IsNullOrWhiteSpace(neighborhood))
                {
                    string overrideLocation;
                    if (!string.IsNullOrWhiteSpace(neighborhood) && !string.IsNullOrWhiteSpace(city))
                        overrideLocation = $"{neighborhood.Trim()}, {city.Trim()}";
                    else if (!string.IsNullOrWhiteSpace(neighborhood))
                        overrideLocation = neighborhood.Trim();
                    else
                        overrideLocation = city!.Trim();

                    _logger.LogInformation("Location override: {From} → {To}",
                        property.Location, overrideLocation);
                    property.Location = overrideLocation;
                }
            }
            catch (Exception ex)
            {
                await SendProgress("reading", "error", "Priprema podataka", ex.Message);
                await SendError("reading", $"Greška: {ex.Message}");
                return;
            }

            // Check if we have enough data to valuate
            bool canValuate = true;
            string? skipReason = null;

            if (string.IsNullOrWhiteSpace(property.Location))
            {
                canValuate = false;
                skipReason = "Lokacija nepoznata — nije moguća tržišna pretraga";
            }
            else if (property.SizeM2 == 0 && zkResult.PropertyType is not "zemljište")
            {
                // We can still try — engine handles size=0 with limitations
                _logger.LogWarning("Building area unknown — valuation will have limited accuracy");
            }

            await SendProgress("reading", "done", "Podaci pripremljeni",
                $"{property.Description}, {property.SizeM2} m², {property.Location}");

            if (!canValuate)
            {
                await SendSSE("complete", new UnifiedZkValuationResult
                {
                    ZkAnalysis = zkResult,
                    ValuationAttempted = false,
                    ValuationSkipReason = skipReason
                });
                return;
            }

            // ────────────────────────────────────────────────────
            // STAGES 2-4: Run the full valuation pipeline
            // (same logic as EvaluateController.RunStreamingPipelineAsync)
            // ────────────────────────────────────────────────────
            var valuationResult = await RunValuationPipelineAsync(
                property, rawPdfText, zkResult, email);

            // ── Final combined result ──
            await SendSSE("complete", new UnifiedZkValuationResult
            {
                ZkAnalysis = zkResult,
                Valuation = valuationResult,
                ValuationAttempted = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZK unified pipeline error");
            await SendError("unknown", $"Neočekivana greška: {ex.Message}");
        }
    }

    /// <summary>
    /// Run stages 2 through result of the valuation pipeline.
    /// Returns FullEvaluationResult or null on failure.
    /// </summary>
    private async Task<FullEvaluationResult?> RunValuationPipelineAsync(
        ExtractedPropertyInfo property, string pdfText,
        ZkParsedResult zkResult, string? email)
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
            return null;
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
            return null;
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
            return null;
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
            SearchPhaseUsed = marketData.SearchPhaseUsed
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

        // ── Email ──
        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var sent = await _emailService.SendValuationResultAsync(
                    email, response, $"{property.Description}, {property.SizeM2} m², {property.Location}");
                if (sent) _logger.LogInformation("Email sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email to {Email}", email);
            }
        }

        return response;
    }

    /// <summary>
    /// Patch reasoning: replace AI's wrong final value with engine's authoritative value.
    /// (Same as EvaluateController.PatchReasoningValue)
    /// </summary>
    private static string PatchReasoningValue(string reasoning, double aiValue, int engineValue)
    {
        if (string.IsNullOrWhiteSpace(reasoning)) return reasoning;

        int aiInt = (int)Math.Round(aiValue);
        string[] aiFormats =
        [
            aiInt.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("hr-HR")),
            aiInt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
            aiInt.ToString(),
        ];

        string engineFormatted = engineValue.ToString("N0",
            System.Globalization.CultureInfo.GetCultureInfo("hr-HR"));

        foreach (var fmt in aiFormats)
        {
            if (reasoning.Contains(fmt))
                reasoning = reasoning.Replace(fmt, engineFormatted);
        }

        foreach (var fmt in aiFormats)
        {
            reasoning = reasoning
                .Replace($"{fmt} €", $"{engineFormatted} €")
                .Replace($"{fmt} EUR", $"{engineFormatted} EUR")
                .Replace($"{fmt} eur", $"{engineFormatted} EUR");
        }

        return reasoning;
    }
}
