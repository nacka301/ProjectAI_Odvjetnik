namespace Backend.Models;

// GLAVA DRUGA, XV. NAKNADA TROŠKOVA (Tbr. 48.-51.) — stvarni izdaci odvjetnika, izvan
// bodovnog sustava. Vrijednosti su string-konstante po istom obrascu kao OsnovicaTbr7/
// RazinaSuda u TarifaOsnovica.cs, ne C# enum, radi dosljednosti s ostatkom baze.
//
// Ovi izdaci su odvjetnikov VLASTITI trošak obavljanja usluge (plaćeni u njegovo ime),
// pa po čl. 33. Zakona o PDV-u i čl. 78. Direktive Vijeća 2006/112/EZ ULAZE u poreznu
// osnovicu — za razliku od sudske pristojbe i predujma, koji se plaćaju u ime i za
// račun stranke (prolazne stavke).

/// <summary>Vrsta stvarnog izdatka iz naknade troškova (Tbr. 48.-51.).</summary>
public static class VrstaTroska
{
    /// <summary>Tbr. 49./50. — naknada za prijevoz izvan sjedišta odvjetničke pisarnice.</summary>
    public const string Prijevoz = "prijevoz";

    /// <summary>Tbr. 51. — dnevnica, u visini dnevnice suca Županijskog suda.</summary>
    public const string Dnevnica = "dnevnica";

    /// <summary>Tbr. 51. — troškovi hotelskog smještaja.</summary>
    public const string HotelskiSmjestaj = "hotelski_smjestaj";

    /// <summary>Tbr. 48. — poštanski, telefonski i bankarski troškovi te ostali izdaci.</summary>
    public const string OstaliIzdaci = "ostali_izdaci";

    public static readonly string[] Sve =
    [
        Prijevoz, Dnevnica, HotelskiSmjestaj, OstaliIzdaci
    ];

    /// <summary>Odredba Tarife uz svaku vrstu izdatka, za ispis uz stavku.</summary>
    public static readonly Dictionary<string, string> PravniIzvor = new()
    {
        [Prijevoz]          = $"{TarifaConstants.NazivTarife}, Tbr. 49. i 50.",
        [Dnevnica]           = $"{TarifaConstants.NazivTarife}, Tbr. 51.",
        [HotelskiSmjestaj]   = $"{TarifaConstants.NazivTarife}, Tbr. 51.",
        [OstaliIzdaci]       = $"{TarifaConstants.NazivTarife}, Tbr. 48.",
    };

    /// <summary>
    /// Vrste izdatka za koje je dopušten izračun (količina × jedinični iznos) umjesto
    /// samo slobodnog unosa — Tbr. 50 (km) i Tbr. 51 (dani) imaju propisanu jediničnu
    /// formulu. Hotelski smještaj i ostali izdaci nemaju propisanu jediničnu cijenu, pa
    /// ostaju isključivo slobodan unos.
    /// </summary>
    public static readonly string[] DopustaIzracun = [Prijevoz, Dnevnica];
}

/// <summary>Način unosa iznosa naknade troškova — slobodan ili izračunat.</summary>
public static class NacinNaknade
{
    /// <summary>Iznos je slobodno upisan (zadano).</summary>
    public const string Rucno = "rucno";

    /// <summary>
    /// Iznos se izvodi iz količine × jediničnog iznosa (+ cestarina za prijevoz).
    /// Dopušteno samo za vrste u <see cref="VrstaTroska.DopustaIzracun"/>.
    /// </summary>
    public const string Izracun = "izracun";

    public static readonly string[] Sve = [Rucno, Izracun];
}
