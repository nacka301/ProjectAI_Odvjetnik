using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Datum radnje u HTML ispisu troškovnika.
///
/// Na papiru datum ide u hrvatskom obliku dd.MM.gggg.; ISO oblik u kojem se prenosi i
/// sprema ne smije se pojaviti pred sudom.
/// </summary>
public class TroskovnikIspisDatumaTests
{
    private static string Ispisi(TroskovnikResponse odgovor)
        => new TroskovnikDocumentService(NullLogger<TroskovnikDocumentService>.Instance)
            .RenderHtml(new TroskovnikReadRecord { RawJsonOutput = odgovor });

    private static TroskovnikResponse Odgovor(params TroskovnikLineItem[] stavke) => new()
    {
        Stavke = [.. stavke],
        DatumOd = stavke.Select(s => s.Datum).Where(d => d is not null).Order().FirstOrDefault(),
        DatumDo = stavke.Select(s => s.Datum).Where(d => d is not null).OrderDescending().FirstOrDefault(),
        UkupnoBodova = 500m,
        UkupnoEurBezPdv = 1_000.00m
    };

    private static TroskovnikLineItem Redak(int rbr, string? datum, string opis = "Tužba") => new()
    {
        Rbr = rbr,
        Opis = opis,
        TarifniBroj = "Tbr. 7.1",
        Kolicina = 1,
        BodovaPoJedinici = 500m,
        UkupnoBodova = 500m,
        IznosEur = 1_000.00m,
        Datum = datum
    };

    [Fact]
    public void Tablica_ImaStupacDatum()
    {
        Assert.Contains("<th>Datum</th>", Ispisi(Odgovor(Redak(1, "2025-03-12"))));
    }

    [Fact]
    public void Datum_IspisujeSeUHrvatskomObliku()
    {
        var html = Ispisi(Odgovor(Redak(1, "2025-03-12")));

        Assert.Contains("12.03.2025.", html);
        Assert.DoesNotContain("2025-03-12", html);
    }

    /// <summary>Vodeće nule ostaju — datumi se na ispisu slažu jedan ispod drugoga.</summary>
    [Fact]
    public void Datum_ZadrzavaVodeceNule()
    {
        Assert.Contains("05.01.2025.", Ispisi(Odgovor(Redak(1, "2025-01-05"))));
    }

    [Fact]
    public void Razdoblje_StojiIznadTablice()
    {
        var html = Ispisi(Odgovor(
            Redak(1, "2025-03-12"),
            Redak(2, "2025-05-19", "Ročište")));

        Assert.Contains("Za razdoblje 12.03.2025. – 19.05.2025.", html);
    }

    /// <summary>Jedan datum nije razdoblje — pisati „od 12.03. do 12.03." je besmisleno.</summary>
    [Fact]
    public void Razdoblje_JednaRadnja_PiseSeKaoJedanDatum()
    {
        var html = Ispisi(Odgovor(Redak(1, "2025-03-12")));

        Assert.Contains("Radnja od 12.03.2025.", html);
        Assert.DoesNotContain("Za razdoblje", html);
    }

    [Fact]
    public void BezDatuma_NemaRazdobljaNiPraznihOznaka()
    {
        var html = Ispisi(Odgovor(Redak(1, datum: null)));

        Assert.DoesNotContain("Za razdoblje", html);
        Assert.DoesNotContain("Radnja od", html);
        Assert.DoesNotContain("Invalid", html);
    }

    /// <summary>
    /// Radnja obuhvaćena jednokratnom nagradom stoji na ispisu sa svojim datumom, ali
    /// bez bodova — bez napomene bi 0,00 € izgledalo kao greška u zbrajanju.
    /// </summary>
    [Fact]
    public void ObuhvacenaRadnja_NosiNapomenu()
    {
        var obuhvacen = Redak(2, "2025-04-04", "Tužba – smetanje posjeda");
        obuhvacen.ObuhvacenoJednokratnom = true;
        obuhvacen.UkupnoBodova = 0m;
        obuhvacen.BodovaPoJedinici = 0m;
        obuhvacen.IznosEur = 0m;

        var html = Ispisi(Odgovor(Redak(1, "2025-03-12", "Tužba – smetanje posjeda"), obuhvacen));

        Assert.Contains("obuhvaćeno jednokratnom nagradom", html);
        Assert.Contains("04.04.2025.", html);
    }

    /// <summary>Neispravan datum ne smije proći kao tekst na dokument koji ide sudu.</summary>
    [Theory]
    [InlineData("12.03.2025.")]
    [InlineData("nije datum")]
    public void NeispravanDatum_OstavljaPraznoPolje(string datum)
    {
        var html = Ispisi(Odgovor(Redak(1, datum)));

        Assert.DoesNotContain(datum, html);
        Assert.DoesNotContain("Invalid", html);
    }
}
