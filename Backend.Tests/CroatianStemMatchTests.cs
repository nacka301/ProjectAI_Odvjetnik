using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// V82: Tests for CroatianStemMatch — fuzzy location matching that handles
/// Croatian grammatical cases (padeži). Prevents the critical bug where
/// AI similarity returns 0% for all comps because gpt-4o-mini can't handle
/// inflected city names ("Hvaru", "Puli", "Zagrebu").
/// </summary>
public class CroatianStemMatchTests
{
    // ══════════════════════════════════════════════════════════
    //  CONSONANT-ENDING CITIES: base + suffix (Hvar → Hvaru)
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("kamena kuća na otoku hvaru", "hvar", true)]
    [InlineData("kuća u rovinju s pogledom na more", "rovinj", true)]
    [InlineData("stan u splitu centar", "split", true)]
    [InlineData("apartman u zagrebu maksimir", "zagreb", true)]
    [InlineData("kuća u dubrovniku stari grad", "dubrovnik", true)]
    [InlineData("kuća u osijeku tvrda", "osijek", true)]
    [InlineData("kuća u zadru", "zadar", true)]      // nepostojano 'a': zadar→zadru
    [InlineData("kuća u sibeniku", "sibenik", true)]  // diacritics pre-stripped
    [InlineData("kuća u karlovcu", "karlovac", true)] // nepostojano 'a': karlovac→karlovcu
    public void ConsonantEnding_LocativeCase_Matches(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  VOWEL-ENDING CITIES: stem change (Pula → Puli, Rijeka → Rijeci)
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("prilika u puli atraktivna lokacija", "pula", true)]
    [InlineData("kuća u rijeci kantrida", "rijeka", true)]   // palatalization: rijek→rijec matches "rijeci"
    [InlineData("stan u slatini mirna ulica", "slatina", true)]
    [InlineData("kuća u koprivnici centar", "koprivnica", true)]  // koprivnic → koprivnici
    [InlineData("stan u vinkovcima", "vinkovci", true)]  // vinkovc → vinkovcima
    [InlineData("kuća u sisku uz kupu", "sisak", true)]  // nepostojano 'a': sisak→sisku
    public void VowelEnding_DativeLocativeCase_Matches(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  EXACT MATCHES (no inflection needed — should still work)
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("kuća hvar centar", "hvar", true)]
    [InlineData("stan pula verudela", "pula", true)]
    [InlineData("kuća split mertojak", "split", true)]
    public void ExactMatch_StillWorks(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  FALSE POSITIVES: should NOT match different cities
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("stan u ljubljani centar", "zagreb", false)]     // Different city entirely
    [InlineData("kuća u rijeci", "pula", false)]                  // Different city
    [InlineData("prelijepi stan na moru", "hvar", false)]        // No city mention
    [InlineData("kuća nova", "novska", false)]                     // Different word, shouldn't match
    public void DifferentCity_DoesNotMatch(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  EDGE CASES: short names, partial overlaps
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("kuća u bolu na braču", "bol", false)]  // 3 chars — below minimum, skip
    [InlineData("stan vis", "vis", false)]               // 3 chars — below minimum, skip
    [InlineData("", "hvar", false)]                      // Empty text
    [InlineData("kuća u hvaru", "", false)]              // Empty part
    public void EdgeCases_HandledCorrectly(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  NEIGHBORHOOD MATCHING with Croatian cases
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("stan na bacvicama", "bacvice", true)]          // bacvic → bacvicama (3-char suffix)
    [InlineData("stan na tresnjevci", "tresnjevka", true)]      // palatalization: tresnjevk → tresnjevc + i
    [InlineData("stan u maksimiru", "maksimir", true)]          // consonant + u
    [InlineData("stan na pescenici", "pescenica", true)]        // pescenic → pescenici
    public void Neighborhoods_CroatianCases_Match(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }

    // ══════════════════════════════════════════════════════════
    //  WORD BOUNDARY: stem must start at word boundary
    // ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("prehvaru nema govora", "hvar", false)]  // "hvaru" is inside "prehvaru" — not a word boundary
    [InlineData("hvaru dolaze turisti", "hvar", true)]   // starts at beginning — word boundary
    [InlineData("na hvaru je lijepo", "hvar", true)]     // "na " before "hvaru" — word boundary
    public void WordBoundary_Respected(string text, string part, bool expected)
    {
        Assert.Equal(expected, ValuationEngine.CroatianStemMatch(text, part));
    }
}
