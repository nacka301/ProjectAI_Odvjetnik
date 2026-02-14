"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Clock,
  MapPin,
  Building2,
  Home,
  TreePine,
  Search,
  FileDown,
  Eye,
  ChevronDown,
  ChevronUp,
  AlertTriangle,
  Loader2,
} from "lucide-react";
import { getEvaluations, type EvaluationRow } from "@/lib/supabase";
import { generateValuationPdf } from "@/lib/generatePdf";
import type { ValuationResult } from "@/app/wizard/types";

const TYPE_ICONS: Record<string, React.ReactNode> = {
  Apartment: <Building2 className="h-5 w-5 text-blue-500" />,
  House: <Home className="h-5 w-5 text-green-500" />,
  Land: <TreePine className="h-5 w-5 text-amber-600" />,
};

const TYPE_LABELS: Record<string, string> = {
  Apartment: "Stan",
  House: "Kuća",
  Land: "Zemljište",
};

function formatPrice(n: number) {
  return n.toLocaleString("hr-HR");
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("hr-HR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function HistoryPage() {
  const [evaluations, setEvaluations] = useState<EvaluationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const data = await getEvaluations(100);
        setEvaluations(data);
      } catch {
        setError("Greška pri dohvaćanju povijesti. Provjerite Supabase konfiguraciju.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const filtered = evaluations.filter((e) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return (
      e.location.toLowerCase().includes(q) ||
      e.property_type.toLowerCase().includes(q) ||
      (TYPE_LABELS[e.property_type] || "").toLowerCase().includes(q) ||
      formatPrice(e.estimated_value).includes(q)
    );
  });

  const handlePdfExport = async (ev: EvaluationRow) => {
    setPdfLoadingId(ev.id);
    try {
      const raw = ev.raw_json_output as unknown as ValuationResult;
      await generateValuationPdf({
        result: raw,
        propertyType: ev.property_type,
        location: ev.location,
        areaM2: ev.size_m2 ?? 0,
      });
    } catch (e) {
      console.error("PDF export failed:", e);
    } finally {
      setPdfLoadingId(null);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
      <div className="mx-auto max-w-4xl">
        <Link href="/" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6">
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-gray-900 flex items-center justify-center gap-3">
            <Clock className="h-8 w-8 text-indigo-600" /> Povijest procjena
          </h1>
          <p className="mt-2 text-gray-500">
            Pregled svih spremljenih procjena tržišne vrijednosti
          </p>
        </div>

        {/* Search */}
        <div className="mb-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Pretraži po lokaciji, tipu ili vrijednosti..."
              className="w-full rounded-xl border border-gray-300 pl-10 pr-4 py-2.5 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        {/* Loading */}
        {loading && (
          <div className="text-center py-20">
            <Loader2 className="h-8 w-8 animate-spin text-indigo-500 mx-auto" />
            <p className="text-sm text-gray-500 mt-3">Učitavam povijest...</p>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="rounded-xl border border-red-300 bg-red-50 p-6 text-center">
            <AlertTriangle className="h-6 w-6 text-red-500 mx-auto mb-2" />
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {/* Empty state */}
        {!loading && !error && filtered.length === 0 && (
          <div className="text-center py-20">
            <Clock className="h-12 w-12 text-gray-300 mx-auto mb-4" />
            <p className="text-gray-500 font-medium">
              {searchQuery ? "Nema rezultata za pretragu." : "Još nema spremljenih procjena."}
            </p>
            {!searchQuery && (
              <Link href="/wizard" className="mt-4 inline-flex items-center gap-2 text-sm text-blue-600 hover:underline">
                Pokreni prvu procjenu →
              </Link>
            )}
          </div>
        )}

        {/* Evaluation Cards */}
        {!loading && filtered.length > 0 && (
          <div className="space-y-3">
            {filtered.map((ev) => {
              const isExpanded = expandedId === ev.id;
              const raw = ev.raw_json_output as unknown as ValuationResult;
              const confidence = raw?.math_confidence_score;

              return (
                <div key={ev.id} className="rounded-xl bg-white border border-gray-200 overflow-hidden transition hover:border-gray-300 hover:shadow-sm">
                  {/* Summary Row */}
                  <div
                    className="flex items-center gap-4 p-4 cursor-pointer"
                    onClick={() => setExpandedId(isExpanded ? null : ev.id)}
                  >
                    <div className="shrink-0">
                      {TYPE_ICONS[ev.property_type] || <Building2 className="h-5 w-5 text-gray-400" />}
                    </div>

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-semibold text-gray-900">
                          {TYPE_LABELS[ev.property_type] || ev.property_type}
                        </span>
                        <span className="text-xs text-gray-400">•</span>
                        <span className="text-sm text-gray-600 flex items-center gap-1">
                          <MapPin className="h-3 w-3" />
                          {ev.location}
                        </span>
                      </div>
                      <p className="text-xs text-gray-400 mt-0.5">{formatDate(ev.created_at)}</p>
                    </div>

                    <div className="text-right shrink-0">
                      <p className="text-lg font-bold text-gray-900">{formatPrice(ev.estimated_value)} €</p>
                      {confidence !== undefined && confidence > 0 && (
                        <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${
                          confidence >= 70 ? "bg-green-100 text-green-800" :
                          confidence >= 40 ? "bg-yellow-100 text-yellow-800" :
                          "bg-red-100 text-red-800"
                        }`}>
                          {confidence}/100
                        </span>
                      )}
                    </div>

                    <div className="shrink-0 text-gray-400">
                      {isExpanded ? <ChevronUp className="h-5 w-5" /> : <ChevronDown className="h-5 w-5" />}
                    </div>
                  </div>

                  {/* Expanded Detail */}
                  {isExpanded && (
                    <div className="border-t border-gray-100 bg-gray-50 p-5 space-y-4">
                      {/* Quick stats */}
                      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                        {raw?.calculated_price_per_m2 && (
                          <div className="rounded-lg bg-white border border-gray-200 p-3">
                            <p className="text-xs text-gray-500">EUR/m²</p>
                            <p className="text-sm font-bold text-gray-900">{formatPrice(Math.round(raw.calculated_price_per_m2))}</p>
                          </div>
                        )}
                        {raw?.total_listings_found !== undefined && (
                          <div className="rounded-lg bg-white border border-gray-200 p-3">
                            <p className="text-xs text-gray-500">Pronađeno</p>
                            <p className="text-sm font-bold text-gray-900">{raw.total_listings_found}</p>
                          </div>
                        )}
                        {raw?.usable_price_points !== undefined && (
                          <div className="rounded-lg bg-white border border-gray-200 p-3">
                            <p className="text-xs text-gray-500">Korištenih</p>
                            <p className="text-sm font-bold text-gray-900">{raw.usable_price_points}</p>
                          </div>
                        )}
                        {raw?.realization_discount_percent !== undefined && (
                          <div className="rounded-lg bg-white border border-gray-200 p-3">
                            <p className="text-xs text-gray-500">Diskont</p>
                            <p className="text-sm font-bold text-gray-900">{raw.realization_discount_percent.toFixed(1)}%</p>
                          </div>
                        )}
                      </div>

                      {/* Price range */}
                      {raw?.price_range && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-2">Raspon procjene</p>
                          <div className="flex items-center gap-4 text-sm">
                            <span className="text-amber-700 font-medium">{formatPrice(raw.price_range.conservative_total)} €</span>
                            <span className="text-gray-300">→</span>
                            <span className="text-blue-700 font-bold">{formatPrice(raw.price_range.realistic_total)} €</span>
                            <span className="text-gray-300">→</span>
                            <span className="text-green-700 font-medium">{formatPrice(raw.price_range.optimistic_total)} €</span>
                          </div>
                        </div>
                      )}

                      {/* Reasoning excerpt */}
                      {raw?.valuation_reasoning && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-1">Stručno mišljenje</p>
                          <p className="text-xs text-gray-700 leading-relaxed line-clamp-4">{raw.valuation_reasoning}</p>
                        </div>
                      )}

                      {/* Actions */}
                      <div className="flex items-center gap-3 pt-1">
                        <button
                          onClick={() => handlePdfExport(ev)}
                          disabled={pdfLoadingId === ev.id}
                          className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition flex items-center gap-1.5"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {pdfLoadingId === ev.id ? "Generiram..." : "Preuzmi PDF"}
                        </button>
                        <Link
                          href="/wizard"
                          className="rounded-lg bg-gray-100 px-4 py-2 text-xs font-medium text-gray-700 hover:bg-gray-200 transition flex items-center gap-1.5"
                        >
                          <Eye className="h-3.5 w-3.5" /> Nova procjena
                        </Link>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {/* Count */}
        {!loading && filtered.length > 0 && (
          <p className="text-center text-xs text-gray-400 mt-6">
            Prikazano {filtered.length} od {evaluations.length} procjena
          </p>
        )}
      </div>
    </div>
  );
}
