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
    private readonly ITroskovnikService _service;
    private readonly ITroskovnikSpremiste _clientCaseService;

    public TroskovnikController(ITroskovnikSpremiste clientCaseService, ITroskovnikService service)
    {
        _clientCaseService = clientCaseService;
        _service = service;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    /// <summary>
    /// Provjere zajedničke akcijama koje računaju troškovnik.
    ///
    /// Baca istu iznimku kojom se javlja i sam servis, pa obje akcije imaju jedno
    /// mjesto na kojem se validacijska greška pretvara u odgovor 400.
    /// </summary>
    private static void ProvjeriZahtjev(TroskovnikRequest request)
    {
        if (request.VrijednostSpora < 0)
            throw new TroskovnikValidationException("Vrijednost spora ne može biti negativna.");

        if (request.Stavke is null || request.Stavke.Count == 0)
            throw new TroskovnikValidationException("Morate odabrati barem jednu stavku.");
    }

    /// <summary>GET /api/troskovnik/katalog — vraća katalog svih tarifnih stavki.</summary>
    [HttpGet("katalog")]
    public IActionResult DohvatiKatalog()
    {
        return Ok(_service.DohvatiKatalog());
    }

    /// <summary>POST /api/troskovnik/izracunaj — izračunava troškovnik.</summary>
    [HttpPost("izracunaj")]
    public async Task<IActionResult> Izracunaj([FromBody] TroskovnikRequest request)
    {
        TroskovnikResponse result;
        try
        {
            ProvjeriZahtjev(request);
            result = _service.Izracunaj(request);
        }
        catch (TroskovnikValidationException ex)
        {
            // Troškovnik se ne može valjano izračunati — bolje jasna poruka nego
            // stavka koja tiho nestane ili redak s 0,00 €.
            return BadRequest(new { error = ex.Message });
        }

        // If a case id was provided, attempt to enrich the response with case metadata (sud, broj predmeta)
        // SECURITY: Only enrich if the case belongs to the authenticated user
        if (!string.IsNullOrEmpty(request.CaseId))
        {
            var userId = GetUserId();
            var cas = await _clientCaseService.GetCaseByIdAsync(request.CaseId);
            if (cas != null && cas.UserId == userId)
            {
                result.Sud = cas.Sud;
                result.BrojPredmeta = cas.BrojPredmeta;
            }
        }

        return Ok(result);
    }

    /// <summary>GET /api/troskovnik/tbr71?vrijednost=50000 — brzi lookup Tbr.7.1 bodova.</summary>
    [HttpGet("tbr71")]
    public IActionResult Tbr71Lookup([FromQuery] decimal vrijednost = 0)
    {
        var bodovi = _service.Tbr71Bodovi(vrijednost);
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
    public async Task<IActionResult> Spremi([FromBody] TroskovnikRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        TroskovnikResponse result;
        try
        {
            ProvjeriZahtjev(request);
            result = _service.Izracunaj(request);
        }
        catch (TroskovnikValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // SECURITY: predmet se smije vezati samo ako pripada prijavljenom korisniku
        if (!await SmijeVezatiPredmet(request.CaseId, userId))
            return StatusCode(403, new { error = "Nemate pristup odabranom predmetu." });

        var klijent = await RazrijesiKlijenta(request, userId);

        var zapis = SastaviZapis(request, result, userId, klijent.Id);

        var saved = await _clientCaseService.SaveTroskovnikAsync(zapis);

        return Ok(new
        {
            troskovnik = result,
            savedId = saved?.Id,
            clientId = klijent.Id,
            createdClientId = klijent.Kreirani
        });
    }

    /// <summary>
    /// Smije li se troškovnik vezati uz zadani predmet.
    ///
    /// Bez predmeta nema što provjeravati, pa je odgovor da. Inače predmet mora
    /// postojati i pripadati prijavljenom korisniku.
    /// </summary>
    private async Task<bool> SmijeVezatiPredmet(string? caseId, string userId)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return true;

        var predmet = await _clientCaseService.GetCaseByIdAsync(caseId);
        return predmet != null && predmet.UserId == userId;
    }

    /// <summary>
    /// Utvrđuje uz kojeg se klijenta troškovnik veže.
    ///
    /// Ako je klijent izabran u sučelju, koristi se on. Ako nije, a zahtjev traži
    /// automatsko otvaranje, klijent se traži po OIB-u ili nazivu i po potrebi
    /// stvara. Podaci se čitaju iz stranke > zastupana_stranka, uz povlačenje na
    /// stariju strukturu <c>klijent</c>.
    /// </summary>
    private async Task<RazrijeseniKlijent> RazrijesiKlijenta(TroskovnikRequest request, string userId)
    {
        if (!string.IsNullOrWhiteSpace(request.ClientId) || !request.AutoCreateClient)
            return new RazrijeseniKlijent(request.ClientId, null);

        var naziv = request.Stranke?.ZastupanaStranka?.Naziv ?? request.Klijent?.Ime;
        if (string.IsNullOrWhiteSpace(naziv))
            return new RazrijeseniKlijent(request.ClientId, null);

        var (upsertedId, wasCreated) = await _clientCaseService.UpsertClientForTroskovnikAsync(
            userId,
            naziv,
            request.Stranke?.ZastupanaStranka?.Oib ?? request.Klijent?.Oib,
            request.Stranke?.ZastupanaStranka?.Adresa ?? request.Klijent?.Adresa,
            request.ClientGrad ?? request.Klijent?.Grad
        );

        if (upsertedId is null)
            return new RazrijeseniKlijent(request.ClientId, null);

        return new RazrijeseniKlijent(upsertedId, wasCreated ? upsertedId : null);
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
        TroskovnikRequest request, TroskovnikResponse result, string userId, string? clientId) => new()
    {
        UserId = userId,
        ClientId = clientId,
        CaseId = request.CaseId,
        OpisPredmeta = request.OpisPredmeta,
        VrijednostSpora = result.VrijednostSpora,
        UkupnoBodova = result.UkupnoBodova,
        UkupnoEurBezPdv = result.UkupnoEurBezPdv,
        Pdv = result.Pdv,
        UkupnoEurSPdv = result.UkupnoEurSPdv,
        Odvjetnik = SastaviOdvjetnika(request),
        Predmet = new PredmetInfo
        {
            Sud = request.Predmet?.Sud ?? request.OpisPredmeta,
            PoslovniBroj = request.Predmet?.PoslovniBroj ?? request.CaseId
        },
        Stranke = SastaviStranke(request, result),
        // Obje zastavice se spremaju radi kompatibilnosti: izričiti IsPdvUkljucen ima prednost
        IsPdvUkljucen = request.IsPdvUkljucen ?? request.HasVat,
        HasVat = request.HasVat,
        // Kazneni postupak
        ZaprijecenaKazna = request.ZaprijecenaKazna,
        IsPoSluzbenojDuznosti = request.IsPoSluzbenojDuznosti,
        IsNaTeretProracuna = request.IsNaTeretProracuna,
        IsZastupnikDjeteta = request.IsZastupnikDjeteta,
        IsStjecaj = request.IsStjecaj,
        IsOduzimanjePrekrsaj = request.IsOduzimanjePrekrsaj,
        VrijednostOduzimanja = request.VrijednostOduzimanja,
        RawJsonOutput = result
    };

    /// <summary>Podaci odvjetnika za zaglavlje troškovnika; izostaju ako ih zahtjev nema.</summary>
    private static OdvjetnikInfo? SastaviOdvjetnika(TroskovnikRequest request) =>
        request.Odvjetnik is null ? null : new OdvjetnikInfo
        {
            Naziv = request.Odvjetnik.Naziv,
            Oib = request.Odvjetnik.Oib,
            Adresa = request.Odvjetnik.Adresa,
            Iban = request.Odvjetnik.Iban
        };

    /// <summary>
    /// Stranke postupka. Zastupana stranka se popunjava iz zahtjeva, a gdje podatka
    /// nema povlači se iz izračuna. Protustranka izostaje ako je zahtjev ne navodi.
    /// </summary>
    private static StrankeInfo SastaviStranke(TroskovnikRequest request, TroskovnikResponse result) => new()
    {
        ZastupanaStranka = new StrankaInfo
        {
            Polozaj = request.Stranke?.ZastupanaStranka?.Polozaj ?? "zastupana",
            Naziv = request.Stranke?.ZastupanaStranka?.Naziv ?? result.Klijent?.Ime,
            Oib = request.Stranke?.ZastupanaStranka?.Oib ?? result.Klijent?.Oib,
            Adresa = request.Stranke?.ZastupanaStranka?.Adresa ?? result.Klijent?.Adresa
        },
        Protustranka = request.Stranke?.Protustranka is null ? null : new StrankaInfo
        {
            Polozaj = request.Stranke.Protustranka.Polozaj ?? request.ProtustrankaPolozaj,
            Naziv = request.Stranke.Protustranka.Naziv,
            Oib = request.Stranke.Protustranka.Oib,
            Adresa = request.Stranke.Protustranka.Adresa
        }
    };

    /// <summary>GET /api/troskovnik/popis — lista svih spremljenih troškovnika korisnika.</summary>
    [HttpGet("popis")]
    public async Task<IActionResult> Popis()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Nedostaje x-user-id header." });

        var list = await _clientCaseService.GetTroskovniciByUserAsync(userId);
        return Ok(list);
    }
}


