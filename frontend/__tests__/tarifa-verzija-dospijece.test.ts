import { describe, expect, it } from "vitest";
import {
  BOD_VALUE,
  TARIFA_TRENUTNA,
  razrijesiTarifu,
  razrijesiTarifuIz,
  type TarifaVerzija,
} from "@/lib/tarifa";

/**
 * Tbr. 52 — mjerodavna verzija Tarife i vrijednost boda biraju se prema DOSPIJEĆU, ne
 * prema datumu radnje. Zrcalo backenda (`TarifaVerzije`). Selekcija se provjerava i sa
 * sintetičkim popisom od dvije verzije, jer je registar za sada jednočlan.
 */
describe("razrijesiTarifu — odabir verzije po dospijeću", () => {
  const dvije: readonly TarifaVerzija[] = [
    { naziv: "Stara Tarifa", bodValue: 1.5, vrijediOd: "2019-01-01" },
    { naziv: "Nova Tarifa", bodValue: 2.0, vrijediOd: "2023-10-14" },
  ];

  it("dospijeće nakon nove verzije bira novu", () => {
    expect(razrijesiTarifuIz(dvije, "2024-05-01").naziv).toBe("Nova Tarifa");
  });

  it("dospijeće između verzija bira staru", () => {
    expect(razrijesiTarifuIz(dvije, "2020-06-01").naziv).toBe("Stara Tarifa");
  });

  it("dospijeće prije najstarije bira najstariju", () => {
    expect(razrijesiTarifuIz(dvije, "2000-01-01").naziv).toBe("Stara Tarifa");
  });

  it("prazno dospijeće bira trenutnu (najnoviju)", () => {
    expect(razrijesiTarifuIz(dvije, null).naziv).toBe("Nova Tarifa");
    expect(razrijesiTarifuIz(dvije, "").naziv).toBe("Nova Tarifa");
  });

  it("na sam dan stupanja na snagu vrijedi ta verzija", () => {
    expect(razrijesiTarifuIz(dvije, "2023-10-14").naziv).toBe("Nova Tarifa");
  });

  it("stvarni registar ima jednu verziju s bodom 2,00 €", () => {
    expect(TARIFA_TRENUTNA.bodValue).toBe(2.0);
    expect(TARIFA_TRENUTNA.bodValue).toBe(BOD_VALUE);
  });

  it("svaki datum razrješava na jedinu verziju (jedna verzija u registru)", () => {
    expect(razrijesiTarifu("2024-03-01")).toEqual(TARIFA_TRENUTNA);
    expect(razrijesiTarifu(null)).toEqual(TARIFA_TRENUTNA);
  });
});
