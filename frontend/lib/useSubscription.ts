"use client";

import { useEffect, useState } from "react";
import { API_BASE } from "@/lib/api";
import { useAuth } from "@/lib/useAuth";

// ── Types matching backend SubscriptionResponse (snake_case serialization) ──

export interface SubscriptionData {
  id: string;
  plan_id: string;
  plan_name: string;
  status: string;
  valid_from: string | null;
  valid_until: string | null;
  price_eur: number;
  max_cases: number;
  max_invoices: number;
  ai_credits_limit: number;
  law_firm_id: string | null;
}

export interface SubscriptionState {
  data: SubscriptionData | null;
  loading: boolean;
  error: string | null;
  /** Derived: Croatian label for the plan */
  planLabel: string;
  /** Derived: "free" | "starter" | "pro" */
  planId: string;
  /** Derived: subscription is currently active or trial */
  isActive: boolean;
}

const PLAN_LABELS: Record<string, string> = {
  free: "Besplatno",
  starter: "Starter",
  pro: "Pro",
};

/**
 * Hook that fetches the authenticated user's subscription data.
 *
 * Returns plan info, status, limits, and loading/error states.
 * Safe default: assumes Free plan when data is unavailable.
 */
export function useSubscription(): SubscriptionState {
  const { userId, authHeaders } = useAuth();
  const [data, setData] = useState<SubscriptionData | null>(null);
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
        const res = await fetch(`${API_BASE}/api/settings/subscription`, {
          headers: { ...authHeaders, "Content-Type": "application/json" },
        });
        if (cancelled) return;

        if (res.ok) {
          const json = await res.json();
          setData(json);
        } else {
          console.error("Subscription fetch failed:", res.status);
          setError(`Greška ${res.status}`);
        }
      } catch (err) {
        if (!cancelled) {
          console.error("Subscription fetch error:", err);
          setError("Mrežna greška");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [userId, authHeaders]);

  const planId = data?.plan_id ?? "free";
  const planLabel =
    PLAN_LABELS[planId] || data?.plan_name || PLAN_LABELS.free;
  const isActive = data?.status === "active" || data?.status === "trial";

  return { data, loading, error, planLabel, planId, isActive };
}
