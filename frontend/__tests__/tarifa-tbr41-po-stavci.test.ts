import { describe, expect, it } from "vitest";
import {
  provjeriTbr41Stavku,
  razrijesiTbr41,
  tbr41Izvor,
  tbr41Udio,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaTbr41PoStavciTests.cs.
 *
 * Tbr. 41. toč. 2: "POJEDINE tarifne stavke odvjetnik može sniziti za 50%."
 * Riječ "pojedine" stoji samo u toč. 2 — prilagodba se veže uz stavku.
 */

// VPS 50.000 €: tužba 500 bod., ročište 500 bod., žalba 625 bod.
const TUZBA = 500;
const ROCISTE = 500;
const ZALBA = 625;

function doprinos(bodovi: number, povisenje: number, snizenje: number): number {
  return bodovi * tbr41Udio(povisenje, snizenje);
}

describe("prilagodba po pojedinoj stavci", () => {
  it("sniženje djeluje samo na svoju stavku", () => {
    expect(doprinos(TUZBA, 0, 50)).toBe(-250);
    expect(doprinos(ROCISTE, 0, 0)).toBe(0);
  });

  it("povišenje djeluje samo na svoju stavku", () => {
    expect(doprinos(TUZBA, 100, 0)).toBe(500);
  });

  it("različite stavke, različiti postoci", () => {
    const ukupno = doprinos(TUZBA, 100, 0) + doprinos(ROCISTE, 0, 50) + doprinos(ZALBA, 0, 0);

    expect(ukupno).toBe(250); // +500 − 250
  });

  it("količina ulazi u izračun", () => {
    expect(doprinos(ROCISTE * 2, 0, 50)).toBe(-500);
  });
});

describe("prečac „primijeni na sve stavke\"", () => {
  it("stavka bez vlastitog postotka nasljeđuje prečac", () => {
    expect(razrijesiTbr41({}, 0, 50)).toEqual({ povisenje: 0, snizenje: 50 });
  });

  it("vlastiti postotak nadjačava prečac", () => {
    expect(razrijesiTbr41({ snizenje: 20 }, 0, 50)).toEqual({ povisenje: 0, snizenje: 20 });
  });

  it("izričita nula nadjačava prečac", () => {
    expect(razrijesiTbr41({ povisenje: 0, snizenje: 0 }, 0, 50)).toEqual({
      povisenje: 0,
      snizenje: 0,
    });
  });
});

describe("povišenje i sniženje se isključuju", () => {
  it("oba na istoj stavci → poruka", () => {
    const poruka = provjeriTbr41Stavku("Tužba", 50, 20);

    expect(poruka).toContain("Tbr. 41");
    expect(poruka).toContain("istovremeno");
  });

  it.each([
    [150, 0],
    [0, 80],
    [-50, 0],
  ])("postotak izvan raspona (%i / %i) → poruka", (povisenje, snizenje) => {
    expect(provjeriTbr41Stavku("Tužba", povisenje, snizenje)).not.toBeNull();
  });

  it("valjana kombinacija prolazi", () => {
    expect(provjeriTbr41Stavku("Tužba", 100, 0)).toBeNull();
    expect(provjeriTbr41Stavku("Tužba", 0, 50)).toBeNull();
    expect(provjeriTbr41Stavku("Tužba", 0, 0)).toBeNull();
  });
});

describe("regresija — sniženje 50 % na sve stavke", () => {
  const OSNOVICA = TUZBA + ROCISTE + ZALBA; // 1.625

  it("daje isti ukupan iznos kao stari izračun na razini zbroja", () => {
    const poStavci =
      doprinos(TUZBA, 0, 50) + doprinos(ROCISTE, 0, 50) + doprinos(ZALBA, 0, 50);

    expect(OSNOVICA + poStavci).toBe(OSNOVICA * 0.5);
    expect(OSNOVICA + poStavci).toBe(812.5);
  });

  it("ostaje aditivna i uz Tbr. 40 (+10 %)", () => {
    const poStavci =
      doprinos(TUZBA, 0, 50) + doprinos(ROCISTE, 0, 50) + doprinos(ZALBA, 0, 50);
    const tbr40 = OSNOVICA * 0.1;

    expect(OSNOVICA + tbr40 + poStavci).toBe(975);
    // Multiplikativno po stavci dalo bi 812,5 × 1,1 = 893,75 — drukčiji iznos.
    expect(OSNOVICA + tbr40 + poStavci).not.toBe(893.75);
  });
});

describe("citat odredbe", () => {
  it("snižena stavka nosi toč. 2", () => {
    const izvor = tbr41Izvor(0, 50);

    expect(izvor).toContain("Tbr. 41. toč. 2");
    expect(izvor).toContain("50");
    expect(izvor.toLowerCase()).toContain("pojedine tarifne stavke");
  });

  it("povišena stavka nosi toč. 1", () => {
    const izvor = tbr41Izvor(100, 0);

    expect(izvor).toContain("Tbr. 41. toč. 1");
    expect(izvor).toContain("100");
  });

  it("bez prilagodbe nema citata", () => {
    expect(tbr41Izvor(0, 0)).toBe("");
  });
});
