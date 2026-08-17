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
 *  - Uredba o Tarifi sudskih pristojbi, NN 50/2026 (na snazi od 1.6.2026.)
 *  - Uredba o Tarifi sudskih pristojbi, NN 37/2023 — samo za dva prijelazna
 *    slučaja iz čl. 2 nove Uredbe
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
 * Opunomoćenik djeteta žrtve kaznenog djela — 70% pune tarife.
 *
 * Izvor stope: Pravilnik, Narodne novine 22/2022, br. 271, čl. 2.
 * https://narodne-novine.nn.hr/clanci/sluzbeni/2022_02_22_271.html
 *
 * Osnovica je ista kao u čl. 1 — nagrada za pojedinu radnju prema Tarifi. Čl. 2 se ne
 * poziva na Tbr. 4. toč. 8, pa se te dvije stope ne množe: 70% je 70%. Umanjenje iz
 * Tbr. 4. toč. 8 (zastupanje oštećenika) nosi sama stavka u katalogu.
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

/** Naziv kategorije parničnog postupka u katalogu. */
export const KATEGORIJA_PARNICNI = "Parnični postupak";

/** Naziv kategorije ovršnog postupka u katalogu. */
export const KATEGORIJA_OVRSNI = "Ovršni postupak";

/**
 * True kad su odabrane isključivo kaznene/prekršajne stavke.
 *
 * U kaznenom se postupku sve veže na zapriječenu kaznu (Tbr. 4. toč. 1), nikad
 * na vrijednost predmeta spora, pa se VPS izbacuje iz izračuna.
 *
 * Sudska se pristojba pritom ne izvodi odavde nego iz PRISTOJBENE_RADNJE: Poglavlje V.
 * Uredbe NN 50/2026 pokriva postupak po privatnoj tužbi, a obrana po javnoj optužbi
 * pristojbu nema.
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

  // Pravilnik NN 22/2022 br. 271 — obje stope idu od iste osnovice: nagrade koja bi
  // odvjetniku pripadala za pojedinu radnju prema Tarifi. Ne množe se međusobno.
  //   čl. 1 — obrana po sl. dužnosti / na teret proračuna → 50%
  //   čl. 2 — opunomoćenik djeteta žrtve kaznenog djela  → 70%
  // Uloge se isključuju (obrana okrivljenika vs. zastupanje djeteta žrtve), pa se
  // primjenjuje najviše jedna stopa i kad ipak stignu obje zastavice.
  if (isZastupnikDjeteta) {
    bodovi *= ZASTUPNIK_DJETETA_STOPA;
  } else if (isPoSluzbenojDuznosti || isNaTeretProracuna) {
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

// ══════════════════════════════════════════════════════════════════
//  Sudske pristojbe — Uredba o Tarifi sudskih pristojbi
// ══════════════════════════════════════════════════════════════════
//
// Zrcalo backenda: Backend/Services/Pristojbe/* i SudskePristojbeCalculator.cs.
// Uredba naplaćuje po RADNJI — tužba (Tar. br. 1), presuda (Tar. br. 2) i žalba
// (Tar. br. 3) tri su zasebne pristojbe — pa je ishod lista, a ne jedan broj.
//
// Očekivani iznosi po odredbama žive u Backend.Tests/TestData/pristojbe-paritet.json,
// koji čitaju i xunit i vitest. Razilaženje dvaju motora ruši testove na obje strane.

/** Koja se Uredba primjenjuje na pojedinu pristojbenu radnju. */
export type UredbaVerzija = "NN 50/2026" | "NN 37/2023";

/**
 * Dan stupanja na snagu NN 50/2026. Čl. 2 uz taj dan vezuje oba prijelazna slučaja:
 * odgovor na tužbu podnesen PRIJE tog dana i upravni spor pokrenut PRIJE tog dana.
 */
export const NOVA_UREDBA_NA_SNAZI = "2026-06-01";

export type PristojbaPoglavlje =
  | "I. Parnični i ovršni postupak"
  | "II. Izvanparnični postupak"
  | "III. Ostavinski postupak"
  | "IV. Zemljišnoknjižni postupak"
  | "V. Kazneni postupak po privatnoj tužbi"
  | "VI. Stečaj i likvidacija"
  | "VII. Sudski registar"
  | "VIII. Upravni spor"
  | "IX. Ostali slučajevi";

/** Jedan razred ljestvice: sve do `do` plaća `iznos`. */
type Razred = { do: number; iznos: number };

/**
 * Kako se iznos izvodi iz osnovice. Zatvoren skup — svih 35 tarifnih brojeva
 * svodi se na ovih sedam oblika.
 */
type Izracun =
  | { vrsta: "fiksno"; iznos: number }
  | { vrsta: "ne_placa_se" }
  | { vrsta: "udio"; odredba: string; faktor: number; max?: number }
  | { vrsta: "ljestvica_postotak"; razredi: Razred[]; prag: number; baza: number; stopa: number; max: number }
  | { vrsta: "ljestvica_korak"; razredi: Razred[]; prag: number; baza: number; korak: number; poKoraku: number; max: number }
  | { vrsta: "postotak"; stopa: number; max: number }
  | { vrsta: "stepenasto"; stepenice: Razred[] };

export interface PristojbenaOdredba {
  id: string;
  poglavlje: PristojbaPoglavlje;
  tarifni_broj: string;
  opis: string;
  izracun: Izracun;
  /** Što je osnovica ove odredbe; prazno kad je ne koristi. */
  osnovica_opis?: string;
}

// ── NN 50/2026, po poglavljima Priloga ────────────────────────────

const VPS_OSNOVICA = "vrijednost predmeta spora";

export const PRISTOJBE_NN50_2026: PristojbenaOdredba[] = [
  // ── Poglavlje I. — Tar. br. 1, podnesci ──
  //   iznad      do eura     pristojba
  //   0,00         400,00       30,00
  //   400,01       800,00       60,00
  //   800,01     1.200,00       80,00
  //   1.200,01   1.600,00      110,00
  //   1.600,01   2.000,00      140,00
  // Preko 2.000,00: 140,00 + 1 % na razliku, najviše 1.500,00.
  {
    id: "tbr1_st1",
    poglavlje: "I. Parnični i ovršni postupak",
    tarifni_broj: "Tar. br. 1 st. 1",
    opis: "Tužba, protutužba, prigovor protiv platnog naloga i prigovor protiv rješenja o ovrsi",
    izracun: {
      vrsta: "ljestvica_postotak",
      razredi: [
        { do: 400, iznos: 30 },
        { do: 800, iznos: 60 },
        { do: 1_200, iznos: 80 },
        { do: 1_600, iznos: 110 },
        { do: 2_000, iznos: 140 },
      ],
      prag: 2_000,
      baza: 140,
      stopa: 0.01,
      max: 1_500,
    },
    osnovica_opis: VPS_OSNOVICA,
  },
  // Nabrajanje u st. 2 završava popisom ODGOVORA ("odgovor na … tužbu, žalbu,
  // prijedlog za dopuštenje revizije i reviziju"). Sama žalba ide po Tar. br. 3 i
  // ondje je skuplja, ne jeftinija.
  {
    id: "tbr1_st2",
    poglavlje: "I. Parnični i ovršni postupak",
    tarifni_broj: "Tar. br. 1 st. 2",
    opis:
      "Tužba s prijedlogom za platni nalog, prijedlog za ovrhu/protuovrhu/osiguranje, " +
      "prijedlog za povrat u prijašnje stanje, prijedlog za osiguranje dokaza, " +
      "odgovor na tužbu, žalbu, prijedlog za dopuštenje revizije i reviziju",
    izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 0.5 },
    osnovica_opis: VPS_OSNOVICA,
  },
  {
    id: "tbr1_st3",
    poglavlje: "I. Parnični i ovršni postupak",
    tarifni_broj: "Tar. br. 1 st. 3",
    opis:
      "Prijedlog za priznanje odluke stranog suda; ovršenikov prijedlog za odgodu ovrhe; " +
      "prijedlog za odgodu prijenosa zaplijenjenih sredstava (ako je odbijen)",
    izracun: { vrsta: "fiksno", iznos: 40 },
  },

  // ── Tar. br. 2 — odluke ──
  { id: "tbr2_st1", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 1", opis: "Prvostupanjska presuda; rješenje u sporu zbog smetanja posjeda", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st2", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 2", opis: "Presuda zbog ogluhe, bez rasprave, zbog izostanka, na temelju odricanja ili priznanja", izracun: { vrsta: "udio", odredba: "tbr1_st2", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st3", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 3", opis: "Rješenje o odbačaju nedopuštenog prijedloga za reviziju i o odbačaju revizije", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st4", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 4", opis: "Rješenje o izdavanju platnog naloga", izracun: { vrsta: "udio", odredba: "tbr1_st2", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st5", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 5", opis: "Rješenje o ovrsi, protuovrsi, osiguranju dokaza i osiguranju", izracun: { vrsta: "udio", odredba: "tbr1_st2", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st5_strane", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 5", opis: "Rješenje o ovrsi na temelju stranih ovršnih isprava", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st6", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 6", opis: "Rješenje o povratu u prijašnje stanje", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr2_st7", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 7", opis: "Rješenje o priznanju strane odluke", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr2_st8", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 8", opis: "Rješenje o odgodi ili obustavi ovrhe", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr2_st9", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 2 st. 9", opis: "Sudske nagodbe — pristojba se ne plaća", izracun: { vrsta: "ne_placa_se" } },

  // ── Tar. br. 3 — pravni lijekovi ──
  // Protiv PRESUDE dvostruko, protiv RJEŠENJA jednostruko.
  { id: "tbr3_st1", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 3 st. 1", opis: "Žalba, prijedlog za dopuštenje revizije i revizija protiv presude; žalba protiv rješenja u sporu zbog smetanja posjeda — uvećano za 100 %", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 2 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr3_st2", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 3 st. 2", opis: "Žalba, prijedlog za dopuštenje revizije i revizija protiv rješenja; prijedlog za ponavljanje postupka", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr3_st3", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 3 st. 3", opis: "Prijedlog za proglašenje pljenidbe i prijenosa nedopuštenim", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 1 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr3_st5", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 3 st. 5", opis: "Žalba protiv rješenja o povratu u prijašnje stanje ili o priznanju strane odluke — najviše 50,00", izracun: { vrsta: "udio", odredba: "tbr1_st2", faktor: 1, max: 50 }, osnovica_opis: VPS_OSNOVICA },

  // ── Poglavlje II. — izvanparnični postupak ──
  { id: "tbr4_prijedlog", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 4", opis: "Prijedlog za pokretanje izvanparničnog postupka ili za povrat u prijašnje stanje", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr4_odluka", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 4", opis: "Odluka prvostupanjskog suda o osnovanosti prijedloga", izracun: { vrsta: "fiksno", iznos: 40 } },
  { id: "tbr4_zalba", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 4", opis: "Žalba protiv odluke o osnovanosti prijedloga", izracun: { vrsta: "fiksno", iznos: 60 } },
  {
    id: "tbr5",
    poglavlje: "II. Izvanparnični postupak",
    tarifni_broj: "Tar. br. 5",
    opis: "Prijedlog za diobu suvlasništva i zajedničke imovine",
    izracun: {
      vrsta: "ljestvica_korak",
      razredi: [
        { do: 2_700, iznos: 30 },
        { do: 4_000, iznos: 40 },
        { do: 6_600, iznos: 60 },
      ],
      prag: 6_600,
      baza: 60,
      korak: 1_300,
      poKoraku: 30,
      max: 840,
    },
    osnovica_opis: "vrijednost imovine koja se dijeli",
  },
  { id: "tbr5_odluka", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 5", opis: "Odluka o diobi suvlasništva i zajedničke imovine", izracun: { vrsta: "udio", odredba: "tbr5", faktor: 1 }, osnovica_opis: "vrijednost imovine koja se dijeli" },
  { id: "tbr5_zalba", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 5", opis: "Žalba u postupku diobe suvlasništva i zajedničke imovine", izracun: { vrsta: "udio", odredba: "tbr5", faktor: 1 }, osnovica_opis: "vrijednost imovine koja se dijeli" },
  { id: "tbr6_prijedlog", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 6", opis: "Prijedlog za uređenje odnosa između suvlasnika", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr6_odluka", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 6", opis: "Odluka o uređenju odnosa između suvlasnika", izracun: { vrsta: "fiksno", iznos: 80 } },
  { id: "tbr6_zalba", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 6", opis: "Žalba u postupku uređenja odnosa između suvlasnika", izracun: { vrsta: "fiksno", iznos: 80 } },
  { id: "tbr7_prijedlog", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 7", opis: "Prijedlog u postupku uređenja međe, nužnog prolaza i služnosti", izracun: { vrsta: "fiksno", iznos: 80 } },
  { id: "tbr7_odluka", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 7", opis: "Odluka u postupku uređenja međe, nužnog prolaza i služnosti", izracun: { vrsta: "fiksno", iznos: 170 } },
  { id: "tbr7_zalba", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 7", opis: "Žalba u postupku uređenja međe, nužnog prolaza i služnosti", izracun: { vrsta: "fiksno", iznos: 170 } },
  { id: "tbr8_tijelo", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 8", opis: "Ovjera isprave za inozemstvo — potpis i pečat tijela ili pravne osobe", izracun: { vrsta: "fiksno", iznos: 15 } },
  { id: "tbr8_tumac", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 8", opis: "Ovjera isprave za inozemstvo — potpis i pečat sudskog tumača ili vještaka", izracun: { vrsta: "fiksno", iznos: 20 } },
  { id: "tbr9_prvi_upis", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 9 st. 1", opis: "Prvi upis broda", izracun: { vrsta: "fiksno", iznos: 840 } },
  { id: "tbr9_rjesenje", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 9", opis: "Rješenje o upisu broda — polovica", izracun: { vrsta: "udio", odredba: "tbr9_prvi_upis", faktor: 0.5 } },
  { id: "tbr9_daljnji_upis", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 9", opis: "Daljnji upisi broda — polovica", izracun: { vrsta: "udio", odredba: "tbr9_prvi_upis", faktor: 0.5 } },
  { id: "tbr9_zalba", poglavlje: "II. Izvanparnični postupak", tarifni_broj: "Tar. br. 9", opis: "Žalba u postupku upisa broda — dvostruko od st. 1", izracun: { vrsta: "udio", odredba: "tbr9_prvi_upis", faktor: 2 } },

  // ── Poglavlje III. — ostavinski postupak ──
  {
    id: "tbr11",
    poglavlje: "III. Ostavinski postupak",
    tarifni_broj: "Tar. br. 11",
    opis: "Rješenje o nasljeđivanju",
    izracun: {
      vrsta: "ljestvica_korak",
      razredi: [
        { do: 3_300, iznos: 30 },
        { do: 4_600, iznos: 40 },
        { do: 8_000, iznos: 60 },
      ],
      prag: 8_000,
      baza: 60,
      korak: 1_300,
      poKoraku: 30,
      max: 700,
    },
    osnovica_opis: "čista vrijednost ostavine",
  },
  { id: "tbr12", poglavlje: "III. Ostavinski postupak", tarifni_broj: "Tar. br. 12", opis: "Žalba u ostavinskom postupku", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr13_sastavljanje", poglavlje: "III. Ostavinski postupak", tarifni_broj: "Tar. br. 13", opis: "Sastavljanje sudske oporuke ili opoziv oporuke pred sudom", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr13_cuvanje", poglavlje: "III. Ostavinski postupak", tarifni_broj: "Tar. br. 13", opis: "Čuvanje i vraćanje oporuke", izracun: { vrsta: "fiksno", iznos: 30 } },

  // ── Poglavlje IV. — zemljišnoknjižni postupak ──
  { id: "tbr14_upis", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 14", opis: "Prijedlog za upis ili brisanje u zemljišnoj knjizi", izracun: { vrsta: "fiksno", iznos: 10 } },
  { id: "tbr14_ispravni", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 14", opis: "Prijedlog u pojedinačnom ispravnom postupku", izracun: { vrsta: "fiksno", iznos: 70 } },
  { id: "tbr14_zakasnjeli", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 14", opis: "Prijedlog podnesen nakon 60 dana od stjecanja uvjeta — peterostruko", izracun: { vrsta: "udio", odredba: "tbr14_upis", faktor: 5 } },
  { id: "tbr15_uknjizba", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 15", opis: "Rješenje o uknjižbi ili predbilježbi", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr15_zabiljezba", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 15", opis: "Rješenje o zabilježbi", izracun: { vrsta: "fiksno", iznos: 10 } },
  { id: "tbr15_poljoprivredni", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 15", opis: "Upis za poljoprivredne kredite — 10 % propisane pristojbe", izracun: { vrsta: "udio", odredba: "tbr15_uknjizba", faktor: 0.1 } },
  {
    id: "tbr15_etaziranje",
    poglavlje: "IV. Zemljišnoknjižni postupak",
    tarifni_broj: "Tar. br. 15",
    opis: "Upis etažnog vlasništva",
    izracun: {
      vrsta: "stepenasto",
      stepenice: [
        { do: 10, iznos: 30 },
        { do: 100, iznos: 80 },
        { do: 500, iznos: 140 },
        { do: Number.POSITIVE_INFINITY, iznos: 280 },
      ],
    },
    osnovica_opis: "broj etažnih dijelova",
  },
  { id: "tbr15_zakasnjelo", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 15", opis: "Odluka po prijedlogu podnesenom nakon 60 dana — peterostruko", izracun: { vrsta: "udio", odredba: "tbr15_uknjizba", faktor: 5 } },
  { id: "tbr16", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 16", opis: "Prigovor i žalba u zemljišnoknjižnom postupku", izracun: { vrsta: "fiksno", iznos: 70 } },
  { id: "tbr17_18", poglavlje: "IV. Zemljišnoknjižni postupak", tarifni_broj: "Tar. br. 17 i 18", opis: "Ovjera preslike", izracun: { vrsta: "fiksno", iznos: 5 } },

  // ── Poglavlje V. — kazneni postupak po privatnoj tužbi ──
  { id: "tbr19_tuzba", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 19", opis: "Privatna tužba i protutužba", izracun: { vrsta: "fiksno", iznos: 70 } },
  { id: "tbr19_povrat", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 19", opis: "Prijedlog za povrat u prijašnje stanje", izracun: { vrsta: "fiksno", iznos: 40 } },
  { id: "tbr20", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 20", opis: "Prvostupanjska presuda ili sudska opomena", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr21_zalba", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 21", opis: "Žalba", izracun: { vrsta: "fiksno", iznos: 170 } },
  { id: "tbr21_odgovor", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 21", opis: "Odgovor na žalbu — polovica od Tar. br. 20", izracun: { vrsta: "udio", odredba: "tbr20", faktor: 0.5 } },
  { id: "tbr21_izvanredni", poglavlje: "V. Kazneni postupak po privatnoj tužbi", tarifni_broj: "Tar. br. 21", opis: "Zahtjev za izvanredno preispitivanje i prijedlog za obnovu — kao žalba", izracun: { vrsta: "udio", odredba: "tbr21_zalba", faktor: 1 } },

  // ── Poglavlje VI. — stečaj i likvidacija ──
  { id: "tbr22_prijedlog", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 22", opis: "Prijedlog za otvaranje predstečajnog postupka", izracun: { vrsta: "fiksno", iznos: 280 } },
  { id: "tbr22_zalba", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 22", opis: "Žalba u predstečajnom postupku", izracun: { vrsta: "fiksno", iznos: 560 } },
  { id: "tbr23_st1", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 23 st. 1", opis: "Prijedlog za otvaranje stečajnog ili likvidacijskog postupka", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr23_st2", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 23 st. 2", opis: "Prijava potraživanja — 2 % vrijednosti tražbine, najviše 140,00", izracun: { vrsta: "postotak", stopa: 0.02, max: 140 }, osnovica_opis: "vrijednost tražbine" },
  { id: "tbr23_zalba", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 23", opis: "Žalba — dvostruko od st. 2", izracun: { vrsta: "udio", odredba: "tbr23_st2", faktor: 2 }, osnovica_opis: "vrijednost tražbine" },
  { id: "tbr24", poglavlje: "VI. Stečaj i likvidacija", tarifni_broj: "Tar. br. 24", opis: "Paušalna pristojba — 2 % diobne mase, najviše 560,00", izracun: { vrsta: "postotak", stopa: 0.02, max: 560 }, osnovica_opis: "diobna masa" },

  // ── Poglavlje VII. — sudski registar ──
  { id: "tbr25_prijava", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 25", opis: "Prijava za upis u sudski registar", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr25_jdoo", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 25", opis: "Prijava za upis — j.d.o.o.", izracun: { vrsta: "fiksno", iznos: 10 } },
  { id: "tbr25_enotar", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 25", opis: "Prijava putem sustava e-Notar ili START — polovica", izracun: { vrsta: "udio", odredba: "tbr25_prijava", faktor: 0.5 } },
  { id: "tbr26_osnivanje", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Upis osnivanja", izracun: { vrsta: "fiksno", iznos: 80 } },
  { id: "tbr26_osnivanje_jdoo", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Upis osnivanja — j.d.o.o.", izracun: { vrsta: "fiksno", iznos: 10 } },
  { id: "tbr26_promjena", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Upis promjene podataka", izracun: { vrsta: "fiksno", iznos: 40 } },
  { id: "tbr26_promjena_jdoo", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Upis promjene podataka — j.d.o.o.", izracun: { vrsta: "fiksno", iznos: 10 } },
  { id: "tbr26_fizicki", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Podnesak u fizičkom obliku", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr26_enotar", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 26", opis: "Upis putem sustava e-Notar ili START — polovica", izracun: { vrsta: "udio", odredba: "tbr26_osnivanje", faktor: 0.5 } },
  { id: "tbr27_prijava", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 27", opis: "Prijava za upis podružnice", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr27_upis", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 27", opis: "Upis podružnice", izracun: { vrsta: "fiksno", iznos: 70 } },
  { id: "tbr27_promjena", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 27", opis: "Upis promjene podataka o podružnici", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr28_preslika", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 28", opis: "Preslika iz registra — po stranici", izracun: { vrsta: "fiksno", iznos: 1 } },
  { id: "tbr28_potvrda", poglavlje: "VII. Sudski registar", tarifni_broj: "Tar. br. 28", opis: "Potvrda iz registra", izracun: { vrsta: "fiksno", iznos: 3 } },

  // ── Poglavlje VIII. — upravni spor ──
  // Fiksni iznosi. Tar. br. 1 se ovdje NE primjenjuje.
  { id: "tbr29_tuzba", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29", opis: "Tužba u upravnom sporu", izracun: { vrsta: "fiksno", iznos: 110 } },
  { id: "tbr29_obnova", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29", opis: "Prijedlog za obnovu upravnog spora", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr29_opci_akti", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29", opis: "Prijedlog za ocjenu zakonitosti općih akata", izracun: { vrsta: "fiksno", iznos: 560 } },
  { id: "tbr29_vus", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29", opis: "Podnesak pred Visokim upravnim sudom", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr29_zalba", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29", opis: "Žalba protiv prvostupanjske presude ili rješenja", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr30_odluka", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30", opis: "Presuda i rješenje u upravnom sporu", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr30_obnova", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30", opis: "Odluka o obnovi upravnog spora", izracun: { vrsta: "fiksno", iznos: 140 } },
  { id: "tbr30_vus", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30", opis: "Prvostupanjska odluka Visokog upravnog suda", izracun: { vrsta: "fiksno", iznos: 140 } },

  // ── Poglavlje IX. — ostali slučajevi ──
  { id: "tbr31_podnesak", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 31", opis: "Podnesak kojim se traži uvjerenje", izracun: { vrsta: "fiksno", iznos: 3 } },
  { id: "tbr31_uvjerenje", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 31", opis: "Uvjerenje", izracun: { vrsta: "fiksno", iznos: 6 } },
  { id: "tbr31_egradani", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 31", opis: "Uvjerenje zatraženo putem sustava e-Građani — polovica", izracun: { vrsta: "udio", odredba: "tbr31_uvjerenje", faktor: 0.5 } },
  { id: "tbr32", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 32", opis: "Ovjera ugovora o doživotnom ili dosmrtnom uzdržavanju", izracun: { vrsta: "fiksno", iznos: 80 } },
  { id: "tbr33", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 33", opis: "Razmatranje sudskog spisa", izracun: { vrsta: "fiksno", iznos: 30 } },
  {
    id: "tbr34",
    poglavlje: "IX. Ostali slučajevi",
    tarifni_broj: "Tar. br. 34",
    opis: "Prijedlog o pologu",
    izracun: { vrsta: "ljestvica_korak", razredi: [], prag: 0, baza: 30, korak: 130, poKoraku: 6, max: 560 },
    osnovica_opis: "vrijednost pologa",
  },
  { id: "tbr35_pohrana", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 35", opis: "Pohrana opće punomoći", izracun: { vrsta: "fiksno", iznos: 30 } },
  { id: "tbr35_odluka", poglavlje: "IX. Ostali slučajevi", tarifni_broj: "Tar. br. 35", opis: "Odluka o pohrani opće punomoći", izracun: { vrsta: "fiksno", iznos: 60 } },
];

/**
 * NN 37/2023 — namjerno samo ono što čl. 2 nove Uredbe traži: Tar. br. 1 (odgovor na
 * tužbu upućuje na st. 2, a on na st. 1) i cijelo Poglavlje VIII. Ostatak mrtvog
 * propisa ne držimo u kodu.
 */
export const PRISTOJBE_NN37_2023: PristojbenaOdredba[] = [
  {
    id: "tbr1_st1",
    poglavlje: "I. Parnični i ovršni postupak",
    tarifni_broj: "Tar. br. 1 st. 1",
    opis: "Tužba, protutužba, prigovor protiv platnog naloga i prigovor protiv rješenja o ovrsi",
    izracun: {
      vrsta: "ljestvica_postotak",
      razredi: [
        { do: 398.17, iznos: 13.27 },
        { do: 796.34, iznos: 26.54 },
        { do: 1_194.51, iznos: 39.82 },
        { do: 1_592.67, iznos: 53.09 },
        { do: 1_990.84, iznos: 66.36 },
      ],
      prag: 1_990.84,
      baza: 66.36,
      stopa: 0.01,
      max: 663.61,
    },
    osnovica_opis: VPS_OSNOVICA,
  },
  { id: "tbr1_st2", poglavlje: "I. Parnični i ovršni postupak", tarifni_broj: "Tar. br. 1 st. 2", opis: "Odgovor na tužbu i ostali podnesci iz st. 2 — polovica pristojbe iz st. 1", izracun: { vrsta: "udio", odredba: "tbr1_st1", faktor: 0.5 }, osnovica_opis: VPS_OSNOVICA },
  { id: "tbr29_tuzba", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29 st. 1", opis: "Tužba u upravnom sporu", izracun: { vrsta: "fiksno", iznos: 53.09 } },
  { id: "tbr29_obnova", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29 st. 2", opis: "Prijedlog za obnovu upravnog spora", izracun: { vrsta: "fiksno", iznos: 66.36 } },
  { id: "tbr29_opci_akti", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29 st. 3", opis: "Prijedlog o ocjeni zakonitosti općih akata", izracun: { vrsta: "fiksno", iznos: 265.45 } },
  { id: "tbr29_vus", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29 st. 4", opis: "Podnesak kojim se pokreće postupak pred Visokim upravnim sudom", izracun: { vrsta: "fiksno", iznos: 66.36 } },
  { id: "tbr29_zalba", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 29 st. 5", opis: "Žalba protiv prvostupanjske presude ili rješenja u upravnom sporu", izracun: { vrsta: "fiksno", iznos: 66.36 } },
  { id: "tbr30_odluka", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30 st. 1", opis: "Presuda i rješenje doneseno u upravnom sporu", izracun: { vrsta: "fiksno", iznos: 66.36 } },
  { id: "tbr30_obnova", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30 st. 2", opis: "Odluka o prijedlogu za obnovu upravnog spora", izracun: { vrsta: "fiksno", iznos: 66.36 } },
  { id: "tbr30_vus", poglavlje: "VIII. Upravni spor", tarifni_broj: "Tar. br. 30 st. 3", opis: "Prvostupanjska odluka Visokog upravnog suda", izracun: { vrsta: "fiksno", iznos: 66.36 } },
];

const ODREDBE_PO_ID: Record<UredbaVerzija, Map<string, PristojbenaOdredba>> = {
  "NN 50/2026": new Map(PRISTOJBE_NN50_2026.map((o) => [o.id, o])),
  "NN 37/2023": new Map(PRISTOJBE_NN37_2023.map((o) => [o.id, o])),
};

const REDOSLIJED = new Map(PRISTOJBE_NN50_2026.map((o, i) => [o.id, i]));

/** Odredba po ključu iz tablice tražene verzije. */
export function pristojbenaOdredba(
  odredbaId: string,
  verzija: UredbaVerzija = "NN 50/2026",
): PristojbenaOdredba | undefined {
  return ODREDBE_PO_ID[verzija].get(odredbaId);
}

// ── Katalog tarife → pristojbena radnja ───────────────────────────
//
// Zrcalo Backend/Services/Pristojbe/PristojbeneRadnjeMapa.cs. Stavka koje ovdje nema
// ne povlači pristojbu — Tar. br. 1 napomena 5 ("ne plaća se pristojba za podneske
// koji nisu navedeni"), a ročišta, savjeti i zastupanje pred upravnim tijelima uopće
// nisu sudske radnje.

export const PRISTOJBENE_RADNJE: Record<string, string[]> = {
  par_tuzba: ["tbr1_st1"],
  par_tuzba_smetanje: ["tbr1_st1"],
  par_tuzba_obiteljski: ["tbr1_st1"],
  par_zahtjev_haska: ["tbr1_st1"],

  par_tuzba_platni_nalog: ["tbr1_st2"],
  par_tuzba_vrijednosni_papiri: ["tbr1_st2"],
  par_povrat_u_prijasnje_stanje: ["tbr1_st2"],

  par_odgovor_tuzba: ["tbr1_st2"],
  par_odgovor_zalba: ["tbr1_st2"],
  par_odgovor_zalba_jednokratno: ["tbr1_st2"],

  par_rociste_presuda: ["tbr2_st1"],

  par_zalba_presuda: ["tbr3_st1"],
  par_zalba_jednokratna: ["tbr3_st1"],
  par_zalba_mali_spor: ["tbr3_st1"],
  par_revizija: ["tbr3_st1"],
  par_revizija_jednokratna: ["tbr3_st1"],
  par_revizija_mali_spor: ["tbr3_st1"],
  par_zalba_rjesenje: ["tbr3_st2"],

  ovr_prijedlog: ["tbr1_st2"],
  ovr_odgoda: ["tbr1_st3"],
  ovr_pravni_lijek: ["tbr3_st2"],

  izv_dioba: ["tbr5"],
  izv_meda: ["tbr7_prijedlog"],
  izv_neprocjenjivi: ["tbr4_prijedlog"],
  izv_amortizacija: ["tbr4_prijedlog"],
  izv_pravni_lijek: ["tbr4_zalba"],

  zk_redoviti: ["tbr14_upis"],
  zk_redoviti_neproc: ["tbr14_upis"],
  zk_posebni: ["tbr14_ispravni"],
  zk_pravni_lijek: ["tbr16"],

  ste_prijedlog: ["tbr23_st1"],
  ste_prijava_potrazivanja: ["tbr23_st2"],
  ste_pravni_lijek: ["tbr23_zalba"],

  reg_upis: ["tbr25_prijava"],
  reg_ponistaj_tuzba_sudski: ["tbr1_st1"],
  reg_ponistaj_lijek_presuda_sudski: ["tbr3_st1"],
  reg_ponistaj_lijek_rjesenje_sudski: ["tbr3_st2"],
  reg_ponistaj_tuzba_drugi_proc: ["tbr29_tuzba"],
  reg_ponistaj_tuzba_drugi_neproc: ["tbr29_tuzba"],
  reg_ponistaj_lijek_presuda_drugi_proc: ["tbr29_zalba"],
  reg_ponistaj_lijek_presuda_drugi_neproc: ["tbr29_zalba"],
  reg_ponistaj_lijek_rjesenje_drugi_proc: ["tbr29_zalba"],
  reg_ponistaj_lijek_rjesenje_drugi_neproc: ["tbr29_zalba"],

  usp_tuzba_proc: ["tbr29_tuzba"],
  usp_tuzba_neproc: ["tbr29_tuzba"],
  usp_zalba: ["tbr29_zalba"],
  usp_zalba_neproc: ["tbr29_zalba"],
};

/**
 * Stavke koje znače "odgovor na tužbu" u smislu čl. 2. Odgovor na žalbu ondje nije
 * spomenut, pa mu stara Uredba ne pripada ni kad je podnesen prije 1.6.2026.
 */
export const ITEMI_ODGOVOR_NA_TUZBU = new Set(["par_odgovor_tuzba"]);

// ── Izračun ───────────────────────────────────────────────────────

const MAX_DUBINA_UPUTE = 8;

// Zaokružuje `zaokruziNovac` iz izračuna nagrade: dvije decimale, od nule a ne
// bankarski (38,2258 → 38,23). Zaokružuje se jednom, na kraju cijelog lanca uputa —
// "polovica pristojbe iz st. 1" računa se iz nezaokruženog st. 1.

function nezaokruzeno(
  odredbaId: string,
  osnovica: number,
  verzija: UredbaVerzija,
  preostalaDubina: number,
): number {
  if (preostalaDubina <= 0) {
    throw new Error(`Upute između odredbi vrte se u krug kod "${odredbaId}".`);
  }

  const odredba = pristojbenaOdredba(odredbaId, verzija);
  if (!odredba) return 0;

  const iz = odredba.izracun;

  switch (iz.vrsta) {
    case "ne_placa_se":
      return 0;

    case "fiksno":
      return iz.iznos;

    case "udio": {
      const iznos = nezaokruzeno(iz.odredba, osnovica, verzija, preostalaDubina - 1) * iz.faktor;
      return iz.max === undefined ? iznos : Math.min(iznos, iz.max);
    }

    case "ljestvica_postotak": {
      if (osnovica <= 0) return 0;
      for (const r of iz.razredi) {
        if (osnovica <= r.do) return r.iznos;
      }
      return Math.min(iz.baza + iz.stopa * (osnovica - iz.prag), iz.max);
    }

    case "ljestvica_korak": {
      if (osnovica <= 0) return iz.razredi.length > 0 ? 0 : iz.baza;
      for (const r of iz.razredi) {
        if (osnovica <= r.do) return r.iznos;
      }
      // "na svakih ZAPOČETIH" — broj koraka se zaokružuje naviše.
      const koraka = Math.ceil((osnovica - iz.prag) / iz.korak);
      return Math.min(iz.baza + koraka * iz.poKoraku, iz.max);
    }

    case "postotak":
      return osnovica <= 0 ? 0 : Math.min(osnovica * iz.stopa, iz.max);

    case "stepenasto": {
      for (const s of iz.stepenice) {
        if (osnovica <= s.do) return s.iznos;
      }
      return iz.stepenice.length > 0 ? iz.stepenice[iz.stepenice.length - 1].iznos : 0;
    }
  }
}

/** Iznos jedne odredbe uz zadanu osnovicu i verziju Uredbe. */
export function izracunajPristojbu(
  odredbaId: string,
  osnovica: number,
  verzija: UredbaVerzija = "NN 50/2026",
): number {
  return zaokruziNovac(nezaokruzeno(odredbaId, osnovica, verzija, MAX_DUBINA_UPUTE));
}

/** Ovisi li iznos o osnovici — fiksni iznosi je ne koriste, pa se uz njih ne prikazuje. */
function koristiOsnovicu(odredbaId: string, verzija: UredbaVerzija, dubina = MAX_DUBINA_UPUTE): boolean {
  const odredba = pristojbenaOdredba(odredbaId, verzija);
  if (!odredba || dubina <= 0) return false;

  const iz = odredba.izracun;
  if (iz.vrsta === "fiksno" || iz.vrsta === "ne_placa_se") return false;
  if (iz.vrsta === "udio") return koristiOsnovicu(iz.odredba, verzija, dubina - 1);
  return true;
}

export interface PristojbenaStavka {
  odredba_id: string;
  tarifni_broj: string;
  poglavlje: PristojbaPoglavlje;
  opis: string;
  osnovica: number;
  kolicina: number;
  iznos_po_jedinici: number;
  iznos: number;
  uredba: UredbaVerzija;
  pravni_izvor: string;
  /** True kad je redak izveden iz odabranih stavki tarife, false kad je dodan ručno. */
  automatski: boolean;
}

export interface RucnaPristojba {
  odredba_id: string;
  osnovica?: number | null;
  kolicina?: number;
}

export interface PristojbeUlaz {
  vrijednostSpora: number;
  odabraneStavke: TarifnaStavka[];
  oslobodjen: boolean;
  /** Čl. 2, prvi slučaj — ISO datum (YYYY-MM-DD) ili null. */
  datumOdgovoraNaTuzbu?: string | null;
  /** Čl. 2, drugi slučaj — ISO datum (YYYY-MM-DD) ili null. */
  datumPokretanjaUpravnogSpora?: string | null;
  dodane?: RucnaPristojba[];
  iskljucene?: string[];
  /**
   * Oslobođenja po Zakonu o sudskim pristojbama, čl. 11–13. Kad izostane, vrijedi
   * samo grubi prekidač `oslobodjen`.
   */
  oslobodjenje?: OslobodjenjeUlaz;
}

export interface PristojbeRezultat {
  stavke: PristojbenaStavka[];
  ukupno: number;
  oslobodjen: boolean;
  /** Čl. 12 — dio VPS-a na koji je pristojba stvarno obračunata. */
  vpsUdio: number;
  /** Čl. 13 — pristojba koju umjesto oslobođene stranke plaća protivna, razmjerno uspjehu. */
  naTeretProtustranke: number;
}

function prijeNoveUredbe(datum: string | null | undefined): boolean {
  return typeof datum === "string" && datum.length > 0 && datum < NOVA_UREDBA_NA_SNAZI;
}

/**
 * Čl. 2 NN 50/2026 — verzija se bira po RETKU, ne po troškovniku, pa predmet sa starim
 * odgovorom na tužbu i novom žalbom nosi jedan redak po svakoj Uredbi.
 */
function razrijesiVerziju(
  odredbaId: string,
  izvorniItemId: string | null,
  ulaz: PristojbeUlaz,
): UredbaVerzija {
  const jeOdgovorNaTuzbu =
    izvorniItemId !== null && ITEMI_ODGOVOR_NA_TUZBU.has(izvorniItemId);
  const jeUpravniSpor = pristojbenaOdredba(odredbaId)?.poglavlje === "VIII. Upravni spor";

  const prijelazni =
    (jeOdgovorNaTuzbu && prijeNoveUredbe(ulaz.datumOdgovoraNaTuzbu)) ||
    (jeUpravniSpor && prijeNoveUredbe(ulaz.datumPokretanjaUpravnogSpora));

  // Stara je tablica namjerno djelomična. Odredba koje u njoj nema ostaje na novoj
  // Uredbi umjesto da ispadne iz zbroja.
  if (prijelazni && pristojbenaOdredba(odredbaId, "NN 37/2023")) {
    return "NN 37/2023";
  }

  return "NN 50/2026";
}

function pravniIzvor(odredba: PristojbenaOdredba, verzija: UredbaVerzija): string {
  const naziv =
    verzija === "NN 37/2023"
      ? "Uredba o Tarifi sudskih pristojbi (NN 37/2023) — čl. 2 NN 50/2026"
      : "Uredba o Tarifi sudskih pristojbi (NN 50/2026)";

  return `${naziv}, ${odredba.tarifni_broj}`;
}

/**
 * Cijeli obračun sudskih pristojbi. Zrcali `SudskePristojbeCalculator.Obracunaj`.
 *
 * Ista se odredba naplaćuje jednom i kad na nju vode dvije tarifne stavke — tužba i
 * tužba zbog smetanja posjeda i dalje su jedan podnesak.
 */
export function obracunajPristojbe(ulaz: PristojbeUlaz): PristojbeRezultat {
  // Čl. 11–13. Kad oslobođenje nije prošireno, stari prekidač i dalje vrijedi.
  const oslobodjenje: OslobodjenjeUlaz = ulaz.oslobodjenje ?? {
    ...OSLOBODJENJE_NEMA,
    oslobodjen: ulaz.oslobodjen,
  };

  const potpuno = sviOslobodjeni(oslobodjenje);
  const vpsUdio = vpsNeoslobodjenih(ulaz.vrijednostSpora, oslobodjenje);

  const iskljucene = new Set(ulaz.iskljucene ?? []);
  const vidjene = new Set<string>();
  const radnje: { odredbaId: string; itemId: string | null; rucna: RucnaPristojba | null }[] = [];

  for (const rucna of ulaz.dodane ?? []) {
    if (iskljucene.has(rucna.odredba_id) || vidjene.has(rucna.odredba_id)) continue;
    vidjene.add(rucna.odredba_id);
    radnje.push({ odredbaId: rucna.odredba_id, itemId: null, rucna });
  }

  for (const stavka of ulaz.odabraneStavke) {
    for (const odredbaId of PRISTOJBENE_RADNJE[stavka.id] ?? []) {
      if (iskljucene.has(odredbaId) || vidjene.has(odredbaId)) continue;
      vidjene.add(odredbaId);
      radnje.push({ odredbaId, itemId: stavka.id, rucna: null });
    }
  }

  const stavke: PristojbenaStavka[] = [];

  for (const { odredbaId, itemId, rucna } of radnje) {
    const verzija = razrijesiVerziju(odredbaId, itemId, ulaz);
    const odredba = pristojbenaOdredba(odredbaId, verzija);

    // Backend na nepoznat ključ baca TroskovnikValidationException; ovdje se redak
    // preskače jer bi iznimka u živom pregledu srušila ekran umjesto da javi grešku.
    // Sučelje nudi samo ključeve iz tablice, pa je ovo obrana od nemogućeg stanja.
    if (!odredba) continue;

    // Čl. 12 umanjuje OSNOVICU, ne gotov iznos — Tar. br. 1 je progresivan.
    const osnovica = rucna?.osnovica ?? vpsUdio;
    const kolicina = Math.max(1, rucna?.kolicina ?? 1);
    const poJedinici = potpuno ? 0 : izracunajPristojbu(odredbaId, osnovica, verzija);

    stavke.push({
      odredba_id: odredbaId,
      tarifni_broj: odredba.tarifni_broj,
      poglavlje: odredba.poglavlje,
      opis: odredba.opis,
      osnovica: koristiOsnovicu(odredbaId, verzija) ? osnovica : 0,
      kolicina,
      iznos_po_jedinici: poJedinici,
      iznos: poJedinici * kolicina,
      uredba: verzija,
      pravni_izvor: pravniIzvor(odredba, verzija),
      automatski: rucna === null,
    });
  }

  stavke.sort(
    (a, b) =>
      (REDOSLIJED.get(a.odredba_id) ?? Number.MAX_SAFE_INTEGER) -
      (REDOSLIJED.get(b.odredba_id) ?? Number.MAX_SAFE_INTEGER),
  );

  // Čl. 13 — pristojba koju bi platili oslobođeni, na istim odredbama, razmjerno
  // njihovu uspjehu. Ne ulazi u `ukupno`: plaća je protivna stranka, po pravomoćnosti.
  const postotak = oslobodjenje.postotakUspjeha ?? 0;
  const vpsOslobodjenih = ulaz.vrijednostSpora - vpsUdio;

  let naTeretProtustranke = 0;
  if (postotak > 0 && vpsOslobodjenih > 0) {
    const pristojbaOslobodjenih = radnje.reduce((zbroj, { odredbaId, itemId, rucna }) => {
      const verzija = razrijesiVerziju(odredbaId, itemId, ulaz);
      if (!pristojbenaOdredba(odredbaId, verzija)) return zbroj;

      const osnovica = rucna?.osnovica ?? vpsOslobodjenih;
      const kolicina = Math.max(1, rucna?.kolicina ?? 1);
      return zbroj + izracunajPristojbu(odredbaId, osnovica, verzija) * kolicina;
    }, 0);

    naTeretProtustranke = zaokruziNovac(
      (pristojbaOslobodjenih * Math.min(Math.max(postotak, 0), 100)) / 100,
    );
  }

  return {
    stavke,
    ukupno: stavke.reduce((zbroj, s) => zbroj + s.iznos, 0),
    oslobodjen: potpuno,
    vpsUdio,
    naTeretProtustranke,
  };
}

/**
 * Objašnjenje kad pristojbi uopće nema — "0,00 €" bez razloga izgleda kao greška
 * u računu, a ne kao ispravan ishod.
 */
export function objasnjenjeBezPristojbe(): string {
  return "Uredba NN 50/2026 za odabrane radnje ne propisuje sudsku pristojbu.";
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
