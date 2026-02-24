"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Languages,
  Copy,
  Check,
  FileText,
  Sparkles,
  RotateCcw,
  ChevronDown,
} from "lucide-react";
import SharedProcessingView, { type ProcessingStage } from "@/components/SharedProcessingView";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import { useCredits, getModuleQuota } from "@/lib/useCredits";
import { CreditQuotaBadge, CreditExhaustedWarning } from "@/components/CreditQuotaBadge";
import { PrivacyBanner } from "@/components/TrustComponents";

// ═══════════════════════════════════════════════════════════
//  Sudski Tumač — Legal Translation & Transliteration
// ═══════════════════════════════════════════════════════════

const API = API_BASE;

// ── Translation modes matching backend TranslationMode enum ──

type TranslationMode =
  | "CyrillicToLatin"
  | "LatinToCyrillic"
  | "EnglishToCroatian"
  | "GermanToCroatian"
  | "CroatianToEnglish"
  | "CroatianToGerman"
  | "AutoDetectToCroatian"
  | "AutoDetectToEnglish";

interface ModeOption {
  value: TranslationMode;
  label: string;
  description: string;
  group: string;
}

const MODE_OPTIONS: ModeOption[] = [
  // Transliteration
  {
    value: "CyrillicToLatin",
    label: "Ćirilica → Latinica",
    description: "Preslovljavanje s ćirilice na latinicu bez promjene jezika",
    group: "Transliteracija",
  },
  {
    value: "LatinToCyrillic",
    label: "Latinica → Ćirilica",
    description: "Preslovljavanje s latinice na ćirilicu bez promjene jezika",
    group: "Transliteracija",
  },
  // Translation to Croatian
  {
    value: "EnglishToCroatian",
    label: "Engleski → Hrvatski",
    description: "Pravni prijevod s engleskog na hrvatski",
    group: "Prijevod na hrvatski",
  },
  {
    value: "GermanToCroatian",
    label: "Njemački → Hrvatski",
    description: "Pravni prijevod s njemačkog na hrvatski",
    group: "Prijevod na hrvatski",
  },
  // Translation from Croatian
  {
    value: "CroatianToEnglish",
    label: "Hrvatski → Engleski",
    description: "Pravni prijevod s hrvatskog na engleski",
    group: "Prijevod s hrvatskog",
  },
  {
    value: "CroatianToGerman",
    label: "Hrvatski → Njemački",
    description: "Pravni prijevod s hrvatskog na njemački",
    group: "Prijevod s hrvatskog",
  },
  // Auto-detect
  {
    value: "AutoDetectToCroatian",
    label: "Auto-detekcija → Hrvatski",
    description: "Automatska detekcija jezika i prijevod na hrvatski",
    group: "Automatska detekcija",
  },
  {
    value: "AutoDetectToEnglish",
    label: "Auto-detekcija → Engleski",
    description: "Automatska detekcija jezika i prijevod na engleski",
    group: "Automatska detekcija",
  },
];

// Group modes for the dropdown
const MODE_GROUPS = [
  { label: "Transliteracija", modes: MODE_OPTIONS.filter((m) => m.group === "Transliteracija") },
  { label: "Prijevod na hrvatski", modes: MODE_OPTIONS.filter((m) => m.group === "Prijevod na hrvatski") },
  { label: "Prijevod s hrvatskog", modes: MODE_OPTIONS.filter((m) => m.group === "Prijevod s hrvatskog") },
  { label: "Automatska detekcija", modes: MODE_OPTIONS.filter((m) => m.group === "Automatska detekcija") },
];

// ── Interfaces ──

interface TranslationResult {
  translated_text: string;
  detected_language: string;
  target_language: string;
  mode: string;
  source_char_count: number;
  translated_char_count: number;
  processing_time_ms: number;
}

// ── Stage config for SharedProcessingView ──

const INITIAL_STAGES: ProcessingStage[] = [
  { id: "translating", label: "Prevodim...", status: "pending" },
];

const STAGE_ICONS: Record<string, React.ReactNode> = {
  translating: <Languages className="h-4 w-4" />,
};

const STAGE_MICROCOPY: Record<string, string[]> = {
  translating: [
    "Modul za pravnu lingvistiku obrađuje vaš tekst...",
    "Primjenjujem strogu pravnu terminologiju...",
    "Zadržavam strukturu i formatiranje dokumenta...",
    "Provjeravam točnost prijevoda...",
  ],
};

// ═══════════════════════════════════════════════════════════
//  MAIN COMPONENT
// ═══════════════════════════════════════════════════════════

export default function TranslationPage() {
  const { userId, authHeaders } = useAuth();
  const { rows: creditRows, loading: creditsLoading } = useCredits(userId);
  const translationQuota = getModuleQuota(creditRows, "translation");

  // ── State ──
  const [textInput, setTextInput] = useState("");
  const [selectedMode, setSelectedMode] = useState<TranslationMode>("CyrillicToLatin");
  const [modeDropdownOpen, setModeDropdownOpen] = useState(false);
  const [result, setResult] = useState<TranslationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isStreaming, setIsStreaming] = useState(false);
  const [stages, setStages] = useState<ProcessingStage[]>([]);
  const [viewState, setViewState] = useState<"INPUT" | "PROCESSING" | "RESULT">("INPUT");
  const [copied, setCopied] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  // ── Get selected mode info ──
  const selectedModeInfo = MODE_OPTIONS.find((m) => m.value === selectedMode)!;

  // ── Handle translation ──
  const handleTranslate = async () => {
    setError(null);
    setResult(null);
    setStages(INITIAL_STAGES.map((s) => ({ ...s })));
    setIsStreaming(true);
    setViewState("PROCESSING");

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const response = await fetch(`${API}/api/translation/stream`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...authHeaders,
        },
        body: JSON.stringify({ text: textInput, mode: selectedMode }),
        signal: controller.signal,
      });

      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let streamedResult: TranslationResult | null = null;

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
            const { message } = evt.data;
            setStages((prev) =>
              prev.map((s) =>
                s.id === "translating" ? { ...s, status: "error" as const, error: message } : s
              )
            );
            setError(message);
          } else if (evt.type === "result") {
            streamedResult = evt.data;
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
      if (buffer.trim()) processEvent(buffer.trim());

      setIsStreaming(false);
      if (streamedResult) setViewState("RESULT");
    } catch (e: unknown) {
      setIsStreaming(false);
      if (e instanceof DOMException && e.name === "AbortError") {
        setError("Prijevod je otkazan.");
        setViewState("INPUT");
      } else {
        setError(e instanceof Error ? e.message : "Server ne odgovara. Pokušajte ponovno.");
      }
    } finally {
      abortRef.current = null;
    }
  };

  const cancelTranslation = () => {
    abortRef.current?.abort();
    setIsStreaming(false);
    setViewState("INPUT");
  };

  const resetTranslation = () => {
    abortRef.current?.abort();
    setViewState("INPUT");
    setStages([]);
    setResult(null);
    setError(null);
    setCopied(false);
  };

  const copyToClipboard = () => {
    if (!result) return;
    navigator.clipboard.writeText(result.translated_text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const charCount = textInput.length;
  const isValid = charCount >= 10 && charCount <= 100_000;

  // ═══════════════════════════════════════════════════════════
  //  RENDER — PROCESSING
  // ═══════════════════════════════════════════════════════════

  if (viewState === "PROCESSING") {
    return (
      <SharedProcessingView
        stages={stages}
        isStreaming={isStreaming}
        error={error}
        onCancel={cancelTranslation}
        onReset={resetTranslation}
        title="Prijevod u tijeku"
        subtitle={selectedModeInfo.label}
        headerIcon={<Languages className="h-8 w-8 text-white" />}
        stageIcons={STAGE_ICONS}
        stageMicrocopy={STAGE_MICROCOPY}
        theme="purple"
      />
    );
  }

  // ═══════════════════════════════════════════════════════════
  //  RENDER — RESULT
  // ═══════════════════════════════════════════════════════════

  if (viewState === "RESULT" && result) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-purple-50 via-white to-indigo-50">
        <div className="mx-auto max-w-5xl px-4 py-8">
          {/* Header */}
          <div className="mb-6 flex items-center justify-between">
            <button
              onClick={resetTranslation}
              className="flex items-center gap-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
            >
              <ArrowLeft className="h-4 w-4" />
              Novi prijevod
            </button>
            <div className="flex items-center gap-2 text-sm text-gray-400">
              <Languages className="h-4 w-4" />
              {selectedModeInfo.label}
            </div>
          </div>

          {/* Stats */}
          <div className="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="rounded-xl bg-white p-4 shadow-sm border border-gray-100">
              <div className="text-xs text-gray-500 mb-1">Izvor</div>
              <div className="text-sm font-semibold text-gray-800">{result.detected_language}</div>
            </div>
            <div className="rounded-xl bg-white p-4 shadow-sm border border-gray-100">
              <div className="text-xs text-gray-500 mb-1">Cilj</div>
              <div className="text-sm font-semibold text-gray-800">{result.target_language}</div>
            </div>
            <div className="rounded-xl bg-white p-4 shadow-sm border border-gray-100">
              <div className="text-xs text-gray-500 mb-1">Znakova</div>
              <div className="text-sm font-semibold text-gray-800">
                {result.source_char_count.toLocaleString("hr-HR")} → {result.translated_char_count.toLocaleString("hr-HR")}
              </div>
            </div>
            <div className="rounded-xl bg-white p-4 shadow-sm border border-gray-100">
              <div className="text-xs text-gray-500 mb-1">Vrijeme</div>
              <div className="text-sm font-semibold text-gray-800">
                {(result.processing_time_ms / 1000).toFixed(1)}s
              </div>
            </div>
          </div>

          {/* Translated text */}
          <div className="rounded-2xl bg-white shadow-lg border border-gray-200 overflow-hidden">
            <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
              <div className="flex items-center gap-2">
                <FileText className="h-5 w-5 text-purple-500" />
                <span className="font-semibold text-gray-800">Prevedeni tekst</span>
              </div>
              <button
                onClick={copyToClipboard}
                className="flex items-center gap-2 rounded-lg bg-purple-50 px-4 py-2 text-sm font-medium text-purple-700 hover:bg-purple-100 transition-colors"
              >
                {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                {copied ? "Kopirano!" : "Kopiraj"}
              </button>
            </div>
            <div className="p-6">
              <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed text-gray-800">
                {result.translated_text}
              </pre>
            </div>
          </div>

          {/* Actions */}
          <div className="mt-6 flex justify-center gap-4">
            <button
              onClick={resetTranslation}
              className="flex items-center gap-2 rounded-xl bg-purple-600 px-6 py-3 text-sm font-semibold text-white shadow-md hover:bg-purple-700 transition-colors"
            >
              <RotateCcw className="h-4 w-4" />
              Novi prijevod
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ═══════════════════════════════════════════════════════════
  //  RENDER — INPUT
  // ═══════════════════════════════════════════════════════════

  return (
    <div className="min-h-screen bg-gradient-to-br from-purple-50 via-white to-indigo-50">
      <div className="mx-auto max-w-4xl px-4 py-8">
        {/* Navigation */}
        <Link
          href="/"
          className="mb-6 inline-flex items-center gap-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          Početna
        </Link>

        {/* Header */}
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-purple-500 to-indigo-600 shadow-lg">
            <Languages className="h-8 w-8 text-white" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900">Sudski Tumač</h1>
          <p className="mt-2 text-gray-500">
            Pravni prijevod i transliteracija — precizno, brzo, sudski spreman.
          </p>
        </div>

        {/* Main Input Card */}
        <PrivacyBanner />
        <div className="rounded-2xl bg-white shadow-lg border border-gray-200 overflow-hidden">
          {/* Mode Selector */}
          <div className="border-b border-gray-100 p-6">
            <label className="mb-2 block text-sm font-semibold text-gray-700">
              Način prijevoda
            </label>
            <div className="relative">
              <button
                onClick={() => setModeDropdownOpen(!modeDropdownOpen)}
                className="flex w-full items-center justify-between rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-left hover:border-purple-300 hover:bg-purple-50/50 transition-colors"
              >
                <div>
                  <div className="font-medium text-gray-800">{selectedModeInfo.label}</div>
                  <div className="text-xs text-gray-500">{selectedModeInfo.description}</div>
                </div>
                <ChevronDown
                  className={`h-5 w-5 text-gray-400 transition-transform ${modeDropdownOpen ? "rotate-180" : ""}`}
                />
              </button>

              {/* Dropdown */}
              {modeDropdownOpen && (
                <div className="absolute left-0 right-0 top-full z-50 mt-2 max-h-80 overflow-y-auto rounded-xl border border-gray-200 bg-white shadow-xl">
                  {MODE_GROUPS.map((group) => (
                    <div key={group.label}>
                      <div className="sticky top-0 bg-gray-50 px-4 py-2 text-xs font-semibold text-gray-500 uppercase tracking-wider border-b border-gray-100">
                        {group.label}
                      </div>
                      {group.modes.map((mode) => (
                        <button
                          key={mode.value}
                          onClick={() => {
                            setSelectedMode(mode.value);
                            setModeDropdownOpen(false);
                          }}
                          className={`flex w-full flex-col px-4 py-3 text-left hover:bg-purple-50 transition-colors ${
                            selectedMode === mode.value ? "bg-purple-50 border-l-2 border-purple-500" : ""
                          }`}
                        >
                          <div className="font-medium text-gray-800">{mode.label}</div>
                          <div className="text-xs text-gray-500">{mode.description}</div>
                        </button>
                      ))}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Text Input */}
          <div className="p-6">
            <label className="mb-2 block text-sm font-semibold text-gray-700">
              Izvorni tekst
            </label>
            <textarea
              value={textInput}
              onChange={(e) => setTextInput(e.target.value)}
              rows={12}
              placeholder="Zalijepite tekst pravnog dokumenta ovdje — presuda, ugovor, rješenje, tužba..."
              className="w-full resize-y rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-sm leading-relaxed text-gray-800 placeholder:text-gray-400 focus:border-purple-400 focus:bg-white focus:outline-none focus:ring-2 focus:ring-purple-100 transition-colors"
            />
            <div className="mt-2 flex items-center justify-between text-xs text-gray-400">
              <span>Min. 10 znakova, max. 100.000</span>
              <span
                className={
                  charCount > 100_000
                    ? "text-red-500 font-medium"
                    : charCount > 80_000
                    ? "text-amber-500"
                    : ""
                }
              >
                {charCount.toLocaleString("hr-HR")} znakova
              </span>
            </div>
          </div>

          {/* Error */}
          {error && (
            <div className="mx-6 mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}

          {/* Submit */}
          <div className="border-t border-gray-100 px-6 py-4 space-y-2">
            <CreditQuotaBadge quota={translationQuota} loading={creditsLoading} />
            <button
              onClick={handleTranslate}
              disabled={!isValid || isStreaming || translationQuota.exhausted}
              className="flex w-full items-center justify-center gap-3 rounded-xl bg-gradient-to-r from-purple-600 to-indigo-600 px-6 py-3.5 text-sm font-semibold text-white shadow-md hover:from-purple-700 hover:to-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
            >
              <Sparkles className="h-5 w-5" />
              Prevedi
            </button>
            <CreditExhaustedWarning quota={translationQuota} />
          </div>
        </div>

        {/* Info box */}
        <div className="mt-6 rounded-xl bg-purple-50/50 border border-purple-100 p-4 text-xs text-gray-500 leading-relaxed">
          <strong className="text-gray-700">Kako radi?</strong>{" "}
          Zalijepite tekst pravnog dokumenta (presuda, ugovor, rješenje) i odaberite željeni način
          prijevoda. Modul za pravnu lingvistiku prevodi uz strogu pravnu terminologiju, zadržavajući strukturu
          i formatiranje originala. Rezultat je spreman za sudsku upotrebu.
        </div>
      </div>
    </div>
  );
}
