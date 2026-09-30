-- ═══════════════════════════════════════════════════════════════════════
-- Parametri za izračun naknade troškova (Tbr. 50 — automobil, Tbr. 51 — dnevnica)
--
-- Tbr. 50: naknada za automobil = 30% cijene litre bezolovnog benzina 100
-- oktana po prijeđenom kilometru + cestarina. Koeficijent 0,30 je propisan
-- (TarifaConstants.Tbr50FaktorGoriva), ali cijena goriva je TRŽIŠNA, nije
-- regulirana Uredbom o najvišim cijenama (ta pokriva samo 95/dizel) — mora
-- biti podesiva po uredu, nikad zakucana u kodu.
--
-- Tbr. 51: dnevnica u visini dnevnice suca Županijskog suda. Iznos je pravno
-- izveden, ali NEPOTVRĐEN (najbolji nađeni izvor 19,91 € iz NN 117/2012, bez
-- potvrde postoji li noviji propis) — ured ga sam potvrđuje i unosi.
--
-- Obje kolone su nullable bez defaulta: NULL = nepotvrđeno/nepostavljeno,
-- izračun se tada NE smije automatski provesti, samo ručni unos iznosa.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE law_firms
    ADD COLUMN IF NOT EXISTS cijena_goriva_eur_l NUMERIC(6,3),
    ADD COLUMN IF NOT EXISTS dnevnica_eur NUMERIC(8,2);

COMMENT ON COLUMN law_firms.cijena_goriva_eur_l IS
    'Tbr. 50 — cijena litre bezolovnog benzina 100 oktana (EUR), za izračun naknade za automobil (30% cijene po km). Tržišna vrijednost, ured je sam unosi i ažurira. NULL = nepostavljeno, naknada za prijevoz se tada ne može automatski izračunati.';
COMMENT ON COLUMN law_firms.dnevnica_eur IS
    'Tbr. 51 — dnevnica u visini dnevnice suca Županijskog suda (EUR/dan). Pravno izveden ali NEPOTVRĐEN iznos — ured ga potvrđuje i unosi sam. NULL = nepostavljeno, naknada za dnevnicu se tada unosi samo ručno.';
