using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Health check endpoint for monitoring and diagnostics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _config;

    public HealthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// GET /api/health — verifies the backend is alive and env vars are set.
    /// </summary>
    [HttpGet]
    public IActionResult Health()
    {
        var openAiConfigured = !string.IsNullOrEmpty(
            _config["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_APIKEY"));

        var tavilyConfigured = !string.IsNullOrEmpty(
            _config["Tavily:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Tavily__ApiKey")
            ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY")
            ?? Environment.GetEnvironmentVariable("TAVILY_APIKEY"));

        var supabaseConfigured = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("SUPABASE_URL")
            ?? _config["Supabase:Url"]);

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown",
            services = new
            {
                openai = openAiConfigured ? "configured" : "MISSING — set OPENAI_API_KEY",
                tavily = tavilyConfigured ? "configured" : "MISSING — set TAVILY_API_KEY",
                supabase = supabaseConfigured ? "configured" : "not configured (optional)"
            }
        });
    }
}
