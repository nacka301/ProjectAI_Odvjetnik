"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { createSupabaseBrowser } from "@/lib/supabase/client";
import { ShieldAlert } from "lucide-react";

// ═══════════════════════════════════════════════════════════
//  SESSION GUARD — Idle Warning + Auto-Logout
//
//  - After 25 min idle → shows modal with 60-second countdown
//  - "Ostani prijavljen" resets the timer
//  - If countdown reaches 0 → Supabase signOut → /auth/login
//  - Cross-tab sync via localStorage
// ═══════════════════════════════════════════════════════════

const IDLE_MS = 25 * 60 * 1000;       // 25 minutes until warning
const COUNTDOWN_SEC = 60;              // 60 second countdown in modal
const THROTTLE_MS = 30_000;            // Activity event throttle
const STORAGE_KEY = "pravotek_last_activity";

export default function SessionGuard({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const idleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const lastActivityRef = useRef(Date.now());

  const [showWarning, setShowWarning] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(COUNTDOWN_SEC);

  // ── Sign out ──
  const signOut = useCallback(async () => {
    try {
      const supabase = createSupabaseBrowser();
      await supabase.auth.signOut();
    } catch {
      // ignore
    }
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
    router.push("/auth/login?reason=idle");
    router.refresh();
  }, [router]);

  // ── Stop countdown interval ──
  const clearCountdown = useCallback(() => {
    if (countdownRef.current) {
      clearInterval(countdownRef.current);
      countdownRef.current = null;
    }
  }, []);

  // ── Start the 60-second countdown modal ──
  const startCountdown = useCallback(() => {
    setSecondsLeft(COUNTDOWN_SEC);
    setShowWarning(true);

    clearCountdown();
    countdownRef.current = setInterval(() => {
      setSecondsLeft((prev) => {
        if (prev <= 1) {
          clearCountdown();
          signOut();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [clearCountdown, signOut]);

  // ── Reset idle timer (called on user activity or "stay" button) ──
  const resetTimer = useCallback(() => {
    const now = Date.now();

    // Throttle rapid events (unless dismissing the warning modal)
    if (!showWarning && now - lastActivityRef.current < THROTTLE_MS) return;
    lastActivityRef.current = now;

    try {
      localStorage.setItem(STORAGE_KEY, now.toString());
    } catch {
      // ignore
    }

    // Dismiss warning if visible
    if (showWarning) {
      clearCountdown();
      setShowWarning(false);
    }

    // Restart idle timer
    if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
    idleTimerRef.current = setTimeout(startCountdown, IDLE_MS);
  }, [showWarning, clearCountdown, startCountdown]);

  // ── "Stay logged in" button handler ──
  const handleStay = useCallback(() => {
    lastActivityRef.current = 0; // bypass throttle
    resetTimer();
  }, [resetTimer]);

  // ── Mount / unmount ──
  useEffect(() => {
    // Check if expired from another tab
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const elapsed = Date.now() - parseInt(stored, 10);
        if (elapsed > IDLE_MS + COUNTDOWN_SEC * 1000) {
          signOut();
          return;
        }
      }
    } catch {
      // ignore
    }

    // Start idle timer
    idleTimerRef.current = setTimeout(startCountdown, IDLE_MS);

    // Listen for activity
    const events: (keyof WindowEventMap)[] = [
      "mousemove",
      "keydown",
      "click",
      "scroll",
      "touchstart",
    ];
    for (const ev of events) {
      window.addEventListener(ev, resetTimer, { passive: true });
    }

    // Cross-tab sync
    const handleStorage = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY && e.newValue) {
        // Another tab was active — reset our timer + dismiss modal
        if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
        idleTimerRef.current = setTimeout(startCountdown, IDLE_MS);
        if (showWarning) {
          clearCountdown();
          setShowWarning(false);
        }
      }
    };
    window.addEventListener("storage", handleStorage);

    return () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      clearCountdown();
      for (const ev of events) {
        window.removeEventListener(ev, resetTimer);
      }
      window.removeEventListener("storage", handleStorage);
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
              Sesija uskoro istječe
            </h2>

            {/* Message */}
            <p className="mt-3 text-center text-sm text-slate-600">
              Zbog sigurnosti podataka, vaša sesija će isteći za{" "}
              <span className="font-bold text-amber-600">{secondsLeft}</span>{" "}
              {secondsLeft === 1 ? "sekundu" : secondsLeft < 5 ? "sekunde" : "sekundi"}.
            </p>

            {/* Progress bar */}
            <div className="mt-5 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
              <div
                className="h-full rounded-full bg-amber-500 transition-all duration-1000 ease-linear"
                style={{ width: `${(secondsLeft / COUNTDOWN_SEC) * 100}%` }}
              />
            </div>

            {/* Action button */}
            <button
              onClick={handleStay}
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
