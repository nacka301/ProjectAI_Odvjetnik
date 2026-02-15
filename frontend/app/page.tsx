"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Search as SearchIcon,
  Home,
  Scale,
  BookOpen,
  ScrollText,
  Clock,
  Building2,
  TreePine,
  MapPin,
  Upload,
  FileText,
  AlertTriangle,
  ChevronRight,
  Loader2,
  TrendingDown,
  ShieldAlert,
  CheckCircle,
  XCircle,
  ArrowRight,
} from "lucide-react";
import { getEvaluations, type EvaluationRow } from "@/lib/supabase";

// ── Helpers ──

function formatPrice(n: number) {
  return n.toLocaleString("hr-HR");
}

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "upravo";
  if (mins < 60) return `prije ${mins} min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `prije ${hrs}h`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `prije ${days}d`;
  return new Date(iso).toLocaleDateString("hr-HR");
}

const TYPE_ICONS: Record<string, React.ReactNode> = {
  Apartment: <Building2 className="h-4 w-4 text-blue-500" />,
  House: <Home className="h-4 w-4 text-green-500" />,
  Land: <TreePine className="h-4 w-4 text-amber-600" />,
};
const TYPE_LABELS: Record<string, string> = {
  Apartment: "Stan",
  House: "Kuća",
  Land: "Zemljište",
};

// ── Types for raw JSON output ──
interface RawEvaluation {
  math_confidence_score?: number;
  confidence_level?: string;
  calculated_price_per_m2?: number;
  total_listings_found?: number;
  usable_price_points?: number;
  valuation_reasoning?: string;
}

// ═══════════════════════════════════════════════════════════
//  Dashboard Component — "Legal Command Center"
// ═══════════════════════════════════════════════════════════

export default function Dashboard() {
  const router = useRouter();
  const [evaluations, setEvaluations] = useState<EvaluationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [dragOver, setDragOver] = useState(false);
  const dropRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    (async () => {
      try {
        const data = await getEvaluations(20);
        setEvaluations(data);
      } catch {
        // Silently handle — dashboard still works without history
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  // ── Quick Drop Zone logic ──
  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      const files = Array.from(e.dataTransfer.files).filter((f) =>
        f.name.toLowerCase().endsWith(".pdf")
      );
      if (files.length === 0) return;

      // Heuristic: multiple files → chronology; single file → detect by name
      if (files.length > 1) {
        router.push("/kronologija");
        return;
      }

      const name = files[0].name.toLowerCase();
      if (
        name.includes("zk") ||
        name.includes("izvadak") ||
        name.includes("gruntovni") ||
        name.includes("vlastovnica") ||
        name.includes("posjedovnica")
      ) {
        router.push("/zk-procjena");
      } else if (
        name.includes("ugovor") ||
        name.includes("predugovor") ||
        name.includes("aneks") ||
        name.includes("kupoprodaj")
      ) {
        router.push("/ugovor");
      } else {
        // Default: contract analysis (most common single-document use case)
        router.push("/ugovor");
      }
    },
    [router]
  );

  // ── Status badge for evaluation ──
  function getStatusBadge(ev: EvaluationRow) {
    const raw = ev.raw_json_output as unknown as RawEvaluation;
    const score = raw?.math_confidence_score ?? 0;

    if (score >= 70) {
      return (
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-green-700 bg-green-50 px-2.5 py-1 rounded-full">
          <CheckCircle className="h-3 w-3" />
          Pouzdano ({score}/100)
        </span>
      );
    }
    if (score >= 40) {
      return (
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-amber-700 bg-amber-50 px-2.5 py-1 rounded-full">
          <AlertTriangle className="h-3 w-3" />
          Ograničeno ({score}/100)
        </span>
      );
    }
    if (score > 0) {
      return (
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-red-700 bg-red-50 px-2.5 py-1 rounded-full">
          <XCircle className="h-3 w-3" />
          Nizak ({score}/100)
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 bg-slate-100 px-2.5 py-1 rounded-full">
        <TrendingDown className="h-3 w-3" />
        Procjena
      </span>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 font-sans">
      {/* ═══════════ Top Navigation ═══════════ */}
      <header className="bg-slate-900 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
              <Scale className="h-5 w-5 text-white" />
            </div>
            <div>
              <h1 className="text-lg font-bold tracking-tight">Legal AI</h1>
              <p className="text-[10px] text-slate-400 uppercase tracking-widest">
                Command Center
              </p>
            </div>
          </div>
          <Link
            href="/history"
            className="text-xs text-slate-400 hover:text-white transition flex items-center gap-1"
          >
            <Clock className="h-3.5 w-3.5" />
            Puna povijest
          </Link>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-6 space-y-6">
        {/* ═══════════ 1. ACTION BAR ═══════════ */}
        <section>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
            {/* Nova Procjena */}
            <Link
              href="/wizard"
              className="group relative bg-white rounded-xl border border-slate-200 p-5 hover:border-blue-400 hover:shadow-md transition overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-20 h-20 bg-blue-50 rounded-bl-[40px] -mr-2 -mt-2 group-hover:bg-blue-100 transition" />
              <div className="relative">
                <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center mb-3 group-hover:bg-blue-200 transition">
                  <SearchIcon className="h-5 w-5 text-blue-600" />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm">
                  Nova Procjena
                </h3>
                <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                  Izračun tržišne vrijednosti nekretnine
                </p>
              </div>
            </Link>

            {/* Analiza Ugovora */}
            <Link
              href="/ugovor"
              className="group relative bg-white rounded-xl border border-slate-200 p-5 hover:border-emerald-400 hover:shadow-md transition overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-20 h-20 bg-emerald-50 rounded-bl-[40px] -mr-2 -mt-2 group-hover:bg-emerald-100 transition" />
              <div className="relative">
                <div className="w-10 h-10 rounded-lg bg-emerald-100 flex items-center justify-center mb-3 group-hover:bg-emerald-200 transition">
                  <ShieldAlert className="h-5 w-5 text-emerald-600" />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm">
                  Analiza Ugovora
                </h3>
                <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                  AI provjera rizika i klauzula
                </p>
              </div>
            </Link>

            {/* ZK Izvadak */}
            <Link
              href="/zk-procjena"
              className="group relative bg-white rounded-xl border border-slate-200 p-5 hover:border-indigo-400 hover:shadow-md transition overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-20 h-20 bg-indigo-50 rounded-bl-[40px] -mr-2 -mt-2 group-hover:bg-indigo-100 transition" />
              <div className="relative">
                <div className="w-10 h-10 rounded-lg bg-indigo-100 flex items-center justify-center mb-3 group-hover:bg-indigo-200 transition">
                  <BookOpen className="h-5 w-5 text-indigo-600" />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm">
                  ZK Izvadak
                </h3>
                <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                  Dohvat i analiza tereta (OCR)
                </p>
              </div>
            </Link>

            {/* Kronologija */}
            <Link
              href="/kronologija"
              className="group relative bg-white rounded-xl border border-slate-200 p-5 hover:border-violet-400 hover:shadow-md transition overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-20 h-20 bg-violet-50 rounded-bl-[40px] -mr-2 -mt-2 group-hover:bg-violet-100 transition" />
              <div className="relative">
                <div className="w-10 h-10 rounded-lg bg-violet-100 flex items-center justify-center mb-3 group-hover:bg-violet-200 transition">
                  <ScrollText className="h-5 w-5 text-violet-600" />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm">
                  Kronologija
                </h3>
                <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                  Slaganje spisa iz dokumenata
                </p>
              </div>
            </Link>
          </div>
        </section>

        {/* ═══════════ 2. MAIN CONTENT: Case Feed + Drop Zone ═══════════ */}
        <section className="grid grid-cols-1 lg:grid-cols-[1fr_340px] gap-6">
          {/* ── LEFT: Smart Case Feed ── */}
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-700 uppercase tracking-wide">
                Nedavni predmeti
              </h2>
              <Link
                href="/history"
                className="text-xs text-blue-600 hover:underline flex items-center gap-1"
              >
                Vidi sve <ArrowRight className="h-3 w-3" />
              </Link>
            </div>

            {/* Loading state */}
            {loading && (
              <div className="bg-white rounded-xl border border-slate-200 p-12 text-center">
                <Loader2 className="h-6 w-6 animate-spin text-slate-400 mx-auto" />
                <p className="text-xs text-slate-400 mt-2">
                  Učitavam povijest...
                </p>
              </div>
            )}

            {/* Empty state */}
            {!loading && evaluations.length === 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-12 text-center">
                <div className="w-12 h-12 bg-slate-100 rounded-xl flex items-center justify-center mx-auto mb-3">
                  <Clock className="h-6 w-6 text-slate-300" />
                </div>
                <p className="text-sm text-slate-500 font-medium">
                  Još nema predmeta
                </p>
                <p className="text-xs text-slate-400 mt-1">
                  Pokrenite prvu procjenu ili analizu da započnete
                </p>
                <Link
                  href="/wizard"
                  className="inline-flex items-center gap-1 mt-4 text-xs font-medium text-blue-600 hover:underline"
                >
                  Nova procjena <ArrowRight className="h-3 w-3" />
                </Link>
              </div>
            )}

            {/* Case table */}
            {!loading && evaluations.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                {/* Table header */}
                <div className="grid grid-cols-[1fr_auto_auto_44px] gap-2 px-4 py-2.5 bg-slate-50 border-b border-slate-200 text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  <span>Predmet</span>
                  <span className="w-36 text-center">Status</span>
                  <span className="w-24 text-right">Vrijednost</span>
                  <span />
                </div>

                {/* Rows */}
                <div className="divide-y divide-slate-100">
                  {evaluations.slice(0, 10).map((ev) => {
                    const raw =
                      ev.raw_json_output as unknown as RawEvaluation;
                    return (
                      <Link
                        key={ev.id}
                        href="/history"
                        className="grid grid-cols-[1fr_auto_auto_44px] gap-2 items-center px-4 py-3 hover:bg-slate-50 transition group"
                      >
                        {/* Left: type + location */}
                        <div className="flex items-center gap-3 min-w-0">
                          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center shrink-0">
                            {TYPE_ICONS[ev.property_type] || (
                              <Building2 className="h-4 w-4 text-slate-400" />
                            )}
                          </div>
                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-medium text-slate-900 truncate">
                                {TYPE_LABELS[ev.property_type] ||
                                  ev.property_type}
                              </span>
                              <span className="text-[10px] text-slate-300">
                                •
                              </span>
                              <span className="text-xs text-slate-500 truncate flex items-center gap-1">
                                <MapPin className="h-3 w-3 shrink-0" />
                                {ev.location}
                              </span>
                            </div>
                            <span className="text-[11px] text-slate-400">
                              {timeAgo(ev.created_at)}
                              {ev.size_m2 ? ` • ${ev.size_m2} m²` : ""}
                            </span>
                          </div>
                        </div>

                        {/* Status badge */}
                        <div className="w-36 flex justify-center">
                          {getStatusBadge(ev)}
                        </div>

                        {/* Value */}
                        <div className="w-24 text-right">
                          <span className="text-sm font-bold text-slate-900">
                            {formatPrice(ev.estimated_value)} €
                          </span>
                          {raw?.calculated_price_per_m2 ? (
                            <span className="block text-[10px] text-slate-400">
                              {formatPrice(
                                Math.round(raw.calculated_price_per_m2)
                              )}{" "}
                              €/m²
                            </span>
                          ) : null}
                        </div>

                        {/* Arrow */}
                        <div className="flex justify-center">
                          <ChevronRight className="h-4 w-4 text-slate-300 group-hover:text-slate-500 transition" />
                        </div>
                      </Link>
                    );
                  })}
                </div>

                {/* Footer */}
                {evaluations.length > 10 && (
                  <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-200 text-center">
                    <Link
                      href="/history"
                      className="text-xs text-blue-600 hover:underline"
                    >
                      Prikaži svih {evaluations.length} predmeta →
                    </Link>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ── RIGHT: Quick Drop Zone + Stats ── */}
          <div className="space-y-4">
            <h2 className="text-sm font-semibold text-slate-700 uppercase tracking-wide">
              AI Inbox
            </h2>

            <div
              ref={dropRef}
              onDragOver={(e) => {
                e.preventDefault();
                setDragOver(true);
              }}
              onDragLeave={() => setDragOver(false)}
              onDrop={handleDrop}
              className={`rounded-xl border-2 border-dashed p-8 text-center transition-all ${
                dragOver
                  ? "border-blue-500 bg-blue-50 scale-[1.02]"
                  : "border-slate-300 bg-white hover:border-slate-400"
              }`}
            >
              <div
                className={`w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-4 transition ${
                  dragOver ? "bg-blue-100" : "bg-slate-100"
                }`}
              >
                <Upload
                  className={`h-7 w-7 transition ${
                    dragOver ? "text-blue-600" : "text-slate-400"
                  }`}
                />
              </div>
              <p
                className={`font-medium text-sm mb-1 transition ${
                  dragOver ? "text-blue-700" : "text-slate-700"
                }`}
              >
                Povuci dokument ovdje
              </p>
              <p className="text-xs text-slate-400 leading-relaxed">
                Ugovor, ZK izvadak ili hrpu
                <br />
                računa — AI automatski prepoznaje
              </p>

              {/* Quick file type hints */}
              <div className="mt-5 space-y-2">
                <div className="flex items-center gap-2 text-[11px] text-slate-400">
                  <FileText className="h-3.5 w-3.5 text-emerald-500" />
                  <span>ugovor.pdf → Analiza rizika</span>
                </div>
                <div className="flex items-center gap-2 text-[11px] text-slate-400">
                  <BookOpen className="h-3.5 w-3.5 text-indigo-500" />
                  <span>zk_izvadak.pdf → Tereti + Procjena</span>
                </div>
                <div className="flex items-center gap-2 text-[11px] text-slate-400">
                  <ScrollText className="h-3.5 w-3.5 text-violet-500" />
                  <span>Više datoteka → Kronologija</span>
                </div>
              </div>
            </div>

            {/* Quick Stats */}
            {!loading && evaluations.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
                <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  Brzi pregled
                </h3>
                <div className="grid grid-cols-2 gap-3">
                  <div className="bg-slate-50 rounded-lg p-3 text-center">
                    <div className="text-xl font-bold text-slate-900">
                      {evaluations.length}
                    </div>
                    <div className="text-[10px] text-slate-400 mt-0.5">
                      Ukupno procjena
                    </div>
                  </div>
                  <div className="bg-slate-50 rounded-lg p-3 text-center">
                    <div className="text-xl font-bold text-slate-900">
                      {
                        evaluations.filter((e) => {
                          const r =
                            e.raw_json_output as unknown as RawEvaluation;
                          return (r?.math_confidence_score ?? 0) >= 70;
                        }).length
                      }
                    </div>
                    <div className="text-[10px] text-slate-400 mt-0.5">
                      Pouzdanih
                    </div>
                  </div>
                </div>

                {/* Top locations */}
                {(() => {
                  const locCounts: Record<string, number> = {};
                  evaluations.forEach((e) => {
                    const city =
                      e.location.split(",")[0]?.trim() || e.location;
                    locCounts[city] = (locCounts[city] || 0) + 1;
                  });
                  const top = Object.entries(locCounts)
                    .sort((a, b) => b[1] - a[1])
                    .slice(0, 3);
                  if (top.length === 0) return null;
                  return (
                    <div>
                      <div className="text-[10px] text-slate-400 mb-1.5">
                        Najčešće lokacije
                      </div>
                      <div className="space-y-1">
                        {top.map(([loc, count]) => (
                          <div
                            key={loc}
                            className="flex items-center justify-between text-xs"
                          >
                            <span className="text-slate-600 truncate">
                              {loc}
                            </span>
                            <span className="text-slate-400 shrink-0 ml-2">
                              {count}×
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>
                  );
                })()}
              </div>
            )}
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="max-w-7xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 flex items-center justify-between text-[11px] text-slate-400">
          <span>Legal AI Assistant v2.0</span>
          <span>
            AI analiza ne zamjenjuje pravni savjet ovlaštenog odvjetnika
          </span>
        </div>
      </footer>
    </div>
  );
}
