using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za LegalIntelligenceService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ILegalIntelligenceService
{
/// <summary>
    /// Generate the Legal Intelligence Feed for the dashboard.
    /// Returns recent relevant court decisions for real estate lawyers.
    /// </summary>
    Task<List<LegalFeedItem>> GetLegalFeedAsync(int count = 5) ;

/// <summary>
    /// The killer feature: Analyze a complete case and predict court outcome.
    /// Combines contract analysis, chronology, ZK data, and court practice.
    /// </summary>
    Task<LegalStrategyResult> AnalyzeLegalStrategyAsync(LegalStrategyRequest request) ;

/// <summary>
    /// Find relevant court precedents for a given legal context.
    /// Used by the Precedent Sidebar component.
    /// </summary>
    Task<(string LegalIssue, List<CourtPrecedent> Precedents)> GetRelevantPrecedentsAsync(string caseContext) ;
}