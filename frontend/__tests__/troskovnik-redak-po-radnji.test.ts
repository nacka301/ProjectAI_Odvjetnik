import { describe, expect, it } from "vitest";
import {
  grupirajJednokratne,
  ukupnoBodovaZaStavku,
  type RedakZaGrupiranje,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Karakterizacija pred uvođenje datuma uz radnju — parnjak
 * Backend.Tests/TroskovnikRedakPoRadnjiKarakterizacijaTests.cs.
 *
 * Datum uz radnju traži da ista tarifna stavka može postojati više puta (tri ročišta
 * na tri datuma = tri retka). Sučelje danas drži mapu item_id → količina, pa se to ne
 * može dogoditi; kad se mapa zamijeni popisom redaka, može.
 *
 * Ovdje se bilježi kako `ukupnoBodovaZaStavku` reagira kad ga se pozove jednom po
 * retku, jer upravo to `liveCalc` radi.
 */

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
    jedinica: "radnja",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...over,
  };
}

/** Tbr. 7. toč. 2 — jednokratno 250 bodova za do četiri radnje, dalje 50 po radnji. */
const tbr72 = stavka({
  id: "par_tuzba_smetanje",
  fiksni_body: 250,
  jednokratna_nagrada: true,
  ukljucene_radnje: 4,
  daljnja_radnja_bodovi: 50,
});

/** Obična stavka po Tbr. 7. toč. 1 — bodovi su umnožak količine. */
const tbr71 = stavka({ id: "par_rociste_glavno", tbr71_postotak: 1.0 });

/** Zbroj kakav `liveCalc` danas dobiva: jedan poziv po retku. */
function zbrojPoRetcima(def: TarifnaStavka, bodovi: number, kolicine: number[]): number {
  return kolicine.reduce((zbroj, kol) => zbroj + ukupnoBodovaZaStavku(def, bodovi, kol), 0);
}

describe("obična stavka — redak po radnji ne mijenja iznos", () => {
  it("tri retka po jedan daju isto što i jedan redak s tri", () => {
    expect(zbrojPoRetcima(tbr71, 500, [1, 1, 1])).toBe(
      ukupnoBodovaZaStavku(tbr71, 500, 3),
    );
    expect(zbrojPoRetcima(tbr71, 500, [1, 1, 1])).toBe(1500);
  });

  it("razlomljene količine se zbrajaju kao i jedna velika", () => {
    expect(zbrojPoRetcima(tbr71, 500, [2, 3])).toBe(ukupnoBodovaZaStavku(tbr71, 500, 5));
  });
});

describe("jednokratna nagrada — bez grupiranja bi se umnožila", () => {
  /**
   * Pozvan jednom po retku, `ukupnoBodovaZaStavku` vraća nagradu svaki put, pa četiri
   * retka daju 1.000 bodova umjesto propisanih 250 (Tbr. 7. toč. 2 — jedna nagrada za
   * cijeli prvostupanjski postupak). Zato retke prije zbrajanja mora proći
   * `grupirajJednokratne`; ovaj test drži razlog za njegovo postojanje.
   */
  it("naivan zbroj po retku umnožava nagradu", () => {
    expect(ukupnoBodovaZaStavku(tbr72, 250, 4)).toBe(250);
    expect(zbrojPoRetcima(tbr72, 250, [1, 1, 1, 1])).toBe(1000);
  });

  it("jedan redak s količinom i dalje računa po Tarifi", () => {
    expect(ukupnoBodovaZaStavku(tbr72, 250, 6)).toBe(350); // 250 + 2 × 50
  });
});

// ── grupirajJednokratne — parnjak TroskovnikService.GrupirajJednokratne ──

const jednokratne = new Set(["par_tuzba_smetanje", "par_tuzba_obiteljski", "par_mali_spor"]);
const jeJednokratna = (itemId: string) => jednokratne.has(itemId);

function retci(...parovi: [string, number][]): RedakZaGrupiranje[] {
  return parovi.map(([itemId, kolicina]) => ({ itemId, kolicina }));
}

/** Zbroj kakav `liveCalc` dobiva kad retke prvo provuče kroz grupiranje. */
function zbrojSGrupiranjem(def: TarifnaStavka, bodovi: number, kolicine: number[]): number {
  const ulaz = retci(...kolicine.map((kol) => [def.id, kol] as [string, number]));

  return grupirajJednokratne(ulaz, jeJednokratna).reduce(
    (zbroj, udio) => zbroj + (udio.nosiNagradu ? ukupnoBodovaZaStavku(def, bodovi, udio.kolicina) : 0),
    0,
  );
}

describe("grupirajJednokratne", () => {
  it("četiri retka daju jednu nagradu, kao jedan redak s količinom četiri", () => {
    expect(zbrojSGrupiranjem(tbr72, 250, [1, 1, 1, 1])).toBe(250);
    expect(zbrojSGrupiranjem(tbr72, 250, [1, 1, 1, 1])).toBe(ukupnoBodovaZaStavku(tbr72, 250, 4));
  });

  it("radnje preko uključene četiri broje se kroz cijelu grupu", () => {
    expect(zbrojSGrupiranjem(tbr72, 250, [1, 1, 1, 1, 1, 1])).toBe(350); // 250 + 2 × 50
  });

  it("nagradu nosi prvi redak grupe, ostali su njome obuhvaćeni", () => {
    const udjeli = grupirajJednokratne(
      retci(["par_tuzba_smetanje", 1], ["par_tuzba_smetanje", 1], ["par_tuzba_smetanje", 1]),
      jeJednokratna,
    );

    expect(udjeli.map((u) => u.nosiNagradu)).toEqual([true, false, false]);
    expect(udjeli[0].kolicina).toBe(3);
  });

  it("retci grupe ne moraju biti susjedni", () => {
    const udjeli = grupirajJednokratne(
      retci(["par_tuzba_smetanje", 1], ["par_rociste_glavno", 2], ["par_tuzba_smetanje", 1]),
      jeJednokratna,
    );

    expect(udjeli.map((u) => u.nosiNagradu)).toEqual([true, true, false]);
    expect(udjeli[0].kolicina).toBe(2); // zbroj obaju redaka jednokratne stavke
  });

  it("dvije različite jednokratne stavke ostaju dvije nagrade", () => {
    const udjeli = grupirajJednokratne(
      retci(["par_tuzba_smetanje", 1], ["par_tuzba_obiteljski", 1], ["par_tuzba_smetanje", 1]),
      jeJednokratna,
    );

    expect(udjeli.map((u) => u.nosiNagradu)).toEqual([true, true, false]);
  });

  it("obične stavke prolaze nedirnute", () => {
    const udjeli = grupirajJednokratne(
      retci(["par_rociste_glavno", 1], ["par_rociste_glavno", 1]),
      jeJednokratna,
    );

    expect(udjeli).toEqual([
      { nosiNagradu: true, kolicina: 1 },
      { nosiNagradu: true, kolicina: 1 },
    ]);
  });

  it("prazan popis daje prazan rezultat", () => {
    expect(grupirajJednokratne([], jeJednokratna)).toEqual([]);
  });
});
