import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";

/**
 * Auth callback Route Handler.
 * Supabase redirects here after:
 *   - Email verification (signup confirmation)
 *   - OAuth sign-in (Google)
 *   - Password reset link
 *
 * The `code` query param is exchanged for a session (stored in HttpOnly cookies).
 */
export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get("code");
  const next = searchParams.get("next") ?? "/";

  if (code) {
    const supabase = await createSupabaseServer();
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (!error) {
      // Successful exchange — redirect to the requested page
      return NextResponse.redirect(`${origin}${next}`);
    }
  }

  // Something went wrong — redirect to login with error hint
  return NextResponse.redirect(`${origin}/auth/login?error=callback`);
}
