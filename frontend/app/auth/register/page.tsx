"use client";

import { useState } from "react";
import Link from "next/link";
import {
  Eye,
  EyeOff,
  Loader2,
  Scale,
  AlertCircle,
  CheckCircle,
  Check,
  X,
  Send,
  Shield,
  Building2,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import {
  isEmailValid,
  isPasswordValid,
  validatePassword,
} from "@/lib/auth-validation";

// ── Google Logo SVG ──
function GoogleLogo() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24">
      <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/>
      <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
      <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
      <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
    </svg>
  );
}

export default function RegisterPage() {
  const supabase = createSupabaseBrowser();

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [agreed, setAgreed] = useState(false);

  // ── Firm fields ──
  const [firmName, setFirmName] = useState("");
  const [firmOib, setFirmOib] = useState("");

  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const checks = validatePassword(password);
  const pwValid = isPasswordValid(password);

  // ── Email/Password Register ──
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError("Unesite ime i prezime.");
      return;
    }
    if (!firmName.trim()) {
      setError("Unesite naziv odvjetničkog ureda.");
      return;
    }
    if (firmOib.trim() && (firmOib.trim().length !== 11 || !/^\d{11}$/.test(firmOib.trim()))) {
      setError("OIB mora imati točno 11 znamenki.");
      return;
    }
    if (!isEmailValid(email)) {
      setError("Unesite ispravnu email adresu.");
      return;
    }
    if (!pwValid) {
      setError("Lozinka ne zadovoljava sigurnosne zahtjeve.");
      return;
    }
    if (!agreed) {
      setError("Morate prihvatiti uvjete korištenja.");
      return;
    }

    setLoading(true);
    try {
      // Firm details travel as signup metadata. The `handle_new_user` DB trigger
      // (migration 015) reads them and creates the firm + profile — for email
      // signup and Google alike. Nothing here depends on a follow-up HTTP call.
      const { error: authError } = await supabase.auth.signUp({
        email,
        password,
        options: {
          data: {
            full_name: name.trim(),
            firm_name: firmName.trim(),
            firm_oib: firmOib.trim() || null,
          },
          emailRedirectTo: `${process.env.NEXT_PUBLIC_SITE_URL || window.location.origin}/auth/callback`,
        },
      });

      // ANTI-ENUMERATION: an existing address must look exactly like a new one.
      // Supabase already sends "someone tried to register your address" to a
      // taken email, so the real owner still learns about it — the person at
      // the form does not.
      if (authError) {
        if (authError.message.toLowerCase().includes("already registered")) {
          setSuccess(true);
          return;
        }
        setError("Registracija trenutno nije moguća. Pokušajte ponovo.");
        return;
      }

      setSuccess(true);
    } catch {
      setError("Greška pri povezivanju sa serverom. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  // ── Google OAuth ──
  const handleGoogle = async () => {
    setError(null);
    setGoogleLoading(true);
    try {
      const { error: authError } = await supabase.auth.signInWithOAuth({
        provider: "google",
        options: {
          redirectTo: `${process.env.NEXT_PUBLIC_SITE_URL || window.location.origin}/auth/callback`,
        },
      });
      if (authError) throw authError;
    } catch {
      setError("Greška pri registraciji putem Googlea.");
      setGoogleLoading(false);
    }
  };

  const isDisabled = loading || googleLoading;

  // ── Success state ──
  if (success) {
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
          <h2 className="font-serif text-lg font-semibold text-slate-900 mb-2">Zahtjev zaprimljen</h2>
          <p className="text-sm text-slate-500 mb-4 leading-relaxed">
            Poslali smo verifikacijski link na{" "}
            <span className="font-semibold text-slate-700">{email}</span>.
            <br />
            Kliknite na link u emailu da aktivirate račun.
          </p>
          <div className="flex items-center justify-center gap-2 mb-4 px-4 py-2.5 rounded-lg" style={{ background: "var(--auth-gold-muted)" }}>
            <Shield className="h-3.5 w-3.5 shrink-0" style={{ color: "var(--auth-gold)" }} />
            <p className="text-xs text-slate-600">
              Vaš zahtjev podliježe ručnoj verifikaciji od strane tima.
            </p>
          </div>
          <p className="text-xs text-slate-400 mb-6">
            Ne vidite email? Provjerite spam/junk folder.
          </p>
          <Link
            href="/auth/login"
            className="inline-flex items-center gap-2 text-white text-sm font-semibold px-6 py-2.5 rounded-xl transition shadow-lg"
            style={{ background: "var(--auth-navy)", boxShadow: "0 4px 14px rgba(27,42,74,0.4)" }}
          >
            Natrag na prijavu
          </Link>
        </div>
      </>
    );
  }

  // ── Registration form ──
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
          <h2 className="font-serif text-lg font-semibold text-slate-900 mb-1">Zatražite pristup uredu</h2>
          <p className="text-sm text-slate-500">Kreirajte račun za PravoTek platformu</p>
        </div>

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
          {/* Error */}
          {error && (
            <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 animate-in fade-in duration-200">
              <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
              <span>{error}</span>
            </div>
          )}

          {/* Google */}
          <button
            onClick={handleGoogle}
            disabled={isDisabled}
            className="w-full flex items-center justify-center gap-3 bg-white border border-slate-200 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed text-sm font-medium text-slate-700 py-2.5 rounded-xl transition shadow-sm"
          >
            {googleLoading ? <Loader2 className="h-5 w-5 animate-spin text-slate-400" /> : <GoogleLogo />}
            Registracija putem Googlea
          </button>

          {/* Divider */}
          <div className="flex items-center gap-3">
            <div className="flex-1 h-px bg-slate-200" />
            <span className="text-xs text-slate-400">ili s emailom</span>
            <div className="flex-1 h-px bg-slate-200" />
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Name */}
            <div>
              <label htmlFor="name" className="block text-xs font-semibold text-slate-700 mb-1.5">
                Ime i prezime
              </label>
              <input
                id="name"
                type="text"
                autoComplete="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={isDisabled}
                placeholder="Ivan Horvat"
                className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
                style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
              />
            </div>

            {/* Law Firm Section */}
            <div className="rounded-xl p-4 space-y-3" style={{ background: "var(--auth-gold-muted)" }}>
              <div className="flex items-center gap-2 mb-1">
                <Building2 className="h-3.5 w-3.5" style={{ color: "var(--auth-gold)" }} />
                <span className="text-xs font-semibold text-slate-600">Odvjetnički ured</span>
              </div>

              {/* Firm Name */}
              <div>
                <label htmlFor="firmName" className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Naziv ureda <span className="text-red-400">*</span>
                </label>
                <input
                  id="firmName"
                  type="text"
                  value={firmName}
                  onChange={(e) => setFirmName(e.target.value)}
                  disabled={isDisabled}
                  placeholder="Odvjetnički ured Horvat"
                  className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400 bg-white"
                  style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
                />
              </div>

              {/* Firm OIB */}
              <div>
                <label htmlFor="firmOib" className="block text-xs font-semibold text-slate-700 mb-1.5">
                  OIB ureda <span className="text-slate-400 font-normal">(opcionalno)</span>
                </label>
                <input
                  id="firmOib"
                  type="text"
                  inputMode="numeric"
                  maxLength={11}
                  value={firmOib}
                  onChange={(e) => setFirmOib(e.target.value.replace(/\D/g, "").slice(0, 11))}
                  disabled={isDisabled}
                  placeholder="12345678901"
                  className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400 bg-white"
                  style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
                />
              </div>
            </div>

            {/* Email */}
            <div>
              <label htmlFor="email" className="block text-xs font-semibold text-slate-700 mb-1.5">
                Email adresa
              </label>
              <input
                id="email"
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                disabled={isDisabled}
                placeholder="ime@odvjetnik.hr"
                className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
                style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
              />
            </div>

            {/* Password */}
            <div>
              <label htmlFor="password" className="block text-xs font-semibold text-slate-700 mb-1.5">
                Lozinka
              </label>
              <div className="relative">
                <input
                  id="password"
                  type={showPw ? "text" : "password"}
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={isDisabled}
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

            {/* Terms */}
            <label className="flex items-start gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                checked={agreed}
                onChange={(e) => setAgreed(e.target.checked)}
                disabled={isDisabled}
                className="mt-0.5 h-4 w-4 rounded border-slate-300 transition"
                style={{ accentColor: "var(--auth-navy)" }}
              />
              <span className="text-xs text-slate-500 leading-relaxed">
                Prihvaćam{" "}
                <a href="/uvjeti" target="_blank" className="font-medium underline underline-offset-2 hover:opacity-80 transition" style={{ color: "var(--auth-navy)" }}>Uvjete korištenja</a> i{" "}
                <a href="/privatnost" target="_blank" className="font-medium underline underline-offset-2 hover:opacity-80 transition" style={{ color: "var(--auth-navy)" }}>Politiku privatnosti</a>
              </span>
            </label>

            {/* Manual verification note */}
            <div className="flex items-center gap-2 px-3 py-2.5 rounded-lg" style={{ background: "var(--auth-gold-muted)" }}>
              <Shield className="h-3.5 w-3.5 shrink-0" style={{ color: "var(--auth-gold)" }} />
              <p className="text-[11px] text-slate-500 leading-relaxed">
                Svi zahtjevi podliježu ručnoj verifikaciji. Odobrenje pristupa obično unutar 24 sata.
              </p>
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={isDisabled || !pwValid || !agreed}
              className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed"
              style={{
                background: "var(--auth-navy)",
                boxShadow: "0 4px 14px rgba(27,42,74,0.4)",
              }}
            >
              {loading ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Send className="h-4 w-4" />
              )}
              {loading ? "Slanje zahtjeva..." : "Pošalji zahtjev"}
            </button>
          </form>
        </div>
      </div>

      {/* Login link */}
      <p className="text-center text-sm text-slate-400 mt-6">
        Već imate račun?{" "}
        <Link
          href="/auth/login"
          className="font-semibold transition"
          style={{ color: "var(--auth-gold)" }}
        >
          Prijavite se
        </Link>
      </p>
    </>
  );
}
