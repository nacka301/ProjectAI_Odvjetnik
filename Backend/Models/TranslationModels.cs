using System.Text.Json.Serialization;

namespace Backend.Models;

// ══════════════════════════════════════════════════════════
//  TRANSLATION MODULE — Models
//  Legal translation & Cyrillic↔Latin transliteration
// ══════════════════════════════════════════════════════════

/// <summary>Supported translation operation modes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TranslationMode
{
    /// <summary>Cyrillic → Latin (same language, script change only).</summary>
    CyrillicToLatin,

    /// <summary>Latin → Cyrillic (same language, script change only).</summary>
    LatinToCyrillic,

    /// <summary>English → Croatian legal translation.</summary>
    EnglishToCroatian,

    /// <summary>German → Croatian legal translation.</summary>
    GermanToCroatian,

    /// <summary>Croatian → English legal translation.</summary>
    CroatianToEnglish,

    /// <summary>Croatian → German legal translation.</summary>
    CroatianToGerman,

    /// <summary>Auto-detect source language → Croatian.</summary>
    AutoDetectToCroatian,

    /// <summary>Auto-detect source language → English.</summary>
    AutoDetectToEnglish
}

/// <summary>Request body for the translation endpoint.</summary>
public class TranslationRequest
{
    /// <summary>The source text to translate or transliterate.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Selected translation mode / direction.</summary>
    public TranslationMode Mode { get; set; } = TranslationMode.AutoDetectToCroatian;
}

/// <summary>Result returned to the frontend via SSE "result" event.</summary>
public class TranslationResult
{
    /// <summary>The translated/transliterated text — ready for court use.</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>Detected source language (ISO code or descriptive, e.g. "srpski (ćirilica)").</summary>
    public string? DetectedLanguage { get; set; }

    /// <summary>Target language / script that was produced.</summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>Translation mode that was used.</summary>
    public TranslationMode Mode { get; set; }

    /// <summary>Character count of the source text.</summary>
    public int SourceCharCount { get; set; }

    /// <summary>Character count of the translated text.</summary>
    public int TranslatedCharCount { get; set; }

    /// <summary>Processing time in milliseconds.</summary>
    public long ProcessingTimeMs { get; set; }
}
