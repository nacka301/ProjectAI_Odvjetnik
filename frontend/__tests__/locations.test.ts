import { describe, it, expect } from "vitest";
import { LOCATIONS, type County, type CityMunicipality } from "@/lib/locations";

// ═══════════════════════════════════════════
//  Location Hierarchy Tests
//  Validates the Croatian location database
// ═══════════════════════════════════════════

describe("LOCATIONS data integrity", () => {
  it("has counties", () => {
    expect(LOCATIONS.length).toBeGreaterThan(0);
  });

  it("every county has a name", () => {
    for (const county of LOCATIONS) {
      expect(county.name).toBeTruthy();
      expect(typeof county.name).toBe("string");
    }
  });

  it("every county has at least one city", () => {
    for (const county of LOCATIONS) {
      expect(county.cities.length).toBeGreaterThan(0);
    }
  });

  it("every city has a name", () => {
    for (const county of LOCATIONS) {
      for (const city of county.cities) {
        expect(city.name).toBeTruthy();
        expect(typeof city.name).toBe("string");
      }
    }
  });

  it("every city has at least one settlement", () => {
    for (const county of LOCATIONS) {
      for (const city of county.cities) {
        expect(city.settlements.length).toBeGreaterThan(0);
      }
    }
  });
});

describe("Major cities exist", () => {
  const findCity = (cityName: string): CityMunicipality | undefined => {
    for (const county of LOCATIONS) {
      const found = county.cities.find(
        (c) => c.name.toLowerCase() === cityName.toLowerCase()
      );
      if (found) return found;
    }
    return undefined;
  };

  const findCounty = (countyName: string): County | undefined =>
    LOCATIONS.find((c) => c.name === countyName);

  it("contains Zagreb", () => {
    const zagreb = findCity("Zagreb");
    expect(zagreb).toBeDefined();
    expect(zagreb!.settlements.length).toBeGreaterThan(10);
  });

  it("contains Split with detailed settlements", () => {
    const split = findCity("Split");
    expect(split).toBeDefined();
    expect(split!.settlements).toContain("Bačvice");
    expect(split!.settlements).toContain("Centar");
    expect(split!.settlements).toContain("Meje");
    expect(split!.settlements).toContain("Žnjan");
    expect(split!.settlements.length).toBeGreaterThan(15);
  });

  it("contains Rijeka with detailed settlements", () => {
    const rijeka = findCity("Rijeka");
    expect(rijeka).toBeDefined();
    expect(rijeka!.settlements).toContain("Centar");
    expect(rijeka!.settlements).toContain("Trsat");
    expect(rijeka!.settlements).toContain("Sušak");
  });

  it("contains Osijek with detailed settlements", () => {
    const osijek = findCity("Osijek");
    expect(osijek).toBeDefined();
    expect(osijek!.settlements).toContain("Centar");
    expect(osijek!.settlements).toContain("Tvrđa");
    expect(osijek!.settlements).toContain("Retfala");
  });

  it("contains Dubrovnik", () => {
    const dubrovnik = findCity("Dubrovnik");
    expect(dubrovnik).toBeDefined();
  });

  it("contains Zadar", () => {
    const zadar = findCity("Zadar");
    expect(zadar).toBeDefined();
  });

  it("contains coastal towns", () => {
    for (const town of ["Makarska", "Opatija", "Hvar", "Bol", "Trogir", "Crikvenica"]) {
      expect(findCity(town)).toBeDefined();
    }
  });

  it("contains Grad Zagreb county", () => {
    const gzg = findCounty("Grad Zagreb");
    expect(gzg).toBeDefined();
    expect(gzg!.cities[0].name).toBe("Zagreb");
  });

  it("Splitsko-dalmatinska has Split", () => {
    const sd = findCounty("Splitsko-dalmatinska županija");
    expect(sd).toBeDefined();
    expect(sd!.cities.find((c) => c.name === "Split")).toBeDefined();
  });
});

describe("No duplicate cities within a county", () => {
  it("each county has unique city names", () => {
    for (const county of LOCATIONS) {
      const names = county.cities.map((c) => c.name);
      const unique = new Set(names);
      expect(unique.size).toBe(names.length);
    }
  });
});

describe("Settlement data quality", () => {
  it("no empty settlement names", () => {
    for (const county of LOCATIONS) {
      for (const city of county.cities) {
        for (const s of city.settlements) {
          expect(s.trim().length).toBeGreaterThan(0);
        }
      }
    }
  });

  it("no duplicate settlements within a city", () => {
    for (const county of LOCATIONS) {
      for (const city of county.cities) {
        const unique = new Set(city.settlements);
        if (unique.size !== city.settlements.length) {
          const dupes = city.settlements.filter(
            (s, i, arr) => arr.indexOf(s) !== i
          );
          throw new Error(
            `Duplicate settlements in ${county.name} / ${city.name}: ${dupes.join(", ")}`
          );
        }
      }
    }
  });
});
