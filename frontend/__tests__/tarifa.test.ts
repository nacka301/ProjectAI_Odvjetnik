import { describe, expect, it } from "vitest";
import { tbr71Bodovi } from "@/lib/tarifa";

/**
 * Paritetni testovi za klijentsko zrcalo tarifnog motora.
 *
 * Ista tablica očekivanih vrijednosti vrti se u `Backend.Tests/TarifaRazrediTests.cs`.
 * Ako se dva motora raziđu, jedan od ta dva paketa pada.
 */

describe("Tbr. 7. toč. 1 — granice razreda", () => {
  // Tablica doslovno iz Tbr. 7. toč. 1. Za svaku granicu ispituje se
  // gornja vrijednost razreda i prva vrijednost sljedećeg razreda.
  const granice: [number, number][] = [
    [0, 25],
    [332.0, 25],
    [332.01, 50],
    [664.0, 50],
    [664.01, 75],
    [1327.0, 75],
    [1327.01, 100],
    [13272.0, 100],
    [13272.01, 250],
    [33181.0, 250],
    [33181.01, 500],
    [66361.0, 500],
  ];

  it.each(granice)("VPS %s € → %s bodova", (vps, ocekivano) => {
    expect(tbr71Bodovi(vps)).toBe(ocekivano);
  });
});

describe("Tbr. 7. toč. 1 — progresivni razredi", () => {
  // "nagradu od 1 boda za svakih započetih 133,00 / 265,00 / 664,00 eura"
  const progresivni: [number, number][] = [
    [66361.01, 501],
    [66494.0, 501],
    [66494.01, 502],
    [663614.0, 4991],
    [663614.01, 4992],
    [1327228.0, 7496],
    [1327228.01, 7497],
  ];

  it.each(progresivni)("VPS %s € → %s bodova", (vps, ocekivano) => {
    expect(tbr71Bodovi(vps)).toBe(ocekivano);
  });

  it("nikad ne prelazi 10.000 bodova (Tbr. 7. toč. 1, zadnja rečenica)", () => {
    expect(tbr71Bodovi(100_000_000)).toBe(10_000);
  });
});
