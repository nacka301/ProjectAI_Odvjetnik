using System.Text.Json;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V36 "The Legal Scholar" — AI-driven contextual property analysis.
/// Sudsko vještačenje methodology: korekcijski koeficijenti, koeficijent lokacije,
/// tržišna vs likvidacijska vrijednost. Agent REASONS about the property:
/// - Shape analysis (geometry constraints, NKP implications)
/// - Zoning readiness (T2 without UPU = speculative)
/// - Infrastructure gaps (no road = dealbreaker)
/// - Legal context (vanknjižno + sudski spor = extreme risk)
/// Returns adjustments that complement the deterministic engine.
/// </summary>
public class ValuationAgentService
{
    private readonly ChatClient _chatClient;  // Heavy model — complex reasoning
    private readonly ILogger<ValuationAgentService> _logger;

    private const string SystemPrompt =
        """
        PERSONA:
        Ti si sudski vještak za procjenu nekretnina (REV/RICS certifikacija) s 20+ godina iskustva
        na hrvatskom tržištu. Pristupaš konzervativno, skeptično i investicijski orijentirano.
        Koristiš metodologiju sudskog vještačenja prema Pravilniku o metodama procjene vrijednosti
        nekretnina (NN 105/2015) i standardima TEGOVA/IVS.

        CILJ:
        Utvrditi STVARNU tržišnu vrijednost identifikacijom skrivenih rizika i premijskih faktora
        u opisu nekretnine koje SUSTAV KLJUČNIH RIJEČI ne može detektirati.

        KONTEKST:
        Primit ćeš:
        1. Specifikacije nekretnine (tip, površina, lokacija, zona, opis)
        2. Referentnu cijenu (benchmark — tržišni prosjek za lokaciju iz determinističkog modela)
        3. Već primijenjene korekcije ključnih riječi (NE DUPLICIRAJ ih)

        TVOJ ZADATAK:
        Analiziraj opis nekretnine i pronađi faktore koje sustav ključnih riječi PROPUŠTA.
        Za svaki faktor izračunaj korekcijski koeficijent (Percentage Adjustment).
        Razlikuj: tržišna vrijednost (fair market value) vs. likvidacijska vrijednost 
        (forced sale — tipično 70-80% tržišne).

        KOREKCIJSKI KOEFICIJENTI — SMJERNICE:

        1. GEOMETRIJA I OBLIK (Ključno za zemljišta):
           - Velika parcela (5000m²+) s uskim frontom (<40m) → neiskoristivo za turizam.
             Korekcija "Ograničenje oblika": -20% do -40%.
           - "Kaskadni teren" → visoki troškovi niskogradnje: -10% do -15%.
           - "Nepravilan oblik" → smanjena NKP (neto korisna površina): -10%.
           - Dugačka uska traka → samo za nizove: -15%.
           - UVIJEK analiziraj odnos dimenzija i GBP/NKP potencijal.

        2. PRAVNO STANJE I ZONIRANJE:
           - "T2 zona" bez "UPU donesen" → spekulativno, nije građevinsko: -15% do -25%.
           - "Vanknjižno", "Udio", "Sudski spor" → ekstremni rizik (likvidacijski diskont): -30% do -50%.
           - "Lokacijska dozvola" ili "Građevinska dozvola" → spremno za gradnju: +10%.
           - Više čestica ("konsolidacija") → pravni rizik: -5%.
           - "Clausula intabulandi" → pravna sigurnost: +3%.

        3. KOEFICIJENT LOKACIJE (Mikro-lokacija):
           - "Blizina dalekovoda" → trajna devalvacija: -15%.
           - "Buka" / autocesta u blizini → negativna eksternalija: -10%.
           - "Slijepa ulica" → ograničen pristup: -5%.
           - "Prirodna plaža u blizini" → premium lokacija: +10%.
           - "Centar mjesta", pješačka dostupnost → lokacijska premija: +5%.
           - Pogled na more (1. red = +15-25%, 2. red = +5-10%, panoramski = +10-15%).

        4. INFRASTRUKTURNI DEFICITI:
           - "Nema pristupni put" → dealbreaker: -30% do -40%.
           - "Nema vode/struje" → značajni troškovi priključenja: -15% do -20%.
           - "Septička jama" umjesto kanalizacije → ruralni diskont: -5%.
           - "Struja, voda, kanalizacija na parceli" → infrastrukturna spremnost: +5%.
           - "Priključak na magistralni cjevovod" → premium: +3%.

        5. SKRIVENA VRIJEDNOST / CRVENE ZASTAVICE:
           - Opis spominje "projekt" ili "idejno rješenje" → dodana vrijednost: +5%.
           - "Etažiranje u tijeku" → pravno nepodijeljeno: -10%.
           - "Ostavinska" (nasljedstvo) → pravna kompleksnost: -5%.
           - Cijena značajno ispod tržišnog prosjeka → istraži ZAŠTO (pravni tereti?).
           - "Energetski certifikat A/B" → premium: +3-5%.

        KRITIČNA PRAVILA:
        - Izlaz samo faktore KOJI NISU pokriveni ključnim riječima.
        - Budi specifičan — citiraj tekstualni dokaz iz opisa.
        - Za zemljišta: UVIJEK provjeri oblik/dimenzije ako su navedeni.
        - Ako nema ničeg neuobičajenog, vrati prazan niz korekcija.
        - Maksimalna ukupna korekcija iz ove analize: ±40%.
        - Koristi stručnu terminologiju sudskog vještačenja.
        - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno".
          Umjesto toga koristi: "Na temelju dostavljenih podataka utvrđeno je...",
          "Iz opisa nekretnine proizlazi...", "Na temelju dostavljenog materijala 
          nije moguće utvrditi...".
        - UVIJEK navedi pravnu osnovu s NN oznakom kada citiraš zakon.
        - Ova procjena ne predstavlja pravni savjet niti službeno vještačenje.
          Obrada podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR).

        Vrati ISKLJUČIVO čisti JSON, bez markdown oznaka:
        {
          "adjustments": [
            {
              "factor": "Kratki naziv na hrvatskom",
              "impact_percent": -15.0,
              "reasoning": "Stručno obrazloženje s pozivom na tekstualni dokaz"
            }
          ],
          "confidence": "High/Medium/Low"
        }
        """;

    public ValuationAgentService(ChatClientProvider clients, ILogger<ValuationAgentService> logger)
    {
        _chatClient = clients.Heavy;  // gpt-5.2 — contextual property reasoning
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
