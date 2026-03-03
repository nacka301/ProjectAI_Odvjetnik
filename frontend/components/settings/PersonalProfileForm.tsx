"use client";

import { useEffect, useState } from "react";
import { Loader2, Save, User, Phone, Mail } from "lucide-react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

interface Props {
  userId: string;
  userEmail: string | null;
}

export default function PersonalProfileForm({ userId: _propUserId, userEmail }: Props) {
  const { userId, authHeaders } = useAuth();
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Fetch profile ──
  useEffect(() => {
    if (!userId) {
      setLoading(false);
      return;
    }
    (async () => {
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/profile`,
          { headers: { ...authHeaders, "Content-Type": "application/json" } }
        );
        if (res.ok) {
          const data = await res.json();
          setFullName(data.full_name || "");
          setPhone(data.phone_number || "");
        } else if (res.status !== 404) {
          console.error("Profile fetch failed:", res.status);
          setError("Greška pri dohvaćanju profila.");
        }
      } catch (err) {
        console.error("Profile fetch error:", err);
        setError("Greška pri učitavanju profila.");
      } finally {
        setLoading(false);
      }
    })();
  }, [userId, authHeaders]);

  // ── Save profile ──
  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (!fullName.trim()) {
      setError("Ime i prezime je obavezno.");
      return;
    }

    setSaving(true);
    try {
      const res = await fetch(`${API_BASE}/api/settings/profile`, {
        method: "PUT",
        headers: { ...authHeaders, "Content-Type": "application/json" },
        body: JSON.stringify({
          full_name: fullName.trim(),
          phone_number: phone.trim() || null,
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
          <User className="h-5 w-5 text-white" />
        </div>
        <div>
          <h2 className="font-serif text-lg font-semibold text-slate-900">Osobni profil</h2>
          <p className="text-sm text-slate-400 mt-0.5">Upravljajte osobnim podacima i kontakt informacijama</p>
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
            Promjene su uspješno spremljene.
          </div>
        )}

        {/* Full Name */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <User className="h-3.5 w-3.5 text-slate-400" />
            Ime i prezime
          </label>
          <input
            type="text"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="Ivan Horvat"
            className="w-full px-4 py-2.5 rounded-lg border border-slate-200 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-slate-400 focus:ring-1 focus:ring-slate-400 transition"
          />
        </div>

        {/* Email (readonly) */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <Mail className="h-3.5 w-3.5 text-slate-400" />
            Email adresa
          </label>
          <input
            type="email"
            value={userEmail || ""}
            disabled
            className="w-full px-4 py-2.5 rounded-lg border border-slate-100 text-sm text-slate-400 bg-slate-50 cursor-not-allowed"
          />
          <p className="text-[11px] text-slate-400 mt-1">Email adresa se ne može mijenjati ovdje.</p>
        </div>

        {/* Phone */}
        <div>
          <label className="flex items-center gap-2 text-xs font-semibold text-slate-700 mb-1.5">
            <Phone className="h-3.5 w-3.5 text-slate-400" />
            Broj telefona
          </label>
          <input
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+385 91 234 5678"
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
            {saving ? "Spremanje..." : "Spremi promjene"}
          </button>
        </div>
      </form>
    </>
  );
}
