"use client";

/**
 * Backward-compatible credit hook.
 *
 * All fetching now lives in CreditContext — this file is a thin re-export wrapper
 * so existing `import { useCredits, getModuleQuota } from "@/lib/useCredits"`
 * calls continue to work without any page-level changes.
 *
 * The `userId` parameter is still accepted for API compat but is ignored;
 * CreditProvider derives the user from its own `useAuth()` subscription.
 */

import { useCreditContext } from "@/contexts/CreditContext";

// Re-export types from the canonical location
export type { CreditRow, CreditSummary, CreditContextValue } from "@/contexts/CreditContext";

export interface ModuleQuota {
  used: number;
  max: number;
  remaining: number;
  exhausted: boolean;
}

/** Safe number accessor — returns 0 for null/undefined/NaN */
function safeNum(val: unknown): number {
  if (val == null) return 0;
  const n = Number(val);
  return Number.isFinite(n) ? n : 0;
}

/**
 * Hook that returns the authenticated user's credit usage.
 *
 * Data is fetched ONCE by the global `CreditProvider` and shared across all
 * pages — no redundant API calls. The `_userId` parameter is accepted for
 * backward compatibility but is not used; the provider derives the user
 * internally via `useAuth()`.
 */
export function useCredits(_userId?: string | null) {
  return useCreditContext();
}

// ═══════════════════════════════════════════
//  Per-module quota helper (unchanged)
// ═══════════════════════════════════════════

/**
 * Extract quota for a single module from the credit rows.
 * Returns { used, max, remaining, exhausted }.
 *
 * Falls back to the "global" pool when no module-specific row exists,
 * so new users who only have the subscription-synthesised "global" row
 * still see meaningful data.
 */
export function getModuleQuota(
  rows: { module_name: string; used_credits: number; max_credits: number }[],
  moduleName: string,
): ModuleQuota {
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
