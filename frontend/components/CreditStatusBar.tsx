"use client";

import React from "react";
import { Zap } from "lucide-react";
import type { ModuleQuota } from "@/lib/useCredits";

interface CreditStatusBarProps {
  label: string;
  quota: ModuleQuota;
  loading: boolean;
}

/**
 * Prominent credit status bar shown at the top of AI feature pages.
 * Displays remaining credits with a mini progress bar.
 */
export const CreditStatusBar = React.memo(function CreditStatusBar({ label, quota, loading }: CreditStatusBarProps) {
  if (loading) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm animate-pulse">
        <Zap className="h-4 w-4 text-slate-300" />
        <span className="text-sm text-slate-400">Učitavanje kredita…</span>
      </div>
    );
  }

  // No plan data yet
  if (quota.max === 0) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
        <Zap className="h-4 w-4 text-slate-400" />
        <span className="text-sm text-slate-500">{label}</span>
        <span className="text-xs text-slate-400 ml-auto">Krediti se aktiviraju pri prvom korištenju</span>
      </div>
    );
  }

  const pct = quota.max > 0 ? Math.min(100, Math.round((quota.used / quota.max) * 100)) : 0;
  const colorRatio = quota.max > 0 ? quota.remaining / quota.max : 0;

  const barColor =
    quota.remaining === 0
      ? "bg-red-500"
      : colorRatio <= 0.1
        ? "bg-red-500"
        : colorRatio <= 0.3
          ? "bg-amber-500"
          : "";

  const badgeBg =
    quota.remaining === 0
      ? "bg-red-50 border-red-200"
      : colorRatio <= 0.3
        ? "bg-amber-50 border-amber-200"
        : "bg-white border-slate-200";

  const countColor =
    quota.remaining === 0
      ? "text-red-700"
      : colorRatio <= 0.3
        ? "text-amber-700"
        : "text-slate-700";

  return (
    <div className={`flex items-center gap-3 rounded-xl border px-4 py-3 shadow-sm ${badgeBg}`}>
      <Zap className="h-4 w-4 text-slate-500 shrink-0" />
      <span className="text-sm font-medium text-slate-700 shrink-0">{label}</span>

      <div className="flex-1 flex items-center gap-3 min-w-0">
        {/* Mini progress bar */}
        <div className="flex-1 h-2 rounded-full bg-slate-100 overflow-hidden max-w-[160px]">
          <div
            className={`h-full rounded-full transition-all duration-500 ${barColor}`}
            style={{
              width: `${pct}%`,
              ...(!barColor ? { backgroundColor: "#1B2A4A" } : {}),
            }}
          />
        </div>

        {/* Count */}
        <span className={`text-sm font-semibold tabular-nums whitespace-nowrap ${countColor}`}>
          {quota.remaining} / {quota.max}
        </span>
      </div>

      {quota.remaining === 0 && (
        <span className="text-xs font-medium text-red-600 shrink-0">Limit dosegnut</span>
      )}
    </div>
  );
});
