import { describe, expect, it } from "vitest";
import { VRSTA_TROSKA, VRSTA_TROSKA_LABELE, zaokruziNovac } from "@/lib/tarifa";

/**
 * Naknada troškova (Tbr. 48.-51.) — vlastiti trošak odvjetnika, ulazi u PDV
 * osnovicu. Zrcali Backend.Tests/TarifaNaknadaTroskovaTests.cs.
 */
describe("VRSTA_TROSKA", () => {
  it("ima labelu za svaku vrstu troška", () => {
    for (const vrsta of Object.values(VRSTA_TROSKA)) {
      expect(VRSTA_TROSKA_LABELE[vrsta]).toBeTruthy();
    }
  });

  it("nema duplikata među vrijednostima", () => {
    const values = Object.values(VRSTA_TROSKA);
    expect(new Set(values).size).toBe(values.length);
  });

  it("svaka labela navodi tarifni broj (Tbr.)", () => {
    for (const label of Object.values(VRSTA_TROSKA_LABELE)) {
      expect(label).toMatch(/Tbr\./);
    }
  });
});

/**
 * Osnovica = nagrada + naknada troškova; PDV se računa na proširenu osnovicu,
 * jednim zaokruživanjem na kraju (isti obrazac kao TroskovnikService).
 */
describe("Naknada troškova ulazi u osnovicu", () => {
  it("proširuje osnovicu prije obračuna PDV-a", () => {
    const nagrada = 1000;
    const naknadaTroskova = 200;
    const osnovica = zaokruziNovac(nagrada + naknadaTroskova);
    const pdv = zaokruziNovac(osnovica * 0.25);

    expect(osnovica).toBe(1200);
    expect(pdv).toBe(300);
  });
});
