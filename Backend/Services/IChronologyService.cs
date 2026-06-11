using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ChronologyService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IChronologyService
{
/// <summary>
    /// Extract timeline events from a single document's text.
    /// </summary>
    Task<List<TimelineEvent>> ExtractEventsAsync(string filename, string documentText) ;
}