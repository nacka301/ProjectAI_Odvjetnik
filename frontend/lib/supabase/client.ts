import { createBrowserClient } from "@supabase/ssr";
import { SUPABASE_URL, SUPABASE_ANON_KEY } from "@/lib/supabase-env";

/**
 * Creates a Supabase client for use in Client Components.
 * This client works with the HttpOnly cookies set by the middleware
 * and server utilities — no tokens in localStorage.
 */
export function createSupabaseBrowser() {
  return createBrowserClient(SUPABASE_URL, SUPABASE_ANON_KEY);
}
