"use client";

import { useState, useCallback, type DragEvent, type ChangeEvent } from "react";
import axios from "axios";
import Link from "next/link";
import {
  Upload,
  FileText,
  AlertTriangle,
  ShieldCheck,
  ShieldAlert,
  Loader2,
  ArrowLeft,
} from "lucide-react";

const API_URL = `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078"}/api/analyze`;

interface MortgageInfo {
  amount: string;
  currency: string;
  year: string;
  details: string;
}

interface AnalysisResult {
  property_id: string;
  owners: string[];
  active_mortgages: MortgageInfo[];
  active_disputes: string[];
  risk_score: "LOW" | "MEDIUM" | "HIGH";
  lawyer_comment: string;
}

export default function AnalyzePage() {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const handleFile = (f: File | null) => {
    if (f && f.type === "application/pdf") {
      setFile(f);
      setResult(null);
      setError(null);
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

  const analyze = async () => {
    if (!file) return;
    setLoading(true);
    setError(null);
    setResult(null);

    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await axios.post<AnalysisResult>(API_URL, formData);
      setResult(res.data);
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        setError(err.response?.data?.error ?? "Greška pri analizi dokumenta.");
      } else {
        setError("Neočekivana greška.");
      }
    } finally {
      setLoading(false);
    }
  };

  const riskColor = (score: string) => {
    switch (score) {
      case "HIGH":
        return "bg-red-100 border-red-500 text-red-800";
      case "MEDIUM":
        return "bg-yellow-100 border-yellow-500 text-yellow-800";
      default:
        return "bg-green-100 border-green-500 text-green-800";
    }
  };

  const RiskIcon = ({ score }: { score: string }) =>
    score === "HIGH" || score === "MEDIUM" ? (
      <ShieldAlert className="inline-block mr-2 h-5 w-5" />
    ) : (
      <ShieldCheck className="inline-block mr-2 h-5 w-5" />
    );

  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
      <div className="mx-auto max-w-3xl">
        <Link
          href="/"
          className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6"
        >
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        <div className="text-center mb-10">
          <h1 className="text-3xl font-bold text-gray-900">
            🔍 ZK Izvadak — Analiza rizika
          </h1>
          <p className="mt-2 text-gray-500">
            Uploadajte zemljišnoknjižni izvadak za analizu
          </p>
        </div>

        {/* Drop Zone */}
        <div
          onDrop={onDrop}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          className={`relative flex flex-col items-center justify-center rounded-xl border-2 border-dashed p-10 transition-colors cursor-pointer ${
            dragOver
              ? "border-blue-500 bg-blue-50"
              : "border-gray-300 bg-white hover:border-gray-400"
          }`}
        >
          <Upload className="h-10 w-10 text-gray-400 mb-3" />
          <p className="text-gray-600 font-medium">
            Povucite PDF ovdje ili{" "}
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
          <p className="text-xs text-gray-400 mt-1">Samo PDF — max 10 MB</p>
        </div>

        {file && (
          <div className="mt-4 flex items-center justify-between rounded-lg bg-white border border-gray-200 p-4">
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
              onClick={analyze}
              disabled={loading}
              className="rounded-lg bg-blue-600 px-5 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
            >
              {loading ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Analiziram...
                </span>
              ) : (
                "Analiziraj"
              )}
            </button>
          </div>
        )}

        {loading && (
          <div className="mt-8 text-center">
            <Loader2 className="mx-auto h-8 w-8 animate-spin text-blue-600" />
            <p className="mt-3 text-gray-500 font-medium">
              Analiziram pravni rizik...
            </p>
          </div>
        )}

        {error && (
          <div className="mt-6 flex items-center gap-2 rounded-lg border border-red-300 bg-red-50 p-4 text-red-700">
            <AlertTriangle className="h-5 w-5 flex-shrink-0" />
            <span className="text-sm">{error}</span>
          </div>
        )}

        {result && (
          <div className="mt-8 space-y-6">
            <div
              className={`flex items-center rounded-lg border-l-4 p-4 ${riskColor(
                result.risk_score
              )}`}
            >
              <RiskIcon score={result.risk_score} />
              <span className="font-bold text-lg">
                Rizik: {result.risk_score}
              </span>
            </div>

            <div className="rounded-lg bg-white border border-gray-200 p-5">
              <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                Komentar odvjetnika
              </h2>
              <p className="text-gray-800 leading-relaxed">
                {result.lawyer_comment}
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="rounded-lg bg-white border border-gray-200 p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                  Nekretnina
                </h3>
                <p className="text-gray-800 font-medium">
                  {result.property_id}
                </p>
              </div>
              <div className="rounded-lg bg-white border border-gray-200 p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">
                  Vlasnici
                </h3>
                <ul className="list-disc list-inside text-gray-800 space-y-1">
                  {result.owners.map((o, i) => (
                    <li key={i}>{o}</li>
                  ))}
                </ul>
              </div>
            </div>

            {result.active_mortgages.length > 0 && (
              <div className="rounded-lg bg-white border border-gray-200 overflow-hidden">
                <div className="px-5 py-3 border-b border-gray-100">
                  <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
                    Aktivne hipoteke
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
                    {result.active_mortgages.map((m, i) => (
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

            {result.active_disputes.length > 0 && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-5">
                <h3 className="text-sm font-semibold text-red-600 uppercase tracking-wide mb-2">
                  ⚠️ Aktivni sporovi
                </h3>
                <ul className="list-disc list-inside text-red-800 space-y-1">
                  {result.active_disputes.map((d, i) => (
                    <li key={i}>{d}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
