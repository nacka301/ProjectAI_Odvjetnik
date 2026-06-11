using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za UsageService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IUsageService
{
/// <summary>
    /// Check whether the tenant may create one more resource of the given kind.
    /// Returns an <see cref="UsageCheckResult"/> — Allowed=true or a structured deny.
    /// </summary>
    Task<UsageCheckResult> CheckLimitAsync(string? tenantId, UsageResource resource) ;

/// <summary>
    /// Increment one counter in tenant_usage_stats (cases_used, invoices_used, or ai_credits_used).
    /// Call AFTER the resource has been successfully created/consumed.
    /// </summary>
    Task IncrementAsync(string? tenantId, UsageResource resource) ;

/// <summary>Bust the cache for a specific tenant (e.g. after plan upgrade).</summary>
    void InvalidateCache(string tenantId) ;
}