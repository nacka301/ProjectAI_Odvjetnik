using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;

namespace Backend.Services;

/// <summary>
/// Uses Perplexity AI (sonar-pro with web search) to validate test scenario
/// results against real Croatian market data.
/// 
/// For each scenario: receives the property input, market comparables, and
/// engine output — then asks Perplexity to judge whether the result is realistic.
/// </summary>
public class PerplexityTestValidationService
:IPerplexityTestValidationService {
    private readonly HttpClient _httpClient;
    private readonly ILogger<PerplexityTestValidationService> _logger;
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

    public PerplexityTestValidationService(
        HttpClient httpClient, IConfiguration config,
        ILogger<PerplexityTestValidationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY")
            ?? config["Perplexity:ApiKey"];
        _model = config["Perplexity:Model"] ?? "sonar-pro";

        _isConfigured = !string.IsNullOrWhiteSpace(_apiKey)
            && _apiKey != "YOUR_PERPLEXITY_API_KEY_HERE";
    }

    public bool IsConfigured => _isConfigured;

    /// <summary>
    /// Validate a batch of test scenarios against real market data.
    /// Each scenario is sent to Perplexity individually for independent evaluation.
    /// </summary>
    public async Task<TestValidationReport> ValidateScenariosAsync(
        List<TestScenarioInput> scenarios, CancellationToken ct = default)
    {
        var report = new TestValidationReport
        {
            ModelUsed = _model,
            TotalScenarios = scenarios.Count,
            RunAt = DateTime.UtcNow
        };

        if (!_isConfigured)
        {
            _logger.LogWarning("Perplexity API not configured — cannot validate test scenarios.");
            return report;
        }

        foreach (var scenario in scenarios)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("Validating scenario: {Name}", scenario.Name);
                var verdict = await ValidateSingleScenarioAsync(scenario, ct);
                report.Scenarios.Add(verdict);

                switch (verdict.Verdict.ToUpperInvariant())
                {
                    case "PASS": report.Passed++; break;
                    case "WARN": report.Warnings++; break;
                    case "FAIL": report.Failed++; break;
                    default: report.Warnings++; break;
                }

                _logger.LogInformation("Scenario {Name}: {Verdict} (confidence: {Score})",
                    scenario.Name, verdict.Verdict, verdict.ConfidenceScore);

                // Rate limit: small delay between calls
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate scenario: {Name}", scenario.Name);
                report.Scenarios.Add(new TestScenarioVerdict
                {
                    ScenarioName = scenario.Name,
                    Verdict = "FAIL",
                    ConfidenceScore = 0,
                    Reasoning = $"Perplexity API greška: {ex.Message}",
                    Issues = [$"Iznimka: {ex.Message}"]
                });
                report.Failed++;
            }
        }

        return report;
    }

    /// <summary>
    /// Validate a single test scenario against Perplexity.
    /// </summary>
    private async Task<TestScenarioVerdict> ValidateSingleScenarioAsync(
        TestScenarioInput scenario, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(scenario);

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1,
            max_tokens = 3000,
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
            throw new HttpRequestException(
                $"Perplexity API error: {response.StatusCode} — {responseBody[..Math.Min(responseBody.Length, 200)]}");
        }

        return ParseResponse(responseBody, scenario.Name);
    }

    // ═══════════════════════════════════════════════════════════
    //  Prompt Construction  
    // ═══════════════════════════════════════════════════════════

    private static string BuildSystemPrompt()
    {
        return """
            Ti si stručni AI revizor za TESTIRANJE sustava procjene nekretnina u Hrvatskoj.
            
            Tvoj zadatak: za svaki test scenarij dobivaš ULAZNE PODATKE (opis nekretnine, lokacija, 
            tržišni podaci/usporedive nekretnine) i REZULTAT motora procjene (procijenjena vrijednost, 
            cijena po m², korekcije, itd.).
            
            Trebaš procijeniti:
            1. JE LI CIJENA REALISTIČNA za tu lokaciju, tip i stanje nekretnine?
               — Pretraži web za stvarne cijene na njuškalo.hr, index.hr/oglasi, crozilla.com
            2. JE LI CIJENA PO m² U OČEKIVANOM RASPONU za tu lokaciju u Hrvatskoj?
            3. JESU LI KOREKCIJE LOGIČNE? (npr. adaptacija → niža cijena, novogradnja → viša)
            4. IMA LI SMISLA ukupna procjena s obzirom na ulazne podatke?
            
            VERDIKT:
            - "PASS" = Rezultat je realan i prihvatljiv za hrvatsko tržište
            - "WARN" = Rezultat je na granici, ali prihvatljiv uz ograde
            - "FAIL" = Rezultat je nerealan ili potpuno kriv za tu lokaciju/tip
            
            ODGOVORI ISKLJUČIVO u ovom JSON formatu (bez markdown blokova):
            {
                "scenario_name": "ime scenarija",
                "verdict": "PASS" | "WARN" | "FAIL",
                "confidence_score": 0-100,
                "reasoning": "Detaljno obrazloženje na hrvatskom (min 3-5 rečenica). Objasni: (1) koje si stvarne cijene pronašao na webu, (2) kako se rezultat motora uspoređuje s tržištem, (3) zašto je verdikt baš takav.",
                "market_reference": "Stvarne cijene pronađene na webu za tu lokaciju — navedi izvore i brojke",
                "price_assessment": "Ocjena cijene — je li realna, preniska ili previsoka za tu lokaciju",
                "methodology_assessment": "Ocjena metodologije — jesu li korekcije logične, je li dovoljno usporedivih",
                "issues": ["lista pronađenih problema, ako ih ima"],
                "source_urls": ["URL-ovi korišteni za provjeru"]
            }
            
            Budi strog ali fer. Ako je cijena unutar ±25% od tržišne realnosti, to je PASS.
            Ako je unutar ±40%, to je WARN. Iznad toga je FAIL.
            """;
    }

    private static string BuildUserPrompt(TestScenarioInput s)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"═══ TEST SCENARIJ: {s.Name} ═══");
        sb.AppendLine();
        sb.AppendLine("── ULAZNI PODACI (nekretnina) ──");
        sb.AppendLine($"  Opis: {s.Property.Description}");
        sb.AppendLine($"  Lokacija: {s.Property.Location}");
        sb.AppendLine($"  Površina: {s.Property.SizeM2} m²");
        sb.AppendLine($"  Stanje: {s.Property.PropertyState}");
        if (s.Property.YardAreaM2 > 0)
            sb.AppendLine($"  Okućnica: {s.Property.YardAreaM2} m²");
        if (s.Property.KeyFeatures?.Count > 0)
            sb.AppendLine($"  Značajke: {string.Join(", ", s.Property.KeyFeatures)}");
        if (!string.IsNullOrWhiteSpace(s.Property.AdditionalNotes))
            sb.AppendLine($"  Napomene: {s.Property.AdditionalNotes}");

        sb.AppendLine();
        sb.AppendLine("── TRŽIŠNI PODACI (usporedive nekretnine) ──");
        sb.AppendLine($"  Regija: {s.Market.RegionName}");
        sb.AppendLine($"  Tip: {s.Market.DetectedPropertyType}");
        sb.AppendLine($"  Broj oglasa: {s.Market.ActiveListings?.Count ?? 0}");

        if (s.Market.ActiveListings is { Count: > 0 })
        {
            sb.AppendLine("  Usporedive nekretnine:");
            foreach (var comp in s.Market.ActiveListings.Take(10))
            {
                sb.AppendLine($"    - {comp.Title}: {comp.ListedPriceEur:N0} € " +
                    $"({comp.ListedPricePerM2:N0} €/m², {comp.AiSizeM2:N0} m², stanje: {comp.AiCondition ?? "?"})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("── REZULTAT MOTORA PROCJENE ──");
        sb.AppendLine($"  ★ Procijenjena vrijednost: {s.Result.EstimatedValueEur:N0} €");
        sb.AppendLine($"  ★ Cijena po m²: {s.Result.CalculatedPricePerM2:F0} €/m²");
        sb.AppendLine($"  Tržišni prosjek (asking): {s.Result.MarketAverageAsking:F0} €/m²");
        sb.AppendLine($"  Diskont za realizaciju: {s.Result.RealizationDiscountPercent:F1}%");
        sb.AppendLine($"  Ukupna korekcija: {s.Result.TotalAdjustmentPercent:+0.0;-0.0}%");
        sb.AppendLine($"  Korištenih cijena: {s.Result.UsablePricePoints}");
        sb.AppendLine($"  Uklonjeno outliera: {s.Result.OutliersRemoved}");
        sb.AppendLine($"  Pouzdanost: {s.Result.ConfidenceLevel}");
        sb.AppendLine($"  CV: {s.Result.CoefficientOfVariation:F2}");

        if (s.Result.Adjustments?.Count > 0)
        {
            sb.AppendLine("  Korekcije:");
            foreach (var adj in s.Result.Adjustments)
                sb.AppendLine($"    - {adj}");
        }

        if (!string.IsNullOrWhiteSpace(s.Result.SherlockInsight))
            sb.AppendLine($"  Sherlock: {s.Result.SherlockInsight}");

        sb.AppendLine();
        sb.AppendLine("Molim provjeri ovaj rezultat prema stvarnom hrvatskom tržištu nekretnina.");
        sb.AppendLine("Pretraži aktualne cijene za tu lokaciju i ocijeni je li procjena realistična.");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════
    //  Response Parsing
    // ═══════════════════════════════════════════════════════════

    private TestScenarioVerdict ParseResponse(string responseBody, string scenarioName)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        // Extract citations
        var sourceUrls = new List<string>();
        if (root.TryGetProperty("citations", out var citations) && citations.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in citations.EnumerateArray())
            {
                var url = c.GetString();
                if (!string.IsNullOrEmpty(url))
                    sourceUrls.Add(url);
            }
        }

        // Clean markdown code fences
        content = content.Trim();
        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            content = content[7..];
        else if (content.StartsWith("```"))
            content = content[3..];
        if (content.EndsWith("```"))
            content = content[..^3];
        content = content.Trim();

        try
        {
            var verdict = JsonSerializer.Deserialize<TestScenarioVerdict>(content, JsonOpts);
            if (verdict != null)
            {
                verdict.ScenarioName = scenarioName;
                if (sourceUrls.Count > 0)
                    verdict.SourceUrls = sourceUrls;
                return verdict;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Perplexity verdict JSON for {Name}", scenarioName);
        }

        // Fallback: couldn't parse structured response
        return new TestScenarioVerdict
        {
            ScenarioName = scenarioName,
            Verdict = "WARN",
            ConfidenceScore = 0,
            Reasoning = $"Perplexity odgovor nije u očekivanom formatu. Sirovi tekst: {content[..Math.Min(content.Length, 500)]}",
            SourceUrls = sourceUrls
        };
    }
}
