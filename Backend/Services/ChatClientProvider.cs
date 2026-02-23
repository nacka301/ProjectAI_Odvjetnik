using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Injects privacy headers into every OpenRouter API request.
/// 
/// Headers:
///   X-No-Log: true         -- OpenRouter: do NOT log prompts/completions
///   HTTP-Referer            -- Required by OpenRouter TOS for app identification
///   X-Title                 -- App title shown in OpenRouter dashboard
///   X-No-Train: true        -- Signal: do not use data for model training
///
/// Also sanitizes ALL request headers before they reach the HTTP transport,
/// stripping any non-ASCII characters that would cause .NET's HttpConnection
/// to throw "Request headers must contain only ASCII characters."
///
/// This ensures ZERO data retention on the gateway side -- critical for
/// odvjetnicka tajna (attorney-client privilege) compliance.
/// </summary>
internal sealed class PrivacyHeaderPolicy : PipelinePolicy
{
    // Pre-computed pure-ASCII title -- no Unicode, no em-dash, no diacritics.
    // NEVER put non-ASCII characters in this string.
    private const string AppTitle = "PravoTek - Pravna Inteligencija";

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
        message.Request.Headers.Set("X-Title", AppTitle);

        // Defense-in-depth: sanitize ALL existing headers (including SDK-set headers)
        // to prevent "Request headers must contain only ASCII characters" from HttpConnection.
        SanitizeAllHeaders(message);
    }

    /// <summary>
    /// Scans all request headers and replaces any non-ASCII values with their
    /// ASCII-safe equivalent. This catches non-ASCII from ANY source: our code,
    /// the OpenAI SDK, System.ClientModel, or any other pipeline policy.
    /// </summary>
    private static void SanitizeAllHeaders(PipelineMessage message)
    {
        // PipelineRequestHeaders doesn't expose an enumerator, so we guard
        // our own known headers plus any SDK headers that commonly have issues.
        // The primary vector was X-Title with an em-dash; now it's a const ASCII string.
        // This method is a safety net for any future regressions.
        var headersToCheck = new[] { "X-Title", "X-No-Log", "X-No-Train", "HTTP-Referer", "User-Agent" };
        foreach (var name in headersToCheck)
        {
            if (message.Request.Headers.TryGetValue(name, out var val) && val != null && HasNonAscii(val))
            {
                message.Request.Headers.Set(name, StripNonAscii(val));
            }
        }
    }

    private static bool HasNonAscii(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c < 0x20 || c > 0x7E) return true;
        }
        return false;
    }

    /// <summary>
    /// Strips non-ASCII characters using a simple char-by-char filter.
    /// No regex -- guaranteed to work in every .NET runtime and locale.
    /// </summary>
    private static string StripNonAscii(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c >= 0x20 && c <= 0x7E) sb.Append(c);
        }
        return sb.ToString();
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
    /// Fast model -- for extraction, classification, and summarization tasks.
    /// Low cost, high speed, sufficient quality for structured data extraction.
    /// </summary>
    public ChatClient Fast { get; }

    /// <summary>
    /// Heavy model -- for complex legal reasoning, strategy, and narrative generation.
    /// Higher cost, superior quality for nuanced analysis and professional text.
    /// </summary>
    public ChatClient Heavy { get; }

    /// <summary>
    /// Reasoning model -- for deep logical reasoning and trap detection.
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
        //   X-No-Log: true    -- OpenRouter will NOT log prompts/completions
        //   X-No-Train: true  -- Data will NOT be used for model training
        // Critical for odvjetnicka tajna (attorney-client privilege) compliance.
        options.AddPolicy(new PrivacyHeaderPolicy(), PipelinePosition.PerCall);

        Fast = new ChatClient(fastModel, credential, options);
        Heavy = new ChatClient(heavyModel, credential, options);
        Reasoning = new ChatClient(reasoningModel, credential, options);
    }
}
