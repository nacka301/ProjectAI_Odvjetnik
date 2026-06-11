using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Services;

namespace Backend.Services;

/// <summary>
/// Apstrakcija za PerplexityTestValidationService. Kreirano u svrhu Dependency Inversion principa.
/// </summary>
public interface IPerplexityTestValidationService
{
    bool IsConfigured { get; }
/// <summary>
    /// Validate a batch of test scenarios against real market data.
    /// Each scenario is sent to Perplexity individually for independent evaluation.
    /// </summary>
    Task<TestValidationReport> ValidateScenariosAsync(
        List<TestScenarioInput> scenarios, CancellationToken ct = default) ;
}
