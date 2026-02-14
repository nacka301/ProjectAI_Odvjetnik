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
  Search,
  Brain,
  FileCheck,
  ExternalLink,
  ShieldCheck,
  ShieldAlert,
  BookOpen,
  ChevronDown,
  ChevronUp,
} from "lucide-react";
import { uploadPdfToStorage, saveEvaluation } from "@/lib/supabase";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

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
}

interface ParcelInfo {
  parcel_number: string;
  land_use: string;
  area_m2: number;
  description: string;
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

// --- Component ---

export default function ZkProcjenaPage() {
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const [stages, setStages] = useState<StreamStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [zkAnalysis, setZkAnalysis] = useState<ZkParsedResult | null>(null);
  const [unifiedResult, setUnifiedResult] = useState<UnifiedResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

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
    if (!file) return;

    setError(null);
    setZkAnalysis(null);
    setUnifiedResult(null);
    setStages(INITIAL_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);

    // Upload PDF to Supabase Storage (non-blocking)
    const pdfUrlPromise = uploadPdfToStorage(file);

    const state = { result: null as UnifiedResult | null };
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const url = `${API_BASE}/api/zk/stream`;
      const formData = new FormData();
      formData.append("file", file);
      if (city.trim()) formData.append("city", city.trim());
      if (neighborhood.trim()) formData.append("neighborhood", neighborhood.trim());
      if (email.trim()) formData.append("email", email.trim());

      const response = await fetch(url, { method: "POST", body: formData, signal: controller.signal });

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

      // Save to Supabase (fire-and-forget)
      if (state.result?.valuation) {
        const pdfUrl = await pdfUrlPromise;
        saveEvaluation({
          property_type: state.result.zk_analysis.property_type || "nekretnina",
          location: state.result.zk_analysis.location || "nepoznato",
          estimated_value: state.result.valuation.estimated_value_eur,
          pdf_url: pdfUrl,
          raw_json_output: state.result as unknown as Record<string, unknown>,
        }).catch(() => { /* non-critical */ });
      }
    } catch (e: unknown) {
      setIsStreaming(false);
      if (e instanceof DOMException && e.name === "AbortError") {
        setError("Analiza je otkazana.");
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
  };

  // --- Helpers ---

  const riskColor = (score: string) => {
    switch (score) {
      case "HIGH": return "bg-red-100 border-red-500 text-red-800";
      case "MEDIUM": return "bg-yellow-100 border-yellow-500 text-yellow-800";
      default: return "bg-green-100 border-green-500 text-green-800";
    }
  };

  const RiskIcon = ({ score }: { score: string }) =>
    score === "HIGH" || score === "MEDIUM" ? (
      <ShieldAlert className="inline-block mr-2 h-5 w-5" />
    ) : (
      <ShieldCheck className="inline-block mr-2 h-5 w-5" />
    );

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
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
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
            ZK Izvadak → Procjena vrijednosti
          </h1>
          <p className="mt-2 text-gray-500">
            Uploadajte ZK izvadak — sustav parsira sve podatke, analizira rizik i automatski procjenjuje tržišnu vrijednost
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
            {/* Location Override */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                📍 Lokacija nekretnine
                <span className="text-xs font-normal text-gray-400">(opcionalno — za precizniju pretragu)</span>
              </h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Grad</label>
                  <input
                    type="text"
                    value={city}
                    onChange={(e) => setCity(e.target.value)}
                    placeholder="npr. Osijek, Split, Zagreb..."
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Kvart / Naselje</label>
                  <input
                    type="text"
                    value={neighborhood}
                    onChange={(e) => setNeighborhood(e.target.value)}
                    placeholder="npr. Retfala, Spinut, Maksimir..."
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                </div>
              </div>
            </div>

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

            {file && (
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
          </div>
        )}

        {/* ===================== STREAMING PROGRESS ===================== */}
        {stages.length > 0 && (
          <div className="mb-8 rounded-xl bg-white border border-gray-200 p-6">
            <h3 className="text-sm font-semibold text-gray-700 mb-4 flex items-center gap-2">
              <Loader2 className={`h-4 w-4 ${isStreaming ? "animate-spin text-indigo-600" : "text-green-600"}`} />
              {isStreaming ? "Pipeline u tijeku..." : "Pipeline završen"}
            </h3>
            <div className="space-y-3">
              {stages.map((s) => (
                <div key={s.id} className="flex items-start gap-3">
                  <StageIcon status={s.status} />
                  <div className="flex-1 min-w-0">
                    <p className={`text-sm font-medium ${
                      s.status === "active" ? "text-blue-700" :
                      s.status === "done" ? "text-green-700" :
                      s.status === "error" ? "text-red-700" :
                      s.status === "skipped" ? "text-gray-500" :
                      "text-gray-400"
                    }`}>{s.label}</p>
                    {s.detail && (
                      <p className="text-xs text-gray-500 mt-0.5 truncate">{s.detail}</p>
                    )}
                    {s.error && (
                      <p className="text-xs text-red-600 mt-0.5">{s.error}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>

            {/* Cancel button */}
            {isStreaming && (
              <div className="mt-4 text-center">
                <button
                  onClick={cancelAnalysis}
                  className="rounded-lg border border-gray-300 bg-white px-5 py-2 text-sm font-medium text-gray-600 hover:bg-red-50 hover:border-red-300 hover:text-red-700 transition"
                >
                  ✕ Odustani
                </button>
              </div>
            )}
          </div>
        )}

        {/* ===================== ZK ANALYSIS RESULTS ===================== */}
        {zkAnalysis && (
          <div className="mb-8 space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                📋 ZK Analiza
              </h2>
              <button
                onClick={() => setShowZkDetails(!showZkDetails)}
                className="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1"
              >
                {showZkDetails ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                {showZkDetails ? "Sakrij" : "Prikaži"}
              </button>
            </div>

            {/* Risk Score */}
            <div className={`flex items-center rounded-lg border-l-4 p-4 ${riskColor(zkAnalysis.risk_score)}`}>
              <RiskIcon score={zkAnalysis.risk_score} />
              <span className="font-bold text-lg">Rizik: {zkAnalysis.risk_score}</span>
            </div>

            {showZkDetails && (
              <div className="space-y-4">
                {/* Lawyer Comment */}
                <div className="rounded-lg bg-white border border-gray-200 p-5">
                  <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                    Komentar odvjetnika
                  </h3>
                  <p className="text-gray-800 leading-relaxed">{zkAnalysis.lawyer_comment}</p>
                </div>

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
                {/* Main Value Card */}
                <div className="rounded-xl bg-gradient-to-r from-blue-600 to-indigo-700 p-8 text-white text-center">
                  <p className="text-sm font-medium text-blue-100 uppercase tracking-wide">
                    Procijenjena tržišna vrijednost
                  </p>
                  <p className="text-5xl font-bold mt-2">
                    {formatPrice(valuation.estimated_value_eur)} €
                  </p>
                  {valuation.calculated_price_per_m2 && valuation.calculated_price_per_m2 > 0 && (
                    <p className="mt-2 text-blue-200">
                      {formatPrice(Math.round(valuation.calculated_price_per_m2))} €/m²
                      {valuation.confidence_level && ` • ${valuation.confidence_level}`}
                    </p>
                  )}
                  {valuation.math_confidence_score !== undefined && valuation.math_confidence_score > 0 && (
                    <div className="mt-3 inline-flex items-center gap-2 bg-white/20 rounded-full px-4 py-1.5">
                      <span className="text-sm font-medium">
                        Pouzdanost: {valuation.math_confidence_score}/100 ({valuation.math_confidence_level})
                      </span>
                    </div>
                  )}
                </div>

                {/* Engine Stats */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <StatCard label="Pronađeno oglasa" value={valuation.total_listings_found ?? 0} />
                  <StatCard label="Korištenih" value={valuation.usable_price_points ?? 0} />
                  <StatCard label="Outlieri uklonjeni" value={valuation.outliers_removed ?? 0} />
                  <StatCard label="Diskont" value={`${valuation.realization_discount_percent?.toFixed(1) ?? "15"}%`} />
                </div>

                {/* Comparables */}
                {valuation.selected_comparables?.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4 flex items-center gap-2">
                      <Search className="h-4 w-4" />
                      Odabrane usporedive nekretnine
                    </h3>
                    <div className="space-y-4">
                      {valuation.selected_comparables.map((c, i) => (
                        <div key={i} className="p-4 border border-gray-100 rounded-lg hover:bg-gray-50 transition">
                          <div className="flex items-start justify-between">
                            <div className="flex-1 min-w-0">
                              <h4 className="text-sm font-semibold text-gray-900 truncate">{c.title}</h4>
                              <div className="flex items-center gap-3 mt-1 text-xs text-gray-500">
                                <span className="font-semibold text-blue-700">{c.price}</span>
                                <span>{c.location}</span>
                              </div>
                            </div>
                            {c.url && (
                              <a
                                href={c.url}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="text-blue-500 hover:text-blue-700 flex-shrink-0 ml-2"
                              >
                                <ExternalLink className="h-4 w-4" />
                              </a>
                            )}
                          </div>
                          <p className="text-xs text-gray-500 mt-2 leading-relaxed">{c.reason_for_selection}</p>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Engine Comparables (used_comparables) */}
                {valuation.used_comparables && valuation.used_comparables.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4 flex items-center gap-2">
                      🧮 Engine usporedbe ({valuation.used_comparables.filter(c => !c.is_outlier).length} korištenih)
                    </h3>
                    <div className="space-y-3">
                      {[...valuation.used_comparables]
                        .sort((a, b) => {
                          if (a.is_outlier !== b.is_outlier) return a.is_outlier ? 1 : -1;
                          return b.similarity_score - a.similarity_score;
                        })
                        .map((c, i) => (
                        <div key={i} className={`p-4 rounded-lg border transition ${c.is_outlier ? "border-red-200 bg-red-50/50 opacity-60" : "border-gray-100 bg-gray-50 hover:border-blue-200 hover:bg-blue-50/30"}`}>
                          <div className="flex items-start justify-between gap-3">
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 flex-wrap">
                                <h4 className="text-sm font-semibold text-gray-900 truncate">{c.title}</h4>
                                {c.is_outlier && (
                                  <span className="text-[10px] font-bold uppercase tracking-wider bg-red-100 text-red-700 px-2 py-0.5 rounded-full">Outlier</span>
                                )}
                              </div>
                              <div className="flex items-center gap-3 mt-1">
                                <span className="text-sm font-bold text-blue-700">{c.price_per_m2.toLocaleString("hr-HR")} €/m²</span>
                                {c.total_price && <span className="text-xs text-gray-500">({c.total_price.toLocaleString("hr-HR")} €)</span>}
                                <span className={`text-xs font-semibold ${c.similarity_score >= 0.7 ? "text-green-600" : c.similarity_score >= 0.4 ? "text-yellow-600" : "text-red-600"}`}>
                                  {(c.similarity_score * 100).toFixed(0)}%
                                </span>
                              </div>
                              {/* Enriched feature pills */}
                              {(c.area_m2 || c.rooms || c.year_built || c.condition || c.energy_class) && (
                                <div className="flex flex-wrap gap-1 mt-2">
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
                              {/* V18: Similarity reason */}
                              {c.similarity_reason && (
                                <div className="text-[10px] text-gray-400 mt-1 italic truncate max-w-[300px]" title={c.similarity_reason}>
                                  {c.similarity_reason}
                                </div>
                              )}
                              {/* V20: Deep dive status */}
                              {c.deep_dive_status && (
                                <div className={`text-[10px] mt-1 truncate max-w-[350px] ${
                                  c.deep_dive_status.startsWith("✓") ? "text-green-500" 
                                  : c.deep_dive_status.startsWith("⚠") ? "text-amber-500"
                                  : "text-red-400"
                                }`} title={c.deep_dive_status}>
                                  {c.deep_dive_status}
                                </div>
                              )}
                            </div>
                            {c.url && (
                              <a href={c.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:text-blue-700 shrink-0">
                                <ExternalLink className="h-4 w-4" />
                              </a>
                            )}
                          </div>
                          {/* Similarity bar */}
                          <div className="mt-2 h-1 bg-gray-200 rounded-full overflow-hidden">
                            <div className={`h-full rounded-full ${c.is_outlier ? "bg-red-300" : c.similarity_score >= 0.7 ? "bg-green-500" : c.similarity_score >= 0.4 ? "bg-yellow-500" : "bg-red-500"}`}
                              style={{ width: `${Math.max(c.similarity_score * 100, 5)}%` }} />
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Value Drivers */}
                {valuation.value_drivers?.length > 0 && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4">
                      Faktori vrijednosti
                    </h3>
                    <div className="space-y-2">
                      {valuation.value_drivers.map((d, i) => (
                        <div key={i} className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
                          <div>
                            <span className="text-sm font-medium text-gray-700">{d.factor}</span>
                            <p className="text-xs text-gray-500">{d.impact}</p>
                          </div>
                          <span className={`text-sm font-bold ${d.adjustment_percent > 0 ? "text-green-600" : d.adjustment_percent < 0 ? "text-red-600" : "text-gray-500"}`}>
                            {d.adjustment_percent > 0 ? "+" : ""}{d.adjustment_percent}%
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Reasoning */}
                {valuation.valuation_reasoning && (
                  <div className="rounded-xl bg-white border border-gray-200 p-6">
                    <div className="flex items-center justify-between mb-4">
                      <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide flex items-center gap-2">
                        <Brain className="h-4 w-4" />
                        Stručno mišljenje
                      </h3>
                      <button
                        onClick={() => copyToClipboard(valuation.valuation_reasoning)}
                        className="flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 transition"
                      >
                        {copied ? (
                          <><Check className="h-4 w-4 text-green-600" /> Kopirano</>
                        ) : (
                          <><ClipboardCopy className="h-4 w-4" /> Kopiraj</>
                        )}
                      </button>
                    </div>
                    <div className="prose prose-sm max-w-none text-gray-800 leading-relaxed whitespace-pre-wrap">
                      {valuation.valuation_reasoning}
                    </div>
                  </div>
                )}
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

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-lg bg-white border border-gray-200 p-4 text-center">
      <p className="text-2xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-500 mt-1">{label}</p>
    </div>
  );
}
