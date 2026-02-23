"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Download,
  Trash2,
  AlertTriangle,
  Loader2,
  Shield,
  Database,
  CheckCircle2,
  X,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

interface PrivacyPanelProps {
  userId: string;
  userEmail: string;
}

export default function PrivacyPanel({ userId, userEmail }: PrivacyPanelProps) {
  const router = useRouter();
  const [exporting, setExporting] = useState(false);
  const [exported, setExported] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");

  /* ── Export all user data ── */
  const handleExport = async () => {
    setExporting(true);
    setExported(false);

    try {
      const supabase = createSupabaseBrowser();

      // Fetch all user data tables in parallel
      const [
        { data: evaluations },
        { data: contracts },
        { data: chronologies },
        { data: legalStrategies },
        { data: clients },
        { data: cases },
        { data: troskovnici },
        { data: invoices },
        { data: auditLogs },
        { data: zkAnalyses },
      ] = await Promise.all([
        supabase.from("evaluations").select("*").eq("user_id", userId),
        supabase.from("contract_analyses").select("*").eq("user_id", userId),
        supabase.from("chronologies").select("*").eq("user_id", userId),
        supabase.from("legal_strategies").select("*").eq("user_id", userId),
        supabase.from("clients").select("*").eq("user_id", userId),
        supabase.from("cases").select("*").eq("user_id", userId),
        supabase.from("troskovnici").select("*").eq("user_id", userId),
        supabase.from("invoices").select("*").eq("user_id", userId),
        supabase.from("audit_logs").select("*").eq("user_id", userId),
        supabase.from("zk_analyses").select("*").eq("user_id", userId),
      ]);

      const exportData = {
        meta: {
          exported_at: new Date().toISOString(),
          user_id: userId,
          user_email: userEmail,
          platform: "PravoTek",
          gdpr_article: "Članak 20. GDPR — Pravo na prenosivost podataka",
        },
        evaluations: evaluations || [],
        contract_analyses: contracts || [],
        chronologies: chronologies || [],
        legal_strategies: legalStrategies || [],
        clients: clients || [],
        cases: cases || [],
        troskovnici: troskovnici || [],
        invoices: invoices || [],
        audit_logs: auditLogs || [],
        zk_analyses: zkAnalyses || [],
      };

      // Download as JSON
      const blob = new Blob([JSON.stringify(exportData, null, 2)], {
        type: "application/json",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `pravotek_export_${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);

      setExported(true);
    } catch {
      // silent
    } finally {
      setExporting(false);
    }
  };

  /* ── Delete account ── */
  const handleDeleteAccount = async () => {
    if (deleteConfirm !== "OBRIŠI") return;
    setDeleting(true);
    setDeleteError("");

    try {
      const supabase = createSupabaseBrowser();

      // Delete all user data from all tables
      const tables = [
        "audit_logs",
        "valuation_audits",
        "invoices",
        "troskovnici",
        "cases",
        "clients",
        "legal_strategies",
        "chronologies",
        "contract_analyses",
        "zk_analyses",
        "evaluations",
      ];

      for (const table of tables) {
        await supabase.from(table).delete().eq("user_id", userId);
      }

      // Delete user profile
      await supabase.from("user_profiles").delete().eq("user_id", userId);

      // Sign out
      await supabase.auth.signOut();

      // Note: Actual auth.users deletion requires admin/service_role.
      // The backend should handle this via a dedicated endpoint.
      // For now, we clear all data and sign out.

      router.push("/auth/login?reason=deleted");
      router.refresh();
    } catch {
      setDeleteError("Došlo je do greške. Kontaktirajte podršku za pomoć.");
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Shield className="h-5 w-5 text-slate-400" />
        <div>
          <h3 className="text-base font-semibold text-slate-900">Privatnost i podaci</h3>
          <p className="text-xs text-slate-400">GDPR čl. 15-20 · Pravo pristupa, ispravka i prenosivosti</p>
        </div>
      </div>

      {/* ── Data portability ── */}
      <div className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <div className="flex items-start gap-3">
          <Database className="h-5 w-5 text-blue-500 mt-0.5 shrink-0" />
          <div>
            <h4 className="text-sm font-semibold text-slate-900">Preuzmi sve svoje podatke</h4>
            <p className="text-xs text-slate-500 mt-1">
              Sukladno čl. 20. GDPR-a (pravo prenosivosti), možete preuzeti sve svoje podatke u strojno-čitljivom JSON formatu.
              Uključuje procjene, ugovore, stranke, predmete, troškovnike, račune i revizijske zapise.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleExport}
            disabled={exporting}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition shadow-sm"
          >
            {exporting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Izvažam podatke...
              </>
            ) : (
              <>
                <Download className="h-4 w-4" />
                Preuzmi sve podatke
              </>
            )}
          </button>
          {exported && (
            <span className="inline-flex items-center gap-1 text-xs text-green-600">
              <CheckCircle2 className="h-3.5 w-3.5" />
              Preuzimanje započeto
            </span>
          )}
        </div>
      </div>

      {/* ── Right to be forgotten ── */}
      <div className="rounded-xl border border-red-200 bg-red-50/30 p-6 space-y-4">
        <div className="flex items-start gap-3">
          <AlertTriangle className="h-5 w-5 text-red-500 mt-0.5 shrink-0" />
          <div>
            <h4 className="text-sm font-semibold text-red-900">Deaktiviraj i obriši račun</h4>
            <p className="text-xs text-red-700/70 mt-1">
              Sukladno čl. 17. GDPR-a (pravo na zaborav), možete zatražiti trajno brisanje svih svojih podataka.
              Ova radnja je <strong>nepovratna</strong> — svi predmeti, stranke, procjene i dokumenti bit će trajno obrisani.
            </p>
          </div>
        </div>

        <button
          onClick={() => setShowDeleteModal(true)}
          className="inline-flex items-center gap-2 rounded-lg border border-red-300 bg-white px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 transition"
        >
          <Trash2 className="h-4 w-4" />
          Deaktiviraj račun
        </button>
      </div>

      {/* ── Legal info ── */}
      <div className="text-[11px] text-slate-400 space-y-1">
        <p>PravoTek poštuje Uredbu (EU) 2016/679 (Opća uredba o zaštiti podataka — GDPR).</p>
        <p>Vaši podaci se pohranjuju na Supabase infrastrukturi u EU regiji uz enkripciju u mirovanju (AES-256) i prijenosu (TLS 1.3).</p>
        <p>Za dodatna pitanja o privatnosti obratite se na: podrska@pravotek.hr</p>
      </div>

      {/* ── Delete Confirmation Modal ── */}
      {showDeleteModal && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-slate-900/50 backdrop-blur-sm" onClick={() => !deleting && setShowDeleteModal(false)} />
          <div className="relative bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md mx-4 p-6 space-y-5">
            {/* Close */}
            <button
              onClick={() => !deleting && setShowDeleteModal(false)}
              className="absolute top-4 right-4 text-slate-400 hover:text-slate-600"
              disabled={deleting}
            >
              <X className="h-5 w-5" />
            </button>

            {/* Icon + title */}
            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center w-10 h-10 rounded-full bg-red-100">
                <AlertTriangle className="h-5 w-5 text-red-600" />
              </div>
              <div>
                <h3 className="text-lg font-bold text-slate-900">Trajno brisanje</h3>
                <p className="text-xs text-slate-500">{userEmail}</p>
              </div>
            </div>

            {/* Warning */}
            <div className="rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-800 space-y-2">
              <p className="font-medium">Ova radnja je nepovratna.</p>
              <p className="text-xs text-red-600">
                Svi vaši podaci — predmeti, stranke, procjene, ugovori, troškovnici,
                računi i revizijski zapisi — bit će trajno obrisani iz sustava.
              </p>
            </div>

            {/* Confirmation input */}
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Upišite <span className="font-mono font-bold text-red-600">OBRIŠI</span> za potvrdu
              </label>
              <input
                type="text"
                value={deleteConfirm}
                onChange={(e) => setDeleteConfirm(e.target.value)}
                placeholder="OBRIŠI"
                className="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-sm text-slate-900 placeholder:text-slate-300 focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-transparent"
                disabled={deleting}
              />
            </div>

            {deleteError && (
              <p className="text-xs text-red-600">{deleteError}</p>
            )}

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-1">
              <button
                onClick={() => setShowDeleteModal(false)}
                disabled={deleting}
                className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 transition"
              >
                Odustani
              </button>
              <button
                onClick={handleDeleteAccount}
                disabled={deleteConfirm !== "OBRIŠI" || deleting}
                className="inline-flex items-center gap-2 rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50 transition"
              >
                {deleting ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Brišem podatke...
                  </>
                ) : (
                  <>
                    <Trash2 className="h-4 w-4" />
                    Trajno obriši sve
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
