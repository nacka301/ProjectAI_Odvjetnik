-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 026 — Popravak migracije 023: pravo se oduzima i rolu PUBLIC
--
-- Zatečeno 16.9.2026.: migracija 023 nije ništa zatvorila. Postgres svakoj
-- novoj funkciji daje EXECUTE pseudo-roli PUBLIC, a PUBLIC nasljeđuje svaka
-- rola, pa i `anon`. `REVOKE ... FROM anon` briše samo izravno dodijeljeno
-- pravo; ono naslijeđeno preko PUBLIC ostaje. Na dev bazi to izgleda ovako:
--
--   purge_old_api_audit_logs  proacl = {=X/postgres, postgres=X/postgres,
--                                       service_role=X/postgres}
--   has_function_privilege('anon', …, 'EXECUTE') = true
--
-- Ono `=X/postgres` na početku je PUBLIC. Tako je i dalje bilo moguće javnim
-- anon ključem (koji je u JS paketu) pozvati
-- /rest/v1/rpc/purge_old_api_audit_logs i obrisati dnevnik pristupa, koji je
-- za odvjetnički ured dokazni materijal. Isto vrijedi za svih 16 funkcija
-- (Supabase advisor: anon_security_definer_function_executable).
--
-- Ispravan obrazac već stoji u migraciji 025 (potvrdi_pravne_tekstove):
-- oduzmi PUBLIC i anon, pa izričito dodijeli onome tko smije.
--
-- Raspodjela je ista kakvu je 023 namjeravala:
--
--   ODRŽAVANJE — samo service_role:
--     purge_old_api_audit_logs, expire_overdue_subscriptions,
--     handle_new_user, auto_provision_trial
--     Okidači ne trebaju GRANT: Postgres kod okidanja ne provjerava EXECUTE.
--     pg_cron poslove izvršava njihov vlasnik (postgres), a vlasnik funkcije
--     pravo ima uvijek.
--
--   KORISNIČKE — authenticated i service_role, anon ne:
--     search_clients, toggle_archive, get_dashboard_stats,
--     get_monthly_revenue, get_case_events_summary,
--     get_unread_notification_count, mark_notifications_read,
--     is_tenant_subscription_active, get_api_audit_logs, get_api_audit_stats
--     Migracija 019 im je dodala provjere (assert_is_self/assert_is_own_firm),
--     pa prijavljeni korisnik ne dobiva tuđe podatke.
--
--   POMOĆNE ZA RLS — authenticated OBAVEZNO:
--     user_firm_id, user_firm_role
--     Pozivaju se iz RLS politika u ime prijavljenog korisnika. Bez prava
--     pukne svaki upit nad predmetima, strankama i troškovnicima.
--
-- Funkcija koje u bazi nema (npr. starija shema) preskače se uz NOTICE, pa
-- migracija ne puca na bazi kojoj neka od prethodnih migracija nedostaje.
-- Na kraju je provjera koja PREKIDA migraciju ako anon i dalje smije zvati
-- neku od ovih funkcija ili ako authenticated izgubi user_firm_id/user_firm_role.
--
-- Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════

DO $$
DECLARE
    r        record;
    fn       regprocedure;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            -- ── Održavanje: nikome osim service_role ──
            ('public.purge_old_api_audit_logs(integer)',                                    'odrzavanje'),
            ('public.expire_overdue_subscriptions()',                                       'odrzavanje'),
            ('public.handle_new_user()',                                                    'odrzavanje'),
            ('public.auto_provision_trial()',                                               'odrzavanje'),

            -- ── Korisničke: prijavljeni da, neprijavljeni ne ──
            ('public.search_clients(uuid, text, integer)',                                  'korisnicka'),
            ('public.toggle_archive(text, uuid, uuid, boolean)',                            'korisnicka'),
            ('public.get_dashboard_stats(uuid)',                                            'korisnicka'),
            ('public.get_monthly_revenue(uuid, integer)',                                   'korisnicka'),
            ('public.get_case_events_summary(uuid, uuid)',                                  'korisnicka'),
            ('public.get_unread_notification_count(uuid)',                                  'korisnicka'),
            ('public.mark_notifications_read(uuid, uuid[])',                                'korisnicka'),
            ('public.is_tenant_subscription_active(uuid)',                                  'korisnicka'),
            ('public.get_api_audit_logs(uuid, timestamptz, timestamptz, integer, integer)', 'korisnicka'),
            ('public.get_api_audit_stats(uuid, integer)',                                   'korisnicka'),

            -- ── Pomoćne za RLS: authenticated OBAVEZNO ──
            ('public.user_firm_id()',                                                       'korisnicka'),
            ('public.user_firm_role()',                                                     'korisnicka')
        ) AS t(potpis, vrsta)
    LOOP
        fn := to_regprocedure(r.potpis);

        IF fn IS NULL THEN
            RAISE NOTICE 'Funkcija % ne postoji — preskačem', r.potpis;
            CONTINUE;
        END IF;

        -- PUBLIC je ono što je 023 propustila; anon se oduzima i izravno.
        EXECUTE format('REVOKE EXECUTE ON FUNCTION %s FROM PUBLIC, anon', fn);

        IF r.vrsta = 'odrzavanje' THEN
            EXECUTE format('REVOKE EXECUTE ON FUNCTION %s FROM authenticated', fn);
            EXECUTE format('GRANT  EXECUTE ON FUNCTION %s TO service_role', fn);
        ELSE
            EXECUTE format('GRANT  EXECUTE ON FUNCTION %s TO authenticated, service_role', fn);
        END IF;
    END LOOP;
END $$;


-- ── Provjera: ako nešto nije kako treba, migracija se prekida ──
DO $$
DECLARE
    potpis  text;
    fn      regprocedure;
BEGIN
    FOREACH potpis IN ARRAY ARRAY[
        'public.purge_old_api_audit_logs(integer)',
        'public.expire_overdue_subscriptions()',
        'public.handle_new_user()',
        'public.auto_provision_trial()',
        'public.search_clients(uuid, text, integer)',
        'public.toggle_archive(text, uuid, uuid, boolean)',
        'public.get_dashboard_stats(uuid)',
        'public.get_monthly_revenue(uuid, integer)',
        'public.get_case_events_summary(uuid, uuid)',
        'public.get_unread_notification_count(uuid)',
        'public.mark_notifications_read(uuid, uuid[])',
        'public.is_tenant_subscription_active(uuid)',
        'public.get_api_audit_logs(uuid, timestamptz, timestamptz, integer, integer)',
        'public.get_api_audit_stats(uuid, integer)',
        'public.user_firm_id()',
        'public.user_firm_role()'
    ]
    LOOP
        fn := to_regprocedure(potpis);
        CONTINUE WHEN fn IS NULL;

        IF has_function_privilege('anon', fn::oid, 'EXECUTE') THEN
            RAISE EXCEPTION 'anon i dalje smije zvati % — migracija 026 nije uspjela', potpis;
        END IF;
    END LOOP;

    FOREACH potpis IN ARRAY ARRAY['public.user_firm_id()', 'public.user_firm_role()']
    LOOP
        fn := to_regprocedure(potpis);
        CONTINUE WHEN fn IS NULL;

        IF NOT has_function_privilege('authenticated', fn::oid, 'EXECUTE') THEN
            RAISE EXCEPTION 'authenticated ne smije zvati % — RLS bi srušio svaki upit', potpis;
        END IF;
    END LOOP;

    IF to_regprocedure('public.purge_old_api_audit_logs(integer)') IS NOT NULL
       AND has_function_privilege('authenticated', 'public.purge_old_api_audit_logs(integer)', 'EXECUTE') THEN
        RAISE EXCEPTION 'authenticated i dalje smije brisati dnevnik (purge_old_api_audit_logs)';
    END IF;
END $$;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera nakon pokretanja:
--
--   -- anon_smije mora biti false u SVAKOM retku; authenticated_smije true
--   -- za korisničke i RLS pomoćne, false za četiri održavateljske:
--   SELECT p.proname,
--          p.proacl::text,
--          has_function_privilege('anon',          p.oid, 'EXECUTE') AS anon_smije,
--          has_function_privilege('authenticated', p.oid, 'EXECUTE') AS authenticated_smije
--     FROM pg_proc p
--     JOIN pg_namespace n ON n.oid = p.pronamespace
--    WHERE n.nspname = 'public' AND p.prosecdef
--    ORDER BY anon_smije DESC, p.proname;
--
--   -- U proacl više ne smije biti stavke koja počinje s „=X/" (to je PUBLIC).
--
-- I obavezno: prijavi se u aplikaciju i otvori troškovnike i stranke. Ako se
-- popisi vide, user_firm_id()/user_firm_role() rade.
-- ═══════════════════════════════════════════════════════════════════════
