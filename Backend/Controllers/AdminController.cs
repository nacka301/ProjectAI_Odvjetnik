using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

// ═══════════════════════════════════════════════════════════
//  DTOs for GET /api/admin/users
// ═══════════════════════════════════════════════════════════

public class AdminUserDto
{
    [JsonPropertyName("user_id")]       public string UserId { get; set; } = "";
    [JsonPropertyName("email")]         public string Email { get; set; } = "";
    [JsonPropertyName("full_name")]     public string FullName { get; set; } = "";
    [JsonPropertyName("phone_number")]  public string? PhoneNumber { get; set; }
    [JsonPropertyName("role")]          public string Role { get; set; } = "lawyer";
    [JsonPropertyName("totp_enabled")]  public bool TotpEnabled { get; set; }
    [JsonPropertyName("created_at")]    public string CreatedAt { get; set; } = "";
    [JsonPropertyName("last_sign_in")]  public string? LastSignIn { get; set; }

    // Firm info
    [JsonPropertyName("law_firm_id")]   public string? LawFirmId { get; set; }
    [JsonPropertyName("firm_name")]     public string? FirmName { get; set; }
    [JsonPropertyName("firm_oib")]      public string? FirmOib { get; set; }

    // Subscription (tenant_subscriptions row for this user's law firm).
    // law_firm_id above is what the admin screen PATCHes; without it the
    // subscription is visible but not editable.
    [JsonPropertyName("sub_status")]    public string? SubStatus { get; set; }
    [JsonPropertyName("valid_from")]    public string? ValidFrom { get; set; }
    [JsonPropertyName("valid_until")]   public string? ValidUntil { get; set; }

    // Credit usage (summed across modules)
    [JsonPropertyName("used_credits")]  public int UsedCredits { get; set; }
    [JsonPropertyName("max_credits")]   public int MaxCredits { get; set; }
}

/// <summary>
/// What the admin screen sends when it changes an office's subscription.
/// </summary>
public class AdminSubscriptionUpdate
{
    /// <summary>One of: trial, active, canceled, expired.</summary>
    [JsonPropertyName("status")]      public string Status { get; set; } = "";

    /// <summary>
    /// When access ends. Required for active and canceled. Ignored for trial —
    /// a trial ends at valid_from + SubscriptionPlans.TrialDays, and letting the
    /// admin type a different date here would create a second, contradicting
    /// answer to "how long is the trial".
    /// </summary>
    [JsonPropertyName("valid_until")] public DateTimeOffset? ValidUntil { get; set; }
}

/// <summary>
/// Admin-only controller — protected by AdminGuardMiddleware (/api/admin/* prefix).
///
/// Endpoints:
///   GET  /api/admin/users                             → Structured user overview
///   GET  /api/admin/audit-logs?limit=25&amp;offset=0  → Paginated audit logs
///   GET  /api/admin/subscriptions                     → All tenant subscriptions
///   PATCH /api/admin/subscriptions/{lawFirmId}        → Set status + valid_until
///   POST /api/admin/seed-demo-data                    → Seed 10 clients / 10 cases / 100 events
/// </summary>
[ApiController]
[Route("api/admin")]
[EnableRateLimiting("general")]
public class AdminController : ControllerBase
{
    private readonly DataSeederService _seeder;
    private readonly ISubscriptionGate _gate;
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
        ISubscriptionGate gate,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AdminController> logger)
    {
        _seeder = seeder;
        _gate = gate;
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

    /// <summary>
    /// Reads totp_enabled out of the embedded user_mfa resource. PostgREST
    /// returns an object for a one-to-one embed and an array otherwise, and a
    /// user who never enrolled has no user_mfa row at all — all three shapes
    /// mean "2FA off" rather than an error.
    /// </summary>
    private static bool ReadTotpEnabled(JsonElement profile)
    {
        if (!profile.TryGetProperty("user_mfa", out var mfa))
            return false;

        var row = mfa.ValueKind switch
        {
            JsonValueKind.Object => mfa,
            JsonValueKind.Array when mfa.GetArrayLength() > 0 => mfa[0],
            _ => default
        };

        return row.ValueKind == JsonValueKind.Object
            && row.TryGetProperty("totp_enabled", out var enabled)
            && enabled.ValueKind == JsonValueKind.True;
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/users — structured user overview
    // ═══════════════════════════════════════════════════════════

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        if (!_isConfigured)
            return StatusCode(503, new { error = "Supabase nije konfiguriran." });

        try
        {
            // 1) Fetch all Supabase Auth users (email, created_at, last_sign_in_at)
            var authUsersUrl = $"{_supabaseUrl!.TrimEnd('/')}/auth/v1/admin/users?per_page=500";
            var authReq = new HttpRequestMessage(HttpMethod.Get, authUsersUrl);
            authReq.Headers.Add("apikey", _serviceKey);
            authReq.Headers.Add("Authorization", $"Bearer {_serviceKey}");
            var authRes = await _httpClient.SendAsync(authReq);
            var authBody = await authRes.Content.ReadAsStringAsync();

            Dictionary<string, (string email, string createdAt, string? lastSignIn)> authMap = new();

            if (authRes.IsSuccessStatusCode)
            {
                using var authDoc = JsonDocument.Parse(authBody);
                JsonElement usersArray;

                // Supabase returns { users: [...] } OR just [...]
                if (authDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    authDoc.RootElement.TryGetProperty("users", out usersArray))
                {
                    // standard format
                }
                else if (authDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    usersArray = authDoc.RootElement;
                }
                else
                {
                    usersArray = default;
                }

                if (usersArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var u in usersArray.EnumerateArray())
                    {
                        var uid = u.GetProperty("id").GetString() ?? "";
                        var email = u.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
                        var created = u.TryGetProperty("created_at", out var c) ? c.GetString() ?? "" : "";
                        var lastIn = u.TryGetProperty("last_sign_in_at", out var l) ? l.GetString() : null;
                        if (!string.IsNullOrEmpty(uid))
                            authMap[uid] = (email, created, lastIn);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Auth admin/users call failed ({Status}): {Body}", authRes.StatusCode, authBody);
            }

            // 2) Fetch user_profiles with embedded law_firms → subscriptions + firm_usage + tenant_usage_stats
            // totp_enabled moved off user_profiles into user_mfa (migration 017),
            // so it now comes in as an embedded resource rather than a column.
            var profilesUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles" +
                "?select=user_id,full_name,phone_number,role,created_at,user_mfa(totp_enabled)," +
                "law_firms(id,name,oib,tenant_subscriptions(status,valid_from,valid_until,price_eur,max_cases,max_invoices,ai_credits_limit),firm_usage(module_name,used_credits,max_credits),tenant_usage_stats(ai_credits_used,period_start))";
            var profilesReq = CreateRequest(HttpMethod.Get, profilesUrl);
            var profilesRes = await _httpClient.SendAsync(profilesReq);
            var profilesBody = await profilesRes.Content.ReadAsStringAsync();

            var result = new List<AdminUserDto>();

            if (profilesRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(profilesBody);
                foreach (var p in doc.RootElement.EnumerateArray())
                {
                    var userId = p.GetProperty("user_id").GetString() ?? "";
                    var dto = new AdminUserDto
                    {
                        UserId = userId,
                        FullName = p.TryGetProperty("full_name", out var fn) ? fn.GetString() ?? "" : "",
                        PhoneNumber = p.TryGetProperty("phone_number", out var ph) ? ph.GetString() : null,
                        Role = p.TryGetProperty("role", out var r) ? r.GetString() ?? "lawyer" : "lawyer",
                        TotpEnabled = ReadTotpEnabled(p),
                        CreatedAt = p.TryGetProperty("created_at", out var ca) ? ca.GetString() ?? "" : "",
                    };

                    // Merge auth data (email, last sign in)
                    if (authMap.TryGetValue(userId, out var auth))
                    {
                        dto.Email = auth.email;
                        dto.LastSignIn = auth.lastSignIn;
                        if (string.IsNullOrEmpty(dto.CreatedAt))
                            dto.CreatedAt = auth.createdAt;
                    }

                    // Law firm + subscriptions + usage
                    if (p.TryGetProperty("law_firms", out var firm) && firm.ValueKind == JsonValueKind.Object)
                    {
                        dto.LawFirmId = firm.TryGetProperty("id", out var fid) ? fid.GetString() : null;
                        dto.FirmName = firm.TryGetProperty("name", out var n) ? n.GetString() : null;
                        dto.FirmOib = firm.TryGetProperty("oib", out var o) ? o.GetString() : null;

                        // Best subscription (from tenant_subscriptions)
                        if (firm.TryGetProperty("tenant_subscriptions", out var subs) && subs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var s in subs.EnumerateArray())
                            {
                                var status = s.TryGetProperty("status", out var st) ? st.GetString() : "";
                                if (status == "active" || status == "trial" || dto.SubStatus == null)
                                {
                                    dto.SubStatus = status;
                                    dto.ValidFrom = s.TryGetProperty("valid_from", out var vf) ? vf.GetString() : null;
                                    dto.ValidUntil = s.TryGetProperty("valid_until", out var vu) ? vu.GetString() : null;
                                    if (status == "active" || status == "trial") break;
                                }
                            }
                        }

                        // Aggregate firm_usage
                        if (firm.TryGetProperty("firm_usage", out var usage) && usage.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var u in usage.EnumerateArray())
                            {
                                dto.UsedCredits += u.TryGetProperty("used_credits", out var uc) ? uc.GetInt32() : 0;
                                dto.MaxCredits += u.TryGetProperty("max_credits", out var mc) ? mc.GetInt32() : 0;
                            }
                        }

                        // Fallback: when no firm_usage rows exist, derive credits
                        // from the subscription's ai_credits_limit + tenant_usage_stats
                        if (dto.MaxCredits == 0 && firm.TryGetProperty("tenant_subscriptions", out var subsForCredits)
                            && subsForCredits.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var s in subsForCredits.EnumerateArray())
                            {
                                var st = s.TryGetProperty("status", out var stVal) ? stVal.GetString() : "";
                                if (st == "active" || st == "trial")
                                {
                                    dto.MaxCredits = s.TryGetProperty("ai_credits_limit", out var al) ? al.GetInt32() : 0;
                                    break;
                                }
                            }

                            // Read actual AI usage from tenant_usage_stats
                            if (dto.MaxCredits > 0 && firm.TryGetProperty("tenant_usage_stats", out var usageStats)
                                && usageStats.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var us in usageStats.EnumerateArray())
                                {
                                    dto.UsedCredits += us.TryGetProperty("ai_credits_used", out var au) ? au.GetInt32() : 0;
                                }
                            }
                        }
                    }

                    result.Add(dto);
                }
            }
            else
            {
                _logger.LogError("Admin users profiles query failed ({Status}): {Body}", profilesRes.StatusCode, profilesBody);
            }

            // 3) Add auth-only users who don't have a profile yet
            foreach (var kvp in authMap)
            {
                if (result.All(u => u.UserId != kvp.Key))
                {
                    result.Add(new AdminUserDto
                    {
                        UserId = kvp.Key,
                        Email = kvp.Value.email,
                        CreatedAt = kvp.Value.createdAt,
                        LastSignIn = kvp.Value.lastSignIn,
                    });
                }
            }

            // Sort by created_at desc
            result.Sort((a, b) => string.Compare(b.CreatedAt, a.CreatedAt, StringComparison.Ordinal));

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching admin users");
            return StatusCode(500, new { error = "Interna greška." });
        }
    }

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
    //  PATCH /api/admin/subscriptions/{lawFirmId}
    //
    //  How an office gets paid access. There is no payment provider: the user
    //  writes an e-mail, and this is where that becomes a subscription.
    // ═══════════════════════════════════════════════════════════

    private static readonly string[] AllowedStatuses = ["trial", "active", "canceled", "expired"];

    [HttpPatch("subscriptions/{lawFirmId}")]
    public async Task<IActionResult> UpdateSubscription(string lawFirmId, [FromBody] AdminSubscriptionUpdate update)
    {
        if (!_isConfigured)
            return StatusCode(503, new { error = "Supabase nije konfiguriran." });

        if (!Guid.TryParse(lawFirmId, out _))
            return BadRequest(new { error = "Neispravan identifikator ureda." });

        // Checked here rather than left to the database, whose CHECK constraint
        // answers an unknown status with a 400 that says nothing about which
        // value it disliked.
        if (!AllowedStatuses.Contains(update.Status, StringComparer.Ordinal))
            return BadRequest(new
            {
                error = $"Nepoznato stanje '{update.Status}'. Dopusteno: {string.Join(", ", AllowedStatuses)}."
            });

        if ((update.Status == "active" || update.Status == "canceled") && update.ValidUntil == null)
            return BadRequest(new { error = "Za aktivnu i otkazanu pretplatu datum isteka je obavezan." });

        try
        {
            var payload = BuildSubscriptionPayload(update);

            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions" +
                      $"?law_firm_id=eq.{Uri.EscapeDataString(lawFirmId)}";
            var request = CreateRequest(HttpMethod.Patch, url, payload);
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Admin subscription PATCH failed ({Status}): {Body}", response.StatusCode, body);
                return StatusCode((int)response.StatusCode, new { error = "Greska pri spremanju pretplate." });
            }

            // PostgREST answers 200 with an empty array when the filter matched
            // nothing. Without this the screen would report success for an
            // office that has no subscription row at all.
            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 0)
                    return NotFound(new { error = "Ovaj ured nema zapis o pretplati." });
            }

            // Without this the change would sit behind the gate cache for up to
            // a minute, which from the admin screen looks exactly like a button
            // that does nothing.
            _gate.InvalidateCache(lawFirmId);

            _logger.LogInformation(
                "Admin je uredu {LawFirmId} postavio stanje {Status} do {ValidUntil}.",
                lawFirmId, update.Status, update.ValidUntil);

            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription for firm {LawFirmId}", lawFirmId);
            return StatusCode(500, new { error = "Interna greska." });
        }
    }

    /// <summary>
    /// Builds the PATCH body. A trial gets its clock restarted from now, because
    /// the only reason to move an office back to "trial" is to give it a fresh
    /// one; leaving the old valid_from would hand it a trial that is already over.
    /// </summary>
    private static string BuildSubscriptionPayload(AdminSubscriptionUpdate update)
    {
        var now = DateTimeOffset.UtcNow;

        if (update.Status == "trial")
        {
            return JsonSerializer.Serialize(new
            {
                plan_id     = SubscriptionPlans.PlanId,
                status      = "trial",
                valid_from  = now,
                // Informational only — SubscriptionGate derives a trial's expiry
                // from valid_from + TrialDays and never reads this column.
                valid_until = now.AddDays(SubscriptionPlans.TrialDays),
                is_active   = true,
            }, JsonOpts);
        }

        return JsonSerializer.Serialize(new
        {
            plan_id     = SubscriptionPlans.PlanId,
            status      = update.Status,
            valid_until = update.ValidUntil,
            is_active   = update.Status != "expired",
        }, JsonOpts);
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
