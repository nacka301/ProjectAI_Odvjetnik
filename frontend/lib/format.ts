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
