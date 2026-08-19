"use client";

import { createSupabaseBrowser } from "@/lib/supabase/client";

/**
 * The only way this app signs a user out.
 *
 * There were five of them, each a bare `supabase.auth.signOut()`, and every one
 * shared the same hole: the second-factor marker cookie is HttpOnly, so none of
 * them could clear it. One helper means the next sign-out someone adds cannot
 * reintroduce that.
 *
 * Order matters. The server route runs FIRST, while the session cookies are
 * still present — it uses them to revoke the refresh token server-side. Clearing
 * the browser session first would leave the route with nothing to revoke.
 */
export async function signOutCompletely(): Promise<void> {
  try {
    await fetch("/api/auth/sign-out", { method: "POST", cache: "no-store" });
  } catch {
    // Offline or the route failed. The browser sign-out below still removes the
    // access token, so this must not be allowed to abort the sign-out.
  }

  try {
    const supabase = createSupabaseBrowser();
    await supabase.auth.signOut();
  } catch {
    // Nothing left to do — the caller navigates to /auth/login either way, and
    // the middleware refuses anything without a valid session.
  }
}
