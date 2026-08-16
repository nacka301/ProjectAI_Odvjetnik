import { describe, expect, it } from "vitest";
import {
  PRAVNI_LIJEK,
  VRSTA_POSTUPKA,
  VRSTA_POSTUPKA_PRAVILA,
  ZAHTJEVI_NACIN,
  izracunajSudskuPristojbu,
  provjeriZahtjev,
  razrijesiVps,
  type VpsUlaz,
} from "@/lib/tarifa";

/**
 * Vrijednost predmeta spora prema Zakonu o sudskim pristojbama (NN 118/18, 51/23),
 * čl. 14–26. Zrcali `VpsVrstaPostupkaTests` na backendu — ista pravila moraju dati
 * iste brojeve na obje strane.
 */

const osnovni = (dopuna: Partial<VpsUlaz> = {}): VpsUlaz => ({
  vrijednostSpora: 0,
  vrstaPostupka: VRSTA_POSTUPKA.Opci,
  ...dopuna,
});

describe("VPS — fiksne vrijednosti (čl. 20, 21, 25)", () => {
  it.each([
    [VRSTA_POSTUPKA.SmetanjePosjeda, 663.61],
    [VRSTA_POSTUPKA.OcinstvoMajcinstvo, 663.61],
    [VRSTA_POSTUPKA.Brak, 663.61],
    [VRSTA_POSTUPKA.OsiguranjeDokaza, 663.61],
    [VRSTA_POSTUPKA.NeutvrdivaVrijednost, 1_327.22],
    [VRSTA_POSTUPKA.PrestanakArbitraze, 6_634.14],
  ])("%s gazi uneseni iznos → %s €", (vrsta, ocekivano) => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 50_000, vrstaPostupka: vrsta }));

    expect(r?.vps).toBe(ocekivano);
    expect(r?.prilagodjen).toBe(true);
    expect(r?.napomena).toBeTruthy();
  });

  it("fiksni iznos vrijedi i kad je uneseno manje", () => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 100, vrstaPostupka: VRSTA_POSTUPKA.SmetanjePosjeda }));

    expect(r?.vps).toBe(663.61);
  });

  it("kad je uneseno točno propisano, nema što objašnjavati", () => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 663.61, vrstaPostupka: VRSTA_POSTUPKA.SmetanjePosjeda }));

    expect(r?.prilagodjen).toBe(false);
    expect(r?.napomena).toBeNull();
  });
});

describe("VPS — donje i gornje granice", () => {
  it.each([
    [VRSTA_POSTUPKA.NenovcaniZahtjev, 500, 1_327.22],
    [VRSTA_POSTUPKA.NenovcaniZahtjev, 80_000, 80_000],
    [VRSTA_POSTUPKA.OtkazNajmaZakupa, 1_200, 2_654.45],
    [VRSTA_POSTUPKA.OtkazNajmaZakupa, 12_000, 12_000],
    [VRSTA_POSTUPKA.PonistenjePravorijeka, 1_000, 6_634.14],
    [VRSTA_POSTUPKA.PonistenjePravorijeka, 90_000, 90_000],
  ])("donja granica %s: %s → %s", (vrsta, uneseni, ocekivano) => {
    expect(razrijesiVps(osnovni({ vrijednostSpora: uneseni, vrstaPostupka: vrsta }))?.vps).toBe(ocekivano);
  });

  it.each([
    [VRSTA_POSTUPKA.RedPrvenstvaOvrha, 40_000, 1_327.22],
    [VRSTA_POSTUPKA.RedPrvenstvaOvrha, 800, 800],
    [VRSTA_POSTUPKA.PrethodnoPravnoPitanje, 40_000, 1_327.22],
    [VRSTA_POSTUPKA.PrethodnoPravnoPitanje, 50, 50],
  ])("gornja granica %s: %s → %s", (vrsta, uneseni, ocekivano) => {
    expect(razrijesiVps(osnovni({ vrijednostSpora: uneseni, vrstaPostupka: vrsta }))?.vps).toBe(ocekivano);
  });
});

describe("VPS — čl. 15 uzdržavanje", () => {
  it("zbroj davanja za tri mjeseca", () => {
    const r = razrijesiVps(
      osnovni({ vrstaPostupka: VRSTA_POSTUPKA.Uzdrzavanje, uzdrzavanjeMjesecno: 400 }),
    );

    expect(r?.vps).toBe(1_200);
  });

  it("bez mjesečnog iznosa nema se što izvesti", () => {
    const ulaz = osnovni({ vrijednostSpora: 5_000, vrstaPostupka: VRSTA_POSTUPKA.Uzdrzavanje });

    expect(razrijesiVps(ulaz)).toBeNull();
    expect(provjeriZahtjev([], { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, vps: ulaz })).toEqual([
      expect.stringContaining("tri mjeseca"),
    ]);
  });
});

describe("VPS — čl. 14, 17, 18 kažu ŠTO se upisuje, ne koliko", () => {
  it.each([VRSTA_POSTUPKA.Opci, VRSTA_POSTUPKA.DiobaImovine, VRSTA_POSTUPKA.Nasljedivanje])(
    "%s ostavlja uneseni iznos",
    (vrsta) => {
      const r = razrijesiVps(osnovni({ vrijednostSpora: 37_500, vrstaPostupka: vrsta }));

      expect(r?.vps).toBe(37_500);
      expect(r?.prilagodjen).toBe(false);
    },
  );

  it("dioba i nasljeđivanje nose naputak što se upisuje", () => {
    expect(VRSTA_POSTUPKA_PRAVILA[VRSTA_POSTUPKA.DiobaImovine].naputak).toContain("izdvojiti");
    expect(VRSTA_POSTUPKA_PRAVILA[VRSTA_POSTUPKA.Nasljedivanje].naputak).toContain("nasljednog dijela");
  });
});

describe("VPS — čl. 16 više zahtjeva", () => {
  const tri = [{ iznos: 1_000 }, { iznos: 2_000 }, { iznos: 3_000 }];

  it("isti tuženik → zbroj", () => {
    const r = razrijesiVps(osnovni({ zahtjevi: tri, zahtjeviNacin: ZAHTJEVI_NACIN.Zbroj }));

    expect(r?.vps).toBe(6_000);
  });

  it("eventualni zahtjevi → veći", () => {
    const r = razrijesiVps(osnovni({ zahtjevi: tri, zahtjeviNacin: ZAHTJEVI_NACIN.Eventualni }));

    expect(r?.vps).toBe(3_000);
  });

  it("suparničarstvo → ne zbraja, uzima označeni i izdvaja ostale", () => {
    const r = razrijesiVps(
      osnovni({
        zahtjevi: [{ iznos: 1_000 }, { iznos: 2_000, primarni: true }, { iznos: 3_000 }],
        zahtjeviNacin: ZAHTJEVI_NACIN.Zasebno,
      }),
    );

    expect(r?.vps).toBe(2_000);
    expect(r?.ostaliZahtjevi.map((z) => z.iznos)).toEqual([1_000, 3_000]);
  });

  it.each([
    [[{ iznos: 1_000 }, { iznos: 2_000 }]],
    [[{ iznos: 1_000, primarni: true }, { iznos: 2_000, primarni: true }]],
  ])("suparničarstvo bez točno jednog označenog daje poruku", (zahtjevi) => {
    const ulaz = osnovni({ zahtjevi, zahtjeviNacin: ZAHTJEVI_NACIN.Zasebno });

    expect(razrijesiVps(ulaz)).toBeNull();
    expect(provjeriZahtjev([], { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, vps: ulaz })).toEqual([
      expect.stringContaining("točno jedan"),
    ]);
  });

  it("bez odabranog načina nema izračuna", () => {
    const ulaz = osnovni({ zahtjevi: tri });

    expect(razrijesiVps(ulaz)).toBeNull();
    expect(provjeriZahtjev([], { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, vps: ulaz })).toEqual([
      expect.stringContaining("Čl. 16"),
    ]);
  });
});

describe("VPS — čl. 24 pravni lijekovi", () => {
  it("cijela odluka zadržava prvostupanjski VPS", () => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 20_000, pravniLijek: PRAVNI_LIJEK.CijelaOdluka }));

    expect(r?.vps).toBe(20_000);
  });

  it.each([PRAVNI_LIJEK.PobijaniDio, PRAVNI_LIJEK.SamoTroskovi])("%s uzima upisani iznos", (lijek) => {
    const r = razrijesiVps(
      osnovni({ vrijednostSpora: 20_000, pravniLijek: lijek, pravniLijekIznos: 800 }),
    );

    expect(r?.vps).toBe(800);
    expect(r?.prilagodjen).toBe(true);
  });

  it.each([PRAVNI_LIJEK.PobijaniDio, PRAVNI_LIJEK.SamoTroskovi])("%s bez iznosa daje poruku", (lijek) => {
    const ulaz = osnovni({ vrijednostSpora: 20_000, pravniLijek: lijek });

    expect(razrijesiVps(ulaz)).toBeNull();
    expect(provjeriZahtjev([], { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, vps: ulaz })).toEqual([
      expect.stringContaining("čl. 24"),
    ]);
  });
});

describe("VPS — redoslijed čl. 16 → pravilo vrste → čl. 24", () => {
  const zahtjevi = [{ iznos: 40_000 }, { iznos: 10_000 }];

  it("bez pravnog lijeka: zbroj pa gornja granica iz čl. 21 st. 5", () => {
    const r = razrijesiVps(
      osnovni({
        vrstaPostupka: VRSTA_POSTUPKA.RedPrvenstvaOvrha,
        zahtjevi,
        zahtjeviNacin: ZAHTJEVI_NACIN.Zbroj,
      }),
    );

    expect(r?.vps).toBe(1_327.22);
  });

  it("čl. 24 nadglasava i čl. 16 i pravilo vrste postupka", () => {
    const r = razrijesiVps(
      osnovni({
        vrstaPostupka: VRSTA_POSTUPKA.RedPrvenstvaOvrha,
        zahtjevi,
        zahtjeviNacin: ZAHTJEVI_NACIN.Zbroj,
        pravniLijek: PRAVNI_LIJEK.PobijaniDio,
        pravniLijekIznos: 5_000,
      }),
    );

    expect(r?.vps).toBe(5_000);
  });
});

describe("VPS — doseg", () => {
  /**
   * Smetanje posjeda s upisanih 50.000 €: pristojba ide po 663,61 € → razred
   * do 796,34 € → 26,54 €.
   */
  it("zakonski VPS obavezno određuje sudsku pristojbu", () => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 50_000, vrstaPostupka: VRSTA_POSTUPKA.SmetanjePosjeda }));

    expect(izracunajSudskuPristojbu(r!.vps)).toBe(26.54);
    expect(izracunajSudskuPristojbu(50_000)).toBe(546.45);
  });

  it("uneseni iznos ostaje vidljiv uz zakonski", () => {
    const r = razrijesiVps(osnovni({ vrijednostSpora: 50_000, vrstaPostupka: VRSTA_POSTUPKA.SmetanjePosjeda }));

    expect(r?.uneseni).toBe(50_000);
    expect(r?.vps).toBe(663.61);
  });
});
