import { createServerClient } from "@supabase/ssr";
import { cookies } from "next/headers";
import { SUPABASE_URL, SUPABASE_ANON_KEY } from "@/lib/supabase-env";

/**
 * Creates a Supabase client for use in Server Components, Server Actions,
 * and Route Handlers. Tokens are stored in secure HttpOnly cookies —
 * never in localStorage.
 */
export async function createSupabaseServer() {
  const cookieStore = await cookies();

  return createServerClient(
    SUPABASE_URL,
    SUPABASE_ANON_KEY,
    {
      cookies: {
        getAll() {
          return cookieStore.getAll();
        },
        setAll(cookiesToSet) {
          try {
            cookiesToSet.forEach(({ name, value, options }) =>
              cookieStore.set(name, value, options),
            );
          } catch {
            // setAll can throw in Server Components (read-only).
            // This is expected — the middleware will handle the refresh.
          }
        },
      },
    },
  );
}
