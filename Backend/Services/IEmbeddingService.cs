using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za EmbeddingService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IEmbeddingService
{
/// <summary>
    /// Generate embedding vector for a single text input.
    /// Returns float array of dimension 1536.
    /// </summary>
    Task<float[]> EmbedAsync(string text) ;

/// <summary>
    /// Generate embeddings for multiple texts in a single API call.
    /// OpenAI embedding API supports batch input natively.
    /// Returns list of float arrays, one per input text.
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts) ;
}