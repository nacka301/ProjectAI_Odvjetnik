"use client";

import { useEffect, useMemo, useState, useCallback } from "react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

/**
 * Auth headers for backend API calls.
 *
 * Returns only `Authorization: Bearer <access_token>` — the backend derives the
 * user from that token and nothing else. The former `x-user-id` header and the
 * public `X-API-Key` are gone: the first was a self-declared identity the
 * backend used to trust, the second shipped inside the JS bundle.
 *
 * ── Why the token is state and `authHeaders` is derived ──
 *
 * Supabase fires `onAuthStateChange` on far more than a real sign-in: it emits
 * `INITIAL_SESSION` / `SIGNED_IN` every time the tab regains focus, and
 * `TOKEN_REFRESHED` roughly hourly. When this hook kept `authHeaders` in state
 * and rebuilt the object on every one of those, its identity changed each time
 * even though the token had not — and every `useEffect([authHeaders])` in the
 * app (there are ~20) re-ran, and any hook that reset `loading` on a refetch
 * swapped the page for a spinner and unmounted the form mid-edit.
 *
 * Storing the raw token string and deriving the header through `useMemo` fixes
 * it at the source: `setToken(sameString)` is a no-op React bails out of, so a
 * focus event with an unchanged token now changes nothing downstream.
 *
 * Usage:
 *   const { userId, authHeaders } = useAuth();
 *   const res = await fetch(url, { headers: { ...authHeaders, "Content-Type": "application/json" } });
 */
type Session = { access_token: string; user: { id: string } };

export function useAuth() {
  const [userId, setUserId] = useState<string | null>(null);
  const [token, setToken] = useState<string | null>(null);

  const applySession = useCallback((session: Session | null) => {
    setUserId(session?.user.id ?? null);
    setToken(session?.access_token ?? null);
  }, []);

  const refreshAuth = useCallback(async () => {
    const supabase = createSupabaseBrowser();
    const { data: { session } } = await supabase.auth.getSession();
    applySession(session);
  }, [applySession]);

  useEffect(() => {
    refreshAuth();

    const supabase = createSupabaseBrowser();
    const { data: { subscription } } = supabase.auth.onAuthStateChange(
      (_event, session) => applySession(session),
    );

    return () => subscription.unsubscribe();
  }, [refreshAuth, applySession]);

  // Stable reference: only a genuine token change (hourly refresh, real
  // sign-in/out) produces a new object. See the header comment for why this
  // matters far beyond this file.
  const authHeaders = useMemo(
    (): Record<string, string> => (token ? { Authorization: `Bearer ${token}` } : {}),
    [token],
  );

  return { userId, authHeaders };
}
