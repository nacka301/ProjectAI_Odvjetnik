"use client";

import { useCallback, useEffect, useMemo, useRef, useState, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import {
  ArrowLeft,
  Calculator,
  ChevronDown,
  ChevronRight,
  ChevronUp,
  ClipboardList,
  Download,
  Euro,
  FileText,
  Loader2,
  Plus,
  Printer,
  Save,
  Scale,
  Search,
  Trash2,
  User,
  Users,
  Building2,
  AlertCircle,
  CheckCircle,
  Info,
  X,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import { getTroskovnik } from "@/lib/supabase";
import { useVodic } from "@/contexts/VodicContext";
import { DEMO_ZASTO_NEAKTIVNO, useDemo } from "@/contexts/DemoContext";
import { DEMO_STRANKE, DEMO_TROSKOVNIK, DEMO_URED } from "@/lib/demo/podaci";
import { clearDraft, loadDraft, saveDraft, type LoadedDraft } from "@/lib/troskovnik-draft";
import { formatThreshold } from "@/lib/format";
import { naslutiTipStranke, type TipStranke } from "@/lib/stranke-tipovi";
import TroskovnikIspis from "@/components/troskovnik/TroskovnikIspis";
import PrijavaGreskeDialog from "@/components/troskovnik/PrijavaGreskeDialog";
import StrankaPicker from "@/components/troskovnik/StrankaPicker";
import { buildIzvozModel, usporediPoDatumu } from "@/lib/troskovnik-izvoz-model";
import { STUPCI, buildTroskovnikSheet, nazivDatoteke } from "@/lib/troskovnik-xlsx";
import {
  BOD_VALUE,
  KATEGORIJA_KAZNENI,
  TBR_7_1_RAZREDI,
  ZAPRIJECENA_KAZNA_TIERS,
  formatirajBodove,
  izracunajBodove,
  jeSamoKazneni,
  izracunajKazneneBodove,
  obracunajPristojbe,
  objasnjenjeBezPristojbe,
  PRISTOJBE_NN50_2026,
  razrijesiVps,
  OSLOBODJENJE_KATEGORIJE,
  OSLOBODJENJE_NAPOMENA_LOKALNA,
  OSNOVICA_TBR7,
  OSNOVICA_LABELE,
  PRAVNI_LIJEK,
  PRAVNI_LIJEK_LABELE,
  RAZINA_SUDA,
  RAZINA_SUDA_LABELE,
  RENTA_RAZDOBLJE,
  VRSTA_POSTUPKA,
  VRSTA_POSTUPKA_LABELE,
  VRSTA_POSTUPKA_PRAVILA,
  ZAHTJEVI_NACIN,
  ZAHTJEVI_NACIN_LABELE,
  TBR41_MAX_POVISENJE,
  TBR41_MAX_SNIZENJE,
  TBR45_NACIN,
  TBR45_NACIN_LABELE,
  VRSTA_TROSKA,
  VRSTA_TROSKA_LABELE,
  VRSTA_TROSKA_DOPUSTA_IZRACUN,
  NACIN_NAKNADE,
  TBR50_FAKTOR_GORIVA,
  izracunajNaknaduStavku,
  efektivniVps,
  izracunajOsnovicu,
  provjeriZahtjev,
  razrijesiTarifu,
  provjeriTbr41Stavku,
  razrijesiTbr41,
  tbr71Bodovi,
  upozorenjeMaliSpor,
  ZADANA_NAPOMENA,
  type OsnovicaTbr7,
  type PristojbenaStavka,
  type RucnaPristojba,
  type PravniLijek,
  type RazinaSuda,
  type RentaRazdoblje,
  type TarifnaStavka,
  type Tbr41Stavka,
  type Tbr45Nacin,
  type VpsZahtjev,
  type VrstaPostupka,
  type VrstaTroska,
  type ZahtjeviNacin,
  type NacinNaknade,
} from "@/lib/tarifa";
import { izracunajTroskovnik } from "@/lib/troskovnik-izracun";

// ── Types (mirror backend models) ──

/**
 * Jedan redak troškovnika: uređeni popis koji korisnik slaže sam (premještanje,
 * brisanje), s vlastitim (opcionalnim) datumom po radnji. Ista tarifna stavka smije
 * se pojaviti više puta — svaki redak je vlastita radnja.
 */
interface RedakTroskovnika {
  /** Stabilan ključ za React i premještanje — neovisan o itemId, jer se stavka smije ponoviti. */
  uid: string;
  itemId: string;
  kolicina: number;
  /** "YYYY-MM-DD" ili null — opcionalan, prazan se ne šalje. */
  datum: string | null;
  tbr41: Tbr41Stavka | null;
}

/** Ručno unesen trošak (dodatna pristojba ili predujam) — oba popisa dijele isti oblik. */
interface TrosakRedak {
  uid: string;
  datum: string | null;
  opis: string;
  /** Držimo kao string zbog vezanja na input, kao i ostale novčane vrijednosti u ovoj formi. */
  iznos: string;
}

interface TroskovnikTrosakStavka {
  rbr: number;
  datum?: string | null;
  opis: string;
  iznos: number;
}

interface TroskovnikLineItem {
  rbr: number;
  item_id: string;
  /** Datum radnje — echo iz zahtjeva. Prazan kad nije unesen. */
  datum?: string | null;
  opis: string;
  tarifni_broj: string;
  /** Propis i tarifni broj iz kojeg redak proizlazi. */
  pravni_izvor?: string;
  kolicina: number;
  bodova_po_jedinici: number;
  ukupno_bodova: number;
  iznos_eur: number;
  /** Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1 — ukupno ≠ bodova_po_jedinici × kolicina. */
  is_jednokratna?: boolean;
  // Tbr. 41 po pojedinoj stavci
  tbr41_povisenje_postotak?: number;
  tbr41_snizenje_postotak?: number;
  tbr41_bodovi?: number;
  tbr41_pravni_izvor?: string;
}

interface TroskovnikResponse {
  stavke: TroskovnikLineItem[];
  ukupno_bodova: number;
  /** 0 u kaznenom postupku — VPS se tamo ne koristi. */
  vrijednost_spora?: number;
  /** True kad su odabrane isključivo kaznene/prekršajne stavke. */
  is_samo_kazneni?: boolean;
  // Tbr. 40 — zastupanje više osoba
  broj_zastupanih_osoba?: number;
  povisenje_vise_osoba_postotak?: number;
  povisenje_vise_osoba_bodovi?: number;
  bodovi_po_osobi?: number;
  nagrada_po_osobi?: number;
  // Osnovica po Tbr. 7. / Tbr. 45
  osnovica_tbr7?: string;
  osnovica_bodovi?: number;
  osnovica_pravni_izvor?: string;
  razina_suda?: string | null;
  tbr45_nacin?: string | null;
  // Tbr. 41
  tbr41_povisenje_postotak?: number;
  tbr41_snizenje_postotak?: number;
  tbr41_bodovi?: number;
  ukupno_eur_bez_pdv: number;
  pdv: number;
  ukupno_eur_s_pdv: number;
  // Naknada troškova (Tbr. 48.-51.) — ulazi u osnovicu (uključena u ukupno_eur_bez_pdv)
  naknada_troskova_stavke?: {
    vrsta: string; opis?: string | null; iznos: number; pravni_izvor: string;
    nacin?: string | null; kolicina?: number | null; jedinicni_iznos?: number | null; cestarina?: number | null;
  }[];
  naknada_troskova_ukupno?: number;
  predujmovi_ukupno?: number;
  vrijednost_boda: number;
  klijent: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  odvjetnik: { ime: string; oib?: string; adresa?: string; grad?: string; iban?: string } | null;
  protustranka?: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  protustranka_polozaj?: string | null;
  is_pdv_ukljucen?: boolean;
  has_vat?: boolean;
  opis_predmeta: string | null;
  datum_izrade: string;
  sud?: string | null;
  broj_predmeta?: string | null;
  // Nested objects (new schema)
  odvjetnik_info?: { naziv?: string; oib?: string; adresa?: string; iban?: string } | null;
  predmet_info?: { sud?: string; poslovni_broj?: string } | null;
  stranke_info?: {
    zastupana_stranka?: { polozaj?: string; naziv?: string; oib?: string; adresa?: string } | null;
    protustranka?: { polozaj?: string; naziv?: string; oib?: string; adresa?: string } | null;
  } | null;
  // Kazneni echo fields
  zaprijecena_kazna?: string | null;
  is_po_sluzbenoj_duznosti?: boolean;
  is_na_teret_proracuna?: boolean;
  is_izabrani_oslobadjajuca?: boolean;
  is_zastupnik_djeteta?: boolean;
  is_stjecaj?: boolean;
  /** Obrazloženje odabranog razreda kod stjecaja (Tbr. 4. toč. 1 al. 6). */
  napomena_stjecaj?: string | null;
  is_oduzimanje_prekrsaj?: boolean;
  vrijednost_oduzimanja?: number;
  // Sudske pristojbe — lista, jer Uredba naplaćuje po radnji
  pristojbene_stavke?: PristojbenaStavka[];
  sudska_pristojba?: number;
  is_oslobodjen_pristojbi?: boolean;
  sveukupni_trosak?: number;
  // Oslobođenja — Zakon o sudskim pristojbama, čl. 11–13
  oslobodjenje_kategorija?: string | null;
  oslobodjenje_naziv?: string;
  broj_oslobodjenih_osoba?: number;
  /** Čl. 12 — dio VPS-a na koji je pristojba obračunata. */
  pristojba_vps_udio?: number;
  /** Čl. 13 — tereti protivnu stranku, ne ulazi u sveukupni trošak. */
  pristojba_na_teret_protustranke?: number;
  postotak_uspjeha?: number | null;
  // Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26
  vps_zakonski?: number;
  vps_vrsta_postupka?: string;
  vps_pravni_izvor?: string;
  vps_napomena?: string | null;
  vps_prilagodjen?: boolean;
  vps_primijeni_na_nagradu?: boolean;
  /** Čl. 16 uz suparničarstvo — zahtjevi koje ovaj troškovnik ne pokriva. */
  zahtjevi_informativno?: { iznos: number; opis?: string | null; primarni?: boolean }[];
  // Ručno dodane pristojbe i predujmovi
  dodatne_pristojbe?: TroskovnikTrosakStavka[];
  ukupno_pristojbe?: number;
  predujmovi?: TroskovnikTrosakStavka[];
  ukupno_predujmovi?: number;
  napomena?: string | null;
}

// ── Uloga odvjetnika u kaznenom postupku ──

/**
 * Pravilnik o visini nagrade za rad odvjetnika prilikom obrana po službenoj dužnosti
 * (NN 22/2022, br. 271) poznaje dvije stope, obje od iste osnovice — nagrade koja bi
 * odvjetniku pripadala za pojedinu radnju prema Tarifi:
 *
 *   čl. 1 — obrana po sl. dužnosti i obrana na teret proračunskih sredstava → 50%
 *   čl. 2 — opunomoćenik djeteta žrtve kaznenog djela                       → 70%
 *
 * Uloge se isključuju: čl. 1 je obrana okrivljenika, čl. 2 zastupanje djeteta žrtve,
 * pa je to jedan izbor, a ne tri neovisne kvačice.
 *
 * "Na teret proračuna" (čl. 1) dalje se grana na dvoje:
 *   - POSTAVLJENI branitelj (po sl. dužnosti ili na teret proračuna kao takav) → 50%
 *   - IZABRANI branitelj, trošak na proračun zbog OSLOBAĐAJUĆE presude        → puna tarifa
 * Isti se odvjetnik ne može istovremeno smatrati postavljenim i izabranim za istu
 * radnju, pa su "proracun" i "proracun_izabrani" dvije zasebne opcije, ne kombinacija.
 */
type KazneniStatus = "puna" | "sluzbena" | "proracun" | "proracun_izabrani" | "dijete";

const KAZNENI_STATUS_OPCIJE: { value: KazneniStatus; label: string; napomena: string }[] = [
  { value: "puna", label: "Puna tarifa", napomena: "bez umanjenja" },
  { value: "sluzbena", label: "Branitelj po službenoj dužnosti", napomena: "50% — Pravilnik NN 22/2022, čl. 1" },
  { value: "proracun", label: "Postavljeni branitelj na teret proračunskih sredstava", napomena: "50% — čl. 1" },
  {
    value: "proracun_izabrani",
    label: "Izabrani branitelj — oslobađajuća presuda (na teret proračuna)",
    napomena: "puna tarifa — bez umanjenja",
  },
  { value: "dijete", label: "Opunomoćenik djeteta žrtve kaznenog djela", napomena: "70% — čl. 2" },
];

/**
 * Jedan redak naknade troškova u obrascu. `iznos`/`kolicina`/`jedinicniIznos`/`cestarina`
 * su stringovi jer dolaze iz teksta obrasca (hrvatski format, parsira se pri izračunu),
 * isto kao svugdje drugdje u ovoj komponenti. `nacin` izostavljen (undefined) = "rucno",
 * isto kao na backendu — stari nacrti bez tog polja ostaju ispravni.
 */
type NaknadaTroskovaRedak = {
  vrsta: VrstaTroska;
  opis: string;
  iznos: string;
  nacin?: NacinNaknade;
  kolicina?: string;
  jedinicniIznos?: string;
  cestarina?: string;
};

// ── Format helpers ──

/** Hrvatski format broja ("1.234,56") → number. Isti obrazac kao vrijednostOduzimanja/rentaIznos. */
function parseHrNumber(s: string): number {
  return parseFloat(s.replace(/\./g, "").replace(",", ".")) || 0;
}

function fmtEur(n: number) {
  return n.toLocaleString("hr-HR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";
}

/** Prikaz bodova — decimalni, jer se bodovi po Tarifi ne zaokružuju. */
function fmtNum(n: number) {
  return formatirajBodove(n);
}

/**
 * Je li redak naknade troškova dovoljno popunjen da ga vrijedi poslati — ručni unos
 * treba pozitivan iznos, izračun treba pozitivnu količinu i jedinični iznos (isti uvjet
 * koji backend provjerava u ProvjeriNaknaduIPredujam, ovdje samo da se prazan redak ne
 * šalje, ne da se odbije zahtjev).
 */
function naknadaStavkaPopunjena(r: NaknadaTroskovaRedak): boolean {
  return r.nacin === NACIN_NAKNADE.Izracun
    ? parseHrNumber(r.kolicina ?? "") > 0 && parseHrNumber(r.jedinicniIznos ?? "") > 0
    : parseHrNumber(r.iznos) > 0;
}

/** Redak naknade troškova u obliku koji backend očekuje (snake_case, brojevi umjesto teksta). */
function naknadaStavkaZaSlanje(r: NaknadaTroskovaRedak) {
  return {
    vrsta: r.vrsta,
    opis: r.opis || undefined,
    iznos: parseHrNumber(r.iznos),
    nacin: r.nacin,
    kolicina: r.kolicina != null ? parseHrNumber(r.kolicina) : undefined,
    jedinicni_iznos: r.jedinicniIznos != null ? parseHrNumber(r.jedinicniIznos) : undefined,
    cestarina: r.cestarina != null ? parseHrNumber(r.cestarina) : undefined,
  };
}

/** Hrvatski unos broja ("1.234,56") → JS number. Prazno ili nevaljano → 0. */
function parseEurInput(s: string): number {
  return parseFloat(s.replace(/\./g, "").replace(",", ".")) || 0;
}

// ── Category icons / colors ──

const CATEGORY_COLORS: Record<string, { bg: string; text: string; border: string }> = {
  "Kazneni postupak": { bg: "bg-red-50", text: "text-red-700", border: "border-red-200" },
  "Parnični postupak": { bg: "bg-blue-50", text: "text-blue-700", border: "border-blue-200" },
  "Ovršni postupak": { bg: "bg-orange-50", text: "text-orange-700", border: "border-orange-200" },
  "Stečajni postupak": { bg: "bg-purple-50", text: "text-purple-700", border: "border-purple-200" },
  "Izvanparnični postupak": { bg: "bg-teal-50", text: "text-teal-700", border: "border-teal-200" },
  "Zemljišnoknjižni postupak": { bg: "bg-amber-50", text: "text-amber-700", border: "border-amber-200" },
  "Upravni postupak": { bg: "bg-indigo-50", text: "text-indigo-700", border: "border-indigo-200" },
  "Upravni spor": { bg: "bg-cyan-50", text: "text-cyan-700", border: "border-cyan-200" },
  "Registarski postupak": { bg: "bg-lime-50", text: "text-lime-700", border: "border-lime-200" },
  "Zaštita pred poslodavcem": { bg: "bg-pink-50", text: "text-pink-700", border: "border-pink-200" },
  "Ustavni sud / ECHR / Sud EU": { bg: "bg-violet-50", text: "text-violet-700", border: "border-violet-200" },
  "Ostali postupci": { bg: "bg-slate-50", text: "text-slate-700", border: "border-slate-200" },
  "Razno": { bg: "bg-emerald-50", text: "text-emerald-700", border: "border-emerald-200" },
  "Zajedničke odredbe": { bg: "bg-stone-50", text: "text-stone-700", border: "border-stone-200" },
};

function getCatColor(cat: string) {
  return CATEGORY_COLORS[cat] ?? { bg: "bg-slate-50", text: "text-slate-700", border: "border-slate-200" };
}

/**
 * Sidra koja žive UNUTAR sažetka na desnoj strani (`lg:sticky` stupac), ne u
 * glavnoj formi. Desni stupac gubi `lg:sticky` SAMO dok korak cilja jedno od
 * ovih: `sticky` element se, jednom prilijepljen na `top-20`, više ne pomiče
 * daljnjim skrolom, pa bi dio stupca ispod ruba ekrana (pristojbe, ukupno,
 * spremi) ostao trajno nedohvatljiv za vodičev algoritam pozicioniranja. Bez
 * `sticky` stupac je običan blok koji se skrola kao svaki drugi cilj.
 *
 * Nema veze s ISTICANJEM cilja — ono ide preko izmjerenih koordinata u
 * `Vodic.tsx`, potpuno neovisno o tome je li ovaj element trenutno sticky.
 */
const SIDRA_U_STUPCU = new Set([
  "vodic-rezultat",
  "vodic-popis-radnji",
  "vodic-bodovi",
  "vodic-pristojbe",
  "vodic-ukupno",
  "vodic-spremi",
]);

// ═══════════════════════════════════════════════════════════════
//  MAIN PAGE COMPONENT
// ═══════════════════════════════════════════════════════════════

export default function TroskovnikPage() {
  return (
    <Suspense fallback={<div className="flex min-h-screen items-center justify-center bg-gray-50"><Loader2 className="h-8 w-8 animate-spin text-indigo-500" /></div>}>
      <TroskovnikContent />
    </Suspense>
  );
}

function TroskovnikContent() {
  const { userId, authHeaders } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const preselectedClientId = searchParams.get("clientId");
  // `?klon=<id>` dolazi s /troskovnik/[id] ("Uredi kao novi") — popunjava formu
  // podacima spremljenog troškovnika, ali NIKAD ne mijenja taj zapis: spremanje
  // ovdje uvijek stvara nov redak. Vidi klonirajZapis niže.
  const klonId = searchParams.get("klon");

  // ── Vodič ──
  //
  // Vodič više ne živi u ovoj stranici. Stoji iznad ruta (`VodicProvider`), jer
  // uvodni vodič ima korak i na /klijenti, a stranica koja bi ga držala pri toj
  // navigaciji prestaje postojati. Odavde se čita samo koje polje treba
  // prstenovati.
  const { target: vodicTarget, aktivan: vodicAktivan } = useVodic();
  const vodicOpen = vodicAktivan !== null;
  /** Trenutni korak cilja nešto UNUTAR desnog stupca — vidi SIDRA_U_STUPCU. */
  const vodicCiljaStupac = vodicOpen && vodicTarget !== null && SIDRA_U_STUPCU.has(vodicTarget);

  // ── Prikaz primjera ──
  // Dok traje, forma je popunjena demo troškovnikom, spremanje/ispis/izvoz su
  // zaključani, a podaci ureda i stranaka dolaze iz `lib/demo/podaci.ts`.
  const { demo } = useDemo();

  // ── Catalog state ──
  const [katalog, setKatalog] = useState<TarifnaStavka[]>([]);
  const [loadingKatalog, setLoadingKatalog] = useState(true);
  const [katalogError, setKatalogError] = useState<string | null>(null);

  // ── Form state ──
  const [vrijednostSpora, setVrijednostSpora] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  // Uređeni popis radnji — korisnik bira redoslijed (premještanje), ista tarifna
  // stavka smije se pojaviti više puta (svaki redak vlastita radnja, vlastiti datum).
  const [redci, setRedci] = useState<RedakTroskovnika[]>([]);
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(new Set());

  // ── Party info ──
  const [odvjetnikIme, setOdvjetnikIme] = useState("");
  const [odvjetnikOib, setOdvjetnikOib] = useState("");
  const [odvjetnikAdresa, setOdvjetnikAdresa] = useState("");
  const [odvjetnikGrad, setOdvjetnikGrad] = useState("");
  const [klijentIme, setKlijentIme] = useState("");
  const [klijentOib, setKlijentOib] = useState("");
  const [klijentAdresa, setKlijentAdresa] = useState("");
  const [klijentGrad, setKlijentGrad] = useState("");
  // Fizička/pravna osoba — bitno samo pri automatskom otvaranju nove stranke iz
  // troškovnika (backend inače sve stvara kao „fizička"). Za stranku odabranu iz
  // baze koristi se njezin postojeći tip. `klijentTipRucno` pamti je li korisnik
  // sam pipnuo izbornik, da mu prijedlog iz naziva to više ne pregazi.
  const [klijentTip, setKlijentTip] = useState<TipStranke>("fizicka");
  const klijentTipRucno = useRef(false);
  const [opisPredmeta, setOpisPredmeta] = useState("");
  const [predmetSud, setPredmetSud] = useState("");
  const [predmetPoslovniBroj, setPredmetPoslovniBroj] = useState("");
  const [showParties, setShowParties] = useState(false);
  // New field for PDV toggle and party data
  // Explicit flag: whether the entity is in the VAT system (preferred by backend)
  const [isPdvUkljucen, setIsPdvUkljucen] = useState(false);
  // True nakon što je korisnik ručno dirao checkbox — sprječava da dohvat postavke
  // ureda kasnije pregazi izbor koji je korisnik već napravio za ovaj dokument.
  const pdvManuallySet = useRef(false);
  const handlePdvToggle = useCallback((checked: boolean) => {
    pdvManuallySet.current = true;
    setIsPdvUkljucen(checked);
  }, []);
  // ── Naknada troškova (Tbr. 48.-51.) — ulazi u PDV osnovicu ──
  const [naknadaTroskovaStavke, setNaknadaTroskovaStavke] = useState<NaknadaTroskovaRedak[]>([]);
  // Cijena goriva (€/l) i dnevnica (€/dan) iz postavki ureda — za izračun Tbr. 50/51.
  // Null dok se ne dohvati ili dok ured nije postavio vrijednost (nepotvrđeno).
  const [cijenaGorivaEurL, setCijenaGorivaEurL] = useState<number | null>(null);
  const [dnevnicaEur, setDnevnicaEur] = useState<number | null>(null);
  const [odvjetnikIban, setOdvjetnikIban] = useState("");
  const [odvjetnikTelefon, setOdvjetnikTelefon] = useState("");
  const [odvjetnikEmail, setOdvjetnikEmail] = useState("");
  // Osobno ime prijavljenog odvjetnika — za potpis, uz naziv ureda. Dolazi iz
  // profila (`user_profiles.full_name`), ne iz obrasca — nije uredivo po
  // troškovniku, isto kao ni login mail.
  const [odvjetnikOsobnoIme, setOdvjetnikOsobnoIme] = useState("");
  const [protustrankaIme, setProtustrankaIme] = useState("");
  const [protustrankaOib, setProtustrankaOib] = useState("");
  const [protustrankaAdresa, setProtustrankaAdresa] = useState("");
  const [protustrankaGrad, setProtustrankaGrad] = useState("");
  const [protustrankaPolozaj, setProtustrankaPolozaj] = useState("");
  const [zastupanaPolozaj, setZastupanaPolozaj] = useState("");

  // ── Kazneni postupak state ──
  const [zaprijecenaKazna, setZaprijecenaKazna] = useState("");
  // Uloga odvjetnika po Pravilniku NN 22/2022 br. 271 je izbor između isključivih
  // mogućnosti, ne skup neovisnih zastavica: čl. 1 je obrana okrivljenika, čl. 2
  // zastupanje djeteta žrtve, a isti odvjetnik ne može u istom postupku biti oboje.
  // Jedno stanje umjesto tri boolean-a čini proturječan odabir nereprezentabilnim,
  // pa se stope ne mogu pomnožiti.
  const [kazneniStatus, setKazneniStatus] = useState<KazneniStatus>("puna");
  const isPoSluzbenojDuznosti = kazneniStatus === "sluzbena";
  // Obje vrijednosti su "na teret proračuna" u pravnom smislu (isti čl. 1), pa se
  // obje šalju kao is_na_teret_proracuna; isIzabraniOslobodjajuca je kvalifikator
  // koji razdvaja postavljenog (50%) od izabranog branitelja u oslobađajućoj (puna).
  const isNaTeretProracuna = kazneniStatus === "proracun" || kazneniStatus === "proracun_izabrani";
  const isIzabraniOslobodjajuca = kazneniStatus === "proracun_izabrani";
  const isZastupnikDjeteta = kazneniStatus === "dijete";
  const [isStjecaj, setIsStjecaj] = useState(false);
  const [isOduzimanjePrekrsaj, setIsOduzimanjePrekrsaj] = useState(false);
  const [vrijednostOduzimanja, setVrijednostOduzimanja] = useState("");

  // ── Sudske pristojbe state ──
  const [isOslobodjenPristojbi, setIsOslobodjenPristojbi] = useState(false);

  // Tbr. 52 — datum dospijeća potraživanja; određuje mjerodavnu verziju Tarife (po
  // dospijeću, ne po datumu radnje). Prazno → trenutno važeća Tarifa.
  const [datumDospijeca, setDatumDospijeca] = useState("");

  // Čl. 2 Uredbe NN 50/2026 — dva prijelazna slučaja u kojima vrijedi stara Uredba.
  // Dva zasebna datuma, jer jedan predmet može imati star odgovor na tužbu i novu žalbu.
  const [datumOdgovoraNaTuzbu, setDatumOdgovoraNaTuzbu] = useState("");
  const [datumPokretanjaUpravnogSpora, setDatumPokretanjaUpravnogSpora] = useState("");

  // Velik dio Uredbe (ovjere, etažiranje, upis broda, polog) nema svoju tarifnu stavku
  // iz koje bi se izveo, a nekoliko stavaka kataloga pokriva dvije odredbe odjednom
  // (dioba/ostavina, stečaj/predstečaj) — zato se lista mora dati urediti.
  const [pristojbeDodane, setPristojbeDodane] = useState<RucnaPristojba[]>([]);
  const [pristojbeIskljucene, setPristojbeIskljucene] = useState<string[]>([]);

  // ── Čl. 7 Zakona o sudskim pristojbama — elektronički oblik ──
  // Tri stavka, tri uvjeta. St. 1 (podnesci) i st. 2 (odluke) idu PO RETKU — u istom
  // predmetu žalba može biti predana elektronički, a tužba na papiru, i rok od 3 dana
  // može se ispuniti za jednu odluku, a ne za drugu. St. 3 (zk upis) je jedan uvjet
  // za cijeli zk postupak, pa ostaje prekidač.
  const [podnesciElektronicki, setPodnesciElektronicki] = useState<string[]>([]);
  const [zkUpisElektronicki, setZkUpisElektronicki] = useState(false);
  const [odlukeElektronicki, setOdlukeElektronicki] = useState<string[]>([]);
  // ── Oslobođenja — Zakon o sudskim pristojbama, čl. 11–13 ──
  const [oslobodjenjeKategorija, setOslobodjenjeKategorija] = useState("");
  const [brojOslobodjenihOsoba, setBrojOslobodjenihOsoba] = useState(0);
  const [udioUVps, setUdioUVps] = useState("");
  const [postotakUspjeha, setPostotakUspjeha] = useState("");

  // ── Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26 ──
  const [vrstaPostupka, setVrstaPostupka] = useState<VrstaPostupka>(VRSTA_POSTUPKA.Opci);
  const [uzdrzavanjeMjesecno, setUzdrzavanjeMjesecno] = useState("");
  const [zahtjevi, setZahtjevi] = useState<VpsZahtjev[]>([]);
  const [zahtjeviNacin, setZahtjeviNacin] = useState<ZahtjeviNacin | null>(null);
  const [pravniLijek, setPravniLijek] = useState<PravniLijek>(PRAVNI_LIJEK.Nema);
  const [pravniLijekIznos, setPravniLijekIznos] = useState("");
  /**
   * Prenosi zakonski VPS i na odvjetničku nagradu po Tbr. 7. toč. 1. Zadano isključeno:
   * čl. 14–26 Zakona pisani su za sudske pristojbe, ne za Tarifu.
   */
  const [vpsPrimijeniNaNagradu, setVpsPrimijeniNaNagradu] = useState(false);

  // ── Tbr. 40 — broj osoba koje odvjetnik zastupa u istom postupku ──
  const [brojZastupanihOsoba, setBrojZastupanihOsoba] = useState(1);

  // ── Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45 ──
  const [osnovicaTbr7, setOsnovicaTbr7] = useState<OsnovicaTbr7>(OSNOVICA_TBR7.Toc1);
  const [razinaSuda, setRazinaSuda] = useState<RazinaSuda | null>(null);
  const [tbr45Nacin, setTbr45Nacin] = useState<Tbr45Nacin | null>(null);
  const [rentaIznos, setRentaIznos] = useState("");
  const [rentaRazdoblje, setRentaRazdoblje] = useState<RentaRazdoblje | null>(null);

  // ── Tbr. 41 — povisivanje i snižavanje POJEDINIH tarifnih stavaka ──
  // Prečac "primijeni na sve": vrijedi za svaki redak koji nema vlastiti postotak
  // (redak.tbr41 === null).
  const [tbr41Povisenje, setTbr41Povisenje] = useState(0);
  const [tbr41Snizenje, setTbr41Snizenje] = useState(0);

  // ── Sudske pristojbe (ručno dodane) i predujmovi ──
  const [dodatnePristojbe, setDodatnePristojbe] = useState<TrosakRedak[]>([]);
  const [predujmovi, setPredujmovi] = useState<TrosakRedak[]>([]);
  const [napomena, setNapomena] = useState(ZADANA_NAPOMENA);

  // ── Client picker state ──
  type ClientOption = { id: string; ime_naziv: string; oib?: string; adresa?: string; grad?: string; tip: string };
  const [clients, setClients] = useState<ClientOption[]>([]);
  const [selectedClientId, setSelectedClientId] = useState<string>("");
  const [loadingClients, setLoadingClients] = useState(false);
  /** True when fields were auto-filled from DB selection (vs manual typing) */
  const [clientAutoFilled, setClientAutoFilled] = useState(false);
  const [clientSearchQuery, setClientSearchQuery] = useState("");
  const [clientDropdownOpen, setClientDropdownOpen] = useState(false);
  const clientContainerRef = useRef<HTMLDivElement>(null);

  // ── Save state ──
  const [saving, setSaving] = useState(false);
  const [savedId, setSavedId] = useState<string | null>(null);
  const [prijavaGreskeOtvorena, setPrijavaGreskeOtvorena] = useState(false);

  // ── Result state ──
  const [result, setResult] = useState<TroskovnikResponse | null>(null);
  const [calculating, setCalculating] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);

  // ═══════════════════════════════════════════════════════════
  //  Nacrt — preživljava odjavu zbog neaktivnosti
  //
  //  Everything the user types, in one object. Deriving the draft TYPE from
  //  this snapshot (`typeof draftSnapshot`) rather than declaring it separately
  //  means restore below is type-checked against the real state — a renamed
  //  field breaks the build instead of silently restoring nothing.
  //
  //  Excluded on purpose: the catalog and client list (fetched), the search box
  //  and expanded categories (view state, and a Set does not survive JSON), and
  //  the computed result (recomputed from the inputs anyway).
  // ═══════════════════════════════════════════════════════════
  const draftSnapshot = useMemo(
    () => ({
      vrijednostSpora, redci, selectedClientId, napomena,
      odvjetnikIme, odvjetnikOib, odvjetnikAdresa, odvjetnikGrad, odvjetnikIban,
      odvjetnikTelefon, odvjetnikEmail,
      klijentIme, klijentOib, klijentAdresa, klijentGrad, klijentTip,
      opisPredmeta, predmetSud, predmetPoslovniBroj, showParties, isPdvUkljucen,
      naknadaTroskovaStavke,
      protustrankaIme, protustrankaOib, protustrankaAdresa, protustrankaGrad,
      protustrankaPolozaj, zastupanaPolozaj,
      zaprijecenaKazna, kazneniStatus, isStjecaj, isOduzimanjePrekrsaj,
      vrijednostOduzimanja,
      isOslobodjenPristojbi, datumDospijeca, datumOdgovoraNaTuzbu, datumPokretanjaUpravnogSpora,
      pristojbeDodane, pristojbeIskljucene,
      podnesciElektronicki, zkUpisElektronicki, odlukeElektronicki,
      oslobodjenjeKategorija, brojOslobodjenihOsoba, udioUVps, postotakUspjeha,
      vrstaPostupka, uzdrzavanjeMjesecno, zahtjevi, zahtjeviNacin,
      pravniLijek, pravniLijekIznos, vpsPrimijeniNaNagradu,
      brojZastupanihOsoba, osnovicaTbr7, razinaSuda, tbr45Nacin,
      rentaIznos, rentaRazdoblje, tbr41Povisenje, tbr41Snizenje,
      dodatnePristojbe, predujmovi,
    }),
    [
      vrijednostSpora, redci, selectedClientId, napomena,
      odvjetnikIme, odvjetnikOib, odvjetnikAdresa, odvjetnikGrad, odvjetnikIban,
      odvjetnikTelefon, odvjetnikEmail,
      klijentIme, klijentOib, klijentAdresa, klijentGrad, klijentTip,
      opisPredmeta, predmetSud, predmetPoslovniBroj, showParties, isPdvUkljucen,
      naknadaTroskovaStavke,
      protustrankaIme, protustrankaOib, protustrankaAdresa, protustrankaGrad,
      protustrankaPolozaj, zastupanaPolozaj,
      zaprijecenaKazna, kazneniStatus, isStjecaj, isOduzimanjePrekrsaj,
      vrijednostOduzimanja,
      isOslobodjenPristojbi, datumDospijeca, datumOdgovoraNaTuzbu, datumPokretanjaUpravnogSpora,
      pristojbeDodane, pristojbeIskljucene,
      podnesciElektronicki, zkUpisElektronicki, odlukeElektronicki,
      oslobodjenjeKategorija, brojOslobodjenihOsoba, udioUVps, postotakUspjeha,
      vrstaPostupka, uzdrzavanjeMjesecno, zahtjevi, zahtjeviNacin,
      pravniLijek, pravniLijekIznos, vpsPrimijeniNaNagradu,
      brojZastupanihOsoba, osnovicaTbr7, razinaSuda, tbr45Nacin,
      rentaIznos, rentaRazdoblje, tbr41Povisenje, tbr41Snizenje,
      dodatnePristojbe, predujmovi,
    ],
  );

  type Nacrt = typeof draftSnapshot;

  const [ponudjeniNacrt, setPonudjeniNacrt] = useState<LoadedDraft<Nacrt> | null>(null);

  /** Worth keeping only once the user has actually put something in. */
  const nacrtVrijedanSpremanja =
    redci.length > 0 || vrijednostSpora.trim() !== "" || klijentIme.trim() !== "";

  // Offer whatever survived, once, on arrival. Deliberately an offer and not an
  // automatic restore: this page can also be opened with ?clientId=… prefilled,
  // and silently overwriting that with an older draft would be its own bug.
  useEffect(() => {
    // U prikazu primjera nema što nuditi: forma je ionako popunjena demo
    // podacima, a ponuda bi ih pregazila usred vodiča.
    if (!userId || demo) return;
    const found = loadDraft<Nacrt>(userId);
    if (found) setPonudjeniNacrt(found);
    // Runs once per user — re-offering after the user dismissed it would make
    // the banner impossible to get rid of.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  // Debounced, so typing does not serialise sixty fields on every keystroke.
  useEffect(() => {
    // Demo podaci ne odlaze ni u sessionStorage. „Ništa od ovoga nije spremljeno
    // u vaš račun" mora vrijediti doslovno, inače bi se izmišljena stranka
    // vratila kao ponuđeni nacrt nakon što vodič odavno završi.
    if (!userId || demo || !nacrtVrijedanSpremanja) return;
    const timer = setTimeout(() => saveDraft(userId, draftSnapshot), 1500);
    return () => clearTimeout(timer);
  }, [userId, demo, draftSnapshot, nacrtVrijedanSpremanja]);

  /** Puts a recovered draft back into the form, field by field. */
  const vratiNacrt = useCallback((d: Nacrt) => {
    setVrijednostSpora(d.vrijednostSpora);
    setRedci(d.redci);
    setSelectedClientId(d.selectedClientId); setNapomena(d.napomena);
    setOdvjetnikIme(d.odvjetnikIme); setOdvjetnikOib(d.odvjetnikOib);
    setOdvjetnikAdresa(d.odvjetnikAdresa); setOdvjetnikGrad(d.odvjetnikGrad);
    setOdvjetnikIban(d.odvjetnikIban);
    setOdvjetnikTelefon(d.odvjetnikTelefon); setOdvjetnikEmail(d.odvjetnikEmail);
    setKlijentIme(d.klijentIme); setKlijentOib(d.klijentOib);
    setKlijentAdresa(d.klijentAdresa); setKlijentGrad(d.klijentGrad);
    // Stariji nacrt (v2) nema tip — složi ga iz naziva. Vraćanje je izričita
    // radnja, pa se prijedlog iz naziva dalje ne miješa.
    setKlijentTip((d as { klijentTip?: TipStranke }).klijentTip ?? naslutiTipStranke(d.klijentIme));
    // Prazan naziv (npr. izlazak iz primjera) — pusti da prijedlog opet radi.
    klijentTipRucno.current = d.klijentIme.trim() !== "";
    setOpisPredmeta(d.opisPredmeta); setPredmetSud(d.predmetSud);
    setPredmetPoslovniBroj(d.predmetPoslovniBroj);
    setShowParties(d.showParties);
    // Vraćanje nacrta je izričita korisnikova radnja — PDV iz nacrta ima prednost
    // pred kasnijim async dohvatom postavki ureda (isto kao ručni klik na checkbox).
    setIsPdvUkljucen(d.isPdvUkljucen); pdvManuallySet.current = true;
    setNaknadaTroskovaStavke(d.naknadaTroskovaStavke);
    setProtustrankaIme(d.protustrankaIme); setProtustrankaOib(d.protustrankaOib);
    setProtustrankaAdresa(d.protustrankaAdresa); setProtustrankaGrad(d.protustrankaGrad);
    setProtustrankaPolozaj(d.protustrankaPolozaj); setZastupanaPolozaj(d.zastupanaPolozaj);
    setZaprijecenaKazna(d.zaprijecenaKazna); setKazneniStatus(d.kazneniStatus);
    setIsStjecaj(d.isStjecaj); setIsOduzimanjePrekrsaj(d.isOduzimanjePrekrsaj);
    setVrijednostOduzimanja(d.vrijednostOduzimanja);
    setIsOslobodjenPristojbi(d.isOslobodjenPristojbi);
    setDatumDospijeca(d.datumDospijeca);
    setDatumOdgovoraNaTuzbu(d.datumOdgovoraNaTuzbu);
    setDatumPokretanjaUpravnogSpora(d.datumPokretanjaUpravnogSpora);
    setPristojbeDodane(d.pristojbeDodane); setPristojbeIskljucene(d.pristojbeIskljucene);
    setPodnesciElektronicki(d.podnesciElektronicki);
    setZkUpisElektronicki(d.zkUpisElektronicki);
    setOdlukeElektronicki(d.odlukeElektronicki);
    setOslobodjenjeKategorija(d.oslobodjenjeKategorija);
    setBrojOslobodjenihOsoba(d.brojOslobodjenihOsoba);
    setUdioUVps(d.udioUVps); setPostotakUspjeha(d.postotakUspjeha);
    setVrstaPostupka(d.vrstaPostupka); setUzdrzavanjeMjesecno(d.uzdrzavanjeMjesecno);
    setZahtjevi(d.zahtjevi); setZahtjeviNacin(d.zahtjeviNacin);
    setPravniLijek(d.pravniLijek); setPravniLijekIznos(d.pravniLijekIznos);
    setVpsPrimijeniNaNagradu(d.vpsPrimijeniNaNagradu);
    setBrojZastupanihOsoba(d.brojZastupanihOsoba);
    setOsnovicaTbr7(d.osnovicaTbr7); setRazinaSuda(d.razinaSuda);
    setTbr45Nacin(d.tbr45Nacin);
    setRentaIznos(d.rentaIznos); setRentaRazdoblje(d.rentaRazdoblje);
    setTbr41Povisenje(d.tbr41Povisenje); setTbr41Snizenje(d.tbr41Snizenje);
    setDodatnePristojbe(d.dodatnePristojbe); setPredujmovi(d.predujmovi);
    setPonudjeniNacrt(null);
  }, []);

  // ═══════════════════════════════════════════════════════════
  //  "Uredi kao novi" — popuni formu iz spremljenog troškovnika (?klon=<id>)
  //
  //  /troskovnik/[id] je namjerno samo za čitanje: taj zapis je možda već predan
  //  sudu, i ponovni izračun po današnjem katalogu mogao bi tiho dati drukčiji
  //  iznos od onoga na papiru (vidi komentar na vrhu te stranice). Zato ovo NE
  //  otvara zapis na uređivanje — puni ovu, praznu formu njegovim podacima, kao
  //  polazište za nov troškovnik. Spremanje ovdje uvijek stvara nov redak u
  //  bazi; izvorni zapis ostaje netaknut.
  //
  //  Ne uspijeva li se nešto pouzdano pročitati natrag (npr. ručno dodane
  //  pristojbe, ili polja koja odgovor ne vraća poput "pravni lijek"), polje
  //  ostaje na svom praznom početnom stanju — bolje prazno nego pogrešno
  //  popunjeno u pravnom dokumentu.
  // ═══════════════════════════════════════════════════════════
  const [klonInfo, setKlonInfo] = useState<{ datum: string } | null>(null);
  const [klonGreska, setKlonGreska] = useState<string | null>(null);
  const klonPrimijenjenRef = useRef(false);

  /** HR broj-string za unos ("1234,5"), bez tisućnog razdjelnika. */
  const formatUnosa = useCallback((n: number | null | undefined): string => {
    if (n === null || n === undefined || n === 0) return "";
    return Number.isInteger(n) ? String(n) : n.toFixed(2).replace(".", ",");
  }, []);

  useEffect(() => {
    if (!klonId || !userId || klonPrimijenjenRef.current) return;
    klonPrimijenjenRef.current = true;

    (async () => {
      const zapis = await getTroskovnik(klonId);
      if (!zapis) {
        setKlonGreska("Troškovnik za kloniranje nije pronađen.");
        router.replace("/troskovnik");
        return;
      }

      // raw_json_output je točno ono što je backend izračunao i spremio u
      // trenutku spremanja — echo gotovo svih polja zahtjeva, pa je vjerniji
      // izvor od uskih stupaca tablice.
      const out = (zapis.raw_json_output ?? {}) as Record<string, unknown>;
      const num = (v: unknown): number | undefined =>
        typeof v === "number" ? v : undefined;
      const str = (v: unknown): string | undefined =>
        typeof v === "string" ? v : undefined;
      const bool = (v: unknown): boolean | undefined =>
        typeof v === "boolean" ? v : undefined;

      const izOut = out as {
        stavke?: {
          item_id: string; kolicina: number; datum?: string | null;
          tbr41_povisenje_postotak?: number; tbr41_snizenje_postotak?: number;
        }[];
        klijent?: { ime?: string; oib?: string; adresa?: string; grad?: string } | null;
        odvjetnik?: { ime?: string; oib?: string; adresa?: string; grad?: string; iban?: string } | null;
        protustranka?: { ime?: string; oib?: string; adresa?: string; grad?: string } | null;
        naknada_troskova_stavke?: {
          vrsta: string; opis?: string | null; iznos: number;
          nacin?: string | null; kolicina?: number | null; jedinicni_iznos?: number | null; cestarina?: number | null;
        }[];
        dodatne_pristojbe?: { datum?: string | null; opis: string; iznos: number }[];
        predujmovi?: { datum?: string | null; opis: string; iznos: number }[];
      };

      const noveRedci: RedakTroskovnika[] = (izOut.stavke ?? []).map((s) => {
        const povisenje = s.tbr41_povisenje_postotak || null;
        const snizenje = s.tbr41_snizenje_postotak || null;
        return {
          uid: noviUid(),
          itemId: s.item_id,
          kolicina: s.kolicina,
          datum: s.datum ?? null,
          tbr41: povisenje || snizenje ? { povisenje, snizenje } : null,
        };
      });

      const jeZastupnikDjeteta = bool(out.is_zastupnik_djeteta) ?? false;
      const jeSluzbena = bool(out.is_po_sluzbenoj_duznosti) ?? false;
      const jeProracun = bool(out.is_na_teret_proracuna) ?? false;
      const jeIzabraniOslobodjajuca = bool(out.is_izabrani_oslobadjajuca) ?? false;
      const noviKazneniStatus: KazneniStatus = jeZastupnikDjeteta
        ? "dijete"
        : jeSluzbena
        ? "sluzbena"
        : jeProracun && jeIzabraniOslobodjajuca
        ? "proracun_izabrani"
        : jeProracun
        ? "proracun"
        : "puna";

      const noveProtustranka = izOut.protustranka ?? null;
      const noviPolozajProtustranke = str(out.protustranka_polozaj)
        ?? (zapis.stranke?.protustranka?.polozaj ?? undefined);

      vratiNacrt({
        ...draftSnapshot,
        vrijednostSpora: formatUnosa(num(out.vrijednost_spora) ?? zapis.vrijednost_spora ?? 0) || "",
        redci: noveRedci,
        selectedClientId: zapis.client_id ?? "",
        napomena: str(out.napomena) || ZADANA_NAPOMENA,
        odvjetnikIme: izOut.odvjetnik?.ime ?? zapis.odvjetnik?.naziv ?? "",
        odvjetnikOib: izOut.odvjetnik?.oib ?? zapis.odvjetnik?.oib ?? "",
        odvjetnikAdresa: izOut.odvjetnik?.adresa ?? zapis.odvjetnik?.adresa ?? "",
        odvjetnikGrad: izOut.odvjetnik?.grad ?? "",
        odvjetnikIban: izOut.odvjetnik?.iban ?? zapis.odvjetnik?.iban ?? "",
        klijentIme: izOut.klijent?.ime ?? "",
        klijentOib: izOut.klijent?.oib ?? "",
        klijentAdresa: izOut.klijent?.adresa ?? "",
        klijentGrad: izOut.klijent?.grad ?? "",
        opisPredmeta: zapis.opis_predmeta ?? str(out.opis_predmeta) ?? "",
        predmetSud: str(out.sud) ?? zapis.predmet?.sud ?? "",
        predmetPoslovniBroj: str(out.broj_predmeta) ?? zapis.predmet?.poslovni_broj ?? "",
        showParties: Boolean(
          zapis.predmet?.sud || zapis.predmet?.poslovni_broj ||
          noveProtustranka?.ime || zapis.stranke?.zastupana_stranka?.polozaj,
        ),
        isPdvUkljucen: bool(out.is_pdv_ukljucen) ?? bool(out.has_vat) ?? false,
        naknadaTroskovaStavke: (izOut.naknada_troskova_stavke ?? []).map((s) => ({
          vrsta: s.vrsta as VrstaTroska,
          opis: s.opis ?? "",
          iznos: formatUnosa(s.iznos),
          nacin: (s.nacin as NacinNaknade | undefined) ?? undefined,
          kolicina: s.kolicina != null ? formatUnosa(s.kolicina) : undefined,
          jedinicniIznos: s.jedinicni_iznos != null ? formatUnosa(s.jedinicni_iznos) : undefined,
          cestarina: s.cestarina != null ? formatUnosa(s.cestarina) : undefined,
        })),
        protustrankaIme: noveProtustranka?.ime ?? zapis.stranke?.protustranka?.naziv ?? "",
        protustrankaOib: noveProtustranka?.oib ?? zapis.stranke?.protustranka?.oib ?? "",
        protustrankaAdresa: noveProtustranka?.adresa ?? zapis.stranke?.protustranka?.adresa ?? "",
        protustrankaGrad: noveProtustranka?.grad ?? "",
        protustrankaPolozaj: noviPolozajProtustranke ?? "",
        zastupanaPolozaj: zapis.stranke?.zastupana_stranka?.polozaj ?? "",
        zaprijecenaKazna: str(out.zaprijecena_kazna) ?? "",
        kazneniStatus: noviKazneniStatus,
        isStjecaj: bool(out.is_stjecaj) ?? false,
        isOduzimanjePrekrsaj: bool(out.is_oduzimanje_prekrsaj) ?? false,
        vrijednostOduzimanja: formatUnosa(num(out.vrijednost_oduzimanja)),
        isOslobodjenPristojbi: bool(out.is_oslobodjen_pristojbi) ?? false,
        oslobodjenjeKategorija: str(out.oslobodjenje_kategorija) ?? "",
        brojOslobodjenihOsoba: num(out.broj_oslobodjenih_osoba) ?? 0,
        postotakUspjeha: formatUnosa(num(out.postotak_uspjeha)),
        vrstaPostupka: (str(out.vps_vrsta_postupka) as VrstaPostupka) ?? VRSTA_POSTUPKA.Opci,
        vpsPrimijeniNaNagradu: bool(out.vps_primijeni_na_nagradu) ?? false,
        brojZastupanihOsoba: num(out.broj_zastupanih_osoba) ?? 1,
        osnovicaTbr7: (str(out.osnovica_tbr7) as OsnovicaTbr7) ?? OSNOVICA_TBR7.Toc1,
        razinaSuda: (str(out.razina_suda) as RazinaSuda | undefined) ?? null,
        tbr45Nacin: (str(out.tbr45_nacin) as Tbr45Nacin | undefined) ?? null,
        tbr41Povisenje: num(out.tbr41_povisenje_postotak) ?? 0,
        tbr41Snizenje: num(out.tbr41_snizenje_postotak) ?? 0,
        dodatnePristojbe: (izOut.dodatne_pristojbe ?? []).map((s) => ({
          uid: noviUid(), datum: s.datum ?? null, opis: s.opis, iznos: formatUnosa(s.iznos),
        })),
        predujmovi: (izOut.predujmovi ?? []).map((s) => ({
          uid: noviUid(), datum: s.datum ?? null, opis: s.opis, iznos: formatUnosa(s.iznos),
        })),
      });

      setKlonInfo({ datum: zapis.created_at });
      router.replace("/troskovnik");
    })();
    // Namjerno samo [klonId, userId]: draftSnapshot/vratiNacrt/noviUid/formatUnosa
    // čitaju se u trenutku okidanja efekta, a ref sprječava dvostruku primjenu.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [klonId, userId]);

  // ── Load catalog ──
  // Waits for userId, exactly like the client fetch below. useAuth() fills
  // authHeaders asynchronously, so firing on mount alone sent this request
  // before the token existed — no Authorization header, and the backend
  // answered 401 with "Nedostaje pristupni token".
  //
  // The guard is on userId rather than authHeaders because the two are set
  // together, and userId is a string: authHeaders is a fresh object on every
  // auth event, which would refetch the catalog on each token refresh.
  useEffect(() => {
    if (!userId) return;

    (async () => {
      try {
        const res = await fetch(`${API_BASE}/api/troskovnik/katalog`, {
          headers: authHeaders,
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data: TarifnaStavka[] = await res.json();
        setKatalog(data);
      } catch (err) {
        setKatalogError(err instanceof Error ? err.message : "Greška pri učitavanju kataloga");
      } finally {
        setLoadingKatalog(false);
      }
    })();
  }, [userId]);

  /** Odgovor `/api/settings/firm` — samo polja koja ova stranica troši. */
  type FirmSettings = {
    name?: string | null;
    oib?: string | null;
    address?: string | null;
    iban?: string | null;
    phone?: string | null;
    email?: string | null;
    pdv_obveznik?: boolean;
    cijena_goriva_eur_l?: number | null;
    dnevnica_eur?: number | null;
  };

  // ── Podaci ureda iz postavki ──
  //
  // Odvjetnik svoje ime, OIB, adresu i IBAN upisuje jednom, u Postavke →
  // Odvjetnički ured. Odatle idu u zaglavlje svakog ispisa, pa ih ne mora
  // pretipkavati u svaki troškovnik.
  //
  // Popunjava se samo ono što je prazno: unos u obrascu uvijek pobjeđuje, jer
  // se troškovnik zna podnositi i pod drukčijim podacima od onih u postavkama
  // (zamjena, zajednički ured). Iz istog razloga vraćeni nacrt, koji stiže
  // kasnije i na izričit zahtjev korisnika, mirno pregazi ovo.
  //
  // Grad se ne popunjava — `law_firms` nema taj stupac, adresa je jedno
  // slobodno polje („Ilica 1, 10000 Zagreb").
  //
  // PDV prekidač je isto samo prijedlog: ako je korisnik već dirao checkbox
  // (npr. jer je fetch stigao nakon što je već odgovorio), dohvat ga ne mijenja.
  // Neuspjeh fetcha tiho ostavlja sve kako jest — ovo nije kritična putanja.
  useEffect(() => {
    if (!userId || demo) return; // U demo načinu zaglavlje puni DEMO_URED.
    fetch(`${API_BASE}/api/settings/firm`, { headers: authHeaders })
      .then((r) => (r.ok ? r.json() : null))
      .then((data: FirmSettings | null) => {
        if (!data) return;
        if (!pdvManuallySet.current) setIsPdvUkljucen(Boolean(data.pdv_obveznik));
        setOdvjetnikIme((prev) => prev || data.name || "");
        setOdvjetnikOib((prev) => prev || data.oib || "");
        setOdvjetnikAdresa((prev) => prev || data.address || "");
        setOdvjetnikIban((prev) => prev || data.iban || "");
        setOdvjetnikTelefon((prev) => prev || data.phone || "");
        setOdvjetnikEmail((prev) => prev || data.email || "");
        setCijenaGorivaEurL(data.cijena_goriva_eur_l ?? null);
        setDnevnicaEur(data.dnevnica_eur ?? null);
      })
      .catch(() => {});
  }, [userId]);

  // ── Osobno ime prijavljenog odvjetnika, za potpis uz naziv ureda ──
  //
  // Zaseban dohvat od ureda: `/api/settings/firm` vraća naziv ureda,
  // `/api/settings/profile` vraća osobno ime prijavljenog korisnika
  // (`user_profiles.full_name`) — dva različita podatka, dosad se na ovoj
  // stranici dohvaćao samo prvi.
  useEffect(() => {
    if (!userId || demo) return;
    fetch(`${API_BASE}/api/settings/profile`, { headers: authHeaders })
      .then((r) => (r.ok ? r.json() : null))
      .then((data: { full_name?: string | null } | null) => {
        if (!data) return;
        setOdvjetnikOsobnoIme(data.full_name || "");
      })
      .catch(() => {});
  }, [userId]);

  // ── Load user's clients for picker ──
  useEffect(() => {
    if (!userId) return;
    // Prikaz primjera ne dira bazu: stranke dolaze iz lokalne datoteke, a
    // korisnikov stvarni popis ostaje netaknut i neprikazan.
    if (demo) {
      setClients(DEMO_STRANKE.map(({ id, ime_naziv, oib, adresa, grad, tip }) => ({
        id, ime_naziv, tip,
        oib: oib ?? undefined, adresa: adresa ?? undefined, grad: grad ?? undefined,
      })));
      setLoadingClients(false);
      return;
    }
    setLoadingClients(true);
    fetch(`${API_BASE}/api/client`, { headers: authHeaders })
      .then((r) => (r.ok ? r.json() : []))
      .then((data: ClientOption[]) => {
        setClients(data);
        // Auto-select if arrived via ?clientId=...
        if (preselectedClientId) {
          const match = data.find((c) => c.id === preselectedClientId);
          if (match) {
            handleClientAutoFill(match);
            setShowParties(true);
          }
        }
      })
      .catch(() => {})
      .finally(() => setLoadingClients(false));
  }, [userId, preselectedClientId, demo]);

  // ── Close client dropdown on outside click ──
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (clientContainerRef.current && !clientContainerRef.current.contains(e.target as Node)) {
        setClientDropdownOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // ── Filtered clients for combobox ──
  const filteredClients = useMemo(() => {
    if (!clientSearchQuery.trim()) return clients;
    const q = clientSearchQuery.toLowerCase();
    return clients.filter(
      (c) =>
        c.ime_naziv.toLowerCase().includes(q) ||
        (c.oib && c.oib.includes(q)) ||
        (c.adresa && c.adresa.toLowerCase().includes(q)) ||
        (c.grad && c.grad?.toLowerCase().includes(q))
    );
  }, [clients, clientSearchQuery]);

  // ── Client selection handler (from combobox) ──
  const handleClientAutoFill = useCallback((client: ClientOption) => {
    setSelectedClientId(client.id);
    setKlijentIme(client.ime_naziv);
    setKlijentOib(client.oib ?? "");
    setKlijentAdresa(client.adresa ?? "");
    setKlijentGrad(client.grad ?? "");
    // Postojeća stranka nosi svoj tip iz baze; prijedlog iz naziva se ne miješa.
    setKlijentTip(client.tip === "pravna" ? "pravna" : "fizicka");
    klijentTipRucno.current = true;
    setClientAutoFilled(true);
    setClientSearchQuery("");
    setClientDropdownOpen(false);
  }, []);

  const handleClientClear = useCallback(() => {
    setSelectedClientId("");
    setKlijentIme("");
    setKlijentOib("");
    setKlijentAdresa("");
    setKlijentGrad("");
    setKlijentTip("fizicka");
    klijentTipRucno.current = false;
    setClientAutoFilled(false);
    setClientSearchQuery("");
  }, []);

  // When user manually edits client fields after auto-fill, unlink
  const handleKlijentManualEdit = useCallback(
    (setter: (v: string) => void, value: string) => {
      setter(value);
      if (clientAutoFilled && selectedClientId) {
        setSelectedClientId("");
        setClientAutoFilled(false);
        // Otkačeno od baze — opet je nova stranka, pa prijedlog tipa iz naziva
        // smije ponovno raditi dok ga korisnik sam ne pipne.
        klijentTipRucno.current = false;
      }
    },
    [clientAutoFilled, selectedClientId]
  );

  // Prijedlog „fizička/pravna" iz naziva nove stranke. Ne dira odabir iz baze
  // (ondje tip dolazi s njom) ni ručni izbor korisnika.
  useEffect(() => {
    if (klijentTipRucno.current || (clientAutoFilled && selectedClientId)) return;
    setKlijentTip(naslutiTipStranke(klijentIme));
  }, [klijentIme, clientAutoFilled, selectedClientId]);

  // ── Derived values ──

  // Zelena oznaka „povezano sa strankom" i poruka „popunjeni iz baze" smiju stajati
  // samo kad odabrana stranka doista postoji u učitanom popisu — ne za tek upisano
  // ime ni za vezu na stranku koje nema (npr. iz zastarjelog nacrta).
  const strankaPovezanaSBazom = useMemo(
    () => clientAutoFilled && !!selectedClientId && clients.some((c) => c.id === selectedClientId),
    [clientAutoFilled, selectedClientId, clients],
  );

  // Odabrane definicije iz kataloga — osnova za određivanje vrste postupka.
  // Ista stavka smije se pojaviti više puta u `redci`; ovdje se svejedno svaka
  // pojava uzima u obzir (some/every na ponovljenom elementu ne mijenja rezultat).
  const odabraneDefinicije = useMemo(() => {
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));
    return redci
      .map((r) => catalogMap.get(r.itemId))
      .filter((d): d is TarifnaStavka => d !== undefined);
  }, [redci, katalog]);

  // Kronološki poredak radnji — datum je glavni, a redoslijed u polju (kojim
  // upravljaju strelice „Pomakni gore/dolje") razrješava jednakost, tj. radnje
  // istog datuma i radnje bez datuma. Ovaj se poredani pogled koristi za prikaz
  // u panelu i za živi izračun (prije nego postoji spremljeni rezultat); sve
  // izmjene retka i dalje idu preko `redci` i njegovih `setRedci` rukovatelja.
  //
  // Za slanje na backend (`stavkeZaSlanje` niže) NE koristi se ovaj poredak,
  // nego izravno `redci` — troškovnik koji ide sudu ispisuje radnje u
  // redoslijedu kakav je odvjetnik složio, ne nužno kronološki. Izračun time
  // nije dirnut: bodovi/iznosi po retku ne ovise o poziciji u nizu (vidi
  // komentar uz `IzracunajRetke` na backendu).
  const redciPoredani = useMemo(
    () => [...redci].sort((a, b) => usporediPoDatumu(a.datum, b.datum)),
    [redci],
  );

  // Bilo koja kaznena stavka → prikaži panel s parametrima kaznenog postupka.
  const isKazneniMode = odabraneDefinicije.some((d) => d.kategorija === KATEGORIJA_KAZNENI);

  // Isključivo kaznene stavke → VPS se ne koristi ni za bodove ni za pristojbu.
  const isOnlyKazneni = jeSamoKazneni(odabraneDefinicije);

  const unesenVps = parseFloat(vrijednostSpora.replace(/\./g, "").replace(",", ".")) || 0;
  // U kaznenom postupku sve ide po zapriječenoj kazni (Tbr. 4. toč. 1) — VPS otpada.
  const vrijednostNum = isOnlyKazneni ? 0 : unesenVps;
  const rentaIznosNum = parseFloat(rentaIznos.replace(/\./g, "").replace(",", ".")) || 0;

  const brojIliNula = (tekst: string) =>
    parseFloat(tekst.replace(/\./g, "").replace(",", ".")) || 0;

  // ── Vrijednost predmeta spora po Zakonu o sudskim pristojbama (čl. 14–26) ──
  // Obavezna za sudsku pristojbu; na nagradu se prenosi samo izričitim odabirom.
  const vpsUlaz = useMemo(
    () => ({
      vrijednostSpora: vrijednostNum,
      vrstaPostupka,
      uzdrzavanjeMjesecno: brojIliNula(uzdrzavanjeMjesecno),
      zahtjevi,
      zahtjeviNacin,
      pravniLijek,
      pravniLijekIznos: brojIliNula(pravniLijekIznos),
    }),
    [vrijednostNum, vrstaPostupka, uzdrzavanjeMjesecno, zahtjevi, zahtjeviNacin, pravniLijek, pravniLijekIznos],
  );

  const vpsRezultat = useMemo(
    () => (isOnlyKazneni ? null : razrijesiVps(vpsUlaz)),
    [isOnlyKazneni, vpsUlaz],
  );

  /** VPS koji ide u sudsku pristojbu. Null rezultat znači „podatak nedostaje". */
  const vpsZakonski = vpsRezultat?.vps ?? vrijednostNum;

  // Osnovica na koju upućuju Tbr. 8. toč. 1, 3, 4 i Tbr. 9. toč. 1–6:
  // "Tbr. 7. toč. 1., 4., 7. i 8." — četiri varijante iste osnovice.
  const osnovicaUlaz = useMemo(
    () => ({
      osnovicaTbr7,
      vrijednostSpora: vpsPrimijeniNaNagradu ? vpsZakonski : vrijednostNum,
      razinaSuda,
      tbr45Nacin,
      rentaIznos: rentaIznosNum,
      rentaRazdoblje,
    }),
    [osnovicaTbr7, vpsPrimijeniNaNagradu, vpsZakonski, vrijednostNum, razinaSuda, tbr45Nacin, rentaIznosNum, rentaRazdoblje],
  );

  const osnovica = useMemo(() => izracunajOsnovicu(osnovicaUlaz), [osnovicaUlaz]);
  const efektivniVpsNum = useMemo(() => efektivniVps(osnovicaUlaz), [osnovicaUlaz]);
  const baseBodovi = osnovica?.bodovi ?? 0;

  // Oslobođenja po čl. 11–13; broj osoba dolazi iz Tbr. 40, gdje već stoji.
  const oslobodjenjeUlaz = useMemo(
    () => ({
      oslobodjen: isOslobodjenPristojbi,
      brojOsoba: brojZastupanihOsoba,
      brojOslobodjenih: brojOslobodjenihOsoba,
      udioVps: udioUVps.trim() ? brojIliNula(udioUVps) : null,
      postotakUspjeha: postotakUspjeha.trim() ? brojIliNula(postotakUspjeha) : null,
    }),
    [isOslobodjenPristojbi, brojZastupanihOsoba, brojOslobodjenihOsoba, udioUVps, postotakUspjeha],
  );

  // Sudske pristojbe (Uredba NN 50/2026). Svaka pristojbena radnja svoj redak — tužba,
  // presuda i žalba tri su pristojbe. Veže se uz ZAKONSKI VPS (Zakon o sudskim
  // pristojbama, čl. 14–26), ne uz uneseni iznos ni uz osnovicu iz Tbr. 45 — one služe
  // samo za odvjetničku nagradu.
  const pristojbe = useMemo(
    () =>
      obracunajPristojbe({
        vrijednostSpora: vpsZakonski,
        odabraneStavke: odabraneDefinicije,
        oslobodjen: isOslobodjenPristojbi,
        oslobodjenje: oslobodjenjeUlaz,
        datumOdgovoraNaTuzbu: datumOdgovoraNaTuzbu || null,
        datumPokretanjaUpravnogSpora: datumPokretanjaUpravnogSpora || null,
        dodane: pristojbeDodane,
        iskljucene: pristojbeIskljucene,
        // Čl. 7 Zakona o sudskim pristojbama — elektronički oblik
        podnesciElektronicki,
        zkUpisElektronicki,
        odlukeElektronicki,
      }),
    [
      vpsZakonski,
      odabraneDefinicije,
      isOslobodjenPristojbi,
      oslobodjenjeUlaz,
      podnesciElektronicki,
      zkUpisElektronicki,
      odlukeElektronicki,
      datumOdgovoraNaTuzbu,
      datumPokretanjaUpravnogSpora,
      pristojbeDodane,
      pristojbeIskljucene,
    ],
  );

  /**
   * Miče redak iz obračuna: automatski izveden se stavlja na popis isključenih,
   * a ručno dodan se jednostavno briše.
   */
  const makniPristojbu = useCallback((odredbaId: string, automatski: boolean) => {
    if (automatski) {
      setPristojbeIskljucene((prev) => (prev.includes(odredbaId) ? prev : [...prev, odredbaId]));
    } else {
      setPristojbeDodane((prev) => prev.filter((p) => p.odredba_id !== odredbaId));
    }
  }, []);

  /** Dodaje odredbu iz popisa; isključenje se pritom poništava. */
  const dodajPristojbu = useCallback((odredbaId: string) => {
    if (!odredbaId) return;
    setPristojbeIskljucene((prev) => prev.filter((id) => id !== odredbaId));
    setPristojbeDodane((prev) =>
      prev.some((p) => p.odredba_id === odredbaId) ? prev : [...prev, { odredba_id: odredbaId }],
    );
  }, []);

  // Polja za datum imaju smisla samo kad je odabrana radnja na koju se čl. 2 odnosi.
  const imaOdgovorNaTuzbu = odabraneDefinicije.some((d) => d.id === "par_odgovor_tuzba");
  const imaUpravniSpor = pristojbe.stavke.some((s) => s.poglavlje === "VIII. Upravni spor");

  // Što nedostaje da se troškovnik može valjano izračunati — zrcali backend provjere.
  const nedostaci = useMemo(
    () =>
      provjeriZahtjev(odabraneDefinicije, {
        zaprijecenaKazna: zaprijecenaKazna || undefined,
        isOduzimanjePrekrsaj,
        vrijednostOduzimanja: parseFloat(vrijednostOduzimanja.replace(/\./g, "").replace(",", ".")) || 0,
        osnovicaTbr7,
        razinaSuda,
        tbr45Nacin,
        // VPS je institut parničnog postupka — u kaznenom se ne provjerava.
        vps: isOnlyKazneni ? undefined : vpsUlaz,
        oslobodjenje: oslobodjenjeUlaz,
      }),
    [odabraneDefinicije, zaprijecenaKazna, isOduzimanjePrekrsaj, vrijednostOduzimanja, osnovicaTbr7, razinaSuda, tbr45Nacin, isOnlyKazneni, vpsUlaz, oslobodjenjeUlaz],
  );

  // Tbr. 41 — povišenje i sniženje ne mogu stajati na istoj stavci. Po retku, ne po
  // itemId — ista stavka može se pojaviti više puta, svaki put s vlastitom prilagodbom.
  const nedostaciTbr41 = useMemo(() => {
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));
    return redci
      .map((redak) => {
        const def = catalogMap.get(redak.itemId);
        if (!def) return null;
        const { povisenje, snizenje } = razrijesiTbr41(
          redak.tbr41 ?? {},
          tbr41Povisenje,
          tbr41Snizenje,
        );
        return provjeriTbr41Stavku(def.opis, povisenje, snizenje);
      })
      .filter((p): p is string => p !== null);
  }, [redci, katalog, tbr41Povisenje, tbr41Snizenje]);

  const sviNedostaci = useMemo(
    () => [...nedostaci, ...nedostaciTbr41],
    [nedostaci, nedostaciTbr41],
  );

  // Tbr. 7. toč. 9 — upozorenje, ne greška: odvjetnik stavku bira sam.
  const upozorenjeSpor = useMemo(
    () => upozorenjeMaliSpor(odabraneDefinicije, vrijednostNum, osnovicaTbr7),
    [odabraneDefinicije, vrijednostNum, osnovicaTbr7],
  );

  // Resolved kazneni tier points
  const kazneniTierBodovi = zaprijecenaKazna
    ? (ZAPRIJECENA_KAZNA_TIERS[zaprijecenaKazna]?.bodovi ?? 0)
    : 0;

  // Parsed vrijednost oduzimanja for prekršaj oduzimanje calc
  const parsedVrijednostOduzimanja = parseFloat(vrijednostOduzimanja.replace(/\./g, "").replace(",", ".")) || 0;

  // Group catalog by category
  const categories = useMemo(() => {
    const map = new Map<string, TarifnaStavka[]>();
    for (const s of katalog) {
      const arr = map.get(s.kategorija) || [];
      arr.push(s);
      map.set(s.kategorija, arr);
    }
    return Array.from(map.entries());
  }, [katalog]);

  // Filter by search
  const filteredCategories = useMemo(() => {
    if (!searchTerm.trim()) return categories;
    const lower = searchTerm.toLowerCase();
    return categories
      .map(([cat, items]) => [cat, items.filter(
        (i) =>
          i.opis.toLowerCase().includes(lower) ||
          i.tarifni_broj.toLowerCase().includes(lower) ||
          i.id.toLowerCase().includes(lower) ||
          cat.toLowerCase().includes(lower)
      )] as [string, TarifnaStavka[]])
      .filter(([, items]) => items.length > 0);
  }, [categories, searchTerm]);

  // Client-side live calculation. Sama agregacija (petlja po recima → Tbr. 40/41 →
  // PDV → pristojbe → sveukupno) živi u čistoj funkciji `izracunajTroskovnik`, da
  // se može testirati bez Reacta; ovdje se samo skupe ulazi iz stanja komponente.
  const liveCalc = useMemo(
    () =>
      izracunajTroskovnik({
        redciPoredani,
        katalog,
        osnovica,
        isPdvUkljucen,
        kazneniTierBodovi,
        isZastupnikDjeteta,
        isPoSluzbenojDuznosti,
        isNaTeretProracuna,
        isIzabraniOslobodjajuca,
        zaprijecenaKazna: zaprijecenaKazna || null,
        isOduzimanjePrekrsaj,
        parsedVrijednostOduzimanja,
        brojZastupanihOsoba,
        tbr41Povisenje,
        tbr41Snizenje,
        pristojbe: {
          ukupno: pristojbe.ukupno,
          vpsUdio: pristojbe.vpsUdio,
          naTeretProtustranke: pristojbe.naTeretProtustranke,
        },
        dodatnePristojbe: dodatnePristojbe.map((t) => ({ opis: t.opis, iznos: parseEurInput(t.iznos) })),
        predujmovi: predujmovi.map((t) => ({ opis: t.opis, iznos: parseEurInput(t.iznos) })),
        naknadaTroskovaStavke: naknadaTroskovaStavke.map((r) => ({
          vrsta: r.vrsta,
          iznos: parseHrNumber(r.iznos),
          nacin: r.nacin,
          kolicina: r.kolicina != null ? parseHrNumber(r.kolicina) : null,
          jedinicniIznos: r.jedinicniIznos != null ? parseHrNumber(r.jedinicniIznos) : null,
          cestarina: r.cestarina != null ? parseHrNumber(r.cestarina) : null,
        })),
        datumDospijeca: datumDospijeca || null,
      }),
    [redciPoredani, katalog, osnovica, isPdvUkljucen, kazneniTierBodovi, isZastupnikDjeteta, isPoSluzbenojDuznosti, isNaTeretProracuna, isIzabraniOslobodjajuca, isOduzimanjePrekrsaj, parsedVrijednostOduzimanja, pristojbe, brojZastupanihOsoba, zaprijecenaKazna, tbr41Povisenje, tbr41Snizenje, dodatnePristojbe, predujmovi, naknadaTroskovaStavke, datumDospijeca],
  );

  // Katalog po id, i izračunati redak po uid — za uređivanje u desnom stupcu, koje
  // iterira `redci` (izvor istine, uključuje i retke koji još ne daju iznos), a
  // izračunate vrijednosti dohvaća odavde.
  const katalogById = useMemo(() => new Map(katalog.map((s) => [s.id, s])), [katalog]);
  const lineByUid = useMemo(() => new Map(liveCalc.lines.map((l) => [l.uid, l])), [liveCalc.lines]);

  // ── Handlers ──
  const toggleCategory = useCallback((cat: string) => {
    setExpandedCategories((prev) => {
      const next = new Set(prev);
      if (next.has(cat)) next.delete(cat);
      else next.add(cat);
      return next;
    });
  }, []);

  /** Stabilan ključ neovisan o crypto.randomUUID dostupnosti u starijim okruženjima. */
  const noviUid = useCallback(() => {
    if (typeof crypto !== "undefined" && crypto.randomUUID) return crypto.randomUUID();
    return `r${Date.now()}_${Math.random().toString(36).slice(2)}`;
  }, []);

  /** Dodaje novi redak na kraj popisa — ista stavka smije se ponoviti. */
  const addItem = useCallback((itemId: string) => {
    setRedci((prev) => [...prev, { uid: noviUid(), itemId, kolicina: 1, datum: null, tbr41: null }]);
  }, [noviUid]);

  /** Mijenja količinu retka; `qty <= 0` briše redak. */
  const postaviKolicinu = useCallback((uid: string, qty: number) => {
    setRedci((prev) =>
      qty <= 0 ? prev.filter((r) => r.uid !== uid) : prev.map((r) => (r.uid === uid ? { ...r, kolicina: qty } : r)),
    );
  }, []);

  const obrisiRedak = useCallback((uid: string) => {
    setRedci((prev) => prev.filter((r) => r.uid !== uid));
  }, []);

  /** Datum radnje — opcionalan; prazan string sprema se kao null. */
  const postaviDatum = useCallback((uid: string, iso: string) => {
    setRedci((prev) => prev.map((r) => (r.uid === uid ? { ...r, datum: iso || null } : r)));
  }, []);

  /**
   * ▲/▼ — zamjena sa susjedom U PRIKAZU (kronološkom poretku), na rubovima bez
   * efekta. Datum je glavni: za dvije radnje istog datuma (ili obje bez datuma)
   * ovo je prava zamjena; preko granice datuma stabilni sort ionako vrati
   * poredak, pa strelica tada nema vidljiv učinak.
   */
  const pomakni = useCallback((uid: string, smjer: -1 | 1) => {
    setRedci((prev) => {
      const poredani = [...prev].sort((a, b) => usporediPoDatumu(a.datum, b.datum));
      const pi = poredani.findIndex((r) => r.uid === uid);
      const ci = pi + smjer;
      if (pi < 0 || ci < 0 || ci >= poredani.length) return prev;
      const ai = prev.findIndex((r) => r.uid === poredani[pi].uid);
      const bi = prev.findIndex((r) => r.uid === poredani[ci].uid);
      const rez = [...prev];
      [rez[ai], rez[bi]] = [rez[bi], rez[ai]];
      return rez;
    });
  }, []);

  /**
   * Tbr. 41 za pojedini redak. Vrijednost je "p<postotak>" za povišenje po toč. 1,
   * "s<postotak>" za sniženje po toč. 2, prazno za nasljeđivanje prečaca.
   */
  const postaviTbr41 = useCallback((uid: string, vrijednost: string) => {
    setRedci((prev) =>
      prev.map((r) => {
        if (r.uid !== uid) return r;
        if (!vrijednost) {
          // Izričita nula, ne nasljeđivanje — inače bi prečac vratio postotak natrag.
          return { ...r, tbr41: { povisenje: 0, snizenje: 0 } };
        }
        if (vrijednost.startsWith("p")) {
          return { ...r, tbr41: { povisenje: Number(vrijednost.slice(1)), snizenje: 0 } };
        }
        return { ...r, tbr41: { povisenje: 0, snizenje: Number(vrijednost.slice(1)) } };
      }),
    );
  }, []);

  /** Prečac "primijeni na sve stavke" — briše sve pojedinačne postotke. */
  const primijeniTbr41NaSve = useCallback(() => {
    setRedci((prev) => prev.map((r) => ({ ...r, tbr41: null })));
  }, []);

  const brojPojedinacnihTbr41 = useMemo(() => redci.filter((r) => r.tbr41 !== null).length, [redci]);

  const clearAll = useCallback(() => {
    setRedci([]);
    setNaknadaTroskovaStavke([]);
    setPredujmovi([]);
    // Sve vezano uz radnje mora pasti s njima — inače ostanu ručno dodane/isključene
    // pristojbe i kvačice čl. 7 za radnje kojih više nema.
    setPristojbeDodane([]);
    setPristojbeIskljucene([]);
    setDodatnePristojbe([]);
    setPodnesciElektronicki([]);
    setOdlukeElektronicki([]);
    setZkUpisElektronicki(false);
    setResult(null);
    setCalcError(null);
  }, []);

  // ═══════════════════════════════════════════════════════════
  //  Prikaz primjera — gotov troškovnik umjesto prazne forme
  //
  //  Vodič dobrodošlice ne objašnjava prazna polja nego GOTOV troškovnik, pa
  //  forma mora doći popunjena. Puni se istim putem kojim se vraća prekinuti
  //  rad i kojim radi „Uredi kao novi" — `vratiNacrt` — tako da nema drugog
  //  koda koji zna kako se ova forma popunjava.
  //
  //  Ulazi dolaze iz `lib/demo/podaci.ts` i to su SAMO ulazi: bodove, iznose,
  //  pristojbe i PDV izračuna `liveCalc`, isti motor koji radi za stvarne
  //  troškovnike. Zato demo troškovnik ujedno pokazuje je li Tarifa ispravna.
  // ═══════════════════════════════════════════════════════════
  const demoPrimijenjenRef = useRef(false);

  /** Polja koja demo dira — u jednom objektu, da se ista lista može i vratiti. */
  const demoPolja = useCallback(() => {
    const stranka = DEMO_STRANKE.find((c) => c.id === DEMO_TROSKOVNIK.klijentId);
    return {
      vrijednostSpora: DEMO_TROSKOVNIK.vrijednostSporaUnos,
      redci: DEMO_TROSKOVNIK.radnje.map((r) => ({
        uid: noviUid(), itemId: r.itemId, kolicina: 1, datum: r.datum, tbr41: null,
      })),
      selectedClientId: DEMO_TROSKOVNIK.klijentId,
      opisPredmeta: DEMO_TROSKOVNIK.opisPredmeta,
      predmetSud: DEMO_TROSKOVNIK.sud,
      predmetPoslovniBroj: DEMO_TROSKOVNIK.poslovniBroj,
      showParties: true,
      isPdvUkljucen: true,
      odvjetnikIme: DEMO_URED.ime,
      odvjetnikOib: DEMO_URED.oib,
      odvjetnikAdresa: DEMO_URED.adresa,
      odvjetnikIban: DEMO_URED.iban,
      klijentIme: stranka?.ime_naziv ?? "",
      klijentOib: stranka?.oib ?? "",
      klijentAdresa: stranka?.adresa ?? "",
      klijentGrad: stranka?.grad ?? "",
      klijentTip: (stranka?.tip === "pravna" ? "pravna" : "fizicka") as TipStranke,
      zastupanaPolozaj: "tužitelj",
      protustrankaIme: DEMO_TROSKOVNIK.protustranka,
      protustrankaPolozaj: "tuženik",
      podnesciElektronicki: [...DEMO_TROSKOVNIK.podnesciElektronicki],
      pristojbeDodane: DEMO_TROSKOVNIK.pristojbeDodane.map((r) => ({ ...r })),
      predujmovi: DEMO_TROSKOVNIK.predujmovi.map((r) => ({
        uid: noviUid(), datum: r.datum, opis: r.opis, iznos: r.iznos,
      })),
    };
  }, [noviUid]);

  /** Ista polja, vraćena na prazno — izlazak iz demo načina. */
  const praznaPolja = useCallback(
    () => ({
      vrijednostSpora: "", redci: [], selectedClientId: "",
      opisPredmeta: "", predmetSud: "", predmetPoslovniBroj: "",
      showParties: false, isPdvUkljucen: false,
      odvjetnikIme: "", odvjetnikOib: "", odvjetnikAdresa: "", odvjetnikIban: "",
      klijentIme: "", klijentOib: "", klijentAdresa: "", klijentGrad: "",
      klijentTip: "fizicka" as TipStranke,
      zastupanaPolozaj: "", protustrankaIme: "", protustrankaPolozaj: "",
      podnesciElektronicki: [], pristojbeDodane: [], predujmovi: [],
    }),
    [],
  );

  useEffect(() => {
    if (demo && !demoPrimijenjenRef.current) {
      demoPrimijenjenRef.current = true;
      vratiNacrt({ ...draftSnapshot, ...demoPolja() });
      setResult(null);
      setSavedId(null);
      return;
    }
    if (!demo && demoPrimijenjenRef.current) {
      // „Zatvaram primjere i otvaram vam prazan troškovnik." Podaci ureda vrati
      // će efekt koji čita Postavke, čim demo prestane blokirati taj dohvat.
      demoPrimijenjenRef.current = false;
      vratiNacrt({ ...draftSnapshot, ...praznaPolja() });
      setResult(null);
      setSavedId(null);
    }
    // draftSnapshot se namjerno ne prati: čita se u trenutku okidanja, a ref
    // sprječava ponavljanje. S njim u ovisnostima efekt bi se vrtio u krug.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [demo, demoPolja, praznaPolja, vratiNacrt]);

  // ── Ručno dodane pristojbe i predujmovi ──
  // Isti oblik popisa za oba odjeljka, pa handleri primaju setter funkciju kao argument
  // umjesto da se dupliciraju za svaki popis.
  type TrosakSetter = (updater: (prev: TrosakRedak[]) => TrosakRedak[]) => void;

  const dodajTrosak = useCallback((setter: TrosakSetter) => {
    setter((prev) => [...prev, { uid: noviUid(), datum: null, opis: "", iznos: "" }]);
  }, [noviUid]);

  const azurirajTrosak = useCallback(
    (setter: TrosakSetter, uid: string, polje: "datum" | "opis" | "iznos", vrijednost: string) => {
      setter((prev) =>
        prev.map((t) => (t.uid === uid ? { ...t, [polje]: polje === "datum" ? vrijednost || null : vrijednost } : t)),
      );
    },
    [],
  );

  const obrisiTrosak = useCallback((setter: TrosakSetter, uid: string) => {
    setter((prev) => prev.filter((t) => t.uid !== uid));
  }, []);

  /**
   * Redci troškovnika u obliku zahtjeva. Redoslijed je onaj koji je korisnik
   * sam složio (`redci`), ne kronološki `redciPoredani` — server ga više ne
   * presortira (vidi `IzracunajRetke` na backendu), pa ispis dobiva radnje
   * točno u tom redoslijedu. Redni broj (`Rbr`) izračun i dalje sam dodjeljuje
   * po redoslijedu ovog polja; bodovi/iznosi ne ovise o poziciji u nizu.
   */
  const stavkeZaSlanje = useCallback(
    () =>
      redci.map((r) => ({
        item_id: r.itemId,
        kolicina: r.kolicina,
        datum: r.datum ?? null,
        // null → stavka nasljeđuje postotak s razine troškovnika (prečac)
        tbr41_povisenje_postotak: r.tbr41?.povisenje ?? null,
        tbr41_snizenje_postotak: r.tbr41?.snizenje ?? null,
      })),
    [redci],
  );

  /**
   * Ručno unesen popis (pristojbe ili predujmovi) u obliku zahtjeva. Redci koje
   * korisnik dodao a ostavio posve prazne (ni opis ni iznos) se ne šalju — nisu
   * namjerno uneseni podaci, nego zaboravljen prazan redak.
   */
  const trosakZaSlanje = useCallback(
    (lista: TrosakRedak[]) =>
      lista
        .filter((t) => t.opis.trim() !== "" || t.iznos.trim() !== "")
        .map((t) => ({ datum: t.datum ?? null, opis: t.opis, iznos: parseEurInput(t.iznos) })),
    [],
  );
  // ── Naknada troškova (Tbr. 48.-51.) — dodaj/uredi/ukloni redak ──
  const addNaknadaTroskova = useCallback(() => {
    setNaknadaTroskovaStavke((prev) => [...prev, { vrsta: VRSTA_TROSKA.Prijevoz, opis: "", iznos: "" }]);
  }, []);

  const updateNaknadaTroskova = useCallback(
    (index: number, patch: Partial<NaknadaTroskovaRedak>) => {
      setNaknadaTroskovaStavke((prev) => prev.map((r, i) => (i === index ? { ...r, ...patch } : r)));
    },
    []
  );

  const removeNaknadaTroskova = useCallback((index: number) => {
    setNaknadaTroskovaStavke((prev) => prev.filter((_, i) => i !== index));
  }, []);


  // ── Submit to backend ──
  const handleCalculate = async () => {
    if (redci.length === 0) {
      setCalcError("Morate odabrati barem jednu stavku.");
      return;
    }
    setCalcError(null);
    setCalculating(true);

    try {
      const body = {
        vrijednost_spora: vrijednostNum,
        stavke: stavkeZaSlanje(),
        dodatne_pristojbe: trosakZaSlanje(dodatnePristojbe),
        predujmovi: trosakZaSlanje(predujmovi),
        napomena: napomena.trim() || undefined,
        // nested new schema
        odvjetnik: odvjetnikIme
          ? { naziv: odvjetnikIme, oib: odvjetnikOib || undefined, adresa: odvjetnikAdresa || undefined, iban: odvjetnikIban || undefined }
          : undefined,
        predmet: { sud: predmetSud || undefined, poslovni_broj: predmetPoslovniBroj || undefined },
        stranke: {
          zastupana_stranka: klijentIme ? { polozaj: zastupanaPolozaj || undefined, naziv: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined } : undefined,
          protustranka: protustrankaIme ? { polozaj: protustrankaPolozaj || undefined, naziv: protustrankaIme, oib: protustrankaOib || undefined, adresa: protustrankaAdresa || undefined } : undefined
        },
        // legacy fields kept for compatibility
        klijent: klijentIme
          ? { ime: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined, grad: klijentGrad || undefined }
          : undefined,
        protustranka: protustrankaIme
          ? { ime: protustrankaIme, oib: protustrankaOib || undefined, adresa: protustrankaAdresa || undefined, grad: protustrankaGrad || undefined }
          : undefined,
        protustranka_polozaj: protustrankaPolozaj || undefined,
        // Keep legacy `has_vat` for backward compatibility, mirror new flag
        has_vat: isPdvUkljucen,
        is_pdv_ukljucen: isPdvUkljucen,
        // Kazneni fields
        zaprijecena_kazna: zaprijecenaKazna || undefined,
        is_po_sluzbenoj_duznosti: isPoSluzbenojDuznosti,
        is_na_teret_proracuna: isNaTeretProracuna,
        is_izabrani_oslobadjajuca: isIzabraniOslobodjajuca,
        is_zastupnik_djeteta: isZastupnikDjeteta,
        is_stjecaj: isStjecaj,
        is_oduzimanje_prekrsaj: isOduzimanjePrekrsaj,
        vrijednost_oduzimanja: parsedVrijednostOduzimanja,
        is_oslobodjen_pristojbi: isOslobodjenPristojbi,
        // Čl. 2 Uredbe NN 50/2026 — prazan datum znači "nije prijelazni slučaj".
        datum_dospijeca: datumDospijeca || null,
        datum_podnosenja_odgovora_na_tuzbu: datumOdgovoraNaTuzbu || null,
        datum_pokretanja_upravnog_spora: datumPokretanjaUpravnogSpora || null,
        pristojbe_dodane: pristojbeDodane,
        pristojbe_iskljucene: pristojbeIskljucene,
        // Čl. 7 Zakona o sudskim pristojbama — elektronički oblik
        podnesci_elektronicki: podnesciElektronicki,
        zk_upis_elektronicki: zkUpisElektronicki,
        odluke_elektronicki: odlukeElektronicki,
        broj_zastupanih_osoba: brojZastupanihOsoba,
        // Oslobođenja — Zakon o sudskim pristojbama, čl. 11–13
        oslobodjenje_kategorija: oslobodjenjeKategorija || undefined,
        broj_oslobodjenih_osoba: brojOslobodjenihOsoba,
        udio_u_vps: oslobodjenjeUlaz.udioVps,
        postotak_uspjeha: oslobodjenjeUlaz.postotakUspjeha,
        // Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26
        vrsta_postupka: vrstaPostupka,
        uzdrzavanje_mjesecno: vpsUlaz.uzdrzavanjeMjesecno,
        zahtjevi: zahtjevi.map((z) => ({ iznos: z.iznos, opis: z.opis, primarni: !!z.primarni })),
        zahtjevi_nacin: zahtjeviNacin ?? undefined,
        pravni_lijek: pravniLijek,
        pravni_lijek_iznos: vpsUlaz.pravniLijekIznos,
        vps_primijeni_na_nagradu: vpsPrimijeniNaNagradu,
        // Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45
        osnovica_tbr7: osnovicaTbr7,
        razina_suda: razinaSuda ?? undefined,
        tbr45_nacin: tbr45Nacin ?? undefined,
        renta_iznos: rentaIznosNum || undefined,
        renta_razdoblje: rentaRazdoblje ?? undefined,
        // Tbr. 41 — povisivanje i snižavanje tarifnih stavaka
        tbr41_povisenje_postotak: tbr41Povisenje,
        tbr41_snizenje_postotak: tbr41Snizenje,
        // Naknada troškova (Tbr. 48.-51.) — ulazi u osnovicu
        naknada_troskova_stavke: naknadaTroskovaStavke
          .filter(naknadaStavkaPopunjena)
          .map(naknadaStavkaZaSlanje),
        // Predujam — prolazna stavka, ne ulazi u osnovicu
      };

      const res = await fetch(`${API_BASE}/api/troskovnik/izracunaj`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...authHeaders },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || `Greška ${res.status}`);
      }

      const data: TroskovnikResponse = await res.json();
      setResult(data);
    } catch (err) {
      setCalcError(err instanceof Error ? err.message : "Nepoznata greška");
    } finally {
      setCalculating(false);
    }
  };

  // ── Save to database ──
  const handleSave = async () => {
    // Ograda, ne samo onemogućen gumb: gumb se može pozvati i tipkovnicom, a
    // demo redak u tuđoj bazi nije nešto što se poslije popravlja.
    if (demo) {
      setCalcError(DEMO_ZASTO_NEAKTIVNO);
      return;
    }
    if (redci.length === 0) {
      setCalcError("Morate odabrati barem jednu stavku.");
      return;
    }
    if (!userId) {
      setCalcError("Morate biti prijavljeni za spremanje.");
      return;
    }
    setCalcError(null);
    setSaving(true);
    setSavedId(null);

    try {
      const body = {
        vrijednost_spora: vrijednostNum,
        stavke: stavkeZaSlanje(),
        dodatne_pristojbe: trosakZaSlanje(dodatnePristojbe),
        predujmovi: trosakZaSlanje(predujmovi),
        napomena: napomena.trim() || undefined,
        odvjetnik: odvjetnikIme
          ? { naziv: odvjetnikIme, oib: odvjetnikOib || undefined, adresa: odvjetnikAdresa || undefined, iban: odvjetnikIban || undefined }
          : undefined,
        predmet: { sud: predmetSud || undefined, poslovni_broj: predmetPoslovniBroj || undefined },
        stranke: {
          zastupana_stranka: klijentIme ? { polozaj: zastupanaPolozaj || undefined, naziv: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined } : undefined,
          protustranka: protustrankaIme ? { polozaj: protustrankaPolozaj || undefined, naziv: protustrankaIme, oib: protustrankaOib || undefined, adresa: protustrankaAdresa || undefined } : undefined
        },
        // legacy
        klijent: klijentIme
          ? { ime: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined, grad: klijentGrad || undefined }
          : undefined,
        protustranka: protustrankaIme
          ? { ime: protustrankaIme, oib: protustrankaOib || undefined, adresa: protustrankaAdresa || undefined, grad: protustrankaGrad || undefined }
          : undefined,
        protustranka_polozaj: protustrankaPolozaj || undefined,
        // Keep legacy `has_vat` for backward compatibility, mirror new flag
        has_vat: isPdvUkljucen,
        is_pdv_ukljucen: isPdvUkljucen,
        // Kazneni fields
        zaprijecena_kazna: zaprijecenaKazna || undefined,
        is_po_sluzbenoj_duznosti: isPoSluzbenojDuznosti,
        is_na_teret_proracuna: isNaTeretProracuna,
        is_izabrani_oslobadjajuca: isIzabraniOslobodjajuca,
        is_zastupnik_djeteta: isZastupnikDjeteta,
        is_stjecaj: isStjecaj,
        is_oduzimanje_prekrsaj: isOduzimanjePrekrsaj,
        vrijednost_oduzimanja: parsedVrijednostOduzimanja,
        is_oslobodjen_pristojbi: isOslobodjenPristojbi,
        // Čl. 2 Uredbe NN 50/2026 — prazan datum znači "nije prijelazni slučaj".
        datum_dospijeca: datumDospijeca || null,
        datum_podnosenja_odgovora_na_tuzbu: datumOdgovoraNaTuzbu || null,
        datum_pokretanja_upravnog_spora: datumPokretanjaUpravnogSpora || null,
        pristojbe_dodane: pristojbeDodane,
        pristojbe_iskljucene: pristojbeIskljucene,
        // Čl. 7 Zakona o sudskim pristojbama — elektronički oblik
        podnesci_elektronicki: podnesciElektronicki,
        zk_upis_elektronicki: zkUpisElektronicki,
        odluke_elektronicki: odlukeElektronicki,
        broj_zastupanih_osoba: brojZastupanihOsoba,
        // Oslobođenja — Zakon o sudskim pristojbama, čl. 11–13
        oslobodjenje_kategorija: oslobodjenjeKategorija || undefined,
        broj_oslobodjenih_osoba: brojOslobodjenihOsoba,
        udio_u_vps: oslobodjenjeUlaz.udioVps,
        postotak_uspjeha: oslobodjenjeUlaz.postotakUspjeha,
        // Vrijednost predmeta spora — Zakon o sudskim pristojbama, čl. 14–26
        vrsta_postupka: vrstaPostupka,
        uzdrzavanje_mjesecno: vpsUlaz.uzdrzavanjeMjesecno,
        zahtjevi: zahtjevi.map((z) => ({ iznos: z.iznos, opis: z.opis, primarni: !!z.primarni })),
        zahtjevi_nacin: zahtjeviNacin ?? undefined,
        pravni_lijek: pravniLijek,
        pravni_lijek_iznos: vpsUlaz.pravniLijekIznos,
        vps_primijeni_na_nagradu: vpsPrimijeniNaNagradu,
        // Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45
        osnovica_tbr7: osnovicaTbr7,
        razina_suda: razinaSuda ?? undefined,
        tbr45_nacin: tbr45Nacin ?? undefined,
        renta_iznos: rentaIznosNum || undefined,
        renta_razdoblje: rentaRazdoblje ?? undefined,
        // Tbr. 41 — povisivanje i snižavanje tarifnih stavaka
        tbr41_povisenje_postotak: tbr41Povisenje,
        tbr41_snizenje_postotak: tbr41Snizenje,
        // Naknada troškova (Tbr. 48.-51.) — ulazi u osnovicu
        naknada_troskova_stavke: naknadaTroskovaStavke
          .filter(naknadaStavkaPopunjena)
          .map(naknadaStavkaZaSlanje),
        // Predujam — prolazna stavka, ne ulazi u osnovicu
        client_id: selectedClientId || undefined,
        // Signal backend to auto-create client from stranke data if no client_id
        auto_create_client: !selectedClientId && !!klijentIme.trim(),
        client_grad: klijentGrad || undefined,
        // Fizička/pravna — da nova stranka ne završi uvijek kao „fizička".
        client_tip: klijentTip,
      };

      const res = await fetch(`${API_BASE}/api/troskovnik/spremi`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...authHeaders },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || `Greška ${res.status}`);
      }

      // API je snake_case (Program.cs postavlja SnakeCaseLower). Ovo je čitalo
      // `savedId` i uvijek dobivalo `undefined`, pa potvrda "Troškovnik
      // spremljen!" nije nikad izlazila, a otvorena stranka se nije upisivala u
      // izbornik — iako je backend oboje uredno vraćao.
      const data: {
        troskovnik: TroskovnikResponse;
        saved_id?: string;
        client_id?: string;
        created_client_id?: string;
      } = await res.json();
      setResult(data.troskovnik);

      // Bez id-a nema spremljenog retka, ma što status rekao. Prikazati potvrdu
      // ovdje značilo bi poslati odvjetnika kući s obračunom kojeg u bazi nema.
      if (!data.saved_id) {
        throw new Error(
          "Troškovnik nije spremljen — baza nije potvrdila upis. " +
          "Izračun je ispravan, pa ga izvezite ili ispišite prije zatvaranja stranice.",
        );
      }

      setSavedId(data.saved_id);

      // It is in the database now. Keeping a copy in the tab would only make it
      // possible to be offered a stale version of something already filed.
      clearDraft(userId);

      // If backend auto-created a client, update local state
      if (data.created_client_id) {
        setSelectedClientId(data.created_client_id);
        setClientAutoFilled(true);
        // Refresh client list so new client appears in combobox
        fetch(`${API_BASE}/api/client`, { headers: authHeaders })
          .then((r) => (r.ok ? r.json() : []))
          .then((freshClients: ClientOption[]) => setClients(freshClients))
          .catch(() => {});
      }
    } catch (err) {
      setCalcError(err instanceof Error ? err.message : "Nepoznata greška");
    } finally {
      setSaving(false);
    }
  };

  // ── Print ──
  const handlePrint = useCallback(() => {
    // Odvjetnik ne smije moći otisnuti troškovnik s izmišljenom strankom i
    // poslati ga sudu.
    if (demo) return;
    window.print();
  }, [demo]);

  // ── Model za izvoz i ispis ──
  // Jedan izvor za obje putanje: `.xlsx` i ispis. Dok su bile odvojene, svaka je
  // imala vlastita pravila o tome koji je odjeljak prisutan.
  const izvozModel = useMemo(
    () =>
      buildIzvozModel({
        result,
        liveCalc,
        pristojbeneStavke: pristojbe.stavke,
        dodatnePristojbe: trosakZaSlanje(dodatnePristojbe),
        predujmovi: trosakZaSlanje(predujmovi),
        isPdvUkljucen,
        napomena,
        polja: {
          odvjetnikIme, odvjetnikOib, odvjetnikAdresa, odvjetnikGrad, odvjetnikIban,
          odvjetnikTelefon, odvjetnikEmail, odvjetnikOsobnoIme,
          klijentIme, zastupanaPolozaj,
          protustrankaIme, protustrankaPolozaj,
          predmetSud, predmetPoslovniBroj,
        },
        // Podaci o predmetu — spremljeni odgovor ih ima gotove; u živom pregledu
        // dolaze izravno s obrasca (izračun sudske pristojbe već ih koristi).
        vrijednostSpora: vrijednostNum,
        vrstaPostupka,
        // Ista verzija po kojoj računa živi pregled (Tbr. 52, po dospijeću).
        tarifa: razrijesiTarifu(datumDospijeca || null),
      }),
    // `pristojbe` je ovdje jer izvoz bez spremljenog rezultata ispisuje retke iz
    // živog pregleda; bez te ovisnosti nosio bi pristojbe od prošle promjene.
    [
      result, liveCalc, pristojbe, dodatnePristojbe, predujmovi, trosakZaSlanje,
      isPdvUkljucen, napomena,
      odvjetnikIme, odvjetnikOib, odvjetnikAdresa, odvjetnikGrad, odvjetnikIban,
      odvjetnikTelefon, odvjetnikEmail, odvjetnikOsobnoIme,
      klijentIme, zastupanaPolozaj, protustrankaIme, protustrankaPolozaj,
      predmetSud, predmetPoslovniBroj, datumDospijeca,
      vrijednostNum, vrstaPostupka,
    ],
  );

  // ── Prijava greške u izračunu (plan pravne zaštite, dio D) ──
  // Samo ono što treba da se izračun ponovi: stavke, vrijednost, datumi i
  // zastavice. Stranke, odvjetnik, sud, broj predmeta i napomena namjerno
  // izostaju — backend ih iz svega što stigne ionako još jednom uklanja.
  const ulazZaPrijavuGreske = () => ({
    vrijednost_spora: vrijednostNum,
    stavke: stavkeZaSlanje(),
    dodatne_pristojbe: trosakZaSlanje(dodatnePristojbe).map(({ datum, iznos }) => ({ datum, iznos })),
    predujmovi: trosakZaSlanje(predujmovi).map(({ datum, iznos }) => ({ datum, iznos })),
    is_pdv_ukljucen: isPdvUkljucen,
    zaprijecena_kazna: zaprijecenaKazna || undefined,
    is_po_sluzbenoj_duznosti: isPoSluzbenojDuznosti,
    is_na_teret_proracuna: isNaTeretProracuna,
    is_izabrani_oslobadjajuca: isIzabraniOslobodjajuca,
    is_zastupnik_djeteta: isZastupnikDjeteta,
    is_stjecaj: isStjecaj,
    is_oduzimanje_prekrsaj: isOduzimanjePrekrsaj,
    vrijednost_oduzimanja: parsedVrijednostOduzimanja,
    is_oslobodjen_pristojbi: isOslobodjenPristojbi,
    datum_dospijeca: datumDospijeca || null,
    datum_podnosenja_odgovora_na_tuzbu: datumOdgovoraNaTuzbu || null,
    datum_pokretanja_upravnog_spora: datumPokretanjaUpravnogSpora || null,
    pristojbe_dodane: pristojbeDodane,
    pristojbe_iskljucene: pristojbeIskljucene,
    podnesci_elektronicki: podnesciElektronicki,
    zk_upis_elektronicki: zkUpisElektronicki,
    odluke_elektronicki: odlukeElektronicki,
    broj_zastupanih_osoba: brojZastupanihOsoba,
    oslobodjenje_kategorija: oslobodjenjeKategorija || undefined,
    broj_oslobodjenih_osoba: brojOslobodjenihOsoba,
    vrsta_postupka: vrstaPostupka,
    zahtjevi: zahtjevi.map((z) => ({ iznos: z.iznos, primarni: !!z.primarni })),
    zahtjevi_nacin: zahtjeviNacin ?? undefined,
    pravni_lijek: pravniLijek,
    vps_primijeni_na_nagradu: vpsPrimijeniNaNagradu,
    osnovica_tbr7: osnovicaTbr7,
    razina_suda: razinaSuda ?? undefined,
    tbr45_nacin: tbr45Nacin ?? undefined,
    renta_iznos: rentaIznosNum || undefined,
    renta_razdoblje: rentaRazdoblje ?? undefined,
    tbr41_povisenje_postotak: tbr41Povisenje,
    tbr41_snizenje_postotak: tbr41Snizenje,
    naknada_troskova_stavke: naknadaTroskovaStavke.filter(naknadaStavkaPopunjena).map(naknadaStavkaZaSlanje),
  });

  // ── Izvoz u Excel ──
  const handleExportXlsx = useCallback(async () => {
    // Isti razlog kao kod ispisa: tablica s izmišljenom strankom ne smije izaći
    // iz aplikacije kao datoteka.
    if (demo) return;
    if (izvozModel.radnje.length === 0) return;
    // Dinamički, da biblioteka ne uđe u početni bundle stranice.
    const { default: writeXlsxFile } = await import("write-excel-file/browser");
    // Preglednička inačica vraća { toBlob, toFile } — datoteka se preuzima tek ovdje.
    await writeXlsxFile(buildTroskovnikSheet(izvozModel), {
      sheet: "Troškovnik",
      columns: STUPCI,
    }).toFile(nazivDatoteke(new Date()));
  }, [izvozModel, demo]);

  // ═══════════════════════════════════════════════════════════════
  //  RENDER
  // ═══════════════════════════════════════════════════════════════

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-white">
      {/* ── Sticky Header — jedan red, ≤56px na mobitelu ── */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40 print:hidden">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-2.5 sm:py-3 flex items-center gap-2 sm:gap-4">
          <Link
            href="/"
            className="relative flex items-center justify-center sm:justify-start gap-2 text-slate-400 hover:text-slate-700 transition text-sm max-sm:min-h-12 max-sm:min-w-12 max-sm:-ml-2"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Početna</span>
          </Link>
          <div className="hidden sm:block h-5 w-px bg-slate-200" />
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-7 h-7 shrink-0 rounded-lg bg-indigo-100 flex items-center justify-center">
              <Calculator className="h-4 w-4 text-indigo-700" />
            </div>
            <div className="min-w-0">
              <h1 className="text-sm font-bold text-slate-900 truncate">Odvjetnički Troškovnik</h1>
              <p className="hidden sm:block text-[10px] text-slate-400">Tarifa NN 2023 · 1 bod = 2,00 €</p>
            </div>
          </div>
          {/* Action buttons — vidljivi i na mobitelu (ikona bez naziva) jer su
              dio obveznog toka: dodavanje radnje → spremanje → ispis. */}
          <div className="ml-auto flex items-center gap-1 sm:gap-2 shrink-0">
            {/* Gumb „Vodič" je preselio u zaglavlje aplikacije (`AppHeader`),
                kao stavka izbornika: vodiča je sad troje, pa jedan klik ovdje
                više ne bi značio jednu stvar. */}
            {liveCalc.lines.length > 0 && (
              <>
                <button
                  onClick={handleExportXlsx}
                  disabled={demo}
                  title={demo ? DEMO_ZASTO_NEAKTIVNO : "Izvoz u Excel"}
                  className="flex items-center justify-center gap-1.5 text-xs text-slate-500 hover:text-slate-700 disabled:text-slate-300 disabled:hover:bg-transparent disabled:cursor-not-allowed transition max-sm:min-h-12 max-sm:min-w-12 sm:px-2.5 sm:py-1.5 rounded-lg hover:bg-slate-100"
                >
                  <Download className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
                  <span className="hidden sm:inline">Excel</span>
                </button>
                <button
                  onClick={handlePrint}
                  disabled={demo}
                  title={demo ? DEMO_ZASTO_NEAKTIVNO : "Ispis"}
                  className="flex items-center justify-center gap-1.5 text-xs text-slate-500 hover:text-slate-700 disabled:text-slate-300 disabled:hover:bg-transparent disabled:cursor-not-allowed transition max-sm:min-h-12 max-sm:min-w-12 sm:px-2.5 sm:py-1.5 rounded-lg hover:bg-slate-100"
                >
                  <Printer className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
                  <span className="hidden sm:inline">Ispis</span>
                </button>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6 print:hidden">
        {/* ── Nedovršeni troškovnik iz prekinutog rada ── */}
        {ponudjeniNacrt && (
          <div className="mb-6 bg-amber-50 border border-amber-200 rounded-xl p-4 print:hidden">
            <div className="flex flex-wrap items-center gap-3">
              <ClipboardList className="h-5 w-5 text-amber-600 shrink-0" />
              <p className="text-sm text-amber-900 flex-1 min-w-[16rem]">
                Imate nedovršen troškovnik od{" "}
                <span className="font-semibold">
                  {ponudjeniNacrt.savedAt.toLocaleTimeString("hr-HR", {
                    hour: "2-digit",
                    minute: "2-digit",
                  })}
                </span>
                . Želite li nastaviti gdje ste stali?
              </p>
              <div className="flex items-center gap-2 ml-auto">
                <button
                  onClick={() => {
                    clearDraft(userId);
                    setPonudjeniNacrt(null);
                  }}
                  className="text-xs text-amber-700 hover:text-amber-900 transition px-3 py-2"
                >
                  Odbaci
                </button>
                <button
                  onClick={() => vratiNacrt(ponudjeniNacrt.data)}
                  className="inline-flex items-center gap-1.5 bg-amber-600 hover:bg-amber-700 text-white text-xs font-medium px-4 py-2 rounded-lg transition"
                >
                  Nastavi
                  <ChevronRight className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          </div>
        )}

        {klonInfo && (
          <div className="mb-6 bg-indigo-50 border border-indigo-200 rounded-xl p-4 print:hidden">
            <div className="flex flex-wrap items-start gap-3">
              <Info className="h-5 w-5 text-indigo-600 shrink-0 mt-0.5" />
              <p className="text-sm text-indigo-900 flex-1 min-w-[16rem]">
                Forma je popunjena prema troškovniku spremljenom{" "}
                <span className="font-semibold">
                  {new Date(klonInfo.datum).toLocaleDateString("hr-HR")}
                </span>
                . Provjerite sve stavke prije spremanja — izvorni zapis ostaje nepromijenjen,
                a ovo spremanje stvara nov troškovnik.
              </p>
              <button
                onClick={() => setKlonInfo(null)}
                className="text-indigo-600 hover:text-indigo-900 shrink-0"
                aria-label="Zatvori"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}

        {klonGreska && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-xl p-4 print:hidden">
            <div className="flex items-center gap-3">
              <AlertCircle className="h-5 w-5 text-red-600 shrink-0" />
              <p className="text-sm text-red-900 flex-1">{klonGreska}</p>
              <button
                onClick={() => setKlonGreska(null)}
                className="text-red-600 hover:text-red-900 shrink-0"
                aria-label="Zatvori"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}

        {/* ── Info Banner ── */}
        <div className="mb-6 bg-indigo-50 border border-indigo-200 rounded-xl p-4 print:hidden">
          <div className="flex items-start gap-3">
            <Scale className="h-5 w-5 text-indigo-600 mt-0.5 shrink-0" />
            <div className="text-sm text-indigo-800">
              <p className="font-semibold mb-1">Odvjetnički troškovnik — prema Tarifi o nagradama (NN 2023)</p>
              <p className="text-indigo-700 text-xs leading-relaxed">
                Unesite vrijednost predmeta spora, odaberite stavke iz tarife i sustav automatski
                izračunava bodove i iznos u eurima (1 bod = 2,00 €). PDV od 25% dodaje se na ukupan iznos.
              </p>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* ═══════════════════════════════════════════════════════
              LEFT COLUMN — Catalog & Selection
          ═══════════════════════════════════════════════════════ */}
          <div className="lg:col-span-2 space-y-4 print:hidden">
            {/* ── Datum dospijeća (Tbr. 52) — bira verziju Tarife; vrijedi i za kazneni ── */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm">
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
                Datum dospijeća (Tbr. 52)
              </label>
              <input
                type="date"
                value={datumDospijeca}
                onChange={(e) => setDatumDospijeca(e.target.value)}
                className="w-full px-3 py-2.5 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
              />
              <p className="mt-1.5 text-[11px] text-slate-500">
                Mjerodavna Tarifa i vrijednost boda određuju se prema dospijeću potraživanja,
                ne prema datumu radnje. Prazno = trenutno važeća Tarifa (HOK, 14.10.2023., {fmtEur(BOD_VALUE)}/bod).
              </p>
            </div>

            {/* ── Case value input (hidden when only kazneni items) ── */}
            {!isOnlyKazneni && (
            <div data-vodic="vodic-vps" className={`bg-white rounded-xl border border-slate-200 p-4 shadow-sm transition`}>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
                <Euro className="h-3.5 w-3.5 inline mr-1 -mt-0.5" />
                Vrijednost predmeta spora (EUR)
              </label>
              <div className="flex items-center gap-3">
                <input
                  type="text"
                  inputMode="decimal"
                  value={vrijednostSpora}
                  onChange={(e) => setVrijednostSpora(e.target.value)}
                  placeholder="npr. 50000"
                  className="flex-1 px-3 py-2.5 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
                />
                <div className="text-right shrink-0">
                  <div className="text-xs text-slate-500">Osnovica</div>
                  <div className="text-lg font-bold text-indigo-600">
                    {osnovica ? `${fmtNum(baseBodovi)} bod.` : "—"}
                  </div>
                </div>
              </div>

              {/* ── Vrsta postupka: Zakon o sudskim pristojbama, čl. 15–25 ──
                  Stoji IZNAD osnovice po Tbr. 7 jer VPS logički prethodi osnovici:
                  prvo se utvrdi vrijednost, pa se na nju primijeni Tarifa. */}
              <div className="mt-3 pt-3 border-t border-dashed border-slate-200 space-y-2">
                <label className="block text-[11px] font-semibold text-slate-600">
                  Vrsta postupka (Zakon o sudskim pristojbama, čl. 15–25)
                </label>
                <select
                  value={vrstaPostupka}
                  onChange={(e) => {
                    const nova = e.target.value as VrstaPostupka;
                    setVrstaPostupka(nova);
                    if (VRSTA_POSTUPKA_PRAVILA[nova].nacin !== "uzdrzavanje") setUzdrzavanjeMjesecno("");
                  }}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
                >
                  {Object.values(VRSTA_POSTUPKA).map((v) => (
                    <option key={v} value={v}>
                      {VRSTA_POSTUPKA_LABELE[v]}
                    </option>
                  ))}
                </select>

                {/* Čl. 15 — zbroj davanja za tri mjeseca */}
                {VRSTA_POSTUPKA_PRAVILA[vrstaPostupka].nacin === "uzdrzavanje" && (
                  <input
                    type="text"
                    inputMode="decimal"
                    value={uzdrzavanjeMjesecno}
                    onChange={(e) => setUzdrzavanjeMjesecno(e.target.value)}
                    placeholder="mjesečno davanje u € (obavezno)"
                    className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                )}

                {/* Čl. 17, 18, 20, 21 — Zakon kaže ŠTO se upisuje, ne koliko */}
                {VRSTA_POSTUPKA_PRAVILA[vrstaPostupka].naputak && (
                  <p className="text-[10px] text-slate-500 leading-snug">
                    {VRSTA_POSTUPKA_PRAVILA[vrstaPostupka].naputak}
                  </p>
                )}

                {/* Zakon je promijenio uneseni iznos — objasni i ponudi prijenos na nagradu */}
                {vpsRezultat?.prilagodjen && vpsRezultat.napomena && (
                  <div className="rounded-lg bg-amber-50 border border-amber-200 p-2.5 space-y-2">
                    <p className="text-[10px] text-amber-800 leading-snug">{vpsRezultat.napomena}</p>
                    <p className="text-[11px] font-semibold text-amber-900">
                      Za sudsku pristojbu vrijedi {fmtEur(vpsRezultat.vps)}
                    </p>
                    <label className="flex items-start gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={vpsPrimijeniNaNagradu}
                        onChange={(e) => setVpsPrimijeniNaNagradu(e.target.checked)}
                        className="mt-0.5 rounded border-amber-300 text-amber-600 focus:ring-amber-500"
                      />
                      <span className="text-[10px] text-amber-800 leading-snug">
                        Primijeni i na odvjetničku nagradu (Tbr. 7. toč. 1). Čl. 14–26 Zakona
                        propisuju vrijednost za sudske pristojbe, ne za Tarifu — zato odabir.
                      </span>
                    </label>
                  </div>
                )}
              </div>

              {/* ── Čl. 16 — više zahtjeva iz iste tužbe ── */}
              <div className="mt-3 pt-3 border-t border-dashed border-slate-200 space-y-2">
                <div className="flex items-center justify-between">
                  <label className="block text-[11px] font-semibold text-slate-600">
                    Više zahtjeva (čl. 16)
                  </label>
                  <button
                    type="button"
                    onClick={() => setZahtjevi((prev) => [...prev, { iznos: 0, opis: "" }])}
                    className="text-[10px] text-indigo-600 hover:text-indigo-800 font-semibold"
                  >
                    + dodaj zahtjev
                  </button>
                </div>

                {zahtjevi.length === 0 ? (
                  <p className="text-[10px] text-slate-400 leading-snug">
                    Bez unesenih zahtjeva vrijedi gornji iznos.
                  </p>
                ) : (
                  <div className="space-y-2">
                    <select
                      value={zahtjeviNacin ?? ""}
                      onChange={(e) => setZahtjeviNacin((e.target.value || null) as ZahtjeviNacin | null)}
                      className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                    >
                      <option value="">— način (obavezno) —</option>
                      {Object.values(ZAHTJEVI_NACIN).map((n) => (
                        <option key={n} value={n}>
                          {ZAHTJEVI_NACIN_LABELE[n]}
                        </option>
                      ))}
                    </select>

                    {zahtjevi.map((z, i) => (
                      <div key={i} className="flex items-center gap-2">
                        <input
                          type="text"
                          value={z.opis ?? ""}
                          onChange={(e) =>
                            setZahtjevi((prev) =>
                              prev.map((s, j) => (j === i ? { ...s, opis: e.target.value } : s)),
                            )
                          }
                          placeholder="opis"
                          className="flex-1 px-2.5 py-1.5 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                        />
                        <input
                          type="text"
                          inputMode="decimal"
                          value={z.iznos === 0 ? "" : String(z.iznos)}
                          onChange={(e) =>
                            setZahtjevi((prev) =>
                              prev.map((s, j) =>
                                j === i ? { ...s, iznos: brojIliNula(e.target.value) } : s,
                              ),
                            )
                          }
                          placeholder="iznos €"
                          className="w-28 px-2.5 py-1.5 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                        />
                        {/* Suparničarstvo: točno jedan zahtjev nosi ovaj troškovnik */}
                        {zahtjeviNacin === ZAHTJEVI_NACIN.Zasebno && (
                          <label className="flex items-center gap-1 shrink-0" title="Zahtjev koji ovaj troškovnik pokriva">
                            <input
                              type="radio"
                              name="zahtjev-primarni"
                              checked={!!z.primarni}
                              onChange={() =>
                                setZahtjevi((prev) => prev.map((s, j) => ({ ...s, primarni: j === i })))
                              }
                              className="text-indigo-600 focus:ring-indigo-500"
                            />
                            <span className="text-[10px] text-slate-500">ovaj</span>
                          </label>
                        )}
                        <button
                          type="button"
                          onClick={() => setZahtjevi((prev) => prev.filter((_, j) => j !== i))}
                          className="text-slate-300 hover:text-red-500 transition shrink-0"
                          aria-label="Ukloni zahtjev"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* ── Čl. 24 — pravni lijekovi ── */}
              <div className="mt-3 pt-3 border-t border-dashed border-slate-200 space-y-2">
                <label className="block text-[11px] font-semibold text-slate-600">
                  Pravni lijek (čl. 24)
                </label>
                <select
                  value={pravniLijek}
                  onChange={(e) => {
                    const novi = e.target.value as PravniLijek;
                    setPravniLijek(novi);
                    if (novi === PRAVNI_LIJEK.Nema || novi === PRAVNI_LIJEK.CijelaOdluka)
                      setPravniLijekIznos("");
                  }}
                  className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  {Object.values(PRAVNI_LIJEK).map((l) => (
                    <option key={l} value={l}>
                      {PRAVNI_LIJEK_LABELE[l]}
                    </option>
                  ))}
                </select>
                {(pravniLijek === PRAVNI_LIJEK.PobijaniDio ||
                  pravniLijek === PRAVNI_LIJEK.SamoTroskovi) && (
                  <input
                    type="text"
                    inputMode="decimal"
                    value={pravniLijekIznos}
                    onChange={(e) => setPravniLijekIznos(e.target.value)}
                    placeholder={
                      pravniLijek === PRAVNI_LIJEK.PobijaniDio
                        ? "vrijednost pobijanog dijela u € (obavezno)"
                        : "iznos troškova u € (obavezno)"
                    }
                    className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                )}
              </div>

              {/* ── Osnovica: Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45 ── */}
              <div className="mt-3 pt-3 border-t border-dashed border-slate-200 space-y-2">
                <label className="block text-[11px] font-semibold text-slate-600">
                  Osnovica po Tbr. 7. toč. 1., 4., 7. i 8.
                </label>
                <select
                  value={osnovicaTbr7}
                  onChange={(e) => {
                    const nova = e.target.value as OsnovicaTbr7;
                    setOsnovicaTbr7(nova);
                    if (nova !== OSNOVICA_TBR7.Tbr45Neprocjenjiv) {
                      setRazinaSuda(null);
                      setTbr45Nacin(null);
                    }
                    if (nova !== OSNOVICA_TBR7.Toc4Renta) {
                      setRentaIznos("");
                      setRentaRazdoblje(null);
                    }
                  }}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
                >
                  {Object.values(OSNOVICA_TBR7).map((o) => (
                    <option key={o} value={o}>
                      {OSNOVICA_LABELE[o]}
                    </option>
                  ))}
                </select>
                {osnovica && (
                  <p className="text-[10px] text-slate-400 leading-snug">{osnovica.pravniIzvor}</p>
                )}

                {/* Tbr. 7. toč. 4 al. 2 — petogodišnji iznos rente / zakupa / najma */}
                {osnovicaTbr7 === OSNOVICA_TBR7.Toc4Renta && (
                  <div className="rounded-lg bg-slate-50 border border-slate-200 p-2.5 space-y-2">
                    <p className="text-[10px] text-slate-500 leading-snug">
                      Kao vrijednost spora uzima se petogodišnji iznos. Unesite iznos i razdoblje —
                      bez toga se ne preračunava ništa.
                    </p>
                    <div className="flex items-center gap-2">
                      <input
                        type="text"
                        inputMode="decimal"
                        value={rentaIznos}
                        onChange={(e) => setRentaIznos(e.target.value)}
                        placeholder="iznos u €"
                        className="flex-1 px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                      />
                      <select
                        value={rentaRazdoblje ?? ""}
                        onChange={(e) =>
                          setRentaRazdoblje((e.target.value || null) as RentaRazdoblje | null)
                        }
                        className="px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                      >
                        <option value="">— razdoblje —</option>
                        <option value={RENTA_RAZDOBLJE.Mjesecno}>mjesečno (× 60)</option>
                        <option value={RENTA_RAZDOBLJE.Godisnje}>godišnje (× 5)</option>
                      </select>
                    </div>
                    {rentaRazdoblje && rentaIznosNum > 0 && (
                      <p className="text-[10px] text-indigo-600 font-semibold">
                        Petogodišnji iznos: {fmtEur(efektivniVpsNum)}
                      </p>
                    )}
                  </div>
                )}

                {/* Tbr. 45 — razina suda i način obračuna */}
                {osnovicaTbr7 === OSNOVICA_TBR7.Tbr45Neprocjenjiv && (
                  <div className="rounded-lg bg-slate-50 border border-slate-200 p-2.5 space-y-2">
                    <select
                      value={razinaSuda ?? ""}
                      onChange={(e) => setRazinaSuda((e.target.value || null) as RazinaSuda | null)}
                      className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                    >
                      <option value="">— razina suda (obavezno) —</option>
                      {Object.values(RAZINA_SUDA).map((r) => (
                        <option key={r} value={r}>
                          {RAZINA_SUDA_LABELE[r]}
                        </option>
                      ))}
                    </select>
                    <select
                      value={tbr45Nacin ?? ""}
                      onChange={(e) => setTbr45Nacin((e.target.value || null) as Tbr45Nacin | null)}
                      className="w-full px-2.5 py-2 border border-slate-200 rounded-lg text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                    >
                      <option value="">— način obračuna (obavezno) —</option>
                      {Object.values(TBR45_NACIN).map((n) => (
                        <option key={n} value={n}>
                          {TBR45_NACIN_LABELE[n]}
                        </option>
                      ))}
                    </select>
                    <p className="text-[10px] text-slate-500 leading-snug">
                      Tarifa ne kaže primjenjuju li se postoci iz Tbr. 8. i 9. na iznos iz Tbr. 45.
                      Oba čitanja su dostupna; odaberite ono koje primjenjujete.
                    </p>
                  </div>
                )}
              </div>
            </div>
            )}

            {/* ── Tbr. 41 — povisivanje i snižavanje tarifnih stavaka ── */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm space-y-3">
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider">
                <Scale className="h-3.5 w-3.5 inline mr-1 -mt-0.5" />
                Povisivanje i snižavanje (Tbr. 41)
              </label>

              <div>
                <div className="flex justify-between text-[11px] text-slate-600 mb-1">
                  <span>Povišenje — posebna stručnost, težina, odgovornost (toč. 1)</span>
                  <span className="font-semibold text-indigo-600">+{tbr41Povisenje} %</span>
                </div>
                <input
                  type="range"
                  min={0}
                  max={TBR41_MAX_POVISENJE}
                  step={5}
                  value={tbr41Povisenje}
                  onChange={(e) => setTbr41Povisenje(Number(e.target.value))}
                  className="w-full accent-indigo-600"
                />
              </div>

              <div>
                <div className="flex justify-between text-[11px] text-slate-600 mb-1">
                  <span>Sniženje (toč. 2)</span>
                  <span className="font-semibold text-amber-600">−{tbr41Snizenje} %</span>
                </div>
                <input
                  type="range"
                  min={0}
                  max={TBR41_MAX_SNIZENJE}
                  step={5}
                  value={tbr41Snizenje}
                  onChange={(e) => setTbr41Snizenje(Number(e.target.value))}
                  className="w-full accent-amber-600"
                />
              </div>

              <button
                onClick={primijeniTbr41NaSve}
                disabled={brojPojedinacnihTbr41 === 0}
                className="w-full text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:text-slate-300 bg-indigo-50 hover:bg-indigo-100 disabled:bg-slate-50 py-2 rounded-lg transition"
              >
                Primijeni na sve stavke
                {brojPojedinacnihTbr41 > 0 && ` (poništi ${brojPojedinacnihTbr41} pojedinačnih)`}
              </button>

              <p className="text-[10px] text-slate-400 leading-snug">
                Toč. 2 kaže „POJEDINE tarifne stavke odvjetnik može sniziti za 50%", pa se
                prilagodba veže uz stavku — svaki redak troškovnika ima vlastiti izbornik.
                Gornji klizači vrijede za stavke bez vlastitog postotka. Toč. 1 dopušta
                povišenje do 100 %, toč. 2 sniženje do 50 %; na istu stavku ne mogu oba.
                Zbraja se s Tbr. 40; sudsku pristojbu ne dira jer nije odvjetnička nagrada.
              </p>
            </div>

            {/* ── Tbr. 40 — zastupanje više osoba ── */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm">
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
                <Users className="h-3.5 w-3.5 inline mr-1 -mt-0.5" />
                Broj zastupanih osoba (Tbr. 40)
              </label>
              <div className="flex items-center gap-3">
                <input
                  type="number"
                  min={1}
                  step={1}
                  value={brojZastupanihOsoba}
                  onChange={(e) => setBrojZastupanihOsoba(Math.max(1, parseInt(e.target.value, 10) || 1))}
                  className="w-24 px-3 py-2.5 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
                />
                <div className="flex-1 text-right">
                  <div className="text-lg font-bold text-indigo-600">
                    +{Math.round(liveCalc.povisenjePostotak * 100)} %
                  </div>
                  <p className="text-[10px] text-slate-400 leading-snug">
                    10 % za drugu i svaku sljedeću osobu, najviše 50 %
                  </p>
                </div>
              </div>
            </div>

            {/* ── Kazneni postupak panel ── */}
            {isKazneniMode && (
            <div className="bg-white rounded-xl border border-red-200 p-4 shadow-sm space-y-3">
              <div className="flex items-center gap-2 mb-1">
                <Scale className="h-4 w-4 text-red-600" />
                <span className="text-xs font-semibold text-red-700 uppercase tracking-wider">Kazneni postupak — parametri</span>
              </div>

              {/* Zapriječena kazna dropdown */}
              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">Zapriječena kazna</label>
                <select
                  value={zaprijecenaKazna}
                  onChange={(e) => setZaprijecenaKazna(e.target.value)}
                  className="w-full px-3 py-2.5 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-red-500 focus:border-red-500 outline-none transition"
                >
                  <option value="">— odaberi zapriječenu kaznu —</option>
                  {Object.entries(ZAPRIJECENA_KAZNA_TIERS).map(([key, tier]) => (
                    <option key={key} value={key}>
                      {tier.label} ({tier.bodovi} bod.)
                    </option>
                  ))}
                </select>
                {zaprijecenaKazna && (
                  <p className="mt-1 text-[10px] text-red-600">
                    Tbr. 4.1 bazni bodovi: <span className="font-semibold">{kazneniTierBodovi}</span>
                  </p>
                )}
              </div>

              {/* Boolean modifiers */}
              <div className="space-y-2">
                {/* Uloga odvjetnika — isključive mogućnosti, vidi KAZNENI_STATUS_OPCIJE */}
                <fieldset className="space-y-2">
                  <legend className="text-xs font-semibold text-slate-600 mb-1">Uloga odvjetnika</legend>
                  {KAZNENI_STATUS_OPCIJE.map((opcija) => (
                    <label key={opcija.value} className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name="kazneniStatus"
                        value={opcija.value}
                        checked={kazneniStatus === opcija.value}
                        onChange={() => setKazneniStatus(opcija.value)}
                        className="h-4 w-4 border-slate-300 text-red-600 focus:ring-red-500"
                      />
                      <span className="text-xs text-slate-700">
                        {opcija.label} <span className="text-slate-400">({opcija.napomena})</span>
                      </span>
                    </label>
                  ))}
                </fieldset>

                {/* Stjecaj / produljeno kazneno djelo */}
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isStjecaj}
                    onChange={(e) => setIsStjecaj(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-red-600 focus:ring-red-500"
                  />
                  <span className="text-xs text-slate-700">Stjecaj / produljeno kazneno djelo</span>
                </label>
                {isStjecaj && (
                  <p className="ml-6 text-[10px] text-red-600 italic leading-relaxed">
                    Za kaznena djela u stjecaju i produljena kaznena djela, odaberite tier prema <strong>najtežoj kazni</strong> koja se u postupku može izreći (Tbr. 4.1).
                  </p>
                )}

                {/* Prekršaj s oduzimanjem predmeta */}
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isOduzimanjePrekrsaj}
                    onChange={(e) => setIsOduzimanjePrekrsaj(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-red-600 focus:ring-red-500"
                  />
                  <span className="text-xs text-slate-700">Prekršaj s oduzimanjem predmeta / imovinske koristi <span className="text-slate-400">(Tbr. 7.1 po vrijednosti)</span></span>
                </label>
                {isOduzimanjePrekrsaj && (
                  <div className="ml-6 space-y-1">
                    <label className="block text-[10px] font-semibold text-slate-600">Vrijednost oduzetog predmeta / imovinske koristi (EUR)</label>
                    <input
                      type="text"
                      inputMode="decimal"
                      value={vrijednostOduzimanja}
                      onChange={(e) => setVrijednostOduzimanja(e.target.value)}
                      placeholder="0,00"
                      className="w-full px-3 py-2 border border-red-200 rounded-lg text-sm focus:ring-2 focus:ring-red-500 focus:border-red-500 outline-none transition"
                    />
                    {parsedVrijednostOduzimanja > 0 && (
                      <p className="text-[10px] text-red-600">
                        Tbr. 7.1 bodovi za oduzimanje: <span className="font-semibold">{fmtNum(tbr71Bodovi(parsedVrijednostOduzimanja))}</span>
                      </p>
                    )}
                  </div>
                )}
              </div>
            </div>
            )}

            {/* ── Search ── */}
            <div data-vodic="vodic-radnje" className={`relative rounded-xl transition`}>
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Pretraži stavke tarife..."
                className="w-full pl-10 pr-4 py-2.5 border border-slate-200 rounded-xl text-sm bg-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition shadow-sm"
              />
            </div>

            {/* ── Loading / Error ── */}
            {loadingKatalog && (
              <div className="flex items-center justify-center py-12 text-slate-400">
                <Loader2 className="h-6 w-6 animate-spin mr-2" />
                Učitavanje tarife...
              </div>
            )}

            {katalogError && (
              <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-sm text-red-700 flex items-start gap-2">
                <AlertCircle className="h-5 w-5 shrink-0 mt-0.5" />
                <div>
                  <p className="font-semibold">Greška pri učitavanju kataloga</p>
                  <p className="text-xs">{katalogError}</p>
                </div>
              </div>
            )}

            {/* ── Category Accordion ── */}
            {filteredCategories.map(([cat, items]) => {
              const color = getCatColor(cat);
              const isExpanded = expandedCategories.has(cat) || searchTerm.trim().length > 0;
              const selectedInCat = items.filter((i) => redci.some((r) => r.itemId === i.id)).length;

              return (
                <div key={cat} className={`rounded-xl border ${color.border} overflow-hidden shadow-sm`}>
                  {/* Category Header */}
                  <button
                    onClick={() => toggleCategory(cat)}
                    className={`w-full flex items-center justify-between px-4 py-3 ${color.bg} hover:opacity-90 transition`}
                  >
                    <div className="flex items-center gap-2">
                      {isExpanded ? (
                        <ChevronDown className={`h-4 w-4 ${color.text}`} />
                      ) : (
                        <ChevronRight className={`h-4 w-4 ${color.text}`} />
                      )}
                      <span className={`text-sm font-semibold ${color.text}`}>{cat}</span>
                      <span className="text-xs text-slate-400">({items.length})</span>
                    </div>
                    {selectedInCat > 0 && (
                      <span className="text-[10px] font-bold bg-indigo-600 text-white px-2 py-0.5 rounded-full">
                        {selectedInCat} odabrano
                      </span>
                    )}
                  </button>

                  {/* Items */}
                  {isExpanded && (
                    <div className="bg-white divide-y divide-slate-100">
                      {items.map((item) => {
                        // Koliko je redaka ove stavke već u troškovniku — samo prikaz.
                        // Klik na "Dodaj" uvijek dodaje NOVI redak; ista stavka smije
                        // se ponoviti (svaki put s vlastitim datumom i količinom).
                        const brojRedaka = redci.filter((r) => r.itemId === item.id).length;
                        const bodovi = item.prekrsaj_oduzimanje
                          ? (isOduzimanjePrekrsaj && parsedVrijednostOduzimanja > 0
                              ? tbr71Bodovi(parsedVrijednostOduzimanja)
                              : 100)
                          : item.kazneni_base
                          ? izracunajKazneneBodove(item, kazneniTierBodovi, isZastupnikDjeteta, isPoSluzbenojDuznosti, isNaTeretProracuna, zaprijecenaKazna, isIzabraniOslobodjajuca)
                          : osnovica
                          ? izracunajBodove(item, osnovica)
                          : null;
                        const iznosPerUnit = bodovi === null ? 0 : bodovi * BOD_VALUE;

                        return (
                          <div
                            key={item.id}
                            className={`px-4 py-3 flex items-center gap-3 hover:bg-slate-50 transition ${
                              brojRedaka > 0 ? "bg-indigo-50/40" : ""
                            }`}
                          >
                            {/* Info */}
                            <div className="flex-1 min-w-0">
                              <p className="text-sm text-slate-800 leading-snug">{item.opis}</p>
                              <div className="flex items-center gap-3 mt-1 flex-wrap">
                                <span className="text-[10px] font-mono text-slate-400">{item.tarifni_broj}</span>
                                {item.kazneni_base && (
                                  <span className="text-[10px] font-semibold text-red-500 bg-red-50 px-1.5 py-0.5 rounded">
                                    ↔ po Tbr. 4.1
                                  </span>
                                )}
                                {item.prekrsaj_oduzimanje && (
                                  <span className="text-[10px] font-semibold text-orange-600 bg-orange-50 px-1.5 py-0.5 rounded">
                                    {isOduzimanjePrekrsaj ? "↔ Tbr. 7.1 oduzimanje" : "100 bod. (prekršaj)"}
                                  </span>
                                )}
                                {item.jednokratna_nagrada && (
                                  <span className="text-[10px] font-semibold text-emerald-700 bg-emerald-50 px-1.5 py-0.5 rounded">
                                    {item.ukljucene_radnje && item.daljnja_radnja_bodovi
                                      ? `jednokratno · do ${item.ukljucene_radnje} radnje, dalje +${item.daljnja_radnja_bodovi} bod.`
                                      : "jednokratno"}
                                  </span>
                                )}
                                <span className="text-[10px] text-indigo-600 font-semibold">
                                  {item.kazneni_base && !zaprijecenaKazna
                                    ? "— odaberi kaznu —"
                                    : item.prekrsaj_oduzimanje && isOduzimanjePrekrsaj && parsedVrijednostOduzimanja <= 0
                                    ? "— unesi vrijednost oduzimanja —"
                                    : bodovi === null
                                    ? "— dovrši osnovicu —"
                                    : `${fmtNum(bodovi)} bod. = ${fmtEur(iznosPerUnit)}`}
                                </span>
                                <span className="text-[10px] text-slate-400">/ {item.jedinica}</span>
                              </div>
                              <p className="text-[9px] text-slate-400/80 leading-snug mt-1">
                                {item.pravni_izvor ?? item.tarifni_broj}
                              </p>
                            </div>

                            {/* Dodavanje — uvijek dodaje novi redak; količina, datum i
                                Tbr. 41 uređuju se po retku u desnom stupcu "Troškovnik". */}
                            <div className="shrink-0 flex items-center gap-2">
                              {brojRedaka > 0 && (
                                <span className="text-[10px] font-semibold text-indigo-600 bg-indigo-50 px-1.5 py-0.5 rounded">
                                  {brojRedaka}×
                                </span>
                              )}
                              <button
                                onClick={() => addItem(item.id)}
                                className="flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800 bg-indigo-50 hover:bg-indigo-100 px-3 py-1.5 rounded-lg transition"
                              >
                                <Plus className="h-3.5 w-3.5" />
                                Dodaj
                              </button>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}

            {/* ── Party info (Collapsible) ── */}
            <div data-vodic="vodic-stranke" className={`bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden transition`}>
              <button
                onClick={() => setShowParties(!showParties)}
                className="w-full flex items-center justify-between px-4 py-3 hover:bg-slate-50 transition"
              >
                <div className="flex items-center gap-2">
                  <User className="h-4 w-4 text-slate-400" />
                  <span className="text-sm font-semibold text-slate-700">Podaci stranaka</span>
                  <span className="text-[10px] text-slate-400">(opcionalno — za ispis)</span>
                </div>
                <ChevronDown
                  className={`h-4 w-4 text-slate-400 transition ${showParties ? "rotate-180" : ""}`}
                />
              </button>

              {showParties && (
                <div className="px-4 pb-4 space-y-4">
                  {/* Predmet */}
                  <div>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                      <div>
                        <label className="block text-xs font-semibold text-slate-600 mb-1">Sud</label>
                        <input
                          type="text"
                          value={predmetSud}
                          onChange={(e) => setPredmetSud(e.target.value)}
                          placeholder="npr. Općinski sud u Zagrebu"
                          className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-semibold text-slate-600 mb-1">Poslovni broj</label>
                        <input
                          type="text"
                          value={predmetPoslovniBroj}
                          onChange={(e) => setPredmetPoslovniBroj(e.target.value)}
                          placeholder="npr. P-123/24"
                          className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                        />
                      </div>
                    </div>
                  </div>

                  {/* ══ Searchable Client Combobox ══ */}
                  {(clients.length > 0 || userId) && (
                    <div ref={clientContainerRef} className="relative">
                      <label className="block text-xs font-semibold text-slate-600 mb-1">
                        <div className="flex items-center gap-1.5">
                          <Users className="h-3.5 w-3.5 text-slate-400" />
                          Poveži sa strankom
                        </div>
                      </label>

                      {/* Selected state — green badge */}
                      {strankaPovezanaSBazom ? (
                        <div className="flex items-center gap-2 px-3 py-2 border border-emerald-300 bg-emerald-50 rounded-lg text-sm">
                          <div className="flex-1 min-w-0">
                            <span className="font-medium text-emerald-800 truncate">
                              {clients.find((c) => c.id === selectedClientId)?.ime_naziv ?? klijentIme}
                            </span>
                            {klijentOib && (
                              <span className="ml-2 text-emerald-600 text-xs">OIB: {klijentOib}</span>
                            )}
                          </div>
                          <button
                            type="button"
                            onClick={handleClientClear}
                            className="shrink-0 p-1 rounded hover:bg-emerald-100 text-emerald-600 transition"
                            title="Ukloni stranku"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      ) : (
                        /* Search input */
                        <div className="relative">
                          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
                          <input
                            type="text"
                            value={clientSearchQuery}
                            onChange={(e) => {
                              setClientSearchQuery(e.target.value);
                              if (!clientDropdownOpen) setClientDropdownOpen(true);
                            }}
                            onFocus={() => setClientDropdownOpen(true)}
                            placeholder="Pretraži stranke po imenu, OIB-u, adresi..."
                            className="w-full pl-10 pr-10 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition"
                          />
                          <button
                            type="button"
                            onClick={() => setClientDropdownOpen(!clientDropdownOpen)}
                            className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600"
                          >
                            <ChevronDown className={`h-4 w-4 transition ${clientDropdownOpen ? "rotate-180" : ""}`} />
                          </button>
                        </div>
                      )}

                      {/* Dropdown list */}
                      {clientDropdownOpen && !selectedClientId && (
                        <div className="absolute z-50 mt-1 w-full max-h-64 overflow-y-auto bg-white border border-slate-200 rounded-xl shadow-lg">
                          {loadingClients ? (
                            <div className="flex items-center justify-center py-6 text-slate-400">
                              <Loader2 className="h-4 w-4 animate-spin mr-2" />
                              <span className="text-xs">Učitavanje stranaka...</span>
                            </div>
                          ) : filteredClients.length === 0 ? (
                            <div className="py-6 text-center">
                              <p className="text-xs text-slate-400">
                                {clientSearchQuery ? "Nema rezultata pretrage" : "Nema spremljenih stranaka"}
                              </p>
                              <p className="text-[10px] text-slate-400 mt-1">
                                Unesite podatke ručno — stranka će se automatski spremiti
                              </p>
                            </div>
                          ) : (
                            filteredClients.map((c) => (
                              <button
                                key={c.id}
                                type="button"
                                onClick={() => {
                                  handleClientAutoFill(c);
                                  if (!showParties) setShowParties(true);
                                }}
                                className="w-full text-left px-4 py-2.5 hover:bg-indigo-50 transition flex items-center gap-3 border-b border-slate-100 last:border-0"
                              >
                                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100">
                                  {c.tip === "pravna" ? (
                                    <Building2 className="h-3.5 w-3.5 text-slate-500" />
                                  ) : (
                                    <User className="h-3.5 w-3.5 text-slate-500" />
                                  )}
                                </div>
                                <div className="flex-1 min-w-0">
                                  <p className="text-sm font-medium text-slate-800 truncate">{c.ime_naziv}</p>
                                  <div className="flex items-center gap-2 text-[10px] text-slate-400">
                                    {c.oib && <span>OIB: {c.oib}</span>}
                                    {c.grad && <span>· {c.grad}</span>}
                                    {c.tip === "pravna" && <span className="text-indigo-500">Pravna</span>}
                                  </div>
                                </div>
                              </button>
                            ))
                          )}
                        </div>
                      )}

                      {/* Status hints */}
                      {strankaPovezanaSBazom && (
                        <p className="mt-1 text-[10px] text-emerald-600 flex items-center gap-1">
                          <CheckCircle className="h-3 w-3" />
                          Podaci stranke automatski popunjeni iz baze
                        </p>
                      )}
                      {!selectedClientId && klijentIme.trim() && userId && (
                        <p className="mt-1 text-[10px] text-indigo-500 flex items-center gap-1">
                          <Plus className="h-3 w-3" />
                          Nova stranka — automatski će se spremiti pri spremanju troškovnika
                        </p>
                      )}
                    </div>
                  )}

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Odvjetnik */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <Building2 className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-xs font-semibold text-slate-600 uppercase tracking-wider">Odvjetnik</span>
                      </div>
                      <input type="text" value={odvjetnikIme} onChange={(e) => setOdvjetnikIme(e.target.value)} placeholder="Ime / Naziv ureda" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" inputMode="numeric" value={odvjetnikOib} onChange={(e) => setOdvjetnikOib(e.target.value)} placeholder="OIB" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikAdresa} onChange={(e) => setOdvjetnikAdresa(e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikGrad} onChange={(e) => setOdvjetnikGrad(e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikIban} onChange={(e) => setOdvjetnikIban(e.target.value)} placeholder="IBAN (Broj računa)" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" inputMode="tel" value={odvjetnikTelefon} onChange={(e) => setOdvjetnikTelefon(e.target.value)} placeholder="Telefon" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikEmail} onChange={(e) => setOdvjetnikEmail(e.target.value)} placeholder="E-mail ureda" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                    </div>
                  
                    {/* Protustranka */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <User className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-xs font-semibold text-slate-600 uppercase tracking-wider">Protustranka</span>
                      </div>
                      {/* Protivnik iz jednog predmeta redovito je već spremljen kao
                          stranka u drugom — odabir prepisuje četiri polja ispod. */}
                      <StrankaPicker
                        stranke={clients}
                        loading={loadingClients}
                        izostaviId={selectedClientId || undefined}
                        placeholder="Odaberi iz spremljenih stranaka…"
                        onSelect={(s) => {
                          setProtustrankaIme(s.ime_naziv);
                          setProtustrankaOib(s.oib ?? "");
                          setProtustrankaAdresa(s.adresa ?? "");
                          setProtustrankaGrad(s.grad ?? "");
                        }}
                      />
                      <input type="text" value={protustrankaIme} onChange={(e) => setProtustrankaIme(e.target.value)} placeholder="Ime / Naziv" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" inputMode="numeric" value={protustrankaOib} onChange={(e) => setProtustrankaOib(e.target.value)} placeholder="OIB" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={protustrankaAdresa} onChange={(e) => setProtustrankaAdresa(e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={protustrankaGrad} onChange={(e) => setProtustrankaGrad(e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <label className="block text-xs font-semibold text-slate-600 mb-1 mt-2">Položaj protustranke</label>
                      <select
                        value={protustrankaPolozaj}
                        onChange={(e) => setProtustrankaPolozaj(e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                      >
                        <option value="">— odaberi položaj —</option>
                        <option value="tužitelj">tužitelj</option>
                        <option value="tuženik">tuženik</option>
                        <option value="predlagatelj">predlagatelj</option>
                        <option value="protustranka">protustranka</option>
                      </select>
                    </div>

                    {/* Klijent — ZASTUPANA STRANKA (tracks manual edits) */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <User className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-xs font-semibold text-slate-600 uppercase tracking-wider">ZASTUPANA STRANKA</span>
                      </div>
                      <input type="text" value={klijentIme} onChange={(e) => handleKlijentManualEdit(setKlijentIme, e.target.value)} placeholder="Ime / Naziv" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" inputMode="numeric" value={klijentOib} onChange={(e) => handleKlijentManualEdit(setKlijentOib, e.target.value.replace(/\D/g, '').slice(0, 11))} placeholder="OIB (11 znamenki)" maxLength={11} pattern="\d{11}" className={`w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none ${klijentOib && klijentOib.length !== 11 ? 'border-amber-400 bg-amber-50' : 'border-slate-200'}`} />
                      {klijentOib && klijentOib.length > 0 && klijentOib.length !== 11 && (
                        <p className="text-xs text-amber-600">OIB mora sadržavati točno 11 znamenki ({klijentOib.length}/11)</p>
                      )}
                      <input type="text" value={klijentAdresa} onChange={(e) => handleKlijentManualEdit(setKlijentAdresa, e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={klijentGrad} onChange={(e) => handleKlijentManualEdit(setKlijentGrad, e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <label className="block text-xs font-semibold text-slate-600 mb-1 mt-2">
                        Vrsta stranke
                      </label>
                      <select
                        value={klijentTip}
                        onChange={(e) => {
                          setKlijentTip(e.target.value as TipStranke);
                          klijentTipRucno.current = true;
                        }}
                        disabled={strankaPovezanaSBazom}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none disabled:bg-slate-50 disabled:text-slate-400"
                      >
                        <option value="fizicka">Fizička osoba</option>
                        <option value="pravna">Pravna osoba</option>
                      </select>
                      {!strankaPovezanaSBazom && klijentIme.trim() && (
                        <p className="text-[10px] text-slate-400">
                          Predloženo iz naziva — ispravite ako treba. Vrijedi za novu stranku koja će se otvoriti pri spremanju.
                        </p>
                      )}
                      <label className="block text-xs font-semibold text-slate-600 mb-1 mt-2">
                        Položaj zastupane stranke
                      </label>
                      <select
                        value={zastupanaPolozaj}
                        onChange={(e) => setZastupanaPolozaj(e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                      >
                        <option value="">— odaberi položaj —</option>
                        <option value="tužitelj">tužitelj</option>
                        <option value="tuženik">tuženik</option>
                        <option value="predlagatelj">predlagatelj</option>
                        <option value="zastupana">zastupana</option>
                      </select>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* ═══════════════════════════════════════════════════════
              RIGHT COLUMN — Live Summary / Result
          ═══════════════════════════════════════════════════════ */}
          <div className="space-y-4 print:hidden">
            {/* Sticky summary.
                `lg:sticky` je korisno dok se popunjava forma, ali kad korak
                cilja nešto UNUTAR ovog stupca (SIDRA_U_STUPCU), gasi se: jednom
                prilijepljen na `top-20`, sticky element se dalje ne pomiče, pa
                se nižih sidara (pristojbe, ukupno, spremi) ne bi dalo doskrolati
                do vidljivog dijela ekrana. Bez `sticky` stupac je običan blok i
                stranica ga skrola kao svaki drugi cilj.

                Isticanje cilja (rupa u zatamnjenju) više NE ovisi o klasama na
                ovom elementu — vidi doc-komentar na vrhu `Vodic.tsx`. Rupa se
                crta iz izmjerenih koordinata cilja (`getBoundingClientRect()`),
                pa joj nije bitno je li ovaj stupac trenutno `sticky` ili obični
                blok; ovdje se sticky gasi ISKLJUČIVO da bi se cilj uopće mogao
                doskrolati u vidljivi dio ekrana. */}
            <div
              data-vodic="vodic-rezultat"
              className={`space-y-4 rounded-xl transition ${
                vodicCiljaStupac ? "" : "lg:sticky lg:top-20"
              }`}
            >
              {/* ── Selected items summary ── */}
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
                <div className="bg-gradient-to-r from-indigo-600 to-indigo-700 px-4 py-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ClipboardList className="h-4 w-4 text-indigo-200" />
                    <span className="text-sm font-bold text-white">Troškovnik</span>
                  </div>
                  {redci.length > 0 && (
                    <button
                      onClick={clearAll}
                      className="text-[10px] text-indigo-200 hover:text-white transition"
                    >
                      Očisti sve
                    </button>
                  )}
                </div>

                <div className="p-4">
                  {redci.length === 0 ? (
                    <div className="text-center py-8 text-slate-400">
                      <FileText className="h-8 w-8 mx-auto mb-2 opacity-40" />
                      <p className="text-sm">Odaberite stavke iz tarife</p>
                      <p className="text-xs mt-1">Kliknite &quot;Dodaj&quot; pokraj željene stavke</p>
                    </div>
                  ) : (
                    <div
                      data-vodic="vodic-popis-radnji"
                      className={`space-y-2 max-h-[50vh] overflow-y-auto rounded-lg transition`}
                    >
                      {redciPoredani.map((redak, i) => {
                        const def = katalogById.get(redak.itemId);
                        const line = lineByUid.get(redak.uid);

                        return (
                          <div key={redak.uid} className="flex items-start justify-between gap-2 py-1.5 border-b border-slate-100 last:border-0">
                            {/* ▲/▼ — premještanje; rubovi bez efekta.
                                Na mobitelu su ova dva gumba jedan uz drugi i
                                rade suprotne stvari (gore/dolje) — puna 48px
                                dodirna meta bi se preklopila s onom pored, pa
                                bi dodir mogao pomaknuti krivi smjer. Umjesto
                                toga: 32px (veće od izvornih 20px) uz ≥8px
                                razmaka, bez preklapanja. */}
                            <div className="flex flex-col shrink-0 mt-0.5 gap-2">
                              <button
                                type="button"
                                onClick={() => pomakni(redak.uid, -1)}
                                disabled={i === 0}
                                className="w-8 h-8 sm:w-5 sm:h-5 flex items-center justify-center rounded text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 disabled:opacity-20 disabled:hover:bg-transparent transition"
                                title="Pomakni gore"
                              >
                                <ChevronUp className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={() => pomakni(redak.uid, 1)}
                                disabled={i === redciPoredani.length - 1}
                                className="w-8 h-8 sm:w-5 sm:h-5 flex items-center justify-center rounded text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 disabled:opacity-20 disabled:hover:bg-transparent transition"
                                title="Pomakni dolje"
                              >
                                <ChevronDown className="h-4 w-4 sm:h-3.5 sm:w-3.5" />
                              </button>
                            </div>

                            <div className="min-w-0 flex-1">
                              <p className="text-xs text-slate-700 leading-snug truncate" title={def?.opis}>
                                {def?.opis ?? redak.itemId}
                              </p>
                              <p className="text-[10px] text-slate-400 mt-0.5">
                                {line
                                  ? line.is_jednokratna
                                    ? `jednokratno · ${line.kolicina} ${line.kolicina === 1 ? "radnja" : "radnji"} = ${fmtNum(line.ukupno_bodova)} bod.`
                                    : `${line.kolicina} × ${fmtNum(line.bodova_po_jedinici)} bod.`
                                  : "— dovrši parametre iznad —"}
                              </p>
                              {line && (
                                <p className="text-[9px] text-slate-400/80 leading-snug mt-0.5" title={line.pravni_izvor}>
                                  {line.pravni_izvor}
                                </p>
                              )}

                              {/* Datum radnje — opcionalan. Font polja ide na
                                  16px ispod 768px (globals.css) da iOS ne
                                  zumira; red se po potrebi lomi (flex-wrap),
                                  nikad vodoravno pomiče. */}
                              <div className="flex items-center gap-2 mt-1 flex-wrap">
                                <input
                                  type="date"
                                  value={redak.datum ?? ""}
                                  onChange={(e) => postaviDatum(redak.uid, e.target.value)}
                                  className="text-[10px] px-1.5 py-0.5 sm:py-0.5 max-sm:py-1.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                  title="Datum radnje (opcionalno)"
                                />
                                <input
                                  type="number"
                                  inputMode="numeric"
                                  min={1}
                                  value={redak.kolicina}
                                  onChange={(e) => postaviKolicinu(redak.uid, parseInt(e.target.value, 10) || 0)}
                                  className="w-14 text-[10px] px-1.5 py-0.5 max-sm:py-1.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                  title="Količina"
                                />
                                <button
                                  type="button"
                                  onClick={() => obrisiRedak(redak.uid)}
                                  className="w-8 h-8 sm:w-5 sm:h-5 flex items-center justify-center rounded text-red-400 hover:text-red-600 hover:bg-red-50 transition"
                                  title="Obriši redak"
                                >
                                  <Trash2 className="h-3.5 w-3.5 sm:h-3 sm:w-3" />
                                </button>
                              </div>

                              {/* Tbr. 41 — prilagodba ovog retka (toč. 2: "pojedine tarifne stavke") */}
                              <div className="flex items-center gap-1.5 mt-1">
                                <select
                                  value={
                                    redak.tbr41?.povisenje && redak.tbr41.povisenje > 0
                                      ? `p${redak.tbr41.povisenje}`
                                      : redak.tbr41?.snizenje && redak.tbr41.snizenje > 0
                                        ? `s${redak.tbr41.snizenje}`
                                        : ""
                                  }
                                  onChange={(e) => postaviTbr41(redak.uid, e.target.value)}
                                  className="text-[10px] px-1.5 py-0.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                  title="Tbr. 41 — povišenje (toč. 1) ili sniženje (toč. 2) ovog retka"
                                >
                                  <option value="">Tbr. 41: bez prilagodbe</option>
                                  <option value="p25">+25 % (toč. 1)</option>
                                  <option value="p50">+50 % (toč. 1)</option>
                                  <option value="p100">+100 % (toč. 1)</option>
                                  <option value="s25">−25 % (toč. 2)</option>
                                  <option value="s50">−50 % (toč. 2)</option>
                                </select>
                                {line && line.tbr41_bodovi !== 0 && (
                                  <span
                                    className={`text-[10px] font-semibold ${line.tbr41_bodovi > 0 ? "text-indigo-600" : "text-amber-600"}`}
                                    title={line.tbr41_pravni_izvor}
                                  >
                                    {line.tbr41_bodovi > 0 ? "+" : "−"}
                                    {fmtNum(Math.abs(line.tbr41_bodovi))} bod.
                                  </span>
                                )}
                              </div>
                            </div>
                            <span className="text-xs font-semibold text-slate-800 shrink-0 text-right">
                              {line ? fmtEur(line.iznos_eur) : "—"}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  )}

                  {/* Totals */}
                  {redci.length > 0 && (
                    <div className="mt-4 pt-3 border-t border-slate-200 space-y-1.5">
                      {(liveCalc.povisenjePostotak > 0 || liveCalc.tbr41Postotak !== 0) && (
                        <div className="flex justify-between text-xs text-slate-500">
                          <span>Osnovica</span>
                          <span>{fmtNum(liveCalc.osnovicaBodova)} bod.</span>
                        </div>
                      )}
                      {liveCalc.povisenjePostotak > 0 && (
                        <div className="flex justify-between text-xs text-slate-500">
                          <span>
                            Zastupanje {brojZastupanihOsoba} osoba (Tbr. 40) +{Math.round(liveCalc.povisenjePostotak * 100)} %
                          </span>
                          <span>{fmtNum(liveCalc.povisenjeBodovi)} bod.</span>
                        </div>
                      )}
                      {liveCalc.tbr41Postotak !== 0 && (
                        <div className="flex justify-between text-xs text-slate-500">
                          <span>
                            Tbr. 41 {liveCalc.tbr41Postotak > 0 ? "+" : "−"}
                            {Math.abs(Math.round(liveCalc.tbr41Postotak * 100))} %
                            {tbr41Povisenje > 0 && tbr41Snizenje > 0
                              ? ` (+${tbr41Povisenje} % / −${tbr41Snizenje} %)`
                              : ""}
                          </span>
                          <span>{fmtNum(liveCalc.tbr41Bodovi)} bod.</span>
                        </div>
                      )}
                      <div
                        data-vodic="vodic-bodovi"
                        className={`flex justify-between text-xs text-slate-500 rounded transition`}
                      >
                        <span>Ukupno bodova</span>
                        <span className="font-semibold">{fmtNum(liveCalc.ukupnoBodova)}</span>
                      </div>

                      {/* PDV toggle (single) and label */}
                      <div className="flex items-center justify-between text-xs text-slate-500 mt-1">
                        <div className="flex items-center gap-3">
                          <label className="inline-flex items-center gap-2">
                            <input
                              type="checkbox"
                              checked={isPdvUkljucen}
                              onChange={(e) => handlePdvToggle(e.target.checked)}
                              className="h-4 w-4 rounded border-slate-200 text-indigo-600 focus:ring-indigo-500"
                            />
                            <span className="text-xs">U sustavu PDV-a</span>
                          </label>
                        </div>
                        <span className="font-semibold">{isPdvUkljucen ? 'PDV 25%' : 'PDV 0%'}</span>
                      </div>

                      {/* ── Naknada troškova (Tbr. 48.-51.) — ulazi u osnovicu ── */}
                      <div className="pt-2 mt-1 border-t border-dashed border-slate-200 space-y-1.5">
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-semibold text-slate-600">Naknada troškova (Tbr. 48.-51.)</span>
                          <button
                            type="button"
                            onClick={addNaknadaTroskova}
                            className="text-[11px] font-semibold text-indigo-600 hover:text-indigo-800"
                          >
                            + Dodaj
                          </button>
                        </div>
                        {naknadaTroskovaStavke.map((redak, i) => {
                          const dopustaIzracun = VRSTA_TROSKA_DOPUSTA_IZRACUN.includes(redak.vrsta);
                          const jeIzracun = dopustaIzracun && redak.nacin === NACIN_NAKNADE.Izracun;
                          // Jedinična cijena iz postavki ureda — €/km (gorivo × 30%) ili €/dan.
                          // Null dok ured nije postavio vrijednost (nepotvrđeno, vidi Tbr. 50/51).
                          const jedinicnaIzPostavki =
                            redak.vrsta === VRSTA_TROSKA.Prijevoz
                              ? (cijenaGorivaEurL != null ? cijenaGorivaEurL * TBR50_FAKTOR_GORIVA : null)
                              : redak.vrsta === VRSTA_TROSKA.Dnevnica
                              ? dnevnicaEur
                              : null;
                          const izracunatiIznos = jeIzracun
                            ? izracunajNaknaduStavku({
                                vrsta: redak.vrsta,
                                iznos: 0,
                                nacin: NACIN_NAKNADE.Izracun,
                                kolicina: parseHrNumber(redak.kolicina ?? ""),
                                jedinicniIznos: parseHrNumber(redak.jedinicniIznos ?? ""),
                                cestarina: parseHrNumber(redak.cestarina ?? ""),
                              })
                            : null;

                          return (
                          <div key={i} className="space-y-1 rounded border border-transparent has-[:focus]:border-slate-200 pb-1">
                            <div className="flex items-center gap-1.5">
                              <select
                                value={redak.vrsta}
                                onChange={(e) => {
                                  const vrsta = e.target.value as VrstaTroska;
                                  // Izračun je dopušten samo za Prijevoz/Dnevnicu — promjena
                                  // vrste na Hotel/Ostalo vraća redak na ručni unos.
                                  updateNaknadaTroskova(i, VRSTA_TROSKA_DOPUSTA_IZRACUN.includes(vrsta)
                                    ? { vrsta }
                                    : { vrsta, nacin: NACIN_NAKNADE.Rucno });
                                }}
                                className="flex-1 min-w-0 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-slate-700"
                              >
                                {Object.values(VRSTA_TROSKA).map((v) => (
                                  <option key={v} value={v}>{VRSTA_TROSKA_LABELE[v]}</option>
                                ))}
                              </select>
                              <input
                                type="text"
                                placeholder="Opis (neobavezno)"
                                value={redak.opis}
                                onChange={(e) => updateNaknadaTroskova(i, { opis: e.target.value })}
                                className="w-20 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-slate-700 placeholder:text-slate-400"
                              />
                              {jeIzracun ? (
                                <span
                                  className="w-16 rounded border border-slate-100 bg-slate-50 px-1.5 py-1 text-[11px] text-right text-slate-600"
                                  title="Izračunato — količina × jedinični iznos"
                                >
                                  {fmtEur(izracunatiIznos ?? 0)}
                                </span>
                              ) : (
                                <input
                                  type="text"
                                  inputMode="decimal"
                                  placeholder="0,00"
                                  value={redak.iznos}
                                  onChange={(e) => updateNaknadaTroskova(i, { iznos: e.target.value })}
                                  className="w-16 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-right text-slate-700 placeholder:text-slate-400"
                                />
                              )}
                              <button
                                type="button"
                                onClick={() => removeNaknadaTroskova(i)}
                                className="shrink-0 flex items-center justify-center w-8 h-8 sm:w-5 sm:h-5 text-slate-400 hover:text-red-600"
                                aria-label="Ukloni"
                              >
                                <Trash2 className="h-3.5 w-3.5 sm:h-3.5 sm:w-3.5" />
                              </button>
                            </div>

                            {dopustaIzracun && (
                              <label className="flex items-center gap-1.5 pl-1 text-[10px] text-slate-500 cursor-pointer">
                                <input
                                  type="checkbox"
                                  checked={jeIzracun}
                                  onChange={(e) =>
                                    updateNaknadaTroskova(i, e.target.checked
                                      ? {
                                          nacin: NACIN_NAKNADE.Izracun,
                                          jedinicniIznos: redak.jedinicniIznos || (jedinicnaIzPostavki != null ? jedinicnaIzPostavki.toFixed(3) : ""),
                                        }
                                      : { nacin: NACIN_NAKNADE.Rucno })
                                  }
                                  className="h-3 w-3 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                                />
                                Izračunaj ({redak.vrsta === VRSTA_TROSKA.Prijevoz ? "km × €/km" : "dani × €/dan"} — Tbr. {redak.vrsta === VRSTA_TROSKA.Prijevoz ? "50" : "51"})
                              </label>
                            )}

                            {jeIzracun && (
                              <div className="flex flex-wrap items-center gap-1.5 pl-1">
                                <input
                                  type="text"
                                  inputMode="decimal"
                                  placeholder={redak.vrsta === VRSTA_TROSKA.Prijevoz ? "km" : "dani"}
                                  value={redak.kolicina ?? ""}
                                  onChange={(e) => updateNaknadaTroskova(i, { kolicina: e.target.value })}
                                  className="w-14 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-right text-slate-700 placeholder:text-slate-400"
                                />
                                <span className="text-[10px] text-slate-400">×</span>
                                <input
                                  type="text"
                                  inputMode="decimal"
                                  placeholder="€/jed."
                                  value={redak.jedinicniIznos ?? ""}
                                  onChange={(e) => updateNaknadaTroskova(i, { jedinicniIznos: e.target.value })}
                                  className="w-16 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-right text-slate-700 placeholder:text-slate-400"
                                />
                                {redak.vrsta === VRSTA_TROSKA.Prijevoz && (
                                  <>
                                    <span className="text-[10px] text-slate-400">+ cestarina</span>
                                    <input
                                      type="text"
                                      inputMode="decimal"
                                      placeholder="0,00"
                                      value={redak.cestarina ?? ""}
                                      onChange={(e) => updateNaknadaTroskova(i, { cestarina: e.target.value })}
                                      className="w-14 rounded border border-slate-200 px-1.5 py-1 text-[11px] text-right text-slate-700 placeholder:text-slate-400"
                                    />
                                  </>
                                )}
                                {jedinicnaIzPostavki == null && (
                                  <span className="w-full text-[10px] text-amber-600">
                                    {redak.vrsta === VRSTA_TROSKA.Prijevoz
                                      ? "Cijena goriva nije postavljena u Postavke → Ured — unesite €/km ručno ili je ondje postavite."
                                      : "Dnevnica nije postavljena u Postavke → Ured — unesite €/dan ručno ili je ondje postavite."}
                                  </span>
                                )}
                              </div>
                            )}
                          </div>
                          );
                        })}
                      </div>

                      {/* Totals when PDV included */}
                      <div className={`transition-all duration-300 ease-in-out overflow-hidden ${isPdvUkljucen ? 'max-h-96 opacity-100' : 'max-h-0 opacity-0'}`}>
                        <div className="flex justify-between text-xs text-slate-500 mt-2">
                          <span>Osnovica (bez PDV) — nagrada + naknada troškova</span>
                          <span className="font-semibold">{fmtEur(liveCalc.osnovicaSaNaknadom)}</span>
                        </div>
                        <div className="flex justify-between text-xs text-slate-500">
                          <span>PDV (25%)</span>
                          <span className="font-semibold">{fmtEur(liveCalc.pdv)}</span>
                        </div>
                        <div className="flex justify-between text-xs font-bold text-indigo-700 pt-1">
                          <span>Odvjetnički trošak s PDV-om</span>
                          <span>{fmtEur(liveCalc.ukupnoSPdv)}</span>
                        </div>
                      </div>

                      {/* Totals when PDV excluded */}
                      <div className={`transition-all duration-300 ease-in-out overflow-hidden ${isPdvUkljucen ? 'max-h-0 opacity-0' : 'max-h-40 opacity-100'}`}>
                        <div className="flex justify-between text-xs font-bold text-indigo-700 pt-1 mt-2">
                          <span>Odvjetnički trošak — nagrada + naknada troškova</span>
                          <span>{fmtEur(liveCalc.osnovicaSaNaknadom)}</span>
                        </div>
                      </div>

                      {/* ── Sudske pristojbe ── */}
                      <div
                        data-vodic="vodic-pristojbe"
                        className={`pt-2 mt-1 border-t border-dashed border-slate-200 space-y-1.5 rounded-lg transition`}
                      >
                        {pristojbe.stavke.length === 0 ? (
                          <div className="flex items-start gap-1.5 text-[10px] text-slate-500 leading-snug">
                            <Info className="h-3 w-3 shrink-0 mt-0.5 text-slate-400" />
                            <span>{objasnjenjeBezPristojbe()}</span>
                          </div>
                        ) : (
                          <>
                            <div className="flex items-center justify-between text-xs text-slate-500">
                              <label className="inline-flex items-center gap-2">
                                <input
                                  type="checkbox"
                                  checked={isOslobodjenPristojbi}
                                  onChange={(e) => setIsOslobodjenPristojbi(e.target.checked)}
                                  className="h-4 w-4 rounded border-slate-200 text-amber-600 focus:ring-amber-500"
                                />
                                <span className="text-xs">Oslobođenje od pristojbi (čl. 11.)</span>
                              </label>
                            </div>

                            {/* Uredba naplaćuje po radnji — tužba, presuda i žalba tri su pristojbe. */}
                            {pristojbe.stavke.map((s) => (
                              <div key={s.odredba_id} className="space-y-0.5 group">
                                <div className="flex justify-between items-center gap-1 text-xs text-slate-600">
                                  <span className="flex items-center gap-1">
                                    <button
                                      type="button"
                                      onClick={() => makniPristojbu(s.odredba_id, s.automatski)}
                                      title="Makni ovu pristojbu iz obračuna"
                                      aria-label={`Makni pristojbu ${s.tarifni_broj}`}
                                      className="opacity-0 group-hover:opacity-100 focus:opacity-100 text-slate-400 hover:text-rose-600 transition"
                                    >
                                      <X className="h-3 w-3" />
                                    </button>
                                    {s.tarifni_broj}
                                    {s.kolicina > 1 && <span className="text-slate-400"> × {s.kolicina}</span>}
                                  </span>
                                  <span className={`font-semibold ${isOslobodjenPristojbi ? 'line-through text-slate-400' : ''}`}>
                                    {fmtEur(s.iznos)}
                                  </span>
                                </div>
                                {/* Čl. 7 st. 1 i st. 2 — uvjet je vezan uz POJEDINU radnju
                                    (podnesak predan elektronički i plaćen pri podnošenju;
                                    odluku sud dostavio elektronički i plaćeno u 3 dana), pa
                                    oznaka stoji uz redak. Zk upis (st. 3) je jedan uvjet za
                                    cijeli postupak, pa ostaje prekidač ispod. */}
                                {s.cl7_rezim === "st_1" && (
                                  <label className="flex items-start gap-1.5 text-[10px] text-slate-500 leading-snug pl-4">
                                    <input
                                      type="checkbox"
                                      checked={podnesciElektronicki.includes(s.odredba_id)}
                                      onChange={(e) =>
                                        setPodnesciElektronicki((prev) =>
                                          e.target.checked
                                            ? [...prev, s.odredba_id]
                                            : prev.filter((id) => id !== s.odredba_id),
                                        )
                                      }
                                      className="mt-0.5 h-3 w-3"
                                    />
                                    <span>
                                      Podnesak predan elektronički i pristojba plaćena pri
                                      podnošenju <span className="text-slate-400">(čl. 7 st. 1 — polovica)</span>
                                    </span>
                                  </label>
                                )}
                                {s.cl7_rezim === "st_2" && (
                                  <label className="flex items-start gap-1.5 text-[10px] text-slate-500 leading-snug pl-4">
                                    <input
                                      type="checkbox"
                                      checked={odlukeElektronicki.includes(s.odredba_id)}
                                      onChange={(e) =>
                                        setOdlukeElektronicki((prev) =>
                                          e.target.checked
                                            ? [...prev, s.odredba_id]
                                            : prev.filter((id) => id !== s.odredba_id),
                                        )
                                      }
                                      className="mt-0.5 h-3 w-3"
                                    />
                                    <span>
                                      Odluku je sud dostavio elektronički i pristojba je plaćena u
                                      roku od 3 dana <span className="text-slate-400">(čl. 7 st. 2 — polovica)</span>
                                    </span>
                                  </label>
                                )}
                                <p className="text-[10px] text-slate-400 leading-snug">
                                  {s.opis}
                                  {s.umanjeno_elektronicki && (
                                    <span className="ml-1 text-emerald-700">
                                      · umanjeno na polovicu (čl. 7 {s.cl7_rezim.replace("_", ". ")})
                                    </span>
                                  )}
                                  {s.uredba === "NN 37/2023" && (
                                    <span className="ml-1 text-amber-600">
                                      · po staroj Uredbi (NN 37/2023), čl. 2
                                    </span>
                                  )}
                                </p>
                              </div>
                            ))}

                            {pristojbe.stavke.length > 1 && (
                              <div className="flex justify-between text-xs font-semibold text-slate-700 pt-1 border-t border-slate-100">
                                <span>Ukupno sudske pristojbe</span>
                                <span className={isOslobodjenPristojbi ? 'line-through text-slate-400' : ''}>
                                  {fmtEur(liveCalc.sudskaPristojba)}
                                </span>
                              </div>
                            )}

                            {/* Čl. 7 st. 3 — zemljišnoknjižni: jedan uvjet (oblik prijedloga
                                za upis) za cijeli zk postupak, pa jedan prekidač. St. 1 i
                                st. 2 idu po retku, uz svaku pristojbu iznad. Uvjet svake
                                uključuje i plaćanje u roku — st. 4 propušten rok vraća puni iznos. */}
                            {pristojbe.stavke.some((s) => s.cl7_rezim === "st_3") && (
                              <div className="pt-1.5 mt-1 border-t border-slate-100 space-y-1.5">
                                <label className="flex items-start gap-1.5 text-[10px] text-slate-600 leading-snug">
                                  <input
                                    type="checkbox"
                                    checked={zkUpisElektronicki}
                                    onChange={(e) => setZkUpisElektronicki(e.target.checked)}
                                    className="mt-0.5 h-3 w-3"
                                  />
                                  <span>
                                    Zemljišnoknjižni upis predložen elektronički, plaćeno pri podnošenju
                                    <span className="block text-slate-400">
                                      Čl. 7 st. 3 — polovica i za podneske i za odluke u tom postupku.
                                    </span>
                                  </span>
                                </label>
                              </div>
                            )}

                            {/* Čl. 2 — datumi se traže samo kad je odabrana radnja na koju se odnose. */}
                            {(imaOdgovorNaTuzbu || imaUpravniSpor) && (
                              <div className="pt-1.5 mt-1 border-t border-slate-100 space-y-1.5">
                                <p className="text-[10px] text-slate-500 leading-snug">
                                  Uredba NN 50/2026 vrijedi od 1.6.2026. Po čl. 2 se pristojba za odgovor
                                  na tužbu podnesen prije tog dana i pristojba u upravnom sporu pokrenutom
                                  prije tog dana utvrđuju po staroj Uredbi.
                                </p>
                                {imaOdgovorNaTuzbu && (
                                  <label className="flex items-center justify-between gap-2 text-[10px] text-slate-500">
                                    <span>Odgovor na tužbu podnesen</span>
                                    <input
                                      type="date"
                                      value={datumOdgovoraNaTuzbu}
                                      onChange={(e) => setDatumOdgovoraNaTuzbu(e.target.value)}
                                      className="rounded border border-slate-200 px-1.5 py-0.5 text-[10px] text-slate-600"
                                    />
                                  </label>
                                )}
                                {imaUpravniSpor && (
                                  <label className="flex items-center justify-between gap-2 text-[10px] text-slate-500">
                                    <span>Upravni spor pokrenut</span>
                                    <input
                                      type="date"
                                      value={datumPokretanjaUpravnogSpora}
                                      onChange={(e) => setDatumPokretanjaUpravnogSpora(e.target.value)}
                                      className="rounded border border-slate-200 px-1.5 py-0.5 text-[10px] text-slate-600"
                                    />
                                  </label>
                                )}
                              </div>
                            )}
                            {/* Osnova oslobođenja — čl. 11 ima 21 točku, uz zahtjev po čl. 10.
                                Ne mijenja iznos, ali mora se vidjeti PO ČEMU je stranka oslobođena. */}
                            <select
                              value={oslobodjenjeKategorija}
                              onChange={(e) => setOslobodjenjeKategorija(e.target.value)}
                              className="w-full px-2.5 py-1.5 border border-slate-200 rounded-lg text-[11px] outline-none focus:ring-2 focus:ring-amber-500"
                            >
                              <option value="">— osnova oslobođenja (neobavezno) —</option>
                              {Object.entries(OSLOBODJENJE_KATEGORIJE).map(([kljuc, naziv]) => (
                                <option key={kljuc} value={kljuc}>
                                  {naziv}
                                </option>
                              ))}
                            </select>
                            {(oslobodjenjeKategorija === "rh_tijela" ||
                              oslobodjenjeKategorija === "javne_ovlasti") && (
                              <p className="text-[10px] text-amber-700 leading-snug">
                                {OSLOBODJENJE_NAPOMENA_LOKALNA}
                              </p>
                            )}

                            {/* Čl. 12 — dio suparničara oslobođen: umanjuje se OSNOVICA, ne iznos */}
                            {brojZastupanihOsoba > 1 && (
                              <div className="rounded-lg bg-slate-50 border border-slate-200 p-2 space-y-1.5">
                                <div className="flex items-center gap-2">
                                  <label className="text-[10px] text-slate-600 flex-1 leading-snug">
                                    Oslobođeno osoba od {brojZastupanihOsoba} (čl. 12)
                                  </label>
                                  <input
                                    type="number"
                                    min={0}
                                    max={brojZastupanihOsoba}
                                    value={brojOslobodjenihOsoba}
                                    onChange={(e) => setBrojOslobodjenihOsoba(Number(e.target.value) || 0)}
                                    className="w-16 px-2 py-1 border border-slate-200 rounded text-[11px] outline-none focus:ring-2 focus:ring-amber-500"
                                  />
                                </div>
                                {brojOslobodjenihOsoba > 0 && (
                                  <>
                                    <input
                                      type="text"
                                      inputMode="decimal"
                                      value={udioUVps}
                                      onChange={(e) => setUdioUVps(e.target.value)}
                                      placeholder="dio VPS-a neoslobođenih u € (neobavezno)"
                                      className="w-full px-2 py-1 border border-slate-200 rounded text-[11px] outline-none focus:ring-2 focus:ring-amber-500"
                                    />
                                    <p className="text-[10px] text-slate-500 leading-snug">
                                      Bez upisanog dijela vrijednost se dijeli brojem osoba (čl. 12
                                      st. 2). Pristojba se obračunava na {fmtEur(liveCalc.pristojbaVpsUdio)}.
                                    </p>
                                  </>
                                )}
                              </div>
                            )}

                            {/* Čl. 13 — oslobođena stranka uspije: plaća protivna, razmjerno uspjehu */}
                            {(isOslobodjenPristojbi || brojOslobodjenihOsoba > 0) && (
                              <div className="flex items-center gap-2">
                                <label className="text-[10px] text-slate-600 flex-1 leading-snug">
                                  Uspjeh oslobođene stranke u % (čl. 13)
                                </label>
                                <input
                                  type="text"
                                  inputMode="decimal"
                                  value={postotakUspjeha}
                                  onChange={(e) => setPostotakUspjeha(e.target.value)}
                                  placeholder="npr. 60"
                                  className="w-20 px-2 py-1 border border-slate-200 rounded text-[11px] outline-none focus:ring-2 focus:ring-amber-500"
                                />
                              </div>
                            )}

                            {vpsRezultat?.prilagodjen && (
                              <p className="text-[10px] text-amber-600 leading-snug">
                                Obračunato na zakonski VPS od {fmtEur(vpsRezultat.vps)} —{" "}
                                {vpsRezultat.pravniIzvor}.
                              </p>
                            )}
                          </>
                        )}

                        {/*
                          Ručno dodavanje. Ovjere, etažiranje, upis broda i polog nemaju
                          odvjetničku tarifnu stavku iz koje bi se izveli, a dioba/ostavina
                          i stečaj/predstečaj dijele jednu stavku kataloga pa se izvedeni
                          redak mora moći zamijeniti.
                        */}
                        <select
                          value=""
                          onChange={(e) => dodajPristojbu(e.target.value)}
                          aria-label="Dodaj sudsku pristojbu iz Uredbe"
                          className="w-full rounded border border-slate-200 px-1.5 py-1 text-[10px] text-slate-600 mt-1"
                        >
                          <option value="">+ Dodaj pristojbu iz Uredbe…</option>
                          {PRISTOJBE_NN50_2026.filter(
                            (o) => !pristojbe.stavke.some((s) => s.odredba_id === o.id),
                          ).map((o) => (
                            <option key={o.id} value={o.id}>
                              {o.poglavlje} · {o.tarifni_broj} — {o.opis.slice(0, 70)}
                              {o.opis.length > 70 ? "…" : ""}
                            </option>
                          ))}
                        </select>
                        {/* Ručno dodane pristojbe — ispod automatske, bez PDV-a.
                            Datum ima fiksnu prirodnu širinu (native date
                            picker) koja uz opis+iznos u jednom retku ne staje
                            na 360px — zato se na mobitelu slaže u tri retka
                            (datum / opis / iznos+briši), a od sm (640px)
                            naviše ostaje izvorni jednoredni raspored. */}
                        {dodatnePristojbe.map((t) => (
                          <div key={t.uid} className="flex flex-col gap-1.5 sm:flex-row sm:items-center">
                            <input
                              type="date"
                              value={t.datum ?? ""}
                              onChange={(e) => azurirajTrosak(setDodatnePristojbe, t.uid, "datum", e.target.value)}
                              className="w-full sm:w-auto text-[10px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-amber-500"
                            />
                            <input
                              type="text"
                              placeholder="Opis pristojbe"
                              value={t.opis}
                              onChange={(e) => azurirajTrosak(setDodatnePristojbe, t.uid, "opis", e.target.value)}
                              className="w-full sm:flex-1 sm:min-w-0 text-[11px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-amber-500"
                            />
                            <div className="flex items-center gap-1.5">
                              <input
                                type="text"
                                inputMode="decimal"
                                placeholder="0,00"
                                value={t.iznos}
                                onChange={(e) => azurirajTrosak(setDodatnePristojbe, t.uid, "iznos", e.target.value)}
                                className="flex-1 sm:flex-none sm:w-16 text-[11px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-amber-500 text-right"
                              />
                              <button
                                type="button"
                                onClick={() => obrisiTrosak(setDodatnePristojbe, t.uid)}
                                className="shrink-0 flex items-center justify-center w-8 h-8 sm:w-5 sm:h-5 text-red-400 hover:text-red-600"
                              >
                                <Trash2 className="h-3.5 w-3.5 sm:h-3 sm:w-3" />
                              </button>
                            </div>
                          </div>
                        ))}
                        <button
                          type="button"
                          onClick={() => dodajTrosak(setDodatnePristojbe)}
                          className="text-[10px] font-medium text-amber-700 hover:text-amber-900 flex items-center gap-1"
                        >
                          <Plus className="h-3 w-3" /> Dodaj pristojbu
                        </button>
                        {dodatnePristojbe.length > 0 && (
                          <div className="flex justify-between text-xs font-semibold text-slate-700 pt-0.5">
                            <span>Ukupno pristojbe</span>
                            <span>{fmtEur(liveCalc.ukupnoPristojbe)}</span>
                          </div>
                        )}
                      </div>

                      {/* ── Predujmovi (vještačenje i sl.) — bez PDV-a ── */}
                      <div className="pt-2 mt-1 border-t border-dashed border-slate-200 space-y-1.5">
                        <span className="text-xs font-semibold text-slate-600">Predujmovi</span>
                        {predujmovi.map((t) => (
                          <div key={t.uid} className="flex flex-col gap-1.5 sm:flex-row sm:items-center">
                            <input
                              type="date"
                              value={t.datum ?? ""}
                              onChange={(e) => azurirajTrosak(setPredujmovi, t.uid, "datum", e.target.value)}
                              className="w-full sm:w-auto text-[10px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-emerald-500"
                            />
                            <input
                              type="text"
                              placeholder="npr. Vještačenje"
                              value={t.opis}
                              onChange={(e) => azurirajTrosak(setPredujmovi, t.uid, "opis", e.target.value)}
                              className="w-full sm:flex-1 sm:min-w-0 text-[11px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-emerald-500"
                            />
                            <div className="flex items-center gap-1.5">
                              <input
                                type="text"
                                inputMode="decimal"
                                placeholder="0,00"
                                value={t.iznos}
                                onChange={(e) => azurirajTrosak(setPredujmovi, t.uid, "iznos", e.target.value)}
                                className="flex-1 sm:flex-none sm:w-16 text-[11px] px-1.5 py-1.5 sm:py-0.5 border border-slate-200 rounded outline-none focus:ring-1 focus:ring-emerald-500 text-right"
                              />
                              <button
                                type="button"
                                onClick={() => obrisiTrosak(setPredujmovi, t.uid)}
                                className="shrink-0 flex items-center justify-center w-8 h-8 sm:w-5 sm:h-5 text-red-400 hover:text-red-600"
                              >
                                <Trash2 className="h-3.5 w-3.5 sm:h-3 sm:w-3" />
                              </button>
                            </div>
                          </div>
                        ))}
                        <button
                          type="button"
                          onClick={() => dodajTrosak(setPredujmovi)}
                          className="text-[10px] font-medium text-emerald-700 hover:text-emerald-900 flex items-center gap-1"
                        >
                          <Plus className="h-3 w-3" /> Dodaj predujam
                        </button>
                        {predujmovi.length > 0 && (
                          <div className="flex justify-between text-xs font-semibold text-slate-700 pt-0.5">
                            <span>Ukupno predujmovi</span>
                            <span>{fmtEur(liveCalc.ukupnoPredujmovi)}</span>
                          </div>
                        )}
                      </div>


                      {/* ── Grand Total ── */}
                      <div
                        data-vodic="vodic-ukupno"
                        className={`flex justify-between text-sm font-bold text-indigo-800 pt-2 mt-1 border-t border-slate-300 rounded transition`}
                      >
                        <span>SVEUKUPNI TROŠAK</span>
                        <span>{fmtEur(liveCalc.sveukupniTrosak)}</span>
                      </div>

                      {/* Prijava greške — tiho, uz iznos na koji se odnosi. Ne u
                          prikazu primjera: izmišljeni podaci nisu stvarna greška. */}
                      {!demo && (
                        <div className="text-right print:hidden">
                          <button
                            type="button"
                            onClick={() => setPrijavaGreskeOtvorena(true)}
                            className="text-[11px] text-slate-500 underline underline-offset-2 hover:text-slate-800"
                          >
                            Iznos mi ne izgleda točno
                          </button>
                        </div>
                      )}

                      {/* Čl. 13 — tereti protivnu stranku po pravomoćnosti, pa stoji ISPOD
                          ukupnog iznosa i u njega ne ulazi. */}
                      {liveCalc.pristojbaNaTeretProtustranke > 0 && (
                        <div className="mt-2 pt-2 border-t border-dashed border-slate-200">
                          <div className="flex justify-between text-xs font-semibold text-slate-700">
                            <span>Pristojba na teret protivne stranke</span>
                            <span>{fmtEur(liveCalc.pristojbaNaTeretProtustranke)}</span>
                          </div>
                          <p className="text-[10px] text-slate-400 leading-snug mt-0.5">
                            Čl. 13: oslobođena stranka uspjela u {postotakUspjeha} %. Obveza nastaje
                            po pravomoćnosti i ne ulazi u sveukupni trošak zastupane stranke.
                          </p>
                        </div>
                      )}

                      {/* Čl. 16 uz suparničarstvo — zahtjevi koje ovaj troškovnik ne pokriva */}
                      {(vpsRezultat?.ostaliZahtjevi.length ?? 0) > 0 && (
                        <div className="mt-2 pt-2 border-t border-dashed border-slate-200">
                          <p className="text-[10px] font-semibold text-slate-600">
                            Nije obuhvaćeno (čl. 16 — zasebna pristojba)
                          </p>
                          {vpsRezultat!.ostaliZahtjevi.map((z, i) => (
                            <div key={i} className="flex justify-between text-[10px] text-slate-500 mt-0.5">
                              <span>{z.opis?.trim() || "zahtjev"}</span>
                              <span>{fmtEur(z.iznos)}</span>
                            </div>
                          ))}
                        </div>
                      )}

                      {/* Tbr. 40. toč. 1, drugi odlomak — dioba na jednake dijelove */}
                      {brojZastupanihOsoba > 1 && (
                        <div className="mt-2 pt-2 border-t border-dashed border-slate-200">
                          <div className="flex justify-between text-xs font-semibold text-slate-700">
                            <span>Nagrada po osobi ({brojZastupanihOsoba} branjenika)</span>
                            <span>{fmtEur(liveCalc.nagradaPoOsobi)}</span>
                          </div>
                          <p className="text-[10px] text-slate-400 leading-snug mt-0.5">
                            {fmtNum(liveCalc.bodoviPoOsobi)} bod. · Tbr. 40. toč. 1: „Iznos povišene
                            nagrade dijeli se na jednake dijelove prema broju branjenika."
                            Dijeli se nagrada, bez PDV-a i sudske pristojbe.
                          </p>
                        </div>
                      )}

                      <div className="text-[10px] text-slate-400 text-right">
                        1 bod = {fmtEur(BOD_VALUE)}
                      </div>

                      {/* ── Napomena — slobodni tekst, izostavlja se u ispisu kad je prazna ── */}
                      <div className="pt-2 mt-1 border-t border-dashed border-slate-200">
                        <label className="block text-[10px] font-semibold text-slate-600 mb-1">Napomena</label>
                        <textarea
                          value={napomena}
                          onChange={(e) => setNapomena(e.target.value)}
                          rows={3}
                          placeholder="Napomena se izostavlja iz ispisa kad je prazna"
                          className="w-full text-[11px] px-2 py-1.5 border border-slate-200 rounded-lg outline-none focus:ring-1 focus:ring-indigo-500 resize-y"
                        />
                      </div>
                    </div>
                  )}
                </div>

                {/* Action buttons */}
                {redci.length > 0 && (
                  <div className="px-4 pb-4 space-y-2">
                    {upozorenjeSpor && (
                      <div className="rounded-lg border border-sky-200 bg-sky-50 p-3">
                        <p className="flex items-start gap-1.5 text-[11px] text-sky-800 leading-snug">
                          <Info className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                          <span>{upozorenjeSpor}</span>
                        </p>
                      </div>
                    )}
                    {sviNedostaci.length > 0 && (
                      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 space-y-1.5">
                        {sviNedostaci.map((poruka) => (
                          <p key={poruka} className="flex items-start gap-1.5 text-[11px] text-amber-800 leading-snug">
                            <AlertCircle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                            <span>{poruka}</span>
                          </p>
                        ))}
                      </div>
                    )}
                    <button
                      onClick={handleCalculate}
                      disabled={calculating || sviNedostaci.length > 0}
                      className="w-full flex items-center justify-center gap-2 bg-indigo-600 hover:bg-indigo-700 disabled:bg-indigo-400 text-white text-sm font-semibold py-3 sm:py-2.5 rounded-lg transition"
                    >
                      {calculating ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Calculator className="h-4 w-4" />
                      )}
                      {calculating ? "Izračunavam..." : "Generiraj troškovnik"}
                    </button>
                    {userId && (
                      <div data-vodic="vodic-spremi" className={`rounded-lg transition`}>
                        <button
                          onClick={handleSave}
                          disabled={saving || demo || sviNedostaci.length > 0}
                          title={demo ? DEMO_ZASTO_NEAKTIVNO : undefined}
                          className="w-full flex items-center justify-center gap-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-emerald-400 disabled:cursor-not-allowed text-white text-sm font-semibold py-3 sm:py-2.5 rounded-lg transition"
                        >
                          {saving ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Save className="h-4 w-4" />
                          )}
                          {saving ? "Spremam..." : "Spremi troškovnik"}
                        </button>
                      </div>
                    )}
                    {savedId && (
                      <div className="flex items-center gap-1.5 text-xs text-emerald-600 justify-center">
                        <CheckCircle className="h-3.5 w-3.5" />
                        <span>Troškovnik spremljen!</span>
                        {selectedClientId && (
                          <Link href={`/klijenti/${selectedClientId}`} className="underline hover:text-emerald-800">
                            Pogledaj stranku →
                          </Link>
                        )}
                      </div>
                    )}
                    <div className="flex gap-2">
                      <button
                        onClick={handleExportXlsx}
                        disabled={demo}
                        title={demo ? DEMO_ZASTO_NEAKTIVNO : undefined}
                        className="flex-1 flex items-center justify-center gap-1.5 text-xs text-slate-600 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 disabled:text-slate-400 disabled:bg-slate-50 disabled:cursor-not-allowed py-2 rounded-lg transition"
                      >
                        <Download className="h-3.5 w-3.5" />
                        Excel
                      </button>
                      <button
                        onClick={handlePrint}
                        disabled={demo}
                        title={demo ? DEMO_ZASTO_NEAKTIVNO : undefined}
                        className="flex-1 flex items-center justify-center gap-1.5 text-xs text-slate-600 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 disabled:text-slate-400 disabled:bg-slate-50 disabled:cursor-not-allowed py-2 rounded-lg transition"
                      >
                        <Printer className="h-3.5 w-3.5" />
                        Ispis
                      </button>
                    </div>
                    {/* Vidljivo objašnjenje, ne samo sivi gumb: neaktivan gumb
                        bez razloga izgleda kao kvar. */}
                    {demo && (
                      <p className="text-[11px] text-amber-700 text-center leading-snug">
                        U prikazu primjera spremanje, ispis i izvoz nisu dostupni —
                        podaci nisu vaši.
                      </p>
                    )}
                    {/* ZPP čl. 164 — statička obavijest, ne prati datume niti sprema stanje.
                        Stoji ovdje jer je ovo trenutak kad odvjetnik sprema/predaje
                        troškovnik, ne u samom obrascu gdje bi samo smetala unosu. */}
                    <p className="flex items-start gap-1.5 text-[11px] text-slate-400 leading-snug print:hidden">
                      <Info className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                      <span>
                        Rok (ZPP čl. 164.): zahtjev za naknadu troškova mora se postaviti najkasnije do
                        zaključenja rasprave koja prethodi odluci o troškovima — najava „troškovnik
                        naknadno" ne vrijedi. Kad rasprave nema (platni nalog, presuda zbog priznanja),
                        rok je u tom podnesku; kod povlačenja ili odricanja — 15 dana od dostave.
                      </span>
                    </p>
                  </div>
                )}
              </div>

              {/* ── Tbr. 7.1 Quick Reference ── */}
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-4 print:hidden">
                <div className="flex items-center gap-2 mb-3">
                  <Info className="h-4 w-4 text-indigo-500" />
                  <span className="text-xs font-semibold text-slate-700 uppercase tracking-wider">Tbr. 7.1 — Tablica bodova</span>
                </div>
                <div className="space-y-1 text-[11px]">
                  {TBR_7_1_RAZREDI.map((razred) => (
                    <div key={razred.od ?? "dno"} className="flex justify-between gap-3 text-slate-500">
                      <span className="shrink-0">
                        {razred.do === null
                          ? `iznad ${formatThreshold(razred.od ?? 0)}`
                          : `${formatThreshold(razred.od ?? 0)} – ${formatThreshold(razred.do)}`}
                      </span>
                      <span className="font-mono font-semibold text-slate-700 text-right">
                        {razred.bodovi}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              {/* ── Error display ── */}
              {calcError && (
                <div className="bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 flex items-start gap-2">
                  <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                  <span>{calcError}</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* ═══════════════════════════════════════════════════════
            RESULT — Full Troškovnik (from server)
        ═══════════════════════════════════════════════════════ */}
        {result && (
          <div className="mt-8 bg-white rounded-2xl border border-indigo-200 shadow-sm overflow-hidden" id="troskovnik-result">
            {/* Header */}
            <div className="bg-gradient-to-r from-indigo-600 to-indigo-700 px-6 py-4 print:from-indigo-800 print:to-indigo-800">
              <div className="flex items-center gap-3">
                <CheckCircle className="h-6 w-6 text-indigo-200" />
                <div>
                  <h2 className="text-white font-bold text-lg">Troškovnik</h2>
                  <p className="text-indigo-200 text-xs">
                    {new Date(result.datum_izrade).toLocaleDateString("hr-HR")}
                    {result.is_samo_kazneni
                      ? " · Kazneni postupak (nagrada po zapriječenoj kazni, Tbr. 4.1)"
                      : ` · Vrijednost spora: ${fmtEur(result.vrijednost_spora ?? vrijednostNum)}`}
                    {" · Tarifa HOK 2023"}
                  </p>
                </div>
              </div>
            </div>

            <div className="p-6">
              {/* Formal document header for print */}
              <div className="mb-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm text-slate-800">
                  <div>
                    <div className="font-semibold text-xs uppercase text-slate-600 mb-1">PODNOSITELJ</div>
                    {result.odvjetnik ? (
                      <div className="text-sm">
                        <div>{result.odvjetnik.ime}</div>
                        {result.odvjetnik.oib && <div>OIB: {result.odvjetnik.oib}</div>}
                        {result.odvjetnik.adresa && <div>{result.odvjetnik.adresa}{result.odvjetnik.grad ? `, ${result.odvjetnik.grad}` : ""}</div>}
                        {result.odvjetnik.iban && <div>IBAN: {result.odvjetnik.iban}</div>}
                      </div>
                    ) : (
                      <div className="text-sm text-slate-500">—</div>
                    )}
                  </div>

                  <div>
                    <div className="font-semibold text-xs uppercase text-slate-600 mb-1">SUD I POSLOVNI BROJ</div>
                    <div className="text-sm">
                      { result.sud || result.broj_predmeta ? (
                        <>
                          <div>{result.sud ?? ""}{result.broj_predmeta ? ` · ${result.broj_predmeta}` : ""}</div>
                        </>
                      ) : (
                        <div className="text-slate-500">—</div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4 text-sm text-slate-800">
                  <div>
                    <div className="font-semibold text-xs uppercase text-slate-600 mb-1">ZASTUPANA STRANKA</div>
                    {result.klijent ? (
                      <div className="text-sm">
                        <div>{result.klijent.ime}</div>
                        {result.klijent.oib && <div>OIB: {result.klijent.oib}</div>}
                        {result.klijent.adresa && <div>{result.klijent.adresa}{result.klijent.grad ? `, ${result.klijent.grad}` : ""}</div>}
                      </div>
                    ) : (
                      <div className="text-sm text-slate-500">—</div>
                    )}
                  </div>

                  <div>
                    <div className="font-semibold text-xs uppercase text-slate-600 mb-1">PROTUSTRANKA</div>
                    {result.protustranka ? (
                      <div className="text-sm">
                        {result.protustranka_polozaj && <div className="font-medium">{result.protustranka_polozaj}</div>}
                        <div>{result.protustranka.ime}</div>
                        {result.protustranka.oib && <div>OIB: {result.protustranka.oib}</div>}
                        {result.protustranka.adresa && <div>{result.protustranka.adresa}{result.protustranka.grad ? `, ${result.protustranka.grad}` : ""}</div>}
                      </div>
                    ) : (
                      <div className="text-sm text-slate-500">—</div>
                    )}
                  </div>
                </div>
              </div>

              {/* Stjecaj — obrazloženje odabranog razreda (Tbr. 4. toč. 1 al. 6) */}
              {result.napomena_stjecaj && (
                <p className="text-xs italic text-slate-500 mb-4">{result.napomena_stjecaj}</p>
              )}

              {/* I. Odvjetničke radnje */}
              <h3 className="text-sm font-bold text-slate-800 mb-2">I. Odvjetničke radnje</h3>
              {/* Line items table — na mobitelu (< 768px) `.troskovnik-tablica-mob`
                  (globals.css) pretvara retke u kartice čisto CSS-om (display),
                  bez zasebnog JSX-a: ista brojka, isti uvjeti, samo drugi raspored.
                  Bez vodoravnog pomicanja na mobitelu jer se sadržaj slaže okomito. */}
              <div className="overflow-x-auto troskovnik-tablica-mob">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b-2 border-indigo-200">
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-10">Rbr.</th>
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-20">Datum</th>
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500">Opis</th>
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-20">Tar. br.</th>
                      <th className="text-center py-2 px-2 text-xs font-semibold text-slate-500 w-16">Kol.</th>
                      <th className="text-right py-2 px-2 text-xs font-semibold text-slate-500 w-20">Bod./jed.</th>
                      <th className="text-right py-2 px-2 text-xs font-semibold text-slate-500 w-20">Uk. bod.</th>
                      <th className="text-right py-2 px-2 text-xs font-semibold text-slate-500 w-24">Iznos (€)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.stavke.map((line) => (
                      <tr key={line.rbr} className="border-b border-slate-100 hover:bg-slate-50">
                        <td className="py-2 px-2 text-slate-400">{line.rbr}.</td>
                        <td className="py-2 px-2 text-slate-500">
                          {line.datum ? new Date(line.datum).toLocaleDateString("hr-HR") : ""}
                        </td>
                        <td className="py-2 px-2 text-slate-800">
                          {line.opis}
                          {line.is_jednokratna && (
                            <span className="ml-1.5 text-[10px] font-semibold text-emerald-700">(jednokratna nagrada)</span>
                          )}
                          {line.tbr41_pravni_izvor && (
                            <span className="block text-[10px] font-normal text-slate-400 leading-snug">
                              {line.tbr41_pravni_izvor}
                            </span>
                          )}
                        </td>
                        <td className="py-2 px-2 text-slate-500 font-mono text-xs">
                          {line.tarifni_broj}
                          {line.pravni_izvor && (
                            <span className="block font-sans text-[9px] leading-snug text-slate-400 normal-case">
                              {line.pravni_izvor}
                            </span>
                          )}
                        </td>
                        <td className="py-2 px-2 text-center text-slate-600">{line.kolicina}</td>
                        <td className="py-2 px-2 text-right text-slate-600">{fmtNum(line.bodova_po_jedinici)}</td>
                        <td className="py-2 px-2 text-right font-semibold text-slate-700">
                          {fmtNum(line.ukupno_bodova + (line.tbr41_bodovi ?? 0))}
                          {(line.tbr41_bodovi ?? 0) !== 0 && (
                            <span className={`block text-[10px] font-normal ${(line.tbr41_bodovi ?? 0) > 0 ? "text-indigo-600" : "text-amber-600"}`}>
                              {(line.tbr41_bodovi ?? 0) > 0 ? "+" : "−"}
                              {fmtNum(Math.abs(line.tbr41_bodovi ?? 0))}
                            </span>
                          )}
                        </td>
                        <td className="py-2 px-2 text-right font-semibold text-slate-800">{fmtEur(line.iznos_eur)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    {(result.povisenje_vise_osoba_postotak ?? 0) > 0 && (
                      <tr className="border-t border-slate-200">
                        <td colSpan={6} className="py-2 px-2 text-right text-xs text-slate-500">
                          Zastupanje {result.broj_zastupanih_osoba} osoba (Tbr. 40) — povišenje{" "}
                          {Math.round((result.povisenje_vise_osoba_postotak ?? 0) * 100)} %
                        </td>
                        <td className="py-2 px-2 text-right text-slate-600">
                          {fmtNum(result.povisenje_vise_osoba_bodovi ?? 0)}
                        </td>
                        <td className="py-2 px-2 text-right text-slate-600">
                          {fmtEur((result.povisenje_vise_osoba_bodovi ?? 0) * BOD_VALUE)}
                        </td>
                      </tr>
                    )}
                    {(result.tbr41_bodovi ?? 0) !== 0 && (
                      <tr className="border-t border-slate-200">
                        <td colSpan={6} className="py-2 px-2 text-right text-xs text-slate-500">
                          Tbr. 41 — povišenje {result.tbr41_povisenje_postotak ?? 0} %, sniženje{" "}
                          {result.tbr41_snizenje_postotak ?? 0} %
                        </td>
                        <td className="py-2 px-2 text-right text-slate-600">
                          {fmtNum(result.tbr41_bodovi ?? 0)}
                        </td>
                        <td className="py-2 px-2 text-right text-slate-600">
                          {fmtEur((result.tbr41_bodovi ?? 0) * BOD_VALUE)}
                        </td>
                      </tr>
                    )}
                    {/* ── Zbroj nagrade ── */}
                    <tr className="border-t-2 border-indigo-200">
                      <td colSpan={6} className="py-2 px-2 text-right text-xs text-slate-500 font-semibold">Zbroj nagrade — ukupno bodova</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtNum(result.ukupno_bodova)}</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtEur(result.ukupno_eur_bez_pdv)}</td>
                    </tr>
                    {(() => {
                      const isPdv = (result as any).is_pdv_ukljucen !== undefined ? (result as any).is_pdv_ukljucen : ((result as any).has_vat !== undefined ? (result as any).has_vat : false);
                      const base = Number(result.ukupno_eur_bez_pdv ?? 0);
                      const pdvCalc = isPdv ? Math.round(base * 0.25 * 100) / 100 : 0;
                      const sveukupni = Number(result.sveukupni_trosak ?? base);
                      const dodatnePristojbeRes = result.dodatne_pristojbe ?? [];
                      const predujmoviRes = result.predujmovi ?? [];
                      const ukupnoPristojbeRes = result.ukupno_pristojbe ?? (result.sudska_pristojba ?? 0);
                      const ukupnoPredujmoviRes = result.ukupno_predujmovi ?? 0;
                      const naknadaTroskova = Number(result.naknada_troskova_ukupno ?? 0);
                      const osnovicaLabel = naknadaTroskova > 0 ? " — nagrada + naknada troškova" : "";

                      return (
                        <>
                          {/* Naknada troškova (Tbr. 48.-51.) — vlastiti trošak odvjetnika,
                              ULAZI u osnovicu, pa stoji prije PDV-a. */}
                          {naknadaTroskova > 0 && (
                            <tr>
                              <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">Naknada troškova (Tbr. 48.-51.)</td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(naknadaTroskova)}</td>
                            </tr>
                          )}

                          {/* ── PDV — samo kad je odvjetnik u sustavu PDV-a ── */}
                          {isPdv && (
                            <>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">Osnovica (bez PDV){osnovicaLabel}</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(base)}</td>
                              </tr>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">PDV (25%)</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(pdvCalc)}</td>
                              </tr>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs font-semibold text-indigo-700">Nagrada s PDV-om</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm font-semibold text-indigo-700">{fmtEur(base + pdvCalc)}</td>
                              </tr>
                            </>
                          )}
                          {!isPdv && (
                            <tr>
                              <td colSpan={5} className="py-1 px-2 text-right text-xs font-semibold text-indigo-700">Odvjetnički trošak{osnovicaLabel}</td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm font-semibold text-indigo-700">{fmtEur(base)}</td>
                            </tr>
                          )}
                          {/* ── II. Sudske pristojbe — automatske + ručno dodane, bez PDV-a ── */}
                          <tr className="border-t border-slate-200">
                            <td colSpan={8} className="pt-3 pb-1 px-2 text-xs font-bold text-slate-700">II. Sudske pristojbe</td>
                          </tr>

                          {/* Uredba naplaćuje po radnji — svaki tarifni broj svoj redak. */}
                          {(result.pristojbene_stavke ?? []).length === 0 ? (
                            <tr>
                              <td colSpan={5} className="py-1 px-2 text-right text-xs text-slate-500">
                                Sudska pristojba
                                <span className="block text-[10px] text-slate-400">
                                  {objasnjenjeBezPristojbe()}
                                </span>
                              </td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(0)}</td>
                            </tr>
                          ) : (
                            <>
                              {(result.pristojbene_stavke ?? []).map((s) => (
                                <tr key={s.odredba_id}>
                                  <td colSpan={5} className="py-1 px-2 text-right text-xs text-slate-500">
                                    Sudska pristojba — {s.tarifni_broj}
                                    {s.kolicina > 1 && <span className="text-slate-400"> × {s.kolicina}</span>}
                                    {(result.is_oslobodjen_pristojbi || (result.broj_oslobodjenih_osoba ?? 0) > 0) && (
                                      <span className="ml-1 text-amber-600">
                                        (oslobođeno{result.oslobodjenje_naziv ? ` — ${result.oslobodjenje_naziv}` : ""})
                                      </span>
                                    )}
                                    <span className="block text-[10px] text-slate-400">
                                      {s.pravni_izvor}
                                    </span>
                                  </td>
                                  <td />
                                  <td className={`py-1 px-2 text-right text-sm ${result.is_oslobodjen_pristojbi ? 'line-through text-slate-400' : 'text-slate-600'}`}>
                                    {fmtEur(s.iznos)}
                                  </td>
                                </tr>
                              ))}
                              {/* Zbroj ne ide ovdje: odjeljak ima jedan redak „Ukupno
                                  pristojbe" niže, koji pokriva i ručno dodane. */}
                            </>
                          )}

                          {/* Na što je pristojba obračunata: zakonski VPS (čl. 14–26) i dio
                              koji otpada na neoslobođene (čl. 12). Bez toga iznos ne odgovara
                              upisanoj vrijednosti spora, a nigdje ne piše zašto. */}
                          {(result.vps_prilagodjen || (result.broj_oslobodjenih_osoba ?? 0) > 0) && (
                            <tr>
                              <td colSpan={7} className="py-1 px-2 text-right text-[10px] text-amber-600">
                                {result.vps_prilagodjen && result.vps_napomena && (
                                  <span className="block">{result.vps_napomena}</span>
                                )}
                                {(result.broj_oslobodjenih_osoba ?? 0) > 0 &&
                                  (result.broj_oslobodjenih_osoba ?? 0) < (result.broj_zastupanih_osoba ?? 1) && (
                                    <span className="block">
                                      Čl. 12 — obračunato na {fmtEur(result.pristojba_vps_udio ?? 0)}, jer je{" "}
                                      {result.broj_oslobodjenih_osoba} od {result.broj_zastupanih_osoba} osoba oslobođeno.
                                    </span>
                                  )}
                              </td>
                            </tr>
                          )}
                          {dodatnePristojbeRes.map((t) => (
                            <tr key={`pristojba-${t.rbr}`}>
                              <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">
                                {t.datum ? new Date(t.datum).toLocaleDateString("hr-HR") + " · " : ""}{t.opis}
                              </td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(t.iznos)}</td>
                            </tr>
                          ))}
                          <tr>
                            <td colSpan={6} className="py-1 px-2 text-right text-xs font-semibold text-slate-700">
                              Ukupno pristojbe
                              <span className="block text-[10px] text-slate-400 font-normal">Na sudske pristojbe ne obračunava se PDV.</span>
                            </td>
                            <td />
                            <td className="py-1 px-2 text-right text-sm font-semibold text-slate-700">{fmtEur(ukupnoPristojbeRes)}</td>
                          </tr>

                          {/* ── III. Predujmovi — bez PDV-a ── */}
                          <tr className="border-t border-slate-200">
                            <td colSpan={8} className="pt-3 pb-1 px-2 text-xs font-bold text-slate-700">III. Predujmovi</td>
                          </tr>
                          {predujmoviRes.map((t) => (
                            <tr key={`predujam-${t.rbr}`}>
                              <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">
                                {t.datum ? new Date(t.datum).toLocaleDateString("hr-HR") + " · " : ""}{t.opis}
                              </td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(t.iznos)}</td>
                            </tr>
                          ))}
                          <tr>
                            <td colSpan={6} className="py-1 px-2 text-right text-xs font-semibold text-slate-700">
                              Ukupno predujmovi
                              <span className="block text-[10px] text-slate-400 font-normal">Na predujmove ne obračunava se PDV.</span>
                            </td>
                            <td />
                            <td className="py-1 px-2 text-right text-sm font-semibold text-slate-700">{fmtEur(ukupnoPredujmoviRes)}</td>
                          </tr>

                          {/* ── SVEUKUPNI TROŠAK ── */}
                          <tr className="bg-indigo-50 border-t-2 border-indigo-200">
                            <td colSpan={6} className="py-3 px-2 text-right text-sm font-bold text-indigo-800">SVEUKUPNI TROŠAK</td>
                            <td />
                            <td className="py-3 px-2 text-right text-lg font-bold text-indigo-800">{fmtEur(sveukupni)}</td>
                          </tr>
                          {/* Čl. 13 — tereti protivnu stranku po pravomoćnosti; ispod ukupnog
                              iznosa jer u njega ne ulazi. */}
                          {(result.pristojba_na_teret_protustranke ?? 0) > 0 && (
                            <tr>
                              <td colSpan={5} className="py-2 px-2 text-right text-xs text-slate-500">
                                Pristojba na teret protivne stranke
                                <span className="block text-[10px] text-slate-400">
                                  Čl. 13 — oslobođena stranka uspjela u {result.postotak_uspjeha} %; obveza
                                  nastaje po pravomoćnosti i ne ulazi u sveukupni trošak
                                </span>
                              </td>
                              <td />
                              <td className="py-2 px-2 text-right text-sm font-semibold text-slate-700">
                                {fmtEur(result.pristojba_na_teret_protustranke ?? 0)}
                              </td>
                            </tr>
                          )}
                          {/* Čl. 16 uz suparničarstvo — zahtjevi za koje ide zasebna pristojba */}
                          {(result.zahtjevi_informativno ?? []).map((z, i) => (
                            <tr key={`cl16-${i}`}>
                              <td colSpan={5} className="py-1 px-2 text-right text-xs text-slate-500">
                                Nije obuhvaćeno: {z.opis?.trim() || "zahtjev"}
                                <span className="block text-[10px] text-slate-400">
                                  Čl. 16 — kod suparničarstva se uzima vrijednost svakog pojedinog
                                  zahtjeva, pa se za ovaj plaća zasebna pristojba
                                </span>
                              </td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(z.iznos)}</td>
                            </tr>
                          ))}
                          {(result.broj_zastupanih_osoba ?? 1) > 1 && (
                            <tr>
                              <td colSpan={6} className="py-2 px-2 text-right text-xs text-slate-500">
                                Nagrada po osobi ({result.broj_zastupanih_osoba} branjenika)
                                <span className="block text-[10px] text-slate-400">
                                  Tbr. 40. toč. 1 — dioba na jednake dijelove; bez PDV-a i pristojbe
                                </span>
                              </td>
                              <td className="py-2 px-2 text-right text-slate-600">
                                {fmtNum(result.bodovi_po_osobi ?? 0)}
                              </td>
                              <td className="py-2 px-2 text-right font-semibold text-slate-700">
                                {fmtEur(result.nagrada_po_osobi ?? 0)}
                              </td>
                            </tr>
                          )}
                        </>
                      );
                    })()}
                  </tfoot>
                </table>
              </div>

              {/* ── Napomena — slobodni tekst, izostaje kad je prazna ── */}
              {result.napomena && (
                <div className="mt-4 pt-3 border-t border-slate-200">
                  <h3 className="text-sm font-bold text-slate-800 mb-1">Napomena</h3>
                  <p className="text-sm text-slate-600 whitespace-pre-line">{result.napomena}</p>
                </div>
              )}

              {/* Footer */}
              <div className="mt-4 pt-3 border-t border-slate-200 text-[10px] text-slate-400 leading-relaxed">
                <p>Izračun je napravljen prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (Hrvatska odvjetnička komora, 14. listopada 2023.). Vrijednost boda: {fmtEur(BOD_VALUE)} (Tbr. 54). Sudske pristojbe prema Uredbi o Tarifi sudskih pristojbi (NN 50/2026), na snazi od 1.6.2026.; za odgovor na tužbu podnesen i upravni spor pokrenut prije tog dana primjenjuje se NN 37/2023 (čl. 2).</p>
                <p className="mt-1">Ovaj troškovnik služi kao orijentacijski izračun i ne predstavlja pravno obvezujući dokument.</p>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── Dokument za sud ──
          Susjed <main>-a, koji je pod ispisom skriven u cijelosti. Vidljiv je
          samo pod @media print — CSS koji to osigurava sad je DIO same
          komponente (TroskovnikIspis.tsx), ne ove stranice, pa ga ovdje više
          nema: prije je bio ovdje i /troskovnik/[id] koji poziva istu
          komponentu ga nije naslijedio, pa se ispis ondje vidio i na zaslonu,
          kao sirov tekst uz lijevi rub. */}
      {result && <TroskovnikIspis model={izvozModel} />}

      <PrijavaGreskeDialog
        open={prijavaGreskeOtvorena}
        onClose={() => setPrijavaGreskeOtvorena(false)}
        troskovnikId={savedId ?? undefined}
        ulazniPodaci={ulazZaPrijavuGreske}
        rezultat={() => result ?? liveCalc}
      />
    </div>
  );
}
