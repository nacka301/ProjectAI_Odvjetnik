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
