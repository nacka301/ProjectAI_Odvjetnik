using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
            overrideLocation = $"{neighborhood.Trim()}, {city.Trim()}";
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
    //  SSE STREAMING ENDPOINTS — real-time progress to frontend
    // ══════════════════════════════════════════════════════════

    private Task SendSSE(string type, object? payload)
        => SseHelpers.SendSSE(Response, type, payload);

    private Task SendProgress(string stage, string status, string label, string? detail = null)
        => SseHelpers.SendProgress(Response, stage, status, label, detail);

    private Task SendError(string stage, string message)
        => SseHelpers.SendError(Response, stage, message);

    private Task SendResult(FullEvaluationResult result)
        => SendSSE("result", result);

    private void SetupSSEHeaders()
        => SseHelpers.SetupSSEHeaders(Response);

    /// <summary>
    /// SSE streaming PDF evaluation — sends real-time stage progress.
    /// </summary>
    [HttpPost("stream")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EnableRateLimiting("valuation")]
    public async Task EvaluateStream(
        IFormFile? file,
        [FromForm] string? city = null,
        [FromForm] string? neighborhood = null,
        [FromForm] string? email = null)
    {
        SetupSSEHeaders();
        var ct = HttpContext.RequestAborted;

        if (file is null || file.Length == 0)
        {
            await SendError("validation", "Nije učitana datoteka.");
            return;
        }

        try
        {
            _logger.LogInformation("\n\n" +
                "╔══════════════════════════════════════════════════════════════╗\n" +
                "║          🔵  NOVA PROCJENA — PDF UPLOAD                     ║\n" +
                "║  Datoteka: {FileName}  ({Size} bytes)                       ║\n" +
                "╚══════════════════════════════════════════════════════════════╝",
                file.FileName, file.Length);

            // ── Stage 1: PDF extraction ──
            _logger.LogInformation("━━━ STAGE 1: PDF EKSTRAKCIJA ━━━");
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

            await RunStreamingPipelineAsync(property, pdfText, email, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected during PDF evaluation");
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
    [EnableRateLimiting("valuation")]
    public async Task EvaluateTextStream([FromBody] TextEvaluationRequest request)
    {
        SetupSSEHeaders();
        var ct = HttpContext.RequestAborted;

        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            await SendError("validation", "Tekst nekretnine je prazan.");
            return;
        }

        try
        {
            _logger.LogInformation("\n\n" +
                "╔══════════════════════════════════════════════════════════════╗\n" +
                "║          🟢  NOVA PROCJENA — WIZARD TEXT                    ║\n" +
                "║  Tekst: {TextPreview}...                                    ║\n" +
                "║  City: {City} | Neighborhood: {Neighborhood}                ║\n" +
                "╚══════════════════════════════════════════════════════════════╝",
                request.Text?.Length > 80 ? request.Text[..80] : request.Text,
                request.City ?? "(none)", request.Neighborhood ?? "(none)");

            _logger.LogInformation("━━━ STAGE 1: AI EKSTRAKCIJA TEKSTA ━━━");
            await SendProgress("reading", "active", "Analiziram uneseni tekst...");

            ExtractedPropertyInfo property;
            try
            {
                property = await _valuationService.ExtractPropertyInfoAsync(request.Text);
                ApplyLocationOverride(property, request.City, request.Neighborhood);

                // Safety net: if the original text clearly says one land type
                // but GPT returned a different one, override.
                var textLower = request.Text.ToLowerInvariant();
                if (textLower.Contains("poljoprivredno") &&
                    property.Description != null &&
                    !property.Description.Contains("Poljoprivredno", StringComparison.OrdinalIgnoreCase))
                {
                    property.Description = property.Description
                        .Replace("Građevinsko zemljište", "Poljoprivredno zemljište", StringComparison.OrdinalIgnoreCase)
                        .Replace("Mješovito zemljište", "Poljoprivredno zemljište", StringComparison.OrdinalIgnoreCase);
                    if (!property.Description.Contains("Poljoprivredno", StringComparison.OrdinalIgnoreCase))
                        property.Description = "Poljoprivredno zemljište";
                }
                else if (textLower.Contains("mješovito") &&
                         property.Description != null &&
                         !property.Description.Contains("Mješovito", StringComparison.OrdinalIgnoreCase))
                {
                    property.Description = property.Description
                        .Replace("Građevinsko zemljište", "Mješovito zemljište", StringComparison.OrdinalIgnoreCase)
                        .Replace("Poljoprivredno zemljište", "Mješovito zemljište", StringComparison.OrdinalIgnoreCase);
                    if (!property.Description.Contains("Mješovito", StringComparison.OrdinalIgnoreCase))
                        property.Description = "Mješovito zemljište";
                }

                // Safety net: house type — if text clearly says kuća/katnica but
                // GPT returned Stan, override to ensure correct search type.
                bool textIsHouse = textLower.Contains("kuća") || textLower.Contains("kuca")
                    || textLower.Contains("katnica") || textLower.Contains("višekatni")
                    || textLower.Contains("visekatni") || textLower.Contains("vikendica")
                    || (textLower.Contains("okućnica") && !textLower.Contains("stan"));
                bool descIsApartment = property.Description != null
                    && (property.Description.Contains("Stan", StringComparison.OrdinalIgnoreCase)
                        || property.Description.Contains("stan", StringComparison.Ordinal))
                    && !property.Description.Contains("Kuća", StringComparison.OrdinalIgnoreCase)
                    && !property.Description.Contains("kuća", StringComparison.OrdinalIgnoreCase);

                if (textIsHouse && descIsApartment && property.Description != null)
                {
                    _logger.LogWarning(
                        "House type safety net: text says kuća/katnica but AI returned '{Desc}' — overriding",
                        property.Description);
                    // Replace "Stan" prefix with "Kuća" while keeping the rest
                    property.Description = System.Text.RegularExpressions.Regex.Replace(
                        property.Description,
                        @"^(Jednosoban|Dvosoban|Trosoban|Četverosoban|\d+-soban)\s+stan",
                        "Kuća",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!property.Description.Contains("Kuća", StringComparison.OrdinalIgnoreCase)
                        && !property.Description.Contains("kuća", StringComparison.OrdinalIgnoreCase))
                        property.Description = "Kuća " + property.Description;
                }

                // Safety net: apartment type — if text clearly says stan but
                // GPT returned Kuća, override.
                bool textIsApartment = (textLower.Contains("stan ") || textLower.Contains("stan,")
                    || textLower.Contains("-soban stan"))
                    && !textLower.Contains("kuća") && !textLower.Contains("kuca")
                    && !textLower.Contains("katnica");
                bool descIsHouse = property.Description != null
                    && (property.Description.Contains("Kuća", StringComparison.OrdinalIgnoreCase)
                        || property.Description.Contains("kuća", StringComparison.OrdinalIgnoreCase))
                    && !property.Description.Contains("Stan", StringComparison.OrdinalIgnoreCase);

                if (textIsApartment && descIsHouse && property.Description != null)
                {
                    _logger.LogWarning(
                        "Apartment type safety net: text says stan but AI returned '{Desc}' — overriding",
                        property.Description);
                    property.Description = System.Text.RegularExpressions.Regex.Replace(
                        property.Description,
                        @"^Kuća\b",
                        "Stan",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!property.Description.Contains("Stan", StringComparison.OrdinalIgnoreCase))
                        property.Description = "Stan " + property.Description;
                }
            }
            catch (Exception ex)
            {
                await SendProgress("reading", "error", "Analiza teksta", ex.Message);
                await SendError("reading", $"Greška: {ex.Message}");
                return;
            }

            await SendProgress("reading", "done", "Tekst analiziran",
                $"{property.Description}, {property.SizeM2} m², {property.Location}");

            _logger.LogInformation("    ✅ STAGE 1 DONE: Opis={Description}, Površina={Size}m², Lokacija={Location}",
                property.Description, property.SizeM2, property.Location);

            // V22: Log additional information fields
            if (!string.IsNullOrEmpty(property.AdditionalNotes))
                _logger.LogInformation("    📝 Dodatne informacije: {Notes}", property.AdditionalNotes);
            if (property.KeyFeatures?.Count > 0)
                _logger.LogInformation("    🔑 Ključne značajke: [{Features}]", string.Join(", ", property.KeyFeatures));
            if (!string.IsNullOrEmpty(property.MicroLocation))
                _logger.LogInformation("    📍 Mikrolokacija: {MicroLoc}", property.MicroLocation);

            await RunStreamingPipelineAsync(property, request.Text, request.Email, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected during text evaluation");
        }
        catch (Exception ex)
        {
            await SendError("unknown", $"Neočekivana greška: {ex.Message}");
        }
    }

    /// <summary>
    /// Shared streaming pipeline: Stage 2 through result.
    /// </summary>
    private async Task RunStreamingPipelineAsync(ExtractedPropertyInfo property, string pdfText, string? email = null, CancellationToken ct = default)
    {
        // ── Stage 2: Market research ──
        _logger.LogInformation("━━━ STAGE 2: PRETRAGA TRŽIŠTA ━━━  Lokacija={Location}, Veličina={Size}m²",
            property.Location, property.SizeM2);
        await SendProgress("searching", "active", "Pretražujem Njuškalo i tržište nekretnina...");
        ct.ThrowIfCancellationRequested();

        MarketResearchResult marketData;
        try
        {
            marketData = await _marketResearchService
                .SearchComparables(property.Location, property.SizeM2, pdfText,
                    property.MicroLocation, property.KeyFeatures);
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

        _logger.LogInformation("    ✅ STAGE 2 DONE: {Active} aktivnih, {Regional} regionalnih, Tip={Type}, Faza={Phase}",
            marketData.ActiveListings.Count, marketData.RegionalData.Count,
            marketData.DetectedPropertyType, marketData.SearchPhaseUsed);
        foreach (var l in marketData.ActiveListings.Take(10))
            _logger.LogInformation("       📋 {Title} — {Url}", l.Title?.Length > 60 ? l.Title[..60] : l.Title, l.Url);

        // ── Stage 2.5: AI feature extraction ──
        _logger.LogInformation("━━━ STAGE 2.5: AI FEATURE EXTRACTION ━━━  {Count} oglasa za analizu",
            marketData.ActiveListings.Count);
        await SendProgress("ai_features", "active", "AI analizira značajke oglasa...");
        ct.ThrowIfCancellationRequested();
        try
        {
            await _valuationService.ExtractListingFeaturesAsync(marketData.ActiveListings, property);
            _valuationService.FilterByAiPropertyType(
                marketData.ActiveListings, marketData.DetectedPropertyType);

            // Also type-filter RegionalData so AllResults (used by engine) doesn't
            // mix in wrong-type prices (e.g. construction land in agricultural valuation)
            _valuationService.FilterByAiPropertyType(
                marketData.RegionalData, marketData.DetectedPropertyType);

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
        _logger.LogInformation("━━━ STAGE 3A: DETERMINISTIC ENGINE ━━━  Tip={Type}, Oglasi={Count}",
            marketData.DetectedPropertyType, marketData.ActiveListings.Count);
        await SendProgress("engine", "active", "Engine izračunava tržišnu vrijednost...");
        ct.ThrowIfCancellationRequested();

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

        _logger.LogInformation("    ✅ STAGE 3A DONE: Vrijednost={Value}€, Cijena/m²={PriceM2}€, Usable={Usable}, Outliers={Outliers}, Confidence={Conf}({Score}/100)",
            engineResult.EstimatedValueEur, engineResult.CalculatedPricePerM2,
            engineResult.UsablePricePoints, engineResult.OutliersRemoved,
            engineResult.MathConfidenceLevel, engineResult.MathConfidenceScore);
        if (engineResult.UsedComparables != null)
            foreach (var c in engineResult.UsedComparables)
                _logger.LogInformation("       {Outlier} {Title} — {Price}€/m² sim={Sim}",
                    c.IsOutlier ? "❌ OUTLIER" : "✅ USED",
                    c.Title?.Length > 50 ? c.Title[..50] : c.Title,
                    c.PricePerM2, c.SimilarityScore);

        // ── Sync filter ──
        if (engineResult.FinalUsedUrls.Count > 0)
        {
            var filtered = marketData.ActiveListings
                .Where(l => engineResult.FinalUsedUrls.Contains(l.Url)).ToList();
            if (filtered.Count > 0)
                marketData.ActiveListings = filtered;
        }

        // ── Stage 3B: AI opinion ──
        _logger.LogInformation("━━━ STAGE 3B: AI MIŠLJENJE ━━━  EngineValue={Value}€, Comps={Comps}",
            engineResult.EstimatedValueEur, engineResult.UsablePricePoints);
        await SendProgress("ai_opinion", "active", "AI generira stručno mišljenje...");
        ct.ThrowIfCancellationRequested();

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
                finalReasoning = SseHelpers.PatchReasoningValue(
                    aiResult.ValuationReasoning, aiResult.EstimatedValueEur, engineResult.EstimatedValueEur);
        }
        else if (engineResult.EstimatedValueEur <= 0 && aiResult.EstimatedValueEur > 0)
        {
            // Engine had 0 usable comparables — fall back to AI estimate
            finalValue = (int)Math.Round(aiResult.EstimatedValueEur);
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
            AfterDiscountPerM2 = engineResult.AfterDiscountPerM2,
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

            // V14: Structured valuation output
            ComparisonTable = SseHelpers.BuildComparisonTable(engineResult, engineResult.TotalAdjustmentPercent),
            PriceRange = SseHelpers.BuildPriceRange(engineResult, property.SizeM2),
            BenchmarkSummary = SseHelpers.BuildBenchmarkSummary(engineResult, marketData),
        };

        // ── Save to Supabase ──
        _logger.LogInformation("━━━ STAGE 4: SPREMANJE REZULTATA ━━━  FinalValue={Value}€", finalValue);
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
