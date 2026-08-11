-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 018 — Privatni bucketi i pristup po uredu
--
-- `zk-izvadci` se u aplikaciji čitao preko getPublicUrl(), što radi samo na
-- javnom bucketu. U njemu su zemljišnoknjižni izvadci: imena, OIB-ovi,
-- katastarske čestice. Javan bucket znači da je svaki taj dokument dostupan
-- svakome tko pogodi ili sazna URL — bez prijave, bez traga u dnevniku.
--
-- Ova migracija zatvara bucket i uvodi pravilo iz dokumenta (§11): prvi segment
-- putanje je law_firm_id, pa se politika svodi na jednu usporedbu.
--
-- ⚠ RUŠI POSTOJEĆE JAVNE LINKOVE. Svaki spremljeni `pdf_url` prestaje raditi.
--   Dogovoreno kao prihvatljivo — u bazi su samo testni podaci.
--
-- ZAHTIJEVA migracije 015 i 016 (user_firm_id / user_firm_role helperi).
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Bucketi — postoje i privatni su
-- ────────────────────────────────────────────────────────────

INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES ('zk-izvadci', 'zk-izvadci', FALSE, 52428800)
ON CONFLICT (id) DO UPDATE
    SET public = FALSE,
        file_size_limit = EXCLUDED.file_size_limit;

INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES ('case-documents', 'case-documents', FALSE, 52428800)
ON CONFLICT (id) DO UPDATE
    SET public = FALSE,
        file_size_limit = EXCLUDED.file_size_limit;


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Uklanjanje zatečenih politika nad ovim bucketima
--
-- Imena ne znamo unaprijed — nastala su klikanjem po dashboardu. Zato ih
-- tražimo po sadržaju izraza, i to samo one koje spominju naša dva bucketa;
-- politike drugih bucketa ostaju netaknute.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    p RECORD;
BEGIN
    FOR p IN
        SELECT policyname
        FROM pg_policies
        WHERE schemaname = 'storage'
          AND tablename = 'objects'
          AND (
              COALESCE(qual, '')       LIKE '%zk-izvadci%'
           OR COALESCE(with_check, '') LIKE '%zk-izvadci%'
           OR COALESCE(qual, '')       LIKE '%case-documents%'
           OR COALESCE(with_check, '') LIKE '%case-documents%'
          )
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS %I ON storage.objects;', p.policyname);
        RAISE NOTICE 'Uklonjena zatečena storage politika: %', p.policyname;
    END LOOP;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: Politike po uredu
--
-- Konvencija putanje: {law_firm_id}/{uuid}-{filename}
-- storage.foldername(name) vraća polje segmenata, pa je [1] uvijek ured.
-- Datoteka spremljena bez tog prefiksa nije čitljiva nikome osim service_role —
-- fail-closed, kako i treba biti.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    b TEXT;
    buckets TEXT[] := ARRAY['zk-izvadci', 'case-documents'];
BEGIN
    FOREACH b IN ARRAY buckets
    LOOP
        -- Čitanje: svi članovi ureda, uključujući readonly.
        EXECUTE format(
            'CREATE POLICY "%s_select_firm" ON storage.objects '
            'FOR SELECT TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text);',
            b, b);

        -- Upload: readonly ne smije.
        EXECUTE format(
            'CREATE POLICY "%s_insert_firm" ON storage.objects '
            'FOR INSERT TO authenticated '
            'WITH CHECK (bucket_id = %L '
            '            AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '            AND public.user_firm_role() <> ''readonly'');',
            b, b);

        -- Prepisivanje postojeće datoteke.
        EXECUTE format(
            'CREATE POLICY "%s_update_firm" ON storage.objects '
            'FOR UPDATE TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '       AND public.user_firm_role() <> ''readonly'') '
            'WITH CHECK (bucket_id = %L '
            '            AND (storage.foldername(name))[1] = public.user_firm_id()::text);',
            b, b, b);

        EXECUTE format(
            'CREATE POLICY "%s_delete_firm" ON storage.objects '
            'FOR DELETE TO authenticated '
            'USING (bucket_id = %L '
            '       AND (storage.foldername(name))[1] = public.user_firm_id()::text '
            '       AND public.user_firm_role() <> ''readonly'');',
            b, b);
    END LOOP;
END $$;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Oba bucketa privatna:
--   SELECT id, public, file_size_limit FROM storage.buckets
--   WHERE id IN ('zk-izvadci', 'case-documents');
--
--   -- Politike:
--   SELECT policyname, cmd, roles FROM pg_policies
--   WHERE schemaname = 'storage' AND tablename = 'objects'
--   ORDER BY policyname;
--
-- TEST IZOLACIJE:
--   1. Kao korisnik ureda A uploadaj datoteku na putanju {ured-A}/test.pdf.
--   2. Kao korisnik ureda B pokušaj createSignedUrl za tu putanju → mora pući.
--   3. Otvori stari javni URL nekog ZK izvatka → mora vratiti 400/404.
--      Ako se i dalje otvara, bucket nije zatvoren — provjeri dashboard.
-- ═══════════════════════════════════════════════════════════════════════
