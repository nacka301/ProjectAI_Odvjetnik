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
