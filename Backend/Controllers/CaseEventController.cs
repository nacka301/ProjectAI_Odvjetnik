using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// CRUD API for Case Events / Upisnik (kronološki pregled radnji u spisu).
///
/// SECURITY: All operations verify case ownership (user_id match).
/// TenantId is auto-injected from JWT middleware (HttpContext.Items["AuthTenantId"]).
/// </summary>
[ApiController]
[Route("api/case-event")]
[EnableRateLimiting("general")]
public class CaseEventController : ControllerBase
{
    private readonly IClientCaseService _service;
    private readonly ILogger<CaseEventController> _logger;

    public CaseEventController(IClientCaseService service, ILogger<CaseEventController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;
    private string? GetTenantId() => HttpContext.Items["AuthTenantId"] as string;

    /// <summary>GET /api/case-event?caseId={id} — list all events for a case (ownership verified).</summary>
    [HttpGet]
    public async Task<IActionResult> GetByCaseId([FromQuery] string caseId)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        if (string.IsNullOrWhiteSpace(caseId))
            return BadRequest(new { error = "caseId parametar je obavezan." });

        // SECURITY: verify the case belongs to this user
        var caseRecord = await _service.GetCaseByIdAsync(caseId);
        if (caseRecord == null || caseRecord.UserId != userId)
            return NotFound(new { error = "Predmet nije pronađen." });

        var events = await _service.GetCaseEventsByCaseAsync(caseId, userId);
        return Ok(events);
    }

    /// <summary>GET /api/case-event/{id} — get a single event by ID (ownership verified).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        var ev = await _service.GetCaseEventByIdAsync(id);
        if (ev == null || ev.UserId != userId)
            return NotFound(new { error = "Događaj nije pronađen." });

        return Ok(ev);
    }

    /// <summary>GET /api/case-event/billable?caseId={id} — get all pending billable events for Troškovnik generation.</summary>
    [HttpGet("billable")]
    public async Task<IActionResult> GetBillable([FromQuery] string caseId)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        if (string.IsNullOrWhiteSpace(caseId))
            return BadRequest(new { error = "caseId parametar je obavezan." });

        var caseRecord = await _service.GetCaseByIdAsync(caseId);
        if (caseRecord == null || caseRecord.UserId != userId)
            return NotFound(new { error = "Predmet nije pronađen." });

        var events = await _service.GetBillableEventsForCaseAsync(caseId, userId);
        return Ok(events);
    }

    /// <summary>GET /api/case-event/types — list allowed event types with default tariff points.</summary>
    [HttpGet("types")]
    public IActionResult GetEventTypes()
    {
        var types = CaseEventTypes.DefaultPoints
            .Select(kv => new { type = kv.Key, default_points = kv.Value })
            .ToList();
        return Ok(types);
    }

    /// <summary>POST /api/case-event — create a new case event.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCaseEventRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        if (string.IsNullOrWhiteSpace(request.CaseId))
            return BadRequest(new { error = "case_id je obavezan." });

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Opis radnje je obavezan." });

        // Validate event_type
        if (!CaseEventTypes.Allowed.Contains(request.EventType))
            return BadRequest(new { error = $"Nepoznat tip radnje: '{request.EventType}'." });

        if (request.TariffPoints < 0)
            return BadRequest(new { error = "Bodovi ne mogu biti negativni." });

        if (request.PointValue < 0)
            return BadRequest(new { error = "Vrijednost boda ne može biti negativna." });

        // SECURITY: verify the case belongs to this user
        var caseRecord = await _service.GetCaseByIdAsync(request.CaseId);
        if (caseRecord == null || caseRecord.UserId != userId)
            return BadRequest(new { error = "Predmet nije pronađen ili ne pripada ovom korisniku." });

        var record = new CaseEventRecord
        {
            CaseId = request.CaseId,
            UserId = userId,
            TenantId = GetTenantId(),
            EventDate = request.EventDate ?? DateTimeOffset.UtcNow.ToString("o"),
            EventType = request.EventType,
            Description = request.Description,
            TariffItem = request.TariffItem,
            TariffPoints = request.TariffPoints,
            PointValue = request.PointValue,
            IsBillable = request.IsBillable,
            BillingStatus = "pending"
        };

        var created = await _service.CreateCaseEventAsync(record);
        if (created == null)
            return StatusCode(500, new { error = "Greška pri spremanju događaja." });

        _logger.LogInformation("CaseEvent created: {EventId} for case {CaseId} by user {UserId}",
            created.Id, request.CaseId, userId);

        return Created($"/api/case-event/{created.Id}", created);
    }

    /// <summary>PATCH /api/case-event/{id} — update an event (ownership verified).</summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCaseEventRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        var existing = await _service.GetCaseEventByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Događaj nije pronađen." });

        // Validate event_type if provided
        if (request.EventType != null && !CaseEventTypes.Allowed.Contains(request.EventType))
            return BadRequest(new { error = $"Nepoznat tip radnje: '{request.EventType}'." });

        // Validate billing_status if provided
        if (request.BillingStatus != null &&
            request.BillingStatus is not ("pending" or "billed" or "written_off"))
            return BadRequest(new { error = "Nevažeći status naplate." });

        var updated = await _service.UpdateCaseEventAsync(id, request);
        if (updated == null)
            return NotFound(new { error = "Događaj nije pronađen." });

        return Ok(updated);
    }

    /// <summary>DELETE /api/case-event/{id} — delete an event (ownership verified).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        var existing = await _service.GetCaseEventByIdAsync(id);
        if (existing == null || existing.UserId != userId)
            return NotFound(new { error = "Događaj nije pronađen." });

        // Prevent deleting already-billed events
        if (existing.BillingStatus == "billed")
            return BadRequest(new { error = "Nije moguće obrisati već naplaćenu radnju." });

        var success = await _service.DeleteCaseEventAsync(id);
        if (!success)
            return NotFound(new { error = "Događaj nije pronađen." });

        return NoContent();
    }

    /// <summary>POST /api/case-event/mark-billed — mark events as billed after Troškovnik generation.</summary>
    [HttpPost("mark-billed")]
    public async Task<IActionResult> MarkAsBilled([FromBody] MarkBilledRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje autentifikacija." });

        if (request.EventIds == null || request.EventIds.Length == 0)
            return BadRequest(new { error = "Nema ID-ova događaja za označiti." });

        // P0 fix: batch-fetch all events in a single query instead of N sequential calls
        var events = await _service.GetCaseEventsByIdsAsync(request.EventIds);

        // Verify all requested events were found and belong to this user
        var foundIds = events.Select(e => e.Id).ToHashSet();
        foreach (var eventId in request.EventIds)
        {
            if (!foundIds.Contains(eventId))
                return BadRequest(new { error = $"Događaj {eventId} nije pronađen ili ne pripada ovom korisniku." });
        }
        if (events.Any(e => e.UserId != userId))
            return BadRequest(new { error = "Neki događaji ne pripadaju ovom korisniku." });

        var count = await _service.MarkEventsAsBilledAsync(request.EventIds);
        return Ok(new { marked = count });
    }
}

/// <summary>Request body for POST /api/case-event/mark-billed.</summary>
public sealed class MarkBilledRequest
{
    public string[] EventIds { get; set; } = [];
}
