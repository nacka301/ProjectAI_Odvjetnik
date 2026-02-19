/**
 * Centralized API base URL.
 * Uses NEXT_PUBLIC_API_URL env var in production, falls back to local dev server.
 * 
 * IMPORTANT: All frontend files should import from here instead of
 * defining their own API constant to avoid port inconsistencies.
 */
export const API_BASE =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:5078";
