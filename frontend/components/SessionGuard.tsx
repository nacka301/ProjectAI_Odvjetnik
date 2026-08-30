"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { signOutCompletely } from "@/lib/sign-out";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { ShieldAlert } from "lucide-react";
import {
  ABSOLUTE_SESSION_MS,
  ACTIVE_REFRESH_MS,
  ACTIVITY_THROTTLE_MS,
  IDLE_TIMEOUT_MS,
  LAST_ACTIVITY_KEY,
  SESSION_START_KEY,
  TOKEN_REFRESH_SKEW_MS,
  WARNING_COUNTDOWN_SEC,
} from "@/lib/session-timeout";

// ═══════════════════════════════════════════════════════════
//  SESSION GUARD — Idle Warning + Auto-Logout
//
//  - Nakon IDLE_TIMEOUT_MS bez aktivnosti → modal s odbrojavanjem
//  - "Ostani prijavljen" resetira tajmer
//  - Odbrojavanje dođe do 0 → potpuna odjava → /auth/login?reason=idle
//  - Aktivnost = klik, tipka, scroll, dodir, pomak miša ILI uspješan
//    mrežni poziv. Dok god korisnik nešto radi, sesija se ne gasi.
//  - Dok je korisnik aktivan, Supabase token se tiho obnavlja da ne
//    istekne neovisno o idle-tajmeru.
//  - Apsolutni maksimum trajanja sesije: ABSOLUTE_SESSION_MS (12 h),
//    aktivnost ga NE produžuje.
//  - Cross-tab sinkronizacija preko localStorage.
//
//  Sve brojke žive u lib/session-timeout.ts — ovdje se ništa ne
//  podešava.
// ═══════════════════════════════════════════════════════════

const FETCH_WRAPPED_FLAG = "__pravotekSessionGuardWrapped";

export default function SessionGuard({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();

  const [showWarning, setShowWarning] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(WARNING_COUNTDOWN_SEC);

  // Sve promjenjivo drži se u ref-ovima da listeneri i tajmeri postavljeni
  // jednom (useEffect s praznim deps) uvijek gledaju SVJEŽE vrijednosti —
  // ranija verzija je preko zastarjelih closure vrijednosti znala prestati
  // resetirati tajmer.
  const idleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const refreshTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const lastActivityRef = useRef(Date.now());
  const showWarningRef = useRef(false);
  const handleStayRef = useRef<() => void>(() => {});

  useEffect(() => {
    showWarningRef.current = showWarning;
  }, [showWarning]);

  useEffect(() => {
    const supabase = createSupabaseBrowser();

    // ── Session start (apsolutni maksimum) ──
    // Mjeri se od prve prijave nakon zadnje odjave. Ključ se stvara samo ako ne
    // postoji — NE prepisuje se (supabase-js zna ispaliti "SIGNED_IN" i na
    // povratak fokusa na karticu; prepisivanje bi tada resetiralo 12 h prozor).
    let sessionStart = Date.now();
    const ensureSessionStart = () => {
      try {
        const stored = localStorage.getItem(SESSION_START_KEY);
        if (stored && parseInt(stored, 10)) {
          sessionStart = parseInt(stored, 10);
        } else {
          sessionStart = Date.now();
          localStorage.setItem(SESSION_START_KEY, String(sessionStart));
        }
      } catch {
        // localStorage nedostupan — session cap onda vrijedi samo za ovu karticu.
      }
    };
    ensureSessionStart();

    const clearCountdown = () => {
      if (countdownRef.current) {
        clearInterval(countdownRef.current);
        countdownRef.current = null;
      }
    };

    // ── Potpuna odjava ──
    const signOut = async () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      clearCountdown();
      if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
      try {
        localStorage.removeItem(LAST_ACTIVITY_KEY);
        localStorage.removeItem(SESSION_START_KEY);
      } catch {
        // ignore
      }
      await signOutCompletely();
      router.push("/auth/login?reason=idle");
      router.refresh();
    };

    const absoluteCapReached = () =>
      Date.now() - sessionStart > ABSOLUTE_SESSION_MS;

    // ── Odbrojavanje u modalu ──
    const startCountdown = () => {
      if (absoluteCapReached()) {
        void signOut();
        return;
      }
      setSecondsLeft(WARNING_COUNTDOWN_SEC);
      setShowWarning(true);
      showWarningRef.current = true;

      clearCountdown();
      countdownRef.current = setInterval(() => {
        setSecondsLeft((prev) => {
          if (prev <= 1) {
            clearCountdown();
            void signOut();
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    };

    const armIdleTimer = () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      idleTimerRef.current = setTimeout(startCountdown, IDLE_TIMEOUT_MS);
    };

    // ── Reset na aktivnost ──
    const registerActivity = (opts?: { force?: boolean }) => {
      const now = Date.now();

      if (absoluteCapReached()) {
        void signOut();
        return;
      }

      // Throttle rafala — ali nikad kad je modal otvoren (tada svaki signal
      // znači "korisnik je tu, poništi odjavu").
      if (
        !opts?.force &&
        !showWarningRef.current &&
        now - lastActivityRef.current < ACTIVITY_THROTTLE_MS
      ) {
        return;
      }
      lastActivityRef.current = now;

      try {
        localStorage.setItem(LAST_ACTIVITY_KEY, String(now));
      } catch {
        // ignore
      }

      if (showWarningRef.current) {
        clearCountdown();
        setShowWarning(false);
        showWarningRef.current = false;
      }

      armIdleTimer();
    };

    // ── "Ostani prijavljen" ──
    const handleStay = () => {
      registerActivity({ force: true });
      // Gurni i token refresh — korisnik je eksplicitno rekao da ostaje.
      void supabase.auth.getSession();
    };
    handleStayRef.current = handleStay;

    // ── Tihi refresh Supabase tokena dok je korisnik aktivan ──
    // `internalAuthCall` drži fetch-hook (niže) da NE broji naš vlastiti poziv
    // Supabaseu kao korisničku aktivnost — inače bi refresh tokena beskonačno
    // produžavao idle prozor korisniku koji ništa ne radi.
    let internalAuthCall = false;
    const maybeRefreshToken = async () => {
      if (typeof document !== "undefined" && document.visibilityState !== "visible")
        return;
      // Ako je korisnik već idle, ne produžuj mu sesiju kroz stražnja vrata.
      if (Date.now() - lastActivityRef.current > IDLE_TIMEOUT_MS) return;
      internalAuthCall = true;
      try {
        const {
          data: { session },
        } = await supabase.auth.getSession();
        if (!session) return;
        const expiresAtMs = (session.expires_at ?? 0) * 1000;
        if (expiresAtMs - Date.now() < TOKEN_REFRESH_SKEW_MS) {
          await supabase.auth.refreshSession();
        }
      } catch {
        // Mreža/refresh pukao — idle-tajmer i dalje čuva sesiju; sljedeća
        // navigacija (middleware) ili idući tik pokušat će ponovno.
      } finally {
        internalAuthCall = false;
      }
    };

    // ── Init ──
    // Istekla u drugoj kartici / dok je ova bila zatvorena?
    try {
      if (absoluteCapReached()) {
        void signOut();
        return;
      }
      const stored = localStorage.getItem(LAST_ACTIVITY_KEY);
      if (stored) {
        const elapsed = Date.now() - parseInt(stored, 10);
        if (elapsed > IDLE_TIMEOUT_MS + WARNING_COUNTDOWN_SEC * 1000) {
          void signOut();
          return;
        }
      }
    } catch {
      // ignore
    }

    armIdleTimer();
    refreshTimerRef.current = setInterval(maybeRefreshToken, ACTIVE_REFRESH_MS);

    // ── DOM aktivnost ──
    const onActivity = () => registerActivity();
    const events: (keyof WindowEventMap)[] = [
      "mousemove",
      "keydown",
      "click",
      "scroll",
      "touchstart",
    ];
    for (const ev of events) {
      window.addEventListener(ev, onActivity, { passive: true });
    }

    // ── Uspješan mrežni poziv = aktivnost ──
    // Svi backend pozivi idu kroz fetch(`${API_BASE}/...`); umjesto diranja
    // svakog poziva, omotamo window.fetch jednom.
    const w = window as typeof window & { [FETCH_WRAPPED_FLAG]?: boolean };
    let unwrapFetch: (() => void) | null = null;
    if (!w[FETCH_WRAPPED_FLAG]) {
      const original = window.fetch;
      const wrapped: typeof window.fetch = async (...args) => {
        const res = await original(...args);
        try {
          if (res.ok && !internalAuthCall) registerActivity();
        } catch {
          // ignore
        }
        return res;
      };
      window.fetch = wrapped;
      w[FETCH_WRAPPED_FLAG] = true;
      unwrapFetch = () => {
        if (window.fetch === wrapped) window.fetch = original;
        delete w[FETCH_WRAPPED_FLAG];
      };
    }

    // ── Cross-tab ──
    const onStorage = (e: StorageEvent) => {
      if (e.key === LAST_ACTIVITY_KEY && e.newValue) {
        lastActivityRef.current = parseInt(e.newValue, 10) || Date.now();
        armIdleTimer();
        if (showWarningRef.current) {
          clearCountdown();
          setShowWarning(false);
          showWarningRef.current = false;
        }
      }
      if (e.key === SESSION_START_KEY) {
        if (e.newValue) sessionStart = parseInt(e.newValue, 10) || sessionStart;
        // Odjava u drugoj kartici obriše ključ — prati je.
        else void signOut();
      }
    };
    window.addEventListener("storage", onStorage);

    // Vraćanje na karticu — provjeri je li sesija u međuvremenu istekla.
    const onVisibility = () => {
      if (document.visibilityState === "visible") {
        registerActivity();
        void maybeRefreshToken();
      }
    };
    document.addEventListener("visibilitychange", onVisibility);

    // ── Prijava / odjava ──
    // SessionGuard je u root layoutu i ne montira se ponovno pri odlasku na
    // /auth/login i natrag. Bez ovoga bi nakon ponovne prijave `sessionStart`
    // ostao od stare sesije (moguća trenutna odjava) ili bi ključ ostao obrisan.
    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((event) => {
      if (event === "SIGNED_OUT") {
        try {
          localStorage.removeItem(LAST_ACTIVITY_KEY);
          localStorage.removeItem(SESSION_START_KEY);
        } catch {
          // ignore
        }
        if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
        clearCountdown();
        return;
      }
      if (event === "SIGNED_IN") {
        ensureSessionStart(); // stvara ključ samo ako je obrisan (nakon odjave)
        lastActivityRef.current = Date.now();
        armIdleTimer();
      }
    });

    return () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      clearCountdown();
      if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
      for (const ev of events) window.removeEventListener(ev, onActivity);
      window.removeEventListener("storage", onStorage);
      document.removeEventListener("visibilitychange", onVisibility);
      subscription.unsubscribe();
      unwrapFetch?.();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <>
      {children}

      {/* ── Timeout Warning Modal ── */}
      {showWarning && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="mx-4 w-full max-w-md rounded-2xl bg-white p-8 shadow-2xl ring-1 ring-slate-200">
            {/* Icon */}
            <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-full bg-amber-100">
              <ShieldAlert className="h-7 w-7 text-amber-600" />
            </div>

            {/* Title */}
            <h2 className="text-center text-lg font-semibold text-slate-900">
              Uskoro ćete biti odjavljeni zbog neaktivnosti
            </h2>

            {/* Message */}
            <p className="mt-3 text-center text-sm text-slate-600">
              Zbog sigurnosti podataka, vaša sesija će isteći za{" "}
              <span className="font-bold text-amber-600">{secondsLeft}</span>{" "}
              {secondsLeft === 1
                ? "sekundu"
                : secondsLeft < 5
                  ? "sekunde"
                  : "sekundi"}
              .
            </p>

            {/* Progress bar */}
            <div className="mt-5 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
              <div
                className="h-full rounded-full bg-amber-500 transition-all duration-1000 ease-linear"
                style={{ width: `${(secondsLeft / WARNING_COUNTDOWN_SEC) * 100}%` }}
              />
            </div>

            {/* Action button */}
            <button
              onClick={() => handleStayRef.current()}
              className="mt-6 w-full rounded-lg bg-blue-600 px-4 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              Ostani prijavljen
            </button>

            <p className="mt-3 text-center text-xs text-slate-400">
              Ako ne odgovorite, bit ćete automatski odjavljeni.
            </p>
          </div>
        </div>
      )}
    </>
  );
}
