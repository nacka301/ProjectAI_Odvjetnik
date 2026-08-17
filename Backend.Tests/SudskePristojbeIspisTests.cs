using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Ispis troškovnika (HTML iz kojeg nastaje PDF) za sudske pristojbe.
///
/// Ispis je ono što odvjetnik predaje sudu. Zbroj bez razrade po tarifnim brojevima
/// ondje se ne može provjeriti, a redak obračunat po staroj Uredbi izgledao bi kao
/// pogreška da uz njega ne piše zašto je star.
/// </summary>
public class SudskePristojbeIspisTests
{
    private static string Ispisi(TroskovnikResponse odgovor)
        => new TroskovnikDocumentService(NullLogger<TroskovnikDocumentService>.Instance)
            .RenderHtml(new TroskovnikReadRecord { RawJsonOutput = odgovor });

    private static TroskovnikResponse Izracunaj(
        decimal vps, DateOnly? datumOdgovoraNaTuzbu, params string[] itemIds)
        => new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = vps,
            IsPdvUkljucen = true,
            DatumPodnosenjaOdgovoraNaTuzbu = datumOdgovoraNaTuzbu,
            Stavke = [.. itemIds.Select(id => new TroskovnikItemRequest { ItemId = id, Kolicina = 1 })]
        });

    [Fact]
    public void SvakaPristojbaImaSvojRedakSTarifnimBrojem()
    {
        var html = Ispisi(Izracunaj(50_000m, null, "par_tuzba", "par_zalba_presuda"));

        Assert.Contains("Sudska pristojba — Tar. br. 1 st. 1", html);
        Assert.Contains("Sudska pristojba — Tar. br. 3 st. 1", html);

        // Odjeljak "II. Sudske pristojbe" nosi jedan zbroj, i on pokriva i ručno dodane
        // pristojbe — zato "Ukupno pristojbe", a ne "Ukupno sudske pristojbe".
        Assert.Contains("Ukupno pristojbe", html);

        Assert.Contains(FormatiraniIznos(620.00m), html);
        Assert.Contains(FormatiraniIznos(1_240.00m), html);
        Assert.Contains(FormatiraniIznos(1_860.00m), html);
    }

    /// <summary>
    /// Uz jednu pristojbu zbroj odjeljka jednak je toj pristojbi — a ne, primjerice,
    /// dvostrukom iznosu zbog retka koji bi se izbrojao dvaput.
    /// </summary>
    [Fact]
    public void JednaPristojba_ZbrojOdjeljkaJednakTojPristojbi()
    {
        var odgovor = Izracunaj(50_000m, null, "par_tuzba");
        var html = Ispisi(odgovor);

        Assert.Contains("Sudska pristojba — Tar. br. 1 st. 1", html);
        Assert.Equal(620.00m, odgovor.SudskaPristojba);
        Assert.Equal(620.00m, odgovor.UkupnoPristojbe);
        Assert.Contains(FormatiraniIznos(620.00m), html);
    }

    /// <summary>Redak po staroj Uredbi mora reći da je star i po kojoj odredbi.</summary>
    [Fact]
    public void PrijelazniRedak_NosiNaznakuStareUredbe()
    {
        var html = Ispisi(Izracunaj(50_000m, new DateOnly(2026, 3, 12), "par_odgovor_tuzba"));

        Assert.Contains("NN 37/2023", html);
        Assert.Contains("čl. 2 NN 50/2026", html);
        Assert.Contains(FormatiraniIznos(273.23m), html);
    }

    /// <summary>
    /// Kad pristojbe nema, ispis mora reći zašto — inače „0,00 €" izgleda kao greška
    /// u računu, a ne kao ispravan ishod.
    /// </summary>
    [Fact]
    public void BezPristojbe_IspisujeRazlog()
    {
        var html = Ispisi(new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            ZaprijecenaKazna = "do_5",
            Stavke = [new TroskovnikItemRequest { ItemId = "kaz_obrana", Kolicina = 1 }]
        }));

        Assert.Contains("ne propisuje pristojbu", html);
        Assert.DoesNotContain("Sudska pristojba — Tar. br.", html);
    }

    [Fact]
    public void OslobodjenaStranka_OstajeVidljivaUzRetke()
    {
        var html = Ispisi(new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsOslobodjenPristojbi = true,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        }));

        Assert.Contains("(oslobođeno)", html);
        Assert.Contains("Tar. br. 1 st. 1", html);
    }

    /// <summary>
    /// Ispis koristi hrvatski zapis iznosa bez oznake valute — 1.860,00, ne 1,860.00
    /// (vidi <c>TroskovnikDocumentService.FormatCurrency</c>).
    /// </summary>
    private static string FormatiraniIznos(decimal iznos)
        => iznos.ToString("N2", new System.Globalization.CultureInfo("hr-HR"));
}
