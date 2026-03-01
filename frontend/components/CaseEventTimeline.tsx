"use client";

import { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import {
  Calendar,
  CheckCircle,
  ChevronDown,
  Clock,
  Euro,
  FileText,
  Loader2,
  Pencil,
  Plus,
  Scale,
  Trash2,
  X,
  XCircle,
} from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

// ── Types ──

export interface CaseEvent {
  id: string;
  created_at: string;
  case_id: string;
  user_id: string;
  tenant_id: string | null;
  event_date: string;
  event_type: string;
  description: string;
  tariff_item: string | null;
  tariff_points: number;
  point_value: number;
  total_eur: number;
  is_billable: boolean;
  billing_status: string;
}

interface EventTypeInfo {
  type: string;
  default_points: number;
}

// ── Constants ──

const EVENT_TYPE_LABELS: Record<string, string> = {
  podnesak: "Podnesak",
  rociste: "Ročište",
  savjetovanje: "Savjetovanje",
  zastupanje: "Zastupanje",
  razgledavanje_spisa: "Razgledavanje spisa",
  pravno_misljenje: "Pravno mišljenje",
  zalba: "Žalba",
  revizija: "Revizija",
  ovrha: "Ovrha",
  ugovor: "Ugovor",
  opomena: "Opomena",
  dopisivanje: "Dopisivanje",
  telefonski_razgovor: "Telefonski razgovor",
  ostalo: "Ostalo",
};

const EVENT_TYPE_ICONS: Record<string, string> = {
  podnesak: "📄",
  rociste: "⚖️",
  savjetovanje: "💬",
  zastupanje: "🏛️",
  razgledavanje_spisa: "📂",
  pravno_misljenje: "📝",
  zalba: "📮",
  revizija: "🔍",
  ovrha: "⚡",
  ugovor: "📋",
  opomena: "⚠️",
  dopisivanje: "✉️",
  telefonski_razgovor: "📞",
  ostalo: "📌",
};

const BILLING_LABELS: Record<string, { label: string; color: string }> = {
  pending: { label: "Na čekanju", color: "text-amber-700 bg-amber-50 border-amber-200" },
  billed: { label: "Naplaćeno", color: "text-emerald-700 bg-emerald-50 border-emerald-200" },
  written_off: { label: "Otpisano", color: "text-slate-500 bg-slate-50 border-slate-200" },
};

// ── Helpers ──

function formatDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString("hr-HR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function formatEur(n: number) {
  return new Intl.NumberFormat("hr-HR", {
    style: "currency",
    currency: "EUR",
    minimumFractionDigits: 2,
  }).format(n);
}

// ── Timeline Component ──

interface CaseEventTimelineProps {
  caseId: string;
}

export default function CaseEventTimeline({ caseId }: CaseEventTimelineProps) {
  const { userId, authHeaders } = useAuth();
  const [events, setEvents] = useState<CaseEvent[]>([]);
  const [eventTypes, setEventTypes] = useState<EventTypeInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Modal state
  const [showModal, setShowModal] = useState(false);
  const [editingEvent, setEditingEvent] = useState<CaseEvent | null>(null);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);

  // Form state
  const [form, setForm] = useState({
    event_date: new Date().toISOString().split("T")[0],
    event_type: "podnesak",
    description: "",
    tariff_item: "",
    tariff_points: 0,
    point_value: 1.0,
    is_billable: true,
  });

  // Summary stats
  const totalEur = events.reduce((s, e) => s + e.total_eur, 0);
  const billableCount = events.filter((e) => e.is_billable).length;
  const pendingEur = events
    .filter((e) => e.billing_status === "pending" && e.is_billable)
    .reduce((s, e) => s + e.total_eur, 0);

  const fetchEvents = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch(
        `${API_BASE}/api/case-event?caseId=${caseId}`,
        { headers: authHeaders }
      );
      if (res.ok) {
        setEvents(await res.json());
      } else {
        setError("Greška pri učitavanju upisnika.");
      }
    } catch {
      setError("Poslužitelj nije dostupan.");
    } finally {
      setLoading(false);
    }
  }, [caseId, userId, authHeaders]);

  const fetchEventTypes = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/api/case-event/types`, {
        headers: authHeaders,
      });
      if (res.ok) {
        setEventTypes(await res.json());
      }
    } catch {
      // Non-critical — fallback to hardcoded defaults
    }
  }, [authHeaders]);

  useEffect(() => {
    if (userId) {
      fetchEvents();
      fetchEventTypes();
    }
  }, [fetchEvents, fetchEventTypes, userId]);

  // Auto-fill default points when event type changes
  const handleTypeChange = (type: string) => {
    const info = eventTypes.find((t) => t.type === type);
    setForm((prev) => ({
      ...prev,
      event_type: type,
      tariff_points: info?.default_points ?? prev.tariff_points,
    }));
  };

  const openCreateModal = () => {
    setEditingEvent(null);
    setForm({
      event_date: new Date().toISOString().split("T")[0],
      event_type: "podnesak",
      description: "",
      tariff_item: "",
      tariff_points: eventTypes.find((t) => t.type === "podnesak")?.default_points ?? 50,
      point_value: 1.0,
      is_billable: true,
    });
    setShowModal(true);
  };

  const openEditModal = (ev: CaseEvent) => {
    setEditingEvent(ev);
    setForm({
      event_date: ev.event_date?.split("T")[0] ?? "",
      event_type: ev.event_type,
      description: ev.description,
      tariff_item: ev.tariff_item ?? "",
      tariff_points: ev.tariff_points,
      point_value: ev.point_value,
      is_billable: ev.is_billable,
    });
    setShowModal(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userId || !form.description.trim()) return;
    setSaving(true);
    setError("");

    try {
      const url = editingEvent
        ? `${API_BASE}/api/case-event/${editingEvent.id}`
        : `${API_BASE}/api/case-event`;
      const method = editingEvent ? "PATCH" : "POST";

      const body: Record<string, unknown> = editingEvent
        ? {
            event_date: form.event_date,
            event_type: form.event_type,
            description: form.description,
            tariff_item: form.tariff_item || null,
            tariff_points: form.tariff_points,
            point_value: form.point_value,
            is_billable: form.is_billable,
          }
        : {
            case_id: caseId,
            event_date: form.event_date,
            event_type: form.event_type,
            description: form.description,
            tariff_item: form.tariff_item || null,
            tariff_points: form.tariff_points,
            point_value: form.point_value,
            is_billable: form.is_billable,
          };

      const res = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json", ...authHeaders },
        body: JSON.stringify(body),
      });

      if (res.ok) {
        setShowModal(false);
        setEditingEvent(null);
        fetchEvents();
      } else {
        const data = await res.json().catch(() => ({}));
        setError(data.error || "Greška pri spremanju.");
      }
    } catch {
      setError("Greška pri povezivanju s poslužiteljem.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!userId || deleting) return;
    if (!confirm("Obrisati ovu radnju iz upisnika?")) return;
    setDeleting(id);
    try {
      const res = await fetch(`${API_BASE}/api/case-event/${id}`, {
        method: "DELETE",
        headers: authHeaders,
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error || "Greška pri brisanju.");
      }
      fetchEvents();
    } catch {
      setError("Greška pri povezivanju s poslužiteljem.");
    } finally {
      setDeleting(null);
    }
  };

  // ── Render ──

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  return (
    <div>
      {/* Error banner */}
      {error && (
        <div className="mb-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm text-red-700">
          {error}
          <button
            onClick={() => setError("")}
            className="ml-auto text-xs font-medium text-red-800 underline"
          >
            Zatvori
          </button>
        </div>
      )}

      {/* Summary stats */}
      <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs text-slate-500">Ukupno radnji</div>
          <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">{events.length}</div>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs text-slate-500">Naplative</div>
          <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">{billableCount}</div>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs text-slate-500">Ukupno EUR</div>
          <div className="mt-1 text-2xl font-bold text-[#1B2A4A]">{formatEur(totalEur)}</div>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs text-slate-500">Na čekanju</div>
          <div className="mt-1 text-2xl font-bold text-amber-600">{formatEur(pendingEur)}</div>
        </div>
      </div>

      {/* Add button */}
      <div className="mb-4 flex justify-end">
        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 active:scale-[0.98]"
        >
          <Plus className="h-4 w-4" />
          Nova radnja
        </button>
      </div>

      {/* Empty state */}
      {events.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-slate-500">
          <FileText className="mb-3 h-10 w-10 text-slate-300" />
          <p className="text-sm">Upisnik je prazan.</p>
          <p className="mt-1 text-xs text-slate-400">
            Dodajte prvu radnju — podnesak, ročište, savjetovanje…
          </p>
        </div>
      ) : (
        /* ── Vertical Timeline ── */
        <div className="relative space-y-0">
          {/* Timeline line */}
          <div className="absolute left-5 top-0 bottom-0 w-px bg-slate-200" />

          {events.map((ev, idx) => {
            const billing = BILLING_LABELS[ev.billing_status] ?? BILLING_LABELS.pending;
            const isFirst = idx === 0;
            return (
              <div key={ev.id} className="group relative flex gap-4 pb-6">
                {/* Timeline dot */}
                <div className="relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2 border-slate-200 bg-white text-lg shadow-sm">
                  {EVENT_TYPE_ICONS[ev.event_type] ?? "📌"}
                </div>

                {/* Content card */}
                <div
                  className={`min-w-0 flex-1 rounded-xl border bg-white p-4 shadow-sm transition hover:shadow-md ${
                    isFirst ? "border-[#1B2A4A]/30" : "border-slate-200"
                  }`}
                >
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="rounded-md bg-[#1B2A4A]/10 px-2 py-0.5 text-xs font-medium text-[#1B2A4A]">
                          {EVENT_TYPE_LABELS[ev.event_type] ?? ev.event_type}
                        </span>
                        <span className="flex items-center gap-1 text-xs text-slate-400">
                          <Calendar className="h-3 w-3" />
                          {formatDate(ev.event_date)}
                        </span>
                        {ev.is_billable && (
                          <span
                            className={`rounded-full border px-2 py-0.5 text-[10px] font-medium ${billing.color}`}
                          >
                            {billing.label}
                          </span>
                        )}
                      </div>
                      <p className="mt-1.5 text-sm text-slate-700">{ev.description}</p>
                      {ev.tariff_item && (
                        <p className="mt-1 text-xs text-slate-400">
                          Tar. stavka: {ev.tariff_item}
                        </p>
                      )}
                    </div>

                    {/* Amount */}
                    {ev.is_billable && ev.total_eur > 0 && (
                      <div className="text-right">
                        <div className="text-sm font-bold text-[#1B2A4A]">
                          {formatEur(ev.total_eur)}
                        </div>
                        <div className="text-[10px] text-slate-400">
                          {ev.tariff_points} bod. × {ev.point_value.toFixed(2)} €
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Actions */}
                  <div className="mt-2 flex items-center gap-2 opacity-0 transition group-hover:opacity-100">
                    <button
                      onClick={() => openEditModal(ev)}
                      className="flex items-center gap-1 rounded-md px-2 py-1 text-xs text-slate-500 transition hover:bg-slate-100 hover:text-slate-700"
                    >
                      <Pencil className="h-3 w-3" />
                      Uredi
                    </button>
                    {ev.billing_status !== "billed" && (
                      <button
                        onClick={() => handleDelete(ev.id)}
                        disabled={deleting === ev.id}
                        className="flex items-center gap-1 rounded-md px-2 py-1 text-xs text-red-400 transition hover:bg-red-50 hover:text-red-600 disabled:opacity-50"
                      >
                        {deleting === ev.id ? (
                          <Loader2 className="h-3 w-3 animate-spin" />
                        ) : (
                          <Trash2 className="h-3 w-3" />
                        )}
                        Obriši
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* ── Modal (Create / Edit) ── */}
      {showModal &&
        createPortal(
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
            <div className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white shadow-2xl">
              <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
                <h3 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-[#1B2A4A]">
                  {editingEvent ? "Uredi radnju" : "Nova radnja"}
                </h3>
                <button
                  onClick={() => setShowModal(false)}
                  className="rounded-lg p-1.5 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              <form onSubmit={handleSave} className="space-y-4 px-6 py-5">
                {/* Row: Date + Type */}
                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Datum radnje *
                    </label>
                    <input
                      type="date"
                      required
                      value={form.event_date}
                      onChange={(e) =>
                        setForm({ ...form, event_date: e.target.value })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Vrsta radnje *
                    </label>
                    <select
                      value={form.event_type}
                      onChange={(e) => handleTypeChange(e.target.value)}
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    >
                      {Object.entries(EVENT_TYPE_LABELS).map(([key, label]) => (
                        <option key={key} value={key}>
                          {EVENT_TYPE_ICONS[key]} {label}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Description */}
                <div>
                  <label className="mb-1 block text-xs text-slate-400">
                    Opis radnje *
                  </label>
                  <textarea
                    required
                    rows={3}
                    value={form.description}
                    onChange={(e) =>
                      setForm({ ...form, description: e.target.value })
                    }
                    className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    placeholder="Sastav tužbe radi naknade štete…"
                  />
                </div>

                {/* Tariff item */}
                <div>
                  <label className="mb-1 block text-xs text-slate-400">
                    Tarifna stavka (Tbr.)
                  </label>
                  <input
                    value={form.tariff_item}
                    onChange={(e) =>
                      setForm({ ...form, tariff_item: e.target.value })
                    }
                    className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    placeholder="Tbr. 7 st. 2"
                  />
                </div>

                {/* Row: Points + Value + Billable */}
                <div className="grid gap-4 sm:grid-cols-3">
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Bodovi
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.tariff_points}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          tariff_points: parseInt(e.target.value) || 0,
                        })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-slate-400">
                      Vrijednost boda (€)
                    </label>
                    <input
                      type="number"
                      min={0}
                      step={0.01}
                      value={form.point_value}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          point_value: parseFloat(e.target.value) || 0,
                        })
                      }
                      className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-[#1B2A4A]/50 focus:outline-none focus:ring-1 focus:ring-[#1B2A4A]/30"
                    />
                  </div>
                  <div className="flex items-end pb-2">
                    <label className="flex items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={form.is_billable}
                        onChange={(e) =>
                          setForm({ ...form, is_billable: e.target.checked })
                        }
                        className="h-4 w-4 rounded border-slate-300 text-[#1B2A4A] focus:ring-[#1B2A4A]/30"
                      />
                      Naplativo
                    </label>
                  </div>
                </div>

                {/* Calculated total preview */}
                {form.is_billable && form.tariff_points > 0 && form.point_value > 0 && (
                  <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-sm text-emerald-700">
                    <Euro className="h-4 w-4" />
                    Ukupno: <strong>{formatEur(form.tariff_points * form.point_value)}</strong>
                    <span className="text-xs text-emerald-600">
                      ({form.tariff_points} bod. × {form.point_value.toFixed(2)} €)
                    </span>
                  </div>
                )}

                {/* Actions */}
                <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
                  <button
                    type="button"
                    onClick={() => setShowModal(false)}
                    className="rounded-lg border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-50"
                  >
                    Odustani
                  </button>
                  <button
                    type="submit"
                    disabled={saving || !form.description.trim()}
                    className="flex items-center gap-2 rounded-lg bg-[#1B2A4A] px-6 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-[#1B2A4A]/90 disabled:opacity-50 active:scale-[0.98]"
                  >
                    {saving ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <CheckCircle className="h-4 w-4" />
                    )}
                    {editingEvent ? "Spremi promjene" : "Dodaj radnju"}
                  </button>
                </div>
              </form>
            </div>
          </div>,
          document.body
        )}
    </div>
  );
}
