/* eslint-disable @typescript-eslint/no-explicit-any */
import { createElement } from "react";
import type { ValuationResult } from "@/app/wizard/types";

// ═══════════════════════════════════════════════════
//  PDF EXPORT — @react-pdf/renderer (lazy-loaded)
//  Professional valuation report with full Croatian
//  diacritics support (č, ć, š, ž, đ)
//
//  @react-pdf/renderer is loaded dynamically to prevent
//  Next.js from bundling pdfkit's Node.js deps at build time.
// ═══════════════════════════════════════════════════

// Cached module & styles — initialized on first call
let R: any = null;
let S: any = null;

async function ensurePdfEngine() {
  if (R) return;
  R = await import("@react-pdf/renderer");

  R.Font.register({
    family: "Roboto",
    fonts: [
      { src: "https://fonts.gstatic.com/s/roboto/v47/KFOMCnqEu92Fr1ME7kSn66aGLdTylUAMQXC89YmC2DPNWubEbGmT.ttf", fontWeight: 400 },
      { src: "https://fonts.gstatic.com/s/roboto/v47/KFOMCnqEu92Fr1ME7kSn66aGLdTylUAMQXC89YmC2DPNWuaabWmT.ttf", fontWeight: 700 },
      { src: "https://fonts.gstatic.com/s/roboto/v47/KFOMCnqEu92Fr1ME7kSn66aGLdTylUAMQXC89YmC2DPNWuYKbGmT.ttf", fontWeight: 400, fontStyle: "italic" },
    ],
  });
  R.Font.registerHyphenationCallback((word: string) => [word]);

  S = R.StyleSheet.create({
    page: { fontFamily: "Roboto", fontSize: 9, color: "#1e1e1e", paddingTop: 30, paddingBottom: 60, paddingHorizontal: 36 },
    header: { backgroundColor: "#1e40af", marginHorizontal: -36, marginTop: -30, paddingVertical: 18, paddingHorizontal: 36, marginBottom: 16 },
    headerTitle: { color: "#ffffff", fontSize: 17, fontWeight: 700, marginBottom: 4 },
    headerSub: { color: "#bfdbfe", fontSize: 9 },
    sectionTitle: { fontSize: 11, fontWeight: 700, color: "#1e3a5f", marginTop: 14, marginBottom: 6, paddingBottom: 2, borderBottomWidth: 1, borderBottomColor: "#cbd5e1" },
    infoBox: { flexDirection: "row", backgroundColor: "#f8fafc", border: "1pt solid #e2e8f0", borderRadius: 3, padding: 10, marginBottom: 10 },
    infoCol: { flex: 1 },
    infoLabel: { fontSize: 7.5, fontWeight: 700, color: "#64748b", textTransform: "uppercase", marginBottom: 2 },
    infoValue: { fontSize: 10, color: "#1e293b" },
    heroBox: { backgroundColor: "#1e40af", borderRadius: 4, padding: 14, flexDirection: "row", alignItems: "center", justifyContent: "space-between", marginBottom: 12 },
    heroLabel: { color: "#ffffff", fontSize: 11, fontWeight: 700 },
    heroValue: { color: "#ffffff", fontSize: 20, fontWeight: 700 },
    heroSub: { color: "#93c5fd", fontSize: 8, marginTop: 2 },
    tableHead: { flexDirection: "row", backgroundColor: "#1e40af", borderTopLeftRadius: 2, borderTopRightRadius: 2, paddingVertical: 5, paddingHorizontal: 4 },
    tableHeadCell: { color: "#ffffff", fontSize: 7.5, fontWeight: 700 },
    tableRow: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 4, borderBottomWidth: 0.5, borderBottomColor: "#e2e8f0" },
    tableRowAlt: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 4, backgroundColor: "#f8fafc", borderBottomWidth: 0.5, borderBottomColor: "#e2e8f0" },
    tableCell: { fontSize: 8, color: "#334155" },
    tableCellBold: { fontSize: 8, fontWeight: 700, color: "#1e293b" },
    paragraph: { fontSize: 9, lineHeight: 1.5, color: "#334155", marginBottom: 4 },
    badge: { alignSelf: "flex-start", paddingVertical: 3, paddingHorizontal: 8, borderRadius: 10, marginBottom: 6 },
    badgeText: { fontSize: 8, fontWeight: 700, color: "#ffffff" },
    footer: { position: "absolute", bottom: 20, left: 36, right: 36 },
    footerLine: { borderTopWidth: 0.5, borderTopColor: "#cbd5e1", paddingTop: 4, flexDirection: "row", justifyContent: "space-between" },
    footerText: { fontSize: 6.5, color: "#94a3b8" },
    footerDisclaimer: { fontSize: 6, color: "#94a3b8", marginTop: 2 },
  });
}

// ── Shared Helpers ──

interface PdfParams {
  result: ValuationResult;
  propertyType: string;
  location: string;
  areaM2: number;
}

const fmt = (n: number) =>
  n.toLocaleString("hr-HR", { maximumFractionDigits: 0 });

const typeLabel: Record<string, string> = {
  Apartment: "Stan",
  House: "Kuća",
  Land: "Zemljište",
};

const h = createElement;

// ── Document Component (uses createElement — not JSX in .ts files) ──

function ValuationPdfDocument({ result, propertyType, location, areaM2 }: PdfParams) {
  const today = new Date().toLocaleDateString("hr-HR");

  // Key metrics
  const metrics: [string, string][] = [
    ["Cijena po m²", result.calculated_price_per_m2 ? `${fmt(Math.round(result.calculated_price_per_m2))} EUR/m²` : "N/A"],
    ["Pronađeno oglasa", String(result.total_listings_found ?? "N/A")],
    ["Korištenih usporedbi", String(result.comparison_table?.length ?? result.usable_price_points ?? "N/A")],
    ["Uklonjeno outliera", String(result.outliers_removed ?? 0)],
    ["Diskont tražene cijene", `${result.realization_discount_percent?.toFixed(1) ?? "15"}%`],
    ["Pouzdanost", result.math_confidence_score !== undefined ? `${result.math_confidence_score}/100 (${result.math_confidence_level ?? ""})` : "N/A"],
  ];
  if (result.market_average_asking) {
    metrics.push(["Prosjek traženih cijena/m²", `${fmt(Math.round(result.market_average_asking))} EUR/m²`]);
  }

  const badgeColor = (result.math_confidence_score ?? 0) >= 70 ? "#16a34a"
    : (result.math_confidence_score ?? 0) >= 40 ? "#ca8a04" : "#dc2626";

  // Table helpers (inline — small functions are fine here)
  const thRow = (cells: string[], widths: string[]) =>
    h(R.View, { style: S.tableHead },
      ...cells.map((c, i) =>
        h(R.Text, { key: String(i), style: { ...S.tableHeadCell, width: widths[i], textAlign: i >= 2 ? ("right" as const) : ("left" as const) } }, c)
      )
    );

  const tdRow = (cells: string[], widths: string[], isAlt: boolean, boldCols?: number[]) =>
    h(R.View, { style: isAlt ? S.tableRowAlt : S.tableRow, wrap: false },
      ...cells.map((c, i) =>
        h(R.Text, {
          key: String(i),
          style: {
            ...(boldCols?.includes(i) ? S.tableCellBold : S.tableCell),
            width: widths[i],
            textAlign: i >= 2 ? ("right" as const) : ("left" as const),
          },
        }, c)
      )
    );

  return h(R.Document, { title: "Procjena tržišne vrijednosti", author: "PravoTek" },
    h(R.Page, { size: "A4", style: S.page, wrap: true },

      // Footer (fixed on every page)
      h(R.View, { style: S.footer, fixed: true },
        h(R.View, { style: S.footerLine },
          h(R.Text, { style: S.footerText }, "PravoTek — Automatizirana tržišna procjena nekretnina"),
          h(R.Text, { style: S.footerText, render: ({ pageNumber, totalPages }: { pageNumber: number; totalPages: number }) => `Stranica ${pageNumber} / ${totalPages}` }),
        ),
        h(R.Text, { style: S.footerDisclaimer }, "Ovaj izvještaj je generiran automatski i ne predstavlja službenu procjenu ovlaštenog sudskog vještaka."),
      ),

      // Header
      h(R.View, { style: S.header },
        h(R.Text, { style: S.headerTitle }, "Izvještaj o procjeni tržišne vrijednosti"),
        h(R.Text, { style: S.headerSub }, `Datum: ${today}  •  PravoTek — Automatizirana tržišna procjena`),
      ),

      // Property info
      h(R.View, { style: S.infoBox },
        h(R.View, { style: S.infoCol },
          h(R.Text, { style: S.infoLabel }, "TIP NEKRETNINE"),
          h(R.Text, { style: S.infoValue }, typeLabel[propertyType] || propertyType),
        ),
        h(R.View, { style: S.infoCol },
          h(R.Text, { style: S.infoLabel }, "LOKACIJA"),
          h(R.Text, { style: S.infoValue }, location),
        ),
        h(R.View, { style: S.infoCol },
          h(R.Text, { style: S.infoLabel }, "POVRŠINA"),
          h(R.Text, { style: S.infoValue }, `${fmt(areaM2)} m²`),
        ),
      ),

      // Target property summary
      ...(result.target_property_summary ? [
        h(R.Text, { key: "summary", style: { ...S.paragraph, fontSize: 8, color: "#64748b", marginBottom: 8 } }, result.target_property_summary),
      ] : []),

      // Hero Value
      h(R.View, { style: S.heroBox },
        h(R.View, null,
          h(R.Text, { style: S.heroLabel }, "PROCIJENJENA TRŽIŠNA VRIJEDNOST"),
          ...(result.calculated_price_per_m2 ? [
            h(R.Text, { key: "ppm2", style: S.heroSub }, `${fmt(Math.round(result.calculated_price_per_m2))} EUR/m²`),
          ] : []),
        ),
        h(R.Text, { style: S.heroValue }, `${fmt(result.estimated_value_eur)} EUR`),
      ),

      // Confidence badge
      ...(result.math_confidence_score !== undefined ? [
        h(R.View, { key: "badge", style: { ...S.badge, backgroundColor: badgeColor } },
          h(R.Text, { style: S.badgeText }, `Pouzdanost: ${result.math_confidence_score}/100 — ${result.math_confidence_level ?? ""}`),
        ),
      ] : []),

      // Key Metrics Table
      h(R.Text, { style: S.sectionTitle }, "Ključne metrike"),
      ...metrics.map(([label, value], i) =>
        tdRow([label, value], ["50%", "50%"], i % 2 === 1, [0])
      ),

      // Price Range
      ...(result.price_range ? [
        h(R.Text, { key: "pr-title", style: S.sectionTitle }, "Raspon procjene"),
        thRow(["Scenarij", "EUR/m²", "Ukupno EUR"], ["40%", "30%", "30%"]),
        ...[
          ["Konzervativno", fmt(Math.round(result.price_range.conservative_price_per_m2)), fmt(result.price_range.conservative_total)],
          ["Realno", fmt(Math.round(result.price_range.realistic_price_per_m2)), fmt(result.price_range.realistic_total)],
          ["Optimistično", fmt(Math.round(result.price_range.optimistic_price_per_m2)), fmt(result.price_range.optimistic_total)],
        ].map((row, i) => tdRow(row, ["40%", "30%", "30%"], i % 2 === 1, [0])),
      ] : []),

      // Comparison Table
      ...(result.comparison_table && result.comparison_table.length > 0 ? [
        h(R.Text, { key: "ct-title", style: S.sectionTitle }, "Tablica usporednih nekretnina"),
        thRow(["#", "Oglas", "m²", "EUR/m²", "Korekcija", "Prilagođeno", "Sličnost"], ["5%", "30%", "10%", "13%", "12%", "15%", "15%"]),
        ...result.comparison_table.map((row, i) =>
          tdRow(
            [
              String(row.index),
              row.title.length > 35 ? row.title.slice(0, 35) + "…" : row.title,
              row.area_m2 > 0 ? fmt(row.area_m2) : "—",
              fmt(Math.round(row.raw_price_per_m2)),
              `${row.correction_percent > 0 ? "+" : ""}${row.correction_percent.toFixed(1)}%`,
              fmt(Math.round(row.adjusted_price_per_m2)),
              `${row.similarity_score}%`,
            ],
            ["5%", "30%", "10%", "13%", "12%", "15%", "15%"],
            i % 2 === 1,
          )
        ),
      ] : []),

      // Adjustments
      ...(result.adjustments && result.adjustments.length > 0 ? [
        h(R.Text, { key: "adj-title", style: S.sectionTitle }, "Primijenjene korekcije"),
        thRow(["Faktor", "Opis", "Postotak"], ["25%", "55%", "20%"]),
        ...result.adjustments.map((a, i) =>
          tdRow(
            [a.factor, a.description, `${a.percent > 0 ? "+" : ""}${a.percent.toFixed(1)}%`],
            ["25%", "55%", "20%"],
            i % 2 === 1,
            [0],
          )
        ),
      ] : []),

      // Elaborat Stats (2σ Test)
      ...(result.elaborat_stats ? [
        h(R.Text, { key: "elab-title", style: S.sectionTitle }, "Statistička validacija (2σ test)"),
        h(R.View, { key: "elab-grid", style: { flexDirection: "row", gap: 8, marginBottom: 6 } },
          ...[
            ["Aritm. sredina", `${fmt(Math.round(result.elaborat_stats.arithmetic_mean_per_m2))} EUR/m²`],
            ["Std. devijacija", `${result.elaborat_stats.std_dev_per_m2.toFixed(0)} EUR/m²`],
            ["Koef. varijacije", `${result.elaborat_stats.relative_std_dev_percent.toFixed(1)}%`],
            ["Isključeno (2σ)", String(result.elaborat_stats.two_sigma_excluded)],
          ].map(([label, val], i) =>
            h(R.View, { key: `es-${i}`, style: { flex: 1, backgroundColor: "#f0fdf4", borderRadius: 3, padding: 6, alignItems: "center" as const } },
              h(R.Text, { style: { fontSize: 10, fontWeight: 700, color: "#1e293b" } }, val),
              h(R.Text, { style: { fontSize: 6.5, color: "#64748b", marginTop: 1, textTransform: "uppercase" as const } }, label),
            )
          )
        ),
        ...(result.elaborat_stats.explanation ? [
          h(R.Text, { key: "elab-exp", style: { ...S.paragraph, fontSize: 8 } }, result.elaborat_stats.explanation),
        ] : []),
      ] : []),

      // Confidence Explanation
      ...(result.math_confidence_explanation ? [
        h(R.Text, { key: "conf-title", style: S.sectionTitle }, `Pouzdanost procjene: ${result.math_confidence_score ?? "N/A"}/100`),
        h(R.Text, { key: "conf-text", style: S.paragraph }, result.math_confidence_explanation),
      ] : []),

      // Benchmark
      ...(result.benchmark_summary ? [
        h(R.Text, { key: "bench-title", style: S.sectionTitle }, "Benchmark regije"),
        h(R.Text, { key: "bench-text", style: S.paragraph }, result.benchmark_summary),
      ] : []),

      // Stručno mišljenje (Valuation Reasoning)
      ...(result.valuation_reasoning ? [
        h(R.Text, { key: "reason-title", style: S.sectionTitle }, "Stručno mišljenje"),
        h(R.Text, { key: "reason-text", style: { ...S.paragraph, lineHeight: 1.6 } }, result.valuation_reasoning),
      ] : []),
    ),
  );
}

// ── Public API — same signature as before ──

export async function generateValuationPdf(params: PdfParams): Promise<void> {
  await ensurePdfEngine();
  const doc = h(ValuationPdfDocument, params);
  const blob = await R.default.pdf(doc).toBlob();

  // Trigger browser download
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Procjena_${params.propertyType}_${params.location.replace(/[^a-zA-Z0-9ČčĆćŠšŽžĐđ]/g, "_")}_${new Date().toISOString().slice(0, 10)}.pdf`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
