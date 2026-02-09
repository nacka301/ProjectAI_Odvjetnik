namespace Backend.Services.V6;

/// <summary>
/// V6 Math Helper Library — Production-grade similarity & distance functions.
/// 
/// Contains:
///   • Haversine distance (km between two lat/lng points)
///   • Exponential decay for location similarity
///   • Gaussian RBF for continuous attribute comparison (area, price)
///   • Ordinal distance for categorical attributes (condition)
///   • Jaccard Index for set-based features (amenities)
///   • Cosine Similarity for embedding vectors (semantic comparison)
///   • Percentile / IQR / Weighted Median helpers
///   • DBSCAN clustering implementation
/// </summary>
public static class MathHelpers
{
    private const double EarthRadiusKm = 6371.0;

    // ══════════════════════════════════════════════════════════
    //  HAVERSINE DISTANCE
    //  Calculates great-circle distance between two lat/lng points.
    //  Formula: a = sin²(Δφ/2) + cos(φ₁)·cos(φ₂)·sin²(Δλ/2)
    //           c = 2·atan2(√a, √(1−a))
    //           d = R·c
    // ══════════════════════════════════════════════════════════
    public static double HaversineDistanceKm(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(DegreesToRadians(lat1))
                 * Math.Cos(DegreesToRadians(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    // ══════════════════════════════════════════════════════════
    //  LOCATION SIMILARITY — Exponential Decay
    //  S_loc = e^(-γ · distance)
    //  γ controls how fast similarity drops with distance.
    //  Default γ=0.5 → ~60% similarity at 1 km, ~22% at 3 km.
    //  For rural areas use smaller γ (slower decay).
    // ══════════════════════════════════════════════════════════
    public static double LocationSimilarity(
        double distanceKm,
        double gamma = 0.5)
    {
        if (distanceKm < 0) distanceKm = 0;
        return Math.Exp(-gamma * distanceKm);
    }

    /// <summary>
    /// Combined location similarity from lat/lng pairs.
    /// Returns 0 if either coordinate pair is missing.
    /// </summary>
    public static double LocationSimilarity(
        double? lat1, double? lon1,
        double? lat2, double? lon2,
        double gamma = 0.5)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return 0.0;  // graceful degradation — no geo data available

        double dist = HaversineDistanceKm(lat1.Value, lon1.Value, lat2.Value, lon2.Value);
        return LocationSimilarity(dist, gamma);
    }

    // ══════════════════════════════════════════════════════════
    //  GAUSSIAN RBF — Radial Basis Function
    //  S_area = e^(-γ · |x - y|²)
    //  Used for continuous attributes: area (m²), land size, price.
    //  γ controls sensitivity. For area in m²:
    //    γ = 0.0001 → 50m² difference gives ~78% similarity
    //    γ = 0.001  → 50m² difference gives  ~8% similarity
    // ══════════════════════════════════════════════════════════
    public static double GaussianRbf(
        double x, double y,
        double gamma = 0.0001)
    {
        double diff = x - y;
        return Math.Exp(-gamma * diff * diff);
    }

    /// <summary>
    /// Area similarity using Gaussian RBF with auto-scaled gamma.
    /// gamma = 1 / (2 * σ²) where σ = 30% of subject area.
    /// This means ~30% area difference → ~60% similarity.
    /// </summary>
    public static double AreaSimilarity(double subjectM2, double comparableM2)
    {
        if (subjectM2 <= 0 || comparableM2 <= 0) return 0.0;

        // Auto-scale γ based on subject size
        double sigma = subjectM2 * 0.30;  // 30% tolerance
        double gamma = 1.0 / (2.0 * sigma * sigma);

        return GaussianRbf(subjectM2, comparableM2, gamma);
    }

    // ══════════════════════════════════════════════════════════
    //  ORDINAL DISTANCE — Condition Similarity
    //  Maps condition to ordinal scale:
    //    Ruins=0, Old=1, Good=2, Renovated=3, New=4
    //  S_cond = 1 - |ordinal_subject - ordinal_comp| / max_range
    //  Range [0, 1]. Same condition = 1.0, opposite ends = 0.0.
    // ══════════════════════════════════════════════════════════
    private const int MaxConditionOrdinal = 4;

    public static double ConditionSimilarity(int subjectOrdinal, int comparableOrdinal)
    {
        // Clamp to valid range [0, 4]
        subjectOrdinal = Math.Clamp(subjectOrdinal, 0, MaxConditionOrdinal);
        comparableOrdinal = Math.Clamp(comparableOrdinal, 0, MaxConditionOrdinal);

        double distance = Math.Abs(subjectOrdinal - comparableOrdinal);
        return 1.0 - (distance / MaxConditionOrdinal);
    }

    /// <summary>
    /// Parse a condition string (Croatian/English) to ordinal 0–4.
    /// </summary>
    public static int ParseConditionOrdinal(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return 2; // default: Good

        string c = condition.Trim().ToLowerInvariant();

        // Croatian and English mappings
        if (c.Contains("ruin") || c.Contains("ruševin") || c.Contains("rusev"))
            return 0; // Ruins
        if (c.Contains("star") || c.Contains("old") || c.Contains("loš") || c.Contains("los")
            || c.Contains("zapušten") || c.Contains("zapusten"))
            return 1; // Old
        if (c.Contains("dobar") || c.Contains("good") || c.Contains("solidan") || c.Contains("ured")
            || c.Contains("održavan") || c.Contains("odrzavan"))
            return 2; // Good
        if (c.Contains("renov") || c.Contains("adaptir") || c.Contains("obnovljen")
            || c.Contains("renovated") || c.Contains("refurbished"))
            return 3; // Renovated
        if (c.Contains("nov") || c.Contains("new") || c.Contains("novograd") || c.Contains("novogradnja")
            || c.Contains("first occupation") || c.Contains("prva uporaba"))
            return 4; // New

        return 2; // default: Good
    }

    // ══════════════════════════════════════════════════════════
    //  JACCARD INDEX — Feature Set Similarity
    //  J(A,B) = |A ∩ B| / |A ∪ B|
    //  Range [0, 1]. 1.0 = identical feature sets, 0.0 = no overlap.
    //  Returns 1.0 if both sets are empty (vacuously similar).
    // ══════════════════════════════════════════════════════════
    public static double JaccardIndex(
        IReadOnlyCollection<string> setA,
        IReadOnlyCollection<string> setB)
    {
        if (setA == null || setB == null) return 0.0;
        if (setA.Count == 0 && setB.Count == 0) return 1.0;  // both empty → vacuously similar

        var a = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        var b = new HashSet<string>(setB, StringComparer.OrdinalIgnoreCase);

        int intersection = a.Count(x => b.Contains(x));
        int union = a.Count + b.Count - intersection;

        return union == 0 ? 1.0 : (double)intersection / union;
    }

    // ══════════════════════════════════════════════════════════
    //  COSINE SIMILARITY — Embedding Vector Comparison
    //  cos(A,B) = (A · B) / (||A|| · ||B||)
    //  Range [-1, 1], but for positive embeddings typically [0, 1].
    //  Returns 0 if either vector is null or empty.
    // ══════════════════════════════════════════════════════════
    public static double CosineSimilarity(float[]? vectorA, float[]? vectorB)
    {
        if (vectorA == null || vectorB == null) return 0.0;
        if (vectorA.Length == 0 || vectorB.Length == 0) return 0.0;
        if (vectorA.Length != vectorB.Length) return 0.0;

        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA < 1e-10 || magnitudeB < 1e-10) return 0.0;

        double cosine = dotProduct / (magnitudeA * magnitudeB);

        // Clamp to [0, 1] for real-estate embeddings (always positive)
        return Math.Clamp(cosine, 0.0, 1.0);
    }

    // ══════════════════════════════════════════════════════════
    //  STATISTICAL HELPERS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Calculates the p-th percentile of a sorted list using linear interpolation.
    /// </summary>
    public static double Percentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues == null || sortedValues.Count == 0)
            throw new ArgumentException("Cannot compute percentile of empty list.");

        if (sortedValues.Count == 1) return sortedValues[0];

        double k = (sortedValues.Count - 1) * (percentile / 100.0);
        int lower = (int)Math.Floor(k);
        int upper = (int)Math.Ceiling(k);
        double weight = k - lower;

        if (upper >= sortedValues.Count) return sortedValues[^1];
        return sortedValues[lower] * (1.0 - weight) + sortedValues[upper] * weight;
    }

    /// <summary>
    /// Weighted median: finds the value where cumulative weight reaches 50%.
    /// </summary>
    public static double WeightedMedian(
        List<(double value, double weight)> items)
    {
        if (items == null || items.Count == 0) return 0.0;
        if (items.Count == 1) return items[0].value;

        var sorted = items.OrderBy(x => x.value).ToList();
        double totalWeight = sorted.Sum(x => x.weight);
        if (totalWeight < 1e-10) return sorted[sorted.Count / 2].value;

        double cumulative = 0;
        foreach (var (value, weight) in sorted)
        {
            cumulative += weight;
            if (cumulative >= totalWeight / 2.0)
                return value;
        }

        return sorted[^1].value;
    }

    /// <summary>
    /// Standard deviation of a list of values.
    /// </summary>
    public static double StandardDeviation(IList<double> values)
    {
        if (values == null || values.Count < 2) return 0.0;

        double mean = values.Average();
        double sumSquaredDiffs = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquaredDiffs / (values.Count - 1));
    }

    /// <summary>
    /// Coefficient of Variation = StdDev / Mean.
    /// </summary>
    public static double CoefficientOfVariation(IList<double> values)
    {
        if (values == null || values.Count < 2) return 0.0;
        double mean = values.Average();
        if (Math.Abs(mean) < 1e-10) return 0.0;
        return StandardDeviation(values) / mean;
    }

    // ══════════════════════════════════════════════════════════
    //  DBSCAN CLUSTERING
    //  Density-Based Spatial Clustering of Applications with Noise.
    //  Uses a distance matrix (1 - similarity) for clustering.
    //  eps: maximum distance between two points in same cluster.
    //  minPts: minimum points to form a dense region.
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// DBSCAN clustering on feature vectors.
    /// Returns cluster assignments: -1 = noise, 0..N = cluster IDs.
    /// </summary>
    /// <param name="distanceMatrix">NxN symmetric matrix of distances (0 = identical)</param>
    /// <param name="eps">Maximum neighbor distance. Default 0.25 (similarity > 0.75)</param>
    /// <param name="minPts">Minimum cluster size. Default 3.</param>
    public static int[] DBSCAN(double[,] distanceMatrix, double eps = 0.25, int minPts = 3)
    {
        int n = distanceMatrix.GetLength(0);
        int[] labels = new int[n];
        Array.Fill(labels, -2);  // -2 = undefined, -1 = noise, 0+ = cluster

        int clusterId = 0;

        for (int i = 0; i < n; i++)
        {
            if (labels[i] != -2) continue;  // already processed

            var neighbors = RangeQuery(distanceMatrix, i, eps);

            if (neighbors.Count < minPts)
            {
                labels[i] = -1;  // noise
                continue;
            }

            // Start new cluster
            labels[i] = clusterId;
            var seeds = new Queue<int>(neighbors.Where(j => j != i));

            while (seeds.Count > 0)
            {
                int q = seeds.Dequeue();

                if (labels[q] == -1)
                {
                    labels[q] = clusterId;  // change noise to border point
                }

                if (labels[q] != -2) continue;  // already processed

                labels[q] = clusterId;

                var qNeighbors = RangeQuery(distanceMatrix, q, eps);
                if (qNeighbors.Count >= minPts)
                {
                    foreach (int r in qNeighbors.Where(r => labels[r] == -2 || labels[r] == -1))
                    {
                        seeds.Enqueue(r);
                    }
                }
            }

            clusterId++;
        }

        return labels;
    }

    /// <summary>
    /// Returns indices of all points within eps distance of point i.
    /// </summary>
    private static List<int> RangeQuery(double[,] distanceMatrix, int i, double eps)
    {
        int n = distanceMatrix.GetLength(0);
        var neighbors = new List<int>();
        for (int j = 0; j < n; j++)
        {
            if (distanceMatrix[i, j] <= eps)
                neighbors.Add(j);
        }
        return neighbors;
    }

    /// <summary>
    /// Build a distance matrix from similarity scores.
    /// Distance = 1 - Similarity (assuming similarity is in [0,1]).
    /// </summary>
    public static double[,] BuildDistanceMatrix(List<double> similarityScores, int count)
    {
        // This is for when we have pairwise similarities
        var matrix = new double[count, count];
        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if (i == j) matrix[i, j] = 0;
                // Will be filled by caller
            }
        }
        return matrix;
    }

    /// <summary>
    /// Build a full pairwise distance matrix from a list of feature vectors.
    /// Each vector element is compared using Euclidean distance, normalized to [0,1].
    /// </summary>
    public static double[,] BuildDistanceMatrixFromVectors(List<double[]> vectors)
    {
        int n = vectors.Count;
        var matrix = new double[n, n];

        // Find max distance for normalization
        double maxDist = 0;
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            double d = EuclideanDistance(vectors[i], vectors[j]);
            if (d > maxDist) maxDist = d;
        }

        if (maxDist < 1e-10) maxDist = 1.0;

        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            if (i == j) { matrix[i, j] = 0; continue; }
            matrix[i, j] = EuclideanDistance(vectors[i], vectors[j]) / maxDist;
        }

        return matrix;
    }

    /// <summary>
    /// Euclidean distance between two vectors.
    /// </summary>
    public static double EuclideanDistance(double[] a, double[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }

    // ══════════════════════════════════════════════════════════
    //  UTILITY
    // ══════════════════════════════════════════════════════════

    private static double DegreesToRadians(double degrees)
        => degrees * Math.PI / 180.0;
}
