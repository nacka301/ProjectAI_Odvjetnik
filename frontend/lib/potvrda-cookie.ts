/**
 * Potpisani kolačić: ovaj korisnik je potvrdio OVU verziju pravnih tekstova.
 *
 * Plan pravne zaštite, dio C. Potvrda se sprema u bazu
 * (`potvrde_pravnih_tekstova`), a middleware je provjerava prije svake zaštićene
 * stranice. Da se baza ne pita pri svakom kliku, nakon prve uspješne provjere
 * izdaje se ovaj kolačić — isti obrazac kao `pt_mfa` za drugi faktor.
 *
 * Vrijednost: `{userId}.{verzija}.{hmac}`, gdje HMAC pokriva
 * `potvrda.{userId}.{verzija}`. Prefiks `potvrda.` odvaja potpis od potpisa
 * `pt_mfa` iako oba koriste isti tajni ključ: MFA potpis se ne može podmetnuti
 * kao potvrda ni obrnuto.
 *
 * Nema roka trajanja: potvrda verzije ne istječe. Kad se tekst promijeni,
 * mijenja se verzija u `lib/pravno.ts`, stari kolačić više ne odgovara i
 * middleware traži novu potvrdu.
 */
import { hmac, timingSafeEqual } from "@/lib/mfa-cookie";

export const POTVRDA_COOKIE = "pt_potvrda";

export function potvrdaCookieOptions() {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: 365 * 24 * 60 * 60,
  };
}

function potpisniNiz(userId: string, verzija: string): string {
  return `potvrda.${userId}.${verzija}`;
}

export async function signPotvrdaCookie(userId: string, verzija: string): Promise<string> {
  return `${userId}.${verzija}.${await hmac(potpisniNiz(userId, verzija))}`;
}

/** Točno za ispravno potpisan kolačić ovog korisnika i ove verzije. */
export async function verifyPotvrdaCookie(
  value: string | undefined,
  userId: string,
  verzija: string,
): Promise<boolean> {
  if (!value) return false;

  const parts = value.split(".");
  if (parts.length !== 3) return false;

  const [cookieUserId, cookieVerzija, potpis] = parts;
  if (cookieUserId !== userId || cookieVerzija !== verzija) return false;

  return timingSafeEqual(potpis, await hmac(potpisniNiz(cookieUserId, cookieVerzija)));
}
