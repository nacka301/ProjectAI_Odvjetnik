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
