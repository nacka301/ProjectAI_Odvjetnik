using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V50: Multi-model ChatClient provider — routes tasks to the optimal OpenAI model.
/// 
/// Routing philosophy:
///   Fast      (gpt-4o-mini) = extraction, classification, summaries — cheap &amp; fast
///   Heavy     (gpt-4o)      = complex reasoning, legal strategy, final reports
///   Reasoning (o1-preview)  = deep logical reasoning, trap detection, second opinion
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
///   ContractAnalysis Pass 2 (traps)     → Reasoning  ← NEW
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
    /// Uses o1-preview chain-of-thought for catching subtle contractual traps.
    /// </summary>
    public ChatClient Reasoning { get; }

    /// <summary>
    /// Model identifiers for logging/diagnostics.
    /// </summary>
    public string FastModel { get; }
    public string HeavyModel { get; }
    public string ReasoningModel { get; }

    public ChatClientProvider(
        string apiKey,
        string fastModel = "gpt-4o-mini",
        string heavyModel = "gpt-4o",
        string reasoningModel = "o1-preview")
    {
        FastModel = fastModel;
        HeavyModel = heavyModel;
        ReasoningModel = reasoningModel;
        Fast = new ChatClient(fastModel, apiKey);
        Heavy = new ChatClient(heavyModel, apiKey);
        Reasoning = new ChatClient(reasoningModel, apiKey);
    }
}
