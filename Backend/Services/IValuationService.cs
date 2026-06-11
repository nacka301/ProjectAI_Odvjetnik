using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ValuationService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IValuationService
{
/// <summary>
    /// Send ALL comparable listings + TARGET property to GPT in one batch call.
    /// Returns extracted features AND similarity scores for each listing.
    /// </summary>
    Task ExtractListingFeaturesAsync(
        List<ComparableSearchResult> listings,
        ExtractedPropertyInfo? subjectProperty = null,
        CancellationToken ct = default) ;

/// <summary>
    /// After AI feature extraction, remove listings whose AiPropertyType
    /// doesn't match the wanted type. This is more reliable than keyword matching
    /// because AI reads and understands the full listing text.
    /// </summary>
    int FilterByAiPropertyType(List<ComparableSearchResult> listings, string wantedType) ;

/// <summary>
    /// Extract property details from PDF text using AI.
    /// </summary>
    Task<ExtractedPropertyInfo> ExtractPropertyInfoAsync(string pdfText, CancellationToken ct = default) ;

/// <summary>
    /// Fully automated valuation: PDF data + market research → opinion.
    /// Uses ONLY real search data — no hardcoded benchmarks.
    /// The engine's deterministic value is injected so the LLM must use it.
    /// </summary>
    Task<AutoValuationResult> GenerateAutoValuationAsync(
        ExtractedPropertyInfo property, MarketResearchResult marketData,
        DeterministicValuation? engineResult = null,
        CancellationToken ct = default) ;
}