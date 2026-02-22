/**
 * E2E Navigation — Homepage + all routes smoke test
 */
import { test, expect } from "@playwright/test";

test.describe("Navigation & Routes", () => {
  test("homepage loads with title", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveTitle(/PravoTek/i);
    await expect(page.locator("body")).toBeVisible();
  });

  test("all routes load without 500 errors", async ({ page }) => {
    const routes = [
      "/",
      "/wizard",
      "/ugovor",
      "/zk-procjena",
      "/valuation",
      "/history",
    ];

    for (const route of routes) {
      const res = await page.goto(route);
      expect(res?.status(), `${route} should not 500`).not.toBe(500);
      await expect(page.locator("body")).toBeVisible();
    }
  });
});
