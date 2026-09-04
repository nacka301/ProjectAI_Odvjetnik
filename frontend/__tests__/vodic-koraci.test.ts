/**
 * Tri vodiča — brojevi i sidra.
 *
 * Nekad je ovdje stajalo jedno pravilo o „predaji štafete": dva ulančana
 * vodiča brojala su 1/7…7/7 i smjela su upisati `onboarded` tek na kraju
 * drugoga. Ta se konstrukcija raspala kad je vodič preselio iznad ruta —
 * korak s `ruta` sad sam navigira, pa lanac više ne postoji, a s njim ni
 * pravilo koje ga je držalo.
 *
 * Ostalo je ono što se i dalje lako slomi tiho: pogrešna numeracija (korisnik
 * ne zna koliko je ostalo) i dva koraka na isto sidro (dva prstena odjednom,
 * ili korak koji izgleda kao da se vodič zaglavio).
 */
import { describe, expect, it } from "vitest";
import {
  DOBRODOSLICA_KORACI,
  STRANKE_KORACI,
  TROSKOVNIK_KORACI,
  type VodicKorak,
} from "@/lib/vodic-koraci";

/** Koraci koji nose broj u naslovu — završna kartica uvoda ga nema. */
function numerirani(koraci: VodicKorak[]) {
  return koraci.filter((k) => /^\d+\//.test(k.title));
}

describe.each([
  ["dobrodošlica", DOBRODOSLICA_KORACI],
  ["troškovnik", TROSKOVNIK_KORACI],
  ["stranke", STRANKE_KORACI],
])("vodič: %s", (_naziv, koraci) => {
  it("numeracija teče 1/N … N/N bez rupa i bez ponavljanja", () => {
    const n = numerirani(koraci);
    expect(n.map((k) => k.title.split(" ")[0])).toEqual(
      Array.from({ length: n.length }, (_, i) => `${i + 1}/${n.length}`),
    );
  });

  it("nijedno sidro se ne ponavlja — dva bi se prstenovala istovremeno", () => {
    const sidra = koraci.map((k) => k.target).filter(Boolean);
    expect(new Set(sidra).size).toBe(sidra.length);
  });

  it("svaki korak ima naslov i tekst", () => {
    for (const k of koraci) {
      expect(k.title.trim().length).toBeGreaterThan(0);
      expect(k.text.trim().length).toBeGreaterThan(0);
    }
  });
});

describe("vodič dobrodošlice", () => {
  it("ima najviše osam numeriranih koraka", () => {
    // „Najviše 8 koraka" je odluka o pažnji, ne o formatiranju: uvod koji traje
    // duže od dvije minute nitko ne dovrši.
    expect(numerirani(DOBRODOSLICA_KORACI).length).toBeLessThanOrEqual(8);
  });

  it("počinje i završava karticom bez sidra", () => {
    // Prva kartica najavljuje primjer, zadnja se oprašta — ni jedna ne pokazuje
    // ni na što, pa stoje na sredini ekrana.
    expect(DOBRODOSLICA_KORACI[0].target).toBeUndefined();
    expect(DOBRODOSLICA_KORACI[DOBRODOSLICA_KORACI.length - 1].target).toBeUndefined();
  });

  it("prelazi na stranke tek u zadnjem numeriranom koraku", () => {
    const n = numerirani(DOBRODOSLICA_KORACI);
    const naStrankama = n.filter((k) => k.ruta === "/klijenti");
    expect(naStrankama).toHaveLength(1);
    expect(naStrankama[0]).toBe(n[n.length - 1]);
  });
});

describe("vodiči na zahtjev", () => {
  it("ne navigiraju nikamo — rade ondje gdje korisnik već jest", () => {
    // Troškovnik i Stranke pokreću se iz izbornika, na stvarnim podacima.
    // Kad bi imali `ruta`, odveli bi odvjetnika s posla koji radi.
    for (const k of [...TROSKOVNIK_KORACI, ...STRANKE_KORACI]) {
      expect(k.ruta).toBeUndefined();
    }
  });
});
