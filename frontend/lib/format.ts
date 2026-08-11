export function formatEur(n: number): string {
  return n.toLocaleString('hr-HR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
}

export function formatNumber(n: number, digits = 2): string {
  return n.toLocaleString('hr-HR', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

/**
 * Format a VPS threshold value with exactly 2 decimal places and hr-HR locale.
 * Examples: 0 → "0,00", 332 → "332,00", 332.01 → "332,01"
 */
export function formatThreshold(n: number): string {
  return n.toLocaleString('hr-HR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/**
 * Datum radnje za prikaz — hrvatski oblik s vodećim nulama: 2025-03-12 → "12.03.2025.".
 *
 * `toLocaleDateString("hr-HR")` daje "12.3.2025." bez vodećih nula, što na troškovniku
 * koji ide sudu izgleda neuredno kad se datumi slažu jedan ispod drugoga.
 *
 * Prima ISO oblik `gggg-mm-dd` — u tom obliku datum putuje i sprema se. Prazan ili
 * neispravan ulaz daje prazan niz, da se u tablici ne pojavi "Invalid Date".
 */
export function formatirajDatum(iso: string | null | undefined): string {
  if (!iso) return '';

  const dijelovi = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
  if (!dijelovi) return '';

  const [, godina, mjesec, dan] = dijelovi;
  return `${dan}.${mjesec}.${godina}.`;
}
