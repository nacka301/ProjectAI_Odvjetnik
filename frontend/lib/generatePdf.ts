import jsPDF from "jspdf";
import autoTable from "jspdf-autotable";
import type { ValuationResult } from "@/app/wizard/types";

// ═══════════════════════════════════════════
//  PDF EXPORT — Professional valuation report
//  for lawyers / odvjetnici
// ═══════════════════════════════════════════

interface PdfParams {
  result: ValuationResult;
  propertyType: string;
  location: string;
  areaM2: number;
}

const fmt = (n: number) => n.toLocaleString("hr-HR");

export async function generateValuationPdf({ result, propertyType, location, areaM2 }: PdfParams) {
  const doc = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });
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
  doc.setFillColor(30, 64, 175); // blue-800
  doc.rect(0, 0, pageW, 38, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(20);
  doc.setFont("helvetica", "bold");
  doc.text("Izvještaj o procjeni tržišne vrijednosti", margin, 18);
  doc.setFontSize(10);
  doc.setFont("helvetica", "normal");
  doc.text(`Datum: ${new Date().toLocaleDateString("hr-HR")}`, margin, 28);
  doc.text("PravoTek — Automatizirana tržišna procjena", margin, 34);

  y = 48;

  // ── Property Info Box ──
  doc.setDrawColor(200, 200, 200);
  doc.setFillColor(248, 250, 252); // gray-50
  doc.roundedRect(margin, y, contentW, 30, 2, 2, "FD");
  doc.setTextColor(50, 50, 50);
  doc.setFontSize(9);
  doc.setFont("helvetica", "bold");

  const col1x = margin + 4;
  const col2x = margin + contentW / 3;
  const col3x = margin + (contentW * 2) / 3;

  doc.text("Tip nekretnine:", col1x, y + 8);
  doc.text("Lokacija:", col2x, y + 8);
  doc.text("Površina:", col3x, y + 8);

  doc.setFont("helvetica", "normal");
  doc.setFontSize(10);
  const typeLabel: Record<string, string> = { Apartment: "Stan", House: "Kuća", Land: "Zemljište" };
  doc.text(typeLabel[propertyType] || propertyType, col1x, y + 15);
  doc.text(location, col2x, y + 15);
  doc.text(`${fmt(areaM2)} m²`, col3x, y + 15);

  if (result.target_property_summary) {
    doc.setFontSize(8);
    doc.setTextColor(100, 100, 100);
    const summaryLines = doc.splitTextToSize(result.target_property_summary, contentW - 8);
    doc.text(summaryLines.slice(0, 2), col1x, y + 23);
  }

  y += 36;

  // ── Hero Value ──
  doc.setFillColor(30, 64, 175);
  doc.roundedRect(margin, y, contentW, 22, 2, 2, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(12);
  doc.setFont("helvetica", "bold");
  doc.text("PROCIJENJENA TRŽIŠNA VRIJEDNOST", margin + 4, y + 9);
  doc.setFontSize(18);
  doc.text(`${fmt(result.estimated_value_eur)} EUR`, pageW - margin - 4, y + 15, { align: "right" });

  y += 28;

  // ── Key Metrics ──
  doc.setTextColor(50, 50, 50);
  doc.setFontSize(11);
  doc.setFont("helvetica", "bold");
  doc.text("Ključne metrike", margin, y);
  y += 6;

  const metrics: [string, string][] = [
    ["Cijena po m²", result.calculated_price_per_m2 ? `${fmt(Math.round(result.calculated_price_per_m2))} EUR/m²` : "N/A"],
    ["Pronađeno oglasa", String(result.total_listings_found ?? "N/A")],
    ["Korištenih usporedbi", String(result.usable_price_points ?? "N/A")],
    ["Uklonjeno outliera", String(result.outliers_removed ?? 0)],
    ["Diskont tražene cijene", `${result.realization_discount_percent?.toFixed(1) ?? "15"}%`],
    ["Pouzdanost", result.math_confidence_score !== undefined ? `${result.math_confidence_score}/100 (${result.math_confidence_level ?? ""})` : "N/A"],
  ];

  if (result.market_average_asking) {
    metrics.push(["Prosjek traženih cijena/m²", `${fmt(Math.round(result.market_average_asking))} EUR/m²`]);
  }

  autoTable(doc, {
    startY: y,
    head: [],
    body: metrics,
    theme: "plain",
    styles: { fontSize: 9, cellPadding: 2.5 },
    columnStyles: {
      0: { fontStyle: "bold", cellWidth: 55, textColor: [80, 80, 80] },
      1: { textColor: [30, 30, 30] },
    },
    margin: { left: margin, right: margin },
  });

  y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;

  // ── Price Range ──
  if (result.price_range) {
    addPageIfNeeded(35);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Raspon procjene", margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["Scenarij", "EUR/m²", "Ukupno EUR"]],
      body: [
        ["Konzervativno", `${fmt(Math.round(result.price_range.conservative_price_per_m2))}`, `${fmt(result.price_range.conservative_total)}`],
        ["Realno", `${fmt(Math.round(result.price_range.realistic_price_per_m2))}`, `${fmt(result.price_range.realistic_total)}`],
        ["Optimistično", `${fmt(Math.round(result.price_range.optimistic_price_per_m2))}`, `${fmt(result.price_range.optimistic_total)}`],
      ],
      theme: "grid",
      headStyles: { fillColor: [30, 64, 175], fontSize: 9 },
      styles: { fontSize: 9, cellPadding: 3 },
      margin: { left: margin, right: margin },
    });

    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Comparison Table ──
  if (result.comparison_table && result.comparison_table.length > 0) {
    addPageIfNeeded(40);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Tablica usporednih nekretnina", margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["#", "Oglas", "m²", "Cijena", "EUR/m²", "Korekcija", "Prilagođeno", "Sličnost"]],
      body: result.comparison_table.map((row) => [
        String(row.index),
        row.title.slice(0, 30) + (row.title.length > 30 ? "…" : ""),
        row.area_m2 > 0 ? fmt(row.area_m2) : "—",
        row.total_price > 0 ? fmt(Math.round(row.total_price)) : "—",
        fmt(Math.round(row.raw_price_per_m2)),
        `${row.correction_percent > 0 ? "+" : ""}${row.correction_percent.toFixed(1)}%`,
        fmt(Math.round(row.adjusted_price_per_m2)),
        `${row.similarity_score}%`,
      ]),
      theme: "striped",
      headStyles: { fillColor: [30, 64, 175], fontSize: 7.5 },
      styles: { fontSize: 7.5, cellPadding: 2 },
      columnStyles: {
        0: { cellWidth: 8 },
        1: { cellWidth: 40 },
        7: { cellWidth: 16 },
      },
      margin: { left: margin, right: margin },
    });

    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Adjustments ──
  if (result.adjustments && result.adjustments.length > 0) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Primijenjene korekcije", margin, y);
    y += 5;

    autoTable(doc, {
      startY: y,
      head: [["Faktor", "Opis", "Postotak"]],
      body: result.adjustments.map((a) => [
        a.factor,
        a.description,
        `${a.percent > 0 ? "+" : ""}${a.percent.toFixed(1)}%`,
      ]),
      theme: "striped",
      headStyles: { fillColor: [30, 64, 175], fontSize: 8 },
      styles: { fontSize: 8, cellPadding: 2.5 },
      margin: { left: margin, right: margin },
    });

    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 6;
  }

  // ── Confidence Explanation ──
  if (result.math_confidence_explanation) {
    addPageIfNeeded(30);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text(`Pouzdanost procjene: ${result.math_confidence_score ?? "N/A"}/100`, margin, y);
    y += 5;
    doc.setFont("helvetica", "normal");
    doc.setFontSize(9);
    const confLines = doc.splitTextToSize(result.math_confidence_explanation, contentW);
    addPageIfNeeded(confLines.length * 4 + 4);
    doc.text(confLines, margin, y);
    y += confLines.length * 4 + 4;
  }

  // ── Benchmark ──
  if (result.benchmark_summary) {
    addPageIfNeeded(20);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Benchmark regije", margin, y);
    y += 5;
    doc.setFont("helvetica", "normal");
    doc.setFontSize(9);
    const benchLines = doc.splitTextToSize(result.benchmark_summary, contentW);
    addPageIfNeeded(benchLines.length * 4 + 4);
    doc.text(benchLines, margin, y);
    y += benchLines.length * 4 + 4;
  }

  // ── Valuation Reasoning ──
  if (result.valuation_reasoning) {
    addPageIfNeeded(25);
    doc.setFontSize(11);
    doc.setFont("helvetica", "bold");
    doc.setTextColor(50, 50, 50);
    doc.text("Stručno mišljenje", margin, y);
    y += 5;
    doc.setFont("helvetica", "normal");
    doc.setFontSize(9);
    doc.setTextColor(30, 30, 30);
    const reasonLines = doc.splitTextToSize(result.valuation_reasoning, contentW);
    // paginate long reasoning
    for (const line of reasonLines) {
      addPageIfNeeded(5);
      doc.text(line, margin, y);
      y += 4;
    }
    y += 4;
  }

  // ── Footer on every page ──
  const totalPages = doc.internal.pages.length - 1; // pages array is 1-indexed
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    const pageH = doc.internal.pageSize.getHeight();
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, pageH - 14, pageW - margin, pageH - 14);
    doc.setFontSize(7);
    doc.setTextColor(150, 150, 150);
    doc.setFont("helvetica", "normal");
    doc.text("PravoTek — Automatizirana tržišna procjena nekretnina", margin, pageH - 9);
    doc.text(`Stranica ${i} / ${totalPages}`, pageW - margin, pageH - 9, { align: "right" });
    doc.text("Ovaj izvještaj je generiran automatski i ne predstavlja službenu procjenu ovlaštenog sudskog vještaka.", margin, pageH - 5);
  }

  // ── Save ──
  const filename = `Procjena_${propertyType}_${location.replace(/[^a-zA-Z0-9ČčĆćŠšŽžĐđ]/g, "_")}_${new Date().toISOString().slice(0, 10)}.pdf`;
  doc.save(filename);
}
