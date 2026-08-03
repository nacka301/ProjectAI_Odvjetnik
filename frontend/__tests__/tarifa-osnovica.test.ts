import { describe, expect, it } from "vitest";
import {
  OSNOVICA_TBR7,
  RAZINA_SUDA,
  TBR41_MAX_POVISENJE,
  TBR41_MAX_SNIZENJE,
  TBR45_NACIN,
  RENTA_RAZDOBLJE,
  efektivniVps,
  izracunajBodove,
  izracunajOsnovicu,
  provjeriZahtjev,
  tbr41Udio,
  upozorenjeMaliSpor,
  type OsnovicaUlaz,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaOsnovicaTests.cs, TarifaTbr41Tests.cs i
 * TarifaTbr45Tests.cs — iste očekivane vrijednosti.
 *
 * Tarifa na devet mjesta upućuje na "Tbr. 7. toč. 1., 4., 7. i 8." — četiri
 * varijante jedne osnovice, ne četiri iznosa.
 */

function ulaz(over: Partial<OsnovicaUlaz>): OsnovicaUlaz {
  return {
    osnovicaTbr7: OSNOVICA_TBR7.Toc1,
    vrijednostSpora: 0,
    ...over,
  };
}

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
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...over,
  };
}

describe("osnovica — sve četiri varijante", () => {
  it.each([
    [OSNOVICA_TBR7.Toc1, 1_000, 75],
    // Prag iz toč. 4 al. 1 DJELUJE: tablica bi dala 75, ali "ne manje od 100 bodova".
    [OSNOVICA_TBR7.Toc4Autorsko, 1_000, 100],
    [OSNOVICA_TBR7.Toc7DomacaArbitraza, 1_000, 75],
    // Toč. 7 st. 2 — povišenje od 100 %: 250 → 500.
    [OSNOVICA_TBR7.Toc7MedunarodnaArbitraza, 20_000, 500],
    [OSNOVICA_TBR7.Toc8Mirenje, 20_000, 250],
  ])("%s pri VPS-u %s € → %s bodova", (osnovica, vps, ocekivano) => {
    expect(izracunajOsnovicu(ulaz({ osnovicaTbr7: osnovica, vrijednostSpora: vps }))!.bodovi).toBe(
      ocekivano,
    );
  });

  it("prag iz toč. 4 ne snižava veće iznose", () => {
    // VPS 20.000 → 250 bodova, prag od 100 ne dira.
    expect(
      izracunajOsnovicu(ulaz({ osnovicaTbr7: OSNOVICA_TBR7.Toc4Autorsko, vrijednostSpora: 20_000 }))!
        .bodovi,
    ).toBe(250);
  });

  it("svaka varijanta nosi vlastiti pravni izvor", () => {
    const izvori = Object.values(OSNOVICA_TBR7)
      .filter((o) => o !== OSNOVICA_TBR7.Tbr45Neprocjenjiv)
      .map((o) => izracunajOsnovicu(ulaz({ osnovicaTbr7: o, vrijednostSpora: 1_000 }))!.pravniIzvor);

    expect(new Set(izvori).size).toBe(izvori.length);
    izvori.forEach((i) => expect(i).toContain("Tarifa o nagradama"));
  });
});

describe("Tbr. 7. toč. 4 al. 2 — petogodišnji iznos rente", () => {
  it("mjesečni iznos množi se sa 60", () => {
    expect(
      efektivniVps(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Toc4Renta,
          rentaIznos: 500,
          rentaRazdoblje: RENTA_RAZDOBLJE.Mjesecno,
        }),
      ),
    ).toBe(30_000);
  });

  it("godišnji iznos množi se s 5", () => {
    expect(
      efektivniVps(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Toc4Renta,
          rentaIznos: 6_000,
          rentaRazdoblje: RENTA_RAZDOBLJE.Godisnje,
        }),
      ),
    ).toBe(30_000);
  });

  it("bez odabranog razdoblja ne preračunava se ništa", () => {
    expect(
      efektivniVps(
        ulaz({ osnovicaTbr7: OSNOVICA_TBR7.Toc4Renta, vrijednostSpora: 1_000, rentaIznos: 500 }),
      ),
    ).toBe(1_000);
  });

  it("preračun se ne primjenjuje na druge osnovice", () => {
    expect(
      efektivniVps(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Toc1,
          vrijednostSpora: 1_000,
          rentaIznos: 500,
          rentaRazdoblje: RENTA_RAZDOBLJE.Mjesecno,
        }),
      ),
    ).toBe(1_000);
  });
});

describe("Tbr. 45 — neprocjenjivi predmeti", () => {
  it.each([
    [RAZINA_SUDA.Opcinski, 100],
    [RAZINA_SUDA.ZupanijskiTrgovacki, 200],
    [RAZINA_SUDA.Vrhovni, 500],
  ])("%s → %s bodova", (razina, ocekivano) => {
    const rezultat = izracunajOsnovicu(
      ulaz({
        osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv,
        razinaSuda: razina,
        tbr45Nacin: TBR45_NACIN.KaoOsnovica,
      }),
    );

    expect(rezultat!.bodovi).toBe(ocekivano);
  });

  it("bez razine suda nema iznosa", () => {
    expect(
      izracunajOsnovicu(
        ulaz({ osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv, tbr45Nacin: TBR45_NACIN.KaoOsnovica }),
      ),
    ).toBeNull();
  });

  it("bez načina obračuna nema iznosa", () => {
    expect(
      izracunajOsnovicu(
        ulaz({ osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv, razinaSuda: RAZINA_SUDA.Opcinski }),
      ),
    ).toBeNull();
  });

  it("oba nedostatka javljaju se korisniku", () => {
    const greske = provjeriZahtjev([], {
      isOduzimanjePrekrsaj: false,
      vrijednostOduzimanja: 0,
      osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv,
    });

    expect(greske).toHaveLength(2);
    expect(greske[0]).toContain("razinu suda");
    expect(greske[1]).toContain("način obračuna");
  });

  describe("dva čitanja Tarife", () => {
    // Obrazloženi podnesak po Tbr. 8. toč. 3: 50 % osnovice, najviše 100 bodova.
    const podnesak = stavka({ tbr71_postotak: 0.5, max_bodova: 100 });

    it("kao osnovica — postotak iz Tbr. 8 se primjenjuje", () => {
      const osnovica = izracunajOsnovicu(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv,
          razinaSuda: RAZINA_SUDA.Vrhovni,
          tbr45Nacin: TBR45_NACIN.KaoOsnovica,
        }),
      )!;

      expect(osnovica.fiksnoPoRadnji).toBe(false);
      expect(izracunajBodove(podnesak, osnovica)).toBe(100); // 50 % od 500 = 250, strop 100
    });

    it("fiksno po radnji — postoci i stropovi se ne primjenjuju", () => {
      const osnovica = izracunajOsnovicu(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv,
          razinaSuda: RAZINA_SUDA.Vrhovni,
          tbr45Nacin: TBR45_NACIN.FiksnoPoRadnji,
        }),
      )!;

      expect(osnovica.fiksnoPoRadnji).toBe(true);
      expect(izracunajBodove(podnesak, osnovica)).toBe(500);
    });

    it("apsolutni iznosi iz Tarife ostaju netaknuti i uz fiksno po radnji", () => {
      const osnovica = izracunajOsnovicu(
        ulaz({
          osnovicaTbr7: OSNOVICA_TBR7.Tbr45Neprocjenjiv,
          razinaSuda: RAZINA_SUDA.Vrhovni,
          tbr45Nacin: TBR45_NACIN.FiksnoPoRadnji,
        }),
      )!;

      // Tbr. 7. toč. 10 = 250 bodova, ne ovisi ni o čemu.
      expect(izracunajBodove(stavka({ fiksni_body: 250 }), osnovica)).toBe(250);
    });
  });
});

describe("Tbr. 41 — povisivanje i snižavanje", () => {
  it("zadano je nula, iznos se ne mijenja", () => {
    expect(tbr41Udio(0, 0)).toBe(0);
  });

  it.each([
    [100, 0, 1],
    [50, 0, 0.5],
    [0, 50, -0.5],
    [0, 20, -0.2],
    [30, 10, 0.2],
  ])("povišenje %s %% i sniženje %s %% → udio %s", (povisenje, snizenje, ocekivano) => {
    expect(tbr41Udio(povisenje, snizenje)).toBeCloseTo(ocekivano, 10);
  });

  it("povišenje je ograničeno na 100 % (toč. 1)", () => {
    expect(tbr41Udio(250, 0)).toBe(1);
    expect(TBR41_MAX_POVISENJE).toBe(100);
  });

  it("sniženje je ograničeno na 50 % (toč. 2)", () => {
    expect(tbr41Udio(0, 90)).toBe(-0.5);
    expect(TBR41_MAX_SNIZENJE).toBe(50);
  });

  it("negativan unos se ignorira", () => {
    expect(tbr41Udio(-30, -30)).toBe(0);
  });
});

describe("Tbr. 7. toč. 9 — upozorenje za male sporove", () => {
  const tuzba = stavka({ id: "par_tuzba", tbr71_postotak: 1 });
  const maliSpor = stavka({ id: "par_mali_spor", fiksni_body: 50 });

  it("upozorava kad je VPS ispod 100 € a stavka nije odabrana", () => {
    expect(upozorenjeMaliSpor([tuzba], 80)).toContain("Tbr. 7. toč. 9");
  });

  it("šuti kad je stavka odabrana", () => {
    expect(upozorenjeMaliSpor([tuzba, maliSpor], 80)).toBeNull();
  });

  it("šuti iznad praga", () => {
    expect(upozorenjeMaliSpor([tuzba], 100)).toBeNull();
    expect(upozorenjeMaliSpor([tuzba], 5_000)).toBeNull();
  });

  it("šuti za druge osnovice — toč. 9 se veže uz toč. 1", () => {
    expect(upozorenjeMaliSpor([tuzba], 80, OSNOVICA_TBR7.Toc7MedunarodnaArbitraza)).toBeNull();
  });

  it("šuti kad nema parničnih stavaka po toč. 1", () => {
    const kaznena = stavka({ kategorija: "Kazneni postupak", kazneni_base: true });
    expect(upozorenjeMaliSpor([kaznena], 80)).toBeNull();
  });
});
