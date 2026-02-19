"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Upload,
  FileText,
  AlertTriangle,
  AlertCircle,
  AlertOctagon,
  CheckCircle,
  PenLine,
  Shield,
  Scale,
  Quote,
  BookOpen,
  ClipboardCheck,
  Link2,
  ListChecks,
  Calculator,
  Sparkles,
  FileDown,
  X,
  ChevronDown,
  ChevronUp,
} from "lucide-react";
import { NormBadge, CitedNormsList, TemporalDatePicker } from "@/components/NormBadge";
import { type CitedNorm } from "@/lib/normApi";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import ResultActions from "@/components/ResultActions";
import { INITIAL_CONTRACT_STAGES, CONTRACT_STAGE_ICONS, CONTRACT_STAGE_MICROCOPY, type ContractStage } from "./contractConstants";
import { generateContractPdf } from "@/lib/generateModulePdfs";

// ── Types matching backend ContractAnalysisResult (V49) ──

interface ContractIssue {
  title: string;
  description: string;
  quoted_text?: string | null;
  legal_basis?: string | null;
}

interface SuggestedAmendment {
  article_reference: string;
  current_text: string;
  proposed_text: string;
  rationale: string;
}

// V49: Structured checklist item
interface ChecklistItem {
  category: string;
  check: string;
  status: string; // UREĐENO | DJELOMIČNO | NIJE_UREĐENO
  risk: string;   // NIZAK | SREDNJI | VISOK
  finding: string;
  article_ref?: string | null;
  recommendation?: string | null;
}

// V49: Cross-article structural analysis
interface CrossArticleIssue {
  title: string;
  linked_articles: string[];
  description: string;
  risk: string;
}

// V49: Mandatory clause detector
interface MandatoryClauseCheck {
  clause: string;
  present: boolean;
  importance: string; // KRITIČNO | PREPORUČENO
  description: string;
}

// V51: Logical trap detected by reasoning model via OpenRouter
interface LogicalTrap {
  title: string;
  articles: string[];
  quoted_text: string;
  explanation: string;
  legal_basis: string;
  severity: string; // KRITIČNO | VISOK | SREDNJI
}

// V50: Deviation from ideal contract template
interface IdealContractDeviation {
  element: string;
  ideal_text: string;
  actual_text: string;
  severity: string;
  recommendation: string;
}

interface ContractAnalysisResult {
  document_type: string;
  summary: string;
  overall_risk: string;
  risk_score: number;
  red_flags: ContractIssue[];
  warnings: ContractIssue[];
  positives: string[];
  suggested_amendments: SuggestedAmendment[];
  // V49: New sections
  checklist?: ChecklistItem[];
  cross_article_issues?: CrossArticleIssue[];
  mandatory_clauses?: MandatoryClauseCheck[];
  risk_score_breakdown?: string[];
  // V50: Deep reasoning sections
  logical_traps?: LogicalTrap[];
  ideal_contract_deviations?: IdealContractDeviation[];
  cited_norms?: CitedNorm[];
  disclaimer: string;
}

// V58: Generated improved contract types
interface ContractChange {
  article: string;
  change_type: string; // IZMIJENJENO | NOVO | UKLONJENO
  description: string;
  legal_basis: string;
}

interface GeneratedContractResult {
  improved_contract: string;
  changes_summary: ContractChange[];
  disclaimer: string;
  error?: string;
}

// V58: Generation processing stages
const GENERATE_STAGES: ProcessingStage[] = [
  { id: "preparing", label: "Priprema analize", status: "pending" },
  { id: "generating", label: "Generiranje ugovora", status: "pending" },
  { id: "reviewing", label: "Završna provjera", status: "pending" },
];

const GENERATE_STAGE_ICONS: Record<string, React.ReactNode> = {
  preparing: <ListChecks className="h-4 w-4" />,
  generating: <Sparkles className="h-4 w-4" />,
  reviewing: <Shield className="h-4 w-4" />,
};

const GENERATE_STAGE_MICROCOPY = [
  "Analiziram nalaze revizije...",
  "AI pravni redaktor sastavlja poboljšani ugovor...",
  "Primjenjujem sve predložene izmjene...",
  "Dodajem nedostajuće zaštitne klauzule...",
  "Usklađujem proturječne članke...",
  "Provjeravam pravnu konzistentnost...",
];

import { API_BASE } from "@/lib/api";
const API = API_BASE;

export default function ContractAnalysisPage() {
  const [mode, setMode] = useState<"pdf" | "text">("pdf");
  const [textInput, setTextInput] = useState("");
  const [fileName, setFileName] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ContractAnalysisResult | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const selectedFileRef = useRef<File | null>(null);
  // V42: Temporal Law Mode
  const [eventDate, setEventDate] = useState("");
  // V57: SSE streaming state
  const [stages, setStages] = useState<ContractStage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");
  const abortRef = useRef<AbortController | null>(null);
  // V58: Generated contract state
  const [generatedContract, setGeneratedContract] = useState<GeneratedContractResult | null>(null);
  const [generateStages, setGenerateStages] = useState<ProcessingStage[]>([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [generateError, setGenerateError] = useState<string | null>(null);
  const [showGenerated, setShowGenerated] = useState(false);
  const [expandedChanges, setExpandedChanges] = useState(false);
  // Keep the original text for generate endpoint
  const originalTextRef = useRef<string>("");

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      selectedFileRef.current = file;
      setFileName(file.name);
      setError(null);
    }
  };

  const handleAnalyze = async () => {
    setError(null);
    setResult(null);
    setGeneratedContract(null);
    setShowGenerated(false);
    setStages(INITIAL_CONTRACT_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);
    setViewState("PROCESSING");

    // For text mode, save text immediately (PDF mode gets it from SSE source_text event)
    if (mode === "text") originalTextRef.current = textInput;

    const state = { streamedResult: null as ContractAnalysisResult | null };
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      let response: Response;

      if (mode === "pdf") {
        if (!selectedFileRef.current) {
          setError("Molimo odaberite PDF datoteku.");
          setIsStreaming(false);
          setViewState("INPUT");
          return;
        }
        const formData = new FormData();
        formData.append("file", selectedFileRef.current);
        response = await fetch(`${API}/api/contract/analyze-pdf-stream`, {
          method: "POST",
          body: formData,
          signal: controller.signal,
        });
      } else {
        if (!textInput.trim() || textInput.trim().length < 50) {
          setError("Tekst ugovora mora imati barem 50 znakova.");
          setIsStreaming(false);
          setViewState("INPUT");
          return;
        }
        response = await fetch(`${API}/api/contract/analyze-text-stream`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ text: textInput }),
          signal: controller.signal,
        });
      }

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
                s.id === stage ? { ...s, status: "error" as const, error: message } : s
              )
            );
            setError(message);
          } else if (evt.type === "source_text") {
            // V58: Capture extracted text for generate endpoint
            originalTextRef.current = evt.data.text || "";
          } else if (evt.type === "result") {
            state.streamedResult = evt.data;
            setResult(evt.data);
          }
        } catch {
          /* skip malformed */
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
      // Flush remaining buffer
      if (buffer.trim()) processEvent(buffer.trim());

      setIsStreaming(false);
      if (state.streamedResult) setViewState("RESULT");
    } catch (e: unknown) {
      setIsStreaming(false);
      if (e instanceof DOMException && e.name === "AbortError") {
        setError("Analiza je otkazana.");
        setViewState("INPUT");
      } else {
        setError(e instanceof Error ? e.message : "Server ne odgovara. Pokušajte ponovno.");
      }
    } finally {
      abortRef.current = null;
    }
  };

  const cancelAnalysis = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
    setViewState("INPUT");
  };

  const resetAnalysis = () => {
    abortRef.current?.abort();
    setViewState("INPUT");
    setStages([]);
    setResult(null);
    setError(null);
    setIsStreaming(false);
    setGeneratedContract(null);
    setShowGenerated(false);
    setGenerateError(null);
    originalTextRef.current = "";
  };

  // ═══ V58: Generate Improved Contract via SSE ═══
  const handleGenerateContract = async () => {
    if (!result || !originalTextRef.current) {
      setGenerateError("Nema dostupnog teksta ili analize za generiranje ugovora.");
      return;
    }

    setGenerateError(null);
    setGeneratedContract(null);
    setGenerateStages(GENERATE_STAGES.map((s) => ({ ...s })));
    setIsGenerating(true);
    setShowGenerated(true);

    try {
      const response = await fetch(`${API}/api/contract/generate-stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          original_text: originalTextRef.current,
          analysis_result: result,
        }),
      });

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
            setGenerateStages((prev) =>
              prev.map((s) =>
                s.id === stage
                  ? { ...s, status, label: label || s.label, detail: detail ?? s.detail }
                  : s
              )
            );
          } else if (evt.type === "error") {
            setGenerateError(evt.data.message);
          } else if (evt.type === "result") {
            setGeneratedContract(evt.data);
          }
        } catch {
          /* skip malformed */
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
      if (buffer.trim()) processEvent(buffer.trim());
    } catch (e: unknown) {
      setGenerateError(e instanceof Error ? e.message : "Generiranje nije uspjelo.");
    } finally {
      setIsGenerating(false);
    }
  };

  const riskColor = (risk: string) => {
    switch (risk.toLowerCase()) {
      case "visok":
        return "text-red-700 bg-red-50 border-red-200";
      case "srednji":
        return "text-amber-700 bg-amber-50 border-amber-200";
      case "nizak":
        return "text-green-700 bg-green-50 border-green-200";
      default:
        return "text-gray-700 bg-gray-50 border-gray-200";
    }
  };

  const riskBarColor = (score: number) => {
    if (score >= 60) return "bg-red-500";
    if (score >= 30) return "bg-amber-500";
    return "bg-green-500";
  };

  // V49: Status badge styling for checklist
  const statusBadge = (status: string) => {
    switch (status) {
      case "UREĐENO":
        return "bg-green-100 text-green-800 border-green-200";
      case "DJELOMIČNO":
        return "bg-amber-100 text-amber-800 border-amber-200";
      case "NIJE_UREĐENO":
        return "bg-red-100 text-red-800 border-red-200";
      default:
        return "bg-gray-100 text-gray-800 border-gray-200";
    }
  };

  const riskBadge = (risk: string) => {
    switch (risk?.toUpperCase()) {
      case "VISOK":
        return "bg-red-100 text-red-700";
      case "SREDNJI":
        return "bg-amber-100 text-amber-700";
      case "NIZAK":
        return "bg-green-100 text-green-700";
      default:
        return "bg-gray-100 text-gray-600";
    }
  };

  return (
    <>
      {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
      {viewState === "PROCESSING" && (
        <SharedProcessingView
          stages={stages}
          isStreaming={isStreaming}
          error={error}
          onCancel={cancelAnalysis}
          onReset={resetAnalysis}
          title="Analiza ugovora u tijeku"
          subtitle="AI pravni revizor provjerava vaš ugovor"
          headerIcon={<Scale className="h-7 w-7 text-white" />}
          stageIcons={CONTRACT_STAGE_ICONS}
          stageMicrocopy={CONTRACT_STAGE_MICROCOPY}
          theme="emerald"
        />
      )}

    <div className={`min-h-screen bg-gray-50 font-sans ${viewState === "PROCESSING" ? "hidden" : ""}`}>
      {/* Header */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-4xl mx-auto px-4 py-4 flex items-center gap-4">
          <Link
            href="/"
            className="text-gray-400 hover:text-gray-700 transition"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <div className="flex items-center gap-2">
            <Scale className="h-6 w-6 text-emerald-600" />
            <h1 className="text-xl font-bold text-gray-900">
              AI Analizator Ugovora
            </h1>
          </div>
        </div>
      </header>

      <main className="max-w-4xl mx-auto px-4 py-8">
        {/* ── Input Section ── */}
        {viewState === "INPUT" && !result && (
          <div className="space-y-6">
            <div className="text-center mb-8">
              <p className="text-gray-500 text-lg">
                Uploadajte ugovor ili zalijepite tekst — AI &quot;paranoični
                odvjetnik&quot; analizira svaki članak
              </p>
            </div>

            {/* Mode Toggle */}
            <div className="flex justify-center gap-2 mb-6">
              <button
                onClick={() => setMode("pdf")}
                className={`px-5 py-2.5 rounded-lg text-sm font-medium transition ${
                  mode === "pdf"
                    ? "bg-emerald-600 text-white shadow-sm"
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
                    ? "bg-emerald-600 text-white shadow-sm"
                    : "bg-white text-gray-600 border border-gray-300 hover:bg-gray-50"
                }`}
              >
                <FileText className="inline h-4 w-4 mr-1.5 -mt-0.5" />
                Tekst
              </button>
            </div>

            {/* PDF Upload */}
            {mode === "pdf" && (
              <div
                onClick={() => fileRef.current?.click()}
                className="border-2 border-dashed border-gray-300 rounded-xl p-12 text-center cursor-pointer hover:border-emerald-400 hover:bg-emerald-50/30 transition"
              >
                <input
                  ref={fileRef}
                  type="file"
                  accept=".pdf"
                  onChange={handleFileChange}
                  className="hidden"
                />
                <Upload className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                {fileName ? (
                  <p className="text-emerald-700 font-medium">{fileName}</p>
                ) : (
                  <>
                    <p className="text-gray-600 font-medium">
                      Kliknite za upload PDF-a
                    </p>
                    <p className="text-gray-400 text-sm mt-1">
                      Predugovor, Ugovor o kupoprodaji, Aneks...
                    </p>
                  </>
                )}
              </div>
            )}

            {/* Text Input */}
            {mode === "text" && (
              <textarea
                value={textInput}
                onChange={(e) => setTextInput(e.target.value)}
                placeholder="Zalijepite tekst ugovora ovdje..."
                className="w-full h-64 rounded-xl border border-gray-300 p-4 text-sm text-gray-800 resize-y focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
              />
            )}

            {/* Error */}
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700 text-sm">
                {error}
              </div>
            )}

            {/* V42: Temporal Law Mode */}
            <TemporalDatePicker
              value={eventDate}
              onChange={setEventDate}
              label="Datum sklapanja ugovora (za temporal provjeru)"
            />

            {/* Analyze Button */}
            <button
              onClick={handleAnalyze}
              disabled={isStreaming}
              className="w-full py-4 rounded-xl bg-emerald-600 text-white font-semibold text-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center justify-center gap-2"
            >
              <Shield className="h-5 w-5" />
              Analiziraj ugovor
            </button>
          </div>
        )}

        {/* ── Results Section ── */}
        {viewState === "RESULT" && result && (
          <div className="space-y-6">
            {/* Back / New Analysis + Actions */}
            <div className="flex items-center justify-between flex-wrap gap-2">
              <button
                onClick={resetAnalysis}
                className="text-sm text-emerald-600 hover:underline flex items-center gap-1"
              >
                <ArrowLeft className="h-4 w-4" /> Nova analiza
              </button>
              <ResultActions
                copyText={[
                  `ANALIZA UGOVORA — ${result.document_type}`,
                  `Ukupni rizik: ${result.overall_risk} (${result.risk_score}/100)`,
                  result.summary,
                  "",
                  `CRVENE ZASTAVICE (${result.red_flags.length}):`,
                  ...result.red_flags.map((f) => `• ${f.title}: ${f.description}`),
                  "",
                  `UPOZORENJA (${result.warnings.length}):`,
                  ...result.warnings.map((w) => `• ${w.title}: ${w.description}`),
                  "",
                  ...(result.logical_traps?.length ? [
                    `LOGIČKE ZAMKE (${result.logical_traps.length}):`,
                    ...result.logical_traps.map((t) => `• [${t.severity}] ${t.title}: ${t.explanation}`),
                    "",
                  ] : []),
                  `PRIJEDLOZI IZMJENA (${result.suggested_amendments.length}):`,
                  ...result.suggested_amendments.map((a) =>
                    `• ${a.article_reference}: ${a.proposed_text}`
                  ),
                  "",
                  `POZITIVNO (${result.positives.length}):`,
                  ...result.positives.map((p) => `✓ ${p}`),
                ].join("\n")}
                onPdfExport={() => generateContractPdf(result as Parameters<typeof generateContractPdf>[0])}
              />
            </div>

            {/* V58: Generate Improved Contract Button */}
            <div className="bg-gradient-to-r from-emerald-50 to-teal-50 rounded-xl border border-emerald-200 shadow-sm p-5">
              <div className="flex items-center justify-between gap-4 flex-wrap">
                <div className="flex-1 min-w-0">
                  <h3 className="font-bold text-emerald-800 flex items-center gap-2">
                    <Sparkles className="h-5 w-5 text-emerald-600" />
                    Generiraj poboljšani ugovor
                  </h3>
                  <p className="text-sm text-emerald-600 mt-1">
                    AI pravni redaktor će na temelju analize izraditi korigiranu verziju ugovora
                    sa svim ispravkama, nedostajućim klauzulama i usklađenim člancima.
                  </p>
                </div>
                <button
                  onClick={handleGenerateContract}
                  disabled={isGenerating || !originalTextRef.current}
                  className="px-6 py-3 rounded-xl bg-emerald-600 text-white font-semibold hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center gap-2 whitespace-nowrap shadow-sm"
                >
                  {isGenerating ? (
                    <>
                      <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24" fill="none">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                      </svg>
                      Generiram...
                    </>
                  ) : (
                    <>
                      <Sparkles className="h-5 w-5" />
                      Generiraj ugovor
                    </>
                  )}
                </button>
              </div>

              {/* Mini progress when generating */}
              {isGenerating && generateStages.length > 0 && (
                <div className="mt-4 space-y-2">
                  {generateStages.map((s) => (
                    <div key={s.id} className="flex items-center gap-2 text-sm">
                      {s.status === "done" ? (
                        <CheckCircle className="h-4 w-4 text-emerald-500" />
                      ) : s.status === "active" ? (
                        <svg className="animate-spin h-4 w-4 text-emerald-500" viewBox="0 0 24 24" fill="none">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                        </svg>
                      ) : (
                        <div className="h-4 w-4 rounded-full border-2 border-gray-300" />
                      )}
                      <span className={s.status === "active" ? "text-emerald-700 font-medium" : s.status === "done" ? "text-emerald-600" : "text-gray-400"}>
                        {s.label}
                      </span>
                      {s.detail && <span className="text-xs text-gray-400">— {s.detail}</span>}
                    </div>
                  ))}
                </div>
              )}

              {generateError && (
                <div className="mt-3 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
                  <AlertCircle className="h-4 w-4 inline mr-1" />
                  {generateError}
                </div>
              )}
            </div>

            {/* Document Type & Risk Overview */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6">
              <div className="flex items-start justify-between gap-4 flex-wrap">
                <div>
                  <div className="text-xs text-gray-400 uppercase tracking-wide mb-1">
                    Vrsta dokumenta
                  </div>
                  <div className="text-xl font-bold text-gray-900 flex items-center gap-2">
                    <BookOpen className="h-5 w-5 text-emerald-600" />
                    {result.document_type}
                  </div>
                  {result.summary && (
                    <p className="text-sm text-gray-500 mt-2 max-w-xl">
                      {result.summary}
                    </p>
                  )}
                </div>
                <div
                  className={`px-4 py-2 rounded-lg border text-sm font-semibold ${riskColor(result.overall_risk)}`}
                >
                  Rizik: {result.overall_risk}
                </div>
              </div>

              {/* Risk Score Bar */}
              <div className="mt-4">
                <div className="flex items-center justify-between text-xs text-gray-400 mb-1">
                  <span>Nizak rizik</span>
                  <span>{result.risk_score}/100</span>
                  <span>Visok rizik</span>
                </div>
                <div className="w-full bg-gray-100 rounded-full h-3 overflow-hidden">
                  <div
                    className={`h-3 rounded-full transition-all ${riskBarColor(result.risk_score)}`}
                    style={{ width: `${Math.min(result.risk_score, 100)}%` }}
                  />
                </div>
              </div>

              {/* Stats */}
              <div className="grid grid-cols-4 gap-4 mt-4 pt-4 border-t border-gray-100">
                <div className="text-center">
                  <div className="text-2xl font-bold text-red-600">
                    {result.red_flags.length}
                  </div>
                  <div className="text-xs text-gray-400">Crvene zastavice</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-red-800">
                    {result.logical_traps?.length ?? 0}
                  </div>
                  <div className="text-xs text-gray-400">Zamke (o1)</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-amber-600">
                    {result.warnings.length}
                  </div>
                  <div className="text-xs text-gray-400">Upozorenja</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-green-600">
                    {result.positives.length}
                  </div>
                  <div className="text-xs text-gray-400">Pozitivno</div>
                </div>
              </div>
            </div>

            {/* V49: ✅ Revizijska kontrolna lista (Checklist) */}
            {result.checklist && result.checklist.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                <div className="bg-slate-50 px-6 py-3 border-b border-gray-200 flex items-center gap-2">
                  <ClipboardCheck className="h-5 w-5 text-slate-700" />
                  <h2 className="font-bold text-slate-800">
                    Revizijska kontrolna lista ({result.checklist.length})
                  </h2>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-gray-50 text-left text-xs text-gray-500 uppercase tracking-wide">
                        <th className="px-4 py-2">Kategorija</th>
                        <th className="px-4 py-2">Provjera</th>
                        <th className="px-4 py-2 text-center">Status</th>
                        <th className="px-4 py-2 text-center">Rizik</th>
                        <th className="px-4 py-2">Nalaz</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {result.checklist.map((item, i) => (
                        <tr key={i} className="hover:bg-gray-50/50">
                          <td className="px-4 py-3 font-medium text-gray-800 whitespace-nowrap">
                            {item.category}
                          </td>
                          <td className="px-4 py-3 text-gray-700">
                            {item.check}
                            {item.article_ref && (
                              <span className="ml-1 text-xs text-indigo-500">({item.article_ref})</span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-center">
                            <span className={`inline-block px-2 py-0.5 rounded text-xs font-semibold border ${statusBadge(item.status)}`}>
                              {item.status.replace("_", " ")}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-center">
                            <span className={`inline-block px-2 py-0.5 rounded text-xs font-semibold ${riskBadge(item.risk)}`}>
                              {item.risk}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-gray-600 max-w-xs">
                            <p>{item.finding}</p>
                            {item.recommendation && (
                              <p className="mt-1 text-xs text-amber-700 italic">
                                💡 {item.recommendation}
                              </p>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* V49: 🔗 Cross-Article strukturni rizici */}
            {result.cross_article_issues && result.cross_article_issues.length > 0 && (
              <div className="bg-white rounded-xl border border-purple-200 shadow-sm overflow-hidden">
                <div className="bg-purple-50 px-6 py-3 border-b border-purple-200 flex items-center gap-2">
                  <Link2 className="h-5 w-5 text-purple-600" />
                  <h2 className="font-bold text-purple-800">
                    Strukturni rizici — međučlanska analiza ({result.cross_article_issues.length})
                  </h2>
                </div>
                <div className="divide-y divide-purple-100">
                  {result.cross_article_issues.map((issue, i) => (
                    <div key={i} className="px-6 py-4">
                      <div className="flex items-start justify-between gap-3">
                        <h3 className="font-semibold text-purple-800">{issue.title}</h3>
                        <span className={`shrink-0 px-2 py-0.5 rounded text-xs font-semibold ${riskBadge(issue.risk)}`}>
                          {issue.risk}
                        </span>
                      </div>
                      <div className="flex flex-wrap gap-1.5 mt-2">
                        {issue.linked_articles.map((art, j) => (
                          <span key={j} className="bg-purple-100 text-purple-700 text-xs px-2 py-0.5 rounded-full font-medium">
                            {art}
                          </span>
                        ))}
                      </div>
                      <p className="text-sm text-gray-700 mt-2 leading-relaxed">{issue.description}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* V49: 📋 Obvezne zaštitne klauzule */}
            {result.mandatory_clauses && result.mandatory_clauses.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                <div className="bg-slate-50 px-6 py-3 border-b border-gray-200 flex items-center gap-2">
                  <ListChecks className="h-5 w-5 text-slate-700" />
                  <h2 className="font-bold text-slate-800">
                    Obvezne zaštitne klauzule
                  </h2>
                </div>
                <div className="divide-y divide-gray-100">
                  {result.mandatory_clauses.map((mc, i) => (
                    <div key={i} className="px-6 py-3 flex items-start gap-3">
                      <div className="mt-0.5">
                        {mc.present ? (
                          <CheckCircle className="h-5 w-5 text-green-500" />
                        ) : (
                          <AlertCircle className="h-5 w-5 text-red-500" />
                        )}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-medium text-gray-800">{mc.clause}</span>
                          <span className={`text-xs px-1.5 py-0.5 rounded font-semibold ${
                            mc.importance === "KRITIČNO" 
                              ? "bg-red-100 text-red-700" 
                              : "bg-blue-100 text-blue-700"
                          }`}>
                            {mc.importance}
                          </span>
                          <span className={`text-xs font-medium ${mc.present ? "text-green-600" : "text-red-600"}`}>
                            {mc.present ? "PRISUTNO" : "NEDOSTAJE"}
                          </span>
                        </div>
                        <p className="text-sm text-gray-500 mt-1">{mc.description}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* V50: 🪤 Logičke zamke — o1-preview deep reasoning */}
            {result.logical_traps && result.logical_traps.length > 0 && (
              <div className="bg-white rounded-xl border border-red-300 shadow-sm overflow-hidden">
                <div className="bg-red-100 px-6 py-3 border-b border-red-300 flex items-center gap-2">
                  <AlertOctagon className="h-5 w-5 text-red-700" />
                  <h2 className="font-bold text-red-900">
                    🪤 Logičke zamke — dubinska analiza ({result.logical_traps.length})
                  </h2>
                  <span className="ml-auto text-[10px] bg-red-200 text-red-800 px-2 py-0.5 rounded-full font-semibold uppercase">AI Reasoning</span>
                </div>
                <div className="divide-y divide-red-100">
                  {result.logical_traps.map((trap, i) => (
                    <div key={i} className="px-6 py-4">
                      <div className="flex items-start gap-3">
                        <span className={`mt-0.5 inline-flex items-center rounded-full px-2 py-0.5 text-xs font-bold ${
                          trap.severity === "KRITIČNO" ? "bg-red-600 text-white"
                          : trap.severity === "VISOK" ? "bg-red-100 text-red-800"
                          : "bg-yellow-100 text-yellow-800"
                        }`}>
                          {trap.severity}
                        </span>
                        <div className="flex-1">
                          <h3 className="font-semibold text-gray-900">{trap.title}</h3>
                          {trap.articles.length > 0 && (
                            <div className="flex flex-wrap gap-1 mt-1">
                              {trap.articles.map((art, j) => (
                                <span key={j} className="text-[10px] bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded">{art}</span>
                              ))}
                            </div>
                          )}
                          {trap.quoted_text && (
                            <blockquote className="mt-2 border-l-3 border-red-300 pl-3 text-sm text-red-700 italic bg-red-50 py-1">
                              &ldquo;{trap.quoted_text}&rdquo;
                            </blockquote>
                          )}
                          <p className="text-sm text-gray-700 mt-2 leading-relaxed">{trap.explanation}</p>
                          {trap.legal_basis && (
                            <p className="text-xs text-blue-600 mt-1 font-medium">📜 {trap.legal_basis}</p>
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* V50: 📋 Usporedba s idealnim ugovorom — RAG */}
            {result.ideal_contract_deviations && result.ideal_contract_deviations.length > 0 && (
              <div className="bg-white rounded-xl border border-indigo-200 shadow-sm overflow-hidden">
                <div className="bg-indigo-50 px-6 py-3 border-b border-indigo-200 flex items-center gap-2">
                  <FileText className="h-5 w-5 text-indigo-600" />
                  <h2 className="font-bold text-indigo-900">
                    Usporedba s idealnim ugovorom ({result.ideal_contract_deviations.length} devijacija)
                  </h2>
                  <span className="ml-auto text-[10px] bg-indigo-200 text-indigo-800 px-2 py-0.5 rounded-full font-semibold uppercase">RAG</span>
                </div>
                <div className="divide-y divide-indigo-100">
                  {result.ideal_contract_deviations.map((dev, i) => (
                    <div key={i} className="px-6 py-4">
                      <div className="flex items-start gap-3">
                        <span className={`mt-0.5 inline-flex items-center rounded-full px-2 py-0.5 text-xs font-bold ${
                          dev.severity === "KRITIČNO" ? "bg-red-100 text-red-800"
                          : dev.severity === "VISOK" ? "bg-orange-100 text-orange-800"
                          : dev.severity === "SREDNJI" ? "bg-yellow-100 text-yellow-800"
                          : "bg-gray-100 text-gray-600"
                        }`}>
                          {dev.severity}
                        </span>
                        <div className="flex-1">
                          <h3 className="font-semibold text-gray-900">{dev.element}</h3>
                          <div className="mt-2 grid grid-cols-1 md:grid-cols-2 gap-3">
                            <div className="rounded-lg bg-green-50 border border-green-200 p-3">
                              <p className="text-[10px] font-bold text-green-700 uppercase mb-1">Idealni ugovor</p>
                              <p className="text-sm text-green-800">{dev.ideal_text}</p>
                            </div>
                            <div className={`rounded-lg p-3 ${
                              dev.actual_text === "NEDOSTAJE" 
                                ? "bg-red-50 border border-red-200" 
                                : "bg-amber-50 border border-amber-200"
                            }`}>
                              <p className={`text-[10px] font-bold uppercase mb-1 ${
                                dev.actual_text === "NEDOSTAJE" ? "text-red-700" : "text-amber-700"
                              }`}>Vaš ugovor</p>
                              <p className={`text-sm ${
                                dev.actual_text === "NEDOSTAJE" ? "text-red-800 font-bold" : "text-amber-800"
                              }`}>{dev.actual_text}</p>
                            </div>
                          </div>
                          <p className="text-sm text-indigo-700 mt-2">💡 {dev.recommendation}</p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* 🔴 Red Flags */}
            {result.red_flags.length > 0 && (
              <div className="bg-white rounded-xl border border-red-200 shadow-sm overflow-hidden">
                <div className="bg-red-50 px-6 py-3 border-b border-red-200 flex items-center gap-2">
                  <AlertCircle className="h-5 w-5 text-red-600" />
                  <h2 className="font-bold text-red-800">
                    Kritične crvene zastavice ({result.red_flags.length})
                  </h2>
                </div>
                <div className="divide-y divide-red-100">
                  {result.red_flags.map((flag, i) => (
                    <div key={i} className="px-6 py-4">
                      <h3 className="font-semibold text-red-800 mb-1">
                        {flag.title}
                      </h3>
                      <p className="text-sm text-gray-700 leading-relaxed">
                        {flag.description}
                      </p>
                      {flag.quoted_text && (
                        <div className="mt-2 bg-red-50 border-l-4 border-red-300 px-4 py-2 rounded-r-lg">
                          <Quote className="inline h-3 w-3 text-red-400 mr-1" />
                          <span className="text-sm italic text-red-700">
                            {flag.quoted_text}
                          </span>
                        </div>
                      )}
                      {flag.legal_basis && (
                        <NormBadge
                          norm={{
                            abbreviation: "",
                            full_name: "",
                            article: flag.legal_basis,
                            paragraph: "",
                            point: "",
                            nn_reference: "",
                            status: "VAŽEĆI",
                            formatted_citation: flag.legal_basis,
                            nn_url: "",
                          }}
                          context="red_flag"
                        />
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* 🟡 Warnings */}
            {result.warnings.length > 0 && (
              <div className="bg-white rounded-xl border border-amber-200 shadow-sm overflow-hidden">
                <div className="bg-amber-50 px-6 py-3 border-b border-amber-200 flex items-center gap-2">
                  <AlertTriangle className="h-5 w-5 text-amber-600" />
                  <h2 className="font-bold text-amber-800">
                    Upozorenja i nedostaci ({result.warnings.length})
                  </h2>
                </div>
                <div className="divide-y divide-amber-100">
                  {result.warnings.map((warn, i) => (
                    <div key={i} className="px-6 py-4">
                      <h3 className="font-semibold text-amber-800 mb-1">
                        {warn.title}
                      </h3>
                      <p className="text-sm text-gray-700 leading-relaxed">
                        {warn.description}
                      </p>
                      {warn.quoted_text && (
                        <div className="mt-2 bg-amber-50 border-l-4 border-amber-300 px-4 py-2 rounded-r-lg">
                          <Quote className="inline h-3 w-3 text-amber-400 mr-1" />
                          <span className="text-sm italic text-amber-700">
                            {warn.quoted_text}
                          </span>
                        </div>
                      )}
                      {warn.legal_basis && (
                        <NormBadge
                          norm={{
                            abbreviation: "",
                            full_name: "",
                            article: warn.legal_basis,
                            paragraph: "",
                            point: "",
                            nn_reference: "",
                            status: "VAŽEĆI",
                            formatted_citation: warn.legal_basis,
                            nn_url: "",
                          }}
                          context="warning"
                        />
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* ✅ Positives */}
            {result.positives.length > 0 && (
              <div className="bg-white rounded-xl border border-green-200 shadow-sm overflow-hidden">
                <div className="bg-green-50 px-6 py-3 border-b border-green-200 flex items-center gap-2">
                  <CheckCircle className="h-5 w-5 text-green-600" />
                  <h2 className="font-bold text-green-800">
                    Pozitivne strane ({result.positives.length})
                  </h2>
                </div>
                <ul className="divide-y divide-green-100">
                  {result.positives.map((p, i) => (
                    <li
                      key={i}
                      className="px-6 py-3 text-sm text-gray-700 flex items-start gap-2"
                    >
                      <CheckCircle className="h-4 w-4 text-green-500 mt-0.5 shrink-0" />
                      {p}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* 📝 Suggested Amendments */}
            {result.suggested_amendments.length > 0 && (
              <div className="bg-white rounded-xl border border-blue-200 shadow-sm overflow-hidden">
                <div className="bg-blue-50 px-6 py-3 border-b border-blue-200 flex items-center gap-2">
                  <PenLine className="h-5 w-5 text-blue-600" />
                  <h2 className="font-bold text-blue-800">
                    Prijedlozi izmjena ({result.suggested_amendments.length})
                  </h2>
                </div>
                <div className="divide-y divide-blue-100">
                  {result.suggested_amendments.map((sa, i) => (
                    <div key={i} className="px-6 py-4 space-y-3">
                      <h3 className="font-semibold text-blue-800">
                        {sa.article_reference}
                      </h3>

                      {sa.current_text && (
                        <div>
                          <div className="text-xs text-gray-400 uppercase tracking-wide mb-1">
                            Trenutni tekst
                          </div>
                          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-800 line-through">
                            {sa.current_text}
                          </div>
                        </div>
                      )}

                      <div>
                        <div className="text-xs text-gray-400 uppercase tracking-wide mb-1">
                          Predloženi tekst
                        </div>
                        <div className="bg-green-50 border border-green-200 rounded-lg px-4 py-2 text-sm text-green-800">
                          {sa.proposed_text}
                        </div>
                      </div>

                      <p className="text-xs text-gray-500 italic">
                        {sa.rationale}
                      </p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* V42: Cited Norms — Validity Badges */}
            {result.cited_norms && result.cited_norms.length > 0 && (
              <div className="bg-white rounded-xl border border-indigo-200 shadow-sm overflow-hidden">
                <div className="bg-indigo-50 px-6 py-3 border-b border-indigo-200 flex items-center gap-2">
                  <Scale className="h-5 w-5 text-indigo-600" />
                  <h2 className="font-bold text-indigo-800">Citirani propisi</h2>
                  <span className="text-xs text-indigo-400">({result.cited_norms.length})</span>
                </div>
                <div className="px-6 py-4">
                  <CitedNormsList norms={result.cited_norms} variant="cards" />
                </div>
              </div>
            )}

            {/* V49: 📊 Risk Score Breakdown */}
            {result.risk_score_breakdown && result.risk_score_breakdown.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                <div className="bg-gray-50 px-6 py-3 border-b border-gray-200 flex items-center gap-2">
                  <Calculator className="h-5 w-5 text-gray-600" />
                  <h2 className="font-bold text-gray-800">
                    Struktura ocjene rizika ({result.risk_score}/100)
                  </h2>
                </div>
                <ul className="divide-y divide-gray-100">
                  {result.risk_score_breakdown.map((line, i) => (
                    <li key={i} className="px-6 py-2 text-sm text-gray-700 font-mono">
                      {line.startsWith("-") ? (
                        <span className="text-green-600">{line}</span>
                      ) : (
                        <span className="text-red-600">{line}</span>
                      )}
                    </li>
                  ))}
                </ul>
                <div className="px-6 py-2 bg-gray-50 border-t border-gray-200 text-sm font-bold text-gray-800 font-mono">
                  = {result.risk_score}/100 → {result.overall_risk}
                </div>
              </div>
            )}

            {/* ═══ V58: GENERATED IMPROVED CONTRACT ═══ */}
            {showGenerated && generatedContract && generatedContract.improved_contract && (
              <div id="generated-contract" className="bg-white rounded-xl border-2 border-emerald-300 shadow-lg overflow-hidden">
                <div className="bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-4 flex items-center justify-between">
                  <div className="flex items-center gap-2 text-white">
                    <Sparkles className="h-5 w-5" />
                    <h2 className="font-bold text-lg">Poboljšani ugovor</h2>
                    <span className="text-emerald-200 text-sm ml-2">
                      ({generatedContract.changes_summary?.length || 0} izmjena)
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <ResultActions
                      copyText={generatedContract.improved_contract}
                      copyLabel="Kopiraj ugovor"
                      className="!bg-white/20 !text-white hover:!bg-white/30"
                    />
                    <button
                      onClick={() => setShowGenerated(false)}
                      className="p-1.5 rounded-lg hover:bg-white/20 text-white transition"
                      title="Sakrij"
                    >
                      <X className="h-5 w-5" />
                    </button>
                  </div>
                </div>

                {/* Changes Summary */}
                {generatedContract.changes_summary && generatedContract.changes_summary.length > 0 && (
                  <div className="border-b border-emerald-200">
                    <button
                      onClick={() => setExpandedChanges(!expandedChanges)}
                      className="w-full px-6 py-3 bg-emerald-50 flex items-center justify-between hover:bg-emerald-100 transition"
                    >
                      <span className="text-sm font-semibold text-emerald-800">
                        Popis izmjena ({generatedContract.changes_summary.length})
                      </span>
                      {expandedChanges ? (
                        <ChevronUp className="h-4 w-4 text-emerald-600" />
                      ) : (
                        <ChevronDown className="h-4 w-4 text-emerald-600" />
                      )}
                    </button>
                    {expandedChanges && (
                      <div className="divide-y divide-emerald-100">
                        {generatedContract.changes_summary.map((change, i) => (
                          <div key={i} className="px-6 py-3 flex items-start gap-3">
                            <span className={`px-2 py-0.5 rounded text-xs font-bold whitespace-nowrap mt-0.5 ${
                              change.change_type === "NOVO" ? "bg-green-100 text-green-700" :
                              change.change_type === "UKLONJENO" ? "bg-red-100 text-red-700" :
                              "bg-amber-100 text-amber-700"
                            }`}>
                              {change.change_type}
                            </span>
                            <div className="flex-1 min-w-0">
                              <div className="text-sm font-medium text-gray-800">{change.article}</div>
                              <div className="text-sm text-gray-600 mt-0.5">{change.description}</div>
                              {change.legal_basis && (
                                <div className="text-xs text-indigo-500 mt-1">{change.legal_basis}</div>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {/* Contract Text */}
                <div className="px-6 py-5">
                  <pre className="whitespace-pre-wrap text-sm text-gray-800 leading-relaxed font-serif">
                    {generatedContract.improved_contract.split(/(\[(?:NOVO|IZMIJENJENO|UKLONJENO):[^\]]*\])/).map((part, i) => {
                      if (/^\[NOVO:/.test(part))
                        return <span key={i} className="bg-green-100 text-green-800 px-1 rounded text-xs font-sans font-bold">{part}</span>;
                      if (/^\[IZMIJENJENO:/.test(part))
                        return <span key={i} className="bg-amber-100 text-amber-800 px-1 rounded text-xs font-sans font-bold">{part}</span>;
                      if (/^\[UKLONJENO:/.test(part))
                        return <span key={i} className="bg-red-100 text-red-800 px-1 rounded text-xs font-sans font-bold">{part}</span>;
                      return <span key={i}>{part}</span>;
                    })}
                  </pre>
                </div>

                {/* Generated contract disclaimer */}
                <div className="px-6 py-3 bg-gray-50 border-t border-gray-200">
                  <p className="text-xs text-gray-400 text-center leading-relaxed">
                    ⚖️ {generatedContract.disclaimer}
                  </p>
                </div>
              </div>
            )}

            {/* Disclaimer */}
            {result.disclaimer && (
              <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 text-center">
                <p className="text-xs text-gray-400 leading-relaxed">
                  ⚖️ {result.disclaimer}
                </p>
              </div>
            )}
          </div>
        )}
      </main>
    </div>
    </>
  );
}
