/**
 * V42: Norm Validity Engine — Frontend API client
 * Provides typed access to /api/norm endpoints.
 */

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

// ── Types ──

export interface NormInfo {
  abbreviation: string;
  full_name: string;
  status: "Važeći" | "Izmijenjen" | "Ukinut";
  nn_reference: string;
  nn_reference_short: string;
  amendments: NormAmendment[];
  last_amendment_date: string;
  original_date: string;
  category: string;
  nn_url: string;
}

export interface NormAmendment {
  nn_number: string;
  publication_date: string;
  type: string;
  description: string;
}

export interface CitedNorm {
  abbreviation: string;
  full_name: string;
  article: string;
  paragraph: string;
  point: string;
  nn_reference: string;
  status: "VAŽEĆI" | "IZMIJENJEN" | "UKINUT" | string;
  formatted_citation: string;
  nn_url: string;
}

export interface TemporalNormResult {
  abbreviation: string;
  full_name: string;
  applicable_nn_version: string;
  current_nn_version: string;
  applicable_nn_reference: string;
  is_outdated: boolean;
  warning: string;
  formatted_citation: string;
  event_date: string;
}

// ── API Functions ──

/** Fetch all registered norms */
export async function fetchAllNorms(): Promise<NormInfo[]> {
  const res = await fetch(`${API}/api/norm`);
  if (!res.ok) throw new Error("Failed to fetch norms");
  return res.json();
}

/** Fetch a single norm by abbreviation */
export async function fetchNorm(abbreviation: string): Promise<NormInfo | null> {
  const res = await fetch(`${API}/api/norm/${encodeURIComponent(abbreviation)}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Failed to fetch norm: ${abbreviation}`);
  return res.json();
}

/** Temporal lookup — which version was in force on a given date */
export async function fetchNormAtDate(
  abbreviation: string,
  date: string
): Promise<TemporalNormResult | null> {
  const res = await fetch(`${API}/api/norm/${encodeURIComponent(abbreviation)}/at/${date}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Temporal lookup failed: ${abbreviation} at ${date}`);
  return res.json();
}

/** Batch temporal lookup for multiple norms */
export async function fetchTemporalBatch(
  abbreviations: string[],
  eventDate: string
): Promise<TemporalNormResult[]> {
  const res = await fetch(`${API}/api/norm/temporal`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ event_date: eventDate, abbreviations }),
  });
  if (!res.ok) throw new Error("Batch temporal lookup failed");
  return res.json();
}

/** Format a citation in court style */
export async function formatCitation(
  abbreviation: string,
  article: string,
  paragraph?: string,
  point?: string,
  eventDate?: string
): Promise<CitedNorm> {
  const res = await fetch(`${API}/api/norm/cite`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      abbreviation,
      article,
      paragraph: paragraph ?? "",
      point: point ?? "",
      event_date: eventDate ?? null,
      court_style: true,
    }),
  });
  if (!res.ok) throw new Error("Citation formatting failed");
  return res.json();
}

// ── Helpers ──

/** Get badge color class based on norm validity status */
export function getStatusColor(status: string): {
  bg: string;
  text: string;
  dot: string;
  label: string;
} {
  switch (status?.toUpperCase()) {
    case "VAŽEĆI":
      return { bg: "bg-green-50", text: "text-green-700", dot: "bg-green-500", label: "Važeći" };
    case "IZMIJENJEN":
      return { bg: "bg-amber-50", text: "text-amber-700", dot: "bg-amber-500", label: "Izmijenjen" };
    case "UKINUT":
      return { bg: "bg-red-50", text: "text-red-700", dot: "bg-red-500", label: "Ukinut" };
    default:
      return { bg: "bg-gray-50", text: "text-gray-600", dot: "bg-gray-400", label: status || "Nepoznat" };
  }
}
