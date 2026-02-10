using System.Text.Json;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Parses a ZK izvadak (land registry extract) PDF into structured data.
/// Extracts all 3 sheets: A (Posjedovnica), B (Vlastovnica), C (Teretovnica).
/// Also provides property info suitable for the valuation pipeline.
/// </summary>
public class ZkParsingService
{
    private readonly ChatClient _chatClient;
    private readonly PdfService _pdfService;
    private readonly ILogger<ZkParsingService> _logger;

    private const string ZkParsingPrompt =
        """
        Ti si expert za hrvatske zemljišnoknjižne izvadke. Analiziraj kompletni ZK izvadak 
        i izvuci SVE podatke iz sva tri lista.

        ═══════════════════════════════════════════
        LIST A — POSJEDOVNICA (What: parcels, areas, land use)
        ═══════════════════════════════════════════
        Izvuci:
        - Katastarsku općinu (k.o.)
        - Broj ZK uloška (ZK.ul.)
        - Sve čestice s opisom kulture i površinom
          (oranica, livada, dvorište, kuća i dvor, voćnjak, šuma...)
        - Etažni udio ako postoji (E-1, E-2, suvlasništvo X/Y)
        - Ukupnu površinu zemljišta
        - Površinu zgrade/stana ako je navedena

        PRAVILA ZA POVRŠINU:
        - "čestica X, oranica, 787 m²" → to je POVRŠINA ZEMLJIŠTA (land_area_m2)
        - "kuća i dvor" → česta oznaka za stambeni objekt na parceli
        - Ako piše "zgrada 120 m²" ili "stan 65 m²" → BUILDING area (building_area_m2)
        - Ako je samo površina čestice bez zgrade → building_area_m2 = 0
        - Pretvori čhv u m² (1 čhv = 3.6 m²) i ha u m² (1 ha = 10000 m²)

        ═══════════════════════════════════════════
        LIST B — VLASTOVNICA (Who: owners, shares)
        ═══════════════════════════════════════════
        Izvuci:
        - Svako vlasnika: ime, udio (1/1, 1/2, itd.), OIB ako postoji
        - Način stjecanja (temeljem kupoprodajnog ugovora, nasljeđivanja, darovnog ugovora...)
        - Ignoriraj predbilježbe koje su brisane

        ═══════════════════════════════════════════
        LIST C — TERETOVNICA (Encumbrances: mortgages, liens, disputes)
        ═══════════════════════════════════════════
        Izvuci:
        - Hipoteke: iznos, valuta, godina, korisnik (banka), detalji
          IGNORIRAJ "uknjižba brisana" / "brisano" — to su zatvorene hipoteke
        - Sporove: "Zabilježba spora", "Ovrha", "Zabilježba tužbe"
        - Ostali tereti: služnosti, pravo građenja, plombe, zabrane otuđenja
          Za svaki: tip, opis, korisnik, je li aktivan

        ═══════════════════════════════════════════
        PROCJENA RIZIKA
        ═══════════════════════════════════════════
        Na temelju C lista ocijeni rizik:
        - "LOW" = nema aktivnih hipoteka, sporova, ni plombi
        - "MEDIUM" = aktivne hipoteke ALI nema sporova, ili služnosti postoje
        - "HIGH" = aktivni sporovi, ovrhe, plombe, ili nerazjašnjeno vlasništvo

        ═══════════════════════════════════════════
        TIP NEKRETNINE
        ═══════════════════════════════════════════
        Detektiraj tip:
        - "stan" = stan, apartman, etažni udio u stambenoj zgradi
        - "kuća" = kuća i dvor, obiteljska kuća, stambena zgrada
        - "zemljište" = samo zemljište bez zgrade (oranica, livada, građevinsko)
        - "poslovni_prostor" = ured, lokal, poslovni objekt

        ═══════════════════════════════════════════
        LOKACIJA
        ═══════════════════════════════════════════
        Izvuci lokaciju:
        - location: "Ime naselja, Općina/Grad" — uvijek uključi k.o. ime i općinu
        - address: ulica i kbr. ako postoji, inače prazan string

        ═══════════════════════════════════════════
        LAWYER COMMENT
        ═══════════════════════════════════════════
        Kratki profesionalni sažetak na hrvatskom koji objašnjava:
        - Pravno stanje nekretnine
        - Eventualne rizike (hipoteke, sporovi)
        - Preporuku za kupca/odvjetnika
        Ton: formalan, kratak, jasan. Kao da si sudski vještak.

        ═══════════════════════════════════════════
        OUTPUT FORMAT — Return ONLY raw JSON, no markdown fences:
        ═══════════════════════════════════════════
        {
          "property_id": "k.o. Ime, k.č. 1234/5",
          "cadastral_municipality": "Ime",
          "parcel_number": "1234/5",
          "zk_number": "ZK.ul. 567",
          "property_type": "kuća|stan|zemljište|poslovni_prostor",
          "property_description": "Kuća i dvor u k.o. Kukunjevac",
          "land_area_m2": 787,
          "building_area_m2": 0,
          "land_use": "oranica|dvorište|kuća i dvor|...",
          "etazni_udio": "E-3, 3/100 ili prazan string",
          "parcels": [
            {"parcel_number": "1234/5", "land_use": "oranica", "area_m2": 500, "description": ""}
          ],
          "owners": [
            {"name": "Ime Prezime", "share": "1/1", "oib": "12345678901", "basis_of_acquisition": "kupoprodajni ugovor"}
          ],
          "active_mortgages": [
            {"amount": "50.000", "currency": "EUR", "year": "2020", "details": "Hipoteka u korist XY banke"}
          ],
          "active_disputes": ["Zabilježba spora radi utvrđivanja prava vlasništva"],
          "other_encumbrances": [
            {"type": "služnost", "description": "Pravo prolaza", "beneficiary": "Ime Prezime", "is_active": true}
          ],
          "risk_score": "LOW|MEDIUM|HIGH",
          "lawyer_comment": "Kratki pravni sažetak na hrvatskom...",
          "location": "Naselje, Općina/Grad",
          "address": "Ulica 5 ili prazan string",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

    public ZkParsingService(ChatClient chatClient, PdfService pdfService, ILogger<ZkParsingService> logger)
    {
        _chatClient = chatClient;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// Parse a ZK izvadak PDF into structured data.
    /// Returns complete ZkParsedResult with all 3 sheets + risk assessment.
    /// </summary>
    public async Task<(ZkParsedResult Parsed, string RawPdfText)> ParseZkIzvadakAsync(IFormFile file)
    {
        // Step 1: Extract text from PDF
        var pdfText = _pdfService.ExtractText(file);

        if (string.IsNullOrWhiteSpace(pdfText) || pdfText.StartsWith("[UPOZORENJE"))
        {
            _logger.LogWarning("ZK PDF extraction returned empty/warning text");
            throw new InvalidOperationException(
                "PDF ne sadrži čitljiv tekst. Moguće da je skeniran bez OCR sloja.");
        }

        _logger.LogInformation("ZK PDF extracted: {Chars} chars", pdfText.Length);

        // Step 2: Send to GPT for structured parsing
        var result = await ParseZkTextAsync(pdfText);

        return (result, pdfText);
    }

    /// <summary>
    /// Parse ZK izvadak from already-extracted text.
    /// Useful when text is already available.
    /// </summary>
    public async Task<ZkParsedResult> ParseZkTextAsync(string pdfText)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(ZkParsingPrompt),
            new UserChatMessage($"Analiziraj ovaj zemljišnoknjižni izvadak:\n\n{pdfText}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);
        var json = completion.Content[0].Text;

        _logger.LogInformation("ZK parsing completed by GPT.");

        var result = JsonSerializer.Deserialize<ZkParsedResult>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (result is null)
            throw new InvalidOperationException("Failed to deserialize ZK parsing response.");

        // Post-process: ensure property_id is set
        if (string.IsNullOrEmpty(result.PropertyId) && !string.IsNullOrEmpty(result.CadastralMunicipality))
        {
            result.PropertyId = $"k.o. {result.CadastralMunicipality}, k.č. {result.ParcelNumber}";
        }

        return result;
    }

    /// <summary>
    /// Convert ZkParsedResult into ExtractedPropertyInfo suitable for the valuation pipeline.
    /// Bridges ZK analysis → valuation engine.
    /// </summary>
    public static ExtractedPropertyInfo ToPropertyInfo(ZkParsedResult zk, string rawPdfText)
    {
        // Determine what size to use for valuation:
        // - If building area is known → use that (for kuća/stan)
        // - If only land area → use that (for zemljište)
        // - If property is kuća but only land area → set 0 and note in condition
        double sizeM2;
        string condition = zk.Condition;

        if (zk.PropertyType is "zemljište")
        {
            sizeM2 = zk.LandAreaM2;
        }
        else if (zk.BuildingAreaM2 > 0)
        {
            sizeM2 = zk.BuildingAreaM2;
        }
        else
        {
            // House/apartment but no building area found
            sizeM2 = 0;
            condition = $"Površina zgrade nije navedena u ZK izvadku, navedena je samo površina čestice: {zk.LandAreaM2} m²";
        }

        // Build description from ZK data
        var typeLabel = zk.PropertyType switch
        {
            "stan" => "Stan",
            "kuća" => "Kuća",
            "zemljište" => "Građevinsko zemljište",
            "poslovni_prostor" => "Poslovni prostor",
            _ => "Nekretnina"
        };

        var desc = $"{typeLabel}, {zk.PropertyDescription}";
        if (!string.IsNullOrEmpty(zk.ParcelNumber))
            desc += $", k.č. {zk.ParcelNumber}";
        if (!string.IsNullOrEmpty(zk.EtazniUdio))
            desc += $", {zk.EtazniUdio}";

        return new ExtractedPropertyInfo
        {
            Description = desc,
            SizeM2 = sizeM2,
            Location = zk.Location,
            Condition = condition,
            PropertyState = zk.PropertyState,
            YardAreaM2 = zk.YardAreaM2,
            RawText = rawPdfText.Length > 500 ? rawPdfText[..500] + "..." : rawPdfText
        };
    }
}
