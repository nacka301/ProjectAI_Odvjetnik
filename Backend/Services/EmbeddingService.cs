using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// V69: Embedding service for semantic RAG.
/// Generates vector embeddings using OpenAI's text-embedding-3-small model
/// via OpenRouter gateway (same API key as chat models).
///
/// Embedding dimension: 1536 (matches pgvector column in law_chunks table).
/// Used by LawRAGService for:
///   1. Seeding: embed each law article before storing in Supabase
///   2. Querying: embed contract text for vector similarity search
/// </summary>
public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public EmbeddingService(HttpClient httpClient, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
            ?? Environment.GetEnvironmentVariable("OpenRouter__ApiKey")
            ?? config["OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("OpenRouter API key not configured for EmbeddingService.");

        _baseUrl = config["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";
        _model = config["OpenRouter:EmbeddingModel"] ?? "openai/text-embedding-3-small";
    }

    /// <summary>
    /// Generate embedding vector for a single text input.
    /// Returns float array of dimension 1536.
    /// </summary>
    public async Task<float[]> EmbedAsync(string text)
    {
        var results = await EmbedBatchAsync([text]);
        return results[0];
    }

    /// <summary>
    /// Generate embeddings for multiple texts in a single API call.
    /// OpenAI embedding API supports batch input natively.
    /// Returns list of float arrays, one per input text.
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
            return [];

        var url = $"{_baseUrl.TrimEnd('/')}/embeddings";
        var payload = new
        {
            model = _model,
            input = texts
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Add("HTTP-Referer", "https://pravotekapp.com");
        request.Headers.Add("X-Title", "PravoTek RAG");

        _logger.LogInformation("Embedding {Count} texts via {Model}...", texts.Count, _model);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Embedding API error ({Status}): {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Embedding API failed: {response.StatusCode} — {responseBody}");
        }

        sw.Stop();
        _logger.LogInformation("Embedding completed in {Ms}ms for {Count} texts.", sw.ElapsedMilliseconds, texts.Count);

        // Parse OpenAI embedding response format:
        // { "data": [ { "embedding": [0.1, 0.2, ...], "index": 0 }, ... ] }
        using var doc = JsonDocument.Parse(responseBody);
        var dataArray = doc.RootElement.GetProperty("data");

        var results = new List<float[]>(texts.Count);
        foreach (var item in dataArray.EnumerateArray().OrderBy(e => e.GetProperty("index").GetInt32()))
        {
            var embeddingArray = item.GetProperty("embedding");
            var vector = new float[embeddingArray.GetArrayLength()];
            int i = 0;
            foreach (var val in embeddingArray.EnumerateArray())
            {
                vector[i++] = val.GetSingle();
            }
            results.Add(vector);
        }

        if (results.Count != texts.Count)
        {
            _logger.LogWarning("Expected {Expected} embeddings, got {Actual}.", texts.Count, results.Count);
        }

        return results;
    }

    /// <summary>
    /// Get the embedding dimension (1536 for text-embedding-3-small).
    /// </summary>
    public int Dimension => 1536;

    /// <summary>
    /// Current embedding model identifier.
    /// </summary>
    public string Model => _model;
}
