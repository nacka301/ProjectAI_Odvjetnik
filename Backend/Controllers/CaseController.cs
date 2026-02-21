using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// CRUD API for Cases (Predmeti).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CaseController : ControllerBase
{
    private readonly ClientCaseService _service;

    public CaseController(ClientCaseService service)
    {
        _service = service;
    }

    private string? GetUserId() =>
        Request.Headers.TryGetValue("x-user-id", out var val) ? val.FirstOrDefault() : null;

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

    /// <summary>GET /api/case/client/{clientId} — list all cases for a specific client.</summary>
    [HttpGet("client/{clientId}")]
    public async Task<IActionResult> GetByClient(string clientId)
    {
        var cases = await _service.GetCasesByClientAsync(clientId);
        return Ok(cases);
    }

    /// <summary>GET /api/case/{id} — get a single case by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var caseRecord = await _service.GetCaseByIdAsync(id);
        if (caseRecord == null) return NotFound(new { error = "Predmet nije pronađen." });
        return Ok(caseRecord);
    }

    /// <summary>POST /api/case — create a new case.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCaseRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        if (string.IsNullOrWhiteSpace(request.Naziv))
            return BadRequest(new { error = "Naziv predmeta je obavezan." });

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

    /// <summary>PATCH /api/case/{id} — update a case.</summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCaseRequest request)
    {
        var updated = await _service.UpdateCaseAsync(id, request);
        if (updated == null) return NotFound(new { error = "Predmet nije pronađen." });
        return Ok(updated);
    }

    /// <summary>DELETE /api/case/{id} — delete a case.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _service.DeleteCaseAsync(id);
        if (!success) return NotFound(new { error = "Predmet nije pronađen." });
        return NoContent();
    }
}
