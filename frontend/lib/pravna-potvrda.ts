/**
 * Je li prijavljeni korisnik potvrdio tekuću verziju Izjave o odgovornosti i
 * Uvjeta korištenja (plan pravne zaštite, dio C).
 *
 * Pita se baza (`potvrde_pravnih_tekstova`) preko PostgRESTa, s korisnikovim
 * tokenom — RLS mu dopušta čitati samo vlastite retke. `fetch` a ne Supabase
 * klijent, jer ovo zove i middleware (Edge), gdje je jedan lagani poziv dovoljan.
 *
 * Upis NIJE ovdje: ide isključivo kroz RPC `potvrdi_pravne_tekstove`, koji sam
 * uzima identitet i vrijeme (migracija 025), pa ga klijent ne može podmetnuti.
 */
import { SUPABASE_ANON_KEY, SUPABASE_URL } from "@/lib/supabase-env";

/**
 * - `potvrdeno`     — postoji redak za ovu verziju.
 * - `nije`          — baza je odgovorila, retka nema.
 * - `nepoznato`     — nismo mogli pitati (nema tokena, mreža, greška baze).
 *
 * `nepoznato` ne propušta dalje: stranica `/potvrda` tada sama provjeri još
 * jednom i, ako je potvrda već tu, vrati korisnika kamo je išao.
 */
export type StanjePotvrde = "potvrdeno" | "nije" | "nepoznato";

/** Odgovor PostgRESTa → stanje. Izdvojeno da se svaki slučaj može testirati. */
export function klasificirajPotvrdu(status: number, tijelo: unknown): StanjePotvrde {
  if (status < 200 || status >= 300) return "nepoznato";
  if (!Array.isArray(tijelo)) return "nepoznato";
  return tijelo.length > 0 ? "potvrdeno" : "nije";
}

export async function provjeriPotvrdu(
  accessToken: string | null,
  userId: string,
  verzija: string,
): Promise<StanjePotvrde> {
  if (!accessToken || !SUPABASE_URL || !SUPABASE_ANON_KEY) return "nepoznato";

  const url =
    `${SUPABASE_URL}/rest/v1/potvrde_pravnih_tekstova` +
    `?select=id&user_id=eq.${encodeURIComponent(userId)}` +
    `&verzija=eq.${encodeURIComponent(verzija)}&limit=1`;

  try {
    const res = await fetch(url, {
      headers: { apikey: SUPABASE_ANON_KEY, Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
      signal: AbortSignal.timeout(5_000),
    });
    const tijelo = res.ok ? await res.json() : null;
    return klasificirajPotvrdu(res.status, tijelo);
  } catch {
    return "nepoznato";
  }
}
