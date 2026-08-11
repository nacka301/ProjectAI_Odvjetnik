-- ═══════════════════════════════════════════════════════════════════════
-- MIGRACIJA 017 — TOTP tajne izvan dosega korisnika
--
-- TOTP tajna i kodovi za oporavak stajali su u `user_profiles`. Nakon
-- migracije 016 korisnik smije čitati profile svog ureda — dakle i te stupce.
-- Šifrirani su, ali nemaju razloga uopće biti dohvatljivi: tko pročita tajnu,
-- generira valjane kodove i drugi faktor prestaje biti drugi faktor.
--
-- Nova tablica `user_mfa` ima RLS uključen i NIJEDNU korisničku politiku.
-- Do nje dolazi samo backend sa service_role ključem.
--
-- Format tajne se NE mijenja — postojeći authenticatori nastavljaju raditi
-- i nitko ne mora ponovno skenirati QR kod.
--
-- ZAHTIJEVA migracije 015 i 016.
-- ═══════════════════════════════════════════════════════════════════════


-- ────────────────────────────────────────────────────────────
-- KORAK 1: Nova tablica
--
-- FK ide na user_profiles(user_id), a ne na auth.users, kako bi PostgREST
-- mogao ugnijezditi user_mfa(totp_enabled) u upit nad user_profiles
-- (koristi ga admin pregled). Lanac brisanja i dalje vodi do auth.users.
-- ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS user_mfa (
    user_id                     UUID        PRIMARY KEY
                                            REFERENCES user_profiles(user_id) ON DELETE CASCADE,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    totp_enabled                BOOLEAN     NOT NULL DEFAULT FALSE,
    totp_secret_encrypted       TEXT,
    recovery_codes_encrypted    TEXT,
    totp_enabled_at             TIMESTAMPTZ
);

DROP TRIGGER IF EXISTS trg_user_mfa_updated_at ON user_mfa;
CREATE TRIGGER trg_user_mfa_updated_at
    BEFORE UPDATE ON user_mfa
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- ────────────────────────────────────────────────────────────
-- KORAK 2: Prijenos postojećih upisa
--
-- Stupci na user_profiles su nastali ad hoc (backend ih je upisivao preko
-- PostgREST-a), pa ne pretpostavljamo da postoje.
-- ────────────────────────────────────────────────────────────

DO $$
DECLARE
    moved BIGINT := 0;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'user_profiles'
          AND column_name = 'totp_secret_encrypted'
    ) THEN
        INSERT INTO user_mfa (
            user_id, totp_enabled, totp_secret_encrypted,
            recovery_codes_encrypted, totp_enabled_at
        )
        SELECT
            up.user_id,
            COALESCE(up.totp_enabled, FALSE),
            up.totp_secret_encrypted,
            up.recovery_codes_encrypted,
            up.totp_enabled_at
        FROM user_profiles up
        WHERE up.totp_secret_encrypted IS NOT NULL
           OR COALESCE(up.totp_enabled, FALSE) = TRUE
        ON CONFLICT (user_id) DO NOTHING;

        GET DIAGNOSTICS moved = ROW_COUNT;
        RAISE NOTICE 'Preneseno % 2FA zapisa u user_mfa', moved;
    ELSE
        RAISE NOTICE 'user_profiles nema TOTP stupce — nema se što prenijeti';
    END IF;
END $$;


-- ────────────────────────────────────────────────────────────
-- KORAK 3: Brisanje starih stupaca
--
-- Tek nakon prijenosa. Ako korak 2 nije ništa našao, ovo je no-op.
-- ────────────────────────────────────────────────────────────

ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_secret_encrypted;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS recovery_codes_encrypted;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_enabled;
ALTER TABLE user_profiles DROP COLUMN IF EXISTS totp_enabled_at;


-- ────────────────────────────────────────────────────────────
-- KORAK 4: RLS — zaključano
--
-- Nema politike za `authenticated`. To znači da korisnik kroz anon ključ ne
-- vidi ni vlastiti red, što je namjerno: nema legitimnog razloga da klijent
-- ikad pročita tajnu. Status 2FA saznaje kroz backend (/api/2fa/status).
-- ────────────────────────────────────────────────────────────

ALTER TABLE user_mfa ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "service_role_full_access" ON user_mfa;
CREATE POLICY "service_role_full_access" ON user_mfa
    FOR ALL USING (auth.role() = 'service_role');


-- ═══════════════════════════════════════════════════════════════════════
-- GOTOVO. Provjera:
--
--   -- Tajne više nisu na profilu:
--   SELECT column_name FROM information_schema.columns
--   WHERE table_schema='public' AND table_name='user_profiles'
--     AND column_name LIKE 'totp%';          -- mora vratiti 0 redova
--
--   -- Preneseni zapisi:
--   SELECT user_id, totp_enabled, totp_enabled_at FROM user_mfa;
--
--   -- Nijedna korisnička politika:
--   SELECT policyname, roles FROM pg_policies
--   WHERE tablename = 'user_mfa';            -- samo service_role_full_access
--
-- NAKON MIGRACIJE: prijavi se korisnikom koji ima 2FA i potvrdi da postojeći
-- authenticator i dalje daje prihvaćene kodove. Tajna se nije mijenjala, samo
-- preselila.
-- ═══════════════════════════════════════════════════════════════════════
