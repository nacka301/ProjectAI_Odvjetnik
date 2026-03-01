-- Add kazneni postupak fields to troskovnici table
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS zaprijecena_kazna text,
  ADD COLUMN IF NOT EXISTS is_po_sluzbenoj_duznosti boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS is_na_teret_proracuna boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS is_zastupnik_djeteta boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN troskovnici.zaprijecena_kazna IS 'Tier key: novcana, do_1, do_3, do_5, do_10, preko_10, dugotrajni';
COMMENT ON COLUMN troskovnici.is_po_sluzbenoj_duznosti IS 'Obrana po službenoj dužnosti – 50% reduction';
COMMENT ON COLUMN troskovnici.is_na_teret_proracuna IS 'Obrana na teret proračuna – 50% reduction';
COMMENT ON COLUMN troskovnici.is_zastupnik_djeteta IS 'Zastupnik djeteta – 70% of Tbr.4';
