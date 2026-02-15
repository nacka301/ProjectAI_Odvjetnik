using Backend.Models;

namespace Backend.Services;

/// <summary>
/// V42: Norm Validity Engine — Croatian Legal Norm Registry & Temporal Lookup
/// 
/// Provides a comprehensive in-memory registry of Croatian laws relevant to
/// real estate, contract, and civil law practice. Supports:
///   1. Norm lookup by abbreviation or full name
///   2. Temporal validity — which version was in force on a given date
///   3. Court-style citation formatting (sudski način)
///   4. Validity status tracking (važeći / izmijenjen / ukinut)
///   5. NN (Narodne novine) URL generation
///
/// Data sources: Croatian official gazette (Narodne novine), zakon.hr
/// </summary>
public class NormValidityService
{
    private static readonly Dictionary<string, NormInfo> Registry = BuildRegistry();

    // ── Public API ──

    /// <summary>Get all registered norms.</summary>
    public IReadOnlyList<NormInfo> GetAllNorms()
        => Registry.Values.OrderBy(n => n.FullName).ToList();

    /// <summary>Look up a norm by abbreviation (case-insensitive).</summary>
    public NormInfo? GetNorm(string abbreviation)
        => Registry.GetValueOrDefault(abbreviation.Trim().ToUpperInvariant());

    /// <summary>Search norms by name fragment.</summary>
    public IReadOnlyList<NormInfo> SearchNorms(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        return Registry.Values
            .Where(n => n.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || n.Abbreviation.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.FullName)
            .ToList();
    }

    /// <summary>
    /// Determine which version of a law was in force on a given date.
    /// Returns temporal result with applicable version and warning if outdated.
    /// </summary>
    public TemporalNormResult? GetApplicableVersion(string abbreviation, DateOnly eventDate)
    {
        var norm = GetNorm(abbreviation);
        if (norm is null) return null;

        // Find all amendments published on or before the event date
        var applicableAmendments = norm.Amendments
            .Where(a => a.PublicationDate <= eventDate)
            .OrderBy(a => a.PublicationDate)
            .ToList();

        if (applicableAmendments.Count == 0)
        {
            return new TemporalNormResult
            {
                Abbreviation = norm.Abbreviation,
                FullName = norm.FullName,
                ApplicableNnVersion = "(zakon još nije bio na snazi)",
                CurrentNnVersion = norm.Amendments.LastOrDefault()?.NnNumber ?? "",
                ApplicableNnReference = "",
                IsOutdated = true,
                Warning = $"{norm.FullName} nije bio na snazi na datum {eventDate:dd.MM.yyyy.}",
                FormattedCitation = "",
                EventDate = eventDate
            };
        }

        var lastApplicable = applicableAmendments.Last();
        var currentLast = norm.Amendments.Last();
        var isOutdated = lastApplicable.NnNumber != currentLast.NnNumber;

        var applicableNnRef = "NN " + string.Join(", ", applicableAmendments.Select(a => a.NnNumber));

        var result = new TemporalNormResult
        {
            Abbreviation = norm.Abbreviation,
            FullName = norm.FullName,
            ApplicableNnVersion = lastApplicable.NnNumber,
            CurrentNnVersion = currentLast.NnNumber,
            ApplicableNnReference = applicableNnRef,
            IsOutdated = isOutdated,
            EventDate = eventDate,
            FormattedCitation = $"{norm.FullName} ({applicableNnRef})"
        };

        if (isOutdated)
        {
            result.Warning = $"UPOZORENJE: Na datum {eventDate:dd.MM.yyyy.} važila je verzija {norm.Abbreviation} " +
                             $"zaključno s NN {lastApplicable.NnNumber}. " +
                             $"Od tada je zakon mijenjan (zadnja izmjena: NN {currentLast.NnNumber}).";
        }

        return result;
    }

    /// <summary>
    /// Format a citation in full court style (sudski način).
    /// Example: "čl. 322. st. 1. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
    /// </summary>
    public CitedNorm FormatCitation(string abbreviation, string article,
        string? paragraph = null, string? point = null, DateOnly? eventDate = null)
    {
        var norm = GetNorm(abbreviation);
        if (norm is null)
        {
            return new CitedNorm
            {
                Abbreviation = abbreviation,
                Article = article,
                FormattedCitation = $"{article} {abbreviation}",
                Status = "NEPOZNAT"
            };
        }

        // Determine applicable NN reference
        string nnRef;
        string status;
        if (eventDate.HasValue)
        {
            var temporal = GetApplicableVersion(abbreviation, eventDate.Value);
            nnRef = temporal?.ApplicableNnReference ?? norm.NnReference;
            status = temporal?.IsOutdated == true ? "IZMIJENJEN" : StatusToString(norm.Status);
        }
        else
        {
            nnRef = norm.NnReference;
            status = StatusToString(norm.Status);
        }

        // Build citation parts
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(article))
            parts.Add(NormalizeArticle(article));
        if (!string.IsNullOrWhiteSpace(paragraph))
            parts.Add(NormalizeParagraph(paragraph));
        if (!string.IsNullOrWhiteSpace(point))
            parts.Add(NormalizePoint(point));

        // Full name in genitive (court style uses genitive case)
        var fullNameGenitive = GetGenitiveForm(norm.FullName);
        parts.Add(fullNameGenitive);
        parts.Add($"({nnRef})");

        var formatted = string.Join(" ", parts);

        return new CitedNorm
        {
            Abbreviation = norm.Abbreviation,
            FullName = norm.FullName,
            Article = article,
            Paragraph = paragraph ?? "",
            Point = point ?? "",
            NnReference = nnRef,
            Status = status,
            FormattedCitation = formatted,
            NnUrl = norm.NnUrl
        };
    }

    /// <summary>
    /// Generate a "NORM VALIDITY ENGINE" instruction block for AI prompts.
    /// Tells the AI how to cite, what format to use, and how to populate cited_norms.
    /// </summary>
    public static string GetPromptInstructionBlock(bool includeTemporalMode = false)
    {
        var block = """
            
            NORM VALIDITY ENGINE — CITATNA PRECIZNOST (Sudski način):
            Sve pravne odredbe MORAJU se citirati u punom sudskom formatu:
            Format: "čl. [broj]. st. [stavak]. [t. [točka].] [Puni naziv zakona] (NN [brojevi])"
            Primjer: "čl. 322. st. 1. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
            
            REGISTAR ZAKONA — obavezne NN oznake:
            ZOO = Zakon o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)
            ZZK = Zakon o zemljišnim knjigama (NN 91/96, 137/99, 114/01, 100/04, 107/07, 152/08, 55/13, 60/13, 108/17)
            ZV  = Zakon o vlasništvu i drugim stvarnim pravima (NN 91/96, 68/98, 137/99, 22/00, 73/00, 114/01, 79/06, 141/06, 146/08, 38/09, 153/09, 143/12, 152/14)
            OZ  = Ovršni zakon (NN 112/12, 25/13, 93/14, 55/16, 73/17, 131/20)
            ZPP = Zakon o parničnom postupku (NN 53/91, 91/92, 112/99, 117/03, 25/13, 89/14, 70/19)
            ObZ = Obiteljski zakon (NN 103/15, 98/19, 47/20)
            ZGradnja = Zakon o gradnji (NN 153/13, 20/17, 39/19, 125/19)
            ZZJB = Zakon o javnom bilježništvu (NN 78/93, 29/94, 162/98, 16/07, 75/09, 120/16)
            ZNS  = Zakon o najmu stanova (NN 91/96, 48/98, 66/98, 22/06, 68/18, 105/20)
            ZOIB = Zakon o osobnom identifikacijskom broju (NN 60/08, 0/12)
            ZPU  = Zakon o prostornom uređenju (NN 153/13, 65/17, 114/18, 39/19, 98/19)
            
            PRAVILO CITIRANJA:
            1. NIKADA ne citiraj samo kraticu (npr. "prema ZOO") — UVIJEK navedi članak i NN
            2. UVIJEK koristi puni naziv zakona pri prvom spominjanju
            3. Pri ponovnom spominjanju dopuštena je kratica s NN: "čl. 303. ZOO (NN 35/05, 29/18)"
            4. Članak + stavak + točka moraju biti precizni — ne zaokružuj
            5. Ako ne znaš točan članak, NE citiraj — radije napiši "sukladno odredbama [zakona]"
            
            CITED_NORMS OUTPUT:
            U JSON output OBAVEZNO dodaj polje "cited_norms" (array) sa svim citiranim zakonima:
            "cited_norms": [
              {
                "abbreviation": "ZOO",
                "full_name": "Zakon o obveznim odnosima",
                "article": "čl. 322.",
                "paragraph": "st. 1.",
                "point": "",
                "nn_reference": "NN 35/05, 41/08, 125/11, 78/15, 29/18",
                "status": "VAŽEĆI",
                "formatted_citation": "čl. 322. st. 1. Zakona o obveznim odnosima (NN 35/05, 41/08, 125/11, 78/15, 29/18)"
              }
            ]
            Svaki citirani članak mora imati vlastiti unos u cited_norms.
            Status može biti: "VAŽEĆI", "IZMIJENJEN", "UKINUT".
            """;

        if (includeTemporalMode)
        {
            block += """
                
                TEMPORALNI NAČIN (Temporal Law Mode):
                Ako je naveden datum pravnog događaja (npr. datum sklapanja ugovora),
                koristi verziju zakona koja je bila na snazi na taj datum.
                Primjer: Ugovor iz 2003. → primijeni ZOO (NN 53/91, 73/91, 111/93, 3/94, 7/96, 91/96, 112/99)
                         jer ZOO (NN 35/05) još nije bio donesen.
                Ako se primjenjuje starija verzija, OBAVEZNO dodaj upozorenje:
                "NAPOMENA: Primjenjuje se verzija zakona važeća na datum [datum]. 
                 Od tada je zakon izmijenjen (važeća verzija: NN [aktualni])."
                """;
        }

        return block;
    }

    // ── Private Helpers ──

    private static string NormalizeArticle(string article)
    {
        var a = article.Trim().TrimEnd('.');
        if (!a.StartsWith("čl", StringComparison.OrdinalIgnoreCase))
            a = "čl. " + a;
        if (!a.EndsWith('.'))
            a += ".";
        return a;
    }

    private static string NormalizeParagraph(string paragraph)
    {
        var p = paragraph.Trim().TrimEnd('.');
        if (!p.StartsWith("st", StringComparison.OrdinalIgnoreCase))
            p = "st. " + p;
        if (!p.EndsWith('.'))
            p += ".";
        return p;
    }

    private static string NormalizePoint(string point)
    {
        var t = point.Trim().TrimEnd('.');
        if (!t.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            t = "t. " + t;
        if (!t.EndsWith('.'))
            t += ".";
        return t;
    }

    private static string StatusToString(NormStatus status) => status switch
    {
        NormStatus.Važeći => "VAŽEĆI",
        NormStatus.Izmijenjen => "IZMIJENJEN",
        NormStatus.Ukinut => "UKINUT",
        _ => "NEPOZNAT"
    };

    /// <summary>
    /// Convert the nominative form of a law name to genitive (for court-style citations).
    /// Croatian grammar: "Zakon o ..." → "Zakona o ..."
    /// </summary>
    private static string GetGenitiveForm(string fullName)
    {
        if (fullName.StartsWith("Zakon ", StringComparison.OrdinalIgnoreCase))
            return "Zakona " + fullName[6..];
        if (fullName.StartsWith("Ovršni zakon", StringComparison.OrdinalIgnoreCase))
            return "Ovršnog zakona" + fullName[12..];
        if (fullName.StartsWith("Obiteljski zakon", StringComparison.OrdinalIgnoreCase))
            return "Obiteljskog zakona" + fullName[16..];
        if (fullName.StartsWith("Uredba ", StringComparison.OrdinalIgnoreCase))
            return "Uredbe " + fullName[7..];
        // Fallback — return as-is
        return fullName;
    }

    // ══════════════════════════════════════════════════════════════
    //  STATIC REGISTRY — Croatian Laws for Real Estate & Civil Law
    // ══════════════════════════════════════════════════════════════

    private static Dictionary<string, NormInfo> BuildRegistry()
    {
        var norms = new List<NormInfo>
        {
            // ── 1. Zakon o obveznim odnosima (ZOO) ──
            new()
            {
                Abbreviation = "ZOO",
                FullName = "Zakon o obveznim odnosima",
                Status = NormStatus.Važeći,
                Category = "Obvezno pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2005_03_35_707.html",
                Amendments =
                [
                    new() { NnNumber = "35/05", PublicationDate = new(2005, 3, 14), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "41/08", PublicationDate = new(2008, 4, 7), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "125/11", PublicationDate = new(2011, 11, 4), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "78/15", PublicationDate = new(2015, 7, 17), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "29/18", PublicationDate = new(2018, 3, 28), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 1b. Zakon o obveznim odnosima — stari (1991-2005) ──
            new()
            {
                Abbreviation = "ZOO-91",
                FullName = "Zakon o obveznim odnosima (1991)",
                Status = NormStatus.Ukinut,
                Category = "Obvezno pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1991_10_53_1333.html",
                Amendments =
                [
                    new() { NnNumber = "53/91", PublicationDate = new(1991, 10, 8), Type = AmendmentType.TemeljniTekst, Description = "Preuzeti savezni zakon" },
                    new() { NnNumber = "73/91", PublicationDate = new(1991, 12, 31), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "111/93", PublicationDate = new(1993, 12, 3), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "3/94", PublicationDate = new(1994, 1, 14), Type = AmendmentType.Ispravak, Description = "Ispravak" },
                    new() { NnNumber = "7/96", PublicationDate = new(1996, 1, 26), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "91/96", PublicationDate = new(1996, 10, 28), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "112/99", PublicationDate = new(1999, 10, 29), Type = AmendmentType.Izmjena, Description = "Izmjene" }
                ]
            },

            // ── 2. Zakon o zemljišnim knjigama (ZZK) ──
            new()
            {
                Abbreviation = "ZZK",
                FullName = "Zakon o zemljišnim knjigama",
                Status = NormStatus.Važeći,
                Category = "Zemljišnoknjižno pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1996_10_91_1576.html",
                Amendments =
                [
                    new() { NnNumber = "91/96", PublicationDate = new(1996, 10, 28), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "137/99", PublicationDate = new(1999, 12, 17), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "114/01", PublicationDate = new(2001, 12, 21), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "100/04", PublicationDate = new(2004, 7, 16), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "107/07", PublicationDate = new(2007, 10, 19), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "152/08", PublicationDate = new(2008, 12, 24), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "55/13", PublicationDate = new(2013, 5, 10), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "60/13", PublicationDate = new(2013, 5, 22), Type = AmendmentType.Ispravak, Description = "Ispravak" },
                    new() { NnNumber = "108/17", PublicationDate = new(2017, 11, 8), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 3. Zakon o vlasništvu i drugim stvarnim pravima (ZV) ──
            new()
            {
                Abbreviation = "ZV",
                FullName = "Zakon o vlasništvu i drugim stvarnim pravima",
                Status = NormStatus.Važeći,
                Category = "Stvarno pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1996_10_91_1580.html",
                Amendments =
                [
                    new() { NnNumber = "91/96", PublicationDate = new(1996, 10, 28), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "68/98", PublicationDate = new(1998, 5, 15), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "137/99", PublicationDate = new(1999, 12, 17), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "22/00", PublicationDate = new(2000, 2, 25), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "73/00", PublicationDate = new(2000, 7, 14), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "114/01", PublicationDate = new(2001, 12, 21), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "79/06", PublicationDate = new(2006, 7, 14), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "141/06", PublicationDate = new(2006, 12, 27), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "146/08", PublicationDate = new(2008, 12, 17), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "38/09", PublicationDate = new(2009, 3, 25), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "153/09", PublicationDate = new(2009, 12, 21), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "143/12", PublicationDate = new(2012, 12, 21), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "152/14", PublicationDate = new(2014, 12, 22), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 4. Ovršni zakon (OZ) ──
            new()
            {
                Abbreviation = "OZ",
                FullName = "Ovršni zakon",
                Status = NormStatus.Važeći,
                Category = "Ovrha",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2012_10_112_2421.html",
                Amendments =
                [
                    new() { NnNumber = "112/12", PublicationDate = new(2012, 10, 12), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "25/13", PublicationDate = new(2013, 2, 28), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "93/14", PublicationDate = new(2014, 7, 30), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "55/16", PublicationDate = new(2016, 6, 17), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "73/17", PublicationDate = new(2017, 7, 26), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "131/20", PublicationDate = new(2020, 11, 27), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 5. Zakon o parničnom postupku (ZPP) ──
            new()
            {
                Abbreviation = "ZPP",
                FullName = "Zakon o parničnom postupku",
                Status = NormStatus.Važeći,
                Category = "Parnično pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1991_10_53_1336.html",
                Amendments =
                [
                    new() { NnNumber = "53/91", PublicationDate = new(1991, 10, 8), Type = AmendmentType.TemeljniTekst, Description = "Preuzeti savezni zakon" },
                    new() { NnNumber = "91/92", PublicationDate = new(1992, 12, 29), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "112/99", PublicationDate = new(1999, 10, 29), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "117/03", PublicationDate = new(2003, 7, 22), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "25/13", PublicationDate = new(2013, 2, 28), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "89/14", PublicationDate = new(2014, 7, 23), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "70/19", PublicationDate = new(2019, 7, 24), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 6. Obiteljski zakon (ObZ) ──
            new()
            {
                Abbreviation = "OBZ",
                FullName = "Obiteljski zakon",
                Status = NormStatus.Važeći,
                Category = "Obiteljsko pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2015_09_103_1992.html",
                Amendments =
                [
                    new() { NnNumber = "103/15", PublicationDate = new(2015, 9, 28), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "98/19", PublicationDate = new(2019, 10, 16), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "47/20", PublicationDate = new(2020, 4, 17), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" }
                ]
            },

            // ── 7. Zakon o gradnji ──
            new()
            {
                Abbreviation = "ZGRADNJA",
                FullName = "Zakon o gradnji",
                Status = NormStatus.Važeći,
                Category = "Graditeljstvo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2013_12_153_3221.html",
                Amendments =
                [
                    new() { NnNumber = "153/13", PublicationDate = new(2013, 12, 20), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "20/17", PublicationDate = new(2017, 3, 3), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "39/19", PublicationDate = new(2019, 4, 18), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "125/19", PublicationDate = new(2019, 12, 20), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 8. Zakon o javnom bilježništvu (ZZJB) ──
            new()
            {
                Abbreviation = "ZZJB",
                FullName = "Zakon o javnom bilježništvu",
                Status = NormStatus.Važeći,
                Category = "Javno bilježništvo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1993_09_78_1535.html",
                Amendments =
                [
                    new() { NnNumber = "78/93", PublicationDate = new(1993, 8, 20), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "29/94", PublicationDate = new(1994, 4, 15), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "162/98", PublicationDate = new(1998, 12, 18), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "16/07", PublicationDate = new(2007, 2, 9), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "75/09", PublicationDate = new(2009, 6, 29), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "120/16", PublicationDate = new(2016, 12, 21), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 9. Zakon o odvjetništvu ──
            new()
            {
                Abbreviation = "ZODV",
                FullName = "Zakon o odvjetništvu",
                Status = NormStatus.Važeći,
                Category = "Odvjetništvo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1994_02_9_142.html",
                Amendments =
                [
                    new() { NnNumber = "9/94", PublicationDate = new(1994, 2, 4), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "117/08", PublicationDate = new(2008, 10, 13), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "75/09", PublicationDate = new(2009, 6, 29), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "18/11", PublicationDate = new(2011, 2, 11), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 10. Zakon o osobnom identifikacijskom broju (ZOIB) ──
            new()
            {
                Abbreviation = "ZOIB",
                FullName = "Zakon o osobnom identifikacijskom broju",
                Status = NormStatus.Važeći,
                Category = "OIB",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2008_05_60_2044.html",
                Amendments =
                [
                    new() { NnNumber = "60/08", PublicationDate = new(2008, 5, 26), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "0/12", PublicationDate = new(2012, 1, 1), Type = AmendmentType.Izmjena, Description = "Pročišćeni tekst" }
                ]
            },

            // ── 11. Zakon o najmu stanova (ZNS) ──
            new()
            {
                Abbreviation = "ZNS",
                FullName = "Zakon o najmu stanova",
                Status = NormStatus.Važeći,
                Category = "Najam",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/1996_10_91_1578.html",
                Amendments =
                [
                    new() { NnNumber = "91/96", PublicationDate = new(1996, 10, 28), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "48/98", PublicationDate = new(1998, 4, 3), Type = AmendmentType.Ispravak, Description = "Ispravak" },
                    new() { NnNumber = "66/98", PublicationDate = new(1998, 5, 13), Type = AmendmentType.Izmjena, Description = "Izmjene" },
                    new() { NnNumber = "22/06", PublicationDate = new(2006, 2, 24), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "68/18", PublicationDate = new(2018, 7, 27), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "105/20", PublicationDate = new(2020, 9, 25), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 12. Zakon o prostornom uređenju (ZPU) ──
            new()
            {
                Abbreviation = "ZPU",
                FullName = "Zakon o prostornom uređenju",
                Status = NormStatus.Važeći,
                Category = "Prostorno uređenje",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2013_12_153_3220.html",
                Amendments =
                [
                    new() { NnNumber = "153/13", PublicationDate = new(2013, 12, 20), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "65/17", PublicationDate = new(2017, 7, 7), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "114/18", PublicationDate = new(2018, 12, 19), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "39/19", PublicationDate = new(2019, 4, 18), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "98/19", PublicationDate = new(2019, 10, 16), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 13. Zakon o postupanju s nezakonito izgrađenim zgradama ──
            new()
            {
                Abbreviation = "ZPNIZ",
                FullName = "Zakon o postupanju s nezakonito izgrađenim zgradama",
                Status = NormStatus.Važeći,
                Category = "Legalizacija",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2012_07_86_1966.html",
                Amendments =
                [
                    new() { NnNumber = "86/12", PublicationDate = new(2012, 7, 26), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "143/13", PublicationDate = new(2013, 12, 3), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "65/17", PublicationDate = new(2017, 7, 7), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "14/19", PublicationDate = new(2019, 2, 8), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" }
                ]
            },

            // ── 14. Zakon o zakupu i kupoprodaji poslovnog prostora ──
            new()
            {
                Abbreviation = "ZZKPP",
                FullName = "Zakon o zakupu i kupoprodaji poslovnog prostora",
                Status = NormStatus.Važeći,
                Category = "Poslovni prostor",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2011_11_125_2507.html",
                Amendments =
                [
                    new() { NnNumber = "125/11", PublicationDate = new(2011, 11, 4), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "64/15", PublicationDate = new(2015, 6, 10), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" },
                    new() { NnNumber = "112/18", PublicationDate = new(2018, 12, 14), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 15. Zakon o nasljeđivanju ──
            new()
            {
                Abbreviation = "ZN",
                FullName = "Zakon o nasljeđivanju",
                Status = NormStatus.Važeći,
                Category = "Nasljedno pravo",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2003_04_48_620.html",
                Amendments =
                [
                    new() { NnNumber = "48/03", PublicationDate = new(2003, 3, 25), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "163/03", PublicationDate = new(2003, 10, 14), Type = AmendmentType.Ispravak, Description = "Ispravak" },
                    new() { NnNumber = "35/05", PublicationDate = new(2005, 3, 14), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "127/13", PublicationDate = new(2013, 10, 21), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "33/15", PublicationDate = new(2015, 3, 20), Type = AmendmentType.OdlukaUSRH, Description = "Odluka USRH" },
                    new() { NnNumber = "14/19", PublicationDate = new(2019, 2, 8), Type = AmendmentType.Izmjena, Description = "Izmjene" }
                ]
            },

            // ── 16. Stečajni zakon ──
            new()
            {
                Abbreviation = "SZ",
                FullName = "Stečajni zakon",
                Status = NormStatus.Važeći,
                Category = "Stečaj",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2015_07_71_1367.html",
                Amendments =
                [
                    new() { NnNumber = "71/15", PublicationDate = new(2015, 6, 26), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" },
                    new() { NnNumber = "104/17", PublicationDate = new(2017, 10, 25), Type = AmendmentType.Izmjena, Description = "Izmjene i dopune" }
                ]
            },

            // ── 17. Zakon o zaštiti potrošača ──
            new()
            {
                Abbreviation = "ZZP",
                FullName = "Zakon o zaštiti potrošača",
                Status = NormStatus.Važeći,
                Category = "Zaštita potrošača",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2022_03_19_232.html",
                Amendments =
                [
                    new() { NnNumber = "19/22", PublicationDate = new(2022, 2, 9), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" }
                ]
            },

            // ── 18. Uredba (EU) 2016/679 — GDPR ──
            new()
            {
                Abbreviation = "GDPR",
                FullName = "Uredba (EU) 2016/679 — Opća uredba o zaštiti podataka",
                Status = NormStatus.Važeći,
                Category = "Zaštita podataka",
                NnUrl = "https://eur-lex.europa.eu/legal-content/HR/TXT/?uri=CELEX%3A32016R0679",
                Amendments =
                [
                    new() { NnNumber = "EU 2016/679", PublicationDate = new(2016, 4, 27), Type = AmendmentType.TemeljniTekst, Description = "Opća uredba — izravno primjenjiva u RH od 25.05.2018." }
                ]
            },

            // ── 19. Zakon o provedbi Opće uredbe o zaštiti podataka ──
            new()
            {
                Abbreviation = "ZPOUZP",
                FullName = "Zakon o provedbi Opće uredbe o zaštiti podataka",
                Status = NormStatus.Važeći,
                Category = "Zaštita podataka",
                NnUrl = "https://narodne-novine.nn.hr/clanci/sluzbeni/2018_05_42_805.html",
                Amendments =
                [
                    new() { NnNumber = "42/18", PublicationDate = new(2018, 5, 9), Type = AmendmentType.TemeljniTekst, Description = "Temeljni tekst" }
                ]
            }
        };

        // Compute derived fields
        foreach (var norm in norms)
        {
            norm.Amendments = norm.Amendments.OrderBy(a => a.PublicationDate).ToList();
            if (norm.Amendments.Count > 0)
            {
                norm.OriginalDate = norm.Amendments.First().PublicationDate;
                norm.LastAmendmentDate = norm.Amendments.Last().PublicationDate;
                norm.NnReference = "NN " + string.Join(", ", norm.Amendments.Select(a => a.NnNumber));
                norm.NnReferenceShort = norm.Amendments.Count > 2
                    ? $"NN {norm.Amendments.First().NnNumber}, {norm.Amendments.Last().NnNumber}"
                    : norm.NnReference;
            }
        }

        // Build lookup — key by uppercase abbreviation + common aliases
        var registry = new Dictionary<string, NormInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var norm in norms)
        {
            registry[norm.Abbreviation] = norm;
        }

        // Aliases for common alternative spellings
        registry["OBZ"] = registry["OBZ"]; // already there
        registry["OBZAKON"] = registry["OBZ"];
        registry["ZAKON O GRADNJI"] = registry["ZGRADNJA"];
        registry["ZOG"] = registry["ZGRADNJA"];
        registry["ZAKON O ODVJETNIŠTVU"] = registry["ZODV"];
        registry["ZAKON O OIB"] = registry["ZOIB"];
        registry["ZOO91"] = registry["ZOO-91"];

        return registry;
    }
}
