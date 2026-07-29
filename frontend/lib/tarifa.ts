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
  prekrsaj_oduzimanje: boolean;
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

export function izracunajBodove(def: TarifnaStavka, baseBodovi: number): number {
  let bodovi: number;

  if (def.fiksni_body !== null && def.fiksni_body !== undefined) {
    bodovi = def.fiksni_body;
  } else if (def.tbr71_postotak !== null && def.tbr71_postotak !== undefined) {
    let raw = Math.ceil(baseBodovi * def.tbr71_postotak);
    if (def.tbr71_povecanje !== null && def.tbr71_povecanje !== undefined) {
      raw = Math.ceil(raw * (1 + def.tbr71_povecanje));
    }
    bodovi = raw;
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
): number {
  let bodovi = tierBodovi;

  // Povišenje/umanjenje vezano uz konkretni tarifni broj (npr. žalba +50%).
  if (def.kazneni_povecanje !== null && def.kazneni_povecanje !== undefined) {
    bodovi = Math.ceil(bodovi * (1 + def.kazneni_povecanje));
  }

  // Opunomoćenik djeteta žrtve: 70% od Tbr. 4. toč. 8, a toč. 8 je 50% Tbr. 4 → 35%.
  if (isZastupnikDjeteta) {
    bodovi = Math.ceil(bodovi * 0.5 * 0.7);
  }

  // Branitelj po službenoj dužnosti / na teret proračuna → 50%.
  if (isPoSluzbenojDuznosti || isNaTeretProracuna) {
    bodovi = Math.ceil(bodovi * 0.5);
  }

  return clampBodovi(bodovi, def);
}

// ── Sudske pristojbe (Uredba NN 37/2023) ─────────────────────────

const PRISTOJBA_BRACKETS: [number, number][] = [
  [398.17, 13.27],
  [796.34, 26.54],
  [1_327.23, 39.82],
  [1_990.84, 53.09],
  [3_318.07, 79.63],
  [5_308.91, 119.45],
  [7_963.37, 172.54],
  [13_272.28, 265.45],
  [26_544.56, 398.17],
  [39_816.84, 530.89],
  [53_089.12, 663.61],
  [66_361.4, 796.34],
  [99_542.1, 929.06],
  [132_722.81, 1_061.78],
  [165_903.51, 1_194.51],
  [199_084.21, 1_327.23],
  [265_445.61, 1_592.67],
  [331_807.02, 1_858.12],
  [398_168.42, 2_123.56],
  [464_529.82, 2_389.01],
  [530_891.22, 2_654.46],
  [663_614.03, 2_919.9],
  [796_336.83, 3_185.35],
  [929_059.64, 3_450.79],
  [1_061_782.44, 3_716.24],
  [1_327_228.05, 3_981.68],
];
const PRISTOJBA_MAX = 3_981.68;

export function izracunajSudskuPristojbu(vps: number): number {
  if (vps <= 0) return 0;
  for (const [maxVps, fee] of PRISTOJBA_BRACKETS) {
    if (vps <= maxVps) return fee;
  }
  return PRISTOJBA_MAX;
}
