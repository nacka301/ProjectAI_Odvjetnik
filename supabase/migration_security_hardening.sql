-- ═══════════════════════════════════════════════════════════════════════
-- MIGRATION: Security Hardening — user_id + RLS for all data tables
-- Run this in Supabase SQL Editor BEFORE deploying the new backend.
-- Safe to run multiple times (IF NOT EXISTS / IF EXISTS guards).
-- ═══════════════════════════════════════════════════════════════════════

-- ────────────────────────────────────────────────────────────
-- STEP 1: Add user_id column to all data tables
-- Uses ON DELETE SET NULL so deleting a user doesn't cascade-delete data.
-- ────────────────────────────────────────────────────────────

ALTER TABLE evaluations        ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE zk_analyses        ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE contract_analyses  ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE chronologies       ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE legal_strategies   ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE audit_logs         ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE invoices           ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;
ALTER TABLE valuation_audits   ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL;

-- ────────────────────────────────────────────────────────────
-- STEP 2: Add indexes for user-scoped queries
-- ────────────────────────────────────────────────────────────

CREATE INDEX IF NOT EXISTS idx_evaluations_user_id       ON evaluations (user_id);
CREATE INDEX IF NOT EXISTS idx_zk_analyses_user_id       ON zk_analyses (user_id);
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_id ON contract_analyses (user_id);
CREATE INDEX IF NOT EXISTS idx_chronologies_user_id      ON chronologies (user_id);
CREATE INDEX IF NOT EXISTS idx_legal_strategies_user_id  ON legal_strategies (user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id        ON audit_logs (user_id);
CREATE INDEX IF NOT EXISTS idx_invoices_user_id          ON invoices (user_id);
CREATE INDEX IF NOT EXISTS idx_valuation_audits_user_id  ON valuation_audits (user_id);

-- ────────────────────────────────────────────────────────────
-- STEP 3: Backfill existing rows — assign to the first registered user
-- If you have a specific admin user, replace the subquery with their UUID.
-- Rows without user_id will be invisible to authenticated users via RLS.
-- ────────────────────────────────────────────────────────────

-- Uncomment and run ONCE if you need to backfill existing data:
-- DO $$
-- DECLARE
--     admin_uid UUID;
-- BEGIN
--     SELECT id INTO admin_uid FROM auth.users ORDER BY created_at LIMIT 1;
--     IF admin_uid IS NOT NULL THEN
--         UPDATE evaluations       SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE zk_analyses       SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE contract_analyses SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE chronologies      SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE legal_strategies  SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE audit_logs        SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE invoices          SET user_id = admin_uid WHERE user_id IS NULL;
--         UPDATE valuation_audits  SET user_id = admin_uid WHERE user_id IS NULL;
--         RAISE NOTICE 'Backfilled existing rows with user_id = %', admin_uid;
--     ELSE
--         RAISE NOTICE 'No users found — skipping backfill';
--     END IF;
-- END $$;

-- ────────────────────────────────────────────────────────────
-- STEP 4: Remove old insecure policies (USING(true))
-- ────────────────────────────────────────────────────────────

-- evaluations
DROP POLICY IF EXISTS "anon_select" ON evaluations;
DROP POLICY IF EXISTS "anon_delete" ON evaluations;
DROP POLICY IF EXISTS "anon_insert_only" ON evaluations;

-- zk_analyses
DROP POLICY IF EXISTS "anon_update" ON zk_analyses;
DROP POLICY IF EXISTS "anon_delete" ON zk_analyses;
DROP POLICY IF EXISTS "anon_insert_only" ON zk_analyses;

-- contract_analyses
DROP POLICY IF EXISTS "anon_select" ON contract_analyses;
DROP POLICY IF EXISTS "anon_delete" ON contract_analyses;

-- chronologies
DROP POLICY IF EXISTS "anon_select" ON chronologies;
DROP POLICY IF EXISTS "anon_delete" ON chronologies;

-- legal_strategies
DROP POLICY IF EXISTS "anon_select" ON legal_strategies;
DROP POLICY IF EXISTS "anon_delete" ON legal_strategies;

-- audit_logs
DROP POLICY IF EXISTS "anon_select" ON audit_logs;
DROP POLICY IF EXISTS "anon_delete" ON audit_logs;

-- invoices
DROP POLICY IF EXISTS "anon_select" ON invoices;
DROP POLICY IF EXISTS "anon_delete" ON invoices;

-- valuation_audits
DROP POLICY IF EXISTS "anon_select" ON valuation_audits;
DROP POLICY IF EXISTS "anon_delete" ON valuation_audits;

-- ────────────────────────────────────────────────────────────
-- STEP 5: Create new user-scoped RLS policies
-- Authenticated users: full CRUD on own rows
-- Anon key: ZERO access
-- ────────────────────────────────────────────────────────────

-- evaluations
DROP POLICY IF EXISTS "users_own_evaluations" ON evaluations;
CREATE POLICY "users_own_evaluations" ON evaluations
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- zk_analyses
DROP POLICY IF EXISTS "users_own_zk_analyses" ON zk_analyses;
CREATE POLICY "users_own_zk_analyses" ON zk_analyses
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- contract_analyses
DROP POLICY IF EXISTS "users_own_contract_analyses" ON contract_analyses;
CREATE POLICY "users_own_contract_analyses" ON contract_analyses
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- chronologies
DROP POLICY IF EXISTS "users_own_chronologies" ON chronologies;
CREATE POLICY "users_own_chronologies" ON chronologies
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- legal_strategies
DROP POLICY IF EXISTS "users_own_legal_strategies" ON legal_strategies;
CREATE POLICY "users_own_legal_strategies" ON legal_strategies
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- audit_logs (IMMUTABLE: SELECT only — no UPDATE/DELETE for users)
DROP POLICY IF EXISTS "users_select_own_audit_logs" ON audit_logs;
CREATE POLICY "users_select_own_audit_logs" ON audit_logs
    FOR SELECT USING (auth.uid() = user_id);

-- invoices
DROP POLICY IF EXISTS "users_own_invoices" ON invoices;
CREATE POLICY "users_own_invoices" ON invoices
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- valuation_audits
DROP POLICY IF EXISTS "users_own_valuation_audits" ON valuation_audits;
CREATE POLICY "users_own_valuation_audits" ON valuation_audits
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ────────────────────────────────────────────────────────────
-- DONE! Verify with:
--   SELECT tablename, policyname, cmd, qual
--   FROM pg_policies
--   WHERE schemaname = 'public'
--   ORDER BY tablename, policyname;
-- ────────────────────────────────────────────────────────────
