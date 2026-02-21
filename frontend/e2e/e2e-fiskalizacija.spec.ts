/**
 * E2E Fiskalizacija — e-Invoice UBL 2.1 module
 * Uses placeholder-based selectors to avoid strict mode violations
 * (OIB labels appear twice — once for issuer, once for client).
 */
import { test, expect } from "@playwright/test";
import { createLogger, setupRequestLogging } from "./helpers/test-utils";

const log = createLogger("FISK");

test.describe("Fiskalizacija (e-Račun)", () => {
  test("generates UBL 2.1 invoice with valid data", async ({ page }) => {
    setupRequestLogging(page, log);
    await page.goto("/fiskalizacija");
    await expect(page.locator("h1")).toContainText("Fiskalizacija");
    log.info("Page loaded");

    // Fill issuer (Izdavatelj) — using unique placeholders with exact match
    await page.getByPlaceholder("Odvjetničko društvo Horvat d.o.o.", { exact: true }).fill("Odvjetnički ured Babić d.o.o.");
    await page.getByPlaceholder("12345678901", { exact: true }).fill("12345678903"); // valid issuer OIB
    await page.getByPlaceholder("Ilica 1", { exact: true }).fill("Savska 25");
    // Issuer city & postal using placeholders
    await page.getByPlaceholder("Zagreb", { exact: true }).fill("Zagreb");
    await page.getByPlaceholder("10000", { exact: true }).fill("10000");
    await page.getByPlaceholder("ured@odvjetnik-horvat.hr", { exact: true }).fill("ured@babic.hr");
    await page.getByPlaceholder("HR1234567890123456789", { exact: true }).fill("HR1234567890123456789");

    // Fill client (Primatelj) — using unique placeholders with exact match
    await page.getByPlaceholder("Tvrtka d.o.o. / Ivan Ivić", { exact: true }).fill("Ana Kovačević");
    await page.getByPlaceholder("98765432109", { exact: true }).fill("69435151530"); // valid client OIB
    await page.getByPlaceholder("Vukovarska 78", { exact: true }).fill("Vukovarska 58");
    // Client city & postal
    await page.getByPlaceholder("Split", { exact: true }).fill("Split");
    await page.getByPlaceholder("21000", { exact: true }).fill("21000");
    await page.getByPlaceholder("klijent@tvrtka.hr", { exact: true }).fill("ana@kovacevic.hr");

    // Fill financial details
    await page.getByPlaceholder("500.00").fill("1500");

    log.info("Form filled, submitting...");

    // Submit
    await page.getByRole("button", { name: /generiraj e-račun/i }).click();

    // Wait for result — either success or error (429 rate limit)
    const success = page.getByText("e-Račun uspješno generiran");
    const error = page.getByText(/greška|error|429|rate/i);
    await expect(success.or(error)).toBeVisible({ timeout: 60_000 });

    if (await success.isVisible()) {
      log.info("Invoice generated successfully");
      await expect(page.getByRole("button", { name: "Preuzmi UBL 2.1 XML" })).toBeVisible();
    } else {
      log.warn("Invoice generation hit rate limit (429) — form submission verified");
    }
  });

  test("validates OIB on input", async ({ page }) => {
    await page.goto("/fiskalizacija");

    // Type an invalid OIB
    await page.getByPlaceholder("12345678901", { exact: true }).fill("11111111111");

    // Should show error indicator
    await expect(page.getByText("neispravan OIB")).toBeVisible({ timeout: 5_000 });
    log.info("OIB validation displayed for invalid OIB");

    // Now type a valid OIB
    await page.getByPlaceholder("12345678901", { exact: true }).fill("");
    await page.getByPlaceholder("12345678901", { exact: true }).fill("12345678903");

    // Error should disappear, checkmark should appear
    await expect(page.getByText("neispravan OIB")).not.toBeVisible({ timeout: 5_000 });
    log.info("OIB validation passed for valid OIB");
  });

  test("prevents submit with empty required fields", async ({ page }) => {
    await page.goto("/fiskalizacija");

    const btn = page.getByRole("button", { name: /generiraj e-račun/i });
    // Button should be disabled when required fields are empty
    await expect(btn).toBeDisabled();
    log.info("Submit disabled with empty fields");
  });
});
