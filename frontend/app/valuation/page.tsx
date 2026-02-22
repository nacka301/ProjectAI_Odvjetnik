"use client";

import {
  useState,
  useCallback,
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
} from "lucide-react";
import { uploadPdfToStorage } from "@/lib/supabase";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import ResultActions from "@/components/ResultActions";
import { useUserId } from "@/lib/useUserId";

import { API_BASE } from "@/lib/api";

// --- Types ---

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

interface AutoValuationResult {
  target_property_summary: string;
  selected_comparables: SelectedComparable[];
  estimated_value_eur: number;
  value_drivers: ValueDriver[];
  valuation_reasoning: string;
  // V2 engine fields
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
  // V3 confidence scoring (market research)
  confidence_score?: number;
  confidence_explanation?: string;
  confidence_color?: string;
  // V4 Robust Estimator fields
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

interface StreamStage {
  id: string;
  label: string;
  status: "pending" | "active" | "done" | "skipped" | "error";
  detail?: string;
  error?: string;
}

const INITIAL_STAGES: StreamStage[] = [
  { id: "reading", label: "Čitam i ekstrahiram podatke o nekretnini...", status: "pending" },
  { id: "searching", label: "Pretražujem Njuškalo i tržište nekretnina...", status: "pending" },
  { id: "ai_features", label: "Analiziram značajke oglasa...", status: "pending" },
  { id: "engine", label: "Engine izračunava tržišnu vrijednost...", status: "pending" },
  { id: "ai_opinion", label: "Generiram stručno mišljenje...", status: "pending" },
  { id: "saving", label: "Spremam rezultate...", status: "pending" },
];

const VALUATION_STAGE_ICONS: Record<string, React.ReactNode> = {
  reading: <FileText className="h-4 w-4" />,
  searching: <Search className="h-4 w-4" />,
  ai_features: <Brain className="h-4 w-4" />,
  engine: <Scale className="h-4 w-4" />,
  ai_opinion: <FileCheck className="h-4 w-4" />,
  saving: <Check className="h-4 w-4" />,
};
const VALUATION_STAGE_MICROCOPY: Record<string, string[]> = {
  reading: ["Učitavam dokument...", "Identificiram lokaciju i tip nekretnine..."],
  searching: ["Pretražujem Njuškalo...", "Pretražujem Index Oglasi...", "Tražim usporedive nekretnine..."],
  ai_features: ["Analiziram slike i opise oglasa...", "Procjenjujem stanje i kvalitetu..."],
  engine: ["Izračunavam medijanu cijena...", "Primjenjujem korekcije...", "Robust estimator u tijeku..."],
  ai_opinion: ["Generiram stručno mišljenje...", "Formuliram obrazloženje procjene..."],
  saving: ["Spremam u bazu...", "Generiranje izvještaja..."],
};

// --- Component ---

type InputMode = "pdf" | "text";

export default function ValuationPage() {
  const userId = useUserId();
  const [inputMode, setInputMode] = useState<InputMode>("pdf");
  const [file, setFile] = useState<File | null>(null);
  const [propertyText, setPropertyText] = useState("");
  const [dragOver, setDragOver] = useState(false);
  const [stages, setStages] = useState<StreamStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [result, setResult] = useState<AutoValuationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  // V58: viewState for full-screen processing
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");

  // Structured location fields
  const [city, setCity] = useState("");
  const [neighborhood, setNeighborhood] = useState("");

  // Email notification
  const [email, setEmail] = useState("");

  const isLoading = isStreaming;
  const canStart = inputMode === "pdf" ? !!file : propertyText.trim().length >= 20;

  // --- File Handling ---

  const handleFile = (f: File | null) => {
    if (f && f.type === "application/pdf") {
      setFile(f);
      setError(null);
      setResult(null);
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

  // --- Auto Generate ---

  const runAutoGenerate = async () => {
    if (inputMode === "pdf" && !file) return;
    if (inputMode === "text" && propertyText.trim().length < 20) return;

    setError(null);
    setResult(null);
    setStages(INITIAL_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);
    setViewState("PROCESSING");

    // Upload PDF to Supabase Storage (non-blocking, only for PDF mode)
    const pdfUrlPromise =
      inputMode === "pdf" && file
        ? uploadPdfToStorage(file)
        : Promise.resolve(null);

    let streamedResult: AutoValuationResult | null = null;

    try {
      let url: string;
      let body: BodyInit;
      const headers: Record<string, string> = {};
      if (userId) headers["x-user-id"] = userId;

      if (inputMode === "pdf" && file) {
        url = `${API_BASE}/api/evaluate/stream`;
        const formData = new FormData();
        formData.append("file", file);
        if (city.trim()) formData.append("city", city.trim());
        if (neighborhood.trim())
          formData.append("neighborhood", neighborhood.trim());
        if (email.trim()) formData.append("email", email.trim());
        body = formData;
      } else {
        url = `${API_BASE}/api/evaluate/text/stream`;
        headers["Content-Type"] = "application/json";
        body = JSON.stringify({
          text: propertyText.trim(),
          city: city.trim() || undefined,
          neighborhood: neighborhood.trim() || undefined,
          email: email.trim() || undefined,
        });
      }

      const response = await fetch(url, { method: "POST", body, headers });

      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split("\n\n");
        buffer = parts.pop()!;

        for (const part of parts) {
          const line = part.trim();
          if (!line.startsWith("data: ")) continue;
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
            } else if (evt.type === "result") {
              streamedResult = evt.data;
              setResult(evt.data);
            }
          } catch {
            // skip malformed SSE lines
          }
        }
      }

      setIsStreaming(false);
      if (streamedResult) setViewState("RESULT");

      // PDF upload result (non-critical)
      if (streamedResult) {
        await pdfUrlPromise;
      }
    } catch {
      setIsStreaming(false);
      setError(
        "Server ne odgovara. Moguće da se Render restartira (~30s). Pokušajte ponovno za minutu."
      );
    }
  };

  // --- Copy ---

  const copyToClipboard = async () => {
    if (!result) return;
    try {
      await navigator.clipboard.writeText(result.valuation_reasoning);
    } catch {
      const ta = document.createElement("textarea");
      ta.value = result.valuation_reasoning;
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
    setFile(null);
    setPropertyText("");
    setCity("");
    setNeighborhood("");
    setEmail("");
    setResult(null);
    setError(null);
    setStages([]);
    setIsStreaming(false);
    setCopied(false);
    setViewState("INPUT");
  };

  // --- Render ---

  return (
    <>
      {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
      {viewState === "PROCESSING" && (
        <SharedProcessingView
          stages={stages as ProcessingStage[]}
          isStreaming={isStreaming}
          error={error}
          onCancel={reset}
          onReset={reset}
          title="Procjena u tijeku"
          subtitle="Automatski pretražujem tržište i procjenjujem vrijednost"
          headerIcon={<Scale className="h-7 w-7 text-white" />}
          stageIcons={VALUATION_STAGE_ICONS}
          stageMicrocopy={VALUATION_STAGE_MICROCOPY}
          theme="blue"
        />
      )}

    <div className={`min-h-screen bg-gray-50 py-12 px-4 font-sans ${viewState === "PROCESSING" ? "hidden" : ""}`}>
      <div className="mx-auto max-w-3xl">
        <Link
          href="/"
          className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6"
        >
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        {/* Header */}
        <div className="text-center mb-10">
          <h1 className="text-3xl font-bold text-gray-900">
            <Scale className="inline-block h-8 w-8 mr-2 text-blue-600" />
            Automatska procjena vrijednosti
          </h1>
          <p className="mt-2 text-gray-500">
            Uploadajte ZK izvadak – sustav automatski pretražuje tržište i
            generira stručno mišljenje
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
        {!isLoading && !result && (
          <div className="space-y-6">
            {/* Mode tabs */}
            <div className="flex rounded-lg bg-gray-100 p-1">
              <button
                onClick={() => setInputMode("pdf")}
                className={`flex-1 flex items-center justify-center gap-2 rounded-md py-2.5 text-sm font-medium transition ${
                  inputMode === "pdf"
                    ? "bg-white text-blue-600 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                }`}
              >
                <Upload className="h-4 w-4" />
                PDF Upload
              </button>
              <button
                onClick={() => setInputMode("text")}
                className={`flex-1 flex items-center justify-center gap-2 rounded-md py-2.5 text-sm font-medium transition ${
                  inputMode === "text"
                    ? "bg-white text-blue-600 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                }`}
              >
                <FileText className="h-4 w-4" />
                Ručni unos teksta
              </button>
            </div>

            {/* Location Fields */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                📍 Lokacija nekretnine
                <span className="text-xs font-normal text-gray-400">(preporučeno za bolju preciznost)</span>
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
              <p className="text-xs text-gray-400 mt-2">
                Ako unesete lokaciju, sustav će je koristiti umjesto automatske ekstrakcije iz dokumenta.
              </p>
            </div>

            {/* Email Notification */}
            <div className="rounded-xl bg-white border border-gray-200 p-5">
              <h4 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
                ✉️ Email obavijest
                <span className="text-xs font-normal text-gray-400">(opcionalno — rezultat na mail)</span>
              </h4>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="npr. ime@gmail.com"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              />
              <p className="text-xs text-gray-400 mt-2">
                Unesite email adresu ako želite primiti rezultat procjene na mail.
              </p>
            </div>

            {/* PDF Drop Zone */}
            {inputMode === "pdf" && (
              <>
                <div
                  onDrop={onDrop}
                  onDragOver={onDragOver}
                  onDragLeave={onDragLeave}
                  className={`flex flex-col items-center justify-center rounded-xl border-2 border-dashed p-12 transition-colors cursor-pointer ${
                    dragOver
                      ? "border-blue-500 bg-blue-50"
                      : "border-gray-300 bg-white hover:border-gray-400"
                  }`}
                >
                  <Upload className="h-12 w-12 text-gray-400 mb-4" />
                  <p className="text-gray-600 font-medium text-lg">
                    Povucite ZK izvadak (PDF) ovdje
                  </p>
                  <p className="text-gray-400 mt-1">
                    ili{" "}
                    <label className="text-blue-600 underline cursor-pointer">
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
                    Max 10 MB • Sustav će automatski pretražiti Njuškalo i Index za
                    usporedive nekretnine
                  </p>
                </div>

                {file && (
                  <div className="flex items-center justify-between rounded-lg bg-white border border-gray-200 p-4">
                    <div className="flex items-center gap-3">
                      <FileText className="h-5 w-5 text-blue-600" />
                      <span className="text-sm font-medium text-gray-700 truncate max-w-xs">
                        {file.name}
                      </span>
                      <span className="text-xs text-gray-400">
                        ({(file.size / 1024).toFixed(0)} KB)
                      </span>
                    </div>
                    <button
                      onClick={runAutoGenerate}
                      className="rounded-lg bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 transition flex items-center gap-2"
                    >
                      <Scale className="h-4 w-4" />
                      Pokreni procjenu
                    </button>
                  </div>
                )}
              </>
            )}

            {/* Text Input Mode */}
            {inputMode === "text" && (
              <div className="space-y-4">
                <div className="rounded-xl bg-white border border-gray-200 p-6">
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Opis nekretnine
                  </label>
                  <textarea
                    value={propertyText}
                    onChange={(e) => setPropertyText(e.target.value)}
                    placeholder={"Primjer:\nGrađevinsko zemljište, 450 m², Vir (Zadar), k.o. Vir\nili\nStan, 65 m², Split, Spinut, 3. kat, novogradnja"}
                    rows={8}
                    className="w-full rounded-lg border border-gray-300 px-4 py-3 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 resize-y"
                  />
                  <p className="text-xs text-gray-400 mt-2">
                    Unesite lokaciju, tip nekretnine, kvadraturu i ostale relevantne podatke.
                    Možete zalijepiti tekst iz ZK izvadka, oglasa ili vlastitog opisa.
                  </p>
                </div>

                <div className="flex justify-end">
                  <button
                    onClick={runAutoGenerate}
                    disabled={!canStart}
                    className={`rounded-lg px-6 py-2.5 text-sm font-semibold text-white transition flex items-center gap-2 ${
                      canStart
                        ? "bg-blue-600 hover:bg-blue-700"
                        : "bg-gray-300 cursor-not-allowed"
                    }`}
                  >
                    <Scale className="h-4 w-4" />
                    Pokreni procjenu
                  </button>
                </div>
              </div>
            )}

            {/* How it works */}
            <div className="rounded-xl bg-white border border-gray-200 p-6">
              <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-4">
                Kako funkcionira?
              </h3>
              <div className="space-y-3">
                {[
                  {
                    icon: <FileCheck className="h-5 w-5 text-blue-600" />,
                    label: "Čitanje PDF-a",
                    desc: "Sustav ekstrahira lokaciju, kvadraturu i tip nekretnine",
                  },
                  {
                    icon: <Search className="h-5 w-5 text-emerald-600" />,
                    label: "Pretraga tržišta",
                    desc: "Automatski pretražuje Njuškalo i Index za slične nekretnine",
                  },
                  {
                    icon: <Brain className="h-5 w-5 text-purple-600" />,
                    label: "Stručna procjena",
                    desc: "Analitički modul obrađuje podatke i generira stručno mišljenje",
                  },
                ].map((step, i) => (
                  <div key={i} className="flex items-start gap-3">
                    <div className="mt-0.5">{step.icon}</div>
                    <div>
                      <p className="text-sm font-medium text-gray-800">
                        {step.label}
                      </p>
                      <p className="text-xs text-gray-500">{step.desc}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* ===================== RESULTS ===================== */}
        {!isLoading && result && (
          <div className="space-y-6">
            {/* Value Card */}
            <div className="rounded-xl bg-gradient-to-r from-blue-600 to-blue-700 p-8 text-center text-white shadow-lg">
              <p className="text-sm uppercase tracking-wide opacity-80 mb-1">
                Procijenjena tržišna vrijednost
              </p>
              <p className="text-4xl font-bold">
                {result.estimated_value_eur.toLocaleString("hr-HR")} €
              </p>
              {/* V4 Math Confidence (primary) */}
              {result.math_confidence_score != null && (
                <div className="mt-4 mx-auto max-w-md">
                  <div className="flex items-center justify-center gap-2 mb-2">
                    <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                      result.math_confidence_score >= 75
                        ? "bg-green-500/20 text-green-100"
                        : result.math_confidence_score >= 50
                        ? "bg-yellow-500/20 text-yellow-100"
                        : "bg-red-500/20 text-red-100"
                    }`}>
                      🧮 Engine: {result.math_confidence_level} ({result.math_confidence_score}/100)
                    </span>
                    {result.confidence_score != null && (
                      <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                        result.confidence_color === "green"
                          ? "bg-green-500/20 text-green-100"
                          : result.confidence_color === "yellow"
                          ? "bg-yellow-500/20 text-yellow-100"
                          : "bg-red-500/20 text-red-100"
                      }`}>
                        🔍 Podaci: {result.confidence_score}/100
                      </span>
                    )}
                  </div>
                  <div className="w-full bg-white/20 rounded-full h-2 mb-2">
                    <div
                      className={`h-2 rounded-full transition-all duration-500 ${
                        result.math_confidence_score >= 75 ? "bg-green-400"
                        : result.math_confidence_score >= 50 ? "bg-yellow-400"
                        : "bg-red-400"
                      }`}
                      style={{ width: `${result.math_confidence_score}%` }}
                    />
                  </div>
                  {result.math_confidence_explanation && (
                    <p className="text-xs opacity-70">{result.math_confidence_explanation}</p>
                  )}
                </div>
              )}
              {/* Fallback to V3 confidence if no V4 */}
              {result.math_confidence_score == null && result.confidence_level && (
                <div className="mt-4 mx-auto max-w-md">
                  <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${
                    result.confidence_color === "green"
                      ? "bg-green-500/20 text-green-100"
                      : result.confidence_color === "yellow"
                      ? "bg-yellow-500/20 text-yellow-100"
                      : "bg-red-500/20 text-red-100"
                  }`}>
                    Pouzdanost: {result.confidence_level}
                    {result.confidence_score ? ` (${result.confidence_score}/100)` : ""}
                  </span>
                </div>
              )}
              {result.calculated_price_per_m2 && (
                <p className="text-sm mt-2 opacity-80">
                  {result.calculated_price_per_m2.toLocaleString("hr-HR")} €/m²
                </p>
              )}
            </div>

            {/* Engine Analytics Panel (V4) */}
            {result.market_average_asking && (
              <div className="rounded-xl bg-white border border-gray-200 p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                  📊 Analitika procjene (Robust Estimator V4)
                </h3>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                  <div className="text-center p-3 bg-gray-50 rounded-lg">
                    <p className="text-xs text-gray-500">Weighted Median</p>
                    <p className="text-lg font-bold text-gray-900">
                      {(result.market_average_asking ?? 0).toLocaleString("hr-HR")} €/m²
                    </p>
                    <p className="text-xs text-gray-400">{result.price_source}</p>
                  </div>
                  <div className="text-center p-3 bg-gray-50 rounded-lg">
                    <p className="text-xs text-gray-500">Comparables</p>
                    <p className="text-lg font-bold text-gray-900">
                      {result.usable_price_points ?? 0} / {result.total_listings_found ?? 0}
                    </p>
                    <p className="text-xs text-gray-400">
                      {result.outliers_removed ?? 0} outliera ({result.outlier_method ?? "N/A"})
                    </p>
                  </div>
                  <div className="text-center p-3 bg-gray-50 rounded-lg">
                    <p className="text-xs text-gray-500">StdDev / CV</p>
                    <p className="text-lg font-bold text-gray-900">
                      {(result.std_dev_per_m2 ?? 0).toLocaleString("hr-HR")} €/m²
                    </p>
                    <p className={`text-xs font-medium ${
                      (result.coefficient_of_variation ?? 0) < 15 ? "text-green-600"
                      : (result.coefficient_of_variation ?? 0) < 30 ? "text-yellow-600"
                      : "text-red-600"
                    }`}>
                      CV: {result.coefficient_of_variation ?? 0}%
                    </p>
                  </div>
                </div>
                {/* Realization Gap Reasoning */}
                {result.realization_gap_reasoning && (
                  <div className="mt-3 p-3 bg-blue-50 border border-blue-200 rounded-lg">
                    <p className="text-xs font-semibold text-blue-700 mb-1">
                      Realizacijski gap: {result.realization_discount_percent ?? 0}%
                    </p>
                    <p className="text-xs text-blue-600">{result.realization_gap_reasoning}</p>
                  </div>
                )}
                {result.note && (
                  <div className="mt-3 flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-lg">
                    <AlertTriangle className="h-4 w-4 text-amber-600 flex-shrink-0 mt-0.5" />
                    <p className="text-xs text-amber-800">{result.note}</p>
                  </div>
                )}
              </div>
            )}

            {/* V4 Used Comparables Table (Engine) */}
            {result.used_comparables && result.used_comparables.length > 0 && (() => {
              // Sort: used (by similarity desc) first, then outliers
              const sorted = [...result.used_comparables].sort((a, b) => {
                if (a.is_outlier !== b.is_outlier) return a.is_outlier ? 1 : -1;
                return b.similarity_score - a.similarity_score;
              });
              const usedCount = sorted.filter(c => !c.is_outlier).length;
              const outlierCount = sorted.filter(c => c.is_outlier).length;
              // Calculate median of used comparables for sanity display
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
            {result.adjustments && result.adjustments.length > 0 && (
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
                      {result.adjustments.map((a, i) => (
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
                            (result.total_adjustment_percent ?? 0) >= 0 ? "bg-green-100 text-green-800" : "bg-red-100 text-red-800"
                          }`}>
                            {(result.total_adjustment_percent ?? 0) > 0 ? "+" : ""}{result.total_adjustment_percent ?? 0}%
                          </span>
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </div>
            )}

            {/* V4 Pipeline Trace (collapsible) */}
            {result.pipeline && result.pipeline.length > 0 && (
              <details className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                <summary className="px-5 py-3 border-b border-gray-100 bg-gray-50 cursor-pointer">
                  <span className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                    🔬 Pipeline Trace ({result.pipeline.length} koraka)
                  </span>
                </summary>
                <div className="px-5 py-3 space-y-1">
                  {result.pipeline.map((step, i) => (
                    <div key={i} className="flex items-start gap-2">
                      <span className="text-xs text-gray-400 font-mono w-6 text-right flex-shrink-0">{i + 1}.</span>
                      <span className="text-xs text-gray-600">{step}</span>
                    </div>
                  ))}
                </div>
                {result.outlier_log && result.outlier_log.length > 0 && (
                  <div className="px-5 py-3 border-t border-gray-100 bg-gray-50/50">
                    <p className="text-xs font-semibold text-gray-500 mb-1">Outlier Log:</p>
                    {result.outlier_log.map((log, i) => (
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
                {result.target_property_summary}
              </p>
            </div>

            {/* Selected Comparables */}
            {result.selected_comparables.length > 0 && (
              <div className="rounded-xl bg-white border border-gray-200 overflow-hidden">
                <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
                  <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                    Odabrane usporedive nekretnine
                  </h3>
                </div>
                <div className="divide-y divide-gray-100">
                  {result.selected_comparables.map((c, i) => (
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
            {result.value_drivers && result.value_drivers.length > 0 && (
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
                        <th className="text-left px-5 py-3 font-semibold text-gray-600">
                          Faktor
                        </th>
                        <th className="text-left px-5 py-3 font-semibold text-gray-600">
                          Utjecaj
                        </th>
                        <th className="text-right px-5 py-3 font-semibold text-gray-600">
                          Korekcija
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {result.value_drivers.map((d, i) => (
                        <tr key={i} className="hover:bg-gray-50 transition-colors">
                          <td className="px-5 py-3 text-gray-800 font-medium">
                            {d.factor}
                          </td>
                          <td className="px-5 py-3 text-gray-600">
                            {d.impact}
                          </td>
                          <td className="px-5 py-3 text-right">
                            <span
                              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                                d.adjustment_percent > 0
                                  ? "bg-green-100 text-green-800"
                                  : d.adjustment_percent < 0
                                  ? "bg-red-100 text-red-800"
                                  : "bg-gray-100 text-gray-600"
                              }`}
                            >
                              {d.adjustment_percent > 0 ? "+" : ""}
                              {d.adjustment_percent}%
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      <tr className="border-t-2 border-gray-200 bg-gray-50">
                        <td
                          colSpan={2}
                          className="px-5 py-3 text-gray-800 font-semibold"
                        >
                          Ukupna korekcija
                        </td>
                        <td className="px-5 py-3 text-right">
                          <span
                            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ${
                              result.value_drivers.reduce(
                                (sum, d) => sum + d.adjustment_percent,
                                0
                              ) >= 0
                                ? "bg-green-100 text-green-800"
                                : "bg-red-100 text-red-800"
                            }`}
                          >
                            {result.value_drivers.reduce(
                              (sum, d) => sum + d.adjustment_percent,
                              0
                            ) > 0
                              ? "+"
                              : ""}
                            {result.value_drivers.reduce(
                              (sum, d) => sum + d.adjustment_percent,
                              0
                            )}
                            %
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
                <ResultActions
                  copyText={result.valuation_reasoning}
                />
              </div>
              <div className="px-6 py-5">
                <div className="prose prose-sm max-w-none text-gray-800 whitespace-pre-wrap leading-relaxed">
                  {result.valuation_reasoning}
                </div>
              </div>
            </div>

            {/* Actions */}
            <div className="flex justify-end">
              <button
                onClick={reset}
                className="rounded-lg bg-gray-800 px-6 py-2.5 text-sm font-semibold text-white hover:bg-gray-900 transition"
              >
                Nova procjena
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
    </>
  );
}
