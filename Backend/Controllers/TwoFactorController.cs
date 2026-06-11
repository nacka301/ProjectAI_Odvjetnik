using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Two-Factor Authentication (TOTP) Controller.
/// 
/// All endpoints require the user to be authenticated via Supabase Auth.
/// The user's ID and email are passed via request headers set by the frontend.
/// 
/// Endpoints:
///   POST /api/2fa/setup       → Generate TOTP secret + QR URI
///   POST /api/2fa/verify-setup→ Verify first token, enable 2FA
///   POST /api/2fa/validate    → Validate token during login
///   GET  /api/2fa/status      → Check if 2FA is enabled
///   POST /api/2fa/disable     → Disable 2FA
/// </summary>
[ApiController]
[Route("api/2fa")]
public class TwoFactorController : ControllerBase
{
    private readonly ITwoFactorService _service;
    private readonly ILogger<TwoFactorController> _logger;

    public TwoFactorController(ITwoFactorService service, ILogger<TwoFactorController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/setup
    //  Body: { "email": "user@example.com" }
    // ─────────────────────────────────────────────────────────
    [HttpPost("setup")]
    public IActionResult Setup([FromBody] TwoFactorSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return BadRequest(new { error = "Email je obavezan." });
        }

        try
        {
            var result = _service.GenerateSetup(request.Email);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating 2FA setup");
            return StatusCode(500, new { error = "Greška pri generiranju 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/verify-setup
    //  Body: { "user_id": "...", "email": "...", "secret": "BASE32", "token": "123456" }
    // ─────────────────────────────────────────────────────────
    [HttpPost("verify-setup")]
    public async Task<IActionResult> VerifySetup([FromBody] TwoFactorVerifySetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId) ||
            string.IsNullOrWhiteSpace(request?.Email) ||
            string.IsNullOrWhiteSpace(request?.Secret) ||
            string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { error = "Sva polja su obavezna." });
        }

        try
        {
            var result = await _service.VerifySetupAsync(request.UserId, request.Email, request.Secret, request.Token);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying 2FA setup for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Greška pri potvrdi 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/validate
    //  Body: { "user_id": "...", "token": "123456" }
    // ─────────────────────────────────────────────────────────
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] TwoFactorValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId) ||
            string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { error = "User ID i token su obavezni." });
        }

        try
        {
            var result = await _service.ValidateTokenAsync(request.UserId, request.Token);
            if (result.Success)
                return Ok(result);
            return Unauthorized(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating 2FA token for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Greška pri validaciji 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/2fa/status?userId=...
    // ─────────────────────────────────────────────────────────
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "User ID je obavezan." });
        }

        try
        {
            var enabled = await _service.IsTwoFactorEnabledAsync(userId);
            return Ok(new TwoFactorStatusResponse { Enabled = enabled });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking 2FA status for user {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri provjeri statusa 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/disable
    //  Body: { "user_id": "...", "token": "123456" }
    // ─────────────────────────────────────────────────────────
    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] TwoFactorValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId) ||
            string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { error = "User ID i trenutni kod su obavezni." });
        }

        try
        {
            var result = await _service.DisableTwoFactorAsync(request.UserId, request.Token);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling 2FA for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Greška pri deaktivaciji 2FA." });
        }
    }
}
