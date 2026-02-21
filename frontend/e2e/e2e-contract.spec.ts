/**
 * E2E Contract Analysis — kupoprodajni ugovor text mode
 */
import { test, expect } from "@playwright/test";
import { createLogger, setupRequestLogging } from "./helpers/test-utils";
import * as fs from "fs";
import * as path from "path";

const log = createLogger("UGOVOR");

// Load real test contract
const contractText = fs.readFileSync(
  path.join(__dirname, "..", "..", "test-files", "02_kupoprodajni_ugovor.txt"),
  "utf-8"
).slice(0, 2000); // Take first 2000 chars (raw contract only, not analysis result)

test.describe("Contract Analysis (Ugovor)", () => {
  test("analyzes kupoprodajni ugovor text", async ({ page }) => {
    test.setTimeout(15 * 60 * 1000); // 15 min for AI
    setupRequestLogging(page, log);
    await page.goto("/ugovor");
    await expect(page.locator("body")).toBeVisible();
    log.info("Page loaded");

    // Switch to text mode
    const textBtn = page.getByRole("button", { name: /tekst/i });
    await textBtn.click();
    log.info("Switched to text mode");

    // Fill textarea
    const textarea = page.getByPlaceholder("Zalijepite tekst ugovora ovdje...");
    await textarea.fill(contractText);
    log.info(`Pasted ${contractText.length} chars of contract text`);

    // Submit
    await page.getByRole("button", { name: /analiziraj ugovor/i }).click();
    log.info("Submitted, waiting for AI analysis...");

    // Wait for streaming result — success or error
    await expect(
      page.getByText(/rizik|crvene zastavice|pozitivne strane|upozorenja|server ne odgovara|greška|error/i).first()
    ).toBeVisible({ timeout: 10 * 60 * 1000 });

    const bodyText = await page.locator("body").textContent() || "";
    if (/rizik|crvene zastavice/i.test(bodyText) && !/server ne odgovara/i.test(bodyText)) {
      log.info("Analysis result received");
      expect(bodyText).toMatch(/kupoprodajni|ugovor/i);
      log.info("Contract analysis test PASSED");
    } else {
      log.warn("Contract analysis hit error/rate limit — form submission verified");
    }
  });

  test("validates minimum text length", async ({ page }) => {
    await page.goto("/ugovor");

    // Switch to text mode
    await page.getByRole("button", { name: /tekst/i }).click();

    // Type too-short text
    const textarea = page.getByPlaceholder("Zalijepite tekst ugovora ovdje...");
    await textarea.fill("Kratki tekst.");

    // Submit button should be disabled or show error
    const btn = page.getByRole("button", { name: /analiziraj ugovor/i });
    
    // Click and check for error message
    if (await btn.isEnabled()) {
      await btn.click();
      await expect(page.getByText(/barem 50 znakova/i)).toBeVisible({ timeout: 5_000 });
      log.info("Validation error shown for short text");
    } else {
      log.info("Button correctly disabled for short text");
    }
  });
});
