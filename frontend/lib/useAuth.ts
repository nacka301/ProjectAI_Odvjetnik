"use client";

import { useEffect, useState, useCallback } from "react";
import { createSupabaseBrowser } from "@/lib/supabase/client";

/**
 * Auth headers for backend API calls.
 *
 * Returns:
 *   - Authorization: Bearer <access_token>  (verified by backend JWT middleware)
 *   - x-user-id: <uuid>                     (backward compat / fallback)
 *   - X-API-Key: <key>                      (if configured)
 *
 * Usage:
 *   const { userId, authHeaders } = useAuth();
 *   const res = await fetch(url, { headers: { ...authHeaders, "Content-Type": "application/json" } });
 */
export function useAuth() {
  const [userId, setUserId] = useState<string | null>(null);
  const [authHeaders, setAuthHeaders] = useState<Record<string, string>>({});

  const refreshAuth = useCallback(async () => {
    const supabase = createSupabaseBrowser();
    const { data: { session } } = await supabase.auth.getSession();

    if (session) {
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${session.access_token}`,
        "x-user-id": session.user.id,
      };

      const apiKey = process.env.NEXT_PUBLIC_API_KEY;
      if (apiKey) headers["X-API-Key"] = apiKey;

      setUserId(session.user.id);
      setAuthHeaders(headers);
    } else {
      setUserId(null);
      setAuthHeaders({});
    }
  }, []);

  useEffect(() => {
    refreshAuth();

    const supabase = createSupabaseBrowser();
    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session) {
        const headers: Record<string, string> = {
          "Authorization": `Bearer ${session.access_token}`,
          "x-user-id": session.user.id,
        };

        const apiKey = process.env.NEXT_PUBLIC_API_KEY;
        if (apiKey) headers["X-API-Key"] = apiKey;

        setUserId(session.user.id);
        setAuthHeaders(headers);
      } else {
        setUserId(null);
        setAuthHeaders({});
      }
    });

    return () => subscription.unsubscribe();
  }, [refreshAuth]);

  return { userId, authHeaders };
}
