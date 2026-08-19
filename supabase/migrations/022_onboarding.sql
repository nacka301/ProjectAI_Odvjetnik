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
