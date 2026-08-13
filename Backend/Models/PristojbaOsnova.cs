namespace Backend.Models;

/// <summary>
/// Naslijeđena oznaka osnove za obračun sudske pristojbe.
///
/// Potječe iz vremena kad je troškovnik nosio jednu pristojbu pa je jedna oznaka mogla
/// opisati cijeli obračun. Od prelaska na NN 50/2026 pristojbi je više — svaka radnja
/// nosi svoju — i ishod je lista u <see cref="PristojbeRezultat"/>.
///
/// Enum je zadržan samo zato što spremljeni troškovnici u bazi nose polje
/// <c>pristojba_osnova</c> i moraju se i dalje moći pročitati. Za nove izračune koristi
/// <see cref="PristojbenaStavka"/>; ondje se iz oznake tarifnog broja vidi točno koja je
/// odredba primijenjena, umjesto grube podjele na „puna / polovica".
/// </summary>
public enum PristojbaOsnova
{
    /// <summary>Pristojba se obračunava u punom iznosu (Tar. br. 1 st. 1).</summary>
    Puna = 0,

    /// <summary>Pristojba je polovica iznosa iz Tar. br. 1 st. 1 (st. 2).</summary>
    Polovica = 1,

    /// <summary>Uredba za odabrane radnje ne propisuje pristojbu.</summary>
    Nema = 2
}
