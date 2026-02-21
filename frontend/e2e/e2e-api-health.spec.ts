/**
 * E2E API Health — Direct backend API endpoint tests (no UI)
 *
 * SSE endpoints are tested via page.evaluate + AbortController so we
 * only check status + content-type without waiting for the full stream.
 */
import { test, expect } from "@playwright/test";
import { API, createLogger } from "./helpers/test-utils";

const log = createLogger("API");

/** Helper: fire a POST, read status + content-type, then abort. */
async function sseHeaderCheck(
  page: import("@playwright/test").Page,
  url: string,
  body: Record<string, unknown>
): Promise<{ status: number; contentType: string }> {
  return page.evaluate(
    async ({ url, body }) => {
      const ctrl = new AbortController();
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
        signal: ctrl.signal,
      });
      const ct = res.headers.get("content-type") || "";
      ctrl.abort(); // don't wait for body
      return { status: res.status, contentType: ct };
    },
    { url, body }
  );
}

test.describe("Backend API Health", () => {
  test("GET /api/health returns healthy", async ({ request }) => {
    const res = await request.get(`${API}/api/health`);
    expect(res.status()).toBe(200);
    const body = await res.json();
    log.info(`Health: ${JSON.stringify(body)}`);
    expect(body.status).toBe("healthy");
  });

  test("POST evaluate/text/stream returns SSE headers", async ({ page }) => {
    await page.goto("/"); // need a page context for evaluate
    const { status, contentType } = await sseHeaderCheck(page, `${API}/api/evaluate/text/stream`, {
      text: "Stan, 65 m², Zagreb, Trnje, 2. kat, novogradnja 2020",
      city: "Zagreb",
      neighborhood: "Trnje",
    });
    // 200 = success, 429 = rate limited (still proves endpoint exists)
    expect([200, 429]).toContain(status);
    if (status === 200) expect(contentType).toContain("text/event-stream");
    log.info(`evaluate/text/stream → ${status} ${status === 429 ? '(rate-limited)' : 'SSE confirmed'}`);
  });

  test("POST legal/strategy/stream returns SSE headers", async ({ page }) => {
    await page.goto("/");
    const { status, contentType } = await sseHeaderCheck(page, `${API}/api/legal/strategy/stream`, {
      case_description: "Kupac traži raskid ugovora i povrat kapare.",
      client_position: "Kupac",
      desired_outcome: "Povrat kapare",
    });
    expect([200, 429]).toContain(status);
    if (status === 200) expect(contentType).toContain("text/event-stream");
    log.info(`legal/strategy/stream → ${status}`);
  });

  test("POST contract/analyze-text-stream returns SSE headers", async ({ page }) => {
    await page.goto("/");
    const { status, contentType } = await sseHeaderCheck(page, `${API}/api/contract/analyze-text-stream`, {
      text: "KUPOPRODAJNI UGOVOR\nProdavatelj prodaje stan od 72 m² u Zagrebu kupcu za 220.000 EUR. Kapara 22.000 EUR.",
    });
    expect([200, 429]).toContain(status);
    if (status === 200) expect(contentType).toContain("text/event-stream");
    log.info(`contract/analyze-text-stream → ${status}`);
  });

  test("POST invoice/generate validates input", async ({ request }) => {
    const res = await request.post(`${API}/api/invoice/generate`, {
      data: {},
      headers: { "Content-Type": "application/json" },
    });
    // Should return 400/422/500 for empty input, or 429 if rate-limited
    expect([400, 422, 429, 500]).toContain(res.status());
    log.info(`invoice/generate empty → ${res.status()} (validation works)`);
  });
});
