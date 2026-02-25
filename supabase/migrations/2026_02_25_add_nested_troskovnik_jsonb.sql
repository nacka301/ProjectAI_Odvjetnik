-- Add nested JSONB columns for troškovnik (odvjetnik, predmet, stranke)

BEGIN;

ALTER TABLE IF EXISTS public.troskovnici
  ADD COLUMN IF NOT EXISTS odvjetnik jsonb,
  ADD COLUMN IF NOT EXISTS predmet jsonb,
  ADD COLUMN IF NOT EXISTS stranke jsonb;

-- Populate odvjetnik from existing flattened columns if present
UPDATE public.troskovnici
SET odvjetnik = jsonb_build_object(
  'naziv', COALESCE(odvjetnik_ime, ''),
  'oib', odvjetnik_oib,
  'adresa', odvjetnik_adresa,
  'iban', odvjetnik_iban
)
WHERE odvjetnik IS NULL;

-- Populate predmet from existing sud / broj_predmeta if present
UPDATE public.troskovnici
SET predmet = jsonb_build_object(
  'sud', sud,
  'poslovni_broj', broj_predmeta
)
WHERE predmet IS NULL;

-- Populate stranke object from existing flattened columns
UPDATE public.troskovnici
SET stranke = jsonb_build_object(
  'zastupana_stranka', jsonb_build_object('polozaj', 'zastupana', 'naziv', COALESCE(klijent_ime, ''), 'oib', klijent_oib, 'adresa', NULL),
  'protustranka', jsonb_build_object('polozaj', protustranka_polozaj, 'naziv', protustranka_ime, 'oib', protustranka_oib, 'adresa', protustranka_adresa)
)
WHERE stranke IS NULL;

COMMIT;
