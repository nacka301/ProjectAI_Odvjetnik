"use client";

import { useEffect, useRef, useState } from "react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

/**
 * The office's standing with us: still in the trial, paying, or shut out.
 *
 * There used to be three plans here — free / starter / pro — with their own
 * label map, duplicated verbatim in two other files and a fourth time in C#.
 * None of them was ever sold. What replaced them is a single path: seven days
 * of trial, then a subscription.
 *
 * Nothing in this file decides anything. `state` and `daysRemaining` are
 * computed by the backend (SubscriptionGate) and read here as given, because the
 * same service is what refuses the API. When the badge and the guard each did
 * their own date arithmetic, they could disagree — and the version the user saw
 * was the reassuring one.
 */

export type SubscriptionState = "proba" | "pretplata" | "isteklo";

// ── Matches backend SubscriptionResponse (snake_case serialization) ──

export interface SubscriptionData {
  id: string;
  plan_id: string;
  status: string;
  valid_from: string | null;
  valid_until: string | null;
  price_eur: number;
  max_cases: number;
  max_invoices: number;
  ai_credits_limit: number;
  law_firm_id: string | null;

  /** "proba" | "pretplata" | "isteklo" — the backend's verdict. */
  state: SubscriptionState;
  /** Whole days of trial left. 0 means the last day. Null when not on trial. */
  days_remaining: number | null;
  /** When access ends. Null means open-ended. */
  access_until: string | null;
}

export interface SubscriptionStatus {
  data: SubscriptionData | null;
  loading: boolean;
  error: string | null;
  /** Derived: which of the three states this office is in. */
  state: SubscriptionState;
  /** Derived: whole days of trial left, or null. */
  daysRemaining: number | null;
  /** Derived: when access ends, ISO, or null for open-ended. */
  accessUntil: string | null;
  /** Derived: may this office still use Troškovnik and Stranke? */
  hasAccess: boolean;
  /** Derived: the wording for the header badge. */
  badgeLabel: string;
}

/** Monthly and yearly price, stated in one place so the copy cannot drift. */
export const CIJENA_MJESECNO = "19 €";
export const CIJENA_GODISNJE = "190 €";
/** Where a user goes to pay. Payment is manual, by e-mail, on purpose. */
export const KONTAKT_EMAIL = "vackadavid@gmail.com";

/**
 * The badge wording.
 *
 * Croatian counts days as "1 dan" and "N dana", and the final day is worded
 * differently again — "Zadnji dan probe" rather than "Još 0 dana probe", which
 * would read as though the trial were already over.
 */
export function badgeLabelFor(
  state: SubscriptionState,
  daysRemaining: number | null,
): string {
  if (state === "isteklo") return "Proba istekla";
  if (state === "pretplata") return "Pretplata aktivna";

  if (daysRemaining === null || daysRemaining <= 0) return "Zadnji dan probe";
  if (daysRemaining === 1) return "Još 1 dan probe";
  return `Još ${daysRemaining} dana probe`;
}

/**
 * Fetches the authenticated user's subscription.
 *
 * On failure it reports "pretplata" — access looks fine. That is the opposite of
 * the old default, which assumed the Free plan and so made a network outage
 * indistinguishable from a real plan. The reasoning is the same as in
 * SubscriptionGate: the backend is what locks the modules, and it fails open for
 * an outage of ours. A frontend that guessed "expired" would put a paywall in
 * front of a paying user over a request that simply did not arrive.
 */
export function useSubscription(): SubscriptionStatus {
  const { userId, authHeaders } = useAuth();
  const [data, setData] = useState<SubscriptionData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // `loading` means "we have never had an answer", not "a request is in flight".
  // A token refresh or a tab-focus revalidation must not blank a page the user
  // is working on — `PretplataGate` swaps the whole subtree for a spinner while
  // this is true, and that is what was unmounting the Troškovnik form mid-edit.
  const ikadOdgovoreno = useRef(false);

  useEffect(() => {
    if (!userId || !authHeaders.Authorization) {
      setLoading(false);
      return;
    }

    let cancelled = false;
    (async () => {
      if (!ikadOdgovoreno.current) setLoading(true);
      setError(null);
      try {
        const res = await fetch(`${API_BASE}/api/settings/subscription`, {
          headers: { ...authHeaders, "Content-Type": "application/json" },
        });
        if (cancelled) return;

        if (res.ok) {
          const json = await res.json();
          setData(json);
        } else {
          setError(`Greška ${res.status}`);
        }
      } catch {
        if (!cancelled) {
          setError("Mrežna greška");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
          // From now on this is a revalidation, never a first load — so it may
          // never again put the page behind a spinner, whatever the outcome.
          ikadOdgovoreno.current = true;
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [userId, authHeaders]);

  const state: SubscriptionState = data?.state ?? "pretplata";
  const daysRemaining = data?.days_remaining ?? null;

  return {
    data,
    loading,
    error,
    state,
    daysRemaining,
    accessUntil: data?.access_until ?? null,
    hasAccess: state !== "isteklo",
    badgeLabel: badgeLabelFor(state, daysRemaining),
  };
}
