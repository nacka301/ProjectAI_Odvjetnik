/**
 * E2E Kronologija — Chronology module (PDF-only multi-file upload)
 * Since this page only supports PDF upload (not text), we test:
 * 1. Page loads correctly
 * 2. Submit button is disabled with no files
 * 3. UI elements are present
 */
import { test, expect } from "@playwright/test";
import { createLogger } from "./helpers/test-utils";

const log = createLogger("KRONO");

test.describe("Kronologija (Chronology)", () => {
  test("page loads with correct elements", async ({ page }) => {
    await page.goto("/kronologija");
    await expect(page.locator("body")).toBeVisible();
    log.info("Page loaded");

    // Check heading
    await expect(page.getByText(/kronologija spisa/i).first()).toBeVisible();

    // Check drop zone text
    await expect(page.getByText(/povucite cijeli registrator/i)).toBeVisible();

    log.info("All UI elements verified");
  });

  test("submit button is disabled without files", async ({ page }) => {
    await page.goto("/kronologija");

    // Look for the submit button — it should be disabled until files are added
    const btn = page.getByRole("button", { name: /generiraj kronologiju/i });
    // Button may not exist or be disabled when no files are present
    const btnCount = await btn.count();
    if (btnCount > 0) {
      await expect(btn).toBeDisabled();
      log.info("Submit button correctly disabled without files");
    } else {
      // Button doesn't appear until files are selected — also valid
      log.info("Submit button not shown without files (correct behavior)");
    }
  });
});
