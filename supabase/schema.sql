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

-- ========================================
-- V42: Norm Registry — Croatian legal norms reference data
-- Static reference table for the Norm Validity Engine.
-- Stores law metadata, amendment history, and validity status.
-- ========================================

CREATE TABLE IF NOT EXISTS norms (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    abbreviation        TEXT            NOT NULL UNIQUE,     -- 'ZOO', 'ZZK', 'ZV', etc.
    full_name           TEXT            NOT NULL,            -- 'Zakon o obveznim odnosima'
    status              TEXT            NOT NULL DEFAULT 'VAŽEĆI',  -- 'VAŽEĆI', 'IZMIJENJEN', 'UKINUT'
    nn_reference        TEXT            NOT NULL DEFAULT '', -- 'NN 35/05, 41/08, 125/11, 78/15, 29/18'
    nn_reference_short  TEXT            NOT NULL DEFAULT '', -- 'NN 35/05, 29/18'
    category            TEXT            NOT NULL DEFAULT '', -- 'Obvezno pravo', 'Stvarno pravo', etc.
    original_date       DATE,                               -- Date of original publication
    last_amendment_date DATE,                               -- Date of most recent amendment
    nn_url              TEXT            NOT NULL DEFAULT '', -- Link to Narodne novine
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_norms_abbreviation ON norms (abbreviation);

-- Amendment history for each norm
CREATE TABLE IF NOT EXISTS norm_amendments (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    norm_id         UUID            NOT NULL REFERENCES norms(id) ON DELETE CASCADE,
    nn_number       TEXT            NOT NULL,            -- '35/05'
    publication_date DATE           NOT NULL,
    amendment_type  TEXT            NOT NULL DEFAULT 'Izmjena', -- 'TemeljniTekst', 'Izmjena', 'Ispravak', 'OdlukaUSRH'
    description     TEXT            NOT NULL DEFAULT '',
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_norm_amendments_norm_id ON norm_amendments (norm_id);
CREATE INDEX IF NOT EXISTS idx_norm_amendments_date ON norm_amendments (publication_date);

-- RLS for norms (read-only for anon)
ALTER TABLE norms ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON norms;
CREATE POLICY "service_role_full_access" ON norms
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON norms;
CREATE POLICY "anon_select" ON norms
    FOR SELECT USING (true);

ALTER TABLE norm_amendments ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON norm_amendments;
CREATE POLICY "service_role_full_access" ON norm_amendments
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON norm_amendments;
CREATE POLICY "anon_select" ON norm_amendments
    FOR SELECT USING (true);

-- ========================================
-- V45: AI Act Compliance — Audit Logs
-- Stores the transparent reasoning trail for each legal strategy analysis.
-- Fulfils EU AI Act (Uredba EU 2024/1689) Article 13 — Transparency requirements
-- for high-risk AI systems used in legal/judicial contexts.
-- ========================================

-- Add audit_trail JSONB column to legal_strategies (stores the full audit trail per analysis)
ALTER TABLE legal_strategies ADD COLUMN IF NOT EXISTS audit_trail JSONB NOT NULL DEFAULT '[]'::jsonb;

-- Standalone audit_logs table — for cross-module, queryable audit history
CREATE TABLE IF NOT EXISTS audit_logs (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    module              TEXT            NOT NULL DEFAULT 'legal_strategy',   -- Which module generated this
    analysis_id         UUID,                                               -- FK to the parent analysis (legal_strategies.id, etc.)
    step                INTEGER         NOT NULL DEFAULT 1,
    action              TEXT            NOT NULL DEFAULT '',                  -- What the AI concluded
    legal_basis         TEXT            NOT NULL DEFAULT '',                  -- čl. 368. ZOO
    citation            TEXT            NOT NULL DEFAULT '',                  -- Full citation with NN
    reasoning           TEXT            NOT NULL DEFAULT '',                  -- Why this article applies
    confidence          TEXT            NOT NULL DEFAULT 'SREDNJE',           -- VISOKO / SREDNJE / NISKO
    model_used          TEXT            NOT NULL DEFAULT 'gpt-4o',            -- Which AI model
    model_temperature   NUMERIC(3, 2)   NOT NULL DEFAULT 0.10                -- Temperature setting used
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_module ON audit_logs (module);
CREATE INDEX IF NOT EXISTS idx_audit_logs_analysis_id ON audit_logs (analysis_id);

ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON audit_logs;
CREATE POLICY "service_role_full_access" ON audit_logs
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON audit_logs;
CREATE POLICY "anon_select" ON audit_logs
    FOR SELECT USING (true);

-- ========================================
-- V47: Fiskalizacija 2.0 — e-Invoices
-- UBL 2.1 compliant electronic invoices for Croatian B2B e-invoicing.
-- Effective 01.01.2026 per Zakon o elektroničkom izdavanju računa.
-- Links invoices to PravoTek AI analysis modules.
-- ========================================

CREATE TABLE IF NOT EXISTS invoices (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    invoice_number      TEXT            NOT NULL UNIQUE,             -- YYYY-NNNNN format
    issue_date          TEXT            NOT NULL DEFAULT '',          -- ISO 8601
    due_date            TEXT            NOT NULL DEFAULT '',          -- ISO 8601
    issuer_name         TEXT            NOT NULL DEFAULT '',
    issuer_oib          TEXT            NOT NULL DEFAULT '',          -- 11-digit OIB
    client_name         TEXT            NOT NULL DEFAULT '',
    client_oib          TEXT            NOT NULL DEFAULT '',          -- 11-digit OIB
    net_amount          NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    pdv_rate            NUMERIC(5, 2)   NOT NULL DEFAULT 25,
    pdv_amount          NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    gross_amount        NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    module              TEXT            NOT NULL DEFAULT 'legal',     -- valuation, contract, chronology, legal
    analysis_id         UUID,                                        -- FK to linked analysis
    case_reference      TEXT,                                         -- Predmet/case number
    status              TEXT            NOT NULL DEFAULT 'DRAFT',     -- DRAFT, ISSUED, SENT, PAID, CANCELLED
    ubl_xml             TEXT            NOT NULL DEFAULT '',          -- Full UBL 2.1 XML
    raw_json_output     JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_invoices_created_at ON invoices (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_invoices_invoice_number ON invoices (invoice_number);
CREATE INDEX IF NOT EXISTS idx_invoices_client_oib ON invoices (client_oib);
CREATE INDEX IF NOT EXISTS idx_invoices_status ON invoices (status);
CREATE INDEX IF NOT EXISTS idx_invoices_module ON invoices (module);

ALTER TABLE invoices ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON invoices;
CREATE POLICY "service_role_full_access" ON invoices
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON invoices;
CREATE POLICY "anon_select" ON invoices
    FOR SELECT USING (true);

-- ========================================
-- Perplexity AI Audit Layer — Valuation Verification
-- Stores audit results from the second AI layer that
-- cross-checks property valuations against real market data.
-- ========================================

CREATE TABLE IF NOT EXISTS valuation_audits (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    evaluation_id       UUID            NOT NULL REFERENCES evaluations(id),
    verdict             TEXT            NOT NULL DEFAULT 'SUSPICIOUS',   -- CONFIRMED, SUSPICIOUS, REJECTED
    overall_confidence  INTEGER         NOT NULL DEFAULT 0,             -- 0-100
    summary             TEXT            NOT NULL DEFAULT '',
    recommendation      TEXT            NOT NULL DEFAULT '',
    price_score         INTEGER         NOT NULL DEFAULT 0,             -- 0-100
    location_score      INTEGER         NOT NULL DEFAULT 0,             -- 0-100
    methodology_score   INTEGER         NOT NULL DEFAULT 0,             -- 0-100
    data_quality_score  INTEGER         NOT NULL DEFAULT 0,             -- 0-100
    findings_count      INTEGER         NOT NULL DEFAULT 0,
    critical_count      INTEGER         NOT NULL DEFAULT 0,
    model_used          TEXT            NOT NULL DEFAULT 'sonar',
    prompt_tokens       INTEGER         NOT NULL DEFAULT 0,
    completion_tokens   INTEGER         NOT NULL DEFAULT 0,
    raw_json_output     JSONB           NOT NULL DEFAULT '{}'::jsonb    -- full audit result
);

-- One audit per evaluation (latest wins)
CREATE UNIQUE INDEX IF NOT EXISTS idx_valuation_audits_eval ON valuation_audits (evaluation_id);
CREATE INDEX IF NOT EXISTS idx_valuation_audits_created_at ON valuation_audits (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_valuation_audits_verdict ON valuation_audits (verdict);

ALTER TABLE valuation_audits ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON valuation_audits;
CREATE POLICY "service_role_full_access" ON valuation_audits
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON valuation_audits;
CREATE POLICY "anon_select" ON valuation_audits
    FOR SELECT USING (true);
