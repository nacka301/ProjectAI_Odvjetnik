-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 004: Auto-update triggers for updated_at columns
-- Adds BEFORE UPDATE triggers on all tables with an updated_at column.
-- Ensures updated_at is always accurate without backend effort.
-- Safe to run multiple times (CREATE OR REPLACE + DROP IF EXISTS).
-- ═══════════════════════════════════════════════════════════════════

-- ── Generic trigger function ──
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ── clients ──
DROP TRIGGER IF EXISTS trg_clients_updated_at ON clients;
CREATE TRIGGER trg_clients_updated_at
    BEFORE UPDATE ON clients
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── cases ──
DROP TRIGGER IF EXISTS trg_cases_updated_at ON cases;
CREATE TRIGGER trg_cases_updated_at
    BEFORE UPDATE ON cases
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── norms ──
DROP TRIGGER IF EXISTS trg_norms_updated_at ON norms;
CREATE TRIGGER trg_norms_updated_at
    BEFORE UPDATE ON norms
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── user_profiles (already has one, replace for consistency) ──
DROP TRIGGER IF EXISTS user_profiles_updated_at ON user_profiles;
DROP TRIGGER IF EXISTS trg_user_profiles_updated_at ON user_profiles;
CREATE TRIGGER trg_user_profiles_updated_at
    BEFORE UPDATE ON user_profiles
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── law_firms (already has one, replace for consistency) ──
DROP TRIGGER IF EXISTS law_firms_updated_at ON law_firms;
DROP TRIGGER IF EXISTS trg_law_firms_updated_at ON law_firms;
CREATE TRIGGER trg_law_firms_updated_at
    BEFORE UPDATE ON law_firms
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── subscriptions (already has one, replace for consistency) ──
DROP TRIGGER IF EXISTS subscriptions_updated_at ON subscriptions;
DROP TRIGGER IF EXISTS trg_subscriptions_updated_at ON subscriptions;
CREATE TRIGGER trg_subscriptions_updated_at
    BEFORE UPDATE ON subscriptions
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ── firm_usage (already has one, replace for consistency) ──
DROP TRIGGER IF EXISTS firm_usage_updated_at ON firm_usage;
DROP TRIGGER IF EXISTS trg_firm_usage_updated_at ON firm_usage;
CREATE TRIGGER trg_firm_usage_updated_at
    BEFORE UPDATE ON firm_usage
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
