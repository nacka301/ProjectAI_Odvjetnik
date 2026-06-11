using System.Threading.Tasks;
using Backend.Models;

namespace Backend.Services;

/// <summary>
/// Sučelje za autentifikacijski servis.
/// Apstrahira operacije registracije i verifikacije e-maila
/// kako bi se osigurala inverzija ovisnosti (DIP) i olakšalo testiranje (Mocking).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Asinkrono registrira novog korisnika u sustav.
    /// </summary>
    /// <param name="request">Podaci potrebni za registraciju korisnika.</param>
    /// <returns>Odgovor koji sadrži rezultat registracije.</returns>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Asinkrono provjerava i verificira e-mail adresu pomoću tokena.
    /// </summary>
    /// <param name="request">Podaci koji sadrže token za verifikaciju.</param>
    /// <returns>Odgovor o uspješnosti verifikacije e-maila.</returns>
    Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request);
}