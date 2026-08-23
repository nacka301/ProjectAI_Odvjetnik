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
