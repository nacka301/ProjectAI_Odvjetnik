"use client";

import { useState } from "react";
import { Loader2, Check, AlertTriangle } from "lucide-react";
import { API_BASE } from "@/lib/api";

/**
 * Sets an office's subscription state and end date.
 *
 * This is the whole billing system. Payment is arranged by e-mail on purpose —
 * with a handful of users, a payment provider would be more machinery than the
 * problem deserves — so this form is what turns "they paid me" into access.
 *
 * The two shortcut buttons exist because extending by a month or a year is what
 * this will actually be used for; typing a date is the exception, not the rule.
 */

type Status = "trial" | "active" | "canceled" | "expired";

const STATUS_OPTIONS: { value: Status; label: string; hint: string }[] = [
  { value: "trial", label: "Proba (7 dana od danas)", hint: "Pokreće probu iznova." },
  { value: "active", label: "Pretplata aktivna", hint: "Pristup do datuma ispod." },
  { value: "canceled", label: "Otkazano", hint: "Radi do datuma, pa se zatvara." },
  { value: "expired", label: "Isteklo — pristup zatvoren", hint: "Zatvara pristup odmah." },
];

/** An <input type="date"> wants yyyy-MM-dd, and nothing else. */
function toDateInput(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function plusMonths(months: number): string {
  const d = new Date();
  d.setMonth(d.getMonth() + months);
  return toDateInput(d);
}

interface Props {
  lawFirmId: string | null;
  currentStatus: string | null;
  currentValidUntil: string | null;
  authHeaders: Record<string, string>;
  onSaved: () => void;
}

export default function SubscriptionEditor({
  lawFirmId,
  currentStatus,
  currentValidUntil,
  authHeaders,
  onSaved,
}: Props) {
  const [status, setStatus] = useState<Status>(
    (["trial", "active", "canceled", "expired"] as const).includes(currentStatus as Status)
      ? (currentStatus as Status)
      : "active",
  );
  const [validUntil, setValidUntil] = useState<string>(
    currentValidUntil ? currentValidUntil.slice(0, 10) : plusMonths(12),
  );
  const [saving, setSaving] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);

  // An account with no profile has no firm, and so nothing to bill.
  if (!lawFirmId) {
    return (
      <p className="text-xs text-slate-400">
        Ovaj račun nema ured, pa ni pretplatu.
      </p>
    );
  }

  // A trial ends at valid_from + 7 days, computed by the backend. Offering a
  // date here would invite the admin to set one that is quietly ignored.
  const needsDate = status === "active" || status === "canceled";

  async function save() {
    if (saving) return;
    setSaving(true);
    setResult(null);
    try {
      const res = await fetch(`${API_BASE}/api/admin/subscriptions/${lawFirmId}`, {
        method: "PATCH",
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({
          status,
          // End of the chosen day, so "do 31.12." includes the 31st.
          valid_until: needsDate ? new Date(`${validUntil}T23:59:59`).toISOString() : null,
        }),
      });

      if (res.ok) {
        setResult({ ok: true, message: "Spremljeno — vrijedi odmah." });
        onSaved();
      } else {
        const body = await res.json().catch(() => ({}));
        setResult({ ok: false, message: body.error || `Greška ${res.status}` });
      }
    } catch {
      setResult({ ok: false, message: "Mrežna greška — pokušajte ponovno." });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <label className="block">
          <span className="block text-[10px] font-semibold text-slate-500 uppercase tracking-wider mb-1">
            Stanje
          </span>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as Status)}
            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs text-slate-800 focus:border-slate-400 focus:outline-none"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </label>

        {needsDate && (
          <>
            <label className="block">
              <span className="block text-[10px] font-semibold text-slate-500 uppercase tracking-wider mb-1">
                Vrijedi do
              </span>
              <input
                type="date"
                value={validUntil}
                onChange={(e) => setValidUntil(e.target.value)}
                className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs text-slate-800 focus:border-slate-400 focus:outline-none"
              />
            </label>

            <div className="flex gap-1.5">
              <button
                type="button"
                onClick={() => setValidUntil(plusMonths(1))}
                className="rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-600 hover:bg-slate-50"
              >
                +1 mjesec
              </button>
              <button
                type="button"
                onClick={() => setValidUntil(plusMonths(12))}
                className="rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-600 hover:bg-slate-50"
              >
                +1 godina
              </button>
            </div>
          </>
        )}

        <button
          type="button"
          onClick={save}
          disabled={saving}
          className="inline-flex items-center gap-1.5 rounded-lg px-3.5 py-1.5 text-xs font-semibold text-white transition hover:opacity-90 disabled:opacity-50"
          style={{ background: "#1B2A4A" }}
        >
          {saving ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
          Spremi
        </button>
      </div>

      <p className="text-[11px] text-slate-400">
        {STATUS_OPTIONS.find((o) => o.value === status)?.hint}
      </p>

      {result && (
        <p
          className={`inline-flex items-center gap-1.5 text-[11px] font-medium ${
            result.ok ? "text-emerald-700" : "text-red-700"
          }`}
        >
          {result.ok ? <Check className="h-3 w-3" /> : <AlertTriangle className="h-3 w-3" />}
          {result.message}
        </p>
      )}
    </div>
  );
}
