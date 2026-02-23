-- ========================================
-- Supabase Schema: PravoTek AI — Security-Hardened
-- Run this in Supabase SQL Editor
-- ========================================
-- SECURITY MODEL:
--   • Backend uses service_role key → bypasses RLS (trusted server)
--   • All user data tables have user_id column → each lawyer sees ONLY their own data
--   • Authenticated users: SELECT/INSERT/UPDATE/DELETE on own rows (auth.uid() = user_id)
--   • Anon key: NO access to user data — zero leakage
--   • Reference data (norms, law_chunks): public SELECT for all (no user data)
--   • audit_logs: immutable for users — SELECT only (no UPDATE/DELETE)
-- ========================================

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Evaluations table — stores every valuation run
CREATE TABLE IF NOT EXISTS evaluations (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
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

-- Index for user-scoped queries
CREATE INDEX IF NOT EXISTS idx_evaluations_user_id ON evaluations (user_id);

-- ========================================
-- ZK Analysis table — stores parsed ZK izvadak data
-- ========================================

CREATE TABLE IF NOT EXISTS zk_analyses (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id                 UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
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
CREATE INDEX IF NOT EXISTS idx_zk_analyses_user_id ON zk_analyses (user_id);

-- ========================================
-- Supabase Storage: create bucket for PDFs
-- Run in Supabase Dashboard → Storage → New Bucket
-- Name: zk-izvadci
-- Public: true (so Backend can fetch the URL)
-- ========================================

-- ========================================
-- Row Level Security — HARDENED
-- Backend uses service_role key → bypasses RLS (trusted server).
-- Authenticated users see ONLY their own rows (auth.uid() = user_id).
-- Anon key gets ZERO access to user data tables.
-- ========================================

-- evaluations
ALTER TABLE evaluations ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON evaluations;
CREATE POLICY "service_role_full_access" ON evaluations
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_insert_only" ON evaluations;
DROP POLICY IF EXISTS "anon_select" ON evaluations;
DROP POLICY IF EXISTS "anon_delete" ON evaluations;
DROP POLICY IF EXISTS "users_own_evaluations" ON evaluations;
CREATE POLICY "users_own_evaluations" ON evaluations
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- zk_analyses
ALTER TABLE zk_analyses ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON zk_analyses;
CREATE POLICY "service_role_full_access" ON zk_analyses
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_insert_only" ON zk_analyses;
DROP POLICY IF EXISTS "anon_update" ON zk_analyses;
DROP POLICY IF EXISTS "anon_delete" ON zk_analyses;
DROP POLICY IF EXISTS "users_own_zk_analyses" ON zk_analyses;
CREATE POLICY "users_own_zk_analyses" ON zk_analyses
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- Contract Analyses table — stores AI contract risk analyses
-- ========================================

CREATE TABLE IF NOT EXISTS contract_analyses (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
    document_type   TEXT            NOT NULL DEFAULT '',       -- 'Kupoprodajni ugovor', 'Predugovor', etc.
    summary         TEXT            NOT NULL DEFAULT '',       -- AI-generated summary
    overall_risk    TEXT            NOT NULL DEFAULT '',       -- 'VISOK', 'SREDNJI', 'NIZAK'
    risk_score      INTEGER         NOT NULL DEFAULT 0,        -- 0-100
    red_flags_count INTEGER         NOT NULL DEFAULT 0,
    raw_json_output JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_contract_analyses_created_at ON contract_analyses (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_id ON contract_analyses (user_id);

ALTER TABLE contract_analyses ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON contract_analyses;
CREATE POLICY "service_role_full_access" ON contract_analyses
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON contract_analyses;
DROP POLICY IF EXISTS "anon_delete" ON contract_analyses;
DROP POLICY IF EXISTS "users_own_contract_analyses" ON contract_analyses;
CREATE POLICY "users_own_contract_analyses" ON contract_analyses
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- Chronologies table — stores timeline extractions
-- ========================================

CREATE TABLE IF NOT EXISTS chronologies (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id                 UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
    total_documents         INTEGER         NOT NULL DEFAULT 0,
    total_events            INTEGER         NOT NULL DEFAULT 0,
    date_range              TEXT            NOT NULL DEFAULT '',   -- "01.01.2020 — 15.02.2026"
    raw_json_output         JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_chronologies_created_at ON chronologies (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_chronologies_user_id ON chronologies (user_id);

ALTER TABLE chronologies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON chronologies;
CREATE POLICY "service_role_full_access" ON chronologies
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON chronologies;
DROP POLICY IF EXISTS "anon_delete" ON chronologies;
DROP POLICY IF EXISTS "users_own_chronologies" ON chronologies;
CREATE POLICY "users_own_chronologies" ON chronologies
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- Legal Strategies table — stores "Provjeri ishod" analyses
-- ========================================

CREATE TABLE IF NOT EXISTS legal_strategies (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
    key_legal_issue     TEXT            NOT NULL DEFAULT '',
    client_position     TEXT            NOT NULL DEFAULT 'kupac',
    win_probability     INTEGER         NOT NULL DEFAULT 0,       -- 0-100
    win_probability_min INTEGER         NOT NULL DEFAULT 0,
    win_probability_max INTEGER         NOT NULL DEFAULT 0,
    precedents_count    INTEGER         NOT NULL DEFAULT 0,
    raw_json_output     JSONB           NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_legal_strategies_created_at ON legal_strategies (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_legal_strategies_user_id ON legal_strategies (user_id);

ALTER TABLE legal_strategies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON legal_strategies;
CREATE POLICY "service_role_full_access" ON legal_strategies
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON legal_strategies;
DROP POLICY IF EXISTS "anon_delete" ON legal_strategies;
DROP POLICY IF EXISTS "users_own_legal_strategies" ON legal_strategies;
CREATE POLICY "users_own_legal_strategies" ON legal_strategies
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

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
-- IMMUTABLE: users can only SELECT their own logs (no UPDATE/DELETE)
CREATE TABLE IF NOT EXISTS audit_logs (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
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
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id ON audit_logs (user_id);

ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON audit_logs;
CREATE POLICY "service_role_full_access" ON audit_logs
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON audit_logs;
DROP POLICY IF EXISTS "anon_delete" ON audit_logs;
-- IMMUTABLE: users can only read their own audit logs (EU AI Act Article 13 compliance)
DROP POLICY IF EXISTS "users_select_own_audit_logs" ON audit_logs;
CREATE POLICY "users_select_own_audit_logs" ON audit_logs
    FOR SELECT USING (auth.uid() = user_id);

-- ========================================
-- V47: Fiskalizacija 2.0 — e-Invoices
-- UBL 2.1 compliant electronic invoices for Croatian B2B e-invoicing.
-- Effective 01.01.2026 per Zakon o elektroničkom izdavanju računa.
-- Links invoices to PravoTek AI analysis modules.
-- ========================================

CREATE TABLE IF NOT EXISTS invoices (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
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
CREATE INDEX IF NOT EXISTS idx_invoices_user_id ON invoices (user_id);

ALTER TABLE invoices ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON invoices;
CREATE POLICY "service_role_full_access" ON invoices
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON invoices;
DROP POLICY IF EXISTS "anon_delete" ON invoices;
DROP POLICY IF EXISTS "users_own_invoices" ON invoices;
CREATE POLICY "users_own_invoices" ON invoices
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- Perplexity AI Audit Layer — Valuation Verification
-- Stores audit results from the second AI layer that
-- cross-checks property valuations against real market data.
-- ========================================

CREATE TABLE IF NOT EXISTS valuation_audits (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,  -- SECURITY: owner
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
CREATE INDEX IF NOT EXISTS idx_valuation_audits_user_id ON valuation_audits (user_id);

ALTER TABLE valuation_audits ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON valuation_audits;
CREATE POLICY "service_role_full_access" ON valuation_audits
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON valuation_audits;
DROP POLICY IF EXISTS "anon_delete" ON valuation_audits;
DROP POLICY IF EXISTS "users_own_valuation_audits" ON valuation_audits;
CREATE POLICY "users_own_valuation_audits" ON valuation_audits
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- V69: Semantic RAG — Law Chunks with pgvector embeddings
-- Stores Croatian law articles as vector-embedded chunks
-- for semantic retrieval during contract analysis.
-- Requires pgvector extension (enable in Supabase Dashboard → Extensions).
-- ========================================

-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS law_chunks (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    law             TEXT            NOT NULL,            -- 'ZOO', 'ZV', 'ZZK'
    full_name       TEXT            NOT NULL DEFAULT '', -- 'Zakon o obveznim odnosima'
    nn_reference    TEXT            NOT NULL DEFAULT '', -- 'NN 35/05, ...'
    article         TEXT            NOT NULL,            -- '303'
    title           TEXT            NOT NULL DEFAULT '', -- 'Kapara'
    content         TEXT            NOT NULL,            -- Full article text
    topic_tags      TEXT[]          NOT NULL DEFAULT '{}', -- '{kapara,plaćanje,kupoprodaja}'
    embedding       vector(1536)                         -- OpenAI text-embedding-3-small dimension
);

-- Unique constraint: one record per law+article combination
CREATE UNIQUE INDEX IF NOT EXISTS idx_law_chunks_law_article ON law_chunks (law, article);

-- IVFFlat index for fast vector similarity search (cosine distance)
-- lists = sqrt(n) ≈ 10 for ~60-80 articles
CREATE INDEX IF NOT EXISTS idx_law_chunks_embedding ON law_chunks
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

-- Index for filtering by law
CREATE INDEX IF NOT EXISTS idx_law_chunks_law ON law_chunks (law);
CREATE INDEX IF NOT EXISTS idx_law_chunks_created_at ON law_chunks (created_at DESC);

-- Full-text search index (fallback when embeddings unavailable)
CREATE INDEX IF NOT EXISTS law_chunks_content_fts_idx
    ON law_chunks USING GIN (to_tsvector('simple', content));

-- Tag-based filtering index
CREATE INDEX IF NOT EXISTS law_chunks_tags_idx
    ON law_chunks USING GIN (topic_tags);

-- RPC function for vector similarity search
-- Usage: SELECT * FROM match_law_chunks(query_embedding, 0.3, 10);
CREATE OR REPLACE FUNCTION match_law_chunks(
    query_embedding vector(1536),
    match_threshold float DEFAULT 0.3,
    match_count int DEFAULT 10
)
RETURNS TABLE (
    id UUID,
    law TEXT,
    full_name TEXT,
    nn_reference TEXT,
    article TEXT,
    title TEXT,
    content TEXT,
    topic_tags TEXT[],
    similarity float
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        1 - (lc.embedding <=> query_embedding) AS similarity
    FROM law_chunks lc
    WHERE lc.embedding IS NOT NULL
      AND 1 - (lc.embedding <=> query_embedding) > match_threshold
    ORDER BY lc.embedding <=> query_embedding
    LIMIT match_count;
$$;

-- RPC function: full-text search (fallback when embeddings unavailable)
CREATE OR REPLACE FUNCTION search_law_chunks_text(
    search_query text,
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id UUID,
    law TEXT,
    full_name TEXT,
    nn_reference TEXT,
    article TEXT,
    title TEXT,
    content TEXT,
    topic_tags TEXT[],
    rank float
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        ts_rank(to_tsvector('simple', lc.content), plainto_tsquery('simple', search_query))::float AS rank
    FROM law_chunks lc
    WHERE to_tsvector('simple', lc.content) @@ plainto_tsquery('simple', search_query)
    ORDER BY rank DESC
    LIMIT max_results;
$$;

-- RPC function: tag-based search
CREATE OR REPLACE FUNCTION search_law_chunks_by_tags(
    search_tags text[],
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id UUID,
    law TEXT,
    full_name TEXT,
    nn_reference TEXT,
    article TEXT,
    title TEXT,
    content TEXT,
    topic_tags TEXT[],
    overlap_count int
)
LANGUAGE sql STABLE
AS $$
    SELECT
        lc.id,
        lc.law,
        lc.full_name,
        lc.nn_reference,
        lc.article,
        lc.title,
        lc.content,
        lc.topic_tags,
        (SELECT count(*)::int FROM unnest(lc.topic_tags) t WHERE t = ANY(search_tags)) AS overlap_count
    FROM law_chunks lc
    WHERE lc.topic_tags && search_tags
    ORDER BY overlap_count DESC, lc.law, lc.article
    LIMIT max_results;
$$;

-- RLS for law_chunks
ALTER TABLE law_chunks ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS "service_role_full_access" ON law_chunks;
CREATE POLICY "service_role_full_access" ON law_chunks
    FOR ALL USING (auth.role() = 'service_role');
DROP POLICY IF EXISTS "anon_select" ON law_chunks;
CREATE POLICY "anon_select" ON law_chunks
    FOR SELECT USING (true);

-- RPC function for upserting a law chunk with embedding
-- Used by LawRAGService.SeedAllLawsAsync()
CREATE OR REPLACE FUNCTION upsert_law_chunk(
    p_law TEXT,
    p_full_name TEXT,
    p_nn_reference TEXT,
    p_article TEXT,
    p_title TEXT,
    p_content TEXT,
    p_topic_tags TEXT[],
    p_embedding vector(1536)
)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO law_chunks (law, full_name, nn_reference, article, title, content, topic_tags, embedding)
    VALUES (p_law, p_full_name, p_nn_reference, p_article, p_title, p_content, p_topic_tags, p_embedding)
    ON CONFLICT (law, article)
    DO UPDATE SET
        full_name = EXCLUDED.full_name,
        nn_reference = EXCLUDED.nn_reference,
        title = EXCLUDED.title,
        content = EXCLUDED.content,
        topic_tags = EXCLUDED.topic_tags,
        embedding = EXCLUDED.embedding;
END;
$$;

-- ========================================
-- V80: Clients (Stranke) — Central entity for client management
-- One client can have many cases and many troškovnici (1-to-N).
-- RLS: each user sees only their own clients.
-- ========================================

CREATE TABLE IF NOT EXISTS clients (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    ime_naziv           TEXT            NOT NULL DEFAULT '',       -- Ime i prezime / naziv tvrtke
    oib                 TEXT,                                      -- OIB (11 digits, optional)
    adresa              TEXT,                                      -- Ulica i kućni broj
    grad                TEXT,                                      -- Grad
    postanski_broj      TEXT,                                      -- Poštanski broj
    email               TEXT,                                      -- E-mail kontakt
    telefon             TEXT,                                      -- Telefon
    tip                 TEXT            NOT NULL DEFAULT 'fizicka', -- 'fizicka' | 'pravna'
    napomena            TEXT                                       -- Slobodna bilješka
);

CREATE INDEX IF NOT EXISTS idx_clients_user_id ON clients (user_id);
CREATE INDEX IF NOT EXISTS idx_clients_oib ON clients (oib) WHERE oib IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_clients_created_at ON clients (created_at DESC);

-- RLS: user sees only their own clients
ALTER TABLE clients ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON clients;
CREATE POLICY "service_role_full_access" ON clients
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_clients" ON clients;
CREATE POLICY "users_own_clients" ON clients
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- V81: Cases (Predmeti) — Legal cases linked to clients
-- Each case belongs to one client and one user.
-- ========================================

CREATE TABLE IF NOT EXISTS cases (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    client_id           UUID            REFERENCES clients(id) ON DELETE SET NULL,
    broj_predmeta       TEXT            NOT NULL DEFAULT '',       -- Poslovni broj predmeta (npr. P-123/2026)
    naziv               TEXT            NOT NULL DEFAULT '',       -- Kratki naziv predmeta
    opis                TEXT,                                      -- Detaljni opis
    vrsta               TEXT            NOT NULL DEFAULT 'parnica',-- parnica, ovrha, savjetovanje, kazneni, upravni, ostalo
    status              TEXT            NOT NULL DEFAULT 'aktivan',-- aktivan, zatvoren, mirovanje, arhiviran
    vrijednost_spora    NUMERIC(14, 2),                            -- EUR
    sud                 TEXT,                                      -- Nadležni sud
    sudac               TEXT,                                      -- Ime suca
    datum_pokretanja    DATE,                                      -- Kad je predmet pokrenut
    datum_zatvaranja    DATE                                       -- Kad je predmet zatvoren
);

CREATE INDEX IF NOT EXISTS idx_cases_user_id ON cases (user_id);
CREATE INDEX IF NOT EXISTS idx_cases_client_id ON cases (client_id);
CREATE INDEX IF NOT EXISTS idx_cases_status ON cases (status);
CREATE INDEX IF NOT EXISTS idx_cases_created_at ON cases (created_at DESC);

-- RLS: user sees only their own cases
ALTER TABLE cases ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON cases;
CREATE POLICY "service_role_full_access" ON cases
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_cases" ON cases;
CREATE POLICY "users_own_cases" ON cases
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- V82: Troškovnici — Persisted lawyer fee calculations
-- Each troškovnik can be linked to a client and/or a case.
-- ========================================

CREATE TABLE IF NOT EXISTS troskovnici (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id             UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    client_id           UUID            REFERENCES clients(id) ON DELETE SET NULL,
    case_id             UUID            REFERENCES cases(id) ON DELETE SET NULL,
    opis_predmeta       TEXT,
    vrijednost_spora    NUMERIC(14, 2)  NOT NULL DEFAULT 0,
    ukupno_bodova       INTEGER         NOT NULL DEFAULT 0,
    ukupno_eur_bez_pdv  NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    pdv                 NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    ukupno_eur_s_pdv    NUMERIC(12, 2)  NOT NULL DEFAULT 0,
    klijent_ime         TEXT,
    klijent_oib         TEXT,
    odvjetnik_ime       TEXT,
    odvjetnik_oib       TEXT,
    raw_json_output     JSONB           NOT NULL DEFAULT '{}'::jsonb  -- Full TroskovnikResponse
);

CREATE INDEX IF NOT EXISTS idx_troskovnici_user_id ON troskovnici (user_id);
CREATE INDEX IF NOT EXISTS idx_troskovnici_client_id ON troskovnici (client_id);
CREATE INDEX IF NOT EXISTS idx_troskovnici_case_id ON troskovnici (case_id);
CREATE INDEX IF NOT EXISTS idx_troskovnici_created_at ON troskovnici (created_at DESC);

-- RLS: user sees only their own troškovnici
ALTER TABLE troskovnici ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON troskovnici;
CREATE POLICY "service_role_full_access" ON troskovnici
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_troskovnici" ON troskovnici;
CREATE POLICY "users_own_troskovnici" ON troskovnici
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ========================================
-- V45: User Profiles — TOTP Two-Factor Authentication
-- Stores encrypted TOTP secrets & recovery codes per user.
-- Secrets encrypted with AES-256-GCM before storage.
-- ========================================

CREATE TABLE IF NOT EXISTS user_profiles (
    user_id                     UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at                  TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at                  TIMESTAMPTZ     DEFAULT now() NOT NULL,
    totp_secret_encrypted       TEXT,            -- AES-256-GCM encrypted base32 TOTP secret
    totp_enabled                BOOLEAN         DEFAULT false NOT NULL,
    recovery_codes_encrypted    TEXT,            -- AES-256-GCM encrypted JSON array of recovery codes
    totp_enabled_at             TIMESTAMPTZ     -- When 2FA was last enabled
);

-- RLS: Backend uses service_role (bypasses RLS), users can only read their own
ALTER TABLE user_profiles ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON user_profiles;
CREATE POLICY "service_role_full_access" ON user_profiles
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_read_own_profile" ON user_profiles;
CREATE POLICY "users_read_own_profile" ON user_profiles
    FOR SELECT USING (auth.uid() = user_id);

-- Trigger to auto-update updated_at
CREATE OR REPLACE FUNCTION update_user_profiles_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS user_profiles_updated_at ON user_profiles;
CREATE TRIGGER user_profiles_updated_at
    BEFORE UPDATE ON user_profiles
    FOR EACH ROW
    EXECUTE FUNCTION update_user_profiles_updated_at();
