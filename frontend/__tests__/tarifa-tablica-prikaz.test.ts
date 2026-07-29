import { describe, expect, it } from "vitest";
import { TBR_7_1_RAZREDI, tbr71Bodovi } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTablicaPrikazTests.cs.
 *
 * Tablica se mora prikazivati točno kako stoji u Tbr. 7. toč. 1 — s decimalama i
 * bez preklapanja: 0–332,00 pa 332,01–664,00.
 */

describe("Tbr. 7. toč. 1 — tablica za prikaz", () => {
  it("granice se ne preklapaju", () => {
    for (let i = 1; i < TBR_7_1_RAZREDI.length; i++) {
      const prethodniDo = TBR_7_1_RAZREDI[i - 1].do;
      expect(prethodniDo).not.toBeNull();
      expect(TBR_7_1_RAZREDI[i].od).toBeCloseTo(prethodniDo! + 0.01, 2);
    }
  });

  it("prvi razred počinje na nuli", () => {
    expect(TBR_7_1_RAZREDI[0].od).toBe(0);
  });

  it("posljednji razred je neograničen i navodi kapu", () => {
    const zadnji = TBR_7_1_RAZREDI[TBR_7_1_RAZREDI.length - 1];

    expect(zadnji.do).toBeNull();
    expect(zadnji.od).toBe(1_327_228.01);
    expect(zadnji.bodovi).toContain("10.000");
  });

  it("prikazane granice odgovaraju izračunu", () => {
    for (let i = 0; i < 6; i++) {
      const razred = TBR_7_1_RAZREDI[i];
      const ocekivano = Number(razred.bodovi);

      expect(tbr71Bodovi(razred.od!)).toBe(ocekivano);
      expect(tbr71Bodovi(razred.do!)).toBe(ocekivano);
    }
  });

  it.each([
    [6, "133,00"],
    [7, "265,00"],
    [8, "664,00"],
  ])("progresivni razred %i navodi korak %s iz Tarife", (indeks, korak) => {
    expect(TBR_7_1_RAZREDI[indeks].bodovi).toContain(korak);
  });
});
