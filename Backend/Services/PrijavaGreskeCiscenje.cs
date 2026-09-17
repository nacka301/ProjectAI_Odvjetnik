using System.Text.Json.Nodes;

namespace Backend.Services;

/// <summary>
/// Iz ulaza i rezultata prijavljenog izračuna uklanja podatke stranaka i
/// odvjetnika, prije nego odu u bazu i u mail.
///
/// Za ponavljanje izračuna trebaju stavke, vrijednost predmeta, datumi i
/// zastavice — a ne tko je tužitelj. Imena, OIB-i i adrese stranaka odvjetnička
/// su tajna ureda, pa ne smiju završiti u tablici koju čita pružatelj usluge ni
/// u njegovom poštanskom sandučiću.
///
/// Uklanja se po nazivu ključa, na svakoj dubini. Popis je namjerno širok:
/// ključ koji nije potreban za izračun, a mogao bi nositi ime ili broj predmeta,
/// ide van. Sučelje ionako šalje ulaz bez stranaka; ovo je druga crta obrane.
/// </summary>
public static class PrijavaGreskeCiscenje
{
    private static readonly HashSet<string> Zabranjeni = new(StringComparer.OrdinalIgnoreCase)
    {
        "odvjetnik", "odvjetnik_info", "stranke", "stranke_info", "klijent", "zastupana_stranka",
        "protustranka", "predmet", "opis_predmeta", "napomena", "sud", "broj_predmeta",
        "poslovni_broj", "case_id", "client_id", "client_grad", "client_tip", "auto_create_client",
        "naziv", "ime", "oib", "adresa", "grad", "iban",
    };

    private static readonly string[] ZabranjeniPrefiksi = ["protustranka", "client_", "klijent", "odvjetnik"];

    public static bool JeZabranjen(string kljuc) =>
        Zabranjeni.Contains(kljuc)
        || ZabranjeniPrefiksi.Any(p => kljuc.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Kopija bez zabranjenih ključeva; ulaz se ne mijenja.</summary>
    public static JsonNode? Ocisti(JsonNode? cvor) => cvor switch
    {
        null => null,
        JsonObject objekt => OcistiObjekt(objekt),
        JsonArray niz => new JsonArray(niz.Select(Ocisti).ToArray()),
        _ => cvor.DeepClone(),
    };

    private static JsonObject OcistiObjekt(JsonObject objekt)
    {
        var rezultat = new JsonObject();
        foreach (var (kljuc, vrijednost) in objekt)
        {
            if (JeZabranjen(kljuc)) continue;
            rezultat[kljuc] = Ocisti(vrijednost);
        }
        return rezultat;
    }
}
