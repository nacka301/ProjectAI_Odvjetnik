using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// V42: Norm Validity Engine — API for Croatian legal norm lookup,
/// temporal validity checks, and court-style citation formatting.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class NormController : ControllerBase
{
    private readonly NormValidityService _normService;

    public NormController(NormValidityService normService)
    {
        _normService = normService;
    }

    /// <summary>
    /// GET /api/norm — List all registered Croatian norms.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<NormInfo>> GetAll()
        => Ok(_normService.GetAllNorms());

    /// <summary>
    /// GET /api/norm/{abbreviation} — Look up a specific norm by abbreviation.
    /// Example: GET /api/norm/ZOO
    /// </summary>
    [HttpGet("{abbreviation}")]
    public ActionResult<NormInfo> GetNorm(string abbreviation)
    {
        var norm = _normService.GetNorm(abbreviation);
        if (norm is null)
            return NotFound(new { error = $"Zakon '{abbreviation}' nije pronađen u registru." });
        return Ok(norm);
    }

    /// <summary>
    /// GET /api/norm/search?q=obveznim — Search norms by name fragment.
    /// </summary>
    [HttpGet("search")]
    public ActionResult<IReadOnlyList<NormInfo>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Parametar 'q' je obavezan." });
        return Ok(_normService.SearchNorms(q));
    }

    /// <summary>
    /// GET /api/norm/{abbreviation}/at/{date} — Temporal lookup.
    /// Returns which version of the law was in force on a specific date.
    /// Example: GET /api/norm/ZOO/at/2003-06-15
    /// </summary>
    [HttpGet("{abbreviation}/at/{date}")]
    public ActionResult<TemporalNormResult> GetAtDate(string abbreviation, DateOnly date)
    {
        var result = _normService.GetApplicableVersion(abbreviation, date);
        if (result is null)
            return NotFound(new { error = $"Zakon '{abbreviation}' nije pronađen u registru." });
        return Ok(result);
    }

    /// <summary>
    /// POST /api/norm/cite — Format a citation in court style (sudski način).
    /// </summary>
    [HttpPost("cite")]
    public ActionResult<CitedNorm> FormatCitation([FromBody] CitationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Abbreviation))
            return BadRequest(new { error = "Polje 'abbreviation' je obavezno." });

        var citation = _normService.FormatCitation(
            request.Abbreviation,
            request.Article,
            request.Paragraph,
            request.Point,
            request.EventDate);

        return Ok(citation);
    }

    /// <summary>
    /// POST /api/norm/temporal — Batch temporal lookup for multiple norms.
    /// Used by the frontend to validate all cited norms against an event date.
    /// </summary>
    [HttpPost("temporal")]
    public ActionResult<List<TemporalNormResult>> BatchTemporalLookup([FromBody] BatchTemporalRequest request)
    {
        if (request.EventDate == default)
            return BadRequest(new { error = "Polje 'event_date' je obavezno." });

        var results = new List<TemporalNormResult>();
        foreach (var abbr in request.Abbreviations ?? [])
        {
            var result = _normService.GetApplicableVersion(abbr, request.EventDate);
            if (result is not null)
                results.Add(result);
        }

        return Ok(results);
    }
}

/// <summary>
/// Request body for batch temporal lookup.
/// </summary>
public class BatchTemporalRequest
{
    public DateOnly EventDate { get; set; }
    public List<string> Abbreviations { get; set; } = [];
}
