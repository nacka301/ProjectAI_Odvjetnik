using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// AI Chronology Extractor — processes individual documents through GPT 
/// to extract dated legal events for case timeline construction.
/// 
/// Each document is analyzed independently (chunking strategy),
/// then results are merged and sorted chronologically on the backend.
/// </summary>
public class ChronologyService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ChronologyService> _logger;

    private const string SystemPrompt = """
        TI SI: Iskusni pravni analitičar i forenzičar dokumenata specijaliziran za hrvatski pravni sustav.
        
        ZADATAK: Analiziraj priloženi tekst dokumenta i izvuci SVE relevantne događaje, datume i činjenice za pravnu kronologiju spisa.

        PRAVILA EKSTRAKCIJE:
        
        1. **Datumi:**
           - Identificiraj sve EKSPLICITNE datume (npr. "12.03.2023.", "12. ožujka 2023.").
           - Identificiraj IMPLICITNE datume (npr. "prošli petak", "sljedeći utorak") — pretvori u datum ako je moguće, inače ostavi originalni tekst.
           - Identificiraj i kontekstualne datume ("rok od 30 dana" — izračunaj od datuma dokumenta).
        
        2. **Format datuma:** 
           - date_iso: ISO 8601 (YYYY-MM-DD) za sortiranje. Ako datum nije poznat, stavi "9999-99-99".
           - display_date: Hrvatski format (DD.MM.YYYY.) ili originalni tekst ako nije moguće pretvoriti.
        
        3. **Kontekst:**
           - Razlikuj "Datum dokumenta" (kad je nastao) od "Datuma događaja" (o kojem se priča u tekstu).
           - Ako u mailu od 15.05. piše "Sastanak je bio 10.05.", to su DVA zasebna događaja.
           - Svaki potpis, slanje, primitak, rok, odluka = zaseban događaj.
        
        4. **Sažetak:**
           - Budi kratak i precizan (pravni stil).
           - Npr. "Tužitelj šalje opomenu pred tužbu." NE "U ovom dokumentu tužitelj opisuje situaciju..."
           - Navedi tko je subjekt radnje kad je moguće.
        
        5. **Pouzdanost (confidence):**
           - "High" = datum je eksplicitno naveden u tekstu (🟢).
           - "Medium" = datum je izveden iz konteksta, npr. "sljedeći utorak" (🟡).
           - "Low" = AI pretpostavlja datum, potrebna ljudska provjera (🔴).

        6. **OCR problemi:**
           - Ako je tekst nejasan (loš OCR), ipak pokušaj izvući što možeš.
           - Označi takve događaje s confidence="Low".

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON objekt (bez markdown fencing-a):
        {
          "events": [
            {
              "date_iso": "2023-03-12",
              "display_date": "12.03.2023.",
              "time": "14:30",
              "event_description": "Prodavatelj šalje prigovor na kvalitetu radova putem e-maila.",
              "page_number": 1,
              "confidence": "High"
            }
          ]
        }

        AKO NEMA DATUMA NI DOGAĐAJA, vrati: {"events": []}
        """;

    public ChronologyService(ChatClient chatClient, ILogger<ChronologyService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Extract timeline events from a single document's text.
    /// </summary>
    public async Task<List<TimelineEvent>> ExtractEventsAsync(string filename, string documentText)
    {
        var result = new List<TimelineEvent>();

        if (string.IsNullOrWhiteSpace(documentText) || documentText.Length < 20)
        {
            _logger.LogWarning("Chronology: document '{File}' has no usable text ({Len} chars)", filename, documentText?.Length ?? 0);
            return result;
        }

        // Truncate very large documents (GPT-4o context limit)
        string text = documentText.Length > 25000 ? documentText[..25000] : documentText;

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(
                    $"NAZIV DATOTEKE: {filename}\n\n" +
                    $"TEKST DOKUMENTA:\n{text}")
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.0f,  // Precision over creativity
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
            var json = completion.Content[0].Text;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("events", out var eventsArray) &&
                eventsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in eventsArray.EnumerateArray())
                {
                    var evt = new TimelineEvent
                    {
                        DateIso = GetString(item, "date_iso", "9999-99-99"),
                        DisplayDate = GetString(item, "display_date", "Nepoznat datum"),
                        Time = GetStringOrNull(item, "time"),
                        EventDescription = GetString(item, "event_description", ""),
                        SourceFile = filename,
                        PageNumber = item.TryGetProperty("page_number", out var pn) && pn.ValueKind == JsonValueKind.Number
                            ? pn.GetInt32() : 0,
                        Confidence = GetString(item, "confidence", "Medium")
                    };

                    if (!string.IsNullOrEmpty(evt.EventDescription))
                        result.Add(evt);
                }
            }

            _logger.LogInformation("Chronology: '{File}' → {Count} events extracted", filename, result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chronology: failed to extract events from '{File}'", filename);
        }

        return result;
    }

    private static string GetString(JsonElement el, string prop, string fallback = "")
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;
    }

    private static string? GetStringOrNull(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
    }
}
