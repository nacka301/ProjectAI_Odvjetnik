import { describe, expect, it } from "vitest";
import { formatirajDatum } from "@/lib/format";
import { nedostatakDatuma, poredajPoDatumu } from "@/lib/tarifa";

/**
 * Datum uz radnju — prikaz, kronološki poredak i provjera prije spremanja i ispisa.
 *
 * Datum putuje i sprema se u ISO obliku gggg-mm-dd; hrvatski oblik postoji samo na
 * ekranu i na papiru.
 */

describe("formatirajDatum", () => {
  it("ISO oblik pretvara u hrvatski, s vodećim nulama", () => {
    expect(formatirajDatum("2025-03-12")).toBe("12.03.2025.");
    expect(formatirajDatum("2025-12-31")).toBe("31.12.2025.");
  });

  /**
   * toLocaleDateString("hr-HR") daje "12.3.2025." — na troškovniku u kojem se datumi
   * slažu jedan ispod drugoga to izgleda neuredno, pa se vodeće nule zadržavaju.
   */
  it("jednoznamenkasti dan i mjesec zadržavaju nulu", () => {
    expect(formatirajDatum("2025-01-05")).toBe("05.01.2025.");
  });

  it.each([null, undefined, "", "   "])("prazan ulaz (%s) daje prazan niz", (ulaz) => {
    expect(formatirajDatum(ulaz)).toBe("");
  });

  /** U tablici se ne smije pojaviti "Invalid Date". */
  it.each(["12.03.2025.", "2025-3-4", "jučer", "2025/03/12"])(
    "neispravan oblik (%s) daje prazan niz",
    (ulaz) => {
      expect(formatirajDatum(ulaz)).toBe("");
    },
  );
});

describe("poredajPoDatumu", () => {
  const radnja = (redakId: string, datum: string) => ({ redakId, datum });

  it("slaže radnje kronološki", () => {
    const poredano = poredajPoDatumu([
      radnja("c", "2025-05-19"),
      radnja("a", "2025-03-12"),
      radnja("b", "2025-04-04"),
    ]);

    expect(poredano.map((r) => r.redakId)).toEqual(["a", "b", "c"]);
  });

  /** Radnje bez datuma nisu „najstarije" nego nedovršene — idu na dno. */
  it("radnje bez datuma idu na dno", () => {
    const poredano = poredajPoDatumu([
      radnja("bez", ""),
      radnja("kasnija", "2025-05-19"),
      radnja("ranija", "2025-03-12"),
    ]);

    expect(poredano.map((r) => r.redakId)).toEqual(["ranija", "kasnija", "bez"]);
  });

  it("radnje istog datuma zadržavaju uneseni poredak", () => {
    const poredano = poredajPoDatumu([
      radnja("prva", "2025-03-12"),
      radnja("druga", "2025-03-12"),
      radnja("treca", "2025-03-12"),
    ]);

    expect(poredano.map((r) => r.redakId)).toEqual(["prva", "druga", "treca"]);
  });

  it("poredak preko granice godine je datumski, ne tekstualni", () => {
    const poredano = poredajPoDatumu([radnja("nova", "2026-01-08"), radnja("stara", "2025-12-31")]);

    expect(poredano.map((r) => r.redakId)).toEqual(["stara", "nova"]);
  });

  it("ne dira izvorni popis", () => {
    const izvorni = [radnja("b", "2025-05-19"), radnja("a", "2025-03-12")];
    poredajPoDatumu(izvorni);

    expect(izvorni.map((r) => r.redakId)).toEqual(["b", "a"]);
  });

  it("prazan popis prolazi", () => {
    expect(poredajPoDatumu([])).toEqual([]);
  });
});

describe("nedostatakDatuma", () => {
  it("svi datumi popunjeni → nema nedostatka", () => {
    expect(
      nedostatakDatuma([
        { datum: "2025-03-12", opis: "Tužba" },
        { datum: "2025-04-04", opis: "Ročište" },
      ]),
    ).toBeNull();
  });

  /** Poruka mora reći KOJI redak, inače u troškovniku od dvadeset radnji ne vrijedi ništa. */
  it("imenuje radnje bez datuma, s rednim brojem", () => {
    const poruka = nedostatakDatuma([
      { datum: "2025-03-12", opis: "Tužba" },
      { datum: "", opis: "Ročište" },
      { datum: "", opis: "Očevid" },
    ]);

    expect(poruka).toContain("2. Ročište");
    expect(poruka).toContain("3. Očevid");
    expect(poruka).not.toContain("1. Tužba");
  });

  it("redni broj prati položaj u troškovniku, ne redoslijed nedostataka", () => {
    const poruka = nedostatakDatuma([
      { datum: "", opis: "Prva" },
      { datum: "2025-03-12", opis: "Druga" },
      { datum: "", opis: "Treća" },
    ]);

    expect(poruka).toContain("1. Prva");
    expect(poruka).toContain("3. Treća");
  });

  it("prazan troškovnik nema što prijaviti", () => {
    expect(nedostatakDatuma([])).toBeNull();
  });
});
