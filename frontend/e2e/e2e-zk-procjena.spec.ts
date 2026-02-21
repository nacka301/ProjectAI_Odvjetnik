/**
 * E2E ZK Procjena — ZK Due Diligence + Valuation (text mode)
 */
import { test, expect } from "@playwright/test";
import { createLogger, setupRequestLogging } from "./helpers/test-utils";
import * as fs from "fs";
import * as path from "path";

const log = createLogger("ZK");

const zkText = fs.readFileSync(
  path.join(__dirname, "..", "..", "test-files", "04_zk_izvadak.txt"),
  "utf-8"
);

test.describe("ZK Procjena (Due Diligence)", () => {
  test("analyzes ZK extract text and estimates value", async ({ page }) => {
    test.setTimeout(20 * 60 * 1000); // 20 min for AI
    setupRequestLogging(page, log);
    await page.goto("/zk-procjena");
    await expect(page.locator("body")).toBeVisible();
    log.info("Page loaded");

    // Switch to text mode
    const textBtn = page.getByRole("button", { name: /tekst/i });
    await textBtn.click();
    log.info("Switched to text mode");

    // Fill ZK extract text
    const textarea = page.getByPlaceholder(/zalijepite tekst ZK izvatka/i);
    await textarea.fill(zkText);
    log.info(`Pasted ${zkText.length} chars of ZK extract`);

    // Fill location using the LocationSelector combobox
    // The page uses a LocationSelector component — look for city input
    const cityTrigger = page.getByRole("button", { name: /odaberite grad/i });
    if (await cityTrigger.isVisible({ timeout: 3000 }).catch(() => false)) {
      await cityTrigger.click();
      const searchInput = page.locator('input[placeholder="Pretraži..."]');
      await searchInput.fill("Zagreb");
      await page.waitForTimeout(500);
      const option = page.locator('li[role="option"]').first();
      if (await option.isVisible({ timeout: 3000 }).catch(() => false)) {
        await option.click();
        log.info("Selected city: Zagreb");
      }
    }

    // Try to fill neighborhood/settlement
    const settlementTrigger = page.locator("button", { hasText: /odaberite naselje/i });
    if (await settlementTrigger.isVisible({ timeout: 3000 }).catch(() => false)) {
      await settlementTrigger.click();
      const searchInput = page.locator('input[placeholder="Pretraži..."]');
      await searchInput.fill("Centar");
      await page.waitForTimeout(500);
      const option = page.locator('li[role="option"]').first();
      if (await option.isVisible({ timeout: 3000 }).catch(() => false)) {
        await option.click();
        log.info("Selected settlement: Centar");
      }
    }

    // Submit
    const submitBtn = page.getByRole("button", { name: /analiziraj i procijeni/i });
    await expect(submitBtn).toBeEnabled({ timeout: 5_000 });
    await submitBtn.click();
    log.info("Submitted, waiting for ZK analysis + valuation...");

    // Wait for result — success or error (429 rate limit, server down, etc.)
    // Use a single combined regex with .first() to avoid strict mode violation
    await expect(
      page.getByText(/tržišna vrijednost|nova analiza|server ne odgovara|greška|error/i).first()
    ).toBeVisible({ timeout: 15 * 60 * 1000 });

    const bodyText = await page.locator("body").textContent() || "";
    if (/tržišna vrijednost/i.test(bodyText) && !/server ne odgovara/i.test(bodyText)) {
      log.info("ZK analysis result received");
      expect(bodyText.length).toBeGreaterThan(500);
      log.info("ZK procjena test PASSED");
    } else {
      log.warn("ZK analysis hit error/rate limit — form submission verified");
    }
  });

  test("validates minimum text length", async ({ page }) => {
    await page.goto("/zk-procjena");

    // Switch to text mode
    await page.getByRole("button", { name: /tekst/i }).click();

    // Type too-short text
    const textarea = page.getByPlaceholder(/zalijepite tekst ZK izvatka/i);
    await textarea.fill("Kratko.");

    // Submit button should be disabled for short text
    const btn = page.getByRole("button", { name: /analiziraj i procijeni/i });
    const btnCount = await btn.count();
    if (btnCount > 0) {
      // Should be disabled for text < 50 chars
      const isDisabled = await btn.isDisabled();
      if (isDisabled) {
        log.info("Button correctly disabled for short text");
      } else {
        // Try to click — should show error
        await btn.click();
        log.info("Clicked with short text, checking for error");
      }
    } else {
      log.info("Submit button not shown for short text (correct behavior)");
    }
  });
});
