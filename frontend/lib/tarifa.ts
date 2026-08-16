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

// ── Ručno uneseni troškovi: dodatne pristojbe i predujmovi ──────
//
// Isti oblik za oba popisa (zrcali `TroskovnikTrosakRequest`). Odjeljak "Sudske
// pristojbe" = automatski izračunata pristojba + ovaj popis; odjeljak "Predujmovi"
// je isključivo ovaj popis. Ni jedno ni drugo ne nosi PDV.

/**
 * Ponuđeni tekst napomene; korisnik ga smije izmijeniti ili obrisati.
 * Mora ostati doslovno jednaka `TarifaConstants.ZadanaNapomena` na backendu —
 * paritetni test to zaključava.
 */
export const ZADANA_NAPOMENA =
  "Na traženi iznos obračunava se zakonska zatezna kamata sukladno članku 29. " +
  "Zakona o obveznim odnosima.";

export interface TrosakStavka {
  datum?: string | null;
  opis: string;
  iznos: number;
}

/**
 * Zbraja ručno unesen popis troškova (dodatne pristojbe ili predujmove). Svaki
 * iznos zaokružuje se zasebno prije zbrajanja — isto kao u
 * `TroskovnikService.RazrijesiRucneTroskove` na backendu.
 */
export function zbrojiRucneTroskove(stavke: TrosakStavka[]): number {
  return stavke.reduce((zbroj, t) => zbroj + zaokruziNovac(t.iznos || 0), 0);
}

// ── Premještanje redaka ──────────────────────────────────────────
//
// Korisnik bira redoslijed radnji sam (nema forsiranog kronološkog poretka).
// Redni broj se renumerira nakon premještanja ili brisanja — izveden je iz
// pozicije u polju (index + 1), nikad spremljen zasebno.

/**
 * Zamjena elementa na `index` sa susjedom u smjeru `smjer` (-1 gore, +1 dolje).
 * Na rubovima (prvi ▲ ili zadnji ▼) nema efekta — vraća isto polje.
 */
export function pomakniRedak<T>(lista: T[], index: number, smjer: -1 | 1): T[] {
  const cilj = index + smjer;
  if (index < 0 || index >= lista.length || cilj < 0 || cilj >= lista.length) return lista;

  const rezultat = [...lista];
  [rezultat[index], rezultat[cilj]] = [rezultat[cilj], rezultat[index]];
  return rezultat;
}
