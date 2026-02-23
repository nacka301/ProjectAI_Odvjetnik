using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.Models;
using Backend.Services;
using Backend.Validators;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Disable file watchers in production (causes segfault on Render/Docker)
    ContentRootPath = AppContext.BaseDirectory
});

// V20d: Configure Kestrel for long-running SSE connections (deep dive can take 60-120s)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    // Disable minimum data rate — SSE streams have long pauses during deep dive
    options.Limits.MinResponseDataRate = null;
    options.Limits.MinRequestBodyDataRate = null;
});

// Disable config reload — Render doesn't support file watchers
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// --- Services ---

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// FluentValidation — auto-register all validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<PropertyInputBaseValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — allow the Next.js frontend
// Supports: FRONTEND_URL env var, multiple comma-separated URLs, and Vercel preview deploys
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
var allowedOrigins = frontendUrl
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(u => u.TrimEnd('/'))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                // Reject null/empty origins in production (prevents CSRF from curl/scripts)
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                // Always allow configured origins
                if (allowedOrigins.Any(ao => ao.Equals(origin, StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Try parse — reject malformed origins gracefully
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                // Allow Vercel preview deploys ONLY for this project
                if (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                    && (uri.Host.Contains("projectai", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("odvjetnik", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("nacka", StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Allow localhost for development
                if (uri.Host is "localhost" or "127.0.0.1")
                    return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Rate Limiting — prevent API credit drain ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Valuation endpoints: 5 requests per 10 minutes per IP
    options.AddFixedWindowLimiter("valuation", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;
    });

    // General API: 30 requests per minute per IP
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Previše zahtjeva. Pokušajte ponovno za nekoliko minuta.\"}",
            cancellationToken);
    };
});

// V51: OpenRouter gateway — single API key for all AI providers
// Priority: env var > appsettings.json (env vars override placeholder values)
var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
    ?? Environment.GetEnvironmentVariable("OpenRouter__ApiKey")
    ?? builder.Configuration["OpenRouter:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenRouter API key is not configured. Set OPENROUTER_API_KEY environment variable.");

// Guard against placeholder values left in appsettings.json
if (openRouterKey.StartsWith("YOUR_") || string.IsNullOrWhiteSpace(openRouterKey))
    throw new InvalidOperationException(
        "OpenRouter API key is a placeholder. Set OPENROUTER_API_KEY environment variable.");

var baseUrl = builder.Configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";
var fastModel = builder.Configuration["OpenRouter:FastModel"] ?? "openai/gpt-4o-mini";
var heavyModel = builder.Configuration["OpenRouter:HeavyModel"] ?? "deepseek/deepseek-chat-v3-0324";
var reasoningModel = builder.Configuration["OpenRouter:ReasoningModel"] ?? "deepseek/deepseek-chat-v3-0324";
builder.Services.AddSingleton(new ChatClientProvider(openRouterKey, baseUrl, fastModel, heavyModel, reasoningModel));
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ValuationService>();
builder.Services.AddScoped<TextAnalysisService>();
builder.Services.AddScoped<ValuationAgentService>();
builder.Services.AddScoped<ContractAnalysisService>();
builder.Services.AddScoped<ChronologyService>();
builder.Services.AddScoped<ValuationEngine>();
builder.Services.AddScoped<ZkParsingService>();
builder.Services.AddScoped<LegalIntelligenceService>();
builder.Services.AddSingleton<NormValidityService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<TranslationService>();
builder.Services.AddHttpClient<EmailService>();
builder.Services.AddHttpClient<EmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<LawRAGService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddHttpClient<MarketResearchService>(client =>
{
    // V20d: Increased timeout — deep dive of 20+ listings can take 60-120s total
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<SupabaseService>();
builder.Services.AddHttpClient<TwoFactorService>();
builder.Services.AddHttpClient<ClientCaseService>();
builder.Services.AddHttpClient<PerplexityAuditService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<PerplexityTestValidationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

// --- Middleware ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

// ── API Key Authentication Middleware (optional, activate via API_KEY env var) ──
var apiKey = Environment.GetEnvironmentVariable("API_KEY");
if (!string.IsNullOrWhiteSpace(apiKey))
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for health check and CORS preflight
        if (path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Method == "OPTIONS")
        {
            await next();
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedKey.ToString()),
                Encoding.UTF8.GetBytes(apiKey)))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Nevaljan ili nedostaje API ključ.\"}");
            return;
        }

        await next();
    });
}

app.UseRateLimiter();

// Only redirect to HTTPS in development; Render handles TLS at the proxy level
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

// In production (Docker/Render), bind to PORT env var.
// In development, launchSettings.json controls the URL.
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}
