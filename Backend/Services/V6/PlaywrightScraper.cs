using System.Net;
using System.Text.RegularExpressions;
using Backend.Models.V6;
using Microsoft.Playwright;

namespace Backend.Services.V6;

/// <summary>
/// Playwright-based scraper that mimics a HUMAN browsing njuskalo.hr.
///
/// Flow:
///   1. Homepage visit → cookie consent
///   2. Type search query character-by-character
///   3. Harvest 10-15 listing URLs from search results
///   4. "Pogo-stick" loop: visit each → read → scroll → extract → return
///
/// Anti-detection:
///   • Real Chromium with stealth UA + 1920×1080 viewport
///   • Random delays between actions (human-like cadence)
///   • navigator.webdriver hidden via Playwright's built-in handling
///
/// Resource management:
///   • Browser disposed immediately after each scraping job
///   • Headless mode with --no-sandbox for Docker/Render
/// </summary>
public class PlaywrightScraper : IPlaywrightScraper
{
    private readonly ILogger<PlaywrightScraper> _logger;

    private const string NjuskaloUrl = "https://www.njuskalo.hr";

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0.0.0 Safari/537.36";

    // Regex for extracting structured data from listing pages
    private static readonly Regex PriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AreaRegex = new(
        @"(\d{1,6}(?:[,\.]\d+)?)\s*m[²2]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PlaywrightScraper(ILogger<PlaywrightScraper> logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  MAIN ENTRY — ScrapeListingsAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<List<ListingData>> ScrapeListingsAsync(
        string location,
        string type,
        int maxListings = 12,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ListingData>();
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            _logger.LogInformation(
                "PlaywrightScraper: Starting scrape for \"{Type} {Location}\" (max {Max})",
                type, location, maxListings);

            // ── 1. Launch browser ────────────────────────────────
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",    // prevent /dev/shm exhaustion on Docker
                    "--disable-gpu",
                    "--disable-extensions",
                    "--disable-background-networking",
                    "--disable-default-apps",
                    "--no-first-run"
                }
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = UserAgent,
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "hr-HR",
                TimezoneId = "Europe/Zagreb",
                JavaScriptEnabled = true,
                IgnoreHTTPSErrors = true
            });

            // Block unnecessary resources to save RAM and speed things up
            await context.RouteAsync("**/*.{png,jpg,jpeg,gif,svg,webp,woff,woff2,ttf}", route =>
                route.AbortAsync());

            var page = await context.NewPageAsync();

            // ── 2. Navigate to homepage ──────────────────────────
            _logger.LogInformation("PlaywrightScraper: Navigating to {Url}", NjuskaloUrl);
            await page.GotoAsync(NjuskaloUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // ── 3. Handle cookie consent ─────────────────────────
            await HandleCookieConsent(page);

            // ── 4. Type search query (human-like) ────────────────
            string query = $"{type} {location}";
            await TypeSearchQuery(page, query);

            // ── 5. Wait for results page ─────────────────────────
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(Random.Shared.Next(1500, 3000), cancellationToken);

            // ── 6. Harvest listing URLs ──────────────────────────
            var listingUrls = await HarvestListingUrls(page, maxListings);
            _logger.LogInformation("PlaywrightScraper: Found {Count} listing URLs", listingUrls.Count);

            if (listingUrls.Count == 0)
            {
                _logger.LogWarning("PlaywrightScraper: No listing URLs found — search may have failed");
                return results;
            }

            // ── 7. Pogo-stick loop ──────────────────────────────
            foreach (var url in listingUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var listing = await ScrapeSingleListing(page, url, cancellationToken);
                    if (listing != null)
                    {
                        results.Add(listing);
                        _logger.LogDebug("PlaywrightScraper: Scraped {Url} — Price={Price}, Area={Area}",
                            url, listing.PriceEur, listing.SizeM2);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PlaywrightScraper: Failed to scrape {Url}", url);
                }

                // Cooldown between listings (1-2s)
                await Task.Delay(Random.Shared.Next(1000, 2500), cancellationToken);
            }

            _logger.LogInformation("PlaywrightScraper: Completed — {Count}/{Total} listings scraped",
                results.Count, listingUrls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlaywrightScraper: Fatal error during scrape");
        }
        finally
        {
            // ── IMMEDIATE RESOURCE CLEANUP ──
            if (browser != null)
            {
                await browser.CloseAsync();
                _logger.LogDebug("PlaywrightScraper: Browser closed");
            }
            playwright?.Dispose();
        }

        return results;
    }

    // ══════════════════════════════════════════════════════════════
    //  STEP 3: COOKIE CONSENT
    // ══════════════════════════════════════════════════════════════

    private async Task HandleCookieConsent(IPage page)
    {
        try
        {
            // Wait a human-like moment before interacting
            await Task.Delay(2000);

            // Try multiple common consent button selectors
            string[] consentSelectors =
            [
                "button:has-text('Prihvaćam')",
                "button:has-text('Prihvacam')",
                "button:has-text('Slažem se')",
                "button:has-text('Slazem se')",
                "button:has-text('Accept')",
                "[id*='accept']",
                "[class*='consent'] button",
                "[id*='consent'] button",
                "[data-testid*='accept']",
                "button:has-text('OK')"
            ];

            foreach (var selector in consentSelectors)
            {
                var button = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(button, 1000))
                {
                    await button.ClickAsync();
                    _logger.LogDebug("PlaywrightScraper: Cookie consent clicked ({Selector})", selector);
                    await Task.Delay(Random.Shared.Next(500, 1000));
                    return;
                }
            }

            _logger.LogDebug("PlaywrightScraper: No cookie consent dialog found — continuing");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("PlaywrightScraper: Cookie consent handling: {Error}", ex.Message);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  STEP 4: HUMAN-LIKE SEARCH TYPING
    // ══════════════════════════════════════════════════════════════

    private async Task TypeSearchQuery(IPage page, string query)
    {
        _logger.LogDebug("PlaywrightScraper: Typing search query \"{Query}\"", query);

        // Try multiple search input selectors (njuskalo may change layout)
        string[] searchSelectors =
        [
            "input[name='keywords']",
            "input[type='search']",
            "input[id*='search']",
            "input[class*='search']",
            "input[placeholder*='Pretraži']",
            "input[placeholder*='Pretrazi']",
            "input[placeholder*='traži']",
            "#text-search",
            ".search-input input",
            "form[action*='search'] input[type='text']"
        ];

        ILocator? searchInput = null;

        foreach (var selector in searchSelectors)
        {
            var locator = page.Locator(selector).First;
            try
            {
                if (await IsVisibleWithinAsync(locator, 2000))
                {
                    searchInput = locator;
                    _logger.LogDebug("PlaywrightScraper: Found search input ({Selector})", selector);
                    break;
                }
            }
            catch { /* try next */ }
        }

        if (searchInput == null)
        {
            _logger.LogWarning("PlaywrightScraper: Could not find search input — attempting fallback URL navigation");
            string encodedQuery = Uri.EscapeDataString(query);
            await page.GotoAsync($"{NjuskaloUrl}/pretraga?keywords={encodedQuery}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });
            return;
        }

        // Click on the search bar
        await searchInput.ClickAsync();
        await Task.Delay(Random.Shared.Next(300, 600));

        // Clear any existing text
        await searchInput.FillAsync("");
        await Task.Delay(Random.Shared.Next(200, 400));

        // Type character by character with random delays (50-150ms)
        foreach (char c in query)
        {
            await searchInput.PressAsync(c.ToString());
            await Task.Delay(Random.Shared.Next(50, 150));
        }

        // Small pause before pressing Enter (like a human would)
        await Task.Delay(Random.Shared.Next(300, 800));

        // Press Enter to submit
        await searchInput.PressAsync("Enter");
        _logger.LogDebug("PlaywrightScraper: Search submitted");
    }

    // ══════════════════════════════════════════════════════════════
    //  STEP 6: HARVEST LISTING URLs FROM SEARCH RESULTS
    // ══════════════════════════════════════════════════════════════

    private async Task<List<string>> HarvestListingUrls(IPage page, int maxListings)
    {
        var urls = new List<string>();

        // Wait for listings to appear
        await Task.Delay(Random.Shared.Next(1000, 2000));

        // Common njuskalo listing link selectors
        string[] listingSelectors =
        [
            "a.EntityList-item--Regular",
            "li.EntityList-item a[href*='/oglas/']",
            "article a[href*='/oglas/']",
            ".EntityList a[href*='nekretnine']",
            "a[href*='/nekretnine-']",
            "a[href*='/oglas/']",
            ".entity-body a[href]",
            ".content-main a[href*='/oglas/']"
        ];

        foreach (var selector in listingSelectors)
        {
            try
            {
                var links = page.Locator(selector);
                int count = await links.CountAsync();

                if (count > 0)
                {
                    _logger.LogDebug("PlaywrightScraper: Found {Count} links with selector {Selector}",
                        count, selector);

                    for (int i = 0; i < Math.Min(count, maxListings + 5); i++)
                    {
                        string? href = await links.Nth(i).GetAttributeAsync("href");
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            // Resolve relative URLs
                            if (href.StartsWith("/"))
                                href = $"{NjuskaloUrl}{href}";

                            // Filter: only listing pages, not categories/ads
                            if (IsValidListingUrl(href) && !urls.Contains(href))
                                urls.Add(href);
                        }
                    }

                    if (urls.Count > 0) break; // found with this selector
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PlaywrightScraper: Selector {Selector} failed: {Error}",
                    selector, ex.Message);
            }
        }

        // Fallback: grab all anchors that look like listing pages
        if (urls.Count == 0)
        {
            _logger.LogDebug("PlaywrightScraper: Using fallback — scanning all <a> tags");
            var allLinks = page.Locator("a[href]");
            int allCount = await allLinks.CountAsync();

            for (int i = 0; i < allCount && urls.Count < maxListings; i++)
            {
                string? href = await allLinks.Nth(i).GetAttributeAsync("href");
                if (!string.IsNullOrWhiteSpace(href))
                {
                    if (href.StartsWith("/")) href = $"{NjuskaloUrl}{href}";
                    if (IsValidListingUrl(href) && !urls.Contains(href))
                        urls.Add(href);
                }
            }
        }

        return urls.Take(maxListings).ToList();
    }

    /// <summary>
    /// Filter URLs to only include real listing pages (not category pages, ads, etc.).
    /// </summary>
    private static bool IsValidListingUrl(string url)
    {
        string lower = url.ToLowerInvariant();

        // Must be njuskalo
        if (!lower.Contains("njuskalo.hr")) return false;

        // Must look like an individual listing (contains "/oglas/" or has an ID pattern)
        bool looksLikeListing = lower.Contains("/oglas/")
            || Regex.IsMatch(url, @"/nekretnine-[\w-]+-oglas-\d+", RegexOptions.IgnoreCase)
            || Regex.IsMatch(url, @"-oglas-\d+", RegexOptions.IgnoreCase);

        if (!looksLikeListing) return false;

        // Exclude non-listing pages
        string[] excludePatterns = ["/vijesti", "/blog", "/info", "/pravila", "/kontakt",
            "/moj-njuskalo", "/favoriti", "/poruke", "/partner"];
        if (excludePatterns.Any(p => lower.Contains(p))) return false;

        return true;
    }

    // ══════════════════════════════════════════════════════════════
    //  STEP 7: POGO-STICK — SCRAPE A SINGLE LISTING
    // ══════════════════════════════════════════════════════════════

    private async Task<ListingData?> ScrapeSingleListing(
        IPage page, string url, CancellationToken ct)
    {
        // ── Navigate to listing ──
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 20000
        });

        // ── Simulate human read time (3-6 seconds) ──
        await Task.Delay(Random.Shared.Next(3000, 6000), ct);

        // ── Scroll to "Podaci o objektu" section to trigger lazy-loading ──
        await ScrollToDetailsSection(page);
        await Task.Delay(Random.Shared.Next(1000, 2000), ct);

        // ── Extract data from the page ──
        return await ExtractListingData(page, url);
    }

    /// <summary>
    /// Scroll down to the property details section, triggering lazy-loaded content.
    /// </summary>
    private async Task ScrollToDetailsSection(IPage page)
    {
        try
        {
            // Try to find the "Podaci o objektu" section and scroll to it
            string[] detailSelectors =
            [
                "text=Podaci o objektu",
                "text=Osnovni podaci",
                "h2:has-text('Podaci')",
                ".ClassifiedDetailBasicDetails",
                "[class*='BasicDetails']",
                "[class*='property-data']",
                "#description"
            ];

            foreach (var selector in detailSelectors)
            {
                try
                {
                    var element = page.Locator(selector).First;
                    if (await IsVisibleWithinAsync(element, 1500))
                    {
                        await element.ScrollIntoViewIfNeededAsync();
                        _logger.LogDebug("PlaywrightScraper: Scrolled to {Selector}", selector);
                        return;
                    }
                }
                catch { /* try next */ }
            }

            // Fallback: smooth scroll down half the page
            await page.EvaluateAsync(@"
                window.scrollTo({
                    top: document.body.scrollHeight * 0.6,
                    behavior: 'smooth'
                });
            ");
            await Task.Delay(800);

            // Scroll a bit more
            await page.EvaluateAsync(@"
                window.scrollTo({
                    top: document.body.scrollHeight * 0.85,
                    behavior: 'smooth'
                });
            ");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("PlaywrightScraper: Scroll error: {Error}", ex.Message);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  DATA EXTRACTION FROM LISTING PAGE
    // ══════════════════════════════════════════════════════════════

    private async Task<ListingData?> ExtractListingData(IPage page, string url)
    {
        try
        {
            // Get full page text and HTML for extraction
            string pageText = await page.InnerTextAsync("body");
            string title = await page.TitleAsync() ?? "";

            var listing = new ListingData
            {
                Url = url,
                Title = CleanTitle(title),
                Portal = "njuskalo",
                ScrapedAt = DateTime.UtcNow,
                DeepFetched = true,         // we have full content
                PassedPriceGate = false     // will be set after extraction
            };

            // ── Extract price ──────────────────────────────────
            listing.PriceEur = await ExtractPrice(page, pageText);

            // ── Extract area (m²) ──────────────────────────────
            listing.SizeM2 = await ExtractArea(page, pageText);

            // ── Extract description ────────────────────────────
            listing.RawDescription = await ExtractDescription(page);
            listing.Snippet = listing.RawDescription.Length > 300
                ? listing.RawDescription[..300]
                : listing.RawDescription;

            // ── Extract feature list ───────────────────────────
            listing.RawHtmlFeatures = await ExtractFeatures(page);

            // ── Extract image URLs ─────────────────────────────
            listing.ImageUrls = await ExtractImages(page);

            // ── Extract location ───────────────────────────────
            listing.Location = await ExtractLocation(page, pageText);

            // ── Calculate derived fields ───────────────────────
            listing.PassedPriceGate = listing.PriceEur is > 0;

            if (listing.PriceEur > 0 && listing.SizeM2 > 0)
                listing.PricePerM2 = listing.PriceEur.Value / listing.SizeM2.Value;

            // Detect property type from title
            listing.DetectedType = CandidateGenerator.DetectListingType(
                listing.Title, listing.Snippet ?? "").ToString();

            // Iron Gate / Junk flags default to true (scraped from a real portal)
            listing.PassedIronGate = true;
            listing.PassedJunkFilter = true;

            return listing;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaywrightScraper: Extraction failed for {Url}", url);
            return null;
        }
    }

    // ── PRICE EXTRACTION ──────────────────────────────────────

    private async Task<double?> ExtractPrice(IPage page, string pageText)
    {
        // Try specific price selectors first
        string[] priceSelectors =
        [
            ".ClassifiedDetailSummary-price",
            "[class*='price']",
            ".entity-price",
            "[data-testid*='price']",
            ".price-item"
        ];

        foreach (var selector in priceSelectors)
        {
            try
            {
                var el = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(el, 500))
                {
                    string text = await el.InnerTextAsync();
                    double? price = ParsePrice(text);
                    if (price is > 100) return price;
                }
            }
            catch { /* try next */ }
        }

        // Fallback: regex from full page text
        return ParsePrice(pageText);
    }

    private static double? ParsePrice(string text)
    {
        var match = PriceRegex.Match(text);
        if (!match.Success) return null;

        string raw = match.Groups[1].Value
            .Replace(".", "")
            .Replace(",", ".");

        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double val) && val > 100 ? val : null;
    }

    // ── AREA EXTRACTION ───────────────────────────────────────

    private async Task<double?> ExtractArea(IPage page, string pageText)
    {
        // Try specific area selectors
        string[] areaSelectors =
        [
            "text=Stambena površina",
            "text=Površina zemljišta",
            "text=Površina",
            "[class*='area']",
            "[class*='size']"
        ];

        foreach (var selector in areaSelectors)
        {
            try
            {
                var el = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(el, 500))
                {
                    // Get the parent or next sibling for the value
                    string text = await el.Locator("..").InnerTextAsync();
                    double? area = ParseArea(text);
                    if (area is > 1) return area;
                }
            }
            catch { /* try next */ }
        }

        // Fallback: regex from page text
        return ParseArea(pageText);
    }

    private static double? ParseArea(string text)
    {
        var match = AreaRegex.Match(text);
        if (!match.Success) return null;

        string raw = match.Groups[1].Value.Replace(",", ".");
        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double val) && val > 1 ? val : null;
    }

    // ── DESCRIPTION EXTRACTION ────────────────────────────────

    private async Task<string> ExtractDescription(IPage page)
    {
        string[] descSelectors =
        [
            ".ClassifiedDetailDescription-text",
            "[class*='description'] p",
            "[class*='Description']",
            ".entity-description",
            "#description",
            ".opis-oglasa"
        ];

        foreach (var selector in descSelectors)
        {
            try
            {
                var el = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(el, 500))
                {
                    string text = await el.InnerTextAsync();
                    if (text.Length > 30) return text.Trim();
                }
            }
            catch { /* try next */ }
        }

        // Fallback: get all paragraph text
        try
        {
            var paragraphs = page.Locator("p");
            int count = await paragraphs.CountAsync();
            var texts = new List<string>();

            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                string text = await paragraphs.Nth(i).InnerTextAsync();
                if (text.Length > 20) texts.Add(text.Trim());
            }

            return string.Join("\n", texts);
        }
        catch
        {
            return "";
        }
    }

    // ── FEATURES EXTRACTION ───────────────────────────────────

    private async Task<string> ExtractFeatures(IPage page)
    {
        string[] featureSelectors =
        [
            ".ClassifiedDetailBasicDetails",
            "[class*='BasicDetails']",
            "[class*='features']",
            "[class*='amenities']",
            "[class*='property-data']",
            "table.details",
            "dl.details"
        ];

        foreach (var selector in featureSelectors)
        {
            try
            {
                var el = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(el, 500))
                {
                    return await el.InnerTextAsync();
                }
            }
            catch { /* try next */ }
        }

        return "";
    }

    // ── IMAGE EXTRACTION ──────────────────────────────────────

    private async Task<List<string>> ExtractImages(IPage page)
    {
        var images = new List<string>();

        try
        {
            // Look for gallery images
            string[] imgSelectors =
            [
                ".ClassifiedDetailGallery img",
                "[class*='gallery'] img",
                "[class*='Gallery'] img",
                ".entity-gallery img",
                "img[src*='/images/']"
            ];

            foreach (var selector in imgSelectors)
            {
                try
                {
                    var imgs = page.Locator(selector);
                    int count = await imgs.CountAsync();

                    if (count > 0)
                    {
                        for (int i = 0; i < Math.Min(count, 20); i++)
                        {
                            string? src = await imgs.Nth(i).GetAttributeAsync("src")
                                ?? await imgs.Nth(i).GetAttributeAsync("data-src");

                            if (!string.IsNullOrWhiteSpace(src)
                                && !src.Contains("logo")
                                && !src.Contains("icon")
                                && !images.Contains(src))
                            {
                                images.Add(src);
                            }
                        }

                        if (images.Count > 0) break;
                    }
                }
                catch { /* try next */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("PlaywrightScraper: Image extraction error: {Error}", ex.Message);
        }

        return images;
    }

    // ── LOCATION EXTRACTION ───────────────────────────────────

    private async Task<string> ExtractLocation(IPage page, string pageText)
    {
        string[] locSelectors =
        [
            ".ClassifiedDetailSummary-location",
            "[class*='location']",
            "[class*='Location']",
            ".entity-location",
            "text=Lokacija"
        ];

        foreach (var selector in locSelectors)
        {
            try
            {
                var el = page.Locator(selector).First;
                if (await IsVisibleWithinAsync(el, 500))
                {
                    string text = await el.InnerTextAsync();
                    if (text.Length > 2 && text.Length < 200)
                        return text.Trim();
                }
            }
            catch { /* try next */ }
        }

        return "";
    }

    // ══════════════════════════════════════════════════════════════
    //  UTILITY
    // ══════════════════════════════════════════════════════════════

    private static string CleanTitle(string title)
    {
        // Remove "| Njuskalo.hr" suffix and trim
        int pipeIdx = title.LastIndexOf('|');
        if (pipeIdx > 0) title = title[..pipeIdx];

        int dashIdx = title.LastIndexOf(" - Njuskalo", StringComparison.OrdinalIgnoreCase);
        if (dashIdx > 0) title = title[..dashIdx];

        return title.Trim();
    }

    /// <summary>
    /// Check if a locator becomes visible within a timeout.
    /// Replaces the obsolete LocatorIsVisibleOptions.Timeout pattern.
    /// </summary>
    private static async Task<bool> IsVisibleWithinAsync(ILocator locator, int timeoutMs = 1000)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
