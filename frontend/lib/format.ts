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
 * Datum onako kako stoji na troškovniku koji ide sudu: `11.04.2026`.
 *
 * `toLocaleDateString("hr-HR")` daje `11. 4. 2026.` — s razmacima, bez vodeće
 * nule i sa završnom točkom. Predložak koji odvjetnici prilažu piše brojeve
 * uz točku bez razmaka, pa se to ovdje slaže ručno.
 */
export function formatDatumIspis(d: Date): string {
  const dan = String(d.getDate()).padStart(2, '0');
  const mjesec = String(d.getMonth() + 1).padStart(2, '0');
  return `${dan}.${mjesec}.${d.getFullYear()}`;
}
