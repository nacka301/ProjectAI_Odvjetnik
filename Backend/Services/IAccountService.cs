using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Brisanje računa i izvoz podataka — GDPR čl. 17 (pravo na brisanje) i
/// čl. 20 (pravo na prenosivost).
///
/// Zašto backend, a ne klijent: brisanje mora ukloniti red iz `auth.users`, a
/// to može samo service_role kroz Admin API. Panel u Postavkama je to dosad
/// pokušavao iz browsera, red po red, i nikad nije obrisao ni profil ni sam
/// račun — korisnik je vidio potvrdu, a račun je ostajao.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Svi podaci ureda kojem korisnik pripada, u obliku pogodnom za prijenos.
    /// Opseg je ured, a ne osoba: predmeti pripadaju uredu, pa je izvoz koji bi
    /// vratio samo zapise koje je korisnik osobno unio nepotpun i time
    /// beskoristan za prenosivost.
    /// </summary>
    Task<Dictionary<string, object>?> ExportFirmDataAsync(string userId);

    /// <summary>
    /// Briše korisnika. Zapisi ureda ostaju, samo im se uklanja autorstvo
    /// (user_id → NULL, migracija 020).
    ///
    /// Odbija kad bi brisanje ostavilo ured u neispravnom stanju:
    ///   • jedini je član      → LAST_MEMBER, treba obrisati ured
    ///   • vlasnik s kolegama  → OWNER_MUST_TRANSFER, treba prenijeti vlasništvo
    /// </summary>
    Task<(DeleteAccountResult? Result, DeleteBlockedResponse? Blocked)>
        DeleteOwnAccountAsync(string userId);

    /// <summary>
    /// Briše ured i sve njegove podatke, uključujući račune svih članova.
    /// Nepovratno. Smije samo vlasnik, uz točan naziv ureda kao potvrdu.
    /// </summary>
    Task<(DeleteAccountResult? Result, DeleteBlockedResponse? Blocked)>
        DeleteFirmAsync(string userId, string firmNameConfirmation);

    // ── Admin-ciljano brisanje (gumb na admin dashboardu) ──

    /// <summary>
    /// Pretpregled za admin-brisanje: naziv ureda, članovi (s e-mailovima) te
    /// broj stranaka/predmeta/računa koji bi nestali. Ne mijenja ništa.
    /// Vraća <c>null</c> ako korisnik/profil ne postoji.
    /// </summary>
    Task<AdminDeletionPreview?> GetDeletionPreviewAsync(string targetUserId);

    /// <summary>
    /// Admin briše ciljanog korisnika. Za razliku od samouslužnih metoda, cilj
    /// dolazi kao <paramref name="targetUserId"/> (identitet pozivatelja i dalje
    /// iz provjerenog admin tokena, izvan ove metode). Uz brisanje čisti i
    /// <c>firm_invites</c>/<c>waitlist</c> za oslobođene e-mailove te invalidira
    /// cacheve, da se isti mail može registrirati kao potpuno nov korisnik.
    ///
    ///   <see cref="AdminDeleteMode.Firm"/>   → potvrda je naziv ureda
    ///   <see cref="AdminDeleteMode.Member"/> → potvrda je e-mail korisnika
    /// </summary>
    Task<(DeleteAccountResult? Result, DeleteBlockedResponse? Blocked)>
        AdminDeleteUserAsync(string targetUserId, AdminDeleteMode mode, string confirmation);
}
