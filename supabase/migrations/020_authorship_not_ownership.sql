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
