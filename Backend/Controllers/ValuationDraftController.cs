using Backend.Helpers;
using Backend.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Smart Property Picker endpoint — accepts polymorphic property input,
/// validates type-specific rules, returns a draft with description.
/// Single endpoint handles Apartment, House, and Land via JSON discriminator.
/// </summary>
[ApiController]
[Route("api/valuation")]
[EnableRateLimiting("general")]
public class ValuationDraftController : ControllerBase
{
    private readonly IValidator<PropertyInputBase> _validator;
    private readonly ILogger<ValuationDraftController> _logger;

    public ValuationDraftController(
        IValidator<PropertyInputBase> validator,
        ILogger<ValuationDraftController> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/valuation/draft — accept polymorphic property input.
    /// JSON must include "type": "Apartment" | "House" | "Land" as discriminator.
    /// Returns validated property data + human-readable description for AI prompt.
    /// </summary>
    /// <remarks>
    /// Example payloads:
    ///
    /// Apartment:
    /// {
    ///   "type": "Apartment",
    ///   "location": { "city": "Split", "district": "Plokite", "zip": "21000" },
    ///   "property_type": "Apartment",
    ///   "area_m2": 68,
    ///   "room_count": 2,
    ///   "floor": 2,
    ///   "total_floors": 5,
    ///   "condition": "Renovated",
    ///   "has_elevator": true,
    ///   "has_parking": false,
    ///   "has_balcony": true,
    ///   "sea_view": false
    /// }
    ///
    /// House:
    /// {
    ///   "type": "House",
    ///   "location": { "city": "Pakrac", "district": "Kukunjevac" },
    ///   "property_type": "House",
    ///   "living_area_m2": 120,
    ///   "land_area_m2": 787,
    ///   "floors": "TwoStory",
    ///   "condition": "Original",
    ///   "has_garage": true,
    ///   "has_pool": false,
    ///   "first_row_to_sea": false,
    ///   "has_auxiliary_object": true
    /// }
    ///
    /// Land:
    /// {
    ///   "type": "Land",
    ///   "location": { "city": "Zadar", "district": "Vir" },
    ///   "property_type": "Land",
    ///   "land_area_m2": 500,
    ///   "land_use": "Construction",
    ///   "has_access_road": true,
    ///   "has_infrastructure": true,
    ///   "first_row_to_sea": true
    /// }
    /// </remarks>
    [HttpPost("draft")]
    public async Task<IActionResult> CreateDraft([FromBody] PropertyInputBase input)
    {
        _logger.LogInformation("Draft request: type={Type}, location={City}/{District}",
            input.PropertyType, input.Location?.City, input.Location?.District);

        // ── Validate ──
        var validation = await _validator.ValidateAsync(input);

        // Separate hard errors from warnings
        var errors = validation.Errors
            .Where(e => e.Severity == FluentValidation.Severity.Error)
            .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
            .ToList();

        var warnings = validation.Errors
            .Where(e => e.Severity == FluentValidation.Severity.Warning)
            .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
            .ToList();

        if (errors.Count > 0)
        {
            _logger.LogWarning("Draft validation failed: {Count} errors", errors.Count);
            return BadRequest(new
            {
                valid = false,
                errors,
                warnings
            });
        }

        // ── Generate human-readable description ──
        var description = PropertyDescriptionGenerator.Generate(input);

        _logger.LogInformation("Draft validated OK: {Description}", description);

        // ── Build response ──
        var response = new
        {
            valid = true,
            warnings,
            description,
            property = input,    // Echo back for frontend confirmation
            ready_for_valuation = true
        };

        return Ok(response);
    }
}
