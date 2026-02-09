namespace Backend.Models.V6;

/// <summary>
/// Canonical representation of a real-estate property.
/// Created by Phase 0 (enrichment) and used throughout the V6 pipeline.
/// All numeric fields default to 0 / null so the pipeline degrades gracefully
/// when external APIs are unavailable.
/// </summary>
public class CanonicalPropertyModel
{
    // ── Identity ──────────────────────────────────────────────
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RawText { get; set; } = "";

    // ── Core attributes ──────────────────────────────────────
    public string PropertyType { get; set; } = "";           // stan, kuća, zemljište, poslovni prostor
    public PropertyCategory TypeCategory { get; set; } = PropertyCategory.Unknown;
    public double SizeM2 { get; set; }
    public double LandSizeM2 { get; set; }                   // for houses / land plots
    public string Location { get; set; } = "";               // city / municipality name
    public string Address { get; set; } = "";                // street-level if known
    public string CadastralMunicipality { get; set; } = "";  // katastarska općina
    public string CadastralParcel { get; set; } = "";        // katastarska čestica

    // ── Condition ordinal (Ruins=0, Old=1, Good=2, Renovated=3, New=4) ──
    public int ConditionOrdinal { get; set; } = 2;           // default: Good
    public string ConditionRaw { get; set; } = "";

    // ── Features (amenity tag set for Jaccard) ──────────────
    public HashSet<string> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Geo-spatial enrichment (Phase 0) ─────────────────────
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceToCoastKm { get; set; }
    public double? DistanceToCityCenterKm { get; set; }
    public double? DistanceToNearestSchoolKm { get; set; }

    // ── Demographics enrichment ──────────────────────────────
    public string? NeighborhoodIncomeTier { get; set; }       // low / medium / high / premium

    // ── Cadastre extras ──────────────────────────────────────
    public string? EnergyRating { get; set; }                 // A+, A, B, C, D, E, F, G
    public string? LegalStatus { get; set; }                  // čisto, teret, spor

    // ── Price (only set on comparables, not on subject) ──────
    public double? PriceEur { get; set; }
    public double? PricePerM2 { get; set; }

    // ── Embedding (Phase 3) ──────────────────────────────────
    public float[]? DescriptionEmbedding { get; set; }
    public string? StructuredDescription { get; set; }        // JSON from NLP parse
    public string? Sentiment { get; set; }                    // economy / standard / luxury

    // ── Source ────────────────────────────────────────────────
    public string? SourceUrl { get; set; }
    public string? SourcePortal { get; set; }
    public string? ListingTitle { get; set; }

    // ── Similarity (Phase 4, set per-candidate) ──────────────
    public double SimilarityScore { get; set; }
    public SimilarityVector? SimilarityDetail { get; set; }

    // ── Filter flags ─────────────────────────────────────────
    public bool PassedIronGate { get; set; } = true;         // type-match filter
    public bool PassedJunkFilter { get; set; } = true;       // anti-article filter

    // ── Cluster (Phase 6) ────────────────────────────────────
    public int ClusterId { get; set; } = -1;                 // -1 = unassigned / noise
}

/// <summary>
/// Normalized property type category for Iron Gate matching.
/// Prevents cross-type contamination (e.g., land scored against houses).
/// </summary>
public enum PropertyCategory
{
    Unknown = 0,
    Apartment = 1,   // stan, apartman, garsonijera
    House = 2,       // kuća, vila, vikendica, obiteljska kuća, dvojna kuća
    Land = 3,        // zemljište, parcela, oranica, teren, građevinsko zemljište
    Commercial = 4,  // poslovni prostor, ured, lokal, trgovina, skladište
}

/// <summary>
/// Multi-dimensional similarity vector.
/// Final score = w1·Loc + w2·Area + w3·Cond + w4·Feat + w5·Sem
/// </summary>
public class SimilarityVector
{
    public double LocationScore { get; set; }   // S_loc — Haversine + exponential decay
    public double AreaScore { get; set; }        // S_area — Gaussian RBF
    public double ConditionScore { get; set; }   // S_cond — ordinal distance
    public double FeaturesScore { get; set; }    // S_feat — Jaccard index
    public double SemanticScore { get; set; }    // S_sem — cosine similarity of embeddings
    public double WeightedTotal { get; set; }    // final composite score
    public bool TypeMatch { get; set; } = true;  // Iron Gate: false → total forced to 0
    public bool IsJunk { get; set; }              // Junk Detection: true → total forced to 0
}
