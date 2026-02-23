"use client";

import { useEffect, useState } from "react";
import { Loader2, Save, Building2, Hash, MapPin, Landmark } from "lucide-react";
import { API_BASE } from "@/lib/api";

function apiHeaders(): Record<string, string> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const apiKey = process.env.NEXT_PUBLIC_API_KEY;
  if (apiKey) headers["X-API-Key"] = apiKey;
  return headers;
}

interface Props {
  userId: string;
}

export default function LawFirmForm({ userId }: Props) {
  const [name, setName] = useState("");
  const [oib, setOib] = useState("");
  const [address, setAddress] = useState("");
  const [iban, setIban] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Fetch firm ──
  useEffect(() => {
    if (!userId) return;
    (async () => {
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/firm?userId=${encodeURIComponent(userId)}`,
          { headers: apiHeaders() }
        );
        if (res.ok) {
          const data = await res.json();
          setName(data.name || "");
          setOib(data.oib || "");
          setAddress(data.address || "");
          setIban(data.iban || "");
        }
      } catch {
        // silent
      } finally {
        setLoading(false);
      }
    })();
  }, [userId]);

  // ── Save firm ──
  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (!name.trim()) {
      setError("Naziv ureda je obavezan.");
      return;
    }

    if (oib.trim() && (oib.trim().length !== 11 || !/^\d{11}$/.test(oib.trim()))) {
      setError("OIB mora imati točno 11 znamenki.");
      return;
    }

    setSaving(true);
    try {
      const res = await fetch(`${API_BASE}/api/settings/firm`, {
        method: "PUT",
        headers: apiHeaders(),
        body: JSON.stringify({
          user_id: userId,
          name: name.trim(),
          oib: oib.trim() || null,
          address: address.trim() || null,
          iban: iban.trim() || null,
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => null);
        throw new Error(data?.error || "Greška pri spremanju.");
      }

      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Nepoznata greška.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  return (
    <>
      {/* Header */}
      <div className="px-6 sm:px-8 py-5 border-b border-slate-200 flex items-center gap-4">
        <div className="w-10 h-10 rounded-lg flex items-center justify-center shrink-0" style={{ background: "#1B2A4A" }}>
          <Building2 className="h-5 w-5 text-white" />
        </div>
        <div>
          <h2 className="font-serif text-lg font-semibold text-slate-900">Odvjetnički ured</h2>
          <p className="text-sm text-slate-400 mt-0.5">Podaci o uredu, OIB, adresa sjedišta i IBAN</p>
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSave} className="px-6 sm:px-8 py-6 space-y-5">
        {error && (
          <div className="rounded-lg p-3 bg-red-50 border border-red-200 text-sm text-red-700">
            {error}
          </div>
        )}

        {success && (
          <div className="rounded-lg p-3 bg-emerald-50 border border-emerald-200 text-sm text-emerald-700">
            Podaci o uredu su uspješno ažurirani.
          </div>
        )}

        {/* Firm Name */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <Building2 className="h-3.5 w-3.5 text-slate-400" />
            Naziv ureda
          </label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Odvjetnički ured Horvat"
            className="w-full px-4 py-2.5 rounded-lg border border-slate-200 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-slate-400 focus:ring-1 focus:ring-slate-400 transition"
          />
        </div>

        {/* OIB */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <Hash className="h-3.5 w-3.5 text-slate-400" />
            OIB ureda
          </label>
          <input
            type="text"
            inputMode="numeric"
            maxLength={11}
            value={oib}
            onChange={(e) => setOib(e.target.value.replace(/\D/g, "").slice(0, 11))}
            placeholder="12345678901"
            className="w-full px-4 py-2.5 rounded-lg border border-slate-200 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-slate-400 focus:ring-1 focus:ring-slate-400 transition"
          />
          <p className="text-[11px] text-slate-400 mt-1">Osobni identifikacijski broj (11 znamenki)</p>
        </div>

        {/* Address */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <MapPin className="h-3.5 w-3.5 text-slate-400" />
            Adresa sjedišta
          </label>
          <input
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            placeholder="Ilica 1, 10000 Zagreb"
            className="w-full px-4 py-2.5 rounded-lg border border-slate-200 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-slate-400 focus:ring-1 focus:ring-slate-400 transition"
          />
        </div>

        {/* IBAN */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <Landmark className="h-3.5 w-3.5 text-slate-400" />
            IBAN
          </label>
          <input
            type="text"
            value={iban}
            onChange={(e) => setIban(e.target.value)}
            placeholder="HR12 1234 0061 1234 5678 9"
            className="w-full px-4 py-2.5 rounded-lg border border-slate-200 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-slate-400 focus:ring-1 focus:ring-slate-400 transition"
          />
        </div>

        {/* Submit */}
        <div className="pt-2">
          <button
            type="submit"
            disabled={saving}
            className="flex items-center gap-2 text-sm font-semibold py-2.5 px-6 rounded-lg text-white transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed hover:opacity-90"
            style={{ background: "#1B2A4A" }}
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            {saving ? "Spremanje..." : "Ažuriraj podatke o uredu"}
          </button>
        </div>
      </form>
    </>
  );
}
