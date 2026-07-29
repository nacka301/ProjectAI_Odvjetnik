/**
 * Klijentski izračun odvjetničke tarife i sudskih pristojbi.
 *
 * Ovo je zrcalo backend motora (`Backend/Services/TroskovnikService.cs` i
 * `Backend/Services/SudskePristojbeCalculator.cs`) i postoji samo zato da živi
 * pregled troškovnika bude trenutan, bez mrežnog poziva po svakoj promjeni količine.
 *
 * MJERODAVAN JE BACKEND. Ako se ova dva motora raziđu, `__tests__/tarifa.test.ts`
 * pada — ista tablica očekivanih vrijednosti vrti se i u xunit testovima.
 *
 * Izvori:
 *  - Tarifa o nagradama i naknadi troškova za rad odvjetnika
 *    (Upravni odbor HOK-a, 14. listopada 2023.; nesl. proč. tekst listopad 2025.)
 *  - Uredba o tarifi sudskih pristojbi, NN 37/2023
 */

/** Tbr. 54 — vrijednost boda. */
export const BOD_VALUE = 2.0;

/** Zakon o PDV-u — opća stopa. */
export const PDV_RATE = 0.25;

/**
 * Branitelj po službenoj dužnosti ili na teret proračunskih sredstava — 50% tarife.
 *
 * Tbr. 4. toč. 7 ne određuje iznos nego upućuje na "Pravilnik ministarstva nadležnog
 * za poslove pravosuđa".
 * Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271.
 * https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
 */
export const KAZNENO_UMANJENJE_STOPA = 0.5;

/**
 * Tbr. 4. toč. 8: odvjetniku koji zastupa oštećenika pripada 50% nagrade iz Tbr. 4.
 */
export const TBR_4_8_STOPA = 0.5;

/**
 * Opunomoćenik djeteta žrtve kaznenog djela — 70% od nagrade iz Tbr. 4. toč. 8.
 *
 * Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271.
 * https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
 *
 * Efektivno je to 0,50 × 0,70 = 35% pune tarife, ne 70%.
 */
export const ZASTUPNIK_DJETETA_STOPA = 0.7;

// ── Tipovi (zrcale backend modele) ───────────────────────────────

export interface TarifnaStavka {
  id: string;
  kategorija: string;
  opis: string;
  tarifni_broj: string;
  fiksni_body: number | null;
  tbr71_postotak: number | null;
  tbr71_povecanje: number | null;
  max_bodova: number | null;
  min_bodova: number | null;
  jedinica: string;
  sort_order: number;
  kazneni_base: boolean;
  kazneni_povecanje: number | null;
  /** Tbr. 4. toč. 2 al. 3 — povišenje koje vrijedi umjesto redovnog kod dugotrajnog zatvora. */
  kazneni_povecanje_dugotrajni?: number | null;
  prekrsaj_oduzimanje: boolean;
  /** Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1 — jedna nagrada za cijeli postupak. */
  jednokratna_nagrada?: boolean;
  /** Koliko je radnji obuhvaćeno osnovicom ("najviše do četiri radnje"). */
  ukljucene_radnje?: number | null;
  /** Bodovi za svaku radnju preko uključenog broja ("Za svaku daljnju radnju... 50 bodova"). */
  daljnja_radnja_bodovi?: number | null;
}

// ── Vrsta postupka ───────────────────────────────────────────────

/** Naziv kategorije kaznenog i prekršajnog postupka u katalogu. */
export const KATEGORIJA_KAZNENI = "Kazneni postupak";

/**
 * True kad su odabrane isključivo kaznene/prekršajne stavke.
 *
 * U kaznenom se postupku sve veže na zapriječenu kaznu (Tbr. 4. toč. 1), nikad
 * na vrijednost predmeta spora, pa se VPS izbacuje iz izračuna. Uredba o tarifi
 * sudskih pristojbi (NN 37/2023) u Poglavlju I. pokriva samo parnični i ovršni
 * postupak, pa se ni pristojba ne obračunava.
 */
export function jeSamoKazneni(odabraneStavke: TarifnaStavka[]): boolean {
  return (
    odabraneStavke.length > 0 &&
    odabraneStavke.every((s) => s.kategorija === KATEGORIJA_KAZNENI)
  );
}

// ── Tbr. 7. toč. 1 — osnovna tablica bodova po VPS-u ─────────────

/**
 * Tbr. 7. toč. 1 — broj bodova prema vrijednosti predmeta spora (EUR).
 *
 * Fiksni razredi doslovno iz tablice u Tbr. 7. toč. 1, progresivni dio iz
 * odlomka ispod tablice: "nagradu od 1 boda za svakih započetih 133,00 eura"
 * (odnosno 265,00 i 664,00 eura), "ali ne više od 10000 bodova".
 *
 * `Math.ceil` ovdje NIJE aritmetičko zaokruživanje nego "za svakih ZAPOČETIH"
 * iz teksta Tarife — zato preživljava pravilo da se bodovi inače ne zaokružuju.
 */
export function tbr71Bodovi(vrijednost: number): number {
  if (vrijednost <= 0) return 25;

  // Fiksni razredi (Tbr. 7. toč. 1, tablica)
  if (vrijednost <= 332.0) return 25;
  if (vrijednost <= 664.0) return 50;
  if (vrijednost <= 1327.0) return 75;
  if (vrijednost <= 13272.0) return 100;
  if (vrijednost <= 33181.0) return 250;
  if (vrijednost <= 66361.0) return 500;

  // Progresivni razredi
  if (vrijednost <= 663614.0) {
    const extra = Math.ceil((vrijednost - 66361.0) / 133.0);
    return 500 + extra;
  }
  if (vrijednost <= 1327228.0) {
    const extra = Math.ceil((vrijednost - 663614.0) / 265.0);
    return 4991 + extra;
  }
  const extra = Math.ceil((vrijednost - 1327228.0) / 664.0);
  return Math.min(7496 + extra, 10000);
}

// ── Kazneni tier (Tbr. 4. toč. 1) ────────────────────────────────

/**
 * Tbr. 4. toč. 1 — bodovi za prvi sat prema zapriječenoj kazni.
 * Sve alineje doslovno iz Tarife.
 */
/** Ključ tiera za kaznu dugotrajnog zatvora (Tbr. 4. toč. 1 al. 5). */
export const KAZNA_DUGOTRAJNI_ZATVOR = "dugotrajni";

export const ZAPRIJECENA_KAZNA_TIERS: Record<string, { label: string; bodovi: number }> = {
  novcana_do3: { label: "Novčana kazna ili kazna do 3 godine zatvora", bodovi: 100 },
  do_5: { label: "Do 5 godina zatvora", bodovi: 200 },
  do_10: { label: "Do 10 godina zatvora", bodovi: 300 },
  preko_10: { label: "Preko 10 godina zatvora", bodovi: 400 },
  dugotrajni: { label: "Dugotrajni zatvor", bodovi: 500 },
};

// ── Izračun bodova po stavci ─────────────────────────────────────

function clampBodovi(bodovi: number, def: TarifnaStavka): number {
  if (def.max_bodova !== null && def.max_bodova !== undefined && bodovi > def.max_bodova)
    bodovi = def.max_bodova;
  if (def.min_bodova !== null && def.min_bodova !== undefined && bodovi < def.min_bodova)
    bodovi = def.min_bodova;
  return bodovi;
}

/**
 * Lanac se računa bez zaokruživanja između koraka. Tarifa zaokruživanje bodova
 * nigdje ne propisuje, a Math.ceil na svakom koraku sustavno je podizao iznose:
 * 75 × 0,5 = 37,5 → 38, pa 38 × 1,25 = 47,5 → 48, iako je točno 46,875.
 */
export function izracunajBodove(def: TarifnaStavka, baseBodovi: number): number {
  let bodovi: number;

  if (def.fiksni_body !== null && def.fiksni_body !== undefined) {
    bodovi = def.fiksni_body;
  } else if (def.tbr71_postotak !== null && def.tbr71_postotak !== undefined) {
    bodovi = baseBodovi * def.tbr71_postotak;
    if (def.tbr71_povecanje !== null && def.tbr71_povecanje !== undefined) {
      bodovi *= 1 + def.tbr71_povecanje;
    }
  } else {
    bodovi = baseBodovi;
  }

  return clampBodovi(bodovi, def);
}

export function izracunajKazneneBodove(
  def: TarifnaStavka,
  tierBodovi: number,
  isZastupnikDjeteta: boolean,
  isPoSluzbenojDuznosti: boolean,
  isNaTeretProracuna: boolean,
  zaprijecenaKazna?: string,
): number {
  let bodovi = tierBodovi;

  // Povišenje/umanjenje vezano uz konkretni tarifni broj (npr. žalba +50%).
  //
  // Tbr. 4. toč. 2 al. 3 propisuje drukčiji postotak za raspravu pred drugim i
  // trećim stupnjem kad je zapriječena kazna dugotrajni zatvor (50% umjesto 25%).
  const jeDugotrajni = zaprijecenaKazna === KAZNA_DUGOTRAJNI_ZATVOR;
  const povecanje =
    jeDugotrajni &&
    def.kazneni_povecanje_dugotrajni !== null &&
    def.kazneni_povecanje_dugotrajni !== undefined
      ? def.kazneni_povecanje_dugotrajni
      : def.kazneni_povecanje;

  if (povecanje !== null && povecanje !== undefined) {
    bodovi *= 1 + povecanje;
  }

  // Opunomoćenik djeteta žrtve kaznenog djela: 70% od nagrade iz Tbr. 4. toč. 8,
  // a toč. 8 je već 50% nagrade iz Tbr. 4 → efektivno 35% pune tarife, ne 70%.
  if (isZastupnikDjeteta) {
    bodovi *= TBR_4_8_STOPA * ZASTUPNIK_DJETETA_STOPA;
  }

  // Branitelj po službenoj dužnosti / na teret proračunskih sredstava → 50%.
  if (isPoSluzbenojDuznosti || isNaTeretProracuna) {
    bodovi *= KAZNENO_UMANJENJE_STOPA;
  }

  return clampBodovi(bodovi, def);
}

// ── Novac ────────────────────────────────────────────────────────

/**
 * Jedino zaokruživanje u izračunu: novac, dvije decimale, standardno (od nule).
 * Bodovi ostaju decimalni.
 */
export function zaokruziNovac(iznos: number): number {
  return Math.round((iznos + Number.EPSILON) * 100) / 100;
}

/**
 * Prikaz bodova: cijeli broj bez decimala, decimalni s najviše tri
 * (100 → "100", 46,875 → "46,875").
 */
export function formatirajBodove(bodovi: number): string {
  return bodovi.toLocaleString("hr-HR", { maximumFractionDigits: 3 });
}

// ── Tbr. 40. toč. 1 — zastupanje više osoba ──────────────────────

/** 10% za drugu i svaku sljedeću osobu. */
export const VISE_OSOBA_PO_OSOBI = 0.1;

/** "uz uvjet da to povišenje može iznositi najviše 50%" */
export const VISE_OSOBA_MAX_POVISENJE = 0.5;

/**
 * Tbr. 40. toč. 1: "Kad odvjetnik u istom postupku brani ili zastupa više osoba,
 * pripada mu pravo na povišenje tarifnih stavaka za drugu i svaku sljedeću osobu
 * za 10% — uz uvjet da to povišenje može iznositi najviše 50%."
 *
 * Prva osoba ne ulazi u postotak. Povišenje ide na cijelu osnovicu troškovnika.
 */
export function povisenjeViseOsobaPostotak(brojOsoba: number): number {
  if (!Number.isFinite(brojOsoba) || brojOsoba <= 1) return 0;

  return Math.min((brojOsoba - 1) * VISE_OSOBA_PO_OSOBI, VISE_OSOBA_MAX_POVISENJE);
}

// ── Ukupno bodova za jednu stavku troškovnika ────────────────────

/**
 * Za obične stavke: osnovica × količina.
 *
 * Za jednokratne nagrade (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) osnovica pokriva
 * cijeli prvostupanjski postupak i NE množi se količinom; dodaje se samo naknada
 * za radnje preko uključenog broja.
 */
export function ukupnoBodovaZaStavku(
  def: TarifnaStavka,
  bodovi: number,
  kolicina: number,
): number {
  if (!def.jednokratna_nagrada) return bodovi * kolicina;

  let ukupno = bodovi;

  if (
    def.ukljucene_radnje !== null &&
    def.ukljucene_radnje !== undefined &&
    def.daljnja_radnja_bodovi !== null &&
    def.daljnja_radnja_bodovi !== undefined
  ) {
    const daljnjeRadnje = Math.max(0, kolicina - def.ukljucene_radnje);
    ukupno += daljnjeRadnje * def.daljnja_radnja_bodovi;
  }

  return ukupno;
}

// ── Sudske pristojbe (Uredba NN 37/2023) ─────────────────────────

/**
 * Osnova za obračun sudske pristojbe.
 *
 * Prilog Uredbe, POGLAVLJE I. nosi naslov "PARNIČNI I OVRŠNI POSTUPAK" — izvan
 * tog poglavlja Tar. br. 1 se ne primjenjuje, pa kazneni predmet dobiva "Nema".
 */
export type PristojbaOsnova = "Puna" | "Polovica" | "Nema";

/** Objašnjenje koje sučelje prikazuje uz iznos pristojbe. */
export function objasnjenjePristojbe(osnova: PristojbaOsnova): string {
  switch (osnova) {
    case "Nema":
      return "Uredba NN 37/23 ne propisuje pristojbu za kazneni postupak (Poglavlje I. – parnični i ovršni postupak).";
    case "Polovica":
      return "Tar. br. 1 st. 2 – polovica iznosa iz st. 1.";
    default:
      return "Tar. br. 1 st. 1.";
  }
}

/** Naziv kategorije parničnog postupka u katalogu. */
export const KATEGORIJA_PARNICNI = "Parnični postupak";

/** Naziv kategorije ovršnog postupka u katalogu. */
export const KATEGORIJA_OVRSNI = "Ovršni postupak";

/**
 * Vrsta postupka → osnova za pristojbu. Zrcali
 * `TroskovnikService.OdrediPristojbuOsnovu`.
 */
export function odrediPristojbuOsnovu(odabraneStavke: TarifnaStavka[]): PristojbaOsnova {
  if (odabraneStavke.length === 0) return "Puna";
  if (jeSamoKazneni(odabraneStavke)) return "Nema";

  const imaOvrsnih = odabraneStavke.some((s) => s.kategorija === KATEGORIJA_OVRSNI);
  const imaParnicnih = odabraneStavke.some((s) => s.kategorija === KATEGORIJA_PARNICNI);

  if (imaOvrsnih && !imaParnicnih) return "Polovica";
  return "Puna";
}

// Tar. br. 1 st. 1 — pet fiksnih razreda, doslovno iz tablice u NN 37/2023:
//
//   iznad        do eura      eura
//   0,00         398,17       13,27
//   398,18       796,34       26,54
//   796,35       1.194,51     39,82
//   1.194,52     1.592,67     53,09
//   1.592,68     1.990,84     66,36
const PRISTOJBA_BRACKETS: [number, number][] = [
  [398.17, 13.27],
  [796.34, 26.54],
  [1_194.51, 39.82],
  [1_592.67, 53.09],
  [1_990.84, 66.36],
];

// "Preko 1.990,84 eura plaća se pristojba u iznosu od 66,36 eura i još 1% na
//  razliku iznad 1.990,84 eura, ali ne više od 663,61 euro."
const PRISTOJBA_PRAG = 1_990.84;
const PRISTOJBA_BAZA = 66.36;
const PRISTOJBA_STOPA = 0.01;
const PRISTOJBA_MAX = 663.61;

/** Nezaokruženi iznos po Tar. br. 1 st. 1 — da se zaokružuje samo jednom, na kraju. */
function pristojbaNezaokruzeno(vps: number): number {
  for (const [maxVps, fee] of PRISTOJBA_BRACKETS) {
    if (vps <= maxVps) return fee;
  }
  return Math.min(PRISTOJBA_BAZA + PRISTOJBA_STOPA * (vps - PRISTOJBA_PRAG), PRISTOJBA_MAX);
}

export function izracunajSudskuPristojbu(
  vps: number,
  osnova: PristojbaOsnova = "Puna",
): number {
  if (osnova === "Nema") return 0;
  if (vps <= 0) return 0;

  let iznos = pristojbaNezaokruzeno(vps);

  // Tar. br. 1 st. 2 — polovica pristojbe iz st. 1.
  if (osnova === "Polovica") iznos *= 0.5;

  // Zaokruživanje "od nule", ne bankarsko: 38,2258 → 38,23.
  return Math.round((iznos + Number.EPSILON) * 100) / 100;
}
