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
    private readonly TroskovnikService _service = new();
    private readonly ClientCaseService _clientCaseService;

    public TroskovnikController(ClientCaseService clientCaseService)
    {
        _clientCaseService = clientCaseService;
    }

    private string? GetUserId() =>
        Request.Headers.TryGetValue("x-user-id", out var val) ? val.FirstOrDefault() : null;

    /// <summary>GET /api/troskovnik/katalog — vraća katalog svih tarifnih stavki.</summary>
    [HttpGet("katalog")]
    public IActionResult GetKatalog()
    {
        return Ok(TroskovnikService.GetKatalog());
    }

    /// <summary>POST /api/troskovnik/izracunaj — izračunava troškovnik.</summary>
    [HttpPost("izracunaj")]
    public IActionResult Izracunaj([FromBody] TroskovnikRequest request)
    {
        if (request.VrijednostSpora < 0)
            return BadRequest(new { error = "Vrijednost spora ne može biti negativna." });

        if (request.Stavke is null || request.Stavke.Count == 0)
            return BadRequest(new { error = "Morate odabrati barem jednu stavku." });

        var result = _service.Izracunaj(request);
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

        var record = new TroskovnikRecord
        {
            UserId = userId,
            OpisPredmeta = request.OpisPredmeta,
            VrijednostSpora = request.VrijednostSpora,
            UkupnoBodova = result.UkupnoBodova,
            UkupnoEurBezPdv = result.UkupnoEurBezPdv,
            Pdv = result.Pdv,
            UkupnoEurSPdv = result.UkupnoEurSPdv,
            KlijentIme = result.Klijent?.Ime,
            KlijentOib = result.Klijent?.Oib,
            OdvjetnikIme = result.Odvjetnik?.Ime,
            OdvjetnikOib = result.Odvjetnik?.Oib,
            RawJsonOutput = result
        };

        var saved = await _clientCaseService.SaveTroskovnikAsync(record);

        return Ok(new { troskovnik = result, savedId = saved?.Id });
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
