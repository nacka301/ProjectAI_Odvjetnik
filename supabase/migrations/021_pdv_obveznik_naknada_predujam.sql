-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 021 — PDV obveznik na razini ureda + naknada troškova i predujam
--
-- Dvije stvari:
--
-- 1. `law_firms.pdv_obveznik` — trajna postavka je li ured u sustavu PDV-a.
--    Ne zamjenjuje per-dokument `troskovnici.is_pdv_ukljucen`/`has_vat` (taj
--    checkbox ostaje uređiv), samo mu daje zadanu vrijednost na frontendu.
--
-- 2. `troskovnici.naknada_troskova_ukupno` / `predujmovi_ukupno` — sumarni
--    iznosi novih vrsta stavki (Tbr. 48.-51. naknada troškova ulazi u PDV
--    osnovicu; predujam je prolazna stavka, ne ulazi). Pojedinačne stavke
--    idu isključivo u postojeći `raw_json_output` JSONB, isto kao bodovni
--    retci i kao sudska pristojba — namjerno NEMA normaliziranih stupaca za
--    pojedinačne retke ni za sudsku pristojbu/sveukupni trošak, pa dosljedno
--    tome ni ovdje ne uvodimo novu tablicu.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE law_firms
    ADD COLUMN IF NOT EXISTS pdv_obveznik BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN law_firms.pdv_obveznik IS
    'Je li ured u sustavu PDV-a. Zadana vrijednost za novi troškovnik — korisnik je i dalje može ručno promijeniti po dokumentu.';

ALTER TABLE troskovnici
    ADD COLUMN IF NOT EXISTS naknada_troskova_ukupno NUMERIC(12,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS predujmovi_ukupno       NUMERIC(12,2) NOT NULL DEFAULT 0;

COMMENT ON COLUMN troskovnici.naknada_troskova_ukupno IS
    'Zbroj stvarnih izdataka po Tbr. 48.-51. (prijevoz, dnevnica, hotel, ostali izdaci) — ULAZI u PDV osnovicu (vlastiti trošak obavljanja usluge). Pojedinačne stavke su u raw_json_output.';
COMMENT ON COLUMN troskovnici.predujmovi_ukupno IS
    'Zbroj predujmova (npr. za vještačenje) — prolazna stavka po čl. 33. Zakona o PDV-u, NE ulazi u osnovicu, ne oporezuje se. Pojedinačne stavke su u raw_json_output.';
