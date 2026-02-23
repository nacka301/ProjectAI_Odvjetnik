"use client";

import { useCallback, useEffect, useState } from "react";
import {
  Clock,
  Shield,
  FileText,
  LogIn,
  Trash2,
  Edit,
  Eye,
  Download,
  Loader2,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

/* ── Types ── */
interface AuditLogEntry {
  id: string;
  created_at: string;
  module: string;
  action: string;
  legal_basis: string;
  reasoning: string;
  confidence: string;
  model_used: string;
}

type ActionCategory = "all" | "view" | "create" | "edit" | "delete" | "auth";

/* ── Helpers ── */
const ACTION_META: Record<string, { icon: typeof Eye; label: string; color: string }> = {
  view:   { icon: Eye,      label: "Pregled",  color: "bg-blue-50 text-blue-700 border-blue-200" },
  create: { icon: FileText,  label: "Kreiranje", color: "bg-green-50 text-green-700 border-green-200" },
  edit:   { icon: Edit,      label: "Izmjena",   color: "bg-amber-50 text-amber-700 border-amber-200" },
  delete: { icon: Trash2,    label: "Brisanje",  color: "bg-red-50 text-red-700 border-red-200" },
  auth:   { icon: LogIn,     label: "Prijava",   color: "bg-emerald-50 text-emerald-700 border-emerald-200" },
};

function classifyAction(action: string): string {
  const lower = action.toLowerCase();
  if (lower.includes("prijavljen") || lower.includes("login") || lower.includes("odjav"))
    return "auth";
  if (lower.includes("obris") || lower.includes("delet") || lower.includes("uklon"))
    return "delete";
  if (lower.includes("izmjen") || lower.includes("ažurir") || lower.includes("updat") || lower.includes("edit"))
    return "edit";
  if (lower.includes("kreiraj") || lower.includes("creat") || lower.includes("spremi") || lower.includes("novi"))
    return "create";
  return "view";
}

const MODULE_LABELS: Record<string, string> = {
  legal_strategy: "Pravna strategija",
  valuation: "Procjena",
  contract: "Ugovor",
  chronology: "Kronologija",
  zk_analysis: "ZK Analiza",
  invoice: "Račun",
};

function formatTimestamp(iso: string) {
  const d = new Date(iso);
  return d.toLocaleDateString("hr-HR", {
    day: "2-digit", month: "2-digit", year: "numeric",
  }) + " · " + d.toLocaleTimeString("hr-HR", {
    hour: "2-digit", minute: "2-digit",
  });
}

const PAGE_SIZE = 15;

/* ── Component ── */
export default function ActivityLog({ userId }: { userId: string }) {
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState("");
  const [filter, setFilter] = useState<ActionCategory>("all");
  const [page, setPage] = useState(0);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setFetchError("");
    try {
      const supabase = createSupabaseBrowser();
      const { data, error } = await supabase
        .from("audit_logs")
        .select("id, created_at, module, action, legal_basis, reasoning, confidence, model_used")
        .eq("user_id", userId)
        .order("created_at", { ascending: false })
        .limit(200);

      if (error) {
        setFetchError("Gre\u0161ka pri u\u010ditavanju dnevnika.");
      } else if (data) {
        setEntries(data as AuditLogEntry[]);
      }
    } catch {
      setFetchError("Nije mogu\u0107e dohvatiti podatke. Provjerite vezu.");
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => { fetchLogs(); }, [fetchLogs]);

  const filtered = filter === "all"
    ? entries
    : entries.filter((e) => classifyAction(e.action) === filter);

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paged = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);

  const handleExportCsv = () => {
    // Build CSV as simple export
    const header = "Vrijeme,Modul,Akcija,Pravna osnova,Pouzdanost,Model\n";
    const rows = filtered.map((e) =>
      [formatTimestamp(e.created_at), e.module, e.action, e.legal_basis, e.confidence, e.model_used]
        .map((v) => `"${(v || "").replace(/"/g, '""')}"`)
        .join(",")
    ).join("\n");
    const blob = new Blob(["\uFEFF" + header + rows], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `pravotek_audit_log_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <Clock className="h-5 w-5 text-slate-400" />
          <h3 className="text-base font-semibold text-slate-900">Dnevnik aktivnosti</h3>
          <span className="text-xs text-slate-400">({filtered.length})</span>
        </div>
        <button
          onClick={handleExportCsv}
          disabled={filtered.length === 0}
          className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 disabled:opacity-40 transition"
        >
          <Download className="h-3.5 w-3.5" />
          Izvezi CSV
        </button>
      </div>

      {/* Filters */}
      <div className="flex gap-1.5 flex-wrap">
        {(["all", "view", "create", "edit", "delete", "auth"] as ActionCategory[]).map((cat) => (
          <button
            key={cat}
            onClick={() => { setFilter(cat); setPage(0); }}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg transition ${
              filter === cat
                ? "bg-[#1B2A4A] text-white"
                : "bg-white border border-slate-200 text-slate-500 hover:bg-slate-50"
            }`}
          >
            {cat === "all" ? "Sve" : ACTION_META[cat]?.label || cat}
          </button>
        ))}
      </div>

      {/* Error banner */}
      {fetchError && (
        <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
          <Shield className="h-4 w-4 shrink-0" />
          {fetchError}
        </div>
      )}

      {/* Table */}
      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
        </div>
      ) : paged.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-slate-400">
          <Shield className="mb-3 h-10 w-10 text-slate-200" />
          <p className="text-sm font-medium">Nema zapisa u dnevniku</p>
          <p className="text-xs mt-1">Aktivnosti se automatski bilježe pri korištenju modula.</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-slate-100">
                <th className="px-4 py-3 text-xs font-semibold tracking-wider text-slate-500 uppercase">Vrijeme</th>
                <th className="px-4 py-3 text-xs font-semibold tracking-wider text-slate-500 uppercase">Modul</th>
                <th className="px-4 py-3 text-xs font-semibold tracking-wider text-slate-500 uppercase">Akcija</th>
                <th className="px-4 py-3 text-xs font-semibold tracking-wider text-slate-500 uppercase">Pouzdanost</th>
                <th className="px-4 py-3 text-xs font-semibold tracking-wider text-slate-500 uppercase">Model</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {paged.map((entry) => {
                const category = classifyAction(entry.action);
                const meta = ACTION_META[category] || ACTION_META.view;
                const Icon = meta.icon;
                return (
                  <tr key={entry.id} className="hover:bg-slate-50/50 transition">
                    <td className="px-4 py-3 whitespace-nowrap text-xs text-slate-500 font-mono">
                      {formatTimestamp(entry.created_at)}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-xs text-slate-600">
                      {MODULE_LABELS[entry.module] || entry.module}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-medium ${meta.color}`}>
                        <Icon className="h-3 w-3" />
                        {entry.action.length > 60 ? entry.action.slice(0, 57) + "…" : entry.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className={`text-[11px] font-medium ${
                        entry.confidence === "VISOKO" ? "text-green-600" :
                        entry.confidence === "NISKO" ? "text-red-500" : "text-amber-600"
                      }`}>
                        {entry.confidence}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-xs text-slate-400 font-mono">
                      {entry.model_used}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      {pageCount > 1 && (
        <div className="flex items-center justify-between pt-1">
          <p className="text-xs text-slate-400">
            Stranica {page + 1} od {pageCount}
          </p>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage((p) => Math.max(0, p - 1))}
              disabled={page === 0}
              className="rounded-lg border border-slate-200 p-1.5 text-slate-400 hover:bg-slate-50 disabled:opacity-30 transition"
            >
              <ChevronLeft className="h-4 w-4" />
            </button>
            <button
              onClick={() => setPage((p) => Math.min(pageCount - 1, p + 1))}
              disabled={page >= pageCount - 1}
              className="rounded-lg border border-slate-200 p-1.5 text-slate-400 hover:bg-slate-50 disabled:opacity-30 transition"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
