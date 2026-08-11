/**
 * Signed marker cookie proving this session has cleared the second factor.
 *
 * Why a cookie and not "we asked on the login screen": the Supabase session is
 * established the moment the password is accepted, so any check that lives in
 * the login component can be skipped by typing another URL. The gate has to sit
 * where every navigation passes — the middleware — and it needs a tamper-proof
 * way to remember that the code was entered. Hence an HMAC, not a flag.
 *
 * Value format: `{userId}.{expiresAtSeconds}.{hmac}` where the HMAC covers
 * `{userId}.{expiresAtSeconds}`. Binding the user id means a cookie lifted from
 * one account is useless on another.
 *
 * Web Crypto rather than node:crypto — this runs in the Edge runtime.
 */

export const MFA_COOKIE = "pt_mfa";

/** How long a passed second factor stays valid. */
export const MFA_TTL_SECONDS = 12 * 60 * 60;

function secret(): string {
  const value = process.env.MFA_COOKIE_SECRET;
  if (!value) {
    // Deliberately fatal. Without a secret the marker cannot be signed or
    // trusted, and the two alternatives are both worse: accepting an unsigned
    // cookie disables 2FA silently, while refusing every cookie traps users in
    // a redirect loop. A 500 is visible and cannot be mistaken for working.
    throw new Error(
      "MFA_COOKIE_SECRET nije postavljen — dvofaktorska provjera se ne može " +
        "potpisati. Postavite varijablu okoline (openssl rand -hex 32).",
    );
  }
  return value;
}

async function hmac(payload: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret()),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    new TextEncoder().encode(payload),
  );
  return Array.from(new Uint8Array(signature))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/** Compares without leaking where the first difference is. */
function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return diff === 0;
}

/** Mints the cookie value for a user who just passed the second factor. */
export async function signMfaCookie(userId: string): Promise<string> {
  const expiresAt = Math.floor(Date.now() / 1000) + MFA_TTL_SECONDS;
  const payload = `${userId}.${expiresAt}`;
  return `${payload}.${await hmac(payload)}`;
}

/** True only for an unexpired, correctly signed cookie belonging to this user. */
export async function verifyMfaCookie(
  value: string | undefined,
  userId: string,
): Promise<boolean> {
  if (!value) return false;

  const parts = value.split(".");
  if (parts.length !== 3) return false;

  const [cookieUserId, expiresAtRaw, signature] = parts;
  if (cookieUserId !== userId) return false;

  const expiresAt = Number(expiresAtRaw);
  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now() / 1000) return false;

  return timingSafeEqual(signature, await hmac(`${cookieUserId}.${expiresAtRaw}`));
}
