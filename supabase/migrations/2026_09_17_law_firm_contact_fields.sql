-- ═══════════════════════════════════════════════════════════════════════
-- Telefon i e-mail ureda — za zaglavlje ispisa troškovnika
--
-- Ispis troškovnika koji ide sudu mora u zaglavlju imati kontaktne podatke
-- ureda (naziv, adresa, OIB, IBAN, telefon, e-mail). Prva četiri polja već
-- postoje na `law_firms`; telefon i e-mail ureda nedostaju — nemaju veze s
-- `user_profiles.email`, koji je korisnikov login mail i na dokument koji ide
-- sudu ne smije doći.
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE law_firms
    ADD COLUMN IF NOT EXISTS phone TEXT,
    ADD COLUMN IF NOT EXISTS email TEXT;

COMMENT ON COLUMN law_firms.phone IS
    'Telefon ureda za zaglavlje ispisa/izvoza troškovnika. Nije korisnikov broj iz postavki profila.';
COMMENT ON COLUMN law_firms.email IS
    'E-mail ureda za zaglavlje ispisa/izvoza troškovnika. Nije korisnikov login mail (user_profiles.email) — taj na dokument koji ide sudu ne smije doći.';
