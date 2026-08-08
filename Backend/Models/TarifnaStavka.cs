using System.Text.Json.Serialization;

namespace Backend.Models;

// Definicija jedne stavke tarifnog kataloga; podaci zive u TarifaKatalog.

public sealed class TarifnaStavka
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kategorija")]
    public string Kategorija { get; set; } = "";

    [JsonPropertyName("opis")]
    public string Opis { get; set; } = "";

    [JsonPropertyName("tarifni_broj")]
    public string TarifniBroj { get; set; } = "";

    /// <summary>Fiksni bodovi (null = koristi Tbr71 lookup).</summary>
    [JsonPropertyName("fiksni_body")]
    public int? FiksniBody { get; set; }

    /// <summary>Postotak od Tbr. 7.1 (1.0 = 100%, 0.5 = 50%, itd.).</summary>
    [JsonPropertyName("tbr71_postotak")]
    public decimal? Tbr71Postotak { get; set; }

    /// <summary>Dodatno povećanje na Tbr. 7.1 rezultat (0.25 = +25%).</summary>
    [JsonPropertyName("tbr71_povecanje")]
    public decimal? Tbr71Povecanje { get; set; }

    /// <summary>Maksimalni broj bodova.</summary>
    [JsonPropertyName("max_bodova")]
    public int? MaxBodova { get; set; }

    /// <summary>Minimalni broj bodova.</summary>
    [JsonPropertyName("min_bodova")]
    public int? MinBodova { get; set; }

    /// <summary>Jedinica mjere (komad, sat, stranica).</summary>
    [JsonPropertyName("jedinica")]
    public string Jedinica { get; set; } = "komad";

    /// <summary>Redoslijed za prikaz.</summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    private string? _pravniIzvor;

    /// <summary>
    /// Propis i tarifni broj iz kojeg nagrada proizlazi. Prikazuje se u sučelju uz
    /// stavku i uz svaki redak troškovnika, da se svaki iznos može provjeriti u izvoru.
    ///
    /// Ako nije postavljen, sastavlja se od naziva Tarife i vlastitog tarifnog broja.
    /// Stavke čija se nagrada izvodi iz DRUGOG tarifnog broja postavljaju ga izričito,
    /// s uputom na taj broj.
    /// </summary>
    [JsonPropertyName("pravni_izvor")]
    public string PravniIzvor
    {
        get => _pravniIzvor ?? $"{TarifaConstants.NazivTarife}, {TarifniBroj}";
        set => _pravniIzvor = value;
    }

    /// <summary>True = bodovi dolaze iz ZaprijecenaKaznaTiers lookup-a (kazneni postupak).</summary>
    [JsonPropertyName("kazneni_base")]
    public bool KazneniBase { get; set; }

    /// <summary>Dodatno povećanje na kazneni base bodove (npr. 0.5 = +50% za žalbu).</summary>
    [JsonPropertyName("kazneni_povecanje")]
    public decimal? KazneniPovecanje { get; set; }

    /// <summary>
    /// Povećanje koje se primjenjuje umjesto <see cref="KazneniPovecanje"/> kad je
    /// zapriječena kazna dugotrajni zatvor.
    ///
    /// Tbr. 4. toč. 2 al. 3: "(...) s povišenjem od 25%, a na raspravi pred sudom
    /// drugog i trećeg stupnja za kaznena djela za koja je predviđena kazna
    /// dugotrajnog zatvora s povišenjem od 50%."
    /// </summary>
    [JsonPropertyName("kazneni_povecanje_dugotrajni")]
    public decimal? KazneniPovecanjeDugotrajni { get; set; }

    /// <summary>True = prekršajni stavka s mogućnošću oduzimanja; kad je oduzimanje aktivno, koristi Tbr. 7.1 po vrijednosti.</summary>
    [JsonPropertyName("prekrsaj_oduzimanje")]
    public bool PrekrsajOduzimanje { get; set; }

    // ── Jednokratna nagrada (Tbr. 7. toč. 2 i 3, Tbr. 8. toč. 1) ──
    //
    // Tarifa na više mjesta propisuje JEDNU nagradu za cijeli prvostupanjski
    // postupak, neovisno o broju obavljenih radnji. Takve se stavke ne množe
    // količinom; količina samo određuje koliko radnji prelazi uključeni broj.

    /// <summary>True = jedna nagrada za cijeli postupak, ne množi se količinom.</summary>
    [JsonPropertyName("jednokratna_nagrada")]
    public bool JednokratnaNagrada { get; set; }

    /// <summary>Koliko je radnji obuhvaćeno osnovicom (Tbr. 7. toč. 2 i 3: "najviše do četiri radnje").</summary>
    [JsonPropertyName("ukljucene_radnje")]
    public int? UkljuceneRadnje { get; set; }

    /// <summary>Bodovi za svaku radnju preko <see cref="UkljuceneRadnje"/> ("Za svaku daljnju radnju... 50 bodova").</summary>
    [JsonPropertyName("daljnja_radnja_bodovi")]
    public int? DaljnjaRadnjaBodovi { get; set; }
}
