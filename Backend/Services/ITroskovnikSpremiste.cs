using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Ono što troškovniku treba od pohrane klijenata i predmeta — i ništa više.
///
/// Izdvojeno iz <see cref="IClientCaseService"/>, koji ima 31 članicu. Kontroler
/// troškovnika koristi četiri, pa je ovisio o 27 metoda koje nikad ne zove:
/// svaka izmjena bilo koje od njih tjerala ga je na ponovno prevođenje, a svaki
/// dvojnik u testu morao je napisati 27 praznih tijela. To je kršenje načela
/// razdvajanja sučelja (ISP).
///
/// <see cref="IClientCaseService"/> nasljeđuje ovo sučelje, pa <c>ClientCaseService</c>
/// zadovoljava oba bez ijedne izmjene, a ostali kontroleri i dalje vide cijelo sučelje.
/// </summary>
public interface ITroskovnikSpremiste
{
    /// <summary>Predmet po identifikatoru — kontroler iz njega čita sud i broj predmeta.</summary>
    Task<CaseReadRecord?> GetCaseByIdAsync(string id);

    /// <summary>
    /// Ensures a client exists for the given user. If a matching client is found
    /// by OIB (preferred) or exact name, returns it. Otherwise creates a new one.
    /// Returns (clientId, wasCreated).
    ///
    /// <paramref name="uredId"/> is required when a client has to be created:
    /// clients are firm-owned since migration 015, and the RLS check rejects an
    /// insert without it.
    /// </summary>
    Task<(string? ClientId, bool WasCreated)> UpsertClientForTroskovnikAsync(
        string userId,
        string? uredId,
        string? naziv,
        string? oib,
        string? adresa,
        string? grad,
        string? tip = null);

    /// <summary>Trajno sprema izračunati troškovnik.</summary>
    Task<TroskovnikReadRecord?> SaveTroskovnikAsync(TroskovnikRecord record);

    /// <summary>Svi spremljeni troškovnici jednog korisnika.</summary>
    Task<List<TroskovnikReadRecord>> GetTroskovniciByUserAsync(string userId);
}
