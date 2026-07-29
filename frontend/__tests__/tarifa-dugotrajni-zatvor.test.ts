import { describe, expect, it } from "vitest";
import {
  KAZNA_DUGOTRAJNI_ZATVOR,
  ZAPRIJECENA_KAZNA_TIERS,
  izracunajKazneneBodove,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaDugotrajniZatvorTests.cs.
 *
 * Tbr. 4. toč. 2 al. 3: rasprava pred drugim stupnjem + 25%, a pred drugim i trećim
 * stupnjem za djela s kaznom dugotrajnog zatvora + 50%.
 */

function stavka(over: Partial<TarifnaStavka>): TarifnaStavka {
  return {
    id: "x",
    kategorija: "Kazneni postupak",
    opis: "",
    tarifni_broj: "",
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: true,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
    ...over,
  };
}

const rasprava = stavka({
  id: "kaz_rasprava_drugostupanjska",
  kazneni_povecanje: 0.25,
  kazneni_povecanje_dugotrajni: 0.5,
});

const zalba = stavka({ id: "kaz_zalba_presuda", kazneni_povecanje: 0.5 });

function bodovi(def: TarifnaStavka, kazna: string) {
  return izracunajKazneneBodove(
    def,
    ZAPRIJECENA_KAZNA_TIERS[kazna].bodovi,
    false,
    false,
    false,
    kazna,
  );
}

describe("Tbr. 4. toč. 2 al. 3 — rasprava pred višim sudom", () => {
  it("dugotrajni zatvor → +50%", () => {
    expect(bodovi(rasprava, KAZNA_DUGOTRAJNI_ZATVOR)).toBe(750); // 500 × 1,5
  });

  it.each([
    ["novcana_do3", 125],
    ["do_5", 250],
    ["do_10", 375],
    ["preko_10", 500],
  ])("%s → +25% (%i bodova)", (kazna, ocekivano) => {
    expect(bodovi(rasprava, kazna)).toBe(ocekivano);
  });
});

describe("žalba protiv presude (Tbr. 5. toč. 1)", () => {
  it.each([
    ["do_5", 300],
    [KAZNA_DUGOTRAJNI_ZATVOR, 750],
  ])("%s → svojih +50% (%i bodova)", (kazna, ocekivano) => {
    expect(bodovi(zalba, kazna)).toBe(ocekivano);
  });
});
