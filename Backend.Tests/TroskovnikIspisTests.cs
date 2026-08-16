using System.Globalization;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Redoslijed odjeljaka u ispisu troškovnika, potvrđen s odvjetnicom: odvjetničke
/// radnje (svaka sa svojim datumom) → zbroj nagrade → PDV (uvjetno) → sudske pristojbe
/// (bez PDV-a) → predujmovi (bez PDV-a) → napomena.
///
/// <see cref="TroskovnikDocumentService"/> je mrtav kod — nijedan kontroler ga ne poziva
/// (nema `/ispis` rute), ali se održava u koraku s izračunom da ne ostanu dvije istine.
/// </summary>
public class TroskovnikIspisTests
{
    private static readonly TroskovnikDocumentService Servis = new(NullLogger<TroskovnikDocumentService>.Instance);

    private static TroskovnikResponse PotpuniOdgovor(bool includeVat = true) => new()
    {
        Stavke =
        [
            new TroskovnikLineItem
            {
                Rbr = 1,
                ItemId = "par_tuzba",
                Datum = new DateOnly(2026, 3, 5),
                Opis = "Tužba",
                TarifniBroj = "Tbr. 7.1",
                PravniIzvor = "Tarifa, Tbr. 7. toč. 1",
                Kolicina = 1,
                BodovaPoJedinici = 500,
                UkupnoBodova = 500,
                IznosEur = 1_000.00m
            },
            new TroskovnikLineItem
            {
                Rbr = 2,
                ItemId = "ovr_prijedlog",
                Datum = null,
                Opis = "Prijedlog za ovrhu",
                TarifniBroj = "Tbr. 11.1",
                PravniIzvor = "Tarifa, Tbr. 11. toč. 1",
                Kolicina = 1,
                BodovaPoJedinici = 500,
                UkupnoBodova = 500,
                IznosEur = 1_000.00m
            }
        ],
        UkupnoBodova = 1_000,
        UkupnoEurBezPdv = 2_000.00m,
        IsPdvUkljucen = includeVat,
        HasVat = includeVat,
        Pdv = includeVat ? 500.00m : 0m,
        UkupnoEurSPdv = includeVat ? 2_500.00m : 2_000.00m,
        SudskaPristojba = 100.00m,
        PristojbaOsnova = PristojbaOsnova.Puna,
        DodatnePristojbe = [new TroskovnikTrosakStavka { Rbr = 1, Datum = new DateOnly(2026, 3, 6), Opis = "Pristojba na presudu", Iznos = 50.00m }],
        UkupnoPristojbe = 150.00m,
        Predujmovi = [new TroskovnikTrosakStavka { Rbr = 1, Datum = null, Opis = "Vještačenje", Iznos = 300.00m }],
        UkupnoPredujmovi = 300.00m,
        SveukupniTrosak = includeVat ? 2_950.00m : 2_450.00m,
        Napomena = "Na traženi iznos obračunava se zakonska zatezna kamata.",
        DatumIzrade = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc)
    };

    private static TroskovnikReadRecord Zapis(TroskovnikResponse odgovor) => new()
    {
        Id = "test",
        RawJsonOutput = odgovor
    };

    [Fact]
    public void RedoslijedOdjeljaka_SlijediTrazeniPoredak()
    {
        var html = Servis.RenderHtml(Zapis(PotpuniOdgovor(includeVat: true)));

        var iRadnje = html.IndexOf("Odvjetničke radnje", StringComparison.Ordinal);
        var iZbroj = html.IndexOf("Zbroj nagrade", StringComparison.Ordinal);
        var iPdv = html.IndexOf(">PDV (25%)<", StringComparison.Ordinal);
        var iPristojbe = html.IndexOf("Sudske pristojbe", StringComparison.Ordinal);
        var iPredujmovi = html.IndexOf("Predujmovi", StringComparison.Ordinal);
        var iNapomena = html.IndexOf(">Napomena<", StringComparison.Ordinal);

        Assert.True(iRadnje >= 0 && iZbroj >= 0 && iPdv >= 0 && iPristojbe >= 0 && iPredujmovi >= 0 && iNapomena >= 0,
            "Svi odjeljci moraju biti prisutni u ispisu.");

        Assert.True(iRadnje < iZbroj);
        Assert.True(iZbroj < iPdv);
        Assert.True(iPdv < iPristojbe);
        Assert.True(iPristojbe < iPredujmovi);
        Assert.True(iPredujmovi < iNapomena);
    }

    [Fact]
    public void BezPdva_OdjeljakPdvaSeNePojavljuje_OstaliRedoslijedNepromijenjen()
    {
        var html = Servis.RenderHtml(Zapis(PotpuniOdgovor(includeVat: false)));

        Assert.DoesNotContain(">PDV (25%)<", html);

        var iRadnje = html.IndexOf("Odvjetničke radnje", StringComparison.Ordinal);
        var iZbroj = html.IndexOf("Zbroj nagrade", StringComparison.Ordinal);
        var iPristojbe = html.IndexOf("Sudske pristojbe", StringComparison.Ordinal);
        var iPredujmovi = html.IndexOf("Predujmovi", StringComparison.Ordinal);
        var iNapomena = html.IndexOf(">Napomena<", StringComparison.Ordinal);

        Assert.True(iRadnje < iZbroj);
        Assert.True(iZbroj < iPristojbe);
        Assert.True(iPristojbe < iPredujmovi);
        Assert.True(iPredujmovi < iNapomena);
    }

    [Fact]
    public void PraznaNapomena_IzostavljaOdjeljak()
    {
        var odgovor = PotpuniOdgovor();
        odgovor.Napomena = null;

        var html = Servis.RenderHtml(Zapis(odgovor));

        Assert.DoesNotContain(">Napomena<", html);
    }

    [Fact]
    public void DatumRadnje_IspisujeSeUHrHrObliku()
    {
        var html = Servis.RenderHtml(Zapis(PotpuniOdgovor()));

        var ocekivano = new DateOnly(2026, 3, 5).ToString("d", new CultureInfo("hr-HR"));
        Assert.Contains(ocekivano, html);
    }

    [Fact]
    public void PrazanDatum_NeOstavljaDefaultDateOnly()
    {
        var html = Servis.RenderHtml(Zapis(PotpuniOdgovor()));

        Assert.DoesNotContain("01.01.0001", html);
        Assert.DoesNotContain("1.1.1.", html);
    }

    [Fact]
    public void RucnoDodanePristojbeIPredujmovi_PrikazaniUOdjeljcima()
    {
        var html = Servis.RenderHtml(Zapis(PotpuniOdgovor()));

        Assert.Contains("Pristojba na presudu", html);
        Assert.Contains("Vještačenje", html);
        Assert.Contains("Ukupno pristojbe", html);
        Assert.Contains("Ukupno predujmovi", html);
    }
}
