import { describe, expect, it } from "vitest";
import {
  NACIN_NAKNADE,
  TBR50_FAKTOR_GORIVA,
  VRSTA_TROSKA,
  VRSTA_TROSKA_LABELE,
  VRSTA_TROSKA_DOPUSTA_IZRACUN,
  izracunajNaknaduStavku,
  zaokruziNovac,
} from "@/lib/tarifa";

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

/**
 * Tbr. 50 (km) i Tbr. 51 (dnevnica) — izračun umjesto ručnog unosa. Zrcali
 * Backend.Tests/TarifaNaknadaTroskovaTests.cs (Prijevoz_Izracun_.../Dnevnica_Izracun_...).
 */
describe("izracunajNaknaduStavku — Tbr. 50/51 izračun", () => {
  it("rucno (zadano) vraća upisani iznos, ignorira količinu/jedinični iznos", () => {
    expect(
      izracunajNaknaduStavku({ vrsta: VRSTA_TROSKA.OstaliIzdaci, iznos: 42 }),
    ).toBe(42);
  });

  it("Prijevoz: količina × jedinični iznos + cestarina, isto kao ekvivalentan ručni iznos", () => {
    // 340 km × 0,459 €/km (= 1,53 €/l × 0,30) + 12,50 € cestarina = 168,56 €.
    const izracunato = izracunajNaknaduStavku({
      vrsta: VRSTA_TROSKA.Prijevoz,
      iznos: 0,
      nacin: NACIN_NAKNADE.Izracun,
      kolicina: 340,
      jedinicniIznos: 0.459,
      cestarina: 12.5,
    });

    expect(izracunato).toBe(168.56);
  });

  it("Dnevnica: količina × jedinični iznos, bez cestarine", () => {
    const izracunato = izracunajNaknaduStavku({
      vrsta: VRSTA_TROSKA.Dnevnica,
      iznos: 0,
      nacin: NACIN_NAKNADE.Izracun,
      kolicina: 3,
      jedinicniIznos: 19.91,
      cestarina: 999, // ne smije ući u zbroj — cestarina vrijedi samo za Prijevoz
    });

    expect(izracunato).toBe(59.73);
  });

  it("nedostajuća količina/jedinični iznos tretira se kao 0 (živi pregled, ne baca)", () => {
    expect(
      izracunajNaknaduStavku({ vrsta: VRSTA_TROSKA.Prijevoz, iznos: 0, nacin: NACIN_NAKNADE.Izracun }),
    ).toBe(0);
  });

  it("Tbr. 50 faktor goriva je 30%", () => {
    expect(TBR50_FAKTOR_GORIVA).toBe(0.3);
  });

  it("izračun je dopušten samo za Prijevoz i Dnevnicu", () => {
    expect(VRSTA_TROSKA_DOPUSTA_IZRACUN).toEqual([VRSTA_TROSKA.Prijevoz, VRSTA_TROSKA.Dnevnica]);
    expect(VRSTA_TROSKA_DOPUSTA_IZRACUN).not.toContain(VRSTA_TROSKA.HotelskiSmjestaj);
    expect(VRSTA_TROSKA_DOPUSTA_IZRACUN).not.toContain(VRSTA_TROSKA.OstaliIzdaci);
  });
});
