using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za TroskovnikService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface ITroskovnikService
{
    TroskovnikResponse Izracunaj(TroskovnikRequest request) ;
}