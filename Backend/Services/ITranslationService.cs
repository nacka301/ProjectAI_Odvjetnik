using System.Diagnostics;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TranslationService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITranslationService
{
/// <summary>
    /// Translates or transliterates the given text according to the specified mode.
    /// </summary>
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        Func<string, string, string, string?, Task>? onProgress = null,
        CancellationToken ct = default) ;
}