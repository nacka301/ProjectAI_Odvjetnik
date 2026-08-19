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
  Info,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { safeNextPath } from "@/lib/auth-redirect";
import GoogleSignIn from "@/components/GoogleSignIn";
import {
  isEmailValid,
  isLockedOut,
  recordFailedAttempt,
  clearAttempts,
  attemptsRemaining,
} from "@/lib/auth-validation";

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

  // The middleware already records where the user was headed when it stopped
  // them. Nothing read it, so everyone landed on the dashboard and had to find
  // their way back to the page they had actually asked for.
  const nextPath = safeNextPath(searchParams.get("next"));

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);

  const [loading, setLoading] = useState(false);
  // Owned by <GoogleSignIn />, mirrored here so the email form locks while a
  // Google sign-in is in flight — otherwise both could be submitted at once.
  const [googleLoading, setGoogleLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Set by any rejected password attempt. Drives the Google note in the render.
  const [credentialFailure, setCredentialFailure] = useState(false);

  // Rate limiting
  const [lockout, setLockout] = useState<{ locked: boolean; remainingSeconds: number }>({ locked: false, remainingSeconds: 0 });

  useEffect(() => {
    setLockout(isLockedOut());
    const iv = setInterval(() => setLockout(isLockedOut()), 1000);
    return () => clearInterval(iv);
  }, []);

  // Show error from callback route (e.g. expired reset link)
  useEffect(() => {
    const callbackError = searchParams.get("error");
    if (callbackError && callbackError !== "callback") {
      setError(decodeURIComponent(callbackError));
    } else if (callbackError === "callback") {
      setError("Autentifikacija nije uspjela. Pokušajte ponovo.");
    }
  }, [searchParams]);

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
      const { error: authError } = await supabase.auth.signInWithPassword({ email, password });

      if (authError) {
        // ANTI-ENUMERATION: one message for every failure mode. Distinguishing
        // "email not confirmed" from "wrong password" told an attacker which
        // lawyers have accounts here — for a legal app that is itself a leak.
        // The "you signed up with Google" hint is gone for the same reason;
        // if we want to keep helping that user, it has to go out by email.
        recordFailedAttempt();
        setCredentialFailure(true);
        const remaining = attemptsRemaining();
        setError(
          remaining <= 0
            ? "Račun je privremeno zaključan. Pokušajte za 15 minuta."
            : `Neispravan email ili lozinka. Preostalo pokušaja: ${remaining}`
        );
        return;
      }

      clearAttempts();

      // No 2FA check here any more. The gate lives in middleware.ts, so it also
      // covers Google sign-in and anyone who types a URL instead of following
      // the redirect — neither of which this component ever saw. If the account
      // has TOTP enabled, the navigation below lands on /auth/2fa.
      router.push(nextPath);
      router.refresh();
    } catch {
      setError("Greška pri povezivanju sa serverom. Pokušajte ponovo.");
    } finally {
      setLoading(false);
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
          <Scale className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
        </div>
        <div>
          <h1 className="font-serif text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p
            className="text-[10px] uppercase tracking-[0.2em]"
            style={{ color: "var(--auth-gold)" }}
          >
            Pravna Inteligencija
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

              {/* A Google-created account has no password, so signInWithPassword
                  answers "Invalid login credentials" — the same thing a typo
                  produces. The lawyer reads "wrong password", tries four more
                  times and hits the lockout, with nothing anywhere telling them
                  the account was never meant to have a password.

                  Shown after EVERY rejected attempt, deliberately. Showing it
                  only for Google accounts would answer "does this address have
                  an account here, and of what kind" — the exact question the
                  single generic error message exists to refuse. Unconditional,
                  it tells an attacker nothing and gives the one person who needs
                  it the only sentence that gets them in. */}
              {credentialFailure && (
                <div
                  className="flex items-start gap-2 rounded-xl p-3 text-sm login-step-enter"
                  style={{ background: "var(--auth-gold-muted)" }}
                >
                  <Info className="h-4 w-4 shrink-0 mt-0.5" style={{ color: "var(--auth-gold)" }} />
                  <span className="text-slate-600">
                    Ako ste račun otvorili preko Googlea, on nema lozinku —
                    prijavite se gumbom <strong className="font-semibold">Prijava putem Googlea</strong>.
                  </span>
                </div>
              )}

              {/* Google — renders Google's own button on Google's own origin,
                  so the consent screen names pravotek.com instead of the
                  Supabase project host. Falls back to the redirect flow if
                  their script never arrives. */}
              <GoogleSignIn
                mode="login"
                nextPath={nextPath}
                onError={setError}
                onBusyChange={setGoogleLoading}
                disabled={loading || lockout.locked}
              />

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
      </div>

      {/* Footer */}
      <p className="text-center text-sm text-slate-400 mt-6">
        Nemate račun?{" "}
        <Link
          href="/auth/register"
          className="font-semibold transition"
          style={{ color: "var(--auth-gold)" }}
        >
          Otvorite račun
        </Link>
      </p>
      <div className="flex items-center justify-center gap-2 mt-4 text-xs text-slate-500">
        <Lock className="h-3.5 w-3.5 text-slate-500" />
        <span>
          Kriptirani prijenos podataka (TLS 1.3). Obrada sukladna GDPR-u i odvjetničkoj tajni.
        </span>
      </div>
    </>
  );
}
