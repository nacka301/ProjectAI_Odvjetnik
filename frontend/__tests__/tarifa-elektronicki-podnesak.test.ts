import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  KATEGORIJA_PARNICNI,
  PRISTOJBE_NN50_2026,
  VRSTA_RADNJE,
  cl7Rezim,
  obracunajPristojbe,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Čl. 7 Zakona o sudskim pristojbama (NN 118/18, 51/23) — umanjenje na polovicu.
 * Zrcali `ElektronickiPodnesakTests` na backendu.
 *
 * Tri stavka, tri uvjeta:
 *   st. 1 — stranka podnijela elektronički i platila pri podnošenju
 *   st. 2 — sud dostavio odluku elektronički i plaćeno u roku od 3 dana (po retku)
 *   st. 3 — zk upis predložen elektronički; pokriva i podneske i odluke
 *   st. 4 — propušten rok vraća puni iznos
 */

function stavka(id: string): TarifnaStavka {
  return {
    id,
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
  };
}

const obracun = (
  itemId: string,
  dodatno: Partial<Parameters<typeof obracunajPristojbe>[0]> = {},
) =>
  obracunajPristojbe({
    vrijednostSpora: 50_000,
    odabraneStavke: [stavka(itemId)],
    oslobodjen: false,
    ...dodatno,
  });

describe("Čl. 7 st. 1 — podnesci", () => {
  it("tužba uz VPS 50.000: 620,00 → 310,00", () => {
    const r = obracun("par_tuzba", { podnesciElektronicki: true });

    expect(r.ukupno).toBe(310.0);
    expect(r.stavke[0].umanjeno_elektronicki).toBe(true);
  });

  it("bez oznake ostaje puni iznos — to je i st. 4 kad rok nije ispunjen", () => {
    const r = obracun("par_tuzba");

    expect(r.ukupno).toBe(620.0);
    expect(r.stavke[0].umanjeno_elektronicki).toBe(false);
  });

  /** Polovica ide na PROPISANI iznos, dakle nakon uvećanja iz Tar. br. 3 st. 1. */
  it("žalba protiv presude: 1.240,00 → 620,00", () => {
    expect(obracun("par_zalba_presuda", { podnesciElektronicki: true }).ukupno).toBe(620.0);
  });

  it("upravni spor je fiksnih 110,00, ali je podnesak — 55,00", () => {
    expect(obracun("usp_tuzba_proc", { podnesciElektronicki: true }).ukupno).toBe(55.0);
  });
});

describe("Čl. 7 st. 2 — odluke", () => {
  it("prekidač za podneske ne dira odluku", () => {
    const r = obracun("par_rociste_presuda", { podnesciElektronicki: true });

    expect(r.ukupno).toBe(620.0);
    expect(r.stavke[0].umanjeno_elektronicki).toBe(false);
  });

  it("uz oznaku po retku odluka se prepolovljuje", () => {
    const r = obracun("par_rociste_presuda", { odlukeElektronicki: ["tbr2_st1"] });

    expect(r.ukupno).toBe(310.0);
    expect(r.stavke[0].umanjeno_elektronicki).toBe(true);
  });

  /** Zašto po retku: rok se može ispuniti za jednu odluku, a za drugu ne. */
  it("jedna odluka umanjena, podnesak uz nju nije", () => {
    const r = obracunajPristojbe({
      vrijednostSpora: 50_000,
      odabraneStavke: [stavka("par_tuzba"), stavka("par_rociste_presuda")],
      oslobodjen: false,
      odlukeElektronicki: ["tbr2_st1"],
    });

    const redak = (id: string) => r.stavke.find((s) => s.odredba_id === id)!;

    expect(redak("tbr1_st1").iznos).toBe(620.0);
    expect(redak("tbr2_st1").iznos).toBe(310.0);
    expect(r.ukupno).toBe(930.0);
  });
});

describe("Čl. 7 st. 3 — zemljišnoknjižni", () => {
  it("prekidač za podneske ne dira zk", () => {
    expect(obracun("zk_redoviti", { podnesciElektronicki: true }).ukupno).toBe(10.0);
  });

  it("zk upis elektronički: 10,00 → 5,00", () => {
    expect(obracun("zk_redoviti", { zkUpisElektronicki: true }).ukupno).toBe(5.0);
  });

  it("pokriva i odluke u zk postupku — rješenje o uknjižbi 30,00 → 15,00", () => {
    const r = obracunajPristojbe({
      vrijednostSpora: 0,
      odabraneStavke: [],
      oslobodjen: false,
      dodane: [{ odredba_id: "tbr15_uknjizba" }],
      zkUpisElektronicki: true,
    });

    expect(r.ukupno).toBe(15.0);
  });
});

describe("Čl. 7 — doseg", () => {
  /** Ovjera preslike stoji u zk poglavlju, ali nije ni podnesak ni odluka. */
  it("ovjeru preslike ne dira nijedna oznaka", () => {
    const r = obracunajPristojbe({
      vrijednostSpora: 0,
      odabraneStavke: [],
      oslobodjen: false,
      dodane: [{ odredba_id: "tbr17_18" }],
      podnesciElektronicki: true,
      zkUpisElektronicki: true,
    });

    expect(r.ukupno).toBe(5.0);
  });

  /** Doplata postoji upravo zato što podnesak NIJE elektronički. */
  it("doplatu za fizički oblik ne umanjuje", () => {
    const r = obracunajPristojbe({
      vrijednostSpora: 0,
      odabraneStavke: [],
      oslobodjen: false,
      dodane: [{ odredba_id: "tbr26_fizicki" }],
      podnesciElektronicki: true,
    });

    expect(r.ukupno).toBe(30.0);
  });

  it("umanjeni redak nosi pravni izvor umanjenja", () => {
    const izvor = obracun("par_tuzba", { podnesciElektronicki: true }).stavke[0].pravni_izvor;

    expect(izvor).toContain("čl. 7");
    expect(izvor).toContain("st. 1");
  });
});

describe("Čl. 7 — paritet razvrstavanja s backendom", () => {
  /**
   * Dva ručno održavana popisa razišla bi se u tišini, pa bi ista odredba u sučelju
   * bila podnesak, a na backendu odluka — i klijent bi vidio jedan iznos, a na
   * troškovniku drugi.
   */
  it("VRSTA_RADNJE se poklapa s blokom `Vrste` u PristojbeTarifaNn50_2026", () => {
    const cs = readFileSync(
      join(__dirname, "../../Backend/Services/Pristojbe/PristojbeTarifaNn50_2026.cs"),
      "utf8",
    );

    const blok = cs.slice(
      cs.indexOf("IReadOnlyDictionary<string, VrstaRadnje> Vrste"),
      cs.indexOf("private static readonly PristojbenaOdredba[] PoRedu"),
    );

    const backend = new Map<string, string>();
    for (const [, id, vrsta] of blok.matchAll(
      /\["?(\w+)"?\]\s*=\s*VrstaRadnje\.(Podnesak|Odluka|Ostalo)/g,
    )) {
      backend.set(id, vrsta.toLowerCase());
    }

    // Dva ključa u C#-u stoje kao konstante (Tbr1St1, Tbr1St2, Tbr29Tuzba), pa ih
    // regex hvata pod imenom konstante — preslikaj ih na stvarne ključeve.
    const konstante: Record<string, string> = {
      Tbr1St1: "tbr1_st1",
      Tbr1St2: "tbr1_st2",
      Tbr29Tuzba: "tbr29_tuzba",
    };
    for (const [ime, id] of Object.entries(konstante)) {
      if (backend.has(ime)) {
        backend.set(id, backend.get(ime)!);
        backend.delete(ime);
      }
    }

    expect(backend.size).toBeGreaterThan(80);

    const razlike: string[] = [];
    for (const [id, vrsta] of backend) {
      if ((VRSTA_RADNJE[id] ?? "ostalo") !== vrsta) {
        razlike.push(`${id}: backend ${vrsta}, frontend ${VRSTA_RADNJE[id] ?? "ostalo"}`);
      }
    }

    expect(razlike).toEqual([]);
  });

  it("svaka odredba iz tablice ima razvrstavanje", () => {
    const stvarnoOstalo = new Set([
      "tbr2_st9", "tbr8_tijelo", "tbr8_tumac", "tbr13_sastavljanje", "tbr13_cuvanje",
      "tbr17_18", "tbr24", "tbr26_fizicki", "tbr28_preslika", "tbr28_potvrda",
      "tbr31_uvjerenje", "tbr31_egradani", "tbr32", "tbr33", "tbr35_pohrana",
    ]);

    const nerazvrstane = PRISTOJBE_NN50_2026
      .filter((o) => (VRSTA_RADNJE[o.id] ?? "ostalo") === "ostalo" && !stvarnoOstalo.has(o.id))
      .map((o) => o.id);

    expect(nerazvrstane).toEqual([]);
  });

  it("zemljišnoknjižni podnesci i odluke idu po st. 3", () => {
    const zk = PRISTOJBE_NN50_2026.filter(
      (o) => o.poglavlje === "IV. Zemljišnoknjižni postupak" && VRSTA_RADNJE[o.id] !== "ostalo",
    );

    expect(zk.length).toBeGreaterThan(0);
    for (const o of zk) {
      expect(cl7Rezim(o.id, o.poglavlje)).toBe("st_3");
    }
  });
});
