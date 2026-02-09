"use client";

import {
  useState,
  useCallback,
  type DragEvent,
  type ChangeEvent,
} from "react";
import axios from "axios";
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
import { uploadPdfToStorage, saveEvaluation } from "@/lib/supabase";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

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
  regional_benchmark_per_m2?: number;
  market_average_asking?: number;
  calculated_price_per_m2?: number;
  realization_discount_percent?: number;
  total_listings_found?: number;
  usable_price_points?: number;
  outliers_removed?: number;
  price_source?: string;
  region_name?: string;
  note?: string;
  evaluation_id?: string;
  // V3 confidence scoring (market research)
  confidence_score?: number;
  confidence_explanation?: string;
  confidence_color?: string;
  search_phase_used?: string;
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
}

interface AdjustmentV4 {
  factor: string;
  description: string;
  percent: number;
}

type LoadingPhase = "idle" | "reading" | "searching" | "generating" | "done";

const PHASE_LABELS: Record<Exclude<LoadingPhase, "idle" | "done">, string> = {
  reading: "Čitam PDF i ekstrahiram podatke o nekretnini...",
  searching: "Pretražujem Njuškalo i Index Oglase...",
  generating: "Generiram stručnu procjenu vrijednosti...",
};

// --- Component ---

export default function ValuationPage() {
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [phase, setPhase] = useState<LoadingPhase>("idle");
  const [result, setResult] = useState<AutoValuationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const isLoading = phase !== "idle" && phase !== "done";

  // --- File Handling ---

  const handleFile = (f: File | null) => {
    if (f && f.type === "application/pdf") {
      setFile(f);
      setError(null);
      setResult(null);
      setPhase("idle");
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
    if (!file) return;
    setError(null);
    setResult(null);

    setPhase("reading");
    const phaseTimer1 = setTimeout(() => setPhase("searching"), 3000);
    const phaseTimer2 = setTimeout(() => setPhase("generating"), 8000);

    // Upload PDF to Supabase Storage (non-blocking — runs in parallel)
    const pdfUrlPromise = uploadPdfToStorage(file);

    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await axios.post<AutoValuationResult>(
        `${API_BASE}/api/evaluate`,
        formData
      );
      clearTimeout(phaseTimer1);
      clearTimeout(phaseTimer2);
      setResult(res.data);
      setPhase("done");

      // Save evaluation to Supabase (fire-and-forget)
      const pdfUrl = await pdfUrlPromise;
      saveEvaluation({
        property_type: res.data.target_property_summary?.split(",")[0] || "nekretnina",
        location: res.data.target_property_summary || "nepoznato",
        estimated_value: res.data.estimated_value_eur,
        pdf_url: pdfUrl,
        raw_json_output: res.data as unknown as Record<string, unknown>,
      }).catch((e) => console.warn("Failed to save evaluation:", e));
    } catch (err: unknown) {
      clearTimeout(phaseTimer1);
      clearTimeout(phaseTimer2);
      setPhase("idle");
      if (axios.isAxiosError(err)) {
        if (err.response) {
          // Server responded with error
          const serverMsg = err.response.data?.error;
          const status = err.response.status;
          setError(
            serverMsg
              ? `[${status}] ${serverMsg}`
              : `Greška ${status} od servera. Pokušajte ponovno.`
          );
        } else if (err.request) {
          // No response — server down or CORS
          setError(
            "Server ne odgovara. Moguće da se Render restartira (~30s). Pokušajte ponovno za minutu."
          );
        } else {
          setError("Greška pri slanju zahtjeva.");
        }
      } else {
        setError("Neočekivana greška.");
      }
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
    setResult(null);
    setError(null);
    setPhase("idle");
    setCopied(false);
  };

  // --- Render ---

  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
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
        {phase === "idle" && !result && (
          <div className="space-y-6">
            {/* Drop Zone */}
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

            {/* Selected File + Start Button */}
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
                    desc: "AI ekstrahira lokaciju, kvadraturu i tip nekretnine",
                  },
                  {
                    icon: <Search className="h-5 w-5 text-emerald-600" />,
                    label: "Pretraga tržišta",
                    desc: "Automatski pretražuje Njuškalo i Index za slične nekretnine",
                  },
                  {
                    icon: <Brain className="h-5 w-5 text-purple-600" />,
                    label: "AI procjena",
                    desc: "Sudski vještak AI analizira podatke i generira mišljenje",
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

        {/* ===================== LOADING ===================== */}
        {isLoading && (
          <div className="space-y-6">
            <div className="rounded-xl bg-white border border-gray-200 p-8">
              <div className="space-y-4">
                {(["reading", "searching", "generating"] as const).map(
                  (p, i) => {
                    const phases: LoadingPhase[] = [
                      "reading",
                      "searching",
                      "generating",
                    ];
                    const currentIndex = phases.indexOf(phase);
                    const stepIndex = i;
                    const isActive = stepIndex === currentIndex;
                    const isDone = stepIndex < currentIndex;

                    return (
                      <div
                        key={p}
                        className={`flex items-center gap-4 rounded-lg p-4 transition-all ${
                          isActive
                            ? "bg-blue-50 border border-blue-200"
                            : isDone
                            ? "bg-green-50 border border-green-200"
                            : "bg-gray-50 border border-gray-100"
                        }`}
                      >
                        <div className="flex-shrink-0">
                          {isActive ? (
                            <Loader2 className="h-5 w-5 animate-spin text-blue-600" />
                          ) : isDone ? (
                            <Check className="h-5 w-5 text-green-600" />
                          ) : (
                            <div className="h-5 w-5 rounded-full border-2 border-gray-300" />
                          )}
                        </div>
                        <p
                          className={`text-sm font-medium ${
                            isActive
                              ? "text-blue-800"
                              : isDone
                              ? "text-green-800"
                              : "text-gray-400"
                          }`}
                        >
                          {PHASE_LABELS[p]}
                        </p>
                      </div>
                    );
                  }
                )}
              </div>
            </div>
          </div>
        )}

        {/* ===================== RESULTS ===================== */}
        {phase === "done" && result && (
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
            {result.regional_benchmark_per_m2 && (
              <div className="rounded-xl bg-white border border-gray-200 p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                  📊 Analitika procjene (Robust Estimator V4)
                </h3>
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                  <div className="text-center p-3 bg-gray-50 rounded-lg">
                    <p className="text-xs text-gray-500">Regionalni benchmark</p>
                    <p className="text-lg font-bold text-gray-900">
                      {result.regional_benchmark_per_m2.toLocaleString("hr-HR")} €/m²
                    </p>
                    <p className="text-xs text-gray-400">{result.region_name}</p>
                  </div>
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
                          </td>
                          <td className={`px-4 py-2 text-right font-mono text-xs ${c.is_outlier ? "text-red-400 line-through" : ""}`}>
                            {c.price_per_m2.toLocaleString("hr-HR")} €
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
                <button
                  onClick={copyToClipboard}
                  className="flex items-center gap-1 text-sm font-medium text-blue-600 hover:text-blue-800 transition"
                >
                  {copied ? (
                    <>
                      <Check className="h-4 w-4" /> Kopirano!
                    </>
                  ) : (
                    <>
                      <ClipboardCopy className="h-4 w-4" /> Kopiraj u clipboard
                    </>
                  )}
                </button>
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
  );
}
