using System.Text.Json.Serialization;

namespace Backend.Models;

// ══════════════════════════════════════════════════════════════
//  Brisanje računa i izvoz podataka (GDPR čl. 17 i čl. 20)
// ══════════════════════════════════════════════════════════════

/// <summary>
/// Zahtjev za brisanje vlastitog korisničkog računa.
///
/// Potvrda se traži da bi radnja bila namjerna, a ne posljedica krivog klika.
/// Identitet korisnika NIJE dio zahtjeva — dolazi iz provjerenog tokena.
/// </summary>
public sealed class DeleteAccountRequest
{
    /// <summary>Mora biti točno "OBRIŠI RAČUN".</summary>
    public string Confirmation { get; set; } = "";
}

/// <summary>
/// Zahtjev za brisanje cijelog ureda sa svim podacima.
///
/// Namjerno odvojeno od brisanja računa: to su dvije različite namjere, pa
/// zaslužuju dvije različite radnje. Potvrda je NAZIV UREDA, ne fiksna riječ —
/// da bi je korisnik morao pročitati i prepisati, a ne otipkati napamet.
/// </summary>
public sealed class DeleteFirmRequest
{
    /// <summary>Mora se točno podudarati s nazivom ureda.</summary>
    public string FirmNameConfirmation { get; set; } = "";
}

/// <summary>Odgovor na pokušaj brisanja koji sustav odbija, s razlogom u kodu.</summary>
public sealed class DeleteBlockedResponse
{
    public string Error { get; set; } = "";

    /// <summary>
    /// Strojno čitljiv razlog, da sučelje može ponuditi pravi sljedeći korak:
    ///   LAST_MEMBER          — jedini ste član; brisanje računa bi ostavilo ured bez ikoga
    ///   OWNER_MUST_TRANSFER  — vlasnik ste, a u uredu ima još ljudi
    ///   CONFIRMATION_MISMATCH— potvrda se ne podudara
    /// </summary>
    public string Code { get; set; } = "";
}

/// <summary>Sažetak onoga što je brisanje stvarno obavilo.</summary>
public sealed class DeleteAccountResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    /// <summary>Broj zapisa kojima je uklonjeno autorstvo, po tablicama.</summary>
    public int RecordsAnonymised { get; set; }
}

// ══════════════════════════════════════════════════════════════
//  Admin-ciljano brisanje korisnika (gumb na admin dashboardu)
//
//  Za razliku od samouslužnog brisanja, ovdje cilj (userId) dolazi iz rute,
//  a NE iz tokena. Identitet POZIVATELJA i dalje dolazi isključivo iz
//  provjerenog admin tokena preko AdminGuardMiddleware (`/api/admin/*` →
//  ADMIN_EMAIL, fail-closed). userId je samo meta radnje.
// ══════════════════════════════════════════════════════════════

/// <summary>Način admin-brisanja: samo član ureda, ili cijeli ured sa svime.</summary>
public enum AdminDeleteMode
{
    /// <summary>Briše samo ciljanog korisnika; ured i ostali članovi ostaju.</summary>
    Member,

    /// <summary>Briše cijeli ured sa svim podacima i računima svih članova.</summary>
    Firm
}

/// <summary>
/// Zahtjev admina za brisanje ciljanog korisnika. Potvrda se prepisuje i ovisi
/// o načinu — naziv ureda za <see cref="AdminDeleteMode.Firm"/>, e-mail za
/// <see cref="AdminDeleteMode.Member"/> — da radnja bude namjerna i nad pravim
/// računom, a ne posljedica krivog klika.
/// </summary>
public sealed class AdminDeleteRequest
{
    /// <summary>"member" ili "firm".</summary>
    public string Mode { get; set; } = "";

    /// <summary>Naziv ureda (firm) ili e-mail korisnika (member) — mora se podudarati točno.</summary>
    public string Confirmation { get; set; } = "";
}

/// <summary>
/// Pretpregled onoga što će admin-brisanje ukloniti. Prikazuje se prije potvrde
/// da admin odluku donosi nad stvarnim brojkama, a ne naslijepo. Snake_case
/// imena da bude u skladu s ostatkom admin API-ja (vidi AdminUserDto).
/// </summary>
public sealed class AdminDeletionPreview
{
    [JsonPropertyName("user_id")]       public string UserId { get; set; } = "";
    [JsonPropertyName("email")]         public string? Email { get; set; }
    [JsonPropertyName("role")]          public string Role { get; set; } = "";
    [JsonPropertyName("law_firm_id")]   public string? FirmId { get; set; }
    [JsonPropertyName("firm_name")]     public string? FirmName { get; set; }
    [JsonPropertyName("member_count")]  public int MemberCount { get; set; }
    [JsonPropertyName("member_emails")] public List<string> MemberEmails { get; set; } = new();
    [JsonPropertyName("clients")]       public int Clients { get; set; }
    [JsonPropertyName("cases")]         public int Cases { get; set; }
    [JsonPropertyName("invoices")]      public int Invoices { get; set; }
}
