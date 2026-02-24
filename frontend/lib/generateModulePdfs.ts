/* eslint-disable @typescript-eslint/no-explicit-any */
import { createElement } from "react";

// ═══════════════════════════════════════════════════
//  PDF EXPORT — Contract Analysis Report
//  @react-pdf/renderer — lazy-loaded to prevent
//  Next.js from bundling pdfkit Node.js deps at build.
//  Full Croatian diacritics via Roboto (č, ć, š, ž, đ).
// ═══════════════════════════════════════════════════

// ── Types ──

interface ContractIssue {
  title: string;
  description: string;
  quoted_text?: string | null;
  legal_basis?: string | null;
}

interface SuggestedAmendment {
  article_reference: string;
  current_text: string;
  proposed_text: string;
  rationale: string;
}

interface ChecklistItem {
  category: string;
  check: string;
  status: string;
  risk: string;
  finding: string;
  article_ref?: string | null;
  recommendation?: string | null;
}

interface CrossArticleIssue {
  title: string;
  linked_articles: string[];
  description: string;
  risk: string;
}

interface MandatoryClauseCheck {
  clause: string;
  present: boolean;
  importance: string;
  description: string;
}

interface LogicalTrap {
  title: string;
  articles: string[];
  quoted_text: string;
  explanation: string;
  legal_basis: string;
  severity: string;
}

interface IdealContractDeviation {
  element: string;
  ideal_text: string;
  actual_text: string;
  severity: string;
  recommendation: string;
}

interface ContractAnalysisResult {
  document_type: string;
  summary: string;
  overall_risk: string;
  risk_score: number;
  red_flags: ContractIssue[];
  warnings: ContractIssue[];
  positives: string[];
  suggested_amendments: SuggestedAmendment[];
  checklist?: ChecklistItem[];
  cross_article_issues?: CrossArticleIssue[];
  mandatory_clauses?: MandatoryClauseCheck[];
  risk_score_breakdown?: string[];
  logical_traps?: LogicalTrap[];
  ideal_contract_deviations?: IdealContractDeviation[];
  disclaimer: string;
}

// ── Lazy-loaded module & styles ──

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
    header: { backgroundColor: "#059669", marginHorizontal: -36, marginTop: -30, paddingVertical: 18, paddingHorizontal: 36, marginBottom: 16 },
    headerTitle: { color: "#ffffff", fontSize: 17, fontWeight: 700, marginBottom: 4 },
    headerSub: { color: "#d1fae5", fontSize: 9 },
    sectionTitle: { fontSize: 11, fontWeight: 700, color: "#1e3a5f", marginTop: 14, marginBottom: 6, paddingBottom: 2, borderBottomWidth: 1, borderBottomColor: "#cbd5e1" },
    infoBox: { flexDirection: "row", backgroundColor: "#f8fafc", border: "1pt solid #e2e8f0", borderRadius: 3, padding: 10, marginBottom: 10 },
    infoCol: { flex: 1 },
    infoLabel: { fontSize: 7.5, fontWeight: 700, color: "#64748b", textTransform: "uppercase", marginBottom: 2 },
    infoValue: { fontSize: 10, color: "#1e293b" },
    tableHead: { flexDirection: "row", backgroundColor: "#059669", borderTopLeftRadius: 2, borderTopRightRadius: 2, paddingVertical: 5, paddingHorizontal: 4 },
    tableHeadCell: { color: "#ffffff", fontSize: 7.5, fontWeight: 700 },
    tableRow: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 4, borderBottomWidth: 0.5, borderBottomColor: "#e2e8f0" },
    tableRowAlt: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 4, backgroundColor: "#f8fafc", borderBottomWidth: 0.5, borderBottomColor: "#e2e8f0" },
    tableCell: { fontSize: 8, color: "#334155" },
    tableCellBold: { fontSize: 8, fontWeight: 700, color: "#1e293b" },
    paragraph: { fontSize: 9, lineHeight: 1.5, color: "#334155", marginBottom: 4 },
    bullet: { fontSize: 9, lineHeight: 1.4, color: "#334155", marginBottom: 2, paddingLeft: 8 },
    riskHigh: { color: "#dc2626" },
    riskMed: { color: "#d97706" },
    riskLow: { color: "#16a34a" },
    blue: { color: "#2563eb" },
    muted: { color: "#94a3b8", fontSize: 7 },
    footer: { position: "absolute", bottom: 20, left: 36, right: 36 },
    footerLine: { borderTopWidth: 0.5, borderTopColor: "#cbd5e1", paddingTop: 4, flexDirection: "row", justifyContent: "space-between" },
    footerText: { fontSize: 6.5, color: "#94a3b8" },
    footerDisclaimer: { fontSize: 6, color: "#94a3b8", marginTop: 2 },
  });
}

// ── Helpers ──

const h = createElement;

const riskStyle = (risk: string) => {
  switch (risk?.toLowerCase()) {
    case "visok": case "kritično": return S.riskHigh;
    case "srednji": return S.riskMed;
    case "nizak": return S.riskLow;
    default: return { color: "#64748b" };
  }
};

function thRow(cells: string[], widths: string[]) {
  return h(R.View, { style: S.tableHead },
    ...cells.map((c, i) =>
      h(R.Text, { key: String(i), style: { ...S.tableHeadCell, width: widths[i] } }, c)
    )
  );
}

function tdRow(cells: string[], widths: string[], isAlt: boolean, boldCols?: number[]) {
  return h(R.View, { style: isAlt ? S.tableRowAlt : S.tableRow, wrap: false },
    ...cells.map((c, i) =>
      h(R.Text, {
        key: String(i),
        style: {
          ...(boldCols?.includes(i) ? S.tableCellBold : S.tableCell),
          width: widths[i],
        },
      }, c)
    )
  );
}

// ── Document Component ──

function ContractPdfDocument({ result }: { result: ContractAnalysisResult }) {
  const today = new Date().toLocaleDateString("hr-HR");

  // Stats rows
  const stats: [string, string][] = [
    ["Crvene zastavice", String(result.red_flags.length)],
    ["Logičke zamke (dubinska analiza)", String(result.logical_traps?.length ?? 0)],
    ["Upozorenja", String(result.warnings.length)],
    ["Pozitivne strane", String(result.positives.length)],
    ["Prijedlozi izmjena", String(result.suggested_amendments.length)],
  ];

  return h(R.Document, { title: "Analiza ugovora", author: "PravoTek" },
    h(R.Page, { size: "A4", style: S.page, wrap: true },

      // ── Footer (fixed on every page) ──
      h(R.View, { style: S.footer, fixed: true },
        h(R.View, { style: S.footerLine },
          h(R.Text, { style: S.footerText }, "PravoTek — analiza ugovora"),
          h(R.Text, { style: S.footerText, render: ({ pageNumber, totalPages }: { pageNumber: number; totalPages: number }) => `Stranica ${pageNumber} / ${totalPages}` }),
        ),
        h(R.Text, { style: S.footerDisclaimer }, "Ovaj izvještaj ne zamjenjuje pravni savjet ovlaštenog odvjetnika."),
      ),

      // ── Header ──
      h(R.View, { style: S.header },
        h(R.Text, { style: S.headerTitle }, "Izvještaj o analizi ugovora"),
        h(R.Text, { style: S.headerSub }, `Datum: ${today}  •  PravoTek — pravni revizor`),
      ),

      // ── Document Info ──
      h(R.View, { style: S.infoBox },
        h(R.View, { style: S.infoCol },
          h(R.Text, { style: S.infoLabel }, "VRSTA DOKUMENTA"),
          h(R.Text, { style: S.infoValue }, result.document_type),
        ),
        h(R.View, { style: S.infoCol },
          h(R.Text, { style: S.infoLabel }, "UKUPNI RIZIK"),
          h(R.Text, { style: { ...S.infoValue, fontWeight: 700, ...riskStyle(result.overall_risk) } }, `${result.overall_risk} (${result.risk_score}/100)`),
        ),
      ),

      // ── Summary ──
      ...(result.summary ? [
        h(R.Text, { key: "sum", style: S.paragraph }, result.summary),
      ] : []),

      // ── Stats Table ──
      h(R.Text, { style: S.sectionTitle }, "Statistika analize"),
      ...stats.map(([label, value], i) =>
        tdRow([label, value], ["70%", "30%"], i % 2 === 1, [0])
      ),

      // ── Checklist ──
      ...(result.checklist && result.checklist.length > 0 ? [
        h(R.Text, { key: "cl-title", style: S.sectionTitle }, "Revizijska kontrolna lista"),
        thRow(["Kategorija", "Provjera", "Status", "Rizik", "Nalaz"], ["15%", "20%", "13%", "10%", "42%"]),
        ...result.checklist.map((item, i) =>
          tdRow(
            [
              item.category,
              item.check + (item.article_ref ? ` (${item.article_ref})` : ""),
              item.status.replace("_", " "),
              item.risk,
              item.finding.length > 80 ? item.finding.slice(0, 80) + "…" : item.finding,
            ],
            ["15%", "20%", "13%", "10%", "42%"],
            i % 2 === 1,
          )
        ),
      ] : []),

      // ── Red Flags ──
      ...(result.red_flags.length > 0 ? [
        h(R.Text, { key: "rf-title", style: { ...S.sectionTitle, color: "#dc2626" } }, `Crvene zastavice (${result.red_flags.length})`),
        ...result.red_flags.flatMap((flag, i) => [
          h(R.View, { key: `rf-${i}`, wrap: false },
            h(R.Text, { style: { fontSize: 9, fontWeight: 700, color: "#dc2626", marginBottom: 1 } }, `• ${flag.title}`),
            h(R.Text, { style: { ...S.paragraph, paddingLeft: 8 } }, flag.description),
            ...(flag.legal_basis ? [
              h(R.Text, { key: `rf-lb-${i}`, style: { fontSize: 8, color: "#64748b", paddingLeft: 8, marginBottom: 4 } }, `Pravna osnova: ${flag.legal_basis}`),
            ] : []),
          ),
        ]),
      ] : []),

      // ── Logical Traps ──
      ...(result.logical_traps && result.logical_traps.length > 0 ? [
        h(R.Text, { key: "lt-title", style: { ...S.sectionTitle, color: "#dc2626" } }, `Logičke zamke — dubinska analiza (${result.logical_traps.length})`),
        ...result.logical_traps.flatMap((trap, i) => [
          h(R.View, { key: `lt-${i}`, wrap: false },
            h(R.Text, { style: { fontSize: 9, fontWeight: 700, ...riskStyle(trap.severity), marginBottom: 1 } }, `[${trap.severity}] ${trap.title}`),
            h(R.Text, { style: { ...S.paragraph, paddingLeft: 8 } }, trap.explanation),
            ...(trap.legal_basis ? [
              h(R.Text, { key: `lt-lb-${i}`, style: { fontSize: 8, color: "#2563eb", paddingLeft: 8, marginBottom: 4 } }, `Pravna osnova: ${trap.legal_basis}`),
            ] : []),
          ),
        ]),
      ] : []),

      // ── Warnings ──
      ...(result.warnings.length > 0 ? [
        h(R.Text, { key: "w-title", style: { ...S.sectionTitle, color: "#d97706" } }, `Upozorenja (${result.warnings.length})`),
        ...result.warnings.flatMap((warn, i) => [
          h(R.View, { key: `w-${i}`, wrap: false },
            h(R.Text, { style: { fontSize: 9, fontWeight: 700, color: "#d97706", marginBottom: 1 } }, `• ${warn.title}`),
            h(R.Text, { style: { ...S.paragraph, paddingLeft: 8 } }, warn.description),
          ),
        ]),
      ] : []),

      // ── Suggested Amendments ──
      ...(result.suggested_amendments.length > 0 ? [
        h(R.Text, { key: "sa-title", style: { ...S.sectionTitle, color: "#2563eb" } }, `Prijedlozi izmjena (${result.suggested_amendments.length})`),
        ...result.suggested_amendments.flatMap((sa, i) => [
          h(R.View, { key: `sa-${i}`, wrap: false, style: { marginBottom: 6 } },
            h(R.Text, { style: { fontSize: 9, fontWeight: 700, color: "#2563eb", marginBottom: 2 } }, sa.article_reference),
            ...(sa.current_text ? [
              h(R.Text, { key: `sa-cur-${i}`, style: { fontSize: 8, color: "#dc2626", paddingLeft: 8, marginBottom: 1 } }, `Trenutno: ${sa.current_text}`),
            ] : []),
            h(R.Text, { style: { fontSize: 8, color: "#16a34a", paddingLeft: 8, marginBottom: 1 } }, `Predloženo: ${sa.proposed_text}`),
            h(R.Text, { style: { fontSize: 8, color: "#64748b", paddingLeft: 8 } }, sa.rationale),
          ),
        ]),
      ] : []),

      // ── Risk Score Breakdown ──
      ...(result.risk_score_breakdown && result.risk_score_breakdown.length > 0 ? [
        h(R.Text, { key: "rsb-title", style: S.sectionTitle }, `Struktura ocjene rizika (${result.risk_score}/100)`),
        ...result.risk_score_breakdown.map((line, i) =>
          h(R.Text, { key: `rsb-${i}`, style: { fontSize: 8, color: line.startsWith("-") ? "#16a34a" : "#dc2626", paddingLeft: 8, marginBottom: 1 } }, line)
        ),
        h(R.Text, { key: "rsb-total", style: { fontSize: 9, fontWeight: 700, color: "#1e293b", paddingLeft: 8, marginTop: 2 } }, `= ${result.risk_score}/100 → ${result.overall_risk}`),
      ] : []),

      // ── Positives ──
      ...(result.positives.length > 0 ? [
        h(R.Text, { key: "pos-title", style: { ...S.sectionTitle, color: "#16a34a" } }, `Pozitivne strane (${result.positives.length})`),
        ...result.positives.map((p, i) =>
          h(R.Text, { key: `pos-${i}`, style: S.bullet }, `✓ ${p}`)
        ),
      ] : []),

      // ── Disclaimer ──
      ...(result.disclaimer ? [
        h(R.View, { key: "disc", style: { marginTop: 14 } },
          h(R.Text, { style: { fontSize: 7, color: "#94a3b8", fontStyle: "italic", lineHeight: 1.4 } }, result.disclaimer),
        ),
      ] : []),
    ),
  );
}

// ── Public API ──

export async function generateContractPdf(result: ContractAnalysisResult) {
  await ensurePdfEngine();
  const doc = h(ContractPdfDocument, { result });
  const blob = await R.default.pdf(doc).toBlob();

  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `Analiza_ugovora_${result.document_type.replace(/[^a-zA-Z0-9ČčĆćŠšŽžĐđ]/g, "_")}_${new Date().toISOString().slice(0, 10)}.pdf`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
