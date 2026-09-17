/**
 * Izvoz troškovnika u `.xlsx`.
 *
 * Težište je na onome što je u CSV-u bilo pokvareno i nevidljivo dok se datoteka
 * ne otvori u Excelu: da je iznos broj a ne tekst, datum serijski datum a ne
 * string, OIB tekst s vodećom nulom, i da odjeljak kojeg nema ne ostavlja za
 * sobom prazno zaglavlje.
 */
import { describe, expect, it } from "vitest";
import { buildIzvozModel, type IzvozUlaz } from "@/lib/troskovnik-izvoz-model";
import { buildTroskovnikSheet } from "@/lib/troskovnik-xlsx";

type Cell = { value?: unknown; type?: unknown; format?: string } | null;

const PRAZNA_POLJA: IzvozUlaz["polja"] = {
  odvjetnikIme: "",
  odvjetnikOib: "",
  odvjetnikAdresa: "",
  odvjetnikGrad: "",
  odvjetnikIban: "",
  klijentIme: "",
  zastupanaPolozaj: "",
  protustrankaIme: "",
  protustrankaPolozaj: "",
  predmetSud: "",
  predmetPoslovniBroj: "",
};

const PRAZAN_LIVECALC: IzvozUlaz["liveCalc"] = {
  lines: [],
  ukupnoBodova: 0,
  naknadaTroskovaUkupno: 0,
  osnovicaSaNaknadom: 0,
  pdv: 0,
  ukupnoSPdv: 0,
  ukupnoPristojbe: 0,
  ukupnoPredujmovi: 0,
  sveukupniTrosak: 0,
};

function ulaz(preinake: Partial<IzvozUlaz> = {}): IzvozUlaz {
  return {
    result: null,
    liveCalc: PRAZAN_LIVECALC,
    pristojbeneStavke: [],
    dodatnePristojbe: [],
    predujmovi: [],
    isPdvUkljucen: false,
    napomena: "",
    polja: PRAZNA_POLJA,
    ...preinake,
  };
}

/** Jedna odvjetnička radnja u obliku u kojem je daje živi pregled. */
function linija(preinake: Record<string, unknown> = {}) {
  return {
    opis: "Sastav tužbe",
    tarifni_broj: "Tbr. 7.1",
    datum: "2026-04-11",
    kolicina: 1,
    bodova_po_jedinici: 100,
    ukupno_bodova: 100,
    iznos_eur: 200,
    ...preinake,
  };
}

function sLinijom(preinake: Partial<IzvozUlaz> = {}): IzvozUlaz {
  return ulaz({
    liveCalc: { ...PRAZAN_LIVECALC, lines: [linija()], ukupnoBodova: 100, osnovicaSaNaknadom: 200, sveukupniTrosak: 200 },
    ...preinake,
  });
}

/** Sve ćelije lista kao ravan popis — za tvrdnje o prisutnosti odjeljka. */
function sveCelije(list: Cell[][]): NonNullable<Cell>[] {
  return list.flat().filter((c): c is NonNullable<Cell> => c !== null);
}

function tekstovi(list: Cell[][]): string[] {
  return sveCelije(list)
    .map((c) => c.value)
    .filter((v): v is string => typeof v === "string");
}

describe("buildIzvozModel", () => {
  it("izostavlja PDV kad odvjetnik nije u sustavu PDV-a", () => {
    expect(buildIzvozModel(sLinijom({ isPdvUkljucen: false })).pdv).toBeNull();
  });

  it("daje osnovicu, PDV i ukupno kad je odvjetnik u sustavu PDV-a", () => {
    const m = buildIzvozModel(
      sLinijom({
        isPdvUkljucen: true,
        liveCalc: { ...PRAZAN_LIVECALC, lines: [linija()], osnovicaSaNaknadom: 200, pdv: 50, ukupnoSPdv: 250 },
      }),
    );
    expect(m.pdv).toEqual({ osnovica: 200, iznos: 50, ukupno: 250 });
  });

  it("pribraja Tbr. 41 bodove retku, kao i prikaz na ekranu", () => {
    const m = buildIzvozModel(
      sLinijom({
        liveCalc: { ...PRAZAN_LIVECALC, lines: [linija({ ukupno_bodova: 100, tbr41_bodovi: 25 })] },
      }),
    );
    expect(m.radnje[0].ukupnoBodova).toBe(125);
  });

  it("pretvara datum radnje u Date, a prazan datum ostavlja kao null", () => {
    const m = buildIzvozModel(
      sLinijom({
        liveCalc: { ...PRAZAN_LIVECALC, lines: [linija(), linija({ datum: null })] },
      }),
    );
    expect(m.radnje[0].datum).toBeInstanceOf(Date);
    expect(m.radnje[1].datum).toBeNull();
  });

  it("radnje slaže kronološki, neovisno o redoslijedu unosa", () => {
    const m = buildIzvozModel(
      sLinijom({
        liveCalc: {
          ...PRAZAN_LIVECALC,
          lines: [
            linija({ opis: "Žalba", datum: "2025-11-14" }),
            linija({ opis: "Tužba", datum: "2025-02-12" }),
            linija({ opis: "Ročište", datum: "2025-06-03" }),
          ],
        },
      }),
    );
    expect(m.radnje.map((r) => r.opis)).toEqual(["Tužba", "Ročište", "Žalba"]);
  });

  it("radnja bez datuma ide na kraj, ostale zadrže redoslijed unosa", () => {
    const m = buildIzvozModel(
      sLinijom({
        liveCalc: {
          ...PRAZAN_LIVECALC,
          lines: [
            linija({ opis: "Bez datuma A", datum: null }),
            linija({ opis: "S datumom", datum: "2025-03-01" }),
            linija({ opis: "Bez datuma B", datum: null }),
          ],
        },
      }),
    );
    expect(m.radnje.map((r) => r.opis)).toEqual([
      "S datumom",
      "Bez datuma A",
      "Bez datuma B",
    ]);
  });

  it("uzima uloge stranaka iz lokalnih polja, prije spremanja", () => {
    const m = buildIzvozModel(
      sLinijom({
        polja: {
          ...PRAZNA_POLJA,
          klijentIme: "Ivan Horvat",
          zastupanaPolozaj: "Tužitelj",
          protustrankaIme: "Marko Marić",
          protustrankaPolozaj: "Tuženik",
        },
      }),
    );
    expect(m.zastupana).toEqual({ polozaj: "Tužitelj", ime: "Ivan Horvat" });
    expect(m.protustranka).toEqual({ polozaj: "Tuženik", ime: "Marko Marić" });
  });

  it("razrađuje pristojbe po tarifnom broju i biljezi staru uredbu", () => {
    const m = buildIzvozModel(
      sLinijom({
        pristojbeneStavke: [
          { tarifni_broj: "Tar. br. 1 st. 2", kolicina: 2, iznos: 110, uredba: "NN 37/2023" },
        ],
      }),
    );
    expect(m.pristojbe[0].opis).toBe("Sudska pristojba — Tar. br. 1 st. 2 × 2 (NN 37/2023, čl. 2)");
  });

  it("prazna napomena ne postaje odjeljak", () => {
    expect(buildIzvozModel(sLinijom({ napomena: "   " })).napomena).toBeUndefined();
  });
});

describe("buildTroskovnikSheet", () => {
  it("iznos je broj s novčanim formatom, ne tekst", () => {
    const list = buildTroskovnikSheet(buildIzvozModel(sLinijom())) as Cell[][];
    const iznos = sveCelije(list).find((c) => c.value === 200 && c.format?.includes("€"));
    expect(iznos).toBeDefined();
    expect(iznos!.type).toBe(Number);
    expect(typeof iznos!.value).toBe("number");
  });

  it("datum je Date, ne tekst, i ostaje na svom danu unatoč vremenskoj zoni", () => {
    const list = buildTroskovnikSheet(buildIzvozModel(sLinijom())) as Cell[][];
    const datumi = sveCelije(list)
      .filter((c) => c.type === Date)
      .map((c) => c.value as Date);

    // Zaglavlje nosi datum izrade, tablica datum radnje — oba su pravi Date.
    expect(datumi.length).toBeGreaterThanOrEqual(2);
    expect(datumi.every((d) => d instanceof Date)).toBe(true);

    // Podne po UTC-u: dovoljno daleko od obje granice dana da se prikazani
    // datum ne pomakne pri pretvorbi u serijski broj.
    expect(datumi.every((d) => d.getUTCHours() === 12)).toBe(true);

    const radnja = datumi.find((d) => d.getUTCFullYear() === 2026 && d.getUTCMonth() === 3);
    expect(radnja).toBeDefined();
    expect(radnja!.getUTCDate()).toBe(11);
  });

  it("OIB je tekstualna ćelija, pa vodeća nula preživi", () => {
    const list = buildTroskovnikSheet(
      buildIzvozModel(sLinijom({ polja: { ...PRAZNA_POLJA, odvjetnikIme: "A. Odvjetnik", odvjetnikOib: "01234567890" } })),
    ) as Cell[][];
    const oib = sveCelije(list).find((c) => c.value === "01234567890");
    expect(oib).toBeDefined();
    expect(oib!.type).toBe(String);
  });

  it("zadnji redak je puna bilješka: alat, propisi i tko odgovara", () => {
    const list = buildTroskovnikSheet(buildIzvozModel(sLinijom())) as Cell[][];
    const zadnjiTekst = sveCelije(list)
      .filter((c) => typeof c.value === "string")
      .at(-1)!.value as string;
    expect(zadnjiTekst).toMatch(/^Izračunato u alatu PravoTek /);
    expect(zadnjiTekst).toContain("Tarifa o nagradama i naknadi troškova za rad odvjetnika");
    expect(zadnjiTekst).toContain("Izračun provjerava i za njega odgovara odvjetnik.");
  });

  it("poslovni broj ostaje tekst", () => {
    const list = buildTroskovnikSheet(
      buildIzvozModel(sLinijom({ polja: { ...PRAZNA_POLJA, predmetPoslovniBroj: "P-007/24" } })),
    ) as Cell[][];
    const broj = sveCelije(list).find((c) => c.value === "P-007/24");
    expect(broj?.type).toBe(String);
  });

  it("bez PDV-a nema redaka Osnovica, PDV ni Nagrada s PDV-om", () => {
    const t = tekstovi(buildTroskovnikSheet(buildIzvozModel(sLinijom({ isPdvUkljucen: false }))) as Cell[][]);
    expect(t).not.toContain("Osnovica (bez PDV)");
    expect(t).not.toContain("PDV (25%)");
    expect(t).not.toContain("Nagrada s PDV-om");
  });

  it("bez pristojbi i predujmova ti odjeljci uopće ne postoje", () => {
    const t = tekstovi(buildTroskovnikSheet(buildIzvozModel(sLinijom())) as Cell[][]);
    expect(t).not.toContain("II. Sudske pristojbe");
    expect(t).not.toContain("III. Predujmovi");
    // Odvjetničke radnje i sveukupno ostaju — troškovnik nije prazan.
    expect(t).toContain("I. Odvjetničke radnje");
    expect(t).toContain("SVEUKUPNI TROŠAK");
  });

  it("s pristojbama i predujmovima odjeljci se pojave", () => {
    const t = tekstovi(
      buildTroskovnikSheet(
        buildIzvozModel(
          sLinijom({
            pristojbeneStavke: [{ tarifni_broj: "Tar. br. 1", kolicina: 1, iznos: 55 }],
            predujmovi: [{ datum: "2026-05-02", opis: "Predujam za vještačenje", iznos: 150 }],
            liveCalc: {
              ...PRAZAN_LIVECALC,
              lines: [linija()],
              ukupnoPristojbe: 55,
              ukupnoPredujmovi: 150,
              sveukupniTrosak: 405,
            },
          }),
        ),
      ) as Cell[][],
    );
    expect(t).toContain("II. Sudske pristojbe");
    expect(t).toContain("III. Predujmovi");
    expect(t).toContain("Predujam za vještačenje");
  });

  it("napomena se izvozi kad postoji, a bez nje nema odjeljka", () => {
    const sNapomenom = tekstovi(
      buildTroskovnikSheet(buildIzvozModel(sLinijom({ napomena: "Traži se naknada troška." }))) as Cell[][],
    );
    expect(sNapomenom).toContain("Napomena");
    expect(sNapomenom).toContain("Traži se naknada troška.");

    expect(tekstovi(buildTroskovnikSheet(buildIzvozModel(sLinijom())) as Cell[][])).not.toContain("Napomena");
  });

  it("svi retci imaju istu širinu, pa iznos uvijek stoji u istom stupcu", () => {
    const list = buildTroskovnikSheet(
      buildIzvozModel(
        sLinijom({
          isPdvUkljucen: true,
          pristojbeneStavke: [{ tarifni_broj: "Tar. br. 1", kolicina: 1, iznos: 55 }],
          predujmovi: [{ datum: null, opis: "Predujam", iznos: 150 }],
        }),
      ),
    ) as Cell[][];

    expect(new Set(list.map((r) => r.length))).toEqual(new Set([7]));

    // Svaki novčani iznos je u stupcu G — u CSV-u su padali čas u I, čas u C.
    for (const redak of list) {
      redak.forEach((c, i) => {
        if (c && typeof c.value === "number" && c.format?.includes("€")) expect(i).toBe(6);
      });
    }
  });
});
