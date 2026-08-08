namespace Backend.Models;

/// <summary>
/// Osnova za obračun sudske pristojbe prema Uredbi o tarifi sudskih pristojbi
/// (NN 37/2023). Prilog, POGLAVLJE I. nosi naslov "PARNIČNI I OVRŠNI POSTUPAK" —
/// izvan tog poglavlja Tar. br. 1 se ne primjenjuje.
/// </summary>
public enum PristojbaOsnova
{
    /// <summary>Tar. br. 1 st. 1 — puni iznos.</summary>
    Puna = 0,

    /// <summary>Tar. br. 1 st. 2 — polovica iznosa iz st. 1 (prijedlog za ovrhu, žalba, revizija...).</summary>
    Polovica = 1,

    /// <summary>Uredba ne propisuje pristojbu za ovu vrstu postupka (npr. kazneni).</summary>
    Nema = 2
}
