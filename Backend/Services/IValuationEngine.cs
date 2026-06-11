using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za ValuationEngine. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IValuationEngine
{
    DeterministicValuation Calculate(
        ExtractedPropertyInfo property,
        MarketResearchResult marketData) ;
}