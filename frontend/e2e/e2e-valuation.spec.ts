/**
 * ═══════════════════════════════════════════════════════════════════════════
 *  E2E PROCJENA NEKRETNINA — Real Black-Box Test
 *
 *  Ova skripta se ponaša kao PRAVI KORISNIK:
 *    1. Otvara wizard na http://localhost:3000/wizard
 *    2. Odabire tip nekretnine (Stan / Kuća / Zemljište)
 *    3. Bira lokaciju iz combobox-a (grad + naselje)
 *    4. Ispunjava sve detalje (m², sobe, kat, stanje, godina...)
 *    5. Klikne "Validiraj i dalje" → čeka draft
 *    6. Klikne "Pokreni procjenu" → čeka SSE streaming do rezultata
 *    7. Čita cijenu s ekrana i provjerava je li u realnom rasponu
 *
 *  AKO PADNE: screenshot + nastavlja dalje (ne ruši cijeli suite)
 *  TIMEOUT: do 14 minuta po procjeni (pravi AI + web scraping)
 *
 *  Pokreni:  npx playwright test e2e/e2e-valuation.spec.ts --headed
 * ═══════════════════════════════════════════════════════════════════════════
 */

import { test, expect, type Page } from "@playwright/test";
import * as path from "path";
import * as fs from "fs";

// ── Load JSON scenarios ──────────────────────────────────────────────

interface Scenario {
  id: string;
  name: string;
  propertyType: "Apartment" | "House" | "Land";
  city: string;
  settlement?: string;
  notes?: string;
  expectedMinPrice: number;
  expectedMaxPrice: number;

  // Apartment fields
  area_m2?: number;
  room_count?: number;
  floor?: string;
  total_floors?: number;
  year_built?: number;
  distance_to_sea_m?: number;
  condition?: string;
  orientation?: string;
  view_type?: string;
  has_elevator?: boolean;
  has_parking?: boolean;
  has_balcony?: boolean;
  sea_view?: boolean;

  // House fields
  living_area_m2?: number;
  land_area_m2?: number;
  floors?: string;
  has_garage?: boolean;
  has_pool?: boolean;
  first_row_to_sea?: boolean;
  has_auxiliary_object?: boolean;

  // Land fields
  land_use?: string;
  has_access_road?: boolean;
  has_infrastructure?: boolean;
  is_legalized?: boolean;
}

const fixturesPath = path.join(__dirname, "fixtures", "croatia-locations.json");
const scenarios: Scenario[] = JSON.parse(fs.readFileSync(fixturesPath, "utf-8"));

const FAILURE_DIR = path.join(__dirname, "..", "test-failures");
if (!fs.existsSync(FAILURE_DIR)) {
  fs.mkdirSync(FAILURE_DIR, { recursive: true });
}

// ── Helper: Parse Croatian-formatted price "125.000" → 125000 ────────

function parseCroatianPrice(text: string): number {
  // Remove everything except digits, dots and commas
  const cleaned = text.replace(/[^0-9.,]/g, "");
  // Croatian format: 125.000 (dot as thousands separator)
  // Replace dots (thousands) and handle comma (decimal)
  const parts = cleaned.split(",");
  const integerPart = parts[0].replace(/\./g, "");
  return parseInt(integerPart, 10);
}

// ── Helper: Screenshot on failure ────────────────────────────────────

async function screenshotOnFailure(page: Page, scenarioId: string, reason: string) {
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
  const filename = `${scenarioId}_${timestamp}.png`;
  const filepath = path.join(FAILURE_DIR, filename);

  try {
    await page.screenshot({ path: filepath, fullPage: true });
    console.error(`  ❌ [${scenarioId}] ${reason}`);
    console.error(`     Screenshot: ${filepath}`);
  } catch (ssErr) {
    console.error(`  ❌ [${scenarioId}] ${reason} (screenshot failed: ${ssErr})`);
  }
}

// ── Helper: Select a city from the combobox ──────────────────────────
//  DOM structure of each Combobox:
//    <div class="relative">          ← containerRef
//      <label>Grad / Općina *</label>
//      <button>                      ← trigger (shows placeholder or value)
//        <span>Odaberite grad...</span>
//        <ChevronDown />
//      </button>
//      <!-- when open: -->
//      <div class="absolute z-50…">
//        <input placeholder="Pretraži..." />
//        <ul role="listbox">
//          <li role="option">...</li>
//        </ul>
//      </div>
//    </div>

async function selectCity(page: Page, cityName: string) {
  // Click the city combobox trigger — exact match to avoid "Prvo odaberite grad"
  const trigger = page.getByRole('button', { name: 'Odaberite grad...' });
  await trigger.waitFor({ state: "visible", timeout: 10000 });
  await trigger.click();

  // Wait for dropdown to open — search input appears inside
  const searchInput = page.locator('input[placeholder="Pretraži..."]').first();
  await searchInput.waitFor({ state: "visible", timeout: 5000 });
  await searchInput.fill(cityName);

  // Wait for filtered results and click matching option
  const option = page.locator('li[role="option"]').filter({ hasText: cityName }).first();
  await option.waitFor({ state: "visible", timeout: 5000 });
  await option.click();

  // Wait for combobox to close and state to update
  await page.waitForTimeout(500);
}

// ── Helper: Select a settlement from the combobox ────────────────────

async function selectSettlement(page: Page, settlementName: string) {
  // The settlement combobox trigger shows "Odaberite naselje..." when city is selected
  const trigger = page.locator('button', { hasText: 'Odaberite naselje' });
  await trigger.waitFor({ state: "visible", timeout: 5000 });
  await trigger.click();

  // Settlement dropdown search input — use last() in case city dropdown lingers
  const searchInput = page.locator('input[placeholder="Pretraži..."]').last();
  await searchInput.waitFor({ state: "visible", timeout: 5000 });
  await searchInput.fill(settlementName);

  const option = page.locator('li[role="option"]').filter({ hasText: settlementName }).first();
  await option.waitFor({ state: "visible", timeout: 8000 });
  await option.click();

  await page.waitForTimeout(500);
}

// ── Helper: Fill a number input by its label text ────────────────────
//  NumberInput DOM: <div> <label>Površina (m²) *</label> <input type="number"> </div>
//  The label text and input are siblings inside a <div>. We use the label's
//  exact text to find its sibling input via the parent container.

async function fillNumber(page: Page, labelText: string, value: number) {
  if (!value || value === 0) return;
  // Find the specific <label> element, go to its parent <div>, then find the input
  const label = page.locator("label").filter({ hasText: labelText }).first();
  const input = label.locator("..").locator('input[type="number"]');
  await input.waitFor({ state: "visible", timeout: 5000 });
  await input.fill(String(value));
}

// ── Helper: Select a <select> by its label text ──────────────────────
//  SelectInput DOM: <div> <label>Stanje *</label> <select>...</select> </div>
//  Same approach: find specific <label>, go to parent, then find the <select>.

async function selectOption(page: Page, labelText: string, value: string) {
  if (!value || value === "Unknown") return;
  const label = page.locator("label").filter({ hasText: labelText }).first();
  const select = label.locator("..").locator("select");
  await select.waitFor({ state: "visible", timeout: 5000 });
  await select.selectOption(value);
}

// ── Helper: Toggle a checkbox by its label text ──────────────────────
//  Toggle DOM: <label class="flex items-center…"> <input type="checkbox"> <span>Lift</span> </label>

async function toggleCheckbox(page: Page, labelText: string, shouldBeChecked: boolean) {
  if (!shouldBeChecked) return;
  const checkbox = page.locator("label", { hasText: labelText }).locator('input[type="checkbox"]');
  const isChecked = await checkbox.isChecked();
  if (shouldBeChecked !== isChecked) {
    await checkbox.click();
  }
}

// ═══════════════════════════════════════════════════════════════════════
//  TEST SUITE — one test per scenario, continues on failure
// ═══════════════════════════════════════════════════════════════════════

test.describe("E2E Procjena Nekretnina — Real Black-Box", () => {

  // Each test is independent — if one fails, others continue
  test.describe.configure({ mode: "serial" });

  for (const scenario of scenarios) {
    test(`[${scenario.id}] ${scenario.name}`, async ({ page }) => {

      // ┌─────────────────────────────────────────────────────┐
      // │  STEP 1: Open wizard & select property type         │
      // └─────────────────────────────────────────────────────┘

      await test.step("Otvaranje wizard stranice", async () => {
        await page.goto("/wizard", { waitUntil: "networkidle" });
        await page.waitForTimeout(500);
      });

      await test.step(`Odabir tipa: ${scenario.propertyType}`, async () => {
        const typeButtonMap: Record<string, string> = {
          Apartment: "Stan",
          House: "Kuća",
          Land: "Zemljište",
        };
        const btnText = typeButtonMap[scenario.propertyType];
        await page.locator(`button:has-text("${btnText}")`).click();
        await page.waitForTimeout(300);
      });

      // ┌─────────────────────────────────────────────────────┐
      // │  STEP 1b: Select location (city + settlement)       │
      // └─────────────────────────────────────────────────────┘

      await test.step(`Odabir lokacije: ${scenario.city}`, async () => {
        await selectCity(page, scenario.city);
      });

      if (scenario.settlement) {
        await test.step(`Odabir naselja: ${scenario.settlement}`, async () => {
          await selectSettlement(page, scenario.settlement!);
        });
      }

      // Click "Dalje" to go to Step 2
      await test.step("Klik na Dalje (korak 1 → 2)", async () => {
        await page.locator('button:has-text("Dalje")').click();
        await page.waitForTimeout(500);
      });

      // ┌─────────────────────────────────────────────────────┐
      // │  STEP 2: Fill type-specific details                 │
      // └─────────────────────────────────────────────────────┘

      await test.step("Popunjavanje detalja nekretnine", async () => {
        if (scenario.propertyType === "Apartment") {
          await fillNumber(page, "Površina (m²)", scenario.area_m2 || 0);
          await fillNumber(page, "Broj soba", scenario.room_count || 0);

          // Floor select (special component)
          if (scenario.floor) {
            await selectOption(page, "Kat", scenario.floor);
          }
          await fillNumber(page, "Ukupno katova", scenario.total_floors || 0);
          await fillNumber(page, "Godina izgradnje", scenario.year_built || 0);
          await fillNumber(page, "Udaljenost od mora", scenario.distance_to_sea_m || 0);

          if (scenario.condition) {
            await selectOption(page, "Stanje", scenario.condition);
          }
          if (scenario.orientation) {
            await selectOption(page, "Orijentacija", scenario.orientation);
          }
          if (scenario.view_type) {
            await selectOption(page, "Pogled", scenario.view_type);
          }

          // Checkboxes
          await toggleCheckbox(page, "Lift", scenario.has_elevator || false);
          await toggleCheckbox(page, "Parking", scenario.has_parking || false);
          await toggleCheckbox(page, "Balkon", scenario.has_balcony || false);
          await toggleCheckbox(page, "Pogled na more", scenario.sea_view || false);

        } else if (scenario.propertyType === "House") {
          await fillNumber(page, "Stambena površina", scenario.living_area_m2 || 0);
          await fillNumber(page, "Okućnica", scenario.land_area_m2 || 0);
          await fillNumber(page, "Godina izgradnje", scenario.year_built || 0);
          await fillNumber(page, "Udaljenost od mora", scenario.distance_to_sea_m || 0);

          if (scenario.floors) {
            await selectOption(page, "Katnost", scenario.floors);
          }
          if (scenario.condition) {
            await selectOption(page, "Stanje", scenario.condition);
          }
          if (scenario.orientation) {
            await selectOption(page, "Orijentacija", scenario.orientation);
          }
          if (scenario.view_type) {
            await selectOption(page, "Pogled", scenario.view_type);
          }

          await toggleCheckbox(page, "Garaža", scenario.has_garage || false);
          await toggleCheckbox(page, "Bazen", scenario.has_pool || false);
          await toggleCheckbox(page, "1. red do mora", scenario.first_row_to_sea || false);
          await toggleCheckbox(page, "Pomoćni objekt", scenario.has_auxiliary_object || false);

        } else if (scenario.propertyType === "Land") {
          await fillNumber(page, "Površina (m²)", scenario.area_m2 || 0);
          await fillNumber(page, "Udaljenost od mora", scenario.distance_to_sea_m || 0);

          if (scenario.land_use) {
            await selectOption(page, "Namjena", scenario.land_use);
          }

          await toggleCheckbox(page, "Pristupni put", scenario.has_access_road || false);
          await toggleCheckbox(page, "Infrastruktura", scenario.has_infrastructure || false);
          await toggleCheckbox(page, "1. red do mora", scenario.first_row_to_sea || false);

          // "Legalizirano" is checked by default — only uncheck if false
          if (scenario.is_legalized === false) {
            const legalCheckbox = page.locator("label", { hasText: "Legalizirano" }).locator('input[type="checkbox"]');
            const isChecked = await legalCheckbox.isChecked();
            if (isChecked) {
              await legalCheckbox.click();
            }
          }
        }

        // Notes (common for all types)
        if (scenario.notes) {
          const textarea = page.locator("textarea").first();
          await textarea.fill(scenario.notes);
        }
      });

      // ┌─────────────────────────────────────────────────────┐
      // │  STEP 2 → 3: Validate draft                        │
      // └─────────────────────────────────────────────────────┘

      await test.step("Klik na Validiraj i dalje", async () => {
        const validateBtn = page.locator('button:has-text("Validiraj i dalje")');
        await validateBtn.click();

        // Wait for Step 3 to appear (draft validation can take a few seconds)
        // Look for the confirmation heading or the "Pokreni procjenu" button
        await page.locator('text=Potvrda').or(page.locator('button:has-text("Pokreni procjenu")')).first()
          .waitFor({ state: "visible", timeout: 30000 });
      });

      // Check for validation errors that block Step 3
      const hasValidationError = await page.locator("text=❌").first().isVisible().catch(() => false);
      if (hasValidationError) {
        await screenshotOnFailure(page, scenario.id, "Validation error blocked Step 3");
        test.fail(true, `[${scenario.id}] Draft validation returned errors`);
        return;
      }

      // ┌─────────────────────────────────────────────────────┐
      // │  STEP 3: Launch valuation (the real deal)           │
      // └─────────────────────────────────────────────────────┘

      await test.step("Klik na Pokreni procjenu", async () => {
        const launchBtn = page.locator('button:has-text("Pokreni procjenu")');
        await launchBtn.waitFor({ state: "visible", timeout: 5000 });
        await launchBtn.click();
      });

      // ┌─────────────────────────────────────────────────────┐
      // │  WAIT: Processing (SSE stream, AI, scraping)        │
      // │  Up to 14 minutes — real AI work happening          │
      // └─────────────────────────────────────────────────────┘

      let priceText = "";

      await test.step("Čekanje rezultata procjene (do 14 min)", async () => {
        // Wait for the processing overlay to appear
        try {
          await page.locator('text=Procjena u tijeku').waitFor({ state: "visible", timeout: 10000 });
          console.log(`  ⏳ [${scenario.id}] Processing started — waiting for AI result…`);
        } catch {
          // Processing view may have been too fast to catch — proceed
        }

        // ── Live SSE stage monitoring ──
        // Poll the visible stage labels every 5s and log progress to console.
        // This reveals which backend stages complete and which one hangs.
        const stageMonitorInterval = setInterval(async () => {
          try {
            const stageTexts = await page.locator('[data-stage-id]').evaluateAll(
              (els) => els.map(el => {
                const id = el.getAttribute('data-stage-id') || '';
                const status = el.getAttribute('data-stage-status') || '';
                const label = el.querySelector('[data-stage-label]')?.textContent || '';
                return `${id}:${status}`;
              })
            );
            if (stageTexts.length > 0) {
              console.log(`  📡 [${scenario.id}] SSE progress: ${stageTexts.join(' | ')}`);
            }
          } catch { /* page may have navigated */ }
        }, 10_000);

        // Now wait for the RESULT to appear — the hero card with the price
        // This is the critical wait — AI + scraping can take up to 14 minutes
        const resultLabel = page.locator('text=Procijenjena tržišna vrijednost');
        try {
          await resultLabel.waitFor({ state: "visible", timeout: 840_000 }); // 14 minutes
        } catch (timeoutErr) {
          clearInterval(stageMonitorInterval);
          // Check if there's an error message instead
          const errorVisible = await page.locator('text=Pokušaj ponovno').isVisible().catch(() => false);
          if (errorVisible) {
            await screenshotOnFailure(page, scenario.id, "Backend returned error during processing");
            throw new Error(`[${scenario.id}] Procjena failed with backend error`);
          }
          await screenshotOnFailure(page, scenario.id, "Timeout waiting for result (840s)");
          throw new Error(`[${scenario.id}] Timeout — result not shown within 14 minutes`);
        }
        clearInterval(stageMonitorInterval);

        // ┌─────────────────────────────────────────────────────┐
        // │  READ: Extract price from the hero card             │
        // └─────────────────────────────────────────────────────┘

        // The price is in a <p> with class text-5xl inside the hero card
        const priceElement = page.locator("p.text-5xl, p.md\\:text-6xl").first();
        await priceElement.waitFor({ state: "visible", timeout: 5000 });
        priceText = await priceElement.textContent() || "";
      });

      // ┌─────────────────────────────────────────────────────┐
      // │  ASSERT: Price is within expected range              │
      // └─────────────────────────────────────────────────────┘

      await test.step("Provjera cijene u očekivanom rasponu", async () => {
        const price = parseCroatianPrice(priceText);

        console.log(
          `  ✅ [${scenario.id}] ${scenario.name}: ${priceText} → parsed ${price} EUR ` +
          `(expected ${scenario.expectedMinPrice}–${scenario.expectedMaxPrice})`
        );

        // Sanity check: price must be a positive number
        if (isNaN(price) || price <= 0) {
          await screenshotOnFailure(page, scenario.id, `Could not parse price from text: "${priceText}"`);
          throw new Error(`[${scenario.id}] Price parse failed: "${priceText}"`);
        }

        // Main assertion: price within expected range
        if (price < scenario.expectedMinPrice || price > scenario.expectedMaxPrice) {
          await screenshotOnFailure(
            page,
            scenario.id,
            `Price ${price} EUR outside range [${scenario.expectedMinPrice}–${scenario.expectedMaxPrice}]`
          );
          expect(
            price,
            `[${scenario.id}] ${scenario.name}: cijena ${price} EUR izvan raspona ` +
            `[${scenario.expectedMinPrice}–${scenario.expectedMaxPrice}]`
          ).toBeGreaterThanOrEqual(scenario.expectedMinPrice);
          expect(
            price,
            `[${scenario.id}] ${scenario.name}: cijena ${price} EUR iznad maksimuma ${scenario.expectedMaxPrice}`
          ).toBeLessThanOrEqual(scenario.expectedMaxPrice);
        }
      });

    }); // end test for this scenario
  } // end for-loop

}); // end describe
