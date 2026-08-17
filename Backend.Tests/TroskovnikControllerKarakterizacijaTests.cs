using Backend.Controllers;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

/// <summary>
/// Karakterizacijski testovi za TroskovnikController.
///
/// Karakterizacijski test bilježi ZATEČENO ponašanje, kakvo god bilo, da bi se
/// poslije vidjelo je li ga refaktoriranje promijenilo. Ne tvrdi da je ponašanje
/// ispravno — tvrdi samo da je takvo. Kontroler prije ovoga nije imao nijedan test,
/// a koraci refaktoriranja koji slijede (uklanjanje duplikata i DIP) diraju ga
/// izravno, pa se prvo postavlja mreža.
///
/// Provjerava se statusni kod i oblik vraćenog objekta. Točni JSON ključevi nastaju
/// tek u cjevovodu ASP.NET-a (Program.cs postavlja SnakeCaseLower), pa se ovdje
/// provjeravaju imena svojstava na razini CLR-a — preimenovanje bilo kojeg od njih
/// srušit će ovaj test, što je i svrha.
/// </summary>
public class TroskovnikControllerKarakterizacijaTests
{
    private const string KorisnikId = "korisnik-1";
    private const string TudjiKorisnikId = "korisnik-2";
    private const string UredId = "ured-1";

    private static TroskovnikController StvoriKontroler(
        FakeClientCaseService klijenti,
        ITroskovnikService? servis = null,
        string? korisnikId = KorisnikId,
        string? uredId = UredId)
    {
        var kontroler = new TroskovnikController(klijenti, servis ?? new TroskovnikService());
        var kontekst = new DefaultHttpContext();

        if (korisnikId is not null)
            kontekst.Items["AuthUserId"] = korisnikId;

        // Ured koji SupabaseJwtMiddleware inace razrijesi preko ITenantResolver-a.
        // Bez njega Spremi vraca 403 jer se troskovnik ne moze vezati ni na koji ured.
        if (uredId is not null)
            kontekst.Items["AuthTenantId"] = uredId;

        kontroler.ControllerContext = new ControllerContext { HttpContext = kontekst };
        return kontroler;
    }

    private static TroskovnikRequest ValjanZahtjev() => new()
    {
        VrijednostSpora = 50_000m,
        Stavke = [new TroskovnikItemRequest { ItemId = "par_tuzba", Kolicina = 1 }]
    };

    /// <summary>Čita svojstvo anonimnog ili konkretnog objekta po imenu.</summary>
    private static object? Svojstvo(object? izvor, string ime)
        => izvor?.GetType().GetProperty(ime)?.GetValue(izvor);

    // ── GET /api/troskovnik/katalog ───────────────────────────────

    [Fact]
    public void DohvatiKatalog_BezObziraNaKorisnika_Vraca200SNepraznimKatalogom()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService(), korisnikId: null);

        var odgovor = Assert.IsType<OkObjectResult>(kontroler.DohvatiKatalog());

        var katalog = Assert.IsAssignableFrom<IReadOnlyList<TarifnaStavka>>(odgovor.Value);
        Assert.NotEmpty(katalog);
    }

    [Fact]
    public void DohvatiKatalog_SDvojnikomServisa_VracaKatalogIzDvojnika()
    {
        var servis = new FakeTroskovnikService();
        var kontroler = StvoriKontroler(new FakeClientCaseService(), servis, korisnikId: null);

        var odgovor = Assert.IsType<OkObjectResult>(kontroler.DohvatiKatalog());

        var katalog = Assert.IsAssignableFrom<IReadOnlyList<TarifnaStavka>>(odgovor.Value);
        Assert.Equal("izmisljena_stavka", Assert.Single(katalog).Id);
    }

    // ── POST /api/troskovnik/izracunaj ────────────────────────────

    [Fact]
    public async Task Izracunaj_NegativnaVrijednostSpora_Vraca400SPorukom()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.VrijednostSpora = -1m;

        var odgovor = Assert.IsType<BadRequestObjectResult>(await kontroler.Izracunaj(zahtjev));

        Assert.Equal("Vrijednost spora ne može biti negativna.", Svojstvo(odgovor.Value, "error"));
    }

    [Fact]
    public async Task Izracunaj_PraznaListaStavki_Vraca400SPorukom()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.Stavke = [];

        var odgovor = Assert.IsType<BadRequestObjectResult>(await kontroler.Izracunaj(zahtjev));

        Assert.Equal("Morate odabrati barem jednu stavku.", Svojstvo(odgovor.Value, "error"));
    }

    /// <summary>
    /// Pravilnik NN 22/2022 br. 271: čl. 1 je obrana okrivljenika, čl. 2 zastupanje
    /// djeteta žrtve. Isti odvjetnik ne može u istom postupku biti oboje, pa se zahtjev
    /// s obje uloge odbija umjesto da se stope tiho pomnože.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Izracunaj_ObranaIZastupanjeDjeteta_Vraca400(bool sluzbena, bool proracun)
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.IsZastupnikDjeteta = true;
        zahtjev.IsPoSluzbenojDuznosti = sluzbena;
        zahtjev.IsNaTeretProracuna = proracun;

        var odgovor = Assert.IsType<BadRequestObjectResult>(await kontroler.Izracunaj(zahtjev));

        var poruka = Assert.IsType<string>(Svojstvo(odgovor.Value, "error"));
        Assert.Contains("te se uloge isključuju", poruka);
    }

    [Fact]
    public async Task Izracunaj_SamoJednaUloga_Prolazi()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.IsZastupnikDjeteta = true;

        Assert.IsType<OkObjectResult>(await kontroler.Izracunaj(zahtjev));
    }

    [Fact]
    public async Task Izracunaj_NepoznataStavka_Vraca400SPorukomIzServisa()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.Stavke = [new TroskovnikItemRequest { ItemId = "ne_postoji", Kolicina = 1 }];

        var odgovor = Assert.IsType<BadRequestObjectResult>(await kontroler.Izracunaj(zahtjev));

        var poruka = Assert.IsType<string>(Svojstvo(odgovor.Value, "error"));
        Assert.Contains("Nepoznata tarifna stavka", poruka);
    }

    [Fact]
    public async Task Izracunaj_ValjanZahtjevBezPredmeta_Vraca200SIzracunatimTroskovnikom()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Izracunaj(ValjanZahtjev()));

        var rezultat = Assert.IsType<TroskovnikResponse>(odgovor.Value);
        Assert.Single(rezultat.Stavke);
        Assert.True(rezultat.UkupnoEurBezPdv > 0);
        Assert.Null(rezultat.Sud);
    }

    [Fact]
    public async Task Izracunaj_VlastitiPredmet_ObogacujeSudIBrojPredmeta()
    {
        var klijenti = new FakeClientCaseService
        {
            Predmet = new CaseReadRecord
            {
                Id = "predmet-1", UserId = KorisnikId, Sud = "Općinski sud u Zagrebu", BrojPredmeta = "P-123/2026"
            }
        };
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.CaseId = "predmet-1";

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Izracunaj(zahtjev));

        var rezultat = Assert.IsType<TroskovnikResponse>(odgovor.Value);
        Assert.Equal("Općinski sud u Zagrebu", rezultat.Sud);
        Assert.Equal("P-123/2026", rezultat.BrojPredmeta);
    }

    [Fact]
    public async Task Izracunaj_TudjiPredmet_Vraca200AliNeObogacuje()
    {
        var klijenti = new FakeClientCaseService
        {
            Predmet = new CaseReadRecord
            {
                Id = "predmet-1", UserId = TudjiKorisnikId, Sud = "Tuđi sud", BrojPredmeta = "P-999/2026"
            }
        };
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.CaseId = "predmet-1";

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Izracunaj(zahtjev));

        var rezultat = Assert.IsType<TroskovnikResponse>(odgovor.Value);
        Assert.Null(rezultat.Sud);

        // Zatečeno ponašanje: broj predmeta pada natrag na CaseId iz zahtjeva, jer
        // SastaviOdgovor koristi `request.Predmet?.PoslovniBroj ?? request.CaseId`.
        // Podaci tuđeg predmeta iz baze NE ulaze u odgovor — to je ono što se štiti.
        Assert.Equal("predmet-1", rezultat.BrojPredmeta);
    }

    // ── GET /api/troskovnik/tbr71 ─────────────────────────────────

    [Fact]
    public void Tbr71Bodovi_ZadanaVrijednost_VracaCetiriPolja()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());

        var odgovor = Assert.IsType<OkObjectResult>(kontroler.Tbr71Bodovi(50_000m));

        Assert.Equal(50_000m, Svojstvo(odgovor.Value, "vrijednostSpora"));
        Assert.Equal(TroskovnikService.Tbr71Bodovi(50_000m), Svojstvo(odgovor.Value, "bodovi"));
        Assert.Equal(TarifaConstants.BodValue, Svojstvo(odgovor.Value, "vrijednostBoda"));
        Assert.NotNull(Svojstvo(odgovor.Value, "eurBezPdv"));
    }

    [Fact]
    public void Tbr71Bodovi_SDvojnikomServisa_KoristiBodoveIzDvojnika()
    {
        var servis = new FakeTroskovnikService();
        var kontroler = StvoriKontroler(new FakeClientCaseService(), servis);

        var odgovor = Assert.IsType<OkObjectResult>(kontroler.Tbr71Bodovi(50_000m));

        Assert.Equal(FakeTroskovnikService.IzmisljeniBodovi, Svojstvo(odgovor.Value, "bodovi"));
        Assert.Equal(
            FakeTroskovnikService.IzmisljeniBodovi * TarifaConstants.BodValue,
            Svojstvo(odgovor.Value, "eurBezPdv"));
    }

    // ── POST /api/troskovnik/spremi ───────────────────────────────

    [Fact]
    public async Task Spremi_BezPrijavljenogKorisnika_Vraca401()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService(), korisnikId: null);

        var odgovor = Assert.IsType<UnauthorizedObjectResult>(await kontroler.Spremi(ValjanZahtjev()));

        Assert.Equal("Neautorizirani pristup.", Svojstvo(odgovor.Value, "error"));
    }

    [Fact]
    public async Task Spremi_NegativnaVrijednostSpora_Vraca400SPorukom()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService());
        var zahtjev = ValjanZahtjev();
        zahtjev.VrijednostSpora = -1m;

        var odgovor = Assert.IsType<BadRequestObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Equal("Vrijednost spora ne može biti negativna.", Svojstvo(odgovor.Value, "error"));
    }

    [Fact]
    public async Task Spremi_TudjiPredmet_Vraca403()
    {
        var klijenti = new FakeClientCaseService
        {
            Predmet = new CaseReadRecord { Id = "predmet-1", UserId = TudjiKorisnikId }
        };
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.CaseId = "predmet-1";

        var odgovor = Assert.IsType<ObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Equal(403, odgovor.StatusCode);
        Assert.Equal("Nemate pristup odabranom predmetu.", Svojstvo(odgovor.Value, "error"));
    }

    [Fact]
    public async Task Spremi_ValjanZahtjev_SpremaZapisIVracaId()
    {
        var klijenti = new FakeClientCaseService();
        var kontroler = StvoriKontroler(klijenti);

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(ValjanZahtjev()));

        Assert.NotNull(klijenti.SpremljeniTroskovnik);
        Assert.Equal(KorisnikId, klijenti.SpremljeniTroskovnik!.UserId);
        Assert.Equal("spremljeni-1", Svojstvo(odgovor.Value, "savedId"));
        Assert.IsType<TroskovnikResponse>(Svojstvo(odgovor.Value, "troskovnik"));
    }

    // ── POST /api/troskovnik/spremi — razrješavanje klijenta ──────
    //
    // Ove grane prije koraka 10 nisu imale nijedan test, a upravo se one izdvajaju
    // u RazrijesiKlijenta. Bilježi se zatečeno ponašanje, prije i poslije isto.

    [Fact]
    public async Task Spremi_ZadanClientId_KoristiGaINeDiraUpsert()
    {
        var klijenti = new FakeClientCaseService();
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.ClientId = "postojeci-klijent";
        zahtjev.AutoCreateClient = true;

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Null(klijenti.UpsertPozvanS);
        Assert.Equal("postojeci-klijent", Svojstvo(odgovor.Value, "clientId"));
        Assert.Null(Svojstvo(odgovor.Value, "createdClientId"));
        Assert.Equal("postojeci-klijent", klijenti.SpremljeniTroskovnik!.ClientId);
    }

    [Fact]
    public async Task Spremi_BezClientIdaIBezAutomatskogOtvaranja_NeDiraUpsert()
    {
        var klijenti = new FakeClientCaseService();
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.AutoCreateClient = false;

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Null(klijenti.UpsertPozvanS);
        Assert.Null(Svojstvo(odgovor.Value, "clientId"));
    }

    [Fact]
    public async Task Spremi_AutomatskoOtvaranje_SaljeUpsertuPodatkeZastupaneStranke()
    {
        var klijenti = new FakeClientCaseService();
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.AutoCreateClient = true;
        zahtjev.ClientGrad = "Split";
        zahtjev.Stranke = new StrankeRequest
        {
            ZastupanaStranka = new StrankaRequest
            {
                Naziv = "Ivan Horvat", Oib = "12345678901", Adresa = "Ilica 1"
            }
        };

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Equal(
            (KorisnikId, UredId, "Ivan Horvat", "12345678901", "Ilica 1", "Split"),
            klijenti.UpsertPozvanS);
        Assert.Equal("klijent-1", Svojstvo(odgovor.Value, "clientId"));
        Assert.Equal("klijent-1", Svojstvo(odgovor.Value, "createdClientId"));
    }

    [Fact]
    public async Task Spremi_AutomatskoOtvaranjeBezZastupaneStranke_PovlaciSeNaStariKlijent()
    {
        var klijenti = new FakeClientCaseService { IshodUpserta = ("klijent-7", false) };
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.AutoCreateClient = true;
        zahtjev.Klijent = new TroskovnikParty
        {
            Ime = "Marija Kovač", Oib = "98765432109", Adresa = "Vukovarska 2", Grad = "Osijek"
        };

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Equal(
            (KorisnikId, UredId, "Marija Kovač", "98765432109", "Vukovarska 2", "Osijek"),
            klijenti.UpsertPozvanS);

        // Zatečeno ponašanje: klijent je pronađen, ne otvoren, pa createdClientId ostaje prazan.
        Assert.Equal("klijent-7", Svojstvo(odgovor.Value, "clientId"));
        Assert.Null(Svojstvo(odgovor.Value, "createdClientId"));
    }

    [Fact]
    public async Task Spremi_AutomatskoOtvaranjeBezNaziva_NeDiraUpsert()
    {
        var klijenti = new FakeClientCaseService();
        var kontroler = StvoriKontroler(klijenti);
        var zahtjev = ValjanZahtjev();
        zahtjev.AutoCreateClient = true;

        var odgovor = Assert.IsType<OkObjectResult>(await kontroler.Spremi(zahtjev));

        Assert.Null(klijenti.UpsertPozvanS);
        Assert.Null(Svojstvo(odgovor.Value, "clientId"));
        Assert.Null(Svojstvo(odgovor.Value, "createdClientId"));
    }

    // ── GET /api/troskovnik/popis ─────────────────────────────────

    [Fact]
    public async Task Popis_BezPrijavljenogKorisnika_Vraca401()
    {
        var kontroler = StvoriKontroler(new FakeClientCaseService(), korisnikId: null);

        var odgovor = Assert.IsType<UnauthorizedObjectResult>(await kontroler.Popis());

        Assert.Equal("Neautorizirani pristup.", Svojstvo(odgovor.Value, "error"));
    }

    /// <summary>
    /// Ručno pisan dvojnik za ITroskovnikService.
    ///
    /// Postoji da dokaže učinak koraka 08: prije njega akcije /katalog i /tbr71 zvale
    /// su TroskovnikService statički, pa im se rezultat nije mogao podmetnuti i testovi
    /// su mogli tvrditi samo "nešto se vratilo". Sad idu kroz ubrizganu apstrakciju,
    /// pa se može provjeriti da kontroler vraća baš ono što je servis dao.
    /// </summary>
    private sealed class FakeTroskovnikService : ITroskovnikService
    {
        /// <summary>Namjerno broj koji nijedan razred Tbr. 7. toč. 1. ne daje.</summary>
        public const int IzmisljeniBodovi = 4242;

        public IReadOnlyList<TarifnaStavka> DohvatiKatalog() =>
            [new TarifnaStavka { Id = "izmisljena_stavka", Opis = "Stavka koja postoji samo u testu" }];

        public int Tbr71Bodovi(decimal vrijednost) => IzmisljeniBodovi;

        public TroskovnikResponse Izracunaj(TroskovnikRequest request) =>
            throw new NotSupportedException("Dvojnik ne podržava Izracunaj — test ga ne bi smio pozvati.");
    }

    /// <summary>
    /// Ručno pisan dvojnik za ITroskovnikSpremiste.
    ///
    /// Dok je kontroler ovisio o cijelom IClientCaseService, ovaj je dvojnik morao
    /// implementirati 31 članicu — 27 od njih samo je bacalo iznimku jer ih nijedan
    /// test ne poziva. Nakon razdvajanja sučelja (ISP) ostale su četiri koje se
    /// stvarno koriste.
    /// </summary>
    private sealed class FakeClientCaseService : ITroskovnikSpremiste
    {
        public CaseReadRecord? Predmet { get; init; }
        public TroskovnikRecord? SpremljeniTroskovnik { get; private set; }

        /// <summary>Što upsert vraća — null kao ClientId znači da klijent nije razriješen.</summary>
        public (string? ClientId, bool WasCreated) IshodUpserta { get; init; } = ("klijent-1", true);

        /// <summary>Argumenti zadnjeg poziva upserta; null ako pozvan nije bio.</summary>
        public (string Korisnik, string? Ured, string? Naziv, string? Oib, string? Adresa, string? Grad)? UpsertPozvanS
        { get; private set; }

        public Task<CaseReadRecord?> GetCaseByIdAsync(string id) => Task.FromResult(Predmet);

        public Task<(string? ClientId, bool WasCreated)> UpsertClientForTroskovnikAsync(
            string userId, string? uredId, string? naziv, string? oib, string? adresa, string? grad)
        {
            UpsertPozvanS = (userId, uredId, naziv, oib, adresa, grad);
            return Task.FromResult(IshodUpserta);
        }

        public Task<TroskovnikReadRecord?> SaveTroskovnikAsync(TroskovnikRecord record)
        {
            SpremljeniTroskovnik = record;
            return Task.FromResult<TroskovnikReadRecord?>(new TroskovnikReadRecord { Id = "spremljeni-1" });
        }

        public Task<List<TroskovnikReadRecord>> GetTroskovniciByUserAsync(string userId)
            => Task.FromResult(new List<TroskovnikReadRecord>());
    }
}
