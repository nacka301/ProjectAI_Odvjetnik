"use client";

import { useState, useRef, useEffect } from "react";
import { ExternalLink, ChevronDown, Clock, Shield, AlertTriangle, XCircle, Info, Scale } from "lucide-react";
import { type CitedNorm, type TemporalNormResult, getStatusColor, fetchNorm, type NormInfo } from "@/lib/normApi";

// ══════════════════════════════════════════════════════════════
//  V42: NormBadge — Validity Badge + Hover Tooltip
//  Shows 🟢 Važeći / 🟡 Izmijenjen / 🔴 Ukinut next to each
//  cited article. Click to expand details (NN, amendments, link).
// ══════════════════════════════════════════════════════════════

interface NormBadgeProps {
  /** The cited norm from AI output */
  norm: CitedNorm;
  /** Optional temporal result if date-aware mode is active */
  temporal?: TemporalNormResult;
  /** Visual variant: "pill" (inline) or "card" (expanded) */
  variant?: "pill" | "card";
  /** Color theme override for parent context (red_flag, warning, neutral) */
  context?: "red_flag" | "warning" | "neutral";
}

export function NormBadge({ norm, temporal, variant = "pill", context = "neutral" }: NormBadgeProps) {
  const [expanded, setExpanded] = useState(false);
  const [details, setDetails] = useState<NormInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const tooltipRef = useRef<HTMLDivElement>(null);

  // Determine display status — temporal override if present
  const displayStatus = temporal?.is_outdated ? "IZMIJENJEN" : norm.status;
  const colors = getStatusColor(displayStatus);

  // Close tooltip on outside click
  useEffect(() => {
    if (!expanded) return;
    const handler = (e: MouseEvent) => {
      if (tooltipRef.current && !tooltipRef.current.contains(e.target as Node)) {
        setExpanded(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [expanded]);

  // Load full norm details on expand
  const handleClick = async () => {
    setExpanded(!expanded);
    if (!expanded && !details && norm.abbreviation) {
      setLoading(true);
      try {
        const info = await fetchNorm(norm.abbreviation);
        setDetails(info);
      } catch {
        // Silently fail — we still show the basic info
      } finally {
        setLoading(false);
      }
    }
  };

  const StatusIcon = displayStatus === "VAŽEĆI" ? Shield
    : displayStatus === "UKINUT" ? XCircle
    : AlertTriangle;

  // ── Pill Variant (inline badge) ──
  if (variant === "pill") {
    return (
      <span className="relative inline-flex items-center" ref={tooltipRef}>
        <button
          onClick={handleClick}
          className={`inline-flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-full border transition-all
            ${colors.bg} ${colors.text} border-current/20 hover:shadow-sm cursor-pointer
            ${context === "red_flag" ? "ring-1 ring-red-200" : ""}
            ${context === "warning" ? "ring-1 ring-amber-200" : ""}`}
          title={norm.formatted_citation || `${norm.article} ${norm.abbreviation}`}
        >
          {/* Validity dot */}
          <span className={`w-1.5 h-1.5 rounded-full ${colors.dot} shrink-0`} />
          {/* Citation text */}
          <span className="font-medium">
            {norm.article ? `${norm.article} ` : ""}
            {norm.abbreviation}
          </span>
          <ChevronDown className={`h-3 w-3 transition-transform ${expanded ? "rotate-180" : ""}`} />
        </button>

        {/* Tooltip popover */}
        {expanded && (
          <NormTooltip
            norm={norm}
            details={details}
            temporal={temporal}
            loading={loading}
            colors={colors}
            StatusIcon={StatusIcon}
          />
        )}
      </span>
    );
  }

  // ── Card Variant (expanded block) ──
  return (
    <div ref={tooltipRef} className={`rounded-lg border ${colors.bg} border-current/10 p-3`}>
      <button
        onClick={handleClick}
        className={`flex items-center gap-2 w-full text-left ${colors.text}`}
      >
        <StatusIcon className="h-4 w-4 shrink-0" />
        <span className="text-sm font-medium flex-1">
          {norm.formatted_citation || `${norm.article} ${norm.full_name} (${norm.nn_reference})`}
        </span>
        <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded-full ${colors.bg} ${colors.text} border border-current/20`}>
          {colors.label}
        </span>
        <ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />
      </button>

      {expanded && (
        <div className="mt-3 space-y-2 text-xs">
          <NormDetails norm={norm} details={details} temporal={temporal} loading={loading} />
        </div>
      )}
    </div>
  );
}

// ── Tooltip Popover ──

function NormTooltip({
  norm,
  details,
  temporal,
  loading,
  colors,
  StatusIcon,
}: {
  norm: CitedNorm;
  details: NormInfo | null;
  temporal?: TemporalNormResult;
  loading: boolean;
  colors: ReturnType<typeof getStatusColor>;
  StatusIcon: typeof Shield;
}) {
  return (
    <div className="absolute z-50 top-full left-0 mt-1 w-80 bg-white rounded-xl shadow-xl border border-gray-200 p-4 text-left animate-in fade-in slide-in-from-top-1">
      {/* Header */}
      <div className="flex items-start gap-2 mb-3">
        <StatusIcon className={`h-5 w-5 ${colors.text} mt-0.5 shrink-0`} />
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-sm text-gray-900 leading-tight">
            {norm.full_name || norm.abbreviation}
          </p>
          <p className="text-[11px] text-gray-500 mt-0.5">{norm.nn_reference}</p>
        </div>
        <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded-full ${colors.bg} ${colors.text} border border-current/20 shrink-0`}>
          {colors.label}
        </span>
      </div>

      {/* Norm details */}
      <NormDetails norm={norm} details={details} temporal={temporal} loading={loading} />

      {/* NN Link */}
      {norm.nn_url && (
        <a
          href={norm.nn_url}
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center gap-1.5 text-[11px] text-blue-600 hover:text-blue-800 mt-3 font-medium"
        >
          <ExternalLink className="h-3 w-3" />
          Otvori u Narodnim novinama
        </a>
      )}
    </div>
  );
}

// ── Norm Details Block (shared between tooltip and card) ──

function NormDetails({
  norm,
  details,
  temporal,
  loading,
}: {
  norm: CitedNorm;
  details: NormInfo | null;
  temporal?: TemporalNormResult;
  loading: boolean;
}) {
  return (
    <>
      {/* Formatted citation */}
      {norm.formatted_citation && (
        <div className="bg-gray-50 rounded-lg p-2 text-xs text-gray-700 font-mono leading-relaxed">
          {norm.formatted_citation}
        </div>
      )}

      {/* Temporal warning */}
      {temporal?.is_outdated && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg p-2 text-xs text-amber-800 flex items-start gap-2 mt-2">
          <AlertTriangle className="h-3.5 w-3.5 text-amber-600 mt-0.5 shrink-0" />
          <span>{temporal.warning}</span>
        </div>
      )}

      {/* Amendment timeline */}
      {loading && (
        <div className="text-[11px] text-gray-400 mt-2 flex items-center gap-1">
          <Clock className="h-3 w-3 animate-spin" /> Učitavam izmjene...
        </div>
      )}

      {details && details.amendments.length > 0 && (
        <div className="mt-2">
          <p className="text-[10px] font-semibold text-gray-500 uppercase tracking-wide mb-1.5">
            Izmjene kroz vrijeme
          </p>
          <div className="space-y-1 max-h-32 overflow-y-auto">
            {details.amendments.map((a, i) => (
              <div key={i} className="flex items-center gap-2 text-[11px]">
                <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${
                  i === details.amendments.length - 1 ? "bg-green-500" : "bg-gray-300"
                }`} />
                <span className="font-mono text-gray-600 w-14 shrink-0">NN {a.nn_number}</span>
                <span className="text-gray-400 w-20 shrink-0">{a.publication_date?.slice(0, 10)}</span>
                <span className="text-gray-500 truncate">{a.description}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

// ══════════════════════════════════════════════════════════════
//  CitedNormsList — Renders all cited norms from AI output
//  Shows a compact list of NormBadges with optional temporal mode
// ══════════════════════════════════════════════════════════════

interface CitedNormsListProps {
  norms: CitedNorm[];
  temporalResults?: TemporalNormResult[];
  variant?: "pills" | "cards";
}

export function CitedNormsList({ norms, temporalResults, variant = "pills" }: CitedNormsListProps) {
  if (!norms || norms.length === 0) return null;

  // Build temporal lookup map
  const temporalMap = new Map<string, TemporalNormResult>();
  temporalResults?.forEach((t) => temporalMap.set(t.abbreviation, t));

  if (variant === "cards") {
    return (
      <div className="space-y-2">
        <div className="flex items-center gap-2 mb-2">
          <Info className="h-4 w-4 text-blue-500" />
          <h4 className="text-sm font-semibold text-gray-700">Citirani propisi</h4>
          <span className="text-xs text-gray-400">({norms.length})</span>
        </div>
        {norms.map((norm, i) => (
          <NormBadge
            key={`${norm.abbreviation}-${norm.article}-${i}`}
            norm={norm}
            temporal={temporalMap.get(norm.abbreviation)}
            variant="card"
          />
        ))}
      </div>
    );
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {norms.map((norm, i) => (
        <NormBadge
          key={`${norm.abbreviation}-${norm.article}-${i}`}
          norm={norm}
          temporal={temporalMap.get(norm.abbreviation)}
          variant="pill"
        />
      ))}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════
//  TemporalDatePicker — Date input for "Temporal Law Mode"
//  Sets the event date for temporal norm validity checking
// ══════════════════════════════════════════════════════════════

interface TemporalDatePickerProps {
  value: string;
  onChange: (date: string) => void;
  label?: string;
}

export function TemporalDatePicker({ value, onChange, label }: TemporalDatePickerProps) {
  return (
    <div className="flex items-center gap-3 bg-blue-50 border border-blue-200 rounded-lg px-4 py-2.5">
      <Clock className="h-4 w-4 text-blue-500 shrink-0" />
      <div className="flex-1">
        <label className="text-xs font-semibold text-blue-700 block">
          {label || "Temporal Law Mode — Datum pravnog događaja"}
        </label>
        <p className="text-[10px] text-blue-500 mt-0.5">
          AI će koristiti verziju zakona važeću na ovaj datum
        </p>
      </div>
      <input
        type="date"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="text-sm border border-blue-300 rounded-md px-3 py-1.5 bg-white text-gray-700 
                   focus:ring-2 focus:ring-blue-400 focus:border-blue-400 outline-none"
        max={new Date().toISOString().split("T")[0]}
      />
    </div>
  );
}

// ══════════════════════════════════════════════════════════════
//  CitationModeToggle — "Sudski način" toggle
//  When enabled, all citations use full court-style format
// ══════════════════════════════════════════════════════════════

interface CitationModeToggleProps {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
}

export function CitationModeToggle({ enabled, onToggle }: CitationModeToggleProps) {
  return (
    <button
      onClick={() => onToggle(!enabled)}
      className={`flex items-center gap-2 px-3 py-2 rounded-lg border text-xs font-medium transition-all
        ${enabled
          ? "bg-indigo-50 border-indigo-300 text-indigo-700"
          : "bg-gray-50 border-gray-200 text-gray-500 hover:bg-gray-100"
        }`}
      title="Sudski način citiranja — puni format s člankom, stavkom i NN oznakom"
    >
      <Scale className="h-3.5 w-3.5" />
      <span>Sudski način</span>
      <span className={`w-8 h-4 rounded-full relative transition-colors ${enabled ? "bg-indigo-500" : "bg-gray-300"}`}>
        <span className={`absolute top-0.5 w-3 h-3 rounded-full bg-white shadow transition-transform ${
          enabled ? "translate-x-4" : "translate-x-0.5"
        }`} />
      </span>
    </button>
  );
}
