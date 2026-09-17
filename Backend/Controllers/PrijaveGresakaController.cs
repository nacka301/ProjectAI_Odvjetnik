using System.Net;
using System.Text.Json;
using Backend.Middleware;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// POST /api/prijave-gresaka — „Iznos mi ne izgleda točno" (plan pravne zaštite, dio D).
///
/// Redoslijed je namjeran: prvo baza, pa mail. Zapis u bazi je izvor istine —
/// prijava se ne smije izgubiti zato što mail nije otišao (Resend nije
/// postavljen, SMTP pao). `email_poslan` bilježi ishod, a admin popis prikazuje
/// sve prijave bez obzira na mail.
///
/// Korisnik i ured dolaze iz tokena. Troškovnik se veže samo ako ga korisnikov
/// token smije pročitati, pa se ne može prijaviti tuđi.
/// </summary>
[ApiController]
[Route("api/prijave-gresaka")]
[EnableRateLimiting("prijave")]
public class PrijaveGresakaController : ControllerBase
{
    private readonly IPrijaveGresakaSpremiste _spremiste;
    private readonly IResendEmailService _email;
    private readonly AdminIdentity _admin;
    private readonly ILogger<PrijaveGresakaController> _logger;

    public PrijaveGresakaController(
        IPrijaveGresakaSpremiste spremiste,
        IResendEmailService email,
        AdminIdentity admin,
        ILogger<PrijaveGresakaController> logger)
    {
        _spremiste = spremiste;
        _email = email;
        _admin = admin;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Prijavi([FromBody] PrijavaGreskeZahtjev zahtjev)
    {
        var korisnikId = HttpContext.Items["AuthUserId"] as string;
        if (string.IsNullOrWhiteSpace(korisnikId))
            return Unauthorized(new { error = "Neautorizirani pristup." });

        var uredId = this.FirmId();
        if (string.IsNullOrWhiteSpace(uredId))
            return this.NoFirmResult();

        var opis = zahtjev.Opis?.Trim() ?? "";
        if (opis.Length < PrijavaGreskeGranice.OpisMin)
            return BadRequest(new { error = "Opišite ukratko što ne izgleda točno (barem 10 znakova)." });
        if (opis.Length > PrijavaGreskeGranice.OpisMax)
            return BadRequest(new { error = "Opis je predug — najviše 2000 znakova." });

        var ocekivani = string.IsNullOrWhiteSpace(zahtjev.OcekivaniIznos) ? null : zahtjev.OcekivaniIznos.Trim();
        if (ocekivani is { Length: > PrijavaGreskeGranice.OcekivaniMax })
            return BadRequest(new { error = "Očekivani iznos je predug — najviše 200 znakova." });

        var troskovnikId = string.IsNullOrWhiteSpace(zahtjev.TroskovnikId) ? null : zahtjev.TroskovnikId.Trim();
        if (troskovnikId is not null && !await _spremiste.TroskovnikJeDostupanAsync(troskovnikId))
            return NotFound(new { error = "Troškovnik nije pronađen." });

        var zapis = new PrijavaGreskeZapis
        {
            UserId = korisnikId,
            LawFirmId = uredId,
            TroskovnikId = troskovnikId,
            Opis = opis,
            OcekivaniIznos = ocekivani,
            UlazniPodaci = OgranicenJson(PrijavaGreskeCiscenje.Ocisti(zahtjev.UlazniPodaci)),
            Rezultat = OgranicenJson(PrijavaGreskeCiscenje.Ocisti(zahtjev.Rezultat)),
            VerzijaIzracuna = VerzijaAplikacije.Commit,
        };

        var id = await _spremiste.SpremiAsync(zapis);
        if (id is null)
            return StatusCode(500, new
            {
                error = "Prijava nije spremljena. Pošaljite je e-poštom na vackadavid@gmail.com.",
            });

        var emailPoslan = await PosaljiMail(id, zapis, HttpContext.Items["AuthEmail"] as string);
        if (emailPoslan)
            await _spremiste.OznaciEmailPoslanAsync(id);

        return Ok(new PrijavaOdgovor(id));
    }

    private sealed record PrijavaOdgovor(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id);

    /// <summary>Preveliki JSON se ne sprema — ostaje oznaka da je bio izostavljen.</summary>
    private static System.Text.Json.Nodes.JsonNode? OgranicenJson(System.Text.Json.Nodes.JsonNode? cvor)
    {
        if (cvor is null) return null;
        return cvor.ToJsonString().Length <= PrijavaGreskeGranice.JsonMax
            ? cvor
            : new System.Text.Json.Nodes.JsonObject { ["izostavljeno"] = "Podaci su bili preveliki za spremanje." };
    }

    private async Task<bool> PosaljiMail(string id, PrijavaGreskeZapis zapis, string? emailKorisnika)
    {
        var adminEmail = _admin.Email;
        // Resend bez ključa sam vraća false (i to logira); ovdje se hvata samo
        // nepostavljen ADMIN_EMAIL, jer bez adrese nemamo kome slati.
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            _logger.LogWarning("Prijava greške {Id} spremljena, mail nije poslan: ADMIN_EMAIL nije postavljen.", id);
            return false;
        }

        try
        {
            return await _email.SendEmailAsync(
                adminEmail,
                "PravoTek — prijava greške u izračunu",
                PrijavaGreskeMail.Html(id, zapis, emailKorisnika, Environment.GetEnvironmentVariable("FRONTEND_URL")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mail za prijavu greške {Id} nije poslan.", id);
            return false;
        }
    }
}

/// <summary>Tijelo maila adminu. Sve što je korisnik upisao ide kroz HTML escape.</summary>
public static class PrijavaGreskeMail
{
    public static string Html(string id, PrijavaGreskeZapis z, string? emailKorisnika, string? frontendUrl)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var opcije = new JsonSerializerOptions { WriteIndented = true };
        var baza = (frontendUrl ?? "").Split(',')[0].Trim().TrimEnd('/');
        var poveznica = z.TroskovnikId is null || baza == ""
            ? E(z.TroskovnikId ?? "nespremljen obrazac")
            : $"<a href=\"{E(baza)}/troskovnik/{E(z.TroskovnikId)}\">{E(z.TroskovnikId)}</a>";

        return $"""
            <h2>Prijava greške u izračunu</h2>
            <p><b>Prijavio:</b> {E(emailKorisnika)}<br>
            <b>Ured:</b> {E(z.LawFirmId)}<br>
            <b>Troškovnik:</b> {poveznica}<br>
            <b>Verzija izračuna:</b> {E(z.VerzijaIzracuna ?? "nepoznata")}<br>
            <b>ID prijave:</b> {E(id)}</p>
            <p><b>Što ne izgleda točno:</b><br>{E(z.Opis).Replace("\n", "<br>")}</p>
            <p><b>Očekivani iznos:</b> {E(z.OcekivaniIznos ?? "—")}</p>
            <p><b>Ulaz (bez podataka stranaka):</b></p>
            <pre>{E(z.UlazniPodaci?.ToJsonString(opcije) ?? "—")}</pre>
            <p><b>Rezultat:</b></p>
            <pre>{E(z.Rezultat?.ToJsonString(opcije) ?? "—")}</pre>
            """;
    }
}

/// <summary>
/// GET /api/admin/prijave-gresaka i PATCH /api/admin/prijave-gresaka/{id}.
/// Zaštićeno AdminGuardMiddlewareom po prefiksu /api/admin/.
/// </summary>
[ApiController]
[Route("api/admin/prijave-gresaka")]
[EnableRateLimiting("general")]
public class PrijaveGresakaAdminController : ControllerBase
{
    private readonly IPrijaveGresakaSpremiste _spremiste;

    public PrijaveGresakaAdminController(IPrijaveGresakaSpremiste spremiste) => _spremiste = spremiste;

    [HttpGet]
    public async Task<IActionResult> Popis([FromQuery] int limit = 100)
    {
        var json = await _spremiste.PopisAsync(limit);
        return json is null
            ? StatusCode(502, new { error = "Prijave nisu dohvaćene." })
            : Content(json, "application/json");
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> PromijeniStatus(string id, [FromBody] PrijavaGreskeStatusZahtjev zahtjev)
    {
        if (zahtjev.Status is null || !PrijavaGreskeStatus.Svi.Contains(zahtjev.Status))
            return BadRequest(new { error = "Nepoznat status." });

        return await _spremiste.PromijeniStatusAsync(id, zahtjev.Status)
            ? NoContent()
            : NotFound(new { error = "Prijava nije pronađena." });
    }
}
