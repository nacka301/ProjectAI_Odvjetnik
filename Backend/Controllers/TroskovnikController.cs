using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TroskovnikController : ControllerBase
{
    private readonly TroskovnikService _service = new();

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
}
