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
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<CreditService> _logger;
    private readonly bool _isConfigured;
    private readonly SettingsService _settingsService;
    private readonly UsageService _usageService;

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
        IConfiguration config,
        ILogger<CreditService> logger,
        SettingsService settingsService,
        UsageService usageService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settingsService = settingsService;
        _usageService = usageService;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl) &&
                        !string.IsNullOrWhiteSpace(_serviceKey);
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
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

            return JsonSerializer.Deserialize<List<FirmUsageResponse>>(body, JsonOpts)
                ?? new List<FirmUsageResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching firm usage for user {UserId}", userId);
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
