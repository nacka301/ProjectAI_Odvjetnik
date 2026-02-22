using System.Diagnostics;
using Backend.Models;
using OpenAI.Chat;

namespace Backend.Services;

/// <summary>
/// Legal translation &amp; transliteration service.
/// Uses AI (via OpenRouter) with a court-expert-level system prompt
/// to produce legally precise translations of contracts, rulings, etc.
/// </summary>
public class TranslationService
{
    private readonly ChatClient _client;
    private readonly ILogger<TranslationService> _logger;

    // ══════════════════════════════════════════════════════════
    //  SYSTEM PROMPT — Ovlašteni sudski tumač
    // ══════════════════════════════════════════════════════════

    private const string SystemPrompt = """
        Ti si vrhunski, ovlašteni sudski tumač i stručnjak za pravnu lingvistiku.
        Tvoje specijalnosti: hrvatski, srpski, bosanski, engleski, njemački i ostali europski jezici.
        Tvoja specijalnost je besprijekoran prijevod kompleksnih pravnih dokumenata
        (ugovori, presude, rješenja, tužbe, punomoći, zapisnici) i apsolutno točno
        preslovljavanje (transliteracija) između ćirilice i latinice.

        STROGA PRAVILA koja MORAŠ poštovati:

        1. TRANSLITERACIJA (Ćirilica ↔ Latinica):
           Kada korisnik traži samo preslovljavanje, NE SMIJEŠ mijenjati pravni smisao,
           strukturu rečenice, niti dodavati svoje riječi.
           Tvoj jedini posao je savršena zamjena pisma uz poštovanje svih pravila
           lokalnog jezika (padeži, rodovi, pravni izrazi).
           Koristi standardnu srpsku/hrvatsku/bosansku latinicu sa dijakritičkim znakovima
           (č, ć, đ, š, ž).

        2. PRAVNI ŽARGON (Legalise):
           Kada prevodiš sa stranih jezika, koristi strogu pravnu terminologiju:
           - "Force Majeure" = "Viša sila"
           - "Breach of Contract" = "Povreda ugovornih obveza" / "Raskid ugovora zbog neispunjenja"
           - "Plaintiff" = "Tužitelj"
           - "Defendant" = "Tuženik"
           - "Whereas" = "Budući da" / "S obzirom na to da"
           - "Hereby" = "Ovime"
           - "Notwithstanding" = "Bez obzira na"
           - "Indemnification" = "Naknada štete" / "Obeštećenje"
           - "Arbitration" = "Arbitraža"
           - "Jurisdiction" = "Nadležnost"
           - "Lien" = "Založno pravo"
           - "Mortgage" = "Hipoteka"
           - "Easement" = "Služnost"
           - "Encumbrance" = "Teret"
           - "Due Diligence" = "Dubinska analiza" / "Provjera s dužnom pažnjom"
           - "Escrow" = "Fiducijarni račun" / "Escrow račun"
           - "Statute of limitations" = "Zastara" / "Zastarni rok"
           Nema kolokvijalnih izraza. Tekst mora zvučati kao da ga je napisao
           odvjetnik s 30 godina iskustva.

        3. FORMATIRANJE I STRUKTURA (Kritično):
           Zadrži APSOLUTNO svaki paragraf, broj članka, točku, alineju, potpis,
           razmak i tablicu TOČNO onako kako je u originalu.
           Ako original ima "Članak 1. (Predmet ugovora)", prijevod mora zadržati
           tu točnu strukturu. Ako original ima numeraciju (1., 2., a), b)), zadrži je.

        4. APSOLUTNA TIŠINA (No AI Chatter):
           Tvoj izlaz mora biti ISKLJUČIVO prevedeni/preslovljeni tekst.
           Strogo su ZABRANJENE fraze poput:
           "Evo prijevoda:", "Izvolite tekst na latinici:", "Nadam se da je ovo u redu.",
           "Napomena:", "Prijevod:", ili bilo kakva objašnjenja ili komentari.
           Samo isporuči goli, gotovi tekst — ništa drugo.

        5. KVALITETA:
           Svaki prijevod mora biti spreman za sudsku upotrebu bez ikakvih
           naknadnih izmjena. Ako naiđeš na termine koji nemaju direktan
           prijevod, koristi uvriježeni pravni termin u ciljnom jeziku
           i zadrži originalni termin u zagradi pri prvom pojavljivanju.
        """;

    public TranslationService(ChatClientProvider clients, ILogger<TranslationService> logger)
    {
        _client = clients.Heavy;  // Heavy model for precise legal translation
        _logger = logger;
    }

    /// <summary>
    /// Translates or transliterates the given text according to the specified mode.
    /// </summary>
    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        Func<string, string, string, string?, Task>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Build the user prompt based on mode
        string userPrompt = BuildUserPrompt(request.Mode, request.Text);
        string targetLanguageLabel = GetTargetLabel(request.Mode);

        _logger.LogInformation("Translation request: mode={Mode}, textLength={Len}, target={Target}",
            request.Mode, request.Text.Length, targetLanguageLabel);

        if (onProgress != null)
            await onProgress("translating", "active", GetProgressLabel(request.Mode), null);

        // Build messages
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.0f,     // Zero creativity — legal precision only
            MaxOutputTokenCount = 16384
        };

        // Call AI
        var completion = await _client.CompleteChatAsync(messages, options, ct);
        var translatedText = completion.Value.Content[0].Text?.Trim() ?? "";

        sw.Stop();

        _logger.LogInformation("Translation complete: mode={Mode}, inputChars={In}, outputChars={Out}, timeMs={Ms}",
            request.Mode, request.Text.Length, translatedText.Length, sw.ElapsedMilliseconds);

        if (onProgress != null)
            await onProgress("translating", "done", "Prijevod završen", $"{translatedText.Length} znakova");

        return new TranslationResult
        {
            TranslatedText = translatedText,
            DetectedLanguage = request.Mode == TranslationMode.AutoDetectToCroatian
                || request.Mode == TranslationMode.AutoDetectToEnglish
                ? "Automatiski detektirano" : GetSourceLabel(request.Mode),
            TargetLanguage = targetLanguageLabel,
            Mode = request.Mode,
            SourceCharCount = request.Text.Length,
            TranslatedCharCount = translatedText.Length,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
    }

    // ══════════════════════════════════════════════════════════
    //  USER PROMPT BUILDER — per mode
    // ══════════════════════════════════════════════════════════

    private static string BuildUserPrompt(TranslationMode mode, string text)
    {
        string task = mode switch
        {
            TranslationMode.CyrillicToLatin =>
                "ZADATAK: Preslovi ovaj tekst s ćirilice na latinicu. Ne mijenjaj jezik, ne mijenjaj smisao, samo zamijeni pismo. Zadrži svu interpunkciju, formatiranje i strukturu dokumenta.",

            TranslationMode.LatinToCyrillic =>
                "ZADATAK: Preslovi ovaj tekst s latinice na ćirilicu. Ne mijenjaj jezik, ne mijenjaj smisao, samo zamijeni pismo. Zadrži svu interpunkciju, formatiranje i strukturu dokumenta.",

            TranslationMode.EnglishToCroatian =>
                "ZADATAK: Prevedi ovaj dokument s engleskog na hrvatski jezik koristeći strogu pravnu terminologiju. Zadrži kompletnu strukturu i formatiranje originala.",

            TranslationMode.GermanToCroatian =>
                "ZADATAK: Prevedi ovaj dokument s njemačkog na hrvatski jezik koristeći strogu pravnu terminologiju. Zadrži kompletnu strukturu i formatiranje originala.",

            TranslationMode.CroatianToEnglish =>
                "ZADATAK: Prevedi ovaj dokument s hrvatskog na engleski jezik koristeći strogu pravnu terminologiju (legal English). Zadrži kompletnu strukturu i formatiranje originala.",

            TranslationMode.CroatianToGerman =>
                "ZADATAK: Prevedi ovaj dokument s hrvatskog na njemački jezik koristeći strogu pravnu terminologiju (Rechtssprache). Zadrži kompletnu strukturu i formatiranje originala.",

            TranslationMode.AutoDetectToCroatian =>
                "ZADATAK: Detektiraj jezik izvornog teksta i prevedi cijeli dokument na hrvatski jezik (latinica) koristeći strogu pravnu terminologiju. Zadrži kompletnu strukturu i formatiranje originala.",

            TranslationMode.AutoDetectToEnglish =>
                "ZADATAK: Detektiraj jezik izvornog teksta i prevedi cijeli dokument na engleski jezik koristeći strogu pravnu terminologiju (legal English). Zadrži kompletnu strukturu i formatiranje originala.",

            _ => "ZADATAK: Prevedi ovaj dokument na hrvatski jezik."
        };

        return $"{task}\n\nTEKST:\n{text}";
    }

    private static string GetTargetLabel(TranslationMode mode) => mode switch
    {
        TranslationMode.CyrillicToLatin => "Latinica",
        TranslationMode.LatinToCyrillic => "Ćirilica",
        TranslationMode.EnglishToCroatian => "Hrvatski",
        TranslationMode.GermanToCroatian => "Hrvatski",
        TranslationMode.CroatianToEnglish => "English",
        TranslationMode.CroatianToGerman => "Deutsch",
        TranslationMode.AutoDetectToCroatian => "Hrvatski",
        TranslationMode.AutoDetectToEnglish => "English",
        _ => "Hrvatski"
    };

    private static string GetSourceLabel(TranslationMode mode) => mode switch
    {
        TranslationMode.CyrillicToLatin => "Ćirilica",
        TranslationMode.LatinToCyrillic => "Latinica",
        TranslationMode.EnglishToCroatian => "English",
        TranslationMode.GermanToCroatian => "Deutsch",
        TranslationMode.CroatianToEnglish => "Hrvatski",
        TranslationMode.CroatianToGerman => "Hrvatski",
        _ => "Nepoznato"
    };

    private static string GetProgressLabel(TranslationMode mode) => mode switch
    {
        TranslationMode.CyrillicToLatin => "Preslovljavam ćirilicu → latinicu...",
        TranslationMode.LatinToCyrillic => "Preslovljavam latinicu → ćirilicu...",
        TranslationMode.EnglishToCroatian => "Prevodim s engleskog na hrvatski...",
        TranslationMode.GermanToCroatian => "Prevodim s njemačkog na hrvatski...",
        TranslationMode.CroatianToEnglish => "Prevodim s hrvatskog na engleski...",
        TranslationMode.CroatianToGerman => "Prevodim s hrvatskog na njemački...",
        TranslationMode.AutoDetectToCroatian => "Detektiram jezik i prevodim na hrvatski...",
        TranslationMode.AutoDetectToEnglish => "Detektiram jezik i prevodim na engleski...",
        _ => "Prevodim..."
    };
}
