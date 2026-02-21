"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Eye,
  EyeOff,
  Loader2,
  LogIn,
  Scale,
  AlertCircle,
  Lock,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
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
  const router = useRouter();
  const supabase = createSupabaseBrowser();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);

  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
      const { error: authError } = await supabase.auth.signInWithPassword({ email, password });

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
      router.push("/");
      router.refresh();
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

  return (
    <>
      {/* Logo */}
      <div className="flex items-center justify-center gap-3 mb-8">
        <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-600/25">
          <Scale className="h-5 w-5 text-white" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-white tracking-tight">PravoTek</h1>
          <p className="text-[10px] text-slate-400 uppercase tracking-widest">Pravna Inteligencija</p>
        </div>
      </div>

      {/* Card */}
      <div className="bg-white rounded-2xl shadow-2xl shadow-black/20 border border-slate-200 overflow-hidden">
        <div className="px-6 pt-6 pb-2 sm:px-8 sm:pt-8">
          <h2 className="text-lg font-bold text-slate-900 mb-1">Dobrodošli natrag</h2>
          <p className="text-sm text-slate-500">Prijavite se na svoj račun</p>
        </div>

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
          {/* Error */}
          {error && (
            <div className="flex items-start gap-2 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700 animate-in fade-in duration-200">
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
            className="w-full flex items-center justify-center gap-3 bg-white border border-slate-300 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed text-sm font-medium text-slate-700 py-2.5 rounded-xl transition shadow-sm"
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
                placeholder="ime@primjer.hr"
                className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
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
                  className="text-xs text-blue-600 hover:text-blue-700 font-medium transition"
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
                  className="w-full px-3.5 py-2.5 pr-10 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
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
              className="w-full flex items-center justify-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 disabled:cursor-not-allowed text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-sm shadow-blue-600/25"
            >
              {loading ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <LogIn className="h-4 w-4" />
              )}
              {loading ? "Prijavljivanje..." : "Prijavi se"}
            </button>
          </form>
        </div>
      </div>

      {/* Register link */}
      <p className="text-center text-sm text-slate-400 mt-6">
        Nemate račun?{" "}
        <Link href="/auth/register" className="text-blue-400 hover:text-blue-300 font-semibold transition">
          Registrirajte se
        </Link>
      </p>
    </>
  );
}
