using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// AI Chronology Extractor — "Forenzičar spisa"
/// Constructs legal case timelines with focus on preclusive deadlines (prekluzivni rokovi),
/// burden of proof (onus probandi), and procedural sequences (litispendencija).
/// </summary>
public class ChronologyService
{
    private readonly ChatClient _chatClient;  // Fast model — extraction only
    private readonly ILogger<ChronologyService> _logger;

    private const string SystemPrompt = """
        PERSONA:
        Ti si forenzički pravni analitičar s dugogodišnjim iskustvom u hrvatskom pravosuđu.
        Specijaliziran si za kronologiju predmeta, dokazni postupak i procesne rokove.
        Tvoj pristup je metodičan — razmišljaš o svakom dokumentu kroz prizmu 
        litispendencije (tijeka parnice) i prekluzivnih rokova.

        ZADATAK: Analiziraj priloženi tekst dokumenta i izvuci SVE relevantne događaje,
        datume i činjenice za pravnu kronologiju spisa.

        METODOLOGIJA EKSTRAKCIJE:
        
        1. **DATUMI — KLASIFIKACIJA:**
           - EKSPLICITNI datumi (npr. "12.03.2023.", "12. ožujka 2023.") → confidence=High
           - IMPLICITNI datumi (npr. "prošli petak", "sljedeći utorak") → pretvori u datum 
             ukoliko se točan datum može odrediti iz konteksta dokumenta, inače ostavi originalni tekst → confidence=Medium
           - KONTEKSTUALNI datumi ("rok od 30 dana", "u roku 8 dana od primitka") → 
             izračunaj od datuma dokumenta → confidence=Medium
           - AKO NEMA DATUMA → confidence=Low, ali ipak izvuci opis radnje
        
        2. **FORMAT DATUMA:** 
           - date_iso: ISO 8601 (YYYY-MM-DD) za sortiranje. Ako datum nije poznat: "9999-99-99"
           - display_date: Hrvatski format (DD.MM.YYYY.) ili originalni tekst
        
        3. **PRAVNI KONTEKST DOGAĐAJA — KRITIČNO:**
           - Razlikuj "Datum dokumenta" (dies a quo) od "Datuma događaja" (tempus commissi delicti)
           - Ako u mailu od 15.05. piše "Sastanak je bio 10.05." → DVA zasebna događaja
           - Svaki potpis, slanje, primitak, rok, odluka, dostava = zaseban događaj
           - PREKLUZIVNI ROKOVI: Identificiraj rokove nakon kojih se gubi pravo na radnju 
             (npr. žalba u roku 15 dana, tužba u roku 30 dana, brisovna tužba iz čl. 150. ZZK, 
             NN 91/96, 108/17)
           - MATERIJALNI DOKAZI: Označi koji dokumenti su relevantni za dokazni postupak 
             i na kojoj su strani tereta dokazivanja (onus probandi)
           - PRAVOMOĆNOST i OVRŠNOST: Označi odluke koje su postale pravomoćne ili ovršne
        
        4. **SAŽETAK — PRAVNI STIL:**
           - Budi kratak, precizan i stručan
           - Koristi pasivne konstrukcije i pravnu terminologiju:
             ✓ "Tužitelj podnosi opomenu pred tužbu suprotnoj stranci."
             ✓ "Pravomoćnom presudom utvrđena je ništetnost čl. 5. Ugovora."
             ✗ "U ovom dokumentu tužitelj opisuje situaciju..."
           - Navedi subjekt radnje, pravnu kvalifikaciju i posljedicu
        
        5. **POUZDANOST (confidence):**
           - "High" = datum eksplicitno naveden u tekstu (🟢)
           - "Medium" = datum izveden iz konteksta, npr. "sljedeći utorak" (🟡)
           - "Low" = Datum nije izričito naveden u tekstu — izveden je iz nepotpunih podataka ili je OCR nejasan (🔴)

        6. **OCR PROBLEMI:**
           - Ako je tekst nejasan (loš OCR), ipak pokušaj izvući što možeš
           - Označi takve događaje s confidence="Low"

        7. **PRAVNA PRECIZNOST:**
           - NE koristi spekulativne izraze: "moguće je", "može doći do", "vjerojatno",
             "pretpostavlja se", "postoji mogućnost".
           - UMJESTO toga koristi: "Na temelju dostavljenog dokumenta utvrđeno je...",
             "Iz dokumenta proizlazi...", "Na temelju dostavljenog dokumenta nije moguće utvrditi...".
           - UVIJEK uz zakonski članak navedi NN oznaku.

        8. **GDPR:**
           - Ova analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu 
             (NN 9/94, 117/08, 75/09, 18/11).
           - Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR).

        IZLAZNI FORMAT — vrati ISKLJUČIVO čisti JSON objekt (bez markdown fencing-a):
        {
          "events": [
            {
              "date_iso": "2023-03-12",
              "display_date": "12.03.2023.",
              "time": "14:30",
              "event_description": "Prodavatelj podnosi prigovor na kvalitetu radova putem e-maila — relevantno za teret dokazivanja (onus probandi) kvalitete ispunjenja obveze.",
              "page_number": 1,
              "confidence": "High"
            }
          ]
        }

        AKO NEMA DATUMA NI DOGAĐAJA, vrati: {"events": []}
        """;

    public ChronologyService(ChatClientProvider clients, ILogger<ChronologyService> logger)
    {
        _chatClient = clients.Fast;  // gpt-5-mini — document event extraction
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
