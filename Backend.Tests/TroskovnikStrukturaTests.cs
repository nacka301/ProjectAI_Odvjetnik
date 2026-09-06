using Backend.Models;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Struktura i ispis troškovnika: datum po radnji, ista tarifna stavka dvaput s dva
/// različita datuma, ručno dodane pristojbe i predujmovi, slobodna napomena.
///
/// Odvjetnica je potvrdila redoslijed dokumenta: odvjetničke radnje (svaka sa svojim
/// datumom) → zbroj nagrade → PDV (uvjetno) → sudske pristojbe (bez PDV-a) → predujmovi
/// (bez PDV-a) → napomena.
/// </summary>
public class TroskovnikStrukturaTests
{
    private static TroskovnikResponse Izracunaj(TroskovnikRequest zahtjev)
        => new TroskovnikService().Izracunaj(zahtjev);

    [Fact]
    public void IstaStavkaDvaputa_SDvaDatuma_DajeDvaRetkaSOdvojenimDatumima()
    {
        var prviDatum = new DateOnly(2026, 1, 10);
        var drugiDatum = new DateOnly(2026, 2, 15);

        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1, Datum = prviDatum },
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1, Datum = drugiDatum }
            ]
        });

        Assert.Equal(2, rezultat.Stavke.Count);

        Assert.Equal(1, rezultat.Stavke[0].Rbr);
        Assert.Equal(prviDatum, rezultat.Stavke[0].Datum);

        Assert.Equal(2, rezultat.Stavke[1].Rbr);
        Assert.Equal(drugiDatum, rezultat.Stavke[1].Datum);
    }

    [Fact]
    public void Radnje_SlazeSePoDatumu_NeovisnoORedoslijeduUZahtjevu()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 45_300m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_zalba_presuda", Kolicina = 1, Datum = new DateOnly(2025, 11, 14) },
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1, Datum = new DateOnly(2025, 2, 12) },
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 1, Datum = new DateOnly(2025, 6, 3) },
            ]
        });

        Assert.Equal(
            [new DateOnly(2025, 2, 12), new DateOnly(2025, 6, 3), new DateOnly(2025, 11, 14)],
            rezultat.Stavke.Select(s => s.Datum));
        Assert.Equal([1, 2, 3], rezultat.Stavke.Select(s => s.Rbr));
    }

    [Fact]
    public void Radnje_BezDatuma_IduNaKraj_OstaleZadrzeRedoslijedUnosa()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 45_300m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_rociste_glavno", Kolicina = 1, Datum = null },
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1, Datum = new DateOnly(2025, 3, 1) },
                new TroskovnikItemRequest { ItemId = "par_rociste_presuda", Kolicina = 1, Datum = null },
            ]
        });

        Assert.Equal(
            ["par_tuzba", "par_rociste_glavno", "par_rociste_presuda"],
            rezultat.Stavke.Select(s => s.ItemId));
    }

    [Fact]
    public void DatumRadnje_JeOpcionalan_NeBlokiraIzracun()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1, Datum = null }]
        });

        Assert.Null(rezultat.Stavke[0].Datum);
        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
    }

    [Fact]
    public void ObrnutiRedoslijedIstihStavki_IstiUkupniIznos_DrukcijiRbr()
    {
        var poredakA = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "ovr_prijedlog", Kolicina = 1 }
            ]
        });

        var poredakB = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke =
            [
                new TroskovnikItemRequest { ItemId = "ovr_prijedlog", Kolicina = 1 },
                new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }
            ]
        });

        Assert.Equal(poredakA.UkupnoEurBezPdv, poredakB.UkupnoEurBezPdv);
        Assert.Equal(poredakA.SveukupniTrosak, poredakB.SveukupniTrosak);

        // Isti redoslijed zahtjeva → isti redoslijed odgovora, redni broj slijedi zahtjev.
        Assert.Equal("par_tuzba", poredakA.Stavke[0].ItemId);
        Assert.Equal("ovr_prijedlog", poredakA.Stavke[1].ItemId);
        Assert.Equal("ovr_prijedlog", poredakB.Stavke[0].ItemId);
        Assert.Equal("par_tuzba", poredakB.Stavke[1].ItemId);
    }

    /// <summary>
    /// Nagrada 1.000 € + PDV 250 € + ručna pristojba 100 € + predujam 200 € = 1.550 €,
    /// NE 1.625 € — PDV se obračunava isključivo na nagradu, nikad na pristojbe ni
    /// predujmove. Automatska sudska pristojba je isključena (oslobođenje), da ostane
    /// samo ručno dodana pristojba u iznosu.
    /// </summary>
    [Fact]
    public void PdvSeNeObracunavaNaPristojbeNiPredujmove()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = true,
            IsOslobodjenPristojbi = true,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            DodatnePristojbe = [new TroskovnikTrosakRequest { Opis = "Pristojba na presudu", Iznos = 100m }],
            Predujmovi = [new TroskovnikTrosakRequest { Opis = "Vještačenje", Iznos = 200m }]
        });

        Assert.Equal(1_000.00m, rezultat.UkupnoEurBezPdv);
        Assert.Equal(250.00m, rezultat.Pdv);
        Assert.Equal(0m, rezultat.SudskaPristojba);
        Assert.Equal(100.00m, rezultat.UkupnoPristojbe);
        Assert.Equal(200.00m, rezultat.UkupnoPredujmovi);
        Assert.Equal(1_550.00m, rezultat.SveukupniTrosak);
    }

    [Fact]
    public void DodatnePristojbeIPredujmovi_RenumeriranjeOd1()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            DodatnePristojbe =
            [
                new TroskovnikTrosakRequest { Opis = "Prva pristojba", Iznos = 10m },
                new TroskovnikTrosakRequest { Opis = "Druga pristojba", Iznos = 20m }
            ],
            Predujmovi = [new TroskovnikTrosakRequest { Opis = "Vještačenje", Iznos = 300m }]
        });

        Assert.Equal(1, rezultat.DodatnePristojbe[0].Rbr);
        Assert.Equal(2, rezultat.DodatnePristojbe[1].Rbr);
        Assert.Equal(1, rezultat.Predujmovi[0].Rbr);
    }

    [Fact]
    public void PrazniPopisi_IznosIdentičanDosadašnjem()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            IsPdvUkljucen = false,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
        });

        // 1.000,00 € nagrada + 620,00 € sudska pristojba (VPS 50.000 € po NN 50/2026,
        // Tar. br. 1 st. 1: 140,00 + 1 % × 48.000,00). Po staroj Uredbi bilo je 546,45 €.
        Assert.Equal(620.00m, rezultat.SudskaPristojba);
        Assert.Equal(620.00m, rezultat.UkupnoPristojbe);
        Assert.Equal(0m, rezultat.UkupnoPredujmovi);
        Assert.Equal(1_620.00m, rezultat.SveukupniTrosak);
        Assert.Empty(rezultat.DodatnePristojbe);
        Assert.Empty(rezultat.Predujmovi);
    }

    [Fact]
    public void NegativanIznos_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            Predujmovi = [new TroskovnikTrosakRequest { Opis = "Vještačenje", Iznos = -50m }]
        }));

        Assert.Contains("negativan", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PredujamBezOpisa_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            Predujmovi = [new TroskovnikTrosakRequest { Opis = "  ", Iznos = 50m }]
        }));

        Assert.Contains("opisa", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PristojbaBezOpisa_BacaGresku()
    {
        var greska = Assert.Throws<TroskovnikValidationException>(() => Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            DodatnePristojbe = [new TroskovnikTrosakRequest { Opis = "", Iznos = 50m }]
        }));

        Assert.Contains("opisa", greska.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PraznaNapomena_VracaNull(string? napomena)
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            Napomena = napomena
        });

        Assert.Null(rezultat.Napomena);
    }

    [Fact]
    public void NapomenaSTekstom_OstajeNepromijenjena()
    {
        var rezultat = Izracunaj(new TroskovnikRequest
        {
            VrijednostSpora = 50_000m,
            Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }],
            Napomena = TarifaConstants.ZadanaNapomena
        });

        Assert.Equal(TarifaConstants.ZadanaNapomena, rezultat.Napomena);
    }
}
