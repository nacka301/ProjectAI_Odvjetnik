using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za PerplexityAuditService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IPerplexityAuditService
{
    bool IsConfigured { get; }
/// <summary>
    /// Audit a completed valuation using Perplexity's web-search-augmented AI.
    /// Returns a structured audit result with verdict, dimension scores, and findings.
    /// </summary>
    Task<ValuationAuditResult> AuditValuationAsync(
        FullEvaluationResult valuation,
        string evaluationId,
        CancellationToken ct = default) ;
}
