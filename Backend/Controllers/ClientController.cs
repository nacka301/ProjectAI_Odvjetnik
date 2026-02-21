using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// CRUD API for Clients (Stranke).
/// All endpoints require user_id header (set by frontend from auth session).
/// </summary>
[ApiController]
[Route("api/[controller]")]
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

    /// <summary>GET /api/client/{id} — get a single client by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var client = await _service.GetClientByIdAsync(id);
        if (client == null) return NotFound(new { error = "Stranka nije pronađena." });
        return Ok(client);
    }

    /// <summary>GET /api/client/{id}/profile — full client profile with cases + troškovnici.</summary>
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetProfile(string id)
    {
        var profile = await _service.GetClientProfileAsync(id);
        if (profile == null) return NotFound(new { error = "Stranka nije pronađena." });
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

    /// <summary>PATCH /api/client/{id} — update a client.</summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateClientRequest request)
    {
        var updated = await _service.UpdateClientAsync(id, request);
        if (updated == null) return NotFound(new { error = "Stranka nije pronađena." });
        return Ok(updated);
    }

    /// <summary>DELETE /api/client/{id} — delete a client.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _service.DeleteClientAsync(id);
        if (!success) return NotFound(new { error = "Stranka nije pronađena." });
        return NoContent();
    }
}
