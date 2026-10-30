"use client";

import React from "react";
import type { ModuleQuota } from "@/lib/useCredits";

interface CreditQuotaBadgeProps {
  quota: ModuleQuota;
  loading?: boolean;
  className?: string;
}

/**
 * Elegant inline badge that displays remaining module credits.
 * Shows a professional limit-reached message when exhausted.
 *
 * Usage:
 *   <CreditQuotaBadge quota={quota} loading={creditsLoading} />
 */
export const CreditQuotaBadge = React.memo(function CreditQuotaBadge({ quota, loading, className = "" }: CreditQuotaBadgeProps) {
  if (loading) {
    return (
      <span className={`inline-flex items-center gap-1.5 text-xs text-slate-400 ${className}`}>
        <span className="h-1.5 w-1.5 rounded-full bg-slate-300 animate-pulse" />
        Učitavanje kredita…
      </span>
    );
  }

  // No plan data yet — neutral state
  if (quota.max === 0) {
    return (
      <span className={`inline-flex items-center gap-1.5 text-xs text-slate-400 ${className}`}>
        <span className="h-1.5 w-1.5 rounded-full bg-slate-300" />
        Krediti se aktiviraju pri prvom korištenju
      </span>
    );
  }

  if (quota.exhausted) {
    return (
      <div className={`space-y-1 ${className}`}>
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-amber-700">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          Iskorišteno: {quota.used}/{quota.max}
        </span>
        <p className="text-xs text-slate-500 leading-relaxed">
          Vaš mjesečni paket ({quota.used}/{quota.max}) je iskorišten.
        </p>
      </div>
    );
  }

  // Normal state — show remaining
  const pct = quota.max > 0 ? quota.remaining / quota.max : 0;
  const dotColor = pct > 0.3 ? "bg-emerald-500" : pct > 0.1 ? "bg-amber-500" : "bg-red-500";
  const textColor = pct > 0.3 ? "text-slate-500" : pct > 0.1 ? "text-amber-700" : "text-red-600";

  return (
    <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${textColor} ${className}`}>
      <span className={`h-1.5 w-1.5 rounded-full ${dotColor}`} />
      Preostalo kredita: {quota.remaining}/{quota.max}
    </span>
  );
});

/**
 * Limit-reached warning block — used below a disabled button.
 */
export const CreditExhaustedWarning = React.memo(function CreditExhaustedWarning({ quota }: { quota: ModuleQuota }) {
  if (!quota.exhausted || quota.max === 0) return null;

  return (
    <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
      <p className="text-sm font-medium text-amber-800">
        Dosegli ste mjesečni limit od {quota.max} korištenja.
      </p>
      <p className="mt-1 text-xs text-amber-600">
        Pričekajte sljedeći mjesec ili se javite podršci.
      </p>
    </div>
  );
});
