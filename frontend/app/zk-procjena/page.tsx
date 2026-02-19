"use client";

import {
  useState,
  useCallback,
  useRef,
  type DragEvent,
  type ChangeEvent,
} from "react";
import Link from "next/link";
import {
  Upload,
  FileText,
  Loader2,
  AlertTriangle,
  ClipboardCopy,
  Check,
  ArrowLeft,
  Scale,
  FileCheck,
  ExternalLink,
  ShieldCheck,
  ShieldAlert,
  BookOpen,
  ChevronDown,
  ChevronUp,
  AlertOctagon,
  CircleCheck,
  CircleAlert,
  Info,
  Landmark,
  Users,
  Gavel,
  FileWarning,
  Lock,
  History,
  ShieldX,
  BarChart3,
} from "lucide-react";
import { uploadPdfToStorage } from "@/lib/supabase";
import LocationSelector from "@/components/LocationSelector";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import ResultActions from "@/components/ResultActions";

import { API_BASE } from "@/lib/api";

// --- Types ---

interface OwnerInfo {
  name: string;
  share: string;
  oib: string;
  basis_of_acquisition: string;
}

interface MortgageInfo {
  amount: string;
  currency: string;
  year: string;
  details: string;
}

interface EncumbranceInfo {
  type: string;
  description: string;
  beneficiary: string;
  is_active: boolean;
  // V44: OCR 2.0
  risk_type?: "FIDUCIJA" | "OSOBNA_SLUZNOST" | "STVARNA_SLUZNOST" | "ZABRANA_OTUDENJA" | "PRAVO_GRADENJA" | "KONCESIJA" | "OVRHA" | "PRAVO_PRVOKUPA" | "OSTALO";
  legal_basis?: string;
  blocks_sale?: boolean;
  blocks_construction?: boolean;
  year_registered?: number;
  is_historical?: boolean;
}

interface ParcelInfo {
  parcel_number: string;
  land_use: string;
  area_m2: number;
  description: string;
}

interface PlombaInfo {
  z_number: string;
  description: string;
  date: string;
  // V44: OCR 2.0
  plomba_type?: "VLASNISTVO" | "HIPOTEKA" | "OVRHA" | "BRISANJE" | "ZABILJEŽBA" | "OSTALO";
  risk_level?: "CRITICAL" | "WARNING" | "INFO";
  blocks_sale?: boolean;
}

// V44: Historical encumbrance (OCR 2.0)
interface HistoricalEncumbrance {
  type: string;
  original_text: string;
  interpretation: string;
  estimated_year: number;
  still_legally_relevant: boolean;
  blocks_sale: string; // "DA" | "NE" | "MOGUĆE"
  blocks_construction: string;
  resolution: string;
  legal_basis: string;
}

// V44: Encumbrance risk matrix summary
interface EncumbranceRiskSummary {
  total_active: number;
  blocking_sale: number;
  blocking_construction: number;
  fiduciary_count: number;
  personal_servitude_count: number;
  historical_count: number;
  enforcement_count: number;
  overall_risk: "ČISTO" | "UVJETNO" | "BLOKIRANO";
  summary: string;
}

interface DueDiligenceFinding {
  category: "PLOMBA" | "VLASNISTVO" | "HIPOTEKA" | "SLUZNOST" | "OVRHA" | "LEGALIZACIJA" | "KATASTAR" | "FIDUCIJA" | "ZABRANA" | "OSTALO";
  severity: "CRITICAL" | "WARNING" | "INFO";
  title: string;
  description: string;
  advice: string;
}

// V40: Katastar vs ZK Usklađenost
interface KatastarParcelDiscrepancy {
  type: "AREA_MISMATCH" | "PARCEL_MISSING" | "LAND_USE_MISMATCH" | "PARCEL_NUMBER_MISMATCH";
  zk_parcel_number: string;
  katastar_parcel_number: string;
  zk_area_m2: number;
  katastar_area_m2: number;
  zk_land_use: string;
  katastar_land_use: string;
  description: string;
  severity: "HIGH" | "MEDIUM" | "LOW";
}

interface KatastarComparison {
  status: "USKLAĐENO" | "NESKLAD" | "DJELOMIČNO";
  area_difference_m2: number;
  area_difference_percent: number;
  katastar_area_m2: number;
  zk_area_m2: number;
  parcel_discrepancies: KatastarParcelDiscrepancy[];
  summary: string;
  discrepancy_count: number;
  survey_recommended: boolean;
}

interface ZkParsedResult {
  property_id: string;
  cadastral_municipality: string;
  parcel_number: string;
  zk_number: string;
  property_type: string;
  property_description: string;
  land_area_m2: number;
  building_area_m2: number;
  land_use: string;
  etazni_udio: string;
  parcels: ParcelInfo[];
  owners: OwnerInfo[];
  active_mortgages: MortgageInfo[];
  active_disputes: string[];
  other_encumbrances: EncumbranceInfo[];
  risk_score: "LOW" | "MEDIUM" | "HIGH";
  lawyer_comment: string;
  location: string;
  address: string;
  condition: string;
  property_state: string;
  yard_area_m2: number;
  // V31: Due Diligence fields
  active_plombe?: PlombaInfo[];
  findings?: DueDiligenceFinding[];
  overall_status?: "ČISTO" | "UVJETNO" | "RIZIČNO";
  ownership_status?: string;
  shares_sum_correct?: boolean;
  shares_sum_display?: string;
  land_use_mismatch?: string;
  has_usage_permit_issue?: boolean;
  due_diligence_summary?: string;
  // V40: Katastar vs ZK
  katastar_uskladjenost?: KatastarComparison;
  // V44: OCR 2.0
  historical_encumbrances?: HistoricalEncumbrance[];
  encumbrance_risk_matrix?: EncumbranceRiskSummary;
}

interface SelectedComparable {
  title: string;
  price: string;
  location: string;
  url: string;
  reason_for_selection: string;
}

interface ValueDriver {
  factor: string;
  impact: string;
  adjustment_percent: number;
}

interface UsedComparableV4 {
  title: string;
  url: string;
  price_per_m2: number;
  similarity_score: number;
  is_outlier: boolean;
  outlier_reason?: string;
  // BrightData-enriched fields
  total_price?: number;
  area_m2?: number;
  rooms?: number;
  floor?: string;
  year_built?: number;
  condition?: string;
  energy_class?: string;
  heating?: string;
  location?: string;
  has_garage?: boolean;
  has_parking?: boolean;
  // V18: Yard area + similarity reason
  yard_area_m2?: number;
  similarity_reason?: string;
  // V20: Deep dive status
  deep_dive_status?: string;
  // V20: Per-comparable correction reason
  correction_percent?: number;
  correction_reason?: string;
}

interface AdjustmentV4 {
  factor: string;
  description: string;
  percent: number;
}

interface FullValuationResult {
  target_property_summary: string;
  selected_comparables: SelectedComparable[];
  estimated_value_eur: number;
  value_drivers: ValueDriver[];
  valuation_reasoning: string;
  confidence_level?: string;
  market_average_asking?: number;
  calculated_price_per_m2?: number;
  realization_discount_percent?: number;
  total_listings_found?: number;
  usable_price_points?: number;
  outliers_removed?: number;
  price_source?: string;
  region_name?: string;
  note?: string;
  confidence_score?: number;
  confidence_explanation?: string;
  confidence_color?: string;
  std_dev_per_m2?: number;
  coefficient_of_variation?: number;
  outlier_method?: string;
  outlier_log?: string[];
  total_adjustment_percent?: number;
  realization_gap_reasoning?: string;
  used_comparables?: UsedComparableV4[];
  adjustments?: AdjustmentV4[];
  pipeline?: string[];
  math_confidence_score?: number;
  math_confidence_level?: string;
  math_confidence_explanation?: string;
}

interface UnifiedResult {
  zk_analysis: ZkParsedResult;
  valuation?: FullValuationResult;
  valuation_attempted: boolean;
  valuation_skip_reason?: string;
}

interface StreamStage {
  id: string;
  label: string;
  status: "pending" | "active" | "done" | "skipped" | "error";
  detail?: string;
  error?: string;
}

// --- Constants ---

const INITIAL_STAGES: StreamStage[] = [
  { id: "zk_parsing", label: "Parsiram ZK izvadak (A/B/C listovi)...", status: "pending" },
  { id: "reading", label: "Pripremam podatke za procjenu...", status: "pending" },
  { id: "searching", label: "Pretražujem Njuškalo i tržište nekretnina...", status: "pending" },
  { id: "ai_features", label: "AI analizira značajke oglasa...", status: "pending" },
  { id: "engine", label: "Engine izračunava tržišnu vrijednost...", status: "pending" },
  { id: "ai_opinion", label: "AI generira stručno mišljenje...", status: "pending" },
  { id: "saving", label: "Spremam rezultate...", status: "pending" },
];

const ZK_STAGE_ICONS: Record<string, React.ReactNode> = {
  zk_parsing: <Landmark className="h-4 w-4" />,
  reading: <FileText className="h-4 w-4" />,
  searching: <Scale className="h-4 w-4" />,
  ai_features: <ShieldCheck className="h-4 w-4" />,
  engine: <BarChart3 className="h-4 w-4" />,
  ai_opinion: <FileCheck className="h-4 w-4" />,
  saving: <Check className="h-4 w-4" />,
};
const ZK_STAGE_MICROCOPY: Record<string, string[]> = {
  zk_parsing: ["Parsiram A list (posjedovnica)...", "Parsiram B list (vlasništvo)...", "Parsiram C list (tereti)..."],
  reading: ["Pripremam podatke o nekretnini...", "Identificiram tip i lokaciju..."],
  searching: ["Pretražujem Njuškalo...", "Pretražujem Index Oglasi...", "Tražim usporedive nekretnine..."],
  ai_features: ["Analiziram detalje oglasa...", "Procjenjujem stanje nekretnina..."],
  engine: ["Izračunavam medijanu cijena...", "Robust estimator u tijeku...", "Primjenjujem korekcije..."],
  ai_opinion: ["AI formulira stručno mišljenje...", "Obrazlažem procjenu..."],
  saving: ["Spremam u bazu...", "Generiranje izvještaja..."],
};

// --- Component ---

export default function ZkProcjenaPage() {
  const [mode, setMode] = useState<"pdf" | "text">("pdf");
  const [file, setFile] = useState<File | null>(null);
  const [textInput, setTextInput] = useState("");
  const [dragOver, setDragOver] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const [stages, setStages] = useState<StreamStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [zkAnalysis, setZkAnalysis] = useState<ZkParsedResult | null>(null);
  const [unifiedResult, setUnifiedResult] = useState<UnifiedResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  // V58: viewState for full-screen processing
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");

  // Structured location override
  const [city, setCity] = useState("");
  const [neighborhood, setNeighborhood] = useState("");
  const [email, setEmail] = useState("");

  // UI toggles
  const [showZkDetails, setShowZkDetails] = useState(true);
  const [showValuation, setShowValuation] = useState(true);

  const isLoading = isStreaming;
  const hasResult = !!unifiedResult;
  const valuation = unifiedResult?.valuation;

  // --- File Handling ---

  const handleFile = (f: File | null) => {
    if (f && f.type === "application/pdf") {
      setFile(f);
      setError(null);
      setZkAnalysis(null);
      setUnifiedResult(null);
    } else if (f) {
      setError("Molimo uploadajte PDF datoteku.");
    }
  };

  const onDrop = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragOver(false);
    handleFile(e.dataTransfer.files?.[0] ?? null);
  }, []);

  const onDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragOver(true);
  }, []);

  const onDragLeave = useCallback(() => setDragOver(false), []);

  const onFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    handleFile(e.target.files?.[0] ?? null);
  };

  // --- Run Unified ZK → Valuation ---

  const runUnified = async () => {
    if (mode === "pdf" && !file) return;
    if (mode === "text" && (!textInput.trim() || textInput.trim().length < 50)) return;

    setError(null);
    setZkAnalysis(null);
    setUnifiedResult(null);
    setStages(INITIAL_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);
    setViewState("PROCESSING");

    // Upload PDF to Supabase Storage (non-blocking, only for PDF mode)
    const pdfUrlPromise = mode === "pdf" && file ? uploadPdfToStorage(file) : Promise.resolve(null);

    const state = { result: null as UnifiedResult | null };
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      let url: string;
      let body: FormData;

      if (mode === "text") {
        url = `${API_BASE}/api/zk/stream-text`;
        body = new FormData();
        body.append("text", textInput.trim());
      } else {
        url = `${API_BASE}/api/zk/stream`;
        body = new FormData();
        body.append("file", file!);
      }

      if (city.trim()) body.append("city", city.trim());
      if (neighborhood.trim()) body.append("neighborhood", neighborhood.trim());
      if (email.trim()) body.append("email", email.trim());

      const response = await fetch(url, { method: "POST", body: body, signal: controller.signal });

      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      const processEvent = (line: string) => {
        if (!line.startsWith("data: ")) return;
        try {
          const evt = JSON.parse(line.slice(6));

          if (evt.type === "progress") {
            const { stage, status, label, detail } = evt.data;
            setStages((prev) =>
              prev.map((s) =>
                s.id === stage
                  ? { ...s, status, label: label || s.label, detail: detail ?? s.detail }
                  : s
              )
            );
          } else if (evt.type === "error") {
            const { stage, message } = evt.data;
            setStages((prev) =>
              prev.map((s) =>
                s.id === stage
                  ? { ...s, status: "error", error: message }
                  : s
              )
            );
            setError(message);
          } else if (evt.type === "zk_analysis") {
            setZkAnalysis(evt.data);
          } else if (evt.type === "complete") {
            state.result = evt.data;
            setUnifiedResult(evt.data);
            if (evt.data.zk_analysis) setZkAnalysis(evt.data.zk_analysis);
          }
        } catch {
          // skip malformed SSE lines
        }
      };

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split("\n\n");
        buffer = parts.pop()!;
        for (const part of parts) processEvent(part.trim());
      }
      // Flush remaining buffer (last SSE event might lack trailing \n\n)
      if (buffer.trim()) processEvent(buffer.trim());

      setIsStreaming(false);
      if (state.result) setViewState("RESULT");

      // PDF upload result (non-critical)
      if (state.result?.valuation) {
        await pdfUrlPromise;
      }
    } catch (e: unknown) {
      setIsStreaming(false);
      if (e instanceof DOMException && e.name === "AbortError") {
        setError("Analiza je otkazana.");
        setViewState("INPUT");
      } else {
        setError(
          "Server ne odgovara. Moguće da se Render restartira (~30s). Pokušajte ponovno za minutu."
        );
      }
    } finally {
      abortRef.current = null;
    }
  };

  const cancelAnalysis = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
  };

  // --- Copy ---

  const copyToClipboard = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      const ta = document.createElement("textarea");
      ta.value = text;
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      document.execCommand("copy");
      document.body.removeChild(ta);
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // --- Reset ---

  const reset = () => {
    abortRef.current?.abort();
    setFile(null);
    setTextInput("");
    setMode("pdf");
    setCity("");
    setNeighborhood("");
    setEmail("");
    setZkAnalysis(null);
    setUnifiedResult(null);
    setError(null);
    setStages([]);
    setIsStreaming(false);
    setCopied(false);
    setShowZkDetails(true);
    setShowValuation(true);
    setViewState("INPUT");
  };

  // --- Helpers ---

  const StageIcon = ({ status }: { status: string }) => {
    switch (status) {
      case "active": return <Loader2 className="h-5 w-5 animate-spin text-blue-600" />;
      case "done": return <FileCheck className="h-5 w-5 text-green-600" />;
      case "error": return <AlertTriangle className="h-5 w-5 text-red-600" />;
      case "skipped": return <FileCheck className="h-5 w-5 text-gray-400" />;
      default: return <div className="h-5 w-5 rounded-full border-2 border-gray-300" />;
    }
  };

  const formatPrice = (n: number) => n.toLocaleString("hr-HR");

  // --- Render ---

  return (
    <>
      {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
      {viewState === "PROCESSING" && (
        <SharedProcessingView
          stages={stages as ProcessingStage[]}
          isStreaming={isStreaming}
          error={error}
          onCancel={() => { cancelAnalysis(); setViewState("INPUT"); }}
          onReset={reset}
          title="ZK analiza i procjena u tijeku"
          subtitle="AI parsira izvadak, provodi due diligence i procjenjuje vrijednost"
          headerIcon={<BookOpen className="h-7 w-7 text-white" />}
          stageIcons={ZK_STAGE_ICONS}
          stageMicrocopy={ZK_STAGE_MICROCOPY}
          theme="indigo"
        />
      )}

    <div className={`min-h-screen bg-gray-50 py-12 px-4 font-sans ${viewState === "PROCESSING" ? "hidden" : ""}`}>
      <div className="mx-auto max-w-4xl">
        <Link
          href="/"
          className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6"
        >
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        {/* Header */}
        <div className="text-center mb-10">
          <h1 className="text-3xl font-bold text-gray-900">
            <BookOpen className="inline-block h-8 w-8 mr-2 text-indigo-600" />
            ZK Due Diligence → Procjena vrijednosti
          </h1>
          <p className="mt-2 text-gray-500">
            Uploadajte ZK izvadak — sustav provodi dubinsku pravnu analizu, detektira rizike i automatski procjenjuje tržišnu vrijednost
          </p>
        </div>

        {/* Error */}
        {error && (
          <div className="mb-6 flex items-center gap-2 rounded-lg border border-red-300 bg-red-50 p-4 text-red-700">
            <AlertTriangle className="h-5 w-5 flex-shrink-0" />
            <span className="text-sm">{error}</span>
          </div>
        )}

        {/* ===================== UPLOAD ===================== */}
        {!isLoading && !hasResult && (
          <div className="space-y-6">
            {/* Mode Toggle */}
            <div className="flex justify-center gap-2">
              <button
                onClick={() => setMode("pdf")}
                className={`px-5 py-2.5 rounded-lg text-sm font-medium transition ${
                  mode === "pdf"
                    ? "bg-indigo-600 text-white shadow-sm"
                    : "bg-white text-gray-600 border border-gray-300 hover:bg-gray-50"
                }`}
              >
                <Upload className="inline h-4 w-4 mr-1.5 -mt-0.5" />
                PDF Upload
              </button>
              <button
                onClick={() => setMode("text")}
                className={`px-5 py-2.5 rounded-lg text-sm font-medium transition ${
                  mode === "text"
                    ? "bg-indigo-600 text-white shadow-sm"
                    : "bg-white text-gray-600 border border-gray-300 hover:bg-gray-50"
                }`}
              >
                <FileText className="inline h-4 w-4 mr-1.5 -mt-0.5" />
                Tekst
              </button>
            </div>

            {/* Location Override — using shared LocationSelector combobox */}
            <LocationSelector
              city={city}
              district={neighborhood}
              onCityChange={(c) => { setCity(c); setNeighborhood(""); }}
              onDistrictChange={(d) => setNeighborhood(d)}
            />

            {/* Email */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                ✉️ Email obavijest
                <span className="text-xs font-normal text-gray-400">(opcionalno)</span>
              </h4>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="npr. ime@gmail.com"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              />
            </div>

            {/* PDF Drop Zone */}
            {mode === "pdf" && (
              <div
                onDrop={onDrop}
                onDragOver={onDragOver}
                onDragLeave={onDragLeave}
                className={`flex flex-col items-center justify-center rounded-xl border-2 border-dashed p-12 transition-colors cursor-pointer ${
                  dragOver
                    ? "border-indigo-500 bg-indigo-50"
                    : "border-gray-300 bg-white hover:border-gray-400"
                }`}
              >
                <Upload className="h-12 w-12 text-gray-400 mb-4" />
                <p className="text-gray-600 font-medium text-lg">
                  Povucite ZK izvadak (PDF) ovdje
                </p>
                <p className="text-gray-400 mt-1">
                  ili{" "}
                  <label className="text-indigo-600 underline cursor-pointer">
                    odaberite datoteku
                    <input
                      type="file"
                      accept=".pdf"
                      className="hidden"
                      onChange={onFileChange}
                    />
                  </label>
                </p>
                <p className="text-xs text-gray-400 mt-3">
                  Max 10 MB • Sustav automatski parsira ZK, analizira rizik i procjenjuje vrijednost
                </p>
              </div>
            )}

            {/* Text Input */}
            {mode === "text" && (
              <textarea
                value={textInput}
                onChange={(e) => setTextInput(e.target.value)}
                placeholder="Zalijepite tekst ZK izvatka ovdje... (A list, B list, C list)"
                className="w-full h-72 rounded-xl border border-gray-300 p-4 text-sm text-gray-800 resize-y focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none bg-white"
              />
            )}

            {/* PDF file selected — analyze button */}
            {mode === "pdf" && file && (
              <div className="flex items-center justify-between rounded-lg bg-white border border-gray-200 p-4">
                <div className="flex items-center gap-3">
                  <FileText className="h-5 w-5 text-indigo-600" />
                  <span className="text-sm font-medium text-gray-700 truncate max-w-xs">
                    {file.name}
                  </span>
                  <span className="text-xs text-gray-400">
                    ({(file.size / 1024).toFixed(0)} KB)
                  </span>
                </div>
                <button
                  onClick={runUnified}
                  className="rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 transition flex items-center gap-2"
                >
                  <BookOpen className="h-4 w-4" />
                  Analiziraj i procijeni
                </button>
              </div>
            )}

            {/* Text mode — analyze button */}
            {mode === "text" && textInput.trim().length >= 50 && (
              <div className="flex justify-end">
                <button
                  onClick={runUnified}
                  className="rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 transition flex items-center gap-2"
                >
                  <BookOpen className="h-4 w-4" />
                  Analiziraj i procijeni
                </button>
              </div>
            )}
          </div>
        )}

        {/* ===================== ZK ANALYSIS RESULTS ===================== */}
        {zkAnalysis && (
          <div className="mb-8 space-y-4">
            {/* Actions bar */}
            <div className="flex items-center justify-between flex-wrap gap-2">
              <button
                onClick={reset}
                className="text-sm text-indigo-600 hover:underline flex items-center gap-1"
              >
                <ArrowLeft className="h-4 w-4" /> Nova analiza
              </button>
              <ResultActions
                copyText={[
                  `ZK DUE DILIGENCE IZVJEŠTAJ`,
                  `Nekretnina: ${zkAnalysis.property_id} (${zkAnalysis.property_type})`,
                  `Status: ${zkAnalysis.overall_status} | Rizik: ${zkAnalysis.risk_score}`,
                  "",
                  `DUE DILIGENCE SAŽETAK:`,
                  zkAnalysis.due_diligence_summary,
                  "",
                  ...(zkAnalysis.findings?.length ? [
                    `NALAZI (${zkAnalysis.findings.length}):`,
                    ...zkAnalysis.findings.map((f) => `• [${f.severity}] ${f.category}: ${f.description}`),
                    "",
                  ] : []),
                  ...(valuation ? [
                    `PROCJENA VRIJEDNOSTI: ${valuation.estimated_value_eur.toLocaleString("hr-HR")} €`,
                    valuation.valuation_reasoning,
                  ] : []),
                ].join("\n")}
              />
            </div>

            <div className="flex items-center justify-between">
              <h2 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                <Landmark className="h-6 w-6 text-indigo-600" />
                Izvještaj o pravnom statusu nekretnine
              </h2>
              <button
                onClick={() => setShowZkDetails(!showZkDetails)}
                className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1"
              >
                {showZkDetails ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                {showZkDetails ? "Sakrij detalje" : "Prikaži detalje"}
              </button>
            </div>

            {/* ─── OVERALL STATUS TRAFFIC LIGHT ─── */}
            <div className={`rounded-xl border-2 p-6 ${
              zkAnalysis.overall_status === "RIZIČNO" ? "border-red-500 bg-red-50" :
              zkAnalysis.overall_status === "UVJETNO" ? "border-yellow-500 bg-yellow-50" :
              "border-green-500 bg-green-50"
            }`}>
              <div className="flex items-center gap-4">
                <div className={`flex items-center justify-center h-16 w-16 rounded-full text-3xl ${
                  zkAnalysis.overall_status === "RIZIČNO" ? "bg-red-200" :
                  zkAnalysis.overall_status === "UVJETNO" ? "bg-yellow-200" :
                  "bg-green-200"
                }`}>
                  {zkAnalysis.overall_status === "RIZIČNO" ? "🔴" : 
                   zkAnalysis.overall_status === "UVJETNO" ? "🟡" : "🟢"}
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-3">
                    <h3 className={`text-2xl font-bold ${
                      zkAnalysis.overall_status === "RIZIČNO" ? "text-red-800" :
                      zkAnalysis.overall_status === "UVJETNO" ? "text-yellow-800" :
                      "text-green-800"
                    }`}>
                      {zkAnalysis.overall_status || zkAnalysis.risk_score}
                    </h3>
                    <span className={`text-xs font-semibold uppercase px-3 py-1 rounded-full ${
                      zkAnalysis.risk_score === "HIGH" ? "bg-red-200 text-red-800" :
                      zkAnalysis.risk_score === "MEDIUM" ? "bg-yellow-200 text-yellow-800" :
                      "bg-green-200 text-green-800"
                    }`}>
                      Rizik: {zkAnalysis.risk_score}
                    </span>
                  </div>
                  <p className={`mt-1 text-sm ${
                    zkAnalysis.overall_status === "RIZIČNO" ? "text-red-700" :
                    zkAnalysis.overall_status === "UVJETNO" ? "text-yellow-700" :
                    "text-green-700"
                  }`}>
                    {zkAnalysis.property_id} • {zkAnalysis.property_type}
                  </p>
                </div>
              </div>
            </div>

            {/* ─── PLOMBA ALERT (if any) ─── */}
            {zkAnalysis.active_plombe && zkAnalysis.active_plombe.length > 0 && (
              <div className="rounded-xl border-2 border-red-600 bg-red-100 p-5 animate-pulse">
                <div className="flex items-center gap-3 mb-3">
                  <AlertOctagon className="h-6 w-6 text-red-700" />
                  <h3 className="text-lg font-bold text-red-800">
                    🚨 AKTIVNE PLOMBE DETEKTIRANE!
                  </h3>
                </div>
                <div className="space-y-2">
                  {zkAnalysis.active_plombe.map((p, i) => (
                    <div key={i} className="flex items-start gap-3 p-3 rounded-lg bg-red-200/60">
                      <span className="font-mono font-bold text-red-900 whitespace-nowrap">{p.z_number}</span>
                      <div className="flex-1">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          {p.plomba_type && (
                            <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded ${
                              p.plomba_type === "OVRHA" ? "bg-red-300 text-red-900" :
                              p.plomba_type === "HIPOTEKA" ? "bg-orange-200 text-orange-800" :
                              p.plomba_type === "BRISANJE" ? "bg-green-200 text-green-800" :
                              "bg-red-200 text-red-800"
                            }`}>
                              {p.plomba_type}
                            </span>
                          )}
                          {p.risk_level && (
                            <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${
                              p.risk_level === "CRITICAL" ? "bg-red-900 text-white" :
                              p.risk_level === "WARNING" ? "bg-yellow-200 text-yellow-800" :
                              "bg-blue-200 text-blue-800"
                            }`}>
                              {p.risk_level === "CRITICAL" ? "KRITIČNO" : p.risk_level === "WARNING" ? "UPOZORENJE" : "INFO"}
                            </span>
                          )}
                          {p.blocks_sale && (
                            <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-red-900 text-white flex items-center gap-1">
                              <Lock className="h-3 w-3" /> BLOKIRA PRODAJU
                            </span>
                          )}
                        </div>
                        <p className="text-sm text-red-800">{p.description}</p>
                        {p.date && <p className="text-xs text-red-600 mt-1">Datum: {p.date}</p>}
                      </div>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-red-700 mt-3 font-medium">
                  ⚠️ Plomba znači da je postupak u tijeku. Netko je predao zahtjev za upis prije vas!
                </p>
              </div>
            )}

            {/* ─── DUE DILIGENCE QUICK CHECKS ─── */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {/* Ownership Check */}
              <div className={`rounded-xl border p-5 ${
                zkAnalysis.ownership_status === "1/1 ČISTO" ? "border-green-200 bg-green-50" :
                zkAnalysis.ownership_status === "NERAZJAŠNJENO" ? "border-red-200 bg-red-50" :
                "border-yellow-200 bg-yellow-50"
              }`}>
                <div className="flex items-center gap-2 mb-2">
                  <Users className={`h-5 w-5 ${
                    zkAnalysis.ownership_status === "1/1 ČISTO" ? "text-green-600" :
                    zkAnalysis.ownership_status === "NERAZJAŠNJENO" ? "text-red-600" :
                    "text-yellow-600"
                  }`} />
                  <h4 className="text-sm font-semibold text-gray-700">Vlasništvo</h4>
                </div>
                <p className={`text-lg font-bold ${
                  zkAnalysis.ownership_status === "1/1 ČISTO" ? "text-green-700" :
                  zkAnalysis.ownership_status === "NERAZJAŠNJENO" ? "text-red-700" :
                  "text-yellow-700"
                }`}>
                  {zkAnalysis.ownership_status || "—"}
                </p>
                {zkAnalysis.shares_sum_display && (
                  <p className="text-xs text-gray-600 mt-1 font-mono">
                    {zkAnalysis.shares_sum_display}
                    {zkAnalysis.shares_sum_correct === true && <span className="ml-1 text-green-600">✓</span>}
                    {zkAnalysis.shares_sum_correct === false && <span className="ml-1 text-red-600">✗</span>}
                  </p>
                )}
              </div>

              {/* Land Use / Legality Check */}
              <div className={`rounded-xl border p-5 ${
                zkAnalysis.land_use_mismatch ? "border-yellow-200 bg-yellow-50" :
                zkAnalysis.has_usage_permit_issue ? "border-red-200 bg-red-50" :
                "border-green-200 bg-green-50"
              }`}>
                <div className="flex items-center gap-2 mb-2">
                  <FileWarning className={`h-5 w-5 ${
                    zkAnalysis.land_use_mismatch || zkAnalysis.has_usage_permit_issue ? "text-yellow-600" : "text-green-600"
                  }`} />
                  <h4 className="text-sm font-semibold text-gray-700">Legalnost</h4>
                </div>
                {zkAnalysis.land_use_mismatch ? (
                  <p className="text-sm text-yellow-700">{zkAnalysis.land_use_mismatch}</p>
                ) : zkAnalysis.has_usage_permit_issue ? (
                  <p className="text-sm text-red-700">Potrebna uporabna dozvola</p>
                ) : (
                  <p className="text-lg font-bold text-green-700">Bez primjedbi</p>
                )}
                <p className="text-xs text-gray-500 mt-1">Kultura: {zkAnalysis.land_use || "—"}</p>
              </div>

              {/* Encumbrances Summary */}
              <div className={`rounded-xl border p-5 ${
                zkAnalysis.active_disputes.length > 0 ? "border-red-200 bg-red-50" :
                zkAnalysis.active_mortgages.length > 0 ? "border-yellow-200 bg-yellow-50" :
                "border-green-200 bg-green-50"
              }`}>
                <div className="flex items-center gap-2 mb-2">
                  <Gavel className={`h-5 w-5 ${
                    zkAnalysis.active_disputes.length > 0 ? "text-red-600" :
                    zkAnalysis.active_mortgages.length > 0 ? "text-yellow-600" :
                    "text-green-600"
                  }`} />
                  <h4 className="text-sm font-semibold text-gray-700">Tereti (C list)</h4>
                </div>
                <div className="space-y-1">
                  <p className="text-sm">
                    <span className="font-semibold">{zkAnalysis.active_mortgages.length}</span>
                    <span className="text-gray-600"> hipoteka</span>
                  </p>
                  <p className="text-sm">
                    <span className="font-semibold">{zkAnalysis.active_disputes.length}</span>
                    <span className="text-gray-600"> sporova/ovrha</span>
                  </p>
                  <p className="text-sm">
                    <span className="font-semibold">{zkAnalysis.other_encumbrances?.filter(e => e.is_active).length || 0}</span>
                    <span className="text-gray-600"> ostalih tereta</span>
                  </p>
                </div>
              </div>
            </div>

            {/* ─── V40: KATASTAR vs ZK USKLAĐENOST ─── */}
            {zkAnalysis.katastar_uskladjenost && (
              <div className={`rounded-xl border-2 p-6 ${
                zkAnalysis.katastar_uskladjenost.status === "NESKLAD" ? "border-orange-500 bg-orange-50" :
                zkAnalysis.katastar_uskladjenost.status === "DJELOMIČNO" ? "border-yellow-500 bg-yellow-50" :
                "border-green-500 bg-green-50"
              }`}>
                <div className="flex items-center gap-3 mb-4">
                  <div className={`flex items-center justify-center h-10 w-10 rounded-full text-xl ${
                    zkAnalysis.katastar_uskladjenost.status === "NESKLAD" ? "bg-orange-200" :
                    zkAnalysis.katastar_uskladjenost.status === "DJELOMIČNO" ? "bg-yellow-200" :
                    "bg-green-200"
                  }`}>
                    {zkAnalysis.katastar_uskladjenost.status === "NESKLAD" ? "⚠️" :
                     zkAnalysis.katastar_uskladjenost.status === "DJELOMIČNO" ? "🔶" : "✅"}
                  </div>
                  <div>
                    <h3 className={`text-lg font-bold ${
                      zkAnalysis.katastar_uskladjenost.status === "NESKLAD" ? "text-orange-800" :
                      zkAnalysis.katastar_uskladjenost.status === "DJELOMIČNO" ? "text-yellow-800" :
                      "text-green-800"
                    }`}>
                      📍 Usklađenost evidencija
                    </h3>
                    <p className="text-xs text-gray-600">Katastar ↔ Zemljišna knjiga</p>
                  </div>
                  <span className={`ml-auto text-xs font-bold uppercase px-3 py-1 rounded-full ${
                    zkAnalysis.katastar_uskladjenost.status === "NESKLAD" ? "bg-orange-200 text-orange-800" :
                    zkAnalysis.katastar_uskladjenost.status === "DJELOMIČNO" ? "bg-yellow-200 text-yellow-800" :
                    "bg-green-200 text-green-800"
                  }`}>
                    {zkAnalysis.katastar_uskladjenost.status}
                  </span>
                </div>

                {/* Area comparison bar */}
                {(zkAnalysis.katastar_uskladjenost.zk_area_m2 > 0 || zkAnalysis.katastar_uskladjenost.katastar_area_m2 > 0) && (
                  <div className="grid grid-cols-2 gap-4 mb-4">
                    <div className="rounded-lg bg-white/70 p-3 text-center">
                      <p className="text-xs font-semibold text-gray-500 uppercase">ZK površina</p>
                      <p className="text-xl font-bold text-gray-900">
                        {zkAnalysis.katastar_uskladjenost.zk_area_m2.toLocaleString("hr-HR")} m²
                      </p>
                    </div>
                    <div className="rounded-lg bg-white/70 p-3 text-center">
                      <p className="text-xs font-semibold text-gray-500 uppercase">Katastar površina</p>
                      <p className="text-xl font-bold text-gray-900">
                        {zkAnalysis.katastar_uskladjenost.katastar_area_m2.toLocaleString("hr-HR")} m²
                      </p>
                    </div>
                  </div>
                )}

                {/* Difference highlight */}
                {zkAnalysis.katastar_uskladjenost.area_difference_m2 !== 0 && (
                  <div className={`rounded-lg p-3 mb-4 text-center ${
                    Math.abs(zkAnalysis.katastar_uskladjenost.area_difference_percent) > 10 ? "bg-red-100" :
                    Math.abs(zkAnalysis.katastar_uskladjenost.area_difference_percent) > 5 ? "bg-orange-100" :
                    "bg-yellow-100"
                  }`}>
                    <p className="text-sm font-semibold">
                      Razlika: {zkAnalysis.katastar_uskladjenost.area_difference_m2 > 0 ? "+" : ""}
                      {zkAnalysis.katastar_uskladjenost.area_difference_m2.toLocaleString("hr-HR")} m²
                      <span className="text-gray-600 ml-2">
                        ({zkAnalysis.katastar_uskladjenost.area_difference_percent > 0 ? "+" : ""}
                        {zkAnalysis.katastar_uskladjenost.area_difference_percent.toFixed(1)}%)
                      </span>
                    </p>
                  </div>
                )}

                {/* Individual parcel discrepancies */}
                {zkAnalysis.katastar_uskladjenost.parcel_discrepancies?.length > 0 && (
                  <div className="space-y-2 mb-4">
                    {zkAnalysis.katastar_uskladjenost.parcel_discrepancies.map((d, i) => (
                      <div key={i} className={`p-3 rounded-lg border-l-4 bg-white/60 ${
                        d.severity === "HIGH" ? "border-red-500" :
                        d.severity === "MEDIUM" ? "border-orange-400" :
                        "border-yellow-400"
                      }`}>
                        <div className="flex items-center gap-2 mb-1">
                          <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded ${
                            d.type === "AREA_MISMATCH" ? "bg-orange-100 text-orange-700" :
                            d.type === "PARCEL_NUMBER_MISMATCH" ? "bg-red-100 text-red-700" :
                            d.type === "LAND_USE_MISMATCH" ? "bg-yellow-100 text-yellow-700" :
                            "bg-gray-100 text-gray-700"
                          }`}>
                            {d.type === "AREA_MISMATCH" ? "Površina" :
                             d.type === "PARCEL_NUMBER_MISMATCH" ? "Čestica" :
                             d.type === "LAND_USE_MISMATCH" ? "Kultura" :
                             d.type === "PARCEL_MISSING" ? "Nedostaje" : d.type}
                          </span>
                          {d.zk_parcel_number && (
                            <span className="text-xs text-gray-500 font-mono">
                              ZK č. {d.zk_parcel_number}
                              {d.katastar_parcel_number && d.katastar_parcel_number !== d.zk_parcel_number && 
                                ` → kat.č. ${d.katastar_parcel_number}`}
                            </span>
                          )}
                        </div>
                        <p className="text-sm text-gray-700">{d.description}</p>
                      </div>
                    ))}
                  </div>
                )}

                {/* Summary */}
                <p className="text-sm text-gray-700 leading-relaxed">
                  {zkAnalysis.katastar_uskladjenost.summary}
                </p>

                {/* Survey recommendation */}
                {zkAnalysis.katastar_uskladjenost.survey_recommended && (
                  <div className="mt-3 flex items-start gap-2 p-3 rounded-lg bg-orange-100 border border-orange-300">
                    <span className="text-lg">📐</span>
                    <div>
                      <p className="text-sm font-semibold text-orange-800">Preporučen geodetski elaborat</p>
                      <p className="text-xs text-orange-700">
                        Značajne razlike između katastra i ZK — preporučamo angažirati ovlaštenog geodeta 
                        za izradu elaborata usklađenja prije kupoprodaje.
                      </p>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* ═══ V44: ENCUMBRANCE RISK MATRIX ═══ */}
            {zkAnalysis.encumbrance_risk_matrix && (
              <div className={`rounded-xl border-2 p-6 ${
                zkAnalysis.encumbrance_risk_matrix.overall_risk === "BLOKIRANO" ? "border-red-500 bg-red-50" :
                zkAnalysis.encumbrance_risk_matrix.overall_risk === "UVJETNO" ? "border-yellow-500 bg-yellow-50" :
                "border-green-500 bg-green-50"
              }`}>
                <div className="flex items-center gap-3 mb-4">
                  <BarChart3 className={`h-6 w-6 ${
                    zkAnalysis.encumbrance_risk_matrix.overall_risk === "BLOKIRANO" ? "text-red-600" :
                    zkAnalysis.encumbrance_risk_matrix.overall_risk === "UVJETNO" ? "text-yellow-600" :
                    "text-green-600"
                  }`} />
                  <div className="flex-1">
                    <h3 className="text-lg font-bold text-gray-800">
                      🛡️ Matrica rizika tereta
                    </h3>
                    <p className="text-xs text-gray-500">OCR 2.0 — napredna analiza svih tereta i ograničenja</p>
                  </div>
                  <span className={`text-xs font-bold uppercase px-3 py-1 rounded-full ${
                    zkAnalysis.encumbrance_risk_matrix.overall_risk === "BLOKIRANO" ? "bg-red-200 text-red-800" :
                    zkAnalysis.encumbrance_risk_matrix.overall_risk === "UVJETNO" ? "bg-yellow-200 text-yellow-800" :
                    "bg-green-200 text-green-800"
                  }`}>
                    {zkAnalysis.encumbrance_risk_matrix.overall_risk}
                  </span>
                </div>

                {/* Metric grid */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
                  <div className="rounded-lg bg-white/70 p-3 text-center">
                    <p className="text-2xl font-bold text-gray-900">{zkAnalysis.encumbrance_risk_matrix.total_active}</p>
                    <p className="text-[10px] font-semibold text-gray-500 uppercase">Ukupno aktivni</p>
                  </div>
                  <div className={`rounded-lg p-3 text-center ${zkAnalysis.encumbrance_risk_matrix.blocking_sale > 0 ? "bg-red-100" : "bg-white/70"}`}>
                    <p className={`text-2xl font-bold ${zkAnalysis.encumbrance_risk_matrix.blocking_sale > 0 ? "text-red-700" : "text-gray-900"}`}>
                      {zkAnalysis.encumbrance_risk_matrix.blocking_sale}
                    </p>
                    <p className="text-[10px] font-semibold text-gray-500 uppercase">Blokira prodaju</p>
                  </div>
                  <div className={`rounded-lg p-3 text-center ${zkAnalysis.encumbrance_risk_matrix.blocking_construction > 0 ? "bg-orange-100" : "bg-white/70"}`}>
                    <p className={`text-2xl font-bold ${zkAnalysis.encumbrance_risk_matrix.blocking_construction > 0 ? "text-orange-700" : "text-gray-900"}`}>
                      {zkAnalysis.encumbrance_risk_matrix.blocking_construction}
                    </p>
                    <p className="text-[10px] font-semibold text-gray-500 uppercase">Blokira gradnju</p>
                  </div>
                  <div className={`rounded-lg p-3 text-center ${zkAnalysis.encumbrance_risk_matrix.fiduciary_count > 0 ? "bg-purple-100" : "bg-white/70"}`}>
                    <p className={`text-2xl font-bold ${zkAnalysis.encumbrance_risk_matrix.fiduciary_count > 0 ? "text-purple-700" : "text-gray-900"}`}>
                      {zkAnalysis.encumbrance_risk_matrix.fiduciary_count}
                    </p>
                    <p className="text-[10px] font-semibold text-gray-500 uppercase">Fiducije</p>
                  </div>
                </div>

                {/* Secondary metrics */}
                <div className="flex flex-wrap gap-3 mb-4">
                  {zkAnalysis.encumbrance_risk_matrix.personal_servitude_count > 0 && (
                    <span className="text-xs px-3 py-1 rounded-full bg-blue-100 text-blue-800 font-semibold">
                      {zkAnalysis.encumbrance_risk_matrix.personal_servitude_count} osobna služnost
                    </span>
                  )}
                  {zkAnalysis.encumbrance_risk_matrix.historical_count > 0 && (
                    <span className="text-xs px-3 py-1 rounded-full bg-amber-100 text-amber-800 font-semibold">
                      {zkAnalysis.encumbrance_risk_matrix.historical_count} povijesni upis
                    </span>
                  )}
                  {zkAnalysis.encumbrance_risk_matrix.enforcement_count > 0 && (
                    <span className="text-xs px-3 py-1 rounded-full bg-red-100 text-red-800 font-semibold">
                      {zkAnalysis.encumbrance_risk_matrix.enforcement_count} ovrha
                    </span>
                  )}
                </div>

                {/* Summary text */}
                <p className="text-sm text-gray-700 leading-relaxed">
                  {zkAnalysis.encumbrance_risk_matrix.summary}
                </p>
              </div>
            )}

            {/* ═══ V44: HISTORICAL ENCUMBRANCES ═══ */}
            {zkAnalysis.historical_encumbrances && zkAnalysis.historical_encumbrances.length > 0 && (
              <div className="rounded-xl border border-amber-300 bg-amber-50 p-6">
                <div className="flex items-center gap-3 mb-4">
                  <History className="h-5 w-5 text-amber-700" />
                  <h3 className="text-lg font-bold text-amber-900">
                    📜 Povijesni tereti
                  </h3>
                  <span className="text-xs font-semibold text-amber-700 bg-amber-200 px-2 py-0.5 rounded-full">
                    {zkAnalysis.historical_encumbrances.length} pronađeno
                  </span>
                </div>
                <div className="space-y-3">
                  {zkAnalysis.historical_encumbrances.map((h, i) => (
                    <div key={i} className={`p-4 rounded-lg border-l-4 bg-white/70 ${
                      h.still_legally_relevant ? "border-red-400" : "border-amber-300"
                    }`}>
                      <div className="flex items-center gap-2 flex-wrap mb-2">
                        <span className="text-[10px] font-bold uppercase px-2 py-0.5 rounded bg-amber-200 text-amber-800">
                          {h.type.replace(/_/g, " ")}
                        </span>
                        {h.estimated_year > 0 && (
                          <span className="text-[10px] font-mono text-gray-500">
                            ~{h.estimated_year}.
                          </span>
                        )}
                        {h.still_legally_relevant ? (
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-red-200 text-red-800">
                            PRAVNO RELEVANTNO
                          </span>
                        ) : (
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-green-200 text-green-800">
                            NERELEVANTNO
                          </span>
                        )}
                        {h.blocks_sale === "DA" && (
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-red-900 text-white flex items-center gap-1">
                            <Lock className="h-3 w-3" /> BLOKIRA PRODAJU
                          </span>
                        )}
                        {h.blocks_construction === "DA" && (
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-orange-700 text-white">
                            BLOKIRA GRADNJU
                          </span>
                        )}
                      </div>
                      <p className="text-sm text-gray-800 italic mb-1">&ldquo;{h.original_text}&rdquo;</p>
                      <p className="text-sm text-gray-700">{h.interpretation}</p>
                      {h.resolution && (
                        <div className="mt-2 flex items-start gap-1.5 text-xs text-gray-600 bg-white/60 rounded p-2">
                          <span className="font-semibold text-gray-500 whitespace-nowrap">📋 Razrješenje:</span>
                          <span>{h.resolution}</span>
                        </div>
                      )}
                      {h.legal_basis && (
                        <p className="text-xs text-gray-500 mt-1">
                          Pravna osnova: <span className="font-medium">{h.legal_basis}</span>
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* ─── DUE DILIGENCE FINDINGS ─── */}
            {zkAnalysis.findings && zkAnalysis.findings.length > 0 && (
              <div className="rounded-xl bg-white border border-gray-200 p-6">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4 flex items-center gap-2">
                  <ShieldAlert className="h-4 w-4" />
                  Pravne napomene i upozorenja ({zkAnalysis.findings.length})
                </h3>
                <div className="space-y-3">
                  {[...zkAnalysis.findings]
                    .sort((a, b) => {
                      const sev = { CRITICAL: 0, WARNING: 1, INFO: 2 };
                      return (sev[a.severity] ?? 2) - (sev[b.severity] ?? 2);
                    })
                    .map((f, i) => (
                    <div key={i} className={`p-4 rounded-lg border-l-4 ${
                      f.severity === "CRITICAL" ? "border-red-500 bg-red-50" :
                      f.severity === "WARNING" ? "border-yellow-500 bg-yellow-50" :
                      "border-blue-500 bg-blue-50"
                    }`}>
                      <div className="flex items-start gap-3">
                        <div className="mt-0.5">
                          {f.severity === "CRITICAL" ? <AlertOctagon className="h-5 w-5 text-red-600" /> :
                           f.severity === "WARNING" ? <CircleAlert className="h-5 w-5 text-yellow-600" /> :
                           <Info className="h-5 w-5 text-blue-600" />}
                        </div>
                        <div className="flex-1">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className={`text-xs font-bold uppercase px-2 py-0.5 rounded ${
                              f.severity === "CRITICAL" ? "bg-red-200 text-red-800" :
                              f.severity === "WARNING" ? "bg-yellow-200 text-yellow-800" :
                              "bg-blue-200 text-blue-800"
                            }`}>
                              {f.severity === "CRITICAL" ? "KRITIČNO" : f.severity === "WARNING" ? "UPOZORENJE" : "INFO"}
                            </span>
                            <span className="text-[10px] font-semibold text-gray-400 uppercase">{f.category}</span>
                          </div>
                          <h4 className={`text-sm font-bold mt-1 ${
                            f.severity === "CRITICAL" ? "text-red-800" :
                            f.severity === "WARNING" ? "text-yellow-800" :
                            "text-blue-800"
                          }`}>{f.title}</h4>
                          <p className="text-sm text-gray-700 mt-1">{f.description}</p>
                          {f.advice && (
                            <div className="mt-2 flex items-start gap-1.5 text-xs text-gray-600 bg-white/60 rounded p-2">
                              <span className="font-semibold text-gray-500 whitespace-nowrap">💡 Savjet:</span>
                              <span>{f.advice}</span>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* ─── DUE DILIGENCE SUMMARY ─── */}
            {zkAnalysis.due_diligence_summary && (
              <div className="rounded-xl bg-gradient-to-br from-slate-50 to-indigo-50 border border-indigo-200 p-6">
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-sm font-semibold text-indigo-800 uppercase tracking-wide flex items-center gap-2">
                    <ShieldCheck className="h-4 w-4" />
                    Pravno mišljenje
                  </h3>
                  <button
                    onClick={() => copyToClipboard(zkAnalysis.due_diligence_summary || "")}
                    className="flex items-center gap-1 text-sm text-indigo-500 hover:text-indigo-700 transition"
                  >
                    {copied ? (
                      <><Check className="h-4 w-4 text-green-600" /> Kopirano</>
                    ) : (
                      <><ClipboardCopy className="h-4 w-4" /> Kopiraj</>
                    )}
                  </button>
                </div>
                <p className="text-gray-800 leading-relaxed whitespace-pre-wrap">{zkAnalysis.due_diligence_summary}</p>
              </div>
            )}

            {/* ─── LEGACY LAWYER COMMENT (fallback) ─── */}
            {!zkAnalysis.due_diligence_summary && zkAnalysis.lawyer_comment && (
              <div className="rounded-lg bg-white border border-gray-200 p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                  Komentar odvjetnika
                </h3>
                <p className="text-gray-800 leading-relaxed">{zkAnalysis.lawyer_comment}</p>
              </div>
            )}

            {showZkDetails && (
              <div className="space-y-4">
                {/* Property Info Grid */}
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  <InfoCard label="Nekretnina" value={zkAnalysis.property_id} />
                  <InfoCard label="Tip" value={zkAnalysis.property_type} />
                  <InfoCard label="K.O." value={zkAnalysis.cadastral_municipality} />
                  <InfoCard label="ZK Uložak" value={zkAnalysis.zk_number} />
                  <InfoCard label="Površina zemljišta" value={zkAnalysis.land_area_m2 > 0 ? `${formatPrice(zkAnalysis.land_area_m2)} m²` : "—"} />
                  <InfoCard label="Površina zgrade" value={zkAnalysis.building_area_m2 > 0 ? `${formatPrice(zkAnalysis.building_area_m2)} m²` : "—"} />
                  <InfoCard label="Kultura" value={zkAnalysis.land_use || "—"} />
                  <InfoCard label="Lokacija" value={zkAnalysis.location} />
                  {zkAnalysis.etazni_udio && <InfoCard label="Etažni udio" value={zkAnalysis.etazni_udio} />}
                </div>

                {/* Owners */}
                {zkAnalysis.owners.length > 0 && (
                  <div className="rounded-lg bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        Vlasnici (B list)
                      </h3>
                    </div>
                    <table className="w-full text-sm text-left">
                      <thead className="bg-gray-50 text-gray-500 uppercase text-xs">
                        <tr>
                          <th className="px-5 py-2">Ime</th>
                          <th className="px-5 py-2">Udio</th>
                          <th className="px-5 py-2">Temelj stjecanja</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {zkAnalysis.owners.map((o, i) => (
                          <tr key={i} className="text-gray-700">
                            <td className="px-5 py-2 font-medium">{o.name}</td>
                            <td className="px-5 py-2">{o.share}</td>
                            <td className="px-5 py-2">{o.basis_of_acquisition || "—"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                {/* Mortgages */}
                {zkAnalysis.active_mortgages.length > 0 && (
                  <div className="rounded-lg bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        Hipoteke (C list)
                      </h3>
                    </div>
                    <table className="w-full text-sm text-left">
                      <thead className="bg-gray-50 text-gray-500 uppercase text-xs">
                        <tr>
                          <th className="px-5 py-2">Iznos</th>
                          <th className="px-5 py-2">Valuta</th>
                          <th className="px-5 py-2">Godina</th>
                          <th className="px-5 py-2">Detalji</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {zkAnalysis.active_mortgages.map((m, i) => (
                          <tr key={i} className="text-gray-700">
                            <td className="px-5 py-2">{m.amount}</td>
                            <td className="px-5 py-2">{m.currency}</td>
                            <td className="px-5 py-2">{m.year}</td>
                            <td className="px-5 py-2">{m.details}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                {/* Disputes */}
                {zkAnalysis.active_disputes.length > 0 && (
                  <div className="rounded-lg border border-red-200 bg-red-50 p-5">
                    <h3 className="text-sm font-semibold text-red-600 uppercase tracking-wide mb-2">
                      ⚠️ Aktivni sporovi
                    </h3>
                    <ul className="list-disc list-inside text-red-800 space-y-1">
                      {zkAnalysis.active_disputes.map((d, i) => (
                        <li key={i}>{d}</li>
                      ))}
                    </ul>
                  </div>
                )}

                {/* Other Encumbrances */}
                {zkAnalysis.other_encumbrances?.length > 0 && (
                  <div className="rounded-lg bg-white border border-gray-200 p-5">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                      Ostali tereti (C list)
                    </h3>
                    <div className="space-y-2">
                      {zkAnalysis.other_encumbrances.map((e, i) => (
                        <div key={i} className={`p-3 rounded-lg border ${e.is_active ? "border-yellow-200 bg-yellow-50" : "border-gray-100 bg-gray-50"}`}>
                          <div className="flex items-center gap-2">
                            <span className={`text-xs font-semibold px-2 py-0.5 rounded ${e.is_active ? "bg-yellow-200 text-yellow-800" : "bg-gray-200 text-gray-600"}`}>
                              {e.type}
                            </span>
                            {!e.is_active && <span className="text-xs text-gray-400">(brisano)</span>}
                          </div>
                          <p className="text-sm text-gray-700 mt-1">{e.description}</p>
                          {e.beneficiary && (
                            <p className="text-xs text-gray-500 mt-1">Korisnik: {e.beneficiary}</p>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Parcels */}
                {zkAnalysis.parcels?.length > 1 && (
                  <div className="rounded-lg bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        Čestice (A list)
                      </h3>
                    </div>
                    <table className="w-full text-sm text-left">
                      <thead className="bg-gray-50 text-gray-500 uppercase text-xs">
                        <tr>
                          <th className="px-5 py-2">Čestica</th>
                          <th className="px-5 py-2">Kultura</th>
                          <th className="px-5 py-2">Površina</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {zkAnalysis.parcels.map((p, i) => (
                          <tr key={i} className="text-gray-700">
                            <td className="px-5 py-2 font-medium">{p.parcel_number}</td>
                            <td className="px-5 py-2">{p.land_use}</td>
                            <td className="px-5 py-2">{formatPrice(p.area_m2)} m²</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* ===================== VALUATION RESULTS ===================== */}
        {hasResult && valuation && (
          <div className="space-y-6">
            <div className="flex items-center justify-between">
              <h2 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                <Scale className="h-6 w-6 text-blue-600" />
                Procjena tržišne vrijednosti
              </h2>
              <button
                onClick={() => setShowValuation(!showValuation)}
                className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1"
              >
                {showValuation ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                {showValuation ? "Sakrij" : "Prikaži"}
              </button>
            </div>

            {showValuation && (
              <div className="space-y-6">
                {/* Value Card */}
                <div className="rounded-xl bg-gradient-to-r from-blue-600 to-blue-700 p-8 text-center text-white shadow-lg">
                  <p className="text-sm uppercase tracking-wide opacity-80 mb-1">
                    Procijenjena tržišna vrijednost
                  </p>
                  <p className="text-4xl font-bold">
                    {valuation.estimated_value_eur.toLocaleString("hr-HR")} €
                  </p>
                  {/* V4 Math Confidence (primary) */}
                  {valuation.math_confidence_score != null && (
                    <div className="mt-4 mx-auto max-w-md">
                      <div className="flex items-center justify-center gap-2 mb-2">
                        <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                          valuation.math_confidence_score >= 75
                            ? "bg-green-500/20 text-green-100"
                            : valuation.math_confidence_score >= 50
                            ? "bg-yellow-500/20 text-yellow-100"
                            : "bg-red-500/20 text-red-100"
                        }`}>
                          🧮 Engine: {valuation.math_confidence_level} ({valuation.math_confidence_score}/100)
                        </span>
                        {valuation.confidence_score != null && (
                          <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                            valuation.confidence_color === "green"
                              ? "bg-green-500/20 text-green-100"
                              : valuation.confidence_color === "yellow"
                              ? "bg-yellow-500/20 text-yellow-100"
                              : "bg-red-500/20 text-red-100"
                          }`}>
                            🔍 Podaci: {valuation.confidence_score}/100
                          </span>
                        )}
                      </div>
                      <div className="w-full bg-white/20 rounded-full h-2 mb-2">
                        <div
                          className={`h-2 rounded-full transition-all duration-500 ${
                            valuation.math_confidence_score >= 75 ? "bg-green-400"
                            : valuation.math_confidence_score >= 50 ? "bg-yellow-400"
                            : "bg-red-400"
                          }`}
                          style={{ width: `${valuation.math_confidence_score}%` }}
                        />
                      </div>
                      {valuation.math_confidence_explanation && (
                        <p className="text-xs opacity-70">{valuation.math_confidence_explanation}</p>
                      )}
                    </div>
                  )}
                  {/* Fallback to V3 confidence if no V4 */}
                  {valuation.math_confidence_score == null && valuation.confidence_level && (
                    <div className="mt-4 mx-auto max-w-md">
                      <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                        valuation.confidence_color === "green"
                          ? "bg-green-500/20 text-green-100"
                          : valuation.confidence_color === "yellow"
                          ? "bg-yellow-500/20 text-yellow-100"
                          : "bg-red-500/20 text-red-100"
                      }`}>
                        Pouzdanost: {valuation.confidence_level}
                        {valuation.confidence_score ? ` (${valuation.confidence_score}/100)` : ""}
                      </span>
                    </div>
                  )}
                  {valuation.calculated_price_per_m2 && (
                    <p className="text-sm mt-2 opacity-80">
                      {valuation.calculated_price_per_m2.toLocaleString("hr-HR")} €/m²
                    </p>
                  )}
                </div>

                {/* Engine Analytics Panel (V4) */}
                {valuation.market_average_asking && (
                  <div className="rounded-xl bg-white border border-gray-200 p-5">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                      📊 Analitika procjene (Robust Estimator V4)
                    </h3>
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                      <div className="text-center p-3 bg-gray-50 rounded-lg">
                        <p className="text-xs text-gray-500">Weighted Median</p>
                        <p className="text-lg font-bold text-gray-900">
                          {(valuation.market_average_asking ?? 0).toLocaleString("hr-HR")} €/m²
                        </p>
                        <p className="text-xs text-gray-400">{valuation.price_source}</p>
                      </div>
                      <div className="text-center p-3 bg-gray-50 rounded-lg">
                        <p className="text-xs text-gray-500">Comparables</p>
                        <p className="text-lg font-bold text-gray-900">
                          {valuation.usable_price_points ?? 0} / {valuation.total_listings_found ?? 0}
                        </p>
                        <p className="text-xs text-gray-400">
                          {valuation.outliers_removed ?? 0} outliera ({valuation.outlier_method ?? "N/A"})
                        </p>
                      </div>
                      <div className="text-center p-3 bg-gray-50 rounded-lg">
                        <p className="text-xs text-gray-500">StdDev / CV</p>
                        <p className="text-lg font-bold text-gray-900">
                          {(valuation.std_dev_per_m2 ?? 0).toLocaleString("hr-HR")} €/m²
                        </p>
                        <p className={`text-xs font-medium ${
                          (valuation.coefficient_of_variation ?? 0) < 15 ? "text-green-600"
                          : (valuation.coefficient_of_variation ?? 0) < 30 ? "text-yellow-600"
                          : "text-red-600"
                        }`}>
                          CV: {valuation.coefficient_of_variation ?? 0}%
                        </p>
                      </div>
                    </div>
                    {/* Realization Gap Reasoning */}
                    {valuation.realization_gap_reasoning && (
                      <div className="mt-3 p-3 bg-blue-50 border border-blue-200 rounded-lg">
                        <p className="text-xs font-semibold text-blue-700 mb-1">
                          Realizacijski gap: {valuation.realization_discount_percent ?? 0}%
                        </p>
                        <p className="text-xs text-blue-600">{valuation.realization_gap_reasoning}</p>
                      </div>
                    )}
                    {valuation.note && (
                      <div className="mt-3 flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-lg">
                        <AlertTriangle className="h-4 w-4 text-amber-600 flex-shrink-0 mt-0.5" />
                        <p className="text-xs text-amber-800">{valuation.note}</p>
                      </div>
                    )}
                  </div>
                )}

                {/* V4 Used Comparables Table (Engine) */}
                {valuation.used_comparables && valuation.used_comparables.length > 0 && (() => {
                  const sorted = [...valuation.used_comparables].sort((a, b) => {
                    if (a.is_outlier !== b.is_outlier) return a.is_outlier ? 1 : -1;
                    return b.similarity_score - a.similarity_score;
                  });
                  const usedCount = sorted.filter(c => !c.is_outlier).length;
                  const outlierCount = sorted.filter(c => c.is_outlier).length;
                  const usedPrices = sorted.filter(c => !c.is_outlier).map(c => c.price_per_m2).sort((a, b) => a - b);
                  const medianPrice = usedPrices.length > 0
                    ? usedPrices[Math.floor(usedPrices.length / 2)]
                    : 0;

                  return (
                  <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        🧮 Engine Comparables ({usedCount} korištenih{outlierCount > 0 ? `, ${outlierCount} uklonjenih` : ""})
                      </h3>
                      {medianPrice > 0 && (
                        <p className="text-xs text-gray-400 mt-1">
                          Median korištenih: {medianPrice.toLocaleString("hr-HR")} €/m²
                        </p>
                      )}
                    </div>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-gray-100 bg-gray-50/50">
                            <th className="text-left px-4 py-2 font-semibold text-gray-600">Oglas</th>
                            <th className="text-right px-4 py-2 font-semibold text-gray-600">€/m²</th>
                            <th className="text-right px-4 py-2 font-semibold text-gray-600">Similarity</th>
                            <th className="text-center px-4 py-2 font-semibold text-gray-600">Status</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {sorted.map((c, i) => (
                            <tr key={i} className={`transition-colors ${c.is_outlier ? "bg-red-50/50 opacity-60" : "hover:bg-gray-50"}`}>
                              <td className="px-4 py-2">
                                {c.url ? (
                                  <a href={c.url} target="_blank" rel="noopener noreferrer" 
                                    className={`hover:underline text-xs truncate block max-w-[250px] ${c.is_outlier ? "text-gray-400" : "text-blue-600"}`}>
                                    {c.title || c.url}
                                  </a>
                                ) : (
                                  <span className="text-xs text-gray-600 truncate block max-w-[250px]">{c.title}</span>
                                )}
                                {/* Enriched feature pills */}
                                {(c.area_m2 || c.rooms || c.year_built || c.condition || c.energy_class) && (
                                  <div className="flex flex-wrap gap-1 mt-1">
                                    {c.area_m2 && <span className="text-[10px] bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded">{c.area_m2} m²</span>}
                                    {c.rooms && <span className="text-[10px] bg-purple-50 text-purple-600 px-1.5 py-0.5 rounded">{c.rooms} soba</span>}
                                    {c.floor && <span className="text-[10px] bg-indigo-50 text-indigo-600 px-1.5 py-0.5 rounded">Kat {c.floor}</span>}
                                    {c.year_built && <span className="text-[10px] bg-amber-50 text-amber-600 px-1.5 py-0.5 rounded">{c.year_built}</span>}
                                    {c.condition && <span className="text-[10px] bg-green-50 text-green-600 px-1.5 py-0.5 rounded capitalize">{c.condition}</span>}
                                    {c.energy_class && <span className="text-[10px] bg-teal-50 text-teal-600 px-1.5 py-0.5 rounded">E: {c.energy_class}</span>}
                                    {c.heating && <span className="text-[10px] bg-orange-50 text-orange-600 px-1.5 py-0.5 rounded">{c.heating}</span>}
                                    {c.has_garage && <span className="text-[10px] bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded">Garaža</span>}
                                    {c.yard_area_m2 != null && c.yard_area_m2 > 0 && <span className="text-[10px] bg-emerald-50 text-emerald-600 px-1.5 py-0.5 rounded">Okućnica {c.yard_area_m2} m²</span>}
                                  </div>
                                )}
                                {c.similarity_reason && (
                                  <div className="text-[10px] text-gray-400 mt-1 italic truncate max-w-[300px]" title={c.similarity_reason}>
                                    {c.similarity_reason}
                                  </div>
                                )}
                                {c.deep_dive_status && (
                                  <div className={`text-[10px] mt-1 truncate max-w-[350px] ${
                                    c.deep_dive_status.startsWith("✓") ? "text-green-500" 
                                    : c.deep_dive_status.startsWith("⚠") ? "text-amber-500"
                                    : "text-red-400"
                                  }`} title={c.deep_dive_status}>
                                    {c.deep_dive_status}
                                  </div>
                                )}
                                {/* Per-comp correction */}
                                {!c.is_outlier && c.correction_reason && (
                                  <div className="text-[10px] text-indigo-500 mt-1">
                                    📐 {c.correction_reason}
                                  </div>
                                )}
                              </td>
                              <td className={`px-4 py-2 text-right font-mono text-xs ${c.is_outlier ? "text-red-400 line-through" : ""}`}>
                                {c.price_per_m2.toLocaleString("hr-HR")} €
                                {c.total_price && (
                                  <div className="text-[10px] text-gray-400 font-normal">
                                    ({c.total_price.toLocaleString("hr-HR")} € uk.)
                                  </div>
                                )}
                              </td>
                              <td className="px-4 py-2 text-right">
                                <div className="flex items-center justify-end gap-1">
                                  <div className="w-12 bg-gray-200 rounded-full h-1.5">
                                    <div className={`h-1.5 rounded-full ${
                                      c.is_outlier ? "bg-red-300"
                                      : c.similarity_score >= 0.7 ? "bg-green-500"
                                      : c.similarity_score >= 0.4 ? "bg-yellow-500"
                                      : "bg-red-500"
                                    }`} style={{ width: `${Math.max(c.similarity_score * 100, 5)}%` }} />
                                  </div>
                                  <span className={`text-xs w-8 text-right ${c.is_outlier ? "text-gray-400" : "text-gray-500"}`}>
                                    {(c.similarity_score * 100).toFixed(0)}%
                                  </span>
                                </div>
                              </td>
                              <td className="px-4 py-2 text-center">
                                <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${
                                  c.is_outlier
                                    ? "bg-red-100 text-red-700"
                                    : "bg-green-100 text-green-700"
                                }`}>
                                  {c.is_outlier ? "✗ Uklonjen" : "✓ Korišten"}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                  );
                })()}

                {/* V4 Engine Adjustments */}
                {valuation.adjustments && valuation.adjustments.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        ⚙️ Engine Korekcije (cap ±40%)
                      </h3>
                    </div>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-gray-100 bg-gray-50/50">
                            <th className="text-left px-5 py-2 font-semibold text-gray-600">Faktor</th>
                            <th className="text-left px-5 py-2 font-semibold text-gray-600">Opis</th>
                            <th className="text-right px-5 py-2 font-semibold text-gray-600">%</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {valuation.adjustments.map((a, i) => (
                            <tr key={i} className="hover:bg-gray-50">
                              <td className="px-5 py-2 text-gray-800 font-medium text-xs">{a.factor}</td>
                              <td className="px-5 py-2 text-gray-500 text-xs">{a.description}</td>
                              <td className="px-5 py-2 text-right">
                                <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-bold ${
                                  a.percent > 0 ? "bg-green-100 text-green-800"
                                  : a.percent < 0 ? "bg-red-100 text-red-800"
                                  : "bg-gray-100 text-gray-600"
                                }`}>
                                  {a.percent > 0 ? "+" : ""}{a.percent}%
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                        <tfoot>
                          <tr className="border-t-2 border-gray-200 bg-gray-50">
                            <td colSpan={2} className="px-5 py-2 font-semibold text-gray-800 text-xs">
                              Ukupno (cap ±40%)
                            </td>
                            <td className="px-5 py-2 text-right">
                              <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-bold ${
                                (valuation.total_adjustment_percent ?? 0) >= 0 ? "bg-green-100 text-green-800" : "bg-red-100 text-red-800"
                              }`}>
                                {(valuation.total_adjustment_percent ?? 0) > 0 ? "+" : ""}{valuation.total_adjustment_percent ?? 0}%
                              </span>
                            </td>
                          </tr>
                        </tfoot>
                      </table>
                    </div>
                  </div>
                )}

                {/* Pipeline Trace (collapsible) */}
                {valuation.pipeline && valuation.pipeline.length > 0 && (
                  <details className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                    <summary className="px-5 py-3 border-b border-gray-100 bg-gray-50 cursor-pointer">
                      <span className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        🔬 Pipeline Trace ({valuation.pipeline.length} koraka)
                      </span>
                    </summary>
                    <div className="px-5 py-3 space-y-1">
                      {valuation.pipeline.map((step, i) => (
                        <div key={i} className="flex items-start gap-2">
                          <span className="text-xs text-gray-400 font-mono w-6 text-right flex-shrink-0">{i + 1}.</span>
                          <span className="text-xs text-gray-600">{step}</span>
                        </div>
                      ))}
                    </div>
                    {valuation.outlier_log && valuation.outlier_log.length > 0 && (
                      <div className="px-5 py-3 border-t border-gray-100 bg-gray-50/50">
                        <p className="text-xs font-semibold text-gray-500 mb-1">Outlier Log:</p>
                        {valuation.outlier_log.map((log, i) => (
                          <p key={i} className="text-xs text-gray-500 ml-4">• {log}</p>
                        ))}
                      </div>
                    )}
                  </details>
                )}

                {/* Target Property Summary */}
                <div className="rounded-xl bg-white border border-gray-200 p-5">
                  <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                    Nekretnina
                  </h3>
                  <p className="text-gray-800">
                    {valuation.target_property_summary}
                  </p>
                </div>

                {/* Selected Comparables (AI) */}
                {valuation.selected_comparables && valuation.selected_comparables.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        Odabrane usporedive nekretnine
                      </h3>
                    </div>
                    <div className="divide-y divide-gray-100">
                      {valuation.selected_comparables.map((c, i) => (
                        <div key={i} className="p-5">
                          <div className="flex items-start justify-between gap-4">
                            <div className="flex-1">
                              <div className="flex items-center gap-2 mb-1">
                                <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-blue-100 text-blue-700 text-xs font-bold">
                                  {i + 1}
                                </span>
                                <span className="text-sm font-semibold text-gray-900">
                                  {c.title || "Usporediva nekretnina"}
                                </span>
                              </div>
                              <div className="flex items-center gap-3 mt-1">
                                <span className="inline-flex items-center rounded-full bg-emerald-100 text-emerald-800 px-2.5 py-0.5 text-xs font-semibold">
                                  {c.price}
                                </span>
                                {c.location && (
                                  <span className="text-xs text-gray-500">
                                    📍 {c.location}
                                  </span>
                                )}
                              </div>
                              <p className="text-sm text-gray-600 mt-2">
                                {c.reason_for_selection}
                              </p>
                              {c.url && (
                                <a
                                  href={c.url}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="inline-flex items-center gap-1 mt-2 text-sm text-blue-600 hover:text-blue-800 font-medium hover:underline"
                                >
                                  <ExternalLink className="h-3.5 w-3.5" />
                                  Otvori oglas
                                </a>
                              )}
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Value Drivers Table */}
                {valuation.value_drivers && valuation.value_drivers.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                    <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                        Faktori vrijednosti
                      </h3>
                    </div>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-gray-100 bg-gray-50/50">
                            <th className="text-left px-5 py-3 font-semibold text-gray-600">Faktor</th>
                            <th className="text-left px-5 py-3 font-semibold text-gray-600">Utjecaj</th>
                            <th className="text-right px-5 py-3 font-semibold text-gray-600">Korekcija</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {valuation.value_drivers.map((d, i) => (
                            <tr key={i} className="hover:bg-gray-50 transition-colors">
                              <td className="px-5 py-3 text-gray-800 font-medium">{d.factor}</td>
                              <td className="px-5 py-3 text-gray-600">{d.impact}</td>
                              <td className="px-5 py-3 text-right">
                                <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                                  d.adjustment_percent > 0
                                    ? "bg-green-100 text-green-800"
                                    : d.adjustment_percent < 0
                                    ? "bg-red-100 text-red-800"
                                    : "bg-gray-100 text-gray-600"
                                }`}>
                                  {d.adjustment_percent > 0 ? "+" : ""}{d.adjustment_percent}%
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                        <tfoot>
                          <tr className="border-t-2 border-gray-200 bg-gray-50">
                            <td colSpan={2} className="px-5 py-3 text-gray-800 font-semibold">Ukupna korekcija</td>
                            <td className="px-5 py-3 text-right">
                              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ${
                                valuation.value_drivers.reduce((sum, d) => sum + d.adjustment_percent, 0) >= 0
                                  ? "bg-green-100 text-green-800"
                                  : "bg-red-100 text-red-800"
                              }`}>
                                {valuation.value_drivers.reduce((sum, d) => sum + d.adjustment_percent, 0) > 0 ? "+" : ""}
                                {valuation.value_drivers.reduce((sum, d) => sum + d.adjustment_percent, 0)}%
                              </span>
                            </td>
                          </tr>
                        </tfoot>
                      </table>
                    </div>
                  </div>
                )}

                {/* Valuation Reasoning */}
                <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                  <div className="flex items-center justify-between px-5 py-3 border-b border-gray-100 bg-gray-50">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                      Obrazloženje procjene
                    </h3>
                    <button
                      onClick={() => copyToClipboard(valuation.valuation_reasoning)}
                      className="flex items-center gap-1 text-sm font-medium text-blue-600 hover:text-blue-800 transition"
                    >
                      {copied ? (
                        <><Check className="h-4 w-4" /> Kopirano!</>
                      ) : (
                        <><ClipboardCopy className="h-4 w-4" /> Kopiraj u clipboard</>
                      )}
                    </button>
                  </div>
                  <div className="px-6 py-5">
                    <div className="prose prose-sm max-w-none text-gray-800 whitespace-pre-wrap leading-relaxed">
                      {valuation.valuation_reasoning}
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Valuation skipped message */}
        {hasResult && !valuation && unifiedResult?.valuation_skip_reason && (
          <div className="rounded-xl bg-yellow-50 border border-yellow-200 p-6 text-center">
            <AlertTriangle className="h-8 w-8 text-yellow-600 mx-auto mb-2" />
            <p className="text-sm font-medium text-yellow-800">
              Procjena nije pokrenuta: {unifiedResult.valuation_skip_reason}
            </p>
          </div>
        )}

        {/* New Analysis Button */}
        {(hasResult || (!isLoading && error)) && (
          <div className="mt-8 text-center">
            <button
              onClick={reset}
              className="rounded-lg bg-gray-100 px-6 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-200 transition"
            >
              Nova analiza
            </button>
          </div>
        )}
      </div>
    </div>
    </>
  );
}

// --- Reusable small components ---

function InfoCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-white border border-gray-200 p-4">
      <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">{label}</h3>
      <p className="text-sm font-medium text-gray-800">{value || "—"}</p>
    </div>
  );
}


