import { describe, expect, it } from "vitest";
import { provjeriZahtjev, type TarifnaStavka } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaValidacijaTests.cs.
 *
 * Sučelje ne smije prikazati redak s 0 bodova — mora reći što nedostaje.
 */

function stavka(over: Partial<TarifnaStavka>): TarifnaStavka {
  return {
    id: "x",
    kategorija: "Kazneni postupak",
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

const kaznenaStavka = stavka({ id: "kaz_obrana", kazneni_base: true });
const oduzimanje = stavka({ id: "kaz_prekrsaj_oduzimanje", prekrsaj_oduzimanje: true });
const parnicna = stavka({ id: "par_tuzba", kategorija: "Parnični postupak", tbr71_postotak: 1 });

const bezOduzimanja = { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0 };

describe("zapriječena kazna", () => {
  it("kaznena stavka bez odabrane kazne javlja nedostatak", () => {
    const greske = provjeriZahtjev([kaznenaStavka], bezOduzimanja);

    expect(greske).toHaveLength(1);
    expect(greske[0]).toContain("zapriječena kazna");
  });

  it("nepoznat ključ kazne javlja nedostatak", () => {
    const greske = provjeriZahtjev([kaznenaStavka], {
      ...bezOduzimanja,
      zaprijecenaKazna: "do_1",
    });

    expect(greske[0]).toContain("do_1");
  });

  it("valjana kazna prolazi", () => {
    expect(
      provjeriZahtjev([kaznenaStavka], { ...bezOduzimanja, zaprijecenaKazna: "do_5" }),
    ).toEqual([]);
  });

  it("parnična stavka ne traži zapriječenu kaznu", () => {
    expect(provjeriZahtjev([parnicna], bezOduzimanja)).toEqual([]);
  });
});

describe("oduzimanje predmeta", () => {
  it("uključeno oduzimanje bez vrijednosti javlja nedostatak", () => {
    const greske = provjeriZahtjev([oduzimanje], {
      isOduzimanjePrekrsaj: true,
      vrijednostOduzimanja: 0,
    });

    expect(greske[0]).toContain("oduzimanje");
  });

  it("uključeno oduzimanje s vrijednošću prolazi", () => {
    expect(
      provjeriZahtjev([oduzimanje], {
        isOduzimanjePrekrsaj: true,
        vrijednostOduzimanja: 10_000,
      }),
    ).toEqual([]);
  });

  it("isključeno oduzimanje prolazi — Tbr. 4. toč. 1 al. 7 daje 100 bodova", () => {
    expect(provjeriZahtjev([oduzimanje], bezOduzimanja)).toEqual([]);
  });
});

describe("prazan odabir", () => {
  it("nema nedostataka", () => {
    expect(provjeriZahtjev([], bezOduzimanja)).toEqual([]);
  });
});
