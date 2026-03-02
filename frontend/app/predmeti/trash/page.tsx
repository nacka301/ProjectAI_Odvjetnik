"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Loader2,
  RotateCcw,
  Trash2,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import AppHeader from "@/components/AppHeader";

// ── Types ──

interface DeletedCase {
  id: string;
  created_at: string;
  broj_predmeta: string;
  naziv: string;
  vrsta: string;
  status: string;
  sud: string | null;
}

// ── Helpers ──

const VRSTA_LABELS: Record<string, string> = {
  parnica: "Parnica",
  ovrha: "Ovrha",
  savjetovanje: "Savjetovanje",
  kazneni: "Kazneni",
  upravni: "Upravni",
  ostalo: "Ostalo",
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("hr-HR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

// ── Page ──

export default function TrashPage() {
  const { userId, authHeaders } = useAuth();
  const [cases, setCases] = useState<DeletedCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [restoringId, setRestoringId] = useState<string | null>(null);

  // ── Fetch deleted cases ──
  const fetchTrash = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/api/case/trash`, {
        headers: authHeaders,
      });
      if (res.status === 403) {
        setError("Nemate ovlasti za pristup košu. Samo vlasnik ureda može pregledavati obrisane predmete.");
        setCases([]);
      } else if (res.ok) {
        setCases(await res.json());
      } else {
        setError("Greška pri dohvaćanju obrisanih predmeta.");
      }
    } catch {
      setError("Poslužitelj nije dostupan. Provjerite vezu.");
    } finally {
      setLoading(false);
    }
  }, [userId, authHeaders]);

  useEffect(() => {
    if (userId) fetchTrash();
  }, [fetchTrash, userId]);

  // ── Restore a case ──
  const handleRestore = async (caseId: string) => {
    setRestoringId(caseId);
    try {
      const res = await fetch(`${API_BASE}/api/case/${caseId}/restore`, {
        method: "POST",
        headers: authHeaders,
      });
      if (res.ok) {
        setCases((prev) => prev.filter((c) => c.id !== caseId));
      } else {
        const body = await res.json().catch(() => null);
        setError(body?.error ?? "Greška pri vraćanju predmeta.");
      }
    } catch {
      setError("Poslužitelj nije dostupan.");
    } finally {
      setRestoringId(null);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <AppHeader />

      <div className="mx-auto max-w-5xl px-4 sm:px-6 py-8">
        {/* ── Back + Title ── */}
        <div className="mb-6">
          <Link
            href="/predmeti"
            className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-700 transition mb-4"
          >
            <ArrowLeft className="h-4 w-4" />
            Natrag na predmete
          </Link>

          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-red-100">
              <Trash2 className="h-5 w-5 text-red-600" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">
                Smeće — obrisani predmeti
              </h1>
              <p className="text-sm text-slate-500">
                Predmeti koje ste obrisali. Vlasnik ureda ih može vratiti.
              </p>
            </div>
          </div>
        </div>

        {/* ── Error ── */}
        {error && (
          <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        {/* ── Loading ── */}
        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="h-7 w-7 animate-spin text-slate-400" />
          </div>
        ) : cases.length === 0 && !error ? (
          /* ── Empty state ── */
          <div className="rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center">
            <Trash2 className="mx-auto h-10 w-10 text-slate-300" />
            <p className="mt-3 text-sm font-medium text-slate-500">
              Koš je prazan
            </p>
            <p className="mt-1 text-xs text-slate-400">
              Nema obrisanih predmeta.
            </p>
          </div>
        ) : (
          /* ── Table ── */
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-100 bg-slate-50 text-xs uppercase tracking-wider text-slate-500">
                <tr>
                  <th className="px-4 py-3">Naziv</th>
                  <th className="px-4 py-3 hidden sm:table-cell">Broj</th>
                  <th className="px-4 py-3 hidden md:table-cell">Vrsta</th>
                  <th className="px-4 py-3 hidden lg:table-cell">Sud</th>
                  <th className="px-4 py-3 hidden sm:table-cell">Obrisano</th>
                  <th className="px-4 py-3 text-right">Radnja</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {cases.map((c) => (
                  <tr key={c.id} className="hover:bg-slate-50 transition">
                    <td className="px-4 py-3 font-medium text-slate-900">
                      {c.naziv || "—"}
                    </td>
                    <td className="px-4 py-3 text-slate-600 hidden sm:table-cell">
                      {c.broj_predmeta || "—"}
                    </td>
                    <td className="px-4 py-3 text-slate-600 hidden md:table-cell">
                      {VRSTA_LABELS[c.vrsta] ?? c.vrsta}
                    </td>
                    <td className="px-4 py-3 text-slate-600 hidden lg:table-cell">
                      {c.sud || "—"}
                    </td>
                    <td className="px-4 py-3 text-slate-500 hidden sm:table-cell">
                      {formatDate(c.created_at)}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => handleRestore(c.id)}
                        disabled={restoringId === c.id}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-700 transition hover:bg-emerald-100 disabled:opacity-50"
                      >
                        {restoringId === c.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <RotateCcw className="h-3.5 w-3.5" />
                        )}
                        Vrati predmet
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
