import jsPDF from "jspdf";

// ═══════════════════════════════════════════
//  PDF Font Loader — Croatian Character Support
//  Loads Roboto font from Google Fonts CDN
// ═══════════════════════════════════════════

let fontsLoaded = false;
let fontLoadPromise: Promise<void> | null = null;

// Google Fonts URLs for Roboto (supports Croatian šćžčđ)
const FONT_URLS = {
  regular: "https://fonts.gstatic.com/s/roboto/v32/KFOmCnqEu92Fr1Me5Q.ttf",
  bold: "https://fonts.gstatic.com/s/roboto/v32/KFOlCnqEu92Fr1MmWUlvAw.ttf",
  italic: "https://fonts.gstatic.com/s/roboto/v32/KFOkCnqEu92Fr1Mu52xP.ttf",
};

async function fetchFontAsArrayBuffer(url: string): Promise<ArrayBuffer> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Failed to fetch font: ${url}`);
  return response.arrayBuffer();
}

function arrayBufferToBase64(buffer: ArrayBuffer): string {
  let binary = "";
  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

/**
 * Load Croatian-compatible fonts into jsPDF.
 * Call this once before generating PDF, or the loadPdfFonts() will handle it.
 */
export async function loadPdfFonts(doc: jsPDF): Promise<void> {
  if (fontsLoaded) {
    // Add already-loaded fonts to this doc instance
    addFontsToDoc(doc);
    return;
  }
  
  if (fontLoadPromise) {
    await fontLoadPromise;
    addFontsToDoc(doc);
    return;
  }

  fontLoadPromise = (async () => {
    try {
      // Fetch all font variants in parallel
      const [regularBuffer, boldBuffer, italicBuffer] = await Promise.all([
        fetchFontAsArrayBuffer(FONT_URLS.regular),
        fetchFontAsArrayBuffer(FONT_URLS.bold),
        fetchFontAsArrayBuffer(FONT_URLS.italic),
      ]);

      // Convert to base64
      fontCache.regular = arrayBufferToBase64(regularBuffer);
      fontCache.bold = arrayBufferToBase64(boldBuffer);
      fontCache.italic = arrayBufferToBase64(italicBuffer);

      fontsLoaded = true;
    } catch (error) {
      console.warn("Failed to load custom fonts, falling back to Helvetica:", error);
    }
  })();

  await fontLoadPromise;
  addFontsToDoc(doc);
}

const fontCache: { regular?: string; bold?: string; italic?: string } = {};

function addFontsToDoc(doc: jsPDF): void {
  if (!fontsLoaded || !fontCache.regular) return;

  try {
    // Add Roboto fonts to jsPDF virtual file system
    doc.addFileToVFS("Roboto-Regular.ttf", fontCache.regular);
    doc.addFileToVFS("Roboto-Bold.ttf", fontCache.bold!);
    doc.addFileToVFS("Roboto-Italic.ttf", fontCache.italic!);

    // Register the fonts
    doc.addFont("Roboto-Regular.ttf", "Roboto", "normal");
    doc.addFont("Roboto-Bold.ttf", "Roboto", "bold");
    doc.addFont("Roboto-Italic.ttf", "Roboto", "italic");
  } catch (error) {
    console.warn("Failed to add fonts to jsPDF:", error);
  }
}

/**
 * Get the font family to use for text.
 * Returns "Roboto" if loaded, otherwise "helvetica".
 */
export function getPdfFont(): string {
  return fontsLoaded ? "Roboto" : "helvetica";
}

/**
 * Sanitize text for PDF output.
 * Replaces problematic characters if fonts aren't loaded.
 */
export function sanitizeTextForPdf(text: string): string {
  if (fontsLoaded) return text;
  
  // Fallback: replace Croatian characters with ASCII if fonts failed to load
  return text
    .replace(/š/g, "s").replace(/Š/g, "S")
    .replace(/ć/g, "c").replace(/Ć/g, "C")
    .replace(/č/g, "c").replace(/Č/g, "C")
    .replace(/ž/g, "z").replace(/Ž/g, "Z")
    .replace(/đ/g, "d").replace(/Đ/g, "D")
    // Replace common special characters
    .replace(/✓/g, "[OK]")
    .replace(/•/g, "-")
    .replace(/—/g, "-")
    .replace(/–/g, "-")
    .replace(/"/g, '"').replace(/"/g, '"')
    .replace(/'/g, "'").replace(/'/g, "'");
}
