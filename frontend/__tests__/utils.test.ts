import { describe, it, expect } from "vitest";
import {
  validateOib,
  formatHR,
  formatEur,
  formatEurPerM2,
  riskToColor,
  confidenceToColor,
  confidenceToLabel,
  calculatePdv,
  formatLocation,
} from "@/lib/utils";

// ═══════════════════════════════════════════
//  OIB Validation Tests (ISO 7064 MOD 11,10)
// ═══════════════════════════════════════════

describe("validateOib", () => {
  // Known valid OIBs (publicly available test cases)
  it("accepts valid OIB 73aborjkl36", () => {
    expect(validateOib("12345678903")).toBe(true);
  });

  it("accepts valid OIB 00000000001", () => {
    // Edge: all zeros except check digit
    // Let's compute: the mod 11,10 of 00000000001 should be valid
    // Actually let's use a known-valid one
    expect(validateOib("94577403194")).toBe(true);
  });

  // Invalid: wrong length
  it("rejects empty string", () => {
    expect(validateOib("")).toBe(false);
  });

  it("rejects too short", () => {
    expect(validateOib("1234567890")).toBe(false);
  });

  it("rejects too long", () => {
    expect(validateOib("123456789012")).toBe(false);
  });

  // Invalid: non-numeric
  it("rejects alpha characters", () => {
    expect(validateOib("1234567890a")).toBe(false);
  });

  it("rejects spaces", () => {
    expect(validateOib("1234 678903")).toBe(false);
  });

  // Invalid: wrong check digit
  it("rejects OIB with wrong check digit", () => {
    expect(validateOib("12345678901")).toBe(false);
    expect(validateOib("12345678902")).toBe(false);
    expect(validateOib("12345678904")).toBe(false);
  });

  // Null/undefined edge cases
  it("rejects null-ish inputs", () => {
    expect(validateOib(null as unknown as string)).toBe(false);
    expect(validateOib(undefined as unknown as string)).toBe(false);
  });

  // Additional valid OIBs (can be generated with the algorithm)
  it("validates correctly computed OIB", () => {
    // Manually verified with ISO 7064 MOD 11,10
    expect(validateOib("69435151530")).toBe(true);
  });
});

// ═══════════════════════════════════════════
//  Number Formatting Tests
// ═══════════════════════════════════════════

describe("formatHR", () => {
  it("formats thousands with Croatian locale", () => {
    const result = formatHR(1500);
    // Croatian uses . for thousands separator
    expect(result).toMatch(/1[\.\s]?500/);
  });

  it("formats zero", () => {
    expect(formatHR(0)).toBe("0");
  });

  it("formats small numbers without separator", () => {
    expect(formatHR(42)).toBe("42");
  });

  it("formats millions", () => {
    const result = formatHR(1250000);
    expect(result).toMatch(/1[\.\s]?250[\.\s]?000/);
  });
});

describe("formatEur", () => {
  it("appends EUR symbol", () => {
    expect(formatEur(150000)).toContain("€");
  });

  it("rounds to integer", () => {
    expect(formatEur(150000.7)).toContain("150");
  });
});

describe("formatEurPerM2", () => {
  it("appends EUR/m² unit", () => {
    expect(formatEurPerM2(2500)).toContain("€/m²");
  });
});

// ═══════════════════════════════════════════
//  Risk Color Mapping Tests
// ═══════════════════════════════════════════

describe("riskToColor", () => {
  it("maps VISOK to red", () => {
    expect(riskToColor("VISOK")).toBe("red");
  });

  it("maps KRITIČNO to red", () => {
    expect(riskToColor("KRITIČNO")).toBe("red");
  });

  it("maps SREDNJI to amber", () => {
    expect(riskToColor("SREDNJI")).toBe("amber");
  });

  it("maps NIZAK to green", () => {
    expect(riskToColor("NIZAK")).toBe("green");
  });

  it("is case-insensitive", () => {
    expect(riskToColor("visok")).toBe("red");
    expect(riskToColor("srednji")).toBe("amber");
    expect(riskToColor("nizak")).toBe("green");
  });

  it("defaults to green for unknown", () => {
    expect(riskToColor("")).toBe("green");
    expect(riskToColor("NEPOZNATO")).toBe("green");
  });
});

// ═══════════════════════════════════════════
//  Confidence Scoring Tests
// ═══════════════════════════════════════════

describe("confidenceToColor", () => {
  it("returns green for high scores (≥70)", () => {
    expect(confidenceToColor(70)).toBe("green");
    expect(confidenceToColor(100)).toBe("green");
    expect(confidenceToColor(85)).toBe("green");
  });

  it("returns amber for medium scores (40-69)", () => {
    expect(confidenceToColor(40)).toBe("amber");
    expect(confidenceToColor(69)).toBe("amber");
    expect(confidenceToColor(55)).toBe("amber");
  });

  it("returns red for low scores (<40)", () => {
    expect(confidenceToColor(0)).toBe("red");
    expect(confidenceToColor(39)).toBe("red");
    expect(confidenceToColor(10)).toBe("red");
  });
});

describe("confidenceToLabel", () => {
  it("returns Pouzdano for ≥70", () => {
    expect(confidenceToLabel(75)).toBe("Pouzdano");
  });

  it("returns Srednje for 40-69", () => {
    expect(confidenceToLabel(50)).toBe("Srednje");
  });

  it("returns Nisko for <40", () => {
    expect(confidenceToLabel(20)).toBe("Nisko");
  });

  // Boundary tests
  it("boundary: 70 is Pouzdano", () => {
    expect(confidenceToLabel(70)).toBe("Pouzdano");
  });

  it("boundary: 40 is Srednje", () => {
    expect(confidenceToLabel(40)).toBe("Srednje");
  });

  it("boundary: 39 is Nisko", () => {
    expect(confidenceToLabel(39)).toBe("Nisko");
  });
});

// ═══════════════════════════════════════════
//  PDV (VAT) Calculation Tests
// ═══════════════════════════════════════════

describe("calculatePdv", () => {
  it("calculates 25% PDV correctly", () => {
    const result = calculatePdv(1000, 25);
    expect(result.net).toBe(1000);
    expect(result.pdvAmount).toBe(250);
    expect(result.gross).toBe(1250);
  });

  it("calculates 13% PDV correctly", () => {
    const result = calculatePdv(1000, 13);
    expect(result.net).toBe(1000);
    expect(result.pdvAmount).toBe(130);
    expect(result.gross).toBe(1130);
  });

  it("calculates 5% PDV correctly", () => {
    const result = calculatePdv(2000, 5);
    expect(result.net).toBe(2000);
    expect(result.pdvAmount).toBe(100);
    expect(result.gross).toBe(2100);
  });

  it("handles 0% PDV (exempt)", () => {
    const result = calculatePdv(500, 0);
    expect(result.net).toBe(500);
    expect(result.pdvAmount).toBe(0);
    expect(result.gross).toBe(500);
  });

  it("rounds PDV amount to 2 decimals", () => {
    const result = calculatePdv(99.99, 25);
    expect(result.pdvAmount).toBe(25);
    expect(result.gross).toBe(124.99);
  });

  it("handles very small amounts", () => {
    const result = calculatePdv(0.01, 25);
    expect(result.pdvAmount).toBeCloseTo(0, 2);
    expect(result.gross).toBeCloseTo(0.01, 2);
  });

  it("handles large amounts", () => {
    const result = calculatePdv(100000, 25);
    expect(result.pdvAmount).toBe(25000);
    expect(result.gross).toBe(125000);
  });
});

// ═══════════════════════════════════════════
//  Location Formatting Tests
// ═══════════════════════════════════════════

describe("formatLocation", () => {
  it("formats city + district", () => {
    expect(formatLocation("Split", "Bačvice")).toBe("Split, Bačvice");
  });

  it("returns only city when no district", () => {
    expect(formatLocation("Zagreb")).toBe("Zagreb");
    expect(formatLocation("Zagreb", "")).toBe("Zagreb");
  });

  it("returns only city when district equals city", () => {
    expect(formatLocation("Split", "Split")).toBe("Split");
  });

  it("returns only city when district is Centar", () => {
    expect(formatLocation("Zagreb", "Centar")).toBe("Zagreb");
  });

  it("returns empty string for empty city", () => {
    expect(formatLocation("")).toBe("");
  });

  it("handles Croatian diacritics", () => {
    expect(formatLocation("Šibenik", "Šubićevac")).toBe("Šibenik, Šubićevac");
  });
});
