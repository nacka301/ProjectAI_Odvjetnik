using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za LawRAGService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ILawRAGService
{
/// <summary>
    /// Seed all law files from Backend/Data/Laws/*.json into Supabase law_chunks table.
    /// Each article is embedded and upserted (idempotent — safe to re-run).
    /// </summary>
    Task<RAGSeedResult> SeedAllLawsAsync() ;

/// <summary>
    /// Retrieve the most relevant law articles for a given contract text.
    /// 1. Embed the contract text (or a summary of it)
    /// 2. Call Supabase match_law_chunks RPC for vector similarity search
    /// 3. Format results for prompt injection
    /// </summary>
    /// <param name="contractText">The contract text to find relevant laws for</param>
    /// <param name="topK">Number of top results to return (default 10)</param>
    /// <param name="threshold">Minimum similarity threshold (default 0.45)</param>
    Task<RAGContext> RetrieveRelevantArticlesAsync(
        string contractText,
        int topK = 10,
        float threshold = 0.45f) ;
}