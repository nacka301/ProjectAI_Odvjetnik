using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class TroskovnikController : ControllerBase
{
    private readonly ITroskovnikService _servis;
    private readonly ITroskovnikSpremiste _spremiste;

    public TroskovnikController(ITroskovnikSpremiste spremiste, ITroskovnikService servis)
    {
        _spremiste = spremiste;
        _servis = servis;
    }

    // SECURITY: provjereni user_id koji je SupabaseJwtMiddleware izvadio iz Supabase JWT-a
    private string? PrijavljeniKorisnik() => HttpContext.Items["AuthUserId"] as string;

    /// <summary>
    /// Provjere zajedničke akcijama koje računaju troškovnik.
    ///
    /// Baca istu iznimku kojom se javlja i sam servis, pa obje akcije imaju jedno
    /// mjesto na kojem se validacijska greška pretvara u odgovor 400.
    /// </summary>
    private static void ProvjeriZahtjev(TroskovnikRequest zahtjev)
    {
        if (zahtjev.VrijednostSpora < 0)
            throw new TroskovnikValidationException("Vrijednost spora ne može biti negativna.");

        if (zahtjev.Stavke is null || zahtjev.Stavke.Count == 0)
            throw new TroskovnikValidationException("Morate odabrati barem jednu stavku.");

        // Pravilnik NN 22/2022 br. 271: čl. 1 je obrana okrivljenika, čl. 2 zastupanje
        // djeteta žrtve. Isti odvjetnik ne može u istom postupku biti oboje — sukob
        // interesa — pa se ni stope ne zbrajaju ni ne množe.
        if (zahtjev.IsZastupnikDjeteta && (zahtjev.IsPoSluzbenojDuznosti || zahtjev.IsNaTeretProracuna))
            throw new TroskovnikValidationException(
                "Odvjetnik ne može u istom postupku biti branitelj okrivljenika i opunomoćenik djeteta žrtve — te se uloge isključuju.");
    }

    /// <summary>
    /// Provjerava zahtjev i računa troškovnik.
    ///
    /// Vraća <c>null</c> kad je sve u redu, a inače gotov odgovor 400 koji pozivatelj
    /// samo proslijedi. Obje akcije koje računaju troškovnik radile su ovo istim
    /// deseterorednim blokom, pa je pravilo „greška u izračunu je 400 s porukom
    /// servisa" postojalo na dva mjesta i moglo se razići.
    /// </summary>
    private IActionResult? ProvjeriIIzracunaj(TroskovnikRequest zahtjev, out TroskovnikResponse rezultat)
    {
        rezultat = default!;
        try
        {
            ProvjeriZahtjev(zahtjev);
            rezultat = _servis.Izracunaj(zahtjev);
            return null;
        }
        catch (TroskovnikValidationException ex)
        {
            // Troškovnik se ne može valjano izračunati — bolje jasna poruka nego
            // stavka koja tiho nestane ili redak s 0,00 €.
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>GET /api/troskovnik/katalog — vraća katalog svih tarifnih stavki.</summary>
    [HttpGet("katalog")]
    public IActionResult DohvatiKatalog()
    {
        return Ok(_servis.DohvatiKatalog());
    }

    /// <summary>POST /api/troskovnik/izracunaj — izračunava troškovnik.</summary>
    [HttpPost("izracunaj")]
    public async Task<IActionResult> Izracunaj([FromBody] TroskovnikRequest zahtjev)
    {
        if (ProvjeriIIzracunaj(zahtjev, out var rezultat) is { } greska)
            return greska;

        // Ako je zadan predmet, odgovor se dopunjuje sudom i brojem predmeta.
        // SECURITY: dopunjuje se samo predmet koji pripada prijavljenom korisniku.
        if (!string.IsNullOrEmpty(zahtjev.CaseId))
        {
            var korisnikId = PrijavljeniKorisnik();
            var predmet = await _spremiste.GetCaseByIdAsync(zahtjev.CaseId);
            if (predmet != null && predmet.UserId == korisnikId)
            {
                rezultat.Sud = predmet.Sud;
                rezultat.BrojPredmeta = predmet.BrojPredmeta;
            }
        }

        return Ok(rezultat);
    }

    /// <summary>GET /api/troskovnik/tbr71?vrijednost=50000 — brzi upit za bodove po Tbr. 7. toč. 1.</summary>
    [HttpGet("tbr71")]
    public IActionResult Tbr71Bodovi([FromQuery] decimal vrijednost = 0)
    {
        var bodovi = _servis.Tbr71Bodovi(vrijednost);
        return Ok(new
        {
            vrijednostSpora = vrijednost,
            bodovi,
            eurBezPdv = bodovi * TarifaConstants.BodValue,
            vrijednostBoda = TarifaConstants.BodValue
        });
    }

    /// <summary>POST /api/troskovnik/spremi — izračunava i trajno sprema troškovnik u bazu.</summary>
    [HttpPost("spremi")]
    public async Task<IActionResult> Spremi([FromBody] TroskovnikRequest zahtjev)
    {
        var korisnikId = PrijavljeniKorisnik();
        if (string.IsNullOrWhiteSpace(korisnikId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        if (ProvjeriIIzracunaj(zahtjev, out var rezultat) is { } greska)
            return greska;

        // SECURITY: predmet se smije vezati samo ako pripada prijavljenom korisniku
        if (!await SmijeVezatiPredmet(zahtjev.CaseId, korisnikId))
            return StatusCode(403, new { error = "Nemate pristup odabranom predmetu." });

        var uredId = this.FirmId();
        if (string.IsNullOrWhiteSpace(uredId))
            return this.NoFirmResult();

        var klijent = await RazrijesiKlijenta(zahtjev, korisnikId, uredId);

        var zapis = SastaviZapis(zahtjev, rezultat, korisnikId, uredId, klijent.Id);

        var spremljeno = await _spremiste.SaveTroskovnikAsync(zapis);

        // Spremište vraća null kad je baza odbila upis — ono grešku samo zabilježi u
        // log. Bez ove provjere odgovor je bio 200 sa praznim id-em, pa je sučelje
        // odbijeni troškovnik puštalo bez ijedne riječi. Odvjetnik je odlazio
        // uvjeren da mu je obračun spremljen, a u bazi ga nije bilo.
        if (spremljeno is null)
            return StatusCode(500, new
            {
                error = "Troškovnik nije spremljen — baza je odbila upis. " +
                        "Izračun je ispravan, pa ga izvezite ili ispišite prije zatvaranja stranice."
            });

        return Ok(new SpremiOdgovor(rezultat, spremljeno.Id, klijent.Id, klijent.Kreirani));
    }

    /// <summary>
    /// Odgovor na spremanje troškovnika.
    ///
    /// Imenovani tip s izričitim <c>[JsonPropertyName]</c>, a ne anonimni objekt:
    /// imena polja anonimnog objekta prolazila su kroz globalnu snake_case politiku
    /// iz <c>Program.cs</c>, pa je `savedId` na žici bio `saved_id`. Sučelje je
    /// čitalo `savedId`, dobivalo `undefined` i nikad nije prikazalo potvrdu —
    /// troškovnik se uredno spremao, a korisnik to nije mogao znati.
    /// </summary>
    private sealed record SpremiOdgovor(
        [property: JsonPropertyName("troskovnik")] TroskovnikResponse Troskovnik,
        [property: JsonPropertyName("saved_id")] string SavedId,
        [property: JsonPropertyName("client_id")] string? ClientId,
        /// <summary>Popunjen samo kad je stranka upravo otvorena — sučelje to javlja posebno.</summary>
        [property: JsonPropertyName("created_client_id")] string? CreatedClientId);

    /// <summary>
    /// Smije li se troškovnik vezati uz zadani predmet.
    ///
    /// Bez predmeta nema što provjeravati, pa je odgovor da. Inače predmet mora
    /// postojati i pripadati prijavljenom korisniku.
    /// </summary>
    private async Task<bool> SmijeVezatiPredmet(string? predmetId, string korisnikId)
    {
        if (string.IsNullOrWhiteSpace(predmetId))
            return true;

        var predmet = await _spremiste.GetCaseByIdAsync(predmetId);
        return predmet != null && predmet.UserId == korisnikId;
    }

    /// <summary>
    /// Utvrđuje uz kojeg se klijenta troškovnik veže.
    ///
    /// Ako je klijent izabran u sučelju, koristi se on. Ako nije, a zahtjev traži
    /// automatsko otvaranje, klijent se traži po OIB-u ili nazivu i po potrebi
    /// stvara. Podaci se čitaju iz stranke > zastupana_stranka, uz povlačenje na
    /// stariju strukturu <c>klijent</c>.
    /// </summary>
    private async Task<RazrijeseniKlijent> RazrijesiKlijenta(
        TroskovnikRequest zahtjev, string korisnikId, string? uredId)
    {
        if (!string.IsNullOrWhiteSpace(zahtjev.ClientId) || !zahtjev.AutoCreateClient)
            return new RazrijeseniKlijent(zahtjev.ClientId, null);

        var naziv = zahtjev.Stranke?.ZastupanaStranka?.Naziv ?? zahtjev.Klijent?.Ime;
        if (string.IsNullOrWhiteSpace(naziv))
            return new RazrijeseniKlijent(zahtjev.ClientId, null);

        var (razrijeseniId, jeOtvoren) = await _spremiste.UpsertClientForTroskovnikAsync(
            korisnikId,
            uredId,
            naziv,
            zahtjev.Stranke?.ZastupanaStranka?.Oib ?? zahtjev.Klijent?.Oib,
            zahtjev.Stranke?.ZastupanaStranka?.Adresa ?? zahtjev.Klijent?.Adresa,
            zahtjev.ClientGrad ?? zahtjev.Klijent?.Grad
        );

        if (razrijeseniId is null)
            return new RazrijeseniKlijent(zahtjev.ClientId, null);

        return new RazrijeseniKlijent(razrijeseniId, jeOtvoren ? razrijeseniId : null);
    }

    /// <summary>
    /// Klijent uz kojeg ide troškovnik. <paramref name="Kreirani"/> je popunjen samo
    /// ako je klijent upravo otvoren, jer to sučelje javlja korisniku posebno.
    /// </summary>
    private sealed record RazrijeseniKlijent(string? Id, string? Kreirani);

    /// <summary>
    /// Sastavlja zapis za bazu iz zahtjeva i izračunatog troškovnika.
    ///
    /// Iznosi se uzimaju iz rezultata, a ne iz zahtjeva: u kaznenom predmetu VPS je
    /// izbačen iz izračuna pa ne smije završiti ni u spremljenom troškovniku
    /// (vidi TroskovnikService).
    /// </summary>
    private static TroskovnikRecord SastaviZapis(
        TroskovnikRequest zahtjev, TroskovnikResponse rezultat,
        string korisnikId, string? uredId, string? klijentId) => new()
    {
        UserId = korisnikId,
        LawFirmId = uredId,
        ClientId = klijentId,
        CaseId = zahtjev.CaseId,
        OpisPredmeta = zahtjev.OpisPredmeta,
        VrijednostSpora = rezultat.VrijednostSpora,
        UkupnoBodova = rezultat.UkupnoBodova,
        UkupnoEurBezPdv = rezultat.UkupnoEurBezPdv,
        Pdv = rezultat.Pdv,
        UkupnoEurSPdv = rezultat.UkupnoEurSPdv,
        NaknadaTroskovaUkupno = rezultat.NaknadaTroskovaUkupno,
        PredujmoviUkupno = rezultat.UkupnoPredujmovi,
        Odvjetnik = SastaviOdvjetnika(zahtjev),
        Predmet = new PredmetInfo
        {
            Sud = zahtjev.Predmet?.Sud ?? zahtjev.OpisPredmeta,
            PoslovniBroj = zahtjev.Predmet?.PoslovniBroj ?? zahtjev.CaseId
        },
        Stranke = SastaviStranke(zahtjev, rezultat),
        // Obje zastavice se spremaju radi kompatibilnosti: izričiti IsPdvUkljucen ima prednost
        IsPdvUkljucen = zahtjev.IsPdvUkljucen ?? zahtjev.HasVat,
        HasVat = zahtjev.HasVat,
        // Kazneni postupak
        ZaprijecenaKazna = zahtjev.ZaprijecenaKazna,
        IsPoSluzbenojDuznosti = zahtjev.IsPoSluzbenojDuznosti,
        IsNaTeretProracuna = zahtjev.IsNaTeretProracuna,
        IsZastupnikDjeteta = zahtjev.IsZastupnikDjeteta,
        IsStjecaj = zahtjev.IsStjecaj,
        IsOduzimanjePrekrsaj = zahtjev.IsOduzimanjePrekrsaj,
        VrijednostOduzimanja = zahtjev.VrijednostOduzimanja,
        RawJsonOutput = rezultat
    };

    /// <summary>Podaci odvjetnika za zaglavlje troškovnika; izostaju ako ih zahtjev nema.</summary>
    private static OdvjetnikInfo? SastaviOdvjetnika(TroskovnikRequest zahtjev) =>
        zahtjev.Odvjetnik is null ? null : new OdvjetnikInfo
        {
            Naziv = zahtjev.Odvjetnik.Naziv,
            Oib = zahtjev.Odvjetnik.Oib,
            Adresa = zahtjev.Odvjetnik.Adresa,
            Iban = zahtjev.Odvjetnik.Iban
        };

    /// <summary>
    /// Stranke postupka. Zastupana stranka se popunjava iz zahtjeva, a gdje podatka
    /// nema povlači se iz izračuna. Protustranka izostaje ako je zahtjev ne navodi.
    /// </summary>
    private static StrankeInfo SastaviStranke(TroskovnikRequest zahtjev, TroskovnikResponse rezultat) => new()
    {
        ZastupanaStranka = new StrankaInfo
        {
            Polozaj = zahtjev.Stranke?.ZastupanaStranka?.Polozaj ?? "zastupana",
            Naziv = zahtjev.Stranke?.ZastupanaStranka?.Naziv ?? rezultat.Klijent?.Ime,
            Oib = zahtjev.Stranke?.ZastupanaStranka?.Oib ?? rezultat.Klijent?.Oib,
            Adresa = zahtjev.Stranke?.ZastupanaStranka?.Adresa ?? rezultat.Klijent?.Adresa
        },
        Protustranka = zahtjev.Stranke?.Protustranka is null ? null : new StrankaInfo
        {
            Polozaj = zahtjev.Stranke.Protustranka.Polozaj ?? zahtjev.ProtustrankaPolozaj,
            Naziv = zahtjev.Stranke.Protustranka.Naziv,
            Oib = zahtjev.Stranke.Protustranka.Oib,
            Adresa = zahtjev.Stranke.Protustranka.Adresa
        }
    };

    /// <summary>GET /api/troskovnik/popis — lista svih spremljenih troškovnika korisnika.</summary>
    [HttpGet("popis")]
    public async Task<IActionResult> Popis()
    {
        var korisnikId = PrijavljeniKorisnik();
        if (string.IsNullOrWhiteSpace(korisnikId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        var popis = await _spremiste.GetTroskovniciByUserAsync(korisnikId);
        return Ok(popis);
    }
}
