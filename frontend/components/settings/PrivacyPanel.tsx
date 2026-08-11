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
  Building2,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { API_BASE } from "@/lib/api";

/**
 * No userId prop any more: every call here derives the acting user from the
 * verified token on the backend. Passing an id from the client would be a
 * value the server must not trust, and this panel deletes things.
 */
interface PrivacyPanelProps {
  userEmail: string;
}

/**
 * Which deletion the modal is currently offering.
 *
 * "account" is the normal path. The backend flips us to "firm" when the caller
 * turns out to be the last member — deleting only the person would leave the
 * firm's files with nobody able to reach them, so that has to become a
 * deliberate, separately confirmed act rather than a side effect.
 */
type DeleteMode = "account" | "firm";

const ACCOUNT_CONFIRMATION = "OBRIŠI RAČUN";

export default function PrivacyPanel({ userEmail }: PrivacyPanelProps) {
  const router = useRouter();

  const [exporting, setExporting] = useState(false);
  const [exported, setExported] = useState(false);
  const [exportError, setExportError] = useState("");

  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [mode, setMode] = useState<DeleteMode>("account");
  const [confirmText, setConfirmText] = useState("");
  const [firmName, setFirmName] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");
  const [blockedNote, setBlockedNote] = useState("");

  /** Bearer headers, read fresh from the session on each call. */
  const authHeaders = async (): Promise<Record<string, string>> => {
    const supabase = createSupabaseBrowser();
    const { data: { session } } = await supabase.auth.getSession();
    return {
      "Content-Type": "application/json",
      ...(session ? { Authorization: `Bearer ${session.access_token}` } : {}),
    };
  };

  /* ── Export (GDPR čl. 20) ────────────────────────────────────
     Runs on the backend now. Doing it from the browser meant the export
     silently reflected whatever RLS let through, and it filtered by user_id —
     so it omitted the firm's records entered by colleagues, which is most of
     them. An incomplete export defeats the purpose of portability. */
  const handleExport = async () => {
    setExporting(true);
    setExported(false);
    setExportError("");

    try {
      const res = await fetch(`${API_BASE}/api/account/export`, {
        headers: await authHeaders(),
      });

      if (!res.ok) throw new Error("export failed");

      const exportData = await res.json();

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
      setExportError("Greška pri izvozu podataka. Pokušajte ponovo.");
    } finally {
      setExporting(false);
    }
  };

  /* ── Delete ──────────────────────────────────────────────────
     The browser can no longer do this itself: removing the row from
     auth.users needs the service_role key, and after migration 016 the
     client cannot delete its own profile either. Both are deliberate. */
  const handleDelete = async () => {
    setDeleting(true);
    setDeleteError("");

    const endpoint = mode === "firm" ? "/api/account/firm" : "/api/account";
    const body =
      mode === "firm"
        ? { firm_name_confirmation: confirmText }
        : { confirmation: confirmText };

    try {
      const res = await fetch(`${API_BASE}${endpoint}`, {
        method: "DELETE",
        headers: await authHeaders(),
        body: JSON.stringify(body),
      });

      const data = await res.json().catch(() => null);

      if (res.status === 409) {
        // Refused because of firm state, not because the request was malformed.
        if (data?.code === "LAST_MEMBER") {
          setMode("firm");
          setConfirmText("");
          setBlockedNote(data.error ?? "");
          await loadFirmName();
          return;
        }
        setDeleteError(data?.error ?? "Brisanje trenutno nije moguće.");
        return;
      }

      if (!res.ok) {
        setDeleteError(data?.error ?? "Došlo je do greške. Kontaktirajte podršku.");
        return;
      }

      const supabase = createSupabaseBrowser();
      await supabase.auth.signOut();
      router.push("/auth/login?reason=deleted");
      router.refresh();
    } catch {
      setDeleteError("Došlo je do greške. Kontaktirajte podršku za pomoć.");
    } finally {
      setDeleting(false);
    }
  };

  /** The firm name is what the owner must retype to confirm firm deletion. */
  const loadFirmName = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/settings/firm`, {
        headers: await authHeaders(),
      });
      if (res.ok) {
        const firm = await res.json();
        setFirmName(firm?.name ?? "");
      }
    } catch {
      // Not fatal — the field simply shows no hint, and the backend still
      // compares against the real name.
    }
  };

  const closeModal = () => {
    if (deleting) return;
    setShowDeleteModal(false);
    setMode("account");
    setConfirmText("");
    setDeleteError("");
    setBlockedNote("");
  };

  const expected = mode === "firm" ? firmName : ACCOUNT_CONFIRMATION;
  const canSubmit = confirmText.trim() === expected.trim() && expected.length > 0;

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
          <Database className="h-5 w-5 text-slate-400 mt-0.5 shrink-0" />
          <div>
            <h4 className="text-sm font-semibold text-slate-900">Izvoz podataka</h4>
            <p className="text-xs text-slate-500 mt-1">
              Preuzmite sve podatke svog ureda u strojno čitljivom JSON formatu
              (čl. 20. GDPR — pravo na prenosivost).
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleExport}
            disabled={exporting}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
          >
            {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            {exporting ? "Pripremam..." : "Preuzmi moje podatke"}
          </button>

          {exported && (
            <span className="inline-flex items-center gap-1.5 text-xs text-emerald-600">
              <CheckCircle2 className="h-3.5 w-3.5" />
              Preuzimanje pokrenuto
            </span>
          )}
          {exportError && (
            <span className="text-xs text-red-600">
              {exportError}
            </span>
          )}
        </div>
      </div>

      {/* ── Right to be forgotten ── */}
      <div className="rounded-xl border border-red-200 bg-red-50/30 p-6 space-y-4">
        <div className="flex items-start gap-3">
          <AlertTriangle className="h-5 w-5 text-red-500 mt-0.5 shrink-0" />
          <div>
            <h4 className="text-sm font-semibold text-red-900">Obriši račun</h4>
            <p className="text-xs text-red-700/70 mt-1">
              Sukladno čl. 17. GDPR-a možete zatražiti brisanje svog korisničkog računa.
              Radnja je <strong>nepovratna</strong>.
            </p>
            <p className="text-xs text-red-700/70 mt-2">
              Predmeti, stranke i računi pripadaju <strong>uredu</strong>, a ne pojedinom
              korisniku — pa ostaju uredu i nakon što obrišete svoj račun. Uklanja se
              vaš pristup i vaši osobni podaci.
            </p>
          </div>
        </div>

        <button
          onClick={() => { setShowDeleteModal(true); setMode("account"); }}
          className="inline-flex items-center gap-2 rounded-lg border border-red-300 bg-white px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 transition"
        >
          <Trash2 className="h-4 w-4" />
          Obriši račun
        </button>
      </div>

      {/* ── Legal info ── */}
      <div className="text-[11px] text-slate-400 space-y-1">
        <p>PravoTek poštuje Uredbu (EU) 2016/679 (Opća uredba o zaštiti podataka — GDPR).</p>
        <p>Vaši podaci se pohranjuju na Supabase infrastrukturi u EU regiji (Frankfurt) uz enkripciju u mirovanju (AES-256) i prijenosu (TLS 1.3).</p>
        <p>Za dodatna pitanja o privatnosti obratite se na: podrska@pravotek.hr</p>
      </div>

      {/* ── Confirmation modal ── */}
      {showDeleteModal && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center" role="dialog" aria-modal="true">
          <div className="absolute inset-0 bg-slate-900/50 backdrop-blur-sm" onClick={closeModal} />
          <div className="relative bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-md mx-4 p-6 space-y-5">
            <button
              onClick={closeModal}
              className="absolute top-4 right-4 text-slate-400 hover:text-slate-600"
              disabled={deleting}
            >
              <X className="h-5 w-5" />
            </button>

            <div className="flex items-center gap-3">
              <div className="flex items-center justify-center w-10 h-10 rounded-full bg-red-100">
                {mode === "firm"
                  ? <Building2 className="h-5 w-5 text-red-600" />
                  : <AlertTriangle className="h-5 w-5 text-red-600" />}
              </div>
              <div>
                <h3 className="text-lg font-bold text-slate-900">
                  {mode === "firm" ? "Brisanje ureda" : "Brisanje računa"}
                </h3>
                <p className="text-xs text-slate-500">{userEmail}</p>
              </div>
            </div>

            {/* Why we switched to firm deletion */}
            {mode === "firm" && blockedNote && (
              <div className="rounded-lg bg-amber-50 border border-amber-200 p-4 text-xs text-amber-800">
                {blockedNote}
              </div>
            )}

            <div className="rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-800 space-y-2">
              <p className="font-medium">Ova radnja je nepovratna.</p>
              {mode === "firm" ? (
                <p className="text-xs text-red-600">
                  Brišu se <strong>svi podaci ureda</strong> — predmeti, stranke, troškovnici,
                  računi, dokumenti i korisnički računi svih članova.
                </p>
              ) : (
                <p className="text-xs text-red-600">
                  Brišu se vaš korisnički račun i osobni podaci. Predmeti i stranke
                  ostaju uredu, bez oznake autora.
                </p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                {mode === "firm" ? (
                  <>Upišite naziv ureda <span className="font-mono font-bold text-red-600">{firmName || "…"}</span> za potvrdu</>
                ) : (
                  <>Upišite <span className="font-mono font-bold text-red-600">{ACCOUNT_CONFIRMATION}</span> za potvrdu</>
                )}
              </label>
              <input
                type="text"
                value={confirmText}
                onChange={(e) => setConfirmText(e.target.value)}
                placeholder={mode === "firm" ? firmName : ACCOUNT_CONFIRMATION}
                className="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-sm text-slate-900 placeholder:text-slate-300 focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-transparent"
                disabled={deleting}
                autoFocus
              />
            </div>

            {deleteError && <p className="text-xs text-red-600">{deleteError}</p>}

            <div className="flex items-center justify-end gap-3 pt-1">
              <button
                onClick={closeModal}
                disabled={deleting}
                className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 transition"
              >
                Odustani
              </button>
              <button
                onClick={handleDelete}
                disabled={!canSubmit || deleting}
                className="inline-flex items-center gap-2 rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50 transition"
              >
                {deleting ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Brišem...
                  </>
                ) : (
                  <>
                    <Trash2 className="h-4 w-4" />
                    {mode === "firm" ? "Obriši ured i sve podatke" : "Obriši račun"}
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
