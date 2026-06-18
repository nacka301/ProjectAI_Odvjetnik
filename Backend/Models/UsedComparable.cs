using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

public class UsedComparable
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public double PricePerM2 { get; set; }
    public double SimilarityScore { get; set; }
    public bool IsOutlier { get; set; }
    public string? OutlierReason { get; set; }

    // V10: BrightData-enriched fields for frontend display
    public double? TotalPrice { get; set; }
    public double? AreaM2 { get; set; }
    public int? Rooms { get; set; }
    public string? Floor { get; set; }
    public int? YearBuilt { get; set; }
    public string? Condition { get; set; }
    public string? EnergyClass { get; set; }
    public string? Heating { get; set; }
    public string? Location { get; set; }
    public bool? HasGarage { get; set; }
    public bool? HasParking { get; set; }

    // V18: Yard area + similarity reason
    public double? YardAreaM2 { get; set; }
    public string? SimilarityReason { get; set; }

    // V20: Deep dive status — what happened when we tried to scrape this listing
    public string? DeepDiveStatus { get; set; }

    // V20: Per-comparable correction % (usporedna metoda — individual adjustment)
    public double CorrectionPercent { get; set; }
    public string? CorrectionReason { get; set; }
}
