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
    private readonly IClientCaseService _clientCaseService;

    public TroskovnikController(IClientCaseService clientCaseService, ITroskovnikService service)
    {
        _clientCaseService = clientCaseService;
        _service = service;
    }

    // SECURITY: Verified user_id extracted from Supabase JWT by SupabaseJwtMiddleware
    private string? GetUserId() => HttpContext.Items["AuthUserId"] as string;

    /// <summary>GET /api/troskovnik/katalog — vraća katalog svih tarifnih stavki.</summary>
    [HttpGet("katalog")]
    public IActionResult GetKatalog()
    {
        return Ok(TroskovnikService.GetKatalog());
    }

    /// <summary>POST /api/troskovnik/izracunaj — izračunava troškovnik.</summary>
    [HttpPost("izracunaj")]
    public async Task<IActionResult> Izracunaj([FromBody] TroskovnikRequest request)
    {
        if (request.VrijednostSpora < 0)
            return BadRequest(new { error = "Vrijednost spora ne može biti negativna." });

        if (request.Stavke is null || request.Stavke.Count == 0)
            return BadRequest(new { error = "Morate odabrati barem jednu stavku." });

        var result = _service.Izracunaj(request);

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
        var bodovi = TroskovnikService.Tbr71Bodovi(vrijednost);
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

        if (request.VrijednostSpora < 0)
            return BadRequest(new { error = "Vrijednost spora ne može biti negativna." });

        if (request.Stavke is null || request.Stavke.Count == 0)
            return BadRequest(new { error = "Morate odabrati barem jednu stavku." });


        var result = _service.Izracunaj(request);

        // ═════════════════════════════════════════════════════════
        //  SECURITY: Validate CaseId ownership before linking
        // ═════════════════════════════════════════════════════════
        if (!string.IsNullOrWhiteSpace(request.CaseId))
        {
            var cas = await _clientCaseService.GetCaseByIdAsync(request.CaseId);
            if (cas == null || cas.UserId != userId)
                return StatusCode(403, new { error = "Nemate pristup odabranom predmetu." });
        }

        // ═════════════════════════════════════════════════════════
        //  CLIENT UPSERT LOGIC
        //  1. If client_id provided → use it (existing client selected from combobox)
        //  2. If auto_create_client && no client_id → find or create client
        // ═════════════════════════════════════════════════════════

        string? resolvedClientId = request.ClientId;
        string? createdClientId = null;

        if (string.IsNullOrWhiteSpace(resolvedClientId) && request.AutoCreateClient)
        {
            // Extract client data from stranke > zastupana_stranka (preferred) or legacy klijent
            var clientNaziv = request.Stranke?.ZastupanaStranka?.Naziv
                              ?? request.Klijent?.Ime;
            var clientOib = request.Stranke?.ZastupanaStranka?.Oib
                            ?? request.Klijent?.Oib;
            var clientAdresa = request.Stranke?.ZastupanaStranka?.Adresa
                               ?? request.Klijent?.Adresa;
            var clientGrad = request.ClientGrad
                             ?? request.Klijent?.Grad;

            if (!string.IsNullOrWhiteSpace(clientNaziv))
            {
                var (upsertedId, wasCreated) = await _clientCaseService.UpsertClientForTroskovnikAsync(
                    userId, clientNaziv, clientOib, clientAdresa, clientGrad
                );

                if (upsertedId != null)
                {
                    resolvedClientId = upsertedId;
                    if (wasCreated) createdClientId = upsertedId;
                }
            }
        }

        var record = new TroskovnikRecord
        {
            UserId = userId,
            ClientId = resolvedClientId,
            CaseId = request.CaseId,
            OpisPredmeta = request.OpisPredmeta,
            VrijednostSpora = request.VrijednostSpora,
            UkupnoBodova = result.UkupnoBodova,
            UkupnoEurBezPdv = result.UkupnoEurBezPdv,
            Pdv = result.Pdv,
            UkupnoEurSPdv = result.UkupnoEurSPdv,
            // Nested objects — prefer new schema, fall back to legacy Klijent
            Odvjetnik = request.Odvjetnik is null ? null : new OdvjetnikInfo
            {
                Naziv = request.Odvjetnik.Naziv,
                Oib = request.Odvjetnik.Oib,
                Adresa = request.Odvjetnik.Adresa,
                Iban = request.Odvjetnik.Iban
            },
            Predmet = new PredmetInfo
            {
                Sud = request.Predmet?.Sud ?? request.OpisPredmeta,
                PoslovniBroj = request.Predmet?.PoslovniBroj ?? request.CaseId
            },
            Stranke = new StrankeInfo
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
            },
            // Save both flags for compatibility: prefer explicit IsPdvUkljucen if provided
            IsPdvUkljucen = request.IsPdvUkljucen ?? request.HasVat,
            HasVat = request.HasVat,
            // Kazneni fields
            ZaprijecenaKazna = request.ZaprijecenaKazna,
            IsPoSluzbenojDuznosti = request.IsPoSluzbenojDuznosti,
            IsNaTeretProracuna = request.IsNaTeretProracuna,
            IsZastupnikDjeteta = request.IsZastupnikDjeteta,
            IsStjecaj = request.IsStjecaj,
            IsOduzimanjePrekrsaj = request.IsOduzimanjePrekrsaj,
            VrijednostOduzimanja = request.VrijednostOduzimanja,
            RawJsonOutput = result
        };

        var saved = await _clientCaseService.SaveTroskovnikAsync(record);

        return Ok(new
        {
            troskovnik = result,
            savedId = saved?.Id,
            clientId = resolvedClientId,
            createdClientId
        });
    }

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


