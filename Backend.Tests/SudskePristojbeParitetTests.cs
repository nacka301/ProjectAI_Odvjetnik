using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Iznosi pojedinih odredbi Uredbe, iz zajedničke datoteke koju čita i frontend.
///
/// Frontend ponavlja backendov izračun da bi troškovnik imao živi prikaz, pa postoje
/// dva neovisna koda za isti propis. Sa 35 tarifnih brojeva ručno održavanje dvaju
/// popisa očekivanih iznosa nije održivo — zato oba testa čitaju
/// <c>TestData/pristojbe-paritet.json</c>. Razilaženje se tako ruši kao test, a ne
/// pojavi se kao dva različita iznosa na istom troškovniku.
///
/// Parnjak: <c>frontend/__tests__/tarifa-pristojbe.test.ts</c>.
/// </summary>
public class SudskePristojbeParitetTests
{
    private sealed record Slucaj(
        string Odredba,
        decimal Osnovica,
        string Uredba,
        decimal Ocekivano,
        string Opis);

    private sealed record Datoteka(
        [property: JsonPropertyName("slucajevi")] List<Slucaj> Slucajevi);

    private static readonly List<Slucaj> Slucajevi = Ucitaj();

    private static List<Slucaj> Ucitaj()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(SudskePristojbeParitetTests).Assembly.Location)!;

        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Backend.Tests.csproj")))
            dir = dir.Parent;

        var putanja = Path.Combine(dir?.FullName ?? assemblyDir, "TestData", "pristojbe-paritet.json");

        if (!File.Exists(putanja))
            throw new FileNotFoundException($"Nema datoteke s paritetnim slučajevima: {putanja}");

        var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var datoteka = JsonSerializer.Deserialize<Datoteka>(File.ReadAllText(putanja), opcije)
            ?? throw new InvalidOperationException("Paritetna datoteka se ne da pročitati.");

        return datoteka.Slucajevi;
    }

    public static TheoryData<int> Indeksi()
    {
        var podaci = new TheoryData<int>();
        for (var i = 0; i < Slucajevi.Count; i++)
            podaci.Add(i);

        return podaci;
    }

    [Theory]
    [MemberData(nameof(Indeksi))]
    public void OdredbaDajeIznosIzZajednickeDatoteke(int indeks)
    {
        var slucaj = Slucajevi[indeks];

        var verzija = slucaj.Uredba == "NN 37/2023"
            ? UredbaVerzija.Nn37_2023
            : UredbaVerzija.Nn50_2026;

        var iznos = SudskePristojbeCalculator.Izracunaj(slucaj.Odredba, slucaj.Osnovica, verzija);

        Assert.Equal(slucaj.Ocekivano, iznos);
    }

    /// <summary>
    /// Datoteka je zajednička, pa prazna ili odrezana datoteka ne smije proći kao
    /// „svi testovi zeleni".
    /// </summary>
    [Fact]
    public void PariteniSlucajeviNisuPrazni()
    {
        Assert.True(Slucajevi.Count >= 50,
            $"Očekuje se barem 50 paritetnih slučajeva, pročitano ih je {Slucajevi.Count}.");
    }

    /// <summary>
    /// Svaki ključ iz paritetne datoteke mora postojati u tablici one verzije koju
    /// slučaj navodi — inače bi tipfeler u ključu prošao kao iznos 0,00.
    /// </summary>
    [Theory]
    [MemberData(nameof(Indeksi))]
    public void OdredbaIzDatotekePostojiUTablici(int indeks)
    {
        var slucaj = Slucajevi[indeks];

        var verzija = slucaj.Uredba == "NN 37/2023"
            ? UredbaVerzija.Nn37_2023
            : UredbaVerzija.Nn50_2026;

        Assert.NotNull(SudskePristojbeCalculator.Odredba(slucaj.Odredba, verzija));
    }
}
