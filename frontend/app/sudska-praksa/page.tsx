"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Scale,
  Upload,
  FileText,
  Loader2,
  Gavel,
  Target,
  Shield,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Clock,
  Coins,
  ChevronDown,
  ChevronUp,
  ThumbsUp,
  ThumbsDown,
  Minus,
  Zap,
  BookOpen,
  TrendingUp,
  CircleDot,
  ArrowRight,
  Info,
  BarChart3,
  ClipboardList,
  Send,
} from "lucide-react";
import PrecedentSidebar from "@/components/PrecedentSidebar";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import ResultActions from "@/components/ResultActions";
import { generateLegalPdf } from "@/lib/generateModulePdfs";
import { NormBadge, CitedNormsList, TemporalDatePicker, CitationModeToggle } from "@/components/NormBadge";
import { type CitedNorm, fetchTemporalBatch, type TemporalNormResult } from "@/lib/normApi";
import { useUserId } from "@/lib/useUserId";

import { API_BASE } from "@/lib/api";
const API = API_BASE;

// ── Types matching backend LegalStrategyResult (V34) ──

interface CourtPrecedent {
  case_number: string;
  court: string;
  date: string;
  summary: string;
  legal_principle: string;
  relevance_to_case: string;
  alignment: string;
  legal_basis: string;
  stability: string;      // V34: "STABILNA" | "PODIJELJENA" | "NOVA"
  similarity_score: number; // V34: 0-100
}

interface LegalRisk {
  title: string;
  description: string;
  severity: string;
  mitigation: string;
  case_element: string;    // V34: contextual anchor
}

interface StrategicRecommendation {
  action: string;
  rationale: string;
  priority: string;
  timing: string;
}

// V34: Score Breakdown — transparent scoring methodology
interface ScoreBreakdown {
  favorable_count: number;
  neutral_count: number;
  unfavorable_count: number;
  practice_stability: string;
  legal_qualification: string;
  precedent_score: number;
  qualitative_score: number;
  methodology: string;
}

// V34: Confidence Factor — what increases/decreases confidence
interface ConfidenceFactor {
  factor: string;
  direction: string;  // "POZITIVNO" | "NEGATIVNO"
  explanation: string;
}

// V45: AI Act Audit Log Entry
interface AuditLogEntry {
  step: number;
  action: string;
  legal_basis: string;
  citation: string;
  reasoning: string;
  confidence: string; // "VISOKO" | "SREDNJE" | "NISKO"
  timestamp: string;
}

interface LegalStrategyResult {
  key_legal_issue: string;
  sub_issues: string[];
  precedents: CourtPrecedent[];
  win_probability: number;
  win_probability_min: number;  // V34: range lower bound
  win_probability_max: number;  // V34: range upper bound
  win_probability_label: string;
  probability_reasoning: string;
  score_breakdown?: ScoreBreakdown;       // V34
  confidence_factors?: ConfidenceFactor[]; // V34
  data_provenance?: string;               // V34
  risks: LegalRisk[];
  strengths: string[];
  recommendations: StrategicRecommendation[];
  strategy_summary: string;
  estimated_duration: string;
  estimated_cost: string;
  cited_norms?: CitedNorm[];
  audit_trail?: AuditLogEntry[];  // V45: AI Act transparency
  disclaimer: string;
}

// ── SSE message types ──
interface SseMessage {
  type: string;
  data: {
    stage?: string;
    status?: string;
    label?: string;
    detail?: string;
    message?: string;
    [key: string]: unknown;
  };
}

// ── Progress stage component ──
function ProgressStage({
  label,
  detail,
  status,
}: {
  label: string;
  detail?: string;
  status: "pending" | "active" | "done" | "error";
}) {
  return (
    <div className="flex items-center gap-3">
      <div className="shrink-0">
        {status === "done" && (
          <CheckCircle className="h-5 w-5 text-green-500" />
        )}
        {status === "active" && (
          <Loader2 className="h-5 w-5 text-indigo-500 animate-spin" />
        )}
        {status === "pending" && (
          <CircleDot className="h-5 w-5 text-slate-300" />
        )}
        {status === "error" && (
          <XCircle className="h-5 w-5 text-red-500" />
        )}
      </div>
      <div>
        <p
          className={`text-sm font-medium ${
            status === "active"
              ? "text-indigo-700"
              : status === "done"
                ? "text-green-700"
                : status === "error"
                  ? "text-red-700"
                  : "text-slate-400"
          }`}
        >
          {label}
        </p>
        {detail && (
          <p className="text-xs text-slate-400">{detail}</p>
        )}
      </div>
    </div>
  );
}

// ── Win probability gauge (V34: shows range) ──
function WinGauge({
  probability,
  probabilityMin,
  probabilityMax,
  label,
}: {
  probability: number;
  probabilityMin?: number;
  probabilityMax?: number;
  label: string;
}) {
  const color =
    probability >= 70
      ? "text-green-600"
      : probability >= 40
        ? "text-amber-600"
        : "text-red-600";
  const bgColor =
    probability >= 70
      ? "bg-green-500"
      : probability >= 40
        ? "bg-amber-500"
        : "bg-red-500";
  const ringColor =
    probability >= 70
      ? "ring-green-200"
      : probability >= 40
        ? "ring-amber-200"
        : "ring-red-200";

  const min = probabilityMin ?? probability;
  const max = probabilityMax ?? probability;
  const hasRange = min !== max;

  return (
    <div className="flex flex-col items-center">
      <div
        className={`relative w-32 h-32 rounded-full bg-white ring-8 ${ringColor} flex items-center justify-center shadow-lg`}
      >
        <div className="text-center">
          {hasRange ? (
            <>
              <span className={`text-lg font-bold ${color}`}>
                {min}-{max}
              </span>
              <span className={`text-sm font-bold ${color}`}>%</span>
            </>
          ) : (
            <>
              <span className={`text-4xl font-black ${color}`}>
                {probability}
              </span>
              <span className={`text-lg font-bold ${color}`}>%</span>
            </>
          )}
        </div>
        {/* Progress arc */}
        <svg
          className="absolute inset-0 w-full h-full -rotate-90"
          viewBox="0 0 128 128"
        >
          <circle
            cx="64"
            cy="64"
            r="56"
            fill="none"
            stroke="#e2e8f0"
            strokeWidth="6"
          />
          {/* Range area (lighter) */}
          {hasRange && (
            <circle
              cx="64"
              cy="64"
              r="56"
              fill="none"
              className={bgColor.replace("bg-", "stroke-")}
              strokeWidth="6"
              strokeDasharray={`${(max / 100) * 352} 352`}
              strokeLinecap="round"
              opacity={0.25}
            />
          )}
          {/* Midpoint arc */}
          <circle
            cx="64"
            cy="64"
            r="56"
            fill="none"
            className={bgColor.replace("bg-", "stroke-")}
            strokeWidth="6"
            strokeDasharray={`${(probability / 100) * 352} 352`}
            strokeLinecap="round"
          />
        </svg>
      </div>
      <p className={`text-sm font-bold mt-3 ${color}`}>
        {label || "Procjena"}
      </p>
      {hasRange ? (
        <p className="text-xs text-slate-400">
          Raspon: {min}% – {max}%
        </p>
      ) : (
        <p className="text-xs text-slate-400">Vjerojatnost uspjeha</p>
      )}
    </div>
  );
}

// ── Alignment badge ──
function AlignmentBadge({ alignment }: { alignment: string }) {
  const upper = alignment?.toUpperCase() ?? "";
  if (upper === "POVOLJNA")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-full">
        <ThumbsUp className="h-3 w-3" /> Povoljna
      </span>
    );
  if (upper === "NEPOVOLJNA")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-full">
        <ThumbsDown className="h-3 w-3" /> Nepovoljna
      </span>
    );
  return (
    <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-slate-600 bg-slate-100 px-2 py-0.5 rounded-full">
      <Minus className="h-3 w-3" /> Neutralna
    </span>
  );
}

// ── Severity badge ──
function SeverityBadge({ severity }: { severity: string }) {
  const upper = severity?.toUpperCase() ?? "";
  if (upper === "VISOK")
    return (
      <span className="text-[10px] font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-full">
        VISOK
      </span>
    );
  if (upper === "NIZAK")
    return (
      <span className="text-[10px] font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-full">
        NIZAK
      </span>
    );
  return (
    <span className="text-[10px] font-semibold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
      SREDNJI
    </span>
  );
}

// ── Priority badge ──
function PriorityBadge({ priority }: { priority: string }) {
  const upper = priority?.toUpperCase() ?? "";
  if (upper === "HITNO")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-full">
        <Zap className="h-3 w-3" /> HITNO
      </span>
    );
  if (upper === "VAŽNO")
    return (
      <span className="text-[10px] font-semibold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
        VAŽNO
      </span>
    );
  return (
    <span className="text-[10px] font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded-full">
      PREPORUČENO
    </span>
  );
}

// ── V34: Stability badge — how settled the legal position is ──
function StabilityBadge({ stability }: { stability: string }) {
  const upper = stability?.toUpperCase() ?? "";
  if (upper === "STABILNA")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-full">
        <Shield className="h-3 w-3" /> Stabilna
      </span>
    );
  if (upper === "PODIJELJENA")
    return (
      <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
        <AlertTriangle className="h-3 w-3" /> Podijeljena
      </span>
    );
  return (
    <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-purple-700 bg-purple-50 px-2 py-0.5 rounded-full">
      <Info className="h-3 w-3" /> Nova praksa
    </span>
  );
}

// ═════════════════════════════════════════════════════════
//  MAIN PAGE COMPONENT
// ═════════════════════════════════════════════════════════

const LEGAL_STAGE_ICONS: Record<string, React.ReactNode> = {
  preparing: <FileText className="h-4 w-4" />,
  analyzing: <Scale className="h-4 w-4" />,
  precedents: <BookOpen className="h-4 w-4" />,
  strategy: <Target className="h-4 w-4" />,
};
const LEGAL_STAGE_MICROCOPY: Record<string, string[]> = {
  preparing: ["Pripremam kontekst...", "Čitam podatke slučaja..."],
  analyzing: ["Analiziram pravno pitanje...", "Identificiram pod-pitanja...", "Klasificiram pravni spor..."],
  precedents: ["Pretražujem sudsku praksu...", "Analiziram relevantne presude...", "Procjenjujem stabilnost prakse..."],
  strategy: ["Procjenjujem šanse za uspjeh...", "Formuliram preporuke...", "Izračunavam vjerojatnost..."],
};
const INITIAL_LEGAL_STAGES: ProcessingStage[] = [
  { id: "preparing", label: "Priprema", status: "pending" },
  { id: "analyzing", label: "Pravna analiza", status: "pending" },
  { id: "precedents", label: "Sudska praksa", status: "pending" },
  { id: "strategy", label: "Strategija i prognoza", status: "pending" },
];

export default function ProvjeriIshodPage() {
  const userId = useUserId();
  // Input state
  const [caseDescription, setCaseDescription] = useState("");
  const [contractText, setContractText] = useState("");
  const [chronologySummary, setChronologySummary] = useState("");
  const [zkSummary, setZkSummary] = useState("");
  const [clientPosition, setClientPosition] = useState("kupac");
  const [desiredOutcome, setDesiredOutcome] = useState("");

  // Processing state
  const [loading, setLoading] = useState(false);
  const [stages, setStages] = useState<
    Record<string, { label: string; detail?: string; status: string }>
  >({});
  const [result, setResult] = useState<LegalStrategyResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  // V58: Stage-based progress for SharedProcessingView
  const [processingStages, setProcessingStages] = useState<ProcessingStage[]>([]);
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");

  // UI state
  const [expandedPrecedent, setExpandedPrecedent] = useState<number | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<"text" | "pdf">("text");
  const fileRef = useRef<HTMLInputElement>(null);
  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);
  // V42: Norm Validity Engine state
  const [eventDate, setEventDate] = useState("");
  const [courtCitationMode, setCourtCitationMode] = useState(true);
  const [temporalResults, setTemporalResults] = useState<TemporalNormResult[]>([]);

  const resetAnalysis = () => {
    setResult(null);
    setStages({});
    setProcessingStages([]);
    setError(null);
    setLoading(false);
    setViewState("INPUT");
  };

  // ── Submit via SSE stream ──
  const submitAnalysis = async () => {
    if (activeTab === "pdf" && uploadedFiles.length > 0) {
      await submitPdfAnalysis();
      return;
    }

    const hasContent =
      caseDescription.trim() ||
      contractText.trim() ||
      chronologySummary.trim() ||
      zkSummary.trim();
    if (!hasContent) {
      setError("Molimo unesite barem opis slučaja.");
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);
    setStages({});
    setProcessingStages(INITIAL_LEGAL_STAGES.map((s) => ({ ...s })));
    setViewState("PROCESSING");

    const knownStages = new Set(INITIAL_LEGAL_STAGES.map((s) => s.id));

    try {
      const response = await fetch(`${API}/api/legal/strategy/stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(userId ? { "x-user-id": userId } : {}) },
        body: JSON.stringify({
          case_description: caseDescription,
          contract_text: contractText || null,
          chronology_summary: chronologySummary || null,
          zk_summary: zkSummary || null,
          client_position: clientPosition,
          desired_outcome: desiredOutcome,
        }),
      });

      if (!response.ok) {
        const errData = await response.json().catch(() => ({}));
        throw new Error(
          (errData as Record<string, string>)?.error ??
            "Greška u analizi"
        );
      }

      const reader = response.body?.getReader();
      if (!reader) throw new Error("Nema streama");

      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          try {
            const msg: SseMessage = JSON.parse(line.slice(6));

            if (msg.type === "progress" && msg.data.stage) {
              setStages((prev) => ({
                ...prev,
                [msg.data.stage!]: {
                  label: msg.data.label ?? "",
                  detail: msg.data.detail ?? undefined,
                  status: msg.data.status ?? "active",
                },
              }));
              // Map to processing stages for SharedProcessingView
              const stageId = msg.data.stage!;
              if (knownStages.has(stageId)) {
                setProcessingStages((prev) =>
                  prev.map((s) =>
                    s.id === stageId
                      ? { ...s, status: (msg.data.status ?? "active") as ProcessingStage["status"], label: msg.data.label || s.label, detail: msg.data.detail ?? s.detail }
                      : s
                  )
                );
              }
            }

            if (msg.type === "result") {
              setProcessingStages((prev) => prev.map((s) => ({ ...s, status: "done" as const })));
              setResult(msg.data as unknown as LegalStrategyResult);
            }

            if (msg.type === "error") {
              setError(msg.data.message ?? "Nepoznata greška");
            }
          } catch {
            // Skip unparseable lines
          }
        }
      }

      if (!error) setViewState("RESULT");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Nepoznata greška"
      );
    } finally {
      setLoading(false);
    }
  };

  // ── Submit via PDF upload ──
  const submitPdfAnalysis = async () => {
    if (uploadedFiles.length === 0) {
      setError("Molimo uploadajte barem jednu PDF datoteku.");
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);
    setProcessingStages(INITIAL_LEGAL_STAGES.map((s) => ({ ...s, status: "active" as const })));
    setViewState("PROCESSING");

    try {
      const formData = new FormData();
      uploadedFiles.forEach((f) => formData.append("files", f));
      if (clientPosition) formData.append("clientPosition", clientPosition);
      if (desiredOutcome) formData.append("desiredOutcome", desiredOutcome);
      if (caseDescription) formData.append("additionalContext", caseDescription);

      const res = await fetch(`${API}/api/legal/strategy-pdf`, {
        method: "POST",
        body: formData,
        headers: userId ? { "x-user-id": userId } : {},
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(
          (errData as Record<string, string>)?.error ??
            "Greška u analizi"
        );
      }

      const data: LegalStrategyResult = await res.json();
      setProcessingStages((prev) => prev.map((s) => ({ ...s, status: "done" as const })));
      setResult(data);
      setViewState("RESULT");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Nepoznata greška"
      );
    } finally {
      setLoading(false);
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []).filter((f) =>
      f.name.toLowerCase().endsWith(".pdf")
    );
    setUploadedFiles(files);
  };

  // ── Build context for sidebar ──
  const sidebarContext = [
    caseDescription,
    result?.key_legal_issue,
    result?.strategy_summary,
  ]
    .filter(Boolean)
    .join("\n");

  return (
    <>
      {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
      {viewState === "PROCESSING" && (
        <SharedProcessingView
          stages={processingStages}
          isStreaming={loading}
          error={error}
          onCancel={resetAnalysis}
          onReset={resetAnalysis}
          title="Analiza u tijeku"
          subtitle="AI pretražuje sudsku praksu i procjenjuje šanse"
          headerIcon={<Gavel className="h-7 w-7 text-white" />}
          stageIcons={LEGAL_STAGE_ICONS}
          stageMicrocopy={LEGAL_STAGE_MICROCOPY}
          theme="indigo"
        />
      )}

    <div className={`min-h-screen bg-slate-50 font-sans ${viewState === "PROCESSING" ? "hidden" : ""}`}>
      {/* Header */}
      <header className="bg-slate-900 text-white">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Link
              href="/"
              className="p-1.5 rounded-lg hover:bg-slate-800 transition"
            >
              <ArrowLeft className="h-5 w-5" />
            </Link>
            <div className="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center">
              <Gavel className="h-5 w-5 text-white" />
            </div>
            <div>
              <h1 className="text-lg font-bold tracking-tight">
                Provjeri Ishod na Sudu
              </h1>
              <p className="text-[10px] text-slate-400 uppercase tracking-widest">
                AI Prognoza + Sudska Praksa
              </p>
            </div>
          </div>
          {result && (
            <button
              onClick={() => setSidebarOpen(true)}
              className="text-xs text-slate-400 hover:text-white transition flex items-center gap-1 bg-slate-800 px-3 py-2 rounded-lg"
            >
              <BookOpen className="h-3.5 w-3.5" />
              Sudska praksa
            </button>
          )}
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-4 sm:px-6 py-6">
        {viewState !== "RESULT" || !result ? (
          /* ═══════════ INPUT FORM ═══════════ */
          <div className="space-y-6">
            {/* Tab selector */}
            <div className="flex gap-2">
              <button
                onClick={() => setActiveTab("text")}
                className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                  activeTab === "text"
                    ? "bg-indigo-600 text-white"
                    : "bg-white text-slate-600 border border-slate-200 hover:border-indigo-300"
                }`}
              >
                <FileText className="h-4 w-4 inline mr-2" />
                Tekst
              </button>
              <button
                onClick={() => setActiveTab("pdf")}
                className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                  activeTab === "pdf"
                    ? "bg-indigo-600 text-white"
                    : "bg-white text-slate-600 border border-slate-200 hover:border-indigo-300"
                }`}
              >
                <Upload className="h-4 w-4 inline mr-2" />
                PDF Upload
              </button>
            </div>

            {/* Client position + Desired outcome */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                  Pozicija klijenta
                </label>
                <select
                  value={clientPosition}
                  onChange={(e) => setClientPosition(e.target.value)}
                  className="w-full text-sm border border-slate-300 rounded-lg px-3 py-2.5 focus:ring-2 focus:ring-indigo-300 outline-none"
                >
                  <option value="kupac">Kupac</option>
                  <option value="prodavatelj">Prodavatelj</option>
                  <option value="zakupac">Zakupac / Najmoprimac</option>
                  <option value="zakupodavac">Zakupodavac / Najmodavac</option>
                  <option value="suvlasnik">Suvlasnik</option>
                  <option value="tužitelj">Tužitelj</option>
                  <option value="tuženik">Tuženik</option>
                  <option value="investitor">Investitor / Graditelj</option>
                  <option value="izvođač">Izvođač radova</option>
                </select>
              </div>

              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                  Željeni ishod
                </label>
                <input
                  type="text"
                  value={desiredOutcome}
                  onChange={(e) => setDesiredOutcome(e.target.value)}
                  placeholder="Npr. 'Raskid ugovora i povrat kapare u dvostrukom iznosu'"
                  className="w-full text-sm border border-slate-300 rounded-lg px-3 py-2.5 focus:ring-2 focus:ring-indigo-300 outline-none"
                />
              </div>
            </div>

            {activeTab === "text" ? (
              /* ── Text input mode ── */
              <div className="space-y-4">
                {/* Main case description */}
                <div className="bg-white rounded-xl border border-slate-200 p-5">
                  <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                    Opis slučaja *
                  </label>
                  <textarea
                    value={caseDescription}
                    onChange={(e) => setCaseDescription(e.target.value)}
                    placeholder="Opišite činjenice i okolnosti slučaja. Npr. 'Klijent je sklopio predugovor 15.03.2024. za kupnju stana u Zagrebu za 180.000 EUR. Platio je kaparu od 18.000 EUR. Prodavatelj je propustio rok za predaju stana...'"
                    rows={6}
                    className="w-full text-sm border border-slate-300 rounded-xl px-4 py-3 resize-none focus:ring-2 focus:ring-indigo-300 outline-none"
                  />
                </div>

                {/* Optional context fields */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="bg-white rounded-xl border border-slate-200 p-5">
                    <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                      Tekst ugovora (opcionalno)
                    </label>
                    <textarea
                      value={contractText}
                      onChange={(e) => setContractText(e.target.value)}
                      placeholder="Zalijepite tekst ugovora..."
                      rows={4}
                      className="w-full text-xs border border-slate-300 rounded-lg px-3 py-2 resize-none focus:ring-2 focus:ring-indigo-300 outline-none"
                    />
                  </div>

                  <div className="bg-white rounded-xl border border-slate-200 p-5">
                    <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                      Kronologija (opcionalno)
                    </label>
                    <textarea
                      value={chronologySummary}
                      onChange={(e) => setChronologySummary(e.target.value)}
                      placeholder="Sažetak kronologije događaja..."
                      rows={4}
                      className="w-full text-xs border border-slate-300 rounded-lg px-3 py-2 resize-none focus:ring-2 focus:ring-indigo-300 outline-none"
                    />
                  </div>

                  <div className="bg-white rounded-xl border border-slate-200 p-5">
                    <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                      ZK podaci (opcionalno)
                    </label>
                    <textarea
                      value={zkSummary}
                      onChange={(e) => setZkSummary(e.target.value)}
                      placeholder="Informacije iz ZK izvatka..."
                      rows={4}
                      className="w-full text-xs border border-slate-300 rounded-lg px-3 py-2 resize-none focus:ring-2 focus:ring-indigo-300 outline-none"
                    />
                  </div>
                </div>
              </div>
            ) : (
              /* ── PDF upload mode ── */
              <div className="bg-white rounded-xl border border-slate-200 p-8">
                <div className="text-center">
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".pdf"
                    multiple
                    onChange={handleFileChange}
                    className="hidden"
                  />
                  <button
                    onClick={() => fileRef.current?.click()}
                    className="w-full border-2 border-dashed border-slate-300 rounded-xl p-8 hover:border-indigo-400 hover:bg-indigo-50 transition"
                  >
                    <Upload className="h-10 w-10 text-slate-400 mx-auto" />
                    <p className="text-sm font-medium text-slate-600 mt-3">
                      Kliknite za upload PDF-ova
                    </p>
                    <p className="text-xs text-slate-400 mt-1">
                      Ugovor, kronologija, ZK izvadak — sve relevantne
                      dokumente
                    </p>
                  </button>

                  {uploadedFiles.length > 0 && (
                    <div className="mt-4 space-y-2">
                      {uploadedFiles.map((f, i) => (
                        <div
                          key={i}
                          className="flex items-center gap-2 text-sm text-slate-600 bg-slate-50 px-3 py-2 rounded-lg"
                        >
                          <FileText className="h-4 w-4 text-indigo-500" />
                          {f.name}
                          <span className="text-xs text-slate-400 ml-auto">
                            {(f.size / 1024).toFixed(0)} KB
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Additional context for PDF mode */}
                <div className="mt-6">
                  <label className="text-xs font-semibold text-slate-600 uppercase tracking-wider mb-2 block">
                    Dodatni kontekst (opcionalno)
                  </label>
                  <textarea
                    value={caseDescription}
                    onChange={(e) => setCaseDescription(e.target.value)}
                    placeholder="Dodajte bilo kakav kontekst koji nije u dokumentima..."
                    rows={3}
                    className="w-full text-sm border border-slate-300 rounded-lg px-3 py-2 resize-none focus:ring-2 focus:ring-indigo-300 outline-none"
                  />
                </div>
              </div>
            )}

            {/* Error */}
            {error && (
              <div className="p-4 bg-red-50 rounded-xl border border-red-200 flex items-start gap-3">
                <AlertTriangle className="h-5 w-5 text-red-500 shrink-0 mt-0.5" />
                <p className="text-sm text-red-700">{error}</p>
              </div>
            )}

            {/* V42: Temporal Law Mode + Citation Toggle */}
            <div className="space-y-3">
              <TemporalDatePicker
                value={eventDate}
                onChange={setEventDate}
                label="Temporal Law Mode — Datum pravnog događaja"
              />
              <div className="flex items-center justify-between">
                <CitationModeToggle
                  enabled={courtCitationMode}
                  onToggle={setCourtCitationMode}
                />
                <span className="text-[10px] text-gray-400">
                  Citati u sudskom formatu: čl. + st. + NN oznaka
                </span>
              </div>
            </div>

            {/* Submit button */}
            <button
              onClick={submitAnalysis}
              disabled={loading}
              className="w-full py-4 bg-gradient-to-r from-indigo-600 to-purple-600 text-white font-bold rounded-xl hover:from-indigo-700 hover:to-purple-700 disabled:opacity-50 transition shadow-lg flex items-center justify-center gap-3 text-base"
            >
              {loading ? (
                <>
                  <Loader2 className="h-5 w-5 animate-spin" />
                  Analiziram...
                </>
              ) : (
                <>
                  <Gavel className="h-5 w-5" />
                  Provjeri Ishod na Sudu
                </>
              )}
            </button>
          </div>
        ) : (
          /* ═══════════ RESULT VIEW ═══════════ */
          <div className="space-y-6">
            {/* Back button + Actions */}
            <div className="flex items-center justify-between flex-wrap gap-2">
              <button
                onClick={resetAnalysis}
                className="text-xs text-blue-600 hover:underline flex items-center gap-1"
              >
                <ArrowLeft className="h-3 w-3" /> Nova analiza
              </button>
              <ResultActions
                copyText={[
                  `PRAVNA STRATEGIJA — AI analiza`,
                  `Ključno pitanje: ${result.key_legal_issue}`,
                  `Vjerojatnost uspjeha: ${result.win_probability}% (${result.win_probability_label})`,
                  "",
                  result.probability_reasoning,
                  "",
                  `SUDSKA PRAKSA (${result.precedents.length}):`,
                  ...result.precedents.map((p) => `• ${p.case_number} (${p.court}, ${p.date}) — ${p.alignment}, ${p.similarity_score}%`),
                  "",
                  `RIZICI (${result.risks.length}):`,
                  ...result.risks.map((r) => `• [${r.severity}] ${r.title}: ${r.description}`),
                  "",
                  `PREPORUKE (${result.recommendations.length}):`,
                  ...result.recommendations.map((r) => `• [${r.priority}] ${r.action}`),
                  "",
                  `STRATEŠKI SAŽETAK:`,
                  result.strategy_summary,
                ].join("\n")}
                onPdfExport={() => generateLegalPdf(result as Parameters<typeof generateLegalPdf>[0])}
              />
            </div>

            {/* ── Hero: Win Probability ── */}
            <div className="bg-white rounded-2xl border border-slate-200 p-8 shadow-sm">
              <div className="flex flex-col md:flex-row items-center gap-8">
                <WinGauge
                  probability={result.win_probability}
                  probabilityMin={result.win_probability_min}
                  probabilityMax={result.win_probability_max}
                  label={result.win_probability_label}
                />
                <div className="flex-1 space-y-4">
                  <div>
                    <p className="text-[10px] font-semibold text-indigo-600 uppercase tracking-wider mb-1">
                      Ključno pravno pitanje
                    </p>
                    <h2 className="text-xl font-bold text-slate-900 leading-tight">
                      {result.key_legal_issue}
                    </h2>
                    {result.sub_issues.length > 0 && (
                      <div className="flex flex-wrap gap-2 mt-2">
                        {result.sub_issues.map((si, i) => (
                          <span
                            key={i}
                            className="text-[10px] bg-indigo-50 text-indigo-700 px-2 py-1 rounded-full"
                          >
                            {si}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>

                  <p className="text-sm text-slate-600 leading-relaxed">
                    {result.probability_reasoning}
                  </p>

                  <div className="flex gap-4 text-xs">
                    <div className="flex items-center gap-1.5 text-slate-500">
                      <Clock className="h-3.5 w-3.5" />
                      {result.estimated_duration || "N/A"}
                    </div>
                    <div className="flex items-center gap-1.5 text-slate-500">
                      <Coins className="h-3.5 w-3.5" />
                      {result.estimated_cost || "N/A"}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* ── V34: Data Provenance Warning ── */}
            <div className="p-4 bg-blue-50 rounded-xl border border-blue-200">
              <div className="flex items-start gap-2.5">
                <Info className="h-4 w-4 text-blue-600 mt-0.5 shrink-0" />
                <div>
                  <p className="text-[10px] font-bold text-blue-700 uppercase tracking-wider mb-1">
                    Izvor podataka
                  </p>
                  <p className="text-xs text-blue-800 leading-relaxed">
                    {result.data_provenance ||
                      "Rezultati se temelje na AI modelu koji analizira kontekst slučaja u usporedbi s općim načelima sudske prakse. Citirane presude su ILUSTRATIVNE — generirane na temelju ustaljenih pravnih stajališta, a ne pretragom stvarne baze sudskih odluka."}
                  </p>
                </div>
              </div>
            </div>

            {/* ── V34: Score Breakdown Panel ── */}
            {result.score_breakdown && (
              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <h3 className="text-xs font-bold text-slate-800 uppercase tracking-wider mb-4 flex items-center gap-2">
                  <BarChart3 className="h-4 w-4 text-indigo-600" />
                  Metodologija Procjene
                </h3>

                {/* Score bars */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                  {/* Deterministic precedent score */}
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-slate-600 font-medium">Bodovi iz presuda</span>
                      <span className="font-bold text-slate-800">{result.score_breakdown.precedent_score}%</span>
                    </div>
                    <div className="h-2.5 bg-slate-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-indigo-500 rounded-full transition-all"
                        style={{ width: `${result.score_breakdown.precedent_score}%` }}
                      />
                    </div>
                    <p className="text-[10px] text-slate-400">
                      40% ukupne ocjene — deterministički izračun iz usklađenosti presuda
                    </p>
                  </div>

                  {/* AI qualitative */}
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-slate-600 font-medium">AI kvalitativna analiza</span>
                      <span className="font-bold text-slate-800">{result.score_breakdown.qualitative_score}%</span>
                    </div>
                    <div className="h-2.5 bg-slate-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-purple-500 rounded-full transition-all"
                        style={{ width: `${result.score_breakdown.qualitative_score}%` }}
                      />
                    </div>
                    <p className="text-[10px] text-slate-400">
                      60% ukupne ocjene — AI procjena specifičnih činjenica
                    </p>
                  </div>
                </div>

                {/* Precedent alignment counts */}
                <div className="flex items-center gap-4 p-3 bg-slate-50 rounded-lg mb-3">
                  <div className="flex items-center gap-1.5">
                    <div className="w-3 h-3 bg-green-500 rounded-full" />
                    <span className="text-xs text-slate-600">
                      {result.score_breakdown.favorable_count} povoljnih
                    </span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <div className="w-3 h-3 bg-slate-400 rounded-full" />
                    <span className="text-xs text-slate-600">
                      {result.score_breakdown.neutral_count} neutralnih
                    </span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <div className="w-3 h-3 bg-red-500 rounded-full" />
                    <span className="text-xs text-slate-600">
                      {result.score_breakdown.unfavorable_count} nepovoljnih
                    </span>
                  </div>
                  {result.score_breakdown.practice_stability && (
                    <div className="ml-auto">
                      <StabilityBadge stability={result.score_breakdown.practice_stability} />
                    </div>
                  )}
                </div>

                {/* Legal qualification */}
                {result.score_breakdown.legal_qualification && (
                  <div className="p-3 bg-indigo-50 rounded-lg mb-3">
                    <p className="text-[10px] font-semibold text-indigo-600 uppercase tracking-wider mb-1">
                      Pravna kvalifikacija
                    </p>
                    <p className="text-xs text-indigo-900">{result.score_breakdown.legal_qualification}</p>
                  </div>
                )}

                {/* Methodology */}
                {result.score_breakdown.methodology && (
                  <p className="text-[10px] text-slate-400 leading-relaxed">
                    {result.score_breakdown.methodology}
                  </p>
                )}
              </div>
            )}

            {/* ── V34: Confidence Factors ── */}
            {result.confidence_factors && result.confidence_factors.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <h3 className="text-xs font-bold text-slate-800 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <Info className="h-4 w-4 text-slate-500" />
                  Faktori Povjerenja
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                  {result.confidence_factors.map((cf, i) => (
                    <div
                      key={i}
                      className={`flex items-start gap-2 p-3 rounded-lg text-xs ${
                        cf.direction?.toUpperCase() === "POZITIVNO"
                          ? "bg-green-50 text-green-800"
                          : "bg-red-50 text-red-800"
                      }`}
                    >
                      {cf.direction?.toUpperCase() === "POZITIVNO" ? (
                        <CheckCircle className="h-3.5 w-3.5 text-green-500 mt-0.5 shrink-0" />
                      ) : (
                        <AlertTriangle className="h-3.5 w-3.5 text-red-500 mt-0.5 shrink-0" />
                      )}
                      <div>
                        <p className="font-semibold">{cf.factor}</p>
                        <p className="text-[10px] opacity-80">{cf.explanation}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* ── Strategy Summary ── */}
            <div className="bg-gradient-to-r from-indigo-50 to-purple-50 rounded-xl border border-indigo-200 p-6">
              <div className="flex items-start gap-3">
                <Target className="h-5 w-5 text-indigo-600 mt-0.5 shrink-0" />
                <div>
                  <p className="text-xs font-bold text-indigo-700 uppercase tracking-wider mb-2">
                    Strategija
                  </p>
                  <p className="text-sm text-indigo-900 leading-relaxed">
                    {result.strategy_summary}
                  </p>
                </div>
              </div>
            </div>

            {/* ── Two-column: Strengths + Risks ── */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Strengths */}
              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <h3 className="text-xs font-bold text-green-700 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <Shield className="h-4 w-4" /> Snage pozicije
                </h3>
                <div className="space-y-2">
                  {result.strengths.length === 0 ? (
                    <p className="text-xs text-slate-400">
                      Nema identificiranih snaga
                    </p>
                  ) : (
                    result.strengths.map((s, i) => (
                      <div
                        key={i}
                        className="flex items-start gap-2 text-xs text-slate-700"
                      >
                        <CheckCircle className="h-3.5 w-3.5 text-green-500 mt-0.5 shrink-0" />
                        <span>{s}</span>
                      </div>
                    ))
                  )}
                </div>
              </div>

              {/* Risks */}
              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <h3 className="text-xs font-bold text-red-700 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4" /> Rizici
                </h3>
                <div className="space-y-3">
                  {result.risks.length === 0 ? (
                    <p className="text-xs text-slate-400">
                      Nema identificiranih rizika
                    </p>
                  ) : (
                    result.risks.map((r, i) => (
                      <div key={i} className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="text-xs font-semibold text-slate-800">
                            {r.title}
                          </span>
                          <SeverityBadge severity={r.severity} />
                        </div>
                        <p className="text-xs text-slate-600">
                          {r.description}
                        </p>
                        {r.case_element && (
                          <p className="text-[10px] text-slate-500 bg-slate-50 px-2 py-1 rounded italic">
                            Uzrok: {r.case_element}
                          </p>
                        )}
                        {r.mitigation && (
                          <p className="text-[10px] text-blue-600 bg-blue-50 px-2 py-1 rounded">
                            Mitigacija: {r.mitigation}
                          </p>
                        )}
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>

            {/* ── Precedents ── */}
            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
              <div className="px-5 py-4 bg-gradient-to-r from-amber-50 to-orange-50 border-b border-slate-200">
                <h3 className="text-xs font-bold text-slate-800 uppercase tracking-wider flex items-center gap-2">
                  <BookOpen className="h-4 w-4 text-amber-600" />
                  Relevantna Sudska Praksa ({result.precedents.length}{" "}
                  presuda)
                </h3>
              </div>
              <div className="divide-y divide-slate-100">
                {result.precedents.map((p, idx) => (
                  <div key={idx}>
                    <button
                      onClick={() =>
                        setExpandedPrecedent(
                          expandedPrecedent === idx ? null : idx
                        )
                      }
                      className="w-full text-left px-5 py-4 hover:bg-slate-50 transition"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="flex-1">
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="text-sm font-mono font-bold text-slate-800">
                              {p.case_number}
                            </span>
                            <AlignmentBadge alignment={p.alignment} />
                            {p.stability && <StabilityBadge stability={p.stability} />}
                          </div>
                          <p className="text-xs text-slate-600">
                            {p.summary}
                          </p>
                          <div className="flex items-center gap-2 mt-1.5 text-[10px] text-slate-400">
                            <span>{p.court}</span>
                            <span className="text-slate-300">•</span>
                            <span>{p.date}</span>
                            {p.legal_basis && (
                              <>
                                <span className="text-slate-300">•</span>
                                <NormBadge
                                  norm={{
                                    abbreviation: "",
                                    full_name: "",
                                    article: p.legal_basis,
                                    paragraph: "",
                                    point: "",
                                    nn_reference: "",
                                    status: "VAŽEĆI",
                                    formatted_citation: p.legal_basis,
                                    nn_url: "",
                                  }}
                                />
                              </>
                            )}
                            {p.similarity_score > 0 && (
                              <>
                                <span className="text-slate-300">•</span>
                                <span className="text-indigo-500 font-medium">
                                  Sličnost: {p.similarity_score}%
                                </span>
                              </>
                            )}
                          </div>
                        </div>
                        {expandedPrecedent === idx ? (
                          <ChevronUp className="h-4 w-4 text-slate-400 shrink-0" />
                        ) : (
                          <ChevronDown className="h-4 w-4 text-slate-400 shrink-0" />
                        )}
                      </div>
                    </button>

                    {expandedPrecedent === idx && (
                      <div className="px-5 pb-4 space-y-3">
                        <div className="p-3 bg-slate-50 rounded-lg">
                          <p className="text-[10px] font-semibold text-slate-500 mb-1">
                            Ključno pravno načelo
                          </p>
                          <p className="text-xs text-slate-700 leading-relaxed">
                            {p.legal_principle}
                          </p>
                        </div>
                        <div className="p-3 bg-blue-50 rounded-lg">
                          <p className="text-[10px] font-semibold text-blue-600 mb-1">
                            Relevantnost za vaš slučaj
                          </p>
                          <p className="text-xs text-blue-800 leading-relaxed">
                            {p.relevance_to_case}
                          </p>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>

            {/* ── Recommendations ── */}
            <div className="bg-white rounded-xl border border-slate-200 p-5">
              <h3 className="text-xs font-bold text-slate-800 uppercase tracking-wider mb-4 flex items-center gap-2">
                <TrendingUp className="h-4 w-4 text-indigo-600" />
                Strateške Preporuke
              </h3>
              <div className="space-y-4">
                {result.recommendations.map((rec, i) => (
                  <div
                    key={i}
                    className="flex items-start gap-3 p-3 bg-slate-50 rounded-lg"
                  >
                    <div className="w-6 h-6 rounded-full bg-indigo-100 flex items-center justify-center shrink-0 mt-0.5">
                      <span className="text-xs font-bold text-indigo-700">
                        {i + 1}
                      </span>
                    </div>
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="text-sm font-semibold text-slate-800">
                          {rec.action}
                        </span>
                        <PriorityBadge priority={rec.priority} />
                      </div>
                      <p className="text-xs text-slate-600">
                        {rec.rationale}
                      </p>
                      {rec.timing && (
                        <p className="text-[10px] text-slate-400 mt-1 flex items-center gap-1">
                          <Clock className="h-3 w-3" /> {rec.timing}
                        </p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* V42: Cited Norms — Validity Badges */}
            {result.cited_norms && result.cited_norms.length > 0 && (
              <div className="bg-white rounded-xl border border-indigo-200 shadow-sm overflow-hidden">
                <div className="bg-indigo-50 px-6 py-3 border-b border-indigo-200 flex items-center gap-2">
                  <Scale className="h-5 w-5 text-indigo-600" />
                  <h3 className="font-bold text-indigo-800">Citirani propisi</h3>
                  <span className="text-xs text-indigo-400">({result.cited_norms.length})</span>
                </div>
                <div className="px-6 py-4">
                  <CitedNormsList
                    norms={result.cited_norms}
                    temporalResults={temporalResults}
                    variant="cards"
                  />
                </div>
              </div>
            )}

            {/* ═══ V45: AI ACT AUDIT LOG ═══ */}
            {result.audit_trail && result.audit_trail.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
                <div className="bg-gradient-to-r from-slate-800 to-slate-700 px-6 py-4 flex items-center gap-3">
                  <ClipboardList className="h-5 w-5 text-blue-400" />
                  <div>
                    <h3 className="font-bold text-white">Audit Log — Transparentnost AI Odluka</h3>
                    <p className="text-[10px] text-slate-400 mt-0.5">
                      Sukladno Uredbi (EU) 2024/1689 o umjetnoj inteligenciji (AI Act), čl. 13. — Transparentnost
                    </p>
                  </div>
                  <span className="ml-auto text-xs font-mono text-slate-400">
                    {result.audit_trail.length} koraka
                  </span>
                </div>

                <div className="divide-y divide-slate-100">
                  {result.audit_trail.map((entry, i) => (
                    <div key={i} className="px-6 py-4 hover:bg-slate-50 transition">
                      <div className="flex items-start gap-4">
                        {/* Step number */}
                        <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center shrink-0 mt-0.5">
                          <span className="text-xs font-bold text-slate-600">{entry.step}</span>
                        </div>

                        <div className="flex-1 min-w-0">
                          {/* Action */}
                          <p className="text-sm font-semibold text-slate-800 mb-1">
                            {entry.action}
                          </p>

                          {/* Legal basis + confidence */}
                          <div className="flex items-center gap-2 flex-wrap mb-2">
                            {entry.legal_basis && (
                              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-indigo-100 text-indigo-800">
                                {entry.legal_basis}
                              </span>
                            )}
                            <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${
                              entry.confidence === "VISOKO" ? "bg-green-100 text-green-800" :
                              entry.confidence === "NISKO" ? "bg-red-100 text-red-800" :
                              "bg-yellow-100 text-yellow-800"
                            }`}>
                              Pouzdanost: {entry.confidence}
                            </span>
                          </div>

                          {/* Citation */}
                          {entry.citation && (
                            <p className="text-xs text-indigo-600 font-medium mb-1">
                              📎 {entry.citation}
                            </p>
                          )}

                          {/* Reasoning */}
                          {entry.reasoning && (
                            <p className="text-xs text-slate-600 leading-relaxed">
                              {entry.reasoning}
                            </p>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>

                {/* AI Act compliance footer */}
                <div className="px-6 py-3 bg-slate-50 border-t border-slate-200">
                  <p className="text-[10px] text-slate-400 leading-relaxed">
                    🇪🇺 Ovaj audit log generiran je automatski radi sukladnosti s Uredbom (EU) 2024/1689
                    o utvrđivanju usklađenih pravila o umjetnoj inteligenciji (AI Act). Visokorizični AI
                    sustavi u pravnom sektoru moraju osigurati transparentnost u donošenju odluka (čl. 13.-14.).
                  </p>
                </div>
              </div>
            )}

            {/* ── Disclaimer ── */}
            <div className="p-4 bg-amber-50 rounded-xl border border-amber-200">
              <div className="flex items-start gap-2">
                <Scale className="h-4 w-4 text-amber-600 mt-0.5 shrink-0" />
                <p className="text-xs text-amber-800 leading-relaxed">
                  {result.disclaimer}
                </p>
              </div>
            </div>

            {/* Open precedent sidebar button */}
            <button
              onClick={() => setSidebarOpen(true)}
              className="w-full py-3 bg-white text-indigo-600 font-medium rounded-xl border border-indigo-200 hover:bg-indigo-50 transition flex items-center justify-center gap-2 text-sm"
            >
              <BookOpen className="h-4 w-4" />
              Otvori bočnu traku sudske prakse
              <ArrowRight className="h-4 w-4" />
            </button>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="max-w-6xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 text-center space-y-1 text-[11px] text-slate-400">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-1">
            <span>PravoTek v4.0</span>
            <span className="px-2 sm:px-0">
              AI analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11)
            </span>
          </div>
          <p>Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu.</p>
        </div>
      </footer>

      {/* Precedent Sidebar */}
      <PrecedentSidebar
        context={sidebarContext}
        isOpen={sidebarOpen}
        onClose={() => setSidebarOpen(false)}
      />

      {/* Sidebar backdrop */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/30 z-40"
          onClick={() => setSidebarOpen(false)}
        />
      )}
    </div>
    </>
  );
}
