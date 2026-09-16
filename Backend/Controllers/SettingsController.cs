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
    private readonly ISettingsService _service;
    private readonly ICreditService _creditService;
    private readonly ISubscriptionGate _gate;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService service,
        ICreditService creditService,
        ISubscriptionGate gate,
        ILogger<SettingsController> logger)
    {
        _service = service;
        _creditService = creditService;
        _gate = gate;
        _logger = logger;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    // ─────────────────────────────────────────────────────────
    //  GET /api/settings/profile
    //  userId derived from JWT — query param ignored.
    // ─────────────────────────────────────────────────────────
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

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
    //  Body: { "full_name": "...", "phone_number": "..." }
    //  userId derived from JWT — body user_id ignored.
    // ─────────────────────────────────────────────────────────
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        // Override body userId with JWT-verified identity
        request.UserId = userId;

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
    //  GET /api/settings/firm
    //  userId derived from JWT — query param ignored.
    // ─────────────────────────────────────────────────────────
    [HttpGet("firm")]
    public async Task<IActionResult> GetFirm()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

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
    //  Body: { "name": "...", "oib": "...", "address": "...", "iban": "..." }
    //  userId derived from JWT — body user_id ignored.
    // ─────────────────────────────────────────────────────────
    [HttpPut("firm")]
    public async Task<IActionResult> UpdateFirm([FromBody] LawFirmUpdateRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        // Override body userId with JWT-verified identity
        request.UserId = userId;

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
    //  GET /api/settings/subscription
    //  userId derived from JWT — query param ignored.
    // ─────────────────────────────────────────────────────────
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        try
        {
            var sub = await _service.GetSubscriptionAsync(userId)
                      ?? new SubscriptionResponse { Status = "active" };

            // The state the user is shown comes from the same service the API
            // guard consults, not from a second reading of the same columns.
            // Two independent computations of "is this office still allowed to
            // work" is how an interface ends up saying one thing while the API
            // does another.
            var access = await _gate.EvaluateAsync(
                HttpContext.Items["AuthTenantId"] as string,
                HttpContext.RequestAborted);

            sub.State = access.State;
            sub.DaysRemaining = access.DaysRemaining;
            sub.AccessUntil = access.AccessUntil;

            return Ok(sub);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription");
            return StatusCode(500, new { error = "Greška pri dohvaćanju stanja pristupa." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/settings/register-firm
    //  Body: { "full_name": "...", "firm_name": "...", "firm_oib": "..." }
    //  Called after Supabase Auth signup — userId comes from the JWT only.
    //  The body user_id used to be accepted as a fallback, which let anyone
    //  create or overwrite the firm of an arbitrary account.
    // ─────────────────────────────────────────────────────────
    [HttpPost("register-firm")]
    public async Task<IActionResult> RegisterFirm([FromBody] RegisterFirmRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId) || request == null)
            return Unauthorized(new { error = "Neautorizirani pristup." });

        // Override body userId with the verified identity
        request.UserId = userId;

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
    //  GET /api/settings/credits
    //  Returns all module credit usage rows for the user's firm.
    //  userId derived from JWT — query param ignored.
    // ─────────────────────────────────────────────────────────
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

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
    //  Body: { "module_name": "..." }
    //  userId derived from JWT — body user_id ignored.
    //  Consumes 1 credit. Returns 403 if limit reached.
    // ─────────────────────────────────────────────────────────
    [HttpPost("consume-credit")]
    public async Task<IActionResult> ConsumeCredit([FromBody] CreditConsumeRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (string.IsNullOrWhiteSpace(request?.ModuleName))
            return BadRequest(new { error = "moduleName je obavezan." });

        try
        {
            var result = await _creditService.ConsumeCreditAsync(userId, request.ModuleName);

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
