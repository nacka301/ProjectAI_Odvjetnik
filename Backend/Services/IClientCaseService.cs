using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ClientCaseService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IClientCaseService
{
    Task<ClientReadRecord?> CreateClientAsync(ClientRecord record) ;

    Task<List<ClientReadRecord>> GetClientsByUserAsync(string userId) ;

    Task<ClientReadRecord?> GetClientByIdAsync(string id) ;

    Task<ClientReadRecord?> UpdateClientAsync(string id, object patch) ;

    Task<bool> DeleteClientAsync(string id) ;

/// <summary>
    /// Finds a client by OIB for a specific user.
    /// Returns null if not found or OIB is empty/invalid.
    /// </summary>
    Task<ClientReadRecord?> FindClientByOibAsync(string userId, string oib) ;

/// <summary>
    /// Finds a client by exact name match for a specific user.
    /// Fallback when OIB is not available.
    /// </summary>
    Task<ClientReadRecord?> FindClientByNameAsync(string userId, string name) ;

/// <summary>
    /// Ensures a client exists for the given user. If a matching client is found
    /// by OIB (preferred) or exact name, returns it. Otherwise creates a new one.
    /// Returns (clientId, wasCreated).
    /// </summary>
    Task<(string? ClientId, bool WasCreated)> UpsertClientForTroskovnikAsync(
        string userId,
        string? naziv,
        string? oib,
        string? adresa,
        string? grad) ;

    Task<CaseReadRecord?> CreateCaseAsync(CaseRecord record) ;

/// <summary>Active (non-deleted) cases for a user.</summary>
    Task<List<CaseReadRecord>> GetCasesByUserAsync(string userId) ;

/// <summary>Active (non-deleted) cases linked to a client.</summary>
    Task<List<CaseReadRecord>> GetCasesByClientAsync(string clientId) ;

/// <summary>Soft-deleted cases for the trash view (owner only).</summary>
    Task<List<CaseReadRecord>> GetDeletedCasesByUserAsync(string userId) ;

    Task<CaseReadRecord?> GetCaseByIdAsync(string id) ;

    Task<CaseReadRecord?> UpdateCaseAsync(string id, object patch) ;

/// <summary>Soft-delete: sets is_deleted = true instead of removing the row.</summary>
    Task<CaseReadRecord?> SoftDeleteCaseAsync(string id) ;

/// <summary>Restore a soft-deleted case.</summary>
    Task<CaseReadRecord?> RestoreCaseAsync(string id) ;

/// <summary>Hard-delete — kept for admin/cleanup scenarios.</summary>
    Task<bool> DeleteCaseAsync(string id) ;

    Task<TroskovnikReadRecord?> SaveTroskovnikAsync(TroskovnikRecord record) ;

    Task<List<TroskovnikReadRecord>> GetTroskovniciByUserAsync(string userId) ;

    Task<List<TroskovnikReadRecord>> GetTroskovniciByClientAsync(string clientId) ;

    Task<TroskovnikReadRecord?> GetTroskovnikByIdAsync(string id) ;

    Task<bool> DeleteTroskovnikAsync(string id) ;

    Task<ClientProfileResponse?> GetClientProfileAsync(string clientId) ;

    Task<CaseEventReadRecord?> CreateCaseEventAsync(CaseEventRecord record) ;

    Task<List<CaseEventReadRecord>> GetCaseEventsByCaseAsync(string caseId, string userId) ;

    Task<CaseEventReadRecord?> GetCaseEventByIdAsync(string id) ;

    Task<CaseEventReadRecord?> UpdateCaseEventAsync(string id, object patch) ;

    Task<bool> DeleteCaseEventAsync(string id) ;

/// <summary>
    /// Get all billable (pending) events for a case, ready for Troškovnik generation.
    /// </summary>
    Task<List<CaseEventReadRecord>> GetBillableEventsForCaseAsync(string caseId, string userId) ;

/// <summary>
    /// Mark events as billed (after Troškovnik is generated from them).
    /// Uses a single PostgREST PATCH with id=in.(...) filter instead of N sequential calls.
    /// </summary>
    Task<int> MarkEventsAsBilledAsync(IEnumerable<string> eventIds) ;

/// <summary>
    /// Batch-fetch multiple case events by their IDs in a single PostgREST query.
    /// Replaces N sequential GetCaseEventByIdAsync calls.
    /// </summary>
    Task<List<CaseEventReadRecord>> GetCaseEventsByIdsAsync(IEnumerable<string> eventIds) ;
}