/**
 * Idle / trajanje sesije — sve na jednom mjestu.
 *
 * Prijavljeni korisnik se odjavljuje kad frontend idle-tajmer istekne, a NE kad
 * istekne Supabase token (taj se tiho obnavlja dok je korisnik aktivan — vidi
 * SessionGuard). Ako David poslije poželi drukčiji idle prozor, mijenja se
 * `IDLE_TIMEOUT_MS` ovdje i nigdje drugdje.
 */

/** Vrijeme bez ikakve aktivnosti prije nego se pojavi upozorenje. */
export const IDLE_TIMEOUT_MS = 60 * 60 * 1000; // 60 min

/**
 * Odbrojavanje u modalu upozorenja. Upozorenje se pojavi ~1 min prije odjave:
 * tajmer istekne na 60:00 → modal → odjava na 61:00.
 */
export const WARNING_COUNTDOWN_SEC = 60; // 60 s

/**
 * Apsolutni maksimum trajanja sesije, bez obzira na aktivnost. Nakon ovoga se
 * svejedno traži ponovna prijava — aktivnost ga ne produžuje.
 */
export const ABSOLUTE_SESSION_MS = 12 * 60 * 60 * 1000; // 12 h

/** Guši rafal DOM događaja (mousemove / scroll) da reset ne okida na svaki piksel. */
export const ACTIVITY_THROTTLE_MS = 30_000; // 30 s

/**
 * Koliko često, dok je korisnik aktivan i kartica vidljiva, provjeriti ističe li
 * Supabase token uskoro i po potrebi ga tiho obnoviti.
 */
export const ACTIVE_REFRESH_MS = 10 * 60 * 1000; // 10 min

/** Ako token ističe unutar ovoga, obnovi ga sada. */
export const TOKEN_REFRESH_SKEW_MS = 15 * 60 * 1000; // 15 min

/** localStorage ključevi koje SessionGuard dijeli među karticama. */
export const LAST_ACTIVITY_KEY = "pravotek_last_activity";
export const SESSION_START_KEY = "pravotek_session_start";
