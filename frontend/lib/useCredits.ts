"use client";

import { useEffect, useState } from "react";
import { API_BASE } from "@/lib/api";

function apiHeaders(): Record<string, string> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const apiKey = process.env.NEXT_PUBLIC_API_KEY;
  if (apiKey) headers["X-API-Key"] = apiKey;
  return headers;
}

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
 */
export function useCredits(userId: string | null): CreditSummary {
  const [rows, setRows] = useState<CreditRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!userId) {
      setLoading(false);
      return;
    }

    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(
          `${API_BASE}/api/settings/credits?userId=${encodeURIComponent(userId)}`,
          { headers: apiHeaders() }
        );
        if (cancelled) return;

        if (res.ok) {
          const data = await res.json();
          console.log("Token Data Received:", data);
          setRows(Array.isArray(data) ? data : []);
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
  }, [userId]);

  const total = rows.reduce((s, r) => s + safeNum(r.max_credits), 0);
  const used = rows.reduce((s, r) => s + safeNum(r.used_credits), 0);
  const remaining = rows.reduce(
    (s, r) => s + Math.max(0, safeNum(r.max_credits) - safeNum(r.used_credits)),
    0
  );

  return { total, used, remaining, rows, loading, error };
}
