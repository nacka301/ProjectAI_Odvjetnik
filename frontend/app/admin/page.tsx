"use client";

import { useEffect, useState, useCallback } from "react";
import { useAuth } from "@/lib/useAuth";
import { API_BASE } from "@/lib/api";
import SubscriptionEditor from "@/components/admin/SubscriptionEditor";
import DeleteUserDialog from "@/components/admin/DeleteUserDialog";
import PrijaveGresakaPanel from "@/components/admin/PrijaveGresakaPanel";
import {
  ShieldCheck,
  Users,
  CreditCard,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Search,
  FlaskConical,
  Loader2,
  DatabaseZap,
  Mail,
  Building2,
  KeyRound,
  Activity,
  ChevronDown,
  ChevronUp,
  Trash2,
} from "lucide-react";

// ═══════════════════════════════════════════════════════════
//  Types
// ═══════════════════════════════════════════════════════════

interface AdminUser {
  user_id: string;
  email: string;
  full_name: string;
  phone_number: string | null;
  role: string;
  totp_enabled: boolean;
  created_at: string;
  last_sign_in: string | null;
  firm_name: string | null;
  firm_oib: string | null;
  law_firm_id: string | null;
  sub_status: string | null;
  valid_from: string | null;
  valid_until: string | null;
  used_credits: number;
  max_credits: number;
}

// ═══════════════════════════════════════════════════════════
//  Date formatters — hr-HR locale
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

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  try {
    return dateFmt.format(new Date(iso));
  } catch {
    return iso;
  }
}

function formatDateOnly(iso: string | null): string {
  if (!iso) return "—";
  try {
    return dateOnlyFmt.format(new Date(iso));
  } catch {
    return iso;
  }
}

function timeAgo(iso: string | null): string {
  if (!iso) return "Nikad";
  try {
    const diff = Date.now() - new Date(iso).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return "Upravo";
    if (mins < 60) return `Prije ${mins} min`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `Prije ${hours}h`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `Prije ${days}d`;
    const months = Math.floor(days / 30);
    return `Prije ${months} mj.`;
  } catch {
    return iso;
  }
}

// ═══════════════════════════════════════════════════════════
//  Badge components
// ═══════════════════════════════════════════════════════════

/**
 * One badge for the whole subscription, replacing the pair that used to sit
 * here.
 *
 * The old PlanBadge named a tier (and still listed "Premium" and "Enterprise",
 * which existed in no other file); SubStatusBadge named a status beside it, in
 * a different Croatian gender. With one plan there is one thing to say: is this
 * office on trial, paying, or shut out.
 *
 * The trial's end is derived here the same way the backend derives it —
 * valid_from + TRIAL_DAYS — rather than from valid_until, which for a trial row
 * is written for readability and is not what the gate consults.
 */
const TRIAL_DAYS = 7;

type OfficeState = "proba" | "pretplata" | "isteklo" | "bez";

function officeState(u: {
  sub_status: string | null;
  valid_from: string | null;
  valid_until: string | null;
}): OfficeState {
  if (!u.sub_status) return "bez";

  const now = Date.now();

  if (u.sub_status === "trial") {
    const start = u.valid_from ? new Date(u.valid_from).getTime() : now;
    return start + TRIAL_DAYS * 86_400_000 > now ? "proba" : "isteklo";
  }

  if (u.sub_status === "active" || u.sub_status === "canceled") {
    if (!u.valid_until) return "pretplata";
    return new Date(u.valid_until).getTime() > now ? "pretplata" : "isteklo";
  }

  return "isteklo";
}

function StateBadge({ state }: { state: OfficeState }) {
  if (state === "bez")
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-slate-50 px-2.5 py-0.5 text-xs font-medium text-slate-500 ring-1 ring-inset ring-slate-200">
        Bez pretplate
      </span>
    );

  if (state === "pretplata")
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-200">
        <CheckCircle2 className="h-3 w-3" /> Pretplata
      </span>
    );

  if (state === "proba")
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-medium text-amber-700 ring-1 ring-inset ring-amber-200">
        <Clock className="h-3 w-3" /> Proba
      </span>
    );

  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-medium text-red-700 ring-1 ring-inset ring-red-200">
      <XCircle className="h-3 w-3" /> Isteklo
    </span>
  );
}

function RoleBadge({ role }: { role: string }) {
  // Firm roles, per migration 015. The legacy "admin"/"member" pair is gone.
  const colors: Record<string, string> = {
    owner: "bg-violet-50 text-violet-700 ring-violet-200",
    lawyer: "bg-blue-50 text-blue-700 ring-blue-200",
    paralegal: "bg-slate-50 text-slate-600 ring-slate-200",
    readonly: "bg-amber-50 text-amber-700 ring-amber-200",
  };
  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ring-1 ring-inset ${colors[role] || "bg-slate-50 text-slate-600 ring-slate-200"}`}
    >
      {role}
    </span>
  );
}

function CreditBar({ used, max }: { used: number; max: number }) {
  if (max === 0) return <span className="text-xs text-slate-400">—</span>;
  const pct = Math.min(100, Math.round((used / max) * 100));
  const barColor =
    pct >= 90 ? "bg-red-500" : pct >= 70 ? "bg-amber-500" : "bg-emerald-500";
  return (
    <div className="flex items-center gap-2 min-w-[120px]">
      <div className="flex-1 h-1.5 rounded-full bg-slate-100 overflow-hidden">
        <div className={`h-full rounded-full ${barColor}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-[11px] tabular-nums text-slate-600 whitespace-nowrap">
        {used}/{max}
      </span>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════
//  Data fetching
// ═══════════════════════════════════════════════════════════

async function fetchUsers(
  authHeaders: Record<string, string>
): Promise<AdminUser[]> {
  try {
    const res = await fetch(`${API_BASE}/api/admin/users`, {
      headers: { ...authHeaders, "Content-Type": "application/json" },
    });
    if (!res.ok) return [];
    return await res.json();
  } catch {
    return [];
  }
}

// ═══════════════════════════════════════════════════════════
//  Main admin page
// ═══════════════════════════════════════════════════════════

export default function AdminDashboard() {
  const { userId, authHeaders } = useAuth();

  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");
  const [expandedUser, setExpandedUser] = useState<string | null>(null);
  const [deletingUser, setDeletingUser] = useState<AdminUser | null>(null);

  // Developer tools state
  const [seeding, setSeeding] = useState(false);
  const [seedResult, setSeedResult] = useState<{
    success: boolean;
    clients_created: number;
    cases_created: number;
    events_created: number;
    errors: string[];
  } | null>(null);

  const loadUsers = useCallback(async () => {
    if (!userId) return;
    setLoadingUsers(true);
    const data = await fetchUsers(authHeaders);
    setUsers(data);
    setLoadingUsers(false);
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
    loadUsers();
  }, [loadUsers]);

  // Client-side filter
  const filtered = searchQuery
    ? users.filter(
        (u) =>
          u.email.toLowerCase().includes(searchQuery.toLowerCase()) ||
          u.full_name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          (u.firm_name || "").toLowerCase().includes(searchQuery.toLowerCase()) ||
          (u.sub_status || "").toLowerCase().includes(searchQuery.toLowerCase())
      )
    : users;

  // Summary stats
  const totalUsers = users.length;
  const usersWithFirm = users.filter((u) => u.firm_name).length;
  // Counted through officeState so the tile agrees with the badge beside it —
  // it used to run its own date comparison and ignore trials entirely.
  const activeSubs = users.filter((u) => officeState(u) === "pretplata").length;
  const profileComplete = users.filter((u) => u.full_name && u.firm_name).length;

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
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-blue-50">
                <Users className="h-5 w-5 text-blue-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingUsers ? "…" : totalUsers}
                </p>
                <p className="text-xs text-slate-500">Ukupno korisnika</p>
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
                  {loadingUsers ? "…" : activeSubs}
                </p>
                <p className="text-xs text-slate-500">Aktivne pretplate</p>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-violet-50">
                <Building2 className="h-5 w-5 text-violet-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingUsers ? "…" : usersWithFirm}
                </p>
                <p className="text-xs text-slate-500">S uredom</p>
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center h-10 w-10 rounded-lg bg-amber-50">
                <Activity className="h-5 w-5 text-amber-600" />
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-900">
                  {loadingUsers ? "…" : `${profileComplete}/${totalUsers}`}
                </p>
                <p className="text-xs text-slate-500">Profil popunjen</p>
              </div>
            </div>
          </div>
        </div>

        {/* ── Users Table ── */}
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            <div className="flex items-center gap-2">
              <Users className="h-4 w-4 text-slate-600" />
              <h2 className="text-sm font-bold text-slate-900">
                Korisnici i pretplate
              </h2>
              {!loadingUsers && (
                <span className="text-xs text-slate-400 ml-1">
                  ({filtered.length}{searchQuery ? ` od ${totalUsers}` : ""})
                </span>
              )}
            </div>
            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
                <input
                  type="text"
                  placeholder="Pretraži ime, email, ured..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-8 pr-3 py-1.5 text-xs rounded-lg border border-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none w-56"
                />
              </div>
              <button
                onClick={loadUsers}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
              >
                <RefreshCw className="h-3 w-3" /> Osvježi
              </button>
            </div>
          </div>

          {/* Desktop table */}
          <div className="overflow-x-auto hidden sm:block">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left">
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Korisnik
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Ured
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Pretplata
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Krediti
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Zadnja prijava
                  </th>
                  <th className="px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wider text-center">
                    2FA
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {loadingUsers ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-12 text-center">
                      <RefreshCw className="h-5 w-5 text-slate-400 animate-spin mx-auto mb-2" />
                      <p className="text-xs text-slate-500">
                        Učitavanje korisnika...
                      </p>
                    </td>
                  </tr>
                ) : filtered.length === 0 ? (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-4 py-12 text-center text-sm text-slate-400"
                    >
                      {searchQuery
                        ? `Nema rezultata za "${searchQuery}"`
                        : "Nema registriranih korisnika"}
                    </td>
                  </tr>
                ) : (
                  filtered.map((user) => (
                    <tr
                      key={user.user_id}
                      className="hover:bg-slate-50/50 transition-colors cursor-pointer"
                      onClick={() =>
                        setExpandedUser(
                          expandedUser === user.user_id ? null : user.user_id
                        )
                      }
                    >
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="flex items-center justify-center h-8 w-8 rounded-full bg-slate-100 text-slate-600 text-xs font-bold uppercase shrink-0">
                            {user.full_name
                              ? user.full_name
                                  .split(" ")
                                  .map((w) => w[0])
                                  .join("")
                                  .slice(0, 2)
                              : user.email
                                ? user.email[0]
                                : "?"}
                          </div>
                          <div className="min-w-0">
                            <p className="text-sm font-medium text-slate-900 truncate">
                              {user.full_name || "Bez imena"}
                            </p>
                            <p className="text-xs text-slate-500 truncate flex items-center gap-1">
                              <Mail className="h-3 w-3 shrink-0" />
                              {user.email || "—"}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        {user.firm_name ? (
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-slate-700 truncate max-w-[180px]">
                              {user.firm_name}
                            </p>
                            {user.firm_oib && (
                              <p className="text-[11px] text-slate-400">
                                OIB: {user.firm_oib}
                              </p>
                            )}
                          </div>
                        ) : (
                          <span className="text-xs text-slate-400">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <StateBadge state={officeState(user)} />
                      </td>
                      <td className="px-4 py-3">
                        <CreditBar
                          used={user.used_credits}
                          max={user.max_credits}
                        />
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-500 whitespace-nowrap">
                        {timeAgo(user.last_sign_in)}
                      </td>
                      <td className="px-4 py-3 text-center">
                        {user.totp_enabled ? (
                          <KeyRound className="h-4 w-4 text-emerald-600 mx-auto" />
                        ) : (
                          <span className="text-xs text-slate-300">—</span>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Mobile card list */}
          <div className="sm:hidden divide-y divide-slate-100">
            {loadingUsers ? (
              <div className="px-4 py-12 text-center">
                <RefreshCw className="h-5 w-5 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-xs text-slate-500">Učitavanje...</p>
              </div>
            ) : filtered.length === 0 ? (
              <div className="px-4 py-12 text-center text-sm text-slate-400">
                Nema korisnika
              </div>
            ) : (
              filtered.map((user) => {
                const isOpen = expandedUser === user.user_id;
                return (
                  <div key={user.user_id} className="px-4 py-3">
                    <button
                      onClick={() =>
                        setExpandedUser(isOpen ? null : user.user_id)
                      }
                      className="w-full flex items-center justify-between gap-2"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <div className="flex items-center justify-center h-8 w-8 rounded-full bg-slate-100 text-slate-600 text-xs font-bold uppercase shrink-0">
                          {user.full_name
                            ? user.full_name
                                .split(" ")
                                .map((w) => w[0])
                                .join("")
                                .slice(0, 2)
                            : "?"}
                        </div>
                        <div className="text-left min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">
                            {user.full_name || user.email || "—"}
                          </p>
                          <div className="flex items-center gap-2 mt-0.5">
                            <StateBadge state={officeState(user)} />
                          </div>
                        </div>
                      </div>
                      {isOpen ? (
                        <ChevronUp className="h-4 w-4 text-slate-400 shrink-0" />
                      ) : (
                        <ChevronDown className="h-4 w-4 text-slate-400 shrink-0" />
                      )}
                    </button>
                    {isOpen && (
                      <div className="mt-3 ml-11 space-y-1.5 text-xs text-slate-600">
                        <p>
                          <span className="font-medium text-slate-500">Email:</span>{" "}
                          {user.email || "—"}
                        </p>
                        <p>
                          <span className="font-medium text-slate-500">Ured:</span>{" "}
                          {user.firm_name || "—"}
                          {user.firm_oib && ` (OIB: ${user.firm_oib})`}
                        </p>
                        <p>
                          <span className="font-medium text-slate-500">Krediti:</span>{" "}
                          {user.max_credits > 0
                            ? `${user.used_credits} / ${user.max_credits}`
                            : "—"}
                        </p>
                        <p>
                          <span className="font-medium text-slate-500">Prijava:</span>{" "}
                          {timeAgo(user.last_sign_in)}
                        </p>
                        <p>
                          <span className="font-medium text-slate-500">Registracija:</span>{" "}
                          {formatDateOnly(user.created_at)}
                        </p>
                        <p>
                          <span className="font-medium text-slate-500">2FA:</span>{" "}
                          {user.totp_enabled ? "Uključena" : "Isključena"}
                        </p>
                        {user.valid_until && (
                          <p>
                            <span className="font-medium text-slate-500">Pretplata do:</span>{" "}
                            {formatDateOnly(user.valid_until)}
                          </p>
                        )}

                        <div className="pt-3 mt-2 border-t border-slate-200">
                          <p className="font-semibold text-slate-500 uppercase tracking-wider mb-2 text-[10px]">
                            Uredi pretplatu
                          </p>
                          <SubscriptionEditor
                            lawFirmId={user.law_firm_id}
                            currentStatus={user.sub_status}
                            currentValidUntil={user.valid_until}
                            authHeaders={authHeaders}
                            onSaved={loadUsers}
                          />
                        </div>

                        <div className="pt-3 mt-2 border-t border-slate-200">
                          <p className="font-semibold text-red-500 uppercase tracking-wider mb-2 text-[10px]">
                            Opasna zona
                          </p>
                          <button
                            onClick={() => setDeletingUser(user)}
                            className="inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-semibold text-red-700 transition hover:bg-red-50"
                          >
                            <Trash2 className="h-3.5 w-3.5" /> Obriši korisnika…
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>

          {/* Expanded user detail (desktop) */}
          {expandedUser && !loadingUsers && (
            <div className="hidden sm:block border-t border-slate-200 bg-slate-50/50">
              {(() => {
                const user = users.find((u) => u.user_id === expandedUser);
                if (!user) return null;
                return (
                  <div className="px-5 py-4 grid grid-cols-2 md:grid-cols-4 gap-4 text-xs">
                    <div>
                      <p className="font-semibold text-slate-500 uppercase tracking-wider mb-1">
                        ID korisnika
                      </p>
                      <p className="font-mono text-slate-600 break-all">
                        {user.user_id}
                      </p>
                    </div>
                    <div>
                      <p className="font-semibold text-slate-500 uppercase tracking-wider mb-1">
                        Registracija
                      </p>
                      <p className="text-slate-700">
                        {formatDate(user.created_at)}
                      </p>
                    </div>
                    <div>
                      <p className="font-semibold text-slate-500 uppercase tracking-wider mb-1">
                        Uloga
                      </p>
                      <RoleBadge role={user.role} />
                    </div>
                    <div>
                      <p className="font-semibold text-slate-500 uppercase tracking-wider mb-1">
                        Telefon
                      </p>
                      <p className="text-slate-700">
                        {user.phone_number || "—"}
                      </p>
                    </div>
                    {user.valid_until && (
                      <div>
                        <p className="font-semibold text-slate-500 uppercase tracking-wider mb-1">
                          Pretplata do
                        </p>
                        <p className="text-slate-700">
                          {formatDateOnly(user.valid_until)}
                        </p>
                      </div>
                    )}

                    {/* This is where an e-mail saying "I paid" becomes access. */}
                    <div className="col-span-2 md:col-span-4 pt-3 border-t border-slate-200">
                      <p className="font-semibold text-slate-500 uppercase tracking-wider mb-2">
                        Uredi pretplatu
                      </p>
                      <SubscriptionEditor
                        lawFirmId={user.law_firm_id}
                        currentStatus={user.sub_status}
                        currentValidUntil={user.valid_until}
                        authHeaders={authHeaders}
                        onSaved={loadUsers}
                      />
                    </div>

                    {/* Opasna zona — potpuno brisanje korisnika/ureda */}
                    <div className="col-span-2 md:col-span-4 pt-3 border-t border-slate-200">
                      <p className="font-semibold text-red-500 uppercase tracking-wider mb-2">
                        Opasna zona
                      </p>
                      <button
                        onClick={() => setDeletingUser(user)}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-xs font-semibold text-red-700 transition hover:bg-red-50"
                      >
                        <Trash2 className="h-3.5 w-3.5" /> Obriši korisnika…
                      </button>
                      <p className="mt-1.5 text-[11px] text-slate-400">
                        Potpuno brisanje (član ili cijeli ured) — oslobađa e-mail za ponovnu registraciju.
                      </p>
                    </div>
                  </div>
                );
              })()}
            </div>
          )}
        </section>

        {/* ── Prijave grešaka u izračunu (plan pravne zaštite, dio D) ── */}
        <PrijaveGresakaPanel
          authHeaders={authHeaders}
          emailPoKorisniku={Object.fromEntries(users.map((u) => [u.user_id, u.email]))}
        />

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
                  upisniku. Podaci se vežu na vaš korisnički račun i{" "}
                  <strong>ne mogu se automatski obrisati</strong>.
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
          Prikazani podaci ne uključuju tijela zahtjeva/odgovora (GDPR /
          odvjetnička tajna).
        </p>
      </div>

      {deletingUser && (
        <DeleteUserDialog
          userId={deletingUser.user_id}
          email={deletingUser.email}
          authHeaders={authHeaders}
          onClose={() => setDeletingUser(null)}
          onDeleted={loadUsers}
        />
      )}
    </div>
  );
}
