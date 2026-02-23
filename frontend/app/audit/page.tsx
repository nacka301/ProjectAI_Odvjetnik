"use client";

import { useEffect, useState } from "react";
import { AuditListSkeleton } from "@/components/Skeletons";
import {
  ShieldCheck,
  AlertTriangle,
  XCircle,
  Loader2,
  BarChart3,
  Globe,
  Calculator,
  Database,
  MapPin,
  ChevronDown,
  ChevronUp,
  Zap,
  RefreshCw,
  CheckCircle2,
  ExternalLink,
} from "lucide-react";
import {
  getAuditResults,
  getAuditStats,
  getEvaluations,
  type AuditRow,
  type AuditVerdict,
  type EvaluationRow,
} from "@/lib/supabase";
import { API_BASE } from "@/lib/api";
import { useUserId } from "@/lib/useUserId";
import AppHeader from "@/components/AppHeader";

// ── Verdict colors/icons ──

const VERDICT_CONFIG: Record<
  AuditVerdict,
  { icon: React.ReactNode; label: string; bg: string; text: string; border: string }
> = {
  CONFIRMED: {
    icon: <ShieldCheck className="h-5 w-5" />,
    label: "Potvrđeno",
    bg: "bg-green-50",
    text: "text-green-700",
    border: "border-green-200",
  },
  SUSPICIOUS: {
    icon: <AlertTriangle className="h-5 w-5" />,
    label: "Sumnjivo",
    bg: "bg-amber-50",
    text: "text-amber-700",
    border: "border-amber-200",
  },
  REJECTED: {
    icon: <XCircle className="h-5 w-5" />,
    label: "Odbačeno",
    bg: "bg-red-50",
    text: "text-red-700",
    border: "border-red-200",
  },
};

const DIMENSION_ICONS: Record<string, React.ReactNode> = {
  price: <Calculator className="h-4 w-4 text-blue-500" />,
  location: <MapPin className="h-4 w-4 text-violet-500" />,
  methodology: <BarChart3 className="h-4 w-4 text-amber-500" />,
  data_quality: <Database className="h-4 w-4 text-teal-500" />,
};

function scoreColor(score: number): string {
  if (score >= 70) return "text-green-600";
  if (score >= 40) return "text-amber-600";
  return "text-red-600";
}

function scoreBg(score: number): string {
  if (score >= 70) return "bg-green-500";
  if (score >= 40) return "bg-amber-500";
  return "bg-red-500";
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("hr-HR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function AuditPage() {
  const userId = useUserId();
  const [audits, setAudits] = useState<AuditRow[]>([]);
  const [evaluations, setEvaluations] = useState<Map<string, EvaluationRow>>(new Map());
  const [stats, setStats] = useState({ total: 0, confirmed: 0, suspicious: 0, rejected: 0 });
  const [loading, setLoading] = useState(true);
  const [auditRunning, setAuditRunning] = useState(false);
  const [activeFilter, setActiveFilter] = useState<AuditVerdict | "all">("all");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [apiStatus, setApiStatus] = useState<{ enabled: boolean; message: string } | null>(null);

  // Load data on mount
  useEffect(() => {
    (async () => {
      try {
        const [auditRows, auditStats, evals] = await Promise.all([
          getAuditResults(100),
          getAuditStats(),
          getEvaluations(100),
        ]);

        setAudits(auditRows);
        setStats(auditStats);

        const evalMap = new Map<string, EvaluationRow>();
        for (const e of evals) {
          evalMap.set(e.id, e);
        }
        setEvaluations(evalMap);
      } catch {
        setError("Greška pri dohvaćanju audit podataka.");
      } finally {
        setLoading(false);
      }
    })();

    // Check API status
    fetch(`${API_BASE}/api/audit/status`, { headers: userId ? { "x-user-id": userId } : {} })
      .then((r) => r.json())
      .then((data) => setApiStatus(data))
      .catch(() => setApiStatus({ enabled: false, message: "Backend nedostupan" }));
  }, []);

  // Run batch audit
  const handleRunAudit = async () => {
    setAuditRunning(true);
    setError(null);
    try {
      const resp = await fetch(`${API_BASE}/api/audit/batch`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(userId ? { "x-user-id": userId } : {}) },
        body: JSON.stringify({ evaluation_ids: [] }), // Empty = audit all unaudited
      });

      if (!resp.ok) {
        const err = await resp.json();
        setError(err.error || "Audit nije uspio.");
        return;
      }

      const result = await resp.json();

      // Refresh data
      const [auditRows, auditStats] = await Promise.all([
        getAuditResults(100),
        getAuditStats(),
      ]);
      setAudits(auditRows);
      setStats(auditStats);

      if (result.total === 0) {
        setError("Nema novih procjena za audit.");
      }
    } catch {
      setError("Greška pri pokretanju audita.");
    } finally {
      setAuditRunning(false);
    }
  };

  // Run audit for a single evaluation
  const handleAuditSingle = async (evaluationId: string) => {
    setAuditRunning(true);
    setError(null);
    try {
      const resp = await fetch(`${API_BASE}/api/audit/single`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(userId ? { "x-user-id": userId } : {}) },
        body: JSON.stringify({ evaluation_id: evaluationId }),
      });

      if (!resp.ok) {
        const err = await resp.json();
        setError(err.error || "Audit nije uspio.");
        return;
      }

      // Refresh data
      const [auditRows, auditStats] = await Promise.all([
        getAuditResults(100),
        getAuditStats(),
      ]);
      setAudits(auditRows);
      setStats(auditStats);
    } catch {
      setError("Greška pri pokretanju audita.");
    } finally {
      setAuditRunning(false);
    }
  };

  const filtered =
    activeFilter === "all" ? audits : audits.filter((a) => a.verdict === activeFilter);

  return (
    <div className="min-h-screen bg-gray-50 font-sans">
      <AppHeader />
      <div className="py-12 px-4">
      <div className="mx-auto max-w-5xl">

        {/* Header */}
        <div className="text-center mb-8">
          <h1 className="text-2xl sm:text-3xl font-bold text-gray-900 flex items-center justify-center gap-2 sm:gap-3">
            <ShieldCheck className="h-8 w-8 text-indigo-600" />
            Revizija — Verifikacija Procjena
          </h1>
          <p className="mt-2 text-gray-500 max-w-xl mx-auto">
            Drugi revizijski sloj automatski provjerava svaku procjenu nekretnine
            prema tržišnim podacima i označava je li pouzdana ili zahtijeva ručni pregled.
          </p>
        </div>

        {/* API Status Banner */}
        {apiStatus && !apiStatus.enabled && (
          <div className="mb-6 rounded-xl border border-amber-200 bg-amber-50 p-4 flex items-center gap-3">
            <AlertTriangle className="h-5 w-5 text-amber-600 shrink-0" />
            <div>
              <p className="text-sm font-medium text-amber-800">{apiStatus.message}</p>
              <p className="text-xs text-amber-600 mt-0.5">
                Postavite API ključ za aktivaciju automatskog revizijskog sloja.
              </p>
            </div>
          </div>
        )}

        {/* Stats Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
          <div className="rounded-xl bg-white border border-gray-200 p-4 text-center">
            <p className="text-xs text-gray-500 mb-1">Ukupno audita</p>
            <p className="text-2xl font-bold text-gray-900">{stats.total}</p>
          </div>
          <div className="rounded-xl bg-white border border-green-200 p-4 text-center">
            <p className="text-xs text-green-600 mb-1 flex items-center justify-center gap-1">
              <ShieldCheck className="h-3.5 w-3.5" /> Potvrđeno
            </p>
            <p className="text-2xl font-bold text-green-700">{stats.confirmed}</p>
          </div>
          <div className="rounded-xl bg-white border border-amber-200 p-4 text-center">
            <p className="text-xs text-amber-600 mb-1 flex items-center justify-center gap-1">
              <AlertTriangle className="h-3.5 w-3.5" /> Sumnjivo
            </p>
            <p className="text-2xl font-bold text-amber-700">{stats.suspicious}</p>
          </div>
          <div className="rounded-xl bg-white border border-red-200 p-4 text-center">
            <p className="text-xs text-red-600 mb-1 flex items-center justify-center gap-1">
              <XCircle className="h-3.5 w-3.5" /> Odbačeno
            </p>
            <p className="text-2xl font-bold text-red-700">{stats.rejected}</p>
          </div>
        </div>

        {/* Action Bar */}
        <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
          {/* Filters */}
          <div className="flex gap-2">
            {(["all", "CONFIRMED", "SUSPICIOUS", "REJECTED"] as const).map((f) => (
              <button
                key={f}
                onClick={() => setActiveFilter(f)}
                className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                  activeFilter === f
                    ? "bg-indigo-600 text-white"
                    : "bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"
                }`}
              >
                {f === "all" ? "Svi" : VERDICT_CONFIG[f].label}
              </button>
            ))}
          </div>

          {/* Run Audit Button */}
          <button
            onClick={handleRunAudit}
            disabled={auditRunning || (apiStatus !== null && !apiStatus.enabled)}
            className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition"
          >
            {auditRunning ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Audit u tijeku...
              </>
            ) : (
              <>
                <Zap className="h-4 w-4" />
                Pokreni Batch Audit
              </>
            )}
          </button>
        </div>

        {/* Error */}
        {error && (
          <div className="rounded-xl border border-red-300 bg-red-50 p-4 mb-6 flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-red-500 shrink-0" />
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {/* Loading */}
        {loading && <AuditListSkeleton />}

        {/* Empty State */}
        {!loading && filtered.length === 0 && (
          <div className="text-center py-20">
            <ShieldCheck className="h-12 w-12 text-gray-300 mx-auto mb-4" />
            <p className="text-gray-500 font-medium">
              {activeFilter !== "all"
                ? "Nema rezultata za odabrani filter."
                : "Još nema audit rezultata. Pokrenite batch audit za provjeru procjena."}
            </p>
          </div>
        )}

        {/* Audit Results */}
        {!loading && filtered.length > 0 && (
          <div className="space-y-3">
            {filtered.map((audit) => {
              const isExpanded = expandedId === audit.id;
              const verdict = VERDICT_CONFIG[audit.verdict];
              const ev = evaluations.get(audit.evaluation_id);
              const raw = audit.raw_json_output as Record<string, unknown>;
              const findings = (raw?.findings as Array<Record<string, unknown>>) ?? [];
              const sourceUrls = (raw?.source_urls as string[]) ?? [];

              return (
                <div
                  key={audit.id}
                  className={`rounded-xl bg-white border ${verdict.border} overflow-hidden transition hover:shadow-sm`}
                >
                  {/* Summary Row */}
                  <div
                    className="flex items-center gap-4 p-4 cursor-pointer"
                    onClick={() => setExpandedId(isExpanded ? null : audit.id)}
                  >
                    <div className={`shrink-0 p-2 rounded-lg ${verdict.bg} ${verdict.text}`}>
                      {verdict.icon}
                    </div>

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span
                          className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${verdict.bg} ${verdict.text}`}
                        >
                          {verdict.label}
                        </span>
                        <span className="text-sm font-semibold text-gray-900 truncate">
                          {ev ? `${ev.location} · ${ev.estimated_value?.toLocaleString("hr-HR")} €` : audit.evaluation_id.slice(0, 8) + "..."}
                        </span>
                      </div>
                      <p className="text-xs text-gray-400 mt-0.5 line-clamp-1">
                        {formatDate(audit.created_at)} · {audit.summary}
                      </p>
                    </div>

                    {/* Confidence */}
                    <div className="text-right shrink-0">
                      <p className={`text-lg font-bold ${scoreColor(audit.overall_confidence)}`}>
                        {audit.overall_confidence}%
                      </p>
                      <p className="text-[10px] text-gray-400 uppercase">Pouzdanost</p>
                    </div>

                    <div className="shrink-0 text-gray-400">
                      {isExpanded ? <ChevronUp className="h-5 w-5" /> : <ChevronDown className="h-5 w-5" />}
                    </div>
                  </div>

                  {/* Expanded Detail */}
                  {isExpanded && (
                    <div className="border-t border-gray-100 bg-gray-50 p-5 space-y-4">

                      {/* Decision Reasoning — WHY this verdict */}
                      {typeof raw?.decision_reasoning === "string" && raw.decision_reasoning && (
                        <div className="rounded-lg bg-indigo-50 border border-indigo-200 p-4">
                          <p className="text-xs font-semibold text-indigo-700 mb-2 flex items-center gap-1.5">
                            <ShieldCheck className="h-4 w-4" />
                            Obrazloženje odluke — Zašto {audit.verdict}?
                          </p>
                          <p className="text-sm text-indigo-900 leading-relaxed whitespace-pre-line">
                            {raw.decision_reasoning}
                          </p>
                        </div>
                      )}

                      {/* Dimension Scores with reasoning */}
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        {[
                          { key: "price", rawKey: "price_check", label: "Cijena", score: audit.price_score },
                          { key: "location", rawKey: "location_check", label: "Lokacija", score: audit.location_score },
                          { key: "methodology", rawKey: "methodology_check", label: "Metodologija", score: audit.methodology_score },
                          { key: "data_quality", rawKey: "data_quality_check", label: "Kvaliteta podataka", score: audit.data_quality_score },
                        ].map((dim) => {
                          const dimData = raw?.[dim.rawKey] as Record<string, unknown> | undefined;
                          const reasoning = typeof dimData?.reasoning === "string" ? dimData.reasoning : "";
                          const label = typeof dimData?.label === "string" ? dimData.label : "";
                          return (
                            <div key={dim.key} className="rounded-lg bg-white border border-gray-200 p-3">
                              <div className="flex items-center justify-between mb-2">
                                <div className="flex items-center gap-1.5">
                                  {DIMENSION_ICONS[dim.key]}
                                  <p className="text-xs text-gray-500">{dim.label}</p>
                                </div>
                                <div className="flex items-center gap-2">
                                  {label && (
                                    <span className="text-[10px] text-gray-400 bg-gray-100 px-1.5 py-0.5 rounded">{label}</span>
                                  )}
                                  <p className={`text-xl font-bold ${scoreColor(dim.score)}`}>{dim.score}</p>
                                </div>
                              </div>
                              <div className="h-1.5 rounded-full bg-gray-200 overflow-hidden mb-2">
                                <div
                                  className={`h-full rounded-full ${scoreBg(dim.score)}`}
                                  style={{ width: `${dim.score}%` }}
                                />
                              </div>
                              {reasoning && (
                                <p className="text-xs text-gray-600 leading-relaxed">{reasoning}</p>
                              )}
                            </div>
                          );
                        })}
                      </div>

                      {/* Recommendation */}
                      <div className="rounded-lg bg-white border border-gray-200 p-3">
                        <p className="text-xs text-gray-500 mb-1">Preporuka</p>
                        <p className="text-sm text-gray-800">{audit.recommendation}</p>
                      </div>

                      {/* Findings */}
                      {findings.length > 0 && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-2">
                            Nalazi ({audit.findings_count}
                            {audit.critical_count > 0 && (
                              <span className="text-red-600 ml-1">
                                · {audit.critical_count} kritičnih
                              </span>
                            )}
                            )
                          </p>
                          <div className="space-y-2">
                            {findings.map((f, i) => {
                              const severity = (f.severity as string) || "INFO";
                              const sevColor =
                                severity === "CRITICAL"
                                  ? "text-red-600 bg-red-50"
                                  : severity === "WARNING"
                                  ? "text-amber-600 bg-amber-50"
                                  : "text-blue-600 bg-blue-50";
                              return (
                                <div key={i} className="flex items-start gap-2 text-xs">
                                  <span
                                    className={`shrink-0 px-1.5 py-0.5 rounded font-mono font-bold uppercase ${sevColor}`}
                                  >
                                    {severity}
                                  </span>
                                  <div>
                                    <p className="text-gray-800">{String(f.description ?? "")}</p>
                                    {typeof f.evidence === "string" && f.evidence && (
                                      <p className="text-gray-400 mt-0.5">{f.evidence}</p>
                                    )}
                                  </div>
                                </div>
                              );
                            })}
                          </div>
                        </div>
                      )}

                      {/* Market Context */}
                      {typeof raw?.market_context === "string" && raw.market_context && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                            <Globe className="h-3.5 w-3.5" /> Tržišni kontekst
                          </p>
                          <p className="text-xs text-gray-700 leading-relaxed">
                            {raw.market_context}
                          </p>
                        </div>
                      )}

                      {/* Sources */}
                      {sourceUrls.length > 0 && (
                        <div className="rounded-lg bg-white border border-gray-200 p-3">
                          <p className="text-xs text-gray-500 mb-1">Izvori</p>
                          <div className="flex flex-wrap gap-2">
                            {sourceUrls.map((url, i) => (
                              <a
                                key={i}
                                href={url}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="inline-flex items-center gap-1 text-xs text-blue-600 hover:underline"
                              >
                                <ExternalLink className="h-3 w-3" />
                                {(() => { try { return new URL(url).hostname; } catch { return url; } })()}
                              </a>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Metadata */}
                      <div className="flex items-center gap-4 text-[10px] text-gray-400 pt-1">
                        <span>Model: {audit.model_used}</span>
                        <span>Tokeni: {audit.prompt_tokens + audit.completion_tokens}</span>
                        {ev && (
                          <span>
                            Procjena: {ev.property_type} · {ev.size_m2 ?? "?"} m²
                          </span>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {/* Unaudited Evaluations Section */}
        {!loading && (
          <UnauditedSection
            evaluations={evaluations}
            auditedIds={new Set(audits.map((a) => a.evaluation_id))}
            onAudit={handleAuditSingle}
            running={auditRunning}
            apiEnabled={apiStatus?.enabled ?? false}
          />
        )}

        {/* Count */}
        {!loading && filtered.length > 0 && (
          <p className="text-center text-xs text-gray-400 mt-6">
            Prikazano {filtered.length} od {audits.length} audit rezultata
          </p>
        )}
      </div>
      </div>
    </div>
  );
}

// ── Unaudited Evaluations Sub-component ──

function UnauditedSection({
  evaluations,
  auditedIds,
  onAudit,
  running,
  apiEnabled,
}: {
  evaluations: Map<string, EvaluationRow>;
  auditedIds: Set<string>;
  onAudit: (id: string) => Promise<void>;
  running: boolean;
  apiEnabled: boolean;
}) {
  const unaudited = Array.from(evaluations.values()).filter(
    (e) => !auditedIds.has(e.id)
  );

  if (unaudited.length === 0) return null;

  return (
    <div className="mt-10">
      <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
        <RefreshCw className="h-5 w-5 text-gray-400" />
        Procjene čekaju audit ({unaudited.length})
      </h2>
      <div className="space-y-2">
        {unaudited.slice(0, 20).map((ev) => (
          <div
            key={ev.id}
            className="flex items-center gap-4 rounded-xl bg-white border border-gray-200 p-3"
          >
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">
                {ev.location} · {ev.estimated_value?.toLocaleString("hr-HR")} €
              </p>
              <p className="text-xs text-gray-400">
                {ev.property_type} · {ev.size_m2 ?? "?"} m² ·{" "}
                {new Date(ev.created_at).toLocaleDateString("hr-HR")}
              </p>
            </div>
            <button
              onClick={() => onAudit(ev.id)}
              disabled={running || !apiEnabled}
              className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-50 px-3 py-1.5 text-xs font-medium text-indigo-700 hover:bg-indigo-100 disabled:opacity-50 transition"
            >
              {running ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <CheckCircle2 className="h-3.5 w-3.5" />
              )}
              Auditiraj
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
