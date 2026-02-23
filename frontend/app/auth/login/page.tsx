"use client";

import { useState, useEffect, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Eye,
  EyeOff,
  Loader2,
  LogIn,
  Scale,
  AlertCircle,
  Lock,
  Shield,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { API_BASE } from "@/lib/api";
import {
  isEmailValid,
  isLockedOut,
  recordFailedAttempt,
  clearAttempts,
  attemptsRemaining,
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

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginContent />
    </Suspense>
  );
}

function LoginContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const supabase = createSupabaseBrowser();
  const idleLogout = searchParams.get("reason") === "idle";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);

  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 2FA state
  const [twoFAPending, setTwoFAPending] = useState(false);
  const [twoFAToken, setTwoFAToken] = useState("");
  const [twoFAUserId, setTwoFAUserId] = useState<string | null>(null);
  const [twoFALoading, setTwoFALoading] = useState(false);

  // Rate limiting
  const [lockout, setLockout] = useState<{ locked: boolean; remainingSeconds: number }>({ locked: false, remainingSeconds: 0 });

  useEffect(() => {
    setLockout(isLockedOut());
    const iv = setInterval(() => setLockout(isLockedOut()), 1000);
    return () => clearInterval(iv);
  }, []);

  // ── Email/Password Login ──
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (lockout.locked) {
      setError(`Previše neuspješnih pokušaja. Pokušajte za ${Math.ceil(lockout.remainingSeconds / 60)} min.`);
      return;
    }

    if (!isEmailValid(email)) {
      setError("Unesite ispravnu email adresu.");
      return;
    }
    if (!password) {
      setError("Unesite lozinku.");
      return;
    }

    setLoading(true);
    try {
      const { data, error: authError } = await supabase.auth.signInWithPassword({ email, password });

      if (authError) {
        recordFailedAttempt();
        const remaining = attemptsRemaining();
        if (remaining <= 0) {
          setError("Račun je privremeno zaključan. Pokušajte za 15 minuta.");
        } else if (authError.message.toLowerCase().includes("email not confirmed")) {
          setError("Email adresa još nije potvrđena. Provjerite inbox.");
        } else {
          setError(`Pogrešna email adresa ili lozinka. Preostalo pokušaja: ${remaining}`);
        }
        return;
      }

      clearAttempts();

      // ── Check if 2FA is enabled for this user ──
      const userId = data.user?.id;
      if (userId) {
        try {
          const apiKey = process.env.NEXT_PUBLIC_API_KEY;
          const headers: Record<string, string> = {};
          if (apiKey) headers["X-API-Key"] = apiKey;

          const res = await fetch(`${API_BASE}/api/2fa/status?userId=${encodeURIComponent(userId)}`, { headers });
          if (res.ok) {
            const status = await res.json();
            if (status.enabled) {
              // 2FA is required — show token input
              setTwoFAPending(true);
              setTwoFAUserId(userId);
              setLoading(false);
              return;
            }
          }
        } catch {
          // If 2FA check fails, allow login (don't lock out user due to backend issue)
        }
      }

      router.push("/");
      router.refresh();
    } catch {
      setError("Greška pri povezivanju sa serverom. Pokušajte ponovo.");
    } finally {
      setLoading(false);
    }
  };

  // ── 2FA Token Validation ──
  const handle2FAVerify = async () => {
    setError(null);
    if (!twoFAToken || !twoFAUserId) {
      setError("Unesite 2FA kod.");
      return;
    }

    setTwoFALoading(true);
    try {
      const apiKey = process.env.NEXT_PUBLIC_API_KEY;
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (apiKey) headers["X-API-Key"] = apiKey;

      const res = await fetch(`${API_BASE}/api/2fa/validate`, {
        method: "POST",
        headers,
        body: JSON.stringify({ user_id: twoFAUserId, token: twoFAToken }),
      });
      const data = await res.json();

      if (!data.success) {
        setError(data.message || "Nevažeći 2FA kod.");
        setTwoFAToken("");
        return;
      }

      // 2FA valid — complete login
      router.push("/");
      router.refresh();
    } catch {
      setError("Greška pri provjeri 2FA koda.");
    } finally {
      setTwoFALoading(false);
    }
  };

  // ── Cancel 2FA — sign out and return to login ──
  const cancel2FA = async () => {
    await supabase.auth.signOut();
    setTwoFAPending(false);
    setTwoFAUserId(null);
    setTwoFAToken("");
    setError(null);
  };

  // ── Google OAuth ──
  const handleGoogle = async () => {
    setError(null);
    setGoogleLoading(true);
    try {
      const { error: authError } = await supabase.auth.signInWithOAuth({
        provider: "google",
        options: {
          redirectTo: `${window.location.origin}/auth/callback`,
        },
      });
      if (authError) throw authError;
    } catch {
      setError("Greška pri prijavi putem Googlea.");
      setGoogleLoading(false);
    }
  };

  const isDisabled = loading || googleLoading || lockout.locked;

  // ═══════════ UNIFIED LOGIN (2-STEP WITHIN SINGLE CARD) ═══════════
  return (
    <>
      {/* Scoped keyframes for step transitions */}
      <style>{`
        @keyframes loginFadeSlideIn {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        .login-step-enter {
          animation: loginFadeSlideIn 0.4s ease-out both;
        }
      `}</style>

      {/* Logo */}
      <div className="flex items-center justify-center gap-3 mb-8">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center shadow-lg transition-all duration-500"
          style={{ background: "var(--auth-navy)", boxShadow: "0 4px 20px rgba(201,168,76,0.15)" }}
        >
          {twoFAPending ? (
            <Shield className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
          ) : (
            <Scale className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
          )}
        </div>
        <div>
          <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p
            className="text-[10px] uppercase tracking-[0.2em] transition-all duration-300"
            style={{ color: "var(--auth-gold)" }}
          >
            {twoFAPending ? "Dvofaktorska Provjera" : "Pravna Inteligencija"}
          </p>
        </div>
      </div>

      {/* Card — single container for both steps */}
      <div
        className="rounded-2xl shadow-2xl overflow-hidden"
        style={{
          background: "var(--auth-card-bg)",
          border: "1px solid var(--auth-card-border)",
          boxShadow: "0 25px 50px -12px rgba(0,0,0,0.4), 0 0 0 1px rgba(201,168,76,0.1)",
        }}
      >
        {twoFAPending ? (
          /* ── STEP 2: 2FA Token Input ── */
          <div key="step-2fa" className="login-step-enter">
            {/* Header */}
            <div className="px-6 pt-6 pb-2 sm:px-8 sm:pt-8">
              <div className="flex items-center gap-2 mb-1">
                <div className="flex items-center justify-center w-5 h-5 rounded-full text-[10px] font-bold text-white" style={{ background: "var(--auth-gold)" }}>2</div>
                <h2 className="font-serif text-lg font-semibold text-slate-900">
                  Dvofaktorska autentifikacija
                </h2>
              </div>
              <p className="text-sm text-slate-500">
                Unesite kod iz vaše authenticator aplikacije
              </p>
            </div>

            {/* Body */}
            <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
              {/* Error */}
              {error && (
                <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 login-step-enter">
                  <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
                  <span>{error}</span>
                </div>
              )}

              {/* Icon + Input */}
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
                  value={twoFAToken}
                  onChange={(e) => setTwoFAToken(e.target.value.replace(/[^A-Za-z0-9]/g, "").slice(0, 8))}
                  placeholder="000000"
                  className="w-48 text-center border border-slate-200 rounded-xl px-4 py-3 text-xl font-mono tracking-[0.3em] text-slate-900 placeholder:text-slate-300 focus:ring-2 outline-none transition"
                  style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
                  autoFocus
                  onKeyDown={(e) => { if (e.key === "Enter") handle2FAVerify(); }}
                />

                <p className="text-xs text-slate-400 text-center leading-relaxed">
                  Unesite 6-znamenkasti kod iz aplikacije<br />
                  ili 8-znakovni kod za oporavak
                </p>
              </div>

              {/* Submit */}
              <button
                onClick={handle2FAVerify}
                disabled={twoFALoading || !twoFAToken}
                className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed hover:opacity-90"
                style={{
                  background: "var(--auth-navy)",
                  boxShadow: "0 4px 14px rgba(27,42,74,0.4)",
                }}
              >
                {twoFALoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Shield className="h-4 w-4" />
                )}
                {twoFALoading ? "Provjera..." : "Potvrdi prijavu"}
              </button>

              {/* Back link */}
              <button
                onClick={cancel2FA}
                className="w-full flex items-center justify-center gap-1.5 text-xs font-medium transition py-2 rounded-lg hover:bg-slate-50"
                style={{ color: "var(--auth-gold)" }}
              >
                ← Povratak nazad
              </button>
            </div>
          </div>
        ) : (
          /* ── STEP 1: Email & Password ── */
          <div key="step-login" className="login-step-enter">
            {/* Header */}
            <div className="px-6 pt-6 pb-2 sm:px-8 sm:pt-8">
              <h2 className="font-serif text-lg font-semibold text-slate-900 mb-1">Dobrodošli natrag</h2>
              <p className="text-sm text-slate-500">Prijavite se na svoj račun</p>
            </div>

            {/* Body */}
            <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
              {/* Idle logout notice */}
              {idleLogout && !error && (
                <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-xl p-3 text-sm text-amber-700 login-step-enter">
                  <Lock className="h-4 w-4 shrink-0 mt-0.5 text-amber-500" />
                  <span>Automatski ste odjavljeni zbog neaktivnosti (15 min). Prijavite se ponovo.</span>
                </div>
              )}

              {/* Error */}
              {error && (
                <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 login-step-enter">
                  {lockout.locked ? (
                    <Lock className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
                  ) : (
                    <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-500" />
                  )}
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
                Prijava putem Googlea
              </button>

              {/* Divider */}
              <div className="flex items-center gap-3">
                <div className="flex-1 h-px bg-slate-200" />
                <span className="text-xs text-slate-400">ili nastavite s emailom</span>
                <div className="flex-1 h-px bg-slate-200" />
              </div>

              {/* Form */}
              <form onSubmit={handleSubmit} className="space-y-4">
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
                  <div className="flex items-center justify-between mb-1.5">
                    <label htmlFor="password" className="block text-xs font-semibold text-slate-700">
                      Lozinka
                    </label>
                    <Link
                      href="/auth/forgot-password"
                      className="text-xs font-medium transition"
                      style={{ color: "var(--auth-gold)" }}
                    >
                      Zaboravili lozinku?
                    </Link>
                  </div>
                  <div className="relative">
                    <input
                      id="password"
                      type={showPw ? "text" : "password"}
                      autoComplete="current-password"
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
                </div>

                {/* Submit */}
                <button
                  type="submit"
                  disabled={isDisabled}
                  className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed"
                  style={{
                    background: "var(--auth-navy)",
                    boxShadow: "0 4px 14px rgba(27,42,74,0.4)",
                  }}
                >
                  {loading ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <LogIn className="h-4 w-4" />
                  )}
                  {loading ? "Prijavljivanje..." : "Prijavi se"}
                </button>
              </form>

              {/* 2FA recommendation */}
              <div className="flex items-center gap-2 pt-1">
                <Shield className="h-3.5 w-3.5 shrink-0" style={{ color: "var(--auth-gold)" }} />
                <p className="text-[11px] text-slate-400 leading-relaxed">
                  Preporučujemo aktiviranje dvofaktorske autentifikacije (2FA) u postavkama računa za dodatnu sigurnost.
                </p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Footer — contextual to current step */}
      {twoFAPending ? (
        <div className="flex items-center justify-center gap-2 mt-6 text-xs text-slate-500 login-step-enter">
          <Shield className="h-3.5 w-3.5" style={{ color: "var(--auth-gold-muted, #B8A060)" }} />
          <span>Zaštićeno dvofaktorskom autentifikacijom</span>
        </div>
      ) : (
        <>
          <p className="text-center text-sm text-slate-400 mt-6">
            Nemate račun?{" "}
            <Link
              href="/auth/register"
              className="font-semibold transition"
              style={{ color: "var(--auth-gold)" }}
            >
              Zatražite pristup
            </Link>
          </p>
          <div className="flex items-center justify-center gap-2 mt-4 text-xs text-slate-500">
            <Lock className="h-3.5 w-3.5 text-slate-500" />
            <span>
              Kriptirani prijenos podataka (TLS 1.3). Obrada sukladna GDPR-u i odvjetničkoj tajni.
            </span>
          </div>
        </>
      )}
    </>
  );
}
