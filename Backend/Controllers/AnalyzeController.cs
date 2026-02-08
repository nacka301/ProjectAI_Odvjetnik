using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(DocumentService documentService, ILogger<AnalyzeController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<IActionResult> Analyze(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only PDF files are accepted." });
        }

        _logger.LogInformation("Received PDF for analysis: {FileName} ({Size} bytes)",
            file.FileName, file.Length);

        try
        {
            var result = await _documentService.AnalyzeDocumentAsync(file);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document.");
            return StatusCode(500, new { error = "An error occurred while analyzing the document." });
        }
    }
}
