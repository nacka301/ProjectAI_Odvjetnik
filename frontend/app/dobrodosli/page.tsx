"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowRight,
  Building2,
  Calculator,
  Loader2,
  Scale,
  ShieldCheck,
} from "lucide-react";
import {
  loadOnboardingState,
  markOnboarded,
  saveFirmName,
  UNKNOWN_ONBOARDING,
  type OnboardingState,
} from "@/lib/onboarding";

/**
 * The first screen after registration — and the only screen in the app that a
 * user sees exactly once.
 *
 * Registration and sign-in used to end in the same place: `router.push("/")`, an
 * empty dashboard identical to the one a returning user sees on their fiftieth
 * visit. A lawyer arriving here for the first time had no name on the screen, no
 * indication of what to do, and six equally-weighted module cards to choose
 * between.
 *
 * So this page does three things and stops:
 *   1. says the person's name, so it is obvious the account exists and is theirs;
 *   2. collects the office name when Google registration could not carry it;
 *   3. offers ONE thing to do next.
 *
 * It is not a wizard. The actual walkthrough lives where the work happens, in
 * /troskovnik — a tour of the dashboard teaches someone the menu, not the job.
 */

/** "Ivan Horvat" → "Ivan". Falls back to the whole string, then to nothing. */
function firstName(fullName: string): string {
  return fullName.trim().split(/\s+/)[0] ?? "";
}

export default function WelcomePage() {
  const router = useRouter();

  const [state, setState] = useState<OnboardingState>(UNKNOWN_ONBOARDING);
  const [loading, setLoading] = useState(true);
  const [firmInput, setFirmInput] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const loaded = await loadOnboardingState();
      if (cancelled) return;
      setState(loaded);
      if (!loaded.firmNameIsPlaceholder) setFirmInput(loaded.firmName);
      setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  /**
   * Saves whatever the user filled in, marks the introduction done, and leaves.
   *
   * `markOnboarded` runs on BOTH buttons. Skipping is an answer, and asking
   * again tomorrow would be ignoring it.
   */
  const finish = useCallback(
    async (destination: string) => {
      setSaving(true);

      if (state.firmId && state.firmNameIsPlaceholder && firmInput.trim()) {
        await saveFirmName(state.firmId, firmInput);
      }
      await markOnboarded();

      router.push(destination);
      router.refresh();
    },
    [state.firmId, state.firmNameIsPlaceholder, firmInput, router],
  );

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  const name = firstName(state.fullName);

  // ── Somebody who already has an account pressed "Registracija putem Googlea" ──
  //
  // OAuth has no notion of "sign up" as distinct from "sign in": both buttons
  // issue the same signInWithOAuth call, so an existing user pressing the
  // registration button was silently signed in and dropped on the dashboard,
  // with nothing on screen acknowledging that they had asked for something else.
  // Landing here is what makes the difference sayable.
  if (state.isNew === false) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center px-4">
        <div className="w-full max-w-md bg-white rounded-2xl border border-slate-200 shadow-sm p-6 sm:p-8 text-center">
          <div className="w-14 h-14 rounded-full bg-slate-100 flex items-center justify-center mx-auto mb-4">
            <Scale className="h-6 w-6" style={{ color: "var(--auth-navy)" }} />
          </div>
          <h2 className="font-[family-name:var(--font-playfair)] text-lg font-semibold text-slate-900 mb-2">
            {name ? `Dobro došli natrag, ${name}.` : "Dobro došli natrag."}
          </h2>
          <p className="text-sm text-slate-500 mb-6 leading-relaxed">
            Ovaj račun već postoji, pa smo Vas prijavili umjesto da otvaramo novi.
            {state.firmName ? ` Radite u uredu: ${state.firmName}.` : ""}
          </p>
          <button
            onClick={() => {
              router.push("/");
              router.refresh();
            }}
            className="inline-flex items-center gap-2 text-white text-sm font-semibold px-6 py-2.5 rounded-xl transition shadow-lg"
            style={{ background: "var(--auth-navy)" }}
          >
            Nastavi na aplikaciju
            <ArrowRight className="h-4 w-4" />
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center px-4 py-10">
      <div className="w-full max-w-lg">
        {/* Mark */}
        <div className="flex items-center justify-center gap-3 mb-8">
          <div
            className="w-11 h-11 rounded-xl flex items-center justify-center shadow-sm"
            style={{ background: "var(--auth-navy)" }}
          >
            <Scale className="h-5 w-5" style={{ color: "var(--auth-gold)" }} />
          </div>
          <div>
            <h1 className="font-[family-name:var(--font-playfair)] text-xl font-bold tracking-tight text-slate-900">
              PravoTek
            </h1>
            <p
              className="text-[10px] uppercase tracking-[0.2em]"
              style={{ color: "var(--auth-gold)" }}
            >
              Pravna Inteligencija
            </p>
          </div>
        </div>

        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="px-6 pt-7 pb-2 sm:px-8">
            <h2 className="font-[family-name:var(--font-playfair)] text-2xl font-semibold text-slate-900 tracking-tight">
              {name ? `Dobro došli, ${name}.` : "Dobro došli."}
            </h2>
            <p className="text-sm text-slate-500 mt-2 leading-relaxed">
              Vaš račun je otvoren. PravoTek računa odvjetničke troškovnike prema
              službenoj Tarifi — odaberete radnje, bodovi i iznosi se izvode sami.
            </p>
          </div>

          <div className="px-6 pb-7 sm:px-8 space-y-6 pt-5">
            {/* Office name — only when signup could not carry it (Google). */}
            {state.firmNameIsPlaceholder && (
              <div
                className="rounded-xl p-4 space-y-2"
                style={{ background: "var(--auth-gold-muted)" }}
              >
                <div className="flex items-center gap-2">
                  <Building2 className="h-3.5 w-3.5" style={{ color: "var(--auth-gold)" }} />
                  <label htmlFor="firm" className="text-xs font-semibold text-slate-700">
                    Naziv Vašeg ureda
                  </label>
                </div>
                <input
                  id="firm"
                  type="text"
                  value={firmInput}
                  onChange={(e) => setFirmInput(e.target.value)}
                  disabled={saving}
                  placeholder="Odvjetnički ured Horvat"
                  className="w-full bg-white px-3.5 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder:text-slate-400 focus:ring-2 outline-none transition disabled:bg-slate-50"
                  style={{ "--tw-ring-color": "var(--auth-gold)" } as React.CSSProperties}
                />
                <p className="text-[11px] text-slate-500 leading-relaxed">
                  Ispisuje se u zaglavlju svakog troškovnika. Njega, OIB, adresu i
                  IBAN možete kasnije dopuniti u Postavkama → Odvjetnički ured.
                </p>
              </div>
            )}

            {/* The one thing to do next. */}
            <button
              onClick={() => finish("/troskovnik?vodic=1")}
              disabled={saving}
              className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-3 rounded-xl transition shadow-lg disabled:opacity-60"
              style={{
                background: "var(--auth-navy)",
                boxShadow: "0 4px 14px rgba(27,42,74,0.35)",
              }}
            >
              {saving ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Calculator className="h-4 w-4" />
              )}
              Izradite prvi troškovnik
              {!saving && <ArrowRight className="h-4 w-4" />}
            </button>

            <div className="flex items-center justify-between">
              <button
                onClick={() => finish("/")}
                disabled={saving}
                className="text-xs text-slate-400 hover:text-slate-600 transition disabled:opacity-60"
              >
                Preskoči — idem sam razgledati
              </button>
              <span className="text-[11px] text-slate-400">Traje oko 2 minute</span>
            </div>

            <div className="flex items-start gap-2 pt-1 border-t border-slate-100">
              <ShieldCheck
                className="h-3.5 w-3.5 shrink-0 mt-3"
                style={{ color: "var(--auth-gold)" }}
              />
              <p className="text-[11px] text-slate-400 leading-relaxed mt-2.5">
                Podaci Vašeg ureda odvojeni su na razini baze i vidljivi samo
                članovima Vašeg ureda. Preporučujemo da u Postavkama uključite
                dvofaktorsku prijavu.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
