/**
 * The two forms of the nonce that Google Identity Services and Supabase need.
 *
 * This is the single place either form is produced, and it exists as its own
 * module for one reason: swapping them is the classic way this integration
 * fails, and the failure surfaces as an opaque Supabase rejection at sign-in
 * rather than as anything a reader of the component would notice.
 *
 * Google receives the HASHED value — it is what ends up inside the signed ID
 * token, as the `nonce` claim. Supabase receives the RAW value and hashes it
 * itself to compare against that claim. Hand Google the raw value, or Supabase
 * the hashed one, and the comparison fails on every attempt.
 */

/** Raw goes to Supabase, hashed goes to Google. Never the other way round. */
export interface NoncePair {
  /** Base64 of 32 random bytes. Passed to `signInWithIdToken({ nonce })`. */
  raw: string;
  /** SHA-256 of `raw`, lowercase hex. Passed to `google.accounts.id.initialize({ nonce })`. */
  hashed: string;
}

/**
 * Mint a fresh nonce pair.
 *
 * Call once per mounted sign-in surface and keep the result: regenerating
 * between `initialize()` and the credential callback leaves Google's token
 * carrying a hash of a nonce nobody holds any more, and Supabase refuses it.
 */
export async function generateNonce(): Promise<NoncePair> {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  const raw = btoa(String.fromCharCode(...bytes));

  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(raw));
  const hashed = Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");

  return { raw, hashed };
}
