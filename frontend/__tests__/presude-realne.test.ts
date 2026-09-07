import { describe, expect, it } from "vitest";
import {
  izracunajOsnovicu,
  obracunajPristojbe,
  tbr71Bodovi,
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
 *  A) ARITMETIKA — nagrada = Σ priznatih bodova × 2 €; PDV = 25 % SAMO na nagradu
 *     (+ naknada troškova) i samo kad je odvjetnik u sustavu PDV-a; predujmovi i
 *     pristojbe BEZ PDV-a; sveukupno štima.
 *  B) RAZRED VRIJEDNOSTI za sastav tužbe (Tbr. 7. toč. 1) — bodovi po VPS-u.
 *
 * Bodove uspoređujemo s presudama (Tarifa NN 138/23, 1 bod = 2,00 €, nepromijenjena).
 * Sudske pristojbe u presudama su po STAROJ uredbi — NE uspoređuju se ovdje; NN 50/2026
 * pokriva `presude-realne` niže i `tarifa-pristojbe.test.ts` / `tarifa-elektronicki-podnesak.test.ts`.
 *
 * Sud ne dosuđuje uvijek pune tarifne bodove po radnji, a `izracunajTroskovnik` nema
 * slobodan unos „bodova po radnji" u tarifnom izračunu (bodovi dolaze iz kataloga +
 * VPS). Priznatu brojku suda nosi polje `priznatiBodovi` po retku → paralelni
 * `priznato` zbroj. Njega ovdje i provjeravamo.
 */

// ── Pomoćne ──────────────────────────────────────────────────────

/** Minimalna kataloška stavka; parnična, puna nagrada po Tbr. 7. toč. 1. */
function stavka(id: string, over: Partial<TarifnaStavka> = {}): TarifnaStavka {
  return {
    id,
    kategorija: "Parnični postupak",
    opis: id,
    tarifni_broj: "Tbr. 7.1",
    pravni_izvor: "Tbr. 7.1",
    fiksni_body: null,
    tbr71_postotak: 1,
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

function redak(
  itemId: string,
  priznatiBodovi: number | null,
  kolicina = 1,
): RedakIzracunaUlaz {
  return { uid: `${itemId}-${priznatiBodovi}-${kolicina}`, itemId, kolicina, datum: null, tbr41: null, priznatiBodovi };
}

const PRAZNE_PRISTOJBE = { ukupno: 0, vpsUdio: 0, naTeretProtustranke: 0 };

/**
 * Sagradi ulaz za `izracunajTroskovnik` iz VPS-a i popisa radnji s priznatim bodovima.
 * Katalog se izvodi automatski (svaka radnja = puna Tbr. 7.1 stavka), osnovica iz VPS-a.
 */
function slucaj(opts: {
  vps: number;
  pdv: boolean;
  radnje: { id: string; priznati: number | null; kolicina?: number }[];
  predujmovi?: number[];
  naknadaTroskova?: number[];
  extra?: Partial<TroskovnikIzracunUlaz>;
}) {
  const osnovica = izracunajOsnovicu({
    osnovicaTbr7: OSNOVICA_TBR7.Toc1,
    vrijednostSpora: opts.vps,
  }) as OsnovicaRezultat | null;

  const katalog = opts.radnje.map((r) => stavka(r.id));
  const redciPoredani = opts.radnje.map((r) => redak(r.id, r.priznati, r.kolicina ?? 1));

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
    pristojbe: PRAZNE_PRISTOJBE,
    dodatnePristojbe: [],
    predujmovi: (opts.predujmovi ?? []).map((iznos) => ({ opis: "predujam", iznos })),
    naknadaTroskovaStavke: (opts.naknadaTroskova ?? []).map((iznos) => ({ iznos })),
    datumDospijeca: null,
    ...opts.extra,
  });
}

/** Σ priznatih bodova × 2 € — etalon nagrade. */
function ocekivanaNagrada(bodovi: number[]): number {
  return bodovi.reduce((s, b) => s + b, 0) * 2;
}

// ── B) RAZRED VRIJEDNOSTI (Tbr. 7. toč. 1) ───────────────────────

describe("B — razred vrijednosti za sastav tužbe", () => {
  it.each([
    // iz presuda
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
    // provjeravamo da progresivni razred uopće raste i da je unutar kape.
    const veliki = tbr71Bodovi(795_247);
    expect(veliki).toBeGreaterThan(5_000);
    expect(veliki).toBeLessThan(10_000);
    expect(tbr71Bodovi(795_247)).toBeGreaterThan(tbr71Bodovi(700_000));
  });
});

// ── A) ARITMETIKA — 10 presuda ───────────────────────────────────

describe("A — aritmetika nagrade i PDV-a (priznato po rješenju suda)", () => {
  it("1 · Naknada štete na vozilu — TS Zagreb P-2064/2022-41", () => {
    // tužba 100, podnesak 100, podnesak (Tbr. 8.4) 25, 4× ročište 100.
    const bodovi = [100, 100, 25, 100, 100, 100, 100];
    const r = slucaj({
      vps: 13_000,
      pdv: true,
      radnje: [
        { id: "par_tuzba", priznati: 100 },
        { id: "par_podnesak_a", priznati: 100 },
        { id: "par_podnesak_b", priznati: 25 },
        { id: "par_rociste_1", priznati: 100 },
        { id: "par_rociste_2", priznati: 100 },
        { id: "par_rociste_3", priznati: 100 },
        { id: "par_rociste_4", priznati: 100 },
      ],
      predujmovi: [400], // vještačenje — bez PDV-a
    });

    expect(r.priznato).not.toBeNull();
    expect(r.priznato!.osnovicaBodova).toBe(625);
    expect(r.priznato!.nagrada).toBe(ocekivanaNagrada(bodovi)); // 1.250,00 €
    expect(r.priznato!.pdv).toBe(312.5); // 25 % × 1.250
    // predujam NE ulazi u PDV osnovicu
    expect(r.priznato!.osnovicaSaNaknadom).toBe(1_250);
    // sveukupno = nagrada + PDV + pristojbe (0 ovdje) + predujmovi
    expect(r.priznato!.sveukupno).toBe(1_250 + 312.5 + 400);
  });

  it("2 · Parnica s vještačenjem — OS Koprivnica P-445/2023-24", () => {
    // isti tip radnje s RAZLIČITIM bodovima: 50 / 100 / 25
    const bodovi = [100, 100, 100, 100, 50, 100, 100, 100, 25]; // Σ = 775
    const r = slucaj({
      vps: 20_000,
      pdv: true,
      radnje: bodovi.map((b, i) => ({ id: `r${i}`, priznati: b })),
      predujmovi: [398.17],
    });

    expect(r.priznato!.osnovicaBodova).toBe(775);
    expect(r.priznato!.nagrada).toBe(1_550); // 775 × 2
    expect(r.priznato!.pdv).toBe(387.5);
    expect(r.priznato!.sveukupno).toBe(1_550 + 387.5 + 398.17);
  });

  it("3 · Parnica male vrijednosti — OS Split P-4036/2024-12 (VPS 463,53 €)", () => {
    // razred 332–664 € → tužba 50; 3 podneska ×50; 6 ročišta ×50 → Σ = 500
    expect(tbr71Bodovi(463.53)).toBe(50);
    const r = slucaj({
      vps: 463.53,
      pdv: true,
      radnje: Array.from({ length: 10 }, (_, i) => ({ id: `r${i}`, priznati: 50 })), // tužba + 3 podneska + 6 ročišta, sve ×50

      predujmovi: [265.45], // vještačenje
    });

    expect(r.priznato!.osnovicaBodova).toBe(500);
    expect(r.priznato!.nagrada).toBe(1_000); // 500 bod × 2 €
    expect(r.priznato!.pdv).toBe(250);
    // NALAZ: sažetak u izvornom dokumentu kaže „500 bod → nagrada 500 € + PDV 125 €",
    // što je proturječno (500 bodova = 1.000 €). Aritmetika (1 bod = 2 €) daje 1.000 €;
    // vjerojatno je u presudi bilo 250 bodova, ne 500. Provjeriti original.
  });

  it("4 · Ovrha → parnica — TS Zadar Povrv 80/2023-12 (parnični dio)", () => {
    // 2 obrazložena podneska ×100, pripremno ročište 25, glavna rasprava 50 → Σ = 275
    const r = slucaj({
      vps: 10_000,
      pdv: true,
      radnje: [
        { id: "par_podnesak_1", priznati: 100 },
        { id: "par_podnesak_2", priznati: 100 },
        { id: "par_rociste_pripremno", priznati: 25 },
        { id: "par_rasprava_glavna", priznati: 50 },
      ],
      naknadaTroskova: [42.05], // javnobilježnička naknada (ulazi u osnovicu za PDV)
    });

    expect(r.priznato!.osnovicaBodova).toBe(275);
    expect(r.priznato!.nagrada).toBe(550);
    // naknada troškova ULAZI u PDV osnovicu (čl. 33 Zakona o PDV-u)
    expect(r.priznato!.osnovicaSaNaknadom).toBe(550 + 42.05);
    expect(r.priznato!.pdv).toBe(148.01); // 25 % × 592,05 = 148,0125 → 148,01
  });

  it("5 · Ovršni postupak — OGS Zagreb Povrv-2030/2022-15", () => {
    // prijedlog za ovrhu 100, pripremna radnja 100, podnesak 25, ročište 100 → Σ = 325
    const bodovi = [100, 100, 25, 100];
    const r = slucaj({
      vps: 8_000,
      pdv: true,
      radnje: bodovi.map((b, i) => ({ id: `r${i}`, priznati: b })),
      predujmovi: [12.44, 200], // javnobilježnička naknada + vještačenje
    });

    expect(r.priznato!.osnovicaBodova).toBe(325);
    expect(r.priznato!.nagrada).toBe(650);
    expect(r.priznato!.pdv).toBe(162.5);
    expect(r.priznato!.sveukupno).toBe(650 + 162.5 + 12.44 + 200);
  });

  it("6 · Potrošački spor — OS Sisak P-324/2025-20 (granica razreda 1.327,22 €)", () => {
    // 1.327,22 € > 1.327,00 → 100 bodova (ne 75)
    expect(tbr71Bodovi(1_327.22)).toBe(100);

    const r = slucaj({
      vps: 1_327.22,
      pdv: true,
      radnje: [{ id: "par_tuzba", priznati: null }], // puni tarifni izračun
    });
    expect(r.lines[0].bodova_po_jedinici).toBe(100);
    expect(r.lines[0].ukupno_bodova).toBe(100);
    expect(r.ukupnoEur).toBe(200);
    expect(r.pdv).toBe(50);
  });

  it("7 · STRESS TEST — TS Varaždin P 4/2024-18 (djelomični uspjeh)", () => {
    // Honorari punomoćnika 12.627,10 € + PDV 3.156,78 €. 12.627,10 / 2 = 6.313,55 bodova.
    const r = slucaj({
      vps: 60_000,
      pdv: true,
      radnje: [{ id: "par_zbirno", priznati: 6_313.55 }],
      predujmovi: [1_832.64, 515.5, 250], // tri vještačenja — bez PDV-a
    });

    expect(r.priznato!.nagrada).toBe(12_627.1);
    expect(r.priznato!.pdv).toBe(3_156.78); // točno 25 %
    // predujmovi ne diraju PDV osnovicu
    expect(r.priznato!.osnovicaSaNaknadom).toBe(12_627.1);
    expect(r.priznato!.sveukupno).toBe(12_627.1 + 3_156.78 + 1_832.64 + 515.5 + 250);

    // NALAZ: motor NEMA opću „podjelu troška po postotku uspjeha" (74,54 %).
    // `postotak_uspjeha` postoji samo uz oslobođenje od pristojbi (čl. 13), ne kao
    // opći množitelj nagrade. Dijeljenje po uspjehu odvjetnik za sada radi ručno.
  });

  it("8 · Visoka vrijednost (kn) — ŽS Slavonski Brod Gž 186/2023-6 — samo logika", () => {
    // stara tarifa u kunama — v. test u bloku B (rast bodova). Ovdje samo da agregacija
    // ne puca na velikim brojevima.
    const r = slucaj({
      vps: 795_247,
      pdv: true,
      radnje: [{ id: "par_zbirno", priznati: 5_488 }],
    });
    expect(r.priznato!.nagrada).toBe(10_976);
    expect(r.priznato!.pdv).toBe(2_744);
  });

  it("9 · Radni spor, odbijeno ročište (kn) — OS Osijek Pr-1107/2021-20", () => {
    // tužba 100, dva podneska 100+100, ročište SNIŽENO na 50 (procesna pitanja) → Σ = 350
    const bodovi = [100, 100, 100, 50];
    const r = slucaj({
      vps: 5_000,
      pdv: true,
      radnje: [
        { id: "par_tuzba", priznati: 100 },
        { id: "par_podnesak_1", priznati: 100 },
        { id: "par_podnesak_2", priznati: 100 },
        { id: "par_rociste_snizeno", priznati: 50 }, // sud snizio jer su bila procesna pitanja
      ],
    });

    expect(r.priznato!.osnovicaBodova).toBe(350);
    expect(r.priznato!.nagrada).toBe(ocekivanaNagrada(bodovi)); // 700 €
    expect(r.priznato!.pdv).toBe(175);
  });

  it("10 · Baseline — OGS Zagreb P-3260/2025-12 (VPS 7.500 €, puni tarifni)", () => {
    expect(tbr71Bodovi(7_500)).toBe(100);

    const r = slucaj({
      vps: 7_500,
      pdv: true,
      radnje: [{ id: "par_tuzba", priznati: null }],
    });

    // Čist tarifni izračun — bez priznatih bodova
    expect(r.priznato).toBeNull();
    expect(r.lines[0].ukupno_bodova).toBe(100);
    expect(r.ukupnoEur).toBe(200); // 100 × 2 €  → poklapa se s presudom
    expect(r.pdv).toBe(50); // 25 % × 200
    expect(r.ukupnoSPdv).toBe(250);
  });
});

// ── PDV: prekidač isključen ──────────────────────────────────────

describe("A — PDV samo kad je odvjetnik u sustavu PDV-a", () => {
  it("bez PDV-a: pdv = 0, sveukupno bez PDV-a", () => {
    const r = slucaj({
      vps: 7_500,
      pdv: false,
      radnje: [{ id: "par_tuzba", priznati: 300 }],
      predujmovi: [100],
    });
    expect(r.priznato!.pdv).toBe(0);
    expect(r.priznato!.nagrada).toBe(600);
    expect(r.priznato!.sveukupno).toBe(600 + 0 + 100);
    expect(r.pdv).toBe(0);
  });
});

// ── C) PRISTOJBE NN 50/2026 + ELEKTRONIČKI ───────────────────────

describe("C — sudske pristojbe po NN 50/2026 (ne po presudama)", () => {
  function pristojba(vps: number, dodatno = {}) {
    return obracunajPristojbe({
      vrijednostSpora: vps,
      odabraneStavke: [stavka("par_tuzba")],
      oslobodjen: false,
      ...dodatno,
    });
  }

  it("presuda 10 · VPS 7.500 € — tužba po Tar. br. 1 st. 1 NN 50/2026", () => {
    // 140,00 + 1 % × (7.500 − 2.000) = 140 + 55 = 195,00 €
    const r = pristojba(7_500);
    expect(r.ukupno).toBe(195);
    expect(r.stavke[0].tarifni_broj).toBe("Tar. br. 1 st. 1");
    // stara vrijednost iz presude (60,73 €) NIJE ono što app daje
    expect(r.ukupno).not.toBe(60.73);
  });

  it("elektronički podnesak → pristojba upola (čl. 7 st. 1 Zakona o sudskim pristojbama)", () => {
    const puna = pristojba(7_500).ukupno;
    const elektronicki = pristojba(7_500, { podnesciElektronicki: ["tbr1_st1"] }).ukupno;
    expect(elektronicki).toBe(puna / 2); // 97,50 €
  });
});
