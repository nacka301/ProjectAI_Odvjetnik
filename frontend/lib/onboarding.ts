"use client";

import { createSupabaseBrowser } from "@/lib/supabase/client";

/**
 * "Is this user new?" — answered from the database, not from the browser.
 *
 * The old answer was `localStorage.pravotek_tour_seen`, which is a fact about a
 * browser profile rather than about a person. A lawyer who registered on a
 * desktop and opened the app on a phone was "new" again; one who cleared their
 * history was "new" again; and there was no way to tell whether anyone had ever
 * finished the introduction.
 *
 * `user_profiles.onboarded_at` (migration 022) is the single truth, and the ONLY
 * one. An earlier version kept a `localStorage` hint to skip the query for people
 * known to have finished — but that hint was global to the browser, not tied to a
 * `user.id`, so a second person signing in on the same browser inherited the
 * first person's "already onboarded" and never got the tour. The cache is gone:
 * every entry point asks the database for the current user, every time. The tour
 * starts closed and opens only on `isNew === true`, so nothing flashes while the
 * query is in flight — there is no need for a local shortcut.
 */

export interface OnboardingState {
  /** Null while unknown (not signed in, or the query failed). */
  isNew: boolean | null;
  fullName: string;
  email: string;
  firmId: string | null;
  firmName: string;
  /**
   * True when the firm is called after the user's own email address.
   *
   * `handle_new_user` falls back to `NEW.email` when signup carried no
   * `firm_name` — which is every Google registration, because OAuth has no
   * form to carry it. The result is an office named "ivan@gmail.com" sitting in
   * the settings screen. The welcome page asks for the real name instead.
   */
  firmNameIsPlaceholder: boolean;
}

export const UNKNOWN_ONBOARDING: OnboardingState = {
  isNew: null,
  fullName: "",
  email: "",
  firmId: null,
  firmName: "",
  firmNameIsPlaceholder: false,
};

export async function loadOnboardingState(): Promise<OnboardingState> {
  const supabase = createSupabaseBrowser();

  const {
    data: { user },
  } = await supabase.auth.getUser();
  if (!user) return UNKNOWN_ONBOARDING;

  // One query, two tables: the firm name is needed on the same screen and a
  // second round trip would show the greeting before the office name loads.
  const { data, error } = await supabase
    .from("user_profiles")
    .select("full_name, email, onboarded_at, law_firm_id, law_firms(name)")
    .eq("user_id", user.id)
    .single();

  if (error || !data) return UNKNOWN_ONBOARDING;

  const profile = data as {
    full_name: string | null;
    email: string | null;
    onboarded_at: string | null;
    law_firm_id: string | null;
    law_firms: { name: string | null } | { name: string | null }[] | null;
  };

  // PostgREST returns an embedded row as an object or a one-element array
  // depending on how it infers the relationship. Handle both rather than
  // depending on which one this schema happens to produce.
  const firm = Array.isArray(profile.law_firms) ? profile.law_firms[0] : profile.law_firms;

  const email = profile.email ?? user.email ?? "";
  const firmName = firm?.name ?? "";

  return {
    isNew: profile.onboarded_at === null,
    fullName: profile.full_name?.trim() || "",
    email,
    firmId: profile.law_firm_id,
    firmName,
    firmNameIsPlaceholder:
      firmName.length === 0 ||
      (email.length > 0 && firmName.toLowerCase() === email.toLowerCase()),
  };
}

/**
 * Marks the introduction as done. Called both when the user finishes it and
 * when they skip it — "do not show me this again" is the same instruction
 * either way, and splitting it into two states would only create a third screen
 * nobody asked for.
 */
export async function markOnboarded(): Promise<void> {
  try {
    const supabase = createSupabaseBrowser();
    const {
      data: { user },
    } = await supabase.auth.getUser();
    if (!user) return;

    await supabase
      .from("user_profiles")
      .update({ onboarded_at: new Date().toISOString() })
      .eq("user_id", user.id);
  } catch {
    // The caller has already closed the tour in this session, so a failed write
    // only means the tour may reappear on the next visit — never a blocker to
    // getting on with the work.
  }
}

/** Saves the real office name over the email-address placeholder. */
export async function saveFirmName(firmId: string, name: string): Promise<boolean> {
  const trimmed = name.trim();
  if (!trimmed) return false;

  try {
    const supabase = createSupabaseBrowser();
    // Only the firm owner may do this (policy `firms_update_owner`, migration
    // 016). A member who was invited into someone else's office gets a silent
    // no-op, which is correct — it is not their office to rename.
    const { error } = await supabase
      .from("law_firms")
      .update({ name: trimmed })
      .eq("id", firmId);

    return !error;
  } catch {
    return false;
  }
}
