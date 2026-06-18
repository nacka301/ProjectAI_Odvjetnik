using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

public class MarketResearchResult
{
    public List<ComparableSearchResult> ActiveListings { get; set; } = [];
    public List<ComparableSearchResult> RegionalData { get; set; } = [];
    public string RegionName { get; set; } = "";
    public string DetectedPropertyType { get; set; } = "";
    public double TargetSizeM2 { get; set; }
    public string Note { get; set; } = "";
    public ConfidenceScore Confidence { get; set; } = new();
    public string SearchPhaseUsed { get; set; } = "";
    public int TotalApiCalls { get; set; }

    // V13: City Integrity stats
    public int CityIntegrityRejections { get; set; }
    public List<string> RejectedCityNames { get; set; } = [];

    // V16: Search debug log — all queries/URLs fired
    public List<SearchLogEntry> SearchLog { get; set; } = [];

    public List<ComparableSearchResult> AllResults =>
        [.. ActiveListings, .. RegionalData];
}
