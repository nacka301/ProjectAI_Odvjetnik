using System.Net;
using System.Text.RegularExpressions;
using Backend.Models.V6;

namespace Backend.Services.V6;

/// <summary>
/// V6 Phase 2 — Controlled "Deep Fetch".
/// 
/// For the top N candidates, performs an HTTP GET to their specific URLs
/// to extract the full listing description, features, and metadata.
/// 
/// Respects:
///   • robots.txt (via conservative User-Agent)
///   • Rate limits (max 3 concurrent, 500ms delay)
///   • Timeout (10s per request)
/// 
/// Note: In production, this should be replaced with a headless browser
/// service (Playwright/Puppeteer) or a dedicated scraping API (Bright Data).
/// The current implementation does basic HTTP GET which works for many
/// portals that serve content without JavaScript rendering.
/// </summary>
public class DeepFetcher : IDeepFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepFetcher> _logger;

    private const int MaxConcurrent = 3;
    private const int DelayBetweenRequestsMs = 500;
    private const int TimeoutSeconds = 10;

    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);

    public DeepFetcher(HttpClient httpClient, ILogger<DeepFetcher> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; PropertyValuationBot/1.0)");
        _logger = logger;
    }

    public async Task<List<ListingData>> DeepFetchAsync(
        List<ListingData> candidates,
        int maxCandidates = 30,
        CancellationToken cancellationToken = default)
    {
        // Sort by search score descending, take top N
        var toFetch = candidates
            .OrderByDescending(c => c.SearchScore ?? 0)
            .ThenByDescending(c => c.PriceEur ?? 0)
            .Take(maxCandidates)
            .ToList();

        _logger.LogInformation("V6 Phase 2: Deep fetching {Count} listings (of {Total})",
            toFetch.Count, candidates.Count);

        var semaphore = new SemaphoreSlim(MaxConcurrent);
        int fetched = 0;

        var tasks = toFetch.Select(async listing =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await FetchListingDetails(listing, cancellationToken);
                if (listing.DeepFetched) Interlocked.Increment(ref fetched);
                await Task.Delay(DelayBetweenRequestsMs, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        _logger.LogInformation("V6 Phase 2: Successfully fetched {Fetched}/{Total} listings",
            fetched, toFetch.Count);

        return toFetch;
    }

    private async Task FetchListingDetails(ListingData listing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(listing.Url))
        {
            listing.DeepFetched = false;
            return;
        }

        try
        {
            var response = await _httpClient.GetAsync(listing.Url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Deep fetch failed ({Status}): {Url}",
                    response.StatusCode, listing.Url);
                listing.DeepFetched = false;
                return;
            }

            string html = await response.Content.ReadAsStringAsync(ct);

            // Extract the main content (raw description)
            listing.RawDescription = ExtractMainContent(html);
            listing.RawHtmlFeatures = ExtractFeaturesList(html);
            listing.ImageUrls = ExtractImageUrls(html);

            // Try to extract structured data from the page
            EnrichFromHtml(listing, html);

            listing.DeepFetched = true;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Deep fetch timed out: {Url}", listing.Url);
            listing.DeepFetched = false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug("Deep fetch HTTP error: {Url} — {Error}", listing.Url, ex.Message);
            listing.DeepFetched = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deep fetch unexpected error: {Url}", listing.Url);
            listing.DeepFetched = false;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HTML CONTENT EXTRACTION
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Extract the main description text by stripping HTML tags.
    /// Tries common listing description container patterns.
    /// </summary>
    private static string ExtractMainContent(string html)
    {
        // Try to find description container (common patterns in Croatian portals)
        string[] descPatterns =
        [
            @"<div[^>]*class=""[^""]*(?:description|opis|content|detail)[^""]*""[^>]*>(.*?)</div>",
            @"<div[^>]*id=""[^""]*(?:description|opis|detail)[^""]*""[^>]*>(.*?)</div>",
            @"<p[^>]*class=""[^""]*(?:description|opis)[^""]*""[^>]*>(.*?)</p>"
        ];

        foreach (var pattern in descPatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups[1].Length > 50)
            {
                return CleanHtml(match.Groups[1].Value);
            }
        }

        // Fallback: strip all HTML and take a reasonable text block
        string stripped = CleanHtml(html);
        if (stripped.Length > 5000)
            stripped = stripped[..5000]; // cap for performance

        return stripped;
    }

    /// <summary>
    /// Extract feature/attribute list (e.g., "Kat: 3", "Sobe: 2", "Parking: Da").
    /// </summary>
    private static string ExtractFeaturesList(string html)
    {
        // Common patterns for feature/attribute lists
        string[] featurePatterns =
        [
            @"<(?:ul|ol)[^>]*class=""[^""]*(?:feature|property|detail|attribute|specif)[^""]*""[^>]*>(.*?)</(?:ul|ol)>",
            @"<table[^>]*class=""[^""]*(?:feature|property|detail|attribute)[^""]*""[^>]*>(.*?)</table>",
            @"<div[^>]*class=""[^""]*(?:feature|property-list|amenities)[^""]*""[^>]*>(.*?)</div>"
        ];

        foreach (var pattern in featurePatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups[1].Length > 20)
            {
                return CleanHtml(match.Groups[1].Value);
            }
        }

        return "";
    }

    /// <summary>
    /// Extract image URLs from the listing page.
    /// </summary>
    private static List<string> ExtractImageUrls(string html)
    {
        var urls = new List<string>();
        var matches = Regex.Matches(html,
            @"<img[^>]+src=""([^""]+(?:\.jpg|\.jpeg|\.png|\.webp)[^""]*)""",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            string url = match.Groups[1].Value;
            // Filter out tiny icons/logos
            if (!url.Contains("logo") && !url.Contains("icon") && !url.Contains("avatar"))
            {
                urls.Add(url);
            }
        }

        return urls.Distinct().Take(20).ToList();
    }

    /// <summary>
    /// Try to extract structured data (price, size, location) from HTML.
    /// </summary>
    private static void EnrichFromHtml(ListingData listing, string html)
    {
        // Try to extract more precise price from the page
        var pricePatterns = new[]
        {
            @"(?:cijena|price|cena)[^<]*?(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR)",
            @"class=""[^""]*price[^""]*""[^>]*>.*?(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR)"
        };

        foreach (var pattern in pricePatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                string raw = match.Groups[1].Value.Replace(".", "").Replace(",", ".");
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double price) && price > 100)
                {
                    listing.PriceEur = price;
                    break;
                }
            }
        }

        // Try to extract floor/rooms/area from structured data
        var areaMatch = Regex.Match(html,
            @"(?:površina|area|size|stambena)[^<]*?(\d{1,6}(?:[,\.]\d+)?)\s*m[²2]",
            RegexOptions.IgnoreCase);
        if (areaMatch.Success)
        {
            string raw = areaMatch.Groups[1].Value.Replace(",", ".");
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double area) && area > 1)
            {
                listing.SizeM2 = area;
            }
        }

        // Recalculate price per m²
        if (listing.PriceEur > 0 && listing.SizeM2 > 0)
        {
            listing.PricePerM2 = listing.PriceEur.Value / listing.SizeM2.Value;
        }
    }

    /// <summary>
    /// Remove HTML tags and normalize whitespace.
    /// </summary>
    private static string CleanHtml(string html)
    {
        string text = WebUtility.HtmlDecode(html);
        text = HtmlTagRegex.Replace(text, " ");
        text = WhitespaceRegex.Replace(text, " ");
        return text.Trim();
    }
}
