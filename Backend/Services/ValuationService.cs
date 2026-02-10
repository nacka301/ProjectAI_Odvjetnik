using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

public class ValuationService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ValuationService> _logger;

    private const string PropertyExtractionPrompt =
        """
        You are a document analysis assistant. Extract property information from this Croatian Land Registry Extract (Zemljisnjoknjizni izvadak) or property description.

        Return a JSON object with:
        - "description": string (type of property, e.g. "Stan", "Kuca", "Zemljiste", include katastarska cestica if found)
        - "size_m2": number (size in square meters if found, 0 if not found)
        - "location": string (address, municipality, or cadastral municipality)
        - "condition": string (any info about condition, or "Nije navedeno" if not found)
        - "property_state": string (one of: "new", "renovated", "good", "original", "needs_adaptation")
          Detection rules:
          * "new" = novogradnja, novo izgradjeno, nova zgrada
          * "renovated" = renovirano, adaptirano, obnovljeno, novoureaeno
          * "good" = odrzavano, uredno, dobro stanje
          * "original" = izvorno stanje, originalno, nije renovirano
          * "needs_adaptation" = potrebna adaptacija, rusevina, zapusteno, treba obnovu,
            vlazno, stara stolarija, dotrajalo, plijesan, prokisnjavanje, curenje,
            oronulo, neuseljivo, napuknuto, budj, probija vlaga
          If no info → use "good" as default.
        - "yard_area_m2": number (size of okucnica/yard/garden in m2 if found, 0 if not found or not applicable)
          Look for: okucnica, dvoriste, vrt, zemljiste uz kucu, parcela uz objekt.
          If the text says "okucnica 500 m2" → 500. If no mention → 0.

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

        2. OUTLIER DETECTION (from search data only):
           - Compare each comparable's price per m² against the MEDIAN of all comparables found.
           - If a comparable deviates MORE THAN 50% from the median, FLAG it as an outlier:
             "UPOZORENJE: Cijena od X €/m² znatno odstupa od medijana usporedivih (Y €/m²)."
             Then either exclude it or adjust it.
           - Do NOT use any hardcoded regional benchmark numbers. Base all analysis on the actual
             search results provided to you.

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
           - If NO comparables are usable at all, use the FALLBACK REGIONAL DATA section (if provided) and provide your best professional estimate. Never return 0.

        6. estimated_value_eur MUST be a positive integer (e.g., 150000). NEVER return 0, null, or a string.

        7. NUMBER PARSING — CRITICAL (Croatian decimal/thousands):
           - In Croatian ads, a DOT (.) is a THOUSANDS separator and a COMMA (,) is a DECIMAL separator.
             '1.088' means ONE THOUSAND AND EIGHTY-EIGHT (1088), NOT 1.088.
             '115.000' means ONE HUNDRED AND FIFTEEN THOUSAND (115000), NOT 115.
           - ALWAYS sanity-check extracted prices per m² against OTHER comparables found in the search.
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
              - Estimate construction costs from the search data and your professional knowledge.
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
            a) Agricultural Land:
               - If the property is clearly agricultural land, base the valuation on the search results provided.
               - Agricultural land is typically much cheaper than building land — adjust expectations accordingly.
               - Do NOT use any hardcoded per-hectare benchmarks. Use only real comparable data.
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
            LIMITED DATA FALLBACK:
            - If the web search results contain mostly CAPTCHA pages, error messages, blocked content,
              or very few usable ads (0-1 with actual prices):
              * Look at the FALLBACK REGIONAL DATA section (if provided) for regional market trends.
              * Extract any price data you can find from those sources.
              * Apply a sea-distance multiplier for coastal/island properties:
                - 1st row to sea (< 100m): avg × 2.5 to 3.0
                - 2nd row (100-500m): avg × 1.5 to 2.0
                - 3rd row (500m-1km): avg × 1.0 to 1.3
                - Interior (> 1km): avg × 0.7 to 1.0
              * Clearly state in valuation_reasoning: "NAPOMENA: Direktni oglasi nisu bili dostupni.
                Procjena se temelji na dostupnim regionalnim tržišnim podacima za [Location]."

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
          6f a search result does not have a URL, set "url" to an empty string.

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
          "valuation_reasoning": "Formal legal text explaining the value including: method used, all corrections, -15% realization gap, market data comparison, contextual adjustments, infrastructure analysis, and location factors. Formatted with paragraphs, ready to copy-paste into MS Word."
        }
        """;

    public ValuationService(ChatClient chatClient, ILogger<ValuationService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  V8.2: BATCH AI FEATURE EXTRACTION FOR COMPARABLES
    // ══════════════════════════════════════════════════════════

    private const string ListingFeatureExtractionPrompt =
        """
        You are a property listing analyzer. You receive multiple Croatian real estate listing snippets.
        For EACH listing, extract these features from the text:

        - "has_garage": boolean — Does this listing mention a garage, parking space, or garažno mjesto?
        - "has_renovation": boolean — Is there evidence of renovation? Look for: nova fasada, novi krov,
          termoizolacija, energetska obnova, potpuno renovirano, kompletno uređen, novi prozori, nova stolarija.
        - "has_yard": boolean — Does it mention a yard, garden, or okućnica? (dvorište, okućnica, vrt, bašta, parcela uz kuću)
        - "condition": string — One of: "new", "renovated", "good", "original", "needs_work", "unknown".
          * "new" = novogradnja, novo izgrađeno
          * "renovated" = renovirano, adaptirano, obnovljeno, kompletno uređeno
          * "good" = održavano, uredno, dobro stanje, useljivo
          * "original" = izvorno stanje, nije renovirano
          * "needs_work" = potrebna adaptacija, rušević, zapušteno, dotrajalo, vlažno, neuseljivo
          * "unknown" = no info available
        - "yard_area_m2": number — Yard/okućnica size in m² if mentioned, 0 if not mentioned.
        - "size_m2": number — Property size in m² if detected, 0 if not clear.
        - "property_type": string — "stan", "kuća", "zemljište", "poslovni prostor", or "unknown".

        Return a JSON object with a single key "listings" containing an array.
        Each array element has an "index" (matching the input listing number, 0-based) and the extracted fields.
        If you cannot determine a field, use the default (false for booleans, "unknown" for strings, 0 for numbers).

        Return ONLY the raw JSON object, no markdown fences.
        Example:
        {"listings": [{"index": 0, "has_garage": true, "has_renovation": false, "has_yard": true, "condition": "renovated", "yard_area_m2": 500, "size_m2": 85, "property_type": "kuća"}, ...]}
        """;

    /// <summary>
    /// Send ALL comparable listings to GPT in one batch call.
    /// Returns extracted features for each listing, which get stored on ComparableSearchResult.
    /// </summary>
    public async Task ExtractListingFeaturesAsync(List<ComparableSearchResult> listings)
    {
        if (listings.Count == 0) return;

        // Build a single prompt with all listings
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Analyze these {listings.Count} Croatian real estate listings:\n");

        for (int i = 0; i < listings.Count; i++)
        {
            var l = listings[i];
            // Truncate content to keep tokens reasonable
            string content = l.Content.Length > 600 ? l.Content[..600] : l.Content;
            sb.AppendLine($"--- Listing {i} ---");
            sb.AppendLine($"Title: {l.Title}");
            sb.AppendLine($"Content: {content}");
            sb.AppendLine();
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(ListingFeatureExtractionPrompt),
                new UserChatMessage(sb.ToString())
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.1f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            _logger.LogInformation("AI feature extraction completed for {Count} listings", listings.Count);

            // Parse the response
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("listings", out var listingsArr))
            {
                foreach (var item in listingsArr.EnumerateArray())
                {
                    int idx = item.TryGetProperty("index", out var idxProp) ? idxProp.GetInt32() : -1;
                    if (idx < 0 || idx >= listings.Count) continue;

                    var target = listings[idx];
                    target.HasGarage = item.TryGetProperty("has_garage", out var g) ? g.GetBoolean() : false;
                    target.HasRenovation = item.TryGetProperty("has_renovation", out var r) ? r.GetBoolean() : false;
                    target.HasYard = item.TryGetProperty("has_yard", out var y) ? y.GetBoolean() : false;
                    target.AiCondition = item.TryGetProperty("condition", out var c) ? c.GetString() : "unknown";
                    target.AiYardAreaM2 = item.TryGetProperty("yard_area_m2", out var ya) ? ya.GetDouble() : 0;
                    target.AiSizeM2 = item.TryGetProperty("size_m2", out var sz) ? sz.GetDouble() : 0;
                    target.AiPropertyType = item.TryGetProperty("property_type", out var pt) ? pt.GetString() : "unknown";
                }

                int populated = listings.Count(l => l.HasGarage.HasValue);
                _logger.LogInformation("AI features populated for {Count}/{Total} listings", populated, listings.Count);
            }
            else
            {
                _logger.LogWarning("AI feature response missing 'listings' key");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI feature extraction failed — engine will use keyword fallback");
            // Non-fatal: engine falls back to keyword detection
        }
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
    /// Fully automated valuation: PDF data + market research → opinion.
    /// Uses ONLY real search data — no hardcoded benchmarks.
    /// The engine's deterministic value is injected so the LLM must use it.
    /// </summary>
    public async Task<AutoValuationResult> GenerateAutoValuationAsync(
        ExtractedPropertyInfo property, MarketResearchResult marketData,
        DeterministicValuation? engineResult = null)
    {
        // Build active listings block
        var activeBlock = marketData.ActiveListings.Count > 0
            ? string.Join("\n\n", marketData.ActiveListings.Select((r, i) =>
                $"--- Active Listing {i + 1} ---\nURL (copy this exactly): {r.Url}\nTitle: {r.Title}\nContent: {r.Content}"))
            : "NEMA AKTIVNIH OGLASA — koristi regionalne podatke ispod ako postoje.";

        // Build regional fallback block
        var regionalBlock = marketData.RegionalData.Count > 0
            ? $"\n\nFALLBACK REGIONAL DATA (use if active ads are insufficient):\n" +
              string.Join("\n\n", marketData.RegionalData.Select((r, i) =>
                $"--- Regional Source {i + 1} ---\nURL: {r.Url}\nTitle: {r.Title}\nContent: {r.Content}"))
            : "";

        // Build context section — NO hardcoded benchmarks, only search metadata
        var contextSection =
            $"\n\n" +
            $"═══════════════════════════════════════════\n" +
            $"SEARCH METADATA (pre-computed by server):\n" +
            $"═══════════════════════════════════════════\n" +
            $"- Region: {marketData.RegionName}\n" +
            $"- Detected Property Type: {marketData.DetectedPropertyType}\n" +
            $"- Target Size: {marketData.TargetSizeM2} m2\n" +
            $"- Active Listings Found: {marketData.ActiveListings.Count}\n" +
            (string.IsNullOrEmpty(marketData.Note) ? "" : $"- NAPOMENA: {marketData.Note}\n") +
            $"\nINSTRUCTION: Base your valuation ONLY on the search results above. " +
            $"Do NOT use any memorized or hardcoded benchmark prices. " +
            $"Apply -15% realization gap to asking prices to estimate transaction values.\n";

        // ── ENGINE OVERRIDE: inject the deterministic value so the LLM MUST use it ──
        var engineOverride = "";
        if (engineResult is not null && engineResult.EstimatedValueEur > 0)
        {
            engineOverride =
                $"\n" +
                $"═══════════════════════════════════════════\n" +
                $"MANDATORY ENGINE VALUE (DO NOT OVERRIDE):\n" +
                $"═══════════════════════════════════════════\n" +
                $"The deterministic engine has ALREADY computed the final market value.\n" +
                $"- Engine Estimated Value: {engineResult.EstimatedValueEur} EUR\n" +
                $"- Engine Price Per m2: {engineResult.CalculatedPricePerM2:F0} EUR/m2\n" +
                $"- Weighted Median of {engineResult.UsablePricePoints} comparables\n" +
                $"- Realization discount already applied: {engineResult.RealizationDiscountPercent:F1}%\n" +
                $"- Confidence: {engineResult.MathConfidenceLevel} ({engineResult.MathConfidenceScore}/100)\n" +
                $"\n" +
                $"CRITICAL INSTRUCTION:\n" +
                $"- Your estimated_value_eur MUST be exactly {engineResult.EstimatedValueEur}.\n" +
                $"- Your valuation_reasoning text MUST conclude with this value ({engineResult.EstimatedValueEur} EUR).\n" +
                $"- Do NOT calculate your own value. The engine value is authoritative.\n" +
                $"- Your job is to provide the LEGAL NARRATIVE explaining WHY this value is correct,\n" +
                $"  referencing the comparables and adjustments.\n" +
                $"- Format the value in Croatian style in the text: {engineResult.EstimatedValueEur:N0} EUR\n" +
                $"═══════════════════════════════════════════\n";
        }

        var userMessage =
            $"""
            TARGET PROPERTY (from Land Registry Extract):
            - Description: {property.Description}
            - Size: {property.SizeM2} m²
            - Location: {property.Location}
            - Condition: {property.Condition}
            - Full PDF excerpt: {property.RawText}

            ACTIVE NJUŠKALO/INDEX LISTINGS (ghost/archived filtered out):
            {activeBlock}
            {regionalBlock}
            {contextSection}
            {engineOverride}
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
