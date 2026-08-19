/**
 * Shared E2E test utilities for PravoTek.
 */
import { type Page } from "@playwright/test";

export const API = "http://localhost:5078";
export const APP = "http://localhost:3000";

/**
 * Modules still in development are reachable only by the ADMIN_EMAIL account —
 * the route redirects and the API answers 403 for everyone else (see
 * `frontend/lib/modules.ts` and `Backend/Middleware/AdminGuardMiddleware.cs`).
 *
 * So the specs covering those modules are skipped unless the run is signed in
 * as that account. Set E2E_ADMIN=1 when it is. They are skipped rather than
 * deleted because they still pass, and still matter, for the admin.
 */
export const LOCKED_MODULE_E2E_DISABLED = !process.env.E2E_ADMIN;

export const LOCKED_MODULE_E2E_REASON =
  "Modul je u razvoju — dostupan samo adminu. Postavi E2E_ADMIN=1 uz admin sesiju.";

/** Simple console logger with timestamps. */
export function createLogger(prefix: string) {
  return {
    info: (msg: string) => console.log(`[${prefix}] ${new Date().toISOString().slice(11, 19)} ${msg}`),
    warn: (msg: string) => console.warn(`[${prefix}] ⚠ ${msg}`),
    error: (msg: string) => console.error(`[${prefix}] ✖ ${msg}`),
  };
}

/** Attach request/response logging to a Playwright page. */
export function setupRequestLogging(page: Page, log: ReturnType<typeof createLogger>) {
  page.on("request", (req) => {
    if (req.url().includes("/api/")) {
      log.info(`→ ${req.method()} ${req.url().replace(API, "").replace(APP, "")}`);
    }
  });
  page.on("response", (res) => {
    if (res.url().includes("/api/")) {
      log.info(`← ${res.status()} ${res.url().replace(API, "").replace(APP, "")}`);
    }
  });
}

/** OIB ISO 7064 MOD 11,10 validator. */
export function isValidOIB(oib: string): boolean {
  if (!oib || oib.length !== 11 || !/^\d{11}$/.test(oib)) return false;
  let r = 10;
  for (let i = 0; i < 10; i++) {
    r = (r + parseInt(oib[i])) % 10;
    if (r === 0) r = 10;
    r = (r * 2) % 11;
  }
  return (11 - r) % 10 === parseInt(oib[10]);
}

/**
 * Wait for SSE streaming result by polling for a result element.
 * Returns when the selector appears or timeout expires.
 */
export async function waitForStreamingResult(
  page: Page,
  selector: string,
  timeoutMs = 10 * 60 * 1000, // 10 min default
  pollMs = 3000
): Promise<boolean> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const el = await page.$(selector);
    if (el) return true;
    await page.waitForTimeout(pollMs);
  }
  return false;
}
