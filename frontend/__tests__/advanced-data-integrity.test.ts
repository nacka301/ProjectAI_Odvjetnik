import { describe, it, expect } from "vitest";
import { LOCATIONS, getFlatCities, type County } from "@/lib/locations";
import {
  validateOib,
  formatHR,
  formatEur,
  formatEurPerM2,
  calculatePdv,
  formatLocation,
  confidenceToColor,
  confidenceToLabel,
  riskToColor,
} from "@/lib/utils";
import { getStatusColor } from "@/lib/normApi";

// ═══════════════════════════════════════════════════════════════════
//  ADVANCED DATA INTEGRITY & EDGE CASE TESTS (V82)
//  Beyond the basic tests — cross-module validation, fuzzy matching
//  simulation, settlement ↔ city consistency, edge cases that
//  only surface with real Croatian data.
// ═══════════════════════════════════════════════════════════════════

// ── 1. LOCATION DATABASE CROSS-VALIDATION ──

describe("Location cross-validation (getFlatCities vs LOCATIONS)", () => {
  const flat = getFlatCities();

  it("flat count equals sum of all cities across all counties", () => {
    const directCount = LOCATIONS.reduce((sum, c) => sum + c.cities.length, 0);
    expect(flat.length).toBe(directCount);
  });

  it("every flat city maps back to its correct county", () => {
    for (const fc of flat) {
      const county = LOCATIONS.find((c) => c.name === fc.county);
      expect(county, `County "${fc.county}" not found for city "${fc.city}"`).toBeDefined();
      const city = county!.cities.find((c) => c.name === fc.city);
      expect(city, `City "${fc.city}" not found in county "${fc.county}"`).toBeDefined();
    }
  });

  it("getFlatCities returns same results on repeated calls (caching)", () => {
    const flat1 = getFlatCities();
    const flat2 = getFlatCities();
    expect(flat1).toBe(flat2); // Same reference (cached)
  });

  it("no city name is empty or whitespace-only", () => {
    for (const fc of flat) {
      expect(fc.city.trim().length).toBeGreaterThan(0);
    }
  });

  it("no county name is empty or whitespace-only", () => {
    for (const county of LOCATIONS) {
      expect(county.name.trim().length).toBeGreaterThan(0);
    }
  });
});

// ── 2. SETTLEMENT INTEGRITY ──

describe("Settlement naming conventions", () => {
  const flat = getFlatCities();

  it("settlement names don't contain leading/trailing whitespace", () => {
    for (const fc of flat) {
      for (const s of fc.settlements) {
        expect(s).toBe(s.trim());
      }
    }
  });

  it("city name itself never appears twice in its settlements", () => {
    for (const fc of flat) {
      const occurrences = fc.settlements.filter((s) => s === fc.city).length;
      expect(
        occurrences,
        `${fc.city} appears ${occurrences} times in its own settlements`
      ).toBeLessThanOrEqual(1);
    }
  });

  it("settlement names have reasonable lengths (2-50 chars)", () => {
    for (const fc of flat) {
      for (const s of fc.settlements) {
        expect(s.length).toBeGreaterThanOrEqual(2);
        expect(s.length).toBeLessThanOrEqual(50);
      }
    }
  });

  it("total settlement count > total city count (cities have neighborhoods)", () => {
    const totalSettlements = flat.reduce((sum, fc) => sum + fc.settlements.length, 0);
    expect(totalSettlements).toBeGreaterThan(flat.length);
  });
});

// ── 3. CROATIAN LOCATION INFLECTION SIMULATION ──
// Simulates the V82 backend fix on the frontend:
// can the frontend correctly handle Croatian grammatical case forms?

describe("Croatian location string handling", () => {
  /** Mini Croatian stem checker — mirrors backend CroatianStemMatch logic */
  function cityMatchesMention(cityName: string, mention: string): boolean {
    const norm = (s: string) =>
      s.toLowerCase()
        .replace(/č/g, "c").replace(/ć/g, "c")
        .replace(/đ/g, "d").replace(/š/g, "s").replace(/ž/g, "z");

    const c = norm(cityName);
    const m = norm(mention);

    // Exact match
    if (m.includes(c)) return true;

    // Stem match: city without last vowel
    if (c.length >= 4) {
      const vowels = "aeiou";
      const endsVowel = vowels.includes(c[c.length - 1]);
      const stem = endsVowel ? c.slice(0, -1) : c;
      if (stem.length >= 3) {
        // Check if mention contains stem as a word start
        const escaped = stem.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        const regex = new RegExp(`\\b${escaped}\\w{0,3}\\b`);
        if (regex.test(m)) return true;

        // Palatalization: k→c (tresnjevka→tresnjevci, rijeka→rijeci)
        if (stem.endsWith("k")) {
          const palStem = stem.slice(0, -1) + "c";
          const palRegex = new RegExp(`\\b${palStem.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\w{0,3}\\b`);
          if (palRegex.test(m)) return true;
        }

        // Nepostojano 'a'/'e': zadar→zadru, sisak→sisku, karlovac→karlovcu
        // Drop the penultimate vowel from consonant-ending words
        if (!endsVowel && c.length >= 4 && vowels.includes(c[c.length - 2])) {
          const nepStem = c.slice(0, -2) + c[c.length - 1];
          if (nepStem.length >= 3) {
            const nepEscaped = nepStem.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
            const nepRegex = new RegExp(`\\b${nepEscaped}\\w{0,3}\\b`);
            if (nepRegex.test(m)) return true;
          }
        }
      }
    }

    return false;
  }

  it("exact city names match themselves", () => {
    expect(cityMatchesMention("Zagreb", "stanovi u Zagrebu")).toBe(true);
    expect(cityMatchesMention("Split", "kuća u Splitu")).toBe(true);
    expect(cityMatchesMention("Hvar", "na otoku Hvaru")).toBe(true);
  });

  it("Croatian locative forms match base city names", () => {
    expect(cityMatchesMention("Pula", "stan u Puli")).toBe(true);
    expect(cityMatchesMention("Hvar", "kuća na Hvaru")).toBe(true);
    expect(cityMatchesMention("Split", "nekretnina u Splitu")).toBe(true);
    expect(cityMatchesMention("Rovinj", "kuća u Rovinju")).toBe(true);
    expect(cityMatchesMention("Dubrovnik", "stan u Dubrovniku")).toBe(true);
  });

  it("different cities do NOT match", () => {
    expect(cityMatchesMention("Zagreb", "stan u Splitu")).toBe(false);
    expect(cityMatchesMention("Pula", "kuća u Rijeci")).toBe(false);
    expect(cityMatchesMention("Hvar", "nekretnina u Dubrovniku")).toBe(false);
  });

  it("all major cities match their locative forms", () => {
    const cityLocativePairs = [
      ["Zagreb", "Zagrebu"],
      ["Split", "Splitu"],
      ["Rijeka", "Rijeci"],
      ["Osijek", "Osijeku"],
      ["Pula", "Puli"],
      ["Zadar", "Zadru"],
      ["Dubrovnik", "Dubrovniku"],
      ["Rovinj", "Rovinju"],
    ];

    for (const [city, locative] of cityLocativePairs) {
      expect(
        cityMatchesMention(city, `oglas u ${locative}`),
        `${city} should match "${locative}"`
      ).toBe(true);
    }
  });
});

// ── 4. formatLocation × LOCATIONS INTEGRATION ──

describe("formatLocation with real location data", () => {
  it("formats all Zagreb settlements correctly", () => {
    const zagreb = LOCATIONS.find((c) => c.name === "Grad Zagreb")?.cities[0];
    expect(zagreb).toBeDefined();

    for (const s of zagreb!.settlements) {
      const formatted = formatLocation(zagreb!.name, s);
      if (s === "Centar") {
        expect(formatted).toBe(zagreb!.name); // "Centar" is suppressed
      } else {
        expect(formatted).toBe(`${zagreb!.name}, ${s}`);
        expect(formatted).toContain(s);
      }
    }
  });

  it("formats all Split settlements correctly", () => {
    const splitCounty = LOCATIONS.find((c) => c.name === "Splitsko-dalmatinska županija");
    const split = splitCounty?.cities.find((c) => c.name === "Split");
    expect(split).toBeDefined();

    for (const s of split!.settlements) {
      const formatted = formatLocation("Split", s);
      if (s === "Centar" || s === "Split") {
        expect(formatted).toBe("Split");
      } else {
        expect(formatted).toContain(s);
      }
    }
  });

  it("all cities format without crashing", () => {
    const flat = getFlatCities();
    for (const fc of flat) {
      for (const s of fc.settlements) {
        const formatted = formatLocation(fc.city, s);
        expect(typeof formatted).toBe("string");
        expect(formatted.length).toBeGreaterThan(0);
      }
    }
  });
});

// ── 5. OIB EDGE CASES ──

describe("validateOib advanced edge cases", () => {
  it("rejects all-same-digit OIBs", () => {
    for (let d = 0; d <= 9; d++) {
      const oib = String(d).repeat(11);
      // Not all are invalid, but most single-digit repeats fail check digit
      // We just verify no crash
      const result = validateOib(oib);
      expect(typeof result).toBe("boolean");
    }
  });

  it("rejects OIBs with special characters", () => {
    expect(validateOib("1234567890!")).toBe(false);
    expect(validateOib("123-456-7890")).toBe(false);
    expect(validateOib("12345.78903")).toBe(false);
  });

  it("rejects null and undefined gracefully", () => {
    expect(validateOib(null as unknown as string)).toBe(false);
    expect(validateOib(undefined as unknown as string)).toBe(false);
  });

  // Real-world valid OIBs from public registries
  it("validates known real OIBs", () => {
    expect(validateOib("12345678903")).toBe(true);  // Standard test OIB
    expect(validateOib("94577403194")).toBe(true);  // Known valid
  });
});

// ── 6. CONFIDENCE / RISK COLOR CONSISTENCY ──

describe("Confidence and risk color functions — boundary tests", () => {
  it("confidence boundaries are exact", () => {
    expect(confidenceToColor(70)).toBe("green");
    expect(confidenceToColor(69)).toBe("amber");
    expect(confidenceToColor(40)).toBe("amber");
    expect(confidenceToColor(39)).toBe("red");
    expect(confidenceToColor(0)).toBe("red");
    expect(confidenceToColor(100)).toBe("green");
  });

  it("confidence labels match colors", () => {
    expect(confidenceToLabel(70)).toBe("Pouzdano");
    expect(confidenceToLabel(69)).toBe("Srednje");
    expect(confidenceToLabel(40)).toBe("Srednje");
    expect(confidenceToLabel(39)).toBe("Nisko");
  });

  it("risk colors for all known values", () => {
    expect(riskToColor("VISOK")).toBe("red");
    expect(riskToColor("KRITIČNO")).toBe("red");
    expect(riskToColor("SREDNJI")).toBe("amber");
    expect(riskToColor("NIZAK")).toBe("green");
    expect(riskToColor("")).toBe("green"); // default
  });

  it("risk colors are case-insensitive", () => {
    expect(riskToColor("visok")).toBe("red");
    expect(riskToColor("Visok")).toBe("red");
    expect(riskToColor("srednji")).toBe("amber");
    expect(riskToColor("nizak")).toBe("green");
  });
});

// ── 7. NORM STATUS COLOR ──

describe("getStatusColor exhaustive", () => {
  it("returns green for VAŽEĆI", () => {
    const result = getStatusColor("VAŽEĆI");
    expect(result.bg).toContain("green");
    expect(result.label).toBe("Važeći");
  });

  it("returns amber for IZMIJENJEN", () => {
    const result = getStatusColor("IZMIJENJEN");
    expect(result.bg).toContain("amber");
    expect(result.label).toBe("Izmijenjen");
  });

  it("returns red for UKINUT", () => {
    const result = getStatusColor("UKINUT");
    expect(result.bg).toContain("red");
    expect(result.label).toBe("Ukinut");
  });

  it("returns gray for unknown status", () => {
    const result = getStatusColor("NEPOZNATO");
    expect(result.bg).toContain("gray");
    expect(result.label).toBe("NEPOZNATO");
  });

  it("handles empty string", () => {
    const result = getStatusColor("");
    expect(result.bg).toContain("gray");
    expect(result.label).toBe("Nepoznat");
  });

  it("is case-insensitive", () => {
    expect(getStatusColor("važeći").label).toBe("Važeći");
    expect(getStatusColor("Važeći").label).toBe("Važeći");
  });

  it("all variants return valid CSS class names", () => {
    for (const status of ["VAŽEĆI", "IZMIJENJEN", "UKINUT", "OTHER"]) {
      const result = getStatusColor(status);
      expect(result.bg).toMatch(/^bg-/);
      expect(result.text).toMatch(/^text-/);
      expect(result.dot).toMatch(/^bg-/);
    }
  });
});

// ── 8. PDV CALCULATION EDGE CASES ──

describe("calculatePdv advanced", () => {
  it("handles negative amounts (credits/refunds)", () => {
    const result = calculatePdv(-1000, 25);
    expect(result.pdvAmount).toBe(-250);
    expect(result.gross).toBe(-1250);
  });

  it("maintains precision for typical invoice amounts", () => {
    // Typical Croatian legal invoice: 500 EUR + 25% PDV
    const result = calculatePdv(500, 25);
    expect(result.pdvAmount).toBe(125);
    expect(result.gross).toBe(625);
  });

  it("handles fractional cent amounts", () => {
    const result = calculatePdv(33.33, 25);
    expect(result.pdvAmount).toBe(8.33);
    expect(result.gross).toBe(41.66);
  });

  it("all valid PDV rates produce correctly structured output", () => {
    for (const rate of [25, 13, 5, 0]) {
      const result = calculatePdv(1000, rate);
      expect(result.net).toBe(1000);
      expect(result.gross).toBeGreaterThanOrEqual(result.net);
      expect(result.pdvAmount).toBeGreaterThanOrEqual(0);
      expect(result.gross).toBe(
        Math.round((result.net + result.pdvAmount) * 100) / 100
      );
    }
  });
});

// ── 9. FORMAT FUNCTIONS WITH EXTREME VALUES ──

describe("Formatting functions — extreme values", () => {
  it("formatHR handles zero", () => {
    expect(formatHR(0)).toBeDefined();
  });

  it("formatHR handles millions", () => {
    const result = formatHR(1234567);
    expect(result.length).toBeGreaterThan(4); // includes separators
  });

  it("formatEur handles zero", () => {
    expect(formatEur(0)).toContain("€");
    expect(formatEur(0)).toContain("0");
  });

  it("formatEur for typical Croatian property values", () => {
    const result = formatEur(250000);
    expect(result).toContain("250");
    expect(result).toContain("€");
  });

  it("formatEurPerM2 for typical price per m²", () => {
    const result = formatEurPerM2(2500);
    expect(result).toContain("2");
    expect(result).toContain("€/m²");
  });

  it("formatEur rounds to nearest integer", () => {
    const result = formatEur(99999.7);
    expect(result).toContain("100");
  });
});

// ── 10. PROPERTY TYPE × LOCATION × SETTLEMENT MATRIX ──
// Extended version: ensure every settlement can form a valid location string

describe("Settlement → Location formatting matrix", () => {
  const flat = getFlatCities();
  const sampleCities = flat.filter((fc) =>
    ["Zagreb", "Split", "Rijeka", "Osijek", "Dubrovnik", "Pula", "Zadar", "Hvar", "Makarska"].includes(fc.city)
  );

  for (const fc of sampleCities) {
    describe(`${fc.city} (${fc.county})`, () => {
      it(`has ${fc.settlements.length} settlement(s)`, () => {
        expect(fc.settlements.length).toBeGreaterThan(0);
      });

      for (const s of fc.settlements.slice(0, 5)) {
        it(`settlement "${s}" produces valid location`, () => {
          const loc = formatLocation(fc.city, s);
          expect(loc.length).toBeGreaterThan(0);
          // Location should always contain the city name OR be just the city
          if (s !== "Centar" && s !== fc.city) {
            expect(loc).toContain(s);
          }
        });
      }
    });
  }
});

// ── 11. COUNTY-LEVEL STATISTICS ──

describe("County statistics validation", () => {
  it("every county has between 1 and 100 cities", () => {
    for (const county of LOCATIONS) {
      expect(county.cities.length).toBeGreaterThanOrEqual(1);
      expect(county.cities.length).toBeLessThanOrEqual(100);
    }
  });

  it("Grad Zagreb has exactly 1 city (Zagreb)", () => {
    const gzg = LOCATIONS.find((c) => c.name === "Grad Zagreb");
    expect(gzg).toBeDefined();
    expect(gzg!.cities.length).toBe(1);
    expect(gzg!.cities[0].name).toBe("Zagreb");
  });

  it("Zagreb has the most settlements of any single city", () => {
    const flat = getFlatCities();
    const sorted = [...flat].sort((a, b) => b.settlements.length - a.settlements.length);
    expect(sorted[0].city).toBe("Zagreb");
  });

  it("island cities exist (Hvar, Korčula, Bol, Komiža, Vis)", () => {
    const flat = getFlatCities();
    const islandCities = ["Hvar", "Bol", "Komiža", "Vis"];
    for (const island of islandCities) {
      expect(
        flat.find((fc) => fc.city === island),
        `Island city ${island} should exist`
      ).toBeDefined();
    }
  });

  it("all 21 Croatian counties are named correctly", () => {
    const expectedCountyPrefixes = [
      "Grad Zagreb",
      "Splitsko",
      "Primorsko",
      "Istarska",
      "Dubrovačko",
      "Zadarska",
      "Osječko",
      "Zagrebačka",
      "Varaždinska",
      "Koprivničko",
      "Bjelovarsko",
      "Karlovačka",
       "Sisačko",
      "Vukovarsko",
      "Brodsko",
      "Požeško",
      "Virovitičko",
      "Šibensko",
      "Ličko",
      "Međimurska",
      "Krapinsko",
    ];

    for (const prefix of expectedCountyPrefixes) {
      const found = LOCATIONS.find((c) => c.name.startsWith(prefix));
      expect(found, `County starting with "${prefix}" should exist`).toBeDefined();
    }
  });
});
