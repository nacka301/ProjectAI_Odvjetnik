"use client";

import { useState } from "react";
import { ClipboardCopy, Check, FileDown, Loader2 } from "lucide-react";

// ═══════════════════════════════════════════
//  SHARED RESULT ACTIONS — V58
//  Copy-to-clipboard + PDF export toolbar.
//  Reused across all modules with results.
// ═══════════════════════════════════════════

interface ResultActionsProps {
  /** Text content to copy to clipboard */
  copyText: string;
  /** Label for the copy button */
  copyLabel?: string;
  /** PDF export handler — if provided, shows the PDF button */
  onPdfExport?: () => Promise<void>;
  /** Label for PDF button */
  pdfLabel?: string;
  /** Extra className for the container */
  className?: string;
}

export default function ResultActions({
  copyText,
  copyLabel = "Kopiraj rezultat",
  onPdfExport,
  pdfLabel = "Preuzmi PDF",
  className = "",
}: ResultActionsProps) {
  const [copied, setCopied] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(copyText);
    } catch {
      // Fallback for older browsers
      const ta = document.createElement("textarea");
      ta.value = copyText;
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      document.execCommand("copy");
      document.body.removeChild(ta);
    }
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handlePdf = async () => {
    if (!onPdfExport) return;
    setPdfLoading(true);
    try {
      await onPdfExport();
    } catch {
      // PDF generation failed — non-critical
    } finally {
      setPdfLoading(false);
    }
  };

  return (
    <div className={`flex flex-wrap gap-2 ${className}`}>
      <button
        onClick={handleCopy}
        className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 transition"
      >
        {copied ? (
          <>
            <Check className="h-4 w-4 text-green-600" />
            <span className="text-green-600">Kopirano!</span>
          </>
        ) : (
          <>
            <ClipboardCopy className="h-4 w-4" />
            {copyLabel}
          </>
        )}
      </button>

      {onPdfExport && (
        <button
          onClick={handlePdf}
          disabled={pdfLoading}
          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-50 transition"
        >
          {pdfLoading ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" />
              Generiram PDF...
            </>
          ) : (
            <>
              <FileDown className="h-4 w-4" />
              {pdfLabel}
            </>
          )}
        </button>
      )}
    </div>
  );
}
