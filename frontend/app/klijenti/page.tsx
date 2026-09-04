"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ClientListSkeleton } from "@/components/Skeletons";
import {
  ArrowLeft,
  Building2,
  ChevronRight,
  Loader2,
  Mail,
  MapPin,
  Phone,
  Plus,
  Search,
  Trash2,
  User,
  Users,
  X,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";
import { highlightClasses } from "@/components/Vodic";
import { useVodic } from "@/contexts/VodicContext";
import { DEMO_ZASTO_NEAKTIVNO, useDemo } from "@/contexts/DemoContext";
import { DEMO_STRANKE } from "@/lib/demo/podaci";
import type { Client } from "@/lib/stranke-tipovi";

// ── Types ──

// ── Helpers ──

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "upravo";
  if (mins < 60) return `prije ${mins} min`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `prije ${hrs}h`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `prije ${days}d`;
  return new Date(iso).toLocaleDateString("hr-HR");
}

// ── Component ──

/**
 * Suspense granica ostaje oko sadržaja: popis stranaka je klijentski i ovisi o
 * tokenu koji stiže asinkrono, pa prerender bez nje pokazuje prazan ekran.
 */
export default function KlijentiPage() {
  return (
    <Suspense fallback={<div className="flex min-h-screen items-center justify-center bg-slate-50"><Loader2 className="h-8 w-8 animate-spin text-[#1B2A4A]" /></div>}>
      <KlijentiSadrzaj />
    </Suspense>
  );
}

function KlijentiSadrzaj() {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [fetchError, setFetchError] = useState("");
  const { userId, authHeaders } = useAuth();

  // ── Vodič ──
  // Vodič živi iznad ruta (`VodicProvider`) jer uvodni vodič ovamo dolazi kao
  // svoj osmi korak, a ne kao zasebna dionica koja se dogovara preko `?vodic=1`.
  // Odavde se čita samo koje polje treba prstenovati.
  const { target: vodicTarget } = useVodic();
  const vodicHighlight = useCallback(
    (id: string) => highlightClasses(vodicTarget === id),
    [vodicTarget],
  );
  // Koraci 1/3 i 2/3 vodiča za Stranke ciljaju elemente u zaglavlju. Zaglavlje je `sticky` +
  // `backdrop-blur` — oboje stvara vlastiti stacking context, pa `z-[210]` s
  // istaknutog elementa ne može prijeći preko zatamnjenja vodiča (`z-[200]`):
  // ostaje zarobljen na zaglavljevom `z-30` i cilj se ne „probije" kao svjetliji.
  // Dok korak cilja element u zaglavlju, dižemo cijelo zaglavlje iznad zatamnjenja
  // da istaknuti gumb/pretraga izađu na svjetlo (tijelo stranice ostaje zatamnjeno).
  const vodicCiljaZaglavlje =
    vodicTarget === "vodic-pretraga" || vodicTarget === "vodic-nova-stranka";

  // ── Prikaz primjera ──
  const { demo } = useDemo();

  // Form state
  const [form, setForm] = useState({
    ime_naziv: "",
    oib: "",
    adresa: "",
    grad: "",
    postanski_broj: "",
    email: "",
    telefon: "",
    tip: "fizicka",
    napomena: "",
  });

  // Fetch clients
  const fetchClients = useCallback(async () => {
    if (!userId) return;
    // Prikaz primjera ne dodiruje bazu ni u jednom smjeru: deset stranaka dolazi
    // iz lokalne datoteke, a korisnikov stvarni popis se niti ne dohvaća.
    if (demo) {
      setClients(DEMO_STRANKE);
      setFetchError("");
      setLoading(false);
      return;
    }
    setLoading(true);
    setFetchError("");
    try {
      const res = await fetch(`${API_BASE}/api/client`, {
        headers: authHeaders,
      });
      if (res.ok) {
        const data = await res.json();
        setClients(data);
      } else {
        setFetchError("Gre\u0161ka pri dohva\u0107anju stranaka.");
      }
    } catch {
      setFetchError("Poslu\u017eitelj nije dostupan. Provjerite vezu.");
    } finally {
      setLoading(false);
    }
  }, [userId, demo]);

  useEffect(() => {
    fetchClients();
  }, [fetchClients]);

  // Create client
  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (demo) {
      setError(DEMO_ZASTO_NEAKTIVNO);
      return;
    }
    if (!userId || !form.ime_naziv.trim()) return;
    setSaving(true);
    setError("");

    try {
      const res = await fetch(`${API_BASE}/api/client`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...authHeaders,
        },
        body: JSON.stringify(form),
      });

      if (!res.ok) {
        const data = await res.json();
        setError(data.error || "Greška pri spremanju.");
        return;
      }

      setForm({
        ime_naziv: "",
        oib: "",
        adresa: "",
        grad: "",
        postanski_broj: "",
        email: "",
        telefon: "",
        tip: "fizicka",
        napomena: "",
      });
      setShowForm(false);
      fetchClients();
    } catch {
      setError("Greška pri povezivanju s poslužiteljem.");
    } finally {
      setSaving(false);
    }
  };

  // Delete client
  const handleDelete = async (id: string) => {
    if (demo) {
      setError(DEMO_ZASTO_NEAKTIVNO);
      return;
    }
    if (!userId || deleting) return;
    if (!confirm("Jeste li sigurni da želite obrisati ovu stranku?")) return;
    setDeleting(id);
    try {
      const res = await fetch(`${API_BASE}/api/client/${id}`, {
        method: "DELETE",
        headers: authHeaders,
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error || "Greška pri brisanju.");
        return;
      }
      fetchClients();
    } catch {
      setError("Greška pri povezivanju s poslužiteljem.");
    } finally {
      setDeleting(null);
    }
  };

  // Filter
  const filtered = clients.filter(
    (c) =>
      c.ime_naziv.toLowerCase().includes(search.toLowerCase()) ||
      (c.oib && c.oib.includes(search)) ||
      (c.email && c.email.toLowerCase().includes(search.toLowerCase()))
  );

  return (
    <div className="min-h-screen bg-gray-50 text-slate-900">
      {/* Header */}
      <header
        className={`sticky top-0 border-b border-slate-200 bg-white/80 backdrop-blur-xl ${
          vodicCiljaZaglavlje ? "z-[210]" : "z-30"
        }`}
      >
        <div className="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3 sm:px-6">
          <Link
            href="/"
            className="flex items-center gap-2 text-sm text-slate-500 transition hover:text-slate-900"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Početna</span>
          </Link>
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-[#1B2A4A]/10">
              <Users className="h-4 w-4 text-[#1B2A4A]" />
            </div>
            <h1 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-[#1B2A4A]">Stranke</h1>
          </div>
          <div className="ml-auto flex items-center gap-3">
            <div data-vodic="vodic-pretraga" className={`relative ${vodicHighlight("vodic-pretraga")}`}>
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                placeholder="Pretraži stranke…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-48 rounded-lg border border-slate-200 bg-white py-2 pl-10 pr-3 text-sm text-slate-900 placeholder-slate-400 transition focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30 sm:w-64"
              />
            </div>
            <button
              data-vodic="vodic-nova-stranka"
              onClick={() => setShowForm(!showForm)}
              disabled={demo}
              title={demo ? DEMO_ZASTO_NEAKTIVNO : undefined}
              className={`flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 active:scale-[0.98] disabled:bg-slate-300 disabled:cursor-not-allowed ${vodicHighlight("vodic-nova-stranka")}`}
            >
              {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {showForm ? "Zatvori" : "Nova stranka"}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        {/* New Client Form */}
        {showForm && (
          <form
            onSubmit={handleCreate}
            className="mb-6 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
          >
            <h2 className="mb-4 text-base font-semibold text-[#1B2A4A]">
              Nova stranka
            </h2>
            {error && (
              <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
                {error}
              </div>
            )}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <label className="mb-1 block text-xs text-slate-500">
                  Ime i prezime / Naziv *
                </label>
                <input
                  required
                  value={form.ime_naziv}
                  onChange={(e) => setForm({ ...form, ime_naziv: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="Ivan Horvat / Primjer d.o.o."
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">OIB (11 znamenki)</label>
                <input
                  value={form.oib}
                  onChange={(e) => setForm({ ...form, oib: e.target.value.replace(/\D/g, '').slice(0, 11) })}
                  maxLength={11}
                  pattern="\d{11}"
                  className={`w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30 ${form.oib && form.oib.length > 0 && form.oib.length !== 11 ? 'border-amber-400 bg-amber-50' : 'border-slate-200'}`}
                  placeholder="12345678901"
                />
                {form.oib && form.oib.length > 0 && form.oib.length !== 11 && (
                  <p className="mt-0.5 text-xs text-amber-600">OIB mora sadržavati točno 11 znamenki ({form.oib.length}/11)</p>
                )}
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">Tip</label>
                <select
                  value={form.tip}
                  onChange={(e) => setForm({ ...form, tip: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                >
                  <option value="fizicka">Fizička osoba</option>
                  <option value="pravna">Pravna osoba</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">Adresa</label>
                <input
                  value={form.adresa}
                  onChange={(e) => setForm({ ...form, adresa: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="Ilica 1"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">Grad</label>
                <input
                  value={form.grad}
                  onChange={(e) => setForm({ ...form, grad: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="Zagreb"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">
                  Poštanski broj
                </label>
                <input
                  value={form.postanski_broj}
                  onChange={(e) => setForm({ ...form, postanski_broj: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="10000"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">E-mail</label>
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="ivan@primjer.hr"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-500">Telefon</label>
                <input
                  value={form.telefon}
                  onChange={(e) => setForm({ ...form, telefon: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="+385 91 234 5678"
                />
              </div>
              <div className="sm:col-span-2 lg:col-span-1">
                <label className="mb-1 block text-xs text-slate-500">Napomena</label>
                <input
                  value={form.napomena}
                  onChange={(e) => setForm({ ...form, napomena: e.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                  placeholder="Interna bilješka…"
                />
              </div>
            </div>
            <div className="mt-4 flex justify-end">
              <button
                type="submit"
                disabled={saving || !form.ime_naziv.trim()}
                className="flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-6 py-2.5 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 disabled:opacity-50 active:scale-[0.98]"
              >
                {saving ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Plus className="h-4 w-4" />
                )}
                Spremi stranku
              </button>
            </div>
          </form>
        )}

        {/* Fetch error banner */}
        {fetchError && (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
            {fetchError}
            <button onClick={fetchClients} className="ml-auto text-xs font-medium text-red-800 underline">Pokušaj ponovo</button>
          </div>
        )}

        {/* Stats */}
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Ukupno stranaka</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {clients.length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Fizičke osobe</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {clients.filter((c) => c.tip === "fizicka").length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Pravne osobe</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {clients.filter((c) => c.tip === "pravna").length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-xs text-slate-500">Rezultati pretrage</div>
            <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">
              {filtered.length}
            </div>
          </div>
        </div>

        {/* Client list.
            Omotač nosi metu vodiča jer korak 7/7 pokazuje na popis, a novi
            korisnik nema nijednu stranku — bez omotača meta bi u praznom stanju
            nestala i vodič bi pokazivao u prazno. */}
        <div data-vodic="vodic-dosje" className={vodicHighlight("vodic-dosje")}>
        {loading ? (
          <ClientListSkeleton />
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-slate-400">
            <Users className="mb-3 h-12 w-12 text-slate-300" />
            <p className="text-lg font-medium">
              {search ? "Nema rezultata pretrage" : "Još nemate stranaka"}
            </p>
            <p className="mt-1 text-sm">
              {search
                ? "Pokušajte s drugim pojmom."
                : 'Kliknite "Nova stranka" za početak.'}
            </p>
          </div>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {filtered.map((client) => (
              <div
                key={client.id}
                className="group relative rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-[#1B2A4A]/40 hover:shadow-md"
              >
                {/* Delete button */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete(client.id);
                  }}
                  disabled={deleting === client.id}
                  className="absolute right-3 top-3 rounded-lg p-1.5 text-slate-300 opacity-0 transition hover:bg-red-50 hover:text-red-500 group-hover:opacity-100 disabled:opacity-50"
                >
                  {deleting === client.id ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Trash2 className="h-4 w-4" />
                  )}
                </button>

                <Link href={`/klijenti/${client.id}`} className="block">
                  {/* Icon + Name */}
                  <div className="mb-3 flex items-start gap-3">
                    <div
                      className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
                        client.tip === "pravna"
                          ? "bg-blue-50 text-blue-700"
                          : "bg-[#1B2A4A]/10 text-[#1B2A4A]"
                      }`}
                    >
                      {client.tip === "pravna" ? (
                        <Building2 className="h-5 w-5" />
                      ) : (
                        <User className="h-5 w-5" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <h3 className="truncate text-sm font-semibold text-slate-900">
                        {client.ime_naziv}
                      </h3>
                      {client.oib && (
                        <p className="text-xs text-slate-500">
                          OIB: {client.oib}
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Details */}
                  <div className="space-y-1.5 text-xs text-slate-500">
                    {client.grad && (
                      <div className="flex items-center gap-2">
                        <MapPin className="h-3.5 w-3.5 text-slate-400" />
                        <span>
                          {client.adresa ? `${client.adresa}, ` : ""}
                          {client.grad}
                        </span>
                      </div>
                    )}
                    {client.email && (
                      <div className="flex items-center gap-2">
                        <Mail className="h-3.5 w-3.5 text-slate-400" />
                        <span className="truncate">{client.email}</span>
                      </div>
                    )}
                    {client.telefon && (
                      <div className="flex items-center gap-2">
                        <Phone className="h-3.5 w-3.5 text-slate-400" />
                        <span>{client.telefon}</span>
                      </div>
                    )}
                  </div>

                  {/* Footer */}
                  <div className="mt-3 flex items-center justify-between border-t border-slate-100 pt-3">
                    <span className="text-xs text-slate-400">
                      {timeAgo(client.created_at)}
                    </span>
                    <span className="flex items-center gap-1 text-xs text-[#1B2A4A] opacity-0 transition group-hover:opacity-100">
                      Profil <ChevronRight className="h-3 w-3" />
                    </span>
                  </div>
                </Link>
              </div>
            ))}
          </div>
        )}
        </div>
      </main>
    </div>
  );
}
