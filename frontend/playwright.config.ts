import { defineConfig, devices } from "@playwright/test";

/**
 * ═══════════════════════════════════════════════════════════════
 *  Playwright Config — E2E Procjena Nekretnina
 *
 *  Designed for REAL black-box testing against live backend+AI.
 *  Timeouts are deliberately extremely generous because each
 *  valuation hits OpenRouter AI + Tavily web scraping.
 *
 *  Usage:
 *    npx playwright test                    # headless
 *    npx playwright test --headed           # watch the bot click
 *    npx playwright test --ui               # interactive UI mode
 *    npx playwright test --workers=1        # sequential (save API $)
 * ═══════════════════════════════════════════════════════════════
 */

export default defineConfig({
  // Only look in the e2e/ folder — don't touch vitest __tests__
  testDir: "./e2e",

  // Global timeout per test: 5 minutes (AI can be slow)
  timeout: 5 * 60 * 1000,

  // Expect assertions timeout: 30 seconds
  expect: {
    timeout: 30_000,
  },

  // Run tests sequentially by default to avoid API rate limits
  // Bump to 2-3 if your OpenRouter plan supports parallel calls
  workers: 1,

  // Full parallel would burn through API credits — keep serial
  fullyParallel: false,

  // Retry failed tests once (network hiccups, AI timeouts)
  retries: 1,

  // Reporter: show each step in console + HTML report for review
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "e2e/report" }],
  ],

  // Shared settings for all tests
  use: {
    // Frontend URL — make sure `npm run dev` is running
    baseURL: "http://localhost:3000",

    // Generous navigation timeout
    navigationTimeout: 30_000,

    // Generous action timeout (filling inputs, clicking buttons)
    actionTimeout: 15_000,

    // Take screenshots on failure automatically
    screenshot: "only-on-failure",

    // Record video on first retry (helps debug flaky AI responses)
    video: "on-first-retry",

    // Save traces for failed tests (viewable in trace.playwright.dev)
    trace: "on-first-retry",

    // Browser viewport
    viewport: { width: 1280, height: 900 },
  },

  // Only use Chromium — no need for cross-browser for API validation
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],

  // Auto-start the Next.js dev server if not already running
  // Comment this out if you prefer to start it manually
  webServer: {
    command: "npm run dev",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
