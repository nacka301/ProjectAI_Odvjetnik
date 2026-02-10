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

-- Row Level Security (optional — enable if needed)
-- ALTER TABLE evaluations ENABLE ROW LEVEL SECURITY;
-- CREATE POLICY "Allow all inserts" ON evaluations FOR INSERT WITH CHECK (true);
-- CREATE POLICY "Allow all selects" ON evaluations FOR SELECT USING (true);
