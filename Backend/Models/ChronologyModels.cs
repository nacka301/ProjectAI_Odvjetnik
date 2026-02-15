namespace Backend.Models;

/// <summary>
/// A single event extracted from a document for the case chronology.
/// </summary>
public class TimelineEvent
{
    /// <summary>ISO 8601 date for sorting (e.g., "2023-03-12")</summary>
    public string DateIso { get; set; } = "";

    /// <summary>Croatian display date (e.g., "12.03.2023.")</summary>
    public string DisplayDate { get; set; } = "";

    /// <summary>Time if detected (e.g., "14:30"), null otherwise</summary>
    public string? Time { get; set; }

    /// <summary>Short legal-style description of the event</summary>
    public string EventDescription { get; set; } = "";

    /// <summary>Source filename this event was extracted from</summary>
    public string SourceFile { get; set; } = "";

    /// <summary>Page number in the source document (0 if unknown)</summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Confidence level for the date:
    /// "High" = explicit date in text (🟢),
    /// "Medium" = derived/inferred date (🟡),
    /// "Low" = AI uncertain, needs human review (🔴)
    /// </summary>
    public string Confidence { get; set; } = "Medium";
}

/// <summary>
/// Full chronology result returned to the frontend.
/// </summary>
public class ChronologyResult
{
    public List<TimelineEvent> Events { get; set; } = [];
    public int TotalDocumentsProcessed { get; set; }
    public int TotalEventsExtracted { get; set; }
    public List<string> Errors { get; set; } = [];
}
