"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  ChevronRight,
  Loader2,
  Mail,
  Phone,
  Plus,
  Search,
  Trash2,
  User,
  Users,
  X,
  Edit3,
  MapPin,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { createSupabaseBrowser } from "@/lib/supabase/client";

// ── Types ──

interface Client {
  id: string;
  created_at: string;
  updated_at: string;
  user_id: string;
  ime_naziv: string;
  oib: string | null;
  adresa: string | null;
  grad: string | null;
  postanski_broj: string | null;
  email: string | null;
  telefon: string | null;
  tip: string;
  napomena: string | null;
}

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

export default function KlijentiPage() {
  const router = useRouter();
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [userId, setUserId] = useState<string | null>(null);

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

  // Get user ID on mount
  useEffect(() => {
    const supabase = createSupabaseBrowser();
    supabase.auth.getUser().then(({ data }) => {
      if (data.user) setUserId(data.user.id);
    });
  }, []);

  // Fetch clients
  const fetchClients = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/client`, {
        headers: { "x-user-id": userId },
      });
      if (res.ok) {
        const data = await res.json();
        setClients(data);
      }
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    fetchClients();
  }, [fetchClients]);

  // Create client
  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userId || !form.ime_naziv.trim()) return;
    setSaving(true);
    setError("");

    try {
      const res = await fetch(`${API_BASE}/api/client`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "x-user-id": userId,
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
    if (!userId) return;
    if (!confirm("Jeste li sigurni da želite obrisati ovu stranku?")) return;
    try {
      const res = await fetch(`${API_BASE}/api/client/${id}`, {
        method: "DELETE",
        headers: { "x-user-id": userId },
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error || "Greška pri brisanju.");
        return;
      }
      fetchClients();
    } catch {
      setError("Greška pri povezivanju s poslužiteljem.");
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
    <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-indigo-950 text-slate-100">
      {/* Header */}
      <header className="sticky top-0 z-30 border-b border-slate-800/60 bg-slate-950/80 backdrop-blur-xl">
        <div className="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3 sm:px-6">
          <Link
            href="/"
            className="flex items-center gap-2 text-sm text-slate-400 transition hover:text-white"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Početna</span>
          </Link>
          <div className="flex items-center gap-2">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-500/20">
              <Users className="h-4 w-4 text-violet-400" />
            </div>
            <h1 className="text-lg font-semibold">Stranke</h1>
          </div>
          <div className="ml-auto flex items-center gap-3">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                placeholder="Pretraži stranke…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-48 rounded-lg border border-slate-700/60 bg-slate-800/50 py-2 pl-10 pr-3 text-sm text-white placeholder-slate-500 transition focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30 sm:w-64"
              />
            </div>
            <button
              onClick={() => setShowForm(!showForm)}
              className="flex items-center gap-2 rounded-lg bg-violet-600 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-violet-500/20 transition hover:bg-violet-500 active:scale-[0.98]"
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
            className="mb-6 rounded-xl border border-slate-800/60 bg-slate-900/50 p-6 backdrop-blur"
          >
            <h2 className="mb-4 text-base font-semibold text-violet-300">
              Nova stranka
            </h2>
            {error && (
              <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-300">
                {error}
              </div>
            )}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <label className="mb-1 block text-xs text-slate-400">
                  Ime i prezime / Naziv *
                </label>
                <input
                  required
                  value={form.ime_naziv}
                  onChange={(e) => setForm({ ...form, ime_naziv: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="Ivan Horvat / Primjer d.o.o."
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">OIB</label>
                <input
                  value={form.oib}
                  onChange={(e) => setForm({ ...form, oib: e.target.value })}
                  maxLength={11}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="12345678901"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">Tip</label>
                <select
                  value={form.tip}
                  onChange={(e) => setForm({ ...form, tip: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                >
                  <option value="fizicka">Fizička osoba</option>
                  <option value="pravna">Pravna osoba</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">Adresa</label>
                <input
                  value={form.adresa}
                  onChange={(e) => setForm({ ...form, adresa: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="Ilica 1"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">Grad</label>
                <input
                  value={form.grad}
                  onChange={(e) => setForm({ ...form, grad: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="Zagreb"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">
                  Poštanski broj
                </label>
                <input
                  value={form.postanski_broj}
                  onChange={(e) => setForm({ ...form, postanski_broj: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="10000"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">E-mail</label>
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="ivan@primjer.hr"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-slate-400">Telefon</label>
                <input
                  value={form.telefon}
                  onChange={(e) => setForm({ ...form, telefon: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="+385 91 234 5678"
                />
              </div>
              <div className="sm:col-span-2 lg:col-span-1">
                <label className="mb-1 block text-xs text-slate-400">Napomena</label>
                <input
                  value={form.napomena}
                  onChange={(e) => setForm({ ...form, napomena: e.target.value })}
                  className="w-full rounded-lg border border-slate-700/60 bg-slate-800/50 px-3 py-2 text-sm text-white placeholder-slate-500 focus:border-violet-500/50 focus:outline-none focus:ring-1 focus:ring-violet-500/30"
                  placeholder="Interna bilješka…"
                />
              </div>
            </div>
            <div className="mt-4 flex justify-end">
              <button
                type="submit"
                disabled={saving || !form.ime_naziv.trim()}
                className="flex items-center gap-2 rounded-lg bg-violet-600 px-6 py-2.5 text-sm font-medium text-white shadow-lg shadow-violet-500/20 transition hover:bg-violet-500 disabled:opacity-50 active:scale-[0.98]"
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

        {/* Stats */}
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-xl border border-slate-800/60 bg-slate-900/50 p-4 backdrop-blur">
            <div className="text-xs text-slate-500">Ukupno stranaka</div>
            <div className="mt-1 text-2xl font-bold text-white">
              {clients.length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-800/60 bg-slate-900/50 p-4 backdrop-blur">
            <div className="text-xs text-slate-500">Fizičke osobe</div>
            <div className="mt-1 text-2xl font-bold text-blue-400">
              {clients.filter((c) => c.tip === "fizicka").length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-800/60 bg-slate-900/50 p-4 backdrop-blur">
            <div className="text-xs text-slate-500">Pravne osobe</div>
            <div className="mt-1 text-2xl font-bold text-violet-400">
              {clients.filter((c) => c.tip === "pravna").length}
            </div>
          </div>
          <div className="rounded-xl border border-slate-800/60 bg-slate-900/50 p-4 backdrop-blur">
            <div className="text-xs text-slate-500">Rezultati pretrage</div>
            <div className="mt-1 text-2xl font-bold text-emerald-400">
              {filtered.length}
            </div>
          </div>
        </div>

        {/* Client list */}
        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="h-8 w-8 animate-spin text-violet-400" />
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-slate-500">
            <Users className="mb-3 h-12 w-12 text-slate-700" />
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
                className="group relative rounded-xl border border-slate-800/60 bg-slate-900/50 p-5 backdrop-blur transition hover:border-violet-500/30 hover:bg-slate-800/40"
              >
                {/* Delete button */}
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleDelete(client.id);
                  }}
                  className="absolute right-3 top-3 rounded-lg p-1.5 text-slate-600 opacity-0 transition hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100"
                >
                  <Trash2 className="h-4 w-4" />
                </button>

                <Link href={`/klijenti/${client.id}`} className="block">
                  {/* Icon + Name */}
                  <div className="mb-3 flex items-start gap-3">
                    <div
                      className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
                        client.tip === "pravna"
                          ? "bg-violet-500/20 text-violet-400"
                          : "bg-blue-500/20 text-blue-400"
                      }`}
                    >
                      {client.tip === "pravna" ? (
                        <Building2 className="h-5 w-5" />
                      ) : (
                        <User className="h-5 w-5" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <h3 className="truncate text-sm font-semibold text-white">
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
                  <div className="space-y-1.5 text-xs text-slate-400">
                    {client.grad && (
                      <div className="flex items-center gap-2">
                        <MapPin className="h-3.5 w-3.5 text-slate-600" />
                        <span>
                          {client.adresa ? `${client.adresa}, ` : ""}
                          {client.grad}
                        </span>
                      </div>
                    )}
                    {client.email && (
                      <div className="flex items-center gap-2">
                        <Mail className="h-3.5 w-3.5 text-slate-600" />
                        <span className="truncate">{client.email}</span>
                      </div>
                    )}
                    {client.telefon && (
                      <div className="flex items-center gap-2">
                        <Phone className="h-3.5 w-3.5 text-slate-600" />
                        <span>{client.telefon}</span>
                      </div>
                    )}
                  </div>

                  {/* Footer */}
                  <div className="mt-3 flex items-center justify-between border-t border-slate-800/40 pt-3">
                    <span className="text-xs text-slate-600">
                      {timeAgo(client.created_at)}
                    </span>
                    <span className="flex items-center gap-1 text-xs text-violet-400 opacity-0 transition group-hover:opacity-100">
                      Profil <ChevronRight className="h-3 w-3" />
                    </span>
                  </div>
                </Link>
              </div>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
