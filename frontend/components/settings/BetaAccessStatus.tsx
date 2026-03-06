"use client";

import { Loader2, Zap, AlertTriangle, Mail, RefreshCw } from "lucide-react";
import { useCreditContext } from "@/contexts/CreditContext";
import type { CreditRow } from "@/contexts/CreditContext";
import { useSubscription } from "@/lib/useSubscription";

interface Props {
  userId: string;
}

// ── Human-readable module labels ──
const MODULE_LABELS: Record<string, string> = {
  global: "AI Krediti (ukupno)",
  contract_analysis: "Analiza Ugovora",
  market_eval: "Tržišna Procjena",
  zk_analysis: "ZK Izvadak",
  chronology: "Kronologija Spisa",
  translation: "Sudski Tumač",
  invoice: "e-Račun / Troškovnik",
  legal_strategy: "Sudska Praksa",
};

function getModuleLabel(name: string): string {
  return MODULE_LABELS[name] || name.replace(/_/g, " ");
}

/** Safe number accessor — returns 0 for null/undefined/NaN */
function safeNum(val: unknown): number {
  if (val == null) return 0;
  const n = Number(val);
  return Number.isFinite(n) ? n : 0;
}

export default function BetaAccessStatus({ userId: _propUserId }: Props) {
  const { rows: usage, loading, error, refreshCredits } = useCreditContext();
  const { data: subscription } = useSubscription();

  // ── Derive effective display rows ──
  // If firm_usage returned nothing (new user), fall back to subscription ai_credits_limit
  const effectiveUsage: CreditRow[] =
    usage.length > 0
      ? usage
      : subscription && subscription.ai_credits_limit > 0
        ? [
            {
              id: "subscription-fallback",
              law_firm_id: subscription.law_firm_id ?? "",
              module_name: "global",
              used_credits: 0,
              max_credits: subscription.ai_credits_limit,
              reset_date: null,
            },
          ]
        : [];

  // Separate "global" summary row from per-module rows
  const globalRow = effectiveUsage.find((r) => r.module_name === "global");
  const moduleRows = effectiveUsage.filter((r) => r.module_name !== "global");

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
        <span className="ml-2 text-sm text-slate-400">Učitavanje kredita...</span>
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
        <div className="flex-1">
          <h2 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-slate-900">
            Status i Krediti
          </h2>
          <p className="text-sm text-slate-400 mt-0.5">
            Pregled potrošnje i limita Beta programa
          </p>
        </div>
        {/* Refresh button */}
        <button
          onClick={refreshCredits}
          className="p-2 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition"
          title="Osvježi podatke"
        >
          <RefreshCw className="h-4 w-4" />
        </button>
      </div>

      {/* Content */}
      <div className="px-6 sm:px-8 py-6 space-y-6">
        {/* ── Error state ── */}
        {error && (
          <div className="rounded-lg p-4 bg-red-50 border border-red-200">
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-red-500 shrink-0" />
              <p className="text-sm text-red-700">{error}</p>
            </div>
            <button
              onClick={refreshCredits}
              className="mt-2 text-xs font-medium text-red-600 hover:text-red-800 underline"
            >
              Pokušaj ponovo
            </button>
          </div>
        )}

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

        {/* ── Total Credit Summary ── */}
        {effectiveUsage.length > 0 && (() => {
          const totalMax = globalRow
            ? safeNum(globalRow.max_credits)
            : effectiveUsage.reduce((sum, r) => sum + safeNum(r.max_credits), 0);
          const totalUsed = globalRow
            ? safeNum(globalRow.used_credits)
            : effectiveUsage.reduce((sum, r) => sum + safeNum(r.used_credits), 0);
          const totalRemaining = Math.max(0, totalMax - totalUsed);
          const pct = totalMax > 0 ? Math.min(100, Math.round((totalUsed / totalMax) * 100)) : 0;

          return (
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-3">
                <div className="rounded-lg bg-slate-50 border border-slate-200 p-3 text-center">
                  <div className="text-lg font-bold text-slate-900 tabular-nums">{totalMax}</div>
                  <div className="text-[10px] text-slate-400 uppercase tracking-wider mt-0.5">Ukupno</div>
                </div>
                <div className="rounded-lg bg-slate-50 border border-slate-200 p-3 text-center">
                  <div className="text-lg font-bold tabular-nums" style={{ color: "#1B2A4A" }}>{totalUsed}</div>
                  <div className="text-[10px] text-slate-400 uppercase tracking-wider mt-0.5">Potrošeno</div>
                </div>
                <div className="rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-center">
                  <div className="text-lg font-bold text-emerald-700 tabular-nums">{totalRemaining}</div>
                  <div className="text-[10px] text-emerald-600 uppercase tracking-wider mt-0.5">Preostalo</div>
                </div>
              </div>

              {/* Overall AI credit progress bar (from global row or subscription) */}
              {globalRow && (
                <div className="rounded-lg border border-slate-200 bg-white p-4 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-slate-800">AI Krediti — mjesečni limit</span>
                    <span className={`text-xs font-semibold tabular-nums ${
                      pct >= 100 ? "text-red-600" : pct >= 90 ? "text-amber-600" : "text-slate-500"
                    }`}>
                      {totalUsed} / {totalMax} kredita
                    </span>
                  </div>
                  <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                    <div
                      className={`h-full rounded-full transition-all duration-500 ${
                        pct >= 100 ? "bg-red-500" : pct >= 90 ? "bg-amber-500" : ""
                      }`}
                      style={{
                        width: `${pct}%`,
                        ...(pct < 90 ? { background: "#1B2A4A" } : {}),
                      }}
                    />
                  </div>
                  {totalUsed === 0 && totalMax > 0 && (
                    <p className="text-xs text-slate-400">
                      Još niste koristili AI module. Vaš mjesečni limit je {totalMax} kredita.
                    </p>
                  )}
                </div>
              )}
            </div>
          );
        })()}

        {/* ── Per-module Usage Progress Bars ── */}
        {moduleRows.length > 0 ? (
          <div className="space-y-4">
            <h3
              className="text-xs font-semibold uppercase tracking-wider"
              style={{ color: "#1B2A4A" }}
            >
              Potrošnja po modulima
            </h3>

            {moduleRows.map((row) => {
              const used = safeNum(row.used_credits);
              const max = safeNum(row.max_credits);
              const pct = max > 0
                ? Math.min(100, Math.round((used / max) * 100))
                : 0;
              const isWarning = pct >= 90;
              const isExhausted = used >= max && max > 0;
              const remaining = Math.max(0, max - used);

              return (
                <div
                  key={row.id || row.module_name}
                  className="rounded-lg border border-slate-200 bg-white p-4 space-y-2"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-slate-800">
                      {getModuleLabel(row.module_name || "nepoznato")}
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
                      {used} / {max} kredita
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
                      Pri kraju limita — preostalo {remaining} kredita.
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        ) : effectiveUsage.length === 0 && !error ? (
          <div className="rounded-lg border border-slate-200 bg-white p-8 text-center">
            <Zap className="h-8 w-8 text-slate-300 mx-auto mb-3" />
            <p className="text-sm text-slate-500 font-medium">
              Još nema podataka o potrošnji
            </p>
            <p className="text-xs text-slate-400 mt-1">
              Krediti za module se automatski kreiraju pri prvom korištenju.
            </p>
          </div>
        ) : null}

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
