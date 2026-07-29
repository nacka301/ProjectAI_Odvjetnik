import { describe, expect, it } from "vitest";
import {
  KAZNENO_UMANJENJE_STOPA,
  TBR_4_8_STOPA,
  ZAPRIJECENA_KAZNA_TIERS,
  ZASTUPNIK_DJETETA_STOPA,
  izracunajKazneneBodove,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaUmanjenjaTests.cs.
 *
 * Obje stope dolaze iz Pravilnika, NN 22/2022 br. 271.
 */

const obrana: TarifnaStavka = {
  id: "kaz_obrana",
  kategorija: "Kazneni postupak",
  opis: "",
  tarifni_broj: "Tbr. 4.1",
  fiksni_body: null,
  tbr71_postotak: null,
  tbr71_povecanje: null,
  max_bodova: null,
  min_bodova: null,
  jedinica: "sat",
  sort_order: 0,
  kazneni_base: true,
  kazneni_povecanje: null,
  prekrsaj_oduzimanje: false,
};

function bodovi(
  kazna: string,
  opts: { dijete?: boolean; sluzbena?: boolean; proracun?: boolean } = {},
) {
  return izracunajKazneneBodove(
    obrana,
    ZAPRIJECENA_KAZNA_TIERS[kazna].bodovi,
    opts.dijete ?? false,
    opts.sluzbena ?? false,
    opts.proracun ?? false,
    kazna,
  );
}

describe("branitelj po službenoj dužnosti / na teret proračuna", () => {
  it("stopa je 50%", () => {
    expect(KAZNENO_UMANJENJE_STOPA).toBe(0.5);
  });

  it("dugotrajni zatvor: 500 → 250 bodova", () => {
    expect(bodovi("dugotrajni", { sluzbena: true })).toBe(250);
    expect(bodovi("dugotrajni", { proracun: true })).toBe(250);
  });
});

describe("opunomoćenik djeteta žrtve kaznenog djela", () => {
  it("efektivna stopa je 35%, ne 70%", () => {
    expect(TBR_4_8_STOPA * ZASTUPNIK_DJETETA_STOPA).toBeCloseTo(0.35, 10);
  });

  it.each([
    ["novcana_do3", 35],
    ["do_5", 70],
    ["do_10", 105],
    ["preko_10", 140],
    ["dugotrajni", 175],
  ])("%s → %i bodova", (kazna, ocekivano) => {
    expect(bodovi(kazna, { dijete: true })).toBeCloseTo(ocekivano, 10);
  });

  it("dugotrajni zatvor nije 350 (to bi bilo 70% pune tarife)", () => {
    expect(bodovi("dugotrajni", { dijete: true })).not.toBe(350);
  });
});
