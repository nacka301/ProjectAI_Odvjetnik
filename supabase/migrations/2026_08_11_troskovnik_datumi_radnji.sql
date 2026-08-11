-- Razdoblje koje troškovnik pokriva.
--
-- Troškovnik koji se predaje sudu mora uz svaku radnju imati datum kada je poduzeta.
-- Pojedinačni datumi putuju u raw_json_output, zajedno s ostalim podacima o stavkama —
-- tablica troskovnici nema stupac za stavke i ne dobiva ga ovom migracijom.
--
-- Ova dva stupca drže samo najraniji i najkasniji datum radnje, da se troškovnici mogu
-- tražiti po razdoblju ("svi s radnjama u 2025.") bez pretraživanja JSONB-a.
--
-- Postojeći troškovnici legitimno nemaju datume radnji i ostaju NULL — namjerno nema
-- UPDATE-a koji bi im podmetnuo created_at kao datum radnje.

ALTER TABLE troskovnici
    ADD COLUMN IF NOT EXISTS datum_od DATE,
    ADD COLUMN IF NOT EXISTS datum_do DATE;

COMMENT ON COLUMN troskovnici.datum_od IS
    'Najraniji datum radnje u troškovniku. NULL za troškovnike spremljene prije uvođenja datuma uz radnju.';

COMMENT ON COLUMN troskovnici.datum_do IS
    'Najkasniji datum radnje u troškovniku. NULL za troškovnike spremljene prije uvođenja datuma uz radnju.';

-- Popis troškovnika korisnika filtriran po razdoblju radnji.
CREATE INDEX IF NOT EXISTS idx_troskovnici_user_datum
    ON troskovnici (user_id, datum_od DESC);
