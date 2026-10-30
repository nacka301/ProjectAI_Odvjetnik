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
    private readonly ITenantResolver _tenantResolver;
    private readonly IUsageService _usage;
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
        ITenantResolver tenantResolver,
        IUsageService usage,
        ILogger<AccountService> logger)
    {
        _httpClient = httpClient;
        _supabase = supabase;
        _auditLog = auditLog;
        _tenantResolver = tenantResolver;
        _usage = usage;
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

        await DeleteFirmCoreAsync(profile.Value.FirmId, memberIds, userId, "/api/account/firm");

        _logger.LogWarning("Ured {FirmId} obrisan sa svim podacima; {Count} racuna uklonjeno",
            profile.Value.FirmId, memberIds.Count);

        return (new DeleteAccountResult
        {
            Success = true,
            Message = $"Ured „{firmName}\" i svi podaci su obrisani."
        }, null);
    }

    // ═══════════════════════════════════════════════════════════
    //  Admin-ciljano brisanje
    // ═══════════════════════════════════════════════════════════

    public async Task<AdminDeletionPreview?> GetDeletionPreviewAsync(string targetUserId)
    {
        var profile = await GetProfileAsync(targetUserId);
        if (profile is null) return null;

        var firmId = profile.Value.FirmId;
        var firm = await SelectAsService(
            $"law_firms?id=eq.{Uri.EscapeDataString(firmId)}&select=name");

        var memberIds = await GetFirmMemberIdsAsync(firmId);
        var memberEmails = new List<string>();
        foreach (var id in memberIds)
        {
            var e = await GetAuthEmailAsync(id);
            if (!string.IsNullOrWhiteSpace(e)) memberEmails.Add(e!);
        }

        return new AdminDeletionPreview
        {
            UserId       = targetUserId,
            Email        = await GetAuthEmailAsync(targetUserId),
            Role         = profile.Value.Role,
            FirmId       = firmId,
            FirmName     = ReadFirstString(firm, "name"),
            MemberCount  = memberIds.Count,
            MemberEmails = memberEmails,
            Clients      = await CountFirmRowsAsync("clients", firmId),
            Cases        = await CountFirmRowsAsync("cases", firmId),
            Invoices     = await CountFirmRowsAsync("invoices", firmId),
        };
    }

    public async Task<(DeleteAccountResult?, DeleteBlockedResponse?)> AdminDeleteUserAsync(
        string targetUserId, AdminDeleteMode mode, string confirmation)
    {
        var profile = await GetProfileAsync(targetUserId);
        if (profile is null)
            return (null, Blocked("Profil korisnika nije pronađen.", "PROFILE_NOT_FOUND"));

        var firmId = profile.Value.FirmId;

        if (mode == AdminDeleteMode.Firm)
        {
            var firm = await SelectAsService(
                $"law_firms?id=eq.{Uri.EscapeDataString(firmId)}&select=name");
            var firmName = ReadFirstString(firm, "name");
            if (firmName is null)
                return (null, Blocked("Ured nije pronađen.", "FIRM_NOT_FOUND"));

            // Ista potvrda kao samouslužno brisanje ureda: prepisani naziv ureda.
            if (!string.Equals(firmName.Trim(), confirmation?.Trim(), StringComparison.Ordinal))
                return (null, Blocked(
                    "Naziv ureda se ne podudara. Prepišite ga točno.", "CONFIRMATION_MISMATCH"));

            var memberIds = await GetFirmMemberIdsAsync(firmId);
            await DeleteFirmCoreAsync(firmId, memberIds, targetUserId, $"/api/admin/users/{targetUserId}");

            _logger.LogWarning(
                "Admin je obrisao ured {FirmId} sa svim podacima; {Count} racuna uklonjeno.",
                firmId, memberIds.Count);

            return (new DeleteAccountResult
            {
                Success = true,
                Message = $"Ured „{firmName}\" i svi podaci su obrisani."
            }, null);
        }

        // ── Member: briše samo ovog korisnika, ured i ostali članovi ostaju ──
        var email = await GetAuthEmailAsync(targetUserId);
        if (string.IsNullOrWhiteSpace(email))
            return (null, Blocked(
                "E-mail korisnika nije pronađen, pa potvrda nije moguća.", "AUTH_EMAIL_NOT_FOUND"));

        // Potvrda je e-mail korisnika (prepisan), neovisno o veličini slova.
        if (!string.Equals(email.Trim(), confirmation?.Trim(), StringComparison.OrdinalIgnoreCase))
            return (null, Blocked(
                "E-mail se ne podudara. Prepišite ga točno.", "CONFIRMATION_MISMATCH"));

        // Nije blokada: admin je izborom „samo član" i prepisom e-maila to već
        // izričito potvrdio. Ali ako ostaje ured bez ijednog vlasnika, to je
        // stanje koje netko mora znati da postoji — zato upozorenje u dnevnik.
        var remaining = (await GetFirmMemberIdsAsync(firmId)).Count - 1;
        if (remaining > 0 && string.Equals(profile.Value.Role, "owner", StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning(
                "Admin brise vlasnika {UserId}; ured {FirmId} ostaje s {Count} clana(ova) bez vlasnika.",
                targetUserId, firmId, remaining);

        await DeleteAsService($"user_mfa?user_id=eq.{Uri.EscapeDataString(targetUserId)}");
        await DeleteAsService($"user_profiles?user_id=eq.{Uri.EscapeDataString(targetUserId)}");

        var deleted = await DeleteAuthUserAsync(targetUserId);
        if (!deleted)
        {
            _logger.LogError("Admin brisanje auth.users neuspjelo za {UserId} — profil je vec obrisan", targetUserId);
            return (null, Blocked(
                "Brisanje računa nije dovršeno. Provjerite dnevnik.", "AUTH_DELETE_FAILED"));
        }

        await _auditLog.LogAsync(
            targetUserId, firmId, "account.deleted.admin",
            $"/api/admin/users/{targetUserId}", "DELETE", 200);

        // Oslobodi mail za čistu ponovnu registraciju: pozivnice na taj e-mail u
        // SVIM uredima (inače re-registracija upadne u tuđi ured) + waitlist.
        await DeleteFirmInvitesByEmailAsync(email!);
        await DeleteWaitlistByEmailAsync(email!);

        _tenantResolver.InvalidateCache(targetUserId);
        _usage.InvalidateCache(firmId);

        _logger.LogInformation("Admin je obrisao korisnika {UserId}; ured {FirmId} zadrzava podatke.",
            targetUserId, firmId);

        return (new DeleteAccountResult
        {
            Success = true,
            Message = "Korisnik je obrisan. Ured i preostali podaci ostaju."
        }, null);
    }

    /// <summary>
    /// Jezgra potpunog brisanja ureda. Redoslijed propisan nalazom i dijele ga
    /// samouslužno <see cref="DeleteFirmAsync"/> i admin brisanje. „Dnevnik prvo,
    /// auth.users zadnje" — jer PostgREST nije jedna transakcija, pa djelomičan
    /// neuspjeh mora ostaviti čitljiv trag i osloboditi mail tek na kraju.
    /// </summary>
    private async Task DeleteFirmCoreAsync(
        string firmId, IReadOnlyList<string> memberIds, string actorUserId, string endpoint)
    {
        // E-mailovi članova PRIJE brisanja — trebaju za čišćenje pozivnica/waitliste.
        var emails = new List<string>();
        foreach (var id in memberIds)
        {
            var e = await GetAuthEmailAsync(id);
            if (!string.IsNullOrWhiteSpace(e)) emails.Add(e!);
        }

        // 1. Dnevnik PRIJE brisanja: api_audit_logs.tenant_id je ON DELETE SET NULL,
        //    pa zapis preživi nestanak ureda.
        await _auditLog.LogAsync(actorUserId, firmId, "firm.deleted", endpoint, "DELETE", 200);

        // 2. Datoteke u Storageu — prefiks putanje je law_firm_id (migracija 018).
        await DeleteStorageFolderAsync("zk-izvadci", firmId);
        await DeleteStorageFolderAsync("case-documents", firmId);

        // 3. MFA i profili svih članova. Profili moraju prije ureda, jer je
        //    user_profiles.law_firm_id ON DELETE RESTRICT (migracija 015).
        foreach (var id in memberIds)
            await DeleteAsService($"user_mfa?user_id=eq.{Uri.EscapeDataString(id)}");

        await DeleteAsService($"user_profiles?law_firm_id=eq.{Uri.EscapeDataString(firmId)}");

        // 4. Sam ured. law_firm_id je na svim poslovnim tablicama ON DELETE CASCADE,
        //    pa ovo odnosi predmete, stranke, troškovnike, pretplatu, statistiku i
        //    pozivnice OVOG ureda.
        await DeleteAsService($"law_firms?id=eq.{Uri.EscapeDataString(firmId)}");

        // 5. auth.users svih članova — ovo oslobađa e-mailove.
        foreach (var id in memberIds)
            await DeleteAuthUserAsync(id);

        // 6. Pozivnice na te e-mailove u DRUGIM uredima + waitlist. (Pozivnice
        //    vlastitog ureda otišle su kaskadom u koraku 4.)
        foreach (var e in emails)
        {
            await DeleteFirmInvitesByEmailAsync(e);
            await DeleteWaitlistByEmailAsync(e);
        }

        // 7. Invalidacija procesnih cacheva, inače stara mapiranja žive do 5 min.
        foreach (var id in memberIds)
            _tenantResolver.InvalidateCache(id);
        _usage.InvalidateCache(firmId);
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
    ///
    /// Listanje je paginirano: Storage list vraća najviše 1000 stavki po pozivu,
    /// pa se ide u petlji s <c>offset</c> dok stranica ne bude kraća od pune —
    /// bez toga bi ured s više od 1000 datoteka ostao s ostatkom kao siročadi.
    /// </summary>
    private async Task DeleteStorageFolderAsync(string bucket, string firmId)
    {
        if (!_supabase.IsConfigured) return;

        const int PageSize = 1000;
        var totalDeleted = 0;

        try
        {
            for (var offset = 0; ; offset += PageSize)
            {
                var listRequest = new HttpRequestMessage(
                    HttpMethod.Post, $"{_supabase.Url}/storage/v1/object/list/{bucket}")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(
                            new { prefix = firmId, limit = PageSize, offset }, JsonOpts),
                        Encoding.UTF8, "application/json")
                };
                _supabase.ApplyServiceAuth(listRequest);

                var listResponse = await _httpClient.SendAsync(listRequest);
                if (!listResponse.IsSuccessStatusCode) break;

                var listBody = await listResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(listBody);

                var pageCount = doc.RootElement.GetArrayLength();
                if (pageCount == 0) break;

                var paths = new List<string>();
                foreach (var item in doc.RootElement.EnumerateArray())
                    if (item.TryGetProperty("name", out var n) && n.GetString() is { } name)
                        paths.Add($"{firmId}/{name}");

                if (paths.Count > 0)
                {
                    var deleteRequest = new HttpRequestMessage(
                        HttpMethod.Delete, $"{_supabase.Url}/storage/v1/object/{bucket}")
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(new { prefixes = paths }, JsonOpts),
                            Encoding.UTF8, "application/json")
                    };
                    _supabase.ApplyServiceAuth(deleteRequest);
                    await _httpClient.SendAsync(deleteRequest);
                    totalDeleted += paths.Count;
                }

                // Zadnja stranica: manje od pune znači da više nema što listati.
                if (pageCount < PageSize) break;
            }

            if (totalDeleted > 0)
                _logger.LogInformation("Obrisano {Count} datoteka iz {Bucket} za ured {FirmId}",
                    totalDeleted, bucket, firmId);
        }
        catch (Exception ex)
        {
            // Ne prekidamo brisanje ureda zbog datoteka — redovi u bazi su
            // važniji, a zaostale datoteke su vidljive i mogu se počistiti.
            _logger.LogError(ex, "Greska pri brisanju datoteka {Bucket}/{FirmId}", bucket, firmId);
        }
    }

    /// <summary>Autoritativni e-mail korisnika iz auth.users (Admin API, service_role).</summary>
    private async Task<string?> GetAuthEmailAsync(string userId)
    {
        if (!_supabase.IsConfigured) return null;

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_supabase.Url}/auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
            _supabase.ApplyServiceAuth(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greska pri citanju e-maila za {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Briše otvorene/sve pozivnice na e-mail u SVIM uredima. Indeks je na
    /// <c>lower(email)</c>, pa <c>ilike</c> (bez wildcarda) daje jednakost
    /// neovisnu o veličini slova.
    /// </summary>
    private async Task DeleteFirmInvitesByEmailAsync(string email)
        => await DeleteAsService($"firm_invites?email=ilike.{Uri.EscapeDataString(email)}");

    /// <summary>Briše red s liste čekanja za taj e-mail (isti razlog za ilike).</summary>
    private async Task DeleteWaitlistByEmailAsync(string email)
        => await DeleteAsService($"waitlist?email=ilike.{Uri.EscapeDataString(email)}");

    /// <summary>
    /// Broj redova tablice za dani ured, bez povlačenja podataka: PostgREST
    /// <c>Prefer: count=exact</c> + <c>Range 0-0</c> vraća ukupan broj u zaglavlju
    /// <c>Content-Range</c> (npr. <c>0-0/123</c> ili <c>*/0</c> kad je prazno).
    /// </summary>
    private async Task<int> CountFirmRowsAsync(string table, string firmId)
    {
        if (!_supabase.IsConfigured) return 0;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_supabase.Url}/rest/v1/{table}?law_firm_id=eq.{Uri.EscapeDataString(firmId)}&select=id");
            _supabase.ApplyServiceAuth(request);
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
            request.Headers.TryAddWithoutValidation("Range-Unit", "items");
            request.Headers.TryAddWithoutValidation("Range", "0-0");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return 0;

            if (!response.Content.Headers.TryGetValues("Content-Range", out var vals) &&
                !response.Headers.TryGetValues("Content-Range", out vals))
                return 0;

            var cr = vals.FirstOrDefault();
            var slash = cr?.LastIndexOf('/') ?? -1;
            return slash >= 0 && int.TryParse(cr!.AsSpan(slash + 1), out var n) ? n : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Greska pri brojanju {Table} za ured {FirmId}", table, firmId);
            return 0;
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
