using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Service for tracking per-module API credit usage per law firm.
/// Used as a "Zaštitar" (guard) before AI endpoints — checks if firm has remaining credits
/// and decrements on use. Uses Supabase PostgREST with service_role key.
///
/// Usage in controllers:
///   var check = await _creditService.ConsumeCreditAsync(userId, "contract_analysis");
///   if (!check.Allowed) return StatusCode(403, new { error = check.Message });
///   // ... proceed with AI logic
/// </summary>
public sealed class CreditService
:ICreditService {
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly string? _supabaseUrl;
    private readonly ILogger<CreditService> _logger;
    private readonly bool _isConfigured;
    private readonly ISettingsService _settingsService;
    private readonly IUsageService _usageService;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex UuidRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    public CreditService(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        ILogger<CreditService> logger,
        ISettingsService settingsService,
        IUsageService usageService)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _logger = logger;
        _settingsService = settingsService;
        _usageService = usageService;

        _supabaseUrl = supabase.Url;
        _isConfigured = supabase.IsConfigured;
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Quota bookkeeping runs as service_role, reads included — deliberately.
    ///
    /// `firm_usage` is SELECT-only for users, so the writes need it anyway. The
    /// reads need it too, and for a sharper reason: this service reads a usage
    /// row and creates a fresh one at zero when it finds none. Under RLS an
    /// unexpected empty read would look identical to "no row yet", so a user who
    /// could suppress the read would get their quota reset. The firm is derived
    /// from the verified token before any query runs, which is what scopes this.
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        _supabase.ApplyServiceAuth(request);
        request.Headers.Add("Prefer", "return=representation");

        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    private static bool IsValidUuid(string? id) =>
        !string.IsNullOrWhiteSpace(id) && UuidRegex.IsMatch(id);

    // ═══════════════════════════════════════════════════════════
    //  ConsumeCredit — The "Zaštitar" Guard
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Check if the user's firm has remaining credits for the given module.
    /// If yes, increment used_credits by 1 and return Allowed=true.
    /// If no, return Allowed=false with a professional Croatian error message.
    /// If the firm_usage row doesn't exist yet, auto-create it with default limits.
    /// </summary>
    public async Task<CreditCheckResult> ConsumeCreditAsync(string userId, string moduleName)
    {
        if (!_isConfigured)
            return CreditCheckResult.Allow(); // Not configured = unlimited (dev mode)

        if (!IsValidUuid(userId))
            return CreditCheckResult.Deny("Nevažeći korisnički identifikator.");

        try
        {
            // 1. Get user's law_firm_id via profile
            var profile = await _settingsService.GetProfileAsync(userId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.LawFirmId))
            {
                // No firm linked — allow (solo user / dev mode)
                _logger.LogWarning("User {UserId} has no law_firm_id — credit check skipped", userId);
                return CreditCheckResult.Allow();
            }

            var firmId = profile.LawFirmId;

            // 1b. Tenant-level AI credit limit check (tenant_subscriptions + tenant_usage_stats)
            var usageCheck = await _usageService.CheckLimitAsync(firmId, UsageResource.AiCredit);
            if (!usageCheck.Allowed)
            {
                return CreditCheckResult.Deny(usageCheck.Message);
            }

            // 2. Query firm_usage for this firm + module
            var usage = await GetUsageAsync(firmId, moduleName);

            if (usage == null)
            {
                // Auto-create a default row (50 credits)
                usage = await CreateDefaultUsageAsync(firmId, moduleName);
                if (usage == null)
                {
                    // If creation fails, allow (fail-open for beta)
                    _logger.LogError("Failed to create default usage for firm {FirmId}, module {Module}", firmId, moduleName);
                    return CreditCheckResult.Allow();
                }
            }

            // 3. Check limit
            if (usage.UsedCredits >= usage.MaxCredits)
            {
                _logger.LogWarning("Firm {FirmId} exceeded credits for {Module}: {Used}/{Max}",
                    firmId, moduleName, usage.UsedCredits, usage.MaxCredits);

                return CreditCheckResult.Deny(
                    $"Dosegli ste limit Beta testiranja za ovaj modul ({usage.UsedCredits}/{usage.MaxCredits}). " +
                    "Molimo kontaktirajte podršku za povećanje limita.");
            }

            // 4. Increment used_credits
            var updated = await IncrementUsageAsync(usage.Id);
            if (!updated)
            {
                _logger.LogError("Failed to increment usage for {UsageId}", usage.Id);
                return CreditCheckResult.Allow(); // Fail-open
            }

            _logger.LogInformation("Credit consumed: firm {FirmId}, module {Module}, now {Used}/{Max}",
                firmId, moduleName, usage.UsedCredits + 1, usage.MaxCredits);

            return CreditCheckResult.Allow(usage.UsedCredits + 1, usage.MaxCredits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ConsumeCredit for user {UserId}, module {Module}", userId, moduleName);
            return CreditCheckResult.Allow(); // Fail-open — don't block user on error
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  GetUsage — Read all modules for a firm (for Settings UI)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Get all credit usage rows for a user's firm.</summary>
    public async Task<List<FirmUsageResponse>> GetFirmUsageAsync(string userId)
    {
        if (!_isConfigured || !IsValidUuid(userId))
            return new List<FirmUsageResponse>();

        try
        {
            var profile = await _settingsService.GetProfileAsync(userId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.LawFirmId))
                return new List<FirmUsageResponse>();

            var safeId = Uri.EscapeDataString(profile.LawFirmId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/firm_usage?law_firm_id=eq.{safeId}&select=id,law_firm_id,module_name,used_credits,max_credits,reset_date&order=module_name.asc";
            var request = CreateRequest(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetFirmUsage failed ({Status}): {Body}", response.StatusCode, body);
                return new List<FirmUsageResponse>();
            }

            var rows = JsonSerializer.Deserialize<List<FirmUsageResponse>>(body, JsonOpts)
                ?? new List<FirmUsageResponse>();

            // If no firm_usage rows exist yet (user never used an AI module),
            // synthesi a "global" row from tenant_subscriptions + tenant_usage_stats
            // so the frontend always has credit data to display.
            if (rows.Count == 0)
            {
                rows = await SynthesizeFromSubscriptionAsync(profile.LawFirmId);
            }

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching firm usage for user {UserId}", userId);
            return new List<FirmUsageResponse>();
        }
    }

    /// <summary>
    /// When no firm_usage rows exist, build a synthetic summary row
    /// from tenant_subscriptions.ai_credits_limit + tenant_usage_stats.ai_credits_used.
    /// </summary>
    private async Task<List<FirmUsageResponse>> SynthesizeFromSubscriptionAsync(string firmId)
    {
        try
        {
            var safeFirmId = Uri.EscapeDataString(firmId);

            // 1) Subscription → ai_credits_limit
            var subUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions" +
                         $"?law_firm_id=eq.{safeFirmId}&status=in.(active,trial)" +
                         "&select=ai_credits_limit&order=created_at.desc&limit=1";
            var subReq = CreateRequest(HttpMethod.Get, subUrl);
            var subRes = await _httpClient.SendAsync(subReq);
            if (!subRes.IsSuccessStatusCode) return new List<FirmUsageResponse>();

            var subBody = await subRes.Content.ReadAsStringAsync();
            using var subDoc = JsonDocument.Parse(subBody);
            // No AI credits are sold on this plan. The modules that spend them
            // are all still in development and admin-only, so an office with no
            // subscription row starts at zero rather than at a free allowance.
            int aiLimit = 0;
            if (subDoc.RootElement.GetArrayLength() > 0)
            {
                var s = subDoc.RootElement[0];
                aiLimit = s.TryGetProperty("ai_credits_limit", out var a) ? a.GetInt32() : aiLimit;
            }

            // 2) Usage stats this month → ai_credits_used
            var period = DateTime.UtcNow.ToString("yyyy-MM-01");
            var usageUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_usage_stats" +
                           $"?law_firm_id=eq.{safeFirmId}&period_start=eq.{period}" +
                           "&select=ai_credits_used&limit=1";
            var usageReq = CreateRequest(HttpMethod.Get, usageUrl);
            var usageRes = await _httpClient.SendAsync(usageReq);

            int aiUsed = 0;
            if (usageRes.IsSuccessStatusCode)
            {
                var usageBody = await usageRes.Content.ReadAsStringAsync();
                using var usageDoc = JsonDocument.Parse(usageBody);
                if (usageDoc.RootElement.GetArrayLength() > 0)
                {
                    var u = usageDoc.RootElement[0];
                    aiUsed = u.TryGetProperty("ai_credits_used", out var au) ? au.GetInt32() : 0;
                }
            }

            // Return a synthetic "global" row
            return new List<FirmUsageResponse>
            {
                new()
                {
                    Id = "00000000-0000-0000-0000-000000000000",
                    LawFirmId = firmId,
                    ModuleName = "global",
                    UsedCredits = aiUsed,
                    MaxCredits = aiLimit,
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SynthesizeFromSubscription failed for firm {FirmId}", firmId);
            return new List<FirmUsageResponse>();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Internal — CRUD helpers
    // ═══════════════════════════════════════════════════════════

    private async Task<FirmUsageResponse?> GetUsageAsync(string firmId, string moduleName)
    {
        var safeFirmId = Uri.EscapeDataString(firmId);
        var safeModule = Uri.EscapeDataString(moduleName);
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/firm_usage?law_firm_id=eq.{safeFirmId}&module_name=eq.{safeModule}&select=id,law_firm_id,module_name,used_credits,max_credits,reset_date";
        var request = CreateRequest(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return null;

        var rows = JsonSerializer.Deserialize<List<FirmUsageResponse>>(body, JsonOpts);
        return rows?.FirstOrDefault();
    }

    private async Task<FirmUsageResponse?> CreateDefaultUsageAsync(string firmId, string moduleName)
    {
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/firm_usage";
        var payload = JsonSerializer.Serialize(new
        {
            law_firm_id = firmId,
            module_name = moduleName,
            used_credits = 0,
            max_credits = 50
        }, JsonOpts);

        var request = CreateRequest(HttpMethod.Post, url, payload);
        // Handle conflict (upsert) in case of race condition
        request.Headers.Remove("Prefer");
        request.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");
        request.Headers.Add("on_conflict", "law_firm_id,module_name");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("CreateDefaultUsage failed ({Status}): {Body}", response.StatusCode, body);
            return null;
        }

        var rows = JsonSerializer.Deserialize<List<FirmUsageResponse>>(body, JsonOpts);
        return rows?.FirstOrDefault();
    }

    private async Task<bool> IncrementUsageAsync(string usageId)
    {
        // PostgREST doesn't support atomic increment, so we read+write.
        // For beta with low concurrency this is fine.
        var safeId = Uri.EscapeDataString(usageId);
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/firm_usage?id=eq.{safeId}&select=used_credits";
        var getRequest = CreateRequest(HttpMethod.Get, url);

        var getResponse = await _httpClient.SendAsync(getRequest);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        if (!getResponse.IsSuccessStatusCode) return false;

        var rows = JsonSerializer.Deserialize<List<FirmUsageResponse>>(getBody, JsonOpts);
        var current = rows?.FirstOrDefault();
        if (current == null) return false;

        var newCount = current.UsedCredits + 1;
        var patchUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/firm_usage?id=eq.{safeId}";
        var payload = JsonSerializer.Serialize(new { used_credits = newCount }, JsonOpts);
        var patchRequest = CreateRequest(HttpMethod.Patch, patchUrl, payload);

        var patchResponse = await _httpClient.SendAsync(patchRequest);
        return patchResponse.IsSuccessStatusCode;
    }
}
