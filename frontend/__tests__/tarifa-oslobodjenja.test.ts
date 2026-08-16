import { describe, expect, it } from "vitest";
import {
  OSLOBODJENJE_KATEGORIJE,
  obracunajPristojbu,
  provjeriZahtjev,
  type OslobodjenjeUlaz,
} from "@/lib/tarifa";

/**
 * Oslobođenja od plaćanja sudskih pristojbi — Zakon o sudskim pristojbama
 * (NN 118/18, 51/23), čl. 10–13. Zrcali `OslobodjenjaTests` na backendu.
 *
 * RUČNI IZRAČUN — Tar. br. 1 st. 1, progresija iznad 1.990,84 €:
 *   20.000 € → 66,36 + 1 % × 18.009,16 = 246,45 €
 *   15.000 € → 196,45 €   10.000 € → 146,45 €   5.000 € → 96,45 €
 */

const PRISTOJBA_NA_20000 = 246.45;
const PRISTOJBA_NA_10000 = 146.45;

const ulaz = (dopuna: Partial<OslobodjenjeUlaz> = {}): OslobodjenjeUlaz => ({
  oslobodjen: false,
  brojOsoba: 1,
  brojOslobodjenih: 0,
  ...dopuna,
});

describe("Oslobođenja — čl. 11 katalog", () => {
  it("ima svih 21 točku čl. 11 i oslobođenje na zahtjev po čl. 10", () => {
    const kljucevi = Object.keys(OSLOBODJENJE_KATEGORIJE);

    expect(kljucevi).toHaveLength(22);
    expect(kljucevi).toContain("zahtjev_cl_10");
    expect(OSLOBODJENJE_KATEGORIJE.radnici).toContain("čl. 11. t. 3");
    expect(OSLOBODJENJE_KATEGORIJE.mirovinsko_zdravstveno).toContain("čl. 11. t. 16");
  });
});

describe("Oslobođenja — potpuno oslobođenje", () => {
  it("daje nulu bez obzira na VPS", () => {
    expect(obracunajPristojbu(20_000, "Puna", ulaz({ oslobodjen: true })).iznos).toBe(0);
  });
});

describe("Oslobođenja — čl. 12 dio suparničara", () => {
  /**
   * Umanjuje se OSNOVICA, ne gotov iznos: dvoje od četvero oslobođeno → pristojba se
   * računa na 10.000 €, što daje 146,45 €, a ne polovicu od 246,45 € (123,23 €).
   */
  it("umanjuje osnovicu, ne gotov iznos", () => {
    const r = obracunajPristojbu(20_000, "Puna", ulaz({ brojOsoba: 4, brojOslobodjenih: 2 }));

    expect(r.vpsUdio).toBe(10_000);
    expect(r.iznos).toBe(PRISTOJBA_NA_10000);
    expect(r.iznos).not.toBe(PRISTOJBA_NA_20000 / 2);
  });

  it.each([
    [4, 1, 15_000, 196.45],
    [4, 2, 10_000, 146.45],
    [4, 3, 5_000, 96.45],
  ])("bez upisanog udjela dijeli VPS brojem osoba (%s osoba, %s oslobođeno)", (
    ukupno,
    oslobodjeni,
    ocekivaniUdio,
    ocekivanaPristojba,
  ) => {
    const r = obracunajPristojbu(
      20_000,
      "Puna",
      ulaz({ brojOsoba: ukupno, brojOslobodjenih: oslobodjeni }),
    );

    expect(r.vpsUdio).toBe(ocekivaniUdio);
    expect(r.iznos).toBe(ocekivanaPristojba);
  });

  it("upisani udio ima prednost pred diobom", () => {
    const r = obracunajPristojbu(
      20_000,
      "Puna",
      ulaz({ brojOsoba: 4, brojOslobodjenih: 1, udioVps: 5_000 }),
    );

    expect(r.vpsUdio).toBe(5_000);
    expect(r.iznos).toBe(96.45);
  });

  it("kad su oslobođeni svi, rezultat je isti kao kod golog prekidača", () => {
    const r = obracunajPristojbu(20_000, "Puna", ulaz({ brojOsoba: 3, brojOslobodjenih: 3 }));

    expect(r.iznos).toBe(0);
    expect(r.vpsUdio).toBe(0);
  });

  it("više oslobođenih nego zastupanih daje poruku", () => {
    const greske = provjeriZahtjev([], {
      isOduzimanjePrekrsaj: false,
      vrijednostOduzimanja: 0,
      oslobodjenje: ulaz({ brojOsoba: 2, brojOslobodjenih: 3 }),
    });

    expect(greske).toEqual([expect.stringContaining("Čl. 12")]);
  });
});

describe("Oslobođenja — čl. 13 teret protivne stranke", () => {
  it("potpuno oslobođenje, 60 % uspjeha → 246,45 × 0,6 = 147,87 €", () => {
    const r = obracunajPristojbu(20_000, "Puna", ulaz({ oslobodjen: true, postotakUspjeha: 60 }));

    expect(r.naTeretProtustranke).toBe(147.87);
  });

  it("djelomično oslobođenje veže se samo uz dio oslobođenih", () => {
    const r = obracunajPristojbu(
      20_000,
      "Puna",
      ulaz({ brojOsoba: 4, brojOslobodjenih: 2, postotakUspjeha: 60 }),
    );

    expect(r.naTeretProtustranke).toBe(87.87);
  });

  it("bez ijednog oslobođenja nema što premjestiti", () => {
    const r = obracunajPristojbu(20_000, "Puna", ulaz({ postotakUspjeha: 100 }));

    expect(r.naTeretProtustranke).toBe(0);
    expect(r.iznos).toBe(PRISTOJBA_NA_20000);
  });

  it.each([-1, 101])("omjer uspjeha %s %% daje poruku", (postotak) => {
    const greske = provjeriZahtjev([], {
      isOduzimanjePrekrsaj: false,
      vrijednostOduzimanja: 0,
      oslobodjenje: ulaz({ oslobodjen: true, postotakUspjeha: postotak }),
    });

    expect(greske).toEqual([expect.stringContaining("čl. 13")]);
  });
});

describe("Oslobođenja — regresija", () => {
  it("bez ijednog novog polja pristojba je ista kao prije", () => {
    const r = obracunajPristojbu(20_000, "Puna");

    expect(r.iznos).toBe(PRISTOJBA_NA_20000);
    expect(r.vpsUdio).toBe(20_000);
    expect(r.naTeretProtustranke).toBe(0);
  });
});
