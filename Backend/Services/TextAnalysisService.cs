using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// V29 "Sherlock Mode" — Semantic Description Analysis.
/// Accepts raw listing description text and uses GPT-4o-mini to extract
/// structured ListingAttributes that override scraper-provided fields.
/// Agents often fill Njuškalo forms incorrectly — the truth is always in the text.
/// </summary>
public class TextAnalysisService
{
    private readonly ChatClient _chatClient;  // Fast model — extraction only
    private readonly ILogger<TextAnalysisService> _logger;

    private const string SystemPrompt =
        """
        You are a Croatian real estate data extraction expert.
        Analyze the following real estate description text.
        Ignore marketing fluff ('prekrasan', 'jedinstven', 'iznimna prilika').
        Focus on HARD FACTS only.

        Extract these fields:

        1. "renovation_status": one of "New", "Renovated", "Livable", "NeedsAdaptation", "Ruin", "Unknown"
           - "New" → novogradnja, novoizgrađeno, novo, upravo dovršeno
           - "Renovated" → renovirano, adaptirano, obnovljeno, kompletno uređeno, nova fasada,
             novi prozori, nova stolarija, energetska obnova, potpuno renoviran
           - "Livable" → useljivo, u dobrom stanju, održavano, solidno stanje, uredno
           - "NeedsAdaptation" → potrebna adaptacija, za uređenje, potrebna obnova,
             potrebna potpuna adaptacija, za renovaciju, potrebno ulaganje
           - "Ruin" → ruševina, za rušenje, zapušteno, dotrajalo, devastirano, urušeno

        2. "is_urgent": boolean — true if text contains urgency signals:
           hitno, iseljenje, prilika, sniženo, cijena snižena, zadnja cijena,
           mora se prodati, ostavinski, povoljno, akcija, brza prodaja

        3. "has_garage": boolean — true if text mentions garage, parking space,
           garažno mjesto, garaža, parking, parkirno mjesto

        4. "view_type": one of "Unknown", "Street", "Courtyard", "Park", "City", "Sea", "Panoramic"
           - "Sea" → pogled na more, morska panorama, sea view, uz more
           - "Panoramic" → panoramski pogled, panorama, otvoreni pogled
           - "City" → pogled na grad, gradska panorama, city view
           - "Park" → pogled na park, zelenilo, pogled na šumu
           - "Street" → ulična strana, pogled na ulicu
           - "Courtyard" → dvorišna strana, pogled na dvorište

        5. "legal_status": one of "Clean", "Issues", "Share", "LegalizationNeeded", "Vanknjizno", "Unknown"
           - "Clean" → vlasništvo 1/1, uredni papiri, čisto vlasništvo, knjižno vlasništvo,
             upisano u gruntovnicu, bez tereta
           - "Issues" → teret, hipoteka, spor, ovršni postupak
           - "Share" → suvlasništvo, idealni dio, ½, etažirano
           - "LegalizationNeeded" → legalizacija, u postupku legalizacije, nelegalno,
             bez dozvole, bez građevinske dozvole, izgrađeno bez dozvole
           - "Vanknjizno" → vanknjižno, nije upisano, neupisano, bez upisa u ZK

        6. "actual_land_type": one of "Building", "Agricultural", "Mixed", "Unknown"
           CRITICAL RULE — this overrides the scraper's category:
           - If text mentions "poljoprivredno", "šuma", "pašnjak", "oranica", "livada",
             "voćnjak", "vinograd", "u naravi oranica" → set to "Agricultural"
           - If text mentions "građevinsko", "zona M1", "dozvola", "građevinska",
             "urbanistički", "lokacijska dozvola", "UPU" → set to "Building"
           - If text mentions BOTH → "Mixed"
           - If neither or not land → "Unknown"

        7. "commercial_potential": one of "None", "Apartments", "Restaurant", "Office", "Mixed"
           - "Apartments" → apartmani, turizam, turistička namjena, iznajmljivanje
           - "Restaurant" → restoran, caffe bar, ugostiteljstvo, konoba
           - "Office" → ured, poslovni prostor, kancelarija
           - "Mixed" → multiple commercial uses mentioned
           - "None" → no commercial use mentioned

        8. "is_business_premise": boolean — true if poslovni prostor, trgovina, lokal,
           ured, ugostiteljski objekt, skladište

        9. "sea_distance": string or null — extract exact distance to sea/beach if mentioned:
           "50m", "100m", "200m do plaže", "1. red do mora", "2. red", "uz more"
           Return null if not mentioned.

        10. "has_pool": boolean — true if bazen, swimming pool, jacuzzi mentioned

        11. "microlocation": one of "Quiet", "BusyRoad", "Isolated", "CityCenter", "Unknown"
            - "Quiet" → mirna lokacija, mirna ulica, tiha ulica, privatnost
            - "BusyRoad" → prometna cesta, glavna cesta, blizu autoputa, bučno
            - "Isolated" → izolirano, udaljeno, bez susjeda, selo, zabačeno
            - "CityCenter" → centar grada, širi centar, strogi centar, centar

        12. "access_type": one of "Asphalt", "Macadam", "FootpathOnly", "Unknown"
            - "Asphalt" → asfaltni pristup, asfaltirana cesta, asfaltni put
            - "Macadam" → makadam, makadamski put, zemljani put, šljunčani put
            - "FootpathOnly" → pješački pristup, samo pješice, nema kolnog pristupa

        13. "major_damages": array of strings, empty if none. Possible values:
            "Damp" → vlaga, vlažnost, prokišnjavanje
            "RoofLeak" → krov prokišnjava, oštećen krov, potrebna zamjena krova
            "StaticIssue" → statički problem, pukotine na zidovima, konstrukcijski problem,
              nagnut objekt, urušavanje
            "Foundation" → temelji, problem s temeljima
            Only include if EXPLICITLY mentioned. Do NOT infer from age or condition.

        14. "energy_class": string or null — A+, A, B, C, D, E, F, G if mentioned. Null if not.
            Look for: energetski razred, energetski certifikat, energy class

        15. "has_elevator": boolean — true if dizalo, lift, elevator mentioned

        16. "heating_system": one of "HeatPump", "Gas", "Wood", "Electric", "CentralHeating", "Unknown"
            - "HeatPump" → toplinska pumpa, heat pump, klima uređaj (za grijanje)
            - "Gas" → plin, plinski, plinsko grijanje, gradski plin
            - "Wood" → drva, peć na drva, kamin, centralno na drva
            - "Electric" → električno grijanje, električni radijatori, podno grijanje
            - "CentralHeating" → centralno grijanje, gradsko grijanje, etažno grijanje

        Return ONLY raw JSON, no markdown fences:
        {
          "renovation_status": "...",
          "is_urgent": false,
          "has_garage": false,
          "view_type": "Unknown",
          "legal_status": "Unknown",
          "actual_land_type": "Unknown",
          "commercial_potential": "None",
          "is_business_premise": false,
          "sea_distance": null,
          "has_pool": false,
          "microlocation": "Unknown",
          "access_type": "Unknown",
          "major_damages": [],
          "energy_class": null,
          "has_elevator": false,
          "heating_system": "Unknown"
        }
        """;

    public TextAnalysisService(ChatClientProvider clients, ILogger<TextAnalysisService> logger)
    {
        _chatClient = clients.Fast;  // Fast model — listing description extraction
        _logger = logger;
    }

    /// <summary>
    /// Analyze a single listing description and return structured attributes.
    /// Uses GPT-4o-mini for speed and cost efficiency.
    /// </summary>
    public async Task<ListingAttributes?> AnalyzeDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length < 30)
            return null;

        try
        {
            // Truncate to 2000 chars — enough for structured extraction, saves tokens
            string text = description.Length > 2000 ? description[..2000] : description;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(text)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var attrs = new ListingAttributes
            {
                RenovationStatus = ParseEnum<RenovationStatus>(root, "renovation_status"),
                IsUrgent = root.TryGetProperty("is_urgent", out var iu) && iu.GetBoolean(),
                HasGarage = root.TryGetProperty("has_garage", out var hg) && hg.GetBoolean(),
                ViewType = ParseEnum<ViewType>(root, "view_type"),
                LegalStatus = ParseEnum<LegalStatus>(root, "legal_status"),
                ActualLandType = ParseEnum<ActualLandType>(root, "actual_land_type"),
                // Super-Extractor fields
                CommercialPotential = ParseString(root, "commercial_potential") ?? "None",
                IsBusinessPremise = root.TryGetProperty("is_business_premise", out var ibp) && ibp.GetBoolean(),
                SeaDistance = ParseString(root, "sea_distance"),
                HasPool = root.TryGetProperty("has_pool", out var hp) && hp.GetBoolean(),
                Microlocation = ParseString(root, "microlocation") ?? "Unknown",
                AccessType = ParseString(root, "access_type") ?? "Unknown",
                MajorDamages = ParseStringArray(root, "major_damages"),
                EnergyClass = ParseString(root, "energy_class"),
                HasElevator = root.TryGetProperty("has_elevator", out var he) && he.GetBoolean(),
                HeatingSystem = ParseString(root, "heating_system") ?? "Unknown",
            };

            _logger.LogInformation(
                "Sherlock: RenovStatus={Renov}, Urgent={Urgent}, Garage={Garage}, View={View}, Legal={Legal}, LandType={Land}, Commercial={Commercial}, Damages={Damages}, Elevator={Elevator}",
                attrs.RenovationStatus, attrs.IsUrgent, attrs.HasGarage,
                attrs.ViewType, attrs.LegalStatus, attrs.ActualLandType,
                attrs.CommercialPotential,
                attrs.MajorDamages.Count > 0 ? string.Join(",", attrs.MajorDamages) : "none",
                attrs.HasElevator);

            return attrs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sherlock: text analysis failed — skipping");
            return null;
        }
    }

    /// <summary>
    /// Analyze multiple listings in a single batch call for efficiency.
    /// Returns a dictionary mapping listing index → ListingAttributes.
    /// </summary>
    public async Task<Dictionary<int, ListingAttributes>> AnalyzeBatchAsync(
        List<(int Index, string Description)> items)
    {
        var result = new Dictionary<int, ListingAttributes>();
        if (items.Count == 0) return result;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Analyze {items.Count} real estate descriptions. Return JSON with \"listings\" array.");
            sb.AppendLine("Each item has \"index\" plus the 16 fields from the system prompt.\n");

            foreach (var (idx, desc) in items)
            {
                string text = desc.Length > 1500 ? desc[..1500] : desc;
                sb.AppendLine($"--- Listing {idx} ---");
                sb.AppendLine(text);
                sb.AppendLine();
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(sb.ToString())
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("listings", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    int idx = item.TryGetProperty("index", out var idxProp) ? idxProp.GetInt32() : -1;
                    if (idx < 0) continue;

                    result[idx] = new ListingAttributes
                    {
                        RenovationStatus = ParseEnum<RenovationStatus>(item, "renovation_status"),
                        IsUrgent = item.TryGetProperty("is_urgent", out var iu) && iu.GetBoolean(),
                        HasGarage = item.TryGetProperty("has_garage", out var hg) && hg.GetBoolean(),
                        ViewType = ParseEnum<ViewType>(item, "view_type"),
                        LegalStatus = ParseEnum<LegalStatus>(item, "legal_status"),
                        ActualLandType = ParseEnum<ActualLandType>(item, "actual_land_type"),
                        // Super-Extractor fields
                        CommercialPotential = ParseString(item, "commercial_potential") ?? "None",
                        IsBusinessPremise = item.TryGetProperty("is_business_premise", out var ibp) && ibp.GetBoolean(),
                        SeaDistance = ParseString(item, "sea_distance"),
                        HasPool = item.TryGetProperty("has_pool", out var hp) && hp.GetBoolean(),
                        Microlocation = ParseString(item, "microlocation") ?? "Unknown",
                        AccessType = ParseString(item, "access_type") ?? "Unknown",
                        MajorDamages = ParseStringArray(item, "major_damages"),
                        EnergyClass = ParseString(item, "energy_class"),
                        HasElevator = item.TryGetProperty("has_elevator", out var he) && he.GetBoolean(),
                        HeatingSystem = ParseString(item, "heating_system") ?? "Unknown",
                    };
                }
            }

            _logger.LogInformation("Sherlock batch: analyzed {Parsed}/{Total} listings",
                result.Count, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sherlock batch: analysis failed — returning empty");
        }

        return result;
    }

    private static T ParseEnum<T>(JsonElement element, string propertyName) where T : struct, Enum
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String &&
            Enum.TryParse<T>(prop.GetString(), ignoreCase: true, out var val))
        {
            return val;
        }
        return default;
    }

    private static string? ParseString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
        }
        return null;
    }

    private static List<string> ParseStringArray(JsonElement element, string propertyName)
    {
        var list = new List<string>();
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s);
                }
            }
        }
        return list;
    }
}
