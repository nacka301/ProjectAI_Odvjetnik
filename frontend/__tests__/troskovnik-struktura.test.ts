import { describe, expect, it } from "vitest";
import { pomakniRedak, zbrojiRucneTroskove, ZADANA_NAPOMENA, zaokruziNovac } from "@/lib/tarifa";

/**
 * Struktura i ispis troškovnika: premještanje redaka, renumeriranje, zrcalo
 * zbrajanja ručno unesenih troškova (pristojbe/predujmovi), i napomena.
 *
 * Paritet s Backend.Tests/TroskovnikStrukturaTests.cs — iste očekivane vrijednosti.
 */

describe("pomakniRedak", () => {
  const lista = ["a", "b", "c"];

  it("▲ zamjenjuje sa susjedom gore", () => {
    expect(pomakniRedak(lista, 1, -1)).toEqual(["b", "a", "c"]);
  });

  it("▼ zamjenjuje sa susjedom dolje", () => {
    expect(pomakniRedak(lista, 1, 1)).toEqual(["a", "c", "b"]);
  });

  it("prvi ▲ ne mijenja ništa (rub)", () => {
    expect(pomakniRedak(lista, 0, -1)).toEqual(lista);
  });

  it("zadnji ▼ ne mijenja ništa (rub)", () => {
    expect(pomakniRedak(lista, lista.length - 1, 1)).toEqual(lista);
  });

  it("ne mutira izvorno polje", () => {
    const original = ["a", "b", "c"];
    const rezultat = pomakniRedak(original, 0, 1);
    expect(original).toEqual(["a", "b", "c"]);
    expect(rezultat).not.toBe(original);
  });
});

describe("renumeriranje (index + 1)", () => {
  it("brisanje srednjeg retka renumerira preostale", () => {
    const redci = [{ uid: "1" }, { uid: "2" }, { uid: "3" }];
    const nakonBrisanja = redci.filter((r) => r.uid !== "2");
    const rbrovi = nakonBrisanja.map((_, i) => i + 1);

    expect(nakonBrisanja.map((r) => r.uid)).toEqual(["1", "3"]);
    expect(rbrovi).toEqual([1, 2]);
  });

  it("premještanje mijenja poredak, rbr slijedi novu poziciju", () => {
    const redci = [{ uid: "1" }, { uid: "2" }, { uid: "3" }];
    const pomaknuto = pomakniRedak(redci, 0, 1); // "1" ide na drugo mjesto

    expect(pomaknuto.map((r) => r.uid)).toEqual(["2", "1", "3"]);
    expect(pomaknuto.map((_, i) => i + 1)).toEqual([1, 2, 3]);
  });
});

describe("zbrojiRucneTroskove — paritet s backendom", () => {
  it("zaokružuje svaku stavku zasebno prije zbrajanja", () => {
    const zbroj = zbrojiRucneTroskove([
      { opis: "a", iznos: 10.005 },
      { opis: "b", iznos: 20.005 },
    ]);
    expect(zbroj).toBe(zaokruziNovac(10.005) + zaokruziNovac(20.005));
  });

  it("prazan popis daje 0", () => {
    expect(zbrojiRucneTroskove([])).toBe(0);
  });

  /**
   * Ista scenarij kao Backend.Tests.TroskovnikStrukturaTests.PdvSeNeObracunavaNaPristojbeNiPredujmove:
   * nagrada 1.000 € + PDV 250 € + pristojba 100 € + predujam 200 € = 1.550 €, ne 1.625 €.
   * Ovdje se provjerava samo zrcalo zbrajanja ručnih popisa — PDV izračun je u lib/tarifa.ts
   * odvojen i ne dira ova dva popisa (vidi izracunajSudskuPristojbu / PDV_RATE primjenu u page.tsx).
   */
  it("pristojba i predujam zbrajaju se odvojeno, bez PDV-a", () => {
    const ukupnoPristojbe = zbrojiRucneTroskove([{ opis: "Pristojba na presudu", iznos: 100 }]);
    const ukupnoPredujmovi = zbrojiRucneTroskove([{ opis: "Vještačenje", iznos: 200 }]);
    const nagrada = 1_000;
    const pdv = 250;

    expect(ukupnoPristojbe).toBe(100);
    expect(ukupnoPredujmovi).toBe(200);
    expect(nagrada + pdv + ukupnoPristojbe + ukupnoPredujmovi).toBe(1_550);
  });
});

describe("ZADANA_NAPOMENA", () => {
  it("doslovno jednaka TarifaConstants.ZadanaNapomena na backendu", () => {
    expect(ZADANA_NAPOMENA).toBe(
      "Na traženi iznos obračunava se zakonska zatezna kamata sukladno članku 29. " +
        "Zakona o obveznim odnosima.",
    );
  });
});
