/**
 * Where an auth flow is allowed to send the browser, and what the user is told
 * when it fails.
 *
 * Both live here because both were previously duplicated across four files, and
 * in the case of the redirect check, duplicated *wrongly* in all four.
 */

/**
 * Turns an untrusted `?next=` into a path this app will actually navigate to.
 *
 * The check that was in place everywhere — `next.startsWith("/")` — lets through
 * `//evil.example`, which a browser reads as a protocol-relative URL and follows
 * off-site. That turns every auth screen into an open redirect: a link like
 * `…/auth/login?next=//evil.example` sends a lawyer who has just typed their
 * password to somebody else's page, and a copy of this app's login screen is not
 * hard to build. `\` is rejected for the same reason — some browsers normalise
 * `/\evil.example` the same way.
 *
 * Returns the fallback for anything that is not a plain in-app path.
 */
export function safeNextPath(raw: string | null | undefined, fallback = "/"): string {
  if (!raw) return fallback;
  if (!raw.startsWith("/")) return fallback;
  // Second character decides: "/" or "\" here means we are leaving the site.
  if (raw.length > 1 && (raw[1] === "/" || raw[1] === "\\")) return fallback;
  return raw;
}

/**
 * Croatian text for the failures Supabase reports through the callback URL.
 *
 * Supabase answers in English. Forwarding `error_description` verbatim put
 * "Email link is invalid or has expired" in front of a Croatian lawyer on the
 * one screen where they are already unsure whether they did something wrong.
 *
 * Unknown codes fall back to a generic sentence rather than the English one:
 * an untranslated string tells the user nothing they can act on, and the code
 * is in the log either way.
 */
export function translateAuthError(
  code: string | null,
  description: string | null,
): string {
  const haystack = `${code ?? ""} ${description ?? ""}`.toLowerCase();

  if (haystack.includes("expired") || haystack.includes("invalid")) {
    return "Link je istekao ili je već iskorišten. Zatražite novi.";
  }
  if (haystack.includes("access_denied")) {
    return "Prijava je prekinuta. Pokušajte ponovo.";
  }
  if (haystack.includes("otp_disabled") || haystack.includes("signup_disabled")) {
    return "Registracija trenutno nije omogućena. Javite se podršci.";
  }
  if (haystack.includes("rate") || haystack.includes("over_email_send")) {
    return "Previše zahtjeva u kratko vrijeme. Pokušajte za nekoliko minuta.";
  }

  return "Prijava nije uspjela. Pokušajte ponovo ili zatražite novi link.";
}
