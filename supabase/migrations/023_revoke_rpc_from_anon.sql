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
