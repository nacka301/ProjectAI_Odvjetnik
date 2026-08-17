import { describe, expect, it } from "vitest";
import {
  KAZNENO_UMANJENJE_STOPA,
  ZAPRIJECENA_KAZNA_TIERS,
  ZASTUPNIK_DJETETA_STOPA,
  izracunajKazneneBodove,
  type TarifnaStavka,
} from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaUmanjenjaTests.cs.
 *
 * Obje stope dolaze iz Pravilnika, NN 22/2022 br. 271 (čl. 1 → 50%, čl. 2 → 70%) i idu
 * od iste osnovice — nagrade za pojedinu radnju prema Tarifi — pa se ne množe.
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
  it("stopa je 70%", () => {
    expect(ZASTUPNIK_DJETETA_STOPA).toBe(0.7);
  });

  it.each([
    ["novcana_do3", 70],
    ["do_5", 140],
    ["do_10", 210],
    ["preko_10", 280],
    ["dugotrajni", 350],
  ])("%s → %i bodova", (kazna, ocekivano) => {
    expect(bodovi(kazna, { dijete: true })).toBeCloseTo(ocekivano, 10);
  });

  it("dugotrajni zatvor nije 175 (to je bio umnožak 0,50 × 0,70)", () => {
    expect(bodovi("dugotrajni", { dijete: true })).not.toBe(175);
  });
});

describe("uloge se isključuju — stope se ne množe", () => {
  it("obje zastavice daju 70%, ne 0,35 × 0,50", () => {
    expect(bodovi("dugotrajni", { dijete: true, sluzbena: true })).toBe(350);
    expect(bodovi("dugotrajni", { dijete: true, proracun: true })).toBe(350);
  });
});
