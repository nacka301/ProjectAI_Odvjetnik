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
        TI SI: Iskusni odvjetnički vježbenik specijaliziran za promet nekretninama i dubinsku pravnu analizu (Due Diligence) hrvatskih zemljišnoknjižnih izvadaka. Tvoj posao je da za 10 sekundi napraviš ono za što bi odvjetniku trebao sat vremena.

        ═══════════════════════════════════════════════════════
        🔴 1. LOVAC NA PLOMBE (NAJKRITIČNIJE!)
        ═══════════════════════════════════════════════════════
        Plomba (oznaka "Z-xxxx/yyyy") znači da je POSTUPAK U TIJEKU.
        - Skeniraj gornji desni kut, zaglavlje i cijeli tekst.
        - Traži: "Z-", "Plomba", aktivne zahtjeve za upis.
        - Ako nađeš plombu → CRITICAL finding:
          "UPOZORENJE: Aktivna plomba {Z-broj}! Netko je predao zahtjev prije vas. 
          Rizik: moguća nemogućnost upisa vlasništva."
        - Vrati sve plombe u "active_plombe" array.

        ═══════════════════════════════════════════════════════
        📋 2. LIST A — POSJEDOVNICA ("Legalnost objekta")
        ═══════════════════════════════════════════════════════
        Izvuci:
        - Katastarsku općinu (k.o.), Broj ZK uloška (ZK.ul.)
        - Sve čestice s opisom kulture i površinom
        - Etažni udio ako postoji (E-1, E-2, suvlasništvo X/Y)
        - Ukupnu površinu zemljišta, Površinu zgrade/stana

        PRAVILA ZA POVRŠINU:
        - "čestica X, oranica, 787 m²" → land_area_m2
        - "kuća i dvor" → stambeni objekt na parceli
        - "zgrada 120 m²" ili "stan 65 m²" → building_area_m2
        - Ako je samo površina čestice bez zgrade → building_area_m2 = 0
        - Pretvori: 1 čhv = 3.6 m², 1 ha = 10000 m², 1 jutro = 5754.64 m²

        🔍 DETEKTIRAJ NESLAGANJE KULTURE:
        - Ako piše "Pašnjak", "Oranica", "Livada" ali kupuje se kuća/stan → 
          FINDING: "Nekretnina nije upisana kao stambeni objekt! Mogući problemi s 
          uporabnom dozvolom ili legalizacijom."
        - Traži zabilježbu "Nije priložena uporabna dozvola" → 
          FINDING: "Banke često odbijaju kredit bez uporabne dozvole."
          Postavi has_usage_permit_issue = true.
        - Traži "legalizacija", "ozakonjenje" → upozori.

        ═══════════════════════════════════════════════════════
        👥 3. LIST B — VLASTOVNICA ("Matematika udjela")
        ═══════════════════════════════════════════════════════
        Izvuci:
        - Svakog vlasnika: ime, udio, OIB, temelj stjecanja
        - Ignoriraj predbilježbe koje su brisane

        🔢 MATEMATIČKA PROVJERA:
        - Zbroji SVE razlomke vlasničkih udjela.
        - Npr. 1/4 + 1/4 + 2/4 = 4/4 ✓ (OK)
        - Ako zbroj NIJE 1/1 → CRITICAL finding:
          "Zbroj vlasničkih udjela ({zbroj}) ≠ 1/1. 
          Moguća greška ili neriješeno vlasništvo."
        - Prikaži izračun u shares_sum_display.

        🔍 PROVJERA VLASNIŠTVA:
        - Ako je samo 1 vlasnik s 1/1 → ownership_status = "1/1 ČISTO"
        - Ako ima više vlasnika → ownership_status = "SUVLASNIŠTVO" + 
          FINDING: "Svi suvlasnici moraju potpisati ugovor ili dati punomoć."
        - Ako vlasništvo nije jasno → ownership_status = "NERAZJAŠNJENO"

        ═══════════════════════════════════════════════════════
        💰 4. LIST C — TERETOVNICA ("Detektor dugova")
        ═══════════════════════════════════════════════════════
        
        🏦 HIPOTEKE:
        - Izdvoji: iznos, valutu, banku/korisnika, godinu.
        - Provjeri piše li ispod "Brisano", "uknjižba brisana" → IGNORIRAJ.
        - Ako je AKTIVNA hipoteka → WARNING finding:
          "Aktivna hipoteka {iznos} {valuta} u korist {banka}. 
          Savjet: Potrebno ishoditi Pismo namjere banke prije isplate kapare."

        🏠 OSOBNE SLUŽNOSTI (DEAL-BREAKER!):
        - Traži: "Pravo doživotnog uživanja", "Pravo stanovanja", 
          "Osobna služnost", "Pravo plodouživanja"
        - Ako nađeš AKTIVNU osobnu služnost → CRITICAL finding:
          "Osobna služnost ({tip}) u korist {osoba}. OVO JE DEAL-BREAKER: 
          Ako kupite nekretninu s pravom doživotnog uživanja, ne možete 
          iseliti nositelja prava. Ovo pravo traje do smrti nositelja."

        ⚖️ OVRHE I SPOROVI:
        - Traži: "Ovrha", "Zabilježba spora", "Zabilježba tužbe", 
          "Zahtjev za povrat", "Sudski spor"
        - Aktivna ovrha/spor → CRITICAL finding.

        🚫 ZABRANE:
        - "Zabrana otuđenja i opterećenja" → WARNING/CRITICAL.

        📌 OSTALI TERETI:
        - Stvarne služnosti (prolaz, provod vode) → INFO finding (obično bezopasne).
        - Pravo građenja → WARNING.
        - Koncesija → WARNING.

        ═══════════════════════════════════════════════════════
        📊 PROCJENA UKUPNOG STATUSA
        ═══════════════════════════════════════════════════════
        
        overall_status:
        - "ČISTO" = nema plombi, nema ovrha, nema osobnih služnosti, 
          vlasništvo jasno, nema neslaganja kulture
        - "UVJETNO" = aktivne hipoteke (ali rješive pismom namjere), 
          ili suvlasništvo, ili manje primjedbe
        - "RIZIČNO" = aktivni sporovi, ovrhe, plombe, osobne služnosti, 
          nerazjašnjeno vlasništvo, ili kombinacija više problema

        risk_score: 
        - "LOW" = ČISTO
        - "MEDIUM" = UVJETNO 
        - "HIGH" = RIZIČNO

        ═══════════════════════════════════════════════════════
        🏘️ TIP NEKRETNINE
        ═══════════════════════════════════════════════════════
        Detektiraj:
        - "stan" = stan, apartman, etažni udio u stambenoj zgradi
        - "kuća" = kuća i dvor, obiteljska kuća, stambena zgrada
        - "zemljište" = samo zemljište (oranica, livada, građevinsko)
        - "poslovni_prostor" = ured, lokal, poslovni objekt

        ═══════════════════════════════════════════════════════
        📍 LOKACIJA
        ═══════════════════════════════════════════════════════
        - location: "Ime naselja, Općina/Grad" — uključi k.o. ime i općinu
        - address: ulica i kbr. ako postoji, inače prazan string

        ═══════════════════════════════════════════════════════
        📝 DUE DILIGENCE SUMMARY
        ═══════════════════════════════════════════════════════
        Napiši profesionalni sažetak u 3-5 rečenica. Ton: formalan, kratak, 
        jasan. Kao da si sudski vještak koji piše pravno mišljenje.
        
        Primjer tona:
        "Na predmetnoj nekretnini upisano je založno pravo (hipoteka) u korist 
        PBZ d.d. u iznosu od 100.000 EUR. Preporučam ishoditi brisovnu 
        dozvolu ili pismo namjere banke prije sklapanja predugovora. 
        Vlasništvo je čisto (1/1), bez aktivnih sporova."

        ═══════════════════════════════════════════════════════
        OUTPUT FORMAT — Vrati ISKLJUČIVO čisti JSON (bez markdown):
        ═══════════════════════════════════════════════════════
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
          "active_plombe": [
            {"z_number": "Z-1234/2024", "description": "Zahtjev za upis prava vlasništva", "date": "15.01.2024."}
          ],
          "risk_score": "LOW|MEDIUM|HIGH",
          "overall_status": "ČISTO|UVJETNO|RIZIČNO",
          "ownership_status": "1/1 ČISTO|SUVLASNIŠTVO|NERAZJAŠNJENO",
          "shares_sum_correct": true,
          "shares_sum_display": "1/2 + 1/2 = 2/2 ✓",
          "land_use_mismatch": "" ili "Upozorenje: kultura 'pašnjak' ne odgovara tipu nekretnine 'kuća'",
          "has_usage_permit_issue": false,
          "findings": [
            {
              "category": "PLOMBA|VLASNISTVO|HIPOTEKA|SLUZNOST|OVRHA|LEGALIZACIJA|OSTALO",
              "severity": "CRITICAL|WARNING|INFO",
              "title": "Kratki naziv",
              "description": "Detaljno objašnjenje rizika",
              "advice": "Konkretni pravni savjet"
            }
          ],
          "due_diligence_summary": "Profesionalni sažetak pravnog stanja...",
          "lawyer_comment": "Kratki sažetak za kompatibilnost...",
          "location": "Naselje, Općina/Grad",
          "address": "Ulica 5 ili prazan string",
          "condition": "Nije navedeno",
          "property_state": "good",
          "yard_area_m2": 0
        }
        """;

    public ZkParsingService(ChatClientProvider clients, PdfService pdfService, ILogger<ZkParsingService> logger)
    {
        _chatClient = clients.Fast;  // gpt-5-mini — ZK document extraction
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
