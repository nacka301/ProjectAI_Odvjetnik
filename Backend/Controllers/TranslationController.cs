using Backend.Helpers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using static Backend.Helpers.SseHelpers;

namespace Backend.Controllers;

/// <summary>
/// Sudski tumač — Legal Translation &amp; Transliteration endpoint.
/// Paste text → choose mode → get legally precise translation via SSE streaming.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly ITranslationService _translationService;
    private readonly SupabaseService _supabaseService;
    private readonly ChatClientProvider _clients;
    private readonly CreditService _creditService;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(
        TranslationService translationService,
        SupabaseService supabaseService,
        ChatClientProvider clients,
        CreditService creditService,
        ILogger<TranslationController> logger)
    {
        _translationService = translationService;
        _supabaseService = supabaseService;
        _clients = clients;
        _creditService = creditService;
        _logger = logger;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    private Task SendProgress(string stage, string status, string label, string? detail = null)
        => SseHelpers.SendProgress(Response, stage, status, label, detail);

    /// <summary>
    /// POST /api/translation/stream — Translate or transliterate text via SSE.
    /// Returns SSE events with progress updates, then the final result.
    /// </summary>
    [HttpPost("stream")]
    [EnableRateLimiting("general")]
    public async Task TranslateStream([FromBody] TranslationRequest request)
    {
        // Credit enforcement — check before SSE headers
        var creditCheck = await _creditService.ConsumeCreditAsync(GetUserId() ?? "", "translation");
        if (!creditCheck.Allowed)
        {
            Response.StatusCode = 403;
            Response.ContentType = "application/json";
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = creditCheck.Message }));
            return;
        }

        SseHelpers.SetupSSEHeaders(Response);

        // ── Validation ──
        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            await SseHelpers.SendError(Response, "translating", "Molimo unesite tekst za prijevod.");
            return;
        }

        if (request.Text.Length < 10)
        {
            await SseHelpers.SendError(Response, "translating", "Tekst je prekratak (min. 10 znakova).");
            return;
        }

        if (request.Text.Length > 100_000)
        {
            await SseHelpers.SendError(Response, "translating", "Tekst je predugačak (max 100.000 znakova).");
            return;
        }

        if (!Enum.IsDefined(typeof(TranslationMode), request.Mode))
        {
            await SseHelpers.SendError(Response, "translating", "Nepoznati način prijevoda.");
            return;
        }

        _logger.LogInformation("Translation stream: mode={Mode}, textLength={Len}",
            request.Mode, request.Text.Length);

        try
        {
            var result = await WithKeepAlive(Response, () =>
                _translationService.TranslateAsync(request, async (stage, status, label, detail) =>
                {
                    await SendProgress(stage, status, label, detail);
                }, Response.HttpContext.RequestAborted));

            // ── Save audit log ──
            try
            {
                await _supabaseService.SaveAuditLogAsync(new AuditLogRecord
                {
                    UserId = GetUserId(),
                    Module = "translation",
                    Step = 1,
                    Action = $"Prijevod: {result.Mode}, {result.SourceCharCount}→{result.TranslatedCharCount} znakova, {result.ProcessingTimeMs}ms",
                    LegalBasis = "Pravilnik o stalnim sudskim tumačima (NN 88/08)",
                    Citation = "čl. 8.–12.",
                    Reasoning = $"Izvor: {result.DetectedLanguage}, Cilj: {result.TargetLanguage}",
                    Confidence = "VISOKO",
                    ModelUsed = _clients.HeavyModel,
                    ModelTemperature = 0.00
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase audit log save failed for translation");
            }

            // ── Send final result ──
            await SseHelpers.SendSSE(Response, "result", result);
        }
        catch (Exception ex)
        {
            await SseHelpers.SendSafeError(Response, "translating",
                "Prijevod nije uspio. Pokušajte ponovno.", ex, _logger);
        }
    }
}
