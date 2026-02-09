using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// V6 Multi-Dimensional Similarity Engine.
/// 
/// Calculates a SimilarityVector for each candidate against the subject:
///   Score = w₁·S_loc + w₂·S_area + w₃·S_cond + w₄·S_feat + w₅·S_sem
///
/// Each dimension uses a mathematically appropriate similarity function:
///   • Location: Haversine + Exponential Decay
///   • Area: Gaussian RBF with auto-scaled γ
///   • Condition: Ordinal distance (5-point scale)
///   • Features: Jaccard Index (amenity overlap)
///   • Semantic: Cosine Similarity on text embeddings
/// </summary>
public class SimilarityEngine : ISimilarityEngine
{
    private readonly ILogger<SimilarityEngine> _logger;

    // Fallback location similarity when coordinates are unavailable
    // Based on string matching of location names
    private static readonly Dictionary<string, (double Lat, double Lon)> CroatianCityCoords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Major cities
        { "Zagreb", (45.8150, 15.9819) },
        { "Split", (43.5081, 16.4402) },
        { "Rijeka", (45.3271, 14.4422) },
        { "Osijek", (45.5511, 18.6939) },
        { "Zadar", (44.1194, 15.2314) },
        { "Dubrovnik", (42.6507, 18.0944) },
        { "Pula", (44.8666, 13.8496) },
        { "Šibenik", (43.7350, 15.8952) },
        { "Varaždin", (46.3057, 16.3366) },
        { "Karlovac", (45.4929, 15.5553) },
        // Coastal
        { "Rovinj", (45.0811, 13.6387) },
        { "Poreč", (45.2269, 13.5947) },
        { "Umag", (45.4316, 13.5235) },
        { "Opatija", (45.3372, 14.3054) },
        { "Makarska", (43.2969, 17.0182) },
        { "Trogir", (43.5125, 16.2519) },
        { "Omiš", (43.4447, 16.6930) },
        { "Hvar", (43.1729, 16.4412) },
        { "Korčula", (42.9596, 17.1358) },
        { "Brač", (43.3109, 16.6544) },
        { "Vis", (43.0622, 16.1827) },
        { "Krk", (45.0268, 14.5753) },
        { "Rab", (44.7561, 14.7601) },
        // Towns
        { "Kaštela", (43.5519, 16.3800) },
        { "Solin", (43.5428, 16.4878) },
        { "Podstrana", (43.4833, 16.5333) },
        { "Velika Gorica", (45.7142, 16.0756) },
        { "Samobor", (45.8011, 15.7117) },
        { "Zaprešić", (45.8564, 15.8081) },
        { "Crikvenica", (45.1782, 14.6921) },
        { "Novigrad", (45.3167, 13.5667) },
        { "Medulin", (44.8167, 13.9333) },
        { "Vir", (44.2978, 15.0806) },
        { "Murter", (43.8167, 15.5833) },
        { "Vodice", (43.7611, 15.7789) },
        { "Primošten", (43.5875, 15.9233) },
        { "Nin", (44.2406, 15.1800) },
        { "Novalja", (44.5578, 14.8861) },
        { "Pag", (44.4397, 15.0461) },
    };

    public SimilarityEngine(ILogger<SimilarityEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate the full multi-dimensional similarity vector.
    /// 
    /// CRITICAL: Binary gates are checked FIRST. If type mismatch or junk,
    /// the score is forced to 0 and the method returns immediately.
    /// This prevents cross-type contamination (e.g., Land scored against House).
    /// </summary>
    public SimilarityVector CalculateSimilarity(
        CanonicalPropertyModel subject,
        CanonicalPropertyModel candidate,
        V6SimilarityWeights? weights = null)
    {
        weights ??= V6SimilarityWeights.Default;

        var vector = new SimilarityVector();

        // ══════════════════════════════════════════════════════
        //  BINARY GATE 1: IRON GATE (Type Matching)
        //  If subject is House and candidate is Land → score = 0
        // ══════════════════════════════════════════════════════
        if (subject.TypeCategory != PropertyCategory.Unknown
            && candidate.TypeCategory != PropertyCategory.Unknown
            && subject.TypeCategory != candidate.TypeCategory)
        {
            vector.TypeMatch = false;
            vector.WeightedTotal = 0;
            _logger.LogDebug("Iron Gate REJECT: {SubjectType} vs {CandidateType} for {Url}",
                subject.TypeCategory, candidate.TypeCategory, candidate.SourceUrl);
            return vector;
        }
        vector.TypeMatch = true;

        // ══════════════════════════════════════════════════════
        //  BINARY GATE 2: JUNK DETECTION
        //  If candidate was flagged as junk → score = 0
        // ══════════════════════════════════════════════════════
        if (!candidate.PassedJunkFilter)
        {
            vector.IsJunk = true;
            vector.WeightedTotal = 0;
            return vector;
        }

        // ── S_loc: Location similarity ────────────────────────
        vector.LocationScore = CalculateLocationScore(subject, candidate);

        // ── S_area: Area similarity (Gaussian RBF) ────────────
        vector.AreaScore = CalculateAreaScore(subject, candidate);

        // ── S_cond: Condition similarity (ordinal) ────────────
        vector.ConditionScore = MathHelpers.ConditionSimilarity(
            subject.ConditionOrdinal,
            candidate.ConditionOrdinal);

        // ── S_feat: Feature similarity (Jaccard) ──────────────
        vector.FeaturesScore = MathHelpers.JaccardIndex(
            subject.Features?.ToList() ?? [],
            candidate.Features?.ToList() ?? []);

        // ── S_sem: Semantic similarity (cosine on embeddings) ─
        vector.SemanticScore = MathHelpers.CosineSimilarity(
            subject.DescriptionEmbedding,
            candidate.DescriptionEmbedding);

        // ── Weighted total ────────────────────────────────────
        vector.WeightedTotal =
            weights.LocationWeight * vector.LocationScore
          + weights.AreaWeight * vector.AreaScore
          + weights.ConditionWeight * vector.ConditionScore
          + weights.FeaturesWeight * vector.FeaturesScore
          + weights.SemanticWeight * vector.SemanticScore;

        return vector;
    }

    /// <summary>
    /// Build an NxN pairwise distance matrix for clustering.
    /// Distance = 1 - Similarity.
    /// </summary>
    public double[,] BuildPairwiseDistanceMatrix(
        List<CanonicalPropertyModel> candidates,
        V6SimilarityWeights? weights = null)
    {
        int n = candidates.Count;
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            matrix[i, i] = 0; // distance to self = 0
            for (int j = i + 1; j < n; j++)
            {
                var sim = CalculateSimilarity(candidates[i], candidates[j], weights);
                double dist = 1.0 - sim.WeightedTotal;
                matrix[i, j] = dist;
                matrix[j, i] = dist; // symmetric
            }
        }

        return matrix;
    }

    // ══════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate location similarity using lat/lng if available,
    /// otherwise fall back to city-name geocoding lookup.
    /// </summary>
    private double CalculateLocationScore(
        CanonicalPropertyModel subject,
        CanonicalPropertyModel candidate)
    {
        // Try direct coordinates first
        if (subject.Latitude.HasValue && subject.Longitude.HasValue
            && candidate.Latitude.HasValue && candidate.Longitude.HasValue)
        {
            return MathHelpers.LocationSimilarity(
                subject.Latitude, subject.Longitude,
                candidate.Latitude, candidate.Longitude,
                gamma: GetLocationGamma(subject));
        }

        // Fallback: geocode from city name lookup
        var subjectCoords = LookupCityCoords(subject.Location);
        var candidateCoords = LookupCityCoords(candidate.Location);

        if (subjectCoords.HasValue && candidateCoords.HasValue)
        {
            return MathHelpers.LocationSimilarity(
                subjectCoords.Value.Lat, subjectCoords.Value.Lon,
                candidateCoords.Value.Lat, candidateCoords.Value.Lon,
                gamma: GetLocationGamma(subject));
        }

        // Last resort: exact city name matching
        if (!string.IsNullOrWhiteSpace(subject.Location)
            && !string.IsNullOrWhiteSpace(candidate.Location))
        {
            // Same city = 0.8, same first word = 0.4, else = 0.1
            if (subject.Location.Equals(candidate.Location, StringComparison.OrdinalIgnoreCase))
                return 0.85;

            string subjectCity = subject.Location.Split(',', ' ')[0].Trim();
            string candidateCity = candidate.Location.Split(',', ' ')[0].Trim();

            if (subjectCity.Equals(candidateCity, StringComparison.OrdinalIgnoreCase))
                return 0.70;
        }

        return 0.1; // insufficient data
    }

    /// <summary>
    /// Calculate area similarity. Uses building area + land area with fallbacks.
    /// </summary>
    private double CalculateAreaScore(
        CanonicalPropertyModel subject,
        CanonicalPropertyModel candidate)
    {
        double subjectArea = subject.SizeM2 > 0 ? subject.SizeM2
            : subject.LandSizeM2 > 0 ? subject.LandSizeM2 : 0;
        double candidateArea = candidate.SizeM2 > 0 ? candidate.SizeM2
            : candidate.LandSizeM2 > 0 ? candidate.LandSizeM2 : 0;

        if (subjectArea <= 0 || candidateArea <= 0) return 0.3; // unknown → mild penalty

        double areaScore = MathHelpers.AreaSimilarity(subjectArea, candidateArea);

        // Bonus: if land sizes are both known, blend in land similarity
        if (subject.LandSizeM2 > 0 && candidate.LandSizeM2 > 0 && subject.SizeM2 > 0)
        {
            double landScore = MathHelpers.AreaSimilarity(subject.LandSizeM2, candidate.LandSizeM2);
            areaScore = 0.7 * areaScore + 0.3 * landScore;
        }

        return areaScore;
    }

    /// <summary>
    /// Get appropriate gamma (decay rate) for location similarity.
    /// Urban properties: gamma=0.5 (fast decay, hyper-local)
    /// Rural/island: gamma=0.1 (slow decay, wider net)
    /// </summary>
    private static double GetLocationGamma(CanonicalPropertyModel subject)
    {
        string loc = subject.Location?.ToLowerInvariant() ?? "";
        string type = subject.PropertyType?.ToLowerInvariant() ?? "";

        // Land / agriculture → very slow decay (large search radius)
        if (type.Contains("zemlji") || type.Contains("oranica") || type.Contains("parcel"))
            return 0.05;

        // Islands → moderate decay
        string[] islands = ["hvar", "brač", "brac", "korčula", "korcula", "vis", "krk",
            "rab", "cres", "lošinj", "losinj", "pag", "ugljan", "pašman", "pasman",
            "murter", "vir", "čiovo", "ciovo", "šolta", "solta", "mljet"];
        if (islands.Any(isl => loc.Contains(isl)))
            return 0.15;

        // Major cities → fast decay (hyperlocal matters)
        string[] cities = ["zagreb", "split", "rijeka", "osijek", "dubrovnik"];
        if (cities.Any(c => loc.Contains(c)))
            return 0.8;

        // Default coastal/town
        return 0.4;
    }

    /// <summary>
    /// Look up approximate city coordinates from the static dictionary.
    /// Tries exact match, then substring match.
    /// </summary>
    private static (double Lat, double Lon)? LookupCityCoords(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;

        // Exact match
        if (CroatianCityCoords.TryGetValue(location.Trim(), out var exact))
            return exact;

        // Try first word
        string firstWord = location.Split(',', ' ', '-')[0].Trim();
        if (CroatianCityCoords.TryGetValue(firstWord, out var byFirst))
            return byFirst;

        // Substring match (for "Zagreb - Trnje" → find "Zagreb")
        foreach (var kvp in CroatianCityCoords)
        {
            if (location.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
