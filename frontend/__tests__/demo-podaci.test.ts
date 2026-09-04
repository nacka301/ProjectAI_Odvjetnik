/**
 * Podaci za primjer — ono što se ne smije potkrasti.
 *
 * Dvije opasnosti, obje tihe:
 *
 *   1. OIB s ispravnom kontrolnom znamenkom vjerojatno pripada stvarnoj osobi
 *      ili tvrtki. Takav broj u javnom repozitoriju je curenje, a nitko ga ne
 *      bi primijetio gledajući ekran — izgleda jednako kao izmišljen.
 *   2. Lanac stranka → predmet → troškovnik je jedina stvar koju popis od deset
 *      imena uopće pokazuje. Pukne li veza, ekran i dalje izgleda ispunjeno.
 */
import { describe, expect, it } from "vitest";
import {
  DEMO_PREDMETI,
  DEMO_STRANKE,
  DEMO_TROSKOVNIK,
  DEMO_URED,
} from "@/lib/demo/podaci";
import { validateOib } from "@/lib/utils";

const SVI_OIB = [
  ...DEMO_STRANKE.map((s) => s.oib),
  DEMO_URED.oib,
].filter((o): o is string => Boolean(o));

describe("OIB-ovi u primjerima", () => {
  it("imaju ispravan oblik — jedanaest znamenki", () => {
    for (const oib of SVI_OIB) expect(oib).toMatch(/^\d{11}$/);
  });

  it("SVI padaju na kontrolnoj znamenki", () => {
    // Ovo je cijela poanta: valjan OIB ovdje bi bio tuđi OIB.
    const valjani = SVI_OIB.filter(validateOib);
    expect(valjani).toEqual([]);
  });
});

describe("stranke", () => {
  it("ima ih deset", () => {
    expect(DEMO_STRANKE).toHaveLength(10);
  });

  it("mješavina je kakva postoji u praksi, ne deset istih", () => {
    const tipovi = new Set(DEMO_STRANKE.map((s) => s.tip));
    expect(tipovi.size).toBeGreaterThanOrEqual(3);
  });

  it("svaka ima adresu i kontakt", () => {
    for (const s of DEMO_STRANKE) {
      expect(s.adresa?.trim()).toBeTruthy();
      expect(s.email?.trim() || s.telefon?.trim()).toBeTruthy();
    }
  });

  it("id-evi su jedinstveni", () => {
    expect(new Set(DEMO_STRANKE.map((s) => s.id)).size).toBe(DEMO_STRANKE.length);
  });
});

describe("lanac stranka → predmet → troškovnik", () => {
  it("tri stranke imaju predmet", () => {
    const sPredmetom = new Set(DEMO_PREDMETI.map((p) => p.client_id));
    expect(sPredmetom.size).toBe(3);
  });

  it("svaki predmet visi o postojećoj stranki", () => {
    const idevi = new Set(DEMO_STRANKE.map((s) => s.id));
    for (const p of DEMO_PREDMETI) expect(idevi.has(p.client_id ?? "")).toBe(true);
  });

  it("troškovnik pripada stranki koja ima predmet", () => {
    const stranka = DEMO_STRANKE.find((s) => s.id === DEMO_TROSKOVNIK.klijentId);
    expect(stranka).toBeDefined();
    expect(DEMO_PREDMETI.some((p) => p.client_id === DEMO_TROSKOVNIK.klijentId)).toBe(true);
  });
});

describe("troškovnik za primjer", () => {
  it("nosi samo ulaze — nijedan izračunat iznos", () => {
    // Bodovi, nagrada, PDV i pristojbe moraju doći iz motora za Tarifu. Polje s
    // gotovim iznosom ovdje bi sakrilo kvar u izračunu umjesto da ga otkrije.
    const kljucevi = Object.keys(DEMO_TROSKOVNIK);
    for (const zabranjen of ["bodovi", "ukupno", "pdv", "nagrada", "iznos"]) {
      expect(kljucevi.some((k) => k.toLowerCase().includes(zabranjen))).toBe(false);
    }
  });

  it("ima pet radnji, s dva ročišta za glavnu raspravu različitih datuma", () => {
    expect(DEMO_TROSKOVNIK.radnje).toHaveLength(5);
    const glavna = DEMO_TROSKOVNIK.radnje.filter((r) => r.itemId === "par_rociste_glavno");
    expect(glavna).toHaveLength(2);
    expect(glavna[0].datum).not.toBe(glavna[1].datum);
  });

  it("svaka radnja nosi datum", () => {
    for (const r of DEMO_TROSKOVNIK.radnje) expect(r.datum).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it("pristojba na presudu dodana je imenovanjem odredbe, ne iznosom", () => {
    expect(DEMO_TROSKOVNIK.pristojbeDodane).toEqual([{ odredba_id: "tbr2_st1" }]);
  });

  it("podnesci su elektronički — čl. 7 prepolavlja pristojbu na tužbu", () => {
    expect(DEMO_TROSKOVNIK.podnesciElektronicki).toBe(true);
  });
});
