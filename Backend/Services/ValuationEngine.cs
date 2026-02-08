using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Deterministic valuation engine — does the MATH server-side.
/// No AI here: pure number-crunching with outlier detection, weighted averages,
/// the -15% realization gap, and contextual adjustments.
/// </summary>
public class ValuationEngine
{
    private readonly ILogger<ValuationEngine> _logger;

    // Regex to extract price-like numbers from ad snippets
    // Matches patterns like: 150.000 €, 150000€, 3.500 €/m², 2500 €/m2
    private static readonly Regex PriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PricePerM2Regex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR|eur)\s*/\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SizeRegex = new(
        @"(\d+(?:[,\.]\d+)?)\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ValuationEngine(ILogger<ValuationEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Run the full deterministic valuation pipeline.
    /// Returns server-computed numbers + confidence level.
    /// </summary>
    public DeterministicValuation Calculate(
        ExtractedPropertyInfo property,
        MarketResearchResult marketData)
    {
        var result = new DeterministicValuation
        {
            RegionName = marketData.RegionName,
            RegionalBenchmarkPerM2 = marketData.RegionalBenchmarkPerM2,
            DetectedPropertyType = marketData.DetectedPropertyType,
            TargetSizeM2 = property.SizeM2 > 0 ? property.SizeM2 : marketData.TargetSizeM2
        };

        // ── Step 1: Extract prices from active listings ──────────
        var extractedPrices = ExtractPricesFromListings(
            marketData.ActiveListings, property.SizeM2);

        _logger.LogInformation(
            "Extracted {Count} price points from {ListingCount} active listings",
            extractedPrices.Count, marketData.ActiveListings.Count);

        // ── Step 2: Outlier detection (±30% from benchmark) ──────
        var benchmark = marketData.RegionalBenchmarkPerM2;
        var lowerBound = benchmark * 0.70;
        var upperBound = benchmark * 1.30;

        var cleanPrices = new List<ExtractedPrice>();
        var outliers = new List<ExtractedPrice>();

        foreach (var p in extractedPrices)
        {
            if (p.PricePerM2 >= lowerBound && p.PricePerM2 <= upperBound)
            {
                cleanPrices.Add(p);
            }
            else
            {
                outliers.Add(p);
                _logger.LogInformation(
                    "Outlier detected: {Price} €/m² from '{Title}' (benchmark: {Benchmark}, range: {Low}-{High})",
                    p.PricePerM2, p.SourceTitle, benchmark, lowerBound, upperBound);
            }
        }

        result.TotalListingsFound = marketData.ActiveListings.Count;
        result.UsablePricePoints = cleanPrices.Count;
        result.OutliersRemoved = outliers.Count;

        // ── Step 3: Calculate weighted average ───────────────────
        double avgPricePerM2;
        string priceSource;

        if (cleanPrices.Count >= 2)
        {
            // Enough data from active listings
            avgPricePerM2 = cleanPrices.Average(p => p.PricePerM2);
            priceSource = $"Prosjek {cleanPrices.Count} aktivnih oglasa";
            result.ConfidenceLevel = cleanPrices.Count >= 3 ? "Visoka" : "Srednja";
        }
        else if (cleanPrices.Count == 1)
        {
            // Only 1 listing — blend with benchmark (60% listing, 40% benchmark)
            avgPricePerM2 = cleanPrices[0].PricePerM2 * 0.6 + benchmark * 0.4;
            priceSource = "1 aktivan oglas + regionalni prosjek (60/40)";
            result.ConfidenceLevel = "Srednja";
        }
        else
        {
            // No usable listings — use benchmark as anchor
            avgPricePerM2 = benchmark;
            priceSource = $"Regionalni benchmark za {marketData.RegionName}";
            result.ConfidenceLevel = "Niska (benchmark)";
        }

        result.MarketAverageAsking = Math.Round(avgPricePerM2, 0);
        result.PriceSource = priceSource;

        // ── Step 4: Apply contextual adjustments ─────────────────
        var adjustments = CalculateAdjustments(property, marketData);
        result.Adjustments = adjustments;

        double totalAdjustmentPercent = adjustments.Sum(a => a.Percent);
        double adjustedPrice = avgPricePerM2 * (1 + totalAdjustmentPercent / 100.0);

        result.AdjustedPricePerM2BeforeDiscount = Math.Round(adjustedPrice, 0);

        // ── Step 5: Apply -15% Realization Gap ───────────────────
        double realizationDiscount = -15.0;

        // Exception: high-demand Novogradnja in major cities → only -5%
        var majorCities = new[] { "Zagreb", "Split", "Dubrovnik" };
        bool isNovogradnja = property.Description.Contains("novogradnja",
            StringComparison.OrdinalIgnoreCase);
        bool isMajorCity = majorCities.Any(c =>
            marketData.RegionName.Contains(c, StringComparison.OrdinalIgnoreCase));

        if (isNovogradnja && isMajorCity)
            realizationDiscount = -5.0;

        result.RealizationDiscountPercent = realizationDiscount;

        double finalPricePerM2 = adjustedPrice * (1 + realizationDiscount / 100.0);
        result.CalculatedPricePerM2 = Math.Round(finalPricePerM2, 0);

        // ── Step 6: Final value ──────────────────────────────────
        double targetSize = result.TargetSizeM2 > 0 ? result.TargetSizeM2 : 1;
        double estimatedValue = finalPricePerM2 * targetSize;

        // Sanity: never return 0 or negative
        if (estimatedValue <= 0)
        {
            estimatedValue = benchmark * 0.85 * targetSize;
            result.ConfidenceLevel = "Niska (sanity fallback)";
        }

        result.EstimatedValueEur = (int)Math.Round(estimatedValue, 0);

        _logger.LogInformation(
            "Deterministic valuation: {Value} € ({PriceM2} €/m² × {Size} m²). " +
            "Confidence: {Confidence}. Source: {Source}",
            result.EstimatedValueEur, result.CalculatedPricePerM2,
            targetSize, result.ConfidenceLevel, priceSource);

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  PRICE EXTRACTION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Extract price per m² from listing snippets using regex + heuristics.
    /// </summary>
    private List<ExtractedPrice> ExtractPricesFromListings(
        List<ComparableSearchResult> listings, double targetSizeM2)
    {
        var results = new List<ExtractedPrice>();

        foreach (var listing in listings)
        {
            var content = listing.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;

            double pricePerM2 = 0;

            // Try to find explicit price/m² first
            var perM2Match = PricePerM2Regex.Match(content);
            if (perM2Match.Success)
            {
                pricePerM2 = ParseCroatianNumber(perM2Match.Groups[1].Value);
            }
            else
            {
                // Try to find total price and size, then calculate
                var priceMatch = PriceRegex.Match(content);
                var sizeMatch = SizeRegex.Match(content);

                if (priceMatch.Success)
                {
                    var totalPrice = ParseCroatianNumber(priceMatch.Groups[1].Value);

                    double size = 0;
                    if (sizeMatch.Success)
                        size = ParseCroatianNumber(sizeMatch.Groups[1].Value);

                    if (size <= 0 && targetSizeM2 > 0)
                        size = targetSizeM2; // Use target size as fallback

                    if (size > 0 && totalPrice > 100)
                    {
                        // Sanity: if totalPrice looks like a price/m² (< 20000), 
                        // it probably IS the price per m² already
                        if (totalPrice < 10000)
                        {
                            pricePerM2 = totalPrice;
                        }
                        else
                        {
                            pricePerM2 = totalPrice / size;
                        }
                    }
                }
            }

            // Sanity check: price/m² should be between 200 and 15000 for Croatia
            if (pricePerM2 >= 200 && pricePerM2 <= 15000)
            {
                results.Add(new ExtractedPrice
                {
                    PricePerM2 = Math.Round(pricePerM2, 0),
                    SourceTitle = listing.Title,
                    SourceUrl = listing.Url
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Parse a Croatian-formatted number: dot = thousands, comma = decimal.
    /// "1.088" → 1088, "150.000" → 150000, "2.500,50" → 2500.50
    /// </summary>
    private static double ParseCroatianNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        text = text.Trim();

        // Remove dots (thousands separator), replace comma with dot (decimal)
        var normalized = text.Replace(".", "").Replace(",", ".");

        return double.TryParse(normalized, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    // ══════════════════════════════════════════════════════════
    //  CONTEXTUAL ADJUSTMENTS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate all contextual adjustment factors.
    /// </summary>
    private static List<ValuationAdjustment> CalculateAdjustments(
        ExtractedPropertyInfo property, MarketResearchResult marketData)
    {
        var adjustments = new List<ValuationAdjustment>();
        var desc = (property.Description + " " + property.Condition + " " + property.RawText)
            .ToLowerInvariant();
        var location = property.Location.ToLowerInvariant();

        // 1. Small apartment premium
        if (property.SizeM2 > 0 && property.SizeM2 < 40 &&
            marketData.DetectedPropertyType == "stan")
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Mali stan (<40 m²)",
                Description = "Manji stanovi imaju višu cijenu po m² zbog veće potražnje",
                Percent = +5
            });
        }

        // 2. Large property discount
        if (property.SizeM2 > 100 && marketData.DetectedPropertyType == "stan")
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Veliki stan (>100 m²)",
                Description = "Veći stanovi imaju nižu cijenu po m² zbog manje potražnje",
                Percent = -5
            });
        }

        // 3. Sea proximity
        if (desc.Contains("prva linija") || desc.Contains("1. red do mora") ||
            desc.Contains("first row") || desc.Contains("uz more"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "1. red do mora",
                Description = "Nekretnina u prvom redu do mora — premium lokacija",
                Percent = +30
            });
        }
        else if (desc.Contains("pogled na more") || desc.Contains("blizu mora") ||
                 desc.Contains("blizina mora") || desc.Contains("sea view"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Blizina mora / pogled",
                Description = "Nekretnina blizu mora ili s pogledom na more",
                Percent = +15
            });
        }

        // 4. Novogradnja / Starogradnja
        if (desc.Contains("novogradnja") || desc.Contains("nova gradnja") ||
            desc.Contains("novoizgrađen"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Novogradnja",
                Description = "Novoizgrađena nekretnina — premium kvaliteta i energetski razred",
                Percent = +10
            });
        }
        else if (desc.Contains("starogradnja") || desc.Contains("stara gradnja") ||
                 desc.Contains("adaptacija potrebna") || desc.Contains("za renoviranje"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Starogradnja / potrebna adaptacija",
                Description = "Starija gradnja ili potrebna ulaganja — umanjenje vrijednosti",
                Percent = -10
            });
        }

        // 5. Island location
        var islands = new[]
        {
            "krk", "cres", "lošinj", "losinj", "rab", "pag", "ugljan", "pašman", "pasman",
            "murter", "vir", "brač", "brac", "hvar", "vis", "korčula", "korcula",
            "lastovo", "mljet", "šolta", "solta", "čiovo", "ciovo", "pelješac", "peljesac",
            "dugi otok", "kornat"
        };
        if (islands.Any(i => location.Contains(i)))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Otočna lokacija",
                Description = "Ograničena ponuda na otoku, sezonska potražnja",
                Percent = +5
            });
        }

        // 6. Infrastructure: no road access mentioned
        if (desc.Contains("makadamski") || desc.Contains("neasfaltirani") ||
            desc.Contains("zemljani put"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Loša infrastruktura",
                Description = "Neasfaltirani pristup — umanjenje vrijednosti",
                Percent = -5
            });
        }

        // 7. Legal risk
        if (desc.Contains("legalizacija") || desc.Contains("legalizirano"))
        {
            adjustments.Add(new ValuationAdjustment
            {
                Factor = "Legalizacija",
                Description = "Prethodno nelegalna gradnja — pravni rizik",
                Percent = -5
            });
        }

        return adjustments;
    }
}

// ══════════════════════════════════════════════════════════
//  ENGINE OUTPUT MODELS
// ══════════════════════════════════════════════════════════

/// <summary>
/// Deterministic valuation result — pure server-side math, no AI.
/// </summary>
public class DeterministicValuation
{
    // Context
    public string RegionName { get; set; } = "";
    public double RegionalBenchmarkPerM2 { get; set; }
    public string DetectedPropertyType { get; set; } = "";
    public double TargetSizeM2 { get; set; }

    // Data quality
    public int TotalListingsFound { get; set; }
    public int UsablePricePoints { get; set; }
    public int OutliersRemoved { get; set; }
    public string PriceSource { get; set; } = "";

    // Pipeline
    public double MarketAverageAsking { get; set; }
    public List<ValuationAdjustment> Adjustments { get; set; } = [];
    public double AdjustedPricePerM2BeforeDiscount { get; set; }
    public double RealizationDiscountPercent { get; set; }
    public double CalculatedPricePerM2 { get; set; }

    // Final
    public int EstimatedValueEur { get; set; }
    public string ConfidenceLevel { get; set; } = "";
}

/// <summary>
/// A single contextual adjustment applied during valuation.
/// </summary>
public class ValuationAdjustment
{
    public string Factor { get; set; } = "";
    public string Description { get; set; } = "";
    public double Percent { get; set; }
}

/// <summary>
/// A price point extracted from a listing snippet.
/// </summary>
public class ExtractedPrice
{
    public double PricePerM2 { get; set; }
    public string SourceTitle { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}
