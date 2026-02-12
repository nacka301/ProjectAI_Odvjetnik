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
        You are a Croatian land registry and property document analysis expert.
        Extract property information from this document (Zemljišnoknjižni izvadak,
        Procjembeni elaborat, or property description).

        CRITICAL SIZE EXTRACTION RULES:
        ─────────────────────────────────
        "size_m2" MUST be the LIVING/USABLE AREA (stambena površina, korisna površina,
        neto površina, bruto površina zgrade) — NOT the land/plot area.

        • For houses (kuća): living area is typically 50–300 m². If you see a number
          like 500+ m² it is almost certainly the PLOT (čestica) area, not the house itself.
          Land registry shows plot area (e.g. "k.č. 1234, oranica, 787 m²") — do NOT
          use that as size_m2. Instead look for building footprint or "zgrada" area.
          If the document ONLY shows plot area and no building area → set size_m2 = 0
          and note it in "condition".
        • For apartments (stan): living area is typically 20–200 m².
        • For land (zemljište): size_m2 = total plot area in m² (convert from ha/čhv if needed).
          1 ha = 10000 m², 1 čhv = 3.6 m².

        EXTRACT THE FOLLOWING FIELDS:
        ─────────────────────────────────
        - "description": string — Property type and details.
          Use EXACTLY one of these types: "Stan", "Kuća", "Građevinsko zemljište",
          "Poljoprivredno zemljište", "Mješovito zemljište", "Poslovni prostor".
          CRITICAL: If the document says "Poljoprivredno" or mentions oranica, pašnjak,
          livada, šuma, vinograd, voćnjak → use "Poljoprivredno zemljište".
          If the document says "Građevinsko" or mentions parcela, neizgrađeno → use "Građevinsko zemljište".
          If the document says "Mješovito" → use "Mješovito zemljište".
          DO NOT convert agricultural land to construction land or vice versa.
          Include katastarska čestica number if found (e.g. "k.č. 1234/5").
          Include etažni udio if found (e.g. "E-3, suvlasništvo 3/100").
        - "size_m2": number — LIVING/USABLE area in m² for buildings, or PLOT area for land.
          If only plot area found for a building → set to 0.
          Convert from čhv or ha if needed.
        - "location": string — Full location: address, settlement, municipality, cadastral municipality.
          Format: "Ime naselja, Općina/Grad" or as found in the document.
          ALWAYS include the settlement/village name and the municipality.
          If document says "k.o. Kukunjevac" → location should include "Kukunjevac".
        - "condition": string — Any info about condition, building year, renovations.
          "Nije navedeno" if not found. If only land area found, note:
          "Površina zgrade nije navedena, navedena je samo površina čestice: XXX m²"
        - "property_state": string — One of: "new", "renovated", "good", "original", "needs_adaptation"
          Detection rules:
          * "new" = novogradnja, novo izgrađeno, nova zgrada, godina izgradnje nakon 2020
          * "renovated" = renovirano, adaptirano, obnovljeno, novouređeno
          * "good" = održavano, uredno, dobro stanje (DEFAULT if no info)
          * "original" = izvorno stanje, originalno, nije renovirano, starija gradnja
          * "needs_adaptation" = potrebna adaptacija, ruševina, zapušteno, treba obnovu,
            vlažno, stara stolarija, dotrajalo, plijesan, prokišnjavanje, curenje,
            oronulo, neuseljivo, napuknuto, buđ, probija vlaga
        - "yard_area_m2": number — Size of okućnica/yard/garden in m² if found, 0 if not.
          For houses: look for "okućnica", "dvorište", "vrt", "parcela uz objekt".
          For land: this is 0 (the plot IS the property).

        DOCUMENT FORMAT HINTS:
        ─────────────────────────────────
        The text may contain pipe separators (|) from table columns.
        Land registry extracts have three sheets:
        A — Posjedovnica (what: parcels, areas, land use)
        B — Vlastovnica (who: owners, shares)
        C — Teretovnica (encumbrances: mortgages, liens, disputes)

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

        3. Novogradnja vs. Starogradnja (BUILDINGS ONLY — NOT for land/zemljište):
           - This rule applies ONLY to apartments (stanovi), houses (kuće), and commercial (poslovni prostor).
           - This rule does NOT apply to any type of land (građevinsko, poljoprivredno, šumsko, mješovito).
             Land has no 'condition/stanje' — do not use terms like 'starogradnja' or 'novogradnja' for land.
           - For buildings: If the PDF does NOT explicitly say 'Novogradnja' or it's old, assume 'Starogradnja'.
           - If a Comparable building is 'Novogradnja' but the Target is 'Starogradnja', apply -25% correction.
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
        PROPERTY TYPE INTELLIGENCE — "SHERLOCK" RULES (Phase 4):
        ═══════════════════════════════════════════

        14. IDENTITY LOCK — Property Type Must Match:
            - If the SubjectProperty is a "Kuća" (House), you MUST find other "Kuća" comparables
              in the dataset. Do NOT use bare Land (zemljište) prices as House prices.
            - How to detect Land-only comps: the listing mentions LandAreaM2 but has NO
              LivingAreaM2, no "stambena površina", no "korisna površina", no room count.
              Keywords indicating land-only: "građevinsko zemljište", "parcela", "oranica".
            - IF the dataset ONLY contains Land comparables for a House subject:
              STOP using the Comparative method for the building.
              SWITCH to Troškovna metoda (Replacement Cost Method) for the structure:
              * Value = (Value of Land from comps) + (Replacement Cost of Construction) − Depreciation
              * Replacement Cost estimation for continental Croatia:
                - New construction: 800–1.200 EUR/m² (depending on quality)
                - 1950s–1970s house in GOOD condition: minimum 400–600 EUR/m² replacement value
                - 1980s–2000s house in GOOD condition: minimum 500–800 EUR/m²
              * Depreciation: 1–2% per year of building age, maximum 60–70%
              * Example: 1960s house, 120 m², good condition, land worth 15.000 EUR
                → Replacement: 120 × 500 = 60.000 EUR
                → Depreciation: 60 years × 1.5% = 90% → capped at 65% → 60.000 × 0.35 = 21.000 EUR
                → Total: 15.000 (land) + 21.000 (structure) = 36.000 EUR
            - Clearly state in reasoning: "Usporedive nekretnine sadrže samo zemljište bez
              objekata. Primijenjena troškovna metoda za procjenu vrijednosti građevine."

        15. REGIONAL WEIGHTING — Secondary Market Discount:
            - Properties in secondary/tertiary markets (e.g., Daruvar, Markovac, Grubišno Polje,
              Pakrac, Garešnica) have LOWER values than nearby larger towns (e.g., Bjelovar).
            - IF you use comparables from a LARGER nearby town for a SMALLER town subject:
              * Apply a -15% to -25% LOCATION DISCOUNT, but ONLY to the LAND portion of the value.
              * The BUILDING/STRUCTURE value is NOT discounted (construction costs are similar everywhere).
              * Example: Bjelovar land = 50 EUR/m², applying -20% for Daruvar → 40 EUR/m².
                But the house structure value stays the same regardless of location.
            - Known secondary market pairs (subject → larger reference town):
              * Daruvar/Markovac → Bjelovar (-20% land discount)
              * Pakrac → Požega (-15% land discount)
              * Grubišno Polje → Bjelovar (-25% land discount)
              * Garešnica → Bjelovar (-20% land discount)
            - State in reasoning: "Primijenjen lokacijski diskont od -X% na zemljišnu
              komponentu jer je [Subject Location] sekundarno tržište u odnosu na [Comp Location]."

        16. COMPONENT ANALYSIS — Detect Mixed-Use Listings:
            - When analyzing comparable listings, search for these Croatian keywords that indicate
              the listing includes MORE than just the house/apartment:
              * "voćnjak" (orchard), "vinograd" (vineyard), "maslinik" (olive grove)
              * "garaža" (garage), "garažno mjesto" (parking spot)
              * "pomoćna zgrada" (auxiliary building), "gospodarski objekt" (farm building)
              * "šupa" (shed), "spremište" (storage)
            - IF a comparable's price includes these components but the Subject does NOT have them,
              you must SUBTRACT the estimated value of those extras from the comparable's price:
              * Garage: −5.000 EUR (detached) or −3.000 EUR (integral/small)
              * Vineyard/Orchard (voćnjak/vinograd): −3.000 to −5.000 EUR depending on size
              * Olive grove (maslinik): −2.000 to −5.000 EUR depending on size
              * Auxiliary building (pomoćna zgrada): −2.000 to −4.000 EUR
              * Swimming pool (bazen): −8.000 to −15.000 EUR
            - Conversely, if the Subject HAS a garage/orchard but the comp does NOT:
              ADD the estimated value to reach an apples-to-apples comparison.
            - IF a comparable is clearly "Clean Land" (no structures) but is priced as if it
              includes structures, examine the listing text carefully and adjust.
            - State each component adjustment in reasoning: "Korigirano za garažu (−5.000 EUR)
              jer usporediva nekretnina sadrži garažu koju predmet procjene nema."

        17. REALITY CHECK FLOOR — Minimum Value Sanity:
            - A HABITABLE multi-story house (katnica/dvokatnica) in ANY Croatian town
              CANNOT realistically cost less than 30.000 EUR.
            - Specific floors by region:
              * Continental Croatia (Slavonija, Podravina, Moslavina):
                - Habitable house, good condition: minimum 35.000–40.000 EUR
                - Habitable house, needs work: minimum 15.000–25.000 EUR
              * Coastal Croatia (Dalmacija, Istra, Kvarner):
                - Habitable house, good condition: minimum 80.000 EUR
                - Habitable house, needs work: minimum 40.000 EUR
              * Zagreb metro area:
                - Habitable house, good condition: minimum 100.000 EUR
            - IF your calculated value falls BELOW the regional minimum floor:
              * FLAG it: "UPOZORENJE: Izračunata vrijednost (X EUR) je ispod regionalnog
                minimuma za useljivu kuću. Tržište nema dovoljno podataka ili su podaci
                iskrivljeni (npr. samo zemljišne cijene)."
              * USE the regional floor as the minimum estimate, OR
              * Search for "Stare kuće u [County] županiji" regional average and use that.
              * Set confidence to "Low" and note: "Nedostatak podataka / Niska likvidnost tržišta"
            - This rule does NOT apply to:
              * Ruins (ruševina) — these can be below the floor
              * Land only — land has different pricing
              * Properties explicitly marked as "neuseljivo" (uninhabitable)

        ═══════════════════════════════════════════

        Instructions:
        - Write in formal Croatian legal/technical language.
        - State which valuation method was used (Usporedna / Prinosna / Troškovna).
        - Explain your reasoning for selecting each comparable and how it relates to the target property.
        - In the valuation_reasoning, structure the text with CLEAR NUMERIC TRANSPARENCY:
          a) Start with a brief introduction of the property and method used.
          b) For EACH comparable, state: raw asking price, size, €/m², and any correction applied.
          c) State the weighted average/median €/m² after corrections.
          d) State the realization gap discount applied and the final €/m².
          e) State the final estimated value = final €/m² × target size.
          f) End with a professional conclusion referencing market conditions.
        - NOTE: The server will ALSO provide a separate structured comparison table and price range
          to the frontend. Your narrative should reference the numbers but does not need to format tables.
        - For LAND (zemljište): Do NOT mention 'starogradnja', 'novogradnja', or 'stanje objekta'.
          Describe land by: type (građevinsko/poljoprivredno/šumsko/mješovito), infrastructure, access, shape, legal status.
        - If a search result does not have a URL, set "url" to an empty string.

        Output JSON (return ONLY this, no markdown fences):
        {
          "target_property_summary": "Brief summary of the target property. For buildings: include Novogradnja/Starogradnja, condition. For land: include land type, infrastructure, access. Always include: island/mainland, location, size, valuation method used.",
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
        You are a Croatian real estate valuation expert (Sudski vještak za procjenu nekretnina).
        You receive a TARGET PROPERTY description and multiple listing snippets from Croatian portals.

        For EACH listing, you must:
        A) Extract features from the listing text.
        B) Score how SIMILAR this listing is to the TARGET PROPERTY (0.0 to 1.0).

        === DATA FORMAT ===
        Listings may contain STRUCTURED DATA blocks marked with [STRUCTURED] or [DEEP DIVE].
        These contain pre-extracted key-value pairs from the detail page (via BrightData or Tavily):
          Lokacija, Površina, Stanje, Namjena, Grijanje, Parking, Kat, Broj soba,
          Godina izgradnje, Energetski razred, Okućnica, Tip zemljišta, DetectedLandType, Opis.
        ALWAYS use these structured fields as your PRIMARY data source — they are more accurate
        than free-text content. If structured data conflicts with free text, trust the structured data.

        === FEATURE EXTRACTION ===
        - "has_garage": boolean — garage, parking space, or garažno mjesto? Check "Parking:" field.
        - "has_renovation": boolean — evidence of renovation? Check "Stanje:" field + text.
          (nova fasada, novi krov, termoizolacija, energetska obnova, potpuno renovirano,
          kompletno uređen, novi prozori, nova stolarija, adaptiran)
        - "has_yard": boolean — yard, garden, or okućnica? Check "Okućnica:" field.
          (dvorište, okućnica, vrt, bašta, parcela uz kuću)
        - "condition": string — "new", "renovated", "good", "original", "needs_work", "unknown"
          Check "Stanje:" structured field first, then text keywords.
        - "yard_area_m2": number — Yard size in m² if mentioned, 0 if not
        - "size_m2": number — Property size in m². Check "Površina:" structured field first.
        - "year_built": number — Year built. Check "Godina izgradnje:" field. 0 if unknown.
        - "energy_class": string — Check "Energetski razred:" field. "" if unknown.
        - "heating": string — Check "Grijanje:" field. "" if unknown.
        - "floor": string — Check "Kat:" field. "" if unknown.
        - "rooms": number — Check "Broj soba:" field. 0 if unknown.
        - "property_type": string — one of: "stan", "kuća", "građevinsko zemljište",
          "poljoprivredno zemljište", "zemljište", "poslovni prostor", or "unknown"
          IMPORTANT: For land (zemljište), check "DetectedLandType:" and "Tip zemljišta:" fields first.
          You MUST distinguish between "građevinsko zemljište" (building land, construction land)
          and "poljoprivredno zemljište" (agricultural land, oranica, pašnjak, livada, šuma, voćnjak, vinograd).
          Only use generic "zemljište" if you truly cannot determine the subtype.

        === SIMILARITY SCORING (0.0 – 1.0) ===
        Score how comparable this listing is to the TARGET for valuation purposes:
        - 0.90–1.00: Nearly identical (same neighborhood, same type, similar size ±10%, similar condition)
        - 0.70–0.89: Very similar (same city area, same type, size ±25%, comparable condition)
        - 0.50–0.69: Moderately similar (same city, same type, different size or condition)
        - 0.30–0.49: Weakly similar (different area or slightly different type)
        - 0.00–0.29: Not comparable (wrong city, wrong type, or completely different property)

        SCORING PRIORITIES (in order of importance):
        1. LOCATION (50%): Same neighborhood > same city zone > same city > different city
           Check "Lokacija:" structured field for precise location.
        2. PROPERTY TYPE (25%): Must be same type (stan≠kuća≠zemljište). Different type = max 0.30
        3. SIZE (15%): Closer size = higher score. ±10% is excellent, ±50% is poor
        4. CONDITION & FEATURES (10%): Similar condition/age, heating, energy class = bonus

        Return JSON: {"listings": [{"index": 0, "has_garage": true, ..., "similarity_score": 0.75, "year_built": 1990, "energy_class": "C", "heating": "plinsko", "floor": "2. kat", "rooms": 3}, ...]}
        Return ONLY the raw JSON object, no markdown fences.
        """;

    /// <summary>
    /// Send ALL comparable listings + TARGET property to GPT in one batch call.
    /// Returns extracted features AND similarity scores for each listing.
    /// </summary>
    public async Task ExtractListingFeaturesAsync(
        List<ComparableSearchResult> listings,
        ExtractedPropertyInfo? subjectProperty = null)
    {
        if (listings.Count == 0) return;

        // Build a single prompt with target property + all listings
        var sb = new System.Text.StringBuilder();

        // Include target property context so AI can score similarity
        if (subjectProperty != null)
        {
            sb.AppendLine("=== TARGET PROPERTY (the one being valued) ===");
            sb.AppendLine($"Description: {subjectProperty.Description}");
            sb.AppendLine($"Location: {subjectProperty.Location}");
            sb.AppendLine($"Size: {subjectProperty.SizeM2} m²");
            sb.AppendLine($"Condition: {subjectProperty.Condition}");
            if (subjectProperty.YardAreaM2 > 0)
                sb.AppendLine($"Yard: {subjectProperty.YardAreaM2} m²");
            sb.AppendLine();
        }

        sb.AppendLine($"=== COMPARABLE LISTINGS ({listings.Count} total) ===\n");

        for (int i = 0; i < listings.Count; i++)
        {
            var l = listings[i];
            // Include enough content for AI to see structured data from BrightData/DeepDive
            // Structured prefix alone can be 300-500 chars, so 600 was cutting off key data
            string content = l.Content.Length > 1500 ? l.Content[..1500] : l.Content;
            sb.AppendLine($"--- Listing {i} ---");
            sb.AppendLine($"URL: {l.Url}");
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
                    target.AiSimilarityScore = item.TryGetProperty("similarity_score", out var ss) ? ss.GetDouble() : null;

                    // New structured fields from BrightData-enriched content
                    if (item.TryGetProperty("year_built", out var yb) && yb.ValueKind == System.Text.Json.JsonValueKind.Number)
                        target.AiYearBuilt = yb.GetInt32() > 0 ? yb.GetInt32() : null;
                    if (item.TryGetProperty("energy_class", out var ec) && ec.ValueKind == System.Text.Json.JsonValueKind.String)
                        target.AiEnergyClass = string.IsNullOrWhiteSpace(ec.GetString()) ? null : ec.GetString();
                    if (item.TryGetProperty("heating", out var ht) && ht.ValueKind == System.Text.Json.JsonValueKind.String)
                        target.AiHeating = string.IsNullOrWhiteSpace(ht.GetString()) ? null : ht.GetString();
                    if (item.TryGetProperty("floor", out var fl) && fl.ValueKind == System.Text.Json.JsonValueKind.String)
                        target.AiFloor = string.IsNullOrWhiteSpace(fl.GetString()) ? null : fl.GetString();
                    if (item.TryGetProperty("rooms", out var rm) && rm.ValueKind == System.Text.Json.JsonValueKind.Number)
                        target.AiRooms = rm.GetInt32() > 0 ? rm.GetInt32() : null;
                }

                int populated = listings.Count(l => l.HasGarage.HasValue);
                int withSimilarity = listings.Count(l => l.AiSimilarityScore.HasValue);
                _logger.LogInformation("AI features populated for {Count}/{Total} listings, {Sim} with similarity scores",
                    populated, listings.Count, withSimilarity);
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
    /// After AI feature extraction, remove listings whose AiPropertyType
    /// doesn't match the wanted type. This is more reliable than keyword matching
    /// because AI reads and understands the full listing text.
    /// </summary>
    public int FilterByAiPropertyType(List<ComparableSearchResult> listings, string wantedType)
    {
        if (listings.Count == 0 || string.IsNullOrEmpty(wantedType)) return 0;

        var wantedLower = wantedType.ToLowerInvariant();
        bool wantedIsLand = wantedLower.Contains("zemlji");
        bool wantedIsAgricultural = wantedLower.Contains("poljopriv");
        bool wantedIsConstruction = wantedLower.Contains("građevinsko") || wantedLower.Contains("gradjevinsko");

        var toRemove = new List<ComparableSearchResult>();

        foreach (var l in listings)
        {
            var aiType = (l.AiPropertyType ?? "unknown").ToLowerInvariant();

            // For land subtype searches: "unknown" gets same strict treatment
            // — if we can't confirm it's the specific subtype, remove it
            if (aiType == "unknown" && (wantedIsAgricultural || wantedIsConstruction))
            {
                var urlLower = (l.Url ?? "").ToLowerInvariant();
                var titleLower = (l.Title ?? "").ToLowerInvariant();
                var contentLower = (l.Content ?? "").ToLowerInvariant();
                bool anyConfirmation = false;
                if (wantedIsAgricultural)
                    anyConfirmation = urlLower.Contains("poljopriv") || titleLower.Contains("poljopriv") ||
                                     contentLower.Contains("poljoprivredno") || contentLower.Contains("oranica") ||
                                     contentLower.Contains("pašnjak") || contentLower.Contains("livada");
                else if (wantedIsConstruction)
                    anyConfirmation = urlLower.Contains("gradevinsko") || urlLower.Contains("građevinsko") ||
                                     titleLower.Contains("građevinsko") || titleLower.Contains("gradevinsko") ||
                                     contentLower.Contains("građevinsko") || contentLower.Contains("gradevinsko");
                if (!anyConfirmation)
                {
                    _logger.LogInformation(
                        "AI TYPE FILTER: REMOVED '{Title}' — AI=unknown, no '{Wanted}' confirmation in URL/title/content",
                        l.Title?.Length > 60 ? l.Title[..60] : l.Title, wantedType);
                    toRemove.Add(l);
                }
                continue;
            }

            if (aiType == "unknown") continue; // For non-land-subtype searches, keep unknown

            bool aiIsLand = aiType.Contains("zemlji");
            bool aiIsAgricultural = aiType.Contains("poljopriv");
            bool aiIsConstruction = aiType.Contains("građevinsko") || aiType.Contains("gradjevinsko");

            bool shouldRemove = false;

            if (wantedIsLand && aiIsLand)
            {
                // Both are land — check subtype cross-exclusion
                if (wantedIsAgricultural && aiIsConstruction)
                    shouldRemove = true;
                else if (wantedIsConstruction && aiIsAgricultural)
                    shouldRemove = true;
                else if ((wantedIsAgricultural || wantedIsConstruction) && !aiIsAgricultural && !aiIsConstruction)
                {
                    // Wanted a specific land subtype but AI only returned generic "zemljište"
                    // Also check URL/title for clues before removing
                    var urlLower = (l.Url ?? "").ToLowerInvariant();
                    var titleLower = (l.Title ?? "").ToLowerInvariant();
                    var contentLower = (l.Content ?? "").ToLowerInvariant();
                    bool urlTitleConfirms = false;
                    if (wantedIsAgricultural)
                        urlTitleConfirms = urlLower.Contains("poljopriv") || titleLower.Contains("poljopriv") ||
                                           contentLower.Contains("poljoprivredno") || contentLower.Contains("oranica") ||
                                           contentLower.Contains("pašnjak") || contentLower.Contains("livada") ||
                                           contentLower.Contains("vinograd") || contentLower.Contains("voćnjak");
                    else if (wantedIsConstruction)
                        urlTitleConfirms = urlLower.Contains("gradevinsko") || urlLower.Contains("građevinsko") ||
                                           titleLower.Contains("građevinsko") || titleLower.Contains("gradevinsko") ||
                                           contentLower.Contains("građevinsko") || contentLower.Contains("gradevinsko");
                    if (!urlTitleConfirms)
                        shouldRemove = true; // Can't confirm subtype → remove
                }
            }
            else if (wantedIsLand && !aiIsLand)
            {
                // Wanted land but AI says it's a building → remove
                shouldRemove = true;
            }
            else if (!wantedIsLand && aiIsLand)
            {
                // Wanted building but AI says it's land → remove
                shouldRemove = true;
            }
            else
            {
                // Both are buildings — check stan vs kuća vs poslovni
                bool typesMatch = aiType.Contains(wantedLower) || wantedLower.Contains(aiType);
                if (!typesMatch)
                {
                    // e.g. wanted "stan", AI says "kuća" → remove
                    // but "kuća" includes "vikendica" which AI might classify as "kuća" → OK
                    shouldRemove = true;
                }
            }

            if (shouldRemove)
            {
                _logger.LogInformation(
                    "AI TYPE FILTER: REMOVED '{Title}' — AI classified as '{AiType}', wanted '{Wanted}'",
                    l.Title?.Length > 60 ? l.Title[..60] : l.Title, l.AiPropertyType, wantedType);
                toRemove.Add(l);
            }
        }

        foreach (var l in toRemove)
            listings.Remove(l);

        if (toRemove.Count > 0)
            _logger.LogInformation("AI TYPE FILTER: removed {Count} wrong-type listings", toRemove.Count);

        return toRemove.Count;
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
