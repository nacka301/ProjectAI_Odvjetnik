using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Injects privacy headers into every OpenRouter API request.
/// 
/// Headers:
///   X-No-Log: true         — OpenRouter: do NOT log prompts/completions
///   HTTP-Referer            — Required by OpenRouter TOS for app identification
///   X-Title                 — App title shown in OpenRouter dashboard
///   X-No-Train: true        — Signal: do not use data for model training
///
/// This ensures ZERO data retention on the gateway side — critical for
/// odvjetnička tajna (attorney-client privilege) compliance.
/// </summary>
internal sealed class PrivacyHeaderPolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AddPrivacyHeaders(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AddPrivacyHeaders(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private static void AddPrivacyHeaders(PipelineMessage message)
    {
        message.Request.Headers.Set("X-No-Log", "true");
        message.Request.Headers.Set("X-No-Train", "true");
        message.Request.Headers.Set("HTTP-Referer", "https://pravotek.hr");
        message.Request.Headers.Set("X-Title", SanitizeAscii("PravoTek — Pravna Inteligencija"));
    }

    /// <summary>
    /// Strips non-ASCII characters from a string so it is safe for HTTP headers.
    /// Croatian diacritics (č, ć, ž, š, đ) and special Unicode (em-dash, etc.)
    /// would cause "Request headers must contain only ASCII characters" errors.
    /// </summary>
    private static string SanitizeAscii(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value, @"[^\x20-\x7E]+", "");
    }
}

/// <summary>
/// V51: Multi-model ChatClient provider via OpenRouter gateway.
/// 
/// OpenRouter acts as a unified gateway to all AI providers (OpenAI, Anthropic,
/// DeepSeek, Meta, Mistral, etc.) using a single API key.
/// 
/// Routing philosophy:
///   Fast      (openai/gpt-4o-mini)          = extraction, classification, summaries — cheap & fast
///   Heavy     (deepseek/deepseek-chat-v3-0324)  = complex reasoning, legal strategy, final reports
///   Reasoning (deepseek/deepseek-chat-v3-0324)  = deep logical reasoning, trap detection, second opinion
///
/// Model format: "provider/model" (e.g., "openai/gpt-4o", "anthropic/claude-3.5-sonnet")
/// To switch providers, just change the model string in appsettings.json.
///
/// Task → Model mapping:
///   ExtractPropertyInfo (PDF)           → Fast
///   ExtractListingFeatures (oglasi)     → Fast
///   TextAnalysis (listing descriptions) → Fast
///   ChronologyService (event extraction)→ Fast
///   ZkParsingService (ZK extraction)    → Fast
///   Legal Feed (dashboard summary)      → Fast
///   ValuationAgent (contextual report)  → Heavy
///   ContractAnalysis Pass 1 (audit)     → Heavy
///   ContractAnalysis Pass 2 (traps)     → Reasoning
///   Legal Strategy (precedent ranking)  → Heavy
///   Precedent Search (RAG simulation)   → Heavy
///   Auto-Valuation (final narrative)    → Heavy
///
/// One API key works for all models. Each ChatClient is stateless and thread-safe.
/// </summary>
public class ChatClientProvider
{
    /// <summary>
    /// Fast model — for extraction, classification, and summarization tasks.
    /// Low cost, high speed, sufficient quality for structured data extraction.
    /// </summary>
    public ChatClient Fast { get; }

    /// <summary>
    /// Heavy model — for complex legal reasoning, strategy, and narrative generation.
    /// Higher cost, superior quality for nuanced analysis and professional text.
    /// </summary>
    public ChatClient Heavy { get; }

    /// <summary>
    /// Reasoning model — for deep logical reasoning and trap detection.
    /// Uses chain-of-thought for catching subtle contractual traps.
    /// </summary>
    public ChatClient Reasoning { get; }

    /// <summary>
    /// Model identifiers for logging/diagnostics.
    /// </summary>
    public string FastModel { get; }
    public string HeavyModel { get; }
    public string ReasoningModel { get; }

    /// <summary>
    /// Base URL of the AI gateway (OpenRouter by default).
    /// </summary>
    public string BaseUrl { get; }

    public ChatClientProvider(
        string apiKey,
        string baseUrl = "https://openrouter.ai/api/v1",
        string fastModel = "openai/gpt-4o-mini",
        string heavyModel = "deepseek/deepseek-chat-v3-0324",
        string reasoningModel = "deepseek/deepseek-chat-v3-0324")
    {
        BaseUrl = baseUrl;
        FastModel = fastModel;
        HeavyModel = heavyModel;
        ReasoningModel = reasoningModel;

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl),
            // DeepSeek V3 is fast but complex legal analysis can take a minute or two.
            // Give enough headroom for large contracts.
            NetworkTimeout = TimeSpan.FromMinutes(5)
        };

        // ── ZERO DATA RETENTION ──
        // Inject privacy headers into every API request:
        //   X-No-Log: true    → OpenRouter will NOT log prompts/completions
        //   X-No-Train: true  → Data will NOT be used for model training
        // Critical for odvjetnička tajna (attorney-client privilege) compliance.
        options.AddPolicy(new PrivacyHeaderPolicy(), PipelinePosition.PerCall);

        Fast = new ChatClient(fastModel, credential, options);
        Heavy = new ChatClient(heavyModel, credential, options);
        Reasoning = new ChatClient(reasoningModel, credential, options);
    }
}
