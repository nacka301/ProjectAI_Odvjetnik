import { describe, expect, it } from "vitest";
import { ukupnoBodovaZaStavku, type TarifnaStavka } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTbr7Tests.cs — iste očekivane vrijednosti.
 */

function stavka(over: Partial<TarifnaStavka>): TarifnaStavka {
  return {
    id: "x",
    kategorija: "Parnični postupak",
    opis: "",
    tarifni_broj: "",
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "radnja",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...over,
  };
}

const tbr72 = stavka({
  id: "par_tuzba_smetanje",
  fiksni_body: 250,
  jednokratna_nagrada: true,
  ukljucene_radnje: 4,
  daljnja_radnja_bodovi: 50,
});

const tbr73 = stavka({
  id: "par_tuzba_obiteljski",
  fiksni_body: 200,
  jednokratna_nagrada: true,
  ukljucene_radnje: 4,
  daljnja_radnja_bodovi: 50,
});

describe("Tbr. 7. toč. 2 — jednokratna nagrada od 250 bodova", () => {
  it.each([
    [1, 250],
    [4, 250], // "najviše do četiri radnje"
    [5, 300], // 250 + 1 × 50
    [6, 350], // 250 + 2 × 50
    [10, 550],
  ])("količina %i → %i bodova", (kolicina, ocekivano) => {
    expect(ukupnoBodovaZaStavku(tbr72, 250, kolicina)).toBe(ocekivano);
  });
});

describe("Tbr. 7. toč. 3 — jednokratna nagrada od 200 bodova", () => {
  it.each([
    [1, 200],
    [4, 200],
    [5, 250],
    [6, 300],
  ])("količina %i → %i bodova", (kolicina, ocekivano) => {
    expect(ukupnoBodovaZaStavku(tbr73, 200, kolicina)).toBe(ocekivano);
  });
});

describe("Tbr. 8. toč. 1 — odgovor na tužbu + četiri podneska", () => {
  // Nema naknade za daljnje radnje: jedna nagrada pokriva sve, bez obzira na količinu.
  const tbr81 = stavka({
    id: "par_odgovor_tuzba",
    tbr71_postotak: 1.0,
    jednokratna_nagrada: true,
    jedinica: "postupak",
  });

  it.each([1, 3, 6, 20])("količina %i i dalje daje osnovicu", (kolicina) => {
    expect(ukupnoBodovaZaStavku(tbr81, 250, kolicina)).toBe(250);
  });
});

describe("obične stavke", () => {
  it("i dalje se množe količinom", () => {
    const obicna = stavka({ id: "par_rociste_glavno", tbr71_postotak: 1.0 });
    expect(ukupnoBodovaZaStavku(obicna, 250, 3)).toBe(750);
  });
});
