"use client";

import { useState } from "react";
import {
  Download,
  Trash2,
  Loader2,
  Shield,
  Database,
  CheckCircle2,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { API_BASE } from "@/lib/api";

/**
 * No userId prop any more: the export runs on the backend and derives the
 * acting user from the verified token. An id passed from the client would be
 * a value the server must not trust.
 *
 * `userEmail` is display-only: it tells the user which address to send an
 * erasure request from.
 */
interface PrivacyPanelProps {
  userEmail: string;
}

export default function PrivacyPanel({ userEmail }: PrivacyPanelProps) {
  const [exporting, setExporting] = useState(false);
  const [exported, setExported] = useState(false);
  const [exportError, setExportError] = useState("");

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

      {/* ── Right to erasure (GDPR čl. 17) ──
           Handled by e-mail, not by a button. The backend endpoints still
           exist so the request can be executed manually. */}
      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex items-start gap-3">
          <Trash2 className="h-5 w-5 text-slate-400 mt-0.5 shrink-0" />
          <div className="space-y-2">
            <h4 className="text-sm font-semibold text-slate-900">Brisanje računa i podataka</h4>
            <p className="text-xs text-slate-500">
              Sukladno čl. 17. GDPR-a imate pravo zatražiti brisanje svog korisničkog
              računa i svih osobnih podataka. Zahtjev pošaljite s adrese e-pošte
              povezane s računom{userEmail ? <> (<strong>{userEmail}</strong>)</> : null} na{" "}
              <a
                href="mailto:vackadavid@gmail.com?subject=Zahtjev%20za%20brisanje%20ra%C4%8Duna"
                className="font-medium text-slate-700 underline hover:text-slate-900"
              >
                vackadavid@gmail.com
              </a>. Odgovaramo u roku od 30 dana.
            </p>
            <p className="text-xs text-slate-500">
              Predmeti, stranke i računi pripadaju <strong>uredu</strong>, a ne pojedinom
              korisniku, pa ostaju uredu i nakon brisanja vašeg računa. Ako ste jedini
              član ureda, u zahtjevu navedite želite li brisanje cijelog ureda sa svim
              podacima — to je nepovratno.
            </p>
          </div>
        </div>
      </div>

      {/* ── Legal info ── */}
      <div className="text-[11px] text-slate-400 space-y-1">
        <p>PravoTek poštuje Uredbu (EU) 2016/679 (Opća uredba o zaštiti podataka — GDPR).</p>
        <p>Vaši podaci se pohranjuju na Supabase infrastrukturi u EU regiji (Frankfurt) uz enkripciju u mirovanju (AES-256) i prijenosu (TLS 1.3).</p>
        <p>Za dodatna pitanja o privatnosti obratite se na: vackadavid@gmail.com</p>
      </div>
    </div>
  );
}
