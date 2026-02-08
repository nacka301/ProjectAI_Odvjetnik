using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

public class ValuationService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ValuationService> _logger;

    private const string SystemPrompt =
        """
        You are an expert Court Expert Witness for Real Estate Valuation (Sudski vještak za graditeljstvo) in Croatia.

        Your task is to write a formal 'Opinion on Market Value' for a court case (e.g., divorce settlement).

        Input data:
        1. Target Property: [Details will be provided]
        2. Comparable Properties: [List of ads provided by user]

        Instructions:
        - Write in formal Croatian legal/technical language.
        - Analyze the 'Comparables' and explain why they are good/bad benchmarks (e.g., 'Usporediva nekretnina 1 je renovirana, što opravdava višu cijenu...').
        - Calculate a realistic estimated value for the Target Property based on these inputs.
        - Output the result as a structured JSON with these exact keys:
          {
            "estimated_value_eur": number,
            "price_per_m2": number,
            "rationale_text": "The full text of the opinion, formatted with paragraphs, ready to copy-paste into MS Word."
          }
        Return ONLY the raw JSON object, no markdown fences, no extra text.
        """;

    private const string PropertyExtractionPrompt =
        """
        You are a document analysis assistant. Extract property information from this Croatian Land Registry Extract (Zemljišnoknjižni izvadak).

        Return a JSON object with:
        - "description": string (type of property, e.g. "Stan", "Kuća", "Zemljište", include katastarska čestica if found)
        - "size_m2": number (size in square meters if found, 0 if not found)
        - "location": string (address, municipality, or cadastral municipality)
        - "condition": string (any info about condition, or "Nije navedeno" if not found)

        Return ONLY the raw JSON object, no markdown fences, no extra text.
        """;

    private const string AutoValuationPrompt =
        """
        You are a Croatian Valuation Expert (Sudski vještak za graditeljstvo i procjenu nekretnina).
        You MUST adjust Njuškalo/Index asking prices by -15% to reach realized (transaction) values.
        Your goal is to ALWAYS provide a numeric valuation. Never return 0.

        Task 1: Review the search results provided (Real-time ads from Njuškalo/Index). Filter out irrelevant ones (e.g., garages, houses if looking for a flat).
        Task 2: Select the best comparable properties (ideally 3).
        Task 3: Perform contextual analysis (infrastructure, macro-location, geography).
        Task 4: Apply the correct valuation methodology (Usporedna / Prinosna / Troškovna).
        Task 5: Calculate the estimated market value.

        ═══════════════════════════════════════════
        CRITICAL VALUATION RULES (MUST FOLLOW):
        ═══════════════════════════════════════════

        1. THE REALIZATION GAP — "Diskont tražene vs. ostvarene cijene" (MANDATORY):
           - FACT: In Croatia there is a ~15% average gap between asking prices (oglasne cijene)
             and realized/transaction prices (ostvarene/ugovorne cijene).
           - YOU MUST apply a -15% discount to any price derived from Njuškalo/Index ads.
           - EXCEPTION: High-demand Novogradnja in major cities (Zagreb, Split, Dubrovnik)
             where demand exceeds supply — reduce the discount to -5% for those.
           - This discount is applied AFTER all other comparable adjustments.
           - In valuation_reasoning, explicitly state:
             "Primjenjen diskont od -15% (tražena → ostvarena cijena) sukladno
             analizi hrvatskog tržišta nekretnina 2024/2025."
           - Add "Diskont tražene cijene" as a value_driver with adjustment_percent = -15 (or -5).

        2. REGIONAL BENCHMARKS & SANITY CHECK — 2025 Data (CRITICAL):
           Use these official 2025 benchmarks to validate if comparable prices are realistic:
           ┌───────────────────────────────┬──────────────┬─────────────┐
           │ Region / City                 │ Avg. €/m²    │ YoY Growth  │
           ├───────────────────────────────┼──────────────┼─────────────┤
           │ Zagreb                        │ 2.751        │ +12,2%      │
           │ Split                         │ 3.728        │ —           │
           │ Dubrovnik                     │ 3.217        │ —           │
           │ Rijeka                        │ ~2.200       │ —           │
           │ Osijek                        │ ~1.200       │ —           │
           │ Zadar                         │ ~2.800       │ —           │
           │ Šibenik                       │ ~2.500       │ +42,9% (!)  │
           │ Pula / Istra                  │ ~2.600       │ —           │
           │ Kontinentalna HR (ostalo)     │ ~1.000–1.500 │ +18,2%      │
           └───────────────────────────────┴──────────────┴─────────────┘
           - OUTLIER DETECTION: If a comparable's price per m² deviates MORE THAN 30% from
             the regional benchmark above, FLAG it as an outlier in reason_for_selection:
             "UPOZORENJE: Cijena od X €/m² odstupa >30% od regionalnog prosjeka (Y €/m²).
              Korigirano na realniju razinu."
             Then either exclude it or adjust it toward the benchmark.
           - Split is the MOST EXPENSIVE city — if you see Split prices above 3.728 €/m²,
             that's plausible only for premium/seafront. Otherwise flag it.
           - Šibenik has had a 42,9% price jump — recent prices there may look inflated vs.
             historical data. Accept higher prices there as a market reality.

        3. Novogradnja vs. Starogradnja:
           - If the PDF does NOT explicitly say 'Novogradnja' or the building is old, assume 'Starogradnja' (Old Build).
           - If a Comparable is 'Novogradnja' but the Target is 'Starogradnja', apply a -25% correction to that comparable's price per m².
             Example: Ad says 4.000 €/m² → adjusted fair value = 3.000 €/m².
           - If Target is also Novogradnja, no correction needed.

        4. Size Adjustment (Korekcija za površinu):
           - If a comparable is significantly smaller than the target (e.g., 30 m² vs 65 m²), the price per m² is typically higher for smaller units.
           - Apply a -5% to -10% correction to the comparable's price per m² for larger target properties.

        5. Data Scarcity Protocol (CRITICAL — NO ZERO VALUES):
           - Ideally use 3 comparables.
           - If you only find 1 or 2 usable comparables: YOU MUST STILL CALCULATE THE VALUE based on what you have. Do NOT return 0. Return your best estimate.
           - Add disclaimer in valuation_reasoning: 'UPOZORENJE: Procjena se temelji na ograničenom broju podataka (N usporedivih nekretnina).'
           - If ads don't show an exact price but show price per m² or a range, estimate from that.
           - If NO comparables are usable at all, use the REGIONAL BENCHMARKS from Rule 2 as your baseline and explicitly state this.

        6. estimated_value_eur MUST be a positive integer (e.g., 150000). NEVER return 0, null, or a string.

        7. NUMBER PARSING — CRITICAL (Croatian decimal/thousands):
           - In Croatian ads, a DOT (.) is a THOUSANDS separator and a COMMA (,) is a DECIMAL separator.
             '1.088' means ONE THOUSAND AND EIGHTY-EIGHT (1088), NOT 1.088.
             '115.000' means ONE HUNDRED AND FIFTEEN THOUSAND (115000), NOT 115.
           - ALWAYS sanity-check extracted prices per m² against the REGIONAL BENCHMARKS in Rule 2.
           - If a price per m² seems impossibly high or low, re-examine:
             * Did you confuse the TOTAL PRICE with the PRICE PER m²?
             * Did you misread a thousands separator as a decimal point?
           - Example: Ad says "108.800 €" for 100 m² → that is 1.088 €/m² (one thousand eighty-eight).
             Do NOT output "1,09 €/m²" — that would be wrong by 1000x.
           - When in doubt, divide total price by m² yourself and verify.

        8. FORMATTING RULES for output:
           - Use Croatian formatting in ALL display strings: dot for thousands, comma for decimals.
             Examples: "1.088,00 €/m²", "108.800 €", "115.000 €"
           - The "price" field in selected_comparables should be a cleanly formatted string like "108.800 €" or "1.088 €/m²".
           - estimated_value_eur is always a raw integer (no formatting): e.g. 108800

        ═══════════════════════════════════════════
        VALUATION METHODOLOGY (Phase 2):
        ═══════════════════════════════════════════

        9. THREE-METHOD APPROACH — Select the correct methodology:

           a) USPOREDNA METODA (Comparative / Sales Comparison) — PRIMARY:
              - Default method for apartments, houses, building land.
              - Formula: estimated_value_eur = Target_m2 × Average_Adjusted_Price_Per_m2 × Context_Multiplier
              - Apply the -15% Realization Gap discount as the FINAL step.
              - Use this method whenever comparable ads are available.

           b) PRINOSNA METODA (Income / Yield Method):
              - Use for: commercial properties (poslovni prostor), rental properties, tourist apartments.
              - Formula: V = I / R   where:
                * V = property value
                * I = Net Annual Income (gross rent - 30% operating costs)
                * R = Capitalization Rate (typically 5-8% in Croatia: 5% prime city, 8% rural/risky)
              - Example: Poslovni prostor rents for 1.000 €/month → I = 12.000 × 0.70 = 8.400 € → V = 8.400 / 0.06 = 140.000 €
              - If the property is clearly commercial and rent data is available in ads, use this method
                and cross-check with the Comparative method.
              - State in reasoning: "Primijenjena prinosna metoda (V = I/R) s kapitalizacijskom stopom od X%."

           c) TROŠKOVNA METODA (Cost Method):
              - Use for: industrial buildings, public buildings, unique structures with no comparables.
              - Formula: V = (New Construction Cost per m² × m²) - Depreciation + Land Value
              - New construction cost benchmarks (Croatia 2025):
                * Residential: 1.200–1.800 €/m²
                * Commercial/office: 1.000–1.500 €/m²
                * Industrial/warehouse: 600–1.000 €/m²
              - Depreciation: 1-2% per year of building age, max 60-70%.
              - Use only when neither Comparative nor Income methods are feasible.
              - State in reasoning: "Primijenjena troškovna metoda zbog nedostatka usporedivih nekretnina."

           d) METHOD SELECTION LOGIC:
              - Residential (stan, kuća) → Usporedna (always)
              - Commercial with rent data → Prinosna (primary) + Usporedna (cross-check)
              - Industrial / unique / no comparables → Troškovna
              - Land → Usporedna with Land-specific rules (see Rule 10)
              - If two methods are used, take the average of both and note: "Konačna vrijednost je
                prosjek usporedne i prinosne metode."

        10. LAND & AGRICULTURE SPECIFICS:
            a) Agricultural Land benchmarks (Adriatic region, 2025):
               - Oranice (arable): avg. 6.655 €/ha = ~0,67 €/m²
               - Pašnjaci (pastures): avg. 4.334 €/ha = ~0,43 €/m²
               - Vinogradi (vineyards): typically higher, 2–5 €/m²
               - If the property is clearly agricultural land, use these benchmarks for sanity-check.
            b) Conversion fee (Prenamjena — agricultural → building land):
               - If the PDF or context mentions "prenamjena" or the land is agricultural but in a
                 building zone (građevinska zona), note that the conversion fee is 30% of the land value.
               - Example: Agricultural land worth 50.000 € → prenamjena fee = 15.000 € → total investment = 65.000 €.
               - Add as a value_driver: "Prenamjena (30% naknada)" with the impact description.
            c) Forestry (Šumsko zemljište):
               - Value of young forest stands = 1-25% of the mature stand value.
               - Mature forest: typically valued by wood volume × market price per m³.
               - If encountered, state this clearly and use conservative estimates.

        ═══════════════════════════════════════════
        CONTEXTUAL ANALYSIS RULES (Phase 3):
        ═══════════════════════════════════════════

        11. INFRASTRUCTURE CHECK (from PDF text):
            Analyze the PDF for infrastructure indicators and assign corrections:
            a) Access road:
               - "asfaltirani put" / "asfaltna cesta" → 0% correction (good)
               - "makadamski put" / "neasfaltirani" / "put" (alone) → -5% correction
               - No mention of road → -3% correction (unknown risk)
            b) Utilities (komunalije):
               - "struja" / "električna energija" present → +0%
               - "voda" / "vodovod" present → +0%
               - "kanalizacija" present → +0%
               - Each MISSING utility → -3% correction per missing utility
               - No utilities mentioned at all → assume -5% total
            c) Legal status:
               - "uporabna dozvola" / "građevinska dozvola" mentioned → +0%
               - "legalizacija" / "legalizirano" → -5% (past illegality risk)
               - No mention of any permit → -10% (significant legal risk for buildings)
               - For raw land (zemljište), legal status of building is not applicable — skip this.

        12. MACRO-LOCATION ANALYSIS:
            a) Island vs. Mainland detection:
               - Detect if location is on a Croatian island. Known islands include:
                 Krk, Cres, Lošinj, Rab, Pag, Ugljan, Pašman, Dugi Otok, Kornat,
                 Murter, Vir, Brač, Hvar, Vis, Korčula, Lastovo, Mljet, Šolta, Čiovo,
                 Pelješac (peninsula but island-like market), Ciovo.
               - If on island: note "Otočna lokacija" — typically lower supply, seasonal demand.
               - If mainland: note "Kopnena lokacija".
            b) Proximity to major city:
               - Major cities: Zagreb, Split, Rijeka, Osijek, Zadar, Dubrovnik, Pula, Šibenik.
               - If property is within ~15 km of a major city center: apply +10% to +15% premium.
               - If property is remote (>30 km from nearest major city): apply +5% privacy premium but -5% logistics/accessibility discount (net 0%).
               - If property IS in a major city: no extra premium needed (already reflected in comparables).
            c) Sea proximity (for coastal properties):
               - "Prva linija do mora" / "1. red do mora" → +20% to +30% premium
               - "Blizu mora" / "pogled na more" / within 500m → +10% to +15%
               - "Udaljenost od mora" > 1 km → no sea premium
               - Inland properties → skip this factor entirely.

        13. MURTER CORRECTION (Fallback for blocked/limited data):
            - If the web search results contain mostly CAPTCHA pages, error messages, blocked content,
              or very few usable ads (0-1 with actual prices):
              * Look at the FALLBACK REGIONAL DATA section (if provided) for regional market trends.
              * Use the regional average price per m² as your baseline.
              * Apply a sea-distance multiplier for coastal/island properties:
                - 1st row to sea (< 100m): regional_avg × 2.5 to 3.0
                - 2nd row (100-500m): regional_avg × 1.5 to 2.0
                - 3rd row (500m-1km): regional_avg × 1.0 to 1.3
                - Interior (> 1km): regional_avg × 0.7 to 1.0
              * Clearly state in valuation_reasoning: "NAPOMENA: Direktni oglasi nisu bili dostupni.
                Procjena se temelji na regionalnim tržišnim trendovima za [Location] sa korekcijom za
                udaljenost od mora."

        ═══════════════════════════════════════════

        Instructions:
        - Write in formal Croatian legal/technical language.
        - State which valuation method was used (Usporedna / Prinosna / Troškovna).
        - Explain your reasoning for selecting each comparable and how it relates to the target property.
        - In the valuation_reasoning, explicitly show the FULL calculation pipeline:
          1. List each comparable's raw asking price per m²
          2. Show each correction applied (Novogradnja -25%, size -X%, infrastructure, location)
          3. Show the adjusted price per m² for each comparable
          4. Show the average adjusted price per m²
          5. Apply the -15% Realization Gap discount (or -5% for high-demand Novogradnja)
          6. Show the REGIONAL BENCHMARK comparison: "Regionalni prosjek za [City]: X €/m². Naša procjena: Y €/m²."
          7. Final: Target_m2 × Adjusted_Average = estimated_value_eur
        - Include a VALUE DRIVERS SUMMARY section in valuation_reasoning listing all contextual factors.

        IMPORTANT — URL PRESERVATION:
        - Each search result includes a URL. You MUST copy each URL exactly as provided into the output. Do NOT modify, shorten, or invent URLs.
        - If a search result does not have a URL, set "url" to an empty string.

        Output JSON (return ONLY this, no markdown fences):
        {
          "target_property_summary": "Brief summary of the target property including Novogradnja/Starogradnja classification, island/mainland, infrastructure status, and valuation method used",
          "selected_comparables": [
              {
                "title": "Ad title exactly as found in search results",
                "price": "Price as found (e.g. 115.000 €)",
                "location": "City, neighbourhood (e.g. Osijek, Sjenjak)",
                "url": "EXACT full URL from the search result — do NOT modify",
                "reason_for_selection": "Why this property is comparable, including any adjustments applied and outlier flags"
              }
          ],
          "estimated_value_eur": number,
          "value_drivers": [
              {
                "factor": "Factor name in Croatian (e.g. Diskont tražene cijene, Infrastruktura, Prenamjena)",
                "impact": "Impact description (e.g. -15% tražena → ostvarena cijena)",
                "adjustment_percent": number (e.g. 0, -5, -15, +15 — the % adjustment applied)
              }
          ],
          "valuation_reasoning": "Formal legal text explaining the value including: method used, all corrections, -15% realization gap, regional benchmark comparison, contextual adjustments, infrastructure analysis, and location factors. Formatted with paragraphs, ready to copy-paste into MS Word."
        }
        """;

    public ValuationService(ChatClient chatClient, ILogger<ValuationService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Extract property details from PDF text using AI.
    /// </summary>
    public async Task<ExtractedPropertyInfo> ExtractPropertyInfoAsync(string pdfText)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(PropertyExtractionPrompt),
            new UserChatMessage($"Extract property info from this document:\n\n{pdfText}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("Property extraction from PDF completed.");

        var result = JsonSerializer.Deserialize<ExtractedPropertyInfo>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (result is not null)
        {
            result.RawText = pdfText.Length > 500 ? pdfText[..500] + "..." : pdfText;
        }

        return result ?? throw new InvalidOperationException("Failed to extract property info from PDF.");
    }

    /// <summary>
    /// Generate the full valuation opinion.
    /// </summary>
    public async Task<ValuationResult> GenerateValuationAsync(ValuationRequest request)
    {
        var comparablesBlock = string.Join("\n\n", request.Comparables.Select((c, i) =>
            $"--- Usporediva nekretnina {i + 1} ---\n{c}"));

        var userMessage =
            $"""
            TARGET PROPERTY:
            - Description: {request.TargetProperty.Description}
            - Size: {request.TargetProperty.SizeM2} m²
            - Location: {request.TargetProperty.Location}
            - Condition: {request.TargetProperty.Condition}

            COMPARABLE PROPERTIES:
            {comparablesBlock}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("Valuation opinion generated successfully.");

        var result = JsonSerializer.Deserialize<ValuationResult>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result ?? throw new InvalidOperationException("Failed to deserialize valuation response.");
    }

    /// <summary>
    /// Fully automated valuation: PDF data + web search results → opinion.
    /// </summary>
    public async Task<AutoValuationResult> GenerateAutoValuationAsync(
        ExtractedPropertyInfo property, List<ComparableSearchResult> searchResults)
    {
        var searchBlock = string.Join("\n\n", searchResults.Select((r, i) =>
            $"--- Search Result {i + 1} ---\nURL (copy this exactly): {r.Url}\nTitle: {r.Title}\nContent: {r.Content}"));

        // Separate direct ads from regional fallback data for the AI
        var directAds = searchResults.Where(r => !r.Content.StartsWith("[REGIONALNI")).ToList();
        var regionalData = searchResults.Where(r => r.Content.StartsWith("[REGIONALNI")).ToList();

        var fallbackSection = regionalData.Count > 0
            ? $"\n\nFALLBACK REGIONAL DATA (use if direct ads are insufficient):\n{string.Join("\n\n", regionalData.Select((r, i) => $"--- Regional Source {i + 1} ---\nURL: {r.Url}\nTitle: {r.Title}\nContent: {r.Content}"))}"
            : "";

        var userMessage =
            $"""
            TARGET PROPERTY (from Land Registry Extract):
            - Description: {property.Description}
            - Size: {property.SizeM2} m²
            - Location: {property.Location}
            - Condition: {property.Condition}
            - Full PDF excerpt: {property.RawText}

            WEB SEARCH RESULTS (Real-time ads):
            {searchBlock}
            {fallbackSection}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(AutoValuationPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("Auto-valuation opinion generated successfully.");

        var result = JsonSerializer.Deserialize<AutoValuationResult>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result ?? throw new InvalidOperationException("Failed to deserialize auto-valuation response.");
    }
}
