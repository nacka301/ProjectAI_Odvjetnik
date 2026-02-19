import { createClient } from "@supabase/supabase-js";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

export const supabase =
  supabaseUrl && supabaseAnonKey
    ? createClient(supabaseUrl, supabaseAnonKey)
    : null;

/**
 * Upload a PDF to the 'zk-izvadci' Supabase Storage bucket.
 * Returns the public URL, or null if Supabase is not configured or bucket doesn't exist.
 * This is fully optional — the evaluation works without it.
 */
export async function uploadPdfToStorage(
  file: File
): Promise<string | null> {
  if (!supabase) {
    return null;
  }

  try {
    const timestamp = Date.now();
    const safeName = file.name.replace(/[^a-zA-Z0-9._-]/g, "_");
    const path = `${timestamp}_${safeName}`;

    const { error } = await supabase.storage
      .from("zk-izvadci")
      .upload(path, file, {
        contentType: "application/pdf",
        upsert: false,
      });

    if (error) {
      // Bucket might not exist yet — this is non-critical
      console.warn("PDF upload skipped:", error.message);
      return null;
    }

    const {
      data: { publicUrl },
    } = supabase.storage.from("zk-izvadci").getPublicUrl(path);

    return publicUrl;
  } catch {
    // Completely swallow — PDF upload is optional
    return null;
  }
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

// ═══════════════════════════════════════════════════════════
//  Chronologies
// ═══════════════════════════════════════════════════════════

export interface ChronologyRow {
  id: string;
  created_at: string;
  total_documents: number;
  total_events: number;
  date_range: string;
  raw_json_output: Record<string, unknown>;
}

export async function getChronologies(limit = 50): Promise<ChronologyRow[]> {
  if (!supabase) return [];

  const { data, error } = await supabase
    .from("chronologies")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase chronologies error:", error.message);
    return [];
  }

  return (data as ChronologyRow[]) ?? [];
}

// ═══════════════════════════════════════════════════════════
//  Legal Strategies
// ═══════════════════════════════════════════════════════════

export interface LegalStrategyRow {
  id: string;
  created_at: string;
  key_legal_issue: string;
  client_position: string;
  win_probability: number;
  win_probability_min: number;
  win_probability_max: number;
  precedents_count: number;
  raw_json_output: Record<string, unknown>;
}

export async function getLegalStrategies(limit = 50): Promise<LegalStrategyRow[]> {
  if (!supabase) return [];

  const { data, error } = await supabase
    .from("legal_strategies")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase legal_strategies error:", error.message);
    return [];
  }

  return (data as LegalStrategyRow[]) ?? [];
}

// ═══════════════════════════════════════════════════════════
//  Invoice (e-Račun) rows
// ═══════════════════════════════════════════════════════════

export interface InvoiceRow {
  id: string;
  created_at: string;
  invoice_number: string;
  client_name: string;
  client_oib: string;
  gross_amount: number;
  module: string;
  status: string;
  case_reference?: string;
}

export async function getInvoices(limit = 50): Promise<InvoiceRow[]> {
  if (!supabase) return [];

  const { data, error } = await supabase
    .from("invoices")
    .select("id,created_at,invoice_number,client_name,client_oib,gross_amount,module,status,case_reference")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase invoices error:", error.message);
    return [];
  }

  return (data as InvoiceRow[]) ?? [];
}

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
  const [evals, contracts, chronos, legals, invoices] = await Promise.all([
    getEvaluations(limitPerTable),
    getContractAnalyses(limitPerTable),
    getChronologies(limitPerTable),
    getLegalStrategies(limitPerTable),
    getInvoices(limitPerTable),
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

  for (const ch of chronos) {
    items.push({
      id: ch.id,
      created_at: ch.created_at,
      type: "chronology",
      title: "Kronologija spisa",
      subtitle: `${ch.total_events} događaja · ${ch.total_documents} dokumenata`,
      badge: ch.date_range || undefined,
    });
  }

  for (const l of legals) {
    items.push({
      id: l.id,
      created_at: l.created_at,
      type: "legal",
      title: l.key_legal_issue?.slice(0, 60) || "Pravna analiza",
      subtitle: `Vjerojatnost: ${l.win_probability}% (${l.win_probability_min}–${l.win_probability_max}%)`,
      badge: `${l.win_probability}%`,
      badgeColor: l.win_probability >= 60 ? "green" : l.win_probability >= 35 ? "amber" : "red",
    });
  }

  for (const inv of invoices) {
    items.push({
      id: inv.id,
      created_at: inv.created_at,
      type: "invoice",
      title: `Račun ${inv.invoice_number}`,
      subtitle: `${inv.client_name} · ${inv.gross_amount?.toLocaleString("hr-HR")} €`,
      badge: inv.status,
      badgeColor: inv.status === "SENT" ? "green" : inv.status === "DRAFT" ? "amber" : "green",
    });
  }

  // Sort by date descending
  items.sort((a, b) => new Date(b.created_at).getTime() - new Date(a.created_at).getTime());

  return items;
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
