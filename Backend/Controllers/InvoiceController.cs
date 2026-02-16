using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// V47: Fiskalizacija 2.0 — e-Invoice Controller.
/// Generates UBL 2.1 compliant e-invoices linked to PravoTek analyses.
///
/// Endpoints:
/// - POST /api/invoice/generate    — Generate a new e-invoice from case data
/// - POST /api/invoice/validate-oib — Validate a Croatian OIB number
/// - GET  /api/invoice/ubl/{id}    — Get raw UBL XML for an invoice
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InvoiceController : ControllerBase
{
    private readonly InvoiceService _invoiceService;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(InvoiceService invoiceService, ILogger<InvoiceController> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/invoice/generate — Generate a UBL 2.1 e-invoice.
    /// </summary>
    [HttpPost("generate")]
    [EnableRateLimiting("valuation")]
    public async Task<ActionResult<InvoiceResult>> Generate([FromBody] InvoiceRequest request)
    {
        // ── Validation ──

        if (request.NetAmount <= 0)
            return BadRequest(new { error = "Neto iznos mora biti veći od 0." });

        if (request.NetAmount > 10_000_000)
            return BadRequest(new { error = "Neto iznos premašuje maksimalni dozvoljeni iznos." });

        if (request.PdvRate is not (0 or 5 or 13 or 25))
            return BadRequest(new { error = "PDV stopa mora biti 0%, 5%, 13% ili 25%." });

        if (string.IsNullOrWhiteSpace(request.Client.Name))
            return BadRequest(new { error = "Naziv klijenta je obavezan." });

        if (string.IsNullOrWhiteSpace(request.Issuer.Name))
            return BadRequest(new { error = "Naziv izdavatelja je obavezan." });

        // Validate OIBs if provided
        if (!string.IsNullOrWhiteSpace(request.Issuer.Oib) && !InvoiceService.ValidateOib(request.Issuer.Oib))
            return BadRequest(new { error = "OIB izdavatelja nije valjan. OIB mora imati 11 znamenki i prolaziti ISO 7064 kontrolu." });

        if (!string.IsNullOrWhiteSpace(request.Client.Oib) && !InvoiceService.ValidateOib(request.Client.Oib))
            return BadRequest(new { error = "OIB klijenta nije valjan. OIB mora imati 11 znamenki i prolaziti ISO 7064 kontrolu." });

        if (request.PaymentDueDays is < 1 or > 365)
            return BadRequest(new { error = "Rok plaćanja mora biti između 1 i 365 dana." });

        var validModules = new[] { "valuation", "contract", "chronology", "legal" };
        if (!validModules.Contains(request.Module))
            return BadRequest(new { error = $"Nepoznat modul: '{request.Module}'. Dozvoljeni: valuation, contract, chronology, legal." });

        try
        {
            _logger.LogInformation(
                "Invoice generation request: module={Module}, client={Client}, net={Net:F2}",
                request.Module, request.Client.Name, request.NetAmount);

            var result = await _invoiceService.GenerateInvoiceAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice generation failed");
            return StatusCode(500, new { error = "Greška pri generiranju e-računa. Pokušajte ponovno." });
        }
    }

    /// <summary>
    /// POST /api/invoice/validate-oib — Validate a Croatian OIB number.
    /// </summary>
    [HttpPost("validate-oib")]
    [EnableRateLimiting("general")]
    public ActionResult ValidateOib([FromBody] OibValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Oib))
            return BadRequest(new { valid = false, error = "OIB je prazan." });

        var isValid = InvoiceService.ValidateOib(request.Oib);
        return Ok(new
        {
            valid = isValid,
            oib = request.Oib,
            message = isValid
                ? "OIB je valjan."
                : "OIB nije valjan. Provjerite broj (11 znamenki, ISO 7064 MOD 11,10)."
        });
    }
}

/// <summary>
/// Request body for OIB validation.
/// </summary>
public class OibValidationRequest
{
    public string Oib { get; set; } = "";
}
