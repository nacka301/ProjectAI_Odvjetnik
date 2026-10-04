// Paritet s Backend.Tests/TarifaUlogaZastupanjaTests.cs
import { describe, expect, it } from "vitest";
import { izracunajTroskovnik } from "@/lib/troskovnik-izracun";
import {
  napomenaUmanjenjaUloge,
  provjeriUlogu,
  provjeriZahtjev,
  tarifniBrojStavke,
  type TarifnaStavka,
  type UlogaZastupanja,
} from "@/lib/tarifa";

function stavka(p: Partial<TarifnaStavka> & Pick<TarifnaStavka, "id" | "tarifni_broj">): TarifnaStavka {
  return {
    kategorija: "Kazneni postupak",
    opis: p.id,
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "sat",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...p,
  };
}

const KATALOG = [
  stavka({ id: "kaz_obrana", tarifni_broj: "Tbr. 4.1", kazneni_base: true }),
  stavka({ id: "kaz_zalba_presuda", tarifni_broj: "Tbr. 5.1", kazneni_base: true, kazneni_povecanje: 0.5 }),
  stavka({ id: "kaz_istraga_dodatni_sat", tarifni_broj: "Tbr. 3.1", fiksni_body: 50 }),
  stavka({ id: "kaz_zastupanje_ostecenika", tarifni_broj: "Tbr. 4.8", kazneni_base: true, kazneni_povecanje: -0.5, umanjenje_uloge_ugradjeno: true }),
];

function racun(
  uloga: UlogaZastupanja | null,
  itemId = "kaz_obrana",
  podesi: Partial<Parameters<typeof izracunajTroskovnik>[0]> = {},
) {
  return izracunajTroskovnik({
    redciPoredani: [{ uid: "1", itemId, kolicina: 1, datum: null }],
    katalog: KATALOG,
    osnovica: { bodovi: 0, pravniIzvor: "" } as never,
    isPdvUkljucen: true,
    kazneniTierBodovi: 200, // do 5 god.
    isZastupnikDjeteta: false,
    isPoSluzbenojDuznosti: false,
    isNaTeretProracuna: false,
    isIzabraniOslobodjajuca: false,
    ulogaZastupanja: uloga,
    zaprijecenaKazna: "do_5",
    isOduzimanjePrekrsaj: false,
    parsedVrijednostOduzimanja: 0,
    brojZastupanihOsoba: 1,
    tbr41Povisenje: 0,
    tbr41Snizenje: 0,
    pristojbe: { ukupno: 0, vpsUdio: 0, naTeretProtustranke: 0 },
    dodatnePristojbe: [],
    predujmovi: [],
    naknadaTroskovaStavke: [],
    datumDospijeca: null,
    ...podesi,
  } as Parameters<typeof izracunajTroskovnik>[0]);
}

describe("uloga zastupanja — paritet s backendom", () => {
  it("branitelj, rasprava 1 sat → 500,00 € s PDV-om", () => {
    const r = racun("BRANITELJ");
    expect(r.ukupnoBodova).toBe(200);
    expect(r.ukupnoSPdv).toBe(500);
  });

  it.each(["PRIVATNI_TUZITELJ", "OSTECENIK_KAO_TUZITELJ"] as const)("%s → puna nagrada", (uloga) => {
    const r = racun(uloga);
    expect(r.ukupnoSPdv).toBe(500);
    expect(r.lines[0].umanjenje_uloge).toBe("");
  });

  it("punomoćnik oštećenika, rasprava → 250,00 € s PDV-om", () => {
    const r = racun("PUNOMOCNIK_OSTECENIKA");
    expect(r.ukupnoBodova).toBe(100);
    expect(r.ukupnoSPdv).toBe(250);
    expect(r.lines[0].umanjenje_uloge).toBe("umanjeno 50% (Tbr. 4 t. 8 Tarife)");
  });

  it("punomoćnik oštećenika, žalba (Tbr. 5) → puna nagrada", () => {
    expect(racun("PUNOMOCNIK_OSTECENIKA", "kaz_zalba_presuda").ukupnoBodova).toBe(300);
  });

  it("izabrani branitelj na teret proračuna → puna nagrada", () => {
    const r = racun("BRANITELJ", "kaz_obrana", { isNaTeretProracuna: true, isIzabraniOslobodjajuca: true });
    expect(r.ukupnoBodova).toBe(200);
  });

  it("branitelj po službenoj dužnosti → 50%", () => {
    expect(racun("BRANITELJ", "kaz_obrana", { isPoSluzbenojDuznosti: true }).ukupnoBodova).toBe(100);
  });

  it("istraga, sljedeći sat (fiksnih 50 bodova) → 25", () => {
    const r = racun("PUNOMOCNIK_OSTECENIKA", "kaz_istraga_dodatni_sat");
    expect(r.ukupnoBodova).toBe(25);
    expect(r.lines[0].umanjenje_uloge).toBe("umanjeno 50% (Tbr. 3 t. 3 Tarife)");
  });

  it("stavka s ugrađenim umanjenjem oštećenika ne umanjuje se dvaput", () => {
    expect(racun("PUNOMOCNIK_OSTECENIKA", "kaz_zastupanje_ostecenika").ukupnoBodova).toBe(100);
  });
});

describe("uloga zastupanja — provjere", () => {
  const ulaz = { isOduzimanjePrekrsaj: false, vrijednostOduzimanja: 0, zaprijecenaKazna: "do_5" };

  it("kazneni troškovnik bez uloge → greška", () => {
    expect(provjeriUlogu([KATALOG[0]], null).join(" ")).toContain("Odaberite koga zastupate");
    expect(provjeriUlogu([KATALOG[0]], "BRANITELJ")).toEqual([]);
  });

  it("službena dužnost uz oštećenika → greška", () => {
    const g = provjeriZahtjev([KATALOG[0]], { ...ulaz, ulogaZastupanja: "PUNOMOCNIK_OSTECENIKA", isPoSluzbenojDuznosti: true });
    expect(g.join(" ")).toContain("samo za branitelja");
  });

  it("oštećenik + opunomoćenik žrtve → upozorenje bez iznosa", () => {
    const g = provjeriZahtjev([KATALOG[0]], { ...ulaz, ulogaZastupanja: "PUNOMOCNIK_OSTECENIKA", isOpunomocenikZrtve: true });
    expect(g.join(" ")).toContain("ne nudi automatski iznos");
  });

  it("tarifni broj se čita iz oznake stavke", () => {
    expect(tarifniBrojStavke("Tbr. 4.1")).toBe(4);
    expect(tarifniBrojStavke("Tbr. 3.3")).toBe(3);
    expect(napomenaUmanjenjaUloge("Tbr. 3.1")).toBe("umanjeno 50% (Tbr. 3 t. 3 Tarife)");
  });
});
