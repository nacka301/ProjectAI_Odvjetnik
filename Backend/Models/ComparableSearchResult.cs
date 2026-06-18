using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Models;

public class ComparableSearchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";

    // V8.2: AI-extracted features (populated by ValuationService batch call)
    public bool? HasGarage { get; set; }
    public bool? HasRenovation { get; set; }
    public bool? HasYard { get; set; }
    public string? AiCondition { get; set; }      // "new", "renovated", "good", "original", "needs_work"
    public double? AiYardAreaM2 { get; set; }      // AI-detected yard area
    public double? AiSizeM2 { get; set; }          // AI-detected property size
    public string? AiPropertyType { get; set; }    // AI-detected type
    public double? AiSimilarityScore { get; set; }  // AI-scored similarity to target (0.0–1.0)
    public string? AiSimilarityReason { get; set; }  // V18: AI-generated one-sentence Croatian reason

    // V24: Explicit listed prices — extracted from source, not regex on mixed content
    public double? ListedPriceEur { get; set; }       // Total price from ad (e.g. 386.000 €)
    public double? ListedPricePerM2 { get; set; }     // Price per m² from ad (e.g. 2.572 €/m²)

    // V20: Deep dive status tracking — what happened when we tried to scrape this listing
    public string? DeepDiveStatus { get; set; }

    // V9 Deep Scraping: enrichment booleans
    public bool? IsRenovated { get; set; }          // True if renovated/new (YearReno>2015 or description)
    public bool? NeedsRenovation { get; set; }      // True if "za adaptaciju", "izvorno stanje", etc.
    public int? YearLastRenovation { get; set; }     // Parsed from detail page attributes

    // V10: AI-extracted structured fields from BrightData
    public int? AiYearBuilt { get; set; }            // Godina izgradnje
    public string? AiEnergyClass { get; set; }       // Energetski razred (A-G)
    public string? AiHeating { get; set; }           // Grijanje type
    public string? AiFloor { get; set; }             // Kat / Etaža
    public int? AiRooms { get; set; }                // Broj soba

    // V29 Sherlock: Structured attributes extracted from description text via LLM
    public Models.ListingAttributes? SherlockAttributes { get; set; }
}
