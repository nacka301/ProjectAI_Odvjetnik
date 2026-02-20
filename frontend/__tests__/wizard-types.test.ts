import { describe, it, expect } from "vitest";
import type {
  PropertyType,
  PropertyCondition,
  ApartmentData,
  HouseData,
  LandData,
  ValuationResult,
  ComparisonTableRow,
  PriceRangeEstimate,
} from "@/app/wizard/types";

// ═══════════════════════════════════════════
//  Wizard Types Structural Tests
//  Ensures type definitions match expected shapes
// ═══════════════════════════════════════════

describe("PropertyType", () => {
  it("accepts valid types", () => {
    const types: PropertyType[] = ["Apartment", "House", "Land"];
    expect(types).toHaveLength(3);
  });
});

describe("PropertyCondition", () => {
  it("accepts all condition values", () => {
    const conds: PropertyCondition[] = ["New", "Renovated", "Good", "Original", "NeedsWork"];
    expect(conds).toHaveLength(5);
  });
});

describe("ApartmentData shape", () => {
  it("creates valid apartment data", () => {
    const apt: ApartmentData = {
      area_m2: 65,
      room_count: 3,
      floor: "3",
      total_floors: 5,
      condition: "Good",
      year_built: 2010,
      distance_to_sea_m: 200,
      orientation: "South",
      view_type: "Sea",
      has_elevator: true,
      has_parking: true,
      has_balcony: true,
      sea_view: true,
    };
    expect(apt.area_m2).toBe(65);
    expect(apt.condition).toBe("Good");
    expect(apt.has_elevator).toBe(true);
  });
});

describe("HouseData shape", () => {
  it("creates valid house data", () => {
    const house: HouseData = {
      living_area_m2: 150,
      land_area_m2: 500,
      floors: "TwoStory",
      condition: "Renovated",
      year_built: 1985,
      orientation: "SouthEast",
      view_type: "Panoramic",
      has_garage: true,
      has_pool: false,
      first_row_to_sea: false,
      has_auxiliary_object: true,
    };
    expect(house.living_area_m2).toBe(150);
    expect(house.land_area_m2).toBe(500);
    expect(house.has_garage).toBe(true);
  });
});

describe("LandData shape", () => {
  it("creates valid land data", () => {
    const land: LandData = {
      land_area_m2: 2000,
      land_use: "Construction",
      distance_to_sea_m: 50,
      has_access_road: true,
      has_infrastructure: true,
      first_row_to_sea: false,
      is_legalized: true,
    };
    expect(land.land_area_m2).toBe(2000);
    expect(land.land_use).toBe("Construction");
  });
});

describe("ValuationResult shape", () => {
  it("creates a minimal valid result", () => {
    const result: ValuationResult = {
      target_property_summary: "Stan u Splitu",
      selected_comparables: [],
      estimated_value_eur: 150000,
      value_drivers: [],
      valuation_reasoning: "Na temelju 5 usporedivih nekretnina...",
    };
    expect(result.estimated_value_eur).toBe(150000);
  });

  it("creates a full result with all optional fields", () => {
    const result: ValuationResult = {
      target_property_summary: "Stan u Zagrebu, 65m², 3. kat",
      selected_comparables: [
        { title: "Stan", price: "150.000 €", location: "Zagreb", url: "https://example.com", reason_for_selection: "Slična veličina" },
      ],
      estimated_value_eur: 165000,
      value_drivers: [
        { factor: "Centar grada", impact: "Pozitivno", adjustment_percent: 12 },
      ],
      valuation_reasoning: "Detaljan opis...",
      calculated_price_per_m2: 2538,
      total_listings_found: 24,
      usable_price_points: 7,
      outliers_removed: 3,
      realization_discount_percent: 10,
      math_confidence_score: 75,
      math_confidence_level: "VISOKA",
      std_dev_per_m2: 350,
      coefficient_of_variation: 14,
      total_adjustment_percent: 12,
      pipeline: ["IQR", "LocationClustering", "SimilarityScoring"],
      comparison_table: [],
      price_range: {
        conservative_price_per_m2: 2200,
        realistic_price_per_m2: 2538,
        optimistic_price_per_m2: 2900,
        conservative_total: 143000,
        realistic_total: 165000,
        optimistic_total: 188500,
      },
    };
    expect(result.math_confidence_score).toBe(75);
    expect(result.pipeline).toContain("IQR");
    expect(result.price_range!.realistic_total).toBe(165000);
  });
});

describe("ComparisonTableRow", () => {
  it("creates valid comparison row", () => {
    const row: ComparisonTableRow = {
      index: 1,
      title: "Stan 65m² Bačvice",
      url: "https://njuskalo.hr/1234",
      area_m2: 65,
      total_price: 170000,
      raw_price_per_m2: 2615,
      correction_percent: -3.5,
      adjusted_price_per_m2: 2523,
      similarity_score: 82,
    };
    expect(row.correction_percent).toBe(-3.5);
    expect(row.similarity_score).toBe(82);
  });
});

describe("PriceRangeEstimate", () => {
  it("conservative < realistic < optimistic", () => {
    const range: PriceRangeEstimate = {
      conservative_price_per_m2: 2000,
      realistic_price_per_m2: 2500,
      optimistic_price_per_m2: 3000,
      conservative_total: 130000,
      realistic_total: 162500,
      optimistic_total: 195000,
    };
    expect(range.conservative_price_per_m2).toBeLessThan(range.realistic_price_per_m2);
    expect(range.realistic_price_per_m2).toBeLessThan(range.optimistic_price_per_m2);
    expect(range.conservative_total).toBeLessThan(range.realistic_total);
    expect(range.realistic_total).toBeLessThan(range.optimistic_total);
  });
});
