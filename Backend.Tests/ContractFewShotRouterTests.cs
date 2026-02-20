using Backend.Services;
using static Backend.Services.ContractFewShotRouter;

namespace Backend.Tests;

/// <summary>
/// V75: Tests for ContractFewShotRouter — Dynamic Few-Shot Routing.
///
/// Verifies:
///   - Contract type detection from Croatian legal text
///   - Correct few-shot example routing per type
///   - Correct ideal template routing per type
///   - Edge cases (empty text, mixed types, ambiguous)
///   - No regression on existing kupoprodajni detection
/// </summary>
public class ContractFewShotRouterTests
{
    // ══════════════════════════════════════════════════════════
    //  KUPOPRODAJNI — sale/purchase detection
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_Kupoprodajni_BasicKeywords()
    {
        var text = """
            UGOVOR O KUPOPRODAJI NEKRETNINE

            Članak 1.
            Prodavatelj prodaje, a kupac kupuje stan u Zagrebu,
            upisan u ZK uložak 1234, k.o. Centar.

            Članak 3.
            Kupoprodajna cijena iznosi 200.000,00 EUR.
            Kupac uplaćuje kaparu u iznosu od 20.000,00 EUR.

            Članak 5.
            Prodavatelj daje bezuvjetnu clausulu intabulandi.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Kupoprodajni, type);
    }

    [Fact]
    public void Detect_Kupoprodajni_WithOdustatnina()
    {
        var text = """
            Kupoprodajni ugovor za nekretninu u Splitu.
            Prodavatelj i kupac ugovaraju odustatninu.
            Prijenos vlasništva nakon isplate kupoprodajne cijene.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Kupoprodajni, type);
    }

    // ══════════════════════════════════════════════════════════
    //  NAJAM — lease/rent detection
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_Najam_BasicKeywords()
    {
        var text = """
            UGOVOR O NAJMU STANA

            Članak 1.
            Najmodavac daje, a najmoprimac prima u najam stan
            na adresi Ilica 100, Zagreb.

            Članak 3.
            Mjesečna najamnina iznosi 500,00 EUR.
            Najamnina se plaća do 5. u mjesecu.

            Članak 5.
            Otkazni rok iznosi 60 dana.
            Trajanje najma: 12 mjeseci od dana potpisa.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Najam, type);
    }

    [Fact]
    public void Detect_Najam_Zakup()
    {
        var text = """
            Ugovor o zakupu poslovnog prostora.
            Zakupodavac daje u zakup poslovni prostor.
            Zakupnik se obvezuje plaćati zakupninu.
            Trajanje zakupa: 5 godina.
            Zakupnina iznosi 1.200,00 EUR mjesečno.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Najam, type);
    }

    [Fact]
    public void Detect_Najam_Podnajam()
    {
        var text = """
            Ugovor o najmu stana u Zagrebu.
            Najmoprimac se obvezuje plaćati najamninu.
            Podnajam stana nije dozvoljen bez suglasnosti najmodavca.
            Otkazni rok za otkaz najma iznosi 30 dana.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Najam, type);
    }

    // ══════════════════════════════════════════════════════════
    //  GRAĐENJE — construction detection
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_Gradnja_BasicKeywords()
    {
        var text = """
            UGOVOR O GRAĐENJU

            Članak 1.
            Naručitelj radova naručuje, a izvođač radova se obvezuje
            izvesti građevinske radove na izgradnji obiteljske kuće.

            Članak 3.
            Rok završetka radova: 12 mjeseci od dana uvođenja u posao.
            Građevinska dozvola pribavljena pod brojem KLASA: UP/I-361.

            Članak 5.
            Cijena radova utvrđuje se na temelju troškovnika.
            Privremene situacije ispostavljaju se mjesečno.
            Okončana situacija nakon primopredaje radova.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Gradnja, type);
    }

    [Fact]
    public void Detect_Gradnja_IzvodjenjeRadova()
    {
        var text = """
            Ugovor o izgradnji stambenog objekta.
            Izvođač radova se obvezuje izvesti sve radove prema
            glavnom projektu i izvedbenom projektu.
            Uporabna dozvola pribavit će se nakon završetka gradnje.
            Primopredaja radova izvršit će se nakon tehničkog pregleda.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Gradnja, type);
    }

    // ══════════════════════════════════════════════════════════
    //  DOŽIVOTNO UZDRŽAVANJE — lifetime maintenance detection
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_DozivotnoUzdrzavanje_BasicKeywords()
    {
        var text = """
            UGOVOR O DOŽIVOTNOM UZDRŽAVANJU

            Članak 1.
            Primatelj uzdržavanja i davatelj uzdržavanja sklapaju
            ovaj ugovor o doživotnom uzdržavanju.

            Članak 3.
            Davatelj uzdržavanja se obvezuje primatelju uzdržavanja
            osigurati stanovanje, prehranu, njegu i liječenje do smrti.

            Članak 5.
            Nakon smrti primatelja, nekretnina prelazi u vlasništvo davatelja.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.DozivotnoUzdrzavanje, type);
    }

    [Fact]
    public void Detect_DozivotnoUzdrzavanje_ObvezaUzdrzavanja()
    {
        var text = """
            Ugovor o doživotnom uzdržavanju sklopljen pred sudom.
            Obveza uzdržavanja traje do smrti primatelja uzdržavanja.
            Doživotna renta u vrijednosti od 500 EUR mjesečno.
            Primatelj uzdržavanja zadržava pravo stanovanja.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.DozivotnoUzdrzavanje, type);
    }

    // ══════════════════════════════════════════════════════════
    //  OSTALO — fallback for unrecognized types
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_Ostalo_EmptyText()
    {
        Assert.Equal(ContractType.Ostalo, DetectContractType(""));
        Assert.Equal(ContractType.Ostalo, DetectContractType(null!));
        Assert.Equal(ContractType.Ostalo, DetectContractType("   "));
    }

    [Fact]
    public void Detect_Ostalo_UnknownContract()
    {
        var text = "Ovo je nekakav dokument koji ne sadrži prepoznatljive pravne pojmove za ugovor.";
        var type = DetectContractType(text);
        Assert.Equal(ContractType.Ostalo, type);
    }

    // ══════════════════════════════════════════════════════════
    //  MIXED TEXT — correct type wins by keyword density
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_NajamWins_WhenDominantKeywords()
    {
        // Has both najam and kupoprodajni hints, but najam dominates
        var text = """
            UGOVOR O NAJMU STANA

            Najmodavac daje u najam stan. Najmoprimac plaća najamninu.
            Mjesečna najamnina iznosi 600 EUR. Trajanje najma 24 mjeseca.
            Otkazni rok za otkaz najma: 60 dana. Zakupnina uključuje režije.
            Napomena: prethodni kupoprodajni ugovor bio je sklopljen 2020.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Najam, type);
    }

    [Fact]
    public void Detect_DozivotnoWins_OverKupoprodajni()
    {
        // Has some kupoprodajni keywords but doživotno should dominate
        var text = """
            UGOVOR O DOŽIVOTNOM UZDRŽAVANJU

            Primatelj uzdržavanja prenosi nekretninu na davatelja uzdržavanja.
            Obveza uzdržavanja uključuje stanovanje i prehranu do smrti primatelja.
            Davatelj uzdržavanja se obvezuje na doživotno uzdržavanje.
            Prodavatelj i kupac su ranije bili u pregovorima o kupoprodaji,
            ali su odlučili sklopiti ugovor o doživotnom uzdržavanju.
            Smrt primatelja uzrokuje prijenos vlasništva.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.DozivotnoUzdrzavanje, type);
    }

    // ══════════════════════════════════════════════════════════
    //  FEW-SHOT ROUTING — correct examples per type
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ContractType.Kupoprodajni, "KUPOPRODAJNI")]
    [InlineData(ContractType.Najam, "NAJMU")]
    [InlineData(ContractType.Gradnja, "GRAĐENJU")]
    [InlineData(ContractType.DozivotnoUzdrzavanje, "DOŽIVOTNOM")]
    public void FewShot_ContainsTypeKeyword(ContractType type, string expectedKeyword)
    {
        var fewShot = GetFewShotExamples(type);
        Assert.Contains(expectedKeyword, fewShot, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ContractType.Kupoprodajni)]
    [InlineData(ContractType.Najam)]
    [InlineData(ContractType.Gradnja)]
    [InlineData(ContractType.DozivotnoUzdrzavanje)]
    [InlineData(ContractType.Ostalo)]
    public void FewShot_AlwaysNonEmpty(ContractType type)
    {
        var fewShot = GetFewShotExamples(type);
        Assert.False(string.IsNullOrWhiteSpace(fewShot));
        Assert.True(fewShot.Length > 100, $"Few-shot too short: {fewShot.Length}");
    }

    [Theory]
    [InlineData(ContractType.Kupoprodajni)]
    [InlineData(ContractType.Najam)]
    [InlineData(ContractType.Gradnja)]
    [InlineData(ContractType.DozivotnoUzdrzavanje)]
    [InlineData(ContractType.Ostalo)]
    public void FewShot_ContainsCorrectAndIncorrect(ContractType type)
    {
        var fewShot = GetFewShotExamples(type);
        Assert.Contains("ISPRAVAN", fewShot);
        Assert.Contains("NEISPRAVAN", fewShot);
        Assert.Contains("❌", fewShot);
    }

    [Fact]
    public void FewShot_Ostalo_FallsBackToKupoprodajni()
    {
        var ostalo = GetFewShotExamples(ContractType.Ostalo);
        var kupoprodajni = GetFewShotExamples(ContractType.Kupoprodajni);
        Assert.Equal(kupoprodajni, ostalo);
    }

    // ══════════════════════════════════════════════════════════
    //  IDEAL TEMPLATE ROUTING — correct template per type
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ContractType.Kupoprodajni, "KUPOPRODAJNI")]
    [InlineData(ContractType.Najam, "NAJMU")]
    [InlineData(ContractType.Gradnja, "GRAĐENJU")]
    [InlineData(ContractType.DozivotnoUzdrzavanje, "DOŽIVOTNOM")]
    public void IdealTemplate_ContainsTypeKeyword(ContractType type, string expectedKeyword)
    {
        var template = GetIdealContractTemplate(type);
        Assert.Contains(expectedKeyword, template, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ContractType.Kupoprodajni)]
    [InlineData(ContractType.Najam)]
    [InlineData(ContractType.Gradnja)]
    [InlineData(ContractType.DozivotnoUzdrzavanje)]
    [InlineData(ContractType.Ostalo)]
    public void IdealTemplate_AlwaysNonEmpty(ContractType type)
    {
        var template = GetIdealContractTemplate(type);
        Assert.False(string.IsNullOrWhiteSpace(template));
        Assert.True(template.Length > 200, $"Template too short: {template.Length}");
    }

    [Theory]
    [InlineData(ContractType.Kupoprodajni)]
    [InlineData(ContractType.Najam)]
    [InlineData(ContractType.Gradnja)]
    [InlineData(ContractType.DozivotnoUzdrzavanje)]
    public void IdealTemplate_ContainsNumberedSections(ContractType type)
    {
        var template = GetIdealContractTemplate(type);
        // All templates should have structured numbered sections
        Assert.Contains("1.", template);
        Assert.Contains("2.", template);
        Assert.Contains("ZAVRŠNE ODREDBE", template);
    }

    [Fact]
    public void IdealTemplate_Ostalo_FallsBackToKupoprodajni()
    {
        var ostalo = GetIdealContractTemplate(ContractType.Ostalo);
        var kupoprodajni = GetIdealContractTemplate(ContractType.Kupoprodajni);
        Assert.Equal(kupoprodajni, ostalo);
    }

    // ══════════════════════════════════════════════════════════
    //  TYPE NAMES — Croatian display names
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData(ContractType.Kupoprodajni, "Kupoprodajni ugovor")]
    [InlineData(ContractType.Najam, "Ugovor o najmu / zakupu")]
    [InlineData(ContractType.Gradnja, "Ugovor o građenju")]
    [InlineData(ContractType.DozivotnoUzdrzavanje, "Ugovor o doživotnom uzdržavanju")]
    [InlineData(ContractType.Ostalo, "Ostalo (generički ugovor)")]
    public void TypeName_ReturnsCorrectCroatianName(ContractType type, string expectedName)
    {
        Assert.Equal(expectedName, GetContractTypeName(type));
    }

    // ══════════════════════════════════════════════════════════
    //  TYPE-SPECIFIC ANTI-HALLUCINATION — unique ❌ per type
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void FewShot_Najam_NoKupoprodajniCriteria()
    {
        var fewShot = GetFewShotExamples(ContractType.Najam);
        // Should warn against using kupoprodajni criteria
        Assert.Contains("clausulu intabulandi", fewShot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kaparu", fewShot, StringComparison.OrdinalIgnoreCase);
        // These should appear as ❌ anti-patterns
    }

    [Fact]
    public void FewShot_Gradnja_NoKupoprodajniCriteria()
    {
        var fewShot = GetFewShotExamples(ContractType.Gradnja);
        Assert.Contains("clausulu intabulandi", fewShot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kaparu", fewShot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FewShot_DozivotnoUzdrzavanje_HasSpecificWarnings()
    {
        var fewShot = GetFewShotExamples(ContractType.DozivotnoUzdrzavanje);
        // Should mention sudska ovjera
        Assert.Contains("sudsk", fewShot, StringComparison.OrdinalIgnoreCase);
        // Should warn against treating vlasništvo transfer as a trap
        Assert.Contains("prijenos vlasništva", fewShot, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════
    //  IDEAL TEMPLATE — domain-specific sections exist
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void IdealTemplate_Najam_HasRentSpecificSections()
    {
        var template = GetIdealContractTemplate(ContractType.Najam);
        Assert.Contains("NAJAMNINA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JAMČEVINA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OTKAZNI ROK", template, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdealTemplate_Gradnja_HasConstructionSpecificSections()
    {
        var template = GetIdealContractTemplate(ContractType.Gradnja);
        Assert.Contains("OPSEG RADOVA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UGOVORNA KAZNA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JAMSTVO", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMOPREDAJA", template, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdealTemplate_DozivotnoUzdrzavanje_HasLifetimeSpecificSections()
    {
        var template = GetIdealContractTemplate(ContractType.DozivotnoUzdrzavanje);
        Assert.Contains("SUDSKA OVJERA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBVEZE DAVATELJA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NUŽNI NASLJEDNICI", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SMRT", template, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdealTemplate_Kupoprodajni_HasSaleSpecificSections()
    {
        var template = GetIdealContractTemplate(ContractType.Kupoprodajni);
        Assert.Contains("CLAUSULA INTABULANDI", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KAPARA", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TERETI", template, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════
    //  TEMPLATES ARE TYPE-DISTINCT — no two types share content
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void AllFewShots_AreDifferentForEachType()
    {
        var types = new[] { ContractType.Kupoprodajni, ContractType.Najam,
                            ContractType.Gradnja, ContractType.DozivotnoUzdrzavanje };

        var fewShots = types.Select(GetFewShotExamples).ToList();

        for (int i = 0; i < fewShots.Count; i++)
        for (int j = i + 1; j < fewShots.Count; j++)
        {
            Assert.NotEqual(fewShots[i], fewShots[j]);
        }
    }

    [Fact]
    public void AllIdealTemplates_AreDifferentForEachType()
    {
        var types = new[] { ContractType.Kupoprodajni, ContractType.Najam,
                            ContractType.Gradnja, ContractType.DozivotnoUzdrzavanje };

        var templates = types.Select(GetIdealContractTemplate).ToList();

        for (int i = 0; i < templates.Count; i++)
        for (int j = i + 1; j < templates.Count; j++)
        {
            Assert.NotEqual(templates[i], templates[j]);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  REALISTIC SAMPLE FROM TEST FILES
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void Detect_RealKupoprodajniSample()
    {
        // Simulated realistic Croatian kupoprodajni ugovor snippet
        var text = """
            UGOVOR O KUPOPRODAJI NEKRETNINE
            Sklopljen dana 15.01.2025. u Zagrebu

            Članak 1. UGOVORNE STRANE
            Prodavatelj: Ivan Horvat, OIB: 12345678901, Zagreb, Ilica 100
            Kupac: Ana Novak, OIB: 98765432109, Split, Marjanska 5

            Članak 2. PREDMET UGOVORA
            Prodavatelj prodaje kupcu stan u naravi trosoban stan, površine
            75,50 m², upisan u ZK uložak br. 1234, k.o. Centar, na adresi
            Jurišićeva 15, Zagreb.

            Članak 3. KUPOPRODAJNA CIJENA
            Kupoprodajna cijena iznosi 250.000,00 EUR
            (slovima: dvestopedesettisućaeura).

            Članak 4. NAČIN PLAĆANJA
            Kupac plaća kaparu u iznosu od 25.000,00 EUR u roku od 3 dana
            od potpisa ovog ugovora. Ostatak kupoprodajne cijene u iznosu
            od 225.000,00 EUR kupac će uplatiti u roku od 30 dana.

            Članak 5. TERETI
            Prodavatelj izjavljuje da nekretnina nije opterećena hipotekom
            niti drugim teretima.

            Članak 6. CLAUSULA INTABULANDI
            Prodavatelj daje bezuvjetnu i neopozivu suglasnost da se kupac
            može uknjižiti kao vlasnik nekretnine u zemljišnoj knjizi.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Kupoprodajni, type);
    }

    [Fact]
    public void Detect_RealNajamSample()
    {
        var text = """
            UGOVOR O NAJMU STANA
            Sklopljen dana 01.02.2025. u Zagrebu

            Članak 1. UGOVORNE STRANE
            Najmodavac: Marko Petrović, OIB: 11122233344, Zagreb
            Najmoprimac: Petra Jurić, OIB: 55566677788, Zagreb

            Članak 2. PREDMET NAJMA
            Najmodavac daje u najam stan na adresi Maksimirska 50, 3. kat,
            površine 55 m², namješten, za stanovanje.

            Članak 3. TRAJANJE NAJMA
            Ugovor se sklapa na određeno vrijeme od 12 (dvanaest) mjeseci,
            počevši od 01.03.2025. do 28.02.2026.

            Članak 4. NAJAMNINA
            Mjesečna najamnina iznosi 550,00 EUR.
            Najamnina se plaća unaprijed, najkasnije do 5. u mjesecu.
            U najamninu su uključeni troškovi pričuve.

            Članak 5. JAMČEVINA
            Najmoprimac uplaćuje jamčevinu u iznosu od 2 mjesečne najamnine
            (1.100,00 EUR). Jamčevina se vraća po isteku najma u roku od
            15 dana od primopredaje stana.

            Članak 6. OTKAZ NAJMA
            Svaka strana može otkazati najam uz otkazni rok od 60 dana.
            """;

        var type = DetectContractType(text);
        Assert.Equal(ContractType.Najam, type);
    }
}
