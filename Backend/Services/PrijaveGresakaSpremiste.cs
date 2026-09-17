using System.Text;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>Pristup tablici `prijave_gresaka` (migracija 025).</summary>
public interface IPrijaveGresakaSpremiste
{
    /// <summary>Pripada li troškovnik uredu pozivatelja — pita se s korisnikovim tokenom, pa RLS odlučuje.</summary>
    Task<bool> TroskovnikJeDostupanAsync(string troskovnikId);

    /// <summary>Sprema prijavu i vraća njezin id, ili null kad baza odbije upis.</summary>
    Task<string?> SpremiAsync(PrijavaGreskeZapis zapis);

    Task OznaciEmailPoslanAsync(string id);

    /// <summary>Zadnje prijave kao sirovi JSON niz, za admin sučelje.</summary>
    Task<string?> PopisAsync(int limit);

    /// <summary>False kad prijava ne postoji ili upis nije uspio.</summary>
    Task<bool> PromijeniStatusAsync(string id, string status);
}

/// <summary>
/// Tablica je zaključana za korisnike (RLS: samo service_role), pa se upis, popis
/// i promjena statusa rade servisnim ključem — to je dopušteni slučaj iz
/// <see cref="ISupabaseRequestContext"/>: zapis koji korisnik ne smije čitati ni
/// mijenjati. Jedina provjera s korisnikovim tokenom je pripadnost troškovnika.
/// </summary>
public sealed class PrijaveGresakaSpremiste : IPrijaveGresakaSpremiste
{
    private const string Tablica = "prijave_gresaka";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ISupabaseRequestContext _supabase;
    private readonly ILogger<PrijaveGresakaSpremiste> _logger;

    public PrijaveGresakaSpremiste(
        HttpClient http, ISupabaseRequestContext supabase, ILogger<PrijaveGresakaSpremiste> logger)
    {
        _http = http;
        _supabase = supabase;
        _logger = logger;
    }

    private string Rest(string putanja) => $"{_supabase.Url}/rest/v1/{putanja}";

    public async Task<bool> TroskovnikJeDostupanAsync(string troskovnikId)
    {
        if (!Guid.TryParse(troskovnikId, out _) || !_supabase.IsConfigured) return false;

        var zahtjev = new HttpRequestMessage(HttpMethod.Get,
            Rest($"troskovnici?select=id&id=eq.{troskovnikId}&limit=1"));
        _supabase.ApplyUserAuth(zahtjev);

        var odgovor = await _http.SendAsync(zahtjev);
        if (!odgovor.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await odgovor.Content.ReadAsStringAsync());
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
    }

    public async Task<string?> SpremiAsync(PrijavaGreskeZapis zapis)
    {
        if (!_supabase.IsConfigured) return null;

        var zahtjev = new HttpRequestMessage(HttpMethod.Post, Rest(Tablica))
        {
            Content = new StringContent(JsonSerializer.Serialize(zapis, JsonOpts), Encoding.UTF8, "application/json"),
        };
        // service_role: tablica nema INSERT politiku za korisnike (vidi klasu).
        _supabase.ApplyServiceAuth(zahtjev);
        zahtjev.Headers.Add("Prefer", "return=representation");

        var odgovor = await _http.SendAsync(zahtjev);
        var tijelo = await odgovor.Content.ReadAsStringAsync();
        if (!odgovor.IsSuccessStatusCode)
        {
            _logger.LogError("Spremanje prijave greške nije uspjelo ({Status}): {Body}", odgovor.StatusCode, tijelo);
            return null;
        }

        using var doc = JsonDocument.Parse(tijelo);
        return doc.RootElement.ValueKind == JsonValueKind.Array
               && doc.RootElement.GetArrayLength() > 0
               && doc.RootElement[0].TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    public async Task OznaciEmailPoslanAsync(string id)
    {
        var zahtjev = new HttpRequestMessage(HttpMethod.Patch, Rest($"{Tablica}?id=eq.{id}"))
        {
            Content = new StringContent("{\"email_poslan\":true}", Encoding.UTF8, "application/json"),
        };
        _supabase.ApplyServiceAuth(zahtjev);
        var odgovor = await _http.SendAsync(zahtjev);
        if (!odgovor.IsSuccessStatusCode)
            _logger.LogWarning("Oznaka email_poslan za prijavu {Id} nije upisana ({Status}).", id, odgovor.StatusCode);
    }

    public async Task<string?> PopisAsync(int limit)
    {
        if (!_supabase.IsConfigured) return null;

        var zahtjev = new HttpRequestMessage(HttpMethod.Get,
            Rest($"{Tablica}?select=*&order=created_at.desc&limit={Math.Clamp(limit, 1, 200)}"));
        // service_role: admin pregled preko svih ureda.
        _supabase.ApplyServiceAuth(zahtjev);

        var odgovor = await _http.SendAsync(zahtjev);
        var tijelo = await odgovor.Content.ReadAsStringAsync();
        if (odgovor.IsSuccessStatusCode) return tijelo;

        _logger.LogError("Popis prijava grešaka nije dohvaćen ({Status}): {Body}", odgovor.StatusCode, tijelo);
        return null;
    }

    public async Task<bool> PromijeniStatusAsync(string id, string status)
    {
        if (!Guid.TryParse(id, out _) || !_supabase.IsConfigured) return false;

        var zahtjev = new HttpRequestMessage(HttpMethod.Patch, Rest($"{Tablica}?id=eq.{id}"))
        {
            Content = new StringContent(JsonSerializer.Serialize(new { status }), Encoding.UTF8, "application/json"),
        };
        _supabase.ApplyServiceAuth(zahtjev);
        zahtjev.Headers.Add("Prefer", "return=representation");

        var odgovor = await _http.SendAsync(zahtjev);
        if (!odgovor.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await odgovor.Content.ReadAsStringAsync());
        return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0;
    }
}
