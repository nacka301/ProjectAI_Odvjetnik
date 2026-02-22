"use client";

import { useEffect, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import { createSupabaseBrowser } from "@/lib/supabase/client";

// ═══════════════════════════════════════════════════════════
//  AUTO-LOGOUT PROVIDER — Idle Timeout (15 min)
//
//  Security measure for law offices: if a lawyer leaves
//  their screen unattended, the session auto-terminates
//  to prevent unauthorized access to confidential data.
//
//  Listens to: mousemove, keydown, click, scroll, touchstart
//  After 15 min of inactivity → Supabase signOut → redirect to login
// ═══════════════════════════════════════════════════════════

const IDLE_TIMEOUT_MS = 15 * 60 * 1000; // 15 minutes
const THROTTLE_MS = 30_000; // Only update timer every 30s (performance)
const STORAGE_KEY = "pravotek_last_activity";

export default function AutoLogoutProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastActivityRef = useRef(Date.now());

  const signOut = useCallback(async () => {
    try {
      const supabase = createSupabaseBrowser();
      await supabase.auth.signOut();
    } catch {
      // Silently ignore — we're logging out anyway
    }
    // Clear any stored activity timestamp
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // localStorage might not be available
    }
    router.push("/auth/login?reason=idle");
    router.refresh();
  }, [router]);

  const resetTimer = useCallback(() => {
    const now = Date.now();

    // Throttle: don't reset more often than THROTTLE_MS
    if (now - lastActivityRef.current < THROTTLE_MS) return;
    lastActivityRef.current = now;

    // Persist across tabs
    try {
      localStorage.setItem(STORAGE_KEY, now.toString());
    } catch {
      // localStorage unavailable
    }

    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(signOut, IDLE_TIMEOUT_MS);
  }, [signOut]);

  useEffect(() => {
    // ── Check if already expired from another tab ──
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const elapsed = Date.now() - parseInt(stored, 10);
        if (elapsed > IDLE_TIMEOUT_MS) {
          signOut();
          return;
        }
      }
    } catch {
      // Ignore
    }

    // ── Start idle timer ──
    timerRef.current = setTimeout(signOut, IDLE_TIMEOUT_MS);

    // ── Listen for user activity ──
    const events: (keyof WindowEventMap)[] = [
      "mousemove",
      "keydown",
      "click",
      "scroll",
      "touchstart",
    ];

    for (const event of events) {
      window.addEventListener(event, resetTimer, { passive: true });
    }

    // ── Listen for activity in other tabs via storage event ──
    const handleStorage = (e: StorageEvent) => {
      if (e.key === STORAGE_KEY && e.newValue) {
        // Another tab was active — reset our timer too
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(signOut, IDLE_TIMEOUT_MS);
      }
    };
    window.addEventListener("storage", handleStorage);

    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      for (const event of events) {
        window.removeEventListener(event, resetTimer);
      }
      window.removeEventListener("storage", handleStorage);
    };
  }, [resetTimer, signOut]);

  return <>{children}</>;
}
