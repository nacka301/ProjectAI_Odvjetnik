/**
 * Demo troškovnik prolazi kroz pravi motor.
 *
 * Zadatak je izričit: u podacima za primjer nema izračunatih iznosa. Ovo je
 * druga polovica tog pravila — dokaz da ih netko doista računa, i da rezultat
 * odgovara Tarifi kad se izračuna ručno. Pukne li motor, vodič dobrodošlice
 * odvjetniku pokazuje krive brojke, a to je gore nego da vodiča nema.
 *
 * Katalog se ovdje slaže iz definicija koje stoje u `TarifaKatalog.cs` za te
 * četiri stavke. To je namjerno DVOSTRUKI zapis: kad netko promijeni postotak u
 * katalogu a ne ovdje, test pukne i natjera ga da pogleda što se dogodilo s
 * primjerom koji svaki novi korisnik vidi prvi.
 */
import { describe, expect, it } from "vitest";
import { demoZbrojevi } from "@/lib/demo/izracun";
import { DEMO_TROSKOVNIK } from "@/lib/demo/podaci";
import {
  BOD_VALUE,
  obracunajPristojbe,
  tbr71Bodovi,
  type TarifnaStavka,
} from "@/lib/tarifa";

const prazna = {
  kategorija: "Parnični postupak",
  fiksni_body: null,
  tbr71_povecanje: null,
  max_bodova: null,
  min_bodova: null,
  jedinica: "komad",
  sort_order: 0,
  kazneni_base: false,
  kazneni_povecanje: null,
  prekrsaj_oduzimanje: false,
};

const KATALOG: TarifnaStavka[] = [
  { ...prazna, id: "par_tuzba", opis: "Tužba", tarifni_broj: "Tbr. 7.1", tbr71_postotak: 1.0 },
  { ...prazna, id: "par_rociste_procesno", opis: "Ročište — procesna pitanja", tarifni_broj: "Tbr. 9.2", tbr71_postotak: 0.5 },
  { ...prazna, id: "par_rociste_glavno", opis: "Ročište — glavna stvar", tarifni_broj: "Tbr. 9.1", tbr71_postotak: 1.0 },
  { ...prazna, id: "par_obrazlozeni_podnesak", opis: "Obrazloženi podnesak", tarifni_broj: "Tbr. 8.3", tbr71_postotak: 0.5, max_bodova: 100 },
];

describe("iznosi demo troškovnika", () => {
  it("slažu se s ručnim izračunom po Tarifi", () => {
    const z = demoZbrojevi(KATALOG);
    expect(z).not.toBeNull();

    // Osnovica po Tbr. 7. toč. 1 za VPS od 8.000,00 €.
    const osnovica = tbr71Bodovi(DEMO_TROSKOVNIK.vrijednostSpora);

    // Tužba 100% + pripremno ročište 50% + dva glavna ročišta 100% + podnesak 50%
    // (Tbr. 8.3 je ograničen na 100 bodova).
    const ocekivano =
      osnovica + osnovica * 0.5 + osnovica * 2 + Math.min(osnovica * 0.5, 100);

    expect(z!.ukupnoBodova).toBeCloseTo(ocekivano, 3);
    expect(z!.nagradaBezPdv).toBeCloseTo(ocekivano * BOD_VALUE, 2);
    expect(z!.pdv).toBeCloseTo(z!.nagradaBezPdv * 0.25, 2);
    expect(z!.nagradaSPdv).toBeCloseTo(z!.nagradaBezPdv + z!.pdv, 2);
  });

  it("naplaćuje pristojbu i na tužbu i na presudu", () => {
    const z = demoZbrojevi(KATALOG)!;
    // Tužba dolazi iz odabrane stavke, presuda iz ručno imenovane odredbe.
    // Nula bi značila da je jedan od ta dva puta prestao raditi.
    expect(z.pristojbe).toBeGreaterThan(0);
  });

  it("elektronički podnesak spušta pristojbu na tužbu", () => {
    // Čl. 7 st. 1 Zakona o sudskim pristojbama — vodič to tvrdi u koraku 5/8, pa
    // mora biti vidljivo i u brojci. Demo troškovnik nosi tužbu predanu
    // elektronički kao popis ključeva odredaba (kao `odlukeElektronicki`).
    expect(DEMO_TROSKOVNIK.podnesciElektronicki).toContain("tbr1_st1");

    const sElektronickim = demoZbrojevi(KATALOG)!.pristojbe;
    expect(sElektronickim).toBeGreaterThan(0);

    // Isti ulaz za pristojbe, jednom s čl. 7 st. 1 i jednom bez — bez mora biti skuplje.
    const tuzba = KATALOG.find((s) => s.id === "par_tuzba")!;
    const ulaz = {
      vrijednostSpora: DEMO_TROSKOVNIK.vrijednostSpora,
      odabraneStavke: [tuzba],
      oslobodjen: false,
      dodane: [...DEMO_TROSKOVNIK.pristojbeDodane],
    };
    const sCl7 = obracunajPristojbe({ ...ulaz, podnesciElektronicki: ["tbr1_st1"] }).ukupno;
    const bezCl7 = obracunajPristojbe({ ...ulaz, podnesciElektronicki: [] }).ukupno;
    expect(sCl7).toBeLessThan(bezCl7);
  });

  it("bez kataloga ne izmišlja brojku", () => {
    // Katalog stiže s mreže. Dok ga nema, jedini pošten odgovor je „ne znam".
    expect(demoZbrojevi([])).toBeNull();
  });
});
