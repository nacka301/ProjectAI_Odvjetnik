-- Bodovi odvjetničke tarife su decimalni.
--
-- Tarifa na više mjesta propisuje postotke jedne nagrade od druge (npr. Tbr. 18. toč. 1
-- daje 50% nagrade iz Tbr. 16 uvećano za 25%), a Tbr. 40. toč. 1 propisuje povišenje
-- od 10% po osobi na cijelu osnovicu. Ti umnošci ne daju cijele brojeve.
--
-- Dosadašnji INTEGER stupac tjerao je izračun da zaokružuje na svakom koraku lanca,
-- što je sustavno podizalo iznose (npr. 46,875 → 48 bodova umjesto 46,875).
--
-- NUMERIC(14,3): tri decimale su dovoljne za sve postotke koje Tarifa poznaje
-- (najsitniji korak je 1/8 boda iz lanca 0,5 × 1,25).

ALTER TABLE troskovnici
    ALTER COLUMN ukupno_bodova TYPE NUMERIC(14, 3) USING ukupno_bodova::NUMERIC(14, 3);

COMMENT ON COLUMN troskovnici.ukupno_bodova IS
    'Ukupno bodova po Tarifi HOK, uključujući povišenje po Tbr. 40. Decimalno — bodovi se ne zaokružuju.';
