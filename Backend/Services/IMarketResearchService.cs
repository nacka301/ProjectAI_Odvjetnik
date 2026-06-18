using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za MarketResearchService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IMarketResearchService
{
    Task<MarketResearchResult> SearchComparables(
        string location, double sizeM2, string pdfText = "",
        string microLocation = "", List<string>? keyFeatures = null,
        CancellationToken ct = default) ;
}
