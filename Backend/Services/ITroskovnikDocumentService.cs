using System.Globalization;
using System.Text;
using System.Text.Json;
using Backend.Models;
using Microsoft.Extensions.Logging;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TroskovnikDocumentService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITroskovnikDocumentService
{
/// <summary>
    /// Render HTML for a troškovnik from a read record. If the record contains
    /// the full response JSON in `RawJsonOutput`, the method will attempt to
    /// deserialize it and include line items; otherwise a minimal document is returned.
    /// </summary>
    string RenderHtml(TroskovnikReadRecord record) ;
}