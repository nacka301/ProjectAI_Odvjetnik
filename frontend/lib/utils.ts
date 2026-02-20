// ═══════════════════════════════════════════
//  OIB Validation — ISO 7064 MOD 11,10
//  Croatian personal identification number
// ═══════════════════════════════════════════

/**
 * Validates a Croatian OIB (Osobni identifikacijski broj).
 * Uses ISO 7064, MOD 11,10 algorithm.
 * @param oib - 11-digit string
 * @returns true if valid
 */
export function validateOib(oib: string): boolean {
  if (!oib || oib.length !== 11 || !/^\d{11}$/.test(oib)) return false;
  let remainder = 10;
  for (let i = 0; i < 10; i++) {
    remainder = (remainder + parseInt(oib[i])) % 10;
    if (remainder === 0) remainder = 10;
    remainder = (remainder * 2) % 11;
  }
  const checkDigit = (11 - remainder) % 10;
  return checkDigit === parseInt(oib[10]);
}

// ═══════════════════════════════════════════
//  Number Formatting (Croatian locale)
// ═══════════════════════════════════════════

/** Format a number with Croatian thousands separator (punkt) */
export function formatHR(n: number): string {
  return n.toLocaleString("hr-HR");
}

/** Format EUR amount */
export function formatEur(n: number): string {
  return `${formatHR(Math.round(n))} €`;
}

/** Format EUR/m² */
export function formatEurPerM2(n: number): string {
  return `${formatHR(Math.round(n))} €/m²`;
}

// ═══════════════════════════════════════════
//  Risk / Badge Color Mapping
// ═══════════════════════════════════════════

export type RiskLevel = "VISOK" | "SREDNJI" | "NIZAK";

export function riskToColor(risk: string): "red" | "amber" | "green" {
  switch (risk?.toUpperCase()) {
    case "VISOK":
    case "KRITIČNO":
      return "red";
    case "SREDNJI":
      return "amber";
    case "NIZAK":
    default:
      return "green";
  }
}

export function confidenceToColor(score: number): "green" | "amber" | "red" {
  if (score >= 70) return "green";
  if (score >= 40) return "amber";
  return "red";
}

export function confidenceToLabel(score: number): string {
  if (score >= 70) return "Pouzdano";
  if (score >= 40) return "Srednje";
  return "Nisko";
}

// ═══════════════════════════════════════════
//  PDV (VAT) Calculation
// ═══════════════════════════════════════════

export const PDV_RATES = [25, 13, 5, 0] as const;
export type PdvRate = (typeof PDV_RATES)[number];

export function calculatePdv(netAmount: number, pdvRate: number): {
  net: number;
  pdvAmount: number;
  gross: number;
} {
  const pdvAmount = Math.round(netAmount * (pdvRate / 100) * 100) / 100;
  return {
    net: netAmount,
    pdvAmount,
    gross: Math.round((netAmount + pdvAmount) * 100) / 100,
  };
}

// ═══════════════════════════════════════════
//  Location Helpers
// ═══════════════════════════════════════════

/**
 * Format a location string from city + district.
 * Used for display and API calls.
 */
export function formatLocation(city: string, district?: string): string {
  if (!city) return "";
  if (!district || district === city || district === "Centar") return city;
  return `${city}, ${district}`;
}
