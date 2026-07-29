import { describe, expect, it } from "vitest";
import { KATEGORIJA_KAZNENI, jeSamoKazneni, type TarifnaStavka } from "@/lib/tarifa";

/**
 * Paritet s Backend.Tests/TarifaKazneniTests.cs.
 */

function stavka(kategorija: string, id: string): TarifnaStavka {
  return {
    id,
    kategorija,
    opis: "",
    tarifni_broj: "",
    fiksni_body: null,
    tbr71_postotak: null,
    tbr71_povecanje: null,
    max_bodova: null,
    min_bodova: null,
    jedinica: "komad",
    sort_order: 0,
    kazneni_base: false,
    kazneni_povecanje: null,
    prekrsaj_oduzimanje: false,
  };
}

const kazneni = stavka(KATEGORIJA_KAZNENI, "kaz_obrana");
const parnicni = stavka("Parnični postupak", "par_tuzba");

describe("jeSamoKazneni", () => {
  it("prazan odabir nije kazneni predmet", () => {
    expect(jeSamoKazneni([])).toBe(false);
  });

  it("isključivo kaznene stavke → true", () => {
    expect(jeSamoKazneni([kazneni, stavka(KATEGORIJA_KAZNENI, "kaz_zalba_presuda")])).toBe(true);
  });

  it("mješoviti troškovnik → false, VPS ostaje u igri", () => {
    expect(jeSamoKazneni([kazneni, parnicni])).toBe(false);
  });
});
