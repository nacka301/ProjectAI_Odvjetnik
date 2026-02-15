"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Upload,
  FileText,
  AlertTriangle,
  AlertCircle,
  CheckCircle,
  PenLine,
  Shield,
  Loader2,
  Scale,
  Quote,
  BookOpen,
} from "lucide-react";

// ── Types matching backend ContractAnalysisResult ──

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

interface ContractAnalysisResult {
  document_type: string;
  summary: string;
  overall_risk: string;
  risk_score: number;
  red_flags: ContractIssue[];
  warnings: ContractIssue[];
  positives: string[];
  suggested_amendments: SuggestedAmendment[];
  disclaimer: string;
}

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export default function ContractAnalysisPage() {
  const [mode, setMode] = useState<"pdf" | "text">("pdf");
  const [textInput, setTextInput] = useState("");
  const [fileName, setFileName] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ContractAnalysisResult | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const selectedFileRef = useRef<File | null>(null);

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
    setLoading(true);

    try {
      let response: Response;

      if (mode === "pdf") {
        if (!selectedFileRef.current) {
          setError("Molimo odaberite PDF datoteku.");
          setLoading(false);
          return;
        }
        const formData = new FormData();
        formData.append("file", selectedFileRef.current);
        response = await fetch(`${API}/api/contract/analyze-pdf`, {
          method: "POST",
          body: formData,
        });
      } else {
        if (!textInput.trim() || textInput.trim().length < 50) {
          setError("Tekst ugovora mora imati barem 50 znakova.");
          setLoading(false);
          return;
        }
        response = await fetch(`${API}/api/contract/analyze-text`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ text: textInput }),
        });
      }

      if (!response.ok) {
        const err = await response.json().catch(() => null);
        throw new Error(err?.error ?? `Greška ${response.status}`);
      }

      const data: ContractAnalysisResult = await response.json();
      setResult(data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Neočekivana greška");
    } finally {
      setLoading(false);
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
    if (score >= 70) return "bg-red-500";
    if (score >= 40) return "bg-amber-500";
    return "bg-green-500";
  };

  return (
    <div className="min-h-screen bg-gray-50 font-sans">
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
        {!result && (
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

            {/* Analyze Button */}
            <button
              onClick={handleAnalyze}
              disabled={loading}
              className="w-full py-4 rounded-xl bg-emerald-600 text-white font-semibold text-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center justify-center gap-2"
            >
              {loading ? (
                <>
                  <Loader2 className="h-5 w-5 animate-spin" />
                  AI analizira ugovor...
                </>
              ) : (
                <>
                  <Shield className="h-5 w-5" />
                  Analiziraj ugovor
                </>
              )}
            </button>
          </div>
        )}

        {/* ── Results Section ── */}
        {result && (
          <div className="space-y-6">
            {/* Back / New Analysis */}
            <button
              onClick={() => {
                setResult(null);
                setError(null);
              }}
              className="text-sm text-emerald-600 hover:underline flex items-center gap-1"
            >
              <ArrowLeft className="h-4 w-4" /> Nova analiza
            </button>

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
              <div className="grid grid-cols-3 gap-4 mt-4 pt-4 border-t border-gray-100">
                <div className="text-center">
                  <div className="text-2xl font-bold text-red-600">
                    {result.red_flags.length}
                  </div>
                  <div className="text-xs text-gray-400">Crvene zastavice</div>
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
                        <span className="inline-block mt-2 text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full">
                          {flag.legal_basis}
                        </span>
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
                        <span className="inline-block mt-2 text-xs bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full">
                          {warn.legal_basis}
                        </span>
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
  );
}
