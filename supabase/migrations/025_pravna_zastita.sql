-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 025 — Pravna zaštita: verzija propisa, potvrda uvjeta, prijave grešaka
--
-- Plan: docs/plan-pravna-zastita.md, dijelovi A, C i D. Ova migracija samo
-- priprema bazu; kod koji je koristi dolazi u sljedećim koracima.
--
--   A. Uz svaki spremljeni troškovnik piše po kojoj je verziji Tarife i Uredbe
--      izračunat i kada. Puni ga ISKLJUČIVO backend pri spremanju, iz istog
--      rezultata koji ide u raw_json_output — nikad iz tijela zahtjeva.
--   C. Evidencija tko je, kada i koju verziju pravnih tekstova potvrdio.
--      Samo dodavanje: nova verzija dodaje redak, stara se ne briše.
--   D. Prijave „Iznos mi ne izgleda točno". Čita ih samo backend (service_role).
--
-- NIŠTA SE NE BRIŠE I NIŠTA SE NE POPUNJAVA UNATRAG. Troškovnici spremljeni
-- prije ove migracije ostaju s NULL u novim stupcima i tako se i prikazuju
-- („propis nije zabilježen") — ne izmišljamo podatak koji nismo zapisali.
--
-- Brisanje korisnika i ureda (AccountService.DeleteFirmCoreAsync) i dalje radi:
--   - brisanje ureda kaskadom odnosi i njegove prijave grešaka,
--   - brisanje auth.users kaskadom odnosi potvrde, a u prijavama postavlja
--     autora na NULL (obrazac iz migracije 020: autorstvo, ne vlasništvo).
--
-- Redoslijed: prvo dev baza, provjera i get_advisors, pa backup, pa produkcija.
-- Sigurna za višekratno pokretanje.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- DIO A: verzija propisa uz troškovnik
--
-- Nazivi se spremaju kao TEKST, ne kao poveznica na konstantu u kodu: kad se
-- propis promijeni i kod dobije novu verziju, stari zapis i dalje nosi naziv
-- po kojem je stvarno izračunat.
--
-- uredbe_pristojbe je polje jer jedan troškovnik može imati retke po dvije
-- Uredbe (čl. 2 Uredbe NN 50/2026 zadržava NN 37/2023 za odgovor na tužbu i
-- upravni spor pokrenut prije 1.6.2026.).
-- ────────────────────────────────────────────────────────────

ALTER TABLE public.troskovnici
    ADD COLUMN IF NOT EXISTS izracunato_at     timestamptz,
    ADD COLUMN IF NOT EXISTS tarifa_naziv      text,
    ADD COLUMN IF NOT EXISTS tarifa_vrijedi_od date,
    ADD COLUMN IF NOT EXISTS vrijednost_boda   numeric(10,2),
    ADD COLUMN IF NOT EXISTS uredbe_pristojbe  text[],
    ADD COLUMN IF NOT EXISTS verzija_izracuna  text;

COMMENT ON COLUMN public.troskovnici.izracunato_at IS
    'Trenutak izračuna na serveru pri spremanju. NULL = spremljeno prije migracije 025.';
COMMENT ON COLUMN public.troskovnici.tarifa_naziv IS
    'Naziv verzije Tarife po kojoj je izračun napravljen (TarifaVerzija.Naziv). Puni backend. NULL = spremljeno prije migracije 025.';
COMMENT ON COLUMN public.troskovnici.tarifa_vrijedi_od IS
    'Datum od kojeg je ta verzija Tarife na snazi (TarifaVerzija.VrijediOd).';
COMMENT ON COLUMN public.troskovnici.vrijednost_boda IS
    'Vrijednost boda u eurima primijenjena u izračunu (TarifaVerzija.BodValue).';
COMMENT ON COLUMN public.troskovnici.uredbe_pristojbe IS
    'Različite Uredbe o Tarifi sudskih pristojbi stvarno primijenjene na retke. Prazno polje = nije bilo sudskih pristojbi.';
COMMENT ON COLUMN public.troskovnici.verzija_izracuna IS
    'Git commit backenda koji je izračunao (RENDER_GIT_COMMIT), da se prijavljena greška može reproducirati.';


-- ────────────────────────────────────────────────────────────
-- DIO C: evidencija potvrde pravnih tekstova
--
-- Klijent smije ČITATI samo vlastite retke (da zna je li već potvrdio), ali
-- ne smije sam upisivati: tada bi mogao podmetnuti tuđi user_id ili datum.
-- Upis ide kroz RPC koji sam uzima auth.uid() i now().
-- ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.potvrde_pravnih_tekstova (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    verzija      text NOT NULL CHECK (length(verzija) BETWEEN 1 AND 40),
    potvrdeno_at timestamptz NOT NULL DEFAULT now(),
    user_agent   text CHECK (user_agent IS NULL OR length(user_agent) <= 300),
    CONSTRAINT uq_potvrde_user_verzija UNIQUE (user_id, verzija)
);

COMMENT ON TABLE public.potvrde_pravnih_tekstova IS
    'Tko je, kada i koju verziju Izjave o odgovornosti i Uvjeta potvrdio. Samo dodavanje; upis kroz potvrdi_pravne_tekstove().';

ALTER TABLE public.potvrde_pravnih_tekstova ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "potvrde_select_own" ON public.potvrde_pravnih_tekstova;
CREATE POLICY "potvrde_select_own" ON public.potvrde_pravnih_tekstova
    FOR SELECT TO authenticated
    USING (user_id = (SELECT auth.uid()));  -- SELECT: računa se jednom po upitu, ne po retku

-- Nema INSERT/UPDATE/DELETE politike. Uz to i GRANT-ovi, da tablica ne ovisi
-- samo o tome da netko jednom ne doda preširoku politiku.
REVOKE ALL ON public.potvrde_pravnih_tekstova FROM anon;
REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public.potvrde_pravnih_tekstova FROM authenticated;
GRANT SELECT ON public.potvrde_pravnih_tekstova TO authenticated;

CREATE OR REPLACE FUNCTION public.potvrdi_pravne_tekstove(
    p_verzija    text,
    p_user_agent text DEFAULT NULL
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
BEGIN
    IF auth.uid() IS NULL THEN
        RAISE EXCEPTION 'Potvrda je moguća samo prijavljenom korisniku.'
            USING ERRCODE = '42501';
    END IF;

    INSERT INTO public.potvrde_pravnih_tekstova (user_id, verzija, user_agent)
    VALUES (auth.uid(), p_verzija, left(p_user_agent, 300))
    ON CONFLICT (user_id, verzija) DO NOTHING;
END $$;

-- Obrazac iz migracije 023: neprijavljeni ne, prijavljeni da.
REVOKE ALL     ON FUNCTION public.potvrdi_pravne_tekstove(text, text) FROM PUBLIC, anon;
GRANT  EXECUTE ON FUNCTION public.potvrdi_pravne_tekstove(text, text) TO authenticated;


-- ────────────────────────────────────────────────────────────
-- DIO D: prijave grešaka u izračunu
--
-- Zaključano kao user_mfa (017) i waitlist: nema politike za authenticated,
-- korisnik ne čita ni tuđe ni vlastite prijave. Upisuje i čita samo backend.
--
-- ulazni_podaci NE SMIJU sadržavati podatke stranaka ni odvjetnika (imena,
-- OIB, adrese) — backend ih uklanja prije upisa. Ovdje se to ne može provjeriti,
-- pa je pravilo zapisano u komentaru stupca.
-- ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.prijave_gresaka (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at       timestamptz NOT NULL DEFAULT now(),
    user_id          uuid REFERENCES auth.users(id) ON DELETE SET NULL,
    law_firm_id      uuid NOT NULL REFERENCES public.law_firms(id) ON DELETE CASCADE,
    troskovnik_id    uuid REFERENCES public.troskovnici(id) ON DELETE SET NULL,
    opis             text NOT NULL CHECK (length(opis) BETWEEN 10 AND 2000),
    ocekivani_iznos  text CHECK (ocekivani_iznos IS NULL OR length(ocekivani_iznos) <= 200),
    ulazni_podaci    jsonb,
    rezultat         jsonb,
    verzija_izracuna text,
    status           text NOT NULL DEFAULT 'nova'
                     CONSTRAINT chk_prijave_status
                     CHECK (status IN ('nova', 'potvrdena', 'nije_greska', 'ispravljena')),
    email_poslan     boolean NOT NULL DEFAULT false
);

COMMENT ON TABLE public.prijave_gresaka IS
    'Prijave „Iznos mi ne izgleda točno". Samo service_role (backend).';
COMMENT ON COLUMN public.prijave_gresaka.ulazni_podaci IS
    'Ulaz izračuna (stavke, VPS, datumi, zastavice) BEZ podataka stranaka i odvjetnika — backend ih uklanja prije upisa.';
COMMENT ON COLUMN public.prijave_gresaka.email_poslan IS
    'Je li obavijest adminu otišla. Prijava se sprema i kad mail ne uspije.';

CREATE INDEX IF NOT EXISTS idx_prijave_gresaka_status_created
    ON public.prijave_gresaka (status, created_at DESC);
-- Indeksi na stranim ključevima: brisanje ureda, troškovnika i korisnika
-- inače bi za svaku kaskadu skeniralo cijelu tablicu.
CREATE INDEX IF NOT EXISTS idx_prijave_gresaka_law_firm_id   ON public.prijave_gresaka (law_firm_id);
CREATE INDEX IF NOT EXISTS idx_prijave_gresaka_troskovnik_id ON public.prijave_gresaka (troskovnik_id);
CREATE INDEX IF NOT EXISTS idx_prijave_gresaka_user_id       ON public.prijave_gresaka (user_id);

ALTER TABLE public.prijave_gresaka ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON public.prijave_gresaka;
CREATE POLICY "service_role_full_access" ON public.prijave_gresaka
    FOR ALL USING ((SELECT auth.role()) = 'service_role');

REVOKE ALL ON public.prijave_gresaka FROM anon, authenticated;


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Šest novih stupaca na troskovnici:
--   SELECT column_name, data_type FROM information_schema.columns
--    WHERE table_schema = 'public' AND table_name = 'troskovnici'
--      AND column_name IN ('izracunato_at','tarifa_naziv','tarifa_vrijedi_od',
--                          'vrijednost_boda','uredbe_pristojbe','verzija_izracuna');
--
--   -- RLS uključen na obje nove tablice (oba retka true):
--   SELECT relname, relrowsecurity FROM pg_class
--    WHERE relname IN ('potvrde_pravnih_tekstova', 'prijave_gresaka');
--
--   -- anon ne smije zvati RPC (false), authenticated smije (true):
--   SELECT has_function_privilege('anon', 'public.potvrdi_pravne_tekstove(text,text)', 'EXECUTE'),
--          has_function_privilege('authenticated', 'public.potvrdi_pravne_tekstove(text,text)', 'EXECUTE');
--
--   -- Klijent ne smije upisivati potvrde ni dirati prijave (sve false):
--   SELECT has_table_privilege('authenticated', 'public.potvrde_pravnih_tekstova', 'INSERT'),
--          has_table_privilege('anon',          'public.potvrde_pravnih_tekstova', 'SELECT'),
--          has_table_privilege('authenticated', 'public.prijave_gresaka', 'SELECT'),
--          has_table_privilege('authenticated', 'public.prijave_gresaka', 'INSERT');
-- ═══════════════════════════════════════════════════════════════════════
