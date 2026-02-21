"use client";

import { useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  Loader2,
  Mail,
  Scale,
  AlertCircle,
  CheckCircle,
} from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { isEmailValid } from "@/lib/auth-validation";

export default function ForgotPasswordPage() {
  const supabase = createSupabaseBrowser();

  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!isEmailValid(email)) {
      setError("Unesite ispravnu email adresu.");
      return;
    }

    setLoading(true);
    try {
      const { error: authError } = await supabase.auth.resetPasswordForEmail(email, {
        redirectTo: `${window.location.origin}/auth/reset-password`,
      });

      if (authError) {
        setError(authError.message);
        return;
      }

      setSuccess(true);
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
          <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-600/25">
            <Scale className="h-5 w-5 text-white" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-white tracking-tight">PravoTek</h1>
            <p className="text-[10px] text-slate-400 uppercase tracking-widest">Pravna Inteligencija</p>
          </div>
        </div>

        <div className="bg-white rounded-2xl shadow-2xl shadow-black/20 border border-slate-200 p-6 sm:p-8 text-center">
          <div className="w-14 h-14 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <Mail className="h-7 w-7 text-blue-600" />
          </div>
          <h2 className="text-lg font-bold text-slate-900 mb-2">Provjerite email</h2>
          <p className="text-sm text-slate-500 mb-4 leading-relaxed">
            Ako postoji račun za{" "}
            <span className="font-semibold text-slate-700">{email}</span>,
            poslali smo link za promjenu lozinke.
          </p>
          <p className="text-xs text-slate-400 mb-6">
            Ne vidite email? Provjerite spam/junk folder.
          </p>
          <Link
            href="/auth/login"
            className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold px-6 py-2.5 rounded-xl transition shadow-sm"
          >
            <ArrowLeft className="h-4 w-4" />
            Natrag na prijavu
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
          <h2 className="text-lg font-bold text-slate-900 mb-1">Zaboravljena lozinka</h2>
          <p className="text-sm text-slate-500 leading-relaxed">
            Unesite email adresu i poslat ćemo Vam link za promjenu lozinke.
          </p>
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
                disabled={loading}
                placeholder="ime@primjer.hr"
                className="w-full px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition disabled:bg-slate-50 disabled:text-slate-400"
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full flex items-center justify-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 disabled:cursor-not-allowed text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-sm shadow-blue-600/25"
            >
              {loading ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Mail className="h-4 w-4" />
              )}
              {loading ? "Slanje..." : "Pošalji link za reset"}
            </button>
          </form>
        </div>
      </div>

      {/* Back to login */}
      <p className="text-center text-sm text-slate-400 mt-6">
        <Link href="/auth/login" className="text-blue-400 hover:text-blue-300 font-semibold transition inline-flex items-center gap-1">
          <ArrowLeft className="h-3.5 w-3.5" />
          Natrag na prijavu
        </Link>
      </p>
    </>
  );
}
