import { describe, expect, it } from "vitest";
import {
  BOD_VALUE,
  formatirajBodove,
  izracunajBodove,
  tbr71Bodovi,
  zaokruziNovac,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaZaokruzivanjeTests.cs.
 *
 * Bodovi se ne zaokružuju. Novac se zaokružuje jednom, na kraju, na dvije decimale.
 */

function stavka(over: Partial<TarifnaStavka>): TarifnaStavka {
  return {
    id: "x",
    kategorija: "Izvanparnični postupak",
    opis: "",
    tarifni_broj: "",
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...over,
  };
}

// Tbr. 18. toč. 1 — 50% nagrade iz Tbr. 16, uvećano za 25%.
const izvPravniLijek = stavka({
  id: "izv_pravni_lijek",
  tbr71_postotak: 0.5,
  tbr71_povecanje: 0.25,
});

describe("test iz audita — izv_pravni_lijek pri VPS-u 1.000 €", () => {
  const baza = tbr71Bodovi(1_000); // 75

  it("osnovica je 75 bodova", () => {
    expect(baza).toBe(75);
  });

  it("daje 46,875 bodova, a nikako 48", () => {
    const bodovi = izracunajBodove(izvPravniLijek, baza);

    expect(bodovi).toBeCloseTo(46.875, 10);
    expect(bodovi).not.toBe(48);
  });

  it("standardno zaokruženo je 47", () => {
    expect(Math.round(izracunajBodove(izvPravniLijek, baza))).toBe(47);
  });

  it("iznos je 93,75 €", () => {
    expect(zaokruziNovac(izracunajBodove(izvPravniLijek, baza) * BOD_VALUE)).toBe(93.75);
  });
});

describe("zaokruziNovac", () => {
  it.each([
    [23.4375, 23.44],
    [38.2258, 38.23],
    [93.75, 93.75],
    [0.005, 0.01],
  ])("%s → %s", (ulaz, ocekivano) => {
    expect(zaokruziNovac(ulaz)).toBe(ocekivano);
  });
});

describe("formatirajBodove", () => {
  it("cijeli broj bez decimala", () => {
    expect(formatirajBodove(100)).toBe("100");
  });

  it("decimalni s najviše tri znamenke", () => {
    expect(formatirajBodove(46.875)).toBe("46,875");
  });
});

describe("Tbr. 7. toč. 1 zadržava zaokruživanje na započeti iznos", () => {
  it.each([
    [66_361.01, 501],
    [66_494.0, 501],
    [66_494.01, 502],
  ])("VPS %s € → %i bodova", (vps, ocekivano) => {
    expect(tbr71Bodovi(vps)).toBe(ocekivano);
  });
});
