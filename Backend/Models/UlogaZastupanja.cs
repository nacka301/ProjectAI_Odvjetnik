namespace Backend.Models;

/// <summary>
/// Koga odvjetnik zastupa u kaznenom ili prekršajnom postupku. Određuje koji se
/// postotak Tarife primjenjuje na radnje iz Tbr. 3 i Tbr. 4.
/// </summary>
public static class UlogaZastupanja
{
    /// <summary>Branitelj okrivljenika / optuženika / okrivljene pravne osobe — puna nagrada.</summary>
    public const string Branitelj = "BRANITELJ";

    /// <summary>Punomoćnik privatnog tužitelja — puna nagrada (Tbr. 4. toč. 1).</summary>
    public const string PrivatniTuzitelj = "PRIVATNI_TUZITELJ";

    /// <summary>Punomoćnik oštećenika kao (supsidijarnog) tužitelja — puna nagrada (Tbr. 4. toč. 1).</summary>
    public const string OstecenikKaoTuzitelj = "OSTECENIK_KAO_TUZITELJ";

    /// <summary>Punomoćnik oštećenika / žrtve kad progoni državno odvjetništvo — 50% (Tbr. 3. toč. 3, Tbr. 4. toč. 8).</summary>
    public const string PunomocnikOstecenika = "PUNOMOCNIK_OSTECENIKA";

    public static readonly IReadOnlyList<string> Sve =
        [Branitelj, PrivatniTuzitelj, OstecenikKaoTuzitelj, PunomocnikOstecenika];

    public static bool JePoznata(string? uloga) => uloga is not null && Sve.Contains(uloga);

    /// <summary>
    /// Tarifni broj stavke kataloga ("Tbr. 4.1" → 4); 0 kad se ne može pročitati.
    /// </summary>
    public static int TarifniBrojStavke(string? tarifniBroj)
    {
        if (string.IsNullOrWhiteSpace(tarifniBroj)) return 0;
        var s = tarifniBroj.Trim();
        if (s.StartsWith("Tbr.", StringComparison.OrdinalIgnoreCase)) s = s[4..].Trim();
        var kraj = 0;
        while (kraj < s.Length && char.IsDigit(s[kraj])) kraj++;
        return kraj > 0 && int.TryParse(s[..kraj], out var n) ? n : 0;
    }

    /// <summary>Tbr. 3 i Tbr. 4 — jedini tarifni brojevi na koje se primjenjuje umanjenje oštećenika.</summary>
    public static bool UmanjujeUloga(string? uloga, string? tarifniBroj) =>
        uloga == PunomocnikOstecenika && TarifniBrojStavke(tarifniBroj) is 3 or 4;

    /// <summary>
    /// Naziv radnje prilagođen ulozi: oštećenik ili tužitelj ne „brani", nego „zastupa".
    /// „Obrana okrivljenika na raspravi" → „Zastupanje oštećenika na raspravi". Branitelj
    /// i nepoznata uloga dobivaju naziv iz kataloga.
    /// </summary>
    public static string OpisZaUlogu(string opis, string? uloga)
    {
        if (uloga is null or Branitelj || !JePoznata(uloga)) return opis;

        var subjekt = uloga switch
        {
            PrivatniTuzitelj => "privatnog tužitelja",
            OstecenikKaoTuzitelj => "oštećenika kao tužitelja",
            _ => "oštećenika",
        };

        if (opis.StartsWith("Obrana okrivljenika", StringComparison.Ordinal))
            return "Zastupanje " + subjekt + opis["Obrana okrivljenika".Length..];
        if (opis.StartsWith("Obrana ", StringComparison.Ordinal))
            return "Zastupanje " + opis["Obrana ".Length..];
        return opis;
    }

    /// <summary>Točka Tarife uz koju stoji umanjenje — za napomenu na ispisu.</summary>
    public static string IzvorUmanjenja(string? tarifniBroj) =>
        TarifniBrojStavke(tarifniBroj) == 3 ? "Tbr. 3 t. 3 Tarife" : "Tbr. 4 t. 8 Tarife";
}
