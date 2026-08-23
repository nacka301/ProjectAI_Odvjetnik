// Shared between the client form (components/landing/WaitlistForm.tsx) and
// the route handler (app/api/waitlist/route.ts) so the two validations can't
// drift apart. Exact regex is fixed by the design handoff — not the looser
// one in lib/auth-validation.ts, which is for the login/register forms.
export const WAITLIST_EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

export function isWaitlistEmailValid(email: string): boolean {
  return WAITLIST_EMAIL_RE.test(email.trim());
}

export interface RateLimitRecord {
  count: number;
  windowStart: number;
}

export interface RateLimitStore {
  get(ip: string): RateLimitRecord | undefined;
  set(ip: string, value: RateLimitRecord): void;
}

/**
 * Pure, injectable-clock rate limiter — no module-level Map or Date.now()
 * here so it's directly testable. The route handler supplies its own Map
 * and Date.now(); see the caveat there about that Map only holding state
 * within one warm serverless instance.
 */
export function checkRateLimit(
  store: RateLimitStore,
  ip: string,
  now: number,
  opts: { windowMs: number; max: number },
): boolean {
  const record = store.get(ip);
  if (!record || now - record.windowStart > opts.windowMs) {
    store.set(ip, { count: 1, windowStart: now });
    return false;
  }
  record.count += 1;
  return record.count > opts.max;
}
