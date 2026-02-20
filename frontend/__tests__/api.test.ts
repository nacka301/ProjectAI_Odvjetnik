import { describe, it, expect } from "vitest";
import { API_BASE } from "@/lib/api";

// ═══════════════════════════════════════════
//  API Configuration Tests
// ═══════════════════════════════════════════

describe("API_BASE", () => {
  it("is a string", () => {
    expect(typeof API_BASE).toBe("string");
  });

  it("starts with http", () => {
    expect(API_BASE).toMatch(/^https?:\/\//);
  });

  it("does not end with trailing slash", () => {
    expect(API_BASE.endsWith("/")).toBe(false);
  });

  it("falls back to localhost:5078 without env var", () => {
    // In test environment, NEXT_PUBLIC_API_URL is not set
    expect(API_BASE).toBe("http://localhost:5078");
  });
});
