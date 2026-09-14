"use client";

import { useEffect, useState, useCallback } from "react";
import {
  Loader2,
  AlertTriangle,
  Trash2,
  X,
  Building2,
  User,
  Users,
} from "lucide-react";
import { API_BASE } from "@/lib/api";

/**
 * Admin-brisanje jednog korisnika: samo član ili cijeli ured.
 *
 * Zašto modal, a ne inline kao SubscriptionEditor: brisanje je nepovratno i
 * širokog dosega, pa zaslužuje vlastiti korak — pretpregled opsega prije
 * potvrde i prepisivanje (naziv ureda / e-mail) da radnja bude namjerna.
 *
 * Sve teško (redoslijed, Storage, pozivnice, waitlist, cache) radi backend
 * (AccountService.AdminDeleteUserAsync). Ovdje je samo potvrda i prikaz.
 */

type Mode = "member" | "firm";

interface DeletionPreview {
  user_id: string;
  email: string | null;
  role: string;
  law_firm_id: string | null;
  firm_name: string | null;
  member_count: number;
  member_emails: string[];
  clients: number;
  cases: number;
  invoices: number;
}

interface Props {
  userId: string;
  /** Fallback e-mail dok se pretpregled učitava. */
  email: string | null;
  authHeaders: Record<string, string>;
  onClose: () => void;
  onDeleted: () => void;
}

export default function DeleteUserDialog({
  userId,
  email,
  authHeaders,
  onClose,
  onDeleted,
}: Props) {
  const [preview, setPreview] = useState<DeletionPreview | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [mode, setMode] = useState<Mode>("firm");
  const [typed, setTyped] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadPreview = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const res = await fetch(
        `${API_BASE}/api/admin/users/${userId}/deletion-preview`,
        { headers: { ...authHeaders, "Content-Type": "application/json" } },
      );
      if (!res.ok) {
        setLoadError(`Pretpregled nije dostupan (${res.status}).`);
        return;
      }
      const data: DeletionPreview = await res.json();
      setPreview(data);
      // Bez ureda → jedino „član" ima smisla. Solo ured → „ured" (inače
      // brisanjem samo člana ured ostaje kao siroče). Više članova → „član",
      // jer je admin kliknuo na konkretnu osobu.
      setMode(!data.law_firm_id ? "member" : data.member_count <= 1 ? "firm" : "member");
    } catch {
      setLoadError("Mrežna greška pri dohvaćanju pretpregleda.");
    } finally {
      setLoading(false);
    }
  }, [userId, authHeaders]);

  useEffect(() => {
    loadPreview();
  }, [loadPreview]);

  // Očekivana potvrda: naziv ureda (firm) ili e-mail (member).
  const expected =
    mode === "firm"
      ? (preview?.firm_name ?? "").trim()
      : (preview?.email ?? email ?? "").trim();

  const confirmMatches =
    expected.length > 0 &&
    (mode === "firm"
      ? typed.trim() === expected
      : typed.trim().toLowerCase() === expected.toLowerCase());

  const noFirm = !preview?.law_firm_id;
  const soloMemberWarning =
    mode === "member" && (preview?.member_count ?? 0) <= 1;

  async function handleDelete() {
    if (deleting || !confirmMatches) return;
    setDeleting(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/admin/users/${userId}`, {
        method: "DELETE",
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({ mode, confirmation: typed.trim() }),
      });

      if (res.ok) {
        onDeleted();
        onClose();
        return;
      }

      const body = await res.json().catch(() => ({}));
      setError(body.error || `Greška ${res.status}.`);
    } catch {
      setError("Mrežna greška — pokušajte ponovno.");
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-xl bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-red-100">
              <Trash2 className="h-4 w-4 text-red-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-900">Obriši korisnika</h2>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 transition hover:text-slate-700"
            aria-label="Zatvori"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4 px-5 py-4">
          {loading ? (
            <div className="flex items-center gap-2 py-6 text-sm text-slate-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Učitavanje pretpregleda…
            </div>
          ) : loadError ? (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              {loadError}{" "}
              <button onClick={loadPreview} className="font-semibold underline">
                Pokušaj ponovno
              </button>
            </div>
          ) : (
            <>
              {/* Pretpregled opsega */}
              <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-xs text-slate-700">
                <p className="mb-2 flex items-center gap-1.5 font-semibold text-slate-800">
                  <Building2 className="h-3.5 w-3.5" />
                  {preview?.firm_name || "Račun bez ureda"}
                </p>
                <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                  <span>E-mail: <span className="font-medium">{preview?.email || email || "—"}</span></span>
                  <span>Uloga: <span className="font-medium">{preview?.role || "—"}</span></span>
                  <span>Članova ureda: <span className="font-medium">{preview?.member_count ?? 0}</span></span>
                  <span>Stranaka: <span className="font-medium">{preview?.clients ?? 0}</span></span>
                  <span>Predmeta: <span className="font-medium">{preview?.cases ?? 0}</span></span>
                  <span>Računa: <span className="font-medium">{preview?.invoices ?? 0}</span></span>
                </div>
                {(preview?.member_count ?? 0) > 1 && preview?.member_emails?.length ? (
                  <p className="mt-2 flex items-start gap-1.5 text-[11px] text-slate-500">
                    <Users className="mt-0.5 h-3 w-3 shrink-0" />
                    <span>{preview.member_emails.join(", ")}</span>
                  </p>
                ) : null}
              </div>

              {/* Izbor načina */}
              <div className="space-y-2">
                <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                  Način brisanja
                </p>
                <label className="flex cursor-pointer items-start gap-2 rounded-lg border border-slate-200 p-2.5 hover:bg-slate-50">
                  <input
                    type="radio"
                    name="delete-mode"
                    checked={mode === "member"}
                    onChange={() => { setMode("member"); setTyped(""); }}
                    className="mt-0.5"
                  />
                  <span className="text-xs">
                    <span className="flex items-center gap-1 font-medium text-slate-800">
                      <User className="h-3 w-3" /> Briši samo ovog člana
                    </span>
                    <span className="text-slate-500">
                      Ured i ostali članovi ostaju. Oslobađa e-mail za ponovnu registraciju.
                    </span>
                  </span>
                </label>
                <label
                  className={`flex items-start gap-2 rounded-lg border border-slate-200 p-2.5 ${
                    noFirm ? "cursor-not-allowed opacity-50" : "cursor-pointer hover:bg-slate-50"
                  }`}
                >
                  <input
                    type="radio"
                    name="delete-mode"
                    checked={mode === "firm"}
                    disabled={noFirm}
                    onChange={() => { setMode("firm"); setTyped(""); }}
                    className="mt-0.5"
                  />
                  <span className="text-xs">
                    <span className="flex items-center gap-1 font-medium text-slate-800">
                      <Building2 className="h-3 w-3" /> Briši cijeli ured
                    </span>
                    <span className="text-slate-500">
                      Nepovratno briše sve članove, predmete, stranke, račune i datoteke ureda.
                    </span>
                  </span>
                </label>
              </div>

              {soloMemberWarning && (
                <div className="flex items-start gap-1.5 rounded-lg border border-amber-200 bg-amber-50 p-2.5 text-[11px] text-amber-800">
                  <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                  <span>
                    Ovaj korisnik je jedini član — brisanjem samo člana ured ostaje bez ikoga
                    (siroče). Za čisto stanje preporuka je <strong>briši cijeli ured</strong>.
                  </span>
                </div>
              )}

              {/* Prepisivanje potvrde */}
              <div className="space-y-1">
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                  {mode === "firm"
                    ? "Za potvrdu prepišite naziv ureda"
                    : "Za potvrdu prepišite e-mail korisnika"}
                </label>
                <input
                  type="text"
                  value={typed}
                  onChange={(e) => setTyped(e.target.value)}
                  placeholder={expected || "—"}
                  autoComplete="off"
                  className="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-800 outline-none focus:border-red-400 focus:ring-2 focus:ring-red-100"
                />
              </div>

              {error && (
                <p className="flex items-center gap-1.5 text-xs font-medium text-red-700">
                  <AlertTriangle className="h-3 w-3" /> {error}
                </p>
              )}
            </>
          )}
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-2 border-t border-slate-200 px-5 py-3">
          <button
            onClick={onClose}
            className="rounded-lg px-3.5 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-100"
          >
            Odustani
          </button>
          <button
            onClick={handleDelete}
            disabled={deleting || loading || !confirmMatches}
            className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-3.5 py-1.5 text-xs font-semibold text-white transition hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {deleting ? <Loader2 className="h-3 w-3 animate-spin" /> : <Trash2 className="h-3 w-3" />}
            {mode === "firm" ? "Obriši ured" : "Obriši člana"}
          </button>
        </div>
      </div>
    </div>
  );
}
