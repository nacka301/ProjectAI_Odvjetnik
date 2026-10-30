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
export const PRAVNI_TEKSTOVI_VERZIJA = "2026-10-30";

/** Verzija onako kako stoji na stranici. */
export const PRAVNI_TEKSTOVI_VERZIJA_TEKST = "30. listopada 2026.";

/**
 * Jedna rečenica o tome što se u novoj verziji promijenilo — prikazuje se na
 * ekranu potvrde korisniku koji je potvrdio neku raniju verziju. `null` za prvu
 * verziju, kad ranije potvrde nema. Mijenja se zajedno s verzijom.
 */
export const PRAVNI_TEKSTOVI_IZMJENA: string | null =
  "Usluga je besplatna: uklonjene su odredbe o probnom razdoblju, isteku pristupa i uvođenju naplate.";

/** Ekran na koji middleware šalje korisnika koji tekuću verziju nije potvrdio. */
export const POTVRDA_RUTA = "/potvrda";

/** Pružatelj usluge je fizička osoba; e-pošta je jedini kanal za kontakt. */
export const PRUZATELJ_IME = "David Vacka";
export const PRUZATELJ_EMAIL = "vackadavid@gmail.com";
