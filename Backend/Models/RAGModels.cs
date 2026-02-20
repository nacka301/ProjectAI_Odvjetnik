namespace Backend.Models;

/// <summary>
/// V69: Semantic RAG — data models for law chunks and vector search.
/// </summary>

/// <summary>
/// A single law article chunk — stored in Supabase law_chunks table.
/// </summary>
public class LawChunk
{
    public string Law { get; set; } = "";
    public string FullName { get; set; } = "";
    public string NnReference { get; set; } = "";
    public string Article { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string[] TopicTags { get; set; } = [];
}

/// <summary>
/// Law chunk with vector search similarity score.
/// </summary>
public class LawChunkMatch
{
    public string Law { get; set; } = "";
    public string FullName { get; set; } = "";
    public string NnReference { get; set; } = "";
    public string Article { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string[] TopicTags { get; set; } = [];
    public float Similarity { get; set; }
}

/// <summary>
/// Law file JSON structure — matches the Data/Laws/*.json files.
/// </summary>
public class LawFile
{
    public string Law { get; set; } = "";
    public string FullName { get; set; } = "";
    public string NnReference { get; set; } = "";
    public List<LawArticleData> Articles { get; set; } = [];
}

/// <summary>
/// Individual article within a law file.
/// </summary>
public class LawArticleData
{
    public string Article { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string[] Tags { get; set; } = [];
}

/// <summary>
/// Result of a RAG seeding operation.
/// </summary>
public class RAGSeedResult
{
    public int TotalArticles { get; set; }
    public int Embedded { get; set; }
    public int Stored { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
    public double ElapsedSeconds { get; set; }
}

/// <summary>
/// Result of a RAG retrieval — articles to inject into prompt.
/// </summary>
public class RAGContext
{
    public List<LawChunkMatch> Articles { get; set; } = [];
    public string FormattedPromptSection { get; set; } = "";
    public int TotalTokensEstimate { get; set; }
    public bool UsedVectorSearch { get; set; }
    public double RetrievalTimeMs { get; set; }
}
