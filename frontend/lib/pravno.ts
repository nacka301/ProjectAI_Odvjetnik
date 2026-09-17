/**
 * Zajednički podaci pravnih tekstova: verzija i kontakt pružatelja usluge.
 *
 * Verzija je jedna za Izjavu o odgovornosti i Uvjete korištenja, jer se Izjava
 * na Uvjete poziva kao na njihov sastavni dio. Kad se bilo koji od ta dva teksta
 * promijeni, mijenja se ovaj datum — i na obje stranice piše isti.
 *
 * Isti niz kasnije koristi potvrda pri prvoj prijavi (plan pravne zaštite, dio C):
 * korisnik potvrđuje točno ovu verziju, pa nova verzija traži novu potvrdu.
 */

/** ISO datum verzije — ujedno i ključ pod kojim se potvrda sprema u bazu. */
export const PRAVNI_TEKSTOVI_VERZIJA = "2026-09-17";

/** Verzija onako kako stoji na stranici. */
export const PRAVNI_TEKSTOVI_VERZIJA_TEKST = "17. rujna 2026.";

/** Pružatelj usluge je fizička osoba; e-pošta je jedini kanal za kontakt. */
export const PRUZATELJ_IME = "David Vacka";
export const PRUZATELJ_EMAIL = "vackadavid@gmail.com";
