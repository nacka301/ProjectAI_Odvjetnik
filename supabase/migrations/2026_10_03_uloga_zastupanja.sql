-- Uloga zastupanja u kaznenom i prekršajnom troškovniku.
--
-- Koga odvjetnik zastupa određuje postotak Tarife na radnje iz Tbr. 3 i Tbr. 4:
-- BRANITELJ, PRIVATNI_TUZITELJ, OSTECENIK_KAO_TUZITELJ → puna nagrada;
-- PUNOMOCNIK_OSTECENIKA → 50% (Tbr. 3 t. 3, Tbr. 4 t. 8).
--
-- is_opunomocenik_zrtve zamjenjuje is_zastupnik_djeteta (NN 119/25 brisao je "djeteta");
-- staro polje ostaje dok frontend ne prijeđe. is_izabrani_oslobadjajuca do sada nije
-- bio pohranjen pa se pri učitavanju gubio.
--
-- Redoslijed: prvo dev baza, tek onda prod (uz backup).

ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS uloga_zastupanja text,
  ADD COLUMN IF NOT EXISTS is_opunomocenik_zrtve boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS is_izabrani_oslobadjajuca boolean NOT NULL DEFAULT false;

ALTER TABLE troskovnici
  DROP CONSTRAINT IF EXISTS troskovnici_uloga_zastupanja_check;
ALTER TABLE troskovnici
  ADD CONSTRAINT troskovnici_uloga_zastupanja_check
  CHECK (uloga_zastupanja IS NULL OR uloga_zastupanja IN
    ('BRANITELJ', 'PRIVATNI_TUZITELJ', 'OSTECENIK_KAO_TUZITELJ', 'PUNOMOCNIK_OSTECENIKA'));

-- Kopija postojeće zastavice.
UPDATE troskovnici SET is_opunomocenik_zrtve = is_zastupnik_djeteta
WHERE is_zastupnik_djeteta AND NOT is_opunomocenik_zrtve;

-- Stari kazneni zapisi: do sada je izračun računao kao branitelj (ili dijete žrtva → oštećenik).
UPDATE troskovnici
SET uloga_zastupanja = CASE WHEN is_zastupnik_djeteta THEN 'PUNOMOCNIK_OSTECENIKA' ELSE 'BRANITELJ' END
WHERE uloga_zastupanja IS NULL
  AND (zaprijecena_kazna IS NOT NULL OR is_po_sluzbenoj_duznosti OR is_na_teret_proracuna OR is_zastupnik_djeteta);

COMMENT ON COLUMN troskovnici.uloga_zastupanja IS
  'BRANITELJ | PRIVATNI_TUZITELJ | OSTECENIK_KAO_TUZITELJ | PUNOMOCNIK_OSTECENIKA (50% Tbr. 3 t. 3 / Tbr. 4 t. 8)';
