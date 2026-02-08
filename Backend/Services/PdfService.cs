using System.Text;
using UglyToad.PdfPig;

namespace Backend.Services;

/// <summary>
/// Extracts text from PDF files in-memory. Shared across features.
/// </summary>
public class PdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
    }

    public string ExtractText(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var document = PdfDocument.Open(stream);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        var text = sb.ToString();

        _logger.LogInformation(
            "PDF text extracted from '{FileName}'. Length: {Length} chars (content not logged for privacy).",
            file.FileName, text.Length);

        return text;
    }
}
