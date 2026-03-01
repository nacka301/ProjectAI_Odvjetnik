-- ╔═══════════════════════════════════════════════════════════════════════════╗
-- ║                  PravoTek — SVE MIGRACIJE U JEDNOM FAJLU               ║
-- ║  Pokreni jednom u Supabase SQL Editoru (Dashboard → SQL Editor → New)  ║
-- ║  Redoslijed je bitan — NE mijenjaj poredak sekcija.                    ║
-- ║  Sigurno za ponovljeno pokretanje (IF NOT EXISTS / DO blokovi).        ║
-- ╚═══════════════════════════════════════════════════════════════════════════╝


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 1: Troskovnici — nove kolone  (ex 003)
-- ═══════════════════════════════════════════════════════════════════════════

-- Base fields
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS opis_predmeta TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS vrijednost_spora NUMERIC(14,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_bodova INTEGER DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_eur_bez_pdv NUMERIC(12,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS pdv NUMERIC(12,2) DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS ukupno_eur_s_pdv NUMERIC(12,2) DEFAULT 0;

-- PDV / party fields
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS has_vat BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_pdv_ukljucen BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik_adresa TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik_iban TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_ime TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_oib TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_adresa TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS protustranka_polozaj TEXT;

-- Nested JSONB objects
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS odvjetnik JSONB;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS predmet JSONB;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS stranke JSONB;

-- Kazneni fields
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS zaprijecena_kazna TEXT;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_po_sluzbenoj_duznosti BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_na_teret_proracuna BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_zastupnik_djeteta BOOLEAN DEFAULT FALSE;

-- Kazneni edge cases
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_stjecaj BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_oduzimanje_prekrsaj BOOLEAN DEFAULT FALSE;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS vrijednost_oduzimanja NUMERIC DEFAULT 0;

-- Komentari
COMMENT ON TABLE troskovnici IS 'Odvjetnički troškovnici — persisted fee calculations';
COMMENT ON COLUMN troskovnici.odvjetnik IS 'JSONB: {naziv, oib, adresa, iban}';
COMMENT ON COLUMN troskovnici.predmet IS 'JSONB: {sud, poslovni_broj}';
COMMENT ON COLUMN troskovnici.stranke IS 'JSONB: {zastupana_stranka: {...}, protustranka: {...}}';


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 2: Auto-update triggers za updated_at  (ex 004)
-- ═══════════════════════════════════════════════════════════════════════════

-- Generic trigger function
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- clients
DROP TRIGGER IF EXISTS trg_clients_updated_at ON clients;
CREATE TRIGGER trg_clients_updated_at
    BEFORE UPDATE ON clients
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- cases
DROP TRIGGER IF EXISTS trg_cases_updated_at ON cases;
CREATE TRIGGER trg_cases_updated_at
    BEFORE UPDATE ON cases
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- norms
DROP TRIGGER IF EXISTS trg_norms_updated_at ON norms;
CREATE TRIGGER trg_norms_updated_at
    BEFORE UPDATE ON norms
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- user_profiles
DROP TRIGGER IF EXISTS user_profiles_updated_at ON user_profiles;
DROP TRIGGER IF EXISTS trg_user_profiles_updated_at ON user_profiles;
CREATE TRIGGER trg_user_profiles_updated_at
    BEFORE UPDATE ON user_profiles
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- law_firms
DROP TRIGGER IF EXISTS law_firms_updated_at ON law_firms;
DROP TRIGGER IF EXISTS trg_law_firms_updated_at ON law_firms;
CREATE TRIGGER trg_law_firms_updated_at
    BEFORE UPDATE ON law_firms
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- subscriptions
DROP TRIGGER IF EXISTS subscriptions_updated_at ON subscriptions;
DROP TRIGGER IF EXISTS trg_subscriptions_updated_at ON subscriptions;
CREATE TRIGGER trg_subscriptions_updated_at
    BEFORE UPDATE ON subscriptions
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- firm_usage
DROP TRIGGER IF EXISTS firm_usage_updated_at ON firm_usage;
DROP TRIGGER IF EXISTS trg_firm_usage_updated_at ON firm_usage;
CREATE TRIGGER trg_firm_usage_updated_at
    BEFORE UPDATE ON firm_usage
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 3: Performance indexi  (ex 005)
-- ═══════════════════════════════════════════════════════════════════════════

-- Trigram extension (za fuzzy search)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Troskovnici
CREATE INDEX IF NOT EXISTS idx_troskovnici_user_date
    ON troskovnici (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_troskovnici_client_date
    ON troskovnici (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- Cases
CREATE INDEX IF NOT EXISTS idx_cases_user_status
    ON cases (user_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_cases_client_date
    ON cases (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- Clients
CREATE INDEX IF NOT EXISTS idx_clients_name_trgm
    ON clients USING GIN (ime_naziv gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_clients_user_name
    ON clients (user_id, ime_naziv);

-- Invoices
CREATE INDEX IF NOT EXISTS idx_invoices_user_status
    ON invoices (user_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_invoices_user_date
    ON invoices (user_id, created_at DESC);

-- Evaluations
CREATE INDEX IF NOT EXISTS idx_evaluations_user_type
    ON evaluations (user_id, property_type, created_at DESC);

-- Audit logs
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_module
    ON audit_logs (user_id, module, created_at DESC);

-- Contract analyses
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_risk
    ON contract_analyses (user_id, overall_risk, created_at DESC);


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 4: Dashboard stats RPC  (ex 006)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION get_dashboard_stats(p_user_id UUID)
RETURNS JSON
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    result JSON;
BEGIN
    SELECT json_build_object(
        'total_clients',       (SELECT COUNT(*) FROM clients WHERE user_id = p_user_id),
        'total_cases',         (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id),
        'active_cases',        (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id AND status = 'aktivan'),
        'total_troskovnici',   (SELECT COUNT(*) FROM troskovnici WHERE user_id = p_user_id),
        'total_eur_bez_pdv',   COALESCE((SELECT SUM(ukupno_eur_bez_pdv) FROM troskovnici WHERE user_id = p_user_id), 0),
        'total_eur_s_pdv',     COALESCE((SELECT SUM(ukupno_eur_s_pdv) FROM troskovnici WHERE user_id = p_user_id), 0),
        'total_evaluations',   (SELECT COUNT(*) FROM evaluations WHERE user_id = p_user_id),
        'total_contracts',     (SELECT COUNT(*) FROM contract_analyses WHERE user_id = p_user_id),
        'total_zk_analyses',   (SELECT COUNT(*) FROM zk_analyses WHERE user_id = p_user_id),
        'total_invoices',      (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id),
        'invoices_draft',      (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id AND status = 'DRAFT'),
        'invoices_issued',     (SELECT COUNT(*) FROM invoices WHERE user_id = p_user_id AND status = 'ISSUED'),
        'invoices_total_eur',  COALESCE((SELECT SUM(gross_amount) FROM invoices WHERE user_id = p_user_id AND status != 'CANCELLED'), 0),
        'recent_troskovnici',  (
            SELECT COALESCE(json_agg(t), '[]'::json) FROM (
                SELECT id, created_at, opis_predmeta, ukupno_eur_s_pdv
                FROM troskovnici WHERE user_id = p_user_id
                ORDER BY created_at DESC LIMIT 5
            ) t
        ),
        'recent_cases',  (
            SELECT COALESCE(json_agg(c), '[]'::json) FROM (
                SELECT id, created_at, naziv, broj_predmeta, status, vrsta
                FROM cases WHERE user_id = p_user_id
                ORDER BY created_at DESC LIMIT 5
            ) c
        )
    ) INTO result;

    RETURN result;
END;
$$;

GRANT EXECUTE ON FUNCTION get_dashboard_stats(UUID) TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 5: Client search RPC  (ex 007)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION search_clients(
    p_user_id UUID,
    p_query TEXT,
    p_limit INT DEFAULT 20
)
RETURNS TABLE (
    id UUID,
    ime_naziv TEXT,
    oib TEXT,
    adresa TEXT,
    grad TEXT,
    telefon TEXT,
    email TEXT,
    tip TEXT,
    created_at TIMESTAMPTZ,
    similarity_score REAL
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    IF p_query IS NULL OR trim(p_query) = '' THEN
        RETURN QUERY
        SELECT c.id, c.ime_naziv, c.oib, c.adresa, c.grad, c.telefon, c.email, c.tip, c.created_at, 1.0::REAL
        FROM clients c
        WHERE c.user_id = p_user_id
        ORDER BY c.ime_naziv
        LIMIT p_limit;
    ELSE
        RETURN QUERY
        SELECT c.id, c.ime_naziv, c.oib, c.adresa, c.grad, c.telefon, c.email, c.tip, c.created_at,
               GREATEST(
                   similarity(c.ime_naziv, p_query),
                   CASE WHEN c.oib ILIKE '%' || p_query || '%' THEN 0.9 ELSE 0 END,
                   CASE WHEN c.adresa ILIKE '%' || p_query || '%' THEN 0.5 ELSE 0 END,
                   CASE WHEN c.grad ILIKE '%' || p_query || '%' THEN 0.4 ELSE 0 END
               ) AS similarity_score
        FROM clients c
        WHERE c.user_id = p_user_id
          AND (
              c.ime_naziv % p_query
              OR c.ime_naziv ILIKE '%' || p_query || '%'
              OR c.oib ILIKE '%' || p_query || '%'
              OR c.adresa ILIKE '%' || p_query || '%'
              OR c.grad ILIKE '%' || p_query || '%'
          )
        ORDER BY similarity_score DESC, c.ime_naziv
        LIMIT p_limit;
    END IF;
END;
$$;

GRANT EXECUTE ON FUNCTION search_clients(UUID, TEXT, INT) TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 6: Case activity log  (ex 008)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS case_activities (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    case_id         UUID            NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    activity_type   TEXT            NOT NULL DEFAULT 'note',
    title           TEXT            NOT NULL DEFAULT '',
    description     TEXT,
    metadata        JSONB           DEFAULT '{}'::jsonb,
    linked_id       UUID
);

CREATE INDEX IF NOT EXISTS idx_case_activities_case
    ON case_activities (case_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_user
    ON case_activities (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_type
    ON case_activities (case_id, activity_type);

ALTER TABLE case_activities ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON case_activities;
CREATE POLICY "service_role_full_access" ON case_activities
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_case_activities" ON case_activities;
CREATE POLICY "users_own_case_activities" ON case_activities
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- Auto-create "created" activity on case insert
CREATE OR REPLACE FUNCTION log_case_created()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO case_activities (user_id, case_id, activity_type, title, description)
    VALUES (
        NEW.user_id,
        NEW.id,
        'created',
        'Predmet kreiran',
        COALESCE(NEW.naziv, '') || CASE WHEN NEW.broj_predmeta != '' THEN ' (' || NEW.broj_predmeta || ')' ELSE '' END
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_case_created_activity ON cases;
CREATE TRIGGER trg_case_created_activity
    AFTER INSERT ON cases
    FOR EACH ROW EXECUTE FUNCTION log_case_created();

-- Auto-log status changes
CREATE OR REPLACE FUNCTION log_case_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.status IS DISTINCT FROM NEW.status THEN
        INSERT INTO case_activities (user_id, case_id, activity_type, title, metadata)
        VALUES (
            NEW.user_id,
            NEW.id,
            'status_change',
            'Status: ' || COALESCE(OLD.status, '?') || ' → ' || COALESCE(NEW.status, '?'),
            jsonb_build_object('old_status', OLD.status, 'new_status', NEW.status)
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_case_status_change ON cases;
CREATE TRIGGER trg_case_status_change
    AFTER UPDATE ON cases
    FOR EACH ROW EXECUTE FUNCTION log_case_status_change();


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 7: Document attachments  (ex 009)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS documents (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    client_id       UUID            REFERENCES clients(id) ON DELETE SET NULL,
    case_id         UUID            REFERENCES cases(id) ON DELETE SET NULL,
    analysis_id     UUID,
    analysis_type   TEXT,
    file_name       TEXT            NOT NULL DEFAULT '',
    file_type       TEXT            NOT NULL DEFAULT '',
    file_size_bytes BIGINT          DEFAULT 0,
    storage_path    TEXT            NOT NULL DEFAULT '',
    public_url      TEXT,
    document_type   TEXT            NOT NULL DEFAULT 'ostalo',
    description     TEXT,
    tags            TEXT[]          DEFAULT '{}'
);

CREATE INDEX IF NOT EXISTS idx_documents_user
    ON documents (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_documents_case
    ON documents (case_id, created_at DESC)
    WHERE case_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_documents_client
    ON documents (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_documents_type
    ON documents (user_id, document_type);

CREATE INDEX IF NOT EXISTS idx_documents_tags
    ON documents USING GIN (tags);

ALTER TABLE documents ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON documents;
CREATE POLICY "service_role_full_access" ON documents
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_documents" ON documents;
CREATE POLICY "users_own_documents" ON documents
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

DROP TRIGGER IF EXISTS trg_documents_updated_at ON documents;
CREATE TRIGGER trg_documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 8: Troškovnik templates  (ex 010)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS troskovnik_templates (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    name            TEXT            NOT NULL DEFAULT '',
    description     TEXT,
    kategorija      TEXT,
    stavke          JSONB           NOT NULL DEFAULT '[]'::jsonb,
    vrijednost_spora NUMERIC(14,2)  DEFAULT 0,
    zaprijecena_kazna TEXT,
    is_po_sluzbenoj_duznosti BOOLEAN DEFAULT FALSE,
    is_na_teret_proracuna    BOOLEAN DEFAULT FALSE,
    is_zastupnik_djeteta     BOOLEAN DEFAULT FALSE,
    is_stjecaj               BOOLEAN DEFAULT FALSE,
    is_oduzimanje_prekrsaj   BOOLEAN DEFAULT FALSE,
    vrijednost_oduzimanja    NUMERIC DEFAULT 0,
    is_pdv_ukljucen BOOLEAN DEFAULT FALSE,
    times_used      INTEGER         DEFAULT 0,
    last_used_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_user
    ON troskovnik_templates (user_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_name
    ON troskovnik_templates (user_id, name);

ALTER TABLE troskovnik_templates ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON troskovnik_templates;
CREATE POLICY "service_role_full_access" ON troskovnik_templates
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_templates" ON troskovnik_templates;
CREATE POLICY "users_own_templates" ON troskovnik_templates
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

DROP TRIGGER IF EXISTS trg_troskovnik_templates_updated_at ON troskovnik_templates;
CREATE TRIGGER trg_troskovnik_templates_updated_at
    BEFORE UPDATE ON troskovnik_templates
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 9: Monthly revenue view  (ex 011)
-- ═══════════════════════════════════════════════════════════════════════════

DROP MATERIALIZED VIEW IF EXISTS monthly_revenue;

CREATE MATERIALIZED VIEW monthly_revenue AS
SELECT
    user_id,
    date_trunc('month', created_at)::DATE AS month,
    COUNT(*) FILTER (WHERE source = 'troskovnik')           AS troskovnik_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'troskovnik'), 0) AS troskovnik_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'troskovnik'), 0)   AS troskovnik_eur_s_pdv,
    COUNT(*) FILTER (WHERE source = 'invoice')              AS invoice_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'invoice'), 0)    AS invoice_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'invoice'), 0)      AS invoice_eur_s_pdv
FROM (
    SELECT user_id, created_at, 'troskovnik' AS source,
           ukupno_eur_bez_pdv AS amount_bez_pdv,
           ukupno_eur_s_pdv AS amount_s_pdv
    FROM troskovnici
    UNION ALL
    SELECT user_id, created_at, 'invoice' AS source,
           net_amount AS amount_bez_pdv,
           gross_amount AS amount_s_pdv
    FROM invoices
    WHERE status != 'CANCELLED'
) combined
GROUP BY user_id, date_trunc('month', created_at)::DATE
ORDER BY month DESC;

CREATE UNIQUE INDEX IF NOT EXISTS idx_monthly_revenue_user_month
    ON monthly_revenue (user_id, month);

CREATE INDEX IF NOT EXISTS idx_monthly_revenue_user
    ON monthly_revenue (user_id);

-- RPC za monthly revenue
CREATE OR REPLACE FUNCTION get_monthly_revenue(
    p_user_id UUID,
    p_months INT DEFAULT 12
)
RETURNS TABLE (
    month DATE,
    troskovnik_count BIGINT,
    troskovnik_eur_bez_pdv NUMERIC,
    troskovnik_eur_s_pdv NUMERIC,
    invoice_count BIGINT,
    invoice_eur_bez_pdv NUMERIC,
    invoice_eur_s_pdv NUMERIC
)
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT mr.month, mr.troskovnik_count, mr.troskovnik_eur_bez_pdv, mr.troskovnik_eur_s_pdv,
           mr.invoice_count, mr.invoice_eur_bez_pdv, mr.invoice_eur_s_pdv
    FROM monthly_revenue mr
    WHERE mr.user_id = p_user_id
    ORDER BY mr.month DESC
    LIMIT p_months;
$$;

GRANT EXECUTE ON FUNCTION get_monthly_revenue(UUID, INT) TO authenticated;

-- NOTE: Za automatski refresh, postavi cron:
--   SELECT cron.schedule('refresh-monthly-revenue', '0 2 * * *',
--       'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue');


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 10: Data validation constraints  (ex 012)
-- VAŽNO: Svaki constraint ima UPDATE koji prvo čisti postojeće podatke,
--        tako da constraint nikad ne padne na postojećim redovima.
-- ═══════════════════════════════════════════════════════════════════════════

-- ── Clients: OIB 11 znamenki (ili prazan ili NULL) ──
UPDATE clients SET oib = NULL
    WHERE oib IS NOT NULL AND oib != '' AND oib !~ '^\d{11}$';

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_oib_format
        CHECK (oib IS NULL OR oib = '' OR oib ~ '^\d{11}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Clients: tip enum ──
UPDATE clients SET tip = 'fizicka'
    WHERE tip NOT IN ('fizicka', 'pravna');

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_tip
        CHECK (tip IN ('fizicka', 'pravna'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Cases: status enum ──
UPDATE cases SET status = 'aktivan'
    WHERE status NOT IN ('aktivan', 'zatvoren', 'mirovanje', 'arhiviran');

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_status
        CHECK (status IN ('aktivan', 'zatvoren', 'mirovanje', 'arhiviran'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Cases: vrsta enum ──
UPDATE cases SET vrsta = 'ostalo'
    WHERE vrsta NOT IN ('parnica', 'ovrha', 'savjetovanje', 'kazneni', 'upravni', 'izvanparnicni', 'stecajni', 'registarski', 'ostalo');

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_vrsta
        CHECK (vrsta IN ('parnica', 'ovrha', 'savjetovanje', 'kazneni', 'upravni', 'izvanparnicni', 'stecajni', 'registarski', 'ostalo'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Invoices: status enum ──
UPDATE invoices SET status = 'DRAFT'
    WHERE status NOT IN ('DRAFT', 'ISSUED', 'SENT', 'PAID', 'CANCELLED');

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_status
        CHECK (status IN ('DRAFT', 'ISSUED', 'SENT', 'PAID', 'CANCELLED'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Invoices: amounts non-negative ──
UPDATE invoices SET net_amount = 0 WHERE net_amount < 0;
UPDATE invoices SET gross_amount = 0 WHERE gross_amount < 0;

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_net_positive
        CHECK (net_amount >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_gross_positive
        CHECK (gross_amount >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Invoices: OIB format (NULL, prazan, ili 11 znamenki) ──
UPDATE invoices SET issuer_oib = ''
    WHERE issuer_oib IS NOT NULL AND issuer_oib != '' AND issuer_oib !~ '^\d{11}$';
UPDATE invoices SET client_oib = ''
    WHERE client_oib IS NOT NULL AND client_oib != '' AND client_oib !~ '^\d{11}$';

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_issuer_oib
        CHECK (issuer_oib IS NULL OR issuer_oib = '' OR issuer_oib ~ '^\d{11}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_client_oib
        CHECK (client_oib IS NULL OR client_oib = '' OR client_oib ~ '^\d{11}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Troskovnici: bodovi i iznosi non-negative ──
UPDATE troskovnici SET ukupno_bodova = 0 WHERE ukupno_bodova < 0;
UPDATE troskovnici SET ukupno_eur_bez_pdv = 0 WHERE ukupno_eur_bez_pdv < 0;

DO $$ BEGIN
    ALTER TABLE troskovnici ADD CONSTRAINT chk_troskovnici_bodovi_positive
        CHECK (ukupno_bodova IS NULL OR ukupno_bodova >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE troskovnici ADD CONSTRAINT chk_troskovnici_eur_positive
        CHECK (ukupno_eur_bez_pdv IS NULL OR ukupno_eur_bez_pdv >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Evaluations: estimated_value pozitivan ──
UPDATE evaluations SET estimated_value = 1 WHERE estimated_value <= 0;

DO $$ BEGIN
    ALTER TABLE evaluations ADD CONSTRAINT chk_evaluations_value_positive
        CHECK (estimated_value > 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Legal strategies: win_probability 0-100 ──
UPDATE legal_strategies SET win_probability = 0 WHERE win_probability < 0;
UPDATE legal_strategies SET win_probability = 100 WHERE win_probability > 100;

DO $$ BEGIN
    ALTER TABLE legal_strategies ADD CONSTRAINT chk_legal_strategies_win_prob
        CHECK (win_probability BETWEEN 0 AND 100);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Firm usage: credits non-negative ──
UPDATE firm_usage SET used_credits = 0 WHERE used_credits < 0;
UPDATE firm_usage SET max_credits = 0 WHERE max_credits < 0;

DO $$ BEGIN
    ALTER TABLE firm_usage ADD CONSTRAINT chk_firm_usage_credits
        CHECK (used_credits >= 0 AND max_credits >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Subscriptions: plan enum ──
UPDATE subscriptions SET plan_name = 'Pro'
    WHERE plan_name NOT IN ('Starter', 'Pro', 'Enterprise');

DO $$ BEGIN
    ALTER TABLE subscriptions ADD CONSTRAINT chk_subscriptions_plan
        CHECK (plan_name IN ('Starter', 'Pro', 'Enterprise'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Subscriptions: status enum ──
UPDATE subscriptions SET status = 'active'
    WHERE status NOT IN ('active', 'trial', 'canceled', 'expired');

DO $$ BEGIN
    ALTER TABLE subscriptions ADD CONSTRAINT chk_subscriptions_status
        CHECK (status IN ('active', 'trial', 'canceled', 'expired'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 11: Notifikacije  (ex 013)
-- ═══════════════════════════════════════════════════════════════════════════

-- Notification preferences
CREATE TABLE IF NOT EXISTS user_notification_prefs (
    user_id             UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    email_enabled       BOOLEAN         DEFAULT TRUE,
    email_case_updates  BOOLEAN         DEFAULT TRUE,
    email_invoice_reminders BOOLEAN     DEFAULT TRUE,
    email_credit_low    BOOLEAN         DEFAULT TRUE,
    email_weekly_summary BOOLEAN        DEFAULT FALSE,
    push_enabled        BOOLEAN         DEFAULT FALSE,
    push_case_updates   BOOLEAN         DEFAULT FALSE
);

ALTER TABLE user_notification_prefs ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON user_notification_prefs;
CREATE POLICY "service_role_full_access" ON user_notification_prefs
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_prefs" ON user_notification_prefs;
CREATE POLICY "users_own_prefs" ON user_notification_prefs
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

DROP TRIGGER IF EXISTS trg_notification_prefs_updated_at ON user_notification_prefs;
CREATE TRIGGER trg_notification_prefs_updated_at
    BEFORE UPDATE ON user_notification_prefs
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- In-app notifications
CREATE TABLE IF NOT EXISTS notifications (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    type            TEXT            NOT NULL DEFAULT 'info',
    title           TEXT            NOT NULL DEFAULT '',
    body            TEXT,
    link            TEXT,
    is_read         BOOLEAN         DEFAULT FALSE,
    read_at         TIMESTAMPTZ,
    metadata        JSONB           DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
    ON notifications (user_id, is_read, created_at DESC)
    WHERE is_read = FALSE;

CREATE INDEX IF NOT EXISTS idx_notifications_user_date
    ON notifications (user_id, created_at DESC);

ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON notifications;
CREATE POLICY "service_role_full_access" ON notifications
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_notifications" ON notifications;
CREATE POLICY "users_own_notifications" ON notifications
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- RPC: Mark notifications as read
CREATE OR REPLACE FUNCTION mark_notifications_read(p_user_id UUID, p_ids UUID[])
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    updated INT;
BEGIN
    UPDATE notifications
    SET is_read = TRUE, read_at = now()
    WHERE user_id = p_user_id
      AND id = ANY(p_ids)
      AND is_read = FALSE;

    GET DIAGNOSTICS updated = ROW_COUNT;
    RETURN updated;
END;
$$;

GRANT EXECUTE ON FUNCTION mark_notifications_read(UUID, UUID[]) TO authenticated;

-- RPC: Get unread notification count
CREATE OR REPLACE FUNCTION get_unread_notification_count(p_user_id UUID)
RETURNS INT
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT COUNT(*)::INT FROM notifications
    WHERE user_id = p_user_id AND is_read = FALSE;
$$;

GRANT EXECUTE ON FUNCTION get_unread_notification_count(UUID) TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 12: Soft delete / Archive  (ex 014)
-- ═══════════════════════════════════════════════════════════════════════════

-- is_archived kolona na glavne tablice
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE cases ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE evaluations ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE contract_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE zk_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE chronologies ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;

-- Partial indexi za aktivne zapise
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

-- RPC: Archive/unarchive
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


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 13: Tenant Subscriptions  (GDPR-safe plan tracking)
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS tenant_subscriptions (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    tenant_id       UUID            NOT NULL REFERENCES law_firms(id) ON DELETE CASCADE,
    plan_type       TEXT            NOT NULL DEFAULT 'Free',
    valid_until     TIMESTAMPTZ     NOT NULL DEFAULT (now() + interval '30 days'),
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE
);

-- Constraint: plan_type enum
DO $$ BEGIN
    ALTER TABLE tenant_subscriptions ADD CONSTRAINT chk_tenant_sub_plan_type
        CHECK (plan_type IN ('Free', 'Starter', 'Pro', 'Premium', 'Enterprise'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- One active subscription per tenant
CREATE UNIQUE INDEX IF NOT EXISTS idx_tenant_sub_active_tenant
    ON tenant_subscriptions (tenant_id) WHERE is_active = TRUE;

-- Fast lookups by tenant + date
CREATE INDEX IF NOT EXISTS idx_tenant_sub_tenant_valid
    ON tenant_subscriptions (tenant_id, valid_until DESC);

-- Auto-expire helper: find all overdue subs
CREATE INDEX IF NOT EXISTS idx_tenant_sub_expiry
    ON tenant_subscriptions (valid_until)
    WHERE is_active = TRUE;

-- RLS
ALTER TABLE tenant_subscriptions ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON tenant_subscriptions;
CREATE POLICY "service_role_full_access" ON tenant_subscriptions
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_read_own_tenant_sub" ON tenant_subscriptions;
CREATE POLICY "users_read_own_tenant_sub" ON tenant_subscriptions
    FOR SELECT USING (
        tenant_id IN (SELECT law_firm_id FROM user_profiles WHERE user_id = auth.uid())
    );

-- updated_at trigger
DROP TRIGGER IF EXISTS trg_tenant_sub_updated_at ON tenant_subscriptions;
CREATE TRIGGER trg_tenant_sub_updated_at
    BEFORE UPDATE ON tenant_subscriptions
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- RPC: Check whether a tenant's subscription is active & not expired
CREATE OR REPLACE FUNCTION is_tenant_subscription_active(p_tenant_id UUID)
RETURNS BOOLEAN
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT EXISTS (
        SELECT 1 FROM tenant_subscriptions
        WHERE tenant_id = p_tenant_id
          AND is_active = TRUE
          AND valid_until > now()
    );
$$;

GRANT EXECUTE ON FUNCTION is_tenant_subscription_active(UUID) TO authenticated;

-- RPC: Bulk-expire overdue subscriptions (call from cron or backend)
CREATE OR REPLACE FUNCTION expire_overdue_subscriptions()
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    cnt INT;
BEGIN
    UPDATE tenant_subscriptions
       SET is_active   = FALSE,
           updated_at  = now()
     WHERE is_active = TRUE
       AND valid_until <= now();

    GET DIAGNOSTICS cnt = ROW_COUNT;
    RETURN cnt;
END;
$$;


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 14: API Audit Logs  (HTTP-request-level, GDPR / privilege safe)
--
-- DESIGN RULES:
--   • NO request_body / response_body / payload columns — EVER.
--   • NO columns for client names, case details, or legal content.
--   • Stores ONLY: who called what endpoint, when, and the HTTP result.
--   • Immutable for users — SELECT only. Backend inserts via service_role.
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS api_audit_logs (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    tenant_id       UUID            REFERENCES law_firms(id) ON DELETE SET NULL,
    user_id         UUID            REFERENCES auth.users(id) ON DELETE SET NULL,
    action_name     TEXT            NOT NULL DEFAULT '',          -- e.g. 'POST /api/auth/register'
    endpoint        TEXT            NOT NULL DEFAULT '',          -- e.g. '/api/evaluate/upload'
    http_method     TEXT            NOT NULL DEFAULT 'GET',       -- GET | POST | PUT | PATCH | DELETE
    status_code     SMALLINT        NOT NULL DEFAULT 200,
    duration_ms     INTEGER,                                      -- optional: request duration
    ip_hash         TEXT                                           -- SHA-256 of IP (not raw IP)
);

-- Constraint: http_method enum
DO $$ BEGIN
    ALTER TABLE api_audit_logs ADD CONSTRAINT chk_api_audit_http_method
        CHECK (http_method IN ('GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'HEAD'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Constraint: status_code realistic range
DO $$ BEGIN
    ALTER TABLE api_audit_logs ADD CONSTRAINT chk_api_audit_status_code
        CHECK (status_code BETWEEN 100 AND 599);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Admin dashboard indexes ──

-- Primary: query by tenant + date (admin dashboard filter)
CREATE INDEX IF NOT EXISTS idx_api_audit_tenant_date
    ON api_audit_logs (tenant_id, created_at DESC);

-- User activity view
CREATE INDEX IF NOT EXISTS idx_api_audit_user_date
    ON api_audit_logs (user_id, created_at DESC);

-- Endpoint analytics
CREATE INDEX IF NOT EXISTS idx_api_audit_endpoint
    ON api_audit_logs (endpoint, created_at DESC);

-- Error monitoring: quickly find 4xx/5xx
CREATE INDEX IF NOT EXISTS idx_api_audit_errors
    ON api_audit_logs (status_code, created_at DESC)
    WHERE status_code >= 400;

-- Time-based partitioning hint: created_at BRIN index for range scans
CREATE INDEX IF NOT EXISTS idx_api_audit_created_brin
    ON api_audit_logs USING BRIN (created_at);

-- ── RLS ──

ALTER TABLE api_audit_logs ENABLE ROW LEVEL SECURITY;

-- Backend (service_role) can insert and read all
DROP POLICY IF EXISTS "service_role_full_access" ON api_audit_logs;
CREATE POLICY "service_role_full_access" ON api_audit_logs
    FOR ALL USING (auth.role() = 'service_role');

-- Users: SELECT only their own rows (immutable — no UPDATE / DELETE)
DROP POLICY IF EXISTS "users_select_own_api_audit" ON api_audit_logs;
CREATE POLICY "users_select_own_api_audit" ON api_audit_logs
    FOR SELECT USING (auth.uid() = user_id);

-- ── Admin RPC: paginated audit log query ──
-- Security: verifies caller belongs to the requested tenant via user_profiles.

CREATE OR REPLACE FUNCTION get_api_audit_logs(
    p_tenant_id UUID,
    p_from      TIMESTAMPTZ DEFAULT now() - interval '30 days',
    p_to        TIMESTAMPTZ DEFAULT now(),
    p_limit     INT         DEFAULT 100,
    p_offset    INT         DEFAULT 0
)
RETURNS TABLE (
    id          UUID,
    created_at  TIMESTAMPTZ,
    user_id     UUID,
    action_name TEXT,
    endpoint    TEXT,
    http_method TEXT,
    status_code SMALLINT,
    duration_ms INTEGER
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    -- Verify caller belongs to this tenant
    IF NOT EXISTS (
        SELECT 1 FROM user_profiles
        WHERE user_id = auth.uid()
          AND law_firm_id = p_tenant_id
    ) THEN
        RAISE EXCEPTION 'Access denied: you do not belong to tenant %', p_tenant_id;
    END IF;

    RETURN QUERY
    SELECT a.id, a.created_at, a.user_id, a.action_name, a.endpoint,
           a.http_method, a.status_code, a.duration_ms
      FROM api_audit_logs a
     WHERE a.tenant_id = p_tenant_id
       AND a.created_at BETWEEN p_from AND p_to
     ORDER BY a.created_at DESC
     LIMIT p_limit OFFSET p_offset;
END;
$$;

GRANT EXECUTE ON FUNCTION get_api_audit_logs(UUID, TIMESTAMPTZ, TIMESTAMPTZ, INT, INT) TO authenticated;

-- ── Admin RPC: aggregate endpoint stats (for dashboard charts) ──
-- Security: verifies caller belongs to the requested tenant via user_profiles.

CREATE OR REPLACE FUNCTION get_api_audit_stats(
    p_tenant_id UUID,
    p_days      INT DEFAULT 30
)
RETURNS TABLE (
    endpoint        TEXT,
    http_method     TEXT,
    total_requests  BIGINT,
    avg_duration_ms NUMERIC,
    error_count     BIGINT
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    -- Verify caller belongs to this tenant
    IF NOT EXISTS (
        SELECT 1 FROM user_profiles
        WHERE user_id = auth.uid()
          AND law_firm_id = p_tenant_id
    ) THEN
        RAISE EXCEPTION 'Access denied: you do not belong to tenant %', p_tenant_id;
    END IF;

    RETURN QUERY
    SELECT a.endpoint, a.http_method,
           COUNT(*)                                         AS total_requests,
           ROUND(AVG(a.duration_ms), 1)                    AS avg_duration_ms,
           COUNT(*) FILTER (WHERE a.status_code >= 400)    AS error_count
      FROM api_audit_logs a
     WHERE a.tenant_id = p_tenant_id
       AND a.created_at >= now() - (p_days || ' days')::interval
     GROUP BY a.endpoint, a.http_method
     ORDER BY total_requests DESC;
END;
$$;

GRANT EXECUTE ON FUNCTION get_api_audit_stats(UUID, INT) TO authenticated;

-- ── Auto-cleanup: purge logs older than 90 days (call from pg_cron) ──

CREATE OR REPLACE FUNCTION purge_old_api_audit_logs(p_retention_days INT DEFAULT 90)
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE
    cnt INT;
BEGIN
    DELETE FROM api_audit_logs
     WHERE created_at < now() - (p_retention_days || ' days')::interval;

    GET DIAGNOSTICS cnt = ROW_COUNT;
    RETURN cnt;
END;
$$;

-- NOTE: Schedule via pg_cron:
--   SELECT cron.schedule('purge-api-audit-logs', '0 3 * * 0',
--       $$SELECT purge_old_api_audit_logs(90)$$);


-- ═══════════════════════════════════════════════════════════════════════════
-- SEKCIJA 15: Case Events / Upisnik  (kronološki pregled radnji u spisu)
--
-- Svaka radnja u spisu (podnesak, ročište, savjet, pristup sudu, itd.)
-- evidentira se ovdje. Povezuje se s tarifom za automatski izračun troškova.
--
-- event_type vrijednosti mapiraju se na stavke Odvjetničke tarife (NN 142/2012).
-- ═══════════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS case_events (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    case_id         UUID            NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    tenant_id       UUID            REFERENCES law_firms(id) ON DELETE SET NULL,

    -- Event data
    event_date      TIMESTAMPTZ     NOT NULL DEFAULT now(),
    event_type      TEXT            NOT NULL DEFAULT 'Ostalo',
    description     TEXT            NOT NULL DEFAULT '',
    tariff_item     TEXT,                                          -- Tbr. iz tarife (npr. 'Tbr. 7')
    tariff_points   NUMERIC(10,2)   NOT NULL DEFAULT 0,            -- Bodovi iz tarife
    point_value     NUMERIC(10,2)   NOT NULL DEFAULT 0.50,         -- Vrijednost boda (EUR)
    total_eur       NUMERIC(12,2)   GENERATED ALWAYS AS (tariff_points * point_value) STORED,
    is_billable     BOOLEAN         NOT NULL DEFAULT TRUE,
    billing_status  TEXT            NOT NULL DEFAULT 'pending'      -- pending | billed | written_off
);

-- Constraints
DO $$ BEGIN
    ALTER TABLE case_events ADD CONSTRAINT chk_case_events_points_positive
        CHECK (tariff_points >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE case_events ADD CONSTRAINT chk_case_events_point_value_positive
        CHECK (point_value >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE case_events ADD CONSTRAINT chk_case_events_billing_status
        CHECK (billing_status IN ('pending', 'billed', 'written_off'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE case_events ADD CONSTRAINT chk_case_events_type
        CHECK (event_type IN (
            'Podnesak', 'Ročište', 'Pravni savjet', 'Pristup sudu',
            'Pregled spisa', 'Sastanak s klijentom', 'Dopis',
            'Žalba', 'Prigovor', 'Nagodba', 'Ovrha',
            'Uvid u dokaze', 'Vještačenje', 'Ostalo'
        ));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Indexes: fast chronological retrieval by case
CREATE INDEX IF NOT EXISTS idx_case_events_case_date
    ON case_events (case_id, event_date DESC);

-- Indexes: user's events across all cases
CREATE INDEX IF NOT EXISTS idx_case_events_user_date
    ON case_events (user_id, event_date DESC);

-- Indexes: tenant-level view
CREATE INDEX IF NOT EXISTS idx_case_events_tenant_date
    ON case_events (tenant_id, event_date DESC)
    WHERE tenant_id IS NOT NULL;

-- Indexes: billable events only
CREATE INDEX IF NOT EXISTS idx_case_events_billable
    ON case_events (case_id, billing_status)
    WHERE is_billable = TRUE;

-- RLS
ALTER TABLE case_events ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON case_events;
CREATE POLICY "service_role_full_access" ON case_events
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_case_events" ON case_events;
CREATE POLICY "users_own_case_events" ON case_events
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- updated_at trigger
DROP TRIGGER IF EXISTS trg_case_events_updated_at ON case_events;
CREATE TRIGGER trg_case_events_updated_at
    BEFORE UPDATE ON case_events
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- RPC: Get case events summary (total points, total EUR, counts by type)
CREATE OR REPLACE FUNCTION get_case_events_summary(p_case_id UUID, p_user_id UUID)
RETURNS JSON
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    result JSON;
BEGIN
    SELECT json_build_object(
        'total_events',      (SELECT COUNT(*) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id),
        'billable_events',   (SELECT COUNT(*) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE),
        'total_points',      COALESCE((SELECT SUM(tariff_points) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE), 0),
        'total_eur',         COALESCE((SELECT SUM(total_eur) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE), 0),
        'pending_eur',       COALESCE((SELECT SUM(total_eur) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE AND billing_status = 'pending'), 0),
        'billed_eur',        COALESCE((SELECT SUM(total_eur) FROM case_events WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE AND billing_status = 'billed'), 0),
        'by_type', (
            SELECT COALESCE(json_agg(row_to_json(t)), '[]'::json)
            FROM (
                SELECT event_type, COUNT(*) AS cnt, SUM(tariff_points) AS points, SUM(total_eur) AS eur
                FROM case_events
                WHERE case_id = p_case_id AND user_id = p_user_id AND is_billable = TRUE
                GROUP BY event_type ORDER BY eur DESC
            ) t
        )
    ) INTO result;

    RETURN result;
END;
$$;

GRANT EXECUTE ON FUNCTION get_case_events_summary(UUID, UUID) TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════════
-- GOTOVO! Sve migracije su primijenjene.
-- ═══════════════════════════════════════════════════════════════════════════
-- TIP: Za Supabase Storage bucket "documents", idi na:
--   Dashboard → Storage → New Bucket
--   Name: documents | Public: false | Limit: 50 MB
--
-- TIP: Za automatski refresh materialized viewa:
--   SELECT cron.schedule('refresh-monthly-revenue', '0 2 * * *',
--       'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue');
--
-- TIP: Za auto-expire pretplata i čišćenje audit logova:
--   SELECT cron.schedule('expire-subscriptions', '0 1 * * *',
--       $$SELECT expire_overdue_subscriptions()$$);
--   SELECT cron.schedule('purge-api-audit-logs', '0 3 * * 0',
--       $$SELECT purge_old_api_audit_logs(90)$$);
-- ═══════════════════════════════════════════════════════════════════════════
