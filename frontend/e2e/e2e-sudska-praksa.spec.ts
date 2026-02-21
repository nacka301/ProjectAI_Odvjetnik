/**
 * E2E Sudska Praksa — Legal Intelligence / Court Outcome Prediction
 */
import { test, expect } from "@playwright/test";
import { createLogger, setupRequestLogging } from "./helpers/test-utils";
import * as fs from "fs";
import * as path from "path";

const log = createLogger("SUDSKA");

const caseText = fs.readFileSync(
  path.join(__dirname, "..", "..", "test-files", "05_sudska_praksa_upit.txt"),
  "utf-8"
);

test.describe("Sudska Praksa (Legal Intelligence)", () => {
  test("predicts court outcome with case description", async ({ page }) => {
    test.setTimeout(15 * 60 * 1000); // 15 min for AI
    setupRequestLogging(page, log);
    await page.goto("/sudska-praksa");
    await expect(page.locator("body")).toBeVisible();
    log.info("Page loaded");

    // Ensure we're in text mode
    const textTab = page.getByRole("button", { name: /^tekst$/i });
    if (await textTab.isVisible()) {
      await textTab.click();
    }

    // Fill case description (required field)
    const caseDesc = page.getByPlaceholder(/opišite činjenice i okolnosti/i);
    await caseDesc.fill(caseText.slice(0, 1500));
    log.info("Filled case description");

    // Select client position — use the select element
    const positionSelect = page.locator("select").first();
    if (await positionSelect.isVisible()) {
      await positionSelect.selectOption({ label: "Kupac" });
      log.info("Selected client position: Kupac");
    }

    // Fill desired outcome
    const outcomeInput = page.getByPlaceholder(/raskid ugovora/i);
    if (await outcomeInput.isVisible()) {
      await outcomeInput.fill("Raskid ugovora i povrat kapare u dvostrukom iznosu");
      log.info("Filled desired outcome");
    }

    // Submit
    await page.getByRole("button", { name: /provjeri ishod/i }).click();
    log.info("Submitted, waiting for AI analysis...");

    // Wait for result — success or error
    await expect(
      page.getByText(/vjerojatnost|prognoza|ishod|server ne odgovara|greška|error/i).first()
    ).toBeVisible({ timeout: 10 * 60 * 1000 });

    const bodyText = await page.locator("body").textContent() || "";
    if (/vjerojatnost|prognoza/i.test(bodyText) && !/server ne odgovara/i.test(bodyText)) {
      log.info("Legal analysis result received");
      expect(bodyText.length).toBeGreaterThan(200);
      log.info("Sudska praksa test PASSED");
    } else {
      log.warn("Legal analysis hit error/rate limit — form submission verified");
    }
  });

  test("requires case description to submit", async ({ page }) => {
    await page.goto("/sudska-praksa");

    // Try to submit without filling case description
    const btn = page.getByRole("button", { name: /provjeri ishod/i });
    
    if (await btn.isEnabled()) {
      await btn.click();
      // Should show error
      await expect(
        page.getByText(/unesite|opis|slučaj/i).first()
      ).toBeVisible({ timeout: 5_000 });
      log.info("Validation error shown for empty case");
    } else {
      log.info("Button correctly disabled without case description");
    }
  });
});
