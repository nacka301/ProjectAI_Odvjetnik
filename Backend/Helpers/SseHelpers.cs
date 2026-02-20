using System.Text.Json;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;

namespace Backend.Helpers;

/// <summary>
/// Shared SSE (Server-Sent Events) helpers used by streaming controllers.
/// </summary>
public static class SseHelpers
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task SendSSE(HttpResponse response, string type, object? payload)
    {
        if (response.HttpContext.RequestAborted.IsCancellationRequested)
            return;
        try
        {
            var json = JsonSerializer.Serialize(new { type, data = payload }, SseJsonOptions);
            await response.WriteAsync($"data: {json}\n\n", response.HttpContext.RequestAborted);
            await response.Body.FlushAsync(response.HttpContext.RequestAborted);
        }
        catch (Exception) when (response.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — expected, swallow silently
        }
    }

    public static Task SendProgress(HttpResponse response, string stage, string status, string label, string? detail = null)
        => SendSSE(response, "progress", new { stage, status, label, detail });

    public static Task SendError(HttpResponse response, string stage, string message)
        => SendSSE(response, "error", new { stage, message });

    /// <summary>
    /// Send an error SSE event with a sanitized exception — never leak internal details to client.
    /// Logs the full exception server-side.
    /// </summary>
    public static Task SendSafeError(HttpResponse response, string stage, string userMessage, Exception ex, ILogger logger)
    {
        logger.LogError(ex, "SSE error in stage {Stage}", stage);

        // Only send the user-friendly message, never ex.Message
        if (response.HttpContext.RequestAborted.IsCancellationRequested)
            return Task.CompletedTask;

        return SendSSE(response, "error", new { stage, message = userMessage });
    }

    public static void SetupSSEHeaders(HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");
        response.Headers.Append("X-Accel-Buffering", "no");
    }

    /// <summary>
    /// Send an SSE comment as a keepalive ping to prevent reverse proxy (Render, Nginx, Cloudflare)
    /// from killing idle connections. SSE spec says lines starting with ":" are comments
    /// and MUST be silently ignored by clients.
    /// </summary>
    public static async Task SendKeepAlive(HttpResponse response)
    {
        if (response.HttpContext.RequestAborted.IsCancellationRequested)
            return;
        try
        {
            await response.WriteAsync(": keepalive\n\n", response.HttpContext.RequestAborted);
            await response.Body.FlushAsync(response.HttpContext.RequestAborted);
        }
        catch (Exception) when (response.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — expected
        }
    }

    /// <summary>
    /// Run a long-running task while sending periodic SSE keepalive pings every 10 seconds
    /// to prevent Render.com (and other reverse proxies) from killing the connection.
    /// 
    /// Usage:
    ///   var result = await SseHelpers.WithKeepAlive(Response, () => _service.LongAiCallAsync(...));
    /// </summary>
    public static async Task<T> WithKeepAlive<T>(HttpResponse response, Func<Task<T>> work, int intervalSeconds = 10)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(response.HttpContext.RequestAborted);
        var workTask = work();

        // Send keepalive pings until the work completes
        while (!workTask.IsCompleted)
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token);
            var finished = await Task.WhenAny(workTask, delay);

            if (finished == workTask)
                break; // Work is done

            // Still working — send a keepalive ping
            if (!cts.Token.IsCancellationRequested)
                await SendKeepAlive(response);
        }

        return await workTask; // Propagate result or exception
    }

    /// <summary>
    /// Void overload — for tasks that don't return a value.
    /// </summary>
    public static async Task WithKeepAlive(HttpResponse response, Func<Task> work, int intervalSeconds = 10)
    {
        await WithKeepAlive<object?>(response, async () => { await work(); return null; }, intervalSeconds);
    }

    /// <summary>
    /// Patch reasoning: replace AI's wrong final value with engine's authoritative value.
    /// </summary>
    public static string PatchReasoningValue(string reasoning, double aiValue, int engineValue)
    {
        if (string.IsNullOrWhiteSpace(reasoning)) return reasoning;

        int aiInt = (int)Math.Round(aiValue);
        string[] aiFormats =
        [
            aiInt.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("hr-HR")),
            aiInt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
            aiInt.ToString(),
        ];

        string engineFormatted = engineValue.ToString("N0",
            System.Globalization.CultureInfo.GetCultureInfo("hr-HR"));

        foreach (var fmt in aiFormats)
        {
            if (reasoning.Contains(fmt))
                reasoning = reasoning.Replace(fmt, engineFormatted);
        }

        foreach (var fmt in aiFormats)
        {
            reasoning = reasoning
                .Replace($"{fmt} €", $"{engineFormatted} €")
                .Replace($"{fmt} EUR", $"{engineFormatted} EUR")
                .Replace($"{fmt} eur", $"{engineFormatted} EUR");
        }

        return reasoning;
    }

    /// <summary>
    /// Build a transparent comparison table from engine UsedComparables.
    /// V27: Show ONLY per-comp corrections (size/condition/features), NOT combined with global adjustments.
    /// Global adjustments (centar, investicija, etc.) are shown separately in the UI.
    /// This prevents the math-disconnect where the table shows one set of numbers
    /// but the final value is computed from different ones.
    /// </summary>
    public static List<ComparisonTableRow> BuildComparisonTable(
        DeterministicValuation engine, double totalAdjPct)
    {
        if (engine.UsedComparables == null || engine.UsedComparables.Count == 0)
            return [];

        var rows = new List<ComparisonTableRow>();
        int idx = 1;
        foreach (var c in engine.UsedComparables.Where(x => !x.IsOutlier).OrderByDescending(x => x.SimilarityScore))
        {
            // V27: Only per-comp correction — NOT combined with global adjustments.
            // The engine's weighted median already uses these corrected prices.
            // Global adjustments are applied AFTER the weighted median in a separate step.
            double perCompCorr = c.CorrectionPercent;
            var adjustedPpm2 = c.PricePerM2 * (1 + perCompCorr / 100.0);
            rows.Add(new ComparisonTableRow
            {
                Index = idx++,
                Title = c.Title,
                Url = c.Url,
                AreaM2 = c.AreaM2 ?? 0,
                TotalPrice = c.TotalPrice ?? 0,
                RawPricePerM2 = Math.Round(c.PricePerM2, 1),
                CorrectionPercent = Math.Round(perCompCorr, 1),
                AdjustedPricePerM2 = Math.Round(adjustedPpm2, 1),
                SimilarityScore = Math.Round(c.SimilarityScore <= 1 ? c.SimilarityScore * 100 : c.SimilarityScore, 0),
                YardAreaM2 = c.YardAreaM2,
                SimilarityReason = c.SimilarityReason,
                DeepDiveStatus = c.DeepDiveStatus,
                CorrectionReason = c.CorrectionReason,
            });
        }
        return rows;
    }

    /// <summary>Build a 3-tier price range: conservative / realistic / optimistic.</summary>
    public static PriceRangeEstimate? BuildPriceRange(
        DeterministicValuation engine, double targetSizeM2)
    {
        if (engine.CalculatedPricePerM2 <= 0 || targetSizeM2 <= 0) return null;

        var ppm2 = engine.CalculatedPricePerM2;
        var stdDev = engine.StdDevPerM2;

        if (stdDev <= 0) stdDev = ppm2 * 0.15;

        var conservative = Math.Max(ppm2 - stdDev, ppm2 * 0.7);
        var optimistic = Math.Min(ppm2 + stdDev, ppm2 * 1.3);

        return new PriceRangeEstimate
        {
            ConservativePricePerM2 = Math.Round(conservative, 1),
            RealisticPricePerM2 = Math.Round(ppm2, 1),
            OptimisticPricePerM2 = Math.Round(optimistic, 1),
            ConservativeTotal = (int)Math.Round(conservative * targetSizeM2),
            RealisticTotal = (int)Math.Round(ppm2 * targetSizeM2),
            OptimisticTotal = (int)Math.Round(optimistic * targetSizeM2),
        };
    }

    /// <summary>Build a benchmark summary string from engine + market data.</summary>
    public static string BuildBenchmarkSummary(
        DeterministicValuation engine, MarketResearchResult market)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(engine.RegionName))
            parts.Add($"Regija: {engine.RegionName}");
        if (!string.IsNullOrWhiteSpace(market.DetectedPropertyType))
            parts.Add($"Tip: {market.DetectedPropertyType}");
        if (engine.MarketAverageAsking > 0)
            parts.Add($"Prosjek traženih cijena: {engine.MarketAverageAsking:N0} €/m²");
        if (engine.CalculatedPricePerM2 > 0)
            parts.Add($"Izračunata cijena: {engine.CalculatedPricePerM2:N0} €/m²");
        if (engine.RealizationDiscountPercent != 0)
            parts.Add($"Diskont tražena→ostvarena: {engine.RealizationDiscountPercent:F1}%");
        if (engine.UsablePricePoints > 0)
            parts.Add($"Korištenih usporedivih: {engine.UsablePricePoints}");
        if (engine.CoefficientOfVariation > 0)
            parts.Add($"Koeficijent varijacije: {engine.CoefficientOfVariation:F1}%");
        return string.Join(" | ", parts);
    }
}
