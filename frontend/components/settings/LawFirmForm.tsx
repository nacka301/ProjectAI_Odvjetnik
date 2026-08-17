"use client";

import { useEffect, useState } from "react";
import { Loader2, Save, Building2, Hash, MapPin, Landmark, Receipt } from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

interface Props {
  userId: string;
}

export default function LawFirmForm({ userId: _propUserId }: Props) {
  const { userId, authHeaders } = useAuth();
  const [name, setName] = useState("");
  const [oib, setOib] = useState("");
  const [address, setAddress] = useState("");
  const [iban, setIban] = useState("");
  const [pdvObveznik, setPdvObveznik] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Fetch firm ──
  useEffect(() => {
    if (!userId) {
      setLoading(false);
      return;
    }
    (async () => {
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/firm`,
          { headers: { ...authHeaders, "Content-Type": "application/json" } }
        );
        if (res.ok) {
          const data = await res.json();
          if (data && typeof data === "object") {
            setName(data.name || "");
            setOib(data.oib || "");
            setAddress(data.address || "");
            setIban(data.iban || "");
            setPdvObveznik(Boolean(data.pdv_obveznik));
          }
          // null / empty → fields stay blank = "create" mode
        } else if (res.status !== 404) {
          console.error("Firm fetch failed:", res.status);
          setError("Greška pri dohvaćanju podataka o uredu.");
        }
        // 404 = firm not set up yet → show empty form
      } catch (err) {
        console.error("Firm fetch error:", err);
        setError("Greška pri učitavanju podataka o uredu.");
      } finally {
        setLoading(false);
      }
    })();
  }, [userId, authHeaders]);

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
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          oib: oib.trim() || null,
          address: address.trim() || null,
          iban: iban.trim() || null,
          pdv_obveznik: pdvObveznik,
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

        {/* PDV obveznik */}
        <div className="rounded-lg border border-slate-200 p-4">
          <label className="flex items-start gap-3 cursor-pointer">
            <input
              type="checkbox"
              checked={pdvObveznik}
              onChange={(e) => setPdvObveznik(e.target.checked)}
              className="mt-0.5 h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
            />
            <span>
              <span className="flex items-center gap-2 text-xs font-semibold text-slate-700">
                <Receipt className="h-3.5 w-3.5 text-slate-400" />
                Ured je u sustavu PDV-a
              </span>
              <span className="block text-[11px] text-slate-400 mt-1">
                Postavlja zadanu vrijednost za nove troškovnike — svaki se troškovnik i dalje može ručno
                izuzeti ili uključiti pojedinačno.
              </span>
            </span>
          </label>
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
