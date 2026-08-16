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

/// <summary>Ishod obračuna sudske pristojbe.</summary>
/// <param name="Osnova">Po kojoj se osnovi pristojba obračunava; ostaje popunjena i kad je stranka oslobođena.</param>
/// <param name="Iznos">Iznos u EUR; 0 kad je stranka oslobođena ili kad Uredba pristojbu ne propisuje.</param>
/// <param name="VpsUdio">
/// Vrijednost predmeta spora na koju je pristojba stvarno obračunata. Razlikuje se od
/// ukupnog VPS-a samo kad je dio suparničara oslobođen — čl. 12 Zakona o sudskim pristojbama.
/// </param>
/// <param name="NaTeretProtustranke">
/// Čl. 13 — pristojba koju bi platila oslobođena stranka, razmjerno njezinu uspjehu u
/// postupku, a koju plaća protivna stranka. Obveza nastaje po pravomoćnosti, pa iznos
/// stoji zasebno i ne ulazi u ukupan trošak zastupane stranke. 0 kad se ne obračunava.
/// </param>
public readonly record struct PristojbaRezultat(
    PristojbaOsnova Osnova,
    decimal Iznos,
    decimal VpsUdio,
    decimal NaTeretProtustranke);
