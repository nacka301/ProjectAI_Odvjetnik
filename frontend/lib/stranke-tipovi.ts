/**
 * Oblik stranke, predmeta i troškovnika kakav backend vraća na `/api/client`.
 *
 * Ova su sučelja prije stajala dvaput — jednom u popisu stranaka, jednom u
 * dosjeu — i bila su znak po znak ista. Izdvojena su ovamo jer im se pridružio
 * treći potrošač: podaci za primjer (`lib/demo/podaci.ts`), koje po zadatku
 * moraju tipizirati POSTOJEĆI tipovi aplikacije. Da su ostala dva puta prepisana,
 * demo bi ih prepisao i treći put, a onda bi razlika između primjera i stvarnog
 * ekrana mogla proći kroz build neopaženo.
 */

export interface Client {
  id: string;
  created_at: string;
  updated_at: string;
  user_id: string;
  ime_naziv: string;
  oib: string | null;
  adresa: string | null;
  grad: string | null;
  postanski_broj: string | null;
  email: string | null;
  telefon: string | null;
  tip: string;
  napomena: string | null;
}

export interface Case {
  id: string;
  created_at: string;
  client_id: string | null;
  broj_predmeta: string;
  naziv: string;
  opis: string | null;
  vrsta: string;
  status: string;
  vrijednost_spora: number | null;
  sud: string | null;
  sudac: string | null;
  datum_pokretanja: string | null;
  datum_zatvaranja: string | null;
}

export interface Troskovnik {
  id: string;
  created_at: string;
  opis_predmeta: string | null;
  vrijednost_spora: number;
  ukupno_bodova: number;
  ukupno_eur_bez_pdv: number;
  pdv: number;
  ukupno_eur_s_pdv: number;
  klijent_ime: string | null;
  odvjetnik_ime: string | null;
}

/** Odgovor `/api/client/{id}/profile` — stranka i sve što visi o njoj. */
export interface ClientProfile {
  client: Client;
  cases: Case[];
  troskovnici: Troskovnik[];
}

/** Vrijednosti stupca `tip` u tablici `clients` — vidi popis stranaka. */
export type TipStranke = "fizicka" | "pravna";

// Oznake pravnih oblika: `d.o.o.`, `j.d.o.o.`, `d.d.`, `k.d.`, `j.t.d.` (uz razmake
// i s/bez točaka), te riječi koje same po sebi znače pravnu osobu.
const PRAVNI_OBLIK =
  /(\bd\s*\.?\s*o\s*\.?\s*o\b|\bj\s*\.?\s*d\s*\.?\s*o\s*\.?\s*o\b|\bd\s*\.?\s*d\b|\bk\s*\.?\s*d\b|\bj\s*\.?\s*t\s*\.?\s*d\b|\bd\s*\.?\s*n\s*\.?\s*o\b|\bustanova\b|\bzadruga\b|\budruga\b|\bzaklada\b|\bdruštvo\b|\bkomora\b|\bsindikat\b|\bministarstvo\b|\brepublika hrvatska\b|\bkonzorcij\b)/i;

/**
 * Pogađa je li naziv stranke pravna ili fizička osoba. Namijenjeno kao PRIJEDLOG
 * kod automatskog otvaranja stranke iz troškovnika — korisnik ga uvijek može
 * pregaziti. Bez naziva vraća `"fizicka"` (češći slučaj).
 */
export function naslutiTipStranke(naziv: string | null | undefined): TipStranke {
  return naziv && PRAVNI_OBLIK.test(naziv) ? "pravna" : "fizicka";
}
