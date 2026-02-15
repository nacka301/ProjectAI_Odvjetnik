using System.Text.Json;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V29 "God Prompt" — AI-driven contextual property analysis.
/// Unlike keyword-based adjustments, this agent REASONS about the property:
/// - Shape analysis (30m width on 9000m² = useless for hotels)
/// - Zoning readiness (T2 without UPU = speculative)
/// - Infrastructure gaps (no road = dealbreaker)
/// - Legal context (vanknjižno + sudski spor = run away)
/// Returns adjustments that complement the deterministic engine.
/// </summary>
public class ValuationAgentService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ValuationAgentService> _logger;

    private const string SystemPrompt =
        """
        You are a Senior Real Estate Appraiser (REV/RICS certified) specializing in the Croatian market.
        You are cynical, conservative, and investment-oriented.
        Your goal is to determine the TRUE market value by identifying hidden risks and premium features
        in the text description that KEYWORD systems cannot catch.

        CONTEXT:
        You will receive:
        1. Property specs (type, size, location, zone, description)
        2. Benchmark price (the engine's calculated market average for this area)
        3. Already-applied keyword adjustments (DO NOT duplicate these)

        YOUR TASK:
        Analyze the property description to find factors that the keyword system MISSED.
        Calculate a Percentage Adjustment for each NEW factor.

        REASONING GUIDELINES:

        1. GEOMETRY & SHAPE (Crucial for Land):
           - Large plot (5000m²+) with narrow width (<40m) → USELESS for hotels/resorts.
             Apply "Shape Constraint" penalty: -20% to -40%.
           - "Kaskadni teren" (cascading) → high construction costs: -10% to -15%.
           - "Nepravilan oblik" (irregular shape) → limits usable area: -10%.
           - Long narrow strip → only row houses possible: -15%.

        2. LEGAL & ZONING:
           - "T2 zona" but NO mention of "UPU donesen" / approved urban plan
             → speculative, not buildable yet: -15% to -25%.
           - "Vanknjižno", "Udio", "Sudski spor" → extreme risk: -30% to -50%.
           - "Lokacijska dozvola" or "Građevinska dozvola" → ready to build: +10%.
           - Multiple parcels ("više čestica") → consolidation risk: -5%.

        3. MICRO-LOCATION CONTEXT:
           - "Blizina dalekovoda" (near power lines) → permanent devaluation: -15%.
           - "Buka" / noise, highway proximity → penalty: -10%.
           - "Slijep ulica" / dead end → limited access, limited appeal: -5%.
           - "Prirodna plaža u blizini" → premium: +10%.
           - "Centar mjesta", walkable to amenities → premium: +5%.

        4. INFRASTRUCTURE GAPS:
           - "Nema pristupni put" (no access road) → dealbreaker: -30% to -40%.
           - "Nema vode/struje" (no water/electricity) → significant cost: -15% to -20%.
           - "Septička jama" instead of sewage → rural penalty: -5%.
           - "Struja, voda, kanalizacija na parceli" → infrastructure ready: +5%.

        5. HIDDEN VALUE / RED FLAGS:
           - Description mentions "projekt" or "idejno rješenje" → value-added: +5%.
           - Mentions "etažiranje u tijeku" → not yet legally divided: -10%.
           - "Ostavinska" (inheritance) → legal complexity: -5%.
           - Mentions specific m² price much below market → investigate WHY.

        CRITICAL RULES:
        - Only output factors NOT already covered by the keyword adjustments provided.
        - Be specific — cite the text evidence.
        - For land: ALWAYS check shape/dimensions if mentioned.
        - If nothing unusual found, return empty adjustments array.
        - Maximum total adjustment from this analysis: ±40%.

        Return ONLY raw JSON, no markdown fences:
        {
          "adjustments": [
            {
              "factor": "Short name in Croatian",
              "impact_percent": -15.0,
              "reasoning": "Professional explanation citing text evidence"
            }
          ],
          "confidence": "High/Medium/Low"
        }
        """;

    public ValuationAgentService(ChatClient chatClient, ILogger<ValuationAgentService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a property using AI reasoning to find contextual adjustments
    /// that keyword-based systems miss.
    /// </summary>
    public async Task<List<AgentAdjustment>> AnalyzePropertyAsync(
        string propertyType,
        double sizeM2,
        string location,
        string description,
        string additionalNotes,
        double benchmarkPricePerM2,
        List<ValuationAdjustment> existingAdjustments)
    {
        var result = new List<AgentAdjustment>();

        // Skip if no meaningful description to analyze
        string fullText = $"{description} {additionalNotes}".Trim();
        if (fullText.Length < 50)
        {
            _logger.LogInformation("Agent: description too short ({Len} chars), skipping", fullText.Length);
            return result;
        }

        try
        {
            // Build the list of already-applied adjustments so AI doesn't duplicate
            var existingList = existingAdjustments
                .Where(a => a.Percent != 0)
                .Select(a => $"  - {a.Factor}: {a.Percent:+0.0;-0.0}% ({a.Description})")
                .ToList();
            string existingBlock = existingList.Count > 0
                ? "ALREADY-APPLIED KEYWORD ADJUSTMENTS (DO NOT DUPLICATE):\n" +
                  string.Join("\n", existingList)
                : "No keyword adjustments were applied.";

            string text = fullText.Length > 3000 ? fullText[..3000] : fullText;

            var userPrompt =
                $"SUBJECT PROPERTY:\n" +
                $"Type: {propertyType}\n" +
                $"Size: {sizeM2:N0} m²\n" +
                $"Location: {location}\n" +
                $"Benchmark Market Price: {benchmarkPricePerM2:F0} €/m²\n" +
                $"\n" +
                $"{existingBlock}\n" +
                $"\n" +
                $"DESCRIPTION TEXT TO ANALYZE:\n" +
                $"\"{text}\"\n" +
                $"\n" +
                $"INSTRUCTION:\n" +
                $"Identify value killers and boosters that the keyword system MISSED.\n" +
                $"Focus on shape/geometry, legal readiness, infrastructure gaps, and contextual red flags.\n" +
                $"If nothing unusual, return empty adjustments array.\n" +
                $"Return JSON only.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,  // Slightly above 0 for reasoning flexibility
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("adjustments", out var adjArray) &&
                adjArray.ValueKind == JsonValueKind.Array)
            {
                double totalImpact = 0;

                foreach (var item in adjArray.EnumerateArray())
                {
                    string factor = item.TryGetProperty("factor", out var f) && f.ValueKind == JsonValueKind.String
                        ? f.GetString() ?? "" : "";
                    double impact = item.TryGetProperty("impact_percent", out var imp)
                        ? (imp.ValueKind == JsonValueKind.Number ? imp.GetDouble() : 0) : 0;
                    // Also try "impact" as decimal (0.15 = 15%)
                    if (impact == 0 && item.TryGetProperty("impact", out var impDec) && impDec.ValueKind == JsonValueKind.Number)
                    {
                        double raw = impDec.GetDouble();
                        impact = Math.Abs(raw) < 1.0 ? raw * 100.0 : raw; // Convert 0.15 → 15, leave 15 as 15
                    }

                    string reasoning = item.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String
                        ? r.GetString() ?? "" : "";

                    if (string.IsNullOrEmpty(factor) || impact == 0) continue;

                    // Cap individual adjustment to ±40%
                    impact = Math.Clamp(impact, -40, 40);
                    totalImpact += impact;

                    result.Add(new AgentAdjustment
                    {
                        Factor = factor,
                        ImpactPercent = Math.Round(impact, 1),
                        Reasoning = reasoning
                    });
                }

                // Cap total to ±40%
                if (Math.Abs(totalImpact) > 40 && result.Count > 0)
                {
                    double scale = 40.0 / Math.Abs(totalImpact);
                    foreach (var adj in result)
                        adj.ImpactPercent = Math.Round(adj.ImpactPercent * scale, 1);
                    _logger.LogWarning("Agent: total impact {Total:F1}% exceeded ±40%, scaled down", totalImpact);
                }

                string confidence = root.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.String
                    ? conf.GetString() ?? "Medium" : "Medium";

                _logger.LogInformation(
                    "Agent: {Count} adjustments found (total {Total:+0.0;-0.0}%), confidence={Conf}",
                    result.Count, result.Sum(a => a.ImpactPercent), confidence);
            }
            else
            {
                _logger.LogInformation("Agent: no adjustments found (property looks standard)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent: AI analysis failed — continuing without agent adjustments");
        }

        return result;
    }
}

/// <summary>
/// A single AI-reasoned adjustment returned by the ValuationagentService.
/// </summary>
public class AgentAdjustment
{
    /// <summary>Short factor name (e.g., "Uski oblik parcele")</summary>
    public string Factor { get; set; } = "";

    /// <summary>Adjustment percentage (e.g., -35.0 for -35%)</summary>
    public double ImpactPercent { get; set; }

    /// <summary>Professional reasoning citing evidence from the description text</summary>
    public string Reasoning { get; set; } = "";
}
