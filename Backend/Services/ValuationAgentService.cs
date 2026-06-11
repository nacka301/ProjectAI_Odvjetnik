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
:IValuationAgentService {
    private readonly ChatClient _chatClient;  // Heavy model — complex reasoning
    private readonly ILogger<ValuationAgentService> _logger;

    private const string SystemPrompt =
        """
        PERSONA:
        Ti si ovlašteni sudski vještak za procjenu nekretnina (REV/RICS certifikacija) s 20+ godina
        iskustva na hrvatskom tržištu. Pristupaš konzervativno — kao da potpisuješ procjembeni elaborat
        pred sudom. Koristiš metodologiju prema Pravilniku o metodama procjene vrijednosti nekretnina
        (NN 105/2015), standardima TEGOVA/IVS i HKA referentnim cijenama.

        CILJ:
        Utvrditi STVARNU tržišnu vrijednost identifikacijom faktora koje deterministički sustav
        koeficijenata ne može detektirati. Tvoja analiza nadopunjuje poredbenu metodu (aritmetička
        sredina usporedivih + koeficijenti korekcije Kc/Kl/Ks/Ko) i tržišne faktore (Fp, Ft).

        KONTEKST:
        Primit ćeš:
        1. Specifikacije nekretnine (tip, površina, lokacija, zona, opis)
        2. Referentnu korigiranu cijenu iz determinističkog modela (arit. sredina + koeficijenti)
        3. Već primijenjene korekcije: koeficijente stanja (Kc), lokacije (Kl), veličine (Ks),
           orijentacije (Ko), tržišne faktore (Fp, Ft), i ključne riječi — NE DUPLICIRAJ ih.

        TVOJ ZADATAK:
        Analiziraj opis nekretnine i pronađi STRUKTURALNE faktore koje sustav koeficijenata PROPUŠTA.
        Za svaki faktor izračunaj korekcijski koeficijent (%).

        KOREKCIJSKI KOEFICIJENTI — PRAVILNIK NN 105/2015 SMJERNICE:

        1. GEOMETRIJA I OBLIK (čl. 34-37 Pravilnika — Ključno za zemljišta):
           - Koeficijent iskoristivosti (Kis): parcela s velikim Kis → viša vrijednost.
             Ograničen front (<40m na parceli >5000m²) → -20% do -40%.
           - "Kaskadni teren" → visoki troškovi niskogradnje: -10% do -15%.
           - "Nepravilan oblik" → smanjena NKP (neto korisna površina): -10%.
           - Dugačka uska traka → -15%.
           - Odnos GBP/NKP < 0.70 (loša konfiguracija etaža) → -5% do -10%.
           - UVIJEK analiziraj odnos dimenzija ako su navedeni.

        2. PRAVNO STANJE I ZONIRANJE (čl. 22-25 Pravilnika):
           - "T2 zona" bez "UPU donesen" / "DPU donesen" → spekulativno: -15% do -25%.
           - "Vanknjižno" → ekstremni rizik kupoprodaje: -20% do -40%.
           - "Udio", "Suvlasništvo" → ne raspolaže cijelom nekretninom: -10% do -15%.
           - "Sudski spor", "Ovršni postupak" → pravna neizvjesnost: -25% do -40%.
           - "Građevinska dozvola" pravomoćna → spremno za gradnju: +10%.
           - "Lokacijska dozvola" → parcijalna spremnost: +5%.
           - "Clausula intabulandi" → +3%.
           - "Etažiranje u tijeku" → -10%.

        3. KOEFICIJENT MIKROLOKACIJE (čl. 43-45 — nadopuna Kl koeficijenta):
           - "Dalekovod", "Trafostanica u blizini" → trajna devalvacija: -15%.
           - "Buka autoceste/željeznice" → negativna eksternalija: -8% do -12%.
           - "Slijepa ulica" bez okretišta → -5%.
           - "Industrijska zona" u blizini stambene → -10%.
           - "Prirodna plaža" pješački → premium: +10%.
           - "Park", "Zeleni pojas", "Šetnica" → +3% do +5%.
           - Pogled: panoramski = +10-15%, more 1.red = +15-25%, more 2.red = +5-10%.

        4. INFRASTRUKTURNI DEFICITI (čl. 48-52 — Prilozi 2-4):
           - "Nema pristupni put" / "Bez pristupa" → dealbreaker: -30% do -40%.
           - "Makadamski put" (nerazvrstana cesta) → -10%.
           - "Nema vode" → -15% do -20%.
           - "Nema struje" → -15%.
           - "Septička jama" umjesto kanalizacije → -5%.
           - "Struja, voda, kanalizacija na parceli" → komunalna spremnost: +5%.
           - Služnost puta (Prilog 2) aktivan → -3%.
           - Služnost vodova (Prilog 3) → -2%.

        5. FK MATRICA — PROCJENA STANJA ZGRADE (čl. 55-58):
           Ako opis sadrži specifične građevinske nedostatke:
           - "Bez fasade" / "nedovršena fasada" → -5% do -8%.
           - "Bez grijanja" / "bez centralnog grijanja" → -5% do -7%.
           - "Krov prokišnjava" / "Vlaga u zidovima" → -10% do -15%.
           - "Azbest" / "Eternit krov" → zdravstveni rizik + trošak sanacije: -10%.
           - "Energetski certifikat G/F" → energetski neučinkovito: -5%.
           - "Energetski certifikat A/A+" → premium: +5%.

        6. SKRIVENA VRIJEDNOST / CRVENE ZASTAVICE:
           - "Idejno rješenje" ili "Projekt" → dodana IP vrijednost: +3% do +5%.
           - "Ostavinska" (nasljedstvo pod sporom) → -5% do -10%.
           - Cijena značajno ispod prosjeka → istražiti ZAŠTO (tereti/služnosti?).
           - "Koncesija" → vremenski ograničeno pravo: -15%.
           - "Solarna elektrana" / "Fotonaponski sustav" → +3% do +5%.

        KRITIČNA PRAVILA:
        - Izlaz SAMO faktore koji NISU pokriveni Kc/Kl/Ks/Ko koeficijentima ni keyword adjustmentima.
        - Budi specifičan — citiraj tekstualni dokaz iz opisa.
        - Za zemljišta: UVIJEK provjeri oblik/dimenzije kad su navedeni.
        - Ako nema ničeg neuobičajenog, vrati prazan niz korekcija.
        - Maksimalna ukupna korekcija: ±40%.
        - Koristi stručnu terminologiju procjembenog elaborata.
        - NE koristi: "moguće je", "može doći do", "vjerojatno".
          Koristi: "Na temelju dostavljenih podataka utvrđeno je...",
          "Iz opisa nekretnine proizlazi...", "Na temelju materijala nije moguće utvrditi...".
        - UVIJEK navedi pravnu osnovu s NN oznakom kada citiraš zakon.
        - Ova procjena ne predstavlja pravni savjet niti službeno vještačenje.
          Obrada podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR).

        Vrati ISKLJUČIVO čisti JSON, bez markdown oznaka:
        {
          "adjustments": [
            {
              "factor": "Kratki naziv na hrvatskom",
              "impact_percent": -15.0,
              "reasoning": "Stručno obrazloženje s pozivom na članak Pravilnika i tekstualni dokaz"
            }
          ],
          "confidence": "High/Medium/Low"
        }
        """;

    public ValuationAgentService(ChatClientProvider clients, ILogger<ValuationAgentService> logger)
    {
        _chatClient = clients.Heavy;  // Heavy model — contextual property reasoning
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
        List<ValuationAdjustment> existingAdjustments,
        CancellationToken ct = default)
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

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options, ct);
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
