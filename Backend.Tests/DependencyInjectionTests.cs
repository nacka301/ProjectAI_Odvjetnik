using Backend.Extensions;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Backend.Tests;

/// <summary>
/// Regresija za commit c04aeeb ("Dodana SOLID Dependency Inversion sučelja"), koji je
/// uveo I*Service sučelja u konstruktore kontrolera, ali ih nije registrirao u DI-ju.
/// Posljedica: svaki pogođeni endpoint bacao je InvalidOperationException u runtimeu,
/// uključujući cijeli /api/troskovnik.
/// </summary>
public class DependencyInjectionTests
{
    /// <summary>
    /// Gradi kontejner istim registracijama koje koristi Program.cs, uz minimalne
    /// infrastrukturne ovisnosti (konfiguracija, logiranje, HttpClient factory).
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(new ChatClientProvider("test-key", "https://example.invalid/v1", "m", "m", "m"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://example.supabase.co",
                ["Supabase:ServiceKey"] = "test-key",
                ["Supabase:AnonKey"] = "test-key",
                ["OpenRouter:ApiKey"] = "test-key",
                ["Tavily:ApiKey"] = "test-key"
            })
            .Build());

        services.AddDomainServices();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = false
        });
    }

    /// <summary>
    /// Svaki parametar konstruktora svakog kontrolera mora se moći razriješiti.
    /// Ovo je test koji je prije popravka padao za 11 od 20 kontrolera.
    /// </summary>
    [Fact]
    public void SviKontroleri_ImajuRazrjesiveOvisnosti()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var kontroleri = typeof(TroskovnikService).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        Assert.NotEmpty(kontroleri);

        var greske = new List<string>();

        foreach (var kontroler in kontroleri)
        {
            foreach (var ctor in kontroler.GetConstructors())
            {
                foreach (var param in ctor.GetParameters())
                {
                    // ASP.NET sam ubacuje okvirne tipove — zanimaju nas samo naši servisi.
                    if (param.ParameterType.Assembly != typeof(TroskovnikService).Assembly)
                        continue;

                    try
                    {
                        if (scope.ServiceProvider.GetService(param.ParameterType) is null)
                            greske.Add($"{kontroler.Name} → {param.ParameterType.Name} nije registriran");
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Tip je registriran, ali mu neka tranzitivna ovisnost nedostaje.
                        greske.Add($"{kontroler.Name} → {param.ParameterType.Name}: {ex.Message}");
                    }
                }
            }
        }

        Assert.True(greske.Count == 0,
            "Neregistrirane ovisnosti kontrolera:\n  " + string.Join("\n  ", greske));
    }

    /// <summary>
    /// Izdvojeno za troškovnik jer je to modul koji se ovim zadatkom popravlja —
    /// želim da pad bude nedvosmislen ako netko ukloni baš ove registracije.
    /// </summary>
    [Theory]
    [InlineData(typeof(ITroskovnikService))]
    [InlineData(typeof(ITroskovnikDocumentService))]
    [InlineData(typeof(IClientCaseService))]
    [InlineData(typeof(ITroskovnikSpremiste))]
    public void TroskovnikOvisnosti_SuRegistrirane(Type sucelje)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService(sucelje));
    }
}
