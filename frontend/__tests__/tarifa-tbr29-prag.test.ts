import { describe, expect, it } from "vitest";
import { izracunajBodove, type OsnovicaRezultat, type TarifnaStavka } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTbr29Tests.cs.
 *
 * Tbr. 29. toč. 1 veže prag "ne manje od 100 bodova" uz nagradu iz toč. 1, a toč. 2 i 3
 * uzimaju POSTOTAK te nagrade. Prag se time skalira s postotkom: 50% od "ne manje od 100"
 * je "ne manje od 50", a ne "ne manje od 100".
 *
 * Motor primjenjuje min_bodova NAKON postotka (clampBodovi), pa je u katalog upisan
 * skalirani prag. Kako je prag konstanta, vrijedi max(x, 100) × p ≡ max(p·x, 100p).
 *
 * Ovaj test čuva živi pregled: definicije dolaze s backenda preko /api/troskovnik/katalog,
 * pa se ovdje zrcale točno ona polja koja katalog nosi.
 */

function stavka(over: Partial<TarifnaStavka>): TarifnaStavka {
  return {
    id: "x",
    kategorija: "Registarski postupak",
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

/** Osnovica po Tbr. 7. toč. 1; VPS 200 € pada u razred do 332,00 € → 25 bodova. */
function osnovica(bodovi: number): OsnovicaRezultat {
  return { bodovi, pravniIzvor: "", fiksnoPoRadnji: false };
}

// Definicije preslikane iz kataloga (TroskovnikService._katalog), grana sudskog registra.
const toc1 = stavka({
  id: "reg_ponistaj_tuzba_sudski",
  tarifni_broj: "Tbr. 29.1",
  tbr71_postotak: 1.0,
  min_bodova: 100,
});
const ostalaRocista = stavka({
  id: "reg_ponistaj_rociste_ostalo_sudski",
  tarifni_broj: "Tbr. 29.2",
  tbr71_postotak: 0.5,
  min_bodova: 50,
});
const lijekPresuda = stavka({
  id: "reg_ponistaj_lijek_presuda_sudski",
  tarifni_broj: "Tbr. 29.3",
  tbr71_postotak: 1.0,
  tbr71_povecanje: 0.25,
  min_bodova: 125,
});

describe("Tbr. 29 — prag od 100 bodova, sudski registar", () => {
  describe("ispod praga (VPS 200 € → 25 bodova po Tbr. 7. toč. 1)", () => {
    const o = osnovica(25);

    it("toč. 1 podiže 25 na prag od 100", () => {
      expect(izracunajBodove(toc1, o)).toBe(100);
    });

    it("ostala ročišta daju 50, ne 100 — prag skaliran s 50%", () => {
      expect(izracunajBodove(ostalaRocista, o)).toBe(50);
    });

    it("pravni lijek protiv presude daje 125 — prag skaliran s 125%", () => {
      expect(izracunajBodove(lijekPresuda, o)).toBe(125);
    });

    it("toč. 2 i 3 ostaju točan postotak toč. 1", () => {
      const t1 = izracunajBodove(toc1, o);

      expect(izracunajBodove(ostalaRocista, o)).toBe(t1 * 0.5);
      expect(izracunajBodove(lijekPresuda, o)).toBe(t1 * 1.25);
    });
  });

  describe("iznad praga (VPS 50.000 € → 500 bodova po Tbr. 7. toč. 1)", () => {
    const o = osnovica(500);

    it("prag ne veže, iznosi idu čisto po Tbr. 7. toč. 1", () => {
      expect(izracunajBodove(toc1, o)).toBe(500);
      expect(izracunajBodove(ostalaRocista, o)).toBe(250);
      expect(izracunajBodove(lijekPresuda, o)).toBe(625);
    });

    it("toč. 2 i 3 ostaju točan postotak toč. 1", () => {
      const t1 = izracunajBodove(toc1, o);

      expect(izracunajBodove(ostalaRocista, o)).toBe(t1 * 0.5);
      expect(izracunajBodove(lijekPresuda, o)).toBe(t1 * 1.25);
    });
  });
});
