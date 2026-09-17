using System.Text.Json.Nodes;
using Backend.Controllers;
using Backend.Middleware;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Prijava „Iznos mi ne izgleda točno" (plan pravne zaštite, dio D).
///
/// Tri stvari ne smiju pasti: podaci stranaka ne idu u bazu ni u mail, prijava
/// se sprema i kad mail ne ode, i tuđi troškovnik se ne može vezati.
/// </summary>
public class PrijaveGresakaTests
{
    private const string KorisnikId = "11111111-1111-1111-1111-111111111111";
    private const string UredId = "ured-1";

    // ── Čišćenje podataka stranaka ────────────────────────────────

    [Fact]
    public void Ciscenje_UklanjaPodatkeStranakaNaSvakojDubini()
    {
        var ulaz = JsonNode.Parse("""
            {
              "vrijednost_spora": 50000,
              "stavke": [{ "item_id": "par_tuzba", "kolicina": 1, "opis": "Sastav tužbe" }],
              "odvjetnik": { "naziv": "Ana Anić", "oib": "01234567890" },
              "stranke": { "zastupana_stranka": { "naziv": "Ivan Horvat" } },
              "klijent": { "ime": "Ivan Horvat" },
              "protustranka": { "ime": "Marko Marić" },
              "protustranka_polozaj": "Tuženik",
              "predmet": { "sud": "OS Daruvar", "poslovni_broj": "P-1/24" },
              "napomena": "Ivan Horvat traži…",
              "client_id": "c-1",
              "rezultat": { "odvjetnik_info": { "naziv": "Ana Anić" }, "ukupno": 250 }
            }
            """);

        var cisto = PrijavaGreskeCiscenje.Ocisti(ulaz)!.ToJsonString();

        Assert.DoesNotContain("Ana Anić", cisto);
        Assert.DoesNotContain("Ivan Horvat", cisto);
        Assert.DoesNotContain("Marko Marić", cisto);
        Assert.DoesNotContain("01234567890", cisto);
        Assert.DoesNotContain("P-1/24", cisto);
        Assert.DoesNotContain("c-1", cisto);
        Assert.DoesNotContain("Tuženik", cisto);
        // Ono što treba za ponavljanje izračuna ostaje.
        Assert.Contains("\"vrijednost_spora\":50000", cisto);
        Assert.Contains("par_tuzba", cisto);
        Assert.Contains("\"ukupno\":250", cisto);
    }

    [Fact]
    public void Ciscenje_NeMijenjaUlaz()
    {
        var ulaz = JsonNode.Parse("""{ "odvjetnik": { "naziv": "X" } }""")!;
        PrijavaGreskeCiscenje.Ocisti(ulaz);
        Assert.Contains("odvjetnik", ulaz.ToJsonString());
    }

    // ── Mail ──────────────────────────────────────────────────────

    [Fact]
    public void Mail_EscapeaSveStoJeKorisnikUpisao()
    {
        var zapis = new PrijavaGreskeZapis
        {
            LawFirmId = UredId,
            Opis = "<script>alert(1)</script> iznos je kriv",
            OcekivaniIznos = "<b>300</b>",
        };

        var html = PrijavaGreskeMail.Html("id-1", zapis, "a@b.hr", "https://pravotek.com");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>300</b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    // ── Resend: placeholder nije ključ ────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("YOUR_RESEND_API_KEY", null)]
    [InlineData(" re_abc ", "re_abc")]
    public void Resend_PraviKljuc(string? ulaz, string? ocekivano)
        => Assert.Equal(ocekivano, ResendEmailService.PraviKljuc(ulaz));

    // ── Kontroler ─────────────────────────────────────────────────

    private static PrijaveGresakaController Kontroler(
        FakeSpremiste spremiste, FakeEmail email, string? adminEmail = "admin@pravotek.hr",
        string? korisnikId = KorisnikId, string? uredId = UredId)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Email"] = adminEmail })
            .Build();

        // AdminIdentity čita ADMIN_EMAIL prije konfiguracije; u testu ga nema.
        var kontroler = new PrijaveGresakaController(
            spremiste, email, new AdminIdentity(config), NullLogger<PrijaveGresakaController>.Instance);

        var ctx = new DefaultHttpContext();
        if (korisnikId is not null) ctx.Items["AuthUserId"] = korisnikId;
        if (uredId is not null) ctx.Items["AuthTenantId"] = uredId;
        ctx.Items["AuthEmail"] = "odvjetnik@ured.hr";
        kontroler.ControllerContext = new ControllerContext { HttpContext = ctx };
        return kontroler;
    }

    private static PrijavaGreskeZahtjev Zahtjev(string opis = "Pristojba mi ne izgleda točna") => new()
    {
        Opis = opis,
        OcekivaniIznos = "310,00 €",
        UlazniPodaci = JsonNode.Parse("""{ "vrijednost_spora": 50000, "odvjetnik": { "naziv": "Ana Anić" } }"""),
        Rezultat = JsonNode.Parse("""{ "sveukupni_trosak": 273.23, "klijent": { "ime": "Ivan Horvat" } }"""),
    };

    [Fact]
    public async Task Prijava_SpremaOcisceniZapisIzTokenaIPosaljeMail()
    {
        var spremiste = new FakeSpremiste();
        var email = new FakeEmail();

        var odgovor = await Kontroler(spremiste, email).Prijavi(Zahtjev());

        Assert.IsType<OkObjectResult>(odgovor);
        var zapis = spremiste.Spremljeno!;
        Assert.Equal(KorisnikId, zapis.UserId);
        Assert.Equal(UredId, zapis.LawFirmId);
        Assert.DoesNotContain("Ana Anić", zapis.UlazniPodaci!.ToJsonString());
        Assert.DoesNotContain("Ivan Horvat", zapis.Rezultat!.ToJsonString());

        Assert.Equal("admin@pravotek.hr", email.Primatelj);
        Assert.DoesNotContain("Ana Anić", email.Tijelo);
        Assert.DoesNotContain("Ivan Horvat", email.Tijelo);
        Assert.True(spremiste.EmailOznacen);
    }

    [Fact]
    public async Task Prijava_MailNijeOtisao_PrijavaIpakSpremljenaBezOznake()
    {
        var spremiste = new FakeSpremiste();
        var email = new FakeEmail { Uspjeh = false };

        var odgovor = await Kontroler(spremiste, email).Prijavi(Zahtjev());

        Assert.IsType<OkObjectResult>(odgovor);
        Assert.NotNull(spremiste.Spremljeno);
        Assert.False(spremiste.EmailOznacen);
    }

    [Fact]
    public async Task Prijava_TudjiTroskovnik_404IBezUpisa()
    {
        var spremiste = new FakeSpremiste { TroskovnikDostupan = false };
        var zahtjev = Zahtjev();
        zahtjev.TroskovnikId = "22222222-2222-2222-2222-222222222222";

        var odgovor = await Kontroler(spremiste, new FakeEmail()).Prijavi(zahtjev);

        Assert.IsType<NotFoundObjectResult>(odgovor);
        Assert.Null(spremiste.Spremljeno);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kratko")]
    public async Task Prijava_PrekratakOpis_400(string opis)
    {
        var spremiste = new FakeSpremiste();
        var odgovor = await Kontroler(spremiste, new FakeEmail()).Prijavi(Zahtjev(opis));
        Assert.IsType<BadRequestObjectResult>(odgovor);
        Assert.Null(spremiste.Spremljeno);
    }

    [Fact]
    public async Task Prijava_BezPrijave_401()
    {
        var odgovor = await Kontroler(new FakeSpremiste(), new FakeEmail(), korisnikId: null).Prijavi(Zahtjev());
        Assert.IsType<UnauthorizedObjectResult>(odgovor);
    }

    [Fact]
    public async Task Prijava_BazaOdbilaUpis_500IBezMaila()
    {
        var spremiste = new FakeSpremiste { IdNakonSpremanja = null };
        var email = new FakeEmail();

        var odgovor = Assert.IsType<ObjectResult>(await Kontroler(spremiste, email).Prijavi(Zahtjev()));

        Assert.Equal(500, odgovor.StatusCode);
        Assert.Null(email.Primatelj);
    }

    [Fact]
    public async Task Admin_NepoznatStatus_400()
    {
        var kontroler = new PrijaveGresakaAdminController(new FakeSpremiste());
        var odgovor = await kontroler.PromijeniStatus("33333333-3333-3333-3333-333333333333",
            new PrijavaGreskeStatusZahtjev { Status = "obrisano" });
        Assert.IsType<BadRequestObjectResult>(odgovor);
    }

    private sealed class FakeSpremiste : IPrijaveGresakaSpremiste
    {
        public bool TroskovnikDostupan { get; init; } = true;
        public string? IdNakonSpremanja { get; init; } = "prijava-1";
        public PrijavaGreskeZapis? Spremljeno { get; private set; }
        public bool EmailOznacen { get; private set; }

        public Task<bool> TroskovnikJeDostupanAsync(string troskovnikId) => Task.FromResult(TroskovnikDostupan);

        public Task<string?> SpremiAsync(PrijavaGreskeZapis zapis)
        {
            if (IdNakonSpremanja is not null) Spremljeno = zapis;
            return Task.FromResult(IdNakonSpremanja);
        }

        public Task OznaciEmailPoslanAsync(string id)
        {
            EmailOznacen = true;
            return Task.CompletedTask;
        }

        public Task<string?> PopisAsync(int limit) => Task.FromResult<string?>("[]");
        public Task<bool> PromijeniStatusAsync(string id, string status) => Task.FromResult(true);
    }

    private sealed class FakeEmail : IResendEmailService
    {
        public bool Uspjeh { get; init; } = true;
        public string? Primatelj { get; private set; }
        public string Tijelo { get; private set; } = "";

        public Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody,
            byte[]? attachment = null, string? attachmentName = null)
        {
            Primatelj = toEmail;
            Tijelo = htmlBody;
            return Task.FromResult(Uspjeh);
        }
    }
}
