"use client";

import { useEffect, useState, useRef, useCallback } from "react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

export interface CreditRow {
  id: string;
  law_firm_id: string;
  module_name: string;
  used_credits: number;
  max_credits: number;
  reset_date: string | null;
}

export interface CreditSummary {
  total: number;
  used: number;
  remaining: number;
  rows: CreditRow[];
  loading: boolean;
  error: string | null;
}

/** Safe number accessor — returns 0 for null/undefined/NaN */
function safeNum(val: unknown): number {
  if (val == null) return 0;
  const n = Number(val);
  return Number.isFinite(n) ? n : 0;
}

/**
 * Hook that fetches the authenticated user's Beta credit usage.
 *
 * Returns a summary object with totals, per-module rows, loading, and error state.
 * Pass `userId` from Supabase auth — when null/undefined the fetch is skipped.
 * Auth headers (Bearer token) are obtained automatically from useAuth().
 *
 * When firm_usage is empty (new user), falls back to the subscription's
 * ai_credits_limit so the header/dashboard can still show the allocation.
 */
export function useCredits(userId: string | null): CreditSummary {
  const { authHeaders } = useAuth();
  const [rows, setRows] = useState<CreditRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // FIX: Extract the token string so the useEffect dependency is a primitive
  // (string) instead of an object reference. This prevents both stale closures
  // AND infinite re-render loops caused by authHeaders being a new object each
  // render.
  const token = authHeaders?.Authorization ?? "";

  // Retry ref: if userId is set but token hasn't arrived yet (Supabase
  // hydration delay ~50-200ms), we schedule a retry.
  const retryTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const doFetch = useCallback(async (bearerToken: string, signal?: AbortSignal): Promise<CreditRow[]> => {
    const hdrs: Record<string, string> = {
      Authorization: bearerToken,
      "Content-Type": "application/json",
    };

    // Also include X-API-Key if configured
    const apiKey = process.env.NEXT_PUBLIC_API_KEY;
    if (apiKey) hdrs["X-API-Key"] = apiKey;

    const res = await fetch(`${API_BASE}/api/settings/credits`, { headers: hdrs, signal });
    if (!res.ok) throw new Error(`Credits fetch failed: ${res.status}`);

    const data = await res.json();
    const arr: CreditRow[] = Array.isArray(data) ? data : [];

    // If firm_usage returned nothing, fall back to subscription ai_credits_limit
    if (arr.length === 0) {
      try {
        const subRes = await fetch(`${API_BASE}/api/settings/subscription`, { headers: hdrs, signal });
        if (subRes.ok) {
          const sub = await subRes.json();
          const limit = safeNum(sub?.ai_credits_limit);
          if (limit > 0) {
            arr.push({
              id: "subscription-fallback",
              law_firm_id: sub?.law_firm_id ?? "",
              module_name: "global",
              used_credits: 0,
              max_credits: limit,
              reset_date: null,
            });
          }
        }
      } catch {
        // Subscription fallback is best-effort; ignore errors
      }
    }

    return arr;
  }, []);

  useEffect(() => {
    // Guard: need userId
    if (!userId) {
      setLoading(false);
      return;
    }

    // FIX: If the Bearer token hasn't arrived yet, keep loading=true and wait.
    // The effect will re-fire when `token` changes (once useAuth sets it).
    if (!token.startsWith("Bearer ")) {
      setLoading(true);
      // Safety: schedule a no-op timer to clean up if component unmounts
      retryTimer.current = setTimeout(() => {}, 300);
      return () => {
        if (retryTimer.current) clearTimeout(retryTimer.current);
      };
    }

    const abortController = new AbortController();
    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);
      try {
        const arr = await doFetch(token, abortController.signal);
        if (!cancelled) setRows(arr);
      } catch (err) {
        if (!cancelled && !abortController.signal.aborted) {
          console.error("Credits fetch error:", err);
          setError(err instanceof Error ? err.message : "Mrežna greška");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
      abortController.abort();
      if (retryTimer.current) clearTimeout(retryTimer.current);
    };
  }, [userId, token, doFetch]);

  const total = rows.reduce((s, r) => s + safeNum(r.max_credits), 0);
  const used = rows.reduce((s, r) => s + safeNum(r.used_credits), 0);
  const remaining = rows.reduce(
    (s, r) => s + Math.max(0, safeNum(r.max_credits) - safeNum(r.used_credits)),
    0
  );

  return { total, used, remaining, rows, loading, error };
}

// ═══════════════════════════════════════════
//  Per-module quota helper
// ═══════════════════════════════════════════

export interface ModuleQuota {
  used: number;
  max: number;
  remaining: number;
  exhausted: boolean;
}

/**
 * Extract quota for a single module from the credit rows.
 * Returns { used, max, remaining, exhausted }.
 *
 * Falls back to the "global" pool when no module-specific row exists,
 * so new users who only have the subscription-synthesised "global" row
 * still see meaningful data.
 */
export function getModuleQuota(rows: CreditRow[], moduleName: string): ModuleQuota {
  // 1. Try exact module match
  let row = rows.find((r) => r.module_name === moduleName);

  // 2. Fallback to "global" pool (synthesised from subscription)
  if (!row) row = rows.find((r) => r.module_name === "global");

  // 3. No data at all — return unknowns (not exhausted so fetch can complete)
  if (!row) {
    return { used: 0, max: 0, remaining: 0, exhausted: false };
  }

  const used = safeNum(row.used_credits);
  const max = safeNum(row.max_credits);
  const remaining = Math.max(0, max - used);
  return { used, max, remaining, exhausted: remaining <= 0 && max > 0 };
}
