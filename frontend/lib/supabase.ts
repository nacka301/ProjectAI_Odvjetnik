import { createBrowserClient } from "@supabase/ssr";
import { isModuleLocked } from "@/lib/modules";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

/**
 * Auth-aware Supabase browser client.
 * Uses @supabase/ssr's createBrowserClient — consistent with the HttpOnly cookie
 * flow used by the rest of the app (middleware, server components, useUserId hook).
 * This ensures RLS sees the correct auth.uid() on direct Supabase queries
 * (history page, dashboard, delete operations).
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
//  Troškovnici
// ═══════════════════════════════════════════════════════════

/**
 * A saved troškovnik, as the `troskovnici` table stores it.
 *
 * Read straight from Supabase rather than through the backend: RLS scopes the
 * table to the signed-in user's firm (`troskovnici_select_firm`), which is the
 * same boundary the API would enforce, and the history page already reads every
 * other module this way.
 */
export interface TroskovnikRow {
  id: string;
  created_at: string;
  client_id: string | null;
  case_id: string | null;
  opis_predmeta: string | null;
  vrijednost_spora: number | null;
  ukupno_bodova: number | null;
  ukupno_eur_bez_pdv: number | null;
  pdv: number | null;
  ukupno_eur_s_pdv: number | null;
  predmet: { sud?: string | null; poslovni_broj?: string | null } | null;
  stranke: {
    zastupana_stranka?: { polozaj?: string | null; naziv?: string | null; oib?: string | null; adresa?: string | null } | null;
    protustranka?: { polozaj?: string | null; naziv?: string | null; oib?: string | null; adresa?: string | null } | null;
  } | null;
  odvjetnik: { naziv?: string | null; oib?: string | null; adresa?: string | null; iban?: string | null } | null;
  raw_json_output: Record<string, unknown> | null;
}

export async function getTroskovnici(limit = 50): Promise<TroskovnikRow[]> {
  if (!supabase) {
    console.warn("Supabase not configured — cannot fetch troškovnici.");
    return [];
  }

  const { data, error } = await supabase
    .from("troskovnici")
    .select("*")
    .order("created_at", { ascending: false })
    .limit(limit);

  if (error) {
    console.error("Supabase troskovnici error:", error.message);
    return [];
  }

  return (data as TroskovnikRow[]) ?? [];
}

/** One saved troškovnik by id, or null when it does not exist or RLS hides it. */
export async function getTroskovnik(id: string): Promise<TroskovnikRow | null> {
  if (!supabase) return null;

  const { data, error } = await supabase
    .from("troskovnici")
    .select("*")
    .eq("id", id)
    .maybeSingle();

  if (error) {
    console.error("Supabase troskovnik error:", error.message);
    return null;
  }

  return (data as TroskovnikRow | null) ?? null;
}

/** Delete a troškovnik by ID. */
export async function deleteTroskovnik(id: string): Promise<boolean> {
  if (!supabase) return false;
  const { error } = await supabase.from("troskovnici").delete().eq("id", id);
  if (error) {
    console.error("Failed to delete troskovnik:", error.message);
    return false;
  }
  return true;
}

// ═══════════════════════════════════════════════════════════
//  Unified Activity Feed — all modules combined
// ═══════════════════════════════════════════════════════════

export type ActivityType = "troskovnik" | "valuation" | "contract" | "chronology" | "legal" | "invoice";

/**
 * Which module in `lib/modules.ts` each activity type belongs to.
 *
 * Only the types the feed still reads are listed. `chronology`, `legal` and
 * `invoice` survive in {@link ActivityType} solely so the delete dispatcher can
 * clean up rows those retired modules left behind; they are never produced.
 *
 * The mapping exists so the feed can leave out records from modules the viewer
 * cannot open. Listing them offered a row that led nowhere — the middleware
 * bounces the route — and put "Procjene" and "Ugovori" in front of users whose
 * only working module is the troškovnik.
 */
const ACTIVITY_MODULE: Partial<Record<ActivityType, string>> = {
  troskovnik: "troskovnik",
  valuation: "procjena",
  contract: "ugovor",
};

/** Whether records of this type belong in front of this viewer. */
export function isActivityTypeVisible(type: ActivityType, isAdmin: boolean): boolean {
  const moduleId = ACTIVITY_MODULE[type];
  // No module means a retired one. Nothing links to it, so nothing should list it.
  return moduleId !== undefined && !isModuleLocked(moduleId, isAdmin);
}

export interface ActivityItem {
  id: string;
  created_at: string;
  type: ActivityType;
  title: string;
  subtitle: string;
  badge?: string;
  badgeColor?: string;
}

const EUR = new Intl.NumberFormat("hr-HR", {
  style: "currency",
  currency: "EUR",
  minimumFractionDigits: 2,
});

/**
 * What the troškovnik's saved response says the case was about.
 *
 * The response carries the tariff category on every line item, so the procedure
 * type is whatever the chosen items belong to. Mixed selections are legitimate
 * (a parnični troškovnik can carry an ovršni step), so they are named as such
 * rather than silently reporting the first one.
 */
function vrstaPostupka(raw: Record<string, unknown> | null): string | undefined {
  const stavke = (raw?.stavke as { kategorija?: string }[] | undefined) ?? [];
  const kategorije = [...new Set(stavke.map((s) => s.kategorija).filter(Boolean))];

  if (kategorije.length === 0) return undefined;
  if (kategorije.length === 1) return kategorije[0];
  return `${kategorije[0]} +${kategorije.length - 1}`;
}

/**
 * The figure the lawyer filed. `sveukupni_trosak` includes the court fee and
 * advances; `ukupno_eur_s_pdv` is only the fee plus VAT, so falling back to it
 * would quietly under-report older records rather than showing nothing.
 */
function ukupanIznos(t: TroskovnikRow): string {
  const raw = t.raw_json_output;
  const sveukupno = raw?.sveukupni_trosak as number | undefined;
  const iznos = sveukupno ?? t.ukupno_eur_s_pdv;
  return iznos == null ? "—" : EUR.format(iznos);
}

/**
 * What names this troškovnik: the represented party, the court reference, the
 * free-text description — whichever of them the record actually has.
 *
 * The strongest available one becomes the title and the rest trail behind it,
 * so nothing identifying is dropped and nothing is repeated. "Troškovnik" is
 * the last resort for records saved with none of the three filled in.
 */
function prepoznatljivPredmet(t: TroskovnikRow): { title: string; ostatak: string } {
  const kandidati = [
    t.stranke?.zastupana_stranka?.naziv?.trim(),
    [t.predmet?.sud?.trim(), t.predmet?.poslovni_broj?.trim()].filter(Boolean).join(", "),
    t.opis_predmeta?.trim(),
  ].filter((s): s is string => !!s);

  return {
    title: kandidati[0] ?? "Troškovnik",
    ostatak: kandidati.slice(1).join(" · "),
  };
}

/**
 * Fetch recent activity from the module tables the viewer can actually open,
 * merged and sorted by date.
 *
 * `isAdmin` decides which modules count as open — see {@link isActivityTypeVisible}.
 * Locked modules are not merely hidden after the fact: their tables are never
 * queried, so a locked module costs nothing.
 */
export async function getRecentActivity(
  limitPerTable = 10,
  isAdmin = false,
): Promise<ActivityItem[]> {
  const wants = (t: ActivityType) => isActivityTypeVisible(t, isAdmin);

  const [troskovnici, evals, contracts] = await Promise.all([
    wants("troskovnik") ? getTroskovnici(limitPerTable) : Promise.resolve([]),
    wants("valuation") ? getEvaluations(limitPerTable) : Promise.resolve([]),
    wants("contract") ? getContractAnalyses(limitPerTable) : Promise.resolve([]),
  ]);

  const items: ActivityItem[] = [];

  for (const t of troskovnici) {
    const { title, ostatak } = prepoznatljivPredmet(t);
    items.push({
      id: t.id,
      created_at: t.created_at,
      type: "troskovnik",
      title,
      subtitle: [ostatak, vrstaPostupka(t.raw_json_output)].filter(Boolean).join(" · "),
      badge: ukupanIznos(t),
      badgeColor: "indigo",
    });
  }

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
    case "troskovnik":  return deleteTroskovnik(id);
    case "valuation":   return deleteEvaluation(id);
    case "contract":    return deleteContractAnalysis(id);
    case "chronology":  return deleteChronology(id);
    case "legal":       return deleteLegalStrategy(id);
    case "invoice":     return deleteInvoice(id);
    default:            return false;
  }
}
