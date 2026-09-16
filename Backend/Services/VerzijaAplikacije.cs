namespace Backend.Services;

/// <summary>
/// Koji je commit backenda pokrenut — zapisuje se uz spremljeni troškovnik, da se
/// prijavljena greška u izračunu može reproducirati na istom kodu.
///
/// Render ga daje kao varijablu okruženja <c>RENDER_GIT_COMMIT</c>. Lokalno i u
/// testovima nje nema, pa je vrijednost null — to je ispravno stanje, ne greška:
/// zapis se sprema i bez nje.
/// </summary>
public static class VerzijaAplikacije
{
    public static string? Commit { get; } = Normaliziraj(Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT"));

    /// <summary>Prazno postaje null; predugačko se reže da ne zatrpa stupac.</summary>
    internal static string? Normaliziraj(string? vrijednost)
    {
        if (string.IsNullOrWhiteSpace(vrijednost))
            return null;

        var ocisceno = vrijednost.Trim();
        return ocisceno.Length > 64 ? ocisceno[..64] : ocisceno;
    }
}
