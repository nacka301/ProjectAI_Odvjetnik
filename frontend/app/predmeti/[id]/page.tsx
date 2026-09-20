"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ArrowLeft,
  BookOpen,
  Briefcase,
  Calendar,
  Euro,
  Loader2,
  MapPin,
  Scale,
  User,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import CaseEventTimeline from "@/components/CaseEventTimeline";

// ── Types ──

interface CaseDetail {
  id: string;
  created_at: string;
  user_id: string;
  client_id: string | null;
  broj_predmeta: string;
  naziv: string;
  opis: string | null;
  vrsta: string;
  status: string;
  vrijednost_spora: number | null;
  sud: string | null;
  sudac: string | null;
  datum_pokretanja: string | null;
  datum_zatvaranja: string | null;
}

// ── Helpers ──

const STATUS_COLORS: Record<string, string> = {
  aktivan: "text-emerald-700 bg-emerald-50 border-emerald-200",
  zatvoren: "text-slate-600 bg-slate-50 border-slate-200",
  mirovanje: "text-amber-700 bg-amber-50 border-amber-200",
  arhiviran: "text-slate-500 bg-slate-50 border-slate-200",
};

const VRSTA_LABELS: Record<string, string> = {
  parnica: "Parnica",
  ovrha: "Ovrha",
  savjetovanje: "Savjetovanje",
  kazneni: "Kazneni",
  upravni: "Upravni",
  ostalo: "Ostalo",
};

function formatDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString("hr-HR");
}

function formatEur(n: number) {
  return new Intl.NumberFormat("hr-HR", {
    style: "currency",
    currency: "EUR",
    minimumFractionDigits: 2,
  }).format(n);
}

// ── Page ──

export default function CaseDetailPage() {
  const params = useParams();
  const caseId =
    typeof params.id === "string"
      ? params.id
      : Array.isArray(params.id)
        ? params.id[0]
        : "";

  const { userId, authHeaders } = useAuth();
  const [caseData, setCaseData] = useState<CaseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState<"upisnik" | "detalji">("upisnik");

  const fetchCase = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/api/case/${caseId}`, {
        headers: authHeaders,
      });
      if (res.ok) {
        setCaseData(await res.json());
      } else if (res.status === 404) {
        setCaseData(null);
      } else {
        setError("Greška pri učitavanju predmeta.");
      }
    } catch {
      setError("Poslužitelj nije dostupan. Provjerite vezu.");
    } finally {
      setLoading(false);
    }
  }, [caseId, userId, authHeaders]);

  useEffect(() => {
    if (userId) fetchCase();
  }, [fetchCase, userId]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-50">
        <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
      </div>
    );
  }

  if (!caseData) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 text-slate-500">
        <p>{error || "Predmet nije pronađen."}</p>
        <Link
          href="/klijenti"
          className="text-sm text-[#1B2A4A] hover:underline"
        >
          ← Natrag na stranke
        </Link>
      </div>
    );
  }

  const statusColor = STATUS_COLORS[caseData.status] || STATUS_COLORS.aktivan;

  return (
    <div className="min-h-screen bg-gray-50 text-slate-900">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/80 backdrop-blur-xl">
        <div className="mx-auto flex max-w-7xl items-center gap-2 sm:gap-4 px-4 py-2.5 sm:py-3 sm:px-6">
          {caseData.client_id ? (
            <Link
              href={`/klijenti/${caseData.client_id}`}
              className="flex items-center justify-center gap-2 max-sm:min-h-12 max-sm:min-w-12 max-sm:-ml-2 shrink-0 text-sm text-slate-500 transition hover:text-slate-900"
            >
              <ArrowLeft className="h-4 w-4" />
              <span className="hidden sm:inline">Stranka</span>
            </Link>
          ) : (
            <Link
              href="/klijenti"
              className="flex items-center justify-center gap-2 max-sm:min-h-12 max-sm:min-w-12 max-sm:-ml-2 shrink-0 text-sm text-slate-500 transition hover:text-slate-900"
            >
              <ArrowLeft className="h-4 w-4" />
              <span className="hidden sm:inline">Stranke</span>
            </Link>
          )}
          <div className="flex items-center gap-2 min-w-0">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[#1B2A4A]/10 text-[#1B2A4A]">
              <Scale className="h-4 w-4" />
            </div>
            <div className="min-w-0">
              <h1 className="font-[family-name:var(--font-playfair)] text-base sm:text-lg font-semibold text-[#1B2A4A] truncate">
                {caseData.naziv}
              </h1>
              {caseData.broj_predmeta && (
                <p className="hidden sm:block text-xs text-slate-400 truncate">{caseData.broj_predmeta}</p>
              )}
            </div>
          </div>
          <span
            className={`ml-auto shrink-0 rounded-full border px-2.5 py-0.5 text-xs font-medium ${statusColor}`}
          >
            {caseData.status.charAt(0).toUpperCase() + caseData.status.slice(1)}
          </span>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        {/* Error banner */}
        {error && (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
            {error}
            <button onClick={() => setError("")} className="ml-auto text-xs font-medium text-red-800 underline">
              Zatvori
            </button>
          </div>
        )}

        {/* Case info card */}
        <div className="mb-6 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-xs text-slate-500">Vrsta</div>
              <div className="mt-0.5 text-sm font-medium text-slate-900">
                {VRSTA_LABELS[caseData.vrsta] || caseData.vrsta}
              </div>
            </div>
            {caseData.vrijednost_spora != null && (
              <div>
                <div className="text-xs text-slate-500">Vrijednost predmeta spora</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm font-medium text-slate-900">
                  <Euro className="h-3.5 w-3.5 text-slate-400" />
                  {formatEur(caseData.vrijednost_spora)}
                </div>
              </div>
            )}
            {caseData.sud && (
              <div>
                <div className="text-xs text-slate-500">Sud</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-900">
                  <MapPin className="h-3.5 w-3.5 text-slate-400" />
                  {caseData.sud}
                </div>
              </div>
            )}
            {caseData.sudac && (
              <div>
                <div className="text-xs text-slate-500">Sudac</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-900">
                  <User className="h-3.5 w-3.5 text-slate-400" />
                  {caseData.sudac}
                </div>
              </div>
            )}
          </div>
          <div className="mt-4 flex items-center gap-4 border-t border-slate-100 pt-3 text-xs text-slate-400">
            {caseData.datum_pokretanja && (
              <span className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                Pokrenuto: {formatDate(caseData.datum_pokretanja)}
              </span>
            )}
            {caseData.datum_zatvaranja && (
              <span>Zatvoreno: {formatDate(caseData.datum_zatvaranja)}</span>
            )}
            <span>Kreirano: {formatDate(caseData.created_at)}</span>
          </div>
          {caseData.opis && (
            <div className="mt-3 border-t border-slate-100 pt-3">
              <div className="text-xs text-slate-500">Opis</div>
              <div className="mt-0.5 text-sm text-slate-600">{caseData.opis}</div>
            </div>
          )}
        </div>

        {/* Tabs */}
        <div className="mb-4 flex gap-1 rounded-lg border border-slate-200 bg-white p-1 shadow-sm">
          <button
            onClick={() => setActiveTab("upisnik")}
            className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition ${
              activeTab === "upisnik"
                ? "bg-[#1B2A4A] text-white shadow"
                : "text-slate-500 hover:text-slate-900"
            }`}
          >
            <BookOpen className="h-4 w-4" />
            Upisnik
          </button>
          <button
            onClick={() => setActiveTab("detalji")}
            className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition ${
              activeTab === "detalji"
                ? "bg-[#1B2A4A] text-white shadow"
                : "text-slate-500 hover:text-slate-900"
            }`}
          >
            <Briefcase className="h-4 w-4" />
            Detalji
          </button>
        </div>

        {/* Tab content */}
        {activeTab === "upisnik" && <CaseEventTimeline caseId={caseId} />}

        {activeTab === "detalji" && (
          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="mb-4 text-sm font-semibold text-[#1B2A4A]">
              Svi podaci predmeta
            </h3>
            <dl className="grid gap-4 sm:grid-cols-2">
              <div>
                <dt className="text-xs text-slate-500">ID</dt>
                <dd className="mt-0.5 font-mono text-xs text-slate-700">{caseData.id}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Broj predmeta</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {caseData.broj_predmeta || "—"}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Naziv</dt>
                <dd className="mt-0.5 text-sm text-slate-700">{caseData.naziv}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Vrsta</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {VRSTA_LABELS[caseData.vrsta] || caseData.vrsta}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Status</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {caseData.status.charAt(0).toUpperCase() + caseData.status.slice(1)}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">VPS (EUR)</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {caseData.vrijednost_spora != null
                    ? formatEur(caseData.vrijednost_spora)
                    : "—"}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Sud</dt>
                <dd className="mt-0.5 text-sm text-slate-700">{caseData.sud || "—"}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Sudac</dt>
                <dd className="mt-0.5 text-sm text-slate-700">{caseData.sudac || "—"}</dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Datum pokretanja</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {formatDate(caseData.datum_pokretanja)}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-slate-500">Datum zatvaranja</dt>
                <dd className="mt-0.5 text-sm text-slate-700">
                  {formatDate(caseData.datum_zatvaranja)}
                </dd>
              </div>
              {caseData.opis && (
                <div className="sm:col-span-2">
                  <dt className="text-xs text-slate-500">Opis</dt>
                  <dd className="mt-0.5 text-sm text-slate-700">{caseData.opis}</dd>
                </div>
              )}
            </dl>
          </div>
        )}
      </main>
    </div>
  );
}
