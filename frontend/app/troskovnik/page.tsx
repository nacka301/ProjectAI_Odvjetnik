"use client";

import { useCallback, useEffect, useMemo, useRef, useState, Suspense } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import {
  ArrowLeft,
  Calculator,
  ChevronDown,
  ChevronRight,
  ClipboardList,
  Download,
  Euro,
  FileText,
  Loader2,
  Minus,
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

const API = API_BASE;
const BOD_VALUE = 2.0;
const PDV_RATE = 0.25;

// ── Types (mirror backend models) ──

interface TarifnaStavka {
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
}

interface SelectedItem {
  itemId: string;
  kolicina: number;
}

interface TroskovnikLineItem {
  rbr: number;
  item_id: string;
  opis: string;
  tarifni_broj: string;
  kolicina: number;
  bodova_po_jedinici: number;
  ukupno_bodova: number;
  iznos_eur: number;
}

interface TroskovnikResponse {
  stavke: TroskovnikLineItem[];
  ukupno_bodova: number;
  ukupno_eur_bez_pdv: number;
  pdv: number;
  ukupno_eur_s_pdv: number;
  vrijednost_boda: number;
  klijent: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  odvjetnik: { ime: string; oib?: string; adresa?: string; grad?: string } | null;
  opis_predmeta: string | null;
  datum_izrade: string;
}

// ── Tbr. 7.1 client-side point lookup (mirrors backend) ──

function tbr71Bodovi(vrijednost: number): number {
  if (vrijednost <= 0) return 25;
  if (vrijednost <= 332.0) return 25;
  if (vrijednost <= 664.0) return 50;
  if (vrijednost <= 1327.0) return 75;
  if (vrijednost <= 13272.0) return 100;
  if (vrijednost <= 33181.0) return 250;
  if (vrijednost <= 66361.0) return 500;
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

function izracunajBodove(def: TarifnaStavka, baseBodovi: number): number {
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

  if (def.max_bodova !== null && def.max_bodova !== undefined && bodovi > def.max_bodova)
    bodovi = def.max_bodova;
  if (def.min_bodova !== null && def.min_bodova !== undefined && bodovi < def.min_bodova)
    bodovi = def.min_bodova;

  return bodovi;
}

// ── Format helpers ──

function fmtEur(n: number) {
  return n.toLocaleString("hr-HR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " €";
}

function fmtNum(n: number) {
  return n.toLocaleString("hr-HR");
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
  const [selectedItems, setSelectedItems] = useState<Map<string, number>>(new Map());
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
  const [showParties, setShowParties] = useState(false);

  // ── Client picker state ──
  type ClientOption = { id: string; ime_naziv: string; oib?: string; adresa?: string; grad?: string; tip: string };
  const [clients, setClients] = useState<ClientOption[]>([]);
  const [selectedClientId, setSelectedClientId] = useState<string>("");
  const [loadingClients, setLoadingClients] = useState(false);

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
        const res = await fetch(`${API}/api/troskovnik/katalog`, {
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
    fetch(`${API}/api/client`, { headers: authHeaders })
      .then((r) => (r.ok ? r.json() : []))
      .then((data: ClientOption[]) => {
        setClients(data);
        // Auto-select if arrived via ?clientId=...
        if (preselectedClientId) {
          const match = data.find((c) => c.id === preselectedClientId);
          if (match) {
            setSelectedClientId(match.id);
            setKlijentIme(match.ime_naziv);
            setKlijentOib(match.oib ?? "");
            setKlijentAdresa(match.adresa ?? "");
            setKlijentGrad(match.grad ?? "");
            setShowParties(true);
          }
        }
      })
      .catch(() => {})
      .finally(() => setLoadingClients(false));
  }, [userId, preselectedClientId]);

  // ── Client selection handler ──
  const handleClientSelect = (clientId: string) => {
    setSelectedClientId(clientId);
    if (!clientId) return;
    const c = clients.find((cl) => cl.id === clientId);
    if (c) {
      setKlijentIme(c.ime_naziv);
      setKlijentOib(c.oib ?? "");
      setKlijentAdresa(c.adresa ?? "");
      setKlijentGrad(c.grad ?? "");
    }
  };

  // ── Derived values ──
  const vrijednostNum = parseFloat(vrijednostSpora.replace(/\./g, "").replace(",", ".")) || 0;
  const baseBodovi = tbr71Bodovi(vrijednostNum);

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
    }[] = [];
    const catalogMap = new Map(katalog.map((s) => [s.id, s]));

    selectedItems.forEach((kol, itemId) => {
      const def = catalogMap.get(itemId);
      if (!def || kol <= 0) return;
      const bodovi = izracunajBodove(def, baseBodovi);
      lines.push({
        opis: def.opis,
        tarifni_broj: def.tarifni_broj,
        kolicina: kol,
        bodova_po_jedinici: bodovi,
        ukupno_bodova: bodovi * kol,
        iznos_eur: bodovi * kol * BOD_VALUE,
        jedinica: def.jedinica,
      });
    });

    const ukupnoBodova = lines.reduce((s, l) => s + l.ukupno_bodova, 0);
    const ukupnoEur = ukupnoBodova * BOD_VALUE;
    const pdv = Math.round(ukupnoEur * PDV_RATE * 100) / 100;

    return { lines, ukupnoBodova, ukupnoEur, pdv, ukupnoSPdv: ukupnoEur + pdv };
  }, [selectedItems, katalog, baseBodovi]);

  // ── Handlers ──
  const toggleCategory = useCallback((cat: string) => {
    setExpandedCategories((prev) => {
      const next = new Set(prev);
      if (next.has(cat)) next.delete(cat);
      else next.add(cat);
      return next;
    });
  }, []);

  const addItem = useCallback((itemId: string) => {
    setSelectedItems((prev) => {
      const next = new Map(prev);
      next.set(itemId, (next.get(itemId) || 0) + 1);
      return next;
    });
  }, []);

  const setItemQty = useCallback((itemId: string, qty: number) => {
    setSelectedItems((prev) => {
      const next = new Map(prev);
      if (qty <= 0) next.delete(itemId);
      else next.set(itemId, qty);
      return next;
    });
  }, []);

  const removeItem = useCallback((itemId: string) => {
    setSelectedItems((prev) => {
      const next = new Map(prev);
      next.delete(itemId);
      return next;
    });
  }, []);

  const clearAll = useCallback(() => {
    setSelectedItems(new Map());
    setResult(null);
    setCalcError(null);
  }, []);

  // ── Submit to backend ──
  const handleCalculate = async () => {
    if (selectedItems.size === 0) {
      setCalcError("Morate odabrati barem jednu stavku.");
      return;
    }
    setCalcError(null);
    setCalculating(true);

    try {
      const body = {
        vrijednost_spora: vrijednostNum,
        stavke: Array.from(selectedItems.entries()).map(([itemId, kolicina]) => ({
          item_id: itemId,
          kolicina,
        })),
        odvjetnik: odvjetnikIme
          ? { ime: odvjetnikIme, oib: odvjetnikOib || undefined, adresa: odvjetnikAdresa || undefined, grad: odvjetnikGrad || undefined }
          : undefined,
        klijent: klijentIme
          ? { ime: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined, grad: klijentGrad || undefined }
          : undefined,
        opis_predmeta: opisPredmeta || undefined,
      };

      const res = await fetch(`${API}/api/troskovnik/izracunaj`, {
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
    if (selectedItems.size === 0) {
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
        stavke: Array.from(selectedItems.entries()).map(([itemId, kolicina]) => ({
          item_id: itemId,
          kolicina,
        })),
        odvjetnik: odvjetnikIme
          ? { ime: odvjetnikIme, oib: odvjetnikOib || undefined, adresa: odvjetnikAdresa || undefined, grad: odvjetnikGrad || undefined }
          : undefined,
        klijent: klijentIme
          ? { ime: klijentIme, oib: klijentOib || undefined, adresa: klijentAdresa || undefined, grad: klijentGrad || undefined }
          : undefined,
        opis_predmeta: opisPredmeta || undefined,
        client_id: selectedClientId || undefined,
      };

      const res = await fetch(`${API}/api/troskovnik/spremi`, {
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

    const header = "Rbr;Tarifni broj;Opis;Količina;Bodova/jed.;Ukupno bodova;Iznos EUR\n";
    const rows = source
      .map((l, i) => {
        const rbr = "rbr" in l ? l.rbr : i + 1;
        return `${rbr};"${l.tarifni_broj}";"${l.opis}";${l.kolicina};${l.bodova_po_jedinici};${l.ukupno_bodova};${l.iznos_eur.toFixed(2)}`;
      })
      .join("\n");

    const totals = result
      ? `\n\n;;;;;Ukupno bodova;${result.ukupno_bodova}\n;;;;;Ukupno bez PDV;${result.ukupno_eur_bez_pdv.toFixed(2)}\n;;;;;PDV 25%;${result.pdv.toFixed(2)}\n;;;;;UKUPNO s PDV;${result.ukupno_eur_s_pdv.toFixed(2)}`
      : `\n\n;;;;;Ukupno bodova;${liveCalc.ukupnoBodova}\n;;;;;Ukupno bez PDV;${liveCalc.ukupnoEur.toFixed(2)}\n;;;;;PDV 25%;${liveCalc.pdv.toFixed(2)}\n;;;;;UKUPNO s PDV;${liveCalc.ukupnoSPdv.toFixed(2)}`;

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
            {/* ── Case value input ── */}
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
                  <div className="text-xs text-slate-500">Tbr. 7.1</div>
                  <div className="text-lg font-bold text-indigo-600">{fmtNum(baseBodovi)} bod.</div>
                </div>
              </div>
            </div>

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
              const selectedInCat = items.filter((i) => selectedItems.has(i.id)).length;

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
                        const qty = selectedItems.get(item.id) || 0;
                        const bodovi = izracunajBodove(item, baseBodovi);
                        const iznosPerUnit = bodovi * BOD_VALUE;

                        return (
                          <div
                            key={item.id}
                            className={`px-4 py-3 flex items-center gap-3 hover:bg-slate-50 transition ${
                              qty > 0 ? "bg-indigo-50/40" : ""
                            }`}
                          >
                            {/* Info */}
                            <div className="flex-1 min-w-0">
                              <p className="text-sm text-slate-800 leading-snug">{item.opis}</p>
                              <div className="flex items-center gap-3 mt-1">
                                <span className="text-[10px] font-mono text-slate-400">{item.tarifni_broj}</span>
                                <span className="text-[10px] text-indigo-600 font-semibold">
                                  {fmtNum(bodovi)} bod. = {fmtEur(iznosPerUnit)}
                                </span>
                                <span className="text-[10px] text-slate-400">/ {item.jedinica}</span>
                              </div>
                            </div>

                            {/* Quantity controls */}
                            {qty === 0 ? (
                              <button
                                onClick={() => addItem(item.id)}
                                className="shrink-0 flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800 bg-indigo-50 hover:bg-indigo-100 px-3 py-1.5 rounded-lg transition"
                              >
                                <Plus className="h-3.5 w-3.5" />
                                Dodaj
                              </button>
                            ) : (
                              <div className="shrink-0 flex items-center gap-1">
                                <button
                                  onClick={() => setItemQty(item.id, qty - 1)}
                                  className="w-7 h-7 rounded-md bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition"
                                >
                                  <Minus className="h-3 w-3 text-slate-600" />
                                </button>
                                <input
                                  type="number"
                                  min={0}
                                  value={qty}
                                  onChange={(e) => setItemQty(item.id, parseInt(e.target.value) || 0)}
                                  className="w-12 text-center text-sm border border-slate-200 rounded-md py-1 focus:ring-1 focus:ring-indigo-400 outline-none"
                                />
                                <button
                                  onClick={() => setItemQty(item.id, qty + 1)}
                                  className="w-7 h-7 rounded-md bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition"
                                >
                                  <Plus className="h-3 w-3 text-slate-600" />
                                </button>
                                <button
                                  onClick={() => removeItem(item.id)}
                                  className="w-7 h-7 rounded-md bg-red-50 hover:bg-red-100 flex items-center justify-center transition ml-1"
                                >
                                  <Trash2 className="h-3 w-3 text-red-500" />
                                </button>
                              </div>
                            )}
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
                    <label className="block text-xs font-semibold text-slate-600 mb-1">Opis predmeta</label>
                    <input
                      type="text"
                      value={opisPredmeta}
                      onChange={(e) => setOpisPredmeta(e.target.value)}
                      placeholder="npr. Parnični predmet P-123/24"
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                    />
                  </div>

                  {/* Client picker */}
                  {clients.length > 0 && (
                    <div>
                      <label className="block text-xs font-semibold text-slate-600 mb-1">
                        <div className="flex items-center gap-1.5">
                          <Users className="h-3.5 w-3.5 text-slate-400" />
                          Poveži sa strankom
                        </div>
                      </label>
                      <select
                        value={selectedClientId}
                        onChange={(e) => handleClientSelect(e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                      >
                        <option value="">— bez stranke —</option>
                        {clients.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.ime_naziv}{c.oib ? ` (OIB: ${c.oib})` : ""} — {c.tip === "pravna" ? "Pravna" : "Fizička"}
                          </option>
                        ))}
                      </select>
                      {selectedClientId && (
                        <p className="mt-1 text-[10px] text-emerald-600 flex items-center gap-1">
                          <CheckCircle className="h-3 w-3" />
                          Troškovnik će biti povezan s ovom strankom
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
                    </div>

                    {/* Klijent */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-1.5 mb-1">
                        <User className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-xs font-semibold text-slate-600 uppercase tracking-wider">Klijent</span>
                      </div>
                      <input type="text" value={klijentIme} onChange={(e) => setKlijentIme(e.target.value)} placeholder="Ime / Naziv" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={klijentOib} onChange={(e) => setKlijentOib(e.target.value)} placeholder="OIB" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={klijentAdresa} onChange={(e) => setKlijentAdresa(e.target.value)} placeholder="Adresa" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
                      <input type="text" value={klijentGrad} onChange={(e) => setKlijentGrad(e.target.value)} placeholder="Grad" className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none" />
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
                  {selectedItems.size > 0 && (
                    <button
                      onClick={clearAll}
                      className="text-[10px] text-indigo-200 hover:text-white transition"
                    >
                      Očisti sve
                    </button>
                  )}
                </div>

                <div className="p-4">
                  {selectedItems.size === 0 ? (
                    <div className="text-center py-8 text-slate-400">
                      <FileText className="h-8 w-8 mx-auto mb-2 opacity-40" />
                      <p className="text-sm">Odaberite stavke iz tarife</p>
                      <p className="text-xs mt-1">Kliknite &quot;Dodaj&quot; pokraj željene stavke</p>
                    </div>
                  ) : (
                    <div className="space-y-2 max-h-[40vh] overflow-y-auto">
                      {liveCalc.lines.map((line, i) => (
                        <div key={i} className="flex items-start justify-between gap-2 py-1.5 border-b border-slate-100 last:border-0">
                          <div className="min-w-0 flex-1">
                            <p className="text-xs text-slate-700 leading-snug truncate" title={line.opis}>
                              {line.opis}
                            </p>
                            <p className="text-[10px] text-slate-400 mt-0.5">
                              {line.kolicina} × {fmtNum(line.bodova_po_jedinici)} bod.
                            </p>
                          </div>
                          <span className="text-xs font-semibold text-slate-800 shrink-0">
                            {fmtEur(line.iznos_eur)}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Totals */}
                  {selectedItems.size > 0 && (
                    <div className="mt-4 pt-3 border-t border-slate-200 space-y-1.5">
                      <div className="flex justify-between text-xs text-slate-500">
                        <span>Ukupno bodova</span>
                        <span className="font-semibold">{fmtNum(liveCalc.ukupnoBodova)}</span>
                      </div>
                      <div className="flex justify-between text-xs text-slate-500">
                        <span>Ukupno bez PDV</span>
                        <span className="font-semibold">{fmtEur(liveCalc.ukupnoEur)}</span>
                      </div>
                      <div className="flex justify-between text-xs text-slate-500">
                        <span>PDV 25%</span>
                        <span className="font-semibold">{fmtEur(liveCalc.pdv)}</span>
                      </div>
                      <div className="flex justify-between text-sm font-bold text-indigo-700 pt-1">
                        <span>UKUPNO s PDV</span>
                        <span>{fmtEur(liveCalc.ukupnoSPdv)}</span>
                      </div>
                      <div className="text-[10px] text-slate-400 text-right">
                        1 bod = {fmtEur(BOD_VALUE)}
                      </div>
                    </div>
                  )}
                </div>

                {/* Action buttons */}
                {selectedItems.size > 0 && (
                  <div className="px-4 pb-4 space-y-2">
                    <CreditQuotaBadge quota={troskovnikQuota} loading={creditsLoading} className="mb-1" />
                    <button
                      onClick={handleCalculate}
                      disabled={calculating || troskovnikQuota.exhausted}
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
                        disabled={saving}
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
                        className="flex-1 flex items-center justify-center gap-1.5 text-xs text-slate-600 hover:text-slate-800 bg-slate-100 hover:bg-slate-200 py-2 rounded-lg transition"
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
                  {[
                    ["0 – 332 €", "25"],
                    ["332 – 664 €", "50"],
                    ["664 – 1.327 €", "75"],
                    ["1.327 – 13.272 €", "100"],
                    ["13.272 – 33.181 €", "250"],
                    ["33.181 – 66.361 €", "500"],
                    ["66.361 – 663.614 €", "500+"],
                    ["663.614 – 1.327.228 €", "4.991+"],
                    ["> 1.327.228 €", "max 10.000"],
                  ].map(([range, pts]) => (
                    <div key={range} className="flex justify-between text-slate-500">
                      <span>{range}</span>
                      <span className="font-mono font-semibold text-slate-700">{pts} bod.</span>
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
                    {new Date(result.datum_izrade).toLocaleDateString("hr-HR")} · Vrijednost spora: {fmtEur(vrijednostNum)} · Tarifa NN 2023
                  </p>
                </div>
              </div>
            </div>

            <div className="p-6">
              {/* Party info */}
              {(result.odvjetnik?.ime || result.klijent?.ime) && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                  {result.odvjetnik?.ime && (
                    <div className="bg-slate-50 rounded-xl p-4">
                      <div className="flex items-center gap-2 mb-2">
                        <Building2 className="h-4 w-4 text-slate-500" />
                        <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Odvjetnik</span>
                      </div>
                      <p className="text-sm font-medium text-slate-900">{result.odvjetnik.ime}</p>
                      {result.odvjetnik.oib && <p className="text-xs text-slate-500">OIB: {result.odvjetnik.oib}</p>}
                      {result.odvjetnik.adresa && <p className="text-xs text-slate-500">{result.odvjetnik.adresa}{result.odvjetnik.grad ? `, ${result.odvjetnik.grad}` : ""}</p>}
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
                </div>
              )}

              {result.opis_predmeta && (
                <p className="text-sm text-slate-600 mb-4">
                  <span className="font-semibold">Predmet:</span> {result.opis_predmeta}
                </p>
              )}

              {/* Line items table */}
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b-2 border-indigo-200">
                      <th className="text-left py-2 px-2 text-xs font-semibold text-slate-500 w-10">Rbr.</th>
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
                        <td className="py-2 px-2 text-slate-800">{line.opis}</td>
                        <td className="py-2 px-2 text-slate-500 font-mono text-xs">{line.tarifni_broj}</td>
                        <td className="py-2 px-2 text-center text-slate-600">{line.kolicina}</td>
                        <td className="py-2 px-2 text-right text-slate-600">{fmtNum(line.bodova_po_jedinici)}</td>
                        <td className="py-2 px-2 text-right font-semibold text-slate-700">{fmtNum(line.ukupno_bodova)}</td>
                        <td className="py-2 px-2 text-right font-semibold text-slate-800">{fmtEur(line.iznos_eur)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t-2 border-indigo-200">
                      <td colSpan={5} className="py-2 px-2 text-right text-xs text-slate-500 font-semibold">Ukupno bodova</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtNum(result.ukupno_bodova)}</td>
                      <td className="py-2 px-2 text-right font-bold text-indigo-700">{fmtEur(result.ukupno_eur_bez_pdv)}</td>
                    </tr>
                    <tr>
                      <td colSpan={5} className="py-1 px-2 text-right text-xs text-slate-500">PDV 25%</td>
                      <td />
                      <td className="py-1 px-2 text-right text-sm text-slate-600">{fmtEur(result.pdv)}</td>
                    </tr>
                    <tr className="bg-indigo-50">
                      <td colSpan={5} className="py-3 px-2 text-right text-sm font-bold text-indigo-800">UKUPNO s PDV</td>
                      <td />
                      <td className="py-3 px-2 text-right text-lg font-bold text-indigo-800">{fmtEur(result.ukupno_eur_s_pdv)}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>

              {/* Footer */}
              <div className="mt-4 pt-3 border-t border-slate-200 text-[10px] text-slate-400 leading-relaxed">
                <p>Izračun je napravljen prema Tarifi o nagradama i naknadi troškova za rad odvjetnika (NN 2023). Vrijednost boda: {fmtEur(BOD_VALUE)}.</p>
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
