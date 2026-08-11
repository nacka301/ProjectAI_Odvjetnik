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
