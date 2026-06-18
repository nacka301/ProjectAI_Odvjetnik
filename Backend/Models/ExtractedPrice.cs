using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

public class ExtractedPrice
{
    public double PricePerM2 { get; set; }
    public string SourceTitle { get; set; } = "";
    public string SourceUrl { get; set; } = "";

    // V5: Condition & yard data extracted from listing text
    public PropertyCondition DetectedCondition { get; set; } = PropertyCondition.Unknown;
    public double DetectedYardM2 { get; set; }

    // V7: Exact neighborhood match flag for weighting & sanity check
    public bool IsExactNeighborhoodMatch { get; set; }

    // V8.1: Feature booleans — detected from listing text
    public bool HasGarage { get; set; }
    public bool HasRenovation { get; set; }
    public bool HasYard { get; set; }

    // V12: Additional feature detection for Twin Comparison
    public bool HasParking { get; set; }    // parking space (not garage)
    public bool HasSeaView { get; set; }    // pogled na more
    public bool IsNorthFacing { get; set; } // north orientation detected
    public bool IsSouthFacing { get; set; } // south/west orientation detected
    public double DetectedSizeM2 { get; set; } // extracted main area
}
