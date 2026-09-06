using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Service for managing Clients (Stranke), Cases (Predmeti), and persisted Troškovnici.
///
/// Every call travels with the CALLER'S access token, not the service_role key,
/// so Postgres evaluates the firm policies from migration 016 and a query can
/// only ever see or touch the caller's own firm. The ownership checks the
/// controllers still perform are now a second line of defence rather than the
/// only one — before this, a missing `if` in a controller meant full exposure.
/// </summary>
public sealed class ClientCaseService
:IClientCaseService {
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly string? _supabaseUrl;
    private readonly ILogger<ClientCaseService> _logger;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex UuidRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    private static readonly Regex OibRegex = new(@"^\d{11}$", RegexOptions.Compiled);

    /// <summary>Returns the OIB if it's exactly 11 digits, otherwise null.</summary>
    private static string? SanitizeOib(string? oib)
        => !string.IsNullOrWhiteSpace(oib) && OibRegex.IsMatch(oib) ? oib : null;

    // Oznake pravnih oblika (d.o.o., j.d.o.o., d.d., k.d., j.t.d., d.n.o. — uz
    // razmake i s/bez točaka) te riječi koje same znače pravnu osobu. Namjerno
    // izostavljeni „grad/općina/županija" — previše se sudaraju s adresama i
    // prezimenima; korisnik ih u sučelju označi ručno.
    private static readonly Regex PravniOblikRegex = new(
        @"(\bd\s*\.?\s*o\s*\.?\s*o\b|\bj\s*\.?\s*d\s*\.?\s*o\s*\.?\s*o\b|\bd\s*\.?\s*d\b|\bk\s*\.?\s*d\b|\bj\s*\.?\s*t\s*\.?\s*d\b|\bd\s*\.?\s*n\s*\.?\s*o\b|\bustanova\b|\bzadruga\b|\budruga\b|\bzaklada\b|\bdruštvo\b|\bkomora\b|\bsindikat\b|\bministarstvo\b|\brepublika hrvatska\b|\bkonzorcij\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>"fizicka"/"pravna" ako je vrijednost jedno od toga, inače null.</summary>
    internal static string? NormalizirajTip(string? tip)
        => tip is "fizicka" or "pravna" ? tip : null;

    /// <summary>
    /// Pogađa tip stranke iz naziva kad ga sučelje nije poslalo. Prijedlog, ne
    /// tvrdnja — korisnik ga u sučelju može pregaziti. Bez pouzdane oznake vraća
    /// "fizicka" (češći slučaj).
    /// </summary>
    internal static string NaslutiTip(string? naziv)
        => !string.IsNullOrWhiteSpace(naziv) && PravniOblikRegex.IsMatch(naziv) ? "pravna" : "fizicka";

    public ClientCaseService(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        ILogger<ClientCaseService> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _logger = logger;

        _supabaseUrl = supabase.Url;
        _isConfigured = supabase.IsConfigured;
    }

    // ═══════════════════════════════════════════════════════════
    //  Generic helpers
    // ═══════════════════════════════════════════════════════════

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        _supabase.ApplyUserAuth(request);
        request.Headers.Add("Prefer", "return=representation");

        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return request;
    }

    private async Task<T?> InsertAsync<T>(string table, object record) where T : class
    {
        if (!_isConfigured) return default;

        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}";
        var payload = JsonSerializer.Serialize(record, JsonOpts);
        var request = CreateRequest(HttpMethod.Post, url, payload);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Insert into {Table} failed ({Status}): {Body}", table, response.StatusCode, responseBody);
            return default;
        }

        var rows = JsonSerializer.Deserialize<List<T>>(responseBody, JsonOpts);
        return rows?.FirstOrDefault();
    }

    private async Task<List<T>> SelectAsync<T>(string table, string filter = "", string order = "created_at.desc", int limit = 100)
    {
        if (!_isConfigured) return [];

        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}?select=*{(string.IsNullOrEmpty(filter) ? "" : "&" + filter)}&order={order}&limit={limit}";
        var request = CreateRequest(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Select from {Table} failed ({Status}): {Body}", table, response.StatusCode, body);
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(body, JsonOpts) ?? [];
    }

    private async Task<T?> GetByIdAsync<T>(string table, string id) where T : class
    {
        if (!_isConfigured || !UuidRegex.IsMatch(id)) return default;

        var safeId = Uri.EscapeDataString(id);
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}?id=eq.{safeId}&select=*&limit=1";
        var request = CreateRequest(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GetById from {Table} failed ({Status}): {Body}", table, response.StatusCode, body);
            return default;
        }

        var rows = JsonSerializer.Deserialize<List<T>>(body, JsonOpts);
        return rows?.FirstOrDefault();
    }

    private async Task<T?> UpdateAsync<T>(string table, string id, object patch) where T : class
    {
        if (!_isConfigured || !UuidRegex.IsMatch(id)) return default;

        var safeId = Uri.EscapeDataString(id);
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}?id=eq.{safeId}";
        var payload = JsonSerializer.Serialize(patch, JsonOpts);
        var request = CreateRequest(HttpMethod.Patch, url, payload);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Update {Table} {Id} failed ({Status}): {Body}", table, id, response.StatusCode, body);
            return default;
        }

        var rows = JsonSerializer.Deserialize<List<T>>(body, JsonOpts);
        return rows?.FirstOrDefault();
    }

    private async Task<bool> DeleteAsync(string table, string id)
    {
        if (!_isConfigured || !UuidRegex.IsMatch(id)) return false;

        var safeId = Uri.EscapeDataString(id);
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/{table}?id=eq.{safeId}";
        var request = CreateRequest(HttpMethod.Delete, url);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Delete from {Table} {Id} failed ({Status}): {Body}", table, id, response.StatusCode, body);
            return false;
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════
    //  Clients (Stranke)
    // ═══════════════════════════════════════════════════════════

    public Task<ClientReadRecord?> CreateClientAsync(ClientRecord record)
        => InsertAsync<ClientReadRecord>("clients", record);

    public Task<List<ClientReadRecord>> GetClientsByUserAsync(string userId)
        => UuidRegex.IsMatch(userId)
            ? SelectAsync<ClientReadRecord>("clients", $"user_id=eq.{Uri.EscapeDataString(userId)}")
            : Task.FromResult(new List<ClientReadRecord>());

    public Task<ClientReadRecord?> GetClientByIdAsync(string id)
        => GetByIdAsync<ClientReadRecord>("clients", id);

    public Task<ClientReadRecord?> UpdateClientAsync(string id, object patch)
        => UpdateAsync<ClientReadRecord>("clients", id, patch);

    public Task<bool> DeleteClientAsync(string id)
        => DeleteAsync("clients", id);

    /// <summary>
    /// Finds a client by OIB for a specific user.
    /// Returns null if not found or OIB is empty/invalid.
    /// </summary>
    public async Task<ClientReadRecord?> FindClientByOibAsync(string userId, string oib)
    {
        if (string.IsNullOrWhiteSpace(oib) || oib.Length != 11 || !UuidRegex.IsMatch(userId))
            return null;

        var results = await SelectAsync<ClientReadRecord>(
            "clients",
            $"user_id=eq.{Uri.EscapeDataString(userId)}&oib=eq.{Uri.EscapeDataString(oib)}",
            limit: 1
        );

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Finds a client by exact name match for a specific user.
    /// Fallback when OIB is not available.
    /// </summary>
    public async Task<ClientReadRecord?> FindClientByNameAsync(string userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !UuidRegex.IsMatch(userId))
            return null;

        var results = await SelectAsync<ClientReadRecord>(
            "clients",
            $"user_id=eq.{Uri.EscapeDataString(userId)}&ime_naziv=eq.{Uri.EscapeDataString(name)}",
            limit: 1
        );

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Ensures a client exists for the given user. If a matching client is found
    /// by OIB (preferred) or exact name, returns it. Otherwise creates a new one.
    /// Returns (clientId, wasCreated).
    /// </summary>
    public async Task<(string? ClientId, bool WasCreated)> UpsertClientForTroskovnikAsync(
        string userId,
        string? uredId,
        string? naziv,
        string? oib,
        string? adresa,
        string? grad,
        string? tip = null)
    {
        if (string.IsNullOrWhiteSpace(naziv))
            return (null, false);

        // 1. Try to find by OIB first (most reliable match)
        if (!string.IsNullOrWhiteSpace(oib) && oib.Length == 11)
        {
            var byOib = await FindClientByOibAsync(userId, oib);
            if (byOib != null)
            {
                _logger.LogInformation("Found existing client by OIB {Oib}: {Id}", oib, byOib.Id);
                return (byOib.Id, false);
            }
        }

        // 2. Fallback: try to find by exact name
        var byName = await FindClientByNameAsync(userId, naziv);
        if (byName != null)
        {
            _logger.LogInformation("Found existing client by name '{Name}': {Id}", naziv, byName.Id);
            return (byName.Id, false);
        }

        // 3. Create new client
        _logger.LogInformation("Auto-creating new client '{Name}' for user {UserId}", naziv, userId);

        var record = new ClientRecord
        {
            UserId = userId,
            LawFirmId = uredId,
            ImeNaziv = naziv,
            Oib = SanitizeOib(oib),
            Adresa = adresa,
            Grad = grad,
            Tip = NormalizirajTip(tip) ?? NaslutiTip(naziv)
        };

        var created = await CreateClientAsync(record);
        if (created != null)
        {
            _logger.LogInformation("Auto-created client {Id} for user {UserId}", created.Id, userId);
            return (created.Id, true);
        }

        _logger.LogWarning("Failed to auto-create client for user {UserId}", userId);
        return (null, false);
    }

    // ═══════════════════════════════════════════════════════════
    //  Cases (Predmeti)
    // ═══════════════════════════════════════════════════════════

    public Task<CaseReadRecord?> CreateCaseAsync(CaseRecord record)
        => InsertAsync<CaseReadRecord>("cases", record);

    /// <summary>Active (non-deleted) cases for a user.</summary>
    public Task<List<CaseReadRecord>> GetCasesByUserAsync(string userId)
        => UuidRegex.IsMatch(userId)
            ? SelectAsync<CaseReadRecord>("cases", $"user_id=eq.{Uri.EscapeDataString(userId)}&is_deleted=eq.false")
            : Task.FromResult(new List<CaseReadRecord>());

    /// <summary>Active (non-deleted) cases linked to a client.</summary>
    public Task<List<CaseReadRecord>> GetCasesByClientAsync(string clientId)
        => UuidRegex.IsMatch(clientId)
            ? SelectAsync<CaseReadRecord>("cases", $"client_id=eq.{Uri.EscapeDataString(clientId)}&is_deleted=eq.false")
            : Task.FromResult(new List<CaseReadRecord>());

    /// <summary>Soft-deleted cases for the trash view (owner only).</summary>
    public Task<List<CaseReadRecord>> GetDeletedCasesByUserAsync(string userId)
        => UuidRegex.IsMatch(userId)
            ? SelectAsync<CaseReadRecord>("cases", $"user_id=eq.{Uri.EscapeDataString(userId)}&is_deleted=eq.true")
            : Task.FromResult(new List<CaseReadRecord>());

    public Task<CaseReadRecord?> GetCaseByIdAsync(string id)
        => GetByIdAsync<CaseReadRecord>("cases", id);

    public Task<CaseReadRecord?> UpdateCaseAsync(string id, object patch)
        => UpdateAsync<CaseReadRecord>("cases", id, patch);

    /// <summary>Soft-delete: sets is_deleted = true instead of removing the row.</summary>
    public Task<CaseReadRecord?> SoftDeleteCaseAsync(string id)
        => UpdateAsync<CaseReadRecord>("cases", id, new { is_deleted = true });

    /// <summary>Restore a soft-deleted case.</summary>
    public Task<CaseReadRecord?> RestoreCaseAsync(string id)
        => UpdateAsync<CaseReadRecord>("cases", id, new { is_deleted = false });

    /// <summary>Hard-delete — kept for admin/cleanup scenarios.</summary>
    public Task<bool> DeleteCaseAsync(string id)
        => DeleteAsync("cases", id);

    // ═══════════════════════════════════════════════════════════
    //  Troškovnici
    // ═══════════════════════════════════════════════════════════

    public Task<TroskovnikReadRecord?> SaveTroskovnikAsync(TroskovnikRecord record)
        => InsertAsync<TroskovnikReadRecord>("troskovnici", record);

    public Task<List<TroskovnikReadRecord>> GetTroskovniciByUserAsync(string userId)
        => UuidRegex.IsMatch(userId)
            ? SelectAsync<TroskovnikReadRecord>("troskovnici", $"user_id=eq.{Uri.EscapeDataString(userId)}")
            : Task.FromResult(new List<TroskovnikReadRecord>());

    public Task<List<TroskovnikReadRecord>> GetTroskovniciByClientAsync(string clientId)
        => UuidRegex.IsMatch(clientId)
            ? SelectAsync<TroskovnikReadRecord>("troskovnici", $"client_id=eq.{Uri.EscapeDataString(clientId)}")
            : Task.FromResult(new List<TroskovnikReadRecord>());

    public Task<TroskovnikReadRecord?> GetTroskovnikByIdAsync(string id)
        => GetByIdAsync<TroskovnikReadRecord>("troskovnici", id);

    public Task<bool> DeleteTroskovnikAsync(string id)
        => DeleteAsync("troskovnici", id);

    // ═══════════════════════════════════════════════════════════
    //  Client Profile — full profile with cases + troškovnici
    // ═══════════════════════════════════════════════════════════

    public async Task<ClientProfileResponse?> GetClientProfileAsync(string clientId)
    {
        var client = await GetClientByIdAsync(clientId);
        if (client == null) return null;

        var casesTask = GetCasesByClientAsync(clientId);
        var troskovniciTask = GetTroskovniciByClientAsync(clientId);

        await Task.WhenAll(casesTask, troskovniciTask);

        return new ClientProfileResponse
        {
            Client = client,
            Cases = casesTask.Result,
            Troskovnici = troskovniciTask.Result
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  Case Events / Upisnik
    // ═══════════════════════════════════════════════════════════

    public Task<CaseEventReadRecord?> CreateCaseEventAsync(CaseEventRecord record)
        => InsertAsync<CaseEventReadRecord>("case_events", record);

    public Task<List<CaseEventReadRecord>> GetCaseEventsByCaseAsync(string caseId, string userId)
        => UuidRegex.IsMatch(caseId) && UuidRegex.IsMatch(userId)
            ? SelectAsync<CaseEventReadRecord>(
                "case_events",
                $"case_id=eq.{Uri.EscapeDataString(caseId)}&user_id=eq.{Uri.EscapeDataString(userId)}",
                "event_date.desc")
            : Task.FromResult(new List<CaseEventReadRecord>());

    public Task<CaseEventReadRecord?> GetCaseEventByIdAsync(string id)
        => GetByIdAsync<CaseEventReadRecord>("case_events", id);

    public Task<CaseEventReadRecord?> UpdateCaseEventAsync(string id, object patch)
        => UpdateAsync<CaseEventReadRecord>("case_events", id, patch);

    public Task<bool> DeleteCaseEventAsync(string id)
        => DeleteAsync("case_events", id);

    /// <summary>
    /// Get all billable (pending) events for a case, ready for Troškovnik generation.
    /// </summary>
    public async Task<List<CaseEventReadRecord>> GetBillableEventsForCaseAsync(string caseId, string userId)
    {
        if (!UuidRegex.IsMatch(caseId) || !UuidRegex.IsMatch(userId))
            return [];

        return await SelectAsync<CaseEventReadRecord>(
            "case_events",
            $"case_id=eq.{Uri.EscapeDataString(caseId)}&user_id=eq.{Uri.EscapeDataString(userId)}&is_billable=eq.true&billing_status=eq.pending",
            "event_date.asc",
            500);
    }

    /// <summary>
    /// Mark events as billed (after Troškovnik is generated from them).
    /// Uses a single PostgREST PATCH with id=in.(...) filter instead of N sequential calls.
    /// </summary>
    public async Task<int> MarkEventsAsBilledAsync(IEnumerable<string> eventIds)
    {
        var ids = eventIds.Where(id => UuidRegex.IsMatch(id)).ToList();
        if (ids.Count == 0 || !_isConfigured) return 0;

        // Single batch PATCH: PATCH /case_events?id=in.(id1,id2,...)
        var csvIds = string.Join(",", ids.Select(Uri.EscapeDataString));
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/case_events?id=in.({csvIds})";
        var payload = JsonSerializer.Serialize(new { billing_status = "billed" }, JsonOpts);
        var request = CreateRequest(HttpMethod.Patch, url, payload);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Batch MarkAsBilled failed ({Status}): {Body}", response.StatusCode, body);
            return 0;
        }

        var rows = JsonSerializer.Deserialize<List<CaseEventReadRecord>>(body, JsonOpts);
        return rows?.Count ?? 0;
    }

    /// <summary>
    /// Batch-fetch multiple case events by their IDs in a single PostgREST query.
    /// Replaces N sequential GetCaseEventByIdAsync calls.
    /// </summary>
    public async Task<List<CaseEventReadRecord>> GetCaseEventsByIdsAsync(IEnumerable<string> eventIds)
    {
        var ids = eventIds.Where(id => UuidRegex.IsMatch(id)).ToList();
        if (ids.Count == 0 || !_isConfigured) return [];

        var csvIds = string.Join(",", ids.Select(Uri.EscapeDataString));
        return await SelectAsync<CaseEventReadRecord>(
            "case_events",
            $"id=in.({csvIds})",
            "created_at.desc",
            ids.Count);
    }
}
