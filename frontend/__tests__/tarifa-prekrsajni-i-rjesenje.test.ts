import { describe, expect, it } from "vitest";
import {
  izracunajOsnovicu,
  kazneniRazredBodovi,
  provjeriZahtjev,
  OSNOVICA_TBR7,
  type OsnovicaRezultat,
  type TarifnaStavka,
} from "@/lib/tarifa";
import {
  izracunajTroskovnik,
  type RedakIzracunaUlaz,
  type TroskovnikIzracunUlaz,
} from "@/lib/troskovnik-izracun";

/**
 * Prekršajni postupak ne traži zapriječenu kaznu + stvarno rješenje suda o trošku branitelja
 * po službenoj dužnosti (Općinski sud u Novom Zagrebu, 2026.). Zrcali backend testove
 * `TarifaPrekrsajniPostupakTests` i `RjesenjeTrosakBranitelja2026Tests`.
 *
 * Tarifa (NN 138/23) Tbr. 4 t. 1 al. 7: obrana u prekršajnom postupku = FIKSNIH 100 bodova.
 */

function stavka(p: Partial<TarifnaStavka> & Pick<TarifnaStavka, "id" | "kategorija" | "tarifni_broj">): TarifnaStavka {
  return {
    opis: p.id,
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
    ...p,
  };
}

const KAZNENI = "Kazneni postupak";
const KATALOG: TarifnaStavka[] = [
  stavka({ id: "kaz_obrana", kategorija: KAZNENI, tarifni_broj: "Tbr. 4.1", kazneni_base: true, jedinica: "sat" }),
  stavka({ id: "kaz_dodatni_sat", kategorija: KAZNENI, tarifni_broj: "Tbr. 4.1", fiksni_body: 50, jedinica: "sat" }),
  stavka({ id: "kaz_sudjelovanje_istraga", kategorija: KAZNENI, tarifni_broj: "Tbr. 3.1", kazneni_base: true, jedinica: "sat" }),
  stavka({ id: "kaz_zalba_istrazni_zatvor", kategorija: KAZNENI, tarifni_broj: "Tbr. 5.3", kazneni_base: true }),
  stavka({ id: "kaz_odgovor_optuznica", kategorija: KAZNENI, tarifni_broj: "Tbr. 5.6", kazneni_base: true }),
  stavka({ id: "kaz_razgovor_pritvor", kategorija: KAZNENI, tarifni_broj: "Tbr. 4.5", fiksni_body: 50 }),
  stavka({ id: "kaz_privatna_tuzba", kategorija: KAZNENI, tarifni_broj: "Tbr. 1.1", kazneni_base: true }),
  stavka({ id: "raz_razmatranje_spisa", kategorija: "Razno", tarifni_broj: "Tbr. 36", fiksni_body: 25, jedinica: "sat" }),
];

const OSNOVICA = izracunajOsnovicu({
  osnovicaTbr7: OSNOVICA_TBR7.Toc1,
  vrijednostSpora: 0,
}) as OsnovicaRezultat | null;

function redak(itemId: string, i: number, kolicina = 1): RedakIzracunaUlaz {
  return { uid: `${itemId}-${i}`, itemId, kolicina, datum: null, tbr41: null };
}

function izracun(
  radnje: [string, number?][],
  ulaz: Partial<TroskovnikIzracunUlaz> & {
    prekrsajni?: boolean;
    pravnaOsoba?: boolean;
    zaprijecenaKazna?: string | null;
    oduzimanje?: number;
  } = {},
) {
  const { prekrsajni, pravnaOsoba, zaprijecenaKazna = null, oduzimanje = 0, ...ostalo } = ulaz;
  return izracunajTroskovnik({
    redciPoredani: radnje.map(([id, kol], i) => redak(id, i, kol ?? 1)),
    katalog: KATALOG,
    osnovica: OSNOVICA,
    isPdvUkljucen: true,
    kazneniTierBodovi: kazneniRazredBodovi({
      zaprijecenaKazna,
      isPrekrsajniPostupak: prekrsajni,
      isPrekrsajPravnaOsoba: pravnaOsoba,
      isOduzimanjePrekrsaj: oduzimanje > 0,
      vrijednostOduzimanja: oduzimanje,
    }),
    isZastupnikDjeteta: false,
    isPoSluzbenojDuznosti: false,
    isNaTeretProracuna: false,
    isIzabraniOslobodjajuca: false,
    ulogaZastupanja: "BRANITELJ",
    zaprijecenaKazna,
    isOduzimanjePrekrsaj: oduzimanje > 0,
    parsedVrijednostOduzimanja: oduzimanje,
    isPrekrsajniPostupak: prekrsajni,
    brojZastupanihOsoba: 1,
    tbr41Povisenje: 0,
    tbr41Snizenje: 0,
    pristojbe: { ukupno: 0, vpsUdio: 0, naTeretProtustranke: 0 },
    dodatnePristojbe: [],
    predujmovi: [],
    naknadaTroskovaStavke: [],
    datumDospijeca: null,
    ...ostalo,
  });
}

describe("prekršajni postupak — bez zapriječene kazne", () => {
  it("obrana 1 sat = 100 b = 200,00 € + PDV = 250,00 €", () => {
    const r = izracun([["kaz_obrana"]], { prekrsajni: true });
    expect(r.ukupnoBodova).toBe(100);
    expect(r.ukupnoEur).toBe(200);
    expect(r.pdv).toBe(50);
    expect(r.ukupnoSPdv).toBe(250);
  });

  it("obrana 2 h 10 min = 100 + 50 + 50 = 200 b", () => {
    const r = izracun([["kaz_obrana"], ["kaz_dodatni_sat", 2]], { prekrsajni: true });
    expect(r.ukupnoBodova).toBe(200);
  });

  it.each(["kaz_privatna_tuzba", "kaz_zalba_istrazni_zatvor", "kaz_odgovor_optuznica", "kaz_sudjelovanje_istraga"])(
    "%s je u prekršajnom 100 bodova",
    (id) => {
      expect(izracun([[id]], { prekrsajni: true }).ukupnoBodova).toBe(100);
    },
  );

  it("provjeriZahtjev NE traži kaznu u prekršajnom, a u kaznenom traži", () => {
    const stavke = [KATALOG[0]];
    const bez = { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, ulogaZastupanja: "BRANITELJ" as const };

    expect(provjeriZahtjev(stavke, { ...bez, isPrekrsajniPostupak: true })).toEqual([]);
    expect(provjeriZahtjev(stavke, bez)).toHaveLength(1);
  });

  it("iznimka oduzimanje: Tbr. 7 t. 1 prema vrijednosti, traži vrijednost", () => {
    expect(kazneniRazredBodovi({ isPrekrsajniPostupak: true, isOduzimanjePrekrsaj: true, vrijednostOduzimanja: 20000 })).toBe(250);
    expect(kazneniRazredBodovi({ isPrekrsajniPostupak: true, isOduzimanjePrekrsaj: true, vrijednostOduzimanja: 0 })).toBe(0);
    expect(
      provjeriZahtjev([KATALOG[0]], {
        isPrekrsajniPostupak: true,
        isOduzimanjePrekrsaj: true,
        vrijednostOduzimanja: 0,
      }),
    ).toHaveLength(1);
  });

  it("iznimka pravna osoba: prema kazni za odgovornu osobu, traži kaznu", () => {
    expect(kazneniRazredBodovi({ isPrekrsajniPostupak: true, isPrekrsajPravnaOsoba: true, zaprijecenaKazna: "do_5" })).toBe(200);
    expect(
      provjeriZahtjev([KATALOG[0]], {
        isPrekrsajniPostupak: true,
        isPrekrsajPravnaOsoba: true,
        isOduzimanjePrekrsaj: false,
        vrijednostOduzimanja: 0,
      }),
    ).toHaveLength(1);
  });

  it("obje iznimke zajedno: alat ne nagađa", () => {
    const ulaz = {
      isPrekrsajniPostupak: true,
      isPrekrsajPravnaOsoba: true,
      isOduzimanjePrekrsaj: true,
      vrijednostOduzimanja: 20000,
      zaprijecenaKazna: "do_5",
    };
    expect(kazneniRazredBodovi(ulaz)).toBe(0);
    expect(provjeriZahtjev([KATALOG[0]], ulaz)).toHaveLength(1);
  });
});

describe("rješenje suda — branitelj po službenoj dužnosti, KZ čl. 326 st. 1 (do 8 god. → 300 b)", () => {
  // [itemId, kolicina, po Tarifi, priznato −50 %]
  const RADNJE: [string, number, number, number][] = [
    ["raz_razmatranje_spisa", 1, 25, 12.5],
    ["kaz_sudjelovanje_istraga", 1, 300, 150],
    ["kaz_sudjelovanje_istraga", 1, 300, 150],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_dodatni_sat", 1, 50, 25],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_zalba_istrazni_zatvor", 1, 300, 150],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_zalba_istrazni_zatvor", 1, 300, 150],
    ["kaz_odgovor_optuznica", 1, 300, 150],
    ["kaz_obrana", 1, 300, 150],
    ["kaz_razgovor_pritvor", 1, 50, 25],
  ];

  const r = izracun(
    RADNJE.map(([id, kol]) => [id, kol] as [string, number]),
    { zaprijecenaKazna: "do_10", isPoSluzbenojDuznosti: true },
  );

  it("tablica iz rješenja: 3.425 b po Tarifi, 1.712,5 b priznato", () => {
    expect(RADNJE.reduce((s, x) => s + x[2], 0)).toBe(3425);
    expect(RADNJE.reduce((s, x) => s + x[3], 0)).toBe(1712.5);
  });

  it("svaka radnja daje priznate bodove (i fiksne: Tbr. 36, Tbr. 4 t. 5)", () => {
    expect(r.lines).toHaveLength(RADNJE.length);
    r.lines.forEach((l, i) => {
      expect(l.ukupno_bodova, `${RADNJE[i][0]} (red ${i + 1})`).toBe(RADNJE[i][3]);
    });
  });

  it("ukupno 4.281,25 € (3.425,00 € + PDV 856,25 €) — iznos koji je sud dosudio", () => {
    expect(r.ukupnoBodova).toBe(1712.5);
    expect(r.ukupnoEur).toBe(3425);
    expect(r.pdv).toBe(856.25);
    expect(r.ukupnoSPdv).toBe(4281.25);
  });

  it("zaostala zastavica ne umanjuje Tbr. 36 u troškovniku bez kaznenih stavki", () => {
    const sam = izracun([["raz_razmatranje_spisa"]], { isPoSluzbenojDuznosti: true });
    expect(sam.ukupnoBodova).toBe(25);
  });
});
