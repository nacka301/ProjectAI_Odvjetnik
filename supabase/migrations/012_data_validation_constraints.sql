-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 012: Data validation constraints
-- Adds CHECK constraints for business rules and data integrity.
-- Prevents invalid data even if the backend skips validation.
-- Safe to run multiple times (use IF NOT EXISTS pattern via DO blocks).
-- VAŽNO: Svaki constraint ima UPDATE koji prvo čisti postojeće podatke.
-- ═══════════════════════════════════════════════════════════════════

-- ── Clients: OIB must be 11 digits, empty, or NULL ──
UPDATE clients SET oib = NULL
    WHERE oib IS NOT NULL AND oib != '' AND oib !~ '^\d{11}$';

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_oib_format
        CHECK (oib IS NULL OR oib = '' OR oib ~ '^\d{11}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Clients: tip must be valid enum ──
UPDATE clients SET tip = 'fizicka'
    WHERE tip NOT IN ('fizicka', 'pravna');

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_tip
        CHECK (tip IN ('fizicka', 'pravna'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Cases: status must be valid enum ──
UPDATE cases SET status = 'aktivan'
    WHERE status NOT IN ('aktivan', 'zatvoren', 'mirovanje', 'arhiviran');

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_status
        CHECK (status IN ('aktivan', 'zatvoren', 'mirovanje', 'arhiviran'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Cases: vrsta must be valid enum ──
UPDATE cases SET vrsta = 'ostalo'
    WHERE vrsta NOT IN ('parnica', 'ovrha', 'savjetovanje', 'kazneni', 'upravni', 'izvanparnicni', 'stecajni', 'registarski', 'ostalo');

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_vrsta
        CHECK (vrsta IN ('parnica', 'ovrha', 'savjetovanje', 'kazneni', 'upravni', 'izvanparnicni', 'stecajni', 'registarski', 'ostalo'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Invoices: status must be valid enum ──
UPDATE invoices SET status = 'DRAFT'
    WHERE status NOT IN ('DRAFT', 'ISSUED', 'SENT', 'PAID', 'CANCELLED');

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_status
        CHECK (status IN ('DRAFT', 'ISSUED', 'SENT', 'PAID', 'CANCELLED'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Invoices: amounts must be non-negative ──
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

-- ── Invoices: OIB format (NULL, empty, or 11 digits) ──
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

-- ── Troskovnici: bodovi and amounts must be non-negative ──
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

-- ── Evaluations: estimated_value must be positive ──
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

-- ── Subscriptions: plan_name valid enum ──
UPDATE subscriptions SET plan_name = 'Pro'
    WHERE plan_name NOT IN ('Starter', 'Pro', 'Enterprise');

DO $$ BEGIN
    ALTER TABLE subscriptions ADD CONSTRAINT chk_subscriptions_plan
        CHECK (plan_name IN ('Starter', 'Pro', 'Enterprise'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ── Subscriptions: status valid enum ──
UPDATE subscriptions SET status = 'active'
    WHERE status NOT IN ('active', 'trial', 'canceled', 'expired');

DO $$ BEGIN
    ALTER TABLE subscriptions ADD CONSTRAINT chk_subscriptions_status
        CHECK (status IN ('active', 'trial', 'canceled', 'expired'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;
