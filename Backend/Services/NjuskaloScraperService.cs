using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Backend.Services;

/// <summary>
/// V7 — Playwright-based Njuškalo scraper.
/// Fixes "tunnel vision" by scrolling the entire results page to trigger
/// lazy loading and capture ALL listings, not just the initially visible ones.
///
/// Features:
///   1. Human-like smooth scrolling (400-600px increments, random delays)
///   2. Dynamic wait (NetworkIdle) after reaching bottom
///   3. Complete DOM collection via .EntityList-item selector
///   4. Bounce scroll reliability check (if <5 items found)
///   5. Sponsored/Adsense filtering AFTER full capture
/// </summary>
public class NjuskaloScraperService
{
    private readonly ILogger<NjuskaloScraperService> _logger;
    private static readonly Random _rng = new();

    // ── Selectors ──
    private const string ListingSelector = ".EntityList-item";
    private const string SponsoredSelector = ".EntityList-item--VauVau, .EntityList-item--Adsense, .EntityList-item--Banner, [class*='sponsored'], [class*='Sponsored'], [class*='adsense'], [class*='Adsense']";

    // ── Scroll tuning ──
    private const int MinScrollPx = 400;
    private const int MaxScrollPx = 600;
    private const int MinDelayMs  = 200;
    private const int MaxDelayMs  = 500;
    private const int MaxListings = 50;

    // ── Price extraction ──
    private static readonly Regex PriceRegex = new(
        @"(\d{1,3}(?:\.\d{3})*(?:,\d+)?)\s*(?:€|EUR)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public NjuskaloScraperService(ILogger<NjuskaloScraperService> logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to the Njuškalo search URL, scroll to load all lazy content,
    /// and return parsed listing data from the full DOM.
    /// </summary>
    public async Task<List<ScrapedListing>> ScrapeListingsAsync(string url)
    {
        _logger.LogInformation("Playwright scraper: navigating to {Url}", url);

        IPlaywright? pw = null;
        IBrowser? browser = null;

        try
        {
            pw = await Playwright.CreateAsync();
            browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--single-process"
                }
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "hr-HR"
            });

            var page = await context.NewPageAsync();

            // Navigate with generous timeout (Njuškalo can be slow)
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000
            });

            // Wait a moment for initial JS to execute
            await page.WaitForTimeoutAsync(1500);

            // ── 1. Human-like smooth scrolling ──
            await SmoothScrollAsync(page);

            // ── 2. Dynamic wait — ensure all lazy content is rendered ──
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 10_000 });
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("NetworkIdle timeout — proceeding with whatever is loaded");
            }

            // ── 3. Complete collection from DOM ──
            var items = await page.QuerySelectorAllAsync(ListingSelector);
            _logger.LogInformation("Playwright: found {Count} .EntityList-item elements after scrolling", items.Count);

            // ── 4. Reliability check — bounce scroll if <5 items ──
            if (items.Count < 5)
            {
                _logger.LogWarning("Playwright: only {Count} items — triggering bounce scroll", items.Count);
                await BounceScrollAsync(page);

                // Re-query after bounce
                items = await page.QuerySelectorAllAsync(ListingSelector);
                _logger.LogInformation("Playwright: {Count} items after bounce scroll", items.Count);
            }

            // ── 5. Parse all items, THEN filter sponsored ──
            var allListings = new List<ScrapedListing>();
            foreach (var item in items)
            {
                var listing = await ParseListingElement(item, page);
                if (listing is not null)
                    allListings.Add(listing);
            }

            _logger.LogInformation("Playwright: parsed {Total} listings from DOM", allListings.Count);

            // Filter out sponsored/Adsense AFTER full capture
            var sponsoredCount = allListings.Count(l => l.IsSponsored);
            var organic = allListings.Where(l => !l.IsSponsored).ToList();

            _logger.LogInformation(
                "Playwright: {Organic} organic + {Sponsored} sponsored = {Total} total",
                organic.Count, sponsoredCount, allListings.Count);

            return organic;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Playwright scraper failed for {Url}", url);
            return [];
        }
        finally
        {
            if (browser is not null)
                await browser.CloseAsync();
            pw?.Dispose();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SMOOTH SCROLL — human-like incremental scrolling
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Scrolls down in 400-600px increments with random delays (200-500ms)
    /// to mimic human behavior and trigger lazy loading.
    /// Stops when: bottom reached, or MaxListings items detected, or 60 scrolls done.
    /// </summary>
    private async Task SmoothScrollAsync(IPage page)
    {
        const int maxScrollAttempts = 60;
        int scrollCount = 0;
        double previousHeight = 0;
        int staleCount = 0;

        _logger.LogDebug("SmoothScroll: starting incremental scroll");

        for (int i = 0; i < maxScrollAttempts; i++)
        {
            // Random scroll distance between 400-600px
            int scrollPx = _rng.Next(MinScrollPx, MaxScrollPx + 1);

            await page.EvaluateAsync($"window.scrollBy(0, {scrollPx})");
            scrollCount++;

            // Random human-like delay between scrolls
            int delayMs = _rng.Next(MinDelayMs, MaxDelayMs + 1);
            await page.WaitForTimeoutAsync(delayMs);

            // Check how many items are in the DOM now
            var itemCount = await page.EvaluateAsync<int>(
                $"document.querySelectorAll('{ListingSelector}').length");

            if (itemCount >= MaxListings)
            {
                _logger.LogDebug("SmoothScroll: reached {Count} items (max={Max}), stopping",
                    itemCount, MaxListings);
                break;
            }

            // Check if we've reached the bottom (page height stopped growing)
            var currentHeight = await page.EvaluateAsync<double>(
                "document.documentElement.scrollHeight");

            if (Math.Abs(currentHeight - previousHeight) < 10)
            {
                staleCount++;
                if (staleCount >= 3)
                {
                    _logger.LogDebug("SmoothScroll: page height stable for 3 cycles — bottom reached");
                    break;
                }
            }
            else
            {
                staleCount = 0;
            }

            previousHeight = currentHeight;
        }

        _logger.LogInformation("SmoothScroll: completed {Scrolls} scroll steps", scrollCount);
    }

    // ══════════════════════════════════════════════════════════
    //  BOUNCE SCROLL — reliability fallback
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Scrolls up 1000px and then back down to force-refresh the DOM.
    /// Used when fewer than 5 items were captured on the first pass.
    /// </summary>
    private async Task BounceScrollAsync(IPage page)
    {
        _logger.LogDebug("BounceScroll: scrolling up 1000px");
        await page.EvaluateAsync("window.scrollBy(0, -1000)");
        await page.WaitForTimeoutAsync(800);

        _logger.LogDebug("BounceScroll: scrolling back down 1000px");
        await page.EvaluateAsync("window.scrollBy(0, 1000)");
        await page.WaitForTimeoutAsync(1000);

        // Wait for any lazy content triggered by the bounce
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 5_000 });
        }
        catch (TimeoutException)
        {
            // Acceptable — some elements may never finish loading
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DOM PARSING — extract data from each .EntityList-item
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Parse a single .EntityList-item element into a ScrapedListing.
    /// Returns null if the element has no usable data.
    /// </summary>
    private async Task<ScrapedListing?> ParseListingElement(
        IElementHandle item, IPage page)
    {
        try
        {
            // Check if sponsored BEFORE extracting data (tag it, don't skip)
            var className = await item.GetAttributeAsync("class") ?? "";
            bool isSponsored = className.Contains("VauVau", StringComparison.OrdinalIgnoreCase)
                            || className.Contains("Adsense", StringComparison.OrdinalIgnoreCase)
                            || className.Contains("Banner", StringComparison.OrdinalIgnoreCase)
                            || className.Contains("sponsored", StringComparison.OrdinalIgnoreCase);

            // Title + URL
            var linkEl = await item.QuerySelectorAsync("h3 a, .entity-title a, .EntityList-item--Regular__name a, a.entity-description-title");
            string title = "";
            string url = "";

            if (linkEl is not null)
            {
                title = (await linkEl.InnerTextAsync()).Trim();
                var href = await linkEl.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    url = href.StartsWith("http")
                        ? href
                        : $"https://www.njuskalo.hr{href}";
                }
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(url))
                return null;

            // Price
            var priceEl = await item.QuerySelectorAsync(".price, .entity-price, .EntityList-item--Regular__price");
            string priceText = "";
            if (priceEl is not null)
            {
                priceText = (await priceEl.InnerTextAsync()).Trim();
            }

            // Location
            var locEl = await item.QuerySelectorAsync(".entity-location, .EntityList-item--Regular__location, .entity-description-main");
            string location = "";
            if (locEl is not null)
            {
                location = (await locEl.InnerTextAsync()).Trim();
            }

            // Description / extra info
            var descEl = await item.QuerySelectorAsync(".entity-description, .EntityList-item--Regular__description");
            string description = "";
            if (descEl is not null)
            {
                description = (await descEl.InnerTextAsync()).Trim();
            }

            // Full text content for downstream pipeline
            var fullText = await item.InnerTextAsync();

            return new ScrapedListing
            {
                Title = title,
                Url = url,
                PriceText = priceText,
                Location = location,
                Description = description,
                FullText = fullText,
                IsSponsored = isSponsored
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse listing element");
            return null;
        }
    }
}

// ══════════════════════════════════════════════════════════
//  OUTPUT MODEL
// ══════════════════════════════════════════════════════════

/// <summary>
/// Raw listing data scraped from Njuškalo DOM.
/// </summary>
public class ScrapedListing
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string FullText { get; set; } = "";
    public bool IsSponsored { get; set; }
}
