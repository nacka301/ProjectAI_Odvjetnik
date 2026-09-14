/**
 * The Supabase project this frontend talks to.
 *
 * On Vercel, NEXT_PUBLIC_SUPABASE_URL / NEXT_PUBLIC_SUPABASE_ANON_KEY are
 * synced by the Supabase↔Vercel marketplace integration and are set for
 * "All Environments" — always the PRODUCTION project. Vercel's UI does not
 * let an integration-managed variable be edited or scoped per environment
 * (its menu offers only Manage Connection / Rotate / Delete, no Edit), so a
 * Preview-only override cannot be applied to those two names directly.
 *
 * NEXT_PUBLIC_SUPABASE_URL_PREVIEW / NEXT_PUBLIC_SUPABASE_ANON_KEY_PREVIEW are
 * ordinary variables — not integration-owned — set ONLY on the Preview
 * environment in Vercel, pointing at the dev Supabase project
 * (ai-odvjetnik-dev). When present they win; they are simply unset on
 * Production, so this never touches what production sees.
 *
 * All Supabase client construction should import from here instead of
 * reading process.env directly, so there is exactly one place this override
 * lives (mirrors the API_BASE pattern in lib/api.ts).
 */
export const SUPABASE_URL =
  process.env.NEXT_PUBLIC_SUPABASE_URL_PREVIEW || process.env.NEXT_PUBLIC_SUPABASE_URL!;

export const SUPABASE_ANON_KEY =
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY_PREVIEW || process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;
