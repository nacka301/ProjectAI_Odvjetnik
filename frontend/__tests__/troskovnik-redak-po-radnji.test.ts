import { describe, expect, it } from "vitest";
import { ukupnoBodovaZaStavku, type TarifnaStavka } from "@/lib/tarifa";

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

describe("jednokratna nagrada — zatečeno ponašanje, mijenja se u sljedećem koraku", () => {
  /**
   * ZATEČENO: nagrada se vraća po svakom retku, pa četiri retka daju 1.000 bodova
   * umjesto propisanih 250 (Tbr. 7. toč. 2 — jedna nagrada za cijeli prvostupanjski
   * postupak). Dok stavka može postojati samo jednom, ovo je nedostižno.
   *
   * Ovaj se test u koraku „grupiranje jednokratnih nagrada" obrće u tvrdnju da
   * četiri retka daju 250.
   */
  it("četiri retka zasad umnožavaju nagradu", () => {
    expect(ukupnoBodovaZaStavku(tbr72, 250, 4)).toBe(250);
    expect(zbrojPoRetcima(tbr72, 250, [1, 1, 1, 1])).toBe(1000);
  });

  it("jedan redak s količinom i dalje računa po Tarifi", () => {
    expect(ukupnoBodovaZaStavku(tbr72, 250, 6)).toBe(350); // 250 + 2 × 50
  });
});
