namespace Backend.Services;

/// <summary>
/// Zahtjev za troškovnik koji se ne može valjano izračunati.
///
/// Postoji zato da nijedna stavka ne bi tiho nestala iz troškovnika ni tiho pala na
/// nula bodova. Odvjetnik koji troškovnik nosi sudu mora dobiti poruku što nedostaje,
/// a ne iznos manji nego što je odabrao.
///
/// Kontroler je pretvara u HTTP 400 s porukom iz <see cref="Exception.Message"/>.
/// </summary>
public sealed class TroskovnikValidationException : Exception
{
    public TroskovnikValidationException(string message) : base(message) { }
}
