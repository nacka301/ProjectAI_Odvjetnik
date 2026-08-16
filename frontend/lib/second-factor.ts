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

/**
 * Statuses that mean "not now", not "no".
 *
 * 503 is the backend saying it could not check — either the MFA store or, since
 * the sign-in outage, the JWT verifier itself. Those recover on their own, often
 * within a second or two, so they are worth one more ask. A 401 is not on this
 * list: now that the backend answers 503 when it cannot verify a signature, a
 * 401 genuinely means the token did not hold up, and asking again with the same
 * token would only produce the same answer.
 */
function isTransient(status: number): boolean {
  return status === 429 || status === 502 || status === 503 || status === 504;
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
    return report({ outcome: "unknown", reason: "no-session" });
  }

  const first = await attempt(accessToken, FIRST_ATTEMPT_TIMEOUT_MS);

  // A backend that gave a real verdict has answered. One that timed out, threw,
  // or said "not now" is worth asking again — a cold Render instance takes over
  // ten seconds to serve its first request, which is longer than one attempt.
  if (first.reached && !isTransient(first.status)) {
    return report(classifySecondFactor(first.status, first.body), first);
  }

  const second = await attempt(accessToken, RETRY_TIMEOUT_MS);
  if (second.reached) {
    return report(classifySecondFactor(second.status, second.body), second);
  }

  // The retry got nowhere, but the first attempt did produce a real answer —
  // reporting "unreachable" would throw away what we actually learned.
  if (first.reached) {
    return report(classifySecondFactor(first.status, first.body), first);
  }

  return report({ outcome: "unknown", reason: "unreachable" });
}

/**
 * Leaves a trace whenever someone is stopped at the gate.
 *
 * This runs in the edge middleware and in the gate-status route, so the line
 * lands in the platform's runtime logs for both — which is the only reason the
 * next lockout will be visible without a user writing in to say they cannot get
 * in. Deliberately `console.error`, so it can be filtered by level.
 *
 * Never logs the token, and never logs a claim from it. The status, the reason
 * and the backend's own error string are enough to name the branch.
 */
function report(result: SecondFactorResult, attempted?: Attempt): SecondFactorResult {
  if (result.outcome !== "unknown") return result;

  const backendError =
    attempted?.reached &&
    typeof (attempted.body as { error?: unknown } | null)?.error === "string"
      ? (attempted.body as { error: string }).error
      : null;

  console.error(
    "[2fa-gate] pristup zaustavljen",
    JSON.stringify({
      reason: result.reason,
      status: attempted?.reached ? attempted.status : null,
      backendError,
    }),
  );

  return result;
}
