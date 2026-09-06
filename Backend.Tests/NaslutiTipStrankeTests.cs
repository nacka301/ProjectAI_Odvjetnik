using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Prijedlog „fizička/pravna" iz naziva stranke pri automatskom otvaranju iz
/// troškovnika. Prije ovoga svaka je auto-otvorena stranka bila „fizicka",
/// pa su d.o.o./d.d. klijenti završavali krivo označeni.
/// </summary>
public class NaslutiTipStrankeTests
{
    [Theory]
    [InlineData("Adriatic Shipping & Logistics d.d.")]
    [InlineData("Elektro Instalacije Zagreb d.o.o.")]
    [InlineData("Zagreb Trade d. o. o.")]
    [InlineData("Mini Servis j.d.o.o.")]
    [InlineData("Poljoprivredna zadruga Slavonija")]
    [InlineData("Udruga oštećenih ulagača Pravda")]
    [InlineData("Konzorcij Autocesta Dalmatina d.o.o.")]
    [InlineData("Hrvatska odvjetnička komora")]
    [InlineData("Zaklada za poticanje partnerstva")]
    [InlineData("Republika Hrvatska")]
    public void PrepoznaPravnuOsobu(string naziv)
        => Assert.Equal("pravna", ClientCaseService.NaslutiTip(naziv));

    [Theory]
    [InlineData("Ivan Horvat")]
    [InlineData("Marija Kovač")]
    [InlineData("Ranko Vuković")]
    [InlineData("Ana-Marija Perić Novak")]
    [InlineData("")]
    [InlineData(null)]
    public void OstajeFizickaKadNemaPouzdaneOznake(string? naziv)
        => Assert.Equal("fizicka", ClientCaseService.NaslutiTip(naziv));

    [Theory]
    [InlineData("fizicka", "fizicka")]
    [InlineData("pravna", "pravna")]
    [InlineData("PRAVNA", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("nešto", null)]
    public void NormalizirajTip_PropustaSamoPoznateVrijednosti(string? ulaz, string? ocekivano)
        => Assert.Equal(ocekivano, ClientCaseService.NormalizirajTip(ulaz));
}
