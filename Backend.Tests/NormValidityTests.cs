using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// V42: Tests for the Norm Validity Engine — NormValidityService
/// Tests norm lookup, temporal validity, citation formatting, and registry completeness.
/// </summary>
public class NormValidityTests
{
    private readonly NormValidityService _service = new();

    // ══════════════════════════════════════════════════════════
    //  Registry Completeness
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void GetAllNorms_ReturnsAtLeast15Norms()
    {
        var norms = _service.GetAllNorms();
        Assert.True(norms.Count >= 15, $"Expected ≥15 norms, got {norms.Count}");
    }

    [Theory]
    [InlineData("ZOO")]
    [InlineData("ZZK")]
    [InlineData("ZV")]
    [InlineData("OZ")]
    [InlineData("ZPP")]
    [InlineData("OBZ")]
    [InlineData("ZGRADNJA")]
    [InlineData("ZODV")]
    [InlineData("GDPR")]
    public void GetNorm_KnownAbbreviation_ReturnsNorm(string abbreviation)
    {
        var norm = _service.GetNorm(abbreviation);
        Assert.NotNull(norm);
        Assert.Equal(abbreviation, norm.Abbreviation);
        Assert.False(string.IsNullOrEmpty(norm.FullName));
        Assert.False(string.IsNullOrEmpty(norm.NnReference));
        Assert.NotEmpty(norm.Amendments);
    }

    [Fact]
    public void GetNorm_CaseInsensitive()
    {
        var upper = _service.GetNorm("ZOO");
        var lower = _service.GetNorm("zoo");
        var mixed = _service.GetNorm("Zoo");

        Assert.NotNull(upper);
        Assert.NotNull(lower);
        Assert.NotNull(mixed);
        Assert.Equal(upper.Abbreviation, lower.Abbreviation);
        Assert.Equal(upper.Abbreviation, mixed.Abbreviation);
    }

    [Fact]
    public void GetNorm_UnknownAbbreviation_ReturnsNull()
    {
        var norm = _service.GetNorm("NEPOSTOJECI");
        Assert.Null(norm);
    }

    // ══════════════════════════════════════════════════════════
    //  Norm Data Integrity
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ZOO_HasCorrectAmendmentHistory()
    {
        var zoo = _service.GetNorm("ZOO");
        Assert.NotNull(zoo);
        Assert.Equal("Zakon o obveznim odnosima", zoo.FullName);
        Assert.Equal(5, zoo.Amendments.Count);
        Assert.Equal("35/05", zoo.Amendments[0].NnNumber);
        Assert.Equal("29/18", zoo.Amendments[^1].NnNumber);
        Assert.Contains("NN 35/05", zoo.NnReference);
        Assert.Contains("29/18", zoo.NnReference);
    }

    [Fact]
    public void ZZK_HasCorrectAmendmentCount()
    {
        var zzk = _service.GetNorm("ZZK");
        Assert.NotNull(zzk);
        Assert.Equal(9, zzk.Amendments.Count);
        Assert.Equal("91/96", zzk.Amendments[0].NnNumber);
        Assert.Equal("108/17", zzk.Amendments[^1].NnNumber);
    }

    [Fact]
    public void AllNorms_HaveNnReference()
    {
        foreach (var norm in _service.GetAllNorms())
        {
            Assert.False(string.IsNullOrEmpty(norm.NnReference),
                $"{norm.Abbreviation} missing NnReference");
            Assert.True(norm.NnReference.StartsWith("NN "),
                $"{norm.Abbreviation} NnReference doesn't start with 'NN '");
        }
    }

    [Fact]
    public void AllNorms_HaveOrderedAmendments()
    {
        foreach (var norm in _service.GetAllNorms())
        {
            for (int i = 1; i < norm.Amendments.Count; i++)
            {
                Assert.True(
                    norm.Amendments[i].PublicationDate >= norm.Amendments[i - 1].PublicationDate,
                    $"{norm.Abbreviation}: amendments not in chronological order at index {i}");
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Search
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void SearchNorms_ByFragment_FindsResults()
    {
        var results = _service.SearchNorms("obveznim");
        Assert.NotEmpty(results);
        Assert.Contains(results, n => n.Abbreviation == "ZOO");
    }

    [Fact]
    public void SearchNorms_EmptyQuery_ReturnsEmpty()
    {
        var results = _service.SearchNorms("xyznonexistent");
        Assert.Empty(results);
    }

    // ══════════════════════════════════════════════════════════
    //  Temporal Validity
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void GetApplicableVersion_CurrentDate_NotOutdated()
    {
        var result = _service.GetApplicableVersion("ZOO", new DateOnly(2025, 1, 1));
        Assert.NotNull(result);
        Assert.False(result.IsOutdated);
        Assert.Equal("29/18", result.ApplicableNnVersion);
        Assert.Equal("29/18", result.CurrentNnVersion);
        Assert.Empty(result.Warning);
    }

    [Fact]
    public void GetApplicableVersion_2010Date_ReturnsPre2011Version()
    {
        // In 2010, ZOO had only NN 35/05 and 41/08 (125/11 came in November 2011)
        var result = _service.GetApplicableVersion("ZOO", new DateOnly(2010, 6, 1));
        Assert.NotNull(result);
        Assert.True(result.IsOutdated);
        Assert.Equal("41/08", result.ApplicableNnVersion);
        Assert.Equal("29/18", result.CurrentNnVersion);
        Assert.Contains("UPOZORENJE", result.Warning);
        Assert.Contains("NN 41/08", result.Warning);
    }

    [Fact]
    public void GetApplicableVersion_2003Date_ReturnsOriginalZOO()
    {
        // In 2003, only ZOO NN 35/05 didn't exist yet — but ZOO-91 was in force
        // For the current ZOO (2005 version), 2003 predates it
        var result = _service.GetApplicableVersion("ZOO", new DateOnly(2003, 6, 15));
        Assert.NotNull(result);
        Assert.True(result.IsOutdated);
        Assert.Contains("zakon još nije bio na snazi", result.ApplicableNnVersion);
    }

    [Fact]
    public void GetApplicableVersion_UnknownNorm_ReturnsNull()
    {
        var result = _service.GetApplicableVersion("FAKE", new DateOnly(2024, 1, 1));
        Assert.Null(result);
    }

    [Fact]
    public void GetApplicableVersion_ZZK_2015_MissingLaterAmendments()
    {
        // In 2015, ZZK had amendments up to 60/13; 108/17 came later
        var result = _service.GetApplicableVersion("ZZK", new DateOnly(2015, 6, 1));
        Assert.NotNull(result);
        Assert.True(result.IsOutdated);
        Assert.Equal("60/13", result.ApplicableNnVersion);
        Assert.Equal("108/17", result.CurrentNnVersion);
    }

    // ══════════════════════════════════════════════════════════
    //  Citation Formatting
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void FormatCitation_FullCourtStyle()
    {
        var citation = _service.FormatCitation("ZOO", "čl. 322.", "st. 1.");
        Assert.NotNull(citation);
        Assert.Contains("čl. 322.", citation.FormattedCitation);
        Assert.Contains("st. 1.", citation.FormattedCitation);
        Assert.Contains("Zakona o obveznim odnosima", citation.FormattedCitation);
        Assert.Contains("NN", citation.FormattedCitation);
        Assert.Equal("VAŽEĆI", citation.Status);
    }

    [Fact]
    public void FormatCitation_ArticleOnly()
    {
        var citation = _service.FormatCitation("ZZK", "čl. 98.");
        Assert.Contains("čl. 98.", citation.FormattedCitation);
        Assert.Contains("Zakona o zemljišnim knjigama", citation.FormattedCitation);
    }

    [Fact]
    public void FormatCitation_UnknownNorm_ReturnsBasicCitation()
    {
        var citation = _service.FormatCitation("UNKNOWN", "čl. 1.");
        Assert.Equal("NEPOZNAT", citation.Status);
        Assert.Contains("čl. 1.", citation.FormattedCitation);
    }

    [Fact]
    public void FormatCitation_WithTemporalDate_ShowsOutdatedStatus()
    {
        var citation = _service.FormatCitation("ZOO", "čl. 322.", eventDate: new DateOnly(2007, 1, 1));
        // In 2007, only NN 35/05 existed — law has since been amended, so status is IZMIJENJEN
        Assert.Equal("IZMIJENJEN", citation.Status);
        Assert.Contains("NN 35/05", citation.NnReference);
        Assert.DoesNotContain("41/08", citation.NnReference);
    }

    // ══════════════════════════════════════════════════════════
    //  Prompt Instruction Block
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void GetPromptInstructionBlock_ContainsKeyElements()
    {
        var block = NormValidityService.GetPromptInstructionBlock();
        Assert.Contains("NORM VALIDITY ENGINE", block);
        Assert.Contains("Sudski način", block);
        Assert.Contains("cited_norms", block);
        Assert.Contains("ZOO", block);
        Assert.Contains("NN 35/05", block);
        Assert.Contains("VAŽEĆI", block);
    }

    [Fact]
    public void GetPromptInstructionBlock_WithTemporal_ContainsTemporalMode()
    {
        var block = NormValidityService.GetPromptInstructionBlock(includeTemporalMode: true);
        Assert.Contains("TEMPORALNI NAČIN", block);
        Assert.Contains("Temporal Law Mode", block);
        Assert.Contains("NAPOMENA: Primjenjuje se verzija zakona", block);
    }

    [Fact]
    public void GetPromptInstructionBlock_WithoutTemporal_NoTemporalSection()
    {
        var block = NormValidityService.GetPromptInstructionBlock(includeTemporalMode: false);
        Assert.DoesNotContain("TEMPORALNI NAČIN", block);
    }
}
