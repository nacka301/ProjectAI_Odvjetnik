"use client";

import { useEffect, useState, useCallback } from "react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

/**
 * Auth headers for backend API calls.
 *
 * Returns only `Authorization: Bearer <access_token>` — the backend derives the
 * user from that token and nothing else. The former `x-user-id` header and the
 * public `X-API-Key` are gone: the first was a self-declared identity the
 * backend used to trust, the second shipped inside the JS bundle.
 *
 * Usage:
 *   const { userId, authHeaders } = useAuth();
 *   const res = await fetch(url, { headers: { ...authHeaders, "Content-Type": "application/json" } });
 */
type Session = { access_token: string; user: { id: string } };

function headersFor(session: Session): Record<string, string> {
  return { Authorization: `Bearer ${session.access_token}` };
}

export function useAuth() {
  const [userId, setUserId] = useState<string | null>(null);
  const [authHeaders, setAuthHeaders] = useState<Record<string, string>>({});

  const applySession = useCallback((session: Session | null) => {
    if (session) {
      setUserId(session.user.id);
      setAuthHeaders(headersFor(session));
    } else {
      setUserId(null);
      setAuthHeaders({});
    }
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

  return { userId, authHeaders };
}
