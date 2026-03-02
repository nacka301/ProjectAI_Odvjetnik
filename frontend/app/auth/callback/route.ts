import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";

/**
 * Auth callback Route Handler.
 * Supabase redirects here after:
 *   - Email verification (signup confirmation)
 *   - OAuth sign-in (Google)
 *   - Password reset link (type=recovery)
 *
 * The `code` query param is exchanged for a session (stored in HttpOnly cookies).
 * Uses `origin` from the request URL (works correctly for both local & Render).
 */
export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get("code");
  const type = searchParams.get("type"); // "recovery" | "signup" | "magiclink" | null
  const next = searchParams.get("next") ?? "/";
  const errorParam = searchParams.get("error");
  const errorDescription = searchParams.get("error_description");

  // ── Handle errors forwarded by Supabase (e.g. expired reset link) ──
  if (errorParam) {
    const loginUrl = new URL("/auth/login", origin);
    loginUrl.searchParams.set(
      "error",
      errorDescription || errorParam,
    );
    return NextResponse.redirect(loginUrl);
  }

  // ── Exchange auth code for session ──
  if (code) {
    const supabase = await createSupabaseServer();
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (!error) {
      // Password-reset flow → redirect to the update-password page
      if (type === "recovery") {
        return NextResponse.redirect(`${origin}/auth/reset-password`);
      }

      // Normal flow (signup confirmation, OAuth callback) → dashboard or `next`
      const destination = next.startsWith("/") ? next : "/";
      return NextResponse.redirect(`${origin}${destination}`);
    }

    // Code exchange failed (expired / already used)
    const loginUrl = new URL("/auth/login", origin);
    loginUrl.searchParams.set("error", "Link je istekao ili je već iskorišten. Pokušajte ponovo.");
    return NextResponse.redirect(loginUrl);
  }

  // No code at all — redirect to login
  return NextResponse.redirect(`${origin}/auth/login?error=callback`);
}
