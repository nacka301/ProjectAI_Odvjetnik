using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValuationController : ControllerBase
{
    private readonly ValuationService _valuationService;
    private readonly PdfService _pdfService;
    private readonly MarketResearchService _marketResearchService;
    private readonly ILogger<ValuationController> _logger;

    public ValuationController(
        ValuationService valuationService,
        PdfService pdfService,
        MarketResearchService marketResearchService,
        ILogger<ValuationController> logger)
    {
        _valuationService = valuationService;
        _pdfService = pdfService;
        _marketResearchService = marketResearchService;
        _logger = logger;
    }

    /// <summary>
    /// Step 1: Upload PDF → extract property info via AI.
    /// </summary>
    [HttpPost("extract")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ExtractFromPdf(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Nije učitana datoteka." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Samo PDF datoteke su prihvaćene." });
        }

        _logger.LogInformation("Extracting property info from PDF: {FileName} ({Size} bytes)",
            file.FileName, file.Length);

        try
        {
            var pdfText = _pdfService.ExtractText(file);
            var extracted = await _valuationService.ExtractPropertyInfoAsync(pdfText);
            return Ok(extracted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting property info from PDF.");
            return StatusCode(500, new { error = "Greška pri ekstrakciji podataka iz PDF-a." });
        }
    }

    /// <summary>
    /// Manual: Generate full valuation opinion from user-provided comparables.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] ValuationRequest request)
    {
        if (request.TargetProperty is null)
            return BadRequest(new { error = "Podaci o nekretnini su obavezni." });

        if (request.Comparables is null || request.Comparables.Count == 0)
            return BadRequest(new { error = "Potrebna je barem jedna usporediva nekretnina." });

        _logger.LogInformation(
            "Generating valuation for property in {Location}, {Size} m², with {Count} comparables.",
            request.TargetProperty.Location,
            request.TargetProperty.SizeM2,
            request.Comparables.Count);

        try
        {
            var result = await _valuationService.GenerateValuationAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating valuation opinion.");
            return StatusCode(500, new { error = "Greška pri generiranju mišljenja o vrijednosti." });
        }
    }

    /// <summary>
    /// Automated: Upload PDF → extract → web search → AI valuation (all-in-one).
    /// </summary>
    [HttpPost("auto-generate")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AutoGenerate(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Nije učitana datoteka." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Samo PDF datoteke su prihvaćene." });
        }

        _logger.LogInformation("Auto-generate valuation from PDF: {FileName} ({Size} bytes)",
            file.FileName, file.Length);

        try
        {
            // Step 1: Extract text from PDF
            var pdfText = _pdfService.ExtractText(file);

            // Step 2: Extract property info via AI
            var property = await _valuationService.ExtractPropertyInfoAsync(pdfText);

            _logger.LogInformation(
                "Extracted property: {Desc}, {Size} m², {Location}",
                property.Description, property.SizeM2, property.Location);

            // Step 3: Search web for comparables via Tavily (pdfText used for property type detection)
            var searchResults = await _marketResearchService
                .SearchComparables(property.Location, property.SizeM2, pdfText);

            if (searchResults.Count == 0)
            {
                return Ok(new AutoValuationResult
                {
                    TargetPropertySummary = $"{property.Description}, {property.SizeM2} m², {property.Location}",
                    SelectedComparables = [],
                    EstimatedValueEur = 0,
                    ValuationReasoning = "Nije pronađena nijedna usporediva nekretnina na tržištu. " +
                        "Potrebno je ručno unijeti podatke o usporedivim nekretninama."
                });
            }

            // Step 4: Send everything to OpenAI for final valuation
            var result = await _valuationService
                .GenerateAutoValuationAsync(property, searchResults);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-generate valuation.");
            return StatusCode(500, new { error = "Greška pri automatskoj procjeni vrijednosti." });
        }
    }
}
