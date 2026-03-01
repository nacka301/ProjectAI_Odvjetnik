using System.Text;
using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Admin-only controller — protected by AdminGuardMiddleware (/api/admin/* prefix).
///
/// Endpoints:
///   GET  /api/admin/audit-logs?limit=25&amp;offset=0  → Paginated audit logs
///   GET  /api/admin/subscriptions                     → All tenant subscriptions
///   POST /api/admin/seed-demo-data                    → Seed 10 clients / 10 cases / 100 events
/// </summary>
[ApiController]
[Route("api/admin")]
[EnableRateLimiting("general")]
public class AdminController : ControllerBase
{
    private readonly DataSeederService _seeder;
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<AdminController> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AdminController(
        DataSeederService seeder,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AdminController> logger)
    {
        _seeder = seeder;
        _httpClient = httpClientFactory.CreateClient("AdminPostgREST");
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl)
                     && !string.IsNullOrWhiteSpace(_serviceKey);
    }

    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;
    private string? GetTenantId() => HttpContext.Items["AuthTenantId"] as string;

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/audit-logs?limit=25&offset=0
    // ═══════════════════════════════════════════════════════════

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 25, [FromQuery] int offset = 0)
    {
        if (!_isConfigured)
            return StatusCode(503, new { error = "Supabase nije konfiguriran." });

        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/api_audit_logs?select=*&order=created_at.desc&limit={limit}&offset={offset}";
            var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Admin audit-logs query failed ({Status}): {Body}", response.StatusCode, body);
                return StatusCode((int)response.StatusCode, new { error = "Greška pri dohvaćanju audit logova." });
            }

            // Return raw JSON array from PostgREST
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching audit logs");
            return StatusCode(500, new { error = "Interna greška." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/subscriptions
    // ═══════════════════════════════════════════════════════════

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions()
    {
        if (!_isConfigured)
            return StatusCode(503, new { error = "Supabase nije konfiguriran." });

        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions?select=*&order=created_at.desc&limit=100";
            var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Admin subscriptions query failed ({Status}): {Body}", response.StatusCode, body);
                return StatusCode((int)response.StatusCode, new { error = "Greška pri dohvaćanju pretplata." });
            }

            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscriptions");
            return StatusCode(500, new { error = "Interna greška." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  POST /api/admin/seed-demo-data
    // ═══════════════════════════════════════════════════════════

    [HttpPost("seed-demo-data")]
    public async Task<IActionResult> SeedDemoData()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Korisnik nije autentificiran." });

        var tenantId = GetTenantId();

        try
        {
            _logger.LogInformation("Admin seed-demo-data requested by user {UserId}", userId);
            var result = await _seeder.SeedAsync(userId, tenantId);

            return Ok(new
            {
                success = result.Errors.Count == 0,
                clients_created = result.ClientsCreated,
                cases_created = result.CasesCreated,
                events_created = result.EventsCreated,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding demo data");
            return StatusCode(500, new { error = "Greška pri generiranju demo podataka." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Helper — PostgREST request builder
    // ═══════════════════════════════════════════════════════════

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", _serviceKey);
        request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
        request.Headers.Add("Prefer", "return=representation");

        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }
}
