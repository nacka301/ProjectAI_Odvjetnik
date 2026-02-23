"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  Eye,
  EyeOff,
  Loader2,
  KeyRound,
  Scale,
  AlertCircle,
  CheckCircle,
  Check,
  X,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { isPasswordValid, validatePassword } from "@/lib/auth-validation";

export default function ResetPasswordPage() {
  const router = useRouter();
  const supabase = createSupabaseBrowser();

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const checks = validatePassword(password);
  const pwValid = isPasswordValid(password);
  const pwMatch = password === confirm && confirm.length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!pwValid) {
      setError("Lozinka ne zadovoljava sigurnosne zahtjeve.");
      return;
    }
    if (!pwMatch) {
      setError("Lozinke se ne podudaraju.");
      return;
    }

    setLoading(true);
    try {
      const { error: authError } = await supabase.auth.updateUser({ password });

      if (authError) {
        if (authError.message.toLowerCase().includes("same password")) {
          setError("Nova lozinka mora biti različita od prethodne.");
        } else {
          setError(authError.message);
        }
        return;
      }

      setSuccess(true);
      // Redirect to login after 3s
      setTimeout(() => {
        router.push("/auth/login");
        router.refresh();
      }, 3000);
    } catch {
      setError("Greška pri povezivanju sa serverom. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  // ── Success state ──
  if (success) {
    return (
      <>
        <div className="flex items-center justify-center gap-3 mb-8">
          <div
            className="w-11 h-11 rounded-xl flex items-center justify-center shadow-lg"
            style={{ background: "var(--auth-navy)", boxShadow: "0 4px 20px rgba(201,168,76,0.15)" }}
          >
            <Scale className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
          </div>
          <div>
            <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
            <p className="text-[10px] uppercase tracking-[0.2em]" style={{ color: "var(--auth-gold)" }}>
              Pravna Inteligencija
            </p>
          </div>
        </div>

        <div
          className="rounded-2xl shadow-2xl p-6 sm:p-8 text-center"
          style={{
            background: "var(--auth-card-bg)",
            border: "1px solid var(--auth-card-border)",
            boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4), 0 0 0 1px rgba(201,168,76,0.1)",
          }}
        >
          <div className="w-14 h-14 bg-emerald-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <CheckCircle className="h-7 w-7 text-emerald-600" />
          </div>
          <h2 className="font-serif text-lg font-semibold text-slate-900 mb-2">Lozinka promijenjena</h2>
          <p className="text-sm text-slate-500 mb-4">
            Vaša lozinka je uspješno ažurirana. Preusmjeravamo Vas na prijavu...
          </p>
          <Link
            href="/auth/login"
            className="inline-flex items-center gap-2 text-white text-sm font-semibold px-6 py-2.5 rounded-xl transition shadow-lg"
            style={{ background: "var(--auth-navy)", boxShadow: "0 4px 14px rgba(27,42,74,0.4)" }}
          >
            Prijavi se
          </Link>
        </div>
      </>
    );
  }

  // ── Form ──
  return (
    <>
      {/* Logo */}
      <div className="flex items-center justify-center gap-3 mb-8">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center shadow-lg"
          style={{ background: "var(--auth-navy)", boxShadow: "0 4px 20px rgba(201,168,76,0.15)" }}
        >
          <Scale className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
        </div>
        <div>
          <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p className="text-[10px] uppercase tracking-[0.2em]" style={{ color: "var(--auth-gold)" }}>
            Pravna Inteligencija
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
          <h2 className="font-serif text-lg font-semibold text-slate-900 mb-1">Nova lozinka</h2>
          <p className="text-sm text-slate-500">Unesite novu lozinku za Vaš račun.</p>
        </div>

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
          {/* Error */}
          {error && (
            <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 animate-in fade-in duration-200">
              <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {/* New Password */}
            <div>
              <label htmlFor="password" className="block text-xs font-semibold text-slate-700 mb-1.5">
                Nova lozinka
              </label>
              <div className="relative">
                <input
                  id="password"
                  type={showPw ? "text" : "password"}
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={loading}
                  placeholder="••••••••"
                  className="w-full px-3.5 py-2.5 pr-10 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
                  style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
                />
                <button
                  type="button"
                  tabIndex={-1}
                  onClick={() => setShowPw(!showPw)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition"
                >
                  {showPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>

              {/* Password strength checklist */}
              {password.length > 0 && (
                <div className="mt-2 space-y-1">
                  {checks.map((c) => (
                    <div key={c.label} className="flex items-center gap-1.5 text-xs">
                      {c.met ? (
                        <Check className="h-3.5 w-3.5 text-emerald-500" />
                      ) : (
                        <X className="h-3.5 w-3.5 text-slate-300" />
                      )}
                      <span className={c.met ? "text-emerald-600" : "text-slate-400"}>
                        {c.label}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Confirm Password */}
            <div>
              <label htmlFor="confirm" className="block text-xs font-semibold text-slate-700 mb-1.5">
                Potvrdite lozinku
              </label>
              <div className="relative">
                <input
                  id="confirm"
                  type={showConfirm ? "text" : "password"}
                  autoComplete="new-password"
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  disabled={loading}
                  placeholder="••••••••"
                  className={`w-full px-3.5 py-2.5 pr-10 border rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400 ${
                    confirm.length > 0 && !pwMatch
                      ? "border-red-300 focus:ring-red-400 focus:border-red-400"
                      : "border-slate-200"
                  }`}
                  style={{ "--tw-ring-color": confirm.length > 0 && !pwMatch ? undefined : "var(--auth-gold)" } as React.CSSProperties}
                />
                <button
                  type="button"
                  tabIndex={-1}
                  onClick={() => setShowConfirm(!showConfirm)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition"
                >
                  {showConfirm ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              {confirm.length > 0 && !pwMatch && (
                <p className="mt-1 text-xs text-red-500">Lozinke se ne podudaraju</p>
              )}
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading || !pwValid || !pwMatch}
              className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed"
              style={{
                background: "var(--auth-navy)",
                boxShadow: "0 4px 14px rgba(27,42,74,0.4)",
              }}
            >
              {loading ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <KeyRound className="h-4 w-4" />
              )}
              {loading ? "Spremanje..." : "Spremi novu lozinku"}
            </button>
          </form>
        </div>
      </div>
    </>
  );
}
