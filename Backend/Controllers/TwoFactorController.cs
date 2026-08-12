using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Two-Factor Authentication (TOTP) Controller.
///
/// SECURITY: the acting user is taken ONLY from the verified Supabase JWT
/// (HttpContext.Items, populated by SupabaseJwtMiddleware). Any user_id or
/// email in the request body is ignored — previously these endpoints trusted
/// the body, which let anyone enrol, probe or disable 2FA for another account.
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

    // SECURITY: verified identity from the Supabase JWT — never from the request.
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;
    private string? GetEmail() => HttpContext.Items["AuthEmail"] as string;

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/setup
    //  Body ignored — enrols the caller, using the email from their token.
    // ─────────────────────────────────────────────────────────
    [HttpPost("setup")]
    public IActionResult Setup()
    {
        var email = GetEmail();
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        try
        {
            var result = _service.GenerateSetup(email);
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
    //  Body: { "secret": "BASE32", "token": "123456" }
    //  user_id / email from the body are ignored.
    // ─────────────────────────────────────────────────────────
    [HttpPost("verify-setup")]
    public async Task<IActionResult> VerifySetup([FromBody] TwoFactorVerifySetupRequest request)
    {
        var userId = GetUserId();
        var email = GetEmail();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (string.IsNullOrWhiteSpace(request?.Secret) ||
            string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { error = "Tajna i kod su obavezni." });
        }

        try
        {
            var result = await _service.VerifySetupAsync(userId, email, request.Secret, request.Token);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
        catch (TwoFactorStoreUnavailableException ex)
        {
            _logger.LogError(ex, "2FA store unavailable while verifying setup for user {UserId}", userId);
            return StatusCode(503, new { error = "Aktivacija 2FA trenutno nije moguća." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying 2FA setup for user {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri potvrdi 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/validate
    //  Body: { "token": "123456" } — user_id from the body is ignored.
    // ─────────────────────────────────────────────────────────
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] TwoFactorValidateRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (string.IsNullOrWhiteSpace(request?.Token))
            return BadRequest(new { error = "Kod je obavezan." });

        try
        {
            var result = await _service.ValidateTokenAsync(userId, request.Token);
            if (result.Success)
                return Ok(result);
            return Unauthorized(result);
        }
        catch (TwoFactorStoreUnavailableException ex)
        {
            // Not 401: a code we could not check is not a code that was wrong.
            _logger.LogError(ex, "2FA store unavailable while validating token for user {UserId}", userId);
            return StatusCode(503, new { error = "Provjera koda trenutno nije moguća." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating 2FA token for user {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri validaciji 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  GET /api/2fa/status — status of the caller, no query param.
    // ─────────────────────────────────────────────────────────
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        try
        {
            var enabled = await _service.IsTwoFactorEnabledAsync(userId);
            return Ok(new TwoFactorStatusResponse { Enabled = enabled });
        }
        catch (TwoFactorStoreUnavailableException ex)
        {
            // 503, never 200 {"enabled": false}. The frontend gate decides whether
            // to let someone in from this answer, so "ne znam" has to look
            // different from "nema 2FA" on the wire.
            _logger.LogError(ex, "2FA store unavailable while checking status for user {UserId}", userId);
            return StatusCode(503, new { error = "Provjera 2FA trenutno nije moguća." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking 2FA status for user {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri provjeri statusa 2FA." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POST /api/2fa/disable
    //  Body: { "token": "123456" } — user_id from the body is ignored.
    // ─────────────────────────────────────────────────────────
    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] TwoFactorValidateRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (string.IsNullOrWhiteSpace(request?.Token))
            return BadRequest(new { error = "Trenutni kod je obavezan." });

        try
        {
            var result = await _service.DisableTwoFactorAsync(userId, request.Token);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
        catch (TwoFactorStoreUnavailableException ex)
        {
            _logger.LogError(ex, "2FA store unavailable while disabling 2FA for user {UserId}", userId);
            return StatusCode(503, new { error = "Deaktivacija 2FA trenutno nije moguća." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling 2FA for user {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri deaktivaciji 2FA." });
        }
    }
}
