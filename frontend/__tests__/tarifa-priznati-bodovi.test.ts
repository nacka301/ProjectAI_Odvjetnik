/**
 * `obracunajPriznato` — paralelni „Po Tarifi" vs „Priznato (rješenje suda)" izračun.
 *
 * Sud ne dosuđuje uvijek pune tarifne bodove po radnji (tetka, iz presuda). Odvjetnik
 * uz redak upiše priznatu brojku; ovaj izračun je čista agregacija povrh tarifnog i
 * zrcali backend `TroskovnikService.IzracunajPriznato`.
 */
import { describe, expect, it } from "vitest";
import { BOD_VALUE, obracunajPriznato, type PriznatiRedakUlaz } from "@/lib/tarifa";

const OPCIJE = {
  povisenjePostotak: 0,
  bodValue: BOD_VALUE,
  naknadaTroskovaUkupno: 0,
  isPdvUkljucen: false,
  ukupnoPristojbe: 0,
  ukupnoPredujmovi: 0,
  brojZastupanihOsoba: 1,
};

// Tri retka: tužba 500, ročište 500, žalba 625 bod. (VPS 50.000 €)
const REDCI: PriznatiRedakUlaz[] = [
  { ukupnoBodova: 500, tbr41Bodovi: 0 },
  { ukupnoBodova: 500, tbr41Bodovi: 0 },
  { ukupnoBodova: 625, tbr41Bodovi: 0 },
];

describe("obracunajPriznato", () => {
  it("vraća null kad nijedan redak nema priznati unos", () => {
    expect(obracunajPriznato(REDCI, OPCIJE)).toBeNull();
  });

  it("prikovani redak ulazi svojom brojkom, ostali punim tarifnim doprinosom", () => {
    const redci = [
      { ukupnoBodova: 500, tbr41Bodovi: 0, priznatiBodovi: 300 },
      { ukupnoBodova: 500, tbr41Bodovi: 0 },
      { ukupnoBodova: 625, tbr41Bodovi: 0 },
    ];
    const r = obracunajPriznato(redci, OPCIJE)!;
    expect(r.osnovicaBodova).toBe(1425); // 300 + 500 + 625
    expect(r.ukupnoBodova).toBe(1425);
    expect(r.nagrada).toBe(2850); // 1425 × 2
    expect(r.sveukupno).toBe(2850);
  });

  it("neprikovani redak nosi i svoj Tbr. 41 doprinos", () => {
    const redci = [
      { ukupnoBodova: 500, tbr41Bodovi: 500, priznatiBodovi: 300 }, // Tbr. 41 se ignorira → 300
      { ukupnoBodova: 500, tbr41Bodovi: 250 }, // 750
    ];
    const r = obracunajPriznato(redci, OPCIJE)!;
    expect(r.osnovicaBodova).toBe(1050); // 300 + 750
  });

  it("Tbr. 40 (više osoba) primjenjuje isti postotak na priznatu osnovicu", () => {
    const redci = [{ ukupnoBodova: 500, tbr41Bodovi: 0, priznatiBodovi: 300 }];
    const r = obracunajPriznato(redci, {
      ...OPCIJE,
      povisenjePostotak: 0.1,
      brojZastupanihOsoba: 2,
    })!;
    expect(r.osnovicaBodova).toBe(300);
    expect(r.povisenjeBodovi).toBeCloseTo(30);
    expect(r.ukupnoBodova).toBeCloseTo(330);
    expect(r.nagrada).toBe(660);
    expect(r.nagradaPoOsobi).toBe(330);
  });

  it("PDV se obračunava na priznatu nagradu + naknadu troškova", () => {
    const redci = [{ ukupnoBodova: 500, tbr41Bodovi: 0, priznatiBodovi: 300 }];
    const r = obracunajPriznato(redci, {
      ...OPCIJE,
      isPdvUkljucen: true,
      naknadaTroskovaUkupno: 100,
    })!;
    expect(r.osnovicaSaNaknadom).toBe(700); // 600 + 100
    expect(r.pdv).toBe(175); // 25 % × 700
    expect(r.ukupnoSPdv).toBe(875);
  });

  it("pristojbe i predujmovi ulaze u sveukupno nepromijenjeni", () => {
    const redci = [{ ukupnoBodova: 500, tbr41Bodovi: 0, priznatiBodovi: 300 }];
    const r = obracunajPriznato(redci, {
      ...OPCIJE,
      ukupnoPristojbe: 620,
      ukupnoPredujmovi: 400,
    })!;
    expect(r.sveukupno).toBe(600 + 620 + 400);
  });
});
