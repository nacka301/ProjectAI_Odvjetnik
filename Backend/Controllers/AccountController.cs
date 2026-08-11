using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Izvoz podataka i brisanje računa — GDPR čl. 20 i čl. 17.
///
///   GET    /api/account/export  → svi podaci ureda kao JSON
///   DELETE /api/account         → briše korisnika, podaci ureda ostaju
///   DELETE /api/account/firm    → briše ured i sve podatke
///
/// Identitet dolazi isključivo iz provjerenog tokena. Nijedan endpoint ne prima
/// user_id — brisanje na temelju vrijednosti iz tijela zahtjeva bilo bi
/// najizravniji mogući način da jedan korisnik ukloni drugoga.
/// </summary>
[ApiController]
[Route("api/account")]
[EnableRateLimiting("auth")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _service;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAccountService service, ILogger<AccountController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    // ─────────────────────────────────────────────────────────
    //  GET /api/account/export
    // ─────────────────────────────────────────────────────────

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        try
        {
            var data = await _service.ExportFirmDataAsync(userId);
            if (data is null)
                return NotFound(new { error = "Podaci nisu pronađeni." });

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri izvozu podataka za {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri izvozu podataka." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  DELETE /api/account
    // ─────────────────────────────────────────────────────────

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (request?.Confirmation?.Trim() != "OBRIŠI RAČUN")
            return BadRequest(new DeleteBlockedResponse
            {
                Error = "Za potvrdu upišite točno: OBRIŠI RAČUN",
                Code = "CONFIRMATION_MISMATCH"
            });

        try
        {
            var (result, blocked) = await _service.DeleteOwnAccountAsync(userId);

            // 409, ne 400: zahtjev je ispravan, ali stanje ureda ga ne dopušta.
            // Sučelje po `code` zna ponuditi pravi sljedeći korak.
            if (blocked is not null) return Conflict(blocked);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri brisanju racuna {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri brisanju računa." });
        }
    }

    // ─────────────────────────────────────────────────────────
    //  DELETE /api/account/firm
    // ─────────────────────────────────────────────────────────

    [HttpDelete("firm")]
    public async Task<IActionResult> DeleteFirm([FromBody] DeleteFirmRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (string.IsNullOrWhiteSpace(request?.FirmNameConfirmation))
            return BadRequest(new DeleteBlockedResponse
            {
                Error = "Upišite naziv ureda za potvrdu.",
                Code = "CONFIRMATION_MISMATCH"
            });

        try
        {
            var (result, blocked) = await _service.DeleteFirmAsync(
                userId, request.FirmNameConfirmation);

            if (blocked is not null) return Conflict(blocked);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri brisanju ureda za {UserId}", userId);
            return StatusCode(500, new { error = "Greška pri brisanju ureda." });
        }
    }
}
