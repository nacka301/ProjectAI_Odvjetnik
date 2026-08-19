import { describe, it, expect } from "vitest";
import { generateNonce } from "@/lib/google-nonce";

/**
 * Google gets the hashed nonce, Supabase gets the raw one. Swapping them is the
 * single most common way this integration breaks, and it breaks silently: the
 * button works, Google returns a valid token, and Supabase rejects it with a
 * nonce error that points at nothing in particular.
 *
 * These tests pin the relationship between the two values so the mistake is
 * caught here rather than by a lawyer who cannot sign in.
 */

/** The comparison Supabase performs on its side, reproduced independently. */
async function sha256Hex(input: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(input));
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

describe("generateNonce", () => {
  it("hashed is the SHA-256 of raw, which is what Supabase checks", async () => {
    const { raw, hashed } = await generateNonce();

    expect(hashed).toBe(await sha256Hex(raw));
  });

  it("the two forms are never interchangeable", async () => {
    const { raw, hashed } = await generateNonce();

    // If these were ever equal, passing the wrong one would still work here and
    // fail only in production against Google's real token.
    expect(raw).not.toBe(hashed);
    expect(hashed).toMatch(/^[0-9a-f]{64}$/);
  });

  it("mints a different nonce every call", async () => {
    const [first, second] = await Promise.all([generateNonce(), generateNonce()]);

    // A reused nonce is a replayable one: a token captured from an earlier
    // sign-in would satisfy the check on a later one.
    expect(first.raw).not.toBe(second.raw);
  });
});
