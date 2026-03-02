using Backend.Filters;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// CRUD API for Cases (Predmeti).
/// SECURITY: All read/write operations verify ownership (user_id match).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class CaseController : ControllerBase
{
    private readonly ClientCaseService _service;

    public CaseController(ClientCaseService service)
    {
        _service = service;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    /// <summary>GET /api/case — list all cases for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var cases = await _service.GetCasesByUserAsync(userId);
        return Ok(cases);
    }

    /// <summary>GET /api/case/client/{clientId} — list all cases for a specific client (ownership verified).</summary>
    [HttpGet("client/{clientId}")]
    public async Task<IActionResult> GetByClient(string clientId)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        // SECURITY: verify the client belongs to this user before returning its cases
        var client = await _service.GetClientByIdAsync(clientId);
        if (client == null || client.UserId != userId)
            return NotFound(new { error = "Stranka nije pronađena." });

        var cases = await _service.GetCasesByClientAsync(clientId);
        return Ok(cases);
    }

    /// <summary>GET /api/case/{id} — get a single case by ID (ownership verified).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var caseRecord = await _service.GetCaseByIdAsync(id);
        if (caseRecord == null) return NotFound(new { error = "Predmet nije pronađen." });

        // SECURITY: verify ownership — prevent IDOR
        if (caseRecord.UserId != userId)
            return NotFound(new { error = "Predmet nije pronađen." });

        return Ok(caseRecord);
    }

    /// <summary>POST /api/case — create a new case.</summary>
    [HttpPost]
    [LimitEnforcement(UsageResource.Case)]
    public async Task<IActionResult> Create([FromBody] CreateCaseRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        if (string.IsNullOrWhiteSpace(request.Naziv))
            return BadRequest(new { error = "Naziv predmeta je obavezan." });

        // SECURITY: if linking to a client, verify the client belongs to this user
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            var client = await _service.GetClientByIdAsync(request.ClientId);
            if (client == null || client.UserId != userId)
                return BadRequest(new { error = "Stranka nije pronađena ili ne pripada ovom korisniku." });
        }

        var record = new CaseRecord
        {
            UserId = userId,
            ClientId = request.ClientId,
            BrojPredmeta = request.BrojPredmeta,
            Naziv = request.Naziv,
            Opis = request.Opis,
            Vrsta = request.Vrsta,
            Status = request.Status,
            VrijednostSpora = request.VrijednostSpora,
            Sud = request.Sud,
            Sudac = request.Sudac,
            DatumPokretanja = request.DatumPokretanja,
            DatumZatvaranja = request.DatumZatvaranja
        };

        var created = await _service.CreateCaseAsync(record);
        if (created == null)
            return StatusCode(500, new { error = "Greška pri spremanju predmeta." });

        return Created($"/api/case/{created.Id}", created);
    }

    /// <summary>PATCH /api/case/{id} — update a case (ownership verified).</summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCaseRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        // SECURITY: verify ownership before allowing update
        var existing = await _service.GetCaseByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Predmet nije pronađen." });

        var updated = await _service.UpdateCaseAsync(id, request);
        if (updated == null) return NotFound(new { error = "Predmet nije pronađen." });
        return Ok(updated);
    }

    /// <summary>DELETE /api/case/{id} — delete a case (ownership verified).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        // SECURITY: verify ownership before allowing delete
        var existing = await _service.GetCaseByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Predmet nije pronađen." });

        var success = await _service.DeleteCaseAsync(id);
        if (!success) return NotFound(new { error = "Predmet nije pronađen." });
        return NoContent();
    }
}
