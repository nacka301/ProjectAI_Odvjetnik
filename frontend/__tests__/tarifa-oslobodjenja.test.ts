import { describe, expect, it } from "vitest";
import {
  KATEGORIJA_PARNICNI,
  OSLOBODJENJE_KATEGORIJE,
  obracunajPristojbe,
  provjeriZahtjev,
  type OslobodjenjeUlaz,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Oslobođenja od plaćanja sudskih pristojbi — Zakon o sudskim pristojbama
 * (NN 118/18, 51/23), čl. 10–13. Zrcali `OslobodjenjaTests` na backendu.
 *
 * RUČNI IZRAČUN — NN 50/2026, Tar. br. 1 st. 1, progresija iznad 2.000,00 €:
 *   20.000 € → 140,00 + 1 % × 18.000,00 = 320,00 €
 *   15.000 € → 270,00 €   10.000 € → 220,00 €   5.000 € → 170,00 €
 *
 * Po staroj Uredbi (NN 37/2023) isti su VPS-ovi davali 246,45 / 196,45 / 146,45 /
 * 96,45 €. Oslobođenja se nisu promijenila — promijenila se tarifa ispod njih.
 */

const PRISTOJBA_NA_20000 = 320.0;
const PRISTOJBA_NA_10000 = 220.0;

/** Tužba — vodi na Tar. br. 1 st. 1, jedinu odredbu koja ide po VPS-u. */
const TUZBA: TarifnaStavka[] = [
  {
    id: "par_tuzba",
    kategorija: KATEGORIJA_PARNICNI,
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
  },
];

const ulaz = (dopuna: Partial<OslobodjenjeUlaz> = {}): OslobodjenjeUlaz => ({
  oslobodjen: false,
  brojOsoba: 1,
  brojOslobodjenih: 0,
  ...dopuna,
});

const izracunaj = (vps: number, oslobodjenje: OslobodjenjeUlaz = ulaz()) =>
  obracunajPristojbe({
    vrijednostSpora: vps,
    odabraneStavke: TUZBA,
    oslobodjen: oslobodjenje.oslobodjen,
    oslobodjenje,
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
    expect(izracunaj(20_000, ulaz({ oslobodjen: true })).ukupno).toBe(0);
  });

  /**
   * Redak ostaje vidljiv s 0,00 € — "0,00 €" bez obrazloženja ne razlikuje se od
   * greške u računu.
   */
  it("zadržava redak, samo mu nuli iznos", () => {
    const r = izracunaj(20_000, ulaz({ oslobodjen: true }));

    expect(r.stavke).toHaveLength(1);
    expect(r.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 1");
    expect(r.stavke[0].iznos).toBe(0);
  });
});

describe("Oslobođenja — čl. 12 dio suparničara", () => {
  /**
   * Umanjuje se OSNOVICA, ne gotov iznos: dvoje od četvero oslobođeno → pristojba se
   * računa na 10.000 €, što daje 220,00 €, a ne polovicu od 320,00 € (160,00 €).
   */
  it("umanjuje osnovicu, ne gotov iznos", () => {
    const r = izracunaj(20_000, ulaz({ brojOsoba: 4, brojOslobodjenih: 2 }));

    expect(r.vpsUdio).toBe(10_000);
    expect(r.ukupno).toBe(PRISTOJBA_NA_10000);
    expect(r.ukupno).not.toBe(PRISTOJBA_NA_20000 / 2);
  });

  it.each([
    [4, 1, 15_000, 270.0],
    [4, 2, 10_000, 220.0],
    [4, 3, 5_000, 170.0],
  ])("bez upisanog udjela dijeli VPS brojem osoba (%s osoba, %s oslobođeno)", (
    ukupno,
    oslobodjeni,
    ocekivaniUdio,
    ocekivanaPristojba,
  ) => {
    const r = izracunaj(20_000, ulaz({ brojOsoba: ukupno, brojOslobodjenih: oslobodjeni }));

    expect(r.vpsUdio).toBe(ocekivaniUdio);
    expect(r.ukupno).toBe(ocekivanaPristojba);
  });

  it("upisani udio ima prednost pred diobom", () => {
    const r = izracunaj(20_000, ulaz({ brojOsoba: 4, brojOslobodjenih: 1, udioVps: 5_000 }));

    expect(r.vpsUdio).toBe(5_000);
    expect(r.ukupno).toBe(170.0);
  });

  it("kad su oslobođeni svi, rezultat je isti kao kod golog prekidača", () => {
    const r = izracunaj(20_000, ulaz({ brojOsoba: 3, brojOslobodjenih: 3 }));

    expect(r.ukupno).toBe(0);
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
  it("potpuno oslobođenje, 60 % uspjeha → 320,00 × 0,6 = 192,00 €", () => {
    const r = izracunaj(20_000, ulaz({ oslobodjen: true, postotakUspjeha: 60 }));

    expect(r.naTeretProtustranke).toBe(192.0);
  });

  /** Obveza tereti drugu stranu i nastaje po pravomoćnosti, pa ne ulazi u zbroj. */
  it("ne ulazi u ukupan iznos pristojbi", () => {
    const r = izracunaj(20_000, ulaz({ oslobodjen: true, postotakUspjeha: 60 }));

    expect(r.ukupno).toBe(0);
    expect(r.naTeretProtustranke).toBeGreaterThan(0);
  });

  it("djelomično oslobođenje veže se samo uz dio oslobođenih", () => {
    const r = izracunaj(20_000, ulaz({ brojOsoba: 4, brojOslobodjenih: 2, postotakUspjeha: 60 }));

    expect(r.naTeretProtustranke).toBe(132.0);
  });

  it("bez ijednog oslobođenja nema što premjestiti", () => {
    const r = izracunaj(20_000, ulaz({ postotakUspjeha: 100 }));

    expect(r.naTeretProtustranke).toBe(0);
    expect(r.ukupno).toBe(PRISTOJBA_NA_20000);
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
    const r = izracunaj(20_000);

    expect(r.ukupno).toBe(PRISTOJBA_NA_20000);
    expect(r.vpsUdio).toBe(20_000);
    expect(r.naTeretProtustranke).toBe(0);
  });
});
