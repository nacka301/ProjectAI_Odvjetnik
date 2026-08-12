import { API_BASE } from "@/lib/api";

/**
 * Asking the backend whether an account has a second factor has three possible
 * answers, not two:
 *
 *   required     — the backend says 2FA is on. Ask for a code.
 *   not-enrolled — the backend says 2FA is off. Let them through.
 *   unknown      — the backend did not answer, or answered that it cannot tell.
 *
 * The gate used to collapse the last two failure shapes into "required", so a
 * backend that rejected the token asked every user for a code from an
 * authenticator they had never set up, with no way out.
 *
 * `unknown` stays fail-closed — it grants nothing and mints no cookie — but it is
 * a distinct outcome, because "prove yourself" and "we cannot check right now"
 * call for different screens.
 */
export type SecondFactor = "required" | "not-enrolled" | "unknown";

/** Why the answer is unknown. A closed set: it travels in a URL and is shown to the user. */
export type UnknownReason =
  | "no-session"  // no access token to ask with
  | "rejected"    // backend refused the token (401/403)
  | "backend"     // backend answered, but with an error
  | "unreachable" // timed out or the request threw
  | "malformed";  // 2xx whose body was not a boolean `enabled`

export type SecondFactorResult =
  | { outcome: "required" | "not-enrolled" }
  | { outcome: "unknown"; reason: UnknownReason };

/** Long enough for a warm backend, short enough to leave room for one retry. */
const FIRST_ATTEMPT_TIMEOUT_MS = 5_000;

/** The retry absorbs a Render free-tier instance waking up mid-request. */
const RETRY_TIMEOUT_MS = 10_000;

/**
 * Classifies one answer from `GET /api/2fa/status`.
 *
 * Kept pure and separate from the fetch so every row of the table above is
 * directly testable — this is the logic that decides who gets in.
 */
export function classifySecondFactor(status: number, body: unknown): SecondFactorResult {
  // A rejected token is not evidence about 2FA. It means we could not ask.
  if (status === 401 || status === 403) {
    return { outcome: "unknown", reason: "rejected" };
  }

  if (status < 200 || status >= 300) {
    return { outcome: "unknown", reason: "backend" };
  }

  const enabled = (body as { enabled?: unknown } | null | undefined)?.enabled;

  if (enabled === true) return { outcome: "required" };
  if (enabled === false) return { outcome: "not-enrolled" };

  // A 200 we cannot read is not a 200 we can act on.
  return { outcome: "unknown", reason: "malformed" };
}

type Attempt =
  | { reached: true; status: number; body: unknown }
  | { reached: false };

async function attempt(accessToken: string, timeoutMs: number): Promise<Attempt> {
  try {
    const response = await fetch(`${API_BASE}/api/2fa/status`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
      signal: AbortSignal.timeout(timeoutMs),
    });

    let body: unknown = null;
    try {
      body = await response.json();
    } catch {
      // Leave body null — classification treats an unreadable 2xx as unknown.
    }

    return { reached: true, status: response.status, body };
  } catch {
    return { reached: false };
  }
}

/**
 * Authoritative answer for one user, used by both the middleware gate and the
 * `/api/2fa/gate-status` route so the two can never disagree.
 */
export async function checkSecondFactor(
  accessToken: string | null,
): Promise<SecondFactorResult> {
  if (!accessToken) {
    return { outcome: "unknown", reason: "no-session" };
  }

  const first = await attempt(accessToken, FIRST_ATTEMPT_TIMEOUT_MS);
  if (first.reached) {
    return classifySecondFactor(first.status, first.body);
  }

  // Only network failures are retried; a backend that answered has answered.
  const second = await attempt(accessToken, RETRY_TIMEOUT_MS);
  if (second.reached) {
    return classifySecondFactor(second.status, second.body);
  }

  return { outcome: "unknown", reason: "unreachable" };
}
