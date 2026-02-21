using System.Text.Json;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Perplexity AI Audit Layer — automatic verification of completed valuations.
/// Endpoints to audit single/batch valuations and retrieve results.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly PerplexityAuditService _auditService;
    private readonly PerplexityTestValidationService _testValidationService;
    private readonly ValuationEngine _valuationEngine;
    private readonly SupabaseService _supabaseService;
    private readonly ILogger<AuditController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    public AuditController(
        PerplexityAuditService auditService,
        PerplexityTestValidationService testValidationService,
        ValuationEngine valuationEngine,
        SupabaseService supabaseService,
        ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _testValidationService = testValidationService;
        _valuationEngine = valuationEngine;
        _supabaseService = supabaseService;
        _logger = logger;
    }

    // SECURITY: Extract user_id from x-user-id header (set by frontend auth)
    private string? GetUserId() =>
        Request.Headers.TryGetValue("x-user-id", out var val) ? val.FirstOrDefault() : null;

    /// <summary>
    /// Check if the Perplexity audit layer is configured and available.
    /// </summary>
    [HttpGet("status")]
    [EnableRateLimiting("general")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            enabled = _auditService.IsConfigured,
            model = _auditService.IsConfigured ? "sonar" : "none",
            message = _auditService.IsConfigured
                ? "Audit sloj je aktivan i spreman za verifikaciju procjena."
                : "Audit sloj nije konfiguriran. Postavite PERPLEXITY_API_KEY."
        });
    }

    /// <summary>
    /// Audit a single valuation by its Supabase evaluation ID.
    /// POST /api/audit/single
    /// </summary>
    [HttpPost("single")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> AuditSingle([FromBody] AuditRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EvaluationId))
            return BadRequest(new { error = "evaluation_id je obavezan." });

        if (!_auditService.IsConfigured)
            return StatusCode(503, new { error = "Perplexity API nije konfiguriran." });

        // Fetch the evaluation from Supabase
        var eval = await _supabaseService.GetEvaluationByIdAsync(request.EvaluationId);
        if (eval == null)
            return NotFound(new { error = $"Procjena {request.EvaluationId} nije pronađena." });

        // Deserialize raw_json_output to FullEvaluationResult
        var valuation = DeserializeValuation(eval);
        if (valuation == null)
            return UnprocessableEntity(new { error = "Nije moguće parsirati podatke procjene." });

        // Run audit
        var result = await _auditService.AuditValuationAsync(valuation, request.EvaluationId, ct);

        // Save to Supabase
        try
        {
            await SaveAuditResultAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit result for {Id}", request.EvaluationId);
        }

        return Ok(result);
    }

    /// <summary>
    /// Audit multiple valuations in batch.
    /// POST /api/audit/batch
    /// </summary>
    [HttpPost("batch")]
    [EnableRateLimiting("valuation")]
    public async Task<IActionResult> AuditBatch([FromBody] BatchAuditRequest request, CancellationToken ct)
    {
        if (!_auditService.IsConfigured)
            return StatusCode(503, new { error = "Perplexity API nije konfiguriran." });

        var ids = request.EvaluationIds;

        // Cap batch size to prevent API credit drain
        if (ids.Count > 20)
            return BadRequest(new { error = "Maksimalno 20 procjena po batch auditu." });

        // If no IDs provided, fetch unaudited evaluations automatically
        if (ids.Count == 0)
        {
            var unaudited = await _supabaseService.GetUnauditedEvaluationsAsync(10);
            ids = unaudited.Select(e => e.Id).ToList();

            if (ids.Count == 0)
                return Ok(new BatchAuditResponse { Total = 0 });
        }

        var response = new BatchAuditResponse { Total = ids.Count };
        var results = new List<BatchAuditItem>();

        foreach (var evalId in ids)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var eval = await _supabaseService.GetEvaluationByIdAsync(evalId);
                if (eval == null)
                {
                    results.Add(new BatchAuditItem
                    {
                        EvaluationId = evalId,
                        Success = false,
                        Error = "Procjena nije pronađena"
                    });
                    response.Failed++;
                    continue;
                }

                var valuation = DeserializeValuation(eval);
                if (valuation == null)
                {
                    results.Add(new BatchAuditItem
                    {
                        EvaluationId = evalId,
                        Success = false,
                        Error = "Neispravni podaci procjene"
                    });
                    response.Failed++;
                    continue;
                }

                var auditResult = await _auditService.AuditValuationAsync(valuation, evalId, ct);

                // Save to Supabase
                await SaveAuditResultAsync(auditResult);

                results.Add(new BatchAuditItem
                {
                    EvaluationId = evalId,
                    Success = true,
                    Result = auditResult
                });

                switch (auditResult.Verdict)
                {
                    case AuditVerdict.CONFIRMED: response.Confirmed++; break;
                    case AuditVerdict.SUSPICIOUS: response.Suspicious++; break;
                    case AuditVerdict.REJECTED: response.Rejected++; break;
                }

                // Brief pause between API calls to respect Perplexity rate limits
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch audit failed for {Id}", evalId);
                results.Add(new BatchAuditItem
                {
                    EvaluationId = evalId,
                    Success = false,
                    Error = "Audit procjene nije uspio. Pokušajte ponovno."
                });
                response.Failed++;
            }
        }

        response.Results = results;
        return Ok(response);
    }

    /// <summary>
    /// Get all audit results, optionally filtered by verdict.
    /// GET /api/audit/results?verdict=CONFIRMED&limit=50
    /// </summary>
    [HttpGet("results")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> GetResults(
        [FromQuery] string? verdict = null,
        [FromQuery] int limit = 50)
    {
        // This reads from the frontend Supabase client, but we also provide a backend route
        // so the frontend can call this without needing direct Supabase table access
        return Ok(new { message = "Koristite Supabase klijent za dohvaćanje rezultata audita.", limit, verdict });
    }

    // ═══════════════════════════════════════════════════════════
    //  TEST SCENARIO VALIDATION — run engine + Perplexity check
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Run predefined test scenarios through the valuation engine, then
    /// send each input+result pair to Perplexity for real-market validation.
    /// POST /api/audit/test-scenarios
    /// </summary>
    [HttpPost("test-scenarios")]
    [EnableRateLimiting("valuation")]
    public async Task<IActionResult> ValidateTestScenarios(CancellationToken ct)
    {
        if (!_testValidationService.IsConfigured)
            return StatusCode(503, new { error = "Perplexity API nije konfiguriran." });

        var scenarios = BuildTestScenarios();
        var report = await _testValidationService.ValidateScenariosAsync(scenarios, ct);

        _logger.LogInformation(
            "Test validation complete: {Total} scenarios, {Pass} PASS, {Warn} WARN, {Fail} FAIL",
            report.TotalScenarios, report.Passed, report.Warnings, report.Failed);

        return Ok(report);
    }

    /// <summary>
    /// Check test validation status.
    /// GET /api/audit/test-scenarios/status
    /// </summary>
    [HttpGet("test-scenarios/status")]
    [EnableRateLimiting("general")]
    public IActionResult GetTestValidationStatus()
    {
        return Ok(new
        {
            enabled = _testValidationService.IsConfigured,
            total_scenarios = BuildTestScenarios().Count,
            message = _testValidationService.IsConfigured
                ? "Test validacija je dostupna. Pokrenite POST /api/audit/test-scenarios."
                : "Perplexity API nije konfiguriran."
        });
    }

    /// <summary>
    /// Builds all test scenarios — runs each through the valuation engine
    /// and packages input+output for Perplexity validation.
    /// </summary>
    private List<TestScenarioInput> BuildTestScenarios()
    {
        var scenarios = new List<TestScenarioInput>();

        // ── Scenario 1: Zagreb center apartment ──
        scenarios.Add(BuildScenario(
            "Zagreb centar — renoviran trosoban stan 75 m²",
            "Trosobni stan u centru Zagreba, renoviran 2022., 3. kat sa liftom",
            new ExtractedPropertyInfo
            {
                Description = "Trosobni stan 75 m² na Trgu bana Jelačića, renoviran 2022., 3. kat sa liftom",
                SizeM2 = 75, Location = "Zagreb, Centar, Trg bana Jelačića",
                Condition = "renovirano", PropertyState = "renovated",
                RawText = "Trosobni stan 75 m² u centru Zagreba, potpuno renoviran 2022.",
                MicroLocation = "Trg bana Jelačića",
                KeyFeatures = ["centar grada", "renovirano 2022", "lift", "3. kat"],
            },
            "Zagreb", "stan",
            [
                MakeListing("Stan Ilica", "Renoviran trosoban stan 80 m², Ilica, centar", 280000, 3500, 80, "renovated"),
                MakeListing("Stan Jelačićev trg", "Dvosoban stan 55 m², potpuno renoviran 2023.", 192500, 3500, 55, "renovated"),
                MakeListing("Stan Cvjetni trg", "Trosoban stan 90 m², dobro stanje, centar", 270000, 3000, 90, "good"),
                MakeListing("Stan Tkalčićeva", "Četverosoban stan novogradnja 100 m²", 380000, 3800, 100, "new"),
                MakeListing("Stan Preradovićev", "Dvosoban 60 m², izvorno stanje, 2. kat bez lifta", 150000, 2500, 60, "original"),
            ]));

        // ── Scenario 2: Split sea view apartment ──
        scenarios.Add(BuildScenario(
            "Split Bačvice — stan 60 m² s pogledom na more",
            "Stan na Bačvicama u Splitu, pogled more, 1. red do mora",
            new ExtractedPropertyInfo
            {
                Description = "Stan 60 m² na Bačvicama, pogled na more, 1. red do mora",
                SizeM2 = 60, Location = "Split, Bačvice",
                Condition = "good", PropertyState = "good",
                RawText = "Stan s prekrasnim pogledom na more, Bačvice Split",
                KeyFeatures = ["pogled na more", "1. red do mora", "Bačvice"],
            },
            "Split", "stan",
            [
                MakeListing("Stan Bačvice A", "Stan 55 m², pogled na more, Bačvice", 275000, 5000, 55, "good"),
                MakeListing("Stan Bačvice B", "Stan 65 m², panoramski pogled more", 325000, 5000, 65, "renovated"),
                MakeListing("Stan Firule", "Stan 70 m², bez pogleda, Firule", 252000, 3600, 70, "good"),
                MakeListing("Stan Meje", "Stan 50 m², dobro stanje", 170000, 3400, 50, "good"),
                MakeListing("Stan Sućidar", "Stan 80 m², periferija Splita", 232000, 2900, 80, "good"),
            ]));

        // ── Scenario 3: Dubrovnik luxury villa ──
        scenarios.Add(BuildScenario(
            "Dubrovnik Lapad — luksuzna vila 250 m² s bazenom",
            "Novogradnja vila s bazenom i pogledom na more, Lapad",
            new ExtractedPropertyInfo
            {
                Description = "Luksuzna villa 250 m², bazenom, pogledom na more, novi Dubrovnik",
                SizeM2 = 250, Location = "Dubrovnik, Lapad",
                Condition = "new", PropertyState = "new", YardAreaM2 = 400,
                RawText = "Novogradnja vila s bazenom i pogledom na more, Lapad, Dubrovnik",
                KeyFeatures = ["bazen", "pogled na more", "novogradnja", "Lapad"],
            },
            "Dubrovnik", "kuća",
            [
                MakeListing("Vila Lapad A", "Vila 200 m², bazen, pogled more, novogradnja", 1200000, 6000, 200, "new"),
                MakeListing("Vila Lapad B", "Kuća 180 m², renovirana, vrt 300 m²", 900000, 5000, 180, "renovated"),
                MakeListing("Vila Gruž", "Vila 300 m², bazen, 1. red", 2100000, 7000, 300, "new"),
                MakeListing("Kuća Mokošica", "Kuća 150 m², potrebna adaptacija", 450000, 3000, 150, "needs_work"),
            ]));

        // ── Scenario 4: Osijek affordable apartment ──
        scenarios.Add(BuildScenario(
            "Osijek Retfala — dvosoban stan 55 m²",
            "Stan u Osijeku, Retfala, dobro stanje, kontinentalni market",
            new ExtractedPropertyInfo
            {
                Description = "Dvosoban stan 55 m², Retfala, Osijek, dobro stanje",
                SizeM2 = 55, Location = "Osijek, Retfala",
                Condition = "good", PropertyState = "good",
                RawText = "Stan u Osijeku, Retfala, dobro održavan",
            },
            "Osijek", "stan",
            [
                MakeListing("Stan Retfala A", "Stan 50 m², dobro stanje", 55000, 1100, 50, "good"),
                MakeListing("Stan Retfala B", "Stan 60 m², renoviran", 72000, 1200, 60, "renovated"),
                MakeListing("Stan centar", "Stan 45 m², centar Osijeka", 54000, 1200, 45, "good"),
                MakeListing("Stan Sjenjak", "Stan 70 m², izvorno stanje", 63000, 900, 70, "original"),
                MakeListing("Stan Gornji grad", "Stan 55 m², novogradnja", 77000, 1400, 55, "new"),
            ]));

        // ── Scenario 5: Pula Istria coastal apartment ──
        scenarios.Add(BuildScenario(
            "Pula Veruda — stan 55 m² blizu plaže",
            "Stan u Puli, Veruda, 200m od plaže, Istra obalski market",
            new ExtractedPropertyInfo
            {
                Description = "Stan 55 m², Pula, Veruda, blizu plaže",
                SizeM2 = 55, Location = "Pula, Veruda",
                Condition = "good", PropertyState = "good",
                RawText = "Stan u Puli, Veruda, 200m od plaže, parking",
                KeyFeatures = ["blizu plaže", "parking"],
            },
            "Pula", "stan",
            [
                MakeListing("Stan Veruda A", "Stan 50 m², dobro, Veruda, parking", 125000, 2500, 50, "good"),
                MakeListing("Stan Veruda B", "Stan 60 m², renoviran, pogled", 174000, 2900, 60, "renovated"),
                MakeListing("Stan centar", "Stan 45 m², centar Pule", 108000, 2400, 45, "good"),
                MakeListing("Stan Šijana", "Stan 65 m², mirna lokacija", 136500, 2100, 65, "good"),
                MakeListing("Stan Stoja", "Stan 55 m², blizu mora, parking mjesto", 143000, 2600, 55, "good"),
            ]));

        // ── Scenario 6: Rural house Slavonia ──
        scenarios.Add(BuildScenario(
            "Požega okolica — stara kuća 120 m² s okućnicom",
            "Kuća u selu blizu Požege, izvorno stanje, ruralni market",
            new ExtractedPropertyInfo
            {
                Description = "Stara kuća 120 m² s okućnicom 800 m², selo kod Požege",
                SizeM2 = 120, Location = "Požega, okolica",
                Condition = "izvorno stanje", PropertyState = "original", YardAreaM2 = 800,
                RawText = "Kuća u selu blizu Požege, izvorno stanje",
            },
            "Požega", "kuća",
            [
                MakeListing("Kuća Požega A", "Kuća 100 m², okućnica 600 m²", 50000, 500, 100, "original"),
                MakeListing("Kuća Požega B", "Kuća 150 m², dobar stanje, vrt 1000 m²", 90000, 600, 150, "good"),
                MakeListing("Kuća Požega C", "Kuća 233 m², potrebna obnova, vrt 500 m²", 107500, 461, 233, "original"),
                MakeListing("Kuća Požega D", "Kuća 130 m², djelomično renovirana, okućnica 400 m²", 78000, 600, 130, "fair"),
                MakeListing("Kuća Požega E", "Kuća 90 m², stara gradnja, dvorište 350 m²", 45000, 500, 90, "original"),
            ]));

        // ── Scenario 7: Agricultural land ──
        scenarios.Add(BuildScenario(
            "Đakovo okolica — oranica 5000 m²",
            "Poljoprivredno zemljište u slavonskoj ravnici",
            new ExtractedPropertyInfo
            {
                Description = "Oranica 5000 m² u ravnici, blizu Đakova",
                SizeM2 = 5000, Location = "Đakovo, okolica",
                PropertyState = "", Condition = "",
                RawText = "Poljoprivredno zemljište - oranica u slavonskoj ravnici",
            },
            "Đakovo", "oranica",
            [
                MakeListing("Oranica A", "Oranica 4000 m², ravnica", 16000, 4, 4000, ""),
                MakeListing("Oranica B", "Oranica 6000 m², Đakovština", 30000, 5, 6000, ""),
                MakeListing("Oranica C", "Poljoprivredno zemljište 3000 m²", 9000, 3, 3000, ""),
            ]));

        // ── Scenario 8: Building land Zadar coast ──
        scenarios.Add(BuildScenario(
            "Zadar Petrčane — građevinsko zemljište 700 m²",
            "Građevinsko zemljište s pogledom na more, Petrčane kod Zadra",
            new ExtractedPropertyInfo
            {
                Description = "Građevinsko zemljište 700 m², Zadar, Petrčane",
                SizeM2 = 700, Location = "Zadar, Petrčane",
                PropertyState = "", Condition = "",
                RawText = "Građevinsko zemljište s pogledom na more, Petrčane kod Zadra",
                KeyFeatures = ["pogled na more", "Petrčane"],
            },
            "Zadar", "građevinsko zemljište",
            [
                MakeListing("Parcela A", "Građevinsko zemljište 600 m², Petrčane", 180000, 300, 600, ""),
                MakeListing("Parcela B", "Zemljište 800 m², pogled more, Zadar", 280000, 350, 800, ""),
                MakeListing("Parcela C", "Građevinsko 500 m², Diklo", 150000, 300, 500, ""),
                MakeListing("Parcela D", "Zemljište 1000 m², Kozino", 250000, 250, 1000, ""),
            ]));

        // ── Scenario 9: Needs adaptation Varaždin ──
        scenarios.Add(BuildScenario(
            "Varaždin centar — stan za adaptaciju 70 m²",
            "Stan iz 1960-ih, izvorno stanje, potrebna kompletna adaptacija",
            new ExtractedPropertyInfo
            {
                Description = "Stan za adaptaciju 70 m², Varaždin centar, 1960-ih",
                SizeM2 = 70, Location = "Varaždin, Centar",
                Condition = "potrebna adaptacija", PropertyState = "needs_adaptation",
                RawText = "Stan iz 1960-ih godina, izvorno stanje, potrebna kompletna adaptacija",
                AdditionalNotes = "Potrebna kompletna obnova instalacija i kupaonice",
            },
            "Varaždin", "stan",
            [
                MakeListing("Stan A reno", "Potpuno renoviran 65 m², Varaždin", 97500, 1500, 65, "renovated"),
                MakeListing("Stan B reno", "Renoviran 75 m², 2022. moderniziran", 120000, 1600, 75, "renovated"),
                MakeListing("Stan C novo", "Novogradnja 60 m², Varaždin", 102000, 1700, 60, "new"),
                MakeListing("Stan D dobro", "Dobro stanje 70 m², uredno", 91000, 1300, 70, "good"),
                MakeListing("Stan E orig", "Izvorno stanje 80 m², potencijal", 80000, 1000, 80, "original"),
            ]));

        // ── Scenario 10: Rijeka mixed data ──
        scenarios.Add(BuildScenario(
            "Rijeka Trsat — trosoban stan 70 m², mixed data",
            "Stan u Rijeci, Trsat, mixed quality data s outlierima i rental trapovima",
            new ExtractedPropertyInfo
            {
                Description = "Trosoban stan 70 m² u Rijeci, Trsat",
                SizeM2 = 70, Location = "Rijeka, Trsat",
                Condition = "good", PropertyState = "good",
                RawText = "Stan u Rijeci na Trsatu, dobro stanje",
            },
            "Rijeka", "stan",
            [
                MakeListing("Stan Trsat A", "Stan 65 m², dobro stanje, Trsat", 117000, 1800, 65, "good"),
                MakeListing("Stan Trsat B", "Stan 75 m², renoviran, Trsat", 142500, 1900, 75, "renovated"),
                MakeListing("Stan Centar", "Stan 60 m², centar Rijeke", 120000, 2000, 60, "good"),
                MakeListing("Stan za najam", "Najam stana 50 m², 500 EUR mjesečno", 500, 10, 50, "good"),
                MakeListing("Penthouse Opatija", "Luksuzni penthouse 200 m²", 2000000, 10000, 200, "new"),
                MakeListing("Ruševina", "Kuća za rušenje 100 m²", 10000, 100, 100, "needs_work"),
            ]));

        return scenarios;
    }

    // ═══════════════════════════════════════════════════════════
    //  Scenario Builder Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a test scenario: runs the property+market through the engine
    /// and packages input+output for Perplexity validation.
    /// </summary>
    private TestScenarioInput BuildScenario(
        string name, string description,
        ExtractedPropertyInfo property,
        string regionName, string propertyType,
        List<ComparableSearchResult> listings)
    {
        // Patch URLs with location for similarity scoring (same as unit tests)
        var slug = LocationSlug(property.Location);
        var patchedListings = listings.Select(l => new ComparableSearchResult
        {
            Title = l.Title, Content = l.Content,
            Url = $"https://nekretnine.hr/{slug}/{l.Title?.Replace(" ", "-").ToLowerInvariant() ?? "listing"}",
            ListedPriceEur = l.ListedPriceEur, ListedPricePerM2 = l.ListedPricePerM2,
            AiSizeM2 = l.AiSizeM2, AiCondition = l.AiCondition,
            HasGarage = l.HasGarage, AiYardAreaM2 = l.AiYardAreaM2,
        }).ToList();

        var market = new MarketResearchResult
        {
            RegionName = regionName,
            DetectedPropertyType = propertyType,
            ActiveListings = patchedListings,
            RegionalData = [],
            SearchPhaseUsed = "Primary",
            Confidence = new ConfidenceScore { Level = "Medium", Score = 65 },
        };

        // Run engine
        var engineResult = _valuationEngine.Calculate(property, market);

        // Build snapshot
        var snapshot = new DeterministicValuationSnapshot
        {
            EstimatedValueEur = engineResult.EstimatedValueEur,
            CalculatedPricePerM2 = engineResult.CalculatedPricePerM2,
            MarketAverageAsking = engineResult.MarketAverageAsking,
            RealizationDiscountPercent = engineResult.RealizationDiscountPercent,
            TotalAdjustmentPercent = engineResult.TotalAdjustmentPercent,
            UsablePricePoints = engineResult.UsablePricePoints,
            OutliersRemoved = engineResult.OutliersRemoved,
            CoefficientOfVariation = engineResult.CoefficientOfVariation,
            StdDevPerM2 = engineResult.StdDevPerM2,
            ConfidenceLevel = engineResult.ConfidenceLevel,
            SherlockInsight = engineResult.SherlockInsight,
            Adjustments = engineResult.Adjustments.Select(a => $"{a.Factor}: {a.Percent:+0.0;-0.0}%").ToList(),
            Pipeline = engineResult.Pipeline,
        };

        return new TestScenarioInput
        {
            Name = name,
            Description = description,
            Property = property,
            Market = market,
            Result = snapshot,
        };
    }

    private static string LocationSlug(string location)
        => location.ToLowerInvariant()
            .Replace("š", "s").Replace("č", "c").Replace("ć", "c")
            .Replace("ž", "z").Replace("đ", "d")
            .Replace(", ", "/").Replace(" ", "-");

    private static ComparableSearchResult MakeListing(
        string title, string content,
        double totalPrice, double pricePerM2, double sizeM2, string condition)
        => new()
        {
            Title = title, Content = content,
            Url = $"https://example.com/{title.Replace(" ", "-").ToLowerInvariant()}",
            ListedPriceEur = totalPrice, ListedPricePerM2 = pricePerM2,
            AiSizeM2 = sizeM2, AiCondition = condition,
        };

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private FullEvaluationResult? DeserializeValuation(EvaluationReadRecord eval)
    {
        try
        {
            if (eval.RawJsonOutput == null) return null;

            var jsonStr = eval.RawJsonOutput is JsonElement je
                ? je.GetRawText()
                : JsonSerializer.Serialize(eval.RawJsonOutput);

            return JsonSerializer.Deserialize<FullEvaluationResult>(jsonStr, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize valuation for audit");
            return null;
        }
    }

    private async Task SaveAuditResultAsync(ValuationAuditResult result)
    {
        var record = new AuditResultRecord
        {
            UserId = GetUserId(),
            EvaluationId = result.EvaluationId,
            Verdict = result.Verdict.ToString(),
            OverallConfidence = result.OverallConfidence,
            Summary = result.Summary,
            Recommendation = result.Recommendation,
            PriceScore = result.PriceCheck.Score,
            LocationScore = result.LocationCheck.Score,
            MethodologyScore = result.MethodologyCheck.Score,
            DataQualityScore = result.DataQualityCheck.Score,
            FindingsCount = result.Findings.Count,
            CriticalCount = result.Findings.Count(f => f.Severity == FindingSeverity.CRITICAL),
            ModelUsed = result.ModelUsed,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            RawJsonOutput = result
        };

        await _supabaseService.SaveAuditResultAsync(record);
    }
}
