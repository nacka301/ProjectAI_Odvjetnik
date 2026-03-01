-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 010: Troškovnik templates
-- Lets lawyers save reusable templates with pre-selected items.
-- E.g. "Standardni kazneni predmet", "Parnični P1 tužba + ročište".
-- Template body is stored as JSONB matching the frontend SelectedItems format.
-- ═══════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS troskovnik_templates (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name            TEXT            NOT NULL DEFAULT '',               -- Template name
    description     TEXT,                                              -- Optional note
    kategorija      TEXT,                                              -- Filter: 'Kazneni', 'Parnični', etc.
    -- Pre-filled form state
    stavke          JSONB           NOT NULL DEFAULT '[]'::jsonb,      -- [{item_id, kolicina}, ...]
    vrijednost_spora NUMERIC(14,2)  DEFAULT 0,                        -- Pre-set VPS
    -- Kazneni pre-sets
    zaprijecena_kazna TEXT,
    is_po_sluzbenoj_duznosti BOOLEAN DEFAULT FALSE,
    is_na_teret_proracuna    BOOLEAN DEFAULT FALSE,
    is_zastupnik_djeteta     BOOLEAN DEFAULT FALSE,
    is_stjecaj               BOOLEAN DEFAULT FALSE,
    is_oduzimanje_prekrsaj   BOOLEAN DEFAULT FALSE,
    vrijednost_oduzimanja    NUMERIC DEFAULT 0,
    -- PDV
    is_pdv_ukljucen BOOLEAN DEFAULT FALSE,
    -- Usage stats
    times_used      INTEGER         DEFAULT 0,
    last_used_at    TIMESTAMPTZ
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_user
    ON troskovnik_templates (user_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_name
    ON troskovnik_templates (user_id, name);

-- RLS
ALTER TABLE troskovnik_templates ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON troskovnik_templates;
CREATE POLICY "service_role_full_access" ON troskovnik_templates
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_templates" ON troskovnik_templates;
CREATE POLICY "users_own_templates" ON troskovnik_templates
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- Auto-update trigger
DROP TRIGGER IF EXISTS trg_troskovnik_templates_updated_at ON troskovnik_templates;
CREATE TRIGGER trg_troskovnik_templates_updated_at
    BEFORE UPDATE ON troskovnik_templates
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
