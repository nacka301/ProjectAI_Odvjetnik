using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Backend.Services;

/// <summary>
/// Extracts text from PDF files with layout-aware parsing.
/// Dual strategy: word-level spatial reconstruction + basic extraction.
/// Picks the best result per page using quality scoring.
/// Handles tables, columns, Croatian diacritics, and scanned-PDF detection.
/// </summary>
public class PdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger) => _logger = logger;

    // Croatian legal/property keywords — used to score extraction quality
    private static readonly string[] QualityKeywords =
    [
        "čestica", "cestica", "katastarska", "katastar", "vlasnik",
        "temeljem", "uknjižba", "upis", "općina", "opcina",
        "nekretnina", "kuća", "kuca", "stan", "zemljište", "zemljiste",
        "etažni", "etazni", "vlastovnica", "posjedovnica", "teretovnica",
        "m²", "m2", "čest", "zk.ul", "zkul", "z.k.", "gruntovnica",
        "hipoteka", "založno", "teret", "plomba", "zabilježba"
    ];

    /// <summary>
    /// Extract text from an uploaded PDF using dual-strategy best-of approach.
    /// </summary>
    public string ExtractText(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return ExtractTextFromStream(stream, file.FileName, file.Length);
    }

    /// <summary>
    /// Core extraction logic — works on a stream.
    /// </summary>
    private string ExtractTextFromStream(Stream stream, string fileName, long fileSize)
    {
        using var document = PdfDocument.Open(stream);
        int pageCount = document.NumberOfPages;
        _logger.LogInformation("PDF '{File}': {Pages} pages, {Size:N0} bytes",
            fileName, pageCount, fileSize);

        var sb = new StringBuilder();
        int emptyPages = 0;

        foreach (var page in document.GetPages())
        {
            // ── Strategy A: Layout-aware word reconstruction ──
            string wordText = "";
            try { wordText = ExtractWithLayoutReconstruction(page); }
            catch (Exception ex)
            {
                _logger.LogDebug("Page {N}: word extraction error — {Err}", page.Number, ex.Message);
            }

            // ── Strategy B: Basic page.Text ──
            string basicText = page.Text ?? "";

            // ── Pick the winner for this page ──
            int scoreWord = ScoreExtraction(wordText);
            int scoreBasic = ScoreExtraction(basicText);
            bool useWord = scoreWord > scoreBasic && scoreWord > 10;

            string bestText = useWord ? wordText : basicText;
            bestText = NormalizePageText(bestText);

            if (string.IsNullOrWhiteSpace(bestText))
            {
                emptyPages++;
                continue;
            }

            _logger.LogDebug("Page {N}: {Len} chars, strategy={S} (word={SW}, basic={SB})",
                page.Number, bestText.Length,
                useWord ? "word-layout" : "basic",
                scoreWord, scoreBasic);

            sb.AppendLine(bestText);
            if (pageCount > 1) sb.AppendLine(); // breathing room between pages
        }

        var text = sb.ToString().Trim();
        text = NormalizeFinalText(text);

        // ── Diagnostics ──
        if (emptyPages > 0)
            _logger.LogWarning("PDF '{File}': {Empty}/{Total} pages empty (scanned/image-based?)",
                fileName, emptyPages, pageCount);

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogError(
                "PDF '{File}': ZERO text extracted from {Pages} pages. Likely scanned without OCR.",
                fileName, pageCount);
            return
                $"[UPOZORENJE: PDF ne sadrži čitljiv tekst — {pageCount} stranica, " +
                "vjerojatno skenirani dokument bez OCR sloja. " +
                "Molimo unesite podatke ručno u tekstualni način rada.]";
        }

        _logger.LogInformation("PDF '{File}' → {Chars} chars extracted ({Empty} empty pages skipped)",
            fileName, text.Length, emptyPages);

        return text;
    }

    // ══════════════════════════════════════════════════════════
    //  STRATEGY A — Word-level spatial layout reconstruction
    //  Groups words by Y-coordinate into lines, preserves table
    //  columns with pipe separators. Much better for tabular data.
    // ══════════════════════════════════════════════════════════

    private static string ExtractWithLayoutReconstruction(Page page)
    {
        var words = page.GetWords()?.ToList();
        if (words is null || words.Count == 0)
            return "";

        // ── Detect letter-by-letter extraction (broken font/encoding) ──
        // If median word length ≤ 1, the PDF writer stored each letter as
        // a separate text run — word extraction is useless, fall back.
        var wordLengths = words.Select(w => w.Text.Length).OrderBy(l => l).ToList();
        if (wordLengths[wordLengths.Count / 2] <= 1)
            return ""; // trigger fallback to basic page.Text

        // ── Adaptive line threshold: ~45% of median word height ──
        var heights = words
            .Select(w => w.BoundingBox.Height)
            .Where(h => h > 0.5) // skip zero-height artifacts
            .OrderBy(h => h)
            .ToList();
        if (heights.Count == 0) return "";
        double medianHeight = heights[heights.Count / 2];
        double lineThreshold = Math.Max(medianHeight * 0.45, 1.0);

        // ── Group words into lines by baseline Y ──
        // PDF coord: Y=0 at bottom, higher Y = higher on page
        var lines = new List<(double Y, List<Word> Words)>();

        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            double wY = word.BoundingBox.Bottom; // baseline
            var match = lines.FirstOrDefault(l =>
                Math.Abs(l.Y - wY) <= lineThreshold);

            if (match.Words is not null)
                match.Words.Add(word);
            else
                lines.Add((wY, [word]));
        }

        // ── Build output: line by line, top→bottom, words left→right ──
        var sb = new StringBuilder();
        foreach (var (_, lineWords) in lines.OrderByDescending(l => l.Y))
        {
            var lr = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();

            for (int i = 0; i < lr.Count; i++)
            {
                if (i > 0)
                {
                    var prev = lr[i - 1];
                    var curr = lr[i];
                    double gap = curr.BoundingBox.Left - prev.BoundingBox.Right;
                    double avgCharW = prev.Text.Length > 0
                        ? prev.BoundingBox.Width / prev.Text.Length
                        : 5.0;

                    if (gap > avgCharW * 4)
                        sb.Append("  |  "); // table column break
                    else if (gap > avgCharW * 2)
                        sb.Append("    "); // wide gap
                    else
                        sb.Append(' ');
                }
                sb.Append(lr[i].Text);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  QUALITY SCORING — which strategy extracted better text?
    // ══════════════════════════════════════════════════════════

    private static int ScoreExtraction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        int letters = text.Count(char.IsLetterOrDigit);
        int totalNonWS = text.Count(c => !char.IsWhiteSpace(c));
        if (totalNonWS == 0) return 0;

        // Letter ratio: penalize high special-char density (gibberish)
        double ratio = (double)letters / totalNonWS;

        // Keyword bonus: Croatian legal/property terms found
        var lower = text.ToLowerInvariant();
        int kwBonus = QualityKeywords.Count(kw => lower.Contains(kw)) * 40;

        // Length bonus (diminishing returns)
        int lenBonus = Math.Min(letters / 5, 200);

        return (int)(letters * ratio) + kwBonus + lenBonus;
    }

    // ══════════════════════════════════════════════════════════
    //  TEXT NORMALIZATION
    // ══════════════════════════════════════════════════════════

    private static string NormalizePageText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Multiple spaces → double space max (preserve table alignment hints)
        text = Regex.Replace(text, @"[ \t]{3,}", "  ");

        // Trim each line
        text = string.Join("\n", text.Split('\n').Select(l => l.TrimEnd()));

        // Collapse 4+ blank lines to 2
        text = Regex.Replace(text, @"\n{4,}", "\n\n\n");

        return text.Trim();
    }

    private static string NormalizeFinalText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Fix common PDF ligatures
        text = text.Replace("\ufb01", "fi").Replace("\ufb02", "fl");

        // Fix broken Croatian diacritics (combining characters)
        // Some PDF fonts store č as c + combining caron (U+030C)
        if (text.Contains('\u030C') || text.Contains('\u0301') || text.Contains('\u0335'))
        {
            text = text
                .Replace("c\u030C", "č").Replace("C\u030C", "Č")
                .Replace("c\u0301", "ć").Replace("C\u0301", "Ć")
                .Replace("s\u030C", "š").Replace("S\u030C", "Š")
                .Replace("z\u030C", "ž").Replace("Z\u030C", "Ž")
                .Replace("d\u0335", "đ").Replace("D\u0335", "Đ");
        }

        // Remove null bytes / control characters (keep \n, \r, \t)
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Final trim + collapse excessive blank lines
        text = Regex.Replace(text, @"\n{4,}", "\n\n\n");

        return text.Trim();
    }
}
