using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V35: Multi-model ChatClient provider — routes tasks to the optimal OpenAI model.
/// 
/// Routing philosophy:
///   Fast  (gpt-5-mini)  = extraction, classification, summaries — cheap &amp; fast
///   Heavy (gpt-5.2)     = complex reasoning, legal strategy, final reports
///
/// Task → Model mapping:
///   ExtractPropertyInfo (PDF)           → Fast
///   ExtractListingFeatures (oglasi)     → Fast
///   TextAnalysis (listing descriptions) → Fast
///   ChronologyService (event extraction)→ Fast
///   ZkParsingService (ZK extraction)    → Fast
///   Legal Feed (dashboard summary)      → Fast
///   ValuationAgent (contextual report)  → Heavy
///   ContractAnalysis (legal reasoning)  → Heavy
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
    /// Model identifiers for logging/diagnostics.
    /// </summary>
    public string FastModel { get; }
    public string HeavyModel { get; }

    public ChatClientProvider(string apiKey, string fastModel = "gpt-4o-mini", string heavyModel = "gpt-4o")
    {
        FastModel = fastModel;
        HeavyModel = heavyModel;
        Fast = new ChatClient(fastModel, apiKey);
        Heavy = new ChatClient(heavyModel, apiKey);
    }
}
