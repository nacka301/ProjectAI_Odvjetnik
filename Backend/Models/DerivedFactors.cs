using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Models;

/// <summary>
/// Factors derived from "twin comparison" in the dataset.
/// Each factor has a derived EUR or % value and a pair count (confidence).
/// </summary>
public class DerivedFactors
{
    public double GarageValueEur { get; set; }       // Flat EUR value of garage in this market
    public int GaragePairCount { get; set; }
    public double ParkingValueEur { get; set; }      // Flat EUR value of parking space
    public int ParkingPairCount { get; set; }
    public double RenovationPremiumPct { get; set; } // % premium renovated vs original
    public int RenovationPairCount { get; set; }
    public double SeaViewPremiumPct { get; set; }    // % premium for sea view
    public int SeaViewPairCount { get; set; }
    public double OrientationDeltaPct { get; set; }  // % delta south vs north
    public int OrientationPairCount { get; set; }
    public MarketContext Context { get; set; }
    public List<string> DerivationLog { get; set; } = [];
}
