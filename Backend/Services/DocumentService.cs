using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

public class DocumentService
{
    private readonly ChatClient _chatClient;
    private readonly PdfService _pdfService;
    private readonly ILogger<DocumentService> _logger;

    private const string SystemPrompt =
        """
        You are a cynically cautious Croatian Real Estate Lawyer. Analyze the provided Land Registry Extract (Zemljišnoknjižni izvadak).
        Return a JSON object with:
        - "property_id": string (Katastarska općina and čestica).
        - "owners": list of strings.
        - "active_mortgages": list of objects { "amount": string, "currency": string, "year": string, "details": string }. Ignore 'uknjižba brisana'.
        - "active_disputes": list of strings (Look for 'Zabilježba spora', 'Ovrha').
        - "risk_score": "LOW", "MEDIUM", or "HIGH".
        - "lawyer_comment": A short professional summary in Croatian explaining the risks.
        Return ONLY the raw JSON object, no markdown fences, no extra text.
        """;

    public DocumentService(ChatClient chatClient, PdfService pdfService, ILogger<DocumentService> logger)
    {
        _chatClient = chatClient;
        _pdfService = pdfService;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeDocumentAsync(IFormFile file)
    {
        var pdfText = _pdfService.ExtractText(file);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage($"Analyze this land registry extract:\n\n{pdfText}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("OpenAI analysis completed successfully.");

        var result = JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result ?? throw new InvalidOperationException("Failed to deserialize OpenAI response.");
    }
}
