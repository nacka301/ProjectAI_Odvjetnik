using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za PdfService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IPdfService
{
/// <summary>
    /// Extract text from an uploaded PDF using dual-strategy best-of approach.
    /// </summary>
    string ExtractText(IFormFile file) ;
}