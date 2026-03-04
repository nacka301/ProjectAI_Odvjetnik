"use client";

import { useEffect, useState } from "react";
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

  useEffect(() => {
    if (!userId || !authHeaders.Authorization) {
      setLoading(false);
      return;
    }

    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const hdrs = { ...authHeaders, "Content-Type": "application/json" };

        const res = await fetch(`${API_BASE}/api/settings/credits`, { headers: hdrs });
        if (cancelled) return;

        if (res.ok) {
          const data = await res.json();
          const arr: CreditRow[] = Array.isArray(data) ? data : [];

          // If firm_usage returned nothing, fall back to subscription ai_credits_limit
          if (arr.length === 0) {
            try {
              const subRes = await fetch(`${API_BASE}/api/settings/subscription`, { headers: hdrs });
              if (!cancelled && subRes.ok) {
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

          if (!cancelled) setRows(arr);
        } else {
          console.error("Credits fetch failed:", res.status);
          setError(`Greška ${res.status}`);
        }
      } catch (err) {
        if (!cancelled) {
          console.error("Credits fetch error:", err);
          setError("Mrežna greška");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => { cancelled = true; };
  }, [userId, authHeaders]);

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
 * If no row exists for the module, returns max=50 (backend default), used=0.
 */
export function getModuleQuota(rows: CreditRow[], moduleName: string): ModuleQuota {
  const row = rows.find((r) => r.module_name === moduleName);
  const used = safeNum(row?.used_credits);
  const max = row ? safeNum(row.max_credits) : 50;
  const remaining = Math.max(0, max - used);
  return { used, max, remaining, exhausted: remaining <= 0 };
}
