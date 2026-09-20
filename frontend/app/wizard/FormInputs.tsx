"use client";

import type { ReactNode } from "react";
import { formatNumber } from "@/lib/format";

// ═══════════════════════════════════════════
//  REUSABLE FORM INPUT COMPONENTS
// ═══════════════════════════════════════════

export function NumberInput({ label, value, onChange, min, max, step = 0.1 }: {
  label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">{label}</label>
      <input type="number" inputMode="decimal" value={value || ""} onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
        min={min} max={max} step={step}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500" />
    </div>
  );
}

export function SelectInput({ label, value, options, onChange }: {
  label: string; value: string; options: { value: string; label: string }[]; onChange: (v: string) => void;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">{label}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </div>
  );
}

export function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-3 p-3 rounded-lg border border-gray-200 cursor-pointer hover:bg-gray-50 transition">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
      <span className="text-sm text-gray-700">{label}</span>
    </label>
  );
}

export function FloorSelect({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-500 mb-1">Kat *</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 bg-white">
        <optgroup label="Posebne etaže">
          <option value="Subbasement">Podrum</option>
          <option value="SemiBasement">Suteren</option>
          <option value="Ground">Prizemlje</option>
          <option value="Attic">Potkrovlje</option>
          <option value="Penthouse">Penthouse</option>
        </optgroup>
        <optgroup label="Katovi">
          {Array.from({ length: 30 }, (_, i) => (
            <option key={i + 1} value={`Floor${i + 1}`}>{i + 1}. kat</option>
          ))}
        </optgroup>
      </select>
    </div>
  );
}

export function MiniStat({ icon, label, value }: { icon: ReactNode; label: string; value: string | number }) {
  return (
    <div className="rounded-xl bg-white border border-gray-200 p-3 flex items-center gap-3">
      <div className="shrink-0">{icon}</div>
      <div className="min-w-0">
        <p className="text-lg font-bold text-gray-900 truncate">{value}</p>
        <p className="text-[10px] text-gray-500 uppercase tracking-wider">{label}</p>
      </div>
    </div>
  );
}

export function SimilarityBadge({ score }: { score: number }) {
  const pct = score <= 1 ? score * 100 : score;
  const color = pct >= 70 ? "bg-green-100 text-green-800" : pct >= 40 ? "bg-yellow-100 text-yellow-800" : "bg-red-100 text-red-800";
  const label = pct >= 70 ? "Visoka" : pct >= 40 ? "Srednja" : "Niska";
  return (
    <span className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${color}`}>
      {label} {formatNumber(pct, 0)}%
    </span>
  );
}

export function PriceDistributionChart({ comparables, calculatedPricePerM2 }: { comparables: { title: string; price_per_m2: number }[]; calculatedPricePerM2: number }) {
  if (comparables.length === 0) return null;
  const prices = comparables.map(c => c.price_per_m2);
  const minP = Math.min(...prices);
  const maxP = Math.max(...prices);

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        {[...comparables]
          .sort((a, b) => b.price_per_m2 - a.price_per_m2)
          .map((c, i) => {
          const widthPct = Math.max(((c.price_per_m2 - minP * 0.9) / (maxP * 1.1 - minP * 0.9)) * 100, 8);
          return (
            <div key={i} className="flex items-center gap-3">
              <span className="text-xs text-gray-500 w-20 truncate shrink-0" title={c.title}>{c.title.slice(0, 18)}</span>
              <div className="flex-1 h-6 bg-gray-100 rounded-full overflow-hidden relative">
                <div className="h-full rounded-full transition-all duration-700" style={{
                  width: `${widthPct}%`,
                  backgroundColor: Math.abs(c.price_per_m2 - calculatedPricePerM2) / calculatedPricePerM2 < 0.1 ? "#3b82f6" : "#94a3b8"
                }} />
              </div>
              <span className="text-xs font-semibold text-gray-700 w-20 text-right shrink-0">{Math.round(c.price_per_m2).toLocaleString("hr-HR")} €</span>
            </div>
          );
        })}
      </div>
      <div className="flex items-center justify-center gap-4 text-xs text-gray-500 pt-2 border-t border-gray-100">
        <span className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full" style={{ backgroundColor: "#3b82f6" }} /> Blizu procjene (±10%)
        </span>
        <span className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full" style={{ backgroundColor: "#94a3b8" }} /> Ostale
        </span>
        <span className="font-semibold text-blue-700">Procjena: {Math.round(calculatedPricePerM2).toLocaleString("hr-HR")} €/m²</span>
      </div>
    </div>
  );
}
