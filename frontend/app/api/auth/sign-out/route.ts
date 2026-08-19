import { NextResponse } from "next/server";
import { createSupabaseServer } from "@/lib/supabase/server";
import { MFA_COOKIE } from "@/lib/mfa-cookie";

/**
 * Ends a session completely — Supabase tokens AND the second-factor marker.
 *
 * `supabase.auth.signOut()` in the browser cannot touch `pt_mfa`: the cookie is
 * HttpOnly, which is the whole reason a client cannot forge it. So every sign-out
 * that ran only in the browser left a valid marker behind for the rest of its
 * 12-hour life. On a shared computer that meant the next person to type the
 * password walked straight past the second factor, because the middleware found
 * a correctly signed cookie for that same user id.
 *
 * Deliberately idempotent and unauthenticated: clearing a cookie for a session
 * that is already gone is exactly what a user hitting "Odjava" twice should get.
 * There is nothing to leak here — the only effect is removing access.
 */
export async function POST() {
  const supabase = await createSupabaseServer();

  // Revoke server-side too, so the refresh token cannot be replayed even if the
  // browser call did not land. A failure here must not stop the cookie removal.
  try {
    await supabase.auth.signOut();
  } catch {
    // Already signed out, or Supabase unreachable — carry on and clear anyway.
  }

  const response = NextResponse.json({ ok: true });

  // `delete` rather than an empty value: an empty string still parses as three
  // missing parts in verifyMfaCookie, but leaving no cookie at all is clearer
  // and cannot be confused for a marker in any future reader.
  response.cookies.delete(MFA_COOKIE);
  response.cookies.delete("is-admin");

  return response;
}
