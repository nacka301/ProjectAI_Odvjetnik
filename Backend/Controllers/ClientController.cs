using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// CRUD API for Clients (Stranke).
/// All endpoints require user_id header (set by frontend from auth session).
/// SECURITY: All read/write operations verify ownership (user_id match).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class ClientController : ControllerBase
{
    private readonly ClientCaseService _service;

    public ClientController(ClientCaseService service)
    {
        _service = service;
    }

    private string? GetUserId() =>
        Request.Headers.TryGetValue("x-user-id", out var val) ? val.FirstOrDefault() : null;

    /// <summary>GET /api/client — list all clients for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var clients = await _service.GetClientsByUserAsync(userId);
        return Ok(clients);
    }

    /// <summary>GET /api/client/{id} — get a single client by ID (ownership verified).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var client = await _service.GetClientByIdAsync(id);
        if (client == null) return NotFound(new { error = "Stranka nije pronađena." });

        // SECURITY: verify ownership — prevent IDOR
        if (client.UserId != userId)
            return NotFound(new { error = "Stranka nije pronađena." });

        return Ok(client);
    }

    /// <summary>GET /api/client/{id}/profile — full client profile with cases + troškovnici (ownership verified).</summary>
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetProfile(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var profile = await _service.GetClientProfileAsync(id);
        if (profile == null) return NotFound(new { error = "Stranka nije pronađena." });

        // SECURITY: verify ownership — prevent IDOR
        if (profile.Client.UserId != userId)
            return NotFound(new { error = "Stranka nije pronađena." });

        return Ok(profile);
    }

    /// <summary>POST /api/client — create a new client.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        if (string.IsNullOrWhiteSpace(request.ImeNaziv))
            return BadRequest(new { error = "Ime/naziv stranke je obavezan." });

        var record = new ClientRecord
        {
            UserId = userId,
            ImeNaziv = request.ImeNaziv,
            Oib = request.Oib,
            Adresa = request.Adresa,
            Grad = request.Grad,
            PostanskiBroj = request.PostanskiBroj,
            Email = request.Email,
            Telefon = request.Telefon,
            Tip = request.Tip,
            Napomena = request.Napomena
        };

        var created = await _service.CreateClientAsync(record);
        if (created == null)
            return StatusCode(500, new { error = "Greška pri spremanju stranke." });

        return Created($"/api/client/{created.Id}", created);
    }

    /// <summary>PATCH /api/client/{id} — update a client (ownership verified).</summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateClientRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        // SECURITY: verify ownership before allowing update
        var existing = await _service.GetClientByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Stranka nije pronađena." });

        var updated = await _service.UpdateClientAsync(id, request);
        if (updated == null) return NotFound(new { error = "Stranka nije pronađena." });
        return Ok(updated);
    }

    /// <summary>DELETE /api/client/{id} — delete a client (ownership verified).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        // SECURITY: verify ownership before allowing delete
        var existing = await _service.GetClientByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Stranka nije pronađena." });

        var success = await _service.DeleteClientAsync(id);
        if (!success) return NotFound(new { error = "Stranka nije pronađena." });
        return NoContent();
    }
}
