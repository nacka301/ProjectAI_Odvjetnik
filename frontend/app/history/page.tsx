"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Clock,
  Building2,
  Search,
  FileDown,
  Eye,
  ChevronDown,
  ChevronUp,
  AlertTriangle,
  Loader2,
  ShieldAlert,
  Trash2,
} from "lucide-react";
import {
  getRecentActivity,
  deleteActivityItem,
  getAuditResults,
  type ActivityItem,
  type ActivityType,
  type AuditRow,
  getEvaluations,
  type EvaluationRow,
} from "@/lib/supabase";
import { generateValuationPdf } from "@/lib/generatePdf";
import type { ValuationResult } from "@/app/wizard/types";

// ── Tab definitions ──
type TabKey = "all" | ActivityType;

const TABS: { key: TabKey; label: string }[] = [
  { key: "all", label: "Sve" },
  { key: "valuation", label: "Procjene" },
  { key: "contract", label: "Ugovori" },
];

const ACTIVITY_ICONS: Record<ActivityType, React.ReactNode> = {
  valuation: <Building2 className="h-5 w-5 text-blue-500" />,
  contract: <ShieldAlert className="h-5 w-5 text-rose-500" />,
  chronology: <Building2 className="h-5 w-5 text-gray-400" />,
  legal: <Building2 className="h-5 w-5 text-gray-400" />,
  invoice: <Building2 className="h-5 w-5 text-gray-400" />,
};

const ACTIVITY_LABELS: Record<ActivityType, string> = {
  valuation: "Procjena",
  contract: "Ugovor",
  chronology: "Kronologija",
  legal: "Pravna analiza",
  invoice: "e-Račun",
};

/* Note: chronology, legal, invoice labels kept for backward compat with existing DB rows */

const BADGE_COLORS: Record<string, string> = {
  green: "bg-green-100 text-green-800",
  amber: "bg-yellow-100 text-yellow-800",
  red: "bg-red-100 text-red-800",
};

const AUDIT_VERDICT_LABELS: Record<string, { label: string; color: string; icon: string }> = {
  CONFIRMED:  { label: "Potvrđeno",  color: "bg-green-100 text-green-800 border-green-300",  icon: "✅" },
  SUSPICIOUS: { label: "Sumnjivo",   color: "bg-yellow-100 text-yellow-800 border-yellow-300", icon: "⚠️" },
  REJECTED:   { label: "Odbijeno",   color: "bg-red-100 text-red-800 border-red-300",     icon: "❌" },
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
  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [evaluationMap, setEvaluationMap] = useState<Map<string, EvaluationRow>>(new Map());
  const [auditMap, setAuditMap] = useState<Map<string, AuditRow>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabKey>("all");
  const [deletingId, setDeletingId] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const [items, evals, audits] = await Promise.all([
          getRecentActivity(100),
          getEvaluations(100),
          getAuditResults(100),
        ]);
        setActivity(items);
        const map = new Map<string, EvaluationRow>();
        for (const e of evals) map.set(e.id, e);
        setEvaluationMap(map);
        const aMap = new Map<string, AuditRow>();
        for (const a of audits) aMap.set(a.evaluation_id, a);
        setAuditMap(aMap);
      } catch {
        setError("Greška pri dohvaćanju povijesti. Provjerite Supabase konfiguraciju.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const filtered = activity.filter((item) => {
    // Tab filter
    if (activeTab !== "all" && item.type !== activeTab) return false;
    // Search filter
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return (
      item.title.toLowerCase().includes(q) ||
      item.subtitle.toLowerCase().includes(q) ||
      (item.badge || "").toLowerCase().includes(q) ||
      ACTIVITY_LABELS[item.type].toLowerCase().includes(q)
    );
  });

  const handlePdfExport = async (itemId: string) => {
    const ev = evaluationMap.get(itemId);
    if (!ev) return;
    setPdfLoadingId(itemId);
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

  const handleDelete = async (item: ActivityItem) => {
    const confirmed = window.confirm(
      `Jeste li sigurni da želite obrisati "${item.title}"?\nOvaj zapis će biti trajno izbrisan.`
    );
    if (!confirmed) return;

    setDeletingId(item.id);
    try {
      const ok = await deleteActivityItem(item.type, item.id);
      if (ok) {
        setActivity((prev) => prev.filter((a) => a.id !== item.id));
        setEvaluationMap((prev) => {
          const next = new Map(prev);
          next.delete(item.id);
          return next;
        });
        if (expandedId === item.id) setExpandedId(null);
      } else {
        alert("Greška pri brisanju zapisa. Pokušajte ponovo.");
      }
    } catch {
      alert("Greška pri brisanju zapisa.");
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 font-sans">
      <div className="mx-auto max-w-4xl">
        <Link href="/" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-6">
          <ArrowLeft className="h-4 w-4" /> Natrag
        </Link>

        <div className="text-center mb-8">
          <h1 className="text-2xl sm:text-3xl font-bold text-gray-900 flex items-center justify-center gap-2 sm:gap-3">
            <Clock className="h-6 w-6 sm:h-8 sm:w-8 text-indigo-600" /> Povijest predmeta
          </h1>
          <p className="mt-2 text-gray-500">
            Pregled svih spremljenih analiza i procjena
          </p>
        </div>

        {/* Tabs */}
        <div className="flex gap-2 mb-4 flex-wrap">
          {TABS.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                activeTab === tab.key
                  ? "bg-indigo-600 text-white"
                  : "bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"
              }`}
            >
              {tab.label}
            </button>
          ))}
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
              {searchQuery || activeTab !== "all" ? "Nema rezultata za odabrane filtere." : "Još nema spremljenih predmeta."}
            </p>
            {!searchQuery && activeTab === "all" && (
              <Link href="/wizard" className="mt-4 inline-flex items-center gap-2 text-sm text-blue-600 hover:underline">
                Pokreni prvu procjenu →
              </Link>
            )}
          </div>
        )}

        {/* Activity Cards */}
        {!loading && filtered.length > 0 && (
          <div className="space-y-3">
            {filtered.map((item) => {
              const isExpanded = expandedId === item.id;
              const isValuation = item.type === "valuation";
              const ev = isValuation ? evaluationMap.get(item.id) : null;
              const raw = ev ? (ev.raw_json_output as unknown as ValuationResult) : null;
              const audit = isValuation ? auditMap.get(item.id) : null;
              const verdictInfo = audit ? AUDIT_VERDICT_LABELS[audit.verdict] : null;

              return (
                <div key={item.id} className="rounded-xl bg-white border border-gray-200 overflow-hidden transition hover:border-gray-300 hover:shadow-sm">
                  {/* Summary Row */}
                  <div
                    className="flex items-center gap-4 p-4 cursor-pointer"
                    onClick={() => setExpandedId(isExpanded ? null : item.id)}
                  >
                    <div className="shrink-0">
                      {ACTIVITY_ICONS[item.type]}
                    </div>

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400">
                          {ACTIVITY_LABELS[item.type]}
                        </span>
                        <span className="text-xs text-gray-400">•</span>
                        <span className="text-sm font-semibold text-gray-900 truncate">
                          {item.title}
                        </span>
                      </div>
                      <p className="text-xs text-gray-400 mt-0.5">
                        {formatDate(item.created_at)}
                        {item.subtitle ? ` · ${item.subtitle}` : ""}
                      </p>
                      {verdictInfo && (
                        <span className={`inline-flex items-center gap-1 mt-1 text-[10px] font-bold px-2 py-0.5 rounded-full border ${verdictInfo.color}`}>
                          {verdictInfo.icon} Perplexity: {verdictInfo.label}
                          {audit && audit.overall_confidence > 0 && (
                            <span className="opacity-70">({audit.overall_confidence}%)</span>
                          )}
                        </span>
                      )}
                    </div>

                    <div className="text-right shrink-0">
                      {item.badge && (
                        <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${
                          item.badgeColor ? BADGE_COLORS[item.badgeColor] || "bg-gray-100 text-gray-600" : "bg-gray-100 text-gray-600"
                        }`}>
                          {item.badge}
                        </span>
                      )}
                    </div>

                    <div className="shrink-0 flex items-center gap-1">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          handleDelete(item);
                        }}
                        disabled={deletingId === item.id}
                        className="p-1.5 rounded-lg text-gray-300 hover:text-red-500 hover:bg-red-50 transition disabled:opacity-50"
                        title="Obriši zapis"
                      >
                        {deletingId === item.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Trash2 className="h-4 w-4" />
                        )}
                      </button>
                      {isExpanded ? <ChevronUp className="h-5 w-5 text-gray-400" /> : <ChevronDown className="h-5 w-5 text-gray-400" />}
                    </div>
                  </div>

                  {/* Expanded Detail — only for valuations (they have rich data) */}
                  {isExpanded && isValuation && raw && (
                    <div className="border-t border-gray-100 bg-gray-50 p-5 space-y-4">
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

                      {raw?.valuation_reasoning && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-1">Stručno mišljenje</p>
                          <p className="text-xs text-gray-700 leading-relaxed line-clamp-4">{raw.valuation_reasoning}</p>
                        </div>
                      )}

                      {/* Perplexity AI Audit Result */}
                      {audit && verdictInfo && (
                        <div className={`rounded-lg border p-3 ${
                          audit.verdict === "CONFIRMED" ? "bg-green-50 border-green-200" :
                          audit.verdict === "SUSPICIOUS" ? "bg-yellow-50 border-yellow-200" :
                          "bg-red-50 border-red-200"
                        }`}>
                          <div className="flex items-center gap-2 mb-1">
                            <span className="text-sm">{verdictInfo.icon}</span>
                            <p className="text-xs font-bold">
                              Perplexity provjera: {verdictInfo.label}
                            </p>
                            {audit.overall_confidence > 0 && (
                              <span className="text-[10px] font-medium opacity-70">
                                Pouzdanost: {audit.overall_confidence}%
                              </span>
                            )}
                          </div>
                          {audit.summary && (
                            <p className="text-xs text-gray-700 leading-relaxed line-clamp-3">{audit.summary}</p>
                          )}
                          {audit.recommendation && (
                            <p className="text-xs text-gray-500 mt-1 italic">💡 {audit.recommendation}</p>
                          )}
                          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 mt-2">
                            <div className="text-center">
                              <p className="text-[10px] text-gray-500">Cijena</p>
                              <p className="text-xs font-bold">{audit.price_score}/100</p>
                            </div>
                            <div className="text-center">
                              <p className="text-[10px] text-gray-500">Lokacija</p>
                              <p className="text-xs font-bold">{audit.location_score}/100</p>
                            </div>
                            <div className="text-center">
                              <p className="text-[10px] text-gray-500">Metoda</p>
                              <p className="text-xs font-bold">{audit.methodology_score}/100</p>
                            </div>
                            <div className="text-center">
                              <p className="text-[10px] text-gray-500">Podaci</p>
                              <p className="text-xs font-bold">{audit.data_quality_score}/100</p>
                            </div>
                          </div>
                        </div>
                      )}

                      <div className="flex items-center gap-3 pt-1">
                        <button
                          onClick={() => handlePdfExport(item.id)}
                          disabled={pdfLoadingId === item.id}
                          className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition flex items-center gap-1.5"
                        >
                          <FileDown className="h-3.5 w-3.5" />
                          {pdfLoadingId === item.id ? "Generiram..." : "Preuzmi PDF"}
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

                  {/* Expanded Detail — for non-valuation items (simple view) */}
                  {isExpanded && !isValuation && (
                    <div className="border-t border-gray-100 bg-gray-50 p-5">
                      <p className="text-sm text-gray-600">
                        {item.subtitle}
                      </p>
                      {item.badge && (
                        <p className="text-xs text-gray-400 mt-2">
                          Status: <span className="font-medium text-gray-700">{item.badge}</span>
                        </p>
                      )}
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
            Prikazano {filtered.length} od {activity.length} predmeta
          </p>
        )}
      </div>
    </div>
  );
}
