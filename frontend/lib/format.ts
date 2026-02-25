export function formatEur(n: number): string {
  return n.toLocaleString('hr-HR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
}

export function formatNumber(n: number, digits = 2): string {
  return n.toLocaleString('hr-HR', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}
