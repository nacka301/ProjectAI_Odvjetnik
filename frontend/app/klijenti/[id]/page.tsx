"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ClientDetailSkeleton } from "@/components/Skeletons";
import {
  ArrowLeft,
  Briefcase,
  Building2,
  Calculator,
  Euro,
  Info,
  Loader2,
  Mail,
  MapPin,
  Phone,
  Plus,
  Scale,
  Trash2,
  User,
  X,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import { formatBodovi } from "@/lib/format";
import { DEMO_ZASTO_NEAKTIVNO, useDemo } from "@/contexts/DemoContext";
import { demoProfil } from "@/lib/demo/izracun";
import type { ClientProfile } from "@/lib/stranke-tipovi";
import type { TarifnaStavka } from "@/lib/tarifa";

// ── Types ──

// ── Helpers ──

/**
 * Veza na predmet — obična poveznica, a u prikazu primjera samo okvir.
 *
 * Demo pokriva popis stranaka i dosje, ne i detalj predmeta. Poveznica koja bi
 * odvjetnika iz primjera odvela na 404 gora je od poveznice koje nema: izgleda
 * kao da je aplikacija pokvarena, a ne kao da primjer ondje prestaje.
 */
function VezaPredmeta({
  demo,
  href,
  className,
  children,
}: {
  demo: boolean;
  href: string;
  className?: string;
  children: React.ReactNode;
}) {
  if (demo) {
    return (
      <div className={className} title="Detalj predmeta nije dio prikaza primjera.">
        {children}
      </div>
    );
  }
  return (
    <Link href={href} className={className}>
      {children}
    </Link>
  );
}

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

function formatEur(n: number) {
  return new Intl.NumberFormat("hr-HR", {
    style: "currency",
    currency: "EUR",
    minimumFractionDigits: 2,
  }).format(n);
}

function formatDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString("hr-HR");
}

// ── Component ──

export default function ClientProfilePage() {
  const params = useParams();
  const clientId = typeof params.id === "string" ? params.id : Array.isArray(params.id) ? params.id[0] : "";

  const [profile, setProfile] = useState<ClientProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState("");
  const [activeTab, setActiveTab] = useState<"predmeti" | "troskovnici">("predmeti");
  const { userId, authHeaders } = useAuth();
  const [deletingCase, setDeletingCase] = useState<string | null>(null);

  // ── Prikaz primjera ──
  // Dosje je mjesto na kojem se vidi cijeli lanac stranka → predmet →
  // troškovnik, pa demo mora doseći i ovamo — inače popis od deset imena vodi
  // u prazno. Iznosi spremljenog demo troškovnika računaju se iz kataloga
  // Tarife, ne prepisuju: vidi lib/demo/izracun.ts.
  const { demo } = useDemo();
  const [katalog, setKatalog] = useState<TarifnaStavka[]>([]);

  useEffect(() => {
    if (!demo || !userId) return;
    fetch(`${API_BASE}/api/troskovnik/katalog`, { headers: authHeaders })
      .then((r) => (r.ok ? r.json() : []))
      .then((data: TarifnaStavka[]) => setKatalog(data))
      .catch(() => {});
    // authHeaders je nov objekt pri svakom osvježavanju tokena; userId nije.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [demo, userId]);

  // Case form
  const [showCaseForm, setShowCaseForm] = useState(false);
  const [savingCase, setSavingCase] = useState(false);
  const [caseForm, setCaseForm] = useState({
    broj_predmeta: "",
    naziv: "",
    opis: "",
    vrsta: "parnica",
    status: "aktivan",
    vrijednost_spora: "",
    sud: "",
    sudac: "",
    datum_pokretanja: "",
  });

  const fetchProfile = useCallback(async () => {
    if (!userId) return;
    if (demo) {
      setProfile(demoProfil(clientId, katalog));
      setFetchError("");
      setLoading(false);
      return;
    }
    setLoading(true);
    setFetchError("");
    try {
      const res = await fetch(`${API_BASE}/api/client/${clientId}/profile`, {
        headers: authHeaders,
      });
      if (res.ok) {
        const data = await res.json();
        setProfile(data);
      } else if (res.status === 404) {
        setProfile(null);
      } else {
        setFetchError("Gre\u0161ka pri u\u010ditavanju profila stranke.");
      }
    } catch {
      setFetchError("Poslu\u017eitelj nije dostupan. Provjerite vezu.");
    } finally {
      setLoading(false);
    }
  }, [clientId, userId, demo, katalog]);

  useEffect(() => {
    if (userId) fetchProfile();
  }, [fetchProfile, userId]);

  // Create case
  const handleCreateCase = async (e: React.FormEvent) => {
    e.preventDefault();
    if (demo) {
      setFetchError(DEMO_ZASTO_NEAKTIVNO);
      return;
    }
    if (!userId || !caseForm.naziv.trim()) return;
    setSavingCase(true);

    try {
      const body: Record<string, unknown> = {
        client_id: clientId,
        broj_predmeta: caseForm.broj_predmeta,
        naziv: caseForm.naziv,
        opis: caseForm.opis || null,
        vrsta: caseForm.vrsta,
        status: caseForm.status,
        sud: caseForm.sud || null,
        sudac: caseForm.sudac || null,
        datum_pokretanja: caseForm.datum_pokretanja || null,
      };
      if (caseForm.vrijednost_spora)
        body.vrijednost_spora = parseFloat(caseForm.vrijednost_spora);

      const res = await fetch(`${API_BASE}/api/case`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...authHeaders,
        },
        body: JSON.stringify(body),
      });

      if (res.ok) {
        setCaseForm({
          broj_predmeta: "",
          naziv: "",
          opis: "",
          vrsta: "parnica",
          status: "aktivan",
          vrijednost_spora: "",
          sud: "",
          sudac: "",
          datum_pokretanja: "",
        });
        setShowCaseForm(false);
        fetchProfile();
      } else {
        const errData = await res.json().catch(() => ({}));
        setFetchError(errData.error || "Gre\u0161ka pri kreiranju predmeta.");
      }
    } catch {
      setFetchError("Gre\u0161ka pri povezivanju s poslu\u017eiteljem.");
    } finally {
      setSavingCase(false);
    }
  };

  // Delete case
  const handleDeleteCase = async (caseId: string) => {
    if (demo) {
      setFetchError(DEMO_ZASTO_NEAKTIVNO);
      return;
    }
    if (!userId || deletingCase) return;
    if (!confirm("Obrisati predmet?")) return;
    setDeletingCase(caseId);
    try {
      const res = await fetch(`${API_BASE}/api/case/${caseId}`, {
        method: "DELETE",
        headers: authHeaders,
      });
      if (!res.ok) {
        setFetchError("Gre\u0161ka pri brisanju predmeta.");
      }
      fetchProfile();
    } catch {
      setFetchError("Gre\u0161ka pri povezivanju s poslu\u017eiteljem.");
    } finally {
      setDeletingCase(null);
    }
  };

  if (loading) {
    return <ClientDetailSkeleton />;
  }

  if (!profile) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 text-slate-500">
        <p>{fetchError || "Stranka nije prona\u0111ena."}</p>
        {fetchError && (
          <button onClick={fetchProfile} className="text-sm text-[#1B2A4A] hover:underline">
            Poku\u0161aj ponovo
          </button>
        )}
        <Link
          href="/klijenti"
          className="text-sm text-[#1B2A4A] hover:underline"
        >
          ← Natrag na stranke
        </Link>
      </div>
    );
  }

  const { client, cases, troskovnici } = profile;

  return (
    <div className="min-h-screen bg-gray-50 text-slate-900">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/80 backdrop-blur-xl">
        <div className="mx-auto flex max-w-7xl items-center gap-2 sm:gap-4 px-4 py-2.5 sm:py-3 sm:px-6">
          <Link
            href="/klijenti"
            className="flex items-center justify-center gap-2 max-sm:min-h-12 max-sm:min-w-12 max-sm:-ml-2 shrink-0 text-sm text-slate-500 transition hover:text-slate-900"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Stranke</span>
          </Link>
          <div className="flex items-center gap-2 min-w-0">
            <div
              className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${
                client.tip === "pravna"
                  ? "bg-blue-50 text-blue-700"
                  : "bg-[#1B2A4A]/10 text-[#1B2A4A]"
              }`}
            >
              {client.tip === "pravna" ? (
                <Building2 className="h-4 w-4" />
              ) : (
                <User className="h-4 w-4" />
              )}
            </div>
            <h1 className="font-[family-name:var(--font-playfair)] text-base sm:text-lg font-semibold text-[#1B2A4A] truncate">{client.ime_naziv}</h1>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        {/* Error banner */}
        {fetchError && (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
            {fetchError}
            <button onClick={() => setFetchError("")} className="ml-auto text-xs font-medium text-red-800 underline">Zatvori</button>
          </div>
        )}

        {/* Client info card */}
        <div className="mb-6 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {client.oib && (
              <div>
                <div className="text-xs text-slate-500">OIB</div>
                <div className="mt-0.5 text-sm font-medium text-slate-900">
                  {client.oib}
                </div>
              </div>
            )}
            {(client.adresa || client.grad) && (
              <div>
                <div className="text-xs text-slate-500">Adresa</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-900">
                  <MapPin className="h-3.5 w-3.5 text-slate-400" />
                  {client.adresa && `${client.adresa}, `}
                  {client.grad}
                  {client.postanski_broj && ` ${client.postanski_broj}`}
                </div>
              </div>
            )}
            {client.email && (
              <div>
                <div className="text-xs text-slate-500">E-mail</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-900">
                  <Mail className="h-3.5 w-3.5 text-slate-400" />
                  {client.email}
                </div>
              </div>
            )}
            {client.telefon && (
              <div>
                <div className="text-xs text-slate-500">Telefon</div>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-900">
                  <Phone className="h-3.5 w-3.5 text-slate-400" />
                  {client.telefon}
                </div>
              </div>
            )}
          </div>
          {client.napomena && (
            <div className="mt-4 border-t border-slate-100 pt-3">
              <div className="text-xs text-slate-500">Napomena</div>
              <div className="mt-0.5 text-sm text-slate-600">
                {client.napomena}
              </div>
            </div>
          )}
          <div className="mt-4 flex items-center gap-4 border-t border-slate-100 pt-3 text-xs text-slate-400">
            <span>Tip: {client.tip === "pravna" ? "Pravna osoba" : "Fizička osoba"}</span>
            <span>Kreirano: {formatDate(client.created_at)}</span>
          </div>
        </div>

        {/* Stats */}
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Predmeti</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {cases.length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Aktivni predmeti</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {cases.filter((c) => c.status === "aktivan").length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Troškovnici</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {troskovnici.length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Ukupno naplaćeno</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {formatEur(
                troskovnici.reduce((sum, t) => sum + t.ukupno_eur_s_pdv, 0)
              )}
            </div>
          </div>
        </div>

        {/* U prikazu primjera se ovamo ulazi iz znatiželje, izvan samog vodiča —
            koraci vodiča ne dolaze do ove stranice. Korak 7/8 obećava da se
            troškovnik sprema i kasnije dopunjuje; taj se lanac ovdje stvarno
            vidi (stranka → predmet → troškovnik), pa se ista poruka ponavlja
            na mjestu gdje odvjetnik zapravo gleda rezultat, ne samo u vodiču. */}
        {demo && (
          <div className="mb-4 flex items-start gap-2 rounded-lg border border-indigo-100 bg-indigo-50 px-3 py-2 text-xs text-indigo-900">
            <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-indigo-500" />
            <p>
              Primjer dosjea: stranku i predmet upisujete jednom, a troškovnik
              kasnije otvorite, dopunite idućom radnjom i ponovno spremite.
            </p>
          </div>
        )}

        {/* Tabs */}
        <div className="mb-4 flex gap-1 rounded-lg border border-slate-200 bg-white p-1 shadow-sm">
          <button
            onClick={() => setActiveTab("predmeti")}
            className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition ${
              activeTab === "predmeti"
                ? "bg-[#1B2A4A] text-white shadow"
                : "text-slate-500 hover:text-slate-900"
            }`}
          >
            <Briefcase className="h-4 w-4" />
            Predmeti ({cases.length})
          </button>
          <button
            onClick={() => setActiveTab("troskovnici")}
            className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition ${
              activeTab === "troskovnici"
                ? "bg-[#1B2A4A] text-white shadow"
                : "text-slate-500 hover:text-slate-900"
            }`}
          >
            <Calculator className="h-4 w-4" />
            Troškovnici ({troskovnici.length})
          </button>
        </div>

        {/* Tab content: Predmeti */}
        {activeTab === "predmeti" && (
          <div>
            <div className="mb-4 flex justify-end">
              <button
                onClick={() => setShowCaseForm(!showCaseForm)}
                disabled={demo}
                title={demo ? DEMO_ZASTO_NEAKTIVNO : undefined}
                className="flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 active:scale-[0.98] disabled:bg-slate-300 disabled:cursor-not-allowed"
              >
                {showCaseForm ? (
                  <X className="h-4 w-4" />
                ) : (
                  <Plus className="h-4 w-4" />
                )}
                {showCaseForm ? "Zatvori" : "Novi predmet"}
              </button>
            </div>

            {/* New case form */}
            {showCaseForm && (
              <form
                onSubmit={handleCreateCase}
                className="mb-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
              >
                <h3 className="mb-4 text-sm font-semibold text-[#1B2A4A]">
                  Novi predmet
                </h3>
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Naziv predmeta *
                    </label>
                    <input
                      required
                      value={caseForm.naziv}
                      onChange={(e) =>
                        setCaseForm({ ...caseForm, naziv: e.target.value })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                      placeholder="Horvat vs. Kovač – nekretnina"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Broj predmeta
                    </label>
                    <input
                      value={caseForm.broj_predmeta}
                      onChange={(e) =>
                        setCaseForm({ ...caseForm, broj_predmeta: e.target.value })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                      placeholder="P-123/2026"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Vrsta
                    </label>
                    <select
                      value={caseForm.vrsta}
                      onChange={(e) =>
                        setCaseForm({ ...caseForm, vrsta: e.target.value })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    >
                      <option value="parnica">Parnica</option>
                      <option value="ovrha">Ovrha</option>
                      <option value="savjetovanje">Savjetovanje</option>
                      <option value="kazneni">Kazneni</option>
                      <option value="upravni">Upravni</option>
                      <option value="ostalo">Ostalo</option>
                    </select>
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Vrijednost spora (EUR)
                    </label>
                    <input
                      type="number"
                      value={caseForm.vrijednost_spora}
                      onChange={(e) =>
                        setCaseForm({
                          ...caseForm,
                          vrijednost_spora: e.target.value,
                        })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                      placeholder="50000"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Sud
                    </label>
                    <input
                      value={caseForm.sud}
                      onChange={(e) =>
                        setCaseForm({ ...caseForm, sud: e.target.value })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                      placeholder="Općinski sud u Zagrebu"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Datum pokretanja
                    </label>
                    <input
                      type="date"
                      value={caseForm.datum_pokretanja}
                      onChange={(e) =>
                        setCaseForm({
                          ...caseForm,
                          datum_pokretanja: e.target.value,
                        })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    />
                  </div>
                  <div className="sm:col-span-2 lg:col-span-3">
                    <label className="mb-1 block text-xs text-slate-400">
                      Opis
                    </label>
                    <textarea
                      value={caseForm.opis}
                      onChange={(e) =>
                        setCaseForm({ ...caseForm, opis: e.target.value })
                      }
                      rows={2}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                      placeholder="Opis predmeta…"
                    />
                  </div>
                </div>
                <div className="mt-4 flex justify-end">
                  <button
                    type="submit"
                    disabled={savingCase || !caseForm.naziv.trim()}
                    className="flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-6 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 disabled:opacity-50 active:scale-[0.98]"
                  >
                    {savingCase ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Plus className="h-4 w-4" />
                    )}
                    Spremi predmet
                  </button>
                </div>
              </form>
            )}

            {/* Cases list */}
            {cases.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-slate-500">
                <Briefcase className="mb-3 h-10 w-10 text-slate-300" />
                <p className="text-sm">Nema predmeta za ovu stranku.</p>
              </div>
            ) : (
              <div className="space-y-3">
                {cases.map((c) => (
                  <div
                    key={c.id}
                    className="group flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition hover:border-[#1B2A4A]/40 hover:shadow-md"
                  >
                    <VezaPredmeta
                      demo={demo}
                      href={`/predmeti/${c.id}`}
                      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#1B2A4A]/10 transition hover:bg-[#1B2A4A]/20"
                    >
                      <Scale className="h-5 w-5 text-[#1B2A4A]" />
                    </VezaPredmeta>
                    <VezaPredmeta demo={demo} href={`/predmeti/${c.id}`} className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <h4 className="truncate text-sm font-semibold text-slate-900">
                          {c.naziv}
                        </h4>
                        <span
                          className={`rounded-full border px-2 py-0.5 text-[10px] font-medium ${
                            STATUS_COLORS[c.status] ||
                            STATUS_COLORS.aktivan
                          }`}
                        >
                          {c.status.charAt(0).toUpperCase() + c.status.slice(1)}
                        </span>
                      </div>
                      <div className="mt-1 flex flex-wrap items-center gap-3 text-xs text-slate-500">
                        {c.broj_predmeta && <span>{c.broj_predmeta}</span>}
                        <span>{VRSTA_LABELS[c.vrsta] || c.vrsta}</span>
                        {c.vrijednost_spora != null && (
                          <span className="flex items-center gap-1">
                            <Euro className="h-3 w-3" />
                            {formatEur(c.vrijednost_spora)}
                          </span>
                        )}
                        {c.sud && <span>{c.sud}</span>}
                        {c.datum_pokretanja && (
                          <span>{formatDate(c.datum_pokretanja)}</span>
                        )}
                      </div>
                    </VezaPredmeta>
                    {!demo && (
                      <button
                        onClick={() => handleDeleteCase(c.id)}
                        className="rounded-lg p-2 text-slate-300 opacity-0 transition hover:bg-red-50 hover:text-red-500 group-hover:opacity-100"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Tab content: Troškovnici */}
        {activeTab === "troskovnici" && (
          <div>
            {troskovnici.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-slate-500">
                <Calculator className="mb-3 h-10 w-10 text-slate-300" />
                <p className="text-sm">Nema troškovnika za ovu stranku.</p>
                <Link
                  href={`/troskovnik?clientId=${client.id}`}
                  className="mt-2 text-xs text-[#1B2A4A] hover:underline"
                >
                  Izradi novi troškovnik →
                </Link>
              </div>
            ) : (
              <div className="space-y-3">
                {troskovnici.map((t) => (
                  <div
                    key={t.id}
                    className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#1B2A4A]/10">
                          <Calculator className="h-5 w-5 text-[#1B2A4A]" />
                        </div>
                        <div>
                          <h4 className="text-sm font-semibold text-slate-900">
                            {t.opis_predmeta || "Troškovnik"}
                          </h4>
                          <p className="text-xs text-slate-500">
                            {formatDate(t.created_at)} · VPS:{" "}
                            {formatEur(t.vrijednost_spora)}
                          </p>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-lg font-bold text-[#1B2A4A]">
                          {formatEur(t.ukupno_eur_s_pdv)}
                        </div>
                        <div className="text-xs text-slate-500">
                          {formatBodovi(t.ukupno_bodova)} bod. · neto{" "}
                          {formatEur(t.ukupno_eur_bez_pdv)}
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
