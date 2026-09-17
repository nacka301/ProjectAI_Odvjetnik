using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Backend.Models;

// Prijava „Iznos mi ne izgleda točno" — plan pravne zaštite, dio D.
// Tablica `prijave_gresaka` je iz migracije 025 i čita je samo backend.

/// <summary>Što sučelje šalje. Identitet i ured dolaze iz tokena, nikad odavde.</summary>
public sealed class PrijavaGreskeZahtjev
{
    [JsonPropertyName("opis")]
    public string? Opis { get; set; }

    [JsonPropertyName("ocekivani_iznos")]
    public string? OcekivaniIznos { get; set; }

    /// <summary>Spremljeni troškovnik na koji se prijava odnosi; prazno za nespremljen obrazac.</summary>
    [JsonPropertyName("troskovnik_id")]
    public string? TroskovnikId { get; set; }

    /// <summary>Ulaz izračuna. Backend iz njega uklanja podatke stranaka prije upisa.</summary>
    [JsonPropertyName("ulazni_podaci")]
    public JsonNode? UlazniPodaci { get; set; }

    /// <summary>Prikazani rezultat. Također se čisti prije upisa.</summary>
    [JsonPropertyName("rezultat")]
    public JsonNode? Rezultat { get; set; }
}

/// <summary>Redak za `prijave_gresaka`. Serijalizira se u snake_case.</summary>
public sealed class PrijavaGreskeZapis
{
    public string? UserId { get; set; }
    public string LawFirmId { get; set; } = "";
    public string? TroskovnikId { get; set; }
    public string Opis { get; set; } = "";
    public string? OcekivaniIznos { get; set; }
    public JsonNode? UlazniPodaci { get; set; }
    public JsonNode? Rezultat { get; set; }
    public string? VerzijaIzracuna { get; set; }
}

public static class PrijavaGreskeStatus
{
    public const string Nova = "nova";
    public const string Potvrdena = "potvrdena";
    public const string NijeGreska = "nije_greska";
    public const string Ispravljena = "ispravljena";

    public static readonly IReadOnlySet<string> Svi =
        new HashSet<string> { Nova, Potvrdena, NijeGreska, Ispravljena };
}

public sealed class PrijavaGreskeStatusZahtjev
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

/// <summary>Granice iz CHECK ograničenja migracije 025 — provjeravaju se prije upisa.</summary>
public static class PrijavaGreskeGranice
{
    public const int OpisMin = 10;
    public const int OpisMax = 2000;
    public const int OcekivaniMax = 200;

    /// <summary>Najviše znakova JSON-a po polju; veći ulaz se ne sprema, da tablica ne naraste.</summary>
    public const int JsonMax = 200_000;
}
