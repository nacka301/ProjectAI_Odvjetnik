"use client";

import { useState, useRef, useCallback } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Upload,
  FileText,
  Clock,
  Loader2,
  CheckCircle,
  AlertCircle,
  XCircle,
  Download,
  Trash2,
  ScrollText,
} from "lucide-react";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import ResultActions from "@/components/ResultActions";
import { generateChronologyPdf } from "@/lib/generateModulePdfs";

// ── Types ──

interface TimelineEvent {
  date_iso: string;
  display_date: string;
  time?: string | null;
  event_description: string;
  source_file: string;
  page_number: number;
  confidence: string;
}

interface ChronologyResult {
  events: TimelineEvent[];
  total_documents_processed: number;
  total_events_extracted: number;
  errors: string[];
}

interface ProgressUpdate {
  stage: string;
  status: string;
  label: string;
  detail?: string;
}

import { API_BASE } from "@/lib/api";
const API = API_BASE;

// ── Confidence badge ──

function ConfidenceBadge({ level }: { level: string }) {
  switch (level.toLowerCase()) {
    case "high":
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700">
          <span className="w-2 h-2 rounded-full bg-green-500" />
          Potvrđeno
        </span>
      );
    case "medium":
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">
          <span className="w-2 h-2 rounded-full bg-amber-500" />
          Izvedeno
        </span>
      );
    case "low":
      return (
        <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-red-100 text-red-700">
          <span className="w-2 h-2 rounded-full bg-red-500" />
          Provjeri
        </span>
      );
    default:
      return null;
  }
}

// ── Word Export ──

async function exportToDocx(events: TimelineEvent[]) {
  // Dynamic import — only loaded when user clicks export
  const { Document, Packer, Paragraph, Table, TableRow, TableCell, TextRun, WidthType, AlignmentType, BorderStyle, HeadingLevel } =
    await import("docx");

  const headerCells = ["R.br.", "Datum", "Opis događaja", "Izvor", "Pouzdanost"].map(
    (text) =>
      new TableCell({
        width: { size: text === "Opis događaja" ? 45 : text === "R.br." ? 5 : text === "Pouzdanost" ? 12 : 19, type: WidthType.PERCENTAGE },
        shading: { fill: "2563EB", color: "FFFFFF" },
        children: [
          new Paragraph({
            children: [new TextRun({ text, bold: true, color: "FFFFFF", size: 20, font: "Calibri" })],
          }),
        ],
      })
  );

  const rows = events.map((evt, i) =>
    new TableRow({
      children: [
        new TableCell({
          children: [new Paragraph({ children: [new TextRun({ text: String(i + 1), size: 20, font: "Calibri" })] })],
        }),
        new TableCell({
          children: [
            new Paragraph({
              children: [
                new TextRun({ text: evt.display_date, size: 20, font: "Calibri" }),
                ...(evt.time ? [new TextRun({ text: ` ${evt.time}`, size: 18, font: "Calibri", color: "666666" })] : []),
              ],
            }),
          ],
        }),
        new TableCell({
          children: [new Paragraph({ children: [new TextRun({ text: evt.event_description, size: 20, font: "Calibri" })] })],
        }),
        new TableCell({
          children: [
            new Paragraph({
              children: [
                new TextRun({ text: evt.source_file, size: 18, font: "Calibri" }),
                ...(evt.page_number > 0 ? [new TextRun({ text: ` (str. ${evt.page_number})`, size: 16, font: "Calibri", color: "888888" })] : []),
              ],
            }),
          ],
        }),
        new TableCell({
          children: [new Paragraph({
            children: [new TextRun({
              text: evt.confidence === "High" ? "🟢 Potvrđeno" : evt.confidence === "Medium" ? "🟡 Izvedeno" : "🔴 Provjeri",
              size: 18, font: "Calibri"
            })]
          })],
        }),
      ],
    })
  );

  const doc = new Document({
    styles: {
      default: {
        document: { run: { font: "Calibri", size: 22 } },
      },
    },
    sections: [
      {
        children: [
          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            alignment: AlignmentType.CENTER,
            children: [new TextRun({ text: "KRONOLOGIJA DOGAĐAJA", bold: true, size: 32, font: "Calibri" })],
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { after: 400 },
            children: [
              new TextRun({
                text: `Generirano: ${new Date().toLocaleDateString("hr-HR")} • ${events.length} događaja`,
                size: 20,
                color: "888888",
                font: "Calibri",
              }),
            ],
          }),
          new Table({
            width: { size: 100, type: WidthType.PERCENTAGE },
            rows: [new TableRow({ children: headerCells }), ...rows],
            borders: {
              top: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
              bottom: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
              left: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
              right: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
              insideHorizontal: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
              insideVertical: { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" },
            },
          }),
          new Paragraph({ spacing: { before: 400 }, children: [] }),
          new Paragraph({
            children: [
              new TextRun({
                text: "Napomena: Ova kronologija je generirana uz pomoć AI alata. Događaji označeni žutim ili crvenim indikatorom zahtijevaju ljudsku provjeru datuma.",
                italics: true,
                size: 18,
                color: "999999",
                font: "Calibri",
              }),
            ],
          }),
        ],
      },
    ],
  });

  const blob = await Packer.toBlob(doc);
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Kronologija_${new Date().toISOString().split("T")[0]}.docx`;
  a.click();
  URL.revokeObjectURL(url);
}

// ═══════════════════════════════════════════════════════
//  Main Component
// ═══════════════════════════════════════════════════════

const CHRONO_STAGE_ICONS: Record<string, React.ReactNode> = {
  upload: <Upload className="h-4 w-4" />,
  processing: <Loader2 className="h-4 w-4" />,
  extraction: <FileText className="h-4 w-4" />,
  sorting: <Clock className="h-4 w-4" />,
};
const CHRONO_STAGE_MICROCOPY: Record<string, string[]> = {
  upload: ["Učitavam datoteke...", "Pripremam dokumente..."],
  processing: ["AI obrađuje dokumente...", "Čitam sadržaj stranica..."],
  extraction: ["Vadim datume i događaje...", "Analiziram kontekst...", "Povezujem izvore..."],
  sorting: ["Sortiram kronološki...", "Formatiram vremensku crtu..."],
};
const INITIAL_CHRONO_STAGES: ProcessingStage[] = [
  { id: "upload", label: "Učitavam datoteke", status: "pending" },
  { id: "processing", label: "Obrada dokumenata", status: "pending" },
  { id: "extraction", label: "Izvlačim događaje", status: "pending" },
  { id: "sorting", label: "Kronološko sortiranje", status: "pending" },
];

export default function ChronologyPage() {
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(false);
  const [progress, setProgress] = useState<ProgressUpdate[]>([]);
  const [result, setResult] = useState<ChronologyResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  // V58: Stage-based progress for SharedProcessingView
  const [stages, setStages] = useState<ProcessingStage[]>([]);
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");

  // ── File handling ──

  const addFiles = useCallback((newFiles: FileList | File[]) => {
    const pdfs = Array.from(newFiles).filter((f) =>
      f.name.toLowerCase().endsWith(".pdf")
    );
    setFiles((prev) => {
      const existing = new Set(prev.map((f) => f.name + f.size));
      const unique = pdfs.filter((f) => !existing.has(f.name + f.size));
      return [...prev, ...unique];
    });
  }, []);

  const removeFile = (index: number) => {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  };

  // ── Drag & Drop ──

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      if (e.dataTransfer.files.length > 0) addFiles(e.dataTransfer.files);
    },
    [addFiles]
  );

  // ── Start SSE stream ──

  const handleGenerate = async () => {
    if (files.length === 0) return;
    setError(null);
    setResult(null);
    setProgress([]);
    setLoading(true);
    setStages(INITIAL_CHRONO_STAGES.map((s) => ({ ...s })));
    setViewState("PROCESSING");

    // Map SSE stages to our predefined stage list
    const knownStages = new Set(["upload", "processing", "extraction", "sorting"]);

    try {
      const formData = new FormData();
      files.forEach((f) => formData.append("files", f));

      // Mark upload as active immediately
      setStages((prev) =>
        prev.map((s) => (s.id === "upload" ? { ...s, status: "active" as const } : s))
      );

      const response = await fetch(`${API}/api/chronology/stream`, {
        method: "POST",
        body: formData,
      });

      if (!response.ok || !response.body) {
        const err = await response.text().catch(() => "");
        throw new Error(err || `Greška ${response.status}`);
      }

      // Upload done, processing starts
      setStages((prev) =>
        prev.map((s) =>
          s.id === "upload" ? { ...s, status: "done" as const } :
          s.id === "processing" ? { ...s, status: "active" as const } : s
        )
      );

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });

        const lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          try {
            const parsed = JSON.parse(line.slice(6));
            if (parsed.type === "progress") {
              setProgress((prev) => [...prev, parsed.data]);
              // Map progress to stage updates
              const { stage, status, label, detail } = parsed.data;
              if (knownStages.has(stage)) {
                setStages((prev) =>
                  prev.map((s) =>
                    s.id === stage
                      ? { ...s, status: status as ProcessingStage["status"], label: label || s.label, detail: detail ?? s.detail }
                      : s
                  )
                );
              }
            } else if (parsed.type === "result") {
              // Mark all stages as done
              setStages((prev) => prev.map((s) => ({ ...s, status: "done" as const })));
              setResult(parsed.data);
            } else if (parsed.type === "error") {
              setError(parsed.data?.message || "Nepoznata greška");
            }
          } catch {
            // skip malformed SSE
          }
        }
      }

      if (!error) setViewState("RESULT");
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Neočekivana greška");
    } finally {
      setLoading(false);
    }
  };

  const resetChronology = () => {
    setResult(null);
    setProgress([]);
    setFiles([]);
    setError(null);
    setStages([]);
    setLoading(false);
    setViewState("INPUT");
  };

  // ── Group events by date for nicer display ──

  const groupedEvents = result
    ? result.events.reduce<Record<string, TimelineEvent[]>>((acc, evt) => {
        const key = evt.display_date || evt.date_iso;
        (acc[key] ??= []).push(evt);
        return acc;
      }, {})
    : {};

  return (
    <>
      {/* ═══ PROCESSING: Full-Screen Pipeline ═══ */}
      {viewState === "PROCESSING" && (
        <SharedProcessingView
          stages={stages}
          isStreaming={loading}
          error={error}
          onCancel={resetChronology}
          onReset={resetChronology}
          title="Kronologija u izradi"
          subtitle="AI izvlači datume i događaje iz dokumenata"
          headerIcon={<ScrollText className="h-7 w-7 text-white" />}
          stageIcons={CHRONO_STAGE_ICONS}
          stageMicrocopy={CHRONO_STAGE_MICROCOPY}
          theme="purple"
        />
      )}

    <div className={`min-h-screen bg-gray-50 font-sans ${viewState === "PROCESSING" ? "hidden" : ""}`}>
      {/* Header */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-5xl mx-auto px-4 py-4 flex items-center gap-4">
          <Link href="/" className="text-gray-400 hover:text-gray-700 transition">
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <div className="flex items-center gap-2">
            <ScrollText className="h-6 w-6 text-violet-600" />
            <h1 className="text-xl font-bold text-gray-900">Kronologija Spisa</h1>
          </div>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 py-8">
        {/* ═══ Input Phase ═══ */}
        {viewState === "INPUT" && (
          <div className="space-y-6">
            <div className="text-center mb-8">
              <p className="text-gray-500 text-lg">
                Uploadajte dokumente iz spisa — AI izvlači sve datume i
                događaje u kronološku vremensku crtu
              </p>
            </div>

            {/* Drop Zone */}
            <div
              onDragOver={(e) => {
                e.preventDefault();
                setDragOver(true);
              }}
              onDragLeave={() => setDragOver(false)}
              onDrop={onDrop}
              onClick={() => inputRef.current?.click()}
              className={`border-2 border-dashed rounded-xl p-12 text-center cursor-pointer transition ${
                dragOver
                  ? "border-violet-500 bg-violet-50"
                  : "border-gray-300 hover:border-violet-400 hover:bg-violet-50/30"
              }`}
            >
              <input
                ref={inputRef}
                type="file"
                accept=".pdf"
                multiple
                onChange={(e) => {
                  if (e.target.files) addFiles(e.target.files);
                  e.target.value = "";
                }}
                className="hidden"
              />
              <Upload className="h-12 w-12 text-gray-400 mx-auto mb-4" />
              <p className="text-gray-600 font-medium text-lg">
                Povucite cijeli registrator ovdje
              </p>
              <p className="text-gray-400 text-sm mt-1">
                ili kliknite za odabir PDF datoteka
              </p>
            </div>

            {/* File List */}
            {files.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                <div className="px-6 py-3 bg-gray-50 border-b border-gray-200 flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-700">
                    {files.length} datoteka odabrano
                  </span>
                  <button
                    onClick={() => setFiles([])}
                    className="text-xs text-red-500 hover:text-red-700"
                  >
                    Ukloni sve
                  </button>
                </div>
                <ul className="divide-y divide-gray-100 max-h-64 overflow-y-auto">
                  {files.map((f, i) => (
                    <li
                      key={f.name + f.size}
                      className="px-6 py-3 flex items-center justify-between"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <FileText className="h-4 w-4 text-violet-500 shrink-0" />
                        <span className="text-sm text-gray-700 truncate">
                          {f.name}
                        </span>
                        <span className="text-xs text-gray-400">
                          {(f.size / 1024).toFixed(0)} KB
                        </span>
                      </div>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          removeFile(i);
                        }}
                        className="text-gray-400 hover:text-red-500 transition"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Error */}
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700 text-sm">
                {error}
              </div>
            )}

            {/* Generate Button */}
            <button
              onClick={handleGenerate}
              disabled={files.length === 0}
              className="w-full py-4 rounded-xl bg-violet-600 text-white font-semibold text-lg hover:bg-violet-700 disabled:opacity-40 disabled:cursor-not-allowed transition flex items-center justify-center gap-2"
            >
              <Clock className="h-5 w-5" />
              Generiraj kronologiju ({files.length} datoteka)
            </button>
          </div>
        )}

        {/* ═══ Results Phase ═══ */}
        {viewState === "RESULT" && result && (
          <div className="space-y-6">
            {/* Controls */}
            <div className="flex items-center justify-between flex-wrap gap-3">
              <button
                onClick={resetChronology}
                className="text-sm text-violet-600 hover:underline flex items-center gap-1"
              >
                <ArrowLeft className="h-4 w-4" /> Nova kronologija
              </button>

              <div className="flex items-center gap-2">
                <ResultActions
                  copyText={result.events.map((e, i) =>
                    `${i + 1}. ${e.display_date}${e.time ? ` ${e.time}` : ""} — ${e.event_description} [${e.source_file}]`
                  ).join("\n")}
                  onPdfExport={() => generateChronologyPdf(result as Parameters<typeof generateChronologyPdf>[0])}
                />
                {result.events.length > 0 && (
                  <button
                    onClick={() => exportToDocx(result.events)}
                    className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm font-medium hover:bg-blue-700 transition"
                  >
                    <Download className="h-4 w-4" />
                    Export .docx
                  </button>
                )}
              </div>
            </div>

            {/* Summary Card */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6">
              <div className="grid grid-cols-3 gap-4 text-center">
                <div>
                  <div className="text-3xl font-bold text-violet-600">
                    {result.total_documents_processed}
                  </div>
                  <div className="text-xs text-gray-400 mt-1">
                    Dokumenata obrađeno
                  </div>
                </div>
                <div>
                  <div className="text-3xl font-bold text-gray-900">
                    {result.total_events_extracted}
                  </div>
                  <div className="text-xs text-gray-400 mt-1">
                    Događaja pronađeno
                  </div>
                </div>
                <div>
                  <div className="text-3xl font-bold text-red-500">
                    {result.errors.length}
                  </div>
                  <div className="text-xs text-gray-400 mt-1">Grešaka</div>
                </div>
              </div>

              {/* Confidence Legend */}
              <div className="flex items-center justify-center gap-6 mt-4 pt-4 border-t border-gray-100">
                <div className="flex items-center gap-1.5 text-xs text-gray-500">
                  <span className="w-2.5 h-2.5 rounded-full bg-green-500" />
                  Potvrđen datum
                </div>
                <div className="flex items-center gap-1.5 text-xs text-gray-500">
                  <span className="w-2.5 h-2.5 rounded-full bg-amber-500" />
                  Izveden datum
                </div>
                <div className="flex items-center gap-1.5 text-xs text-gray-500">
                  <span className="w-2.5 h-2.5 rounded-full bg-red-500" />
                  Potrebna provjera
                </div>
              </div>
            </div>

            {/* Errors */}
            {result.errors.length > 0 && (
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 space-y-1">
                <h3 className="text-sm font-semibold text-amber-800">
                  Problemi pri obradi:
                </h3>
                {result.errors.map((err, i) => (
                  <p key={i} className="text-xs text-amber-700">
                    • {err}
                  </p>
                ))}
              </div>
            )}

            {/* ── Vertical Timeline ── */}
            {result.events.length > 0 ? (
              <div className="relative">
                {/* Vertical line */}
                <div className="absolute left-[22px] top-0 bottom-0 w-0.5 bg-violet-200" />

                <div className="space-y-0">
                  {Object.entries(groupedEvents).map(
                    ([dateLabel, events], groupIdx) => (
                      <div key={groupIdx} className="relative">
                        {/* Date header dot */}
                        <div className="flex items-center gap-4 mb-3 relative z-10">
                          <div className="w-[45px] flex justify-center">
                            <div className="w-4 h-4 rounded-full bg-violet-600 border-2 border-white shadow" />
                          </div>
                          <div className="bg-violet-100 text-violet-800 px-3 py-1 rounded-full text-sm font-semibold">
                            {dateLabel}
                          </div>
                        </div>

                        {/* Events for this date */}
                        <div className="space-y-3 mb-6">
                          {events.map((evt, evtIdx) => (
                            <div
                              key={evtIdx}
                              className="flex items-start gap-4 relative z-10"
                            >
                              {/* Small dot on line */}
                              <div className="w-[45px] flex justify-center pt-2">
                                <div
                                  className={`w-2.5 h-2.5 rounded-full border-2 border-white shadow ${
                                    evt.confidence === "High"
                                      ? "bg-green-500"
                                      : evt.confidence === "Medium"
                                        ? "bg-amber-500"
                                        : "bg-red-500"
                                  }`}
                                />
                              </div>

                              {/* Event card */}
                              <div className="flex-1 bg-white rounded-lg border border-gray-200 shadow-sm p-4 hover:shadow-md transition">
                                <div className="flex items-start justify-between gap-3">
                                  <p className="text-sm text-gray-800 leading-relaxed flex-1">
                                    {evt.event_description}
                                  </p>
                                  <ConfidenceBadge level={evt.confidence} />
                                </div>
                                <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
                                  {evt.time && (
                                    <span className="flex items-center gap-1">
                                      <Clock className="h-3 w-3" />
                                      {evt.time}
                                    </span>
                                  )}
                                  <span className="flex items-center gap-1">
                                    <FileText className="h-3 w-3" />
                                    {evt.source_file}
                                    {evt.page_number > 0 && ` (str. ${evt.page_number})`}
                                  </span>
                                </div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    )
                  )}
                </div>
              </div>
            ) : (
              <div className="text-center py-12 text-gray-400">
                <Clock className="h-12 w-12 mx-auto mb-3 opacity-50" />
                <p>Nijedan događaj nije pronađen u uploadanim dokumentima.</p>
              </div>
            )}
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="max-w-5xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 text-center space-y-1 text-[11px] text-slate-400">
          <p>AI analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11)</p>
          <p>Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu.</p>
        </div>
      </footer>
    </div>
    </>
  );
}
