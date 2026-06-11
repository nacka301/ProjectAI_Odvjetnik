using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V69: Law RAG Service — Semantic retrieval of Croatian law articles.
///
/// Two responsibilities:
///   1. SEED: Load law JSON files → generate embeddings → upsert into Supabase law_chunks table
///   2. RETRIEVE: Take contract text → embed → vector similarity search → return relevant articles
///
/// The retrieved articles are injected into the AI prompt so the model
/// cites real Croatian law text instead of relying on training data.
/// </summary>
public class LawRAGService
:ILawRAGService {
    private readonly IEmbeddingService _embedding;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LawRAGService> _logger;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceKey;
    private readonly bool _isConfigured;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions CamelOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public LawRAGService(
        EmbeddingService embedding,
        HttpClient httpClient,
        IConfiguration config,
        ILogger<LawRAGService> logger)
    {
        _embedding = embedding;
        _httpClient = httpClient;
        _logger = logger;

        _supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? config["Supabase:Url"];
        _serviceKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
            ?? config["Supabase:ServiceKey"];

        _isConfigured = !string.IsNullOrWhiteSpace(_supabaseUrl) &&
                        !string.IsNullOrWhiteSpace(_serviceKey);

        if (!_isConfigured)
        {
            _logger.LogWarning("Supabase not configured — LawRAGService will use fallback (no vector search).");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SEED: Load law JSON files → embed → store in Supabase
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Seed all law files from Backend/Data/Laws/*.json into Supabase law_chunks table.
    /// Each article is embedded and upserted (idempotent — safe to re-run).
    /// </summary>
    public async Task<RAGSeedResult> SeedAllLawsAsync()
    {
        var sw = Stopwatch.StartNew();
        var result = new RAGSeedResult();

        var lawDir = Path.Combine(AppContext.BaseDirectory, "Data", "Laws");
        if (!Directory.Exists(lawDir))
        {
            // Fallback: check relative to project root (development)
            lawDir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Laws");
        }

        if (!Directory.Exists(lawDir))
        {
            result.Errors.Add($"Law data directory not found: {lawDir}");
            _logger.LogError("Law data directory not found: {Dir}", lawDir);
            return result;
        }

        var jsonFiles = Directory.GetFiles(lawDir, "*.json");
        _logger.LogInformation("Found {Count} law files in {Dir}", jsonFiles.Length, lawDir);

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var lawFile = JsonSerializer.Deserialize<LawFile>(json, CamelOpts);

                if (lawFile?.Articles == null || lawFile.Articles.Count == 0)
                {
                    _logger.LogWarning("No articles found in {File}", file);
                    continue;
                }

                _logger.LogInformation("Processing {Law}: {Count} articles from {File}",
                    lawFile.Law, lawFile.Articles.Count, Path.GetFileName(file));

                // Convert to LawChunks
                var chunks = lawFile.Articles.Select(a => new LawChunk
                {
                    Law = lawFile.Law,
                    FullName = lawFile.FullName,
                    NnReference = lawFile.NnReference,
                    Article = a.Article,
                    Title = a.Title,
                    Content = a.Content,
                    TopicTags = a.Tags
                }).ToList();

                result.TotalArticles += chunks.Count;

                // Embed in batches of 20
                const int batchSize = 20;
                for (int i = 0; i < chunks.Count; i += batchSize)
                {
                    var batch = chunks.Skip(i).Take(batchSize).ToList();
                    var texts = batch.Select(c =>
                        $"{c.Law} čl. {c.Article} — {c.Title}: {c.Content}").ToList();

                    try
                    {
                        var embeddings = await _embedding.EmbedBatchAsync(texts);
                        result.Embedded += embeddings.Count;

                        // Upsert each chunk with embedding into Supabase
                        for (int j = 0; j < batch.Count && j < embeddings.Count; j++)
                        {
                            var stored = await UpsertLawChunkAsync(batch[j], embeddings[j]);
                            if (stored)
                                result.Stored++;
                            else
                                result.Failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Embedding batch failed for {Law} articles {Start}-{End}",
                            chunks[0].Law, i, i + batch.Count);
                        result.Failed += batch.Count;
                        result.Errors.Add($"{chunks[0].Law} batch {i}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process law file: {File}", file);
                result.Errors.Add($"File {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        sw.Stop();
        result.ElapsedSeconds = sw.Elapsed.TotalSeconds;

        _logger.LogInformation(
            "RAG Seeding complete: {Total} articles, {Embedded} embedded, {Stored} stored, {Failed} failed in {Elapsed:F1}s",
            result.TotalArticles, result.Embedded, result.Stored, result.Failed, result.ElapsedSeconds);

        return result;
    }

    /// <summary>
    /// Upsert a single law chunk with its embedding vector into Supabase.
    /// Uses ON CONFLICT (law, article) DO UPDATE for idempotency.
    /// </summary>
    private async Task<bool> UpsertLawChunkAsync(LawChunk chunk, float[] embedding)
    {
        if (!_isConfigured) return false;

        try
        {
            // Use Supabase RPC to upsert with embedding vector
            // PostgREST doesn't natively handle vector types well,
            // so we use a raw SQL call via RPC
            var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/rpc/upsert_law_chunk";

            var payload = new
            {
                p_law = chunk.Law,
                p_full_name = chunk.FullName,
                p_nn_reference = chunk.NnReference,
                p_article = chunk.Article,
                p_title = chunk.Title,
                p_content = chunk.Content,
                p_topic_tags = chunk.TopicTags,
                p_embedding = embedding
            };

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Upsert failed for {Law} čl.{Art} ({Status}): {Body}",
                    chunk.Law, chunk.Article, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting {Law} čl.{Art}", chunk.Law, chunk.Article);
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  RETRIEVE: Vector search for relevant law articles
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Retrieve the most relevant law articles for a given contract text.
    /// 1. Embed the contract text (or a summary of it)
    /// 2. Call Supabase match_law_chunks RPC for vector similarity search
    /// 3. Format results for prompt injection
    /// </summary>
    /// <param name="contractText">The contract text to find relevant laws for</param>
    /// <param name="topK">Number of top results to return (default 10)</param>
    /// <param name="threshold">Minimum similarity threshold (default 0.45)</param>
    public async Task<RAGContext> RetrieveRelevantArticlesAsync(
        string contractText,
        int topK = 10,
        float threshold = 0.45f)
    {
        var sw = Stopwatch.StartNew();
        var context = new RAGContext();

        // If Supabase is not configured, return empty context (graceful degradation)
        if (!_isConfigured)
        {
            _logger.LogInformation("RAG: Supabase not configured — skipping vector search.");
            context.UsedVectorSearch = false;
            sw.Stop();
            context.RetrievalTimeMs = sw.Elapsed.TotalMilliseconds;
            return context;
        }

        try
        {
            // Extract key legal concepts from contract for embedding
            // Use first ~2000 chars + key legal terms for better semantic matching
            var queryText = BuildQueryText(contractText);

            _logger.LogInformation("RAG: Embedding query text ({Len} chars) for vector search...", queryText.Length);

            // Generate embedding for the query
            var queryEmbedding = await _embedding.EmbedAsync(queryText);

            // Call Supabase RPC function match_law_chunks
            var matches = await VectorSearchAsync(queryEmbedding, threshold, topK);

            context.Articles = matches;
            context.UsedVectorSearch = true;

            // Format the matched articles for prompt injection
            context.FormattedPromptSection = FormatForPrompt(matches);
            context.TotalTokensEstimate = context.FormattedPromptSection.Length / 4; // rough estimate

            sw.Stop();
            context.RetrievalTimeMs = sw.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "RAG: Retrieved {Count} relevant articles in {Ms:F0}ms (≈{Tokens} tokens)",
                matches.Count, context.RetrievalTimeMs, context.TotalTokensEstimate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed — continuing without vector search.");
            context.UsedVectorSearch = false;
            sw.Stop();
            context.RetrievalTimeMs = sw.Elapsed.TotalMilliseconds;
        }

        return context;
    }

    /// <summary>
    /// Build a focused query text from the contract for embedding.
    /// Extracts the most semantically relevant portions.
    /// </summary>
    private static string BuildQueryText(string contractText)
    {
        // Take first 2000 chars (usually contains parties, property, key terms)
        var text = contractText.Length > 2000 ? contractText[..2000] : contractText;

        // Append a few key legal search terms to bias toward relevant law articles
        text += "\n\nKljučni pojmovi: kupoprodaja nekretnina, cijena, kapara, clausula intabulandi, " +
                "zemljišna knjiga, hipoteka, služnost, posjed, predaja, tereti, vlasništvo, " +
                "bračna stečevina, pisani oblik, ništetnost, zastara, raskid ugovora, " +
                "naknadna nemogućnost, naknada štete, ugovorna kazna";

        return text;
    }

    /// <summary>
    /// Execute vector similarity search via Supabase RPC.
    /// Calls the match_law_chunks function defined in schema.sql.
    /// </summary>
    private async Task<List<LawChunkMatch>> VectorSearchAsync(
        float[] queryEmbedding,
        float threshold,
        int topK)
    {
        var url = $"{_supabaseUrl!.TrimEnd('/')}/rest/v1/rpc/match_law_chunks";

        var payload = new
        {
            query_embedding = $"[{string.Join(",", queryEmbedding)}]",
            match_threshold = threshold,
            match_count = topK
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("apikey", _serviceKey);
        request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Vector search failed ({Status}): {Body}", response.StatusCode, responseBody);
            return [];
        }

        // Parse the RPC response — array of matched law chunks
        using var doc = JsonDocument.Parse(responseBody);
        var matches = new List<LawChunkMatch>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            matches.Add(new LawChunkMatch
            {
                Law = item.GetProperty("law").GetString() ?? "",
                FullName = item.GetProperty("full_name").GetString() ?? "",
                NnReference = item.GetProperty("nn_reference").GetString() ?? "",
                Article = item.GetProperty("article").GetString() ?? "",
                Title = item.GetProperty("title").GetString() ?? "",
                Content = item.GetProperty("content").GetString() ?? "",
                TopicTags = item.TryGetProperty("topic_tags", out var tags) && tags.ValueKind == JsonValueKind.Array
                    ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToArray()
                    : [],
                Similarity = item.TryGetProperty("similarity", out var sim) ? sim.GetSingle() : 0f
            });
        }

        return matches;
    }

    /// <summary>
    /// Format matched law articles into a structured prompt section.
    /// This text is injected into the AI system prompt before analysis.
    /// </summary>
    private static string FormatForPrompt(List<LawChunkMatch> articles)
    {
        if (articles.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("RELEVANTNI ČLANCI HRVATSKOG PRAVA (RAG — stvarni tekst zakona)");
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("NAPOMENA: Dolje su STVARNI tekstovi članaka hrvatskih zakona.");
        sb.AppendLine("OBAVEZNO citiraj iz ovih članaka kada analiziraš ugovor.");
        sb.AppendLine("NE izmišljaj članke koji nisu navedeni ovdje.");
        sb.AppendLine();

        foreach (var article in articles)
        {
            sb.AppendLine($"── {article.Law} čl. {article.Article} — {article.Title} ──");
            sb.AppendLine($"   Zakon: {article.FullName} ({article.NnReference})");
            sb.AppendLine($"   Tekst: {article.Content}");
            sb.AppendLine($"   Sličnost: {article.Similarity:P0}");
            sb.AppendLine();
        }

        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("Koristi ISKLJUČIVO gore navedene članke za pravne reference u analizi.");
        sb.AppendLine("Ako nisi siguran je li članak primjenjiv, NEMOJ ga citirati.");
        sb.AppendLine("════════════════════════════════════════════════");

        return sb.ToString();
    }
}
