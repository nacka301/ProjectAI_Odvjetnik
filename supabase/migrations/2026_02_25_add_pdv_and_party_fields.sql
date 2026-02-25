-- MIGRATION: Add PDV option and full party fields to troskovnici
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS has_vat BOOLEAN NOT NULL DEFAULT TRUE, -- true = obračunaj PDV
  ADD COLUMN IF NOT EXISTS is_pdv_ukljucen BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS odvjetnik_adresa TEXT,
  ADD COLUMN IF NOT EXISTS odvjetnik_iban TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_ime TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_oib TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_adresa TEXT,
  ADD COLUMN IF NOT EXISTS protustranka_polozaj TEXT;

-- Optionally, add index for protustranka if needed
-- CREATE INDEX IF NOT EXISTS idx_troskovnici_protustranka_oib ON troskovnici (protustranka_oib);
