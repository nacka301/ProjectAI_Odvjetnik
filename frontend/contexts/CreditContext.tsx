"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

// ═══════════════════════════════════════════════════════════════
//  Types (re-exported by useCredits.ts for backward compat)
// ═══════════════════════════════════════════════════════════════

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

export interface CreditContextValue extends CreditSummary {
  /** Force a fresh fetch (e.g. after consuming a credit). */
  refreshCredits: () => void;
}

// ═══════════════════════════════════════════════════════════════
//  Default value — safe for components rendered outside provider
// ═══════════════════════════════════════════════════════════════

const DEFAULT: CreditContextValue = {
  total: 0,
  used: 0,
  remaining: 0,
  rows: [],
  loading: true,
  error: null,
  refreshCredits: () => {},
};

const CreditCtx = createContext<CreditContextValue>(DEFAULT);

// ═══════════════════════════════════════════════════════════════
//  Helpers
// ═══════════════════════════════════════════════════════════════

function safeNum(val: unknown): number {
  if (val == null) return 0;
  const n = Number(val);
  return Number.isFinite(n) ? n : 0;
}

function summarise(rows: CreditRow[]) {
  const total = rows.reduce((s, r) => s + safeNum(r.max_credits), 0);
  const used = rows.reduce((s, r) => s + safeNum(r.used_credits), 0);
  const remaining = rows.reduce(
    (s, r) => s + Math.max(0, safeNum(r.max_credits) - safeNum(r.used_credits)),
    0,
  );
  return { total, used, remaining };
}

// ═══════════════════════════════════════════════════════════════
//  Provider
// ═══════════════════════════════════════════════════════════════

const STALE_MS = 60_000; // 60-second stale window

export function CreditProvider({ children }: { children: ReactNode }) {
  const { userId, authHeaders } = useAuth();
  const token = authHeaders?.Authorization ?? "";

  const [rows, setRows] = useState<CreditRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Track when we last successfully fetched
  const lastFetchedAt = useRef(0);
  // Increment to force a re-fetch from consumers
  const [fetchSeq, setFetchSeq] = useState(0);

  const doFetch = useCallback(
    async (bearerToken: string, signal?: AbortSignal): Promise<CreditRow[]> => {
      const hdrs: Record<string, string> = {
        Authorization: bearerToken,
        "Content-Type": "application/json",
      };
      const apiKey = process.env.NEXT_PUBLIC_API_KEY;
      if (apiKey) hdrs["X-API-Key"] = apiKey;

      const res = await fetch(`${API_BASE}/api/settings/credits`, {
        headers: hdrs,
        signal,
      });
      if (!res.ok) throw new Error(`Credits fetch failed: ${res.status}`);

      const data = await res.json();
      const arr: CreditRow[] = Array.isArray(data) ? data : [];

      // Subscription fallback for brand-new users with empty firm_usage
      if (arr.length === 0) {
        try {
          const subRes = await fetch(`${API_BASE}/api/settings/subscription`, {
            headers: hdrs,
            signal,
          });
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
          // best-effort
        }
      }

      return arr;
    },
    [],
  );

  // ── Main fetch effect ──
  useEffect(() => {
    if (!userId) {
      setRows([]);
      setLoading(false);
      return;
    }

    // Wait for the Bearer token to arrive (Supabase hydration delay)
    if (!token.startsWith("Bearer ")) {
      setLoading(true);
      return;
    }

    // Respect stale window — don't re-fetch if data is fresh
    if (Date.now() - lastFetchedAt.current < STALE_MS && rows.length > 0) {
      return;
    }

    const ac = new AbortController();
    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);
      try {
        const arr = await doFetch(token, ac.signal);
        if (!cancelled) {
          setRows(arr);
          lastFetchedAt.current = Date.now();
        }
      } catch (err) {
        if (!cancelled && !ac.signal.aborted) {
          setError(err instanceof Error ? err.message : "Mrežna greška");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
      ac.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, token, doFetch, fetchSeq]);

  const refreshCredits = useCallback(() => {
    lastFetchedAt.current = 0; // invalidate stale window
    setFetchSeq((s) => s + 1);
  }, []);

  const value: CreditContextValue = {
    ...summarise(rows),
    rows,
    loading,
    error,
    refreshCredits,
  };

  return <CreditCtx.Provider value={value}>{children}</CreditCtx.Provider>;
}

// ═══════════════════════════════════════════════════════════════
//  Consumer hook
// ═══════════════════════════════════════════════════════════════

/**
 * Read credit data from the global CreditProvider.
 *
 * Safe to call outside the provider — returns a default empty / loading state
 * so the app never crashes.
 */
export function useCreditContext(): CreditContextValue {
  return useContext(CreditCtx);
}
