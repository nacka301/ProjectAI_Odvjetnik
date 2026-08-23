import { describe, it, expect } from "vitest";
import { checkRateLimit, isWaitlistEmailValid, type RateLimitRecord } from "@/lib/waitlist";

describe("isWaitlistEmailValid", () => {
  it("accepts an ordinary address", () => {
    expect(isWaitlistEmailValid("ime@odvjetnik.hr")).toBe(true);
  });

  it("trims surrounding whitespace before checking", () => {
    expect(isWaitlistEmailValid("  ime@odvjetnik.hr  ")).toBe(true);
  });

  it("rejects a missing @", () => {
    expect(isWaitlistEmailValid("ime.odvjetnik.hr")).toBe(false);
  });

  it("rejects a missing domain", () => {
    expect(isWaitlistEmailValid("ime@")).toBe(false);
  });

  it("rejects a one-character TLD", () => {
    // the regex requires {2,} after the last dot
    expect(isWaitlistEmailValid("ime@odvjetnik.h")).toBe(false);
  });

  it("rejects a bare string with no domain part", () => {
    expect(isWaitlistEmailValid("nijeemail")).toBe(false);
  });

  it("rejects an empty string", () => {
    expect(isWaitlistEmailValid("")).toBe(false);
  });
});

describe("checkRateLimit", () => {
  function newStore() {
    const map = new Map<string, RateLimitRecord>();
    return { get: (ip: string) => map.get(ip), set: (ip: string, v: RateLimitRecord) => void map.set(ip, v) };
  }

  it("allows the first request from an IP", () => {
    const store = newStore();
    expect(checkRateLimit(store, "1.2.3.4", 0, { windowMs: 1000, max: 5 })).toBe(false);
  });

  it("allows up to `max` requests inside the window", () => {
    const store = newStore();
    const opts = { windowMs: 1000, max: 3 };
    expect(checkRateLimit(store, "1.2.3.4", 0, opts)).toBe(false); // 1
    expect(checkRateLimit(store, "1.2.3.4", 10, opts)).toBe(false); // 2
    expect(checkRateLimit(store, "1.2.3.4", 20, opts)).toBe(false); // 3
  });

  it("blocks the request after `max` is exceeded within the window", () => {
    const store = newStore();
    const opts = { windowMs: 1000, max: 2 };
    expect(checkRateLimit(store, "1.2.3.4", 0, opts)).toBe(false); // 1
    expect(checkRateLimit(store, "1.2.3.4", 10, opts)).toBe(false); // 2
    expect(checkRateLimit(store, "1.2.3.4", 20, opts)).toBe(true); // 3 — blocked
  });

  it("resets once the window has passed", () => {
    const store = newStore();
    const opts = { windowMs: 1000, max: 1 };
    expect(checkRateLimit(store, "1.2.3.4", 0, opts)).toBe(false); // 1, allowed
    expect(checkRateLimit(store, "1.2.3.4", 10, opts)).toBe(true); // 2, blocked (same window)
    expect(checkRateLimit(store, "1.2.3.4", 2000, opts)).toBe(false); // new window, allowed again
  });

  it("tracks each IP independently", () => {
    const store = newStore();
    const opts = { windowMs: 1000, max: 1 };
    expect(checkRateLimit(store, "1.1.1.1", 0, opts)).toBe(false);
    expect(checkRateLimit(store, "2.2.2.2", 0, opts)).toBe(false);
    expect(checkRateLimit(store, "1.1.1.1", 5, opts)).toBe(true);
    expect(checkRateLimit(store, "2.2.2.2", 5, opts)).toBe(true);
  });
});
