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
 * Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, čl. 1.
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
 * Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, čl. 2.
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
  /** Propis i tarifni broj iz kojeg nagrada proizlazi — prikazuje se uz stavku i uz redak. */
  pravni_izvor?: string;
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

// ── Tbr. 7. toč. 1 — tablica za prikaz ───────────────────────────

export interface RazredZaPrikaz {
  /** Donja granica razreda; null za "iznad …". */
  od: number | null;
  /** Gornja granica razreda; null za posljednji, neograničeni razred. */
  do: number | null;
  /** Bodovi ili opis progresivnog obračuna, doslovno po tekstu Tarife. */
  bodovi: string;
}

/**
 * Tablica iz Tbr. 7. toč. 1 onako kako stoji u Tarifi — s decimalama i bez
 * preklapanja granica: 0–332,00 pa 332,01–664,00.
 *
 * Progresivni razredi nemaju jedan broj nego formulu, pa se ispisuju tekstom
 * kojim ih Tarifa i opisuje.
 */
export const TBR_7_1_RAZREDI: RazredZaPrikaz[] = [
  { od: 0, do: 332.0, bodovi: "25" },
  { od: 332.01, do: 664.0, bodovi: "50" },
  { od: 664.01, do: 1_327.0, bodovi: "75" },
  { od: 1_327.01, do: 13_272.0, bodovi: "100" },
  { od: 13_272.01, do: 33_181.0, bodovi: "250" },
  { od: 33_181.01, do: 66_361.0, bodovi: "500" },
  { od: 66_361.01, do: 663_614.0, bodovi: "500 + 1 bod / započetih 133,00 €" },
  { od: 663_614.01, do: 1_327_228.0, bodovi: "4.991 + 1 bod / započetih 265,00 €" },
  { od: 1_327_228.01, do: null, bodovi: "7.496 + 1 bod / započetih 664,00 €, najviše 10.000" },
];

// ── Osnovica: "Tbr. 7. toč. 1., 4., 7. i 8." ─────────────────────
//
// Tarifa na devet mjesta upućuje na "nagradu iz Tbr. 7. toč. 1., 4., 7. i 8."
// To nisu četiri iznosa nego četiri VARIJANTE iste osnovice; koja se primjenjuje
// ovisi o vrsti postupka. Zrcali `TroskovnikService.IzracunajOsnovicu`.

export const OSNOVICA_TBR7 = {
  /** Tbr. 7. toč. 1 — tablica vrijednost spora → bodovi. Zadano. */
  Toc1: "toc1",
  /** Tbr. 7. toč. 4 al. 1 — autorsko / industrijsko vlasništvo: po toč. 1, ali ne manje od 100. */
  Toc4Autorsko: "toc4_autorsko",
  /** Tbr. 7. toč. 4 al. 2 — renta / zakup / najam: kao VPS uzima se petogodišnji iznos. */
  Toc4Renta: "toc4_renta",
  /** Tbr. 7. toč. 7 st. 1 — domaća arbitraža. */
  Toc7DomacaArbitraza: "toc7_domaca_arbitraza",
  /** Tbr. 7. toč. 7 st. 2 — međunarodna arbitraža: povišenje od 100 %. */
  Toc7MedunarodnaArbitraza: "toc7_medunarodna_arbitraza",
  /** Tbr. 7. toč. 8 — mirno rješavanje sporova. */
  Toc8Mirenje: "toc8_mirenje",
  /** Tbr. 45 — neprocjenjivi predmeti: 100 / 200 / 500 prema razini suda. */
  Tbr45Neprocjenjiv: "tbr45_neprocjenjiv",
} as const;

export type OsnovicaTbr7 = (typeof OSNOVICA_TBR7)[keyof typeof OSNOVICA_TBR7];

/** Nazivi za padajući izbornik, s tarifnim brojem iz kojeg proizlaze. */
export const OSNOVICA_LABELE: Record<OsnovicaTbr7, string> = {
  [OSNOVICA_TBR7.Toc1]: "Tbr. 7. toč. 1 — prema vrijednosti predmeta spora",
  [OSNOVICA_TBR7.Toc4Autorsko]: "Tbr. 7. toč. 4 al. 1 — autorsko pravo i industrijsko vlasništvo (ne manje od 100 bodova)",
  [OSNOVICA_TBR7.Toc4Renta]: "Tbr. 7. toč. 4 al. 2 — renta / zakup / najam (petogodišnji iznos)",
  [OSNOVICA_TBR7.Toc7DomacaArbitraza]: "Tbr. 7. toč. 7 st. 1 — domaća arbitraža",
  [OSNOVICA_TBR7.Toc7MedunarodnaArbitraza]: "Tbr. 7. toč. 7 st. 2 — međunarodna arbitraža (+100 %)",
  [OSNOVICA_TBR7.Toc8Mirenje]: "Tbr. 7. toč. 8 — mirno rješavanje sporova",
  [OSNOVICA_TBR7.Tbr45Neprocjenjiv]: "Tbr. 45 — neprocjenjiv predmet koji Tarifa ne predviđa",
};

/** Tbr. 45. toč. 1–3 — bodovi po razini suda, doslovno iz Tarife. */
export const RAZINA_SUDA = {
  Opcinski: "opcinski",
  ZupanijskiTrgovacki: "zupanijski_trgovacki",
  Vrhovni: "vrhovni",
} as const;

export type RazinaSuda = (typeof RAZINA_SUDA)[keyof typeof RAZINA_SUDA];

export const RAZINA_SUDA_BODOVI: Record<RazinaSuda, number> = {
  [RAZINA_SUDA.Opcinski]: 100,
  [RAZINA_SUDA.ZupanijskiTrgovacki]: 200,
  [RAZINA_SUDA.Vrhovni]: 500,
};

export const RAZINA_SUDA_LABELE: Record<RazinaSuda, string> = {
  [RAZINA_SUDA.Opcinski]: "Općinski sud i tijela iste razine — 100 bodova (toč. 1)",
  [RAZINA_SUDA.ZupanijskiTrgovacki]: "Županijski i trgovački sudovi te tijela iste razine — 200 bodova (toč. 2)",
  [RAZINA_SUDA.Vrhovni]: "Vrhovni sud RH i tijela iste razine — 500 bodova (toč. 3)",
};

/**
 * Tarifa ne kaže primjenjuju li se postoci iz Tbr. 8 i 9 na iznos iz Tbr. 45.
 * Oba su čitanja implementirana; odabir je obavezan, ništa se ne pretpostavlja.
 */
export const TBR45_NACIN = {
  KaoOsnovica: "kao_osnovica",
  FiksnoPoRadnji: "fiksno_po_radnji",
} as const;

export type Tbr45Nacin = (typeof TBR45_NACIN)[keyof typeof TBR45_NACIN];

export const TBR45_NACIN_LABELE: Record<Tbr45Nacin, string> = {
  [TBR45_NACIN.KaoOsnovica]: "Kao osnovica — postoci iz Tbr. 8. i 9. primjenjuju se na 100/200/500",
  [TBR45_NACIN.FiksnoPoRadnji]: "Fiksno po radnji — svaka radnja nosi puni iznos, bez postotaka i stropova",
};

export const RENTA_RAZDOBLJE = {
  Mjesecno: "mjesecno",
  Godisnje: "godisnje",
} as const;

export type RentaRazdoblje = (typeof RENTA_RAZDOBLJE)[keyof typeof RENTA_RAZDOBLJE];

export interface OsnovicaUlaz {
  osnovicaTbr7: OsnovicaTbr7;
  vrijednostSpora: number;
  razinaSuda?: RazinaSuda | null;
  tbr45Nacin?: Tbr45Nacin | null;
  rentaIznos?: number;
  rentaRazdoblje?: RentaRazdoblje | null;
}

export interface OsnovicaRezultat {
  bodovi: number;
  pravniIzvor: string;
  /** Tbr. 45 uz "fiksno po radnji" — postoci iz Tbr. 8./9. se ne primjenjuju. */
  fiksnoPoRadnji: boolean;
}

const NAZIV_TARIFE =
  "Tarifa o nagradama i naknadi troškova za rad odvjetnika (Hrvatska odvjetnička komora, 14. listopada 2023.)";

/**
 * Tbr. 7. toč. 4 al. 2: "za utvrđivanje rente, zakupa, najma i u drugim sličnim
 * postupcima kao vrijednost spora uzima se petogodišnji iznos rente, zakupa, najma
 * i drugih sličnih davanja."
 *
 * Preračun se radi samo uz osnovicu `toc4_renta` i uz izričito odabrano razdoblje —
 * ništa se ne izvodi tiho.
 */
export function efektivniVps(ulaz: OsnovicaUlaz): number {
  if (ulaz.osnovicaTbr7 !== OSNOVICA_TBR7.Toc4Renta || !ulaz.rentaIznos || ulaz.rentaIznos <= 0) {
    return ulaz.vrijednostSpora;
  }

  if (ulaz.rentaRazdoblje === RENTA_RAZDOBLJE.Mjesecno) return ulaz.rentaIznos * 60; // 5 god. × 12 mj.
  if (ulaz.rentaRazdoblje === RENTA_RAZDOBLJE.Godisnje) return ulaz.rentaIznos * 5;

  return ulaz.vrijednostSpora; // razdoblje nije odabrano → bez preračuna
}

/**
 * Razrješava koja se varijanta osnovice primjenjuje.
 * Vraća `null` kad Tbr. 45 nema odabranu razinu suda ili način — tada `provjeriZahtjev`
 * vraća poruku, a sučelje ne prikazuje iznos.
 */
export function izracunajOsnovicu(ulaz: OsnovicaUlaz): OsnovicaRezultat | null {
  const vps = efektivniVps(ulaz);

  switch (ulaz.osnovicaTbr7) {
    // "nagrada po Tbr. 7. toč. 1., ali ne manje od 100 bodova"
    case OSNOVICA_TBR7.Toc4Autorsko:
      return {
        bodovi: Math.max(tbr71Bodovi(vps), 100),
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 4 al. 1 — nagrada po Tbr. 7. toč. 1, ali ne manje od 100 bodova`,
        fiksnoPoRadnji: false,
      };

    case OSNOVICA_TBR7.Toc4Renta:
      return {
        bodovi: tbr71Bodovi(vps),
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 4 al. 2 — kao vrijednost spora uzima se petogodišnji iznos rente, zakupa ili najma`,
        fiksnoPoRadnji: false,
      };

    case OSNOVICA_TBR7.Toc7DomacaArbitraza:
      return {
        bodovi: tbr71Bodovi(vps),
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 7 st. 1 — nagrada po Tbr. 7. toč. 1`,
        fiksnoPoRadnji: false,
      };

    // "...pred međunarodnom arbitražom (...) po Tbr. 7. toč. 1. s povišenjem od 100%."
    case OSNOVICA_TBR7.Toc7MedunarodnaArbitraza:
      return {
        bodovi: tbr71Bodovi(vps) * 2,
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 7 st. 2 — Tbr. 7. toč. 1 s povišenjem od 100%`,
        fiksnoPoRadnji: false,
      };

    case OSNOVICA_TBR7.Toc8Mirenje:
      return {
        bodovi: tbr71Bodovi(vps),
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 8 — nagrada po Tbr. 7. toč. 1`,
        fiksnoPoRadnji: false,
      };

    case OSNOVICA_TBR7.Tbr45Neprocjenjiv: {
      if (!ulaz.razinaSuda || !(ulaz.razinaSuda in RAZINA_SUDA_BODOVI)) return null;
      if (!ulaz.tbr45Nacin) return null;

      const tocka =
        ulaz.razinaSuda === RAZINA_SUDA.Opcinski
          ? "Tbr. 45. toč. 1 — pred općinskim sudom i tijelima iste razine 100 bodova"
          : ulaz.razinaSuda === RAZINA_SUDA.ZupanijskiTrgovacki
            ? "Tbr. 45. toč. 2 — pred županijskim i trgovačkim sudovima te tijelima iste razine 200 bodova"
            : "Tbr. 45. toč. 3 — pred Vrhovnim sudom RH i tijelima iste razine 500 bodova";

      return {
        bodovi: RAZINA_SUDA_BODOVI[ulaz.razinaSuda],
        pravniIzvor: `${NAZIV_TARIFE}, ${tocka}`,
        fiksnoPoRadnji: ulaz.tbr45Nacin === TBR45_NACIN.FiksnoPoRadnji,
      };
    }

    default:
      return {
        bodovi: tbr71Bodovi(vps),
        pravniIzvor: `${NAZIV_TARIFE}, Tbr. 7. toč. 1 — nagrada prema vrijednosti predmeta spora`,
        fiksnoPoRadnji: false,
      };
  }
}

// ── Tbr. 41 — povisivanje i snižavanje tarifnih stavaka ──────────

/** Tbr. 41. toč. 1 — "tarifne stavke mogu se povisiti za 100%". */
export const TBR41_MAX_POVISENJE = 100;

/** Tbr. 41. toč. 2 — "pojedine tarifne stavke odvjetnik može sniziti za 50%". */
export const TBR41_MAX_SNIZENJE = 50;

/**
 * Neto udio: 0,25 znači +25 %, −0,10 znači −10 %. Oba su zadano 0, pa bez
 * korisnikova zahvata iznos ostaje nepromijenjen.
 */
export function tbr41Udio(povisenjePostotak: number, snizenjePostotak: number): number {
  const povisenje = Math.min(Math.max(povisenjePostotak || 0, 0), TBR41_MAX_POVISENJE) / 100;
  const snizenje = Math.min(Math.max(snizenjePostotak || 0, 0), TBR41_MAX_SNIZENJE) / 100;

  return povisenje - snizenje;
}

/** Postoci Tbr. 41 za jednu stavku troškovnika. */
export interface Tbr41Stavka {
  /** Toč. 1 — 0–100 %. Null znači „naslijedi postotak s razine troškovnika". */
  povisenje?: number | null;
  /** Toč. 2 — 0–50 %. Null znači isto. */
  snizenje?: number | null;
}

/**
 * Razrješava postotke Tbr. 41 za jednu stavku. Stavka bez vlastitog postotka
 * nasljeđuje onaj s razine troškovnika — to je prečac „primijeni na sve stavke".
 *
 * Zrcali `TroskovnikService.RazrijesiTbr41`.
 */
export function razrijesiTbr41(
  stavka: Tbr41Stavka,
  precacPovisenje: number,
  precacSnizenje: number,
): { povisenje: number; snizenje: number } {
  return {
    povisenje: stavka.povisenje ?? precacPovisenje,
    snizenje: stavka.snizenje ?? precacSnizenje,
  };
}

/**
 * Citat odredbe uz prilagođenu stavku; prazan string kad Tbr. 41 nije primijenjen.
 * Zrcali `TroskovnikService.Tbr41Izvor`.
 */
export function tbr41Izvor(povisenje: number, snizenje: number): string {
  if (povisenje > 0) {
    return (
      `${NAZIV_TARIFE}, Tbr. 41. toč. 1 — povišenje od ${povisenje} % za posebno stručno ` +
      `i specijalističko znanje, osobitu težinu predmeta ili posebnu odgovornost za obavljeni rad`
    );
  }

  if (snizenje > 0) {
    return (
      `${NAZIV_TARIFE}, Tbr. 41. toč. 2 — sniženje od ${snizenje} %; ` +
      `„pojedine tarifne stavke odvjetnik može sniziti za 50%"`
    );
  }

  return "";
}

/**
 * Toč. 1 i toč. 2 polaze od suprotnih razloga, pa ne mogu stajati na istoj stavci.
 * Vraća poruku ili null.
 */
export function provjeriTbr41Stavku(
  opisStavke: string,
  povisenje: number,
  snizenje: number,
): string | null {
  if (povisenje > 0 && snizenje > 0) {
    return (
      `Stavka „${opisStavke}" ima istovremeno povišenje (${povisenje} %) i sniženje ` +
      `(${snizenje} %) po Tbr. 41. Toč. 1 i toč. 2 polaze od suprotnih razloga, pa se na ` +
      `istu stavku ne mogu primijeniti istovremeno.`
    );
  }

  if (povisenje < 0 || povisenje > TBR41_MAX_POVISENJE) {
    return `Povišenje po Tbr. 41. toč. 1 za „${opisStavke}" iznosi ${povisenje} %. Tarifa dopušta najviše ${TBR41_MAX_POVISENJE} %.`;
  }

  if (snizenje < 0 || snizenje > TBR41_MAX_SNIZENJE) {
    return `Sniženje po Tbr. 41. toč. 2 za „${opisStavke}" iznosi ${snizenje} %. Tarifa dopušta najviše ${TBR41_MAX_SNIZENJE} %.`;
  }

  return null;
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
export function izracunajBodove(def: TarifnaStavka, osnovica: OsnovicaRezultat): number {
  let bodovi: number;

  if (def.fiksni_body !== null && def.fiksni_body !== undefined) {
    // Iznos koji Tarifa određuje apsolutno (npr. Tbr. 7. toč. 10 = 250 bodova) —
    // ne ovisi ni o vrijednosti spora ni o osnovici.
    bodovi = def.fiksni_body;
  } else if (osnovica.fiksnoPoRadnji) {
    // Tbr. 45 uz način "fiksno po radnji": svaka radnja nosi puni iznos iz toč. 1–3.
    // Postoci i stropovi iz Tbr. 8./9. se NE primjenjuju.
    return osnovica.bodovi;
  } else if (def.tbr71_postotak !== null && def.tbr71_postotak !== undefined) {
    bodovi = osnovica.bodovi * def.tbr71_postotak;
    if (def.tbr71_povecanje !== null && def.tbr71_povecanje !== undefined) {
      bodovi *= 1 + def.tbr71_povecanje;
    }
  } else {
    bodovi = osnovica.bodovi;
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

// ── Provjera zahtjeva ────────────────────────────────────────────

export interface ProvjeraUlaza {
  zaprijecenaKazna?: string;
  isOduzimanjePrekrsaj: boolean;
  vrijednostOduzimanja: number;
  osnovicaTbr7?: OsnovicaTbr7;
  razinaSuda?: RazinaSuda | null;
  tbr45Nacin?: Tbr45Nacin | null;
  /** Zakon o sudskim pristojbama, čl. 14–26. Izostavljeno u kaznenom troškovniku. */
  vps?: VpsUlaz;
  /** Zakon o sudskim pristojbama, čl. 11–13. */
  oslobodjenje?: OslobodjenjeUlaz;
}

/**
 * Vraća poruke o onome što nedostaje da se troškovnik može valjano izračunati.
 * Zrcali provjere iz `TroskovnikService.Izracunaj` — sučelje ne smije prikazati
 * redak s 0 bodova ni tiho izostaviti odabranu stavku.
 */
export function provjeriZahtjev(
  odabraneStavke: TarifnaStavka[],
  ulaz: ProvjeraUlaza,
): string[] {
  const greske: string[] = [];

  const trebaKazneniTier = odabraneStavke.some((s) => s.kazneni_base);
  if (trebaKazneniTier) {
    if (!ulaz.zaprijecenaKazna) {
      greske.push(
        "Odabrane su stavke iz kaznenog postupka, a nije odabrana zapriječena kazna. " +
          "Tbr. 4. toč. 1 nagradu veže isključivo uz visinu zapriječene kazne.",
      );
    } else if (!(ulaz.zaprijecenaKazna in ZAPRIJECENA_KAZNA_TIERS)) {
      greske.push(`Nepoznata zapriječena kazna: "${ulaz.zaprijecenaKazna}".`);
    }
  }

  const imaOduzimanje = odabraneStavke.some((s) => s.prekrsaj_oduzimanje);
  if (imaOduzimanje && ulaz.isOduzimanjePrekrsaj && ulaz.vrijednostOduzimanja <= 0) {
    greske.push(
      "Uključeno je oduzimanje predmeta, a nije unesena vrijednost oduzetog predmeta " +
        "odnosno imovinske koristi (Tbr. 4. toč. 1 al. 8 → Tbr. 7. toč. 1).",
    );
  }

  // Tbr. 45 — razlika između razina suda je do peterostruke, pa nema razumne
  // zadane vrijednosti. Isto vrijedi za način obračuna, koji Tarifa ne razrješava.
  if (ulaz.osnovicaTbr7 === OSNOVICA_TBR7.Tbr45Neprocjenjiv) {
    if (!ulaz.razinaSuda) {
      greske.push(
        "Tbr. 45 zahtijeva odabranu razinu suda (općinski / županijski i trgovački / " +
          "Vrhovni). Bez nje se bodovi ne mogu odrediti — razlika je do peterostruke.",
      );
    }
    if (!ulaz.tbr45Nacin) {
      greske.push(
        "Tbr. 45 zahtijeva odabrani način obračuna. Tarifa ne kaže primjenjuju li se " +
          "postoci iz Tbr. 8. i Tbr. 9. na iznos iz Tbr. 45, pa sustav ne smije pretpostaviti.",
      );
    }
  }

  // ── Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26 ──
  // Zrcali provjere iz `VpsCalculator.Razrijesi`.
  if (ulaz.vps) {
    greske.push(...provjeriVps(ulaz.vps));
  }

  // ── Oslobođenja — čl. 11–13 ──
  if (ulaz.oslobodjenje) {
    greske.push(...provjeriOslobodjenje(ulaz.oslobodjenje));
  }

  return greske;
}

function provjeriVps(vps: VpsUlaz): string[] {
  const greske: string[] = [];

  if (!(vps.vrstaPostupka in VRSTA_POSTUPKA_PRAVILA)) {
    greske.push(`Nepoznata vrsta postupka: "${vps.vrstaPostupka}".`);
    return greske;
  }

  if (
    VRSTA_POSTUPKA_PRAVILA[vps.vrstaPostupka].nacin === "uzdrzavanje" &&
    !(vps.uzdrzavanjeMjesecno && vps.uzdrzavanjeMjesecno > 0)
  ) {
    greske.push(
      "U postupcima radi uzdržavanja vrijednost predmeta spora je zbroj davanja za tri " +
        "mjeseca (čl. 15), pa je potreban mjesečni iznos davanja.",
    );
  }

  const zahtjevi = vps.zahtjevi ?? [];
  if (zahtjevi.length > 0) {
    if (!vps.zahtjeviNacin) {
      greske.push(
        "Uneseno je više zahtjeva, a nije odabrano kako se sastavljaju. Čl. 16 razlikuje " +
          "zbroj (isti tuženik), zasebnu procjenu (suparničarstvo) i veći od povezanih zahtjeva.",
      );
    } else if (vps.zahtjeviNacin === ZAHTJEVI_NACIN.Zasebno) {
      const oznaceni = zahtjevi.filter((z) => z.primarni).length;
      if (oznaceni !== 1) {
        greske.push(
          "Uz suparničarstvo (čl. 16) svaki se zahtjev procjenjuje zasebno, pa se vrijednosti " +
            `ne zbrajaju. Označite točno jedan zahtjev koji ovaj troškovnik pokriva — označeno ih je ${oznaceni}.`,
        );
      }
    }

    if (zahtjevi.some((z) => z.iznos < 0)) {
      greske.push("Vrijednost pojedinog zahtjeva po čl. 16 ne može biti negativna.");
    }
  }

  const lijek = vps.pravniLijek ?? PRAVNI_LIJEK.Nema;
  if (
    (lijek === PRAVNI_LIJEK.PobijaniDio || lijek === PRAVNI_LIJEK.SamoTroskovi) &&
    !(vps.pravniLijekIznos && vps.pravniLijekIznos > 0)
  ) {
    greske.push(
      lijek === PRAVNI_LIJEK.PobijaniDio
        ? "Kad se pobija samo dio odluke, pristojba se plaća prema vrijednosti pobijanog dijela (čl. 24), pa je taj iznos potreban."
        : "Kad se pravni lijek podnosi samo protiv odluke o troškovima ili sporednim potraživanjima, pristojba se plaća prema iznosu troškova (čl. 24), pa je taj iznos potreban.",
    );
  }

  return greske;
}

function provjeriOslobodjenje(o: OslobodjenjeUlaz): string[] {
  const greske: string[] = [];
  const brojOsoba = Math.max(1, o.brojOsoba);

  if (o.brojOslobodjenih < 0) {
    greske.push("Broj oslobođenih osoba ne može biti negativan.");
  } else if (o.brojOslobodjenih > brojOsoba) {
    greske.push(
      `Oslobođeno je ${o.brojOslobodjenih} osoba, a zastupa se ${brojOsoba}. Čl. 12 dijeli ` +
        "vrijednost predmeta spora među osobama koje podnesak podnose, pa oslobođenih ne može " +
        "biti više nego ukupno.",
    );
  }

  if (o.udioVps !== null && o.udioVps !== undefined && o.udioVps < 0) {
    greske.push(
      "Dio vrijednosti predmeta spora koji otpada na neoslobođene osobe (čl. 12) ne može biti negativan.",
    );
  }

  const postotak = o.postotakUspjeha;
  if (postotak !== null && postotak !== undefined && (postotak < 0 || postotak > 100)) {
    greske.push(`Omjer uspjeha po čl. 13 iznosi ${postotak} %. Dopušteno je 0–100 %.`);
  }

  return greske;
}

/** ID stavke po Tbr. 7. toč. 9 — jednokratna nagrada u sporovima ispod 100,00 €. */
export const ITEM_MALI_SPOR = "par_mali_spor";

/** Gornja granica iz Tbr. 7. toč. 9. */
export const PRAG_MALOG_SPORA = 100;

/**
 * Tbr. 7. toč. 9: "Za zastupanje u parničnom postupku u kojem je vrijednost predmeta
 * spora manja od 100,00 eura (…) odvjetniku pripada jednokratna nagrada za cijeli
 * prvostupanjski postupak u visini od 50 bodova."
 *
 * Odredba je obavezna, ali je u katalogu stavka koju odvjetnik bira sam. Ovo vraća
 * upozorenje — ne grešku — kad je VPS ispod praga, a odabrane su parnične stavke
 * koje se obračunavaju po toč. 1.
 */
export function upozorenjeMaliSpor(
  odabraneStavke: TarifnaStavka[],
  vrijednostSpora: number,
  osnovicaTbr7: OsnovicaTbr7 = OSNOVICA_TBR7.Toc1,
): string | null {
  if (osnovicaTbr7 !== OSNOVICA_TBR7.Toc1) return null;
  if (vrijednostSpora <= 0 || vrijednostSpora >= PRAG_MALOG_SPORA) return null;
  if (odabraneStavke.some((s) => s.id === ITEM_MALI_SPOR)) return null;

  const imaParnicnihPoToc1 = odabraneStavke.some(
    (s) => s.kategorija === KATEGORIJA_PARNICNI && s.tbr71_postotak !== null && s.tbr71_postotak !== undefined,
  );
  if (!imaParnicnihPoToc1) return null;

  return (
    `Vrijednost spora je ispod 100,00 €. Tbr. 7. toč. 9 za takve postupke propisuje ` +
    `jednokratnu nagradu od 50 bodova za cijeli prvostupanjski postupak — provjerite ` +
    `treba li umjesto ovih stavaka odabrati „Parnica male vrijednosti (Tbr. 7.9)".`
  );
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
/**
 * Tbr. 40. toč. 1, drugi odlomak: "Iznos povišene nagrade dijeli se na jednake
 * dijelove prema broju branjenika, odnosno zastupanih osoba."
 *
 * Dioba ne mijenja ukupan iznos — pokazuje koliko od njega otpada na svaku osobu.
 * Dijeli se nagrada; PDV i sudska pristojba nisu odvjetnička nagrada pa ne ulaze.
 */
export function podijeliPoOsobama(iznos: number, brojOsoba: number): number {
  return iznos / Math.max(1, brojOsoba);
}

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

// ── Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26 ──
//
// Za niz postupaka VPS nije stvar unosa nego zakonske odredbe: smetanje posjeda je
// uvijek 663,61 €, razvod isto, poništenje arbitražnog pravorijeka nikad ispod
// 6.634,14 €. Zrcali `VpsCalculator.Razrijesi`.
//
// Rezultat obavezno vrijedi za SUDSKU PRISTOJBU. Na odvjetničku nagradu po
// Tbr. 7. toč. 1 prenosi se samo kad korisnik to izričito odabere — čl. 14–26 pisani
// su za pristojbe, ne za Tarifu.

const NAZIV_ZAKONA_O_PRISTOJBAMA = "Zakon o sudskim pristojbama (NN 118/18, 51/23)";

/** Iznosi iz čl. 19–25; svaki se pojavljuje na više mjesta. */
export const VPS_IZNOSI = {
  /** čl. 19 (minimum), čl. 25 (fiksno), čl. 21 st. 5 i 6 (maksimum). */
  Prag1327: 1_327.22,
  /** čl. 21 st. 2, 3 i 4 te osiguranje dokaza. */
  Prag663: 663.61,
  /** čl. 21 st. 1 — otkaz najma ili zakupa. */
  Prag2654: 2_654.45,
  /** čl. 20 st. 1 i 2 — arbitraža. */
  Prag6634: 6_634.14,
  /** čl. 15 — „zbroj davanja za tri mjeseca". */
  UzdrzavanjeMjeseci: 3,
} as const;

export const VRSTA_POSTUPKA = {
  Opci: "opci",
  NenovcaniZahtjev: "nenovcani_zahtjev",
  NeutvrdivaVrijednost: "neutvrdiva_vrijednost",
  OtkazNajmaZakupa: "otkaz_najma_zakupa",
  SmetanjePosjeda: "smetanje_posjeda",
  OcinstvoMajcinstvo: "ocinstvo_majcinstvo",
  Brak: "brak",
  RedPrvenstvaOvrha: "red_prvenstva_ovrha",
  PrethodnoPravnoPitanje: "prethodno_pravno_pitanje",
  OsiguranjeDokaza: "osiguranje_dokaza",
  Uzdrzavanje: "uzdrzavanje",
  DiobaImovine: "dioba_imovine",
  Nasljedivanje: "nasljedivanje",
  PonistenjePravorijeka: "ponistenje_pravorijeka",
  PrestanakArbitraze: "prestanak_arbitraze",
} as const;

export type VrstaPostupka = (typeof VRSTA_POSTUPKA)[keyof typeof VRSTA_POSTUPKA];

export type VpsNacin = "slobodan" | "fiksan" | "minimalan" | "maksimalan" | "uzdrzavanje";

export interface VpsPravilo {
  nacin: VpsNacin;
  /** Iznos iz Zakona; null kod slobodne vrijednosti i uzdržavanja. */
  iznos: number | null;
  clanak: string;
  naziv: string;
  /** Što Zakon kaže da treba upisati kad ne propisuje iznos (čl. 17, 18). */
  naputak?: string;
}

/** Tablica iz čl. 15–25, doslovno. Zrcali `VrstaPostupka.Pravila`. */
export const VRSTA_POSTUPKA_PRAVILA: Record<VrstaPostupka, VpsPravilo> = {
  [VRSTA_POSTUPKA.Opci]: {
    nacin: "slobodan",
    iznos: null,
    clanak: "čl. 14",
    naziv: "Opće pravilo — vrijednost u vrijeme podnošenja tužbe",
  },
  [VRSTA_POSTUPKA.NenovcaniZahtjev]: {
    nacin: "minimalan",
    iznos: VPS_IZNOSI.Prag1327,
    clanak: "čl. 19",
    naziv: "Nenovčani zahtjev (činjenje, trpljenje, utvrđenje prava, istinitost isprave)",
  },
  [VRSTA_POSTUPKA.NeutvrdivaVrijednost]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag1327,
    clanak: "čl. 25",
    naziv: "Vrijednost se ne može utvrditi",
  },
  [VRSTA_POSTUPKA.OtkazNajmaZakupa]: {
    nacin: "minimalan",
    iznos: VPS_IZNOSI.Prag2654,
    clanak: "čl. 21 st. 1",
    naziv: "Otkaz ugovora o najmu ili zakupu",
    naputak: "Upisuje se jednogodišnja najamnina odnosno zakupnina.",
  },
  [VRSTA_POSTUPKA.SmetanjePosjeda]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag663,
    clanak: "čl. 21 st. 2",
    naziv: "Smetanje posjeda",
  },
  [VRSTA_POSTUPKA.OcinstvoMajcinstvo]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag663,
    clanak: "čl. 21 st. 3",
    naziv: "Utvrđivanje ili osporavanje očinstva odnosno majčinstva",
  },
  [VRSTA_POSTUPKA.Brak]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag663,
    clanak: "čl. 21 st. 4",
    naziv: "Brak — postojanje, nepostojanje, poništaj, razvod",
  },
  [VRSTA_POSTUPKA.RedPrvenstvaOvrha]: {
    nacin: "maksimalan",
    iznos: VPS_IZNOSI.Prag1327,
    clanak: "čl. 21 st. 5",
    naziv: "Red prvenstva tražbina u ovrsi",
    naputak: "Upisuje se visina potraživanja; iznad zakonske granice se ne obračunava.",
  },
  [VRSTA_POSTUPKA.PrethodnoPravnoPitanje]: {
    nacin: "maksimalan",
    iznos: VPS_IZNOSI.Prag1327,
    clanak: "čl. 21 st. 6",
    naziv: "Prethodna pravna pitanja bez restitucije ili naknade",
  },
  [VRSTA_POSTUPKA.OsiguranjeDokaza]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag663,
    clanak: "čl. 21 st. 3",
    naziv: "Osiguranje dokaza prije pokretanja parnice",
  },
  [VRSTA_POSTUPKA.Uzdrzavanje]: {
    nacin: "uzdrzavanje",
    iznos: null,
    clanak: "čl. 15",
    naziv: "Uzdržavanje",
    naputak: "Upisuje se mjesečno davanje; vrijednost je zbroj davanja za tri mjeseca.",
  },
  [VRSTA_POSTUPKA.DiobaImovine]: {
    nacin: "slobodan",
    iznos: null,
    clanak: "čl. 17",
    naziv: "Dioba imovine",
    naputak: "Upisuje se vrijednost dijela koji tužitelj traži izdvojiti, ne cijele imovine.",
  },
  [VRSTA_POSTUPKA.Nasljedivanje]: {
    nacin: "slobodan",
    iznos: null,
    clanak: "čl. 18",
    naziv: "Nasljeđivanje cijele ostavine",
    naputak: "Upisuje se vrijednost nasljednog dijela, ne cijele ostavine.",
  },
  [VRSTA_POSTUPKA.PonistenjePravorijeka]: {
    nacin: "minimalan",
    iznos: VPS_IZNOSI.Prag6634,
    clanak: "čl. 20 st. 1",
    naziv: "Poništenje pravorijeka arbitraže",
    naputak: "Upisuje se dosuđeni iznos.",
  },
  [VRSTA_POSTUPKA.PrestanakArbitraze]: {
    nacin: "fiksan",
    iznos: VPS_IZNOSI.Prag6634,
    clanak: "čl. 20 st. 2",
    naziv: "Prestanak važnosti ugovora o arbitraži",
  },
};

/** Naziv s člankom, za padajući izbornik. */
export const VRSTA_POSTUPKA_LABELE: Record<VrstaPostupka, string> = Object.fromEntries(
  Object.entries(VRSTA_POSTUPKA_PRAVILA).map(([k, p]) => [k, `${p.clanak} — ${p.naziv}`]),
) as Record<VrstaPostupka, string>;

/** Čl. 16 — kako se sastavlja vrijednost kad tužba sadrži više zahtjeva. */
export const ZAHTJEVI_NACIN = {
  /** Više zahtjeva protiv istog tuženika → zbroj. */
  Zbroj: "zbroj",
  /** Suparničarstvo → svaki zahtjev zasebno; troškovnik pokriva označeni. */
  Zasebno: "zasebno",
  /** Povezani (eventualni) zahtjevi → onaj veće vrijednosti. */
  Eventualni: "eventualni",
} as const;

export type ZahtjeviNacin = (typeof ZAHTJEVI_NACIN)[keyof typeof ZAHTJEVI_NACIN];

export const ZAHTJEVI_NACIN_LABELE: Record<ZahtjeviNacin, string> = {
  [ZAHTJEVI_NACIN.Zbroj]: "Zbroj — više zahtjeva protiv istog tuženika (čl. 16 st. 1)",
  [ZAHTJEVI_NACIN.Zasebno]: "Zasebno — suparničarstvo, svaki zahtjev ima svoju pristojbu (čl. 16 st. 2)",
  [ZAHTJEVI_NACIN.Eventualni]: "Veći od povezanih — eventualni zahtjevi (čl. 16 st. 3)",
};

/** Čl. 24 — po kojoj se vrijednosti plaća pristojba na pravni lijek. */
export const PRAVNI_LIJEK = {
  Nema: "nema",
  CijelaOdluka: "cijela_odluka",
  PobijaniDio: "pobijani_dio",
  SamoTroskovi: "samo_troskovi",
} as const;

export type PravniLijek = (typeof PRAVNI_LIJEK)[keyof typeof PRAVNI_LIJEK];

export const PRAVNI_LIJEK_LABELE: Record<PravniLijek, string> = {
  [PRAVNI_LIJEK.Nema]: "Nije pravni lijek",
  [PRAVNI_LIJEK.CijelaOdluka]: "Pobija se cijela odluka — vrijednost iz prvostupanjskog postupka",
  [PRAVNI_LIJEK.PobijaniDio]: "Pobija se samo dio odluke — vrijednost pobijanog dijela",
  [PRAVNI_LIJEK.SamoTroskovi]: "Samo protiv odluke o troškovima — iznos troškova",
};

export interface VpsZahtjev {
  iznos: number;
  opis?: string;
  /** Uz „zasebno" — zahtjev koji ovaj troškovnik pokriva. */
  primarni?: boolean;
}

export interface VpsUlaz {
  vrijednostSpora: number;
  vrstaPostupka: VrstaPostupka;
  uzdrzavanjeMjesecno?: number;
  zahtjevi?: VpsZahtjev[];
  zahtjeviNacin?: ZahtjeviNacin | null;
  pravniLijek?: PravniLijek;
  pravniLijekIznos?: number;
}

export interface VpsRezultat {
  vps: number;
  uneseni: number;
  vrstaPostupka: VrstaPostupka;
  pravniIzvor: string;
  prilagodjen: boolean;
  napomena: string | null;
  /** Čl. 16 uz „zasebno" — zahtjevi koje ovaj troškovnik ne pokriva. */
  ostaliZahtjevi: VpsZahtjev[];
}

function novac(iznos: number): string {
  return `${iznos.toLocaleString("hr-HR", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })} €`;
}

/**
 * Razrješava vrijednost predmeta spora, tri koraka redom: čl. 16 (od čega se sastoji),
 * pravilo vrste postupka (kako je Zakon ograničava), čl. 24 (koji se broj uzima kod
 * pravnog lijeka). Vraća `null` kad podatak nedostaje — tada `provjeriZahtjev` vraća
 * poruku, a sučelje ne prikazuje iznos. Zrcali `VpsCalculator.Razrijesi`.
 */
export function razrijesiVps(ulaz: VpsUlaz): VpsRezultat | null {
  const osnovni = osnovniVps(ulaz);
  if (osnovni === null) return null;

  const pravilo = VRSTA_POSTUPKA_PRAVILA[ulaz.vrstaPostupka] ?? VRSTA_POSTUPKA_PRAVILA[VRSTA_POSTUPKA.Opci];
  const primijenjeno = primijeniPravilo(pravilo, osnovni.vps, ulaz);
  if (primijenjeno === null) return null;

  const konacno = primijeniPravniLijek(ulaz, primijenjeno);
  if (konacno === null) return null;

  return {
    vps: konacno.vps,
    uneseni: osnovni.vps,
    vrstaPostupka: ulaz.vrstaPostupka,
    pravniIzvor: konacno.izvor,
    prilagodjen: konacno.vps !== osnovni.vps,
    napomena: konacno.napomena,
    ostaliZahtjevi: osnovni.ostali,
  };
}

function osnovniVps(ulaz: VpsUlaz): { vps: number; ostali: VpsZahtjev[] } | null {
  const zahtjevi = ulaz.zahtjevi ?? [];
  if (zahtjevi.length === 0) return { vps: ulaz.vrijednostSpora, ostali: [] };

  switch (ulaz.zahtjeviNacin) {
    case ZAHTJEVI_NACIN.Zbroj:
      return { vps: zahtjevi.reduce((s, z) => s + z.iznos, 0), ostali: [] };

    case ZAHTJEVI_NACIN.Eventualni:
      return { vps: Math.max(...zahtjevi.map((z) => z.iznos)), ostali: [] };

    case ZAHTJEVI_NACIN.Zasebno: {
      const primarni = zahtjevi.filter((z) => z.primarni);
      if (primarni.length !== 1) return null;
      return { vps: primarni[0].iznos, ostali: zahtjevi.filter((z) => !z.primarni) };
    }

    default:
      return null; // način nije odabran
  }
}

function primijeniPravilo(
  pravilo: VpsPravilo,
  uneseni: number,
  ulaz: VpsUlaz,
): { vps: number; napomena: string | null; izvor: string } | null {
  const izvor = `${NAZIV_ZAKONA_O_PRISTOJBAMA}, ${pravilo.clanak} — ${pravilo.naziv}`;

  switch (pravilo.nacin) {
    case "fiksan": {
      const iznos = pravilo.iznos!;
      const napomena =
        uneseni === iznos
          ? null
          : `${pravilo.naziv}: Zakon propisuje vrijednost predmeta spora od ${novac(iznos)}, ` +
            `pa uneseni iznos od ${novac(uneseni)} nije primijenjen (${pravilo.clanak}).`;
      return { vps: iznos, napomena, izvor };
    }

    case "minimalan": {
      const granica = pravilo.iznos!;
      if (uneseni >= granica) return { vps: uneseni, napomena: null, izvor };
      return {
        vps: granica,
        napomena:
          `${pravilo.naziv}: vrijednost predmeta spora ne može biti niža od ${novac(granica)}, ` +
          `pa je uneseni iznos od ${novac(uneseni)} podignut na zakonski minimum (${pravilo.clanak}).`,
        izvor,
      };
    }

    case "maksimalan": {
      const granica = pravilo.iznos!;
      if (uneseni <= granica) return { vps: uneseni, napomena: null, izvor };
      return {
        vps: granica,
        napomena:
          `${pravilo.naziv}: vrijednost predmeta spora ne može biti viša od ${novac(granica)}, ` +
          `pa je uneseni iznos od ${novac(uneseni)} spušten na zakonski maksimum (${pravilo.clanak}).`,
        izvor,
      };
    }

    // Čl. 15 — „zbroj davanja za tri mjeseca". Bez mjesečnog iznosa nema se što izvesti.
    case "uzdrzavanje": {
      const mjesecno = ulaz.uzdrzavanjeMjesecno ?? 0;
      if (mjesecno <= 0) return null;

      const vps = mjesecno * VPS_IZNOSI.UzdrzavanjeMjeseci;
      return {
        vps,
        napomena:
          `Uzdržavanje: vrijednost predmeta spora je zbroj davanja za tri mjeseca — ` +
          `${VPS_IZNOSI.UzdrzavanjeMjeseci} × ${novac(mjesecno)} = ${novac(vps)} (${pravilo.clanak}).`,
        izvor,
      };
    }

    // Čl. 14, 17, 18 — Zakon kaže ŠTO se upisuje, ne koliko.
    default:
      return { vps: uneseni, napomena: null, izvor };
  }
}

function primijeniPravniLijek(
  ulaz: VpsUlaz,
  prethodni: { vps: number; napomena: string | null; izvor: string },
): { vps: number; napomena: string | null; izvor: string } | null {
  const lijek = ulaz.pravniLijek ?? PRAVNI_LIJEK.Nema;
  if (lijek === PRAVNI_LIJEK.Nema) return prethodni;

  const spoji = (druga: string) =>
    prethodni.napomena ? `${prethodni.napomena} ${druga}` : druga;

  if (lijek === PRAVNI_LIJEK.CijelaOdluka) {
    return {
      ...prethodni,
      napomena: spoji(
        "Pravni lijek protiv cijele odluke: pristojba se plaća prema vrijednosti predmeta " +
          "spora utvrđenoj u prvostupanjskom postupku (čl. 24).",
      ),
    };
  }

  const iznos = ulaz.pravniLijekIznos ?? 0;
  if (iznos <= 0) return null;

  const opis =
    lijek === PRAVNI_LIJEK.PobijaniDio
      ? `Pobija se samo dio odluke: vrijednost predmeta spora je vrijednost pobijanog dijela, ${novac(iznos)} (čl. 24).`
      : `Pravni lijek samo protiv odluke o troškovima odnosno sporednim potraživanjima: ` +
        `vrijednost predmeta spora je iznos troškova, ${novac(iznos)} (čl. 24).`;

  return {
    vps: iznos,
    napomena: spoji(opis),
    izvor: `${NAZIV_ZAKONA_O_PRISTOJBAMA}, čl. 24 — pristojba na pravni lijek`,
  };
}

// ── Oslobođenja — Zakon o sudskim pristojbama, čl. 10–13 ─────────

/**
 * Čl. 11 — 21 točka osoba oslobođenih po sili zakona, uz oslobođenje na zahtjev
 * stranke po čl. 10, o kojem odlučuje sud. Zrcali `OslobodjenjeKategorija`.
 */
export const OSLOBODJENJE_KATEGORIJE: Record<string, string> = {
  rh_tijela: "čl. 11. t. 1 — Republika Hrvatska i tijela državne vlasti",
  javne_ovlasti: "čl. 11. t. 2 — osobe i tijela s javnim ovlastima, u postupcima iz tih ovlasti",
  radnici: "čl. 11. t. 3 — radnici u sporovima o pravima iz radnog odnosa",
  sluzbenici: "čl. 11. t. 4 — službenici i namještenici u upravnim sporovima o službeničkim pravima",
  invaliditet: "čl. 11. t. 5 — osobe s invaliditetom",
  obitelji_branitelja:
    "čl. 11. t. 6 — supružnici, djeca i roditelji branitelja smrtno stradalih, nestalih i zatočenih",
  obitelji_domovinski_rat:
    "čl. 11. t. 7 — supružnici, djeca i roditelji smrtno stradalih, nestalih i zatočenih u Domovinskom ratu",
  prognanici: "čl. 11. t. 8 — prognanici, izbjeglice i povratnici",
  socijalna_skrb: "čl. 11. t. 9 — korisnici socijalne skrbi koji primaju zajamčenu minimalnu naknadu",
  humanitarne: "čl. 11. t. 10 — humanitarne organizacije i organizacije osoba s invaliditetom",
  djeca_uzdrzavanje: "čl. 11. t. 11 — djeca u postupcima radi uzdržavanja",
  majcinstvo_ocinstvo:
    "čl. 11. t. 12 — postupci radi utvrđivanja majčinstva ili očinstva i troškova trudnoće i poroda",
  poslovna_sposobnost: "čl. 11. t. 13 — postupci radi vraćanja poslovne sposobnosti",
  maloljetnici_brak: "čl. 11. t. 14 — maloljetnici u postupcima dopuštenja za sklapanje braka",
  predaja_djeteta: "čl. 11. t. 15 — postupci radi predaje djeteta i ostvarivanja osobnih odnosa s djetetom",
  mirovinsko_zdravstveno:
    "čl. 11. t. 16 — postupci o pravima iz mirovinskog i zdravstvenog osiguranja, osiguranja za slučaj nezaposlenosti i socijalne skrbi",
  ustavna_prava: "čl. 11. t. 17 — zaštita ustavom zajamčenih ljudskih prava protiv konačnih pojedinačnih akata",
  steta_okolis: "čl. 11. t. 18 — naknada štete zbog onečišćenja okoliša",
  sindikati: "čl. 11. t. 19 — sindikati u kolektivnim radnim sporovima",
  potrosaci: "čl. 11. t. 20 — potrošači kao stečajni dužnici i tužitelji u kolektivnoj zaštiti",
  posebni_zakon: "čl. 11. t. 21 — drugi kada je propisano posebnim zakonom",
  zahtjev_cl_10: "čl. 10 — oslobođenje na zahtjev stranke koja ne može platiti bez štete za uzdržavanje",
};

/**
 * Čl. 11 st. 5 — oslobođenje iz t. 1 i 2 ne obuhvaća jedinice lokalne i područne
 * samouprave, osim kad im je preneseno obavljanje javnih ovlasti.
 */
export const OSLOBODJENJE_NAPOMENA_LOKALNA =
  "Čl. 11. st. 5: oslobođenje se ne odnosi na jedinice lokalne i područne (regionalne) " +
  "samouprave, osim kada im je preneseno obavljanje javnih ovlasti.";

export interface OslobodjenjeUlaz {
  /** Zastupana strana oslobođena u cijelosti. */
  oslobodjen: boolean;
  brojOsoba: number;
  brojOslobodjenih: number;
  /** Čl. 12 — dio VPS-a koji otpada na neoslobođene; null → dijeli se brojem osoba. */
  udioVps?: number | null;
  /** Čl. 13 — omjer uspjeha oslobođene stranke, 0–100. */
  postotakUspjeha?: number | null;
}

export interface PristojbaRezultat {
  iznos: number;
  /** Dio VPS-a na koji je pristojba stvarno obračunata (čl. 12). */
  vpsUdio: number;
  /** Čl. 13 — pristojba na teret protivne stranke; ne ulazi u ukupan trošak. */
  naTeretProtustranke: number;
}

const OSLOBODJENJE_NEMA: OslobodjenjeUlaz = {
  oslobodjen: false,
  brojOsoba: 1,
  brojOslobodjenih: 0,
};

function sviOslobodjeni(o: OslobodjenjeUlaz): boolean {
  return o.oslobodjen || (o.brojOslobodjenih > 0 && o.brojOslobodjenih >= Math.max(1, o.brojOsoba));
}

/** Čl. 12 st. 2 — bez upisanog udjela vrijednost se dijeli brojem osoba. */
function vpsNeoslobodjenih(vps: number, o: OslobodjenjeUlaz): number {
  if (sviOslobodjeni(o)) return 0;
  if (o.brojOslobodjenih <= 0) return vps;

  if (o.udioVps !== null && o.udioVps !== undefined) {
    return Math.min(Math.max(o.udioVps, 0), vps);
  }

  const brojOsoba = Math.max(1, o.brojOsoba);
  return (vps * Math.max(0, brojOsoba - o.brojOslobodjenih)) / brojOsoba;
}

/**
 * Puni obračun pristojbe s oslobođenjima. Čl. 12 umanjuje OSNOVICU, ne gotov iznos —
 * tarifa je progresivna, pa pristojba na polovicu VPS-a nije polovica pristojbe.
 * Zrcali `SudskePristojbeCalculator.Obracunaj`.
 */
export function obracunajPristojbu(
  vps: number,
  osnova: PristojbaOsnova = "Puna",
  oslobodjenje: OslobodjenjeUlaz = OSLOBODJENJE_NEMA,
): PristojbaRezultat {
  const vpsUdio = vpsNeoslobodjenih(vps, oslobodjenje);
  const iznos = izracunajSudskuPristojbu(vpsUdio, osnova);

  const postotak = oslobodjenje.postotakUspjeha ?? 0;
  const vpsOslobodjenih = vps - vpsUdio;

  const naTeretProtustranke =
    postotak > 0 && vpsOslobodjenih > 0
      ? zaokruziNovac(
          (izracunajSudskuPristojbu(vpsOslobodjenih, osnova) * Math.min(Math.max(postotak, 0), 100)) / 100,
        )
      : 0;

  return { iznos, vpsUdio, naTeretProtustranke };
}
