using System.Text.Json;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ValuationAgentService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IValuationAgentService
{
/// <summary>
    /// Analyze a property using AI reasoning to find contextual adjustments
    /// that keyword-based systems miss.
    /// </summary>
    Task<List<AgentAdjustment>> AnalyzePropertyAsync(
        string propertyType,
        double sizeM2,
        string location,
        string description,
        string additionalNotes,
        double benchmarkPricePerM2,
        List<ValuationAdjustment> existingAdjustments,
        CancellationToken ct = default) ;
}