-- ══════════════════════════════════════════════════════════════════════
-- PravoTek — DEV BOOTSTRAP (puna shema za svježu dev Supabase bazu)
-- Generirano spajanjem unified_schema.sql + svih 32 migracija, ispravnim redom.
-- Pokreni JEDNOM u dev projektu: Supabase Dashboard → SQL Editor → New → Run.
-- Idempotentno (IF NOT EXISTS / DO blokovi) — sigurno za ponovljeno pokretanje.
-- NE pokretati na produkciji; ovo je za dev bazu (ai-odvjetnik-dev).
-- ══════════════════════════════════════════════════════════════════════

-- ═══════════ unified_schema.sql ═══════════
-- ╔═══════════════════════════════════════════════════════════════════════════╗
-- ║           PravoTek — UNIFIED SCHEMA (Cross-Referenced with C# DTOs)    ║
-- ║                                                                         ║
-- ║  Source of truth: C# Record DTOs in Backend/Models + Backend/Services   ║
-- ║  Generated: 2026-03-03                                                  ║
-- ║                                                                         ║
-- ║  BLOCK 1: Base Tables & Enums (Profiles, Law Firms, Subscriptions)      ║
-- ║  BLOCK 2: Core Business Tables                                          ║
-- ║  BLOCK 3: RLS Policies, Indexes, Functions                              ║
-- ║                                                                         ║
-- ║  Safe for repeated execution (IF NOT EXISTS / DO blocks).               ║
-- ╚═══════════════════════════════════════════════════════════════════════════╝


-- ═══════════════════════════════════════════════════════════════════════════
-- BLOCK 1: BASE TABLES & ENUMS
-- (Profiles, Law Firms, Subscriptions, Usage Tracking)
-- ═══════════════════════════════════════════════════════════════════════════

-- ── Extensions ──

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS vector;


-- ─────────────────────────────────────────────────────────
--  1.1  law_firms
--  C# DTO: LawFirmResponse { Id, Name, Oib, Address, Iban }
--  Written by: SettingsService.CreateFirmAndLinkAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS law_firms (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    name            TEXT            NOT NULL DEFAULT '',
    oib             TEXT,
    address         TEXT,
    iban            TEXT,
    pdv_obveznik    BOOLEAN         NOT NULL DEFAULT FALSE   -- zadana vrijednost za novi troškovnik (migracija 021)
);


-- ─────────────────────────────────────────────────────────
--  1.2  user_profiles
--  C# DTO: ProfileResponse { UserId, FullName, PhoneNumber, Email, Role, LawFirmId }
--  Written by: SettingsService.UpdateProfileAsync / UpsertProfileAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS user_profiles (
    user_id         UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    full_name       TEXT            NOT NULL DEFAULT '',
    phone_number    TEXT,
    email           TEXT,
    role            TEXT            NOT NULL DEFAULT 'admin',
    law_firm_id     UUID            REFERENCES law_firms(id) ON DELETE SET NULL
);


-- ─────────────────────────────────────────────────────────
--  1.3  tenant_subscriptions
--  C# DTO: SubscriptionResponse { Id, PlanId, Status, ValidFrom, ValidUntil,
--           PriceEur, MaxCases, MaxInvoices, AiCreditsLimit, LawFirmId }
--  Read by: SettingsService.GetSubscriptionAsync (law_firm_id, plan_id, status, ...)
--        + UsageService.GetSnapshotAsync (plan_id, max_cases, max_invoices, ai_credits_limit)
--  Written by: SettingsService.CreateDefaultSubscriptionAsync
--
--  !! CONFLICT FIX: all_in_one.sql had tenant_id → now law_firm_id
--  !! CONFLICT FIX: all_in_one.sql had plan_type → now plan_id
--  !! ADDED: status, valid_from, price_eur, max_cases, max_invoices, ai_credits_limit
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS tenant_subscriptions (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    law_firm_id         UUID            NOT NULL REFERENCES law_firms(id) ON DELETE CASCADE,
    plan_id             TEXT            NOT NULL DEFAULT 'pravotek',
    status              TEXT            NOT NULL DEFAULT 'active',
    valid_from          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    valid_until         TIMESTAMPTZ     NOT NULL DEFAULT (now() + interval '30 days'),
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,
    price_eur           NUMERIC(10,2)   NOT NULL DEFAULT 19,
    max_cases           INTEGER         NOT NULL DEFAULT -1,   -- -1 = neograniceno
    max_invoices        INTEGER         NOT NULL DEFAULT -1,
    ai_credits_limit    INTEGER         NOT NULL DEFAULT 0
);

-- plan_id: jedna vrijednost, jer postoji jedan plan (migracija 024).
-- Stupac je ostao radi kompatibilnosti; vise ne nosi informaciju.
DO $$ BEGIN
    ALTER TABLE tenant_subscriptions ADD CONSTRAINT chk_tenant_sub_plan_id
        CHECK (plan_id = 'pravotek');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- status enum
DO $$ BEGIN
    ALTER TABLE tenant_subscriptions ADD CONSTRAINT chk_tenant_sub_status
        CHECK (status IN ('active', 'trial', 'canceled', 'expired'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  1.4  tenant_usage_stats  *** NEW TABLE ***
--  C# DTO: read directly via JsonDocument in UsageService
--  Columns: law_firm_id, period_start, cases_used, invoices_used, ai_credits_used
--  Written by: UsageService.IncrementAsync
--  Read by: UsageService.GetSnapshotAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS tenant_usage_stats (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    law_firm_id         UUID            NOT NULL REFERENCES law_firms(id) ON DELETE CASCADE,
    period_start        DATE            NOT NULL DEFAULT (date_trunc('month', now())::date),
    cases_used          INTEGER         NOT NULL DEFAULT 0,
    invoices_used       INTEGER         NOT NULL DEFAULT 0,
    ai_credits_used     INTEGER         NOT NULL DEFAULT 0,
    UNIQUE (law_firm_id, period_start)
);


-- ─────────────────────────────────────────────────────────
--  1.5  firm_usage  (per-module Beta credit tracking)
--  C# DTO: FirmUsageResponse { Id, LawFirmId, ModuleName, UsedCredits, MaxCredits, ResetDate }
--  Written by: CreditService.CreateDefaultUsageAsync / IncrementUsageAsync
--  Read by: CreditService.GetUsageAsync / GetFirmUsageAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS firm_usage (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    law_firm_id     UUID            NOT NULL REFERENCES law_firms(id) ON DELETE CASCADE,
    module_name     TEXT            NOT NULL DEFAULT '',
    used_credits    INTEGER         NOT NULL DEFAULT 0,
    max_credits     INTEGER         NOT NULL DEFAULT 50,
    reset_date      TIMESTAMPTZ,
    UNIQUE (law_firm_id, module_name)
);

DO $$ BEGIN
    ALTER TABLE firm_usage ADD CONSTRAINT chk_firm_usage_credits
        CHECK (used_credits >= 0 AND max_credits >= 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  1.6  subscriptions (LEGACY — kept for back-compat)
--  Referenced by: all_in_one.sql constraints & triggers
--  Being replaced by tenant_subscriptions, but keep for existing data.
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS subscriptions (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    plan_name       TEXT            NOT NULL DEFAULT 'Pro',
    status          TEXT            NOT NULL DEFAULT 'active',
    valid_from      TIMESTAMPTZ     DEFAULT now(),
    valid_until     TIMESTAMPTZ
);


-- ═══════════════════════════════════════════════════════════════════════════
-- BLOCK 2: CORE BUSINESS TABLES
-- (Clients, Cases, CaseEvents, Invoices, Troskovnici, Evaluations,
--  ContractAnalyses, Chronologies, LegalStrategies, ZkAnalyses,
--  ValuationAudits, AuditLogs, ApiAuditLogs, LawChunks, Documents, etc.)
-- ═══════════════════════════════════════════════════════════════════════════


-- ─────────────────────────────────────────────────────────
--  2.1  clients
--  C# DTO: ClientRecord { UserId, ImeNaziv, Oib, Adresa, Grad,
--           PostanskiBroj, Email, Telefon, Tip, Napomena }
--  Written by: ClientCaseService.InsertAsync<ClientReadRecord>("clients", record)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS clients (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    ime_naziv       TEXT            NOT NULL DEFAULT '',
    oib             TEXT,
    adresa          TEXT,
    grad            TEXT,
    postanski_broj  TEXT,
    email           TEXT,
    telefon         TEXT,
    tip             TEXT            NOT NULL DEFAULT 'fizicka',
    napomena        TEXT
);

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_oib_format
        CHECK (oib IS NULL OR oib = '' OR oib ~ '^\d{11}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE clients ADD CONSTRAINT chk_clients_tip
        CHECK (tip IN ('fizicka', 'pravna'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  2.2  cases
--  C# DTO: CaseRecord { UserId, ClientId, BrojPredmeta, Naziv, Opis,
--           Vrsta, Status, VrijednostSpora, Sud, Sudac,
--           DatumPokretanja, DatumZatvaranja }
--  CaseReadRecord adds: Id, CreatedAt, UpdatedAt, IsDeleted
--  Written by: ClientCaseService.InsertAsync<CaseReadRecord>("cases", record)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS cases (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id             UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    client_id           UUID            REFERENCES clients(id) ON DELETE SET NULL,
    broj_predmeta       TEXT            NOT NULL DEFAULT '',
    naziv               TEXT            NOT NULL DEFAULT '',
    opis                TEXT,
    vrsta               TEXT            NOT NULL DEFAULT 'parnica',
    status              TEXT            NOT NULL DEFAULT 'aktivan',
    vrijednost_spora    NUMERIC(14,2),
    sud                 TEXT,
    sudac               TEXT,
    datum_pokretanja    TEXT,
    datum_zatvaranja    TEXT,
    is_deleted          BOOLEAN         NOT NULL DEFAULT FALSE,
    is_archived         BOOLEAN         NOT NULL DEFAULT FALSE
);

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_status
        CHECK (status IN ('aktivan', 'zatvoren', 'mirovanje', 'arhiviran'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE cases ADD CONSTRAINT chk_cases_vrsta
        CHECK (vrsta IN ('parnica', 'ovrha', 'savjetovanje', 'kazneni', 'upravni', 'izvanparnicni', 'stecajni', 'registarski', 'ostalo'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  2.3  troskovnici
--  C# DTO: TroskovnikRecord { UserId, ClientId, CaseId, OpisPredmeta,
--           VrijednostSpora, UkupnoBodova, UkupnoEurBezPdv, Pdv,
--           UkupnoEurSPdv, Odvjetnik (JSONB), Predmet (JSONB),
--           Stranke (JSONB), HasVat, IsPdvUkljucen, ZaprijecenaKazna,
--           IsPoSluzbenojDuznosti, IsNaTeretProracuna, IsZastupnikDjeteta,
--           IsStjecaj, IsOduzimanjePrekrsaj, VrijednostOduzimanja,
--           RawJsonOutput (JSONB) }
--  Written by: ClientCaseService.InsertAsync<TroskovnikReadRecord>("troskovnici", record)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS troskovnici (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at              TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id                 UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    client_id               UUID            REFERENCES clients(id) ON DELETE SET NULL,
    case_id                 UUID            REFERENCES cases(id) ON DELETE SET NULL,
    opis_predmeta           TEXT,
    vrijednost_spora        NUMERIC(14,2)   DEFAULT 0,
    ukupno_bodova           NUMERIC(14,3)   DEFAULT 0,  -- decimalno: Tarifa ne zaokružuje bodove (migracija 2026_07_29)
    ukupno_eur_bez_pdv      NUMERIC(12,2)   DEFAULT 0,
    pdv                     NUMERIC(12,2)   DEFAULT 0,
    ukupno_eur_s_pdv        NUMERIC(12,2)   DEFAULT 0,
    naknada_troskova_ukupno NUMERIC(12,2)   NOT NULL DEFAULT 0,  -- Tbr. 48.-51., ulazi u osnovicu (migracija 021)
    predujmovi_ukupno       NUMERIC(12,2)   NOT NULL DEFAULT 0,  -- prolazna stavka, ne ulazi u osnovicu (migracija 021)

    -- Nested JSONB objects
    odvjetnik               JSONB,                          -- {naziv, oib, adresa, iban}
    predmet                 JSONB,                          -- {sud, poslovni_broj}
    stranke                 JSONB,                          -- {zastupana_stranka: {...}, protustranka: {...}}

    -- PDV / party fields
    has_vat                 BOOLEAN         NOT NULL DEFAULT TRUE,
    is_pdv_ukljucen         BOOLEAN         NOT NULL DEFAULT FALSE,

    -- Legacy flat party fields (kept for back-compat)
    odvjetnik_adresa        TEXT,
    odvjetnik_iban          TEXT,
    protustranka_ime        TEXT,
    protustranka_oib        TEXT,
    protustranka_adresa     TEXT,
    protustranka_polozaj    TEXT,

    -- Kazneni fields
    zaprijecena_kazna       TEXT,
    is_po_sluzbenoj_duznosti BOOLEAN        DEFAULT FALSE,
    is_na_teret_proracuna   BOOLEAN         DEFAULT FALSE,
    is_zastupnik_djeteta    BOOLEAN         DEFAULT FALSE,
    is_stjecaj              BOOLEAN         DEFAULT FALSE,
    is_oduzimanje_prekrsaj  BOOLEAN         DEFAULT FALSE,
    vrijednost_oduzimanja   NUMERIC         DEFAULT 0,

    -- Full calculation output (JSONB)
    raw_json_output         JSONB,

    -- Soft delete
    is_archived             BOOLEAN         DEFAULT FALSE
);

COMMENT ON TABLE troskovnici IS 'Odvjetnički troškovnici — persisted fee calculations';
COMMENT ON COLUMN troskovnici.odvjetnik IS 'JSONB: {naziv, oib, adresa, iban}';
COMMENT ON COLUMN troskovnici.predmet IS 'JSONB: {sud, poslovni_broj}';
COMMENT ON COLUMN troskovnici.stranke IS 'JSONB: {zastupana_stranka: {...}, protustranka: {...}}';

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


-- ─────────────────────────────────────────────────────────
--  2.4  case_events / Upisnik
--  C# DTO: CaseEventRecord { CaseId, UserId, TenantId, EventDate,
--           EventType, Description, TariffItem, TariffPoints,
--           PointValue, IsBillable, BillingStatus }
--  CaseEventReadRecord adds: Id, CreatedAt, UpdatedAt, TotalEur (GENERATED)
--  Written by: ClientCaseService.InsertAsync<CaseEventReadRecord>("case_events", record)
-- ─────────────────────────────────────────────────────────

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
    tariff_points   NUMERIC(10,2)   NOT NULL DEFAULT 0,
    point_value     NUMERIC(10,2)   NOT NULL DEFAULT 0.50,
    total_eur       NUMERIC(12,2)   GENERATED ALWAYS AS (tariff_points * point_value) STORED,
    is_billable     BOOLEAN         NOT NULL DEFAULT TRUE,
    billing_status  TEXT            NOT NULL DEFAULT 'pending'
);

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


-- ─────────────────────────────────────────────────────────
--  2.5  invoices
--  C# DTO: InvoiceRecord { UserId, InvoiceNumber, IssueDate, DueDate,
--           IssuerName, IssuerOib, ClientName, ClientOib, NetAmount,
--           PdvRate, PdvAmount, GrossAmount, Module, AnalysisId,
--           CaseReference, Status, UblXml, RawJsonOutput }
--  Written by: SupabaseService.SaveInvoiceAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS invoices (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    invoice_number  TEXT            NOT NULL DEFAULT '',
    issue_date      TEXT            NOT NULL DEFAULT '',
    due_date        TEXT            NOT NULL DEFAULT '',
    issuer_name     TEXT            NOT NULL DEFAULT '',
    issuer_oib      TEXT,
    client_name     TEXT            NOT NULL DEFAULT '',
    client_oib      TEXT,
    net_amount      NUMERIC(12,2)   NOT NULL DEFAULT 0,
    pdv_rate        NUMERIC(5,2)    NOT NULL DEFAULT 25,
    pdv_amount      NUMERIC(12,2)   NOT NULL DEFAULT 0,
    gross_amount    NUMERIC(12,2)   NOT NULL DEFAULT 0,
    module          TEXT            NOT NULL DEFAULT '',
    analysis_id     TEXT,
    case_reference  TEXT,
    status          TEXT            NOT NULL DEFAULT 'DRAFT',
    ubl_xml         TEXT            NOT NULL DEFAULT '',
    raw_json_output JSONB,
    is_archived     BOOLEAN         DEFAULT FALSE
);

DO $$ BEGIN
    ALTER TABLE invoices ADD CONSTRAINT chk_invoices_status
        CHECK (status IN ('DRAFT', 'ISSUED', 'SENT', 'PAID', 'CANCELLED'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

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


-- ─────────────────────────────────────────────────────────
--  2.6  evaluations
--  C# DTO: EvaluationRecord { UserId, PropertyType, Location,
--           SizeM2, EstimatedValue, RawJsonOutput }
--  Written by: SupabaseService.SaveEvaluationAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS evaluations (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    property_type   TEXT            NOT NULL DEFAULT '',
    location        TEXT            NOT NULL DEFAULT '',
    size_m2         DOUBLE PRECISION,
    estimated_value DOUBLE PRECISION,
    raw_json_output JSONB,
    is_archived     BOOLEAN         DEFAULT FALSE
);

DO $$ BEGIN
    ALTER TABLE evaluations ADD CONSTRAINT chk_evaluations_value_positive
        CHECK (estimated_value IS NULL OR estimated_value > 0);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  2.7  contract_analyses
--  C# DTO: ContractAnalysisRecord { UserId, DocumentType, Summary,
--           OverallRisk, RiskScore, RedFlagsCount, RawJsonOutput }
--  Written by: SupabaseService.SaveContractAnalysisAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS contract_analyses (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    document_type   TEXT            NOT NULL DEFAULT '',
    summary         TEXT            NOT NULL DEFAULT '',
    overall_risk    TEXT            NOT NULL DEFAULT '',
    risk_score      INTEGER         NOT NULL DEFAULT 0,
    red_flags_count INTEGER         NOT NULL DEFAULT 0,
    raw_json_output JSONB,
    is_archived     BOOLEAN         DEFAULT FALSE
);


-- ─────────────────────────────────────────────────────────
--  2.8  chronologies
--  C# DTO: ChronologyRecord { UserId, TotalDocuments, TotalEvents,
--           DateRange, RawJsonOutput }
--  Written by: SupabaseService.SaveChronologyAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS chronologies (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    total_documents INTEGER         NOT NULL DEFAULT 0,
    total_events    INTEGER         NOT NULL DEFAULT 0,
    date_range      TEXT            NOT NULL DEFAULT '',
    raw_json_output JSONB,
    is_archived     BOOLEAN         DEFAULT FALSE
);


-- ─────────────────────────────────────────────────────────
--  2.9  legal_strategies
--  C# DTO: LegalStrategyRecord { UserId, KeyLegalIssue, ClientPosition,
--           WinProbability, WinProbabilityMin, WinProbabilityMax,
--           PrecedentsCount, RawJsonOutput }
--  Written by: SupabaseService.SaveLegalStrategyAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS legal_strategies (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id             UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    key_legal_issue     TEXT            NOT NULL DEFAULT '',
    client_position     TEXT            NOT NULL DEFAULT 'kupac',
    win_probability     INTEGER         NOT NULL DEFAULT 0,
    win_probability_min INTEGER         NOT NULL DEFAULT 0,
    win_probability_max INTEGER         NOT NULL DEFAULT 0,
    precedents_count    INTEGER         NOT NULL DEFAULT 0,
    raw_json_output     JSONB
);

DO $$ BEGIN
    ALTER TABLE legal_strategies ADD CONSTRAINT chk_legal_strategies_win_prob
        CHECK (win_probability BETWEEN 0 AND 100);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  2.10  zk_analyses
--  C# DTO: ZkAnalysisRecord { UserId, PropertyId, CadastralMunicipality,
--           ParcelNumber, PropertyType, Location, LandAreaM2,
--           BuildingAreaM2, RiskScore, Owners, Mortgages, Disputes,
--           Encumbrances, LawyerComment, EvaluationId, RawJsonOutput }
--  Written by: SupabaseService.SaveZkAnalysisAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS zk_analyses (
    id                      UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at              TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id                 UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    property_id             TEXT            NOT NULL DEFAULT '',
    cadastral_municipality  TEXT,
    parcel_number           TEXT,
    property_type           TEXT,
    location                TEXT            NOT NULL DEFAULT '',
    land_area_m2            DOUBLE PRECISION,
    building_area_m2        DOUBLE PRECISION,
    risk_score              TEXT            NOT NULL DEFAULT '',
    owners                  JSONB,
    mortgages               JSONB,
    disputes                JSONB,
    encumbrances            JSONB,
    lawyer_comment          TEXT,
    evaluation_id           TEXT,
    raw_json_output         JSONB,
    is_archived             BOOLEAN         DEFAULT FALSE
);


-- ─────────────────────────────────────────────────────────
--  2.11  valuation_audits  *** NEW TABLE ***
--  C# DTO: AuditResultRecord { UserId, EvaluationId, Verdict,
--           OverallConfidence, Summary, Recommendation, PriceScore,
--           LocationScore, MethodologyScore, DataQualityScore,
--           FindingsCount, CriticalCount, ModelUsed, PromptTokens,
--           CompletionTokens, RawJsonOutput }
--  Written by: SupabaseService.SaveAuditResultAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS valuation_audits (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,
    evaluation_id       TEXT            NOT NULL DEFAULT '',
    verdict             TEXT            NOT NULL DEFAULT '',
    overall_confidence  INTEGER         NOT NULL DEFAULT 0,
    summary             TEXT            NOT NULL DEFAULT '',
    recommendation      TEXT            NOT NULL DEFAULT '',
    price_score         INTEGER         NOT NULL DEFAULT 0,
    location_score      INTEGER         NOT NULL DEFAULT 0,
    methodology_score   INTEGER         NOT NULL DEFAULT 0,
    data_quality_score  INTEGER         NOT NULL DEFAULT 0,
    findings_count      INTEGER         NOT NULL DEFAULT 0,
    critical_count      INTEGER         NOT NULL DEFAULT 0,
    model_used          TEXT            NOT NULL DEFAULT '',
    prompt_tokens       INTEGER         NOT NULL DEFAULT 0,
    completion_tokens   INTEGER         NOT NULL DEFAULT 0,
    raw_json_output     JSONB
);


-- ─────────────────────────────────────────────────────────
--  2.12  audit_logs (EU AI Act Article 13 compliance)
--  C# DTO: AuditLogRecord { UserId, Module, AnalysisId, Step,
--           Action, LegalBasis, Citation, Reasoning, Confidence,
--           ModelUsed, ModelTemperature }
--  Written by: SupabaseService.SaveAuditLogAsync / SaveAuditLogBatchAsync
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS audit_logs (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id             UUID            REFERENCES auth.users(id) ON DELETE SET NULL,
    module              TEXT            NOT NULL DEFAULT '',
    analysis_id         TEXT,
    step                INTEGER         NOT NULL DEFAULT 1,
    action              TEXT            NOT NULL DEFAULT '',
    legal_basis         TEXT            NOT NULL DEFAULT '',
    citation            TEXT            NOT NULL DEFAULT '',
    reasoning           TEXT            NOT NULL DEFAULT '',
    confidence          TEXT            NOT NULL DEFAULT 'SREDNJE',
    model_used          TEXT            NOT NULL DEFAULT '',
    model_temperature   DOUBLE PRECISION NOT NULL DEFAULT 0.10
);


-- ─────────────────────────────────────────────────────────
--  2.13  api_audit_logs (HTTP-request-level audit)
--  No C# DTO — written by middleware (SupabaseJwtMiddleware)
--  Stores ONLY: who called what endpoint, when, HTTP result.
--  NO request_body / response_body / payload — EVER (GDPR).
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS api_audit_logs (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    tenant_id       UUID            REFERENCES law_firms(id) ON DELETE SET NULL,
    user_id         UUID            REFERENCES auth.users(id) ON DELETE SET NULL,
    action_name     TEXT            NOT NULL DEFAULT '',
    endpoint        TEXT            NOT NULL DEFAULT '',
    http_method     TEXT            NOT NULL DEFAULT 'GET',
    status_code     SMALLINT        NOT NULL DEFAULT 200,
    duration_ms     INTEGER,
    ip_hash         TEXT
);

DO $$ BEGIN
    ALTER TABLE api_audit_logs ADD CONSTRAINT chk_api_audit_http_method
        CHECK (http_method IN ('GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'HEAD'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    ALTER TABLE api_audit_logs ADD CONSTRAINT chk_api_audit_status_code
        CHECK (status_code BETWEEN 100 AND 599);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;


-- ─────────────────────────────────────────────────────────
--  2.14  norms (Norm Validity Engine — static reference data)
--  Written by: seed script / admin
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS norms (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    abbreviation    TEXT            NOT NULL DEFAULT '' UNIQUE,
    full_name       TEXT            NOT NULL DEFAULT '',
    nn_reference    TEXT            NOT NULL DEFAULT '',
    status          TEXT            NOT NULL DEFAULT 'Važeći',
    category        TEXT            NOT NULL DEFAULT '',
    amendments      JSONB           DEFAULT '[]'::jsonb
);


-- ─────────────────────────────────────────────────────────
--  2.15  law_chunks (pgvector RAG — semantic search)
--  Written by: embedding pipeline / seed script
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS law_chunks (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    law             TEXT            NOT NULL,
    full_name       TEXT            NOT NULL,
    nn_reference    TEXT            NOT NULL,
    article         TEXT            NOT NULL,
    title           TEXT            NOT NULL,
    content         TEXT            NOT NULL,
    topic_tags      TEXT[]          NOT NULL DEFAULT '{}',
    embedding       vector(1536),
    UNIQUE(law, article)
);


-- ─────────────────────────────────────────────────────────
--  2.16  case_activities (event log on cases — auto-generated)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS case_activities (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    case_id         UUID            NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    activity_type   TEXT            NOT NULL DEFAULT 'note',
    title           TEXT            NOT NULL DEFAULT '',
    description     TEXT,
    metadata        JSONB           DEFAULT '{}'::jsonb,
    linked_id       UUID
);


-- ─────────────────────────────────────────────────────────
--  2.17  documents (file attachments)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS documents (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
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


-- ─────────────────────────────────────────────────────────
--  2.18  troskovnik_templates
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS troskovnik_templates (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
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


-- ─────────────────────────────────────────────────────────
--  2.19  user_notification_prefs
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS user_notification_prefs (
    user_id             UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    email_enabled       BOOLEAN         DEFAULT TRUE,
    email_case_updates  BOOLEAN         DEFAULT TRUE,
    email_invoice_reminders BOOLEAN     DEFAULT TRUE,
    email_credit_low    BOOLEAN         DEFAULT TRUE,
    email_weekly_summary BOOLEAN        DEFAULT FALSE,
    push_enabled        BOOLEAN         DEFAULT FALSE,
    push_case_updates   BOOLEAN         DEFAULT FALSE
);


-- ─────────────────────────────────────────────────────────
--  2.20  notifications (in-app)
-- ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS notifications (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    type            TEXT            NOT NULL DEFAULT 'info',
    title           TEXT            NOT NULL DEFAULT '',
    body            TEXT,
    link            TEXT,
    is_read         BOOLEAN         DEFAULT FALSE,
    read_at         TIMESTAMPTZ,
    metadata        JSONB           DEFAULT '{}'::jsonb
);


-- ═══════════════════════════════════════════════════════════════════════════
-- BLOCK 3: RLS POLICIES, INDEXES, TRIGGERS, AND FUNCTIONS
-- ═══════════════════════════════════════════════════════════════════════════


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.1  Generic updated_at trigger function               │
-- └─────────────────────────────────────────────────────────┘

CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.2  updated_at triggers (all tables with updated_at)  │
-- └─────────────────────────────────────────────────────────┘

DO $$ DECLARE
    t RECORD;
BEGIN
    FOR t IN
        SELECT unnest(ARRAY[
            'law_firms', 'user_profiles', 'tenant_subscriptions', 'tenant_usage_stats',
            'firm_usage', 'subscriptions', 'clients', 'cases', 'troskovnici',
            'case_events', 'invoices', 'documents', 'troskovnik_templates',
            'user_notification_prefs', 'norms'
        ]) AS tbl
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_%I_updated_at ON %I; ' ||
            'CREATE TRIGGER trg_%I_updated_at BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION set_updated_at();',
            t.tbl, t.tbl, t.tbl, t.tbl
        );
    END LOOP;
END $$;


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.3  ROW LEVEL SECURITY — Enable + Policies            │
-- └─────────────────────────────────────────────────────────┘

-- Helper: enable RLS on all tables with user_id
DO $$ DECLARE
    t RECORD;
BEGIN
    FOR t IN
        SELECT unnest(ARRAY[
            'law_firms', 'user_profiles', 'tenant_subscriptions', 'tenant_usage_stats',
            'firm_usage', 'subscriptions', 'clients', 'cases', 'troskovnici',
            'case_events', 'invoices', 'evaluations', 'contract_analyses',
            'chronologies', 'legal_strategies', 'zk_analyses', 'valuation_audits',
            'audit_logs', 'api_audit_logs', 'case_activities', 'documents',
            'troskovnik_templates', 'user_notification_prefs', 'notifications', 'norms'
        ]) AS tbl
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', t.tbl);
    END LOOP;
END $$;

-- ── service_role full access (backend uses service_role key) ──

DO $$ DECLARE
    t RECORD;
BEGIN
    FOR t IN
        SELECT unnest(ARRAY[
            'law_firms', 'user_profiles', 'tenant_subscriptions', 'tenant_usage_stats',
            'firm_usage', 'subscriptions', 'clients', 'cases', 'troskovnici',
            'case_events', 'invoices', 'evaluations', 'contract_analyses',
            'chronologies', 'legal_strategies', 'zk_analyses', 'valuation_audits',
            'audit_logs', 'api_audit_logs', 'case_activities', 'documents',
            'troskovnik_templates', 'user_notification_prefs', 'notifications', 'norms'
        ]) AS tbl
    LOOP
        EXECUTE format(
            'DROP POLICY IF EXISTS "service_role_full_access" ON %I; ' ||
            'CREATE POLICY "service_role_full_access" ON %I FOR ALL USING (auth.role() = ''service_role'');',
            t.tbl, t.tbl
        );
    END LOOP;
END $$;

-- ── User-owned data (tables with user_id column) ──

DO $$ DECLARE
    t RECORD;
BEGIN
    FOR t IN
        SELECT unnest(ARRAY[
            'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
            'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
            'zk_analyses', 'valuation_audits', 'audit_logs', 'case_activities',
            'documents', 'troskovnik_templates', 'notifications'
        ]) AS tbl
    LOOP
        EXECUTE format(
            'DROP POLICY IF EXISTS "users_own_%I" ON %I; ' ||
            'CREATE POLICY "users_own_%I" ON %I FOR ALL USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);',
            t.tbl, t.tbl, t.tbl, t.tbl
        );
    END LOOP;
END $$;

-- ── Special RLS: user_profiles (user_id is PK, not a regular column) ──
DROP POLICY IF EXISTS "users_own_user_profiles" ON user_profiles;
CREATE POLICY "users_own_user_profiles" ON user_profiles
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ── Special RLS: user_notification_prefs (user_id is PK) ──
DROP POLICY IF EXISTS "users_own_user_notification_prefs" ON user_notification_prefs;
CREATE POLICY "users_own_user_notification_prefs" ON user_notification_prefs
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ── Special RLS: tenant_subscriptions (user reads via law_firm_id link) ──
DROP POLICY IF EXISTS "users_read_own_tenant_sub" ON tenant_subscriptions;
CREATE POLICY "users_read_own_tenant_sub" ON tenant_subscriptions
    FOR SELECT USING (
        law_firm_id IN (SELECT up.law_firm_id FROM user_profiles up WHERE up.user_id = auth.uid())
    );

-- ── Special RLS: tenant_usage_stats (same pattern) ──
DROP POLICY IF EXISTS "users_read_own_usage_stats" ON tenant_usage_stats;
CREATE POLICY "users_read_own_usage_stats" ON tenant_usage_stats
    FOR SELECT USING (
        law_firm_id IN (SELECT up.law_firm_id FROM user_profiles up WHERE up.user_id = auth.uid())
    );

-- ── Special RLS: firm_usage (same pattern) ──
DROP POLICY IF EXISTS "users_read_own_firm_usage" ON firm_usage;
CREATE POLICY "users_read_own_firm_usage" ON firm_usage
    FOR SELECT USING (
        law_firm_id IN (SELECT up.law_firm_id FROM user_profiles up WHERE up.user_id = auth.uid())
    );

-- ── Special RLS: api_audit_logs (users SELECT only their own) ──
DROP POLICY IF EXISTS "users_select_own_api_audit" ON api_audit_logs;
CREATE POLICY "users_select_own_api_audit" ON api_audit_logs
    FOR SELECT USING (auth.uid() = user_id);


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.4  PERFORMANCE INDEXES                               │
-- └─────────────────────────────────────────────────────────┘

-- Clients
CREATE INDEX IF NOT EXISTS idx_clients_user_name          ON clients (user_id, ime_naziv);
CREATE INDEX IF NOT EXISTS idx_clients_name_trgm          ON clients USING GIN (ime_naziv gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_clients_active              ON clients (user_id, ime_naziv) WHERE is_archived = FALSE;

-- Cases
CREATE INDEX IF NOT EXISTS idx_cases_user_status           ON cases (user_id, status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_cases_client_date           ON cases (client_id, created_at DESC) WHERE client_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_cases_active                ON cases (user_id, created_at DESC) WHERE is_archived = FALSE;
CREATE INDEX IF NOT EXISTS idx_cases_not_deleted           ON cases (user_id, created_at DESC) WHERE is_deleted = FALSE;

-- Troskovnici
CREATE INDEX IF NOT EXISTS idx_troskovnici_user_date       ON troskovnici (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_troskovnici_client_date     ON troskovnici (client_id, created_at DESC) WHERE client_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_troskovnici_active          ON troskovnici (user_id, created_at DESC) WHERE is_archived = FALSE;

-- Case Events
CREATE INDEX IF NOT EXISTS idx_case_events_case_date       ON case_events (case_id, event_date DESC);
CREATE INDEX IF NOT EXISTS idx_case_events_user_date       ON case_events (user_id, event_date DESC);
CREATE INDEX IF NOT EXISTS idx_case_events_tenant_date     ON case_events (tenant_id, event_date DESC) WHERE tenant_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_case_events_billable        ON case_events (case_id, billing_status) WHERE is_billable = TRUE;

-- Invoices
CREATE INDEX IF NOT EXISTS idx_invoices_user_status        ON invoices (user_id, status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_invoices_user_date          ON invoices (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_invoices_active             ON invoices (user_id, created_at DESC) WHERE is_archived = FALSE;

-- Evaluations
CREATE INDEX IF NOT EXISTS idx_evaluations_user_type       ON evaluations (user_id, property_type, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_evaluations_active          ON evaluations (user_id, created_at DESC) WHERE is_archived = FALSE;

-- Contract Analyses
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_risk ON contract_analyses (user_id, overall_risk, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_contract_analyses_active    ON contract_analyses (user_id, created_at DESC) WHERE is_archived = FALSE;

-- ZK Analyses
CREATE INDEX IF NOT EXISTS idx_zk_analyses_user            ON zk_analyses (user_id, created_at DESC);

-- Valuation Audits
CREATE INDEX IF NOT EXISTS idx_valuation_audits_eval       ON valuation_audits (evaluation_id);
CREATE INDEX IF NOT EXISTS idx_valuation_audits_user       ON valuation_audits (user_id, created_at DESC);

-- Audit Logs
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_module      ON audit_logs (user_id, module, created_at DESC);

-- API Audit Logs
CREATE INDEX IF NOT EXISTS idx_api_audit_tenant_date       ON api_audit_logs (tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_api_audit_user_date         ON api_audit_logs (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_api_audit_endpoint          ON api_audit_logs (endpoint, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_api_audit_errors            ON api_audit_logs (status_code, created_at DESC) WHERE status_code >= 400;
CREATE INDEX IF NOT EXISTS idx_api_audit_created_brin      ON api_audit_logs USING BRIN (created_at);

-- Case Activities
CREATE INDEX IF NOT EXISTS idx_case_activities_case        ON case_activities (case_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_case_activities_user        ON case_activities (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_case_activities_type        ON case_activities (case_id, activity_type);

-- Documents
CREATE INDEX IF NOT EXISTS idx_documents_user              ON documents (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_documents_case              ON documents (case_id, created_at DESC) WHERE case_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_client            ON documents (client_id, created_at DESC) WHERE client_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_type              ON documents (user_id, document_type);
CREATE INDEX IF NOT EXISTS idx_documents_tags              ON documents USING GIN (tags);

-- Troskovnik Templates
CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_user   ON troskovnik_templates (user_id, updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_name   ON troskovnik_templates (user_id, name);

-- Notifications
CREATE INDEX IF NOT EXISTS idx_notifications_user_unread   ON notifications (user_id, is_read, created_at DESC) WHERE is_read = FALSE;
CREATE INDEX IF NOT EXISTS idx_notifications_user_date     ON notifications (user_id, created_at DESC);

-- Tenant Subscriptions
CREATE UNIQUE INDEX IF NOT EXISTS idx_tenant_sub_active    ON tenant_subscriptions (law_firm_id) WHERE is_active = TRUE;
CREATE INDEX IF NOT EXISTS idx_tenant_sub_firm_valid       ON tenant_subscriptions (law_firm_id, valid_until DESC);
CREATE INDEX IF NOT EXISTS idx_tenant_sub_expiry           ON tenant_subscriptions (valid_until) WHERE is_active = TRUE;

-- Tenant Usage Stats
CREATE INDEX IF NOT EXISTS idx_tenant_usage_firm_period    ON tenant_usage_stats (law_firm_id, period_start DESC);

-- Firm Usage
CREATE INDEX IF NOT EXISTS idx_firm_usage_firm             ON firm_usage (law_firm_id, module_name);

-- Law Chunks (pgvector)
CREATE INDEX IF NOT EXISTS law_chunks_embedding_idx        ON law_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);
CREATE INDEX IF NOT EXISTS law_chunks_content_fts_idx      ON law_chunks USING GIN (to_tsvector('simple', content));
CREATE INDEX IF NOT EXISTS law_chunks_tags_idx             ON law_chunks USING GIN (topic_tags);
CREATE INDEX IF NOT EXISTS law_chunks_law_idx              ON law_chunks (law);


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.5  CASE ACTIVITY TRIGGERS (auto-log case events)     │
-- └─────────────────────────────────────────────────────────┘

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


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.6  AUTO-PROVISION FREE TIER on law_firms INSERT      │
-- └─────────────────────────────────────────────────────────┘

-- Novi ured dobiva probu, ne besplatni plan (migracija 024).
-- valid_until je informativan: backend istek probe racuna kao
-- valid_from + SubscriptionPlans.TrialDays i ovaj stupac za probu ne cita.
CREATE OR REPLACE FUNCTION auto_provision_trial()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO public.tenant_subscriptions (
        law_firm_id, plan_id, status, valid_from, valid_until,
        is_active, price_eur, max_cases, max_invoices, ai_credits_limit)
    VALUES (NEW.id, 'pravotek', 'trial', now(), now() + interval '7 days',
            TRUE, 19, -1, -1, 0)
    ON CONFLICT DO NOTHING;
    RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS trg_auto_provision_free_tier ON law_firms;
DROP TRIGGER IF EXISTS trg_auto_provision_trial ON law_firms;
CREATE TRIGGER trg_auto_provision_trial
    AFTER INSERT ON law_firms
    FOR EACH ROW EXECUTE FUNCTION auto_provision_trial();


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.7  RPC FUNCTIONS                                     │
-- └─────────────────────────────────────────────────────────┘

-- ── Dashboard stats ──
CREATE OR REPLACE FUNCTION get_dashboard_stats(p_user_id UUID)
RETURNS JSON
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
DECLARE
    result JSON;
BEGIN
    SELECT json_build_object(
        'total_clients',       (SELECT COUNT(*) FROM clients WHERE user_id = p_user_id),
        'total_cases',         (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id AND is_deleted = FALSE),
        'active_cases',        (SELECT COUNT(*) FROM cases WHERE user_id = p_user_id AND status = 'aktivan' AND is_deleted = FALSE),
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
                FROM cases WHERE user_id = p_user_id AND is_deleted = FALSE
                ORDER BY created_at DESC LIMIT 5
            ) c
        )
    ) INTO result;

    RETURN result;
END;
$$;

GRANT EXECUTE ON FUNCTION get_dashboard_stats(UUID) TO authenticated;

-- ── Client search (trigram) ──
CREATE OR REPLACE FUNCTION search_clients(
    p_user_id UUID,
    p_query TEXT,
    p_limit INT DEFAULT 20
)
RETURNS TABLE (
    id UUID, ime_naziv TEXT, oib TEXT, adresa TEXT, grad TEXT,
    telefon TEXT, email TEXT, tip TEXT, created_at TIMESTAMPTZ, similarity_score REAL
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    IF p_query IS NULL OR trim(p_query) = '' THEN
        RETURN QUERY
        SELECT c.id, c.ime_naziv, c.oib, c.adresa, c.grad, c.telefon, c.email, c.tip, c.created_at, 1.0::REAL
        FROM clients c WHERE c.user_id = p_user_id
        ORDER BY c.ime_naziv LIMIT p_limit;
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
          AND (c.ime_naziv % p_query OR c.ime_naziv ILIKE '%' || p_query || '%'
               OR c.oib ILIKE '%' || p_query || '%' OR c.adresa ILIKE '%' || p_query || '%'
               OR c.grad ILIKE '%' || p_query || '%')
        ORDER BY similarity_score DESC, c.ime_naziv LIMIT p_limit;
    END IF;
END;
$$;

GRANT EXECUTE ON FUNCTION search_clients(UUID, TEXT, INT) TO authenticated;

-- ── Case events summary ──
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

-- ── Law chunks similarity search ──
CREATE OR REPLACE FUNCTION match_law_chunks(
    query_embedding vector(1536),
    match_threshold float DEFAULT 0.3,
    match_count int DEFAULT 10
)
RETURNS TABLE (
    id uuid, law text, full_name text, nn_reference text,
    article text, title text, content text, topic_tags text[], similarity float
)
LANGUAGE sql STABLE
AS $$
    SELECT lc.id, lc.law, lc.full_name, lc.nn_reference,
           lc.article, lc.title, lc.content, lc.topic_tags,
           1 - (lc.embedding <=> query_embedding) AS similarity
    FROM law_chunks lc
    WHERE lc.embedding IS NOT NULL
      AND 1 - (lc.embedding <=> query_embedding) > match_threshold
    ORDER BY lc.embedding <=> query_embedding
    LIMIT match_count;
$$;

-- ── Notifications ──
CREATE OR REPLACE FUNCTION mark_notifications_read(p_user_id UUID, p_ids UUID[])
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE updated INT;
BEGIN
    UPDATE notifications SET is_read = TRUE, read_at = now()
    WHERE user_id = p_user_id AND id = ANY(p_ids) AND is_read = FALSE;
    GET DIAGNOSTICS updated = ROW_COUNT;
    RETURN updated;
END;
$$;

GRANT EXECUTE ON FUNCTION mark_notifications_read(UUID, UUID[]) TO authenticated;

CREATE OR REPLACE FUNCTION get_unread_notification_count(p_user_id UUID)
RETURNS INT
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT COUNT(*)::INT FROM notifications WHERE user_id = p_user_id AND is_read = FALSE;
$$;

GRANT EXECUTE ON FUNCTION get_unread_notification_count(UUID) TO authenticated;

-- ── Archive toggle ──
CREATE OR REPLACE FUNCTION toggle_archive(
    p_table TEXT, p_id UUID, p_user_id UUID, p_archived BOOLEAN DEFAULT TRUE
)
RETURNS BOOLEAN
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE updated INT;
BEGIN
    IF p_table NOT IN ('troskovnici', 'cases', 'clients', 'invoices',
                        'evaluations', 'contract_analyses', 'zk_analyses', 'chronologies') THEN
        RAISE EXCEPTION 'Invalid table: %', p_table;
    END IF;
    EXECUTE format('UPDATE %I SET is_archived = $1 WHERE id = $2 AND user_id = $3', p_table)
    USING p_archived, p_id, p_user_id;
    GET DIAGNOSTICS updated = ROW_COUNT;
    RETURN updated > 0;
END;
$$;

GRANT EXECUTE ON FUNCTION toggle_archive(TEXT, UUID, UUID, BOOLEAN) TO authenticated;

-- ── Tenant subscription check ──
CREATE OR REPLACE FUNCTION is_tenant_subscription_active(p_tenant_id UUID)
RETURNS BOOLEAN
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT EXISTS (
        SELECT 1 FROM tenant_subscriptions
        WHERE law_firm_id = p_tenant_id AND is_active = TRUE AND valid_until > now()
    );
$$;

GRANT EXECUTE ON FUNCTION is_tenant_subscription_active(UUID) TO authenticated;

-- ── Expire overdue subscriptions ──
CREATE OR REPLACE FUNCTION expire_overdue_subscriptions()
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE cnt INT;
BEGIN
    UPDATE tenant_subscriptions SET is_active = FALSE, updated_at = now()
    WHERE is_active = TRUE AND valid_until <= now();
    GET DIAGNOSTICS cnt = ROW_COUNT;
    RETURN cnt;
END;
$$;

-- ── API audit log paginated query ──
CREATE OR REPLACE FUNCTION get_api_audit_logs(
    p_tenant_id UUID,
    p_from TIMESTAMPTZ DEFAULT now() - interval '30 days',
    p_to TIMESTAMPTZ DEFAULT now(),
    p_limit INT DEFAULT 100,
    p_offset INT DEFAULT 0
)
RETURNS TABLE (
    id UUID, created_at TIMESTAMPTZ, user_id UUID, action_name TEXT,
    endpoint TEXT, http_method TEXT, status_code SMALLINT, duration_ms INTEGER
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM user_profiles WHERE user_id = auth.uid() AND law_firm_id = p_tenant_id) THEN
        RAISE EXCEPTION 'Access denied: you do not belong to tenant %', p_tenant_id;
    END IF;
    RETURN QUERY
    SELECT a.id, a.created_at, a.user_id, a.action_name, a.endpoint,
           a.http_method, a.status_code, a.duration_ms
    FROM api_audit_logs a
    WHERE a.tenant_id = p_tenant_id AND a.created_at BETWEEN p_from AND p_to
    ORDER BY a.created_at DESC LIMIT p_limit OFFSET p_offset;
END;
$$;

GRANT EXECUTE ON FUNCTION get_api_audit_logs(UUID, TIMESTAMPTZ, TIMESTAMPTZ, INT, INT) TO authenticated;

-- ── API audit stats (for dashboard charts) ──
CREATE OR REPLACE FUNCTION get_api_audit_stats(p_tenant_id UUID, p_days INT DEFAULT 30)
RETURNS TABLE (
    endpoint TEXT, http_method TEXT, total_requests BIGINT,
    avg_duration_ms NUMERIC, error_count BIGINT
)
LANGUAGE plpgsql STABLE SECURITY DEFINER
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM user_profiles WHERE user_id = auth.uid() AND law_firm_id = p_tenant_id) THEN
        RAISE EXCEPTION 'Access denied: you do not belong to tenant %', p_tenant_id;
    END IF;
    RETURN QUERY
    SELECT a.endpoint, a.http_method,
           COUNT(*) AS total_requests,
           ROUND(AVG(a.duration_ms), 1) AS avg_duration_ms,
           COUNT(*) FILTER (WHERE a.status_code >= 400) AS error_count
    FROM api_audit_logs a
    WHERE a.tenant_id = p_tenant_id AND a.created_at >= now() - (p_days || ' days')::interval
    GROUP BY a.endpoint, a.http_method ORDER BY total_requests DESC;
END;
$$;

GRANT EXECUTE ON FUNCTION get_api_audit_stats(UUID, INT) TO authenticated;

-- ── Purge old API audit logs ──
CREATE OR REPLACE FUNCTION purge_old_api_audit_logs(p_retention_days INT DEFAULT 90)
RETURNS INT
LANGUAGE plpgsql SECURITY DEFINER
AS $$
DECLARE cnt INT;
BEGIN
    DELETE FROM api_audit_logs WHERE created_at < now() - (p_retention_days || ' days')::interval;
    GET DIAGNOSTICS cnt = ROW_COUNT;
    RETURN cnt;
END;
$$;


-- ┌─────────────────────────────────────────────────────────┐
-- │  3.8  MATERIALIZED VIEW — Monthly Revenue               │
-- └─────────────────────────────────────────────────────────┘

DROP MATERIALIZED VIEW IF EXISTS monthly_revenue;

CREATE MATERIALIZED VIEW monthly_revenue AS
SELECT
    user_id,
    date_trunc('month', created_at)::DATE AS month,
    COUNT(*) FILTER (WHERE source = 'troskovnik')            AS troskovnik_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'troskovnik'), 0) AS troskovnik_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'troskovnik'), 0)   AS troskovnik_eur_s_pdv,
    COUNT(*) FILTER (WHERE source = 'invoice')               AS invoice_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'invoice'), 0)    AS invoice_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'invoice'), 0)      AS invoice_eur_s_pdv
FROM (
    SELECT user_id, created_at, 'troskovnik' AS source,
           ukupno_eur_bez_pdv AS amount_bez_pdv, ukupno_eur_s_pdv AS amount_s_pdv
    FROM troskovnici
    UNION ALL
    SELECT user_id, created_at, 'invoice' AS source,
           net_amount AS amount_bez_pdv, gross_amount AS amount_s_pdv
    FROM invoices WHERE status != 'CANCELLED'
) combined
GROUP BY user_id, date_trunc('month', created_at)::DATE
ORDER BY month DESC;

CREATE UNIQUE INDEX IF NOT EXISTS idx_monthly_revenue_user_month ON monthly_revenue (user_id, month);
CREATE INDEX IF NOT EXISTS idx_monthly_revenue_user ON monthly_revenue (user_id);

CREATE OR REPLACE FUNCTION get_monthly_revenue(p_user_id UUID, p_months INT DEFAULT 12)
RETURNS TABLE (
    month DATE, troskovnik_count BIGINT, troskovnik_eur_bez_pdv NUMERIC,
    troskovnik_eur_s_pdv NUMERIC, invoice_count BIGINT,
    invoice_eur_bez_pdv NUMERIC, invoice_eur_s_pdv NUMERIC
)
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT mr.month, mr.troskovnik_count, mr.troskovnik_eur_bez_pdv, mr.troskovnik_eur_s_pdv,
           mr.invoice_count, mr.invoice_eur_bez_pdv, mr.invoice_eur_s_pdv
    FROM monthly_revenue mr WHERE mr.user_id = p_user_id
    ORDER BY mr.month DESC LIMIT p_months;
$$;

GRANT EXECUTE ON FUNCTION get_monthly_revenue(UUID, INT) TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════════
-- DONE! Unified schema complete.
-- ═══════════════════════════════════════════════════════════════════════════
--
-- Cron jobs (set up in Supabase Dashboard → Database → Extensions → pg_cron):
--
--   SELECT cron.schedule('refresh-monthly-revenue', '0 2 * * *',
--       'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue');
--
--   SELECT cron.schedule('expire-subscriptions', '0 1 * * *',
--       $$SELECT expire_overdue_subscriptions()$$);
--
--   SELECT cron.schedule('purge-api-audit-logs', '0 3 * * 0',
--       $$SELECT purge_old_api_audit_logs(90)$$);
--
-- Storage bucket (Dashboard → Storage → New Bucket):
--   Name: documents | Public: false | Limit: 50 MB
-- ═══════════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/001_law_chunks_pgvector.sql ═══════════
-- ========================================
-- V69: Semantic RAG — pgvector law chunks table
-- Run this in Supabase SQL Editor
-- Prerequisites: Supabase project with pgvector extension
-- ========================================

-- Enable pgvector extension (available on all Supabase plans)
CREATE EXTENSION IF NOT EXISTS vector;

-- Law chunks table — stores individual law articles with embeddings
CREATE TABLE IF NOT EXISTS law_chunks (
    id                  UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    law                 TEXT            NOT NULL,           -- 'ZOO', 'ZV', 'ZZK'
    full_name           TEXT            NOT NULL,           -- 'Zakon o obveznim odnosima'
    nn_reference        TEXT            NOT NULL,           -- 'NN 35/05, ...'
    article             TEXT            NOT NULL,           -- '303', '41', etc.
    title               TEXT            NOT NULL,           -- 'Kapara', 'Clausula intabulandi'
    content             TEXT            NOT NULL,           -- Full article text
    topic_tags          TEXT[]          NOT NULL DEFAULT '{}', -- ['kapara', 'plaćanje']
    embedding           vector(1536),                      -- OpenAI text-embedding-3-small
    UNIQUE(law, article)                                   -- One entry per law+article
);

-- Vector similarity search index (IVFFlat — good for <100K rows)
-- Use lists=10 for small datasets (<1000 rows)
CREATE INDEX IF NOT EXISTS law_chunks_embedding_idx 
    ON law_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

-- Full-text search index (fallback when embeddings unavailable)
CREATE INDEX IF NOT EXISTS law_chunks_content_fts_idx 
    ON law_chunks USING GIN (to_tsvector('simple', content));

-- Tag-based filtering index
CREATE INDEX IF NOT EXISTS law_chunks_tags_idx 
    ON law_chunks USING GIN (topic_tags);

-- Fast lookup by law abbreviation
CREATE INDEX IF NOT EXISTS law_chunks_law_idx ON law_chunks (law);

-- ========================================
-- RPC function: similarity search
-- Called from backend via POST /rest/v1/rpc/match_law_chunks
-- ========================================

CREATE OR REPLACE FUNCTION match_law_chunks(
    query_embedding vector(1536),
    match_threshold float DEFAULT 0.3,
    match_count int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
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

-- ========================================
-- RPC function: full-text search (fallback)
-- Used when embeddings are not available
-- ========================================

CREATE OR REPLACE FUNCTION search_law_chunks_text(
    search_query text,
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
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

-- ========================================
-- RPC function: tag-based search
-- Find articles by topic tags
-- ========================================

CREATE OR REPLACE FUNCTION search_law_chunks_by_tags(
    search_tags text[],
    max_results int DEFAULT 10
)
RETURNS TABLE (
    id uuid,
    law text,
    full_name text,
    nn_reference text,
    article text,
    title text,
    content text,
    topic_tags text[],
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

-- ========================================
-- Row Level Security
-- ========================================

ALTER TABLE law_chunks ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON law_chunks;
CREATE POLICY "service_role_full_access" ON law_chunks
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "anon_select" ON law_chunks;
CREATE POLICY "anon_select" ON law_chunks
    FOR SELECT USING (true);

-- ═══════════ migrations/003_consolidated_troskovnici_columns.sql ═══════════
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

-- ── Naknada troškova (Tbr. 48.-51.) i predujam (from 021 migration) ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS naknada_troskova_ukupno NUMERIC(12,2) NOT NULL DEFAULT 0;
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS predujmovi_ukupno NUMERIC(12,2) NOT NULL DEFAULT 0;

-- ── Helpful comments ──
COMMENT ON TABLE troskovnici IS 'Odvjetnički troškovnici — persisted fee calculations';
COMMENT ON COLUMN troskovnici.odvjetnik IS 'JSONB: {naziv, oib, adresa, iban}';
COMMENT ON COLUMN troskovnici.predmet IS 'JSONB: {sud, poslovni_broj}';
COMMENT ON COLUMN troskovnici.stranke IS 'JSONB: {zastupana_stranka: {...}, protustranka: {...}}';

-- ═══════════ migrations/004_auto_updated_at_triggers.sql ═══════════
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

-- ═══════════ migrations/005_performance_composite_indexes.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 005: Performance composite indexes
-- Adds indexes for the most common query patterns:
--   • User + date (listing pages with sort)
--   • Client + date (client profile views)
--   • Status filters
--   • Full-text search on client names
-- Safe to run multiple times (IF NOT EXISTS).
-- ═══════════════════════════════════════════════════════════════════

-- ── Troskovnici: user listing sorted by date ──
CREATE INDEX IF NOT EXISTS idx_troskovnici_user_date
    ON troskovnici (user_id, created_at DESC);

-- ── Troskovnici: client + date for profile view ──
CREATE INDEX IF NOT EXISTS idx_troskovnici_client_date
    ON troskovnici (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- ── Cases: user + status for active-cases dashboard ──
CREATE INDEX IF NOT EXISTS idx_cases_user_status
    ON cases (user_id, status, created_at DESC);

-- ── Cases: client + date for client profile ──
CREATE INDEX IF NOT EXISTS idx_cases_client_date
    ON cases (client_id, created_at DESC)
    WHERE client_id IS NOT NULL;

-- ── Clients: full-text search on name (trigram) ──
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_clients_name_trgm
    ON clients USING GIN (ime_naziv gin_trgm_ops);

-- ── Clients: user + name for alphabetical list ──
CREATE INDEX IF NOT EXISTS idx_clients_user_name
    ON clients (user_id, ime_naziv);

-- ── Invoices: user + status for invoice listing ──
CREATE INDEX IF NOT EXISTS idx_invoices_user_status
    ON invoices (user_id, status, created_at DESC);

-- ── Invoices: user + date ──
CREATE INDEX IF NOT EXISTS idx_invoices_user_date
    ON invoices (user_id, created_at DESC);

-- ── Evaluations: user + property type for filtering ──
CREATE INDEX IF NOT EXISTS idx_evaluations_user_type
    ON evaluations (user_id, property_type, created_at DESC);

-- ── Audit logs: user + module for filtered audit trail ──
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_module
    ON audit_logs (user_id, module, created_at DESC);

-- ── Contract analyses: user + risk for dashboard ──
CREATE INDEX IF NOT EXISTS idx_contract_analyses_user_risk
    ON contract_analyses (user_id, overall_risk, created_at DESC);

-- ═══════════ migrations/006_dashboard_stats_rpc.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 006: Dashboard stats RPC function
-- Returns a single JSON object with counts and totals for the
-- user's dashboard: # clients, # cases, # troškovnici, $ sum, etc.
-- Called by frontend: supabase.rpc('get_dashboard_stats')
-- ═══════════════════════════════════════════════════════════════════

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

-- Grant execute to authenticated users
GRANT EXECUTE ON FUNCTION get_dashboard_stats(UUID) TO authenticated;

-- ═══════════ migrations/007_client_search_rpc.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 007: Client search RPC function
-- Fuzzy search on clients by name, OIB, or address.
-- Uses pg_trgm for similarity matching + ILIKE fallback.
-- Called by frontend: supabase.rpc('search_clients', { ... })
-- ═══════════════════════════════════════════════════════════════════

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
        -- No search term → return all for user, sorted by name
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
              c.ime_naziv % p_query                        -- trigram similarity
              OR c.ime_naziv ILIKE '%' || p_query || '%'   -- substring match
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

-- ═══════════ migrations/008_case_activity_log.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 008: Case activity log
-- Tracks events on cases: creation, status changes, linked documents,
-- troškovnik attachments, notes, etc.
-- Provides a timeline view per case in the frontend.
-- ═══════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS case_activities (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    case_id         UUID            NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    activity_type   TEXT            NOT NULL DEFAULT 'note',
        -- Allowed values:
        -- 'created'          — case was created
        -- 'status_change'    — status changed (e.g. aktivan → zatvoren)
        -- 'note'             — user added a free-text note
        -- 'troskovnik'       — troškovnik linked/created for this case
        -- 'evaluation'       — valuation linked
        -- 'contract'         — contract analysis linked
        -- 'zk_analysis'      — ZK analysis linked
        -- 'invoice'          — invoice generated
        -- 'document'         — document uploaded
    title           TEXT            NOT NULL DEFAULT '',    -- Short summary, e.g. "Status → zatvoren"
    description     TEXT,                                  -- Longer details / note body
    metadata        JSONB           DEFAULT '{}'::jsonb,   -- Flexible: {old_status, new_status, linked_id, ...}
    linked_id       UUID                                   -- Optional FK to the linked record
);

CREATE INDEX IF NOT EXISTS idx_case_activities_case
    ON case_activities (case_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_user
    ON case_activities (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_case_activities_type
    ON case_activities (case_id, activity_type);

-- RLS
ALTER TABLE case_activities ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON case_activities;
CREATE POLICY "service_role_full_access" ON case_activities
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_case_activities" ON case_activities;
CREATE POLICY "users_own_case_activities" ON case_activities
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- ── Auto-create "created" activity when a case is inserted ──
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

-- ── Auto-log status changes ──
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

-- ═══════════ migrations/009_document_attachments.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 009: Document attachments
-- Stores references to uploaded documents (PDFs, images, etc.)
-- linked to cases, clients, or specific analyses.
-- Files stored in Supabase Storage; this table holds metadata.
-- ═══════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS documents (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    -- Linkage (all optional — document can be standalone)
    client_id       UUID            REFERENCES clients(id) ON DELETE SET NULL,
    case_id         UUID            REFERENCES cases(id) ON DELETE SET NULL,
    analysis_id     UUID,           -- Generic FK to any analysis table
    analysis_type   TEXT,           -- 'evaluation', 'contract', 'zk', 'troskovnik', 'chronology', 'invoice'
    -- File metadata
    file_name       TEXT            NOT NULL DEFAULT '',       -- Original file name
    file_type       TEXT            NOT NULL DEFAULT '',       -- MIME type
    file_size_bytes BIGINT          DEFAULT 0,                 -- File size
    storage_path    TEXT            NOT NULL DEFAULT '',       -- Supabase Storage path (bucket/key)
    public_url      TEXT,                                      -- Public URL if available
    -- Classification
    document_type   TEXT            NOT NULL DEFAULT 'ostalo',
        -- 'ugovor', 'zk_izvadak', 'presuda', 'rjesenje', 'racun',
        -- 'troskovnik', 'podnesak', 'dopis', 'punomoć', 'ostalo'
    description     TEXT,                                      -- User note
    tags            TEXT[]          DEFAULT '{}'               -- Flexible tagging
);

-- Indexes
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

-- RLS
ALTER TABLE documents ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON documents;
CREATE POLICY "service_role_full_access" ON documents
    FOR ALL USING (auth.role() = 'service_role');

DROP POLICY IF EXISTS "users_own_documents" ON documents;
CREATE POLICY "users_own_documents" ON documents
    FOR ALL USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- Auto-update trigger
DROP TRIGGER IF EXISTS trg_documents_updated_at ON documents;
CREATE TRIGGER trg_documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ═══════════════════════════════════════════════════════════════════
-- Supabase Storage bucket for documents
-- Run this via Supabase Dashboard → Storage → New Bucket:
--   Name: documents
--   Public: false (private — access via signed URLs)
--   File size limit: 50 MB
--   Allowed MIME types: application/pdf, image/png, image/jpeg, image/webp,
--                       application/msword, application/vnd.openxmlformats-officedocument.wordprocessingml.document
-- ═══════════════════════════════════════════════════════════════════

-- ═══════════ migrations/010_troskovnik_templates.sql ═══════════
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

-- ═══════════ migrations/011_monthly_revenue_view.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 011: Monthly revenue reporting view
-- Materialized view for fast dashboard reporting.
-- Aggregates troškovnici + invoices per month per user.
-- Refresh: REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue;
-- ═══════════════════════════════════════════════════════════════════

-- Drop if exists to allow re-running
DROP MATERIALIZED VIEW IF EXISTS monthly_revenue;

CREATE MATERIALIZED VIEW monthly_revenue AS
SELECT
    user_id,
    date_trunc('month', created_at)::DATE AS month,
    -- Troškovnici stats
    COUNT(*) FILTER (WHERE source = 'troskovnik')           AS troskovnik_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'troskovnik'), 0) AS troskovnik_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'troskovnik'), 0)   AS troskovnik_eur_s_pdv,
    -- Invoice stats
    COUNT(*) FILTER (WHERE source = 'invoice')              AS invoice_count,
    COALESCE(SUM(amount_bez_pdv) FILTER (WHERE source = 'invoice'), 0)    AS invoice_eur_bez_pdv,
    COALESCE(SUM(amount_s_pdv) FILTER (WHERE source = 'invoice'), 0)      AS invoice_eur_s_pdv
FROM (
    -- Troškovnici
    SELECT user_id, created_at, 'troskovnik' AS source,
           ukupno_eur_bez_pdv AS amount_bez_pdv,
           ukupno_eur_s_pdv AS amount_s_pdv
    FROM troskovnici
    UNION ALL
    -- Invoices (exclude cancelled)
    SELECT user_id, created_at, 'invoice' AS source,
           net_amount AS amount_bez_pdv,
           gross_amount AS amount_s_pdv
    FROM invoices
    WHERE status != 'CANCELLED'
) combined
GROUP BY user_id, date_trunc('month', created_at)::DATE
ORDER BY month DESC;

-- Unique index needed for CONCURRENTLY refresh
CREATE UNIQUE INDEX IF NOT EXISTS idx_monthly_revenue_user_month
    ON monthly_revenue (user_id, month);

-- Index for quick user lookups
CREATE INDEX IF NOT EXISTS idx_monthly_revenue_user
    ON monthly_revenue (user_id);

-- ── RPC to get monthly revenue for a user ──
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

-- ═══════════════════════════════════════════════════════════════════
-- NOTE: To keep the view fresh, set up a Supabase cron job:
--   SELECT cron.schedule(
--       'refresh-monthly-revenue',
--       '0 2 * * *',    -- daily at 2 AM
--       'REFRESH MATERIALIZED VIEW CONCURRENTLY monthly_revenue'
--   );
-- Or call REFRESH manually after bulk inserts.
-- ═══════════════════════════════════════════════════════════════════

-- ═══════════ migrations/012_data_validation_constraints.sql ═══════════
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

-- ═══════════ migrations/013_notifications.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 013: Notification preferences + system notifications
-- user_notification_prefs: per-user toggles for email/push alerts.
-- notifications: in-app notification queue (bell icon in frontend).
-- ═══════════════════════════════════════════════════════════════════

-- ── Notification preferences ──
CREATE TABLE IF NOT EXISTS user_notification_prefs (
    user_id             UUID            PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    updated_at          TIMESTAMPTZ     DEFAULT now() NOT NULL,
    email_enabled       BOOLEAN         DEFAULT TRUE,      -- Receive emails at all
    email_case_updates  BOOLEAN         DEFAULT TRUE,      -- Case status changes
    email_invoice_reminders BOOLEAN     DEFAULT TRUE,      -- Invoice due date reminders
    email_credit_low    BOOLEAN         DEFAULT TRUE,      -- Credit quota running low
    email_weekly_summary BOOLEAN        DEFAULT FALSE,     -- Weekly activity digest
    push_enabled        BOOLEAN         DEFAULT FALSE,     -- Browser push (future)
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

-- ── In-app notifications ──
CREATE TABLE IF NOT EXISTS notifications (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now() NOT NULL,
    user_id         UUID            NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    type            TEXT            NOT NULL DEFAULT 'info',
        -- 'info', 'warning', 'success', 'error',
        -- 'case_update', 'invoice_due', 'credit_low', 'system'
    title           TEXT            NOT NULL DEFAULT '',
    body            TEXT,
    link            TEXT,                                  -- Frontend route to navigate to
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

-- ── RPC: Mark notifications as read ──
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

-- ── RPC: Get unread notification count ──
CREATE OR REPLACE FUNCTION get_unread_notification_count(p_user_id UUID)
RETURNS INT
LANGUAGE sql STABLE SECURITY DEFINER
AS $$
    SELECT COUNT(*)::INT FROM notifications
    WHERE user_id = p_user_id AND is_read = FALSE;
$$;

GRANT EXECUTE ON FUNCTION get_unread_notification_count(UUID) TO authenticated;

-- ═══════════ migrations/014_soft_delete_archive.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION 014: Soft delete + archive pattern
-- Adds is_archived flag to main data tables so records can be
-- "deleted" from the UI without actually destroying data.
-- Archived records are hidden by default via partial indexes.
-- ═══════════════════════════════════════════════════════════════════

-- ── Add is_archived column ──
ALTER TABLE troskovnici ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE cases ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE evaluations ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE contract_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE zk_analyses ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;
ALTER TABLE chronologies ADD COLUMN IF NOT EXISTS is_archived BOOLEAN DEFAULT FALSE;

-- ── Partial indexes: only non-archived rows for default queries ──
-- These let "WHERE is_archived = FALSE" queries use the index efficiently.

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

-- ── RPC: Archive/unarchive a record ──
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
    -- Validate table name to prevent SQL injection
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

-- ═══════════════════════════════════════════════════════════════════
-- NOTE: The backend should add "is_archived=eq.false" to SELECT
-- queries by default. Example PostgREST filter:
--   GET /rest/v1/troskovnici?user_id=eq.{uid}&is_archived=eq.false
-- The UI can show a "Prikaži arhivirane" toggle to include them.
-- ═══════════════════════════════════════════════════════════════════

-- ═══════════ migrations/015_firm_scoping.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 015 — Prelazak s vlasništva po korisniku na vlasništvo po uredu
--
-- Do sada je svaki zapis pripadao jednom korisniku (`user_id`), pa kolege iz
-- istog odvjetničkog ureda nisu mogli dijeliti predmete. Ova migracija dodaje
-- `law_firm_id` na sve poslovne tablice, popunjava ga i priprema teren za
-- RLS politike po uredu (migracija 016).
--
-- `user_id` kolone OSTAJU — one i dalje govore tko je zapis stvorio.
-- Vlasništvo ide na ured, autorstvo ostaje na osobi.
--
-- Sigurna za višekratno pokretanje (IF NOT EXISTS / DO blokovi).
-- Pokrenuti PRIJE migracije 016.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Svaki profil mora imati ured
-- Bez ureda korisnikovi podaci nestaju iza RLS-a u migraciji 016.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    p RECORD;
    new_firm_id UUID;
BEGIN
    FOR p IN SELECT user_id, full_name, email FROM user_profiles WHERE law_firm_id IS NULL
    LOOP
        INSERT INTO law_firms (name)
        VALUES (COALESCE(NULLIF(p.full_name, ''), p.email, 'Moj ured'))
        RETURNING id INTO new_firm_id;

        UPDATE user_profiles SET law_firm_id = new_firm_id WHERE user_id = p.user_id;

        RAISE NOTICE 'Kreiran ured % za korisnika %', new_firm_id, p.user_id;
    END LOOP;
END $$;

-- Profili za korisnike koji ih uopće nemaju (npr. Google prijava prije trigera
-- iz koraka 8 — taj put nikad nije kreirao profil).
DO $$
DECLARE
    u RECORD;
    new_firm_id UUID;
BEGIN
    FOR u IN
        SELECT au.id, au.email, au.raw_user_meta_data
        FROM auth.users au
        LEFT JOIN user_profiles up ON up.user_id = au.id
        WHERE up.user_id IS NULL
    LOOP
        INSERT INTO law_firms (name)
        VALUES (COALESCE(u.raw_user_meta_data->>'firm_name', u.email, 'Moj ured'))
        RETURNING id INTO new_firm_id;

        INSERT INTO user_profiles (user_id, law_firm_id, full_name, email, role)
        VALUES (
            u.id,
            new_firm_id,
            COALESCE(u.raw_user_meta_data->>'full_name', u.email, ''),
            u.email,
            'owner'
        );

        RAISE NOTICE 'Kreiran profil i ured za korisnika bez profila: %', u.id;
    END LOOP;
END $$;

ALTER TABLE user_profiles ALTER COLUMN law_firm_id SET NOT NULL;

-- Ured se više ne smije obrisati ispod profila (bilo je ON DELETE SET NULL,
-- što bi korisniku oduzelo pristup vlastitim podacima).
-- Postojeći FK tražimo po stupcu, ne po pretpostavljenom imenu — inače bismo
-- na bazi s drugačije imenovanim ograničenjem dodali drugi FK pokraj starog.
DO $$
DECLARE
    fk_name TEXT;
BEGIN
    SELECT con.conname INTO fk_name
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace ns ON ns.oid = rel.relnamespace
    WHERE ns.nspname = 'public'
      AND rel.relname = 'user_profiles'
      AND con.contype = 'f'
      AND con.conkey = ARRAY[
          (SELECT attnum FROM pg_attribute
           WHERE attrelid = rel.oid AND attname = 'law_firm_id')
      ];

    IF fk_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE user_profiles DROP CONSTRAINT %I;', fk_name);
    END IF;

    ALTER TABLE user_profiles ADD CONSTRAINT user_profiles_law_firm_id_fkey
        FOREIGN KEY (law_firm_id) REFERENCES law_firms(id) ON DELETE RESTRICT;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Uloge unutar ureda
-- Shema je koristila 'admin' kao default; dokument definira četiri uloge.
-- ────────────────────────────────────────────────────────────

UPDATE user_profiles SET role = 'owner'  WHERE role IN ('admin', '');
UPDATE user_profiles SET role = 'lawyer' WHERE role NOT IN ('owner', 'lawyer', 'paralegal', 'readonly');

ALTER TABLE user_profiles ALTER COLUMN role SET DEFAULT 'lawyer';

DO $$ BEGIN
    ALTER TABLE user_profiles DROP CONSTRAINT IF EXISTS chk_user_profiles_role;
    ALTER TABLE user_profiles ADD CONSTRAINT chk_user_profiles_role
        CHECK (role IN ('owner', 'lawyer', 'paralegal', 'readonly'));
END $$;

-- Svaki ured mora imati barem jednog vlasnika — inače nitko ne može mijenjati
-- postavke ureda ni čitati dnevnik.
DO $$
DECLARE
    f RECORD;
BEGIN
    FOR f IN
        SELECT DISTINCT up.law_firm_id
        FROM user_profiles up
        WHERE NOT EXISTS (
            SELECT 1 FROM user_profiles o
            WHERE o.law_firm_id = up.law_firm_id AND o.role = 'owner'
        )
    LOOP
        UPDATE user_profiles
        SET role = 'owner'
        WHERE user_id = (
            SELECT user_id FROM user_profiles
            WHERE law_firm_id = f.law_firm_id
            ORDER BY created_at
            LIMIT 1
        );
        RAISE NOTICE 'Uredu % dodijeljen vlasnik (najstariji član)', f.law_firm_id;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: law_firm_id na sve poslovne tablice
--
-- Namjerna denormalizacija: `documents` i `case_events` bi se mogli izvesti
-- preko `case_id`, ali kolona na svakoj tablici čini RLS pravilo iz migracije
-- 016 jednom usporedbom umjesto podupitom.
--
-- IZUZETAK: `notifications` i `user_notification_prefs` NEMAJU law_firm_id i
-- ostaju vezani na osobu. Obavijest je upućena čovjeku, ne uredu — da smo ih
-- opsegli po uredu, kolege bi čitali tuđe obavijesti.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    t TEXT;
    firm_tables TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'valuation_audits', 'audit_logs', 'case_activities',
        'documents', 'troskovnik_templates'
    ];
BEGIN
    FOREACH t IN ARRAY firm_tables
    LOOP
        EXECUTE format(
            'ALTER TABLE %I ADD COLUMN IF NOT EXISTS law_firm_id UUID '
            'REFERENCES law_firms(id) ON DELETE CASCADE;', t
        );
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 4: Popunjavanje iz user_profiles
-- ────────────────────────────────────────────────────────────

-- case_events već ima tenant_id → law_firms; njega uzimamo kao primarni izvor.
UPDATE case_events
SET law_firm_id = tenant_id
WHERE law_firm_id IS NULL AND tenant_id IS NOT NULL;

DO $$
DECLARE
    t TEXT;
    firm_tables TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'valuation_audits', 'audit_logs', 'case_activities',
        'documents', 'troskovnik_templates'
    ];
    affected BIGINT;
BEGIN
    FOREACH t IN ARRAY firm_tables
    LOOP
        EXECUTE format(
            'UPDATE %I x SET law_firm_id = up.law_firm_id '
            'FROM user_profiles up '
            'WHERE up.user_id = x.user_id AND x.law_firm_id IS NULL;', t
        );
        GET DIAGNOSTICS affected = ROW_COUNT;
        IF affected > 0 THEN
            RAISE NOTICE 'Popunjeno % redova u tablici %', affected, t;
        END IF;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 5: NOT NULL — ali samo gdje je popunjavanje uspjelo
--
-- `valuation_audits` i `audit_logs` imaju nullable user_id (ON DELETE SET NULL),
-- pa im se ured ne može uvijek izvesti. Umjesto da migracija pukne, takve
-- tablice ostaju nullable uz jasnu poruku — takvi redovi su nakon 016 nevidljivi
-- korisnicima, a i dalje dostupni preko service_role ključa.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    t TEXT;
    firm_tables TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'valuation_audits', 'audit_logs', 'case_activities',
        'documents', 'troskovnik_templates'
    ];
    orphans BIGINT;
BEGIN
    FOREACH t IN ARRAY firm_tables
    LOOP
        EXECUTE format('SELECT count(*) FROM %I WHERE law_firm_id IS NULL;', t)
            INTO orphans;

        IF orphans = 0 THEN
            EXECUTE format('ALTER TABLE %I ALTER COLUMN law_firm_id SET NOT NULL;', t);
        ELSE
            RAISE WARNING
                'Tablica % ima % redova bez law_firm_id — NOT NULL preskočen. '
                'Ti redovi nakon migracije 016 nisu vidljivi korisnicima.',
                t, orphans;
        END IF;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 6: Indeksi
-- RLS filtrira po law_firm_id na svakom upitu, pa je ovo vruć put.
-- ────────────────────────────────────────────────────────────

CREATE INDEX IF NOT EXISTS idx_clients_firm             ON clients (law_firm_id, ime_naziv);
CREATE INDEX IF NOT EXISTS idx_cases_firm_status        ON cases (law_firm_id, status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_troskovnici_firm_date    ON troskovnici (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_case_events_firm_date    ON case_events (law_firm_id, event_date DESC);
CREATE INDEX IF NOT EXISTS idx_invoices_firm_date       ON invoices (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_evaluations_firm_date    ON evaluations (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_contract_analyses_firm   ON contract_analyses (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_chronologies_firm        ON chronologies (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_legal_strategies_firm    ON legal_strategies (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_zk_analyses_firm         ON zk_analyses (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_valuation_audits_firm    ON valuation_audits (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_logs_firm          ON audit_logs (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_case_activities_firm     ON case_activities (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_documents_firm           ON documents (law_firm_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_troskovnik_templates_firm ON troskovnik_templates (law_firm_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_user_profiles_firm       ON user_profiles (law_firm_id);


-- ────────────────────────────────────────────────────────────
-- KORAK 7: Helper koji RLS politike koriste
--
-- SECURITY DEFINER je nužan: politika na user_profiles koja bi čitala
-- user_profiles ušla bi u beskonačnu rekurziju. STABLE dopušta planeru da
-- funkciju pozove jednom po upitu umjesto po retku.
-- ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.user_firm_id()
RETURNS UUID
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT law_firm_id FROM public.user_profiles WHERE user_id = auth.uid()
$$;

CREATE OR REPLACE FUNCTION public.user_firm_role()
RETURNS TEXT
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT role FROM public.user_profiles WHERE user_id = auth.uid()
$$;

GRANT EXECUTE ON FUNCTION public.user_firm_id()   TO authenticated;
GRANT EXECUTE ON FUNCTION public.user_firm_role() TO authenticated;


-- ────────────────────────────────────────────────────────────
-- KORAK 8: Pozivnice u ured
--
-- Trigger iz koraka 9 mora znati pripada li novi korisnik nekom postojećem
-- uredu. Predložak u dokumentu to čita iz `raw_user_meta_data->>'firm_id'` —
-- a to je polje koje klijent slobodno postavlja pri registraciji. Tko zna
-- tuđi firm_id, ušao bi u tuđi ured i vidio sve njihove predmete.
--
-- Zato pripadnost dolazi iz pozivnice vezane na EMAIL, koju je netko iz ureda
-- morao ranije kreirati. Metadata se ne koristi za pripadnost.
-- ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS firm_invites (
    id              UUID            DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at      TIMESTAMPTZ     DEFAULT now()   NOT NULL,
    law_firm_id     UUID            NOT NULL REFERENCES law_firms(id) ON DELETE CASCADE,
    email           TEXT            NOT NULL,
    role            TEXT            NOT NULL DEFAULT 'lawyer',
    invited_by      UUID            REFERENCES auth.users(id) ON DELETE SET NULL,
    expires_at      TIMESTAMPTZ     NOT NULL DEFAULT (now() + interval '7 days'),
    accepted_at     TIMESTAMPTZ,
    accepted_by     UUID            REFERENCES auth.users(id) ON DELETE SET NULL
);

DO $$ BEGIN
    ALTER TABLE firm_invites ADD CONSTRAINT chk_firm_invites_role
        CHECK (role IN ('lawyer', 'paralegal', 'readonly'));
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Jedna otvorena pozivnica po emailu i uredu.
CREATE UNIQUE INDEX IF NOT EXISTS idx_firm_invites_open
    ON firm_invites (law_firm_id, lower(email))
    WHERE accepted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_firm_invites_email
    ON firm_invites (lower(email))
    WHERE accepted_at IS NULL;

ALTER TABLE firm_invites ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON firm_invites;
CREATE POLICY "service_role_full_access" ON firm_invites
    FOR ALL USING (auth.role() = 'service_role');


-- ────────────────────────────────────────────────────────────
-- KORAK 9: Automatsko kreiranje profila pri registraciji
--
-- Do sada je profil i ured stvarao frontend pozivom na backend
-- (`POST /api/settings/register-firm`) po principu "pošalji i zaboravi".
-- Ako bi korisnik zatvorio karticu, ostao bi red u auth.users bez profila —
-- a Google prijava taj poziv nikad nije ni izvršila.
--
-- Sada to radi baza, na istom mjestu za sve načine prijave.
-- ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    invite      RECORD;
    target_firm UUID;
    target_role TEXT;
BEGIN
    -- Postoji li otvorena pozivnica za ovaj email?
    SELECT fi.id, fi.law_firm_id, fi.role
    INTO invite
    FROM public.firm_invites fi
    WHERE lower(fi.email) = lower(NEW.email)
      AND fi.accepted_at IS NULL
      AND fi.expires_at > now()
    ORDER BY fi.created_at DESC
    LIMIT 1;

    IF FOUND THEN
        target_firm := invite.law_firm_id;
        target_role := invite.role;

        UPDATE public.firm_invites
        SET accepted_at = now(), accepted_by = NEW.id
        WHERE id = invite.id;
    ELSE
        -- Nema pozivnice → korisnik otvara vlastiti ured i vlasnik je.
        -- Naziv i OIB su podaci o vlastitom uredu, pa ih je sigurno uzeti iz
        -- metadata — za razliku od firm_id, koji bi bio tuđi ured.
        INSERT INTO public.law_firms (name, oib)
        VALUES (
            COALESCE(
                NULLIF(NEW.raw_user_meta_data->>'firm_name', ''),
                NEW.email,
                'Moj ured'
            ),
            NULLIF(NEW.raw_user_meta_data->>'firm_oib', '')
        )
        RETURNING id INTO target_firm;

        target_role := 'owner';
    END IF;

    INSERT INTO public.user_profiles (user_id, law_firm_id, full_name, email, role)
    VALUES (
        NEW.id,
        target_firm,
        COALESCE(NULLIF(NEW.raw_user_meta_data->>'full_name', ''), NEW.email, ''),
        NEW.email,
        target_role
    )
    ON CONFLICT (user_id) DO NOTHING;

    RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Nijedan red bez ureda (osim upozorenja iz koraka 5):
--   SELECT 'cases' AS t, count(*) FROM cases WHERE law_firm_id IS NULL
--   UNION ALL SELECT 'clients', count(*) FROM clients WHERE law_firm_id IS NULL
--   UNION ALL SELECT 'troskovnici', count(*) FROM troskovnici WHERE law_firm_id IS NULL;
--
--   -- Svaki korisnik ima profil, ured i valjanu ulogu:
--   SELECT au.email, up.law_firm_id, up.role
--   FROM auth.users au LEFT JOIN user_profiles up ON up.user_id = au.id;
--
--   -- Trigger postoji:
--   SELECT tgname FROM pg_trigger WHERE tgname = 'on_auth_user_created';
--
-- SLJEDEĆE: migracija 016 (RLS politike po uredu).
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/016_rls_firm_policies.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 016 — RLS politike po uredu
--
-- Zamjenjuje politike `auth.uid() = user_id` politikama nad `law_firm_id`.
-- Od ovog trenutka izolacija podataka je pravilo u Postgresu koje se izvršava
-- na svakom upitu — iz weba, iz backenda s korisnikovim tokenom, iz Postmana.
--
-- ZAHTIJEVA migraciju 015 (kolone law_firm_id + user_firm_id() helper).
--
-- Dvije napomene o obliku politika:
--
-- 1. Politike su razlomljene po naredbi (SELECT / INSERT / UPDATE / DELETE)
--    umjesto jedne FOR ALL. Postoji razlog: više permisivnih politika Postgres
--    spaja s OR. Da smo "svi članovi smiju čitati" i "samo ne-readonly smiju
--    pisati" napisali kao dvije FOR ALL politike, druga bi proširila prvu
--    umjesto da je suzi, i `readonly` bi mogao pisati.
--
-- 2. Svaka politika ima `TO authenticated`. Bez toga vrijedi i za `anon`, pa bi
--    neprijavljeni korisnik s javnim anon ključem prošao kroz `user_firm_id()`
--    koji vraća NULL — što danas ne propušta ništa, ali se oslanja na sretnu
--    okolnost umjesto na pravilo.
--
-- Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Uklanjanje starih politika po korisniku
--
-- Imena dolaze iz petlje u unified_schema.sql i iz
-- migration_security_hardening.sql — oba su generirala "users_own_<tablica>".
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    t TEXT;
    all_tables TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'valuation_audits', 'audit_logs', 'case_activities',
        'documents', 'troskovnik_templates', 'notifications',
        'user_profiles', 'user_notification_prefs'
    ];
BEGIN
    FOREACH t IN ARRAY all_tables
    LOOP
        -- %s, ne %I: %I je quote_ident, koji bi unutar već navodnicima
        -- omeđenog imena politike dodao svoje navodnike. Za ova imena tablica
        -- ishod je isti, ali ne oslanjamo se na to.
        EXECUTE format('DROP POLICY IF EXISTS "users_own_%s" ON %I;', t, t);
    END LOOP;
END $$;

DROP POLICY IF EXISTS "users_select_own_audit_logs"  ON audit_logs;
DROP POLICY IF EXISTS "users_select_own_api_audit"   ON api_audit_logs;
DROP POLICY IF EXISTS "users_read_own_tenant_sub"    ON tenant_subscriptions;
DROP POLICY IF EXISTS "users_read_own_usage_stats"   ON tenant_usage_stats;
DROP POLICY IF EXISTS "users_read_own_firm_usage"    ON firm_usage;


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Poslovne tablice — čitanje cijelom uredu, pisanje svima osim readonly
--
-- `audit_logs` je namjerno izvan ove petlje: dnevnik mora biti nepromjenjiv,
-- pa dobiva samo SELECT (korak 4).
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    t TEXT;
    firm_tables TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'valuation_audits', 'case_activities',
        'documents', 'troskovnik_templates'
    ];
BEGIN
    FOREACH t IN ARRAY firm_tables
    LOOP
        -- Čitanje: svi članovi ureda, uključujući readonly.
        EXECUTE format('DROP POLICY IF EXISTS "%s_select_firm" ON %I;', t, t);
        EXECUTE format(
            'CREATE POLICY "%s_select_firm" ON %I '
            'FOR SELECT TO authenticated '
            'USING (law_firm_id = public.user_firm_id());', t, t);

        -- Upis: WITH CHECK sprječava da član ubaci red s tuđim law_firm_id.
        EXECUTE format('DROP POLICY IF EXISTS "%s_insert_firm" ON %I;', t, t);
        EXECUTE format(
            'CREATE POLICY "%s_insert_firm" ON %I '
            'FOR INSERT TO authenticated '
            'WITH CHECK (law_firm_id = public.user_firm_id() '
            '            AND public.user_firm_role() <> ''readonly'');', t, t);

        -- Izmjena: USING bira što se smije dohvatiti, WITH CHECK što smije ostati.
        -- Bez WITH CHECK član bi mogao UPDATE-om premjestiti red u tuđi ured.
        EXECUTE format('DROP POLICY IF EXISTS "%s_update_firm" ON %I;', t, t);
        EXECUTE format(
            'CREATE POLICY "%s_update_firm" ON %I '
            'FOR UPDATE TO authenticated '
            'USING (law_firm_id = public.user_firm_id() '
            '       AND public.user_firm_role() <> ''readonly'') '
            'WITH CHECK (law_firm_id = public.user_firm_id());', t, t);

        EXECUTE format('DROP POLICY IF EXISTS "%s_delete_firm" ON %I;', t, t);
        EXECUTE format(
            'CREATE POLICY "%s_delete_firm" ON %I '
            'FOR DELETE TO authenticated '
            'USING (law_firm_id = public.user_firm_id() '
            '       AND public.user_firm_role() <> ''readonly'');', t, t);
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: Profili i ured
-- ────────────────────────────────────────────────────────────

-- Vidiš kolege iz svog ureda — imenik tima.
DROP POLICY IF EXISTS "profiles_select_firm" ON user_profiles;
CREATE POLICY "profiles_select_firm" ON user_profiles
    FOR SELECT TO authenticated
    USING (law_firm_id = public.user_firm_id());

-- Mijenjaš isključivo vlastiti red, i ne možeš se premjestiti u drugi ured
-- ni sam sebi promijeniti ulogu na višu (rola se mijenja samo kroz service_role).
DROP POLICY IF EXISTS "profiles_update_own" ON user_profiles;
CREATE POLICY "profiles_update_own" ON user_profiles
    FOR UPDATE TO authenticated
    USING (user_id = auth.uid())
    WITH CHECK (
        user_id = auth.uid()
        AND law_firm_id = public.user_firm_id()
        AND role = public.user_firm_role()
    );

-- Ured: čitaju svi članovi, mijenja samo vlasnik.
DROP POLICY IF EXISTS "firms_select_members" ON law_firms;
CREATE POLICY "firms_select_members" ON law_firms
    FOR SELECT TO authenticated
    USING (id = public.user_firm_id());

DROP POLICY IF EXISTS "firms_update_owner" ON law_firms;
CREATE POLICY "firms_update_owner" ON law_firms
    FOR UPDATE TO authenticated
    USING (id = public.user_firm_id() AND public.user_firm_role() = 'owner')
    WITH CHECK (id = public.user_firm_id());

-- Nema INSERT/DELETE politike za law_firms: uredi nastaju kroz trigger
-- handle_new_user, a brišu se samo administrativno preko service_role ključa.


-- ────────────────────────────────────────────────────────────
-- KORAK 4: Dnevnici — samo čitanje, samo vlasnik
--
-- Za odvjetnički ured „tko je kad pristupio kojem predmetu" je dokazni
-- materijal. Zato nema UPDATE ni DELETE politike ni za koga osim service_role:
-- član ureda ne smije moći obrisati trag o sebi.
-- ────────────────────────────────────────────────────────────

DROP POLICY IF EXISTS "audit_logs_select_owner" ON audit_logs;
CREATE POLICY "audit_logs_select_owner" ON audit_logs
    FOR SELECT TO authenticated
    USING (
        law_firm_id = public.user_firm_id()
        AND public.user_firm_role() = 'owner'
    );

DROP POLICY IF EXISTS "api_audit_select_owner" ON api_audit_logs;
CREATE POLICY "api_audit_select_owner" ON api_audit_logs
    FOR SELECT TO authenticated
    USING (
        tenant_id = public.user_firm_id()
        AND public.user_firm_role() = 'owner'
    );


-- ────────────────────────────────────────────────────────────
-- KORAK 5: Obavijesti ostaju osobne
--
-- Namjerno NISU opsegnute po uredu. Obavijest je upućena čovjeku; da smo je
-- vezali na ured, kolege bi čitali tuđe obavijesti.
-- ────────────────────────────────────────────────────────────

DROP POLICY IF EXISTS "notifications_own" ON notifications;
CREATE POLICY "notifications_own" ON notifications
    FOR ALL TO authenticated
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

DROP POLICY IF EXISTS "notification_prefs_own" ON user_notification_prefs;
CREATE POLICY "notification_prefs_own" ON user_notification_prefs
    FOR ALL TO authenticated
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());


-- ────────────────────────────────────────────────────────────
-- KORAK 6: Pretplata i potrošnja — čitanje cijelom uredu
--
-- Stare politike su radile podupit nad user_profiles pri svakom retku;
-- user_firm_id() je STABLE, pa ga planer pozove jednom po upitu.
-- Pisanje ostaje na backendu (service_role) — korisnik si ne smije podići limit.
-- ────────────────────────────────────────────────────────────

DROP POLICY IF EXISTS "tenant_sub_select_firm" ON tenant_subscriptions;
CREATE POLICY "tenant_sub_select_firm" ON tenant_subscriptions
    FOR SELECT TO authenticated
    USING (law_firm_id = public.user_firm_id());

DROP POLICY IF EXISTS "usage_stats_select_firm" ON tenant_usage_stats;
CREATE POLICY "usage_stats_select_firm" ON tenant_usage_stats
    FOR SELECT TO authenticated
    USING (law_firm_id = public.user_firm_id());

DROP POLICY IF EXISTS "firm_usage_select_firm" ON firm_usage;
CREATE POLICY "firm_usage_select_firm" ON firm_usage
    FOR SELECT TO authenticated
    USING (law_firm_id = public.user_firm_id());


-- ────────────────────────────────────────────────────────────
-- KORAK 7: Pozivnice — vidi i kreira ih samo vlasnik ureda
--
-- Token pozivnice se NIKAD ne čita kroz ove politike (tablica ga i ne izlaže
-- korisniku u čitljivom obliku) — pozivnicu razrješava trigger iz 015 koji
-- radi kao SECURITY DEFINER.
-- ────────────────────────────────────────────────────────────

DROP POLICY IF EXISTS "invites_select_owner" ON firm_invites;
CREATE POLICY "invites_select_owner" ON firm_invites
    FOR SELECT TO authenticated
    USING (
        law_firm_id = public.user_firm_id()
        AND public.user_firm_role() = 'owner'
    );

DROP POLICY IF EXISTS "invites_insert_owner" ON firm_invites;
CREATE POLICY "invites_insert_owner" ON firm_invites
    FOR INSERT TO authenticated
    WITH CHECK (
        law_firm_id = public.user_firm_id()
        AND public.user_firm_role() = 'owner'
    );

DROP POLICY IF EXISTS "invites_delete_owner" ON firm_invites;
CREATE POLICY "invites_delete_owner" ON firm_invites
    FOR DELETE TO authenticated
    USING (
        law_firm_id = public.user_firm_id()
        AND public.user_firm_role() = 'owner'
        AND accepted_at IS NULL
    );


-- ────────────────────────────────────────────────────────────
-- KORAK 8: Referentni podaci (zakoni)
--
-- `law_chunks` je imao politiku "anon_select USING (true)" — cijeli korpus
-- čitljiv svakome tko ima javni anon ključ. Sam sadržaj su objavljeni propisi,
-- ali nema razloga da ga čita netko tko nije prijavljen.
-- `norms` se čita isključivo kroz backend (/api/norm), pa ostaje bez
-- korisničke politike.
-- ────────────────────────────────────────────────────────────

DROP POLICY IF EXISTS "anon_select" ON law_chunks;
DROP POLICY IF EXISTS "law_chunks_select_authenticated" ON law_chunks;
CREATE POLICY "law_chunks_select_authenticated" ON law_chunks
    FOR SELECT TO authenticated
    USING (true);


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO.
--
-- OBAVEZAN TEST IZOLACIJE — napraviti PRIJE nego se dirne ijedan ekran:
--
--   1. Kreiraj dva ureda i dva korisnika (A u uredu 1, B u uredu 2).
--   2. Unesi po jedan predmet u svaki ured.
--   3. Prijavi se kao A i pozovi:  select * from cases;
--      → smiješ vidjeti točno predmet ureda 1. Ako vidiš ijedan predmet
--        ureda 2, STANI i ne idi dalje.
--   4. Kao A pokušaj:  insert into cases (law_firm_id, user_id, naziv)
--                      values ('<ured-2-id>', auth.uid(), 'test');
--      → mora puknuti na WITH CHECK.
--   5. Postavi A-u ulogu 'readonly' i ponovi insert u vlastiti ured
--      → mora puknuti. select mora i dalje raditi.
--
-- Pregled svih politika:
--   SELECT tablename, policyname, cmd, roles
--   FROM pg_policies WHERE schemaname = 'public'
--   ORDER BY tablename, cmd, policyname;
--
-- Nijedna tablica bez RLS-a:
--   SELECT tablename FROM pg_tables
--   WHERE schemaname = 'public' AND rowsecurity = false;
--
-- POSLJEDICA ZA BRISANJE RAČUNA (components/settings/PrivacyPanel.tsx):
--   Taj panel briše podatke iz browsera, red po red. Nakon ove migracije
--   `user_profiles` i `audit_logs` više nemaju DELETE politiku, pa ta dva
--   brisanja tiho pogode nula redova. Za audit je to namjerno — dnevnik mora
--   biti nepromjenjiv. Za profil nije: brisanje računa mora ionako obrisati i
--   red u auth.users, što klijent ne može, pa cijeli postupak pripada backendu
--   sa service_role ključem. Do tada „Obriši račun" nije potpuno brisanje.
--
-- SLJEDEĆE: korak 7 iz plana — backend mora slati korisnikov JWT PostgREST-u,
-- inače ove politike ne vrijede ni za jedan zahtjev koji ide kroz .NET.
-- Uz to, svaki INSERT iz backenda mora od tada postavljati law_firm_id,
-- inače pada na WITH CHECK.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/017_user_mfa.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 017 — TOTP tajne izvan dosega korisnika
--
-- TOTP tajna i kodovi za oporavak stajali su u `user_profiles`. Nakon
-- migracije 016 korisnik smije čitati profile svog ureda — dakle i te stupce.
-- Šifrirani su, ali nemaju razloga uopće biti dohvatljivi: tko pročita tajnu,
-- generira valjane kodove i drugi faktor prestaje biti drugi faktor.
--
-- Nova tablica `user_mfa` ima RLS uključen i NIJEDNU korisničku politiku.
-- Do nje dolazi samo backend sa service_role ključem.
--
-- Format tajne se NE mijenja — postojeći authenticatori nastavljaju raditi
-- i nitko ne mora ponovno skenirati QR kod.
--
-- ZAHTIJEVA migracije 015 i 016.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Nova tablica
--
-- FK ide na user_profiles(user_id), a ne na auth.users, kako bi PostgREST
-- mogao ugnijezditi user_mfa(totp_enabled) u upit nad user_profiles
-- (koristi ga admin pregled). Lanac brisanja i dalje vodi do auth.users.
-- ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS user_mfa (
    user_id                     UUID        PRIMARY KEY
                                            REFERENCES user_profiles(user_id) ON DELETE CASCADE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    totp_enabled                BOOLEAN     NOT NULL DEFAULT FALSE,
    totp_secret_encrypted       TEXT,
    recovery_codes_encrypted    TEXT,
    totp_enabled_at             TIMESTAMPTZ
);

DROP TRIGGER IF EXISTS trg_user_mfa_updated_at ON user_mfa;
CREATE TRIGGER trg_user_mfa_updated_at
    BEFORE UPDATE ON user_mfa
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Prijenos postojećih upisa
--
-- Stupci na user_profiles su nastali ad hoc (backend ih je upisivao preko
-- PostgREST-a), pa ne pretpostavljamo da postoje.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    moved BIGINT := 0;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'user_profiles'
          AND column_name = 'totp_secret_encrypted'
    ) THEN
        INSERT INTO user_mfa (
            user_id, totp_enabled, totp_secret_encrypted,
            recovery_codes_encrypted, totp_enabled_at
        )
        SELECT
            up.user_id,
            COALESCE(up.totp_enabled, FALSE),
            up.totp_secret_encrypted,
            up.recovery_codes_encrypted,
            up.totp_enabled_at
        FROM user_profiles up
        WHERE up.totp_secret_encrypted IS NOT NULL
           OR COALESCE(up.totp_enabled, FALSE) = TRUE
        ON CONFLICT (user_id) DO NOTHING;

        GET DIAGNOSTICS moved = ROW_COUNT;
        RAISE NOTICE 'Preneseno % 2FA zapisa u user_mfa', moved;
    ELSE
        RAISE NOTICE 'user_profiles nema TOTP stupce — nema se što prenijeti';
    END IF;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: Brisanje starih stupaca
--
-- Tek nakon prijenosa. Ako korak 2 nije ništa našao, ovo je no-op.
-- ────────────────────────────────────────────────────────────

ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_secret_encrypted;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS recovery_codes_encrypted;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_enabled;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_enabled_at;


-- ────────────────────────────────────────────────────────────
-- KORAK 4: RLS — zaključano
--
-- Nema politike za `authenticated`. To znači da korisnik kroz anon ključ ne
-- vidi ni vlastiti red, što je namjerno: nema legitimnog razloga da klijent
-- ikad pročita tajnu. Status 2FA saznaje kroz backend (/api/2fa/status).
-- ────────────────────────────────────────────────────────────

ALTER TABLE user_mfa ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON user_mfa;
CREATE POLICY "service_role_full_access" ON user_mfa
    FOR ALL USING (auth.role() = 'service_role');


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Tajne više nisu na profilu:
--   SELECT column_name FROM information_schema.columns
--   WHERE table_schema='public' AND table_name='user_profiles'
--     AND column_name LIKE 'totp%';          -- mora vratiti 0 redova
--
--   -- Preneseni zapisi:
--   SELECT user_id, totp_enabled, totp_enabled_at FROM user_mfa;
--
--   -- Nijedna korisnička politika:
--   SELECT policyname, roles FROM pg_policies
--   WHERE tablename = 'user_mfa';            -- samo service_role_full_access
--
-- NAKON MIGRACIJE: prijavi se korisnikom koji ima 2FA i potvrdi da postojeći
-- authenticator i dalje daje prihvaćene kodove. Tajna se nije mijenjala, samo
-- preselila.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/018_storage_policies.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 018 — Privatni bucketi i pristup po uredu
--
-- `zk-izvadci` se u aplikaciji čitao preko getPublicUrl(), što radi samo na
-- javnom bucketu. U njemu su zemljišnoknjižni izvadci: imena, OIB-ovi,
-- katastarske čestice. Javan bucket znači da je svaki taj dokument dostupan
-- svakome tko pogodi ili sazna URL — bez prijave, bez traga u dnevniku.
--
-- Ova migracija zatvara bucket i uvodi pravilo iz dokumenta (§11): prvi segment
-- putanje je law_firm_id, pa se politika svodi na jednu usporedbu.
--
-- ⚠ RUŠI POSTOJEĆE JAVNE LINKOVE. Svaki spremljeni `pdf_url` prestaje raditi.
--   Dogovoreno kao prihvatljivo — u bazi su samo testni podaci.
--
-- ZAHTIJEVA migracije 015 i 016 (user_firm_id / user_firm_role helperi).
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Bucketi — postoje i privatni su
-- ────────────────────────────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES ('zk-izvadci', 'zk-izvadci', FALSE, 52428800)
ON CONFLICT (id) DO UPDATE
    SET public = FALSE,
        file_size_limit = EXCLUDED.file_size_limit;

INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES ('case-documents', 'case-documents', FALSE, 52428800)
ON CONFLICT (id) DO UPDATE
    SET public = FALSE,
        file_size_limit = EXCLUDED.file_size_limit;


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Uklanjanje zatečenih politika nad ovim bucketima
--
-- Imena ne znamo unaprijed — nastala su klikanjem po dashboardu. Zato ih
-- tražimo po sadržaju izraza, i to samo one koje spominju naša dva bucketa;
-- politike drugih bucketa ostaju netaknute.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    p RECORD;
BEGIN
    FOR p IN
        SELECT policyname
        FROM pg_policies
        WHERE schemaname = 'storage'
          AND tablename = 'objects'
          AND (
              COALESCE(qual, '')       LIKE '%zk-izvadci%'
           OR COALESCE(with_check, '') LIKE '%zk-izvadci%'
           OR COALESCE(qual, '')       LIKE '%case-documents%'
           OR COALESCE(with_check, '') LIKE '%case-documents%'
          )
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS %I ON storage.objects;', p.policyname);
        RAISE NOTICE 'Uklonjena zatečena storage politika: %', p.policyname;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: Politike po uredu
--
-- Konvencija putanje: {law_firm_id}/{uuid}-{filename}
-- storage.foldername(name) vraća polje segmenata, pa je [1] uvijek ured.
-- Datoteka spremljena bez tog prefiksa nije čitljiva nikome osim service_role —
-- fail-closed, kako i treba biti.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    b TEXT;
    buckets TEXT[] := ARRAY['zk-izvadci', 'case-documents'];
BEGIN
    FOREACH b IN ARRAY buckets
    LOOP
        -- Čitanje: svi članovi ureda, uključujući readonly.
        EXECUTE format(
            'CREATE POLICY "%s_select_firm" ON storage.objects '
            'FOR SELECT TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text);',
            b, b);

        -- Upload: readonly ne smije.
        EXECUTE format(
            'CREATE POLICY "%s_insert_firm" ON storage.objects '
            'FOR INSERT TO authenticated '
            'WITH CHECK (bucket_id = %L '
            '            AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '            AND public.user_firm_role() <> ''readonly'');',
            b, b);

        -- Prepisivanje postojeće datoteke.
        EXECUTE format(
            'CREATE POLICY "%s_update_firm" ON storage.objects '
            'FOR UPDATE TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '       AND public.user_firm_role() <> ''readonly'') '
            'WITH CHECK (bucket_id = %L '
            '            AND (storage.foldername(name))[1] = public.user_firm_id()::text);',
            b, b, b);

        EXECUTE format(
            'CREATE POLICY "%s_delete_firm" ON storage.objects '
            'FOR DELETE TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '       AND public.user_firm_role() <> ''readonly'');',
            b, b);
    END LOOP;
END $$;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Oba bucketa privatna:
--   SELECT id, public, file_size_limit FROM storage.buckets
--   WHERE id IN ('zk-izvadci', 'case-documents');
--
--   -- Politike:
--   SELECT policyname, cmd, roles FROM pg_policies
--   WHERE schemaname = 'storage' AND tablename = 'objects'
--   ORDER BY policyname;
--
-- TEST IZOLACIJE:
--   1. Kao korisnik ureda A uploadaj datoteku na putanju {ured-A}/test.pdf.
--   2. Kao korisnik ureda B pokušaj createSignedUrl za tu putanju → mora pući.
--   3. Otvori stari javni URL nekog ZK izvatka → mora vratiti 400/404.
--      Ako se i dalje otvara, bucket nije zatvoren — provjeri dashboard.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/019_trigger_and_rpc_hardening.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 019 — Popravak trigera i zatvaranje SECURITY DEFINER funkcija
--
-- Dvije stvari, obje otkrivene testom izolacije nakon migracija 015–018.
--
-- A) KVAR KOJI ZAUSTAVLJA APLIKACIJU
--    Na `cases` postoje trigeri koji pri kreiranju i promjeni statusa upisuju
--    redak u `case_activities`. Migracija 015 je `case_activities.law_firm_id`
--    postavila na NOT NULL, a trigeri ga ne postavljaju — pa svako kreiranje
--    predmeta puca s "null value in column law_firm_id".
--
-- B) ZAOBILAŽENJE RLS-a KOJE 016 NIJE MOGLA POKRITI
--    Osam funkcija je SECURITY DEFINER i GRANT EXECUTE ... TO authenticated.
--    SECURITY DEFINER znači da se izvršavaju kao vlasnik i RLS se NE primjenjuje.
--    Sve primaju p_user_id ili p_tenant_id kao PARAMETAR i nijedna ne provjerava
--    tko ih zove — pa prijavljen korisnik proslijedi tuđi UUID i dobije tuđe
--    podatke. `search_clients` tako vraća popis stranaka drugog ureda,
--    `toggle_archive` arhivira tuđe zapise.
--
--    Politike iz 016 tu ne pomažu: SECURITY DEFINER ih po definiciji preskače.
--    Zato guard mora biti unutar same funkcije.
--
-- ZAHTIJEVA migracije 015 i 016.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- A: Trigeri na cases upisuju ured u case_activities
--
-- Ured se uzima iz NEW.law_firm_id — predmet ga ima i NOT NULL je, pa je to
-- uvijek ispravna vrijednost. Trigeri namjerno NISU SECURITY DEFINER: neka se
-- upis u case_activities i dalje provjerava kroz RLS.
-- ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION log_case_created()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO case_activities (
        user_id, law_firm_id, case_id, activity_type, title, description
    )
    VALUES (
        NEW.user_id,
        NEW.law_firm_id,
        NEW.id,
        'created',
        'Predmet kreiran',
        COALESCE(NEW.naziv, '')
            || CASE WHEN NEW.broj_predmeta != '' THEN ' (' || NEW.broj_predmeta || ')' ELSE '' END
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION log_case_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.status IS DISTINCT FROM NEW.status THEN
        INSERT INTO case_activities (
            user_id, law_firm_id, case_id, activity_type, title, metadata
        )
        VALUES (
            NEW.user_id,
            NEW.law_firm_id,
            NEW.id,
            'status_change',
            'Status: ' || COALESCE(OLD.status, '?') || ' → ' || COALESCE(NEW.status, '?'),
            jsonb_build_object('old_status', OLD.status, 'new_status', NEW.status)
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;


-- ────────────────────────────────────────────────────────────
-- B: Guard za funkcije koje primaju tuđi identitet
--
-- Potpisi se NE mijenjaju — frontend ih trenutno uopće ne poziva (provjereno:
-- nema nijednog .rpc() u kodu), pa je ovo bez posljedica za aplikaciju, a
-- dokumentirani primjeri u README-u nastavljaju raditi.
--
-- Pomoćne funkcije da se ista provjera ne prepisuje osam puta.
-- ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.assert_is_self(p_user_id UUID)
RETURNS VOID
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF auth.uid() IS NULL OR p_user_id IS DISTINCT FROM auth.uid() THEN
        RAISE EXCEPTION 'Pristup odbijen: dopusten je samo vlastiti korisnicki ID.'
            USING ERRCODE = '42501';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION public.assert_is_own_firm(p_tenant_id UUID)
RETURNS VOID
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF auth.uid() IS NULL OR p_tenant_id IS DISTINCT FROM public.user_firm_id() THEN
        RAISE EXCEPTION 'Pristup odbijen: dopusten je samo vlastiti ured.'
            USING ERRCODE = '42501';
    END IF;
END $$;

GRANT EXECUTE ON FUNCTION public.assert_is_self(UUID)      TO authenticated;
GRANT EXECUTE ON FUNCTION public.assert_is_own_firm(UUID)  TO authenticated;


-- ── Ubacivanje guarda u postojeće funkcije ──
-- Tijela se ne prepisuju ručno (dugačka su i lako se pogriješi), nego se
-- postojeći izvor dohvati iz kataloga i guard se umetne odmah iza BEGIN.

DO $$
DECLARE
    f            RECORD;
    src          TEXT;
    novi         TEXT;
    guard        TEXT;
    pozicija     INT;
    ciljevi      TEXT[] := ARRAY[
        'get_dashboard_stats',
        'search_clients',
        'get_case_events_summary',
        'mark_notifications_read',
        'get_unread_notification_count',
        'toggle_archive',
        'get_monthly_revenue',
        'is_tenant_subscription_active'
    ];
BEGIN
    FOR f IN
        SELECT p.oid,
               p.proname,
               pg_get_functiondef(p.oid) AS def,
               pg_get_function_identity_arguments(p.oid) AS args
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public'
          AND p.proname = ANY(ciljevi)
    LOOP
        src := f.def;

        IF src LIKE '%assert_is_self%' OR src LIKE '%assert_is_own_firm%' THEN
            RAISE NOTICE 'Funkcija %(%) vec ima guard — preskacem', f.proname, f.args;
            CONTINUE;
        END IF;

        guard := CASE
            WHEN f.proname = 'is_tenant_subscription_active'
                THEN '  PERFORM public.assert_is_own_firm(p_tenant_id);'
            ELSE '  PERFORM public.assert_is_self(p_user_id);'
        END;

        -- SQL-jezične funkcije nemaju BEGIN blok u koji bi se guard umetnuo.
        -- Njih prepisujemo pojedinačno ispod, pa ih ovdje samo prijavimo.
        IF src ILIKE '%LANGUAGE sql%' THEN
            RAISE NOTICE 'Funkcija %(%) je LANGUAGE sql — rjesava se zasebno', f.proname, f.args;
            CONTINUE;
        END IF;

        pozicija := position('BEGIN' in src);
        IF pozicija = 0 THEN
            RAISE WARNING 'Ne mogu pronaci BEGIN u %(%) — GUARD NIJE DODAN', f.proname, f.args;
            CONTINUE;
        END IF;

        novi := overlay(src placing E'BEGIN\n' || guard from pozicija for 5);
        EXECUTE novi;
        RAISE NOTICE 'Guard dodan u %(%)', f.proname, f.args;
    END LOOP;
END $$;


-- ── LANGUAGE sql funkcije — prepisane u plpgsql da guard uopće stane ──
--
-- DROP prije CREATE, ne CREATE OR REPLACE: zamjena ne smije mijenjati povratni
-- tip ni imena izlaznih stupaca, a ovdje mijenjamo i jezik funkcije. Bez DROP-a
-- migracija stane na "cannot change return type of existing function" čim se
-- tip u bazi i tip ovdje razlikuju makar u INT vs BIGINT.
-- Sigurno je: nijedan view ne ovisi o njima, a frontend ih ne poziva.
-- GRANT se gubi s DROP-om, pa se ponovno dodjeljuje na kraju.

DROP FUNCTION IF EXISTS get_unread_notification_count(UUID);
DROP FUNCTION IF EXISTS get_monthly_revenue(UUID, INT);
DROP FUNCTION IF EXISTS is_tenant_subscription_active(UUID);


CREATE FUNCTION get_unread_notification_count(p_user_id UUID)
RETURNS INT
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    PERFORM public.assert_is_self(p_user_id);
    RETURN (
        SELECT count(*)::INT FROM notifications
        WHERE user_id = p_user_id AND is_read = FALSE
    );
END $$;


CREATE FUNCTION get_monthly_revenue(p_user_id UUID, p_months INT DEFAULT 12)
RETURNS TABLE (
    month DATE, troskovnik_count BIGINT, troskovnik_eur_bez_pdv NUMERIC,
    troskovnik_eur_s_pdv NUMERIC, invoice_count BIGINT,
    invoice_eur_bez_pdv NUMERIC, invoice_eur_s_pdv NUMERIC
)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    PERFORM public.assert_is_self(p_user_id);
    RETURN QUERY
        SELECT mr.month, mr.troskovnik_count, mr.troskovnik_eur_bez_pdv, mr.troskovnik_eur_s_pdv,
               mr.invoice_count, mr.invoice_eur_bez_pdv, mr.invoice_eur_s_pdv
        FROM monthly_revenue mr
        WHERE mr.user_id = p_user_id
        ORDER BY mr.month DESC
        LIMIT p_months;
END $$;


CREATE FUNCTION is_tenant_subscription_active(p_tenant_id UUID)
RETURNS BOOLEAN
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    PERFORM public.assert_is_own_firm(p_tenant_id);
    RETURN EXISTS (
        SELECT 1 FROM tenant_subscriptions
        WHERE law_firm_id = p_tenant_id AND is_active = TRUE AND valid_until > now()
    );
END $$;

GRANT EXECUTE ON FUNCTION get_unread_notification_count(UUID)      TO authenticated;
GRANT EXECUTE ON FUNCTION get_monthly_revenue(UUID, INT)           TO authenticated;
GRANT EXECUTE ON FUNCTION is_tenant_subscription_active(UUID)      TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera — svaka ciljana funkcija mora imati guard:
--
--   SELECT p.proname,
--          pg_get_function_identity_arguments(p.oid) AS args,
--          (pg_get_functiondef(p.oid) LIKE '%assert_is_%') AS ima_guard
--   FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--   WHERE n.nspname = 'public'
--     AND p.proname IN ('get_dashboard_stats','search_clients','get_case_events_summary',
--                       'mark_notifications_read','get_unread_notification_count',
--                       'toggle_archive','get_monthly_revenue','is_tenant_subscription_active')
--   ORDER BY 1;
--   -- ima_guard mora biti true za sve.
--
-- Zatim ponoviti Test 1 izolacije — sada mora proći do kraja i vratiti 0.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/020_authorship_not_ownership.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 020 — user_id je autorstvo, ne vlasništvo
--
-- Zatečeno stanje: `user_id` na poslovnim tablicama je
-- `NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE`.
--
-- To je bilo točno dok su podaci pripadali korisniku. Migracije 015 i 016
-- vlasništvo su prebacile na ured — ali strani ključevi su ostali kakvi jesu.
-- Posljedica: brisanje JEDNOG odvjetnika iz auth.users obriše cijelu arhivu
-- predmeta njegovog ureda, i onda kad u uredu radi još troje ljudi.
--
-- Bomba koja opali prvi put kad netko obriše račun. Zato ide prije nego
-- brisanje računa uopće postane moguće.
--
-- Nakon ove migracije:
--   law_firm_id  → tko POSJEDUJE zapis   (NOT NULL, ON DELETE CASCADE)
--   user_id      → tko ga je NAPRAVIO    (nullable, ON DELETE SET NULL)
--
-- Osobne tablice (profil, MFA, obavijesti, postavke obavijesti) namjerno
-- ZADRŽAVAJU CASCADE — one opisuju osobu i s njom nestaju.
--
-- ZAHTIJEVA migracije 015 i 016.
-- ═══════════════════════════════════════════════════════════════════════

DO $$
DECLARE
    t          TEXT;
    fk_name    TEXT;
    firm_owned TEXT[] := ARRAY[
        'clients', 'cases', 'troskovnici', 'case_events', 'invoices',
        'evaluations', 'contract_analyses', 'chronologies', 'legal_strategies',
        'zk_analyses', 'case_activities', 'documents', 'troskovnik_templates'
    ];
BEGIN
    FOREACH t IN ARRAY firm_owned
    LOOP
        -- Postojeći FK tražimo po stupcu, ne po pretpostavljenom imenu — imena
        -- su nastajala kroz nekoliko migracija i ne moraju slijediti konvenciju.
        SELECT con.conname INTO fk_name
        FROM pg_constraint con
        JOIN pg_class rel      ON rel.oid = con.conrelid
        JOIN pg_namespace ns   ON ns.oid = rel.relnamespace
        WHERE ns.nspname = 'public'
          AND rel.relname = t
          AND con.contype = 'f'
          AND con.confrelid = 'auth.users'::regclass
          AND con.conkey = ARRAY[
              (SELECT attnum FROM pg_attribute
               WHERE attrelid = rel.oid AND attname = 'user_id')
          ];

        IF fk_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE %I DROP CONSTRAINT %I;', t, fk_name);
        END IF;

        -- SET NULL ne može raditi nad NOT NULL stupcem.
        EXECUTE format('ALTER TABLE %I ALTER COLUMN user_id DROP NOT NULL;', t);

        EXECUTE format(
            'ALTER TABLE %I ADD CONSTRAINT %I '
            'FOREIGN KEY (user_id) REFERENCES auth.users(id) ON DELETE SET NULL;',
            t, t || '_user_id_fkey');

        RAISE NOTICE 'Tablica %: user_id je sada nullable, ON DELETE SET NULL', t;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- Politike se ne mijenjaju
--
-- Migracija 016 filtrira isključivo po law_firm_id, pa prazan user_id ne utječe
-- na vidljivost: zapis autora kojeg više nema i dalje pripada uredu i uredu je
-- vidljiv. To je i svrha — spis ostaje uredu kad odvjetnik ode.
-- ────────────────────────────────────────────────────────────


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera — nijedan zapis ureda ne smije visjeti o korisniku:
--
--   SELECT rel.relname AS tablica,
--          con.conname AS ogranicenje,
--          CASE con.confdeltype WHEN 'c' THEN 'CASCADE'
--                               WHEN 'n' THEN 'SET NULL'
--                               WHEN 'a' THEN 'NO ACTION'
--                               ELSE con.confdeltype::text END AS pri_brisanju
--   FROM pg_constraint con
--   JOIN pg_class rel    ON rel.oid = con.conrelid
--   JOIN pg_namespace ns ON ns.oid = rel.relnamespace
--   WHERE ns.nspname = 'public'
--     AND con.contype = 'f'
--     AND con.confrelid = 'auth.users'::regclass
--   ORDER BY 1;
--
--   -- Poslovne tablice moraju biti SET NULL.
--   -- user_profiles, user_notification_prefs, notifications, subscriptions
--   -- ostaju CASCADE — to su osobni podaci i s osobom nestaju.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/021_pdv_obveznik_naknada_predujam.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 021 — PDV obveznik na razini ureda + naknada troškova i predujam
--
-- Dvije stvari:
--
-- 1. `law_firms.pdv_obveznik` — trajna postavka je li ured u sustavu PDV-a.
--    Ne zamjenjuje per-dokument `troskovnici.is_pdv_ukljucen`/`has_vat` (taj
--    checkbox ostaje uređiv), samo mu daje zadanu vrijednost na frontendu.
--
-- 2. `troskovnici.naknada_troskova_ukupno` / `predujmovi_ukupno` — sumarni
--    iznosi novih vrsta stavki (Tbr. 48.-51. naknada troškova ulazi u PDV
--    osnovicu; predujam je prolazna stavka, ne ulazi). Pojedinačne stavke
--    idu isključivo u postojeći `raw_json_output` JSONB, isto kao bodovni
--    retci i kao sudska pristojba — namjerno NEMA normaliziranih stupaca za
--    pojedinačne retke ni za sudsku pristojbu/sveukupni trošak, pa dosljedno
--    tome ni ovdje ne uvodimo novu tablicu.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE law_firms
    ADD COLUMN IF NOT EXISTS pdv_obveznik BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN law_firms.pdv_obveznik IS
    'Je li ured u sustavu PDV-a. Zadana vrijednost za novi troškovnik — korisnik je i dalje može ručno promijeniti po dokumentu.';

ALTER TABLE troskovnici
    ADD COLUMN IF NOT EXISTS naknada_troskova_ukupno NUMERIC(12,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS predujmovi_ukupno       NUMERIC(12,2) NOT NULL DEFAULT 0;

COMMENT ON COLUMN troskovnici.naknada_troskova_ukupno IS
    'Zbroj stvarnih izdataka po Tbr. 48.-51. (prijevoz, dnevnica, hotel, ostali izdaci) — ULAZI u PDV osnovicu (vlastiti trošak obavljanja usluge). Pojedinačne stavke su u raw_json_output.';
COMMENT ON COLUMN troskovnici.predujmovi_ukupno IS
    'Zbroj predujmova (npr. za vještačenje) — prolazna stavka po čl. 33. Zakona o PDV-u, NE ulazi u osnovicu, ne oporezuje se. Pojedinačne stavke su u raw_json_output.';

-- ═══════════ migrations/022_onboarding.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 022 — je li korisnik prošao uvod
--
-- Zatečeno stanje: aplikacija ima vodič i traku dobrodošlice, ali obje pamti
-- `localStorage.pravotek_tour_seen`. To znači:
--
--   • isti odvjetnik na drugom uređaju je opet „nov" i opet dobiva uvod;
--   • tko je uvod prošao na mobitelu, dobiva ga ponovo na računalu;
--   • tko je jednom kliknuo „Preskoči", nakon čišćenja preglednika opet ga vidi;
--   • vlasnik aplikacije nema način vidjeti je li itko uvod uopće završio.
--
-- Biljeg pripada korisniku, ne pregledniku. Otud stupac.
--
-- NULL  = uvod još nije prošao (ili preskočen)
-- vrijeme = trenutak kad ga je završio ILI svjesno preskočio; oboje znače isto
--           za pitanje „prikazati opet?", a razlika nije vrijedna drugog stupca.
--
-- RLS: politika `profiles_update_own` iz migracije 016 već dopušta korisniku
-- izmjenu vlastitog reda, a njezin WITH CHECK ograničava samo `law_firm_id` i
-- `role`. Postavljanje `onboarded_at` prolazi bez nove politike.
--
-- ZAHTIJEVA migracije 015 i 016. Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE public.user_profiles
    ADD COLUMN IF NOT EXISTS onboarded_at TIMESTAMPTZ;

COMMENT ON COLUMN public.user_profiles.onboarded_at IS
    'Kad je korisnik završio ili preskočio uvodni vodič. NULL = još nije. '
    'Zamjenjuje localStorage biljeg koji nije preživio promjenu uređaja.';


-- ────────────────────────────────────────────────────────────
-- Postojeći korisnici nisu novi
--
-- Bez ovoga bi svi zatečeni računi pri prvoj prijavi nakon deploya dobili
-- ekran dobrodošlice — što je za nekoga tko aplikaciju koristi mjesecima
-- gore nego da uvoda nema.
--
-- Granica je namjerno „ima li išta u bazi", a ne datum: račun koji je
-- otvoren, ali nikad nije ništa napravio, uvod zapravo treba.
-- ────────────────────────────────────────────────────────────

UPDATE public.user_profiles up
SET onboarded_at = COALESCE(up.onboarded_at, up.created_at)
WHERE up.onboarded_at IS NULL
  AND EXISTS (
      SELECT 1 FROM public.troskovnici t WHERE t.user_id = up.user_id
      UNION ALL
      SELECT 1 FROM public.cases c       WHERE c.user_id = up.user_id
      UNION ALL
      SELECT 1 FROM public.clients cl    WHERE cl.user_id = up.user_id
  );


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Tko će vidjeti uvod pri sljedećoj prijavi:
--   SELECT email, created_at, onboarded_at
--   FROM user_profiles
--   WHERE onboarded_at IS NULL
--   ORDER BY created_at DESC;
--
--   -- Uredi imenovani po e-mail adresi (Google registracija nije slala naziv;
--   -- ekran /dobrodosli sada traži da se to ispravi):
--   SELECT lf.id, lf.name
--   FROM law_firms lf
--   JOIN user_profiles up ON up.law_firm_id = lf.id
--   WHERE lower(lf.name) = lower(up.email);
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/023_revoke_rpc_from_anon.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 023 — RPC funkcije se više ne zovu javnim ključem
--
-- Zatečeno: petnaest SECURITY DEFINER funkcija imalo je EXECUTE za rolu `anon`,
-- pa ih je preko /rest/v1/rpc/<ime> mogao pozvati bilo tko tko ima javni anon
-- ključ — a on je po prirodi u JS paketu svake stranice.
--
-- Migracija 019 je čitačima podataka (search_clients, toggle_archive,
-- get_dashboard_stats…) dodala provjere, pa one ne propuštaju tuđe podatke.
-- Tri funkcije te provjere nemaju jer nikad nisu ni bile zamišljene kao
-- korisničke:
--
--   purge_old_api_audit_logs(int) — BRIŠE dnevnik. Migracija 016 je namjerno
--       maknula DELETE politiku s audit_logs jer je za odvjetnički ured
--       „tko je kad pristupio kojem spisu" dokazni materijal. Ovaj RPC je taj
--       isti dnevnik brisao zaobilazeći RLS, i to bez prijave.
--   expire_overdue_subscriptions() — gasi pretplate cijelog sustava.
--   handle_new_user() — okidač; kao RPC bi pukao, ali nema razloga da stoji.
--
-- Ove tri ostaju isključivo za service_role i za cron (koji radi kao postgres);
-- nijedno od toga ne ovisi o GRANT-u koji se ovdje oduzima.
--
-- Ostalima se oduzima samo `anon`. `authenticated` ZADRŽAVA EXECUTE:
-- user_firm_id() i user_firm_role() pozivaju se iz RLS politika u ime
-- prijavljenog korisnika, pa bi im oduzimanje prava srušilo svaki upit.
--
-- Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════

-- ── Održavateljske funkcije: nikome osim service_role ──
REVOKE EXECUTE ON FUNCTION public.purge_old_api_audit_logs(integer)   FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.expire_overdue_subscriptions()      FROM anon, authenticated;
REVOKE EXECUTE ON FUNCTION public.handle_new_user()                   FROM anon, authenticated;

-- ── Korisničke funkcije: prijavljeni da, neprijavljeni ne ──
REVOKE EXECUTE ON FUNCTION public.search_clients(uuid, text, integer)                    FROM anon;
REVOKE EXECUTE ON FUNCTION public.toggle_archive(text, uuid, uuid, boolean)              FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_dashboard_stats(uuid)                              FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_monthly_revenue(uuid, integer)                     FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_case_events_summary(uuid, uuid)                    FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_unread_notification_count(uuid)                    FROM anon;
REVOKE EXECUTE ON FUNCTION public.mark_notifications_read(uuid, uuid[])                  FROM anon;
REVOKE EXECUTE ON FUNCTION public.is_tenant_subscription_active(uuid)                    FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_api_audit_logs(uuid, timestamptz, timestamptz, integer, integer) FROM anon;
REVOKE EXECUTE ON FUNCTION public.get_api_audit_stats(uuid, integer)                     FROM anon;

-- ── Pomoćne za RLS: anon ne, authenticated OBAVEZNO da ──
REVOKE EXECUTE ON FUNCTION public.user_firm_id()   FROM anon;
REVOKE EXECUTE ON FUNCTION public.user_firm_role() FROM anon;
GRANT  EXECUTE ON FUNCTION public.user_firm_id()   TO authenticated;
GRANT  EXECUTE ON FUNCTION public.user_firm_role() TO authenticated;


-- ═══════════════════════════════════════════════════════════════════════
-- PROVJERA nakon pokretanja — `anon_smije` mora biti false u svakom retku:
--
--   SELECT p.proname, has_function_privilege('anon', p.oid, 'EXECUTE') AS anon_smije
--   FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
--   WHERE n.nspname = 'public' AND p.prosecdef
--   ORDER BY anon_smije DESC, p.proname;
--
-- I obavezno: prijavi se u aplikaciju i otvori bilo koji popis. Ako se
-- predmeti i stranke i dalje vide, `authenticated` je zadržao što treba.
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/024_jedan_model_naplate.sql ═══════════
-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 024 — Jedan model naplate: proba 7 dana, pa pretplata
--
-- Do sada su postojale tri razine — 'free', 'starter', 'pro' — od kojih se
-- nijedna nikada nije prodavala. Ostaje jedan plan: novi ured dobiva 7 dana
-- probe od otvaranja, nakon čega mu se Troškovnik i Stranke zatvaraju dok
-- pretplata ne bude plaćena.
--
-- NIŠTA SE NE BRIŠE. Nema DROP TABLE, nema DROP COLUMN, nema DELETE. Migracija
-- samo mijenja dopuštene vrijednosti, popunjava postojeće retke i zamjenjuje
-- okidač koji je novim uredima dodjeljivao besplatni plan.
--
-- Trajanje probe NIJE ovdje. Ono je konstanta `SubscriptionPlans.TrialDays` u
-- Backend/Models/SubscriptionPlans.cs, i backend istek probe računa kao
-- valid_from + TrialDays. `valid_until` koji ovdje upisujemo za probne retke
-- postoji samo zato da red pročitan izravno u Supabaseu ne bude zavaravajući —
-- SubscriptionGate ga za probu ne čita. Zahvaljujući tome se trajanje probe
-- mijenja izmjenom jedne konstante, bez nove migracije i bez diranja podataka.
--
-- Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: plan_id ima jednu dopuštenu vrijednost
--
-- Stupac ostaje iako više ne nosi informaciju. Njegovo brisanje bilo bi
-- destruktivna promjena sheme, a ovako migracija ne može ništa izgubiti.
-- Kod ga je prestao čitati; ostaje kao trag, ne kao odluka.
-- ────────────────────────────────────────────────────────────

ALTER TABLE tenant_subscriptions DROP CONSTRAINT IF EXISTS chk_tenant_sub_plan_id;

UPDATE tenant_subscriptions
   SET plan_id = 'pravotek'
 WHERE plan_id IS DISTINCT FROM 'pravotek';

ALTER TABLE tenant_subscriptions ALTER COLUMN plan_id SET DEFAULT 'pravotek';

ALTER TABLE tenant_subscriptions
    ADD CONSTRAINT chk_tenant_sub_plan_id CHECK (plan_id = 'pravotek');


-- ────────────────────────────────────────────────────────────
-- KORAK 2: postojeći uredi ne smiju ispasti u trenutku deploya
--
-- Proba se računa od otvaranja ureda, pa bi svaki ured otvoren prije ove
-- promjene bio „istekao" istog trena. Zato svi postojeći retci prelaze na
-- aktivnu pretplatu.
--
-- Godina dana je samo polazno stanje da nitko ne ostane vani dok se ne otvori
-- /admin. Svakom uredu se stanje i datum mijenjaju u sučelju
-- (PATCH /api/admin/subscriptions/{lawFirmId}) i vrijede odmah.
--
-- WHERE stiti od ponovnog pokretanja: ured koji je u meduvremenu rucno
-- postavljen na 'expired' ili 'trial' ne vraca se ovim UPDATE-om na aktivan.
-- ────────────────────────────────────────────────────────────

UPDATE tenant_subscriptions
   SET status       = 'active',
       valid_until  = now() + interval '1 year',
       is_active    = TRUE,
       price_eur    = 19,
       max_cases    = -1,      -- -1 = neograničeno (SubscriptionPlans.Unlimited)
       max_invoices = -1
 WHERE status IN ('active', 'canceled');


-- ────────────────────────────────────────────────────────────
-- KORAK 3: novi ured dobiva probu, ne besplatni plan
--
-- Nasljednik okidača auto_provision_free_tier. Uz promjenu sadržaja dobiva i
-- SECURITY DEFINER + SET search_path, kojih prethodnik nije imao — za razliku
-- od svakog drugog okidača otvrdnutog u migracijama 015 i 019.
-- ────────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION public.auto_provision_trial()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO public.tenant_subscriptions (
        law_firm_id, plan_id, status,
        valid_from, valid_until,
        is_active, price_eur, max_cases, max_invoices, ai_credits_limit
    )
    VALUES (
        NEW.id, 'pravotek', 'trial',
        now(),
        -- Informativno. Mjerodavan je valid_from + SubscriptionPlans.TrialDays.
        now() + interval '7 days',
        TRUE, 19, -1, -1, 0
    )
    ON CONFLICT DO NOTHING;

    RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS trg_auto_provision_free_tier ON public.law_firms;
DROP TRIGGER IF EXISTS trg_auto_provision_trial     ON public.law_firms;

CREATE TRIGGER trg_auto_provision_trial
    AFTER INSERT ON public.law_firms
    FOR EACH ROW EXECUTE FUNCTION public.auto_provision_trial();

-- Stara funkcija se briše tek nakon što okidač više ne pokazuje na nju.
DROP FUNCTION IF EXISTS public.auto_provision_free_tier();

-- Iz istog razloga iz kojeg migracija 023 oduzima prava na ostale okidače:
-- ovo smije pozivati samo Postgres, iz okidača.
REVOKE EXECUTE ON FUNCTION public.auto_provision_trial() FROM anon, authenticated;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Nijedan ured bez pretplate, i nijedan na starom planu:
--   SELECT plan_id, status, count(*)
--     FROM tenant_subscriptions
--    GROUP BY plan_id, status;
--
--   -- Uredi bez ijednog retka o pretplati (očekivano: nula):
--   SELECT f.id, f.name
--     FROM law_firms f
--    WHERE NOT EXISTS (
--          SELECT 1 FROM tenant_subscriptions s WHERE s.law_firm_id = f.id);
--
--   -- Okidač je na mjestu:
--   SELECT tgname FROM pg_trigger WHERE tgrelid = 'public.law_firms'::regclass;
-- ═══════════════════════════════════════════════════════════════════════

-- ═══════════ migrations/2026_02_25_add_nested_troskovnik_jsonb.sql ═══════════
-- Add nested JSONB columns for troškovnik (odvjetnik, predmet, stranke)

BEGIN;

ALTER TABLE IF EXISTS public.troskovnici
  ADD COLUMN IF NOT EXISTS odvjetnik jsonb,
  ADD COLUMN IF NOT EXISTS predmet jsonb,
  ADD COLUMN IF NOT EXISTS stranke jsonb;

-- Populate odvjetnik from existing flattened columns if present
UPDATE public.troskovnici
SET odvjetnik = jsonb_build_object(
  'naziv', COALESCE(odvjetnik_ime, ''),
  'oib', odvjetnik_oib,
  'adresa', odvjetnik_adresa,
  'iban', odvjetnik_iban
)
WHERE odvjetnik IS NULL;

-- Populate predmet from existing sud / broj_predmeta if present
UPDATE public.troskovnici
SET predmet = jsonb_build_object(
  'sud', sud,
  'poslovni_broj', broj_predmeta
)
WHERE predmet IS NULL;

-- Populate stranke object from existing flattened columns
UPDATE public.troskovnici
SET stranke = jsonb_build_object(
  'zastupana_stranka', jsonb_build_object('polozaj', 'zastupana', 'naziv', COALESCE(klijent_ime, ''), 'oib', klijent_oib, 'adresa', NULL),
  'protustranka', jsonb_build_object('polozaj', protustranka_polozaj, 'naziv', protustranka_ime, 'oib', protustranka_oib, 'adresa', protustranka_adresa)
)
WHERE stranke IS NULL;

COMMIT;

-- ═══════════ migrations/2026_02_25_add_pdv_and_party_fields.sql ═══════════
-- MIGRATION: Add PDV option and full party fields to troskovnici
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS has_vat BOOLEAN NOT NULL DEFAULT TRUE, -- true = obračunaj PDV
  ADD COLUMN IF NOT EXISTS is_pdv_ukljucen BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS odvjetnik_adresa TEXT,
  ADD COLUMN IF NOT EXISTS odvjetnik_iban TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_ime TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_oib TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_adresa TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_polozaj TEXT;

-- Optionally, add index for protustranka if needed
-- CREATE INDEX IF NOT EXISTS idx_troskovnici_protustranka_oib ON troskovnici (protustranka_oib);

-- ═══════════ migrations/2026_02_26_add_kazneni_fields.sql ═══════════
-- Add kazneni postupak fields to troskovnici table
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS zaprijecena_kazna text,
  ADD COLUMN IF NOT EXISTS is_po_sluzbenoj_duznosti boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS is_na_teret_proracuna boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS is_zastupnik_djeteta boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN troskovnici.zaprijecena_kazna IS 'Tier key: novcana, do_1, do_3, do_5, do_10, preko_10, dugotrajni';
COMMENT ON COLUMN troskovnici.is_po_sluzbenoj_duznosti IS 'Obrana po službenoj dužnosti – 50% reduction';
COMMENT ON COLUMN troskovnici.is_na_teret_proracuna IS 'Obrana na teret proračuna – 50% reduction';
COMMENT ON COLUMN troskovnici.is_zastupnik_djeteta IS 'Zastupnik djeteta – 70% of Tbr.4';

-- ═══════════ migrations/2026_03_02_add_kazneni_edge_cases.sql ═══════════
-- Add new kazneni edge-case fields to troskovnici table
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS is_stjecaj BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS is_oduzimanje_prekrsaj BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS vrijednost_oduzimanja NUMERIC DEFAULT 0;

-- ═══════════ migrations/2026_07_29_troskovnik_decimalni_bodovi.sql ═══════════
-- Bodovi odvjetničke tarife su decimalni.
--
-- Tarifa na više mjesta propisuje postotke jedne nagrade od druge (npr. Tbr. 18. toč. 1
-- daje 50% nagrade iz Tbr. 16 uvećano za 25%), a Tbr. 40. toč. 1 propisuje povišenje
-- od 10% po osobi na cijelu osnovicu. Ti umnošci ne daju cijele brojeve.
--
-- Dosadašnji INTEGER stupac tjerao je izračun da zaokružuje na svakom koraku lanca,
-- što je sustavno podizalo iznose (npr. 46,875 → 48 bodova umjesto 46,875).
--
-- NUMERIC(14,3): tri decimale su dovoljne za sve postotke koje Tarifa poznaje
-- (najsitniji korak je 1/8 boda iz lanca 0,5 × 1,25).

ALTER TABLE troskovnici
    ALTER COLUMN ukupno_bodova TYPE NUMERIC(14, 3) USING ukupno_bodova::NUMERIC(14, 3);

COMMENT ON COLUMN troskovnici.ukupno_bodova IS
    'Ukupno bodova po Tarifi HOK, uključujući povišenje po Tbr. 40. Decimalno — bodovi se ne zaokružuju.';

-- ═══════════ migrations/2026_08_12_fix_kazneni_column_comments.sql ═══════════
-- Ispravak komentara na kaznenim stupcima.
--
-- 2026_02_26_add_kazneni_fields.sql opisao je is_zastupnik_djeteta kao "70% of Tbr.4",
-- što je bilo nedorečeno, a kod je uz to stopu množio s Tbr. 4. toč. 8 i dobivao 35%.
-- Pravilnik o visini nagrade za rad odvjetnika prilikom obrana po službenoj dužnosti
-- (NN 22/2022, br. 271) ima dvije stope, obje od ISTE osnovice — "nagrade za rad koja bi
-- odvjetniku pripadala za pojedine radnje u postupku prema Tarifi":
--
--   čl. 1 — obrana po sl. dužnosti i obrana na teret proračunskih sredstava → 50%
--   čl. 2 — opunomoćenik djeteta žrtve kaznenog djela                       → 70%
--
-- Uloge se međusobno isključuju (obrana okrivljenika vs. zastupanje djeteta žrtve).
-- Samo komentari — struktura i podaci se ne diraju.

COMMENT ON COLUMN troskovnici.is_po_sluzbenoj_duznosti IS
  'Obrana po službenoj dužnosti – 50% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 1). Isključuje is_zastupnik_djeteta.';

COMMENT ON COLUMN troskovnici.is_na_teret_proracuna IS
  'Obrana na teret proračunskih sredstava – 50% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 1). Isključuje is_zastupnik_djeteta.';

COMMENT ON COLUMN troskovnici.is_zastupnik_djeteta IS
  'Opunomoćenik djeteta žrtve kaznenog djela – 70% nagrade po Tarifi za pojedinu radnju (Pravilnik NN 22/2022, čl. 2). Isključuje obje zastavice iz čl. 1.';

-- ═══════════ migrations/2026_08_23_add_waitlist.sql ═══════════
-- Lista čekanja za PravoTek landing (prijava e-mailom prije javnog izlaska).
--
-- Anonimni posjetitelj šalje samo e-mail; upis ide kroz
-- frontend/app/api/waitlist/route.ts (server-side, ali anon ključem, kao
-- ostatak frontenda — nema service-role ključa u aplikaciji). Zato RLS mora
-- dopustiti INSERT anonimnoj ulozi, ali ne i SELECT/UPDATE/DELETE — nitko
-- osim service_role ne smije čitati tuđe e-mailove.

CREATE TABLE IF NOT EXISTS waitlist (
    id          UUID        DEFAULT uuid_generate_v4() PRIMARY KEY,
    created_at  TIMESTAMPTZ DEFAULT now() NOT NULL,
    email       TEXT        NOT NULL,
    izvor       TEXT        NOT NULL DEFAULT 'landing'
);

DO $$ BEGIN
    ALTER TABLE waitlist ADD CONSTRAINT chk_waitlist_email_format
        CHECK (email ~* '^[^\s@]+@[^\s@]+\.[^\s@]{2,}$');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS idx_waitlist_email_unique ON waitlist (lower(email));

ALTER TABLE waitlist ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "service_role_full_access" ON waitlist
        FOR ALL USING (auth.role() = 'service_role');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Samo INSERT za anonimne posjetitelje — bez SELECT/UPDATE/DELETE, fail-closed.
DO $$ BEGIN
    CREATE POLICY "anon_can_join_waitlist" ON waitlist
        FOR INSERT
        TO anon
        WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ═══════════ migrations/2026_08_26_index_troskovnici_case_id.sql ═══════════
-- FK troskovnici.case_id nema pokrivajuci indeks (Supabase performance advisor).
--
-- Aditivno i nula-rizicno: ubrzava dohvat troskovnika po predmetu i provjeru
-- kaskade pri brisanju predmeta (case_id ima ON DELETE SET NULL). Pri danasnjoj
-- kolicini podataka (4 troskovnika) ne mijenja mjerljivo nista, ali sprjecava
-- sekvencijalni scan kad podataka bude vise. Ostala tri FK-a na tablici
-- (client_id, law_firm_id, user_id) vec imaju indeks.
create index if not exists idx_troskovnici_case_id on public.troskovnici (case_id);

-- ═══════════ migrations/2026_08_26_keep_alive_backend_warm.sql ═══════════
-- Keep-alive protiv Render cold starta.
--
-- Backend je na Render free tieru koji gasi instancu nakon ~15 min mirovanja;
-- prvi poziv nakon toga budi kontejner i traje 13-50 s (mjereno: 13.79 s prvi
-- poziv, 0.69 s topli). To je bio jedini uzrok koji vrijedi sekunde na
-- prijavi, dashboardu i troskovniku, jer svaki tok dodiruje backend.
--
-- pg_cron svakih 10 min zove javni /api/health, pa instanca nikad ne dosegne
-- prag uspavljivanja i cold start efektivno nestaje. Health je bez
-- autentikacije i vraca samo {status: healthy}, pa se ovime nista ne otkriva.
--
-- Timeout je 30 s (ne pg_net default od 5 s): kad backend jest hladan, ping s
-- 5 s istekne bas dok se instanca budi. S 30 s ping preziva cold start, doceka
-- 200 i drzi vezu dovoljno dugo da se buduci potvrdi. Kad je topao, ~0.5 s.
--
-- pg_net sam prunea odgovore starije od nekoliko sati, pa net._http_response
-- ne raste.
--
-- Trajniji popravak (izvan koda): Render Starter plan (~7 USD/mj) nema
-- spin-down, pa keep-alive ne bi ni trebao. Do tada ovo drzi backend toplim.

create extension if not exists pg_cron;
create extension if not exists pg_net;

-- Idempotentno: makni stari posao istog imena ako postoji, pa ga (ponovno) zakazi.
do $$
begin
  perform cron.unschedule('keep-backend-warm');
exception when others then
  null; -- posao jos ne postoji
end $$;

select cron.schedule(
  'keep-backend-warm',
  '*/10 * * * *',
  $ping$ select net.http_get(
    url := 'https://projectai-odvjetnik-fl8y.onrender.com/api/health',
    timeout_milliseconds := 30000
  ) $ping$
);

