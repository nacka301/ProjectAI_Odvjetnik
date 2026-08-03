import { describe, expect, it } from "vitest";
import { podijeliPoOsobama, povisenjeViseOsobaPostotak, zaokruziNovac } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTbr40DiobaTests.cs.
 *
 * Tbr. 40. toč. 1, drugi odlomak: "Iznos povišene nagrade dijeli se na jednake
 * dijelove prema broju branjenika, odnosno zastupanih osoba."
 */

// Tužba pri VPS-u 50.000 € = 500 bodova po Tbr. 7. toč. 1.
const OSNOVICA = 500;

function ukupnoBodova(brojOsoba: number): number {
  return OSNOVICA * (1 + povisenjeViseOsobaPostotak(brojOsoba));
}

describe("dioba povišene nagrade po Tbr. 40", () => {
  it.each([
    [1, 500, 500, 1_000.0],
    [2, 550, 275, 550.0],
    [4, 650, 162.5, 325.0],
    [5, 700, 140, 280.0],
    [8, 750, 93.75, 187.5], // kapa +50 %
  ])(
    "%i osoba → ukupno %s bod., po osobi %s bod. = %s €",
    (brojOsoba, ukupno, poOsobiBodovi, poOsobiEur) => {
      const ukupnoBod = ukupnoBodova(brojOsoba);

      expect(ukupnoBod).toBeCloseTo(ukupno, 10);
      expect(podijeliPoOsobama(ukupnoBod, brojOsoba)).toBeCloseTo(poOsobiBodovi, 10);
      expect(zaokruziNovac(podijeliPoOsobama(ukupnoBod * 2, brojOsoba))).toBe(poOsobiEur);
    },
  );

  it.each([2, 5, 8])("dioba ne mijenja ukupan iznos (%i osoba)", (brojOsoba) => {
    const ukupnoBod = ukupnoBodova(brojOsoba);

    expect(podijeliPoOsobama(ukupnoBod, brojOsoba) * brojOsoba).toBeCloseTo(ukupnoBod, 10);
  });

  it("jedna osoba — iznos po osobi je cijeli iznos", () => {
    expect(podijeliPoOsobama(1_000, 1)).toBe(1_000);
  });

  it("nevaljan broj osoba se tretira kao jedna", () => {
    expect(podijeliPoOsobama(1_000, 0)).toBe(1_000);
    expect(podijeliPoOsobama(1_000, -3)).toBe(1_000);
  });
});
