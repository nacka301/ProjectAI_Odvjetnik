using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// Core pipeline interface for V6 valuation.
/// Implementations orchestrate the 8-phase pipeline.
/// </summary>
public interface IValuationPipeline
{
    /// <summary>
    /// Execute the full V6 valuation pipeline.
    /// </summary>
    /// <param name="pdfText">Raw text extracted from the land registry PDF.</param>
    /// <param name="extractedProperty">Property info extracted by AI (Phase 0 input).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete valuation report with explainability.</returns>
    Task<V6ValuationReport> ExecuteAsync(
        string pdfText,
        Backend.Models.ExtractedPropertyInfo extractedProperty,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Similarity engine interface — calculates multi-dimensional
/// similarity between a subject property and a candidate.
/// </summary>
public interface ISimilarityEngine
{
    /// <summary>
    /// Calculate the full similarity vector between subject and candidate.
    /// </summary>
    SimilarityVector CalculateSimilarity(
        CanonicalPropertyModel subject,
        CanonicalPropertyModel candidate,
        V6SimilarityWeights? weights = null);

    /// <summary>
    /// Calculate pairwise similarity matrix for clustering.
    /// Returns an NxN distance matrix (distance = 1 - similarity).
    /// </summary>
    double[,] BuildPairwiseDistanceMatrix(
        List<CanonicalPropertyModel> candidates,
        V6SimilarityWeights? weights = null);
}

/// <summary>
/// Candidate generation interface — Phase 1.
/// </summary>
public interface ICandidateGenerator
{
    /// <summary>
    /// Generate ~50 raw candidates using broad search.
    /// </summary>
    Task<List<ListingData>> GenerateCandidatesAsync(
        CanonicalPropertyModel subject,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deep fetch interface — Phase 2.
/// </summary>
public interface IDeepFetcher
{
    /// <summary>
    /// Perform detailed HTTP fetch on top candidates.
    /// </summary>
    Task<List<ListingData>> DeepFetchAsync(
        List<ListingData> candidates,
        int maxCandidates = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// NLP extraction interface — Phase 3.
/// </summary>
public interface INlpExtractor
{
    /// <summary>
    /// Convert raw listing data to structured canonical models using NLP/LLM.
    /// Also generates text embeddings for semantic comparison.
    /// </summary>
    Task<List<CanonicalPropertyModel>> ExtractAndEmbedAsync(
        List<ListingData> listings,
        CancellationToken cancellationToken = default);
}
