import { describe, expect, it } from "vitest";
import {
  KATEGORIJA_KAZNENI,
  KATEGORIJA_OVRSNI,
  KATEGORIJA_PARNICNI,
  izracunajSudskuPristojbu,
  odrediPristojbuOsnovu,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/SudskePristojbeTests.cs — iste očekivane vrijednosti.
 * Uredba o tarifi sudskih pristojbi, NN 37/2023, Tar. br. 1.
 */

function stavka(kategorija: string, id: string): TarifnaStavka {
  return {
    id,
    kategorija,
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
  };
}

describe("Tar. br. 1 st. 1 — puna pristojba", () => {
  it.each([
    [300, 13.27],
    [500, 26.54],
    [1_000, 39.82],
    [1_500, 53.09],
    [1_900, 66.36],
    [3_000, 76.45],
    [50_000, 546.45],
    [500_000, 663.61], // kapa
    [10_000_000, 663.61],
  ])("VPS %s € → %s €", (vps, ocekivano) => {
    expect(izracunajSudskuPristojbu(vps, "Puna")).toBe(ocekivano);
  });
});

describe("Tar. br. 1 st. 2 — polovica u ovrsi", () => {
  it.each([
    [3_000, 38.23],
    [300, 6.64],
    [50_000, 273.23],
    [500_000, 331.81],
  ])("VPS %s € → %s €", (vps, ocekivano) => {
    expect(izracunajSudskuPristojbu(vps, "Polovica")).toBe(ocekivano);
  });
});

describe("kazneni postupak", () => {
  it("nema pristojbe bez obzira na VPS", () => {
    expect(izracunajSudskuPristojbu(50_000, "Nema")).toBe(0);
  });
});

describe("odrediPristojbuOsnovu", () => {
  it("samo kaznene stavke → Nema", () => {
    expect(odrediPristojbuOsnovu([stavka(KATEGORIJA_KAZNENI, "kaz_obrana")])).toBe("Nema");
  });

  it("samo ovršne stavke → Polovica", () => {
    expect(odrediPristojbuOsnovu([stavka(KATEGORIJA_OVRSNI, "ovr_prijedlog")])).toBe("Polovica");
  });

  it("ovršne uz parnične → Puna", () => {
    expect(
      odrediPristojbuOsnovu([
        stavka(KATEGORIJA_OVRSNI, "ovr_prijedlog"),
        stavka(KATEGORIJA_PARNICNI, "par_tuzba"),
      ]),
    ).toBe("Puna");
  });

  it("parnične stavke → Puna", () => {
    expect(odrediPristojbuOsnovu([stavka(KATEGORIJA_PARNICNI, "par_tuzba")])).toBe("Puna");
  });
});

describe("maksimalna pristojba", () => {
  it("je 663,61 € iz NN 37/2023, ne 3.981,68 € iz stare HRK tarife", () => {
    expect(izracunajSudskuPristojbu(1_000_000, "Puna")).toBe(663.61);
  });
});
