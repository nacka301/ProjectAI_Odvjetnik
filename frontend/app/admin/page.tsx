"use client";

import { useEffect, useState, useCallback } from "react";
import { useAuth } from "@/lib/useAuth";
import { API_BASE } from "@/lib/api";
import {
  ShieldCheck,
  Activity,
  CreditCard,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Search,
  ChevronLeft,
  ChevronRight,
  FlaskConical,
  Loader2,
  DatabaseZap,
} from "lucide-react";

// ═══════════════════════════════════════════════════════════
//  Types
// ═══════════════════════════════════════════════════════════

interface AuditLogEntry {
  id: string;
  created_at: string;
  tenant_id: string | null;
  user_id: string | null;
  action_name: string;
  endpoint: string;
  http_method: string;
  status_code: number;
  duration_ms: number | null;
}

interface TenantSubscription {
  id: string;
  tenant_id: string;
  plan_type: string;
  valid_until: string;
  is_active: boolean;
  created_at: string;
}

// ═══════════════════════════════════════════════════════════
//  Date formatter — hr-HR locale
// ═══════════════════════════════════════════════════════════

const dateFmt = new Intl.DateTimeFormat("hr-HR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

const dateOnlyFmt = new Intl.DateTimeFormat("hr-HR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

function formatDate(iso: string): string {
  try {
    return dateFmt.format(new Date(iso));
  } catch {
    return iso;
  }
}

function formatDateOnly(iso: string): string {
  try {
    return dateOnlyFmt.format(new Date(iso));
  } catch {
    return iso;
  }
}

// ═══════════════════════════════════════════════════════════
//  Data fetching hooks
// ═══════════════════════════════════════════════════════════

async function fetchAuditLogs(
  authHeaders: Record<string, string>,
  page: number = 0,
  limit: number = 25
): Promise<AuditLogEntry[]> {
  try {
    const offset = page * limit;
    const res = await fetch(
      `${API_BASE}/api/admin/audit-logs?limit=${limit}&offset=${offset}`,
      { headers: { ...authHeaders, "Content-Type": "application/json" } }
    );
    if (!res.ok) return [];
    return await res.json();
  } catch {
    return [];
  }
}

async function fetchSubscriptions(
  authHeaders: Record<string, string>
): Promise<TenantSubscription[]> {
  try {
    const res = await fetch(`${API_BASE}/api/admin/subscriptions`, {
      headers: { ...authHeaders, "Content-Type": "application/json" },
    });
    if (!res.ok) return [];
    return await res.json();
  } catch {
    return [];
  }
}

// ═══════════════════════════════════════════════════════════
//  Status badge helpers
// ═══════════════════════════════════════════════════════════

function StatusCodeBadge({ code }: { code: number }) {
  if (code >= 500)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-medium text-red-700 ring-1 ring-inset ring-red-200">
        <XCircle className="h-3 w-3" /> {code}
      </span>
    );
  if (code >= 400)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-medium text-amber-700 ring-1 ring-inset ring-amber-200">
        <AlertTriangle className="h-3 w-3" /> {code}
      </span>
    );
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-200">
      <CheckCircle2 className="h-3 w-3" /> {code}
    </span>
  );
}

function MethodBadge({ method }: { method: string }) {
  const colors: Record<string, string> = {
    GET: "bg-blue-50 text-blue-700 ring-blue-200",
    POST: "bg-green-50 text-green-700 ring-green-200",
    PUT: "bg-amber-50 text-amber-700 ring-amber-200",
    PATCH: "bg-orange-50 text-orange-700 ring-orange-200",
    DELETE: "bg-red-50 text-red-700 ring-red-200",
  };
  const cls = colors[method] || "bg-slate-50 text-slate-700 ring-slate-200";
  return (
    <span
      className={`inline-flex rounded px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ring-1 ring-inset ${cls}`}
    >
      {method}
    </span>
  );
}

function PlanBadge({ plan }: { plan: string }) {
  const colors: Record<string, string> = {
    Free: "bg-slate-100 text-slate-600",
    Starter: "bg-blue-100 text-blue-700",
    Pro: "bg-violet-100 text-violet-700",
    Premium: "bg-amber-100 text-amber-700",
    Enterprise: "bg-emerald-100 text-emerald-700",
  };
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${colors[plan] || "bg-slate-100 text-slate-600"}`}
    >
      {plan}
    </span>
  );
}

function SubscriptionStatusBadge({ active, validUntil }: { active: boolean; validUntil: string }) {
  const expired = new Date(validUntil) < new Date();
  if (!active || expired)
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-medium text-red-700 ring-1 ring-inset ring-red-200">
        <XCircle className="h-3 w-3" /> Neaktivna
      </span>
    );
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-200">
      <CheckCircle2 className="h-3 w-3" /> Aktivna
    </span>
  );
}

// ═══════════════════════════════════════════════════════════
//  Truncated UUID display
// ═══════════════════════════════════════════════════════════

function TruncatedId({ id }: { id: string | null }) {
  if (!id) return <span className="text-slate-400">—</span>;
  return (
    <span className="font-mono text-xs text-slate-500" title={id}>
      {id.slice(0, 8)}…
    </span>
  );
}

// ═══════════════════════════════════════════════════════════
//  Main admin page
// ═══════════════════════════════════════════════════════════

export default function AdminDashboard() {
  const { userId, authHeaders } = useAuth();

  const [auditLogs, setAuditLogs] = useState<AuditLogEntry[]>([]);
  const [subscriptions, setSubscriptions] = useState<TenantSubscription[]>([]);
  const [loadingLogs, setLoadingLogs] = useState(true);
  const [loadingSubs, setLoadingSubs] = useState(true);
  const [logPage, setLogPage] = useState(0);
  const [searchQuery, setSearchQuery] = useState("");
  const [seeding, setSeeding] = useState(false);
  const [seedResult, setSeedResult] = useState<{
    success: boolean;
    clients_created: number;
    cases_created: number;
    events_created: number;
    errors: string[];
  } | null>(null);

  const PAGE_SIZE = 25;

  const loadLogs = useCallback(
    async (page: number) => {
      if (!userId) return;
      setLoadingLogs(true);
      const data = await fetchAuditLogs(authHeaders, page, PAGE_SIZE);
      setAuditLogs(data);
      setLoadingLogs(false);
    },
    [userId, authHeaders]
  );

  const loadSubs = useCallback(async () => {
    if (!userId) return;
    setLoadingSubs(true);
    const data = await fetchSubscriptions(authHeaders);
    setSubscriptions(data);
    setLoadingSubs(false);
  }, [userId, authHeaders]);

  const handleSeedDemoData = async () => {
    if (seeding) return;
    setSeedResult(null);
    setSeeding(true);
    try {
      const res = await fetch(`${API_BASE}/api/admin/seed-demo-data`, {
        method: "POST",
        headers: { ...authHeaders, "Content-Type": "application/json" },
      });
      const data = await res.json();
      setSeedResult(data);
    } catch {
      setSeedResult({
        success: false,
        clients_created: 0,
        cases_created: 0,
        events_created: 0,
        errors: ["Mrežna greška — pokušajte ponovno."],
      });
    } finally {
      setSeeding(false);
    }
  };

  useEffect(() => {
    loadLogs(logPage);
  }, [loadLogs, logPage]);

  useEffect(() => {
    loadSubs();
  }, [loadSubs]);

  // Filter audit logs by search query (client-side)
  const filteredLogs = searchQuery
    ? auditLogs.filter(
        (l) =>
          l.action_name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          l.endpoint.toLowerCase().includes(searchQuery.toLowerCase()) ||
          l.http_method.toLowerCase().includes(searchQuery.toLowerCase()) ||
          String(l.status_code).includes(searchQuery)
      )
    : auditLogs;

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-gradient-to-br from-slate-900 to-slate-700">
                <ShieldCheck className="h-5 w-5 text-white" />
              </div>
              <div>
                <h1 className="text-lg font-bold text-slate-900">
                  Admin Dashboard
                </h1>
                <p className="text-xs text-slate-500">
                  PravoTek — Upravljačka ploča
                </p>
              </div>
            </div>
            <a
              href="/"
              className="text-sm text-slate-500 hover:text-slate-900 transition"
            >
              ← Natrag na app
            </a>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
        {/* ── Summary cards ── */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-blue-50">
                <Activity className="h-5 w-5 text-blue-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingLogs ? "…" : auditLogs.length}
                </p>
                <p className="text-xs text-slate-500">Audit zapisi (stranica)</p>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-emerald-50">
                <CreditCard className="h-5 w-5 text-emerald-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingSubs
                    ? "…"
                    : subscriptions.filter((s) => s.is_active).length}
                </p>
                <p className="text-xs text-slate-500">Aktivne pretplate</p>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-amber-50">
                <Clock className="h-5 w-5 text-amber-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingLogs
                    ? "…"
                    : auditLogs.filter((l) => l.status_code >= 400).length}
                </p>
                <p className="text-xs text-slate-500">Grešaka (ova stranica)</p>
              </div>
            </div>
          </div>
        </div>

        {/* ── Audit Logs Table ── */}
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            <div className="flex items-center gap-2">
              <Activity className="h-4 w-4 text-slate-600" />
              <h2 className="text-sm font-bold text-slate-900">
                Nedavne aktivnosti (Audit Logs)
              </h2>
            </div>
            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
                <input
                  type="text"
                  placeholder="Pretraži..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-8 pr-3 py-1.5 text-xs rounded-lg border border-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none w-48"
                />
              </div>
              <button
                onClick={() => loadLogs(logPage)}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
              >
                <RefreshCw className="h-3 w-3" /> Osvježi
              </button>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left">
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Datum
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Tenant ID
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Metoda
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Akcija
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider text-right">
                    Trajanje
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {loadingLogs ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-12 text-center">
                      <RefreshCw className="h-5 w-5 text-slate-400 animate-spin mx-auto mb-2" />
                      <p className="text-xs text-slate-500">
                        Učitavanje audit logova...
                      </p>
                    </td>
                  </tr>
                ) : filteredLogs.length === 0 ? (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-4 py-12 text-center text-sm text-slate-400"
                    >
                      Nema zapisa
                      {searchQuery && (
                        <span>
                          {" "}
                          za upit &quot;{searchQuery}&quot;
                        </span>
                      )}
                    </td>
                  </tr>
                ) : (
                  filteredLogs.map((log) => (
                    <tr
                      key={log.id}
                      className="hover:bg-slate-50/50 transition-colors"
                    >
                      <td className="px-4 py-3 text-xs text-slate-600 whitespace-nowrap">
                        {formatDate(log.created_at)}
                      </td>
                      <td className="px-4 py-3">
                        <TruncatedId id={log.tenant_id} />
                      </td>
                      <td className="px-4 py-3">
                        <MethodBadge method={log.http_method} />
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-slate-700 max-w-[280px] truncate">
                        {log.endpoint}
                      </td>
                      <td className="px-4 py-3">
                        <StatusCodeBadge code={log.status_code} />
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-500 text-right tabular-nums whitespace-nowrap">
                        {log.duration_ms != null
                          ? `${log.duration_ms} ms`
                          : "—"}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <div className="px-5 py-3 border-t border-slate-100 flex items-center justify-between">
            <p className="text-xs text-slate-500">
              Stranica {logPage + 1} · {filteredLogs.length} zapisa
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setLogPage((p) => Math.max(0, p - 1))}
                disabled={logPage === 0}
                className="inline-flex items-center justify-center h-7 w-7 rounded-md border border-slate-200 text-slate-500 hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronLeft className="h-3.5 w-3.5" />
              </button>
              <button
                onClick={() => setLogPage((p) => p + 1)}
                disabled={auditLogs.length < PAGE_SIZE}
                className="inline-flex items-center justify-center h-7 w-7 rounded-md border border-slate-200 text-slate-500 hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed transition"
              >
                <ChevronRight className="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        </section>

        {/* ── Subscriptions Table ── */}
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-200 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <CreditCard className="h-4 w-4 text-slate-600" />
              <h2 className="text-sm font-bold text-slate-900">
                Aktivne pretplate
              </h2>
            </div>
            <button
              onClick={loadSubs}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
            >
              <RefreshCw className="h-3 w-3" /> Osvježi
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left">
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Tenant ID
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Plan
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Vrijedi do
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Kreirano
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {loadingSubs ? (
                  <tr>
                    <td colSpan={5} className="px-4 py-12 text-center">
                      <RefreshCw className="h-5 w-5 text-slate-400 animate-spin mx-auto mb-2" />
                      <p className="text-xs text-slate-500">
                        Učitavanje pretplata...
                      </p>
                    </td>
                  </tr>
                ) : subscriptions.length === 0 ? (
                  <tr>
                    <td
                      colSpan={5}
                      className="px-4 py-12 text-center text-sm text-slate-400"
                    >
                      Nema pretplata
                    </td>
                  </tr>
                ) : (
                  subscriptions.map((sub) => (
                    <tr
                      key={sub.id}
                      className="hover:bg-slate-50/50 transition-colors"
                    >
                      <td className="px-4 py-3">
                        <TruncatedId id={sub.tenant_id} />
                      </td>
                      <td className="px-4 py-3">
                        <PlanBadge plan={sub.plan_type} />
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-600 whitespace-nowrap">
                        {formatDateOnly(sub.valid_until)}
                      </td>
                      <td className="px-4 py-3">
                        <SubscriptionStatusBadge
                          active={sub.is_active}
                          validUntil={sub.valid_until}
                        />
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-500 whitespace-nowrap">
                        {formatDateOnly(sub.created_at)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>

        {/* ── Developer Tools — Danger Zone ── */}
        <section className="rounded-xl border-2 border-red-300 bg-red-50/30 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-red-200 flex items-center gap-2">
            <FlaskConical className="h-4 w-4 text-red-600" />
            <h2 className="text-sm font-bold text-red-900">
              Developer Tools — Opasna zona
            </h2>
          </div>

          <div className="px-5 py-5 space-y-4">
            <div className="flex items-start gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-red-100 shrink-0 mt-0.5">
                <DatabaseZap className="h-5 w-5 text-red-600" />
              </div>
              <div className="flex-1">
                <h3 className="text-sm font-semibold text-slate-900">
                  Generiraj Demo Podatke
                </h3>
                <p className="text-xs text-slate-600 mt-0.5 leading-relaxed">
                  Stvara 10 klijenata s hrvatskim imenima i OIB-ovima, 10
                  predmeta (parnice, ovrhe, savjetovanja…) i 100 događaja u
                  upisniku. Podaci se vežu na vaš korisnički račun i <strong>ne mogu
                  se automatski obrisati</strong>.
                </p>

                <div className="flex items-center gap-3 mt-3">
                  <button
                    onClick={handleSeedDemoData}
                    disabled={seeding}
                    className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-700 disabled:opacity-60 disabled:cursor-not-allowed rounded-lg transition shadow-sm"
                  >
                    {seeding ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Generiranje…
                      </>
                    ) : (
                      <>
                        <DatabaseZap className="h-4 w-4" />
                        Generiraj Demo Podatke
                      </>
                    )}
                  </button>
                </div>

                {/* Seed result toast */}
                {seedResult && (
                  <div
                    className={`mt-3 rounded-lg border p-3 text-xs ${
                      seedResult.success
                        ? "border-emerald-200 bg-emerald-50 text-emerald-800"
                        : "border-amber-200 bg-amber-50 text-amber-800"
                    }`}
                  >
                    <div className="flex items-center gap-1.5 font-semibold mb-1">
                      {seedResult.success ? (
                        <>
                          <CheckCircle2 className="h-3.5 w-3.5" /> Podaci
                          uspješno generirani!
                        </>
                      ) : (
                        <>
                          <AlertTriangle className="h-3.5 w-3.5" /> Djelomično
                          generirano (s greškama)
                        </>
                      )}
                    </div>
                    <p>
                      Klijenata: {seedResult.clients_created} · Predmeta:{" "}
                      {seedResult.cases_created} · Događaja:{" "}
                      {seedResult.events_created}
                    </p>
                    {seedResult.errors.length > 0 && (
                      <ul className="mt-1 list-disc list-inside text-[11px] opacity-80">
                        {seedResult.errors.slice(0, 5).map((e, i) => (
                          <li key={i}>{e}</li>
                        ))}
                        {seedResult.errors.length > 5 && (
                          <li>…i još {seedResult.errors.length - 5} grešaka</li>
                        )}
                      </ul>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* Footer note */}
        <p className="text-center text-xs text-slate-400 pb-4">
          Audit logovi ne sadrže tijela zahtjeva/odgovora (GDPR / odvjetnička
          tajna). Prikazani su samo metapodaci.
        </p>
      </div>
    </div>
  );
}
