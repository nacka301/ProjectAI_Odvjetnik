// ── Password validation ─────────────────────────────────────────
// Min 12 chars, 1 uppercase, 1 number, 1 special char.
// 12 is the floor for attorney-client data; the same minimum must also be set
// in Supabase → Authentication → Providers → Email, since this check is only UX.

export interface PasswordCheck {
  label: string;
  met: boolean;
}

const MIN_PASSWORD_LENGTH = 12;

const SPECIAL_CHARS = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?`~]/;

export function validatePassword(pw: string): PasswordCheck[] {
  return [
    { label: `Minimalno ${MIN_PASSWORD_LENGTH} znakova`, met: pw.length >= MIN_PASSWORD_LENGTH },
    { label: "Jedno veliko slovo", met: /[A-Z]/.test(pw) },
    { label: "Jedan broj", met: /[0-9]/.test(pw) },
    { label: "Jedan specijalni znak (!@#$...)", met: SPECIAL_CHARS.test(pw) },
  ];
}

export function isPasswordValid(pw: string): boolean {
  return validatePassword(pw).every((c) => c.met);
}

// ── Email validation ────────────────────────────────────────────

export function isEmailValid(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

// ── OIB ─────────────────────────────────────────────────────────
// Re-exported from lib/utils so the signup form and the rest of the app cannot
// drift apart on what a valid OIB is. Registration used to accept "any eleven
// digits", which passes for 24 out of 25 mistyped numbers — and the office OIB
// it records goes on to print on every troškovnik and invoice the firm issues.
export { validateOib } from "@/lib/utils";

// ── Rate-limit for brute-force protection ───────────────────────
// Client-side tracking — the real enforcement is Supabase-side,
// but this gives immediate UX feedback.

const LOGIN_KEY = "pravotek_login_attempts";
const MAX_ATTEMPTS = 5;
const LOCKOUT_MS = 15 * 60 * 1000; // 15 min

interface AttemptRecord {
  count: number;
  firstAttempt: number;
}

function getRecord(): AttemptRecord {
  try {
    const raw = sessionStorage.getItem(LOGIN_KEY);
    if (raw) return JSON.parse(raw);
  } catch { /* ignore */ }
  return { count: 0, firstAttempt: Date.now() };
}

function saveRecord(rec: AttemptRecord) {
  try {
    sessionStorage.setItem(LOGIN_KEY, JSON.stringify(rec));
  } catch { /* ignore */ }
}

export function recordFailedAttempt(): void {
  const rec = getRecord();
  if (Date.now() - rec.firstAttempt > LOCKOUT_MS) {
    saveRecord({ count: 1, firstAttempt: Date.now() });
  } else {
    saveRecord({ ...rec, count: rec.count + 1 });
  }
}

export function clearAttempts(): void {
  try { sessionStorage.removeItem(LOGIN_KEY); } catch { /* ignore */ }
}

export function isLockedOut(): { locked: boolean; remainingSeconds: number } {
  const rec = getRecord();
  if (rec.count >= MAX_ATTEMPTS) {
    const elapsed = Date.now() - rec.firstAttempt;
    if (elapsed < LOCKOUT_MS) {
      return { locked: true, remainingSeconds: Math.ceil((LOCKOUT_MS - elapsed) / 1000) };
    }
    // Lockout expired — reset
    clearAttempts();
  }
  return { locked: false, remainingSeconds: 0 };
}

export function attemptsRemaining(): number {
  const rec = getRecord();
  if (Date.now() - rec.firstAttempt > LOCKOUT_MS) return MAX_ATTEMPTS;
  return Math.max(0, MAX_ATTEMPTS - rec.count);
}
