using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Service for managing enterprise settings: user profiles, law firms, subscriptions.
///
/// Reads and edits run as the CALLER (RLS applies): a member sees their firm's
/// profiles and only an owner may edit the firm, per migration 016.
///
/// Provisioning runs as service_role, because it creates rows no user policy
/// permits: `law_firms` has no INSERT policy (firms are born from the signup
/// trigger or from here), and `tenant_subscriptions` is read-only to users so
/// nobody can raise their own plan limits.
/// </summary>
public sealed class SettingsService
:ISettingsService {
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly string? _supabaseUrl;
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

    public SettingsService(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        ILogger<SettingsService> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _logger = logger;

        _supabaseUrl = supabase.Url;
        _isConfigured = supabase.IsConfigured;
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>Request that runs as the caller — RLS decides what it can touch.</summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body = null)
        => BuildRequest(method, url, body, asService: false);

    /// <summary>
    /// Request that bypasses RLS. Only for provisioning rows no user policy
    /// allows; state which one at the call site.
    /// </summary>
    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string url, string? body = null)
        => BuildRequest(method, url, body, asService: true);

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string? body, bool asService)
    {
        var request = new HttpRequestMessage(method, url);

        if (asService) _supabase.ApplyServiceAuth(request);
        else           _supabase.ApplyUserAuth(request);

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

            // PATCH as the caller — profiles_update_own limits this to their own row.
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

    /// <summary>
    /// Fallback for a user who somehow has no profile row.
    ///
    /// Runs as service_role: `user_profiles` deliberately has no INSERT policy,
    /// and PostgREST's merge-duplicates upsert needs one even when the row
    /// already exists. This path should be dead for accounts created after
    /// migration 015 — the signup trigger makes the row — and it stays as a
    /// repair route for older ones. It cannot set law_firm_id, so a profile
    /// created here still needs a firm before the user can save anything.
    /// </summary>
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

            var request = CreateServiceRequest(HttpMethod.Post, url, payload);
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
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/law_firms?id=eq.{safeId}&select=id,name,oib,address,iban,pdv_obveznik";
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
                    iban = req.Iban?.Trim(),
                    pdv_obveznik = req.PdvObveznik
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
                return await CreateFirmAndLinkAsync(req.UserId, req.Name, req.Oib, req.Address, req.Iban, req.PdvObveznik);
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

            // No status filter, on purpose. It used to read
            // "&status=in.(active,trial)", which meant an expired row matched
            // nothing, this method returned null, and the controller answered
            // with its own free/active fallback — so the one user whose access
            // had just been closed was the one told everything was fine.
            // Take the newest row whatever its status and let SubscriptionGate
            // judge it.
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions?law_firm_id=eq.{safeId}&select=id,plan_id,status,valid_from,valid_until,price_eur,max_cases,max_invoices,ai_credits_limit,law_firm_id&order=created_at.desc&limit=1";
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
                req.UserId, req.FirmName, req.FirmOib, null, null, pdvObveznik: false);

            if (firm == null) return null;

            // 2. Update profile with full_name
            await UpdateProfileAsync(new ProfileUpdateRequest
            {
                UserId = req.UserId,
                FullName = req.FullName
            });

            // 3. Make sure the office has its trial row
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
        string userId, string? name, string? oib, string? address, string? iban, bool pdvObveznik = false)
    {
        // Insert new law firm
        var firmUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/law_firms";
        var firmPayload = JsonSerializer.Serialize(new
        {
            name = name?.Trim() ?? "",
            oib = oib?.Trim(),
            address = address?.Trim(),
            iban = iban?.Trim(),
            pdv_obveznik = pdvObveznik
        }, JsonOpts);

        // service_role: law_firms has no INSERT policy — a user must not be able
        // to conjure firms, only to be placed in one.
        var firmRequest = CreateServiceRequest(HttpMethod.Post, firmUrl, firmPayload);
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

        // service_role: profiles_update_own forbids moving yourself between firms,
        // which is exactly what this link does — legitimately, at provisioning time.
        var linkRequest = CreateServiceRequest(HttpMethod.Patch, profileUrl, linkPayload);
        var linkResponse = await _httpClient.SendAsync(linkRequest);
        var linkBody = await linkResponse.Content.ReadAsStringAsync();

        if (!linkResponse.IsSuccessStatusCode || linkBody.Contains("[]"))
        {
            // Upsert - profile row might not exist yet
            var upsertUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/user_profiles";
            var upsertRequest = CreateServiceRequest(HttpMethod.Post, upsertUrl, linkPayload);
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
            // The DB trigger auto_provision_trial already creates the trial row
            // on law_firms INSERT.  This method is kept as a safety net — if the
            // trigger row is missing we upsert the same thing.
            //
            // valid_until is written for the benefit of anyone reading the row
            // straight out of Supabase; the backend derives a trial's expiry
            // from valid_from + SubscriptionPlans.TrialDays and ignores it.
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions";
            var now = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new
            {
                law_firm_id = firmId,
                plan_id     = SubscriptionPlans.PlanId,
                status      = "trial",
                valid_from  = now,
                valid_until = now.AddDays(SubscriptionPlans.TrialDays),
                price_eur   = SubscriptionPlans.MonthlyPriceEur,
                max_cases   = SubscriptionPlans.Unlimited,
                max_invoices    = SubscriptionPlans.Unlimited,
                ai_credits_limit = 0
            }, JsonOpts);

            // service_role: tenant_subscriptions is SELECT-only for users, so a
            // firm cannot raise its own plan limits.
            var request = CreateServiceRequest(HttpMethod.Post, url, payload);
            // Upsert — the trigger may have beaten us to it
            request.Headers.Remove("Prefer");
            request.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("CreateSubscription failed ({Status}): {Body}", response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Ensured trial subscription for firm {FirmId}", firmId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for firm {FirmId}", firmId);
        }
    }
}
