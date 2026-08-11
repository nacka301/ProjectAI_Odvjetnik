import { createBrowserClient } from "@supabase/ssr";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

/**
 * Auth-aware Supabase browser client.
 * Uses @supabase/ssr's createBrowserClient — consistent with the HttpOnly cookie
 * flow used by the rest of the app (middleware, server components, useUserId hook).
 * This ensures RLS sees the correct auth.uid() on direct Supabase queries
 * (history page, dashboard, audit page, delete operations).
 */
export const supabase =
  supabaseUrl && supabaseAnonKey
    ? createBrowserClient(supabaseUrl, supabaseAnonKey)
    : null;

const ZK_BUCKET = "zk-izvadci";

/**
 * The signed-in user's law firm, read through RLS.
 *
 * Storage paths start with the firm id (migration 018), so uploads need it.
 * Reading it from `user_profiles` rather than trusting anything client-held
 * means a tampered value simply fails the storage policy.
 */
async function getUserFirmId(): Promise<string | null> {
  if (!supabase) return null;

  const { data: { user } } = await supabase.auth.getUser();
  if (!user) return null;

  const { data, error } = await supabase
    .from("user_profiles")
    .select("law_firm_id")
    .eq("user_id", user.id)
    .single();

  if (error) {
    console.warn("Nije moguće odrediti ured korisnika:", error.message);
    return null;
  }

  return (data?.law_firm_id as string | null) ?? null;
}

/**
 * Upload a PDF to the private 'zk-izvadci' bucket.
 *
 * Returns the STORAGE PATH, not a URL. The bucket used to be public and this
 * function handed back a permanent `getPublicUrl()` link — which made every
 * land-registry extract readable by anyone who had, guessed, or was forwarded
 * the address. Downloads now go through {@link getSignedPdfUrl}, which issues a
 * link that expires.
 *
 * Still optional: a failure here does not block the valuation.
 */
export async function uploadPdfToStorage(
  file: File
): Promise<string | null> {
  if (!supabase) {
    return null;
  }

  try {
    const firmId = await getUserFirmId();
    if (!firmId) {
      console.warn("PDF upload skipped: korisnik nije povezan s uredom.");
      return null;
    }

    const safeName = file.name.replace(/[^a-zA-Z0-9._-]/g, "_");
    // {law_firm_id}/... — the policy compares the first path segment, so the
    // prefix is what makes the file visible to the firm and nobody else.
    const path = `${firmId}/${crypto.randomUUID()}-${safeName}`;

    const { error } = await supabase.storage
      .from(ZK_BUCKET)
      .upload(path, file, {
        contentType: "application/pdf",
        upsert: false,
      });

    if (error) {
      console.warn("PDF upload skipped:", error.message);
      return null;
    }

    return path;
  } catch {
    // Completely swallow — PDF upload is optional
    return null;
  }
}

/**
 * Short-lived download link for a stored PDF.
 *
 * Sixty seconds is deliberate: long enough to click, short enough that a link
 * pasted into a chat or left in browser history is useless by the time anyone
 * else opens it. Returns null when the path belongs to another firm — RLS
 * refuses to sign it.
 */
export async function getSignedPdfUrl(
  path: string,
  expiresInSeconds = 60,
): Promise<string | null> {
  if (!supabase || !path) return null;

  const { data, error } = await supabase.storage
    .from(ZK_BUCKET)
    .createSignedUrl(path, expiresInSeconds);

  if (error) {
    console.warn("Nije moguće izdati privremeni link:", error.message);
    return null;
  }

  return data?.signedUrl ?? null;
}

/**
 * Save an evaluation result to the 'evaluations' table.
 * Returns the inserted row ID, or null if Supabase is not configured.
 */
export async function saveEvaluation(evaluation: {
  property_type: string;
  location: string;
  size_m2?: number;
  estimated_value: number;
  /**
   * Storage path inside the private bucket, e.g. `{law_firm_id}/{uuid}-name.pdf`.
   * Since migration 018 this is no longer a URL — pass it through
   * {@link getSignedPdfUrl} to get something openable.
   */
  pdf_url?: string | null;
  raw_json_output: Record<string, unknown>;
}): Promise<string | null> {
  if (!supabase) {
    console.warn("Supabase not configured — skipping evaluation save.");
    return null;
  }

  const { data, error } = await supabase
    .from("evaluations")
    .insert(evaluation)
    .select("id")
    .single();

  if (error) {
    console.error("Supabase insert error:", error.message);
    return null;
  }

  return data?.id ?? null;
}

/**
 * Fetch evaluation history from the 'evaluations' table.
 * Returns the most recent evaluations, ordered by created_at DESC.
 * Requires anon SELECT policy on evaluations (see schema.sql).
 */
export interface EvaluationRow {
  id: string;
  created_at: string;
  property_type: string;
  location: string;
  size_m2: number | null;
  estimated_value: number;
  /** Storage path, not a URL — see {@link getSignedPdfUrl}. */
  pdf_url: string | null;
  raw_json_output: Record<string, unknown>;
}

export async function getEvaluations(limit = 50): Promise<EvaluationRow[]> {
  if (!supabase) {
    console.warn("Supabase not configured — cannot fetch evaluations.");
    return [];
  }

  const { data, error } = await supabase
    .from("evaluations")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase select error:", error.message);
    return [];
  }

  return (data as EvaluationRow[]) ?? [];
}

// ═══════════════════════════════════════════════════════════
//  Contract Analyses
// ═══════════════════════════════════════════════════════════

export interface ContractAnalysisRow {
  id: string;
  created_at: string;
  document_type: string;
  summary: string;
  overall_risk: string;
  risk_score: number;
  red_flags_count: number;
  raw_json_output: Record<string, unknown>;
}

export async function getContractAnalyses(limit = 50): Promise<ContractAnalysisRow[]> {
  if (!supabase) return [];

  const { data, error } = await supabase
    .from("contract_analyses")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase contract_analyses error:", error.message);
    return [];
  }

  return (data as ContractAnalysisRow[]) ?? [];
}

// NOTE: Chronologies, Legal Strategies, and Invoices modules have been removed from the UI.
// Delete helpers are kept below for backward-compat cleanup of existing DB rows.

// ═══════════════════════════════════════════════════════════
//  Unified Activity Feed — all modules combined
// ═══════════════════════════════════════════════════════════

export type ActivityType = "valuation" | "contract" | "chronology" | "legal" | "invoice";

export interface ActivityItem {
  id: string;
  created_at: string;
  type: ActivityType;
  title: string;
  subtitle: string;
  badge?: string;
  badgeColor?: string;
}

/**
 * Fetch recent activity from ALL module tables, merged and sorted by date.
 */
export async function getRecentActivity(limitPerTable = 10): Promise<ActivityItem[]> {
  const [evals, contracts] = await Promise.all([
    getEvaluations(limitPerTable),
    getContractAnalyses(limitPerTable),
  ]);

  const items: ActivityItem[] = [];

  for (const e of evals) {
    const raw = e.raw_json_output as Record<string, unknown>;
    const score = (raw?.math_confidence_score as number) ?? 0;
    items.push({
      id: e.id,
      created_at: e.created_at,
      type: "valuation",
      title: `${e.location}`,
      subtitle: `${e.estimated_value?.toLocaleString("hr-HR") ?? "?"} € · ${e.size_m2 ?? "?"} m²`,
      badge: score >= 70 ? "Pouzdano" : score >= 40 ? "Srednje" : "Nisko",
      badgeColor: score >= 70 ? "green" : score >= 40 ? "amber" : "red",
    });
  }

  for (const c of contracts) {
    items.push({
      id: c.id,
      created_at: c.created_at,
      type: "contract",
      title: c.document_type || "Ugovor",
      subtitle: c.summary?.slice(0, 80) || "Analiza rizika",
      badge: c.overall_risk,
      badgeColor: c.overall_risk === "VISOK" ? "red" : c.overall_risk === "SREDNJI" ? "amber" : "green",
    });
  }

  // Sort by date descending
  items.sort((a, b) => new Date(b.created_at).getTime() - new Date(a.created_at).getTime());

  return items;
}

// ═══════════════════════════════════════════════════════════
//  Delete Records — actual Supabase deletion
// ═══════════════════════════════════════════════════════════

/**
 * Delete an evaluation and its dependent valuation_audits.
 * Also unlinks any zk_analyses referencing this evaluation.
 */
export async function deleteEvaluation(id: string): Promise<boolean> {
  if (!supabase) return false;

  // 1. Delete dependent valuation_audits (FK without CASCADE)
  const { error: auditErr } = await supabase
    .from("valuation_audits")
    .delete()
    .eq("evaluation_id", id);
  if (auditErr) {
    console.error("Failed to delete valuation_audits:", auditErr.message);
    return false;
  }

  // 2. Unlink zk_analyses (SET NULL on nullable FK)
  const { error: zkErr } = await supabase
    .from("zk_analyses")
    .update({ evaluation_id: null })
    .eq("evaluation_id", id);
  if (zkErr) {
    console.error("Failed to unlink zk_analyses:", zkErr.message);
    // Non-critical — continue
  }

  // 3. Delete the evaluation itself
  const { error } = await supabase
    .from("evaluations")
    .delete()
    .eq("id", id);

  if (error) {
    console.error("Failed to delete evaluation:", error.message);
    return false;
  }

  return true;
}

/** Delete a contract analysis by ID. */
export async function deleteContractAnalysis(id: string): Promise<boolean> {
  if (!supabase) return false;
  const { error } = await supabase.from("contract_analyses").delete().eq("id", id);
  if (error) {
    console.error("Failed to delete contract_analysis:", error.message);
    return false;
  }
  return true;
}

/** Delete a chronology by ID. */
export async function deleteChronology(id: string): Promise<boolean> {
  if (!supabase) return false;
  const { error } = await supabase.from("chronologies").delete().eq("id", id);
  if (error) {
    console.error("Failed to delete chronology:", error.message);
    return false;
  }
  return true;
}

/** Delete a legal strategy by ID. */
export async function deleteLegalStrategy(id: string): Promise<boolean> {
  if (!supabase) return false;

  const { error } = await supabase.from("legal_strategies").delete().eq("id", id);
  if (error) {
    console.error("Failed to delete legal_strategy:", error.message);
    return false;
  }
  return true;
}

/** Delete an invoice by ID. */
export async function deleteInvoice(id: string): Promise<boolean> {
  if (!supabase) return false;
  const { error } = await supabase.from("invoices").delete().eq("id", id);
  if (error) {
    console.error("Failed to delete invoice:", error.message);
    return false;
  }
  return true;
}

/**
 * Delete any activity item by type and ID — dispatcher.
 */
export async function deleteActivityItem(type: ActivityType, id: string): Promise<boolean> {
  switch (type) {
    case "valuation":   return deleteEvaluation(id);
    case "contract":    return deleteContractAnalysis(id);
    case "chronology":  return deleteChronology(id);
    case "legal":       return deleteLegalStrategy(id);
    case "invoice":     return deleteInvoice(id);
    default:            return false;
  }
}

// ═══════════════════════════════════════════════════════════
//  Valuation Audit Results (Perplexity AI Layer)
// ═══════════════════════════════════════════════════════════

export type AuditVerdict = "CONFIRMED" | "SUSPICIOUS" | "REJECTED";

export interface AuditRow {
  id: string;
  created_at: string;
  evaluation_id: string;
  verdict: AuditVerdict;
  overall_confidence: number;
  summary: string;
  recommendation: string;
  price_score: number;
  location_score: number;
  methodology_score: number;
  data_quality_score: number;
  findings_count: number;
  critical_count: number;
  model_used: string;
  prompt_tokens: number;
  completion_tokens: number;
  raw_json_output: Record<string, unknown>;
}

/**
 * Fetch all audit results, optionally filtered by verdict.
 */
export async function getAuditResults(
  limit = 50,
  verdict?: AuditVerdict
): Promise<AuditRow[]> {
  if (!supabase) return [];

  let query = supabase
    .from("valuation_audits")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (verdict) {
    query = query.eq("verdict", verdict);
  }

  const { data, error } = await query;

  if (error) {
    console.error("Supabase valuation_audits error:", error.message);
    return [];
  }

  return (data as AuditRow[]) ?? [];
}

/**
 * Fetch audit result for a specific evaluation.
 */
export async function getAuditForEvaluation(
  evaluationId: string
): Promise<AuditRow | null> {
  if (!supabase) return null;

  const { data, error } = await supabase
    .from("valuation_audits")
    .select("*")
    .eq("evaluation_id", evaluationId)
    .limit(1)
    .single();

  if (error) {
    // Not found is expected for unaudited evaluations
    if (error.code !== "PGRST116") {
      console.error("Supabase audit fetch error:", error.message);
    }
    return null;
  }

  return data as AuditRow;
}

/**
 * Fetch audit stats — counts per verdict.
 */
export async function getAuditStats(): Promise<{
  total: number;
  confirmed: number;
  suspicious: number;
  rejected: number;
}> {
  if (!supabase) return { total: 0, confirmed: 0, suspicious: 0, rejected: 0 };

  const { data, error } = await supabase
    .from("valuation_audits")
    .select("verdict");

  if (error) {
    console.error("Supabase audit stats error:", error.message);
    return { total: 0, confirmed: 0, suspicious: 0, rejected: 0 };
  }

  const rows = data ?? [];
  return {
    total: rows.length,
    confirmed: rows.filter((r) => r.verdict === "CONFIRMED").length,
    suspicious: rows.filter((r) => r.verdict === "SUSPICIOUS").length,
    rejected: rows.filter((r) => r.verdict === "REJECTED").length,
  };
}
