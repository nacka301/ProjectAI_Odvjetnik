"use client";

import { useCallback, useEffect, useMemo, useRef, useState, Suspense } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import {
  ArrowLeft,
  Calculator,
  CalendarDays,
  ChevronDown,
  ChevronRight,
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
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import { useCredits, getModuleQuota } from "@/lib/useCredits";
import { CreditQuotaBadge, CreditExhaustedWarning } from "@/components/CreditQuotaBadge";
import { formatEur, formatNumber, formatThreshold, formatirajDatum } from "@/lib/format";
import {
  BOD_VALUE,
  KATEGORIJA_KAZNENI,
  PDV_RATE,
  TBR_7_1_RAZREDI,
  ZAPRIJECENA_KAZNA_TIERS,
  formatirajBodove,
  izracunajBodove,
  jeSamoKazneni,
  izracunajKazneneBodove,
  izracunajSudskuPristojbu,
  objasnjenjePristojbe,
  OSNOVICA_TBR7,
  OSNOVICA_LABELE,
  RAZINA_SUDA,
  RAZINA_SUDA_LABELE,
  RENTA_RAZDOBLJE,
  TBR41_MAX_POVISENJE,
  TBR41_MAX_SNIZENJE,
  TBR45_NACIN,
  TBR45_NACIN_LABELE,
  efektivniVps,
  grupirajJednokratne,
  izracunajOsnovicu,
  nedostatakDatuma,
  poredajPoDatumu,
  odrediPristojbuOsnovu,
  podijeliPoOsobama,
  povisenjeViseOsobaPostotak,
  provjeriZahtjev,
  provjeriTbr41Stavku,
  razrijesiTbr41,
  tbr41Izvor,
  tbr41Udio,
  tbr71Bodovi,
  upozorenjeMaliSpor,
  ukupnoBodovaZaStavku,
  zaokruziNovac,
  type OsnovicaTbr7,
  type PristojbaOsnova,
  type RazinaSuda,
  type RentaRazdoblje,
  type TarifnaStavka,
  type Tbr41Stavka,
  type Tbr45Nacin,
} from "@/lib/tarifa";

// ── Types (mirror backend models) ──

/**
 * Jedna radnja u troškovniku — vlastiti redak s vlastitim datumom.
 *
 * Prije uvođenja datuma odabir je bio mapa item_id → količina, pa je tarifna stavka
 * mogla postojati najviše jednom: tri ročišta bila su jedan redak s količinom tri.
 * Datum se veže uz pojedinu radnju, pa su to sada tri retka.
 *
 * `redakId` je stabilan ključ (brojač, ne item_id) jer ista se stavka ponavlja.
 * `kolicina` ostaje — za jedinice „sat" i „stranica" i dalje je množitelj.
 */
interface OdabranaRadnja {
  redakId: string;
  itemId: string;
  kolicina: number;
  /** Datum radnje u obliku gggg-mm-dd; prazan niz dok nije unesen. */
  datum: string;
}

interface TroskovnikLineItem {
  rbr: number;
  item_id: string;
  opis: string;
  tarifni_broj: string;
  /** Propis i tarifni broj iz kojeg redak proizlazi. */
  pravni_izvor?: string;
  kolicina: number;
  /** Datum kad je radnja poduzeta, gggg-mm-dd. */
  datum?: string | null;
  bodova_po_jedinici: number;
  ukupno_bodova: number;
  iznos_eur: number;
  /** Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1 — ukupno ≠ bodova_po_jedinici × kolicina. */
  is_jednokratna?: boolean;
  /** Radnja obuhvaćena jednokratnom nagradom ranijeg retka — bez vlastitih bodova. */
  obuhvaceno_jednokratnom?: boolean;
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
  vrijednost_boda: number;
  klijent: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  odvjetnik: { ime: string; oib?: string; adresa?: string; grad?: string; iban?: string } | null;
  protustranka?: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  protustranka_polozaj?: string | null;
  is_pdv_ukljucen?: boolean;
  has_vat?: boolean;
  opis_predmeta: string | null;
  datum_izrade: string;
  /** Razdoblje koje troškovnik pokriva — najraniji i najkasniji datum radnje. */
  datum_od?: string | null;
  datum_do?: string | null;
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
  is_zastupnik_djeteta?: boolean;
  is_stjecaj?: boolean;
  /** Obrazloženje odabranog razreda kod stjecaja (Tbr. 4. toč. 1 al. 6). */
  napomena_stjecaj?: string | null;
  is_oduzimanje_prekrsaj?: boolean;
  vrijednost_oduzimanja?: number;
  // Sudske pristojbe
  sudska_pristojba?: number;
  pristojba_osnova?: PristojbaOsnova;
  is_oslobodjen_pristojbi?: boolean;
  sveukupni_trosak?: number;
}

// ── Format helpers ──

function fmtEur(n: number) {
  return n.toLocaleString("hr-HR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";
}

/** Prikaz bodova — decimalni, jer se bodovi po Tarifi ne zaokružuju. */
function fmtNum(n: number) {
  return formatirajBodove(n);
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
  const { rows: creditRows, loading: creditsLoading } = useCredits(userId);
  const troskovnikQuota = getModuleQuota(creditRows, "troskovnik");
  const searchParams = useSearchParams();
  const preselectedClientId = searchParams.get("clientId");

  // ── Catalog state ──
  const [katalog, setKatalog] = useState<TarifnaStavka[]>([]);
  const [loadingKatalog, setLoadingKatalog] = useState(true);
  const [katalogError, setKatalogError] = useState<string | null>(null);

  // ── Form state ──
  const [vrijednostSpora, setVrijednostSpora] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [odabraneRadnje, setOdabraneRadnje] = useState<OdabranaRadnja[]>([]);
  // Brojač, ne nasumičan ključ: projekt nema uuid biblioteku, a brojač je predvidiv.
  const sljedeciRedakId = useRef(0);
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
  const [opisPredmeta, setOpisPredmeta] = useState("");
  const [predmetSud, setPredmetSud] = useState("");
  const [predmetPoslovniBroj, setPredmetPoslovniBroj] = useState("");
  const [showParties, setShowParties] = useState(false);
  // New field for PDV toggle and party data
  // Explicit flag: whether the entity is in the VAT system (preferred by backend)
  const [isPdvUkljucen, setIsPdvUkljucen] = useState(false);
  const [odvjetnikIban, setOdvjetnikIban] = useState("");
  const [protustrankaIme, setProtustrankaIme] = useState("");
  const [protustrankaOib, setProtustrankaOib] = useState("");
  const [protustrankaAdresa, setProtustrankaAdresa] = useState("");
  const [protustrankaGrad, setProtustrankaGrad] = useState("");
  const [protustrankaPolozaj, setProtustrankaPolozaj] = useState("");
  const [zastupanaPolozaj, setZastupanaPolozaj] = useState("");

  // ── Kazneni postupak state ──
  const [zaprijecenaKazna, setZaprijecenaKazna] = useState("");
  const [isPoSluzbenojDuznosti, setIsPoSluzbenojDuznosti] = useState(false);
  const [isNaTeretProracuna, setIsNaTeretProracuna] = useState(false);
  const [isZastupnikDjeteta, setIsZastupnikDjeteta] = useState(false);
  const [isStjecaj, setIsStjecaj] = useState(false);
  const [isOduzimanjePrekrsaj, setIsOduzimanjePrekrsaj] = useState(false);
  const [vrijednostOduzimanja, setVrijednostOduzimanja] = useState("");

  // ── Sudske pristojbe state ──
  const [isOslobodjenPristojbi, setIsOslobodjenPristojbi] = useState(false);

  // ── Tbr. 40 — broj osoba koje odvjetnik zastupa u istom postupku ──
  const [brojZastupanihOsoba, setBrojZastupanihOsoba] = useState(1);

  // ── Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45 ──
  const [osnovicaTbr7, setOsnovicaTbr7] = useState<OsnovicaTbr7>(OSNOVICA_TBR7.Toc1);
  const [razinaSuda, setRazinaSuda] = useState<RazinaSuda | null>(null);
  const [tbr45Nacin, setTbr45Nacin] = useState<Tbr45Nacin | null>(null);
  const [rentaIznos, setRentaIznos] = useState("");
  const [rentaRazdoblje, setRentaRazdoblje] = useState<RentaRazdoblje | null>(null);

  // ── Tbr. 41 — povisivanje i snižavanje POJEDINIH tarifnih stavaka ──
  // Prečac "primijeni na sve": vrijedi za svaku stavku koja nema vlastiti postotak.
  const [tbr41Povisenje, setTbr41Povisenje] = useState(0);
  const [tbr41Snizenje, setTbr41Snizenje] = useState(0);
  /** itemId → vlastiti postoci stavke; odsutnost znači "naslijedi prečac". */
  const [tbr41PoStavci, setTbr41PoStavci] = useState<Map<string, Tbr41Stavka>>(new Map());

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

  // ── Result state ──
  const [result, setResult] = useState<TroskovnikResponse | null>(null);
  const [calculating, setCalculating] = useState(false);
  const [calcError, setCalcError] = useState<string | null>(null);

  const printRef = useRef<HTMLDivElement>(null);

  // ── Load catalog ──
  useEffect(() => {
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
  }, []);

  // ── Load user's clients for picker ──
  useEffect(() => {
    if (!userId) return;
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
  }, [userId, preselectedClientId]);

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
      }
    },
    [clientAutoFilled, selectedClientId]
  );

  // ── Derived values ──

  // Odabrane definicije iz kataloga — osnova za određivanje vrste postupka.
  // Jedna definicija po tarifnoj stavci, koliko god redaka ona imala: provjere i
  // upozorenja govore o stavci, pa se ne smiju ponoviti onoliko puta koliko je radnji.
  const odabraneDefinicije = useMemo(() => {
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));
    return Array.from(new Set(odabraneRadnje.map((radnja) => radnja.itemId)))
      .map((id) => catalogMap.get(id))
      .filter((d): d is TarifnaStavka => d !== undefined);
  }, [odabraneRadnje, katalog]);

  // Bilo koja kaznena stavka → prikaži panel s parametrima kaznenog postupka.
  const isKazneniMode = odabraneDefinicije.some((d) => d.kategorija === KATEGORIJA_KAZNENI);

  // Isključivo kaznene stavke → VPS se ne koristi ni za bodove ni za pristojbu.
  const isOnlyKazneni = jeSamoKazneni(odabraneDefinicije);

  const unesenVps = parseFloat(vrijednostSpora.replace(/\./g, "").replace(",", ".")) || 0;
  // U kaznenom postupku sve ide po zapriječenoj kazni (Tbr. 4. toč. 1) — VPS otpada.
  const vrijednostNum = isOnlyKazneni ? 0 : unesenVps;
  const rentaIznosNum = parseFloat(rentaIznos.replace(/\./g, "").replace(",", ".")) || 0;

  // Osnovica na koju upućuju Tbr. 8. toč. 1, 3, 4 i Tbr. 9. toč. 1–6:
  // "Tbr. 7. toč. 1., 4., 7. i 8." — četiri varijante iste osnovice.
  const osnovicaUlaz = useMemo(
    () => ({
      osnovicaTbr7,
      vrijednostSpora: vrijednostNum,
      razinaSuda,
      tbr45Nacin,
      rentaIznos: rentaIznosNum,
      rentaRazdoblje,
    }),
    [osnovicaTbr7, vrijednostNum, razinaSuda, tbr45Nacin, rentaIznosNum, rentaRazdoblje],
  );

  const osnovica = useMemo(() => izracunajOsnovicu(osnovicaUlaz), [osnovicaUlaz]);
  const efektivniVpsNum = useMemo(() => efektivniVps(osnovicaUlaz), [osnovicaUlaz]);
  const baseBodovi = osnovica?.bodovi ?? 0;

  // Uredba NN 37/23, Poglavlje I. pokriva parnični i ovršni postupak.
  // Pristojba se veže uz stvarnu vrijednost spora, ne uz osnovicu iz Tbr. 45.
  const pristojbaOsnova = odrediPristojbuOsnovu(odabraneDefinicije);

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
      }),
    [odabraneDefinicije, zaprijecenaKazna, isOduzimanjePrekrsaj, vrijednostOduzimanja, osnovicaTbr7, razinaSuda, tbr45Nacin],
  );

  // Tbr. 41 — povišenje i sniženje ne mogu stajati na istoj stavci.
  // Prilagodba se veže uz redak, pa se i provjerava po retku.
  const nedostaciTbr41 = useMemo(() => {
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));

    return Array.from(
      new Set(
        odabraneRadnje
          .map((radnja) => {
            const def = catalogMap.get(radnja.itemId);
            if (!def) return null;

            const { povisenje, snizenje } = razrijesiTbr41(
              tbr41PoStavci.get(radnja.redakId) ?? {},
              tbr41Povisenje,
              tbr41Snizenje,
            );
            return provjeriTbr41Stavku(def.opis, povisenje, snizenje);
          })
          .filter((p): p is string => p !== null && p !== undefined),
      ),
    );
  }, [odabraneRadnje, katalog, tbr41PoStavci, tbr41Povisenje, tbr41Snizenje]);

  const sviNedostaci = useMemo(
    () => [...nedostaci, ...nedostaciTbr41],
    [nedostaci, nedostaciTbr41],
  );

  // Datum se traži za spremanje i ispis, ali NE za izračun — zato stoji odvojeno od
  // `sviNedostaci`, koji zaključava i gumb za izračun.
  const nedostajuDatumi = useMemo(() => {
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));
    return nedostatakDatuma(
      odabraneRadnje.map((radnja) => ({
        datum: radnja.datum,
        opis: catalogMap.get(radnja.itemId)?.opis ?? radnja.itemId,
      })),
    );
  }, [odabraneRadnje, katalog]);

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

  // Client-side live calculation
  const liveCalc = useMemo(() => {
    const lines: {
      opis: string;
      tarifni_broj: string;
      kolicina: number;
      bodova_po_jedinici: number;
      ukupno_bodova: number;
      iznos_eur: number;
      jedinica: string;
      is_jednokratna: boolean;
      obuhvaceno_jednokratnom: boolean;
      pravni_izvor: string;
      item_id: string;
      redak_id: string;
      datum: string;
      tbr41_povisenje: number;
      tbr41_snizenje: number;
      tbr41_bodovi: number;
      tbr41_pravni_izvor: string;
    }[] = [];
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));

    // Jednokratna nagrada (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) pripada cijelom postupku,
    // ne pojedinoj radnji — računa se jednom za sve retke iste stavke, kao na poslužitelju.
    const udjeli = grupirajJednokratne(
      odabraneRadnje.map((radnja) => ({ itemId: radnja.itemId, kolicina: radnja.kolicina })),
      (itemId) => catalogMap.get(itemId)?.jednokratna_nagrada === true,
    );

    odabraneRadnje.forEach((radnja, polozaj) => {
      const { itemId, kolicina: kol } = radnja;
      const def = catalogMap.get(itemId);
      if (!def || kol <= 0) return;

      let bodovi: number;
      if (def.prekrsaj_oduzimanje) {
        // Prekršaj s oduzimanjem — Tbr. 7.1 po vrijednosti oduzetog predmeta
        if (isOduzimanjePrekrsaj && parsedVrijednostOduzimanja > 0) {
          bodovi = tbr71Bodovi(parsedVrijednostOduzimanja);
        } else {
          bodovi = 100; // fallback: standard prekršaj 100 bodova
        }
      } else if (def.kazneni_base) {
        bodovi = izracunajKazneneBodove(def, kazneniTierBodovi, isZastupnikDjeteta, isPoSluzbenojDuznosti, isNaTeretProracuna, zaprijecenaKazna);
      } else if (osnovica) {
        bodovi = izracunajBodove(def, osnovica);
      } else {
        // Osnovica nije razriješena (Tbr. 45 bez razine suda ili načina) —
        // razlog stoji u `nedostaci`, iznad gumba za izračun.
        return;
      }

      // Redak s 0 bodova izgleda kao valjan iznos, a nije — ne prikazuje se.
      // Razlog stoji u `nedostaci`, iznad gumba za izračun.
      if (bodovi <= 0) return;

      // Nagradu nosi prvi redak grupe, s količinom cijele grupe; ostali stoje u
      // troškovniku sa svojim datumom, ali bez bodova.
      const udio = udjeli[polozaj];
      const obuhvacen = !udio.nosiNagradu;
      const ukupnoStavke = obuhvacen ? 0 : ukupnoBodovaZaStavku(def, bodovi, udio.kolicina);

      // Tbr. 41 po pojedinom retku; bez vlastitog postotka vrijedi prečac.
      const { povisenje, snizenje } = razrijesiTbr41(
        tbr41PoStavci.get(radnja.redakId) ?? {},
        tbr41Povisenje,
        tbr41Snizenje,
      );
      const tbr41BodoviStavke = ukupnoStavke * tbr41Udio(povisenje, snizenje);

      lines.push({
        opis: def.opis,
        tarifni_broj: def.tarifni_broj,
        kolicina: kol,
        // Bodovi po jedinici na obuhvaćenom retku su 0: „250 bod./jed." uz iznos
        // 0,00 € izgleda kao greška u zbrajanju.
        bodova_po_jedinici: obuhvacen ? 0 : bodovi,
        ukupno_bodova: ukupnoStavke,
        iznos_eur: zaokruziNovac((ukupnoStavke + tbr41BodoviStavke) * BOD_VALUE),
        jedinica: def.jedinica,
        is_jednokratna: def.jednokratna_nagrada === true,
        obuhvaceno_jednokratnom: obuhvacen,
        pravni_izvor: def.pravni_izvor ?? def.tarifni_broj,
        item_id: itemId,
        redak_id: radnja.redakId,
        datum: radnja.datum,
        tbr41_povisenje: povisenje,
        tbr41_snizenje: snizenje,
        tbr41_bodovi: tbr41BodoviStavke,
        tbr41_pravni_izvor: tbr41Izvor(povisenje, snizenje),
      });
    });

    const osnovicaBodova = lines.reduce((s, l) => s + l.ukupno_bodova, 0);

    // Tbr. 40. toč. 1 — povišenje za drugu i svaku sljedeću zastupanu osobu.
    const povisenjePostotak = povisenjeViseOsobaPostotak(brojZastupanihOsoba);
    const povisenjeBodovi = osnovicaBodova * povisenjePostotak;

    // Tbr. 41 — zbroj prilagodbi po stavkama. Doprinos je aditivan na ukupan iznos,
    // kao i Tbr. 40, iako se računa po stavci; sudska pristojba se ne dira.
    const tbr41Bodovi = lines.reduce((sum, l) => sum + l.tbr41_bodovi, 0);
    const tbr41Postotak = osnovicaBodova === 0 ? 0 : tbr41Bodovi / osnovicaBodova;

    const ukupnoBodova = osnovicaBodova + povisenjeBodovi + tbr41Bodovi;

    // Bodovi su do ovdje ostali decimalni; tek se gotov iznos zaokružuje, jednom.
    const ukupnoEur = zaokruziNovac(ukupnoBodova * BOD_VALUE);
    const pdv = isPdvUkljucen ? zaokruziNovac(ukupnoEur * PDV_RATE) : 0;
    const odvjetnickiTrosak = ukupnoEur + pdv;

    // Sudske pristojbe (Uredba NN 37/2023, Tar. br. 1)
    const sudskaPristojba = isOslobodjenPristojbi
      ? 0
      : izracunajSudskuPristojbu(vrijednostNum, pristojbaOsnova);
    const sveukupniTrosak = odvjetnickiTrosak + sudskaPristojba;

    // Tbr. 40. toč. 1, drugi odlomak — dioba na jednake dijelove po zastupanoj osobi.
    const bodoviPoOsobi = podijeliPoOsobama(ukupnoBodova, brojZastupanihOsoba);
    const nagradaPoOsobi = zaokruziNovac(podijeliPoOsobama(ukupnoEur, brojZastupanihOsoba));

    return { lines, osnovicaBodova, povisenjePostotak, povisenjeBodovi, tbr41Postotak, tbr41Bodovi, ukupnoBodova, bodoviPoOsobi, nagradaPoOsobi, ukupnoEur, pdv, ukupnoSPdv: ukupnoEur + pdv, sudskaPristojba, sveukupniTrosak };
  }, [odabraneRadnje, katalog, osnovica, isPdvUkljucen, kazneniTierBodovi, isZastupnikDjeteta, isPoSluzbenojDuznosti, isNaTeretProracuna, isOduzimanjePrekrsaj, parsedVrijednostOduzimanja, isOslobodjenPristojbi, vrijednostNum, pristojbaOsnova, brojZastupanihOsoba, zaprijecenaKazna, tbr41Povisenje, tbr41Snizenje, tbr41PoStavci]);

  // ── Handlers ──
  const toggleCategory = useCallback((cat: string) => {
    setExpandedCategories((prev) => {
      const next = new Set(prev);
      if (next.has(cat)) next.delete(cat);
      else next.add(cat);
      return next;
    });
  }, []);

  /** Svaki klik na „Dodaj" otvara NOVU radnju — istu stavku odvjetnik poduzima više puta. */
  const dodajRadnju = useCallback((itemId: string) => {
    setOdabraneRadnje((prev) => [
      ...prev,
      { redakId: `r${sljedeciRedakId.current++}`, itemId, kolicina: 1, datum: "" },
    ]);
  }, []);

  const postaviKolicinu = useCallback((redakId: string, kolicina: number) => {
    setOdabraneRadnje((prev) =>
      kolicina <= 0
        ? prev.filter((radnja) => radnja.redakId !== redakId)
        : prev.map((radnja) => (radnja.redakId === redakId ? { ...radnja, kolicina } : radnja)),
    );
  }, []);

  const postaviDatum = useCallback((redakId: string, datum: string) => {
    setOdabraneRadnje((prev) =>
      prev.map((radnja) => (radnja.redakId === redakId ? { ...radnja, datum } : radnja)),
    );
  }, []);

  const ukloniRadnju = useCallback((redakId: string) => {
    setOdabraneRadnje((prev) => prev.filter((radnja) => radnja.redakId !== redakId));
    // Prilagodba po Tbr. 41 vezana je uz redak; bez retka nema što prilagođavati.
    setTbr41PoStavci((prev) => {
      if (!prev.has(redakId)) return prev;
      const next = new Map(prev);
      next.delete(redakId);
      return next;
    });
  }, []);

  /** Kronološki poredak na zahtjev — zadano korisnik zadržava svoj redoslijed. */
  const poredajRadnje = useCallback(() => {
    setOdabraneRadnje((prev) => poredajPoDatumu(prev));
  }, []);

  /** Sve radnje iste tarifne stavke — katalog na njima pokazuje koliko ih je dodano. */
  const brojRadnjiPoStavci = useMemo(() => {
    const broj = new Map<string, number>();
    odabraneRadnje.forEach((radnja) => broj.set(radnja.itemId, (broj.get(radnja.itemId) ?? 0) + 1));
    return broj;
  }, [odabraneRadnje]);

  /**
   * Tbr. 41 za pojedini redak. Vrijednost je "p<postotak>" za povišenje po toč. 1,
   * "s<postotak>" za sniženje po toč. 2, prazno za nasljeđivanje prečaca.
   *
   * Veže se uz redak, ne uz tarifnu stavku: dvije radnje iste stavke mogu tražiti
   * različitu prilagodbu (toč. 2 govori o „pojedinim tarifnim stavkama").
   */
  const postaviTbr41ZaStavku = useCallback((redakId: string, vrijednost: string) => {
    setTbr41PoStavci((prev) => {
      const next = new Map(prev);
      if (!vrijednost) {
        // Izričita nula, ne nasljeđivanje — inače bi prečac vratio postotak natrag.
        next.set(redakId, { povisenje: 0, snizenje: 0 });
      } else if (vrijednost.startsWith("p")) {
        next.set(redakId, { povisenje: Number(vrijednost.slice(1)), snizenje: 0 });
      } else {
        next.set(redakId, { povisenje: 0, snizenje: Number(vrijednost.slice(1)) });
      }
      return next;
    });
  }, []);

  /** Prečac "primijeni na sve stavke" — briše sve pojedinačne postotke. */
  const primijeniTbr41NaSve = useCallback(() => {
    setTbr41PoStavci(new Map());
  }, []);

  const clearAll = useCallback(() => {
    setOdabraneRadnje([]);
    // Prilagodbe po Tbr. 41 ključem su vezane uz retke kojih više nema.
    setTbr41PoStavci(new Map());
    setResult(null);
    setCalcError(null);
  }, []);

  // ── Submit to backend ──
  const handleCalculate = async () => {
    if (odabraneRadnje.length === 0) {
      setCalcError("Morate odabrati barem jednu stavku.");
      return;
    }
    setCalcError(null);
    setCalculating(true);

    try {
      const body = {
        vrijednost_spora: vrijednostNum,
        stavke: odabraneRadnje.map((radnja) => {
          const vlastiti = tbr41PoStavci.get(radnja.redakId);
          return {
            item_id: radnja.itemId,
            kolicina: radnja.kolicina,
            // Prazan datum se ne šalje — izračun ga ne traži, spremanje hoće.
            datum: radnja.datum || undefined,
            // null → stavka nasljeđuje postotak s razine troškovnika (prečac)
            tbr41_povisenje_postotak: vlastiti?.povisenje ?? null,
            tbr41_snizenje_postotak: vlastiti?.snizenje ?? null,
          };
        }),
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
        is_zastupnik_djeteta: isZastupnikDjeteta,
        is_stjecaj: isStjecaj,
        is_oduzimanje_prekrsaj: isOduzimanjePrekrsaj,
        vrijednost_oduzimanja: parsedVrijednostOduzimanja,
        is_oslobodjen_pristojbi: isOslobodjenPristojbi,
        broj_zastupanih_osoba: brojZastupanihOsoba,
        // Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45
        osnovica_tbr7: osnovicaTbr7,
        razina_suda: razinaSuda ?? undefined,
        tbr45_nacin: tbr45Nacin ?? undefined,
        renta_iznos: rentaIznosNum || undefined,
        renta_razdoblje: rentaRazdoblje ?? undefined,
        // Tbr. 41 — povisivanje i snižavanje tarifnih stavaka
        tbr41_povisenje_postotak: tbr41Povisenje,
        tbr41_snizenje_postotak: tbr41Snizenje,
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
    if (odabraneRadnje.length === 0) {
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
        stavke: odabraneRadnje.map((radnja) => {
          const vlastiti = tbr41PoStavci.get(radnja.redakId);
          return {
            item_id: radnja.itemId,
            kolicina: radnja.kolicina,
            // Prazan datum se ne šalje — izračun ga ne traži, spremanje hoće.
            datum: radnja.datum || undefined,
            // null → stavka nasljeđuje postotak s razine troškovnika (prečac)
            tbr41_povisenje_postotak: vlastiti?.povisenje ?? null,
            tbr41_snizenje_postotak: vlastiti?.snizenje ?? null,
          };
        }),
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
        is_zastupnik_djeteta: isZastupnikDjeteta,
        is_stjecaj: isStjecaj,
        is_oduzimanje_prekrsaj: isOduzimanjePrekrsaj,
        vrijednost_oduzimanja: parsedVrijednostOduzimanja,
        is_oslobodjen_pristojbi: isOslobodjenPristojbi,
        broj_zastupanih_osoba: brojZastupanihOsoba,
        // Osnovica po Tbr. 7. toč. 1., 4., 7. i 8., odnosno Tbr. 45
        osnovica_tbr7: osnovicaTbr7,
        razina_suda: razinaSuda ?? undefined,
        tbr45_nacin: tbr45Nacin ?? undefined,
        renta_iznos: rentaIznosNum || undefined,
        renta_razdoblje: rentaRazdoblje ?? undefined,
        // Tbr. 41 — povisivanje i snižavanje tarifnih stavaka
        tbr41_povisenje_postotak: tbr41Povisenje,
        tbr41_snizenje_postotak: tbr41Snizenje,
        client_id: selectedClientId || undefined,
        // Signal backend to auto-create client from stranke data if no client_id
        auto_create_client: !selectedClientId && !!klijentIme.trim(),
        client_grad: klijentGrad || undefined,
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

      const data = await res.json();
      setResult(data.troskovnik);
      setSavedId(data.savedId ?? null);

      // If backend auto-created a client, update local state
      if (data.createdClientId) {
        setSelectedClientId(data.createdClientId);
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
    window.print();
  }, []);

  // ── CSV export ──
  const handleExportCsv = useCallback(() => {
    const source = result?.stavke ?? liveCalc.lines.map((l, i) => ({ rbr: i + 1, ...l }));
    if (source.length === 0) return;

    const header = "Rbr;Datum;Tarifni broj;Pravni izvor;Opis;Količina;Bodova/jed.;Ukupno bodova;Iznos EUR\n";
    const rows = source
      .map((l, i) => {
        const rbr = "rbr" in l ? l.rbr : i + 1;
        const izvor = (l.pravni_izvor ?? l.tarifni_broj).replace(/"/g, "'");
        return `${rbr};"${formatirajDatum(l.datum)}";"${l.tarifni_broj}";"${izvor}";"${l.opis}";${l.kolicina};${fmtNum(l.bodova_po_jedinici)};${fmtNum(l.ukupno_bodova)};${formatNumber(l.iznos_eur)}`;
      })
      .join("\n");

    // Conditionally include PDV rows in CSV based on toggle
    const csvPdvActive = result ? (result.is_pdv_ukljucen ?? result.has_vat ?? isPdvUkljucen) : isPdvUkljucen;
    const csvPristojba = result?.sudska_pristojba ?? liveCalc.sudskaPristojba;
    const csvSveukupni = result?.sveukupni_trosak ?? liveCalc.sveukupniTrosak;
    let totals: string;
    if (csvPdvActive) {
      const base = result ? result.ukupno_eur_bez_pdv : liveCalc.ukupnoEur;
      const pdvAmt = result ? result.pdv : liveCalc.pdv;
      const grand = result ? result.ukupno_eur_s_pdv : liveCalc.ukupnoSPdv;
      const bodova = result ? result.ukupno_bodova : liveCalc.ukupnoBodova;
      totals = `\n\n;;;;;;;Ukupno bodova;${bodova}\n;;;;;;;Osnovica (bez PDV);${formatNumber(base)}\n;;;;;;;PDV (25%);${formatNumber(pdvAmt)}\n;;;;;;;Odvjetnički trošak s PDV-om;${formatNumber(grand)}\n;;;;;;;Sudska pristojba (Tar. br. 1);${formatNumber(csvPristojba)}\n;;;;;;;SVEUKUPNI TROŠAK;${formatNumber(csvSveukupni)}`;
    } else {
      const base = result ? result.ukupno_eur_bez_pdv : liveCalc.ukupnoEur;
      const bodova = result ? result.ukupno_bodova : liveCalc.ukupnoBodova;
      totals = `\n\n;;;;;;;Ukupno bodova;${bodova}\n;;;;;;;Odvjetnički trošak;${formatNumber(base)}\n;;;;;;;Sudska pristojba (Tar. br. 1);${formatNumber(csvPristojba)}\n;;;;;;;SVEUKUPNI TROŠAK;${formatNumber(csvSveukupni)}`;
    }

    const bom = "\uFEFF";
    const blob = new Blob([bom + header + rows + totals], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `troskovnik-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }, [result, liveCalc]);

  // ═══════════════════════════════════════════════════════════════
  //  RENDER
  // ═══════════════════════════════════════════════════════════════

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-white">
      {/* ── Sticky Header ── */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40 print:hidden">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-3 flex items-center gap-4">
          <Link
            href="/"
            className="flex items-center gap-2 text-slate-400 hover:text-slate-700 transition text-sm"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Početna</span>
          </Link>
          <div className="h-5 w-px bg-slate-200" />
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-lg bg-indigo-100 flex items-center justify-center">
              <Calculator className="h-4 w-4 text-indigo-700" />
            </div>
            <div>
              <h1 className="text-sm font-bold text-slate-900">Odvjetnički Troškovnik</h1>
              <p className="text-[10px] text-slate-400">Tarifa NN 2023 · 1 bod = 2,00 €</p>
            </div>
          </div>
          {/* Action buttons */}
          <div className="ml-auto flex items-center gap-2">
            {liveCalc.lines.length > 0 && (
              <>
                <button
                  onClick={handleExportCsv}
                  className="hidden sm:flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-700 transition px-2.5 py-1.5 rounded-lg hover:bg-slate-100"
                >
                  <Download className="h-3.5 w-3.5" />
                  CSV
                </button>
                <button
                  onClick={handlePrint}
                  className="hidden sm:flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-700 transition px-2.5 py-1.5 rounded-lg hover:bg-slate-100"
                >
                  <Printer className="h-3.5 w-3.5" />
                  Ispis
                </button>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
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
            {/* ── Case value input (hidden when only kazneni items) ── */}
            {!isOnlyKazneni && (
            <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm">
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
                disabled={tbr41PoStavci.size === 0}
                className="w-full text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:text-slate-300 bg-indigo-50 hover:bg-indigo-100 disabled:bg-slate-50 py-2 rounded-lg transition"
              >
                Primijeni na sve stavke
                {tbr41PoStavci.size > 0 && ` (poništi ${tbr41PoStavci.size} pojedinačnih)`}
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
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isPoSluzbenojDuznosti}
                    onChange={(e) => setIsPoSluzbenojDuznosti(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-red-600 focus:ring-red-500"
                  />
                  <span className="text-xs text-slate-700">Branitelj po službenoj dužnosti <span className="text-slate-400">(–50%)</span></span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isNaTeretProracuna}
                    onChange={(e) => setIsNaTeretProracuna(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-red-600 focus:ring-red-500"
                  />
                  <span className="text-xs text-slate-700">Branitelj na teret proračunskih sredstava <span className="text-slate-400">(–50%)</span></span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={isZastupnikDjeteta}
                    onChange={(e) => setIsZastupnikDjeteta(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-red-600 focus:ring-red-500"
                  />
                  <span className="text-xs text-slate-700">Opunomoćenik djeteta žrtve kaznenog djela <span className="text-slate-400">(70% od Tbr. 4.8 = 35%)</span></span>
                </label>

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
            <div className="relative">
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
              const selectedInCat = items.filter((i) => brojRadnjiPoStavci.has(i.id)).length;

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
                        const brojRadnji = brojRadnjiPoStavci.get(item.id) ?? 0;
                        const bodovi = item.prekrsaj_oduzimanje
                          ? (isOduzimanjePrekrsaj && parsedVrijednostOduzimanja > 0
                              ? tbr71Bodovi(parsedVrijednostOduzimanja)
                              : 100)
                          : item.kazneni_base
                          ? izracunajKazneneBodove(item, kazneniTierBodovi, isZastupnikDjeteta, isPoSluzbenojDuznosti, isNaTeretProracuna, zaprijecenaKazna)
                          : osnovica
                          ? izracunajBodove(item, osnovica)
                          : null;
                        const iznosPerUnit = bodovi === null ? 0 : bodovi * BOD_VALUE;

                        return (
                          <div
                            key={item.id}
                            className={`px-4 py-3 flex items-center gap-3 hover:bg-slate-50 transition ${
                              brojRadnji > 0 ? "bg-indigo-50/40" : ""
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

                            {/* Dodavanje radnje — svaki klik otvara novi redak s vlastitim datumom.
                                Količina, datum i brisanje uređuju se na retku, u popisu desno. */}
                            <div className="shrink-0 flex items-center gap-2">
                              {brojRadnji > 0 && (
                                <span className="text-[10px] font-bold bg-indigo-600 text-white px-2 py-0.5 rounded-full">
                                  {brojRadnji}×
                                </span>
                              )}
                              <button
                                onClick={() => dodajRadnju(item.id)}
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
            <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
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
                      {selectedClientId && clientAutoFilled ? (
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
                      {clientAutoFilled && selectedClientId && (
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
                      <input type="text" value={odvjetnikOib} onChange={(e) => setOdvjetnikOib(e.target.value)} placeholder="OIB" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikAdresa} onChange={(e) => setOdvjetnikAdresa(e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikGrad} onChange={(e) => setOdvjetnikGrad(e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={odvjetnikIban} onChange={(e) => setOdvjetnikIban(e.target.value)} placeholder="IBAN (Broj računa)" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                    </div>
                  
                    {/* Protustranka */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <User className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-xs font-semibold text-slate-600 uppercase tracking-wider">Protustranka</span>
                      </div>
                      <input type="text" value={protustrankaIme} onChange={(e) => setProtustrankaIme(e.target.value)} placeholder="Ime / Naziv" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={protustrankaOib} onChange={(e) => setProtustrankaOib(e.target.value)} placeholder="OIB" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
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
                      <input type="text" value={klijentOib} onChange={(e) => handleKlijentManualEdit(setKlijentOib, e.target.value.replace(/\D/g, '').slice(0, 11))} placeholder="OIB (11 znamenki)" maxLength={11} pattern="\d{11}" className={`w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none ${klijentOib && klijentOib.length !== 11 ? 'border-amber-400 bg-amber-50' : 'border-slate-200'}`} />
                      {klijentOib && klijentOib.length > 0 && klijentOib.length !== 11 && (
                        <p className="text-xs text-amber-600">OIB mora sadržavati točno 11 znamenki ({klijentOib.length}/11)</p>
                      )}
                      <input type="text" value={klijentAdresa} onChange={(e) => handleKlijentManualEdit(setKlijentAdresa, e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={klijentGrad} onChange={(e) => handleKlijentManualEdit(setKlijentGrad, e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
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
          <div className="space-y-4">
            {/* Sticky summary */}
            <div className="lg:sticky lg:top-20 space-y-4" ref={printRef}>
              {/* ── Selected items summary ── */}
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
                <div className="bg-gradient-to-r from-indigo-600 to-indigo-700 px-4 py-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ClipboardList className="h-4 w-4 text-indigo-200" />
                    <span className="text-sm font-bold text-white">Troškovnik</span>
                  </div>
                  {odabraneRadnje.length > 0 && (
                    <div className="flex items-center gap-3">
                      <button
                        onClick={poredajRadnje}
                        className="flex items-center gap-1 text-[10px] text-indigo-200 hover:text-white transition"
                        title="Kronološki; radnje bez datuma idu na dno"
                      >
                        <CalendarDays className="h-3 w-3" />
                        Poredaj po datumu
                      </button>
                      <button
                        onClick={clearAll}
                        className="text-[10px] text-indigo-200 hover:text-white transition"
                      >
                        Očisti sve
                      </button>
                    </div>
                  )}
                </div>

                <div className="p-4">
                  {odabraneRadnje.length === 0 ? (
                    <div className="text-center py-8 text-slate-400">
                      <FileText className="h-8 w-8 mx-auto mb-2 opacity-40" />
                      <p className="text-sm">Odaberite stavke iz tarife</p>
                      <p className="text-xs mt-1">Kliknite &quot;Dodaj&quot; pokraj željene stavke</p>
                    </div>
                  ) : (
                    <div className="space-y-2 max-h-[40vh] overflow-y-auto">
                      {liveCalc.lines.map((line) => (
                        <div key={line.redak_id} className="flex items-start justify-between gap-2 py-1.5 border-b border-slate-100 last:border-0">
                          <div className="min-w-0 flex-1">
                            <p className="text-xs text-slate-700 leading-snug truncate" title={line.opis}>
                              {line.opis}
                            </p>
                            <p className="text-[10px] text-slate-400 mt-0.5">
                              {line.obuhvaceno_jednokratnom
                                ? "obuhvaćeno jednokratnom nagradom"
                                : line.is_jednokratna
                                ? `jednokratno · ${line.kolicina} ${line.kolicina === 1 ? "radnja" : "radnji"} = ${fmtNum(line.ukupno_bodova)} bod.`
                                : `${line.kolicina} × ${fmtNum(line.bodova_po_jedinici)} bod.`}
                            </p>
                            <p className="text-[9px] text-slate-400/80 leading-snug mt-0.5" title={line.pravni_izvor}>
                              {line.pravni_izvor}
                            </p>

                            {/* Uređivanje retka — na papir ne ide, ondje stoje gotove vrijednosti. */}
                            <div className="flex items-center gap-1.5 mt-1 print:hidden">
                              <input
                                type="date"
                                value={line.datum}
                                onChange={(e) => postaviDatum(line.redak_id, e.target.value)}
                                className="text-[10px] px-1.5 py-0.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                title="Datum kad je radnja poduzeta — obavezan za spremanje i ispis"
                              />
                              <input
                                type="number"
                                min={1}
                                value={line.kolicina}
                                onChange={(e) => postaviKolicinu(line.redak_id, parseInt(e.target.value) || 0)}
                                className="w-12 text-[10px] px-1.5 py-0.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                title={`Količina (${line.jedinica})`}
                              />
                              <button
                                onClick={() => ukloniRadnju(line.redak_id)}
                                className="w-6 h-6 rounded bg-red-50 hover:bg-red-100 flex items-center justify-center transition"
                                title="Ukloni ovu radnju"
                              >
                                <Trash2 className="h-3 w-3 text-red-500" />
                              </button>
                            </div>

                            {/* Tbr. 41 — prilagodba ove stavke (toč. 2: "pojedine tarifne stavke") */}
                            <div className="flex items-center gap-1.5 mt-1 print:hidden">
                              <select
                                value={
                                  line.tbr41_povisenje > 0
                                    ? `p${line.tbr41_povisenje}`
                                    : line.tbr41_snizenje > 0
                                      ? `s${line.tbr41_snizenje}`
                                      : ""
                                }
                                onChange={(e) => postaviTbr41ZaStavku(line.redak_id, e.target.value)}
                                className="text-[10px] px-1.5 py-0.5 border border-slate-200 rounded bg-white outline-none focus:ring-1 focus:ring-indigo-500"
                                title="Tbr. 41 — povišenje (toč. 1) ili sniženje (toč. 2) ove stavke"
                              >
                                <option value="">Tbr. 41: bez prilagodbe</option>
                                <option value="p25">+25 % (toč. 1)</option>
                                <option value="p50">+50 % (toč. 1)</option>
                                <option value="p100">+100 % (toč. 1)</option>
                                <option value="s25">−25 % (toč. 2)</option>
                                <option value="s50">−50 % (toč. 2)</option>
                              </select>
                              {line.tbr41_bodovi !== 0 && (
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
                          <span className="text-xs font-semibold text-slate-800 shrink-0">
                            {fmtEur(line.iznos_eur)}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Totals */}
                  {odabraneRadnje.length > 0 && (
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
                      <div className="flex justify-between text-xs text-slate-500">
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
                              onChange={(e) => setIsPdvUkljucen(e.target.checked)}
                              className="h-4 w-4 rounded border-slate-200 text-indigo-600 focus:ring-indigo-500"
                            />
                            <span className="text-xs">U sustavu PDV-a</span>
                          </label>
                        </div>
                        <span className="font-semibold">{isPdvUkljucen ? 'PDV 25%' : 'PDV 0%'}</span>
                      </div>

                      {/* Totals when PDV included */}
                      <div className={`transition-all duration-300 ease-in-out overflow-hidden ${isPdvUkljucen ? 'max-h-80 opacity-100' : 'max-h-0 opacity-0'}`}>
                        <div className="flex justify-between text-xs text-slate-500 mt-2">
                          <span>Osnovica (bez PDV)</span>
                          <span className="font-semibold">{fmtEur(liveCalc.ukupnoEur)}</span>
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
                          <span>Odvjetnički trošak</span>
                          <span>{fmtEur(liveCalc.ukupnoEur)}</span>
                        </div>
                      </div>

                      {/* ── Sudske pristojbe ── */}
                      <div className="pt-2 mt-1 border-t border-dashed border-slate-200 space-y-1.5">
                        {pristojbaOsnova === "Nema" ? (
                          <div className="flex items-start gap-1.5 text-[10px] text-slate-500 leading-snug">
                            <Info className="h-3 w-3 shrink-0 mt-0.5 text-slate-400" />
                            <span>{objasnjenjePristojbe("Nema")}</span>
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
                            <div className="flex justify-between text-xs text-slate-600">
                              <span>Sudska pristojba (Tar. br. 1)</span>
                              <span className={`font-semibold ${isOslobodjenPristojbi ? 'line-through text-slate-400' : ''}`}>
                                {fmtEur(isOslobodjenPristojbi ? 0 : liveCalc.sudskaPristojba)}
                              </span>
                            </div>
                            <p className="text-[10px] text-slate-400">{objasnjenjePristojbe(pristojbaOsnova)}</p>
                          </>
                        )}
                      </div>

                      {/* ── Grand Total ── */}
                      <div className="flex justify-between text-sm font-bold text-indigo-800 pt-2 mt-1 border-t border-slate-300">
                        <span>SVEUKUPNI TROŠAK</span>
                        <span>{fmtEur(liveCalc.sveukupniTrosak)}</span>
                      </div>

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
                    </div>
                  )}
                </div>

                {/* Action buttons */}
                {odabraneRadnje.length > 0 && (
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
                    {/* Datum ne blokira izračun — samo spremanje i ispis. */}
                    {nedostajuDatumi && (
                      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
                        <p className="flex items-start gap-1.5 text-[11px] text-amber-800 leading-snug">
                          <CalendarDays className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                          <span>{nedostajuDatumi}</span>
                        </p>
                      </div>
                    )}
                    <CreditQuotaBadge quota={troskovnikQuota} loading={creditsLoading} className="mb-1" />
                    <button
                      onClick={handleCalculate}
                      disabled={calculating || troskovnikQuota.exhausted || sviNedostaci.length > 0}
                      className="w-full flex items-center justify-center gap-2 bg-indigo-600 hover:bg-indigo-700 disabled:bg-indigo-400 text-white text-sm font-semibold py-2.5 rounded-lg transition"
                    >
                      {calculating ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Calculator className="h-4 w-4" />
                      )}
                      {calculating ? "Izračunavam..." : "Generiraj troškovnik"}
                    </button>
                    {userId && (
                      <button
                        onClick={handleSave}
                        disabled={saving || sviNedostaci.length > 0 || nedostajuDatumi !== null}
                        className="w-full flex items-center justify-center gap-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-emerald-400 text-white text-sm font-semibold py-2.5 rounded-lg transition"
                      >
                        {saving ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Save className="h-4 w-4" />
                        )}
                        {saving ? "Spremam..." : "Spremi troškovnik"}
                      </button>
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
                        onClick={handleExportCsv}
                        className="flex-1 flex items-center justify-center gap-1.5 text-xs text-slate-600 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 py-2 rounded-lg transition"
                      >
                        <Download className="h-3.5 w-3.5" />
                        CSV
                      </button>
                      <button
                        onClick={handlePrint}
                        disabled={nedostajuDatumi !== null}
                        title={nedostajuDatumi ?? undefined}
                        className="flex-1 flex items-center justify-center gap-1.5 text-xs text-slate-600 hover:text-slate-800 disabled:text-slate-300 bg-slate-100 hover:bg-slate-200 disabled:hover:bg-slate-100 py-2 rounded-lg transition"
                      >
                        <Printer className="h-3.5 w-3.5" />
                        Ispis
                      </button>
                    </div>
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
                  {(result.datum_od || result.datum_do) && (
                    <p className="text-white text-xs font-semibold">
                      {result.datum_od === result.datum_do
                        ? `Radnja od ${formatirajDatum(result.datum_od)}`
                        : `Za razdoblje ${formatirajDatum(result.datum_od)} – ${formatirajDatum(result.datum_do)}`}
                    </p>
                  )}
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
                      { (result as any).sud || (result as any).broj_predmeta ? (
                        <>
                          <div>{(result as any).sud ?? ""}{(result as any).broj_predmeta ? ` · ${ (result as any).broj_predmeta}` : ""}</div>
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
              {/* Party info */}
              {(result.odvjetnik?.ime || result.klijent?.ime || result.protustranka?.ime) && (
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
                  {result.odvjetnik?.ime && (
                    <div className="bg-slate-50 rounded-xl p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <Building2 className="h-4 w-4 text-slate-500" />
                        <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Odvjetnik</span>
                      </div>
                      <p className="text-sm font-medium text-slate-900">{result.odvjetnik.ime}</p>
                      {result.odvjetnik.oib && <p className="text-xs text-slate-500">OIB: {result.odvjetnik.oib}</p>}
                      {result.odvjetnik.adresa && <p className="text-xs text-slate-500">{result.odvjetnik.adresa}{result.odvjetnik.grad ? `, ${result.odvjetnik.grad}` : ""}</p>}
                      {result.odvjetnik.iban && <p className="text-xs text-slate-500">IBAN: {result.odvjetnik.iban}</p>}
                    </div>
                  )}
                  {result.klijent?.ime && (
                    <div className="bg-slate-50 rounded-xl p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <User className="h-4 w-4 text-slate-500" />
                        <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Klijent</span>
                      </div>
                      <p className="text-sm font-medium text-slate-900">{result.klijent.ime}</p>
                      {result.klijent.oib && <p className="text-xs text-slate-500">OIB: {result.klijent.oib}</p>}
                      {result.klijent.adresa && <p className="text-xs text-slate-500">{result.klijent.adresa}{result.klijent.grad ? `, ${result.klijent.grad}` : ""}</p>}
                    </div>
                  )}
                  {result.protustranka?.ime && (
                    <div className="bg-slate-50 rounded-xl p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <User className="h-4 w-4 text-slate-500" />
                        <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Protustranka</span>
                      </div>
                      <p className="text-sm font-medium text-slate-900">{result.protustranka.ime}</p>
                      {result.protustranka.oib && <p className="text-xs text-slate-500">OIB: {result.protustranka.oib}</p>}
                      {result.protustranka.adresa && <p className="text-xs text-slate-500">{result.protustranka.adresa}{result.protustranka.grad ? `, ${result.protustranka.grad}` : ""}</p>}
                      {result.protustranka_polozaj && <p className="text-xs text-slate-500">Položaj: {result.protustranka_polozaj}</p>}
                    </div>
                  )}
                </div>
              )}

              {(result.sud || (result as any).broj_predmeta) && (
                <p className="text-sm text-slate-600 mb-4">
                  <span className="font-semibold">Predmet:</span> {result.sud ?? ""}{(result as any).broj_predmeta ? ` · ${ (result as any).broj_predmeta}` : ""}
                </p>
              )}

              {/* Line items table */}
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b-2 border-indigo-200">
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-10">Rbr.</th>
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-24">Datum</th>
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
                        <td className="py-2 px-2 text-slate-600 whitespace-nowrap">{formatirajDatum(line.datum)}</td>
                        <td className="py-2 px-2 text-slate-800">
                          {line.opis}
                          {line.obuhvaceno_jednokratnom ? (
                            <span className="ml-1.5 text-[10px] font-semibold text-slate-500">(obuhvaćeno jednokratnom nagradom)</span>
                          ) : line.is_jednokratna && (
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
                    <tr className="border-t-2 border-indigo-200">
                      <td colSpan={6} className="py-2 px-2 text-right text-xs text-slate-500 font-semibold">Ukupno bodova</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtNum(result.ukupno_bodova)}</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtEur(result.ukupno_eur_bez_pdv)}</td>
                    </tr>
                    {(() => {
                      const isPdv = (result as any).is_pdv_ukljucen !== undefined ? (result as any).is_pdv_ukljucen : ((result as any).has_vat !== undefined ? (result as any).has_vat : false);
                      const base = Number(result.ukupno_eur_bez_pdv ?? 0);
                      const pdvCalc = isPdv ? Math.round(base * 0.25 * 100) / 100 : 0;
                      const odvjetnickiTrosak = Math.round((base + pdvCalc) * 100) / 100;
                      const pristojba = Number(result.sudska_pristojba ?? 0);
                      const sveukupni = Number(result.sveukupni_trosak ?? (odvjetnickiTrosak + pristojba));

                      return (
                        <>
                          {isPdv && (
                            <>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">Osnovica (bez PDV)</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(base)}</td>
                              </tr>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">PDV (25%)</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(pdvCalc)}</td>
                              </tr>
                              <tr>
                                <td colSpan={6} className="py-1 px-2 text-right text-xs font-semibold text-indigo-700">Odvjetnički trošak s PDV-om</td>
                                <td />
                                <td className="py-1 px-2 text-right text-sm font-semibold text-indigo-700">{fmtEur(odvjetnickiTrosak)}</td>
                              </tr>
                            </>
                          )}
                          {!isPdv && (
                            <tr>
                              <td colSpan={6} className="py-1 px-2 text-right text-xs font-semibold text-indigo-700">Odvjetnički trošak</td>
                              <td />
                              <td className="py-1 px-2 text-right text-sm font-semibold text-indigo-700">{fmtEur(base)}</td>
                            </tr>
                          )}
                          <tr>
                            <td colSpan={6} className="py-1 px-2 text-right text-xs text-slate-500">
                              Sudska pristojba (Tar. br. 1)
                              {result.is_oslobodjen_pristojbi && <span className="ml-1 text-amber-600">(oslobođeno)</span>}
                              <span className="block text-[10px] text-slate-400">
                                {objasnjenjePristojbe(result.pristojba_osnova ?? "Puna")}
                              </span>
                            </td>
                            <td />
                            <td className={`py-1 px-2 text-right text-sm ${result.is_oslobodjen_pristojbi ? 'line-through text-slate-400' : 'text-slate-600'}`}>
                              {fmtEur(pristojba)}
                            </td>
                          </tr>
                          <tr className="bg-indigo-50">
                            <td colSpan={6} className="py-3 px-2 text-right text-sm font-bold text-indigo-800">SVEUKUPNI TROŠAK</td>
                            <td />
                            <td className="py-3 px-2 text-right text-lg font-bold text-indigo-800">{fmtEur(sveukupni)}</td>
                          </tr>
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

              {/* Footer */}
              <div className="mt-4 pt-3 border-t border-slate-200 text-[10px] text-slate-400 leading-relaxed">
                <p>Izračun je napravljen prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (Hrvatska odvjetnička komora, 14. listopada 2023.). Vrijednost boda: {fmtEur(BOD_VALUE)} (Tbr. 54). Sudske pristojbe prema Uredbi o tarifi sudskih pristojbi (NN 37/2023), Tar. br. 1.</p>
                <p className="mt-1">Ovaj troškovnik služi kao orijentacijski izračun i ne predstavlja pravno obvezujući dokument.</p>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── Print Styles ── */}
      <style jsx global>{`
        @media print {
          body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
          .print\\:hidden { display: none !important; }
          #troskovnik-result { border: none !important; box-shadow: none !important; margin: 0 !important; border-radius: 0 !important; }
        }
      `}</style>
    </div>
  );
}
