"use client";

import { useEffect, useState } from "react";
import { Loader2, Zap, AlertTriangle, Mail } from "lucide-react";
import { API_BASE } from "@/lib/api";

function apiHeaders(): Record<string, string> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const apiKey = process.env.NEXT_PUBLIC_API_KEY;
  if (apiKey) headers["X-API-Key"] = apiKey;
  return headers;
}

interface UsageRow {
  id: string;
  law_firm_id: string;
  module_name: string;
  used_credits: number;
  max_credits: number;
  reset_date: string | null;
}

interface Props {
  userId: string;
}

// ── Human-readable module labels ──
const MODULE_LABELS: Record<string, string> = {
  contract_analysis: "Analiza Ugovora",
  market_eval: "Tržišna Procjena",
  zk_analysis: "ZK Izvadak",
  chronology: "Kronologija Spisa",
  translation: "Sudski Tumač",
  invoice: "e-Račun / Troškovnik",
};

function getModuleLabel(name: string): string {
  return MODULE_LABELS[name] || name.replace(/_/g, " ");
}

export default function BetaAccessStatus({ userId }: Props) {
  const [usage, setUsage] = useState<UsageRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!userId) return;
    (async () => {
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/credits?userId=${encodeURIComponent(userId)}`,
          { headers: apiHeaders() }
        );
        if (res.ok) {
          const data = await res.json();
          setUsage(Array.isArray(data) ? data : []);
        }
      } catch {
        // silent
      } finally {
        setLoading(false);
      }
    })();
  }, [userId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  return (
    <>
      {/* Header */}
      <div className="px-6 sm:px-8 py-5 border-b border-slate-200 flex items-center gap-4">
        <div
          className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0"
          style={{ background: "#1B2A4A" }}
        >
          <Zap className="h-5 w-5 text-white" />
        </div>
        <div>
          <h2 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-slate-900">
            Status i Krediti
          </h2>
          <p className="text-sm text-slate-400 mt-0.5">
            Pregled potrošnje i limita Beta programa
          </p>
        </div>
      </div>

      {/* Content */}
      <div className="px-6 sm:px-8 py-6 space-y-6">
        {/* ── Early Access Badge ── */}
        <div className="rounded-lg p-5 border" style={{ borderColor: "#C9A84C33", background: "#FEFCF6" }}>
          <div className="flex items-center gap-3">
            <div
              className="w-12 h-12 rounded-lg flex items-center justify-center shrink-0"
              style={{ background: "#1B2A4A" }}
            >
              <span className="text-lg font-bold" style={{ color: "#C9A84C" }}>β</span>
            </div>
            <div>
              <p
                className="font-[family-name:var(--font-playfair)] text-base font-bold"
                style={{ color: "#1B2A4A" }}
              >
                PravoTek Early Access
              </p>
              <p className="text-sm text-slate-500 mt-0.5">
                Vaš ured sudjeluje u zatvorenom beta programu. Svaki modul ima ograničen broj besplatnih
                korištenja mjesečno.
              </p>
            </div>
          </div>
        </div>

        {/* ── Usage Progress Bars ── */}
        {usage.length > 0 ? (
          <div className="space-y-4">
            <h3
              className="text-xs font-semibold uppercase tracking-wider"
              style={{ color: "#1B2A4A" }}
            >
              Potrošnja po modulima
            </h3>

            {usage.map((row) => {
              const pct = row.max_credits > 0
                ? Math.min(100, Math.round((row.used_credits / row.max_credits) * 100))
                : 0;
              const isWarning = pct >= 90;
              const isExhausted = row.used_credits >= row.max_credits;

              return (
                <div
                  key={row.id}
                  className="rounded-lg border border-slate-200 bg-white p-4 space-y-2"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-slate-800">
                      {getModuleLabel(row.module_name)}
                    </span>
                    <span
                      className={`text-xs font-semibold tabular-nums ${
                        isExhausted
                          ? "text-red-600"
                          : isWarning
                            ? "text-amber-600"
                            : "text-slate-500"
                      }`}
                    >
                      {row.used_credits} / {row.max_credits} kredita
                    </span>
                  </div>

                  {/* Progress bar */}
                  <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${
                        isExhausted
                          ? "bg-red-500"
                          : isWarning
                            ? "bg-amber-500"
                            : ""
                      }`}
                      style={{
                        width: `${pct}%`,
                        ...(!isWarning && !isExhausted
                          ? { background: "#1B2A4A" }
                          : {}),
                      }}
                    />
                  </div>

                  {/* Warning text */}
                  {isExhausted && (
                    <div className="flex items-center gap-1.5 text-xs text-red-600 font-medium">
                      <AlertTriangle className="h-3 w-3" />
                      Limit dosegnut — obratite se podršci za povećanje.
                    </div>
                  )}
                  {isWarning && !isExhausted && (
                    <div className="flex items-center gap-1.5 text-xs text-amber-600 font-medium">
                      <AlertTriangle className="h-3 w-3" />
                      Pri kraju limita — preostalo {row.max_credits - row.used_credits} kredita.
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        ) : (
          <div className="rounded-lg border border-slate-200 bg-white p-8 text-center">
            <Zap className="h-8 w-8 text-slate-300 mx-auto mb-3" />
            <p className="text-sm text-slate-500 font-medium">
              Još nema podataka o potrošnji
            </p>
            <p className="text-xs text-slate-400 mt-1">
              Krediti za module se automatski kreiraju pri prvom korištenju.
            </p>
          </div>
        )}

        {/* ── Divider ── */}
        <div className="h-px bg-slate-100" />

        {/* ── CTA: Request Limit Increase ── */}
        <div>
          <a
            href="mailto:podrska@pravotek.hr?subject=Zahtjev%20za%20povećanje%20limita%20Beta%20kredita"
            className="inline-flex items-center gap-2 text-sm font-medium px-4 py-2.5 rounded-lg transition-all text-slate-600 bg-white border border-slate-200 hover:bg-slate-50 hover:border-slate-300"
          >
            <Mail className="h-4 w-4" />
            Zatraži povećanje limita
          </a>
          <p className="text-[11px] text-slate-400 mt-2">
            Kontaktirajte naš tim za prilagodbu limita vašem uredu.
          </p>
        </div>
      </div>
    </>
  );
}
