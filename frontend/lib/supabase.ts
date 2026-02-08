import { createClient } from "@supabase/supabase-js";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

export const supabase =
  supabaseUrl && supabaseAnonKey
    ? createClient(supabaseUrl, supabaseAnonKey)
    : null;

/**
 * Upload a PDF to the 'zk-izvadci' Supabase Storage bucket.
 * Returns the public URL, or null if Supabase is not configured.
 */
export async function uploadPdfToStorage(
  file: File
): Promise<string | null> {
  if (!supabase) {
    console.warn("Supabase not configured — skipping PDF upload to storage.");
    return null;
  }

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
    console.error("Supabase storage upload error:", error.message);
    return null;
  }

  const {
    data: { publicUrl },
  } = supabase.storage.from("zk-izvadci").getPublicUrl(path);

  return publicUrl;
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
