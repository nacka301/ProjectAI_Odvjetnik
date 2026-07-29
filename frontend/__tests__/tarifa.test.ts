import { describe, expect, it } from "vitest";
import { tbr71Bodovi } from "@/lib/tarifa";

/**
 * Paritetni testovi za klijentsko zrcalo tarifnog motora.
 *
 * Ista tablica očekivanih vrijednosti vrti se u `Backend.Tests/TarifaRazrediTests.cs`.
 * Ako se dva motora raziđu, jedan od ta dva paketa pada.
 *
 * Tbr. 7. toč. 1 je temelj cijelog parničnog obračuna i njegove granice se ne diraju.
 */

describe("Tbr. 7. toč. 1 — fiksni razredi", () => {
  // Obje vrijednosti na svakoj granici razreda.
  it.each([
    // razred 1 — do 332,00 €
    [0.0, 25],
    [0.01, 25],
    [331.99, 25],
    [332.0, 25],
    // razred 2 — 332,01 do 664,00 €
    [332.01, 50],
    [663.99, 50],
    [664.0, 50],
    // razred 3 — 664,01 do 1.327,00 €
    [664.01, 75],
    [1_326.99, 75],
    [1_327.0, 75],
    // razred 4 — 1.327,01 do 13.272,00 €
    [1_327.01, 100],
    [13_271.99, 100],
    [13_272.0, 100],
    // razred 5 — 13.272,01 do 33.181,00 €
    [13_272.01, 250],
    [33_180.99, 250],
    [33_181.0, 250],
    // razred 6 — 33.181,01 do 66.361,00 €
    [33_181.01, 500],
    [66_360.99, 500],
    [66_361.0, 500],
  ])("VPS %s € → %s bodova", (vps, ocekivano) => {
    expect(tbr71Bodovi(vps)).toBe(ocekivano);
  });
});

describe("Tbr. 7. toč. 1 — progresivni razredi", () => {
  it.each([
    // 1 bod za svakih započetih 133,00 € iznad 66.361,00 €
    [66_361.01, 501],
    [66_494.0, 501],
    [66_494.01, 502],
    [663_613.99, 4991],
    [663_614.0, 4991],
    // 1 bod za svakih započetih 265,00 € iznad 663.614,00 €
    [663_614.01, 4992],
    [663_879.0, 4992],
    [663_879.01, 4993],
    [1_327_227.99, 7496],
    [1_327_228.0, 7496],
    // 1 bod za svakih započetih 664,00 € iznad 1.327.228,00 €
    [1_327_228.01, 7497],
    [1_327_892.0, 7497],
    [1_327_892.01, 7498],
  ])("VPS %s € → %s bodova", (vps, ocekivano) => {
    expect(tbr71Bodovi(vps)).toBe(ocekivano);
  });
});

describe("Tbr. 7. toč. 1 — rubovi", () => {
  it.each([-1, -1_000_000])("negativan VPS (%s) pada na dno tablice", (vps) => {
    expect(tbr71Bodovi(vps)).toBe(25);
  });

  it.each([2_990_000, 10_000_000, 1_000_000_000])(
    "VPS %s € nikad ne prelazi 10.000 bodova",
    (vps) => {
      expect(tbr71Bodovi(vps)).toBe(10_000);
    },
  );

  it("tablica je monotona", () => {
    const tocke = [
      0, 332, 332.01, 664, 664.01, 1_327, 1_327.01, 13_272, 13_272.01, 33_181, 33_181.01,
      66_361, 66_361.01, 663_614, 663_614.01, 1_327_228, 1_327_228.01, 5_000_000, 100_000_000,
    ];

    let prosli = 0;
    for (const vps of tocke) {
      const bodovi = tbr71Bodovi(vps);
      expect(bodovi).toBeGreaterThanOrEqual(prosli);
      prosli = bodovi;
    }
  });
});
