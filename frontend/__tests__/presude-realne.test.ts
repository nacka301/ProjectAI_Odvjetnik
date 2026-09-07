import { describe, expect, it } from "vitest";
import {
  izracunajOsnovicu,
  obracunajPristojbe,
  tbr71Bodovi,
  zaokruziNovac,
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
 * 10 realnih troškovnika iz objavljenih presuda (`test-troskovnici-realne-presude.md`
 * u rootu repoa) — provjera izlučene agregacije `izracunajTroskovnik`.
 *
 * Što se provjerava (KORAK 3 iz zadatka):
 *  A) ARITMETIKA — nagrada = Σ bodova po retku × 2 €; PDV = 25 % SAMO na nagradu
 *     (+ naknada troškova) i samo kad je odvjetnik u sustavu PDV-a; predujmovi i
 *     pristojbe BEZ PDV-a; sveukupno štima.
 *  B) RAZRED VRIJEDNOSTI za sastav tužbe (Tbr. 7. toč. 1) — bodovi po VPS-u.
 *
 * Bodove uspoređujemo s presudama (Tarifa NN 138/23, 1 bod = 2,00 €, nepromijenjena).
 * Sudske pristojbe u presudama su po STAROJ uredbi — NE uspoređuju se ovdje.
 *
 * Sud ne dosuđuje uvijek pune tarifne bodove po radnji. Bodovi po retku su uređivi
 * kroz izbor kataloške stavke (Tbr. 7.1 100 % / Tbr. 9.2 50 % / Tbr. 8.4 25 % …) i
 * kroz Tbr. 41 (−25 % / −50 %) po retku — to ovdje i modeliramo.
 */

// ── Pomoćne ──────────────────────────────────────────────────────

/** Kataloška stavka za parnični postupak, s postotkom Tbr. 7.1 po retku. */
function stavka(id: string, postotak: number, maxBodova: number | null = null): TarifnaStavka {
  return {
    id,
    kategorija: "Parnični postupak",
    opis: id,
    tarifni_broj: "Tbr. 7.1",
    pravni_izvor: "Tbr. 7.1",
    fiksni_body: null,
    tbr71_postotak: postotak,
    tbr71_povecanje: null,
    max_bodova: maxBodova,
    min_bodova: null,
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
  };
}

/** Puna nagrada po Tbr. 7.1 (100 %). */
const PUNA = (id: string) => stavka(id, 1);
/** Procesno ročište — 50 % Tbr. 7.1 (Tbr. 9.2). */
const POLA = (id: string) => stavka(id, 0.5);
/** Ostali podnesak — 25 % Tbr. 7.1, najviše 25 bodova (Tbr. 8.4). */
const CETVRT = (id: string) => stavka(id, 0.25, 25);

const PRAZNE_PRISTOJBE = { ukupno: 0, vpsUdio: 0, naTeretProtustranke: 0 };

function redak(itemId: string, snizenje: 0 | 25 | 50 = 0): RedakIzracunaUlaz {
  return {
    uid: `${itemId}-${snizenje}`,
    itemId,
    kolicina: 1,
    datum: null,
    tbr41: snizenje ? { povisenje: 0, snizenje } : null,
  };
}

/**
 * Sagradi ulaz za `izracunajTroskovnik` iz VPS-a i popisa radnji. Svaka radnja =
 * { def, snizenje? }; katalog i retci izvode se automatski, osnovica iz VPS-a.
 */
function slucaj(opts: {
  vps: number;
  pdv: boolean;
  radnje: { def: TarifnaStavka; snizenje?: 0 | 25 | 50 }[];
  predujmovi?: number[];
  naknadaTroskova?: number[];
  pristojbe?: { ukupno: number; vpsUdio: number; naTeretProtustranke: number };
}) {
  const osnovica = izracunajOsnovicu({
    osnovicaTbr7: OSNOVICA_TBR7.Toc1,
    vrijednostSpora: opts.vps,
  }) as OsnovicaRezultat | null;

  const katalog = opts.radnje.map((r) => r.def);
  const redciPoredani = opts.radnje.map((r) => redak(r.def.id, r.snizenje ?? 0));

  const extra: Partial<TroskovnikIzracunUlaz> = {};

  return izracunajTroskovnik({
    redciPoredani,
    katalog,
    osnovica,
    isPdvUkljucen: opts.pdv,
    kazneniTierBodovi: 0,
    isZastupnikDjeteta: false,
    isPoSluzbenojDuznosti: false,
    isNaTeretProracuna: false,
    zaprijecenaKazna: null,
    isOduzimanjePrekrsaj: false,
    parsedVrijednostOduzimanja: 0,
    brojZastupanihOsoba: 1,
    tbr41Povisenje: 0,
    tbr41Snizenje: 0,
    pristojbe: opts.pristojbe ?? PRAZNE_PRISTOJBE,
    dodatnePristojbe: [],
    predujmovi: (opts.predujmovi ?? []).map((iznos) => ({ opis: "predujam", iznos })),
    naknadaTroskovaStavke: (opts.naknadaTroskova ?? []).map((iznos) => ({ iznos })),
    datumDospijeca: null,
    ...extra,
  });
}

/** Aritmetički invarijanti koji moraju vrijediti za SVAKI troškovnik (KORAK 3-A). */
function provjeriInvarijante(
  r: ReturnType<typeof slucaj>,
  opts: { pdv: boolean; predujmovi?: number[]; naknada?: number[] },
) {
  // nagrada = ukupno bodova × 2 €
  expect(r.ukupnoEur).toBe(zaokruziNovac(r.ukupnoBodova * 2));
  // PDV samo na nagradu + naknadu troškova, samo kad je odvjetnik u sustavu PDV-a
  const naknada = (opts.naknada ?? []).reduce((s, x) => s + x, 0);
  expect(r.naknadaTroskovaUkupno).toBe(zaokruziNovac(naknada));
  expect(r.osnovicaSaNaknadom).toBe(r.ukupnoEur + r.naknadaTroskovaUkupno);
  expect(r.pdv).toBe(opts.pdv ? zaokruziNovac(r.osnovicaSaNaknadom * 0.25) : 0);
  // predujmovi NISU u PDV osnovici
  const predujmovi = (opts.predujmovi ?? []).reduce((s, x) => s + x, 0);
  expect(r.ukupnoPredujmovi).toBe(predujmovi);
  // sveukupno = odvjetnički trošak (s PDV) + pristojbe + predujmovi
  expect(r.sveukupniTrosak).toBe(
    r.osnovicaSaNaknadom + r.pdv + r.ukupnoPristojbe + r.ukupnoPredujmovi,
  );
}

// ── B) RAZRED VRIJEDNOSTI (Tbr. 7. toč. 1) ───────────────────────

describe("B — razred vrijednosti za sastav tužbe", () => {
  it.each([
    ["presuda 3 · VPS 463,53 €", 463.53, 50],
    ["presuda 6 · VPS 1.327,22 € (tik iznad granice 1.327,00)", 1_327.22, 100],
    ["presuda 10 · VPS 7.500 €", 7_500, 100],
  ])("%s → %d bodova", (_opis, vps, bodovi) => {
    expect(tbr71Bodovi(vps)).toBe(bodovi);
  });

  it("granice razreda: 664,00 vs 664,01 i 1.327,00 vs 1.327,01", () => {
    expect(tbr71Bodovi(664.0)).toBe(50);
    expect(tbr71Bodovi(664.01)).toBe(75);
    expect(tbr71Bodovi(1_327.0)).toBe(75);
    expect(tbr71Bodovi(1_327.01)).toBe(100);
  });

  it("presuda 8 — velika vrijednost (kn) → logika rasta, ne aritmetika", () => {
    // 5.991.708 kn ≈ 795.247 € (÷ 7,53450). Ne uspoređujemo 1:1 sa starom tarifom u kn;
    // provjeravamo da progresivni razred raste i da je unutar kape od 10.000.
    const veliki = tbr71Bodovi(795_247);
    expect(veliki).toBeGreaterThan(5_000);
    expect(veliki).toBeLessThan(10_000);
    expect(tbr71Bodovi(795_247)).toBeGreaterThan(tbr71Bodovi(700_000));
  });
});

// ── A) ARITMETIKA — 10 presuda ───────────────────────────────────

describe("A — aritmetika nagrade i PDV-a", () => {
  it("1 · Naknada štete na vozilu — TS Zagreb P-2064/2022-41", () => {
    // tužba 100, podnesak 100, podnesak (Tbr. 8.4) 25, 4× ročište 100 → Σ 625 bod.
    const r = slucaj({
      vps: 13_000,
      pdv: true,
      radnje: [
        { def: PUNA("par_tuzba") },
        { def: PUNA("par_podnesak_a") },
        { def: CETVRT("par_podnesak_b") },
        { def: PUNA("par_rociste_1") },
        { def: PUNA("par_rociste_2") },
        { def: PUNA("par_rociste_3") },
        { def: PUNA("par_rociste_4") },
      ],
      predujmovi: [400],
    });

    expect(r.osnovicaBodova).toBe(625);
    expect(r.ukupnoEur).toBe(1_250); // 625 × 2 €
    expect(r.pdv).toBe(312.5);
    expect(r.osnovicaSaNaknadom).toBe(1_250); // predujam ne ulazi
    expect(r.sveukupniTrosak).toBe(1_250 + 312.5 + 400);
    provjeriInvarijante(r, { pdv: true, predujmovi: [400] });
  });

  it("2 · Parnica s vještačenjem — OS Koprivnica P-445/2023-24", () => {
    // isti tip radnje s RAZLIČITIM bodovima: 100 ×7, 50 ×1, 25 ×1 → Σ 775
    const r = slucaj({
      vps: 10_000, // razred 1.327,01–13.272 € → puna radnja = 100 bod
      pdv: true,
      radnje: [
        { def: PUNA("r0") },
        { def: PUNA("r1") },
        { def: PUNA("r2") },
        { def: PUNA("r3") },
        { def: POLA("r4") }, // procesno ročište
        { def: PUNA("r5") },
        { def: PUNA("r6") },
        { def: PUNA("r7") },
        { def: CETVRT("r8") }, // pristup ročištu objave presude
      ],
      predujmovi: [398.17],
    });

    expect(r.osnovicaBodova).toBe(775);
    expect(r.ukupnoEur).toBe(1_550);
    expect(r.pdv).toBe(387.5);
    provjeriInvarijante(r, { pdv: true, predujmovi: [398.17] });
  });

  it("3 · Parnica male vrijednosti — OS Split P-4036/2024-12 (VPS 463,53 €)", () => {
    // razred 332–664 € → svaka „puna" radnja nosi 50 bod. 10 radnji → Σ 500.
    expect(tbr71Bodovi(463.53)).toBe(50);
    const r = slucaj({
      vps: 463.53,
      pdv: true,
      radnje: Array.from({ length: 10 }, (_, i) => ({ def: PUNA(`r${i}`) })),
      predujmovi: [265.45],
    });

    expect(r.osnovicaBodova).toBe(500);
    expect(r.ukupnoEur).toBe(1_000); // 500 bod × 2 €
    expect(r.pdv).toBe(250);
    provjeriInvarijante(r, { pdv: true, predujmovi: [265.45] });
    // NALAZ: sažetak u izvornom dokumentu kaže „500 bod → nagrada 500 € + PDV 125 €",
    // što je proturječno (500 bodova = 1.000 €). Aritmetika (1 bod = 2 €) daje 1.000 €.
  });

  it("4 · Ovrha → parnica — TS Zadar Povrv 80/2023-12 (parnični dio)", () => {
    // 2 podneska ×100, pripremno ročište 25, glavna rasprava 50 → Σ 275
    const r = slucaj({
      vps: 10_000,
      pdv: true,
      radnje: [
        { def: PUNA("par_podnesak_1") },
        { def: PUNA("par_podnesak_2") },
        { def: CETVRT("par_rociste_pripremno") },
        { def: POLA("par_rasprava_glavna") },
      ],
      naknadaTroskova: [42.05], // javnobilježnička naknada — ULAZI u PDV osnovicu
    });

    expect(r.osnovicaBodova).toBe(275);
    expect(r.ukupnoEur).toBe(550);
    expect(r.osnovicaSaNaknadom).toBe(592.05); // 550 + 42,05
    expect(r.pdv).toBe(148.01); // 25 % × 592,05 = 148,0125 → 148,01
    provjeriInvarijante(r, { pdv: true, naknada: [42.05] });
  });

  it("5 · Ovršni postupak — OGS Zagreb Povrv-2030/2022-15", () => {
    // prijedlog 100, pripremna radnja 100, podnesak 25, ročište 100 → Σ 325
    const r = slucaj({
      vps: 8_000,
      pdv: true,
      radnje: [
        { def: PUNA("r0") },
        { def: PUNA("r1") },
        { def: CETVRT("r2") },
        { def: PUNA("r3") },
      ],
      predujmovi: [12.44, 200],
    });

    expect(r.osnovicaBodova).toBe(325);
    expect(r.ukupnoEur).toBe(650);
    expect(r.pdv).toBe(162.5);
    expect(r.sveukupniTrosak).toBe(650 + 162.5 + 212.44);
    provjeriInvarijante(r, { pdv: true, predujmovi: [12.44, 200] });
  });

  it("6 · Potrošački spor — OS Sisak P-324/2025-20 (granica razreda 1.327,22 €)", () => {
    expect(tbr71Bodovi(1_327.22)).toBe(100);
    const r = slucaj({
      vps: 1_327.22,
      pdv: true,
      radnje: [{ def: PUNA("par_tuzba") }],
    });
    expect(r.lines[0].bodova_po_jedinici).toBe(100);
    expect(r.ukupnoEur).toBe(200);
    expect(r.pdv).toBe(50);
    provjeriInvarijante(r, { pdv: true });
  });

  it("7 · STRESS TEST — TS Varaždin P 4/2024-18 (djelomični uspjeh)", () => {
    // Dugačak troškovnik: tužba + 8 ročišta + 3 sniženja po Tbr. 41, tri vještačenja.
    const r = slucaj({
      vps: 60_000,
      pdv: true,
      radnje: [
        { def: PUNA("par_tuzba") },
        ...Array.from({ length: 8 }, (_, i) => ({ def: PUNA(` r${i}`) })),
        { def: PUNA("p1"), snizenje: 25 as const },
        { def: PUNA("p2"), snizenje: 50 as const },
      ],
      predujmovi: [1_832.64, 515.5, 250],
    });

    // Aritmetika mora štimati bez obzira na sastav
    provjeriInvarijante(r, { pdv: true, predujmovi: [1_832.64, 515.5, 250] });
    // predujmovi (2.598,14 €) ne diraju PDV osnovicu
    expect(r.osnovicaSaNaknadom).toBe(r.ukupnoEur);

    // NALAZ: motor NEMA opću „podjelu troška po postotku uspjeha" (74,54 %).
    // To polje (`postotak_uspjeha`) postoji samo uz oslobođenje od pristojbi (čl. 13),
    // ne kao opći množitelj nagrade. Podjelu po uspjehu odvjetnik radi ručno.
  });

  it("9 · Radni spor, sniženo ročište (kn) — OS Osijek Pr-1107/2021-20", () => {
    // tužba 100, dva podneska 100+100, ročište SNIŽENO na 50 (procesna pitanja) → Σ 350
    const r = slucaj({
      vps: 5_000,
      pdv: true,
      radnje: [
        { def: PUNA("par_tuzba") },
        { def: PUNA("par_podnesak_1") },
        { def: PUNA("par_podnesak_2") },
        { def: POLA("par_rociste_snizeno") }, // Tbr. 9.2 — procesno
      ],
    });

    expect(r.osnovicaBodova).toBe(350);
    expect(r.ukupnoEur).toBe(700);
    expect(r.pdv).toBe(175);
    provjeriInvarijante(r, { pdv: true });
  });

  it("10 · Baseline — OGS Zagreb P-3260/2025-12 (VPS 7.500 €)", () => {
    expect(tbr71Bodovi(7_500)).toBe(100);
    const pristojbe = obracunajPristojbe({
      vrijednostSpora: 7_500,
      odabraneStavke: [stavka("par_tuzba", 1)],
      oslobodjen: false,
    });

    const r = slucaj({
      vps: 7_500,
      pdv: true,
      radnje: [{ def: PUNA("par_tuzba") }],
      pristojbe: { ukupno: pristojbe.ukupno, vpsUdio: pristojbe.vpsUdio, naTeretProtustranke: pristojbe.naTeretProtustranke },
    });

    expect(r.lines[0].ukupno_bodova).toBe(100);
    expect(r.ukupnoEur).toBe(200); // poklapa se s presudom
    expect(r.pdv).toBe(50);
    expect(r.ukupnoSPdv).toBe(250);
    expect(r.sudskaPristojba).toBe(195); // NN 50/2026, ne stara 60,73
    expect(r.sveukupniTrosak).toBe(250 + 195);
  });
});

// ── PDV: prekidač isključen ──────────────────────────────────────

describe("A — PDV samo kad je odvjetnik u sustavu PDV-a", () => {
  it("bez PDV-a: pdv = 0, sveukupno bez PDV-a", () => {
    const r = slucaj({
      vps: 7_500,
      pdv: false,
      radnje: [{ def: PUNA("par_tuzba") }, { def: POLA("par_rociste") }],
      predujmovi: [100],
    });
    expect(r.ukupnoEur).toBe(300); // (100 + 50) × 2
    expect(r.pdv).toBe(0);
    expect(r.sveukupniTrosak).toBe(300 + 0 + 100);
    provjeriInvarijante(r, { pdv: false, predujmovi: [100] });
  });
});

// ── C) PRISTOJBE NN 50/2026 + ELEKTRONIČKI ───────────────────────

describe("C — sudske pristojbe po NN 50/2026 (ne po presudama)", () => {
  function pristojba(vps: number, dodatno = {}) {
    return obracunajPristojbe({
      vrijednostSpora: vps,
      odabraneStavke: [stavka("par_tuzba", 1)],
      oslobodjen: false,
      ...dodatno,
    });
  }

  it("presuda 10 · VPS 7.500 € — tužba po Tar. br. 1 st. 1 NN 50/2026", () => {
    // 140,00 + 1 % × (7.500 − 2.000) = 195,00 €
    const r = pristojba(7_500);
    expect(r.ukupno).toBe(195);
    expect(r.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 1");
    expect(r.ukupno).not.toBe(60.73); // stara vrijednost iz presude
  });

  it("elektronički podnesak → pristojba upola (čl. 7 st. 1 Zakona o sudskim pristojbama)", () => {
    const puna = pristojba(7_500).ukupno;
    const elektronicki = pristojba(7_500, { podnesciElektronicki: ["tbr1_st1"] }).ukupno;
    expect(elektronicki).toBe(puna / 2); // 97,50 €
  });
});
