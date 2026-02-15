-- ========================================
-- Supabase Schema: evaluations table
-- Run this in Supabase SQL Editor
-- ========================================

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Evaluations table — stores every valuation run
CREATE TABLE IF NOT EXISTS evaluations (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    property_type   TEXT            NOT NULL,           -- 'stan', 'kuća', 'zemljište', 'poslovni prostor'
    location        TEXT            NOT NULL,           -- e.g. 'Split, Bačvice'
    size_m2         NUMERIC(10, 2),                     -- property size
    estimated_value NUMERIC(12, 2)  NOT NULL,           -- final EUR value
    pdf_url         TEXT,                               -- Supabase Storage public URL
    raw_json_output JSONB           NOT NULL DEFAULT '{}'::jsonb  -- full AI response
);

-- Index for quick lookups by location
CREATE INDEX IF NOT EXISTS idx_evaluations_location ON evaluations (location);

-- Index for date-range queries
CREATE INDEX IF NOT EXISTS idx_evaluations_created_at ON evaluations (created_at DESC);

-- ========================================
-- ZK Analysis table — stores parsed ZK izvadak data
-- Run in Supabase SQL Editor after evaluations table
-- ========================================

CREATE TABLE IF NOT EXISTS zk_analyses (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now() NOT NULL,
    property_id             TEXT            NOT NULL,           -- k.o. + čestica
    cadastral_municipality  TEXT,
    parcel_number           TEXT,
    property_type           TEXT,                               -- stan, kuća, zemljište, poslovni_prostor
    location                TEXT            NOT NULL,
    land_area_m2            NUMERIC(10, 2),
    building_area_m2        NUMERIC(10, 2),
    risk_score              TEXT            NOT NULL,           -- LOW, MEDIUM, HIGH
    owners                  JSONB           NOT NULL DEFAULT '[]'::jsonb,
    mortgages               JSONB           NOT NULL DEFAULT '[]'::jsonb,
    disputes                JSONB           NOT NULL DEFAULT '[]'::jsonb,
    encumbrances            JSONB           NOT NULL DEFAULT '[]'::jsonb,
    lawyer_comment          TEXT,
    evaluation_id           UUID            REFERENCES evaluations(id),  -- linked valuation
    raw_json_output         JSONB           NOT NULL DEFAULT '{}'::jsonb,
    pdf_url                 TEXT
);

CREATE INDEX IF NOT EXISTS idx_zk_analyses_property_id ON zk_analyses (property_id);
CREATE INDEX IF NOT EXISTS idx_zk_analyses_location ON zk_analyses (location);
CREATE INDEX IF NOT EXISTS idx_zk_analyses_created_at ON zk_analyses (created_at DESC);

-- ========================================
-- Supabase Storage: create bucket for PDFs
-- Run in Supabase Dashboard → Storage → New Bucket
-- Name: zk-izvadci
-- Public: true (so Backend can fetch the URL)
-- ========================================

-- ========================================
-- Row Level Security
-- Backend uses the service_role key, which bypasses RLS.
-- The anon key (exposed in frontend) is limited to INSERT only.
-- ========================================

-- evaluations
ALTER TABLE evaluations ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON evaluations;
CREATE POLICY "service_role_full_access" ON evaluations
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_insert_only" ON evaluations;
CREATE POLICY "anon_insert_only" ON evaluations
    FOR INSERT WITH CHECK (true);
DROP POLICY IF EXISTS "anon_select" ON evaluations;
CREATE POLICY "anon_select" ON evaluations
    FOR SELECT USING (true);

-- zk_analyses
ALTER TABLE zk_analyses ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON zk_analyses;
CREATE POLICY "service_role_full_access" ON zk_analyses
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_insert_only" ON zk_analyses;
CREATE POLICY "anon_insert_only" ON zk_analyses
    FOR INSERT WITH CHECK (true);

-- ========================================
-- Contract Analyses table — stores AI contract risk analyses
-- ========================================

CREATE TABLE IF NOT EXISTS contract_analyses (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    document_type   TEXT            NOT NULL DEFAULT '',       -- 'Kupoprodajni ugovor', 'Predugovor', etc.
    summary         TEXT            NOT NULL DEFAULT '',       -- AI-generated summary
    overall_risk    TEXT            NOT NULL DEFAULT '',       -- 'VISOK', 'SREDNJI', 'NIZAK'
    risk_score      INTEGER         NOT NULL DEFAULT 0,        -- 0-100
    red_flags_count INTEGER         NOT NULL DEFAULT 0,
    raw_json_output JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_contract_analyses_created_at ON contract_analyses (created_at DESC);

ALTER TABLE contract_analyses ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON contract_analyses;
CREATE POLICY "service_role_full_access" ON contract_analyses
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON contract_analyses;
CREATE POLICY "anon_select" ON contract_analyses
    FOR SELECT USING (true);

-- ========================================
-- Chronologies table — stores timeline extractions
-- ========================================

CREATE TABLE IF NOT EXISTS chronologies (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now() NOT NULL,
    total_documents         INTEGER         NOT NULL DEFAULT 0,
    total_events            INTEGER         NOT NULL DEFAULT 0,
    date_range              TEXT            NOT NULL DEFAULT '',   -- "01.01.2020 — 15.02.2026"
    raw_json_output         JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_chronologies_created_at ON chronologies (created_at DESC);

ALTER TABLE chronologies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON chronologies;
CREATE POLICY "service_role_full_access" ON chronologies
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON chronologies;
CREATE POLICY "anon_select" ON chronologies
    FOR SELECT USING (true);

-- ========================================
-- Legal Strategies table — stores "Provjeri ishod" analyses
-- ========================================

CREATE TABLE IF NOT EXISTS legal_strategies (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    key_legal_issue     TEXT            NOT NULL DEFAULT '',
    client_position     TEXT            NOT NULL DEFAULT 'kupac',
    win_probability     INTEGER         NOT NULL DEFAULT 0,       -- 0-100
    win_probability_min INTEGER         NOT NULL DEFAULT 0,
    win_probability_max INTEGER         NOT NULL DEFAULT 0,
    precedents_count    INTEGER         NOT NULL DEFAULT 0,
    raw_json_output     JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_legal_strategies_created_at ON legal_strategies (created_at DESC);

ALTER TABLE legal_strategies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON legal_strategies;
CREATE POLICY "service_role_full_access" ON legal_strategies
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON legal_strategies;
CREATE POLICY "anon_select" ON legal_strategies
    FOR SELECT USING (true);
