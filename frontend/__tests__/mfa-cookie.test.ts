import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

/**
 * The MFA marker cookie is the only thing standing between "password accepted"
 * and "past the second factor", so each way of forging it gets its own test.
 *
 * The module reads MFA_COOKIE_SECRET at call time, so tests set it before
 * importing and reset the module registry between cases.
 */

const SECRET = "test-secret-do-not-use-in-production";
const USER = "11111111-1111-1111-1111-111111111111";
const OTHER_USER = "22222222-2222-2222-2222-222222222222";

async function loadModule() {
  vi.resetModules();
  return import("@/lib/mfa-cookie");
}

describe("mfa-cookie", () => {
  beforeEach(() => {
    process.env.MFA_COOKIE_SECRET = SECRET;
  });

  afterEach(() => {
    vi.useRealTimers();
    delete process.env.MFA_COOKIE_SECRET;
  });

  it("accepts a cookie it just signed for the same user", async () => {
    const { signMfaCookie, verifyMfaCookie } = await loadModule();

    const cookie = await signMfaCookie(USER);

    expect(await verifyMfaCookie(cookie, USER)).toBe(true);
  });

  it("rejects a cookie minted for a different user", async () => {
    const { signMfaCookie, verifyMfaCookie } = await loadModule();

    const cookie = await signMfaCookie(OTHER_USER);

    // Lifting the cookie from another account must not work, which is why the
    // user id is inside the signed payload rather than alongside it.
    expect(await verifyMfaCookie(cookie, USER)).toBe(false);
  });

  it("rejects a cookie whose user id was swapped after signing", async () => {
    const { signMfaCookie, verifyMfaCookie } = await loadModule();

    const cookie = await signMfaCookie(OTHER_USER);
    const [, expiresAt, signature] = cookie.split(".");
    const forged = `${USER}.${expiresAt}.${signature}`;

    expect(await verifyMfaCookie(forged, USER)).toBe(false);
  });

  it("rejects a cookie with a tampered expiry", async () => {
    const { signMfaCookie, verifyMfaCookie } = await loadModule();

    const cookie = await signMfaCookie(USER);
    const [userId, expiresAt, signature] = cookie.split(".");
    const extended = `${userId}.${Number(expiresAt) + 86_400}.${signature}`;

    expect(await verifyMfaCookie(extended, USER)).toBe(false);
  });

  it("rejects a cookie signed with a different secret", async () => {
    const first = await loadModule();
    const cookie = await first.signMfaCookie(USER);

    process.env.MFA_COOKIE_SECRET = "a-completely-different-secret";
    const second = await loadModule();

    expect(await second.verifyMfaCookie(cookie, USER)).toBe(false);
  });

  it("rejects a cookie once the TTL has elapsed", async () => {
    const { signMfaCookie, verifyMfaCookie, MFA_TTL_SECONDS } = await loadModule();

    const cookie = await signMfaCookie(USER);
    expect(await verifyMfaCookie(cookie, USER)).toBe(true);

    vi.useFakeTimers();
    vi.setSystemTime(Date.now() + (MFA_TTL_SECONDS + 1) * 1000);

    expect(await verifyMfaCookie(cookie, USER)).toBe(false);
  });

  it("rejects malformed and missing values", async () => {
    const { verifyMfaCookie } = await loadModule();

    expect(await verifyMfaCookie(undefined, USER)).toBe(false);
    expect(await verifyMfaCookie("", USER)).toBe(false);
    expect(await verifyMfaCookie("nonsense", USER)).toBe(false);
    expect(await verifyMfaCookie(`${USER}.notanumber.abc`, USER)).toBe(false);
    expect(await verifyMfaCookie(`${USER}.999999999999`, USER)).toBe(false);
  });

  it("throws instead of signing when the secret is missing", async () => {
    delete process.env.MFA_COOKIE_SECRET;
    const { signMfaCookie } = await loadModule();

    // Failing loudly is the point: an unsigned marker would disable 2FA silently.
    await expect(signMfaCookie(USER)).rejects.toThrow(/MFA_COOKIE_SECRET/);
  });
});
