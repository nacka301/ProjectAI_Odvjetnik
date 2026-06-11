using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Reads tenant_subscriptions + tenant_usage_stats to decide whether a tenant
/// is within plan limits.  All lookups are against two flat tables (no joins).
/// Results are cached per-tenant for 60 s to minimise PostgREST round-trips.
/// </summary>
public sealed class UsageService
:IUsageService {
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly ILogger<UsageService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ── Thread-safe cache: tenantId → (snapshot, expiry) ──
    private static readonly ConcurrentDictionary<string, (TenantSnapshot Snap, DateTime Expiry)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public UsageService(HttpClient httpClient, IConfiguration config, ILogger<UsageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
                    ?? config["Supabase:Url"];
        _serviceKey  = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
                    ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl)
                     && !string.IsNullOrWhiteSpace(_serviceKey);
    }

    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Check whether the tenant may create one more resource of the given kind.
    /// Returns an <see cref="UsageCheckResult"/> — Allowed=true or a structured deny.
    /// </summary>
    public async Task<UsageCheckResult> CheckLimitAsync(string? tenantId, UsageResource resource)
    {
        // Not configured / no tenant → fail-open (dev mode / solo user)
        if (!_isConfigured || string.IsNullOrWhiteSpace(tenantId))
            return UsageCheckResult.Ok();

        var snap = await GetSnapshotAsync(tenantId);
        if (snap == null)
            return UsageCheckResult.Ok(); // fail-open if tables are unreachable

        var (limitValue, currentUsed, resourceLabel, numericLimit) = resource switch
        {
            UsageResource.Case    => (snap.MaxCases,       snap.CasesUsed,     "predmeta",  snap.MaxCases),
            UsageResource.Invoice => (snap.MaxInvoices,    snap.InvoicesUsed,  "računa",    snap.MaxInvoices),
            UsageResource.AiCredit=> (snap.AiCreditsLimit, snap.AiCreditsUsed, "AI kredita",snap.AiCreditsLimit),
            _ => (-1, 0, "", -1)
        };

        // -1 = unlimited
        if (SubscriptionPlans.IsUnlimited(limitValue))
            return UsageCheckResult.Ok();

        if (currentUsed >= limitValue)
        {
            var planLabel = SubscriptionPlans.LabelFor(snap.PlanId);
            return UsageCheckResult.Blocked(
                $"Dosegli ste besplatni limit od {numericLimit} {resourceLabel}. Molimo nadogradite plan.",
                numericLimit,
                currentUsed);
        }

        return UsageCheckResult.Ok();
    }

    /// <summary>
    /// Increment one counter in tenant_usage_stats (cases_used, invoices_used, or ai_credits_used).
    /// Call AFTER the resource has been successfully created/consumed.
    /// </summary>
    public async Task IncrementAsync(string? tenantId, UsageResource resource)
    {
        if (!_isConfigured || string.IsNullOrWhiteSpace(tenantId))
            return;

        try
        {
            var column = resource switch
            {
                UsageResource.Case     => "cases_used",
                UsageResource.Invoice  => "invoices_used",
                UsageResource.AiCredit => "ai_credits_used",
                _ => null
            };
            if (column == null) return;

            var safeTenant = Uri.EscapeDataString(tenantId);
            var period = DateTime.UtcNow.ToString("yyyy-MM-01"); // first of current month

            // Read current value
            var readUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_usage_stats" +
                          $"?law_firm_id=eq.{safeTenant}&period_start=eq.{period}" +
                          $"&select=id,{column}";
            var readReq = BuildRequest(HttpMethod.Get, readUrl);
            var readRes = await _httpClient.SendAsync(readReq);
            if (!readRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("UsageService.Increment: read failed ({Status})", readRes.StatusCode);
                return;
            }

            var readBody = await readRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(readBody);
            var arr = doc.RootElement;

            if (arr.GetArrayLength() == 0)
            {
                // Auto-create row for this month (in case trigger/cron hasn't run yet)
                var insertUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_usage_stats";
                var insertPayload = JsonSerializer.Serialize(new
                {
                    law_firm_id   = tenantId,
                    period_start  = period,
                    cases_used    = resource == UsageResource.Case     ? 1 : 0,
                    invoices_used = resource == UsageResource.Invoice  ? 1 : 0,
                    ai_credits_used = resource == UsageResource.AiCredit ? 1 : 0,
                }, JsonOpts);

                var insertReq = BuildRequest(HttpMethod.Post, insertUrl, insertPayload);
                insertReq.Headers.Remove("Prefer");
                insertReq.Headers.Add("Prefer", "return=representation,resolution=merge-duplicates");
                await _httpClient.SendAsync(insertReq);
            }
            else
            {
                var row = arr[0];
                var rowId = row.GetProperty("id").GetString()!;
                var current = row.GetProperty(column).GetInt32();

                var patchUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_usage_stats?id=eq.{Uri.EscapeDataString(rowId)}";
                var patchPayload = $"{{\"{column}\": {current + 1}}}";
                var patchReq = BuildRequest(HttpMethod.Patch, patchUrl, patchPayload);
                await _httpClient.SendAsync(patchReq);
            }

            // Invalidate cached snapshot so next check sees the new count
            _cache.TryRemove(tenantId, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsageService.Increment failed for tenant {TenantId}, resource {Resource}", tenantId, resource);
        }
    }

    /// <summary>Bust the cache for a specific tenant (e.g. after plan upgrade).</summary>
    public void InvalidateCache(string tenantId)
        => _cache.TryRemove(tenantId, out _);

    // ═══════════════════════════════════════════════════════════
    //  Internal — Snapshot loader
    // ═══════════════════════════════════════════════════════════

    private async Task<TenantSnapshot?> GetSnapshotAsync(string tenantId)
    {
        if (_cache.TryGetValue(tenantId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Snap;

        try
        {
            var safeTenant = Uri.EscapeDataString(tenantId);

            // ── 1) Subscription ──
            var subUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_subscriptions" +
                         $"?law_firm_id=eq.{safeTenant}&status=in.(active,trial)" +
                         "&select=plan_id,max_cases,max_invoices,ai_credits_limit&order=created_at.desc&limit=1";
            var subReq = BuildRequest(HttpMethod.Get, subUrl);
            var subRes = await _httpClient.SendAsync(subReq);
            if (!subRes.IsSuccessStatusCode) return null;

            var subBody = await subRes.Content.ReadAsStringAsync();
            using var subDoc = JsonDocument.Parse(subBody);

            string planId = "free";
            int maxCases = SubscriptionPlans.FreeCasesLimit;
            int maxInvoices = SubscriptionPlans.FreeInvoicesLimit;
            int aiLimit = SubscriptionPlans.FreeAiCreditsPerMonth;

            if (subDoc.RootElement.GetArrayLength() > 0)
            {
                var s = subDoc.RootElement[0];
                planId      = s.TryGetProperty("plan_id",          out var p) ? p.GetString() ?? "free" : "free";
                maxCases    = s.TryGetProperty("max_cases",        out var c) ? c.GetInt32() : maxCases;
                maxInvoices = s.TryGetProperty("max_invoices",     out var i) ? i.GetInt32() : maxInvoices;
                aiLimit     = s.TryGetProperty("ai_credits_limit", out var a) ? a.GetInt32() : aiLimit;
            }

            // ── 2) Usage stats for the current month ──
            var period = DateTime.UtcNow.ToString("yyyy-MM-01");
            var usageUrl = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/tenant_usage_stats" +
                           $"?law_firm_id=eq.{safeTenant}&period_start=eq.{period}" +
                           "&select=cases_used,invoices_used,ai_credits_used&limit=1";
            var usageReq = BuildRequest(HttpMethod.Get, usageUrl);
            var usageRes = await _httpClient.SendAsync(usageReq);

            int casesUsed = 0, invoicesUsed = 0, aiUsed = 0;
            if (usageRes.IsSuccessStatusCode)
            {
                var usageBody = await usageRes.Content.ReadAsStringAsync();
                using var usageDoc = JsonDocument.Parse(usageBody);
                if (usageDoc.RootElement.GetArrayLength() > 0)
                {
                    var u = usageDoc.RootElement[0];
                    casesUsed    = u.TryGetProperty("cases_used",       out var cu) ? cu.GetInt32() : 0;
                    invoicesUsed = u.TryGetProperty("invoices_used",    out var iu) ? iu.GetInt32() : 0;
                    aiUsed       = u.TryGetProperty("ai_credits_used",  out var au) ? au.GetInt32() : 0;
                }
            }

            var snap = new TenantSnapshot
            {
                PlanId          = planId,
                MaxCases        = maxCases,
                MaxInvoices     = maxInvoices,
                AiCreditsLimit  = aiLimit,
                CasesUsed       = casesUsed,
                InvoicesUsed    = invoicesUsed,
                AiCreditsUsed   = aiUsed,
            };

            _cache[tenantId] = (snap, DateTime.UtcNow.Add(CacheTtl));
            return snap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsageService: snapshot load failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HTTP helper
    // ═══════════════════════════════════════════════════════════

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("apikey", _serviceKey);
        req.Headers.Add("Authorization", $"Bearer {_serviceKey}");
        req.Headers.Add("Prefer", "return=representation");
        if (body != null)
            req.Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return req;
    }

    // ═══════════════════════════════════════════════════════════
    //  Inner types
    // ═══════════════════════════════════════════════════════════

    private sealed class TenantSnapshot
    {
        public string PlanId { get; init; } = "free";
        public int MaxCases { get; init; }
        public int MaxInvoices { get; init; }
        public int AiCreditsLimit { get; init; }
        public int CasesUsed { get; init; }
        public int InvoicesUsed { get; init; }
        public int AiCreditsUsed { get; init; }
    }
}

/// <summary>The kind of limited resource to check.</summary>
public enum UsageResource
{
    Case,
    Invoice,
    AiCredit
}

/// <summary>Result of a usage limit check.</summary>
public sealed class UsageCheckResult
{
    public bool Allowed { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public int? Limit { get; init; }
    public int? Used { get; init; }

    public static UsageCheckResult Ok()
        => new() { Allowed = true };

    public static UsageCheckResult Blocked(string message, int limit, int used)
        => new()
        {
            Allowed = false,
            Code    = "UPGRADE_REQUIRED",
            Message = message,
            Limit   = limit,
            Used    = used,
        };
}
