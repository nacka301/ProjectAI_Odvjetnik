using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ZkParsingService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IZkParsingService
{
/// <summary>
    /// Parse a ZK izvadak PDF into structured data.
    /// Returns complete ZkParsedResult with all 3 sheets + risk assessment.
    /// </summary>
    Task<(ZkParsedResult Parsed, string RawPdfText)> ParseZkIzvadakAsync(IFormFile file) ;

/// <summary>
    /// Parse ZK izvadak from already-extracted text.
    /// Useful when text is already available.
    /// </summary>
    Task<ZkParsedResult> ParseZkTextAsync(string pdfText) ;
}