/**
 * `naslutiTipStranke` — prijedlog „fizička/pravna" iz naziva stranke.
 *
 * Koristi se pri automatskom otvaranju nove stranke iz troškovnika: bez ovoga je
 * backend svaku takvu stranku stvarao kao „fizička", pa su d.o.o./d.d. klijenti
 * završavali krivo označeni. Prijedlog, ne tvrdnja — korisnik ga u sučelju mijenja.
 */
import { describe, expect, it } from "vitest";
import { naslutiTipStranke } from "@/lib/stranke-tipovi";

describe("naslutiTipStranke", () => {
  it.each([
    "Adriatic Shipping & Logistics d.d.",
    "Elektro Instalacije Zagreb d.o.o.",
    "Zagreb Trade d. o. o.",
    "Mini Servis j.d.o.o.",
    "Poljoprivredna zadruga Slavonija",
    "Udruga oštećenih ulagača Pravda",
    "Konzorcij Autocesta Dalmatina d.o.o.",
    "Hrvatska odvjetnička komora",
    "Republika Hrvatska",
  ])("prepoznaje pravnu osobu: %s", (naziv) => {
    expect(naslutiTipStranke(naziv)).toBe("pravna");
  });

  it.each([
    "Ivan Horvat",
    "Marija Kovač",
    "Ranko Vuković",
    "",
    null,
    undefined,
  ])("ostaje fizička bez pouzdane oznake: %s", (naziv) => {
    expect(naslutiTipStranke(naziv)).toBe("fizicka");
  });
});
