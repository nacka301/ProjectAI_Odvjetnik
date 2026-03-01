-- Add new kazneni edge-case fields to troskovnici table
ALTER TABLE troskovnici
  ADD COLUMN IF NOT EXISTS is_stjecaj BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS is_oduzimanje_prekrsaj BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS vrijednost_oduzimanja NUMERIC DEFAULT 0;
