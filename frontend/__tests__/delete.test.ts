import { describe, it, expect, vi } from "vitest";

// ═══════════════════════════════════════════
//  Delete Activity Functions — Unit Tests
// ═══════════════════════════════════════════

// Set env vars BEFORE module import so supabase client is created
vi.stubEnv("NEXT_PUBLIC_SUPABASE_URL", "https://test.supabase.co");
vi.stubEnv("NEXT_PUBLIC_SUPABASE_ANON_KEY", "test-anon-key");

// Mock Supabase client — all .from().delete().eq() chains return { error: null }
vi.mock("@supabase/supabase-js", () => ({
  createClient: () => ({
    from: () => ({
      delete: () => ({
        eq: () => ({ error: null }),
      }),
      update: () => ({
        eq: () => ({ error: null }),
      }),
      select: () => ({
        order: () => ({
          limit: () => ({ data: [], error: null }),
        }),
        eq: () => ({
          limit: () => ({
            single: () => ({ data: null, error: { code: "PGRST116" } }),
          }),
        }),
      }),
      insert: () => ({
        select: () => ({
          single: () => ({ data: { id: "x" }, error: null }),
        }),
      }),
    }),
    storage: { from: () => ({ upload: vi.fn(), getPublicUrl: () => ({ data: { publicUrl: "" } }) }) },
  }),
}));

describe("delete function exports", () => {
  it("deleteEvaluation is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteEvaluation).toBe("function");
  });

  it("deleteContractAnalysis is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteContractAnalysis).toBe("function");
  });

  it("deleteChronology is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteChronology).toBe("function");
  });

  it("deleteLegalStrategy is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteLegalStrategy).toBe("function");
  });

  it("deleteInvoice is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteInvoice).toBe("function");
  });

  it("deleteActivityItem is exported and callable", async () => {
    const mod = await import("@/lib/supabase");
    expect(typeof mod.deleteActivityItem).toBe("function");
  });
});

describe("deleteActivityItem dispatcher", () => {
  it("dispatches valuation type and returns true", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    const result = await deleteActivityItem("valuation", "test-id-1");
    expect(result).toBe(true);
  });

  it("dispatches contract type and returns true", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    const result = await deleteActivityItem("contract", "test-id-2");
    expect(result).toBe(true);
  });

  it("dispatches chronology type and returns true", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    const result = await deleteActivityItem("chronology", "test-id-3");
    expect(result).toBe(true);
  });

  it("dispatches legal type and returns true", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    const result = await deleteActivityItem("legal", "test-id-4");
    expect(result).toBe(true);
  });

  it("dispatches invoice type and returns true", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    const result = await deleteActivityItem("invoice", "test-id-5");
    expect(result).toBe(true);
  });

  it("returns false for unknown type", async () => {
    const { deleteActivityItem } = await import("@/lib/supabase");
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = await deleteActivityItem("unknown" as any, "test-id-6");
    expect(result).toBe(false);
  });
});
