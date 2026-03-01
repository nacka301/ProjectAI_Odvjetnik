-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 003: Consolidated troskovnici columns
-- Ensures ALL columns that the backend writes to actually exist.
-- Safe to run multiple times (IF NOT EXISTS).
-- ═══════════════════════════════════════════════════════════════════

-- ── Base fields (already in schema.sql, added defensively) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS opis_predmeta TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS vrijednost_spora NUMERIC(14,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_bodova INTEGER DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_eur_bez_pdv NUMERIC(12,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS pdv NUMERIC(12,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_eur_s_pdv NUMERIC(12,2) DEFAULT 0;

-- ── PDV / party fields (from 2026_02_25 migration) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS has_vat BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_pdv_ukljucen BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik_adresa TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik_iban TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_ime TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_oib TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_adresa TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_polozaj TEXT;

-- ── Nested JSONB objects (from 2026_02_25 nested migration) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik JSONB;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS predmet JSONB;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS stranke JSONB;

-- ── Kazneni fields (from 2026_02_26 migration) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS zaprijecena_kazna TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_po_sluzbenoj_duznosti BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_na_teret_proracuna BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_zastupnik_djeteta BOOLEAN DEFAULT FALSE;

-- ── Kazneni edge cases (from 2026_03_02 migration) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_stjecaj BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_oduzimanje_prekrsaj BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS vrijednost_oduzimanja NUMERIC DEFAULT 0;

-- ── Helpful comments ──
COMMENT ON TABLE troskovnici IS 'Odvjetnički troškovnici — persisted fee calculations';
COMMENT ON COLUMN troskovnici.odvjetnik IS 'JSONB: {naziv, oib, adresa, iban}';
COMMENT ON COLUMN troskovnici.predmet IS 'JSONB: {sud, poslovni_broj}';
COMMENT ON COLUMN troskovnici.stranke IS 'JSONB: {zastupana_stranka: {...}, protustranka: {...}}';
