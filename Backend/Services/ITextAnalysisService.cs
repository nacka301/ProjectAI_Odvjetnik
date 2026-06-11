using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TextAnalysisService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITextAnalysisService
{
/// <summary>
    /// Analyze multiple listings in a single batch call for efficiency.
    /// Returns a dictionary mapping listing index → ListingAttributes.
    /// </summary>
    Task<Dictionary<int, ListingAttributes>> AnalyzeBatchAsync(
        List<(int Index, string Description)> items,
        CancellationToken ct = default) ;
}