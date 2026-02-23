using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Service for managing enterprise settings: user profiles, law firms, subscriptions.
/// Uses Supabase PostgREST API with service_role key (bypasses RLS).
/// </summary>
public sealed class SettingsService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<SettingsService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex UuidRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    public SettingsService(HttpClient httpClient, IConfiguration config, ILogger<SettingsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

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
    //  Profile — GET / PUT
    // ═══════════════════════════════════════════════════════════

    /// <summary>Fetch user profile by user_id. Returns null if not found or not configured.</summary>
    public async Task<ProfileResponse?> GetProfileAsync(string userId)
    {
        if (!_isConfigured || !IsValidUuid(userId)) return null;

        try
        {
            var safeId = Uri.EscapeDataString(userId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles?user_id=eq.{safeId}&select=user_id,full_name,phone_number,role,law_firm_id";
            var request = CreateRequest(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetProfile failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<ProfileResponse>>(body, JsonOpts);
            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile for {UserId}", userId);
            return null;
        }
    }

    /// <summary>Update user profile fields (full_name, phone_number). Creates the row via upsert if missing.</summary>
    public async Task<ProfileResponse?> UpdateProfileAsync(ProfileUpdateRequest req)
    {
        if (!_isConfigured || !IsValidUuid(req.UserId)) return null;

        try
        {
            var safeId = Uri.EscapeDataString(req.UserId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles?user_id=eq.{safeId}";

            var payload = JsonSerializer.Serialize(new
            {
                user_id = req.UserId,
                full_name = req.FullName?.Trim() ?? "",
                phone_number = req.PhoneNumber?.Trim()
            }, JsonOpts);

            // Use PATCH to update (service_role bypasses RLS)
            var request = CreateRequest(HttpMethod.Patch, url, payload);
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // If row doesn't exist yet, insert it
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    body.Contains("0 rows"))
                {
                    return await UpsertProfileAsync(req);
                }
                _logger.LogError("UpdateProfile failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<ProfileResponse>>(body, JsonOpts);
            if (rows == null || rows.Count == 0)
            {
                // PostgREST returns empty array if no row matched — do upsert
                return await UpsertProfileAsync(req);
            }
            return rows.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for {UserId}", req.UserId);
            return null;
        }
    }

    private async Task<ProfileResponse?> UpsertProfileAsync(ProfileUpdateRequest req)
    {
        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles";

            var payload = JsonSerializer.Serialize(new
            {
                user_id = req.UserId,
                full_name = req.FullName?.Trim() ?? "",
                phone_number = req.PhoneNumber?.Trim()
            }, JsonOpts);

            var request = CreateRequest(HttpMethod.Post, url, payload);
            // Upsert on conflict
            request.Headers.Remove("Prefer");
            request.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("UpsertProfile failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<ProfileResponse>>(body, JsonOpts);
            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting profile for {UserId}", req.UserId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Law Firm — GET / PUT
    // ═══════════════════════════════════════════════════════════

    /// <summary>Fetch the law firm for a given user (via user_profiles.law_firm_id).</summary>
    public async Task<LawFirmResponse?> GetFirmAsync(string userId)
    {
        if (!_isConfigured || !IsValidUuid(userId)) return null;

        try
        {
            // First, get the user's law_firm_id
            var profile = await GetProfileAsync(userId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.LawFirmId))
                return null;

            var safeId = Uri.EscapeDataString(profile.LawFirmId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/law_firms?id=eq.{safeId}&select=id,name,oib,address,iban";
            var request = CreateRequest(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetFirm failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<LawFirmResponse>>(body, JsonOpts);
            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching firm for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>Update or create the user's law firm and link it to their profile.</summary>
    public async Task<LawFirmResponse?> UpdateFirmAsync(LawFirmUpdateRequest req)
    {
        if (!_isConfigured || !IsValidUuid(req.UserId)) return null;

        try
        {
            // Get existing profile to check if firm exists
            var profile = await GetProfileAsync(req.UserId);

            if (profile != null && !string.IsNullOrWhiteSpace(profile.LawFirmId))
            {
                // Update existing firm
                var safeId = Uri.EscapeDataString(profile.LawFirmId);
                var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/law_firms?id=eq.{safeId}";

                var payload = JsonSerializer.Serialize(new
                {
                    name = req.Name?.Trim() ?? "",
                    oib = req.Oib?.Trim(),
                    address = req.Address?.Trim(),
                    iban = req.Iban?.Trim()
                }, JsonOpts);

                var request = CreateRequest(HttpMethod.Patch, url, payload);
                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("UpdateFirm failed ({Status}): {Body}", response.StatusCode, body);
                    return null;
                }

                var rows = JsonSerializer.Deserialize<List<LawFirmResponse>>(body, JsonOpts);
                return rows?.FirstOrDefault();
            }
            else
            {
                // Create new firm and link to profile
                return await CreateFirmAndLinkAsync(req.UserId, req.Name, req.Oib, req.Address, req.Iban);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating firm for user {UserId}", req.UserId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Subscription — GET
    // ═══════════════════════════════════════════════════════════

    /// <summary>Fetch the subscription for a user's law firm.</summary>
    public async Task<SubscriptionResponse?> GetSubscriptionAsync(string userId)
    {
        if (!_isConfigured || !IsValidUuid(userId)) return null;

        try
        {
            var profile = await GetProfileAsync(userId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.LawFirmId))
                return null;

            var safeId = Uri.EscapeDataString(profile.LawFirmId);
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/subscriptions?law_firm_id=eq.{safeId}&status=in.(active,trial)&select=id,plan_name,status,valid_until,law_firm_id&order=created_at.desc&limit=1";
            var request = CreateRequest(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetSubscription failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var rows = JsonSerializer.Deserialize<List<SubscriptionResponse>>(body, JsonOpts);
            return rows?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription for user {UserId}", userId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Registration — Create firm + profile in one go
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Called after Supabase Auth signup: creates a law firm, links profile, and creates a default subscription.
    /// </summary>
    public async Task<LawFirmResponse?> RegisterFirmAsync(RegisterFirmRequest req)
    {
        if (!_isConfigured || !IsValidUuid(req.UserId)) return null;

        try
        {
            // 1. Create the law firm
            var firm = await CreateFirmAndLinkAsync(
                req.UserId, req.FirmName, req.FirmOib, null, null);

            if (firm == null) return null;

            // 2. Update profile with full_name
            await UpdateProfileAsync(new ProfileUpdateRequest
            {
                UserId = req.UserId,
                FullName = req.FullName
            });

            // 3. Create default Pro subscription (1 year)
            await CreateDefaultSubscriptionAsync(firm.Id);

            return firm;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering firm for user {UserId}", req.UserId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Internal helpers
    // ═══════════════════════════════════════════════════════════

    private async Task<LawFirmResponse?> CreateFirmAndLinkAsync(
        string userId, string? name, string? oib, string? address, string? iban)
    {
        // Insert new law firm
        var firmUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/law_firms";
        var firmPayload = JsonSerializer.Serialize(new
        {
            name = name?.Trim() ?? "",
            oib = oib?.Trim(),
            address = address?.Trim(),
            iban = iban?.Trim()
        }, JsonOpts);

        var firmRequest = CreateRequest(HttpMethod.Post, firmUrl, firmPayload);
        var firmResponse = await _httpClient.SendAsync(firmRequest);
        var firmBody = await firmResponse.Content.ReadAsStringAsync();

        if (!firmResponse.IsSuccessStatusCode)
        {
            _logger.LogError("CreateFirm failed ({Status}): {Body}", firmResponse.StatusCode, firmBody);
            return null;
        }

        var firms = JsonSerializer.Deserialize<List<LawFirmResponse>>(firmBody, JsonOpts);
        var firm = firms?.FirstOrDefault();
        if (firm == null) return null;

        // Link the firm to the user's profile
        var safeUserId = Uri.EscapeDataString(userId);
        var profileUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles?user_id=eq.{safeUserId}";
        var linkPayload = JsonSerializer.Serialize(new
        {
            user_id = userId,
            law_firm_id = firm.Id
        }, JsonOpts);

        // Try PATCH first, then upsert if profile doesn't exist
        var linkRequest = CreateRequest(HttpMethod.Patch, profileUrl, linkPayload);
        var linkResponse = await _httpClient.SendAsync(linkRequest);
        var linkBody = await linkResponse.Content.ReadAsStringAsync();

        if (!linkResponse.IsSuccessStatusCode || linkBody.Contains("[]"))
        {
            // Upsert - profile row might not exist yet
            var upsertUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles";
            var upsertRequest = CreateRequest(HttpMethod.Post, upsertUrl, linkPayload);
            upsertRequest.Headers.Remove("Prefer");
            upsertRequest.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");
            await _httpClient.SendAsync(upsertRequest);
        }

        _logger.LogInformation("Created firm {FirmId} and linked to user {UserId}", firm.Id, userId);
        return firm;
    }

    private async Task CreateDefaultSubscriptionAsync(string firmId)
    {
        try
        {
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/subscriptions";
            var payload = JsonSerializer.Serialize(new
            {
                law_firm_id = firmId,
                plan_name = "Pro",
                status = "active"
                // valid_until defaults to now() + 1 year in DB
            }, JsonOpts);

            var request = CreateRequest(HttpMethod.Post, url, payload);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("CreateSubscription failed ({Status}): {Body}", response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Created default Pro subscription for firm {FirmId}", firmId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for firm {FirmId}", firmId);
        }
    }
}
