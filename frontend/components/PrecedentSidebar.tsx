"use client";

import { useState } from "react";
import {
  BookOpen,
  Search,
  Loader2,
  ThumbsUp,
  ThumbsDown,
  Minus,
  Scale,
  X,
  ChevronDown,
  ChevronUp,
  Shield,
  AlertTriangle,
  Info,
} from "lucide-react";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

interface CourtPrecedent {
  case_number: string;
  court: string;
  date: string;
  summary: string;
  legal_principle: string;
  relevance_to_case: string;
  alignment: string;
  legal_basis: string;
  stability: string;        // V34
  similarity_score: number;  // V34
}

interface PrecedentResult {
  legal_issue: string;
  precedents: CourtPrecedent[];
}

function AlignmentIcon({ alignment }: { alignment: string }) {
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

// V34: Stability badge
function StabilityIcon({ stability }: { stability: string }) {
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
      <Info className="h-3 w-3" /> Nova
    </span>
  );
}

interface PrecedentSidebarProps {
  context?: string;
  isOpen: boolean;
  onClose: () => void;
}

export default function PrecedentSidebar({
  context,
  isOpen,
  onClose,
}: PrecedentSidebarProps) {
  const [query, setQuery] = useState(context ?? "");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<PrecedentResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedIdx, setExpandedIdx] = useState<number | null>(null);

  const searchPrecedents = async () => {
    if (!query.trim() || query.trim().length < 20) {
      setError("Unesite barem 20 znakova za pretragu.");
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const res = await fetch(`${API}/api/legal/precedents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ context: query }),
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(
          (errData as Record<string, string>)?.error ??
            "Greška u pretrazi"
        );
      }

      const data: PrecedentResult = await res.json();
      setResult(data);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Nepoznata greška"
      );
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-y-0 right-0 w-full sm:w-[440px] bg-white shadow-2xl border-l border-slate-200 z-50 flex flex-col">
      {/* Header */}
      <div className="px-5 py-4 bg-gradient-to-r from-indigo-50 to-purple-50 border-b border-slate-200 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-indigo-100 flex items-center justify-center">
            <BookOpen className="h-5 w-5 text-indigo-700" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-slate-800">
              Relevantna Sudska Praksa
            </h2>
            <p className="text-[10px] text-slate-500">
              AI pretraga presuda hrvatskih sudova
            </p>
          </div>
        </div>
        <button
          onClick={onClose}
          className="p-2 rounded-lg hover:bg-indigo-100 transition"
        >
          <X className="h-4 w-4 text-slate-500" />
        </button>
      </div>

      {/* Search Box */}
      <div className="px-5 py-4 border-b border-slate-100 shrink-0">
        <div className="relative">
          <textarea
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Opišite pravno pitanje ili zalijepite tekst ugovora/kronologije..."
            rows={3}
            className="w-full text-sm border border-slate-300 rounded-xl px-4 py-3 pr-12 resize-none focus:ring-2 focus:ring-indigo-300 focus:border-indigo-400 outline-none"
          />
          <button
            onClick={searchPrecedents}
            disabled={loading}
            className="absolute right-2 bottom-2 p-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition"
          >
            {loading ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Search className="h-4 w-4" />
            )}
          </button>
        </div>
        {error && (
          <p className="text-xs text-red-500 mt-2">{error}</p>
        )}
      </div>

      {/* Results */}
      <div className="flex-1 overflow-y-auto">
        {loading && (
          <div className="p-12 text-center">
            <Loader2 className="h-8 w-8 animate-spin text-indigo-400 mx-auto" />
            <p className="text-sm text-slate-500 mt-3">
              Pretražujem sudsku praksu...
            </p>
            <p className="text-xs text-slate-400 mt-1">
              VSRH, VTS, županijski sudovi
            </p>
          </div>
        )}

        {result && (
          <div className="p-5 space-y-4">
            {/* Legal Issue */}
            <div className="p-3 bg-indigo-50 rounded-xl border border-indigo-100">
              <div className="flex items-start gap-2">
                <Scale className="h-4 w-4 text-indigo-600 mt-0.5 shrink-0" />
                <div>
                  <p className="text-[10px] font-semibold text-indigo-600 uppercase tracking-wider mb-1">
                    Identificirano pravno pitanje
                  </p>
                  <p className="text-sm text-indigo-900 font-medium leading-snug">
                    {result.legal_issue}
                  </p>
                </div>
              </div>
            </div>

            {/* Precedents */}
            <div className="space-y-3">
              <p className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">
                Pronađene presude ({result.precedents.length})
              </p>

              {result.precedents.map((p, idx) => (
                <div
                  key={idx}
                  className="border border-slate-200 rounded-xl overflow-hidden hover:border-indigo-200 transition"
                >
                  {/* Precedent header */}
                  <button
                    onClick={() =>
                      setExpandedIdx(expandedIdx === idx ? null : idx)
                    }
                    className="w-full text-left px-4 py-3 flex items-start justify-between gap-2"
                  >
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1 flex-wrap">
                        <span className="text-xs font-mono font-bold text-slate-800">
                          {p.case_number}
                        </span>
                        <AlignmentIcon alignment={p.alignment} />
                        {p.stability && <StabilityIcon stability={p.stability} />}
                      </div>
                      <p className="text-xs text-slate-600 leading-snug">
                        {p.summary}
                      </p>
                      <div className="flex items-center gap-2 mt-1.5">
                        <span className="text-[10px] text-slate-400">
                          {p.court}
                        </span>
                        <span className="text-[10px] text-slate-300">
                          •
                        </span>
                        <span className="text-[10px] text-slate-400">
                          {p.date}
                        </span>
                        {p.legal_basis && (
                          <>
                            <span className="text-[10px] text-slate-300">
                              •
                            </span>
                            <span className="text-[10px] text-blue-500 font-medium">
                              {p.legal_basis}
                            </span>
                          </>
                        )}
                        {p.similarity_score > 0 && (
                          <>
                            <span className="text-[10px] text-slate-300">
                              •
                            </span>
                            <span className="text-[10px] text-indigo-500 font-medium">
                              Sličnost: {p.similarity_score}%
                            </span>
                          </>
                        )}
                      </div>
                    </div>
                    {expandedIdx === idx ? (
                      <ChevronUp className="h-4 w-4 text-slate-400 shrink-0" />
                    ) : (
                      <ChevronDown className="h-4 w-4 text-slate-400 shrink-0" />
                    )}
                  </button>

                  {/* Expanded details */}
                  {expandedIdx === idx && (
                    <div className="px-4 pb-4 space-y-3">
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
        )}

        {/* Empty state */}
        {!loading && !result && (
          <div className="p-12 text-center">
            <div className="w-16 h-16 bg-slate-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <BookOpen className="h-8 w-8 text-slate-300" />
            </div>
            <p className="text-sm text-slate-500 font-medium">
              Pretražite sudsku praksu
            </p>
            <p className="text-xs text-slate-400 mt-1">
              Unesite pravni kontekst ili opis spora
            </p>
          </div>
        )}
      </div>

      {/* Footer disclaimer */}
      <div className="px-5 py-3 bg-blue-50 border-t border-blue-100 shrink-0">
        <div className="flex items-start gap-2">
          <Info className="h-3.5 w-3.5 text-blue-500 mt-0.5 shrink-0" />
          <p className="text-[10px] text-blue-700 leading-relaxed">
            <strong>Napomena:</strong> Presude su ilustrativne — generirane AI modelom
            na temelju ustaljenih pravnih stajališta, a ne iz stvarne sudske baze.
            Stabilnost prakse podliježe AI procjeni.
          </p>
        </div>
      </div>
    </div>
  );
}
