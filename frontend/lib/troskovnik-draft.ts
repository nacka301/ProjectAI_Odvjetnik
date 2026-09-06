"use client";

/**
 * Keeps an unfinished troškovnik alive across a sign-out.
 *
 * The loss this exists for: the session guard signs a user out after 14 minutes
 * idle. A lawyer who filled in the value of the dispute, twelve tariff items and
 * the parties, then took a phone call, came back to the login screen and lost
 * every keystroke. Nothing in the app wrote any of it down.
 *
 * ── Why sessionStorage and not localStorage ──
 *
 * The contents of a troškovnik are client names, OIBs, case numbers and amounts
 * — privileged material. localStorage writes that to disk and leaves it there
 * for the next person who opens the browser on that machine, which for a shared
 * office computer is exactly the exposure the rest of this app works to avoid
 * (HttpOnly cookies, no tokens in storage, row-level security in Postgres).
 *
 * sessionStorage is scoped to the browser tab and never outlives it. That is
 * enough, because the failure being fixed happens IN the tab: the idle logout
 * navigates to /auth/login without closing anything, so the draft is still there
 * when the lawyer signs back in. Closing the browser loses the draft — an
 * acceptable trade for not leaving case files on someone else's disk.
 *
 * The key carries the user id on top of that, so two lawyers sharing a machine
 * cannot be shown each other's work even within one tab.
 */

// v2: `podnesciElektronicki` je s `boolean` prešao na `string[]` (čl. 7 st. 1 po
// pojedinoj radnji, kao odluke). Stari nacrt s boolean vrijednošću bi srušio
// `vratiNacrt`, pa se odbacuje — nacrti su ionako kratkotrajni (sessionStorage).
const VERSION = 2;

/** Drafts older than this are ignored — a stale form is worse than none. */
const MAX_AGE_MS = 12 * 60 * 60 * 1000;

interface Envelope<T> {
  version: number;
  savedAt: number;
  userId: string;
  data: T;
}

function keyFor(userId: string): string {
  return `pravotek_troskovnik_draft_${userId}`;
}

export function saveDraft<T>(userId: string | null, data: T): void {
  if (!userId) return;
  try {
    const envelope: Envelope<T> = {
      version: VERSION,
      savedAt: Date.now(),
      userId,
      data,
    };
    sessionStorage.setItem(keyFor(userId), JSON.stringify(envelope));
  } catch {
    // Quota exceeded or storage disabled. Losing the draft is the status quo
    // ante, so this must never surface as an error to the user.
  }
}

export interface LoadedDraft<T> {
  data: T;
  savedAt: Date;
}

export function loadDraft<T>(userId: string | null): LoadedDraft<T> | null {
  if (!userId) return null;
  try {
    const raw = sessionStorage.getItem(keyFor(userId));
    if (!raw) return null;

    const envelope = JSON.parse(raw) as Envelope<T>;

    // A draft written by an older build may not line up with today's fields.
    // Dropping it is safer than restoring half a form the user cannot see is
    // half — they would submit it believing it was what they typed.
    if (envelope.version !== VERSION) return null;
    if (envelope.userId !== userId) return null;
    if (Date.now() - envelope.savedAt > MAX_AGE_MS) return null;

    return { data: envelope.data, savedAt: new Date(envelope.savedAt) };
  } catch {
    return null;
  }
}

export function clearDraft(userId: string | null): void {
  if (!userId) return;
  try {
    sessionStorage.removeItem(keyFor(userId));
  } catch {
    // Nothing to do — an unreadable store is also an unwritable one.
  }
}
