import { describe, expect, it } from "vitest";
import { povisenjeViseOsobaPostotak } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTbr40Tests.cs.
 *
 * Tbr. 40. toč. 1: 10% za drugu i svaku sljedeću osobu, najviše 50%.
 */

describe("Tbr. 40. toč. 1 — povišenje za više osoba", () => {
  it.each([
    [1, 0],
    [2, 0.1],
    [3, 0.2],
    [4, 0.3],
    [5, 0.4],
    [6, 0.5],
    [8, 0.5], // kapa drži
    [20, 0.5],
  ])("%i osoba → +%s", (brojOsoba, ocekivano) => {
    expect(povisenjeViseOsobaPostotak(brojOsoba)).toBeCloseTo(ocekivano, 10);
  });

  it("prva osoba ne ulazi u postotak", () => {
    expect(povisenjeViseOsobaPostotak(1)).toBe(0);
  });

  it.each([0, -3, Number.NaN])("nevaljan broj osoba (%s) → bez povišenja", (brojOsoba) => {
    expect(povisenjeViseOsobaPostotak(brojOsoba)).toBe(0);
  });
});
