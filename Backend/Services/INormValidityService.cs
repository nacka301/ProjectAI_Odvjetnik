using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za NormValidityService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface INormValidityService
{
/// <summary>Get all registered norms.</summary>
    IReadOnlyList<NormInfo> GetAllNorms() ;

/// <summary>Look up a norm by abbreviation (case-insensitive).</summary>
    NormInfo? GetNorm(string abbreviation) ;

/// <summary>Search norms by name fragment.</summary>
    IReadOnlyList<NormInfo> SearchNorms(string query) ;

/// <summary>
    /// Determine which version of a law was in force on a given date.
    /// Returns temporal result with applicable version and warning if outdated.
    /// </summary>
    TemporalNormResult? GetApplicableVersion(string abbreviation, DateOnly eventDate) ;

/// <summary>
    /// Format a citation in full court style (sudski način).
    /// Example: "čl. 322. st. 1. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
    /// </summary>
    CitedNorm FormatCitation(string abbreviation, string article,
        string? paragraph = null, string? point = null, DateOnly? eventDate = null) ;
}