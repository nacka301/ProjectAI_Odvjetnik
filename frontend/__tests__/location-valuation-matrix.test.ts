import { describe, it, expect } from "vitest";
import { LOCATIONS, getFlatCities, type County, type CityMunicipality } from "@/lib/locations";

// ═══════════════════════════════════════════════════════════════════
//  PROPERTY-TYPE × LOCATION MATRIX TESTS
//  For every city/municipality in the Croatian location database,
//  verifies that zemljište (land), stan (apartment), kuća (house)
//  produce valid valuation input combinations.
//
//  Total: 555 cities × 3 property types = 1,665 tests
// ═══════════════════════════════════════════════════════════════════

const PROPERTY_TYPES = ["stan", "kuća", "zemljište"] as const;
type PropertyType = (typeof PROPERTY_TYPES)[number];

/** Simulate a valuation input for the given location + property type. */
function makeValuationInput(
  city: string,
  county: string,
  settlement: string,
  propertyType: PropertyType
) {
  const location = settlement && settlement !== city
    ? `${settlement}, ${city}`
    : city;

  const sizeM2 =
    propertyType === "zemljište" ? 500 :
    propertyType === "kuća"     ? 120 :
    /* stan */                    65;

  return {
    propertyType,
    location,
    city,
    county,
    settlement,
    sizeM2,
    description: `${propertyType} ${sizeM2} m² — ${location}, ${county}`,
  };
}

/** Validate a valuation input is well-formed. */
function isValidInput(input: ReturnType<typeof makeValuationInput>) {
  return (
    input.propertyType.length > 0 &&
    input.location.length > 0 &&
    input.city.length > 0 &&
    input.county.length > 0 &&
    input.sizeM2 > 0 &&
    input.description.length > 10
  );
}

// ─── Generate test cases for every county × city × type ───

for (const county of LOCATIONS) {
  describe(`${county.name}`, () => {
    for (const city of county.cities) {
      // Pick the first settlement (most specific) for the test location
      const settlement = city.settlements[0] ?? city.name;

      for (const pType of PROPERTY_TYPES) {
        it(`${city.name} · ${pType}`, () => {
          const input = makeValuationInput(city.name, county.name, settlement, pType);

          // 1. Input is valid
          expect(isValidInput(input)).toBe(true);

          // 2. Location string is non-empty and contains city name
          expect(input.location).toContain(city.name);

          // 3. Size is appropriate for type
          if (pType === "zemljište") {
            expect(input.sizeM2).toBeGreaterThanOrEqual(100);
          } else if (pType === "kuća") {
            expect(input.sizeM2).toBeGreaterThanOrEqual(50);
          } else {
            expect(input.sizeM2).toBeGreaterThanOrEqual(20);
          }

          // 4. Description includes property type and location
          expect(input.description).toContain(pType);
          expect(input.description).toContain(input.location);

          // 5. County is properly set
          expect(input.county).toBe(county.name);
        });
      }
    }
  });
}

// ─── Aggregate checks ───

describe("Location coverage statistics", () => {
  const flat = getFlatCities();

  it("covers all 21 Croatian counties", () => {
    expect(LOCATIONS.length).toBe(21);
  });

  it("covers 555+ cities/municipalities", () => {
    expect(flat.length).toBeGreaterThanOrEqual(555);
  });

  it("every city produces valid inputs for all 3 property types", () => {
    let validCount = 0;
    for (const fc of flat) {
      for (const pType of PROPERTY_TYPES) {
        const input = makeValuationInput(fc.city, fc.county, fc.settlements[0] ?? fc.city, pType);
        if (isValidInput(input)) validCount++;
      }
    }
    expect(validCount).toBe(flat.length * 3);
  });

  it("all major cities included: Zagreb, Split, Rijeka, Osijek, Zadar, Dubrovnik", () => {
    const majorCities = ["Zagreb", "Split", "Rijeka", "Osijek", "Zadar", "Dubrovnik"];
    for (const mc of majorCities) {
      const found = flat.find(f => f.city === mc);
      expect(found, `${mc} should be in locations`).toBeDefined();
    }
  });

  it("every settlement string is non-empty", () => {
    for (const fc of flat) {
      for (const s of fc.settlements) {
        expect(s.trim().length).toBeGreaterThan(0);
      }
    }
  });
});
