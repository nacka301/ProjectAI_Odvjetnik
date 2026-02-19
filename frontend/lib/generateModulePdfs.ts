import jsPDF from "jspdf";
import autoTable from "jspdf-autotable";
import { loadPdfFonts, getPdfFont } from "./pdfFontLoader";

// ═══════════════════════════════════════════
//  PDF EXPORT — Contract Analysis Report
//  V59: Professional export with Croatian character support
// ═══════════════════════════════════════════

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

const fmt = (n: number) => n.toLocaleString("hr-HR");

export async function generateContractPdf(result: ContractAnalysisResult) {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
  
  // Load Croatian-compatible fonts
  await loadPdfFonts(doc);
  const fontFamily = getPdfFont();
  
  const pageW = doc.internal.pageSize.getWidth();
  const margin = 18;
  const contentW = pageW - margin * 2;
  let y = margin;

  const addPageIfNeeded = (needed: number) => {
    if (y + needed > doc.internal.pageSize.getHeight() - margin) {
      doc.addPage();
      y = margin;
    }
  };

  const riskColor = (risk: string): [number, number, number] => {
    switch (risk?.toLowerCase()) {
      case "visok": case "kritično": return [220, 38, 38];
      case "srednji": return [217, 119, 6];
      case "nizak": return [22, 163, 74];
      default: return [100, 100, 100];
    }
  };

  // ── Header ──
  doc.setFillColor(5, 150, 105); // emerald-600
  doc.rect(0, 0, pageW, 38, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(20);
  doc.setFont(fontFamily, "bold");
  doc.text("Izvještaj o analizi ugovora", margin, 18);
  doc.setFontSize(10);
  doc.setFont(fontFamily, "normal");
  doc.text(`Datum: ${new Date().toLocaleDateString("hr-HR")}`, margin, 28);
  doc.text("PravoTek — AI pravni revizor", margin, 34);

  y = 48;

  // ── Document Info Box ──
  doc.setDrawColor(200, 200, 200);
  doc.setFillColor(248, 250, 252);
  doc.roundedRect(margin, y, contentW, 24, 2, 2, "FD");
  doc.setTextColor(50, 50, 50);
  doc.setFontSize(9);
  doc.setFont(fontFamily, "bold");

  const col1x = margin + 4;
  const col2x = margin + contentW / 2;

  doc.text("Vrsta dokumenta:", col1x, y + 8);
  doc.text("Ukupni rizik:", col2x, y + 8);

  doc.setFont(fontFamily, "normal");
  doc.setFontSize(10);
  doc.text(result.document_type, col1x, y + 15);

  const [rr, rg, rb] = riskColor(result.overall_risk);
  doc.setTextColor(rr, rg, rb);
  doc.setFont(fontFamily, "bold");
  doc.text(`${result.overall_risk} (${result.risk_score}/100)`, col2x, y + 15);

  y += 30;

  // ── Summary ──
  if (result.summary) {
    doc.setTextColor(50, 50, 50);
    doc.setFontSize(9);
    doc.setFont(fontFamily, "normal");
    const sumLines = doc.splitTextToSize(result.summary, contentW);
    doc.text(sumLines.slice(0, 3), margin, y);
    y += sumLines.slice(0, 3).length * 4 + 4;
  }

  // ── Stats ──
  addPageIfNeeded(20);
  doc.setFontSize(11);
  doc.setFont(fontFamily, "bold");
  doc.setTextColor(50, 50, 50);
  doc.text("Statistika analize", margin, y);
  y += 5;

  autoTable(doc, {
    startY: y,
    head: [],
    body: [
      ["Crvene zastavice", String(result.red_flags.length)],
      ["Logičke zamke (AI Reasoning)", String(result.logical_traps?.length ?? 0)],
      ["Upozorenja", String(result.warnings.length)],
      ["Pozitivne strane", String(result.positives.length)],
      ["Prijedlozi izmjena", String(result.suggested_amendments.length)],
    ],
    theme: "plain",
    styles: { fontSize: 9, cellPadding: 2.5, font: fontFamily },
    columnStyles: {
      0: { fontStyle: "bold", cellWidth: 55, textColor: [80, 80, 80] },
      1: { textColor: [30, 30, 30] },
    },
    margin: { left: margin, right: margin },
  });
  y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;

  // ── Checklist ──
  if (result.checklist && result.checklist.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Revizijska kontrolna lista", margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["Kategorija", "Provjera", "Status", "Rizik", "Nalaz"]],
      body: result.checklist.map((item) => [
        item.category,
        item.check + (item.article_ref ? ` (${item.article_ref})` : ""),
        item.status.replace("_", " "),
        item.risk,
        item.finding.slice(0, 80) + (item.finding.length > 80 ? "..." : ""),
      ]),
      theme: "striped",
      headStyles: { fillColor: [5, 150, 105], fontSize: 7.5, font: fontFamily },
      styles: { fontSize: 7, cellPadding: 2, font: fontFamily },
      columnStyles: { 0: { cellWidth: 25 }, 1: { cellWidth: 30 }, 2: { cellWidth: 20 }, 3: { cellWidth: 15 } },
      margin: { left: margin, right: margin },
    });
    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Red Flags ──
  if (result.red_flags.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(220, 38, 38);
    doc.text(`Crvene zastavice (${result.red_flags.length})`, margin, y);
    y += 5;

    for (const flag of result.red_flags) {
      addPageIfNeeded(20);
      doc.setFont(fontFamily, "bold");
      doc.setFontSize(9);
      doc.setTextColor(220, 38, 38);
      doc.text(`• ${flag.title}`, margin, y);
      y += 4;
      doc.setFont(fontFamily, "normal");
      doc.setTextColor(50, 50, 50);
      const descLines = doc.splitTextToSize(flag.description, contentW - 4);
      for (const line of descLines.slice(0, 4)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 4;
      }
      if (flag.legal_basis) {
        doc.setFontSize(8);
        doc.setTextColor(100, 100, 100);
        doc.text(`Pravna osnova: ${flag.legal_basis}`, margin + 4, y);
        y += 4;
      }
      y += 2;
    }
  }

  // ── Logical Traps ──
  if (result.logical_traps && result.logical_traps.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(220, 38, 38);
    doc.text(`Logičke zamke — AI dubinska analiza (${result.logical_traps.length})`, margin, y);
    y += 5;

    for (const trap of result.logical_traps) {
      addPageIfNeeded(25);
      doc.setFont(fontFamily, "bold");
      doc.setFontSize(9);
      const [sr, sg, sb] = riskColor(trap.severity);
      doc.setTextColor(sr, sg, sb);
      doc.text(`[${trap.severity}] ${trap.title}`, margin, y);
      y += 4;
      doc.setFont(fontFamily, "normal");
      doc.setTextColor(50, 50, 50);
      const explLines = doc.splitTextToSize(trap.explanation, contentW - 4);
      for (const line of explLines.slice(0, 5)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 4;
      }
      if (trap.legal_basis) {
        doc.setFontSize(8);
        doc.setTextColor(37, 99, 235);
        doc.text(`Pravna osnova: ${trap.legal_basis}`, margin + 4, y);
        y += 4;
      }
      y += 2;
    }
  }

  // ── Warnings ──
  if (result.warnings.length > 0) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(217, 119, 6);
    doc.text(`Upozorenja (${result.warnings.length})`, margin, y);
    y += 5;

    for (const warn of result.warnings) {
      addPageIfNeeded(15);
      doc.setFont(fontFamily, "bold");
      doc.setFontSize(9);
      doc.setTextColor(217, 119, 6);
      doc.text(`• ${warn.title}`, margin, y);
      y += 4;
      doc.setFont(fontFamily, "normal");
      doc.setTextColor(50, 50, 50);
      const descLines = doc.splitTextToSize(warn.description, contentW - 4);
      for (const line of descLines.slice(0, 3)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 4;
      }
      y += 2;
    }
  }

  // ── Suggested Amendments ──
  if (result.suggested_amendments.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(37, 99, 235);
    doc.text(`Prijedlozi izmjena (${result.suggested_amendments.length})`, margin, y);
    y += 5;

    for (const sa of result.suggested_amendments) {
      addPageIfNeeded(25);
      doc.setFont(fontFamily, "bold");
      doc.setFontSize(9);
      doc.setTextColor(37, 99, 235);
      doc.text(sa.article_reference, margin, y);
      y += 4;
      doc.setFont(fontFamily, "normal");
      doc.setTextColor(50, 50, 50);
      doc.setFontSize(8);
      if (sa.current_text) {
        const curLines = doc.splitTextToSize(`Trenutno: ${sa.current_text}`, contentW - 4);
        doc.setTextColor(220, 38, 38);
        for (const line of curLines.slice(0, 2)) {
          addPageIfNeeded(5);
          doc.text(line, margin + 4, y);
          y += 3.5;
        }
      }
      const propLines = doc.splitTextToSize(`Predloženo: ${sa.proposed_text}`, contentW - 4);
      doc.setTextColor(22, 163, 74);
      for (const line of propLines.slice(0, 2)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 3.5;
      }
      doc.setTextColor(100, 100, 100);
      const ratLines = doc.splitTextToSize(sa.rationale, contentW - 4);
      for (const line of ratLines.slice(0, 2)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 3.5;
      }
      y += 3;
    }
  }

  // ── Risk Score Breakdown ──
  if (result.risk_score_breakdown && result.risk_score_breakdown.length > 0) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(50, 50, 50);
    doc.text(`Struktura ocjene rizika (${result.risk_score}/100)`, margin, y);
    y += 5;

    for (const line of result.risk_score_breakdown) {
      addPageIfNeeded(5);
      doc.setFont(fontFamily, "normal");
      doc.setFontSize(8);
      if (line.startsWith("-")) {
        doc.setTextColor(22, 163, 74);
      } else {
        doc.setTextColor(220, 38, 38);
      }
      doc.text(line, margin + 4, y);
      y += 3.5;
    }
    doc.setTextColor(50, 50, 50);
    doc.setFont(fontFamily, "bold");
    doc.text(`= ${result.risk_score}/100 → ${result.overall_risk}`, margin + 4, y);
    y += 6;
  }

  // ── Positives ──
  if (result.positives.length > 0) {
    addPageIfNeeded(20);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(22, 163, 74);
    doc.text(`Pozitivne strane (${result.positives.length})`, margin, y);
    y += 5;
    doc.setFont(fontFamily, "normal");
    doc.setFontSize(9);
    doc.setTextColor(50, 50, 50);
    for (const p of result.positives) {
      addPageIfNeeded(5);
      doc.text(`✓ ${p}`, margin + 4, y);
      y += 4;
    }
    y += 2;
  }

  // ── Disclaimer ──
  if (result.disclaimer) {
    addPageIfNeeded(20);
    doc.setFontSize(7);
    doc.setTextColor(150, 150, 150);
    doc.setFont(fontFamily, "italic");
    const disclaimerLines = doc.splitTextToSize(result.disclaimer, contentW);
    for (const line of disclaimerLines) {
      addPageIfNeeded(4);
      doc.text(line, margin, y);
      y += 3;
    }
  }

  // ── Footer on every page ──
  const totalPages = doc.internal.pages.length - 1;
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    const pageH = doc.internal.pageSize.getHeight();
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, pageH - 14, pageW - margin, pageH - 14);
    doc.setFontSize(7);
    doc.setTextColor(150, 150, 150);
    doc.setFont(fontFamily, "normal");
    doc.text("PravoTek — AI analiza ugovora", margin, pageH - 9);
    doc.text(`Stranica ${i} / ${totalPages}`, pageW - margin, pageH - 9, { align: "right" });
    doc.text("Ovaj izvještaj ne zamjenjuje pravni savjet ovlaštenog odvjetnika.", margin, pageH - 5);
  }

  // ── Save ──
  const filename = `Analiza_ugovora_${result.document_type.replace(/[^a-zA-Z0-9ČčĆćŠšŽžĐđ]/g, "_")}_${new Date().toISOString().slice(0, 10)}.pdf`;
  doc.save(filename);
}

// ═══════════════════════════════════════════
//  PDF EXPORT — Legal Strategy Report
// ═══════════════════════════════════════════

interface CourtPrecedent {
  case_number: string;
  court: string;
  date: string;
  summary: string;
  legal_principle: string;
  relevance_to_case: string;
  alignment: string;
  similarity_score: number;
}

interface LegalRisk {
  title: string;
  description: string;
  severity: string;
  mitigation: string;
}

interface StrategicRecommendation {
  action: string;
  rationale: string;
  priority: string;
  timing: string;
}

interface LegalStrategyResult {
  key_legal_issue: string;
  sub_issues: string[];
  precedents: CourtPrecedent[];
  win_probability: number;
  win_probability_label: string;
  probability_reasoning: string;
  risks: LegalRisk[];
  strengths: string[];
  recommendations: StrategicRecommendation[];
  strategy_summary: string;
  estimated_duration: string;
  estimated_cost: string;
  disclaimer: string;
}

export async function generateLegalPdf(result: LegalStrategyResult) {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
  
  // Load Croatian-compatible fonts
  await loadPdfFonts(doc);
  const fontFamily = getPdfFont();
  
  const pageW = doc.internal.pageSize.getWidth();
  const margin = 18;
  const contentW = pageW - margin * 2;
  let y = margin;

  const addPageIfNeeded = (needed: number) => {
    if (y + needed > doc.internal.pageSize.getHeight() - margin) {
      doc.addPage();
      y = margin;
    }
  };

  // ── Header ──
  doc.setFillColor(79, 70, 229); // indigo-600
  doc.rect(0, 0, pageW, 38, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(20);
  doc.setFont(fontFamily, "bold");
  doc.text("Pravna strategija — AI analiza", margin, 18);
  doc.setFontSize(10);
  doc.setFont(fontFamily, "normal");
  doc.text(`Datum: ${new Date().toLocaleDateString("hr-HR")}`, margin, 28);
  doc.text("PravoTek — AI pravna inteligencija", margin, 34);

  y = 48;

  // ── Key Issue ──
  doc.setTextColor(50, 50, 50);
  doc.setFontSize(11);
  doc.setFont(fontFamily, "bold");
  doc.text("Ključno pravno pitanje", margin, y);
  y += 5;
  doc.setFont(fontFamily, "normal");
  doc.setFontSize(9);
  const issueLines = doc.splitTextToSize(result.key_legal_issue, contentW);
  doc.text(issueLines, margin, y);
  y += issueLines.length * 4 + 4;

  // ── Win Probability ──
  addPageIfNeeded(20);
  doc.setFontSize(11);
  doc.setFont(fontFamily, "bold");
  doc.text("Procjena uspjeha", margin, y);
  y += 6;
  doc.setFontSize(14);
  doc.setTextColor(79, 70, 229);
  doc.text(`${result.win_probability}% — ${result.win_probability_label}`, margin, y);
  y += 6;
  doc.setTextColor(50, 50, 50);
  doc.setFont(fontFamily, "normal");
  doc.setFontSize(9);
  const probLines = doc.splitTextToSize(result.probability_reasoning, contentW);
  for (const line of probLines.slice(0, 6)) {
    addPageIfNeeded(5);
    doc.text(line, margin, y);
    y += 4;
  }
  y += 4;

  // ── Precedents ──
  if (result.precedents.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(50, 50, 50);
    doc.text(`Sudska praksa (${result.precedents.length})`, margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["Predmet", "Sud", "Datum", "Poravnanje", "Sličnost"]],
      body: result.precedents.map((p) => [
        p.case_number,
        p.court,
        p.date,
        p.alignment,
        `${p.similarity_score}%`,
      ]),
      theme: "striped",
      headStyles: { fillColor: [79, 70, 229], fontSize: 8, font: fontFamily },
      styles: { fontSize: 7.5, cellPadding: 2, font: fontFamily },
      margin: { left: margin, right: margin },
    });
    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Risks ──
  if (result.risks.length > 0) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(220, 38, 38);
    doc.text("Rizici", margin, y);
    y += 5;

    for (const risk of result.risks) {
      addPageIfNeeded(15);
      doc.setFont(fontFamily, "bold");
      doc.setFontSize(9);
      doc.setTextColor(220, 38, 38);
      doc.text(`[${risk.severity}] ${risk.title}`, margin, y);
      y += 4;
      doc.setFont(fontFamily, "normal");
      doc.setTextColor(50, 50, 50);
      const descLines = doc.splitTextToSize(risk.description, contentW - 4);
      for (const line of descLines.slice(0, 3)) {
        addPageIfNeeded(5);
        doc.text(line, margin + 4, y);
        y += 4;
      }
      y += 2;
    }
  }

  // ── Strategy Summary ──
  if (result.strategy_summary) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Strateški sažetak", margin, y);
    y += 5;
    doc.setFont(fontFamily, "normal");
    doc.setFontSize(9);
    const stratLines = doc.splitTextToSize(result.strategy_summary, contentW);
    for (const line of stratLines) {
      addPageIfNeeded(5);
      doc.text(line, margin, y);
      y += 4;
    }
    y += 4;
  }

  // ── Recommendations ──
  if (result.recommendations.length > 0) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont(fontFamily, "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Preporuke", margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["Radnja", "Obrazloženje", "Prioritet", "Tajming"]],
      body: result.recommendations.map((r) => [
        r.action,
        r.rationale.slice(0, 60) + (r.rationale.length > 60 ? "..." : ""),
        r.priority,
        r.timing,
      ]),
      theme: "striped",
      headStyles: { fillColor: [79, 70, 229], fontSize: 8, font: fontFamily },
      styles: { fontSize: 7.5, cellPadding: 2, font: fontFamily },
      margin: { left: margin, right: margin },
    });
    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Footer ──
  const totalPages = doc.internal.pages.length - 1;
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    const pageH = doc.internal.pageSize.getHeight();
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, pageH - 14, pageW - margin, pageH - 14);
    doc.setFontSize(7);
    doc.setTextColor(150, 150, 150);
    doc.setFont(fontFamily, "normal");
    doc.text("PravoTek — AI pravna inteligencija", margin, pageH - 9);
    doc.text(`Stranica ${i} / ${totalPages}`, pageW - margin, pageH - 9, { align: "right" });
    doc.text("Ovaj izvještaj ne zamjenjuje pravni savjet ovlaštenog odvjetnika.", margin, pageH - 5);
  }

  const filename = `Pravna_strategija_${new Date().toISOString().slice(0, 10)}.pdf`;
  doc.save(filename);
}

// ═══════════════════════════════════════════
//  PDF EXPORT — Chronology Report
// ═══════════════════════════════════════════

interface TimelineEvent {
  date_iso: string;
  display_date: string;
  time?: string | null;
  event_description: string;
  source_file: string;
  page_number: number;
  confidence: string;
}

interface ChronologyResult {
  events: TimelineEvent[];
  total_documents_processed: number;
  total_events_extracted: number;
}

export async function generateChronologyPdf(result: ChronologyResult) {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
  
  // Load Croatian-compatible fonts
  await loadPdfFonts(doc);
  const fontFamily = getPdfFont();
  
  const pageW = doc.internal.pageSize.getWidth();
  const margin = 18;
  const contentW = pageW - margin * 2;
  let y = margin;

  // ── Header ──
  doc.setFillColor(37, 99, 235); // blue-600
  doc.rect(0, 0, pageW, 38, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(20);
  doc.setFont(fontFamily, "bold");
  doc.text("Kronologija spisa", margin, 18);
  doc.setFontSize(10);
  doc.setFont(fontFamily, "normal");
  doc.text(`Datum: ${new Date().toLocaleDateString("hr-HR")}`, margin, 28);
  doc.text(`${result.total_documents_processed} dokumenata, ${result.total_events_extracted} događaja`, margin, 34);

  y = 48;

  // ── Events Table ──
  autoTable(doc, {
    startY: y,
    head: [["#", "Datum", "Vrijeme", "Opis događaja", "Izvor", "Pouzdanost"]],
    body: result.events.map((evt, i) => [
      String(i + 1),
      evt.display_date,
      evt.time || "",
      evt.event_description,
      evt.source_file,
      evt.confidence === "high" ? "Potvrđeno" : evt.confidence === "medium" ? "Izvedeno" : "Provjeri",
    ]),
    theme: "striped",
    headStyles: { fillColor: [37, 99, 235], fontSize: 8, font: fontFamily },
    styles: { fontSize: 7.5, cellPadding: 2, font: fontFamily },
    columnStyles: {
      0: { cellWidth: 8 },
      1: { cellWidth: 22 },
      2: { cellWidth: 15 },
      3: { cellWidth: 70 },
      4: { cellWidth: 30 },
      5: { cellWidth: 18 },
    },
    margin: { left: margin, right: margin },
  });

  // ── Footer ──
  const totalPages = doc.internal.pages.length - 1;
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    const pageH = doc.internal.pageSize.getHeight();
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, pageH - 14, pageW - margin, pageH - 14);
    doc.setFontSize(7);
    doc.setTextColor(150, 150, 150);
    doc.setFont(fontFamily, "normal");
    doc.text("PravoTek — Kronologija spisa", margin, pageH - 9);
    doc.text(`Stranica ${i} / ${totalPages}`, pageW - margin, pageH - 9, { align: "right" });
  }

  const filename = `Kronologija_${new Date().toISOString().slice(0, 10)}.pdf`;
  doc.save(filename);
}
