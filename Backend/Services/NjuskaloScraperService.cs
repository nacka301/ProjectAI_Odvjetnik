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

    // Concurrency limiter: max 2 Playwright browsers at once
    // Prevents OOM on Render (each Chromium ~200MB)
    private static readonly SemaphoreSlim _browserSemaphore = new(2, 2);

    // ── Selectors ──
    private const string ListingSelector = ".EntityList-item";
    private const string SponsoredSelector = ".EntityList-item--VauVau, .EntityList-item--Adsense, .EntityList-item--Banner, [class*='sponsored'], [class*='Sponsored'], [class*='adsense'], [class*='Adsense']";

    // ── Scroll tuning ──
    private const int MinScrollPx = 600;
    private const int MaxScrollPx = 1000;
    private const int MinDelayMs  = 300;
    private const int MaxDelayMs  = 700;
    private const int MaxListings = 80;
    private const int MaxPages    = 3;
    private const int MinItemsTarget = 40;

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

        await _browserSemaphore.WaitAsync();
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

            _logger.LogInformation("Playwright: parsed {Total} listings from page 1", allListings.Count);

            // ── 6. Scrape additional pages (page 2, etc.) for more listings ──
            var seenUrls = new HashSet<string>(allListings.Select(l => l.Url), StringComparer.OrdinalIgnoreCase);

            for (int pg = 2; pg <= MaxPages; pg++)
            {
                if (allListings.Count >= MaxListings)
                    break;

                var pageUrl = url.Contains('?')
                    ? $"{url}&page={pg}"
                    : $"{url}?page={pg}";

                _logger.LogInformation("Playwright: navigating to page {Page} → {Url}", pg, pageUrl);

                try
                {
                    await page.GotoAsync(pageUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 20_000
                    });
                    await page.WaitForTimeoutAsync(1200);
                    await SmoothScrollAsync(page);

                    var pgItems = await page.QuerySelectorAllAsync(ListingSelector);
                    _logger.LogInformation("Playwright: page {Page} has {Count} DOM items", pg, pgItems.Count);

                    if (pgItems.Count == 0) break; // no more pages

                    int addedFromPage = 0;
                    foreach (var pgItem in pgItems)
                    {
                        var listing = await ParseListingElement(pgItem, page);
                        if (listing is not null && seenUrls.Add(listing.Url))
                        {
                            allListings.Add(listing);
                            addedFromPage++;
                        }
                    }

                    _logger.LogInformation("Playwright: page {Page} added {Added} new listings (total: {Total})",
                        pg, addedFromPage, allListings.Count);
                }
                catch (Exception pgEx)
                {
                    _logger.LogWarning(pgEx, "Playwright: failed to scrape page {Page}, continuing", pg);
                    break;
                }
            }

            // Filter out sponsored/Adsense AFTER full capture
            var sponsoredCount = allListings.Count(l => l.IsSponsored);
            var organic = allListings.Where(l => !l.IsSponsored).ToList();

            _logger.LogInformation(
                "Playwright: {Organic} organic + {Sponsored} sponsored = {Total} total (across {Pages} pages)",
                organic.Count, sponsoredCount, allListings.Count, Math.Min(MaxPages, 2));

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
            _browserSemaphore.Release();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  SMOOTH SCROLL — human-like incremental scrolling
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Scrolls down in 600-1000px increments with random delays (300-700ms)
    /// to mimic human behavior and trigger lazy loading.
    /// Stops when: bottom reached, or MaxListings items detected, or 80 scrolls done.
    /// </summary>
    private async Task SmoothScrollAsync(IPage page)
    {
        const int maxScrollAttempts = 80;
        int scrollCount = 0;
        double previousHeight = 0;
        int staleCount = 0;

        _logger.LogDebug("SmoothScroll: starting incremental scroll");

        for (int i = 0; i < maxScrollAttempts; i++)
        {
            // Random scroll distance between 400-600px
            int scrollPx = Random.Shared.Next(MinScrollPx, MaxScrollPx + 1);

            await page.EvaluateAsync($"window.scrollBy(0, {scrollPx})");
            scrollCount++;

            // Random human-like delay between scrolls
            int delayMs = Random.Shared.Next(MinDelayMs, MaxDelayMs + 1);
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
                // Only stop on stale if we have enough items or truly stuck (5 cycles)
                if (staleCount >= 3 && itemCount >= MinItemsTarget)
                {
                    _logger.LogDebug("SmoothScroll: page height stable + {Count} items \u2265 {Target} \u2014 done",
                        itemCount, MinItemsTarget);
                    break;
                }
                if (staleCount >= 5)
                {
                    _logger.LogDebug("SmoothScroll: page height stable for 5 cycles \u2014 bottom reached ({Count} items)",
                        itemCount);
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
    //  DEEP DIVE — Visit a single listing's detail page
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to an individual Njuškalo listing page, extract the rich
    /// description, attribute table (year built, energy class, yard size),
    /// and equipment list that are NOT visible on the search results page.
    /// </summary>
    public async Task<ListingDetails?> ScrapeListingDetailsAsync(string url)
    {
        _logger.LogInformation("DeepDive: visiting {Url}", url);

        await _browserSemaphore.WaitAsync();
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

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 20_000
            });

            // Human-like reading delay (2-4 seconds)
            int readDelay = Random.Shared.Next(2000, 4001);
            await page.WaitForTimeoutAsync(readDelay);

            var details = new ListingDetails { Url = url };

            // ── RESILIENT EXTRACTION via JavaScript ──
            // Njuškalo detail pages use dynamic class names that change on redesigns.
            // Instead of fragile CSS selectors, use JS to extract by page structure.

            // ── 1. Full description text ("Opis oglasa" section) ──
            details.Description = await page.EvaluateAsync<string?>(@"
                (() => {
                    // Try known CSS selectors first
                    const selectors = [
                        '.ClassifiedDetail-description',
                        '.classified-detail-description',
                        '.oglas-description',
                        '[class*=""Description""] .description-text',
                        '.entity-body'
                    ];
                    for (const sel of selectors) {
                        const el = document.querySelector(sel);
                        if (el && el.innerText.trim().length > 10) return el.innerText.trim();
                    }

                    // Fallback: find heading 'Opis oglasa' and get next sibling text
                    const headings = [...document.querySelectorAll('h2, h3, h4')];
                    const opisH = headings.find(h => h.innerText.trim().toLowerCase().includes('opis oglasa'));
                    if (opisH) {
                        let sibling = opisH.nextElementSibling;
                        while (sibling) {
                            const txt = sibling.innerText?.trim();
                            if (txt && txt.length > 10 && !sibling.matches('h2, h3, h4')) return txt;
                            if (sibling.matches('h2')) break;
                            sibling = sibling.nextElementSibling;
                        }
                    }

                    // Last resort: grab largest paragraph-like block
                    const paras = [...document.querySelectorAll('p, div')]
                        .filter(el => el.innerText.trim().length > 50 && el.children.length < 3);
                    if (paras.length > 0) {
                        paras.sort((a, b) => b.innerText.length - a.innerText.length);
                        return paras[0].innerText.trim();
                    }
                    return null;
                })()
            ");

            // ── 2. Attribute table (key-value pairs from "Osnovne informacije") ──
            var attrJson = await page.EvaluateAsync<string>(@"
                (() => {
                    const attrs = {};

                    // Strategy 1: <li> with <strong>/<b> key
                    document.querySelectorAll('li').forEach(li => {
                        const strong = li.querySelector('strong, b');
                        if (strong) {
                            const key = strong.innerText.replace(/[:\s]+$/, '').trim();
                            const fullText = li.innerText.trim();
                            const value = fullText.substring(strong.innerText.length).replace(/^[:\s]+/, '').trim();
                            if (key && value && key.length < 60) attrs[key] = value;
                        }
                    });

                    // Strategy 2: <tr> with <th> key and <td> value
                    document.querySelectorAll('tr').forEach(tr => {
                        const th = tr.querySelector('th');
                        const td = tr.querySelector('td');
                        if (th && td) {
                            const key = th.innerText.trim();
                            const value = td.innerText.trim();
                            if (key && value && key.length < 60) attrs[key] = value;
                        }
                    });

                    // Strategy 3: <dt>/<dd> pairs
                    document.querySelectorAll('dt').forEach(dt => {
                        const dd = dt.nextElementSibling;
                        if (dd && dd.tagName === 'DD') {
                            const key = dt.innerText.trim();
                            const value = dd.innerText.trim();
                            if (key && value) attrs[key] = value;
                        }
                    });

                    return JSON.stringify(attrs);
                })()
            ");

            try
            {
                var parsedAttrs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(attrJson);
                if (parsedAttrs is not null)
                {
                    foreach (var (key, value) in parsedAttrs)
                    {
                        details.RawAttributes[key] = value;

                        var lk = key.ToLowerInvariant();
                        if (lk.Contains("godina izgradnje") || lk.Contains("god. izgradnje"))
                            details.YearBuilt = ParseIntSafe(value);
                        else if (lk.Contains("godina") && lk.Contains("renovac"))
                            details.YearLastRenovation = ParseIntSafe(value);
                        else if (lk.Contains("energetski razred") || lk.Contains("energetski certifikat"))
                            details.EnergyClass = value.Trim();
                        else if (lk.Contains("okućnica") || lk.Contains("okucnica") ||
                                 lk.Contains("površina zemljišta") || lk.Contains("površina parcele"))
                            details.YardAreaM2 = ParseDoubleSafe(value);
                        else if ((lk.Contains("stambena površina") || lk.Contains("korisna površina") ||
                                  lk.Contains("površina")) && !lk.Contains("parcele") && !lk.Contains("zemljišta"))
                            details.SizeM2 = ParseDoubleSafe(value);
                        else if (lk.Contains("grijanje") || lk.Contains("heating"))
                            details.HeatingType = value.Trim();
                        else if (lk.Contains("kat") || lk.Contains("etaža"))
                            details.Floor = value.Trim();
                    }
                }
            }
            catch { /* JSON parse failed — attributes remain empty */ }

            // ── 3. Equipment / features ("Dodatne informacije" section) ──
            var equipJson = await page.EvaluateAsync<string>(@"
                (() => {
                    const items = [];

                    // Strategy 1: Known CSS selectors
                    const selectors = [
                        '.ClassifiedDetail-features li',
                        '.classified-detail-features li',
                        '.ClassifiedDetail-featuresItem',
                        '.features-list li',
                        '.equipment-list li'
                    ];
                    for (const sel of selectors) {
                        document.querySelectorAll(sel).forEach(el => {
                            const t = el.innerText.trim();
                            if (t && t.length > 1 && t.length < 200) items.push(t);
                        });
                    }
                    if (items.length > 0) return JSON.stringify(items);

                    // Strategy 2: Find 'Dodatne informacije' heading and grab all <li> after it
                    const headings = [...document.querySelectorAll('h2, h3, h4')];
                    const addH = headings.find(h => h.innerText.trim().toLowerCase().includes('dodatne informacije'));
                    if (addH) {
                        let sibling = addH.nextElementSibling;
                        while (sibling && !sibling.matches('h2')) {
                            sibling.querySelectorAll('li').forEach(li => {
                                const t = li.innerText.trim();
                                if (t && t.length > 1 && t.length < 200) items.push(t);
                            });
                            // Also grab subsection headings as context (e.g. 'Grijanje')
                            if (sibling.matches('h3, h4')) {
                                const hText = sibling.innerText.trim();
                                if (hText) items.push('[' + hText + ']');
                            }
                            sibling = sibling.nextElementSibling;
                        }
                    }

                    return JSON.stringify(items);
                })()
            ");

            try
            {
                var parsedEquip = System.Text.Json.JsonSerializer.Deserialize<List<string>>(equipJson);
                if (parsedEquip is not null)
                    details.Equipment = parsedEquip;
            }
            catch { /* JSON parse failed — equipment remains empty */ }

            // ── 4. Price from detail page ──
            var priceText = await page.EvaluateAsync<string?>(@"
                (() => {
                    // Known selectors
                    const selectors = [
                        '.ClassifiedDetail-price',
                        '.classified-detail-price',
                        '.entity-price',
                        '[class*=""price""]'
                    ];
                    for (const sel of selectors) {
                        const el = document.querySelector(sel);
                        if (el) {
                            const t = el.innerText.trim();
                            if (t.match(/\d/) && t.includes('€')) return t;
                        }
                    }

                    // Fallback: find 'Cijena' label
                    const labels = [...document.querySelectorAll('strong, b, dt, th')];
                    const priceLabel = labels.find(l => l.innerText.trim().toLowerCase().includes('cijena'));
                    if (priceLabel) {
                        const parent = priceLabel.closest('li, tr, div, dl');
                        if (parent) {
                            const t = parent.innerText.replace(priceLabel.innerText, '').trim();
                            if (t.match(/\d/)) return t;
                        }
                    }
                    return null;
                })()
            ");
            if (!string.IsNullOrWhiteSpace(priceText))
                details.PriceText = priceText;

            _logger.LogInformation(
                "DeepDive: extracted {AttrCount} attributes, {EquipCount} equipment items, " +
                "desc={DescLen} chars from {Url}",
                details.RawAttributes.Count, details.Equipment.Count,
                details.Description?.Length ?? 0, url);

            return details;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeepDive: failed for {Url}", url);
            return null;
        }
        finally
        {
            if (browser is not null)
                await browser.CloseAsync();
            pw?.Dispose();
            _browserSemaphore.Release();
        }
    }

    // ── Helpers for attribute parsing ──

    private static (string Key, string Value)? ParseAttributePair(string text)
    {
        // Try "Key: Value"
        var colonIdx = text.IndexOf(':');
        if (colonIdx > 0 && colonIdx < text.Length - 1)
            return (text[..colonIdx].Trim(), text[(colonIdx + 1)..].Trim());

        // Try tab-separated
        var tabIdx = text.IndexOf('\t');
        if (tabIdx > 0 && tabIdx < text.Length - 1)
            return (text[..tabIdx].Trim(), text[(tabIdx + 1)..].Trim());

        // Try newline-separated (some layouts)
        var parts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return null;
    }

    private static int? ParseIntSafe(string s)
    {
        // Extract first 4-digit number (year-like)
        var m = Regex.Match(s, @"(\d{4})");
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }

    private static double? ParseDoubleSafe(string s)
    {
        // Extract number before "m²" or just the first number
        var m = Regex.Match(s, @"([\d.,]+)\s*m");
        if (!m.Success)
            m = Regex.Match(s, @"([\d.,]+)");
        if (!m.Success) return null;

        var raw = m.Groups[1].Value.Replace(".", "").Replace(",", ".");
        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
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

/// <summary>
/// Rich data extracted from an individual Njuškalo listing detail page.
/// </summary>
public class ListingDetails
{
    public string Url { get; set; } = "";
    public string? Description { get; set; }
    public string? PriceText { get; set; }
    public int? YearBuilt { get; set; }
    public int? YearLastRenovation { get; set; }
    public string? EnergyClass { get; set; }
    public double? YardAreaM2 { get; set; }
    public double? SizeM2 { get; set; }
    public string? HeatingType { get; set; }
    public string? Floor { get; set; }
    public List<string> Equipment { get; set; } = [];
    public Dictionary<string, string> RawAttributes { get; set; } = [];

    /// <summary>
    /// Build a rich text block combining all extracted data for the AI/Engine pipeline.
    /// </summary>
    public string ToEnrichedText()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Description))
            parts.Add($"Opis: {Description}");
        if (YearBuilt.HasValue)
            parts.Add($"Godina izgradnje: {YearBuilt}");
        if (YearLastRenovation.HasValue)
            parts.Add($"Godina zadnje renovacije: {YearLastRenovation}");
        if (!string.IsNullOrWhiteSpace(EnergyClass))
            parts.Add($"Energetski razred: {EnergyClass}");
        if (YardAreaM2.HasValue && YardAreaM2 > 0)
            parts.Add($"Površina okućnice: {YardAreaM2:0} m²");
        if (SizeM2.HasValue && SizeM2 > 0)
            parts.Add($"Površina: {SizeM2:0} m²");
        if (!string.IsNullOrWhiteSpace(HeatingType))
            parts.Add($"Grijanje: {HeatingType}");
        if (!string.IsNullOrWhiteSpace(Floor))
            parts.Add($"Kat: {Floor}");
        if (Equipment.Count > 0)
            parts.Add($"Oprema: {string.Join(", ", Equipment)}");

        // Append any remaining attributes not already captured
        foreach (var kv in RawAttributes)
        {
            var already = parts.Any(p => p.Contains(kv.Value, StringComparison.OrdinalIgnoreCase));
            if (!already)
                parts.Add($"{kv.Key}: {kv.Value}");
        }

        return string.Join(". ", parts);
    }
}
