"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Loader2, Shield, ServerCrash, RefreshCw } from "lucide-react";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import type { UnknownReason } from "@/lib/second-factor";

/**
 * Shown when the security check could not be completed — the backend refused the
 * token, errored, or never answered.
 *
 * Deliberately separate from /auth/2fa. Both are dead ends by design, but only
 * one of them is the user's to solve: there is no code to type here, and asking
 * for one is exactly how this failure used to present itself.
 */
export default function GateUnavailablePage() {
  return (
    <Suspense fallback={null}>
      <GateUnavailableContent />
    </Suspense>
  );
}

const REASONS: Record<UnknownReason, string> = {
  "no-session": "Vaša prijava nije potpuna — nedostaje pristupni token.",
  rejected: "Poslužitelj nije prihvatio vašu prijavu.",
  backend: "Poslužitelj je javio grešku pri provjeri.",
  unreachable: "Poslužitelj se ne javlja.",
  malformed: "Poslužitelj je vratio neočekivan odgovor.",
};

function describe(raw: string | null): string {
  return (raw && REASONS[raw as UnknownReason]) || "Provjera nije uspjela iz nepoznatog razloga.";
}

/**
 * When to ask again, in seconds from arriving here.
 *
 * The failures this screen exists for are short: a backend still waking up, or
 * one that has not yet fetched Supabase's signing keys. Both clear in seconds.
 * Leaving the user to guess when to press a button meant they pressed it once,
 * too early, and left — so the page does the waiting for them.
 *
 * It stops after the last delay rather than polling forever: past a minute this
 * is not a blip, and a page quietly retrying into an outage is just load.
 */
const RETRY_DELAYS_SECONDS = [3, 8, 20, 45];

type GateStatus = { outcome: "required" | "not-enrolled" } | { outcome: "unknown"; reason: UnknownReason };

function GateUnavailableContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [retrying, setRetrying] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [secondsLeft, setSecondsLeft] = useState(RETRY_DELAYS_SECONDS[0]);
  const [reason, setReason] = useState<string | null>(searchParams.get("razlog"));
  const cancelled = useRef(false);

  const next = searchParams.get("next");
  const destination = next && next.startsWith("/") ? next : "/";

  const goThrough = useCallback(() => {
    setRetrying(true);
    // A full navigation, so the middleware gate runs again from scratch rather
    // than reusing anything the client router has cached.
    window.location.href = destination;
  }, [destination]);

  const handleRetry = () => goThrough();

  /**
   * Re-asks the backend rather than just reloading. A reload that lands on the
   * same failure bounces straight back here, which looks to the user exactly
   * like the button doing nothing — the thing this screen was reported for.
   */
  const recheck = useCallback(async (): Promise<boolean> => {
    try {
      const response = await fetch("/api/2fa/gate-status", { cache: "no-store" });
      if (!response.ok) return false;

      const status = (await response.json()) as GateStatus;
      if (status.outcome !== "unknown") return true;

      setReason(status.reason);
      return false;
    } catch {
      return false;
    }
  }, []);

  useEffect(() => {
    cancelled.current = false;
    return () => {
      cancelled.current = true;
    };
  }, []);

  const exhausted = attempt >= RETRY_DELAYS_SECONDS.length;
  const stillTrying = !exhausted && !retrying;

  useEffect(() => {
    if (exhausted || retrying) return;

    const tick = setInterval(() => {
      setSecondsLeft((s) => (s <= 1 ? 0 : s - 1));
    }, 1000);

    const timer = setTimeout(async () => {
      if (cancelled.current) return;

      const resolved = await recheck();
      if (cancelled.current) return;

      if (resolved) {
        goThrough();
        return;
      }

      // Both counters advance here rather than in an effect body, so the next
      // window opens with its own delay already on the clock.
      const nextAttempt = attempt + 1;
      setAttempt(nextAttempt);
      setSecondsLeft(RETRY_DELAYS_SECONDS[nextAttempt] ?? 0);
    }, RETRY_DELAYS_SECONDS[attempt] * 1000);

    return () => {
      clearInterval(tick);
      clearTimeout(timer);
    };
  }, [attempt, exhausted, retrying, recheck, goThrough]);

  const handleSignOut = async () => {
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
            Sigurnosna Provjera
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
            Provjera trenutno nije moguća
          </h2>
          <p className="text-sm text-slate-500">{describe(reason)}</p>
        </div>

        <div className="px-6 pb-6 sm:px-8 sm:pb-8 space-y-5">
          <div className="flex flex-col items-center gap-4 py-2">
            <div
              className="w-16 h-16 rounded-2xl flex items-center justify-center"
              style={{ background: "var(--auth-navy)" }}
            >
              <ServerCrash className="h-8 w-8" style={{ color: "var(--auth-gold)" }} />
            </div>

            <p className="text-xs text-slate-500 text-center leading-relaxed max-w-xs">
              Ne možemo utvrditi je li na vašem računu uključena dvofaktorska
              provjera, pa vas iz opreza ne puštamo dalje.{" "}
              <strong className="text-slate-600">Nije potreban nikakav kod</strong> i
              niste ništa krivo napravili.
              {stillTrying ? (
                <> Sami pokušavamo ponovo — ne morate ništa raditi.</>
              ) : (
                <> Provjeru smo pokušali nekoliko puta bez uspjeha.</>
              )}
            </p>

            {stillTrying && (
              <p
                aria-live="polite"
                className="text-xs font-medium tabular-nums"
                style={{ color: "var(--auth-gold)" }}
              >
                {secondsLeft > 0 ? `Sljedeći pokušaj za ${secondsLeft} s` : "Provjeravam..."}
                {attempt > 0 && ` · pokušaj ${attempt + 1}/${RETRY_DELAYS_SECONDS.length}`}
              </p>
            )}
          </div>

          <button
            onClick={handleRetry}
            disabled={retrying}
            className="w-full flex items-center justify-center gap-2 text-white text-sm font-semibold py-2.5 rounded-xl transition shadow-lg disabled:opacity-60 disabled:cursor-not-allowed hover:opacity-90"
            style={{ background: "var(--auth-navy)", boxShadow: "0 4px 14px rgba(27,42,74,0.4)" }}
          >
            {retrying ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            {retrying ? "Pokušavam..." : "Pokušaj odmah"}
          </button>

          <button
            onClick={handleSignOut}
            className="w-full flex items-center justify-center gap-1.5 text-xs font-medium transition py-2 rounded-lg hover:bg-slate-50"
            style={{ color: "var(--auth-gold)" }}
          >
            ← Odjavi se
          </button>
        </div>
      </div>

      <div className="flex items-center justify-center gap-2 mt-6 text-xs text-slate-500">
        <Shield className="h-3.5 w-3.5" style={{ color: "var(--auth-gold-muted, #B8A060)" }} />
        <span>Pristup je zaustavljen iz sigurnosnih razloga</span>
      </div>
    </>
  );
}
