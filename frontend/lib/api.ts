/**
 * Centralized API base URL.
 * Uses NEXT_PUBLIC_API_URL env var in production, falls back to local dev server.
 * 
 * IMPORTANT: All frontend files should import from here instead of
 * defining their own API constant to avoid port inconsistencies.
 */
export const API_BASE =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";

/**
 * Turns a caught fetch() failure into a Croatian, user-facing message.
 *
 * fetch() throws a `TypeError` ("Failed to fetch") only when no HTTP
 * response ever arrived — CORS, mixed content, DNS, offline. That's a
 * different situation than a request that reached the server and got a
 * bad status, so we name it explicitly instead of forwarding the
 * browser's English text. Callers should still `console.error` the raw
 * `err` for diagnostics; this string is only what the user sees.
 */
export function describeFetchError(err: unknown): string {
  if (err instanceof TypeError) {
    return "Poslužitelj trenutno nije dostupan. Provjerite internetsku vezu ili pokušajte ponovno kasnije.";
  }
  return "Došlo je do neočekivane greške. Pokušajte ponovno kasnije.";
}
