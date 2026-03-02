using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Enterprise Settings Controller — Profile, Law Firm, Subscription management.
/// 
/// Endpoints:
///   GET  /api/settings/profile         → Fetch user profile
///   PUT  /api/settings/profile         → Update user profile
///   GET  /api/settings/firm            → Fetch user's law firm
///   PUT  /api/settings/firm            → Update/create law firm
///   GET  /api/settings/subscription    → Fetch subscription status
///   POST /api/settings/register-firm   → Register firm during signup
///   GET  /api/settings/credits?userId= → Fetch beta credit usage
///   POST /api/settings/consume-credit  → Consume 1 credit (used by AI controllers)
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _service;
    private readonly CreditService _creditService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SettingsService service, CreditService creditService, ILogger<SettingsController> logger)
    {
        _service = service;
        _creditService = creditService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/settings/profile?userId=...
    // ─────────────────────────────────────────────────────────
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId je obavezan." });

        try
        {
            var profile = await _service.GetProfileAsync(userId);
            if (profile == null)
                return Ok(new ProfileResponse { UserId = userId }); // Return empty profile

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile");
            return StatusCode(500, new { error = "Greška pri dohvaćanju profila." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  PUT /api/settings/profile
    //  Body: { "user_id": "...", "full_name": "...", "phone_number": "..." }
    // ─────────────────────────────────────────────────────────
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId))
            return BadRequest(new { error = "userId je obavezan." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { error = "Ime i prezime je obavezno." });

        try
        {
            var result = await _service.UpdateProfileAsync(request);
            if (result == null)
                return StatusCode(500, new { error = "Greška pri spremanju profila." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new { error = "Greška pri ažuriranju profila." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/settings/firm?userId=...
    // ─────────────────────────────────────────────────────────
    [HttpGet("firm")]
    public async Task<IActionResult> GetFirm([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId je obavezan." });

        try
        {
            var firm = await _service.GetFirmAsync(userId);
            if (firm == null)
                return Ok(new LawFirmResponse()); // Return empty — no firm yet

            return Ok(firm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching firm");
            return StatusCode(500, new { error = "Greška pri dohvaćanju podataka o uredu." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  PUT /api/settings/firm
    //  Body: { "user_id": "...", "name": "...", "oib": "...", "address": "...", "iban": "..." }
    // ─────────────────────────────────────────────────────────
    [HttpPut("firm")]
    public async Task<IActionResult> UpdateFirm([FromBody] LawFirmUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId))
            return BadRequest(new { error = "userId je obavezan." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Naziv ureda je obavezan." });

        // Validate OIB format (11 digits) if provided
        if (!string.IsNullOrWhiteSpace(request.Oib))
        {
            var trimmedOib = request.Oib.Trim();
            if (trimmedOib.Length != 11 || !trimmedOib.All(char.IsDigit))
                return BadRequest(new { error = "OIB mora imati točno 11 znamenki." });
        }

        try
        {
            var result = await _service.UpdateFirmAsync(request);
            if (result == null)
                return StatusCode(500, new { error = "Greška pri spremanju podataka o uredu." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating firm");
            return StatusCode(500, new { error = "Greška pri ažuriranju podataka o uredu." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/settings/subscription?userId=...
    // ─────────────────────────────────────────────────────────
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId je obavezan." });

        try
        {
            var sub = await _service.GetSubscriptionAsync(userId);
            if (sub == null)
                return Ok(new SubscriptionResponse { PlanId = "free", Status = "active" });

            return Ok(sub);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription");
            return StatusCode(500, new { error = "Greška pri dohvaćanju pretplate." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/settings/register-firm
    //  Body: { "user_id": "...", "full_name": "...", "firm_name": "...", "firm_oib": "..." }
    //  Called after Supabase Auth signup to create the firm + profile + subscription.
    // ─────────────────────────────────────────────────────────
    [HttpPost("register-firm")]
    public async Task<IActionResult> RegisterFirm([FromBody] RegisterFirmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId))
            return BadRequest(new { error = "userId je obavezan." });

        if (string.IsNullOrWhiteSpace(request.FirmName))
            return BadRequest(new { error = "Naziv ureda je obavezan." });

        try
        {
            var firm = await _service.RegisterFirmAsync(request);
            if (firm == null)
                return StatusCode(500, new { error = "Greška pri registraciji ureda." });

            return Ok(new { success = true, firm });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering firm for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Greška pri registraciji ureda." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/settings/credits?userId=...
    //  Returns all module credit usage rows for the user's firm.
    // ─────────────────────────────────────────────────────────
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId je obavezan." });

        try
        {
            var usage = await _creditService.GetFirmUsageAsync(userId);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching credits");
            return StatusCode(500, new { error = "Greška pri dohvaćanju kredita." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/settings/consume-credit
    //  Body: { "user_id": "...", "module_name": "..." }
    //  Consumes 1 credit. Returns 403 if limit reached.
    // ─────────────────────────────────────────────────────────
    [HttpPost("consume-credit")]
    public async Task<IActionResult> ConsumeCredit([FromBody] CreditConsumeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId))
            return BadRequest(new { error = "userId je obavezan." });

        if (string.IsNullOrWhiteSpace(request.ModuleName))
            return BadRequest(new { error = "moduleName je obavezan." });

        try
        {
            var result = await _creditService.ConsumeCreditAsync(request.UserId, request.ModuleName);

            if (!result.Allowed)
                return StatusCode(403, new { error = result.Message });

            return Ok(new { allowed = true, used = result.Used, max = result.Max });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming credit");
            return StatusCode(500, new { error = "Greška pri potrošnji kredita." });
        }
    }
}
