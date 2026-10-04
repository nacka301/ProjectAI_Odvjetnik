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
    napomena        TEXT,
    is_archived     BOOLEAN         NOT NULL DEFAULT FALSE
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
    is_opunomocenik_zrtve   BOOLEAN         NOT NULL DEFAULT FALSE,
    is_izabrani_oslobadjajuca BOOLEAN       NOT NULL DEFAULT FALSE,
    uloga_zastupanja        TEXT,
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
