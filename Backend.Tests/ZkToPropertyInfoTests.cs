using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Testovi za ZkParsingService.ToPropertyInfo() — statička metoda koja 
/// pretvara ZkParsedResult u ExtractedPropertyInfo za pipeline valuacije.
/// 
/// ŠTO TESTIRAMO:
/// 1. ToPropertyInfo() za razne tipove nekretnina (stan, kuća, zemljište, poslovni_prostor)
/// 2. Pravilno mapiranje površina (building_area vs land_area)
/// 3. Generiranje ispravnog description stringa
/// 4. Edge cases: nepoznata površina zgrade, prazan etažni udio
/// </summary>
public class ZkToPropertyInfoTests
{
    private const string SamplePdfText = "Ovo je tekst izvučen iz ZK izvadka. Sadrži podatke o nekretnini u k.o. Trešnjevka.";

    // ══════════════════════════════════════════════════════════
    //  1. Tip: STAN — koristi building_area_m2
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_Stan_UsesBuildingArea()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "stan",
            PropertyDescription = "Trosoban stan u k.o. Trnje",
            ParcelNumber = "4521/3",
            LandAreaM2 = 1250,
            BuildingAreaM2 = 65,
            Location = "Trnje, Zagreb",
            PropertyState = "good",
            EtazniUdio = "E-5, 65/1000"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(65, info.SizeM2);  // building area, NOT land area
        Assert.Equal("Trnje, Zagreb", info.Location);
        Assert.Equal("good", info.PropertyState);
        Assert.Contains("Stan", info.Description);
        Assert.Contains("4521/3", info.Description);
        Assert.Contains("E-5", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_Stan_NoBuildingArea_SetsZeroAndWarnsInCondition()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "stan",
            PropertyDescription = "Stan bez poznate površine",
            LandAreaM2 = 800,
            BuildingAreaM2 = 0,
            Location = "Maksimir, Zagreb"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(0, info.SizeM2);  // Can't valuate without building area
        Assert.Contains("800 m²", info.Condition);  // Warns that only land area is known
        Assert.Contains("Površina zgrade nije navedena", info.Condition);
    }

    // ══════════════════════════════════════════════════════════
    //  2. Tip: KUĆA — koristi building_area_m2 ako postoji
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_Kuca_WithBuildingArea_UsesBuildingArea()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "kuća",
            PropertyDescription = "Kuća i dvor",
            LandAreaM2 = 500,
            BuildingAreaM2 = 120,
            Location = "Sesvete, Zagreb"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(120, info.SizeM2);
        Assert.Contains("Kuća", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_Kuca_NoBuildingArea_SetsZero()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "kuća",
            PropertyDescription = "Kuća i dvor u k.o. Dubrava",
            LandAreaM2 = 787,
            BuildingAreaM2 = 0,
            Location = "Dubrava, Zagreb"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(0, info.SizeM2);
        Assert.Contains("787 m²", info.Condition);
    }

    // ══════════════════════════════════════════════════════════
    //  3. Tip: ZEMLJIŠTE — koristi land_area_m2
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_Zemljiste_UsesLandArea()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "zemljište",
            PropertyDescription = "Građevinsko zemljište",
            LandAreaM2 = 2500,
            BuildingAreaM2 = 0,
            Location = "Samobor"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(2500, info.SizeM2);  // Land → uses land area
        Assert.Contains("Građevinsko zemljište", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_Zemljiste_EvenWithBuildingArea_UsesLandArea()
    {
        // Edge case: GPT sometimes sets building area even for land
        var zk = new ZkParsedResult
        {
            PropertyType = "zemljište",
            PropertyDescription = "Oranica",
            LandAreaM2 = 5000,
            BuildingAreaM2 = 50, // spurious
            Location = "Velika Gorica"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(5000, info.SizeM2);  // Should use land area for zemljište
    }

    // ══════════════════════════════════════════════════════════
    //  4. Tip: POSLOVNI PROSTOR
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_PoslovniProstor_UsesBuildingArea()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "poslovni_prostor",
            PropertyDescription = "Uredski prostor u prizemlju",
            LandAreaM2 = 3000,
            BuildingAreaM2 = 150,
            Location = "Centar, Zagreb"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(150, info.SizeM2);
        Assert.Contains("Poslovni prostor", info.Description);
    }

    // ══════════════════════════════════════════════════════════
    //  5. Description generiranje
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_Description_IncludesTypeAndParcel()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "stan",
            PropertyDescription = "Garsonijera u novogradnji",
            ParcelNumber = "1111/2",
            BuildingAreaM2 = 30,
            Location = "Novi Zagreb"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Contains("Stan", info.Description);
        Assert.Contains("Garsonijera", info.Description);
        Assert.Contains("k.č. 1111/2", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_Description_IncludesEtazniUdio_WhenPresent()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "stan",
            PropertyDescription = "Stan",
            EtazniUdio = "E-3, 55/800",
            BuildingAreaM2 = 55,
            Location = "Split"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Contains("E-3, 55/800", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_Description_OmitsEtazniUdio_WhenEmpty()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "kuća",
            PropertyDescription = "Kuća",
            EtazniUdio = "",
            LandAreaM2 = 300,
            BuildingAreaM2 = 100,
            Location = "Zadar"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.DoesNotContain("E-", info.Description);
    }

    // ══════════════════════════════════════════════════════════
    //  6. PropertyState i YardAreaM2
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_PreservesPropertyState()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "kuća",
            PropertyDescription = "Stara kuća",
            PropertyState = "needs_adaptation",
            LandAreaM2 = 200,
            BuildingAreaM2 = 80,
            Location = "Varaždin"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal("needs_adaptation", info.PropertyState);
    }

    [Fact]
    public void ToPropertyInfo_PreservesYardArea()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "kuća",
            PropertyDescription = "Kuća s okućnicom",
            LandAreaM2 = 800,
            BuildingAreaM2 = 120,
            YardAreaM2 = 680,
            Location = "Osijek"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(680, info.YardAreaM2);
    }

    // ══════════════════════════════════════════════════════════
    //  7. RawText truncation
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_ShortPdfText_NotTruncated()
    {
        var shortText = "Kratki tekst";
        var zk = new ZkParsedResult { PropertyType = "stan", BuildingAreaM2 = 50, Location = "Zagreb" };

        var info = ZkParsingService.ToPropertyInfo(zk, shortText);

        Assert.Equal("Kratki tekst", info.RawText);
        Assert.DoesNotContain("...", info.RawText);
    }

    [Fact]
    public void ToPropertyInfo_LongPdfText_TruncatedTo500CharsWithEllipsis()
    {
        var longText = new string('A', 1000);
        var zk = new ZkParsedResult { PropertyType = "stan", BuildingAreaM2 = 50, Location = "Zagreb" };

        var info = ZkParsingService.ToPropertyInfo(zk, longText);

        Assert.True(info.RawText.Length <= 503); // 500 + "..."
        Assert.EndsWith("...", info.RawText);
    }

    // ══════════════════════════════════════════════════════════
    //  8. Unknown property types
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_UnknownType_UsesNekretnina()
    {
        var zk = new ZkParsedResult
        {
            PropertyType = "garaža", // non-standard type
            PropertyDescription = "Garaža",
            BuildingAreaM2 = 15,
            Location = "Rijeka"
        };

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Contains("Nekretnina", info.Description);
    }

    // ══════════════════════════════════════════════════════════
    //  9. Integration: Clean Property → ToPropertyInfo
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ToPropertyInfo_FromCleanProperty_AllFieldsMapped()
    {
        var zk = ZkAnalysisModelsTests.CreateCleanProperty();
        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        Assert.Equal(55, info.SizeM2);
        Assert.Equal("Trešnjevka, Zagreb", info.Location);
        Assert.Contains("Stan", info.Description);
        Assert.Contains("E-3", info.Description);
    }

    [Fact]
    public void ToPropertyInfo_FromPropertyWithMortgage_StillMapsCorrectly()
    {
        var zk = ZkAnalysisModelsTests.CreatePropertyWithMortgage();
        zk.BuildingAreaM2 = 72;
        zk.Location = "Maksimir, Zagreb";

        var info = ZkParsingService.ToPropertyInfo(zk, SamplePdfText);

        // Mortgages don't affect property info for valuation
        Assert.Equal(72, info.SizeM2);
        Assert.Equal("Maksimir, Zagreb", info.Location);
    }
}
