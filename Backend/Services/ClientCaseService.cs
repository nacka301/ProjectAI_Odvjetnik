using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Service for managing Clients (Stranke), Cases (Predmeti), and persisted Troškovnici.
/// Uses Supabase PostgREST API with service_role key.
/// </summary>
public sealed class ClientCaseService
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
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

    public ClientCaseService(HttpClient httpClient, IConfiguration config, ILogger<ClientCaseService> logger)
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
    //  Generic helpers
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
        string? naziv,
        string? oib,
        string? adresa,
        string? grad)
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
            ImeNaziv = naziv,
            Oib = string.IsNullOrWhiteSpace(oib) ? null : oib,
            Adresa = adresa,
            Grad = grad,
            Tip = "fizicka"
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

    public Task<List<CaseReadRecord>> GetCasesByUserAsync(string userId)
        => UuidRegex.IsMatch(userId)
            ? SelectAsync<CaseReadRecord>("cases", $"user_id=eq.{Uri.EscapeDataString(userId)}")
            : Task.FromResult(new List<CaseReadRecord>());

    public Task<List<CaseReadRecord>> GetCasesByClientAsync(string clientId)
        => UuidRegex.IsMatch(clientId)
            ? SelectAsync<CaseReadRecord>("cases", $"client_id=eq.{Uri.EscapeDataString(clientId)}")
            : Task.FromResult(new List<CaseReadRecord>());

    public Task<CaseReadRecord?> GetCaseByIdAsync(string id)
        => GetByIdAsync<CaseReadRecord>("cases", id);

    public Task<CaseReadRecord?> UpdateCaseAsync(string id, object patch)
        => UpdateAsync<CaseReadRecord>("cases", id, patch);

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
}
