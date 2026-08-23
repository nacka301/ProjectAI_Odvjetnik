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
