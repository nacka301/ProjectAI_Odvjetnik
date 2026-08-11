"use client";

import { useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Loader2, Lock, Shield, AlertCircle } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

export default function TwoFactorPage() {
  return (
    <Suspense fallback={null}>
      <TwoFactorContent />
    </Suspense>
  );
}

function TwoFactorContent() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Where the middleware wanted to send the user before it stopped them.
  const next = searchParams.get("next");
  const destination = next && next.startsWith("/") ? next : "/";

  const handleVerify = async () => {
    setError(null);
    if (!token) {
      setError("Unesite kod.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/2fa/verify", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token }),
      });

      const data = await res.json().catch(() => null);

      if (!res.ok || data?.success !== true) {
        setError(data?.error ?? "Neispravan kod.");
        setToken("");
        return;
      }

      router.push(destination);
      router.refresh();
    } catch {
      setError("Greška pri provjeri koda. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = async () => {
    const supabase = createSupabaseBrowser();
    await supabase.auth.signOut();
    router.push("/auth/login");
    router.refresh();
  };

  return (
    <>
      {/* Logo */}
      <div className="flex items-center justify-center gap-3 mb-8">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center shadow-lg"
          style={{ background: "var(--auth-navy)", boxShadow: "0 4px 20px rgba(201,168,76,0.15)" }}
        >
          <Shield className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
        </div>
        <div>
          <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p className="text-[10px] uppercase tracking-[0.2em]" style={{ color: "var(--auth-gold)" }}>
            Dvofaktorska Provjera
          </p>
        </div>
      </div>

      {/* Card */}
      <div
        className="rounded-2xl shadow-2xl overflow-hidden"
        style={{
          background: "var(--auth-card-bg)",
          border: "1px solid var(--auth-card-border)",
          boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4), 0 0 0 1px rgba(201,168,76,0.1)",
        }}
      >
        <div className="px-6 pt-6 pb-2 sm:px-8 sm:pt-8">
          <h2 className="font-serif text-lg font-semibold text-slate-900 mb-1">
            Dvofaktorska autentifikacija
          </h2>
          <p className="text-sm text-slate-500">
            Unesite kod iz vaše authenticator aplikacije
          </p>
        </div>

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
          {error && (
            <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700">
              <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
              <span>{error}</span>
            </div>
          )}

          <div className="flex flex-col items-center gap-4 py-2">
            <div
              className="w-16 h-16 rounded-2xl flex items-center justify-center"
              style={{ background: "var(--auth-navy)" }}
            >
              <Lock className="h-8 w-8" style={{ color: "var(--auth-gold)" }} />
            </div>

            <input
              type="text"
              inputMode="numeric"
              maxLength={8}
              value={token}
              onChange={(e) => setToken(e.target.value.replace(/[^A-Za-z0-9]/g, "").slice(0, 8))}
              placeholder="000000"
              className="w-48 text-center border border-slate-200 rounded-xl px-4 py-3 text-xl font-mono tracking-[0.3em] text-slate-900 placeholder:text-slate-300 focus:ring-2 outline-none transition"
              style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
              autoFocus
              onKeyDown={(e) => { if (e.key === "Enter") handleVerify(); }}
            />

            <p className="text-xs text-slate-400 text-center leading-relaxed">
              Unesite 6-znamenkasti kod iz aplikacije<br />
              ili 8-znakovni kod za oporavak
            </p>
          </div>

          <button
            onClick={handleVerify}
            disabled={loading || !token}
            className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed hover:opacity-90"
            style={{ background: "var(--auth-navy)", boxShadow: "0 4px 14px rgba(27,42,74,0.4)" }}
          >
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Shield className="h-4 w-4" />}
            {loading ? "Provjera..." : "Potvrdi prijavu"}
          </button>

          <button
            onClick={handleCancel}
            className="w-full flex items-center justify-center gap-1.5 text-xs font-medium transition py-2 rounded-lg hover:bg-slate-50"
            style={{ color: "var(--auth-gold)" }}
          >
            ← Odjavi se
          </button>
        </div>
      </div>

      <div className="flex items-center justify-center gap-2 mt-6 text-xs text-slate-500">
        <Shield className="h-3.5 w-3.5" style={{ color: "var(--auth-gold-muted, #B8A060)" }} />
        <span>Zaštićeno dvofaktorskom autentifikacijom</span>
      </div>
    </>
  );
}
