using System.Text.Json;
using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Oblik JSON-a koji `/api/troskovnik/izracunaj` vraća za sudske pristojbe.
///
/// Frontend čita točno ova imena polja (`frontend/lib/tarifa.ts`, sučelje
/// `PristojbenaStavka`), a spremljeni troškovnici u bazi nose stara polja. Promjena
/// imena ne bi srušila nijedan izračun — samo bi tiho ostavila prazan prikaz i krivi
/// zbroj na ispisu, pa se imena zaključavaju testom.
/// </summary>
public class SudskePristojbeSerijalizacijaTests
{
    private static JsonElement Izracunaj()
    {
        var odgovor = new TroskovnikService().Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = true,
            DatumPodnosenjaOdgovoraNaTuzbu = new DateOnly(2026, 3, 12),
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_odgovor_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1 }
            ]
        });

        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(odgovor));
    }

    [Fact]
    public void OdgovorNosiListuPristojbiSPoljimaKojaFrontendCita()
    {
        var json = Izracunaj();

        var stavke = json.GetProperty("pristojbene_stavke");
        Assert.Equal(2, stavke.GetArrayLength());

        var prva = stavke[0];
        Assert.Equal("tbr1_st2", prva.GetProperty("odredba_id").GetString());
        Assert.Equal("Tar. br. 1 st. 2", prva.GetProperty("tarifni_broj").GetString());
        Assert.Equal("I. Parnični i ovršni postupak", prva.GetProperty("poglavlje").GetString());
        Assert.Equal("NN 37/2023", prva.GetProperty("uredba").GetString());
        Assert.Equal(273.23m, prva.GetProperty("iznos").GetDecimal());
        Assert.Equal(50_000m, prva.GetProperty("osnovica").GetDecimal());
        Assert.Equal(1, prva.GetProperty("kolicina").GetInt32());
        Assert.True(prva.GetProperty("automatski").GetBoolean());
        Assert.Contains("čl. 2", prva.GetProperty("pravni_izvor").GetString());

        var druga = stavke[1];
        Assert.Equal("tbr3_st1", druga.GetProperty("odredba_id").GetString());
        Assert.Equal("NN 50/2026", druga.GetProperty("uredba").GetString());
        Assert.Equal(1_240.00m, druga.GetProperty("iznos").GetDecimal());
    }

    /// <summary>
    /// Stara polja moraju preživjeti: `sudska_pristojba` čitaju CSV izvoz, PDF i svaki
    /// troškovnik već spremljen u bazi.
    /// </summary>
    [Fact]
    public void StaraPoljaOstajuUOdgovoru()
    {
        var json = Izracunaj();

        Assert.Equal(1_513.23m, json.GetProperty("sudska_pristojba").GetDecimal());
        Assert.False(json.GetProperty("is_oslobodjen_pristojbi").GetBoolean());
        Assert.Equal("Polovica", json.GetProperty("pristojba_osnova").GetString());

        // Pristojba se pribraja tek na kraju — ne ulazi u osnovicu za PDV.
        var sPdv = json.GetProperty("ukupno_eur_s_pdv").GetDecimal();
        Assert.Equal(sPdv + 1_513.23m, json.GetProperty("sveukupni_trosak").GetDecimal());
    }

    /// <summary>
    /// Zahtjev prihvaća datume i ručne izmjene pod imenima koja frontend šalje.
    /// </summary>
    [Fact]
    public void ZahtjevPrihvacaNovaPoljaIzFrontenda()
    {
        const string tijelo = """
        {
          "vrijednost_spora": 10000,
          "stavke": [{ "item_id": "izv_dioba", "kolicina": 1 }],
          "datum_podnosenja_odgovora_na_tuzbu": "2026-03-12",
          "datum_pokretanja_upravnog_spora": null,
          "pristojbe_dodane": [{ "odredba_id": "tbr11", "osnovica": 10000, "kolicina": 1 }],
          "pristojbe_iskljucene": ["tbr5"]
        }
        """;

        var zahtjev = JsonSerializer.Deserialize<TroskovnikRequest>(tijelo)!;

        Assert.Equal(new DateOnly(2026, 3, 12), zahtjev.DatumPodnosenjaOdgovoraNaTuzbu);
        Assert.Null(zahtjev.DatumPokretanjaUpravnogSpora);
        Assert.Equal("tbr11", Assert.Single(zahtjev.PristojbeDodane).OdredbaId);
        Assert.Equal("tbr5", Assert.Single(zahtjev.PristojbeIskljucene));

        // I da izmjene stvarno dođu do izračuna: dioba (150,00) zamijenjena ostavinom (120,00).
        var odgovor = new TroskovnikService().Izracunaj(zahtjev);
        Assert.Equal(120.00m, odgovor.SudskaPristojba);
    }
}
