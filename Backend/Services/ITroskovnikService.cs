using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TroskovnikService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITroskovnikService
{
    TroskovnikResponse Izracunaj(TroskovnikRequest request);

    /// <summary>Katalog svih tarifnih stavki.</summary>
    IReadOnlyList<TarifnaStavka> DohvatiKatalog();

    /// <summary>Bodovi prema Tbr. 7. toč. 1. za zadanu vrijednost predmeta spora.</summary>
    int Tbr71Bodovi(decimal vrijednost);
}