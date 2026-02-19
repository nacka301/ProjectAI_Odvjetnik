using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Second AI layer — uses Perplexity API (sonar model with web search)
/// to automatically audit/verify completed property valuations.
/// 
/// Perplexity is ideal here because it can cross-reference the valuation
/// against real-time market data, news, and public listings.
/// </summary>
public class PerplexityAuditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PerplexityAuditService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly bool _isConfigured;

    private const string PerplexityApiUrl = "https://api.perplexity.ai/chat/completions";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public PerplexityAuditService(HttpClient httpClient, IConfiguration config, ILogger<PerplexityAuditService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY")
            ?? config["Perplexity:ApiKey"];

        _model = config["Perplexity:Model"] ?? "sonar-pro";

        _isConfigured = !string.IsNullOrWhiteSpace(_apiKey)
            && _apiKey != "YOUR_PERPLEXITY_API_KEY_HERE";

        if (!_isConfigured)
        {
            _logger.LogWarning("Perplexity API is not configured. Audit layer is disabled. " +
                "Set PERPLEXITY_API_KEY environment variable or Perplexity:ApiKey in appsettings.json.");
        }
    }

    public bool IsConfigured => _isConfigured;

    /// <summary>
    /// Audit a completed valuation using Perplexity's web-search-augmented AI.
    /// Returns a structured audit result with verdict, dimension scores, and findings.
    /// </summary>
    public async Task<ValuationAuditResult> AuditValuationAsync(
        FullEvaluationResult valuation,
        string evaluationId,
        CancellationToken ct = default)
    {
        if (!_isConfigured)
        {
            return CreateDisabledResult(evaluationId);
        }

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(valuation);

            _logger.LogInformation("Starting Perplexity audit for evaluation {Id}, location: {Location}",
                evaluationId, valuation.RegionName);

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
                max_tokens = 4000,
                return_citations = true
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOpts);
            var request = new HttpRequestMessage(HttpMethod.Post, PerplexityApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Truncate error body to avoid logging sensitive data
                var safeBody = responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody;
                _logger.LogError("Perplexity API error ({Status}): {Body}",
                    response.StatusCode, safeBody);

                return new ValuationAuditResult
                {
                    EvaluationId = evaluationId,
                    Verdict = AuditVerdict.SUSPICIOUS,
                    OverallConfidence = 0,
                    Summary = $"Audit nije uspio — Perplexity API greška: {response.StatusCode}",
                    Recommendation = "Ponovite audit ili pregledajte ručno.",
                    ModelUsed = _model,
                    AuditedAt = DateTime.UtcNow
                };
            }

            return ParsePerplexityResponse(responseBody, evaluationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Perplexity audit failed for evaluation {Id}", evaluationId);

            return new ValuationAuditResult
            {
                EvaluationId = evaluationId,
                Verdict = AuditVerdict.SUSPICIOUS,
                OverallConfidence = 0,
                Summary = $"Audit nije uspio — iznimka: {ex.Message}",
                Recommendation = "Ponovite audit ili pregledajte ručno.",
                ModelUsed = _model,
                AuditedAt = DateTime.UtcNow
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Prompt Construction
    // ═══════════════════════════════════════════════════════════

    private static string BuildSystemPrompt()
    {
        return """
            Ti si stručni AI revizor za procjene nekretnina u Hrvatskoj.
            Tvoj zadatak je provjeriti gotovu procjenu nekretnine i dati objektivnu ocjenu.

            Za svaku procjenu trebaš:
            1. PROVJERA CIJENE — Je li cijena po m² realistična za tu lokaciju i tip nekretnine?
               Koristi web pretragu za provjeru trenutnih cijena na njuškalo.hr, index.hr/oglasi, i drugim portalima.
            2. PROVJERA LOKACIJE — Je li lokacija točno klasificirana? Postoji li taj kvart/ulica?
            3. PROVJERA METODOLOGIJE — Je li korišteno dovoljno usporedivih nekretnina (min. 3)?
               Je li diskont za realizaciju razuman (10-25% za stanove, 15-35% za kuće)?
            4. KVALITETA PODATAKA — Koliko je podataka korišteno? Jesu li outlieri pravilno uklonjeni?

            ODGOVORI ISKLJUČIVO u ovom JSON formatu (bez markdown blokova, samo čist JSON):
            {
                "verdict": "CONFIRMED" | "SUSPICIOUS" | "REJECTED",
                "overall_confidence": 0-100,
                "summary": "Kratki sažetak na hrvatskom (2-3 rečenice)",
                "recommendation": "Preporuka za korisnika na hrvatskom",
                "decision_reasoning": "Detaljno obrazloženje odluke na hrvatskom — objasni korak po korak KAKO si došao do verdikta i ZAŠTO. Navedi: (1) koje si izvore pretražio, (2) koje cijene si pronašao na tržištu, (3) što to znači za ovu procjenu, (4) koji su ključni razlozi za/protiv procjene, (5) zašto si odabrao baš taj verdikt. Budi konkretan — navedi brojke, URL-ove i usporedbe. Minimalno 5-8 rečenica.",
                "price_check": {
                    "score": 0-100,
                    "label": "kratki label",
                    "reasoning": "obrazloženje na hrvatskom"
                },
                "location_check": {
                    "score": 0-100,
                    "label": "kratki label",
                    "reasoning": "obrazloženje"
                },
                "methodology_check": {
                    "score": 0-100,
                    "label": "kratki label",
                    "reasoning": "obrazloženje"
                },
                "data_quality_check": {
                    "score": 0-100,
                    "label": "kratki label",
                    "reasoning": "obrazloženje"
                },
                "findings": [
                    {
                        "category": "CIJENA" | "LOKACIJA" | "METODOLOGIJA" | "PODACI",
                        "severity": "INFO" | "WARNING" | "CRITICAL",
                        "description": "opis nalaza",
                        "evidence": "izvor ili dokaz (URL ili broj)"
                    }
                ],
                "market_context": "Kratki prikaz tržišnog konteksta — što Perplexity pronađe o cijenama na toj lokaciji"
            }

            Pravila za verdikt:
            - CONFIRMED: Svi dimenzijski score-ovi >= 60, nema CRITICAL nalaza
            - SUSPICIOUS: Barem jedan score < 60 ILI postoji 1 CRITICAL nalaz
            - REJECTED: 2+ score-a < 40 ILI 2+ CRITICAL nalaza

            Budi precizan i konstruktivan. Koristi hrvatske izvore kad god je moguće.
            """;
    }

    private static string BuildUserPrompt(FullEvaluationResult v)
    {
        var usedComps = v.UsedComparables?.Count ?? 0;
        var comparableDetails = "";

        if (v.UsedComparables is { Count: > 0 })
        {
            var sb = new StringBuilder();
            sb.AppendLine("Korištene usporedive nekretnine:");
            for (int i = 0; i < Math.Min(v.UsedComparables.Count, 10); i++)
            {
                var c = v.UsedComparables[i];
                sb.AppendLine($"  {i + 1}. {c.Title} — {c.PricePerM2:F0} €/m² (ukupno: {c.TotalPrice:F0} €, {c.AreaM2:F0} m²)");
            }
            comparableDetails = sb.ToString();
        }

        var adjustmentDetails = "";
        if (v.Adjustments is { Count: > 0 })
        {
            var sb = new StringBuilder();
            sb.AppendLine("Primijenjene korekcije:");
            foreach (var adj in v.Adjustments)
            {
                sb.AppendLine($"  - {adj.Factor}: {adj.Percent:+0.0;-0.0}%");
            }
            adjustmentDetails = sb.ToString();
        }

        var priceRangeInfo = "";
        if (v.PriceRange != null)
        {
            priceRangeInfo = $"""
                Raspon procjene:
                  Konzervativno: {v.PriceRange.ConservativeTotal:N0} € ({v.PriceRange.ConservativePricePerM2:F0} €/m²)
                  Realistično: {v.PriceRange.RealisticTotal:N0} € ({v.PriceRange.RealisticPricePerM2:F0} €/m²)
                  Optimistično: {v.PriceRange.OptimisticTotal:N0} € ({v.PriceRange.OptimisticPricePerM2:F0} €/m²)
                """;
        }

        return $"""
            PROCJENA ZA REVIZIJU:

            Lokacija: {v.RegionName}
            Tip nekretnine: {v.TargetPropertySummary}
            Procijenjena vrijednost: {v.EstimatedValueEur:N0} €
            Cijena po m²: {v.CalculatedPricePerM2:F0} €/m²
            Prosječna tržišna cijena: {v.MarketAverageAsking:F0} €/m² (asking)
            Diskont za realizaciju: {v.RealizationDiscountPercent:F1}%
            Ukupna korekcija: {v.TotalAdjustmentPercent:+0.0;-0.0}%

            Pouzdanost podataka:
              Math confidence: {v.MathConfidenceScore}/100 ({v.MathConfidenceLevel})
              Pronađeno oglasa: {v.TotalListingsFound}
              Korištenih cijena: {v.UsablePricePoints}
              Uklonjeni outlieri: {v.OutliersRemoved}
              Koeficijent varijacije: {v.CoefficientOfVariation:F2}
              Standardna devijacija: {v.StdDevPerM2:F0} €/m²

            {priceRangeInfo}

            {comparableDetails}

            {adjustmentDetails}

            Stručno mišljenje AI-a:
            {v.ValuationReasoning}

            Benchmark: {v.BenchmarkSummary}
            Sherlock uvid: {v.SherlockInsight}

            Molim provjeri ovu procjenu prema svim kriterijima i daj strukturiranu ocjenu.
            """;
    }

    // ═══════════════════════════════════════════════════════════
    //  Response Parsing
    // ═══════════════════════════════════════════════════════════

    private ValuationAuditResult ParsePerplexityResponse(string responseBody, string evaluationId)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Extract the assistant message content
            var content = root.GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // Extract citations/sources if available
            var sourceUrls = new List<string>();
            if (root.TryGetProperty("citations", out var citations) && citations.ValueKind == JsonValueKind.Array)
            {
                foreach (var citation in citations.EnumerateArray())
                {
                    var url = citation.GetString();
                    if (!string.IsNullOrEmpty(url))
                        sourceUrls.Add(url);
                }
            }

            // Extract token usage
            int promptTokens = 0, completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt))
                    promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct))
                    completionTokens = ct.GetInt32();
            }

            // Clean markdown code fences if present
            content = content.Trim();
            if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                content = content[7..];
            else if (content.StartsWith("```"))
                content = content[3..];
            if (content.EndsWith("```"))
                content = content[..^3];
            content = content.Trim();

            // Parse the structured JSON from the AI response
            var audit = JsonSerializer.Deserialize<ValuationAuditResult>(content, JsonOpts);

            if (audit == null)
            {
                _logger.LogWarning("Perplexity returned unparseable audit for {Id}", evaluationId);
                return CreateFallbackResult(evaluationId, content);
            }

            // Enrich with metadata
            audit.EvaluationId = evaluationId;
            audit.AuditedAt = DateTime.UtcNow;
            audit.ModelUsed = _model;
            audit.PromptTokens = promptTokens;
            audit.CompletionTokens = completionTokens;
            if (sourceUrls.Count > 0)
                audit.SourceUrls = sourceUrls;

            _logger.LogInformation(
                "Audit complete for {Id}: verdict={Verdict}, confidence={Confidence}, findings={Count}",
                evaluationId, audit.Verdict, audit.OverallConfidence, audit.Findings.Count);

            return audit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Perplexity audit response for {Id}", evaluationId);
            return CreateFallbackResult(evaluationId, "Parse error: " + ex.Message);
        }
    }

    private ValuationAuditResult CreateDisabledResult(string evaluationId)
    {
        return new ValuationAuditResult
        {
            EvaluationId = evaluationId,
            Verdict = AuditVerdict.SUSPICIOUS,
            OverallConfidence = 0,
            Summary = "Perplexity API nije konfiguriran. Audit sloj je onemogućen.",
            Recommendation = "Konfigurirajte PERPLEXITY_API_KEY za automatsku verifikaciju procjena.",
            ModelUsed = "none",
            AuditedAt = DateTime.UtcNow
        };
    }

    private ValuationAuditResult CreateFallbackResult(string evaluationId, string rawContent)
    {
        return new ValuationAuditResult
        {
            EvaluationId = evaluationId,
            Verdict = AuditVerdict.SUSPICIOUS,
            OverallConfidence = 0,
            Summary = "Audit je izvršen ali odgovor nije u očekivanom formatu.",
            Recommendation = "Pregledajte procjenu ručno.",
            MarketContext = rawContent.Length > 500 ? rawContent[..500] + "..." : rawContent,
            ModelUsed = _model,
            AuditedAt = DateTime.UtcNow
        };
    }
}
