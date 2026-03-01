-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 014: Soft delete + archive pattern
-- Adds is_archived flag to main data tables so records can be
-- "deleted" from the UI without actually destroying data.
-- Archived records are hidden by default via partial indexes.
-- ═══════════════════════════════════════════════════════════════════

-- ── Add is_archived column ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE cases ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE evaluations ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE contract_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE zk_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE chronologies ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;

-- ── Partial indexes: only non-archived rows for default queries ──
-- These let "WHERE is_archived = FALSE" queries use the index efficiently.

CREATE INDEX IF NOT EXISTS idx_troskovnici_active
    ON troskovnici (user_id, created_at DESC)
    WHERE is_archived = FALSE;

CREATE INDEX IF NOT EXISTS idx_cases_active
    ON cases (user_id, created_at DESC)
    WHERE is_archived = FALSE;

CREATE INDEX IF NOT EXISTS idx_clients_active
    ON clients (user_id, ime_naziv)
    WHERE is_archived = FALSE;

CREATE INDEX IF NOT EXISTS idx_invoices_active
    ON invoices (user_id, created_at DESC)
    WHERE is_archived = FALSE;

CREATE INDEX IF NOT EXISTS idx_evaluations_active
    ON evaluations (user_id, created_at DESC)
    WHERE is_archived = FALSE;

CREATE INDEX IF NOT EXISTS idx_contract_analyses_active
    ON contract_analyses (user_id, created_at DESC)
    WHERE is_archived = FALSE;

-- ── RPC: Archive/unarchive a record ──
CREATE OR REPLACE FUNCTION toggle_archive(
    p_table TEXT,
    p_id UUID,
    p_user_id UUID,
    p_archived BOOLEAN DEFAULT TRUE
)
RETURNS BOOLEAN
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    updated INT;
BEGIN
    -- Validate table name to prevent SQL injection
    IF p_table NOT IN ('troskovnici', 'cases', 'clients', 'invoices',
                        'evaluations', 'contract_analyses', 'zk_analyses', 'chronologies') THEN
        RAISE EXCEPTION 'Invalid table: %', p_table;
    END IF;

    EXECUTE format(
        'UPDATE %I SET is_archived = $1 WHERE id = $2 AND user_id = $3',
        p_table
    ) USING p_archived, p_id, p_user_id;

    GET DIAGNOSTICS updated = ROW_COUNT;
    RETURN updated > 0;
END;
$$;

GRANT EXECUTE ON FUNCTION toggle_archive(TEXT, UUID, UUID, BOOLEAN) TO authenticated;

-- ═══════════════════════════════════════════════════════════════════
-- NOTE: The backend should add "is_archived=eq.false" to SELECT
-- queries by default. Example PostgREST filter:
--   GET /rest/v1/troskovnici?user_id=eq.{uid}&is_archived=eq.false
-- The UI can show a "Prikaži arhivirane" toggle to include them.
-- ═══════════════════════════════════════════════════════════════════
