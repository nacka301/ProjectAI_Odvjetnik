namespace Backend.Models.V6;

/// <summary>
/// Raw listing data scraped/fetched from a real-estate portal.
/// This is the intermediate representation between search results and
/// the canonicalized CanonicalPropertyModel produced by Phase 3.
/// </summary>
public class ListingData
{
    // ── Identity ──
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";
    public string Portal { get; set; } = "";            // njuskalo, index, crozilla, nekretnine.hr

    // ── Raw text fields (Phase 2 deep fetch) ──
    public string Title { get; set; } = "";
    public string RawDescription { get; set; } = "";    // full listing text
    public string RawHtmlFeatures { get; set; } = "";   // HTML features list
    public List<string> ImageUrls { get; set; } = [];

    // ── Parsed fields (Phase 1 / Phase 2) ──
    public string? PropertyType { get; set; }
    public double? SizeM2 { get; set; }
    public double? LandSizeM2 { get; set; }
    public double? PriceEur { get; set; }
    public double? PricePerM2 { get; set; }
    public string? Location { get; set; }
    public string? Address { get; set; }

    // ── Geo ──
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // ── Metadata ──
    public DateTime? ScrapedAt { get; set; }
    public string? Snippet { get; set; }                // Tavily / search engine snippet
    public double? SearchScore { get; set; }            // relevance score from search API

    // ── Status flags ──
    public bool DeepFetched { get; set; }               // Phase 2 completed?
    public bool NlpParsed { get; set; }                 // Phase 3 completed?
    public bool PassedPriceGate { get; set; }           // has a visible price?
}

/// <summary>
/// Structured features extracted by NLP/LLM from a listing description.
/// Output of Phase 3 text analysis.
/// </summary>
public class StructuredListingFeatures
{
    public string Condition { get; set; } = "unknown"; // ruins, old, good, renovated, new
    public List<string> Features { get; set; } = [];    // loggia, parking, garden, pool, etc.
    public string Sentiment { get; set; } = "standard"; // economy, standard, luxury
    public int? Floor { get; set; }
    public int? TotalFloors { get; set; }
    public int? Rooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? YearBuilt { get; set; }
    public bool? HasElevator { get; set; }
    public bool? HasParking { get; set; }
    public bool? HasGarage { get; set; }
    public bool? HasBalcony { get; set; }
    public bool? HasGarden { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasSeaView { get; set; }
    public bool? IsFirstRow { get; set; }
}
