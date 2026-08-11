using System.Text;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Implementacija brisanja računa i izvoza podataka.
///
/// Cijeli servis radi service_role ključem, i to je ovdje opravdano: briše
/// redove u `auth.users` (moguće samo kroz Admin API), uklanja profile drugih
/// članova ureda, i dira `user_profiles.law_firm_id` koji politika
/// `profiles_update_own` iz migracije 016 izričito brani mijenjati. Nijedna od
/// tih radnji nema — niti smije imati — korisničku politiku.
///
/// Ovlaštenje se zato provjerava u kodu, prije svakog poziva: tko zove, koja mu
/// je uloga, i je li potvrda točna.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseRequestContext _supabase;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AccountService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Tablice koje se izvoze. Sve su opsegnute uredom (migracija 015), pa se
    /// filtriraju po law_firm_id.
    /// </summary>
    private static readonly string[] ExportTables =
    [
        "clients", "cases", "troskovnici", "case_events", "invoices",
        "evaluations", "contract_analyses", "chronologies", "legal_strategies",
        "zk_analyses", "case_activities", "documents", "troskovnik_templates",
        "audit_logs"
    ];

    public AccountService(
        HttpClient httpClient,
        ISupabaseRequestContext supabase,
        IAuditLogService auditLog,
        ILogger<AccountService> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _auditLog = auditLog;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    //  Izvoz (GDPR čl. 20)
    // ═══════════════════════════════════════════════════════════

    public async Task<Dictionary<string, object>?> ExportFirmDataAsync(string userId)
    {
        var profile = await GetProfileAsync(userId);
        if (profile is null) return null;

        var export = new Dictionary<string, object>
        {
            ["meta"] = new
            {
                izvezeno = DateTime.UtcNow.ToString("o"),
                user_id = userId,
                law_firm_id = profile.Value.FirmId,
                platforma = "PravoTek",
                pravna_osnova = "Članak 20. GDPR — pravo na prenosivost podataka",
                opseg = "Svi podaci odvjetničkog ureda kojem korisnik pripada."
            }
        };

        foreach (var table in ExportTables)
        {
            var rows = await SelectAsService(
                $"{table}?law_firm_id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}&select=*");
            export[table] = rows ?? new List<object>();
        }

        // Profili članova i podaci ureda idu uz to — bez njih izvoz nije cjelovit.
        export["law_firm"] = await SelectAsService(
            $"law_firms?id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}&select=*")
            ?? new List<object>();

        export["members"] = await SelectAsService(
            $"user_profiles?law_firm_id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}" +
            "&select=user_id,full_name,email,phone_number,role,created_at")
            ?? new List<object>();

        return export;
    }

    // ═══════════════════════════════════════════════════════════
    //  Brisanje korisnika
    // ═══════════════════════════════════════════════════════════

    public async Task<(DeleteAccountResult?, DeleteBlockedResponse?)> DeleteOwnAccountAsync(string userId)
    {
        var profile = await GetProfileAsync(userId);
        if (profile is null)
            return (null, Blocked("Profil nije pronađen.", "PROFILE_NOT_FOUND"));

        var members = await CountFirmMembersAsync(profile.Value.FirmId);

        // Jedini član: brisanje bi ostavilo ured s podacima, a bez ikoga tko im
        // može pristupiti. Za odvjetnički spis to nije prihvatljivo stanje —
        // ili ostaje netko odgovoran, ili se ured briše svjesno i u cijelosti.
        if (members <= 1)
            return (null, Blocked(
                "Vi ste jedini član ureda. Brisanjem samo svog računa podaci ureda ostali bi " +
                "bez ijedne osobe koja im može pristupiti. Ako želite prekinuti korištenje, " +
                "obrišite cijeli ured — to briše i sve predmete, stranke i račune.",
                "LAST_MEMBER"));

        // Vlasnik s kolegama: ured bi ostao bez vlasnika, pa nitko ne bi mogao
        // mijenjati postavke ureda ni čitati dnevnik.
        if (string.Equals(profile.Value.Role, "owner", StringComparison.OrdinalIgnoreCase))
            return (null, Blocked(
                "Vi ste vlasnik ureda. Prije brisanja računa prenesite vlasništvo na drugog člana.",
                "OWNER_MUST_TRANSFER"));

        // Od ovog trenutka brisanje je dopušteno.
        // Redoslijed je bitan: user_profiles ima FK na law_firms s ON DELETE
        // RESTRICT, a auth.users briše profil kaskadno — pa profil ide prvi.
        await DeleteAsService($"user_mfa?user_id=eq.{Uri.EscapeDataString(userId)}");
        await DeleteAsService($"user_profiles?user_id=eq.{Uri.EscapeDataString(userId)}");

        var deleted = await DeleteAuthUserAsync(userId);
        if (!deleted)
        {
            _logger.LogError("Brisanje auth.users neuspjelo za {UserId} — profil je vec obrisan", userId);
            return (null, Blocked(
                "Brisanje računa nije dovršeno. Obratite se podršci.", "AUTH_DELETE_FAILED"));
        }

        // Zapisi ureda ostaju; migracija 020 im je user_id postavila na NULL.
        await _auditLog.LogAsync(
            userId, profile.Value.FirmId, "account.deleted",
            "/api/account", "DELETE", 200);

        _logger.LogInformation("Racun {UserId} obrisan; ured {FirmId} zadrzava podatke",
            userId, profile.Value.FirmId);

        return (new DeleteAccountResult
        {
            Success = true,
            Message = "Vaš račun je obrisan. Predmeti i stranke ostaju uredu."
        }, null);
    }

    // ═══════════════════════════════════════════════════════════
    //  Brisanje ureda
    // ═══════════════════════════════════════════════════════════

    public async Task<(DeleteAccountResult?, DeleteBlockedResponse?)> DeleteFirmAsync(
        string userId, string firmNameConfirmation)
    {
        var profile = await GetProfileAsync(userId);
        if (profile is null)
            return (null, Blocked("Profil nije pronađen.", "PROFILE_NOT_FOUND"));

        if (!string.Equals(profile.Value.Role, "owner", StringComparison.OrdinalIgnoreCase))
            return (null, Blocked(
                "Samo vlasnik ureda može obrisati ured.", "OWNER_REQUIRED"));

        var firm = await SelectAsService(
            $"law_firms?id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}&select=id,name");
        var firmName = ReadFirstString(firm, "name");

        if (firmName is null)
            return (null, Blocked("Ured nije pronađen.", "FIRM_NOT_FOUND"));

        // Potvrda je naziv ureda: mora se pročitati i prepisati, za razliku od
        // fiksne riječi koja se otipka napamet.
        if (!string.Equals(firmName.Trim(), firmNameConfirmation?.Trim(), StringComparison.Ordinal))
            return (null, Blocked(
                "Naziv ureda se ne podudara. Prepišite ga točno.", "CONFIRMATION_MISMATCH"));

        var memberIds = await GetFirmMemberIdsAsync(profile.Value.FirmId);

        // Dnevnik PRIJE brisanja: api_audit_logs.tenant_id je ON DELETE SET NULL,
        // pa zapis preživi nestanak ureda. Da smo pisali poslije, ne bismo imali
        // ni ured ni korisnika za upisati.
        await _auditLog.LogAsync(
            userId, profile.Value.FirmId, "firm.deleted",
            "/api/account/firm", "DELETE", 200);

        // 1. Datoteke u Storageu — prefiks putanje je law_firm_id (migracija 018).
        await DeleteStorageFolderAsync("zk-izvadci", profile.Value.FirmId);
        await DeleteStorageFolderAsync("case-documents", profile.Value.FirmId);

        // 2. MFA i profili svih članova. Profili moraju otići prije ureda, jer je
        //    user_profiles.law_firm_id ON DELETE RESTRICT (migracija 015).
        foreach (var memberId in memberIds)
            await DeleteAsService($"user_mfa?user_id=eq.{Uri.EscapeDataString(memberId)}");

        await DeleteAsService(
            $"user_profiles?law_firm_id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}");

        // 3. Sam ured. law_firm_id je na svim poslovnim tablicama ON DELETE
        //    CASCADE, pa ovo odnosi predmete, stranke, troškovnike i ostalo.
        await DeleteAsService($"law_firms?id=eq.{Uri.EscapeDataString(profile.Value.FirmId)}");

        // 4. Računi svih članova.
        foreach (var memberId in memberIds)
            await DeleteAuthUserAsync(memberId);

        _logger.LogWarning("Ured {FirmId} obrisan sa svim podacima; {Count} racuna uklonjeno",
            profile.Value.FirmId, memberIds.Count);

        return (new DeleteAccountResult
        {
            Success = true,
            Message = $"Ured „{firmName}\" i svi podaci su obrisani."
        }, null);
    }

    // ═══════════════════════════════════════════════════════════
    //  Pomoćne — sve idu service_role ključem, vidi komentar na klasi
    // ═══════════════════════════════════════════════════════════

    private readonly record struct ProfileInfo(string FirmId, string Role);

    private async Task<ProfileInfo?> GetProfileAsync(string userId)
    {
        var rows = await SelectAsService(
            $"user_profiles?user_id=eq.{Uri.EscapeDataString(userId)}&select=law_firm_id,role&limit=1");

        var firmId = ReadFirstString(rows, "law_firm_id");
        var role = ReadFirstString(rows, "role");

        if (string.IsNullOrWhiteSpace(firmId)) return null;
        return new ProfileInfo(firmId, role ?? "lawyer");
    }

    private async Task<int> CountFirmMembersAsync(string firmId)
        => (await GetFirmMemberIdsAsync(firmId)).Count;

    private async Task<List<string>> GetFirmMemberIdsAsync(string firmId)
    {
        var rows = await SelectAsService(
            $"user_profiles?law_firm_id=eq.{Uri.EscapeDataString(firmId)}&select=user_id");

        var ids = new List<string>();
        if (rows is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            foreach (var row in arr.EnumerateArray())
                if (row.TryGetProperty("user_id", out var v) && v.GetString() is { } id)
                    ids.Add(id);
        }
        return ids;
    }

    private async Task<object?> SelectAsService(string pathAndQuery)
    {
        if (!_supabase.IsConfigured) return null;

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_supabase.Url}/rest/v1/{pathAndQuery}");
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Citanje {Path} neuspjelo ({Status})", pathAndQuery, response.StatusCode);
                return null;
            }

            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri citanju {Path}", pathAndQuery);
            return null;
        }
    }

    private async Task<bool> DeleteAsService(string pathAndQuery)
    {
        if (!_supabase.IsConfigured) return false;

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete, $"{_supabase.Url}/rest/v1/{pathAndQuery}");
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brisanje {Path} neuspjelo ({Status}): {Body}",
                    pathAndQuery, response.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri brisanju {Path}", pathAndQuery);
            return false;
        }
    }

    /// <summary>Uklanja red iz auth.users. Jedino kroz Admin API i service_role.</summary>
    private async Task<bool> DeleteAuthUserAsync(string userId)
    {
        if (!_supabase.IsConfigured) return false;

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete, $"{_supabase.Url}/auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Admin brisanje korisnika {UserId} neuspjelo ({Status}): {Body}",
                    userId, response.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri brisanju korisnika {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Briše sve datoteke ureda iz jednog bucketa.
    /// Storage nema brisanje po prefiksu, pa se putanje prvo popišu.
    /// </summary>
    private async Task DeleteStorageFolderAsync(string bucket, string firmId)
    {
        if (!_supabase.IsConfigured) return;

        try
        {
            var listRequest = new HttpRequestMessage(
                HttpMethod.Post, $"{_supabase.Url}/storage/v1/object/list/{bucket}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { prefix = firmId, limit = 1000 }, JsonOpts),
                    Encoding.UTF8, "application/json")
            };
            _supabase.ApplyServiceAuth(listRequest);

            var listResponse = await _httpClient.SendAsync(listRequest);
            if (!listResponse.IsSuccessStatusCode) return;

            var listBody = await listResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(listBody);

            var paths = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
                if (item.TryGetProperty("name", out var n) && n.GetString() is { } name)
                    paths.Add($"{firmId}/{name}");

            if (paths.Count == 0) return;

            var deleteRequest = new HttpRequestMessage(
                HttpMethod.Delete, $"{_supabase.Url}/storage/v1/object/{bucket}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { prefixes = paths }, JsonOpts),
                    Encoding.UTF8, "application/json")
            };
            _supabase.ApplyServiceAuth(deleteRequest);

            await _httpClient.SendAsync(deleteRequest);
            _logger.LogInformation("Obrisano {Count} datoteka iz {Bucket} za ured {FirmId}",
                paths.Count, bucket, firmId);
        }
        catch (Exception ex)
        {
            // Ne prekidamo brisanje ureda zbog datoteka — redovi u bazi su
            // važniji, a zaostale datoteke su vidljive i mogu se počistiti.
            _logger.LogError(ex, "Greska pri brisanju datoteka {Bucket}/{FirmId}", bucket, firmId);
        }
    }

    private static string? ReadFirstString(object? rows, string property)
    {
        if (rows is not JsonElement { ValueKind: JsonValueKind.Array } arr) return null;
        if (arr.GetArrayLength() == 0) return null;
        return arr[0].TryGetProperty(property, out var v) ? v.GetString() : null;
    }

    private static DeleteBlockedResponse Blocked(string error, string code)
        => new() { Error = error, Code = code };
}
