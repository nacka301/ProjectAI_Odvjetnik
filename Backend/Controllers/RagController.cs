using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// V69: RAG Admin Controller — seed and manage Croatian law embeddings.
///
/// Endpoints:
///   POST /api/rag/seed  — Embed all law articles and store in Supabase (idempotent)
///   GET  /api/rag/test   — Test RAG retrieval with sample query
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly ILawRAGService _ragService;
    private readonly ILogger<RagController> _logger;

    public RagController(ILawRAGService ragService, ILogger<RagController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Seed all law articles from Data/Laws/*.json into Supabase with vector embeddings.
    /// Safe to call multiple times — uses upsert (idempotent).
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedLaws()
    {
        _logger.LogInformation("RAG seed requested — loading and embedding all law articles...");

        try
        {
            var result = await _ragService.SeedAllLawsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG seed failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test RAG retrieval with a sample contract query.
    /// Returns matched law articles with similarity scores.
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> TestRetrieval([FromQuery] string? q)
    {
        var query = q ?? "Kupoprodajni ugovor za stan, cijena, kapara, clausula intabulandi, zemljišna knjiga";

        _logger.LogInformation("RAG test retrieval: {Query}", query);

        try
        {
            var context = await _ragService.RetrieveRelevantArticlesAsync(query, topK: 5);
            return Ok(new
            {
                query,
                articlesFound = context.Articles.Count,
                usedVectorSearch = context.UsedVectorSearch,
                retrievalTimeMs = context.RetrievalTimeMs,
                tokensEstimate = context.TotalTokensEstimate,
                articles = context.Articles.Select(a => new
                {
                    a.Law,
                    a.Article,
                    a.Title,
                    content = a.Content.Length > 200 ? a.Content[..200] + "..." : a.Content,
                    similarity = $"{a.Similarity:P1}"
                }),
                formattedPromptPreview = context.FormattedPromptSection.Length > 500
                    ? context.FormattedPromptSection[..500] + "..."
                    : context.FormattedPromptSection
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG test failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
