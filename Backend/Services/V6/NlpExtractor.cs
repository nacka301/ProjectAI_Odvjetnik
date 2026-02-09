using System.Text.Json;
using Backend.Models.V6;
using OpenAI.Chat;

namespace Backend.Services.V6;

/// <summary>
/// V6 Phase 3 — Structured Extraction (NLP &amp; Embeddings).
/// 
/// For each listing:
///   1. Uses OpenAI GPT-4o to parse raw descriptions into structured JSON:
///      condition, features, sentiment, rooms, floor, amenities
///   2. Generates text embeddings (text-embedding-3-small) for semantic comparison
///   3. Converts ListingData → CanonicalPropertyModel
/// 
/// The embeddings enable Phase 4's cosine-similarity semantic dimension.
/// </summary>
public class NlpExtractor : INlpExtractor
{
    private readonly ChatClient _chatClient;
    private readonly string _openAiKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NlpExtractor> _logger;

    private const int MaxConcurrentNlp = 5;
    private const int MaxConcurrentEmbeddings = 10;

    private const string ListingParsePrompt =
        """
        You are a real-estate data extraction assistant for Croatian property listings.
        Parse the listing description into structured JSON.

        Return ONLY a raw JSON object with these fields:
        {
          "condition": "ruins|old|good|renovated|new",
          "features": ["parking", "balcony", "garden", "pool", "elevator", "loggia", "terrace", "garage", "sea_view", "first_row", "air_conditioning", "central_heating", "furnished", "basement", "attic"],
          "sentiment": "economy|standard|luxury",
          "floor": null or number,
          "total_floors": null or number,
          "rooms": null or number,
          "bathrooms": null or number,
          "year_built": null or number,
          "has_elevator": null or boolean,
          "has_parking": null or boolean,
          "has_garage": null or boolean,
          "has_balcony": null or boolean,
          "has_garden": null or boolean,
          "has_pool": null or boolean,
          "has_sea_view": null or boolean,
          "is_first_row": null or boolean
        }

        Rules:
        - Only include features explicitly mentioned in the text
        - "condition" should be inferred from keywords: "novogradnja"→new, "renoviran/adaptiran"→renovated, "dobar/solidan"→good, "stariji"→old
        - "sentiment" is based on overall tone: luxury keywords (ekskluzivan, premium, luksuz) → luxury, budget/basic → economy
        - Croatian text: "loža"=loggia, "vrt"=garden, "bazen"=pool, "dizalo/lift"=elevator, "pogled na more"=sea_view
        - Return ONLY the JSON, no markdown fences
        """;

    public NlpExtractor(
        ChatClient chatClient,
        IConfiguration config,
        HttpClient httpClient,
        ILogger<NlpExtractor> logger)
    {
        _chatClient = chatClient;
        _httpClient = httpClient;
        _logger = logger;

        _openAiKey = config["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_APIKEY")
            ?? "";
    }

    public async Task<List<CanonicalPropertyModel>> ExtractAndEmbedAsync(
        List<ListingData> listings,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CanonicalPropertyModel>();

        _logger.LogInformation("V6 Phase 3: Processing {Count} listings (NLP + embeddings)", listings.Count);

        // ── Step 1: NLP structured extraction (parallel, max 5 concurrent) ──
        var semaphore = new SemaphoreSlim(MaxConcurrentNlp);
        var nlpTasks = listings.Select(async listing =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExtractStructuredData(listing, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var canonicalModels = await Task.WhenAll(nlpTasks);
        var validModels = canonicalModels.Where(m => m != null).Cast<CanonicalPropertyModel>().ToList();

        _logger.LogInformation("V6 Phase 3: NLP extraction done — {Valid}/{Total} valid",
            validModels.Count, listings.Count);

        // ── Step 2: Generate embeddings (batch via OpenAI API) ──
        await GenerateEmbeddingsBatch(validModels, cancellationToken);

        _logger.LogInformation("V6 Phase 3: Embeddings generated for {Count} models", validModels.Count);

        return validModels;
    }

    // ══════════════════════════════════════════════════════════
    //  NLP STRUCTURED EXTRACTION (per listing)
    // ══════════════════════════════════════════════════════════

    private async Task<CanonicalPropertyModel?> ExtractStructuredData(
        ListingData listing, CancellationToken ct)
    {
        try
        {
            // Build the text to analyze (prefer deep-fetched content)
            string textToAnalyze = !string.IsNullOrWhiteSpace(listing.RawDescription)
                ? listing.RawDescription
                : $"{listing.Title} {listing.Snippet}";

            // Limit text length for API efficiency
            if (textToAnalyze.Length > 3000)
                textToAnalyze = textToAnalyze[..3000];

            StructuredListingFeatures? features = null;

            // Only call LLM if we have meaningful text
            if (textToAnalyze.Length > 30)
            {
                features = await ParseWithLlm(textToAnalyze, ct);
            }

            // Build canonical model
            var model = new CanonicalPropertyModel
            {
                PropertyType = listing.PropertyType ?? "",
                SizeM2 = listing.SizeM2 ?? 0,
                LandSizeM2 = listing.LandSizeM2 ?? 0,
                Location = listing.Location ?? "",
                Address = listing.Address ?? "",
                PriceEur = listing.PriceEur,
                PricePerM2 = listing.PricePerM2,
                SourceUrl = listing.Url,
                SourcePortal = listing.Portal,
                ListingTitle = listing.Title,
                Latitude = listing.Latitude,
                Longitude = listing.Longitude,
                RawText = textToAnalyze,
                // ── Iron Gate: carry forward filter flags ──
                PassedIronGate = listing.PassedIronGate,
                PassedJunkFilter = listing.PassedJunkFilter,
                TypeCategory = CandidateGenerator.ClassifyPropertyType(
                    listing.DetectedType ?? listing.PropertyType ?? listing.Title)
            };

            // Apply NLP features if available
            if (features != null)
            {
                model.ConditionOrdinal = MathHelpers.ParseConditionOrdinal(features.Condition);
                model.ConditionRaw = features.Condition;
                model.Sentiment = features.Sentiment;
                model.StructuredDescription = JsonSerializer.Serialize(features);

                // Build feature set for Jaccard
                foreach (var feat in features.Features)
                {
                    model.Features.Add(feat.ToLowerInvariant());
                }

                // Add boolean features to the set
                if (features.HasElevator == true) model.Features.Add("elevator");
                if (features.HasParking == true) model.Features.Add("parking");
                if (features.HasGarage == true) model.Features.Add("garage");
                if (features.HasBalcony == true) model.Features.Add("balcony");
                if (features.HasGarden == true) model.Features.Add("garden");
                if (features.HasPool == true) model.Features.Add("pool");
                if (features.HasSeaView == true) model.Features.Add("sea_view");
                if (features.IsFirstRow == true) model.Features.Add("first_row");
            }
            else
            {
                // Fallback: basic keyword extraction from text
                model.ConditionOrdinal = MathHelpers.ParseConditionOrdinal(
                    InferConditionFromText(textToAnalyze));
                model.Features = ExtractFeaturesFromKeywords(textToAnalyze);
            }

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V6 Phase 3: Failed to process listing {Url}", listing.Url);
            return null;
        }
    }

    /// <summary>
    /// Call GPT-4o to parse listing text into structured features.
    /// </summary>
    private async Task<StructuredListingFeatures?> ParseWithLlm(string text, CancellationToken ct)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(ListingParsePrompt),
                new UserChatMessage($"Parse this listing:\n\n{text}")
            };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                Temperature = 0.1f,
                MaxOutputTokenCount = 500
            };

            var response = await _chatClient.CompleteChatAsync(messages, options, ct);
            string json = response.Value.Content[0].Text;

            // Clean up potential markdown fences
            json = json.Trim();
            if (json.StartsWith("```"))
            {
                int braceIdx = json.IndexOf('{');
                if (braceIdx >= 0) json = json.Substring(braceIdx);
            }
            if (json.EndsWith("```"))
            {
                int lastBrace = json.LastIndexOf('}');
                if (lastBrace >= 0) json = json.Substring(0, lastBrace + 1);
            }

            var features = JsonSerializer.Deserialize<StructuredListingFeatures>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return features;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("LLM parse failed: {Error}", ex.Message);
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  EMBEDDINGS (batch via OpenAI API)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Generate text embeddings for all models using OpenAI text-embedding-3-small.
    /// Uses the REST API directly since the OpenAI C# SDK Chat client
    /// doesn't expose embeddings natively.
    /// </summary>
    private async Task GenerateEmbeddingsBatch(
        List<CanonicalPropertyModel> models,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_openAiKey) || models.Count == 0) return;

        // Batch in groups of 20 (API limit considerations)
        const int batchSize = 20;

        for (int i = 0; i < models.Count; i += batchSize)
        {
            var batch = models.Skip(i).Take(batchSize).ToList();

            try
            {
                var texts = batch.Select(m =>
                {
                    string text = m.RawText;
                    if (text.Length > 2000) text = text[..2000]; // embedding input limit
                    return text;
                }).ToList();

                var requestBody = new
                {
                    model = "text-embedding-3-small",
                    input = texts,
                    dimensions = 256  // compact embeddings for efficiency
                };

                var json = JsonSerializer.Serialize(requestBody);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiKey);

                var response = await _httpClient.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Embedding API returned {Status}", response.StatusCode);
                    continue;
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(responseJson);

                if (doc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        int index = item.GetProperty("index").GetInt32();
                        var embedding = item.GetProperty("embedding")
                            .EnumerateArray()
                            .Select(v => (float)v.GetDouble())
                            .ToArray();

                        if (index < batch.Count)
                        {
                            batch[index].DescriptionEmbedding = embedding;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding batch {Batch} failed", i / batchSize);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  FALLBACK KEYWORD EXTRACTION (when LLM is unavailable)
    // ══════════════════════════════════════════════════════════

    private static string InferConditionFromText(string text)
    {
        string lower = text.ToLowerInvariant();
        if (lower.Contains("novogradnja") || lower.Contains("novoizgra")) return "new";
        if (lower.Contains("renovir") || lower.Contains("adaptir") || lower.Contains("obnovljen")) return "renovated";
        if (lower.Contains("starij") || lower.Contains("zapušten")) return "old";
        if (lower.Contains("ruševin") || lower.Contains("ruin")) return "ruins";
        return "good";
    }

    private static HashSet<string> ExtractFeaturesFromKeywords(string text)
    {
        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string lower = text.ToLowerInvariant();

        var keywordMap = new Dictionary<string, string>
        {
            { "parking", "parking" }, { "garaž", "garage" }, { "garaz", "garage" },
            { "balkon", "balcony" }, { "loža", "loggia" }, { "loza", "loggia" },
            { "logia", "loggia" }, { "terasa", "terrace" }, { "vrt", "garden" },
            { "bazen", "pool" }, { "dizalo", "elevator" }, { "lift", "elevator" },
            { "klima", "air_conditioning" }, { "centralno", "central_heating" },
            { "namješten", "furnished" }, { "namjesten", "furnished" },
            { "pogled na more", "sea_view" }, { "sea view", "sea_view" },
            { "prvi red", "first_row" }, { "1. red", "first_row" },
            { "podrum", "basement" }, { "tavan", "attic" }, { "potkrovlje", "attic" },
        };

        foreach (var kvp in keywordMap)
        {
            if (lower.Contains(kvp.Key))
                features.Add(kvp.Value);
        }

        return features;
    }
}
