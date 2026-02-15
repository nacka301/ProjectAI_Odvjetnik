"use client";

import { useEffect, useState } from "react";
import Link from "next/link";

import {
  Search as SearchIcon,
  Scale,
  BookOpen,
  ScrollText,
  Clock,
  Building2,
  ChevronRight,
  Loader2,
  ShieldAlert,
  ArrowRight,
  Gavel,
} from "lucide-react";
import LegalFeed from "@/components/LegalFeed";
import { getRecentActivity, type ActivityItem, type ActivityType } from "@/lib/supabase";

// ── Helpers ──

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

const ACTIVITY_ICONS: Record<ActivityType, React.ReactNode> = {
  valuation: <Building2 className="h-4 w-4 text-blue-500" />,
  contract: <ShieldAlert className="h-4 w-4 text-rose-500" />,
  chronology: <ScrollText className="h-4 w-4 text-violet-500" />,
  legal: <Gavel className="h-4 w-4 text-amber-500" />,
};

const ACTIVITY_LABELS: Record<ActivityType, string> = {
  valuation: "Procjena",
  contract: "Ugovor",
  chronology: "Kronologija",
  legal: "Pravna analiza",
};

const BADGE_COLORS: Record<string, string> = {
  green: "text-green-700 bg-green-50",
  amber: "text-amber-700 bg-amber-50",
  red: "text-red-700 bg-red-50",
};

// ═══════════════════════════════════════════════════════════
//  Dashboard Component — "Legal Command Center"
// ═══════════════════════════════════════════════════════════

export default function Dashboard() {

  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const data = await getRecentActivity(10);
        setActivity(data);
      } catch {
        // Silently handle — dashboard still works without history
      } finally {
        setLoading(false);
      }
    })();
  }, []);

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
          <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
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

            {/* Provjeri Ishod */}
            <Link
              href="/sudska-praksa"
              className="group relative bg-white rounded-xl border border-slate-200 p-5 hover:border-amber-400 hover:shadow-md transition overflow-hidden"
            >
              <div className="absolute top-0 right-0 w-20 h-20 bg-amber-50 rounded-bl-[40px] -mr-2 -mt-2 group-hover:bg-amber-100 transition" />
              <div className="relative">
                <div className="w-10 h-10 rounded-lg bg-amber-100 flex items-center justify-center mb-3 group-hover:bg-amber-200 transition">
                  <Gavel className="h-5 w-5 text-amber-600" />
                </div>
                <h3 className="font-semibold text-slate-900 text-sm">
                  Provjeri Ishod
                </h3>
                <p className="text-xs text-slate-400 mt-1 leading-relaxed">
                  AI prognoza na sudu + sudska praksa
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
            {!loading && activity.length === 0 && (
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
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                {/* Table header */}
                <div className="grid grid-cols-[1fr_auto_44px] gap-2 px-4 py-2.5 bg-slate-50 border-b border-slate-200 text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  <span>Predmet</span>
                  <span className="w-28 text-center">Status</span>
                  <span />
                </div>

                {/* Rows */}
                <div className="divide-y divide-slate-100">
                  {activity.slice(0, 12).map((item) => (
                    <Link
                      key={item.id}
                      href="/history"
                      className="grid grid-cols-[1fr_auto_44px] gap-2 items-center px-4 py-3 hover:bg-slate-50 transition group"
                    >
                      {/* Left: type icon + title + subtitle */}
                      <div className="flex items-center gap-3 min-w-0">
                        <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center shrink-0">
                          {ACTIVITY_ICONS[item.type]}
                        </div>
                        <div className="min-w-0">
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                              {ACTIVITY_LABELS[item.type]}
                            </span>
                            <span className="text-[10px] text-slate-300">•</span>
                            <span className="text-sm font-medium text-slate-900 truncate">
                              {item.title}
                            </span>
                          </div>
                          <span className="text-[11px] text-slate-400">
                            {timeAgo(item.created_at)}
                            {item.subtitle ? ` · ${item.subtitle}` : ""}
                          </span>
                        </div>
                      </div>

                      {/* Badge */}
                      <div className="w-28 flex justify-center">
                        {item.badge && (
                          <span className={`inline-flex items-center text-xs font-medium px-2.5 py-1 rounded-full ${
                            item.badgeColor ? BADGE_COLORS[item.badgeColor] || "text-slate-500 bg-slate-100" : "text-slate-500 bg-slate-100"
                          }`}>
                            {item.badge}
                          </span>
                        )}
                      </div>

                      {/* Arrow */}
                      <div className="flex justify-center">
                        <ChevronRight className="h-4 w-4 text-slate-300 group-hover:text-slate-500 transition" />
                      </div>
                    </Link>
                  ))}
                </div>

                {/* Footer */}
                {activity.length > 12 && (
                  <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-200 text-center">
                    <Link
                      href="/history"
                      className="text-xs text-blue-600 hover:underline"
                    >
                      Prikaži svih {activity.length} predmeta →
                    </Link>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ── RIGHT: Legal Feed + Stats ── */}
          <div className="space-y-4">
            {/* Legal Intelligence Feed */}
            <LegalFeed />

            {/* Quick Stats */}
            {!loading && activity.length > 0 && (
              <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
                <h3 className="text-[11px] text-slate-400 uppercase tracking-wider font-medium">
                  Brzi pregled
                </h3>
                <div className="grid grid-cols-2 gap-3">
                  {(["valuation", "contract", "chronology", "legal"] as ActivityType[]).map((t) => {
                    const count = activity.filter((a) => a.type === t).length;
                    if (count === 0) return null;
                    return (
                      <div key={t} className="bg-slate-50 rounded-lg p-3 flex items-center gap-2">
                        {ACTIVITY_ICONS[t]}
                        <div>
                          <div className="text-lg font-bold text-slate-900">{count}</div>
                          <div className="text-[10px] text-slate-400">{ACTIVITY_LABELS[t]}</div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="max-w-7xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 text-center space-y-1 text-[11px] text-slate-400">
          <div className="flex items-center justify-between">
            <span>Legal AI Assistant v2.0</span>
            <span>
              AI analiza ne predstavlja pravni savjet sukladno čl. 1. Zakona o odvjetništvu (NN 9/94, 18/11)
            </span>
          </div>
          <p>Obrada osobnih podataka provodi se sukladno Uredbi (EU) 2016/679 (GDPR). Korisnik potvrđuje da ima pravo na pristup dostavljenom dokumentu.</p>
        </div>
      </footer>
    </div>
  );
}
