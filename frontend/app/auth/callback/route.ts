import { NextResponse } from "next/server";
import type { EmailOtpType } from "@supabase/supabase-js";
import { createSupabaseServer } from "@/lib/supabase/server";
import { safeNextPath, translateAuthError } from "@/lib/auth-redirect";

/**
 * Auth callback. Supabase sends the browser here after Google sign-in, after an
 * email confirmation, and after a password-reset link.
 *
 * Two ways in, deliberately, because they fail in different places:
 *
 *   • `token_hash` + `type` → verifyOtp(). Nothing is stored in the browser
 *     beforehand, so the link works from ANY device. This is what the email
 *     templates now send.
 *
 *   • `code` → exchangeCodeForSession(). This is the PKCE flow, which OAuth
 *     needs and which older emails still use. It requires the code_verifier
 *     cookie written by the browser that STARTED the flow — so a link opened on
 *     a phone after registering on a desktop cannot complete, and the user is
 *     told the link expired when it did not. That is why email no longer relies
 *     on it, and why the failure message below says to request a new one rather
 *     than blaming the link.
 *
 * Where the user lands is decided by `?next=`, not by guessing from `type`.
 * The previous version routed password resets by checking `type === "recovery"`,
 * a parameter Supabase does not forward in the PKCE redirect — so a reset link
 * signed the user in and dropped them on the dashboard with no password form,
 * which is indistinguishable from "forgot password is broken".
 */

/** OTP types this route will act on. Anything else is not ours to verify. */
const ALLOWED_OTP_TYPES = new Set<EmailOtpType>([
  "signup",
  "recovery",
  "invite",
  "magiclink",
  "email",
  "email_change",
]);

function parseOtpType(raw: string | null): EmailOtpType | null {
  return raw && ALLOWED_OTP_TYPES.has(raw as EmailOtpType)
    ? (raw as EmailOtpType)
    : null;
}

/** Sends the user back to the login screen with something readable. */
function toLogin(origin: string, message: string) {
  const loginUrl = new URL("/auth/login", origin);
  loginUrl.searchParams.set("error", message);
  return NextResponse.redirect(loginUrl);
}

export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url);

  const code = searchParams.get("code");
  const tokenHash = searchParams.get("token_hash");
  const type = parseOtpType(searchParams.get("type"));
  const errorParam = searchParams.get("error");
  const errorDescription = searchParams.get("error_description");

  // A recovery link that carries no explicit `next` still has exactly one
  // sensible destination, so it is filled in here rather than left to "/".
  const destination = safeNextPath(
    searchParams.get("next"),
    type === "recovery" ? "/auth/reset-password" : "/",
  );

  // ── Supabase reported the failure itself (expired link, denied consent) ──
  if (errorParam) {
    return toLogin(origin, translateAuthError(errorParam, errorDescription));
  }

  const supabase = await createSupabaseServer();

  // ── Path A: token_hash — works from any device ──
  if (tokenHash && type) {
    const { error } = await supabase.auth.verifyOtp({ type, token_hash: tokenHash });

    if (error) {
      return toLogin(
        origin,
        "Link je istekao ili je već iskorišten. Zatražite novi.",
      );
    }

    return NextResponse.redirect(`${origin}${destination}`);
  }

  // ── Path B: PKCE code — OAuth, and older email links ──
  if (code) {
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (error) {
      return toLogin(
        origin,
        // Not "the link expired": the most common cause is that the link was
        // opened in a different browser than the one that requested it, and
        // telling the user it expired sends them to request another one that
        // will fail the same way.
        "Link nije bilo moguće otvoriti u ovom pregledniku. Otvorite ga u istom " +
          "pregledniku u kojem ste poslali zahtjev, ili zatražite novi.",
      );
    }

    return NextResponse.redirect(`${origin}${destination}`);
  }

  // ── Neither — somebody reached this URL by hand ──
  return toLogin(origin, "Autentifikacija nije uspjela. Pokušajte ponovo.");
}
