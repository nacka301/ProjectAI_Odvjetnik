-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 005: Performance composite indexes
-- Adds indexes for the most common query patterns:
--   • User + date (listing pages with sort)
--   • Client + date (client profile views)
--   • Status filters
--   • Full-text search on client names
-- Safe to run multiple times (IF NOT EXISTS).
-- ═══════════════════════════════════════════════════════════════════

-- ── Troskovnici: user listing sorted by date ──
CREATE INDEX IF NOT EXISTS idx_troskovnici_user_date
    ON troskovnici (user_id, created_at DESC);

-- ── Troskovnici: client + date for profile view ──
CREATE INDEX IF NOT EXISTS idx_troskovnici_client_date
    ON troskovnici (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- ── Cases: user + status for active-cases dashboard ──
CREATE INDEX IF NOT EXISTS idx_cases_user_status
    ON cases (user_id, status, created_at DESC);

-- ── Cases: client + date for client profile ──
CREATE INDEX IF NOT EXISTS idx_cases_client_date
    ON cases (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- ── Clients: full-text search on name (trigram) ──
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_clients_name_trgm
    ON clients USING GIN (ime_naziv gin_trgm_ops);

-- ── Clients: user + name for alphabetical list ──
CREATE INDEX IF NOT EXISTS idx_clients_user_name
    ON clients (user_id, ime_naziv);

-- ── Invoices: user + status for invoice listing ──
CREATE INDEX IF NOT EXISTS idx_invoices_user_status
    ON invoices (user_id, status, created_at DESC);

-- ── Invoices: user + date ──
CREATE INDEX IF NOT EXISTS idx_invoices_user_date
    ON invoices (user_id, created_at DESC);

-- ── Evaluations: user + property type for filtering ──
CREATE INDEX IF NOT EXISTS idx_evaluations_user_type
    ON evaluations (user_id, property_type, created_at DESC);

-- ── Audit logs: user + module for filtered audit trail ──
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_module
    ON audit_logs (user_id, module, created_at DESC);

-- ── Contract analyses: user + risk for dashboard ──
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_risk
    ON contract_analyses (user_id, overall_risk, created_at DESC);
