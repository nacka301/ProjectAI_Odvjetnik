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
    private readonly TextAnalysisService _textAnalysisService;
    private readonly ValuationAgentService _valuationAgentService;
    private readonly ILogger<EvaluateController> _logger;

    public EvaluateController(
        ValuationService valuationService,
        PdfService pdfService,
        MarketResearchService marketResearchService,
        ValuationEngine valuationEngine,
        SupabaseService supabaseService,
        EmailService emailService,
        TextAnalysisService textAnalysisService,
        ValuationAgentService valuationAgentService,
        ILogger<EvaluateController> logger)
    {
        _valuationService = valuationService;
        _pdfService = pdfService;
        _marketResearchService = marketResearchService;
        _valuationEngine = valuationEngine;
        _supabaseService = supabaseService;
        _emailService = emailService;
        _textAnalysisService = textAnalysisService;
        _valuationAgentService = valuationAgentService;
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

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await SendError("validation", "Samo PDF datoteke su podržane.");
            return;
        }

        try
        {
            // Sanitize filename for logging (prevent log injection)
            var safeFileName = Path.GetFileName(file.FileName ?? "unknown")
                .Replace("\n", "").Replace("\r", "");

            _logger.LogInformation("\n\n" +
                "╔══════════════════════════════════════════════════════════════╗\n" +
                "║          🔵  NOVA PROCJENA — PDF UPLOAD                     ║\n" +
                "║  Datoteka: {FileName}  ({Size} bytes)                       ║\n" +
                "╚══════════════════════════════════════════════════════════════╝",
                safeFileName, file.Length);

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
                _logger.LogError(ex, "PDF reading failed");
                await SendProgress("reading", "error", "Čitanje PDF-a", "Greška pri čitanju PDF-a");
                await SendError("reading", "Greška pri čitanju PDF-a. Provjerite format datoteke.");
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
            _logger.LogError(ex, "Unexpected error during PDF evaluation");
            if (!HttpContext.RequestAborted.IsCancellationRequested)
                await SendError("unknown", "Neočekivana greška pri obradi. Pokušajte ponovno.");
        }
    }

    /// <summary>
    /// SSE streaming text evaluation — sends real-time stage progress.
    /// </summary>
    [HttpPost("text/stream")]
    [RequestSizeLimit(2 * 1024 * 1024)]
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
                _logger.LogError(ex, "Text analysis failed");
                await SendProgress("reading", "error", "Analiza teksta", "Greška pri analizi teksta");
                await SendError("reading", "Greška pri analizi teksta. Pokušajte ponovno.");
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
            _logger.LogError(ex, "Unexpected error during text evaluation");
            if (!HttpContext.RequestAborted.IsCancellationRequested)
                await SendError("unknown", "Neočekivana greška pri obradi. Pokušajte ponovno.");
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
            _logger.LogError(ex, "Market search failed");
            await SendProgress("searching", "error", "Pretraga tržišta", "Greška pri pretrazi");
            await SendError("searching", "Greška pri pretrazi tržišta. Pokušajte ponovno.");
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

        // ── Stage 2.7: Sherlock — Semantic Description Analysis ──
        _logger.LogInformation("━━━ STAGE 2.7: SHERLOCK — TEXT ANALYSIS ━━━  {Count} oglasa",
            marketData.ActiveListings.Count);
        await SendProgress("sherlock", "active", "Sherlock analizira opise oglasa...");
        ct.ThrowIfCancellationRequested();
        try
        {
            // Build batch of listings that have deep-dived content (meaningful text to analyze)
            var toAnalyze = marketData.ActiveListings
                .Select((l, i) => (Index: i, Listing: l))
                .Where(x => x.Listing.Content.Length > 100)
                .Select(x => (x.Index, x.Listing.Content))
                .ToList();

            if (toAnalyze.Count > 0)
            {
                var sherlockResults = await _textAnalysisService.AnalyzeBatchAsync(toAnalyze);
                foreach (var (idx, attrs) in sherlockResults)
                {
                    if (idx >= 0 && idx < marketData.ActiveListings.Count)
                        marketData.ActiveListings[idx].SherlockAttributes = attrs;
                }

                int analyzed = sherlockResults.Count;
                int urgent = sherlockResults.Values.Count(a => a.IsUrgent);
                int landOverrides = sherlockResults.Values.Count(a => a.ActualLandType != Backend.Models.ActualLandType.Unknown);
                await SendProgress("sherlock", "done", "Sherlock analiza gotova",
                    $"{analyzed} analizirano • {urgent} hitno • {landOverrides} tip zemljišta");

                _logger.LogInformation("    ✅ STAGE 2.7 DONE: {Analyzed} analyzed, {Urgent} urgent, {LandOverrides} land-type overrides",
                    analyzed, urgent, landOverrides);
            }
            else
            {
                await SendProgress("sherlock", "skipped", "Sherlock preskočen", "Nema dovoljno teksta za analizu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sherlock analysis failed — continuing without");
            await SendProgress("sherlock", "skipped", "Sherlock preskočen", "Greška u analizi teksta");
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
            _logger.LogError(ex, "Valuation engine failed");
            await SendProgress("engine", "error", "Engine izračun", "Greška pri izračunu");
            await SendError("engine", "Greška pri izračunu vrijednosti. Pokušajte ponovno.");
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

        // ── Stage 3A½: AI Agent (God Prompt) ──
        // Contextual AI analysis of the SUBJECT property description.
        // Finds things keyword adjustments miss: shape constraints, zoning readiness,
        // infrastructure gaps, legal red flags, etc.
        try
        {
            _logger.LogInformation("━━━ STAGE 3A½: AI AGENT (GOD PROMPT) ━━━");
            await SendProgress("agent", "active", "AI agent analizira nekretninu...");
            ct.ThrowIfCancellationRequested();

            string descriptionText = $"{property.Description} {property.AdditionalNotes} {property.KeyFeatures}".Trim();
            double benchmarkPrice = engineResult.MarketAverageAsking > 0
                ? engineResult.MarketAverageAsking
                : engineResult.CalculatedPricePerM2;

            var agentAdjustments = await _valuationAgentService.AnalyzePropertyAsync(
                propertyType: marketData.DetectedPropertyType,
                sizeM2: property.SizeM2,
                location: property.Location,
                description: descriptionText,
                additionalNotes: property.RawText ?? "",
                benchmarkPricePerM2: benchmarkPrice,
                existingAdjustments: engineResult.Adjustments);

            if (agentAdjustments.Count > 0)
            {
                // Convert agent adjustments to ValuationAdjustment and append
                foreach (var agentAdj in agentAdjustments)
                {
                    engineResult.Adjustments.Add(new ValuationAdjustment
                    {
                        Factor = $"🤖 {agentAdj.Factor}",
                        Description = agentAdj.Reasoning,
                        Percent = agentAdj.ImpactPercent
                    });
                    _logger.LogInformation("    🤖 Agent: {Factor} → {Pct:+0.0;-0.0}%  {Reason}",
                        agentAdj.Factor, agentAdj.ImpactPercent,
                        agentAdj.Reasoning.Length > 80 ? agentAdj.Reasoning[..80] + "…" : agentAdj.Reasoning);
                }

                // Recalculate TotalAdjustmentPercent including agent adjustments
                double newTotalAdj = engineResult.Adjustments.Sum(a => a.Percent);
                // Cap at ±40%
                newTotalAdj = Math.Clamp(newTotalAdj, -40, 40);
                double oldTotal = engineResult.TotalAdjustmentPercent;
                engineResult.TotalAdjustmentPercent = Math.Round(newTotalAdj, 1);

                // Recalculate price: start from AfterDiscountPerM2 (post-realization-gap)
                // and apply the updated total adjustment
                double basePrice = engineResult.AfterDiscountPerM2 > 0
                    ? engineResult.AfterDiscountPerM2
                    : engineResult.MarketAverageAsking;
                double newPricePerM2 = basePrice * (1 + newTotalAdj / 100.0);
                engineResult.CalculatedPricePerM2 = Math.Round(newPricePerM2, 0);
                engineResult.EstimatedValueEur = (int)Math.Round(newPricePerM2 * property.SizeM2);

                engineResult.Pipeline.Add($"Agent: {agentAdjustments.Count} contextual adjustments → total adj {oldTotal:+0.0;-0.0}% → {newTotalAdj:+0.0;-0.0}%");

                _logger.LogInformation("    🤖 Agent recalc: TotalAdj={Adj}%, PriceM2={Price}€, Value={Val}€",
                    newTotalAdj, engineResult.CalculatedPricePerM2, engineResult.EstimatedValueEur);

                await SendProgress("agent", "done", "AI agent završio",
                    $"{agentAdjustments.Count} korekcija • ukupno {newTotalAdj:+0.0;-0.0}%");
            }
            else
            {
                engineResult.Pipeline.Add("Agent: no additional contextual adjustments found");
                await SendProgress("agent", "done", "AI agent završio", "Nema dodatnih korekcija");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent analysis failed — continuing without");
            await SendProgress("agent", "skipped", "AI agent preskočen", "Greška u analizi");
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
            _logger.LogError(ex, "AI opinion generation failed");
            await SendProgress("ai_opinion", "error", "AI mišljenje", "Greška pri generiranju mišljenja");
            await SendError("ai_opinion", "Greška pri generiranju AI mišljenja. Pokušajte ponovno.");
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
            // V39: Engine had 0 usable comps — DO NOT blindly trust AI estimate.
            // The AI gets ALL raw listings (including cross-city junk) and can hallucinate.
            // Only accept AI value if engine had SOME comps that just scored low.
            if (engineResult.UsablePricePoints > 0)
            {
                finalValue = (int)Math.Round(aiResult.EstimatedValueEur);
                _logger.LogWarning("V39: Engine value=0 but {Comps} usable points — using AI fallback {Value}€",
                    engineResult.UsablePricePoints, finalValue);
            }
            else
            {
                _logger.LogWarning(
                    "V39: Engine=0€ with 0 usable comps — AI suggested {AiValue}€ but REJECTING (no valid comparables)",
                    aiResult.EstimatedValueEur);
                finalValue = 0; // Signal to frontend: no valid valuation possible
                finalReasoning = "UPOZORENJE: Nije pronađen nijedan valjan usporedivi oglas za ovu nekretninu u zadanoj lokaciji. " +
                    "Sustav ne može izraditi pouzdanu procjenu tržišne vrijednosti bez relevantnih usporedivih prodaja. " +
                    "Preporučamo angažiranje ovlaštenog sudskog vještaka za procjenu vrijednosti.";
            }
        }

        // ── V29: Enrich engine comps with AI titles/URLs ──
        // The engine's UsedComparables often have raw scraping titles ("Nekretnine-AD - Njuškalo")
        // while the AI's SelectedComparables have proper human-readable titles.
        // Cross-match by URL and adopt the AI's Title + Url when matched.
        if (engineResult.UsedComparables?.Count > 0 && aiResult.SelectedComparables?.Count > 0)
        {
            foreach (var engineComp in engineResult.UsedComparables)
            {
                // Try to find a matching AI comparable by URL overlap
                var aiMatch = aiResult.SelectedComparables.FirstOrDefault(ai =>
                    !string.IsNullOrEmpty(ai.Url) && !string.IsNullOrEmpty(engineComp.Url) &&
                    (ai.Url == engineComp.Url ||
                     ai.Url.Contains(engineComp.Url, StringComparison.OrdinalIgnoreCase) ||
                     engineComp.Url.Contains(ai.Url, StringComparison.OrdinalIgnoreCase)));

                if (aiMatch != null && !string.IsNullOrWhiteSpace(aiMatch.Title))
                {
                    _logger.LogInformation("V29: Engine comp '{Old}' → AI title '{New}'",
                        engineComp.Title?.Length > 40 ? engineComp.Title[..40] : engineComp.Title,
                        aiMatch.Title.Length > 40 ? aiMatch.Title[..40] : aiMatch.Title);
                    engineComp.Title = aiMatch.Title;
                    if (!string.IsNullOrEmpty(aiMatch.Url))
                        engineComp.Url = aiMatch.Url;
                }
            }
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

            // Audit log
            try
            {
                await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
                {
                    Module = "valuation",
                    AnalysisId = evaluationId,
                    Step = 1,
                    Action = $"Procjena nekretnine: {marketData.DetectedPropertyType}, {property.Location}, {property.SizeM2} m², vrijednost={finalValue}€",
                    LegalBasis = "Zakon o procjeni vrijednosti nekretnina (NN 78/15)",
                    Citation = "čl. 4., čl. 19., čl. 24. ZPVN",
                    Reasoning = $"Engine: {engineResult.CalculatedPricePerM2:N0}€/m², {engineResult.UsablePricePoints} usporedivih. AI: {aiResult.EstimatedValueEur:N0}€. Pouzdanost: {engineResult.ConfidenceLevel}.",
                    Confidence = engineResult.ConfidenceLevel == "HIGH" ? "VISOKO" : engineResult.ConfidenceLevel == "MEDIUM" ? "SREDNJE" : "NISKO",
                    ModelUsed = "openai/gpt-4o",
                    ModelTemperature = 0.10
                });
            }
            catch { /* non-critical */ }
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
