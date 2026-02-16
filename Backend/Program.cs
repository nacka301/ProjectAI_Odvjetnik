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
                // Allow null/empty origin (file:// or tools like Postman)
                if (string.IsNullOrWhiteSpace(origin))
                    return true;

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

// V35: Multi-model OpenAI routing — Fast (extraction) + Heavy (reasoning)
var openAiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_APIKEY")
    ?? throw new InvalidOperationException(
        "OpenAI API key is not configured. Set OpenAI__ApiKey or OPENAI_API_KEY environment variable.");

var fastModel = builder.Configuration["OpenAI:FastModel"] ?? "gpt-4o-mini";
var heavyModel = builder.Configuration["OpenAI:HeavyModel"] ?? "gpt-4o";
var reasoningModel = builder.Configuration["OpenAI:ReasoningModel"] ?? "o1-preview";
builder.Services.AddSingleton(new ChatClientProvider(openAiKey, fastModel, heavyModel, reasoningModel));
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
builder.Services.AddHttpClient<EmailService>();
builder.Services.AddHttpClient<MarketResearchService>(client =>
{
    // V20d: Increased timeout — deep dive of 20+ listings can take 60-120s total
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<SupabaseService>();

var app = builder.Build();

// --- Middleware ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
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
