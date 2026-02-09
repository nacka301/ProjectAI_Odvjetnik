using System.Text.Json;
using Backend.Services;
using Backend.Services.V6;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Disable file watchers in production (causes segfault on Render/Docker)
    ContentRootPath = AppContext.BaseDirectory
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
    });

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
                // Always allow configured origins
                if (allowedOrigins.Any(ao => ao.Equals(origin, StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Allow all Vercel preview deploys for the same project
                var uri = new Uri(origin);
                if (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
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

// OpenAI ChatClient (reads key from config or env var)
var openAiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OpenAI__ApiKey")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_APIKEY")
    ?? throw new InvalidOperationException(
        "OpenAI API key is not configured. Set OpenAI__ApiKey or OPENAI_API_KEY environment variable.");

builder.Services.AddSingleton(new ChatClient("gpt-4o", openAiKey));
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ValuationService>();
builder.Services.AddScoped<ValuationEngine>();
builder.Services.AddHttpClient<MarketResearchService>();
builder.Services.AddHttpClient<SupabaseService>();

// ── V6 Pipeline Services ──
builder.Services.AddScoped<ISimilarityEngine, SimilarityEngine>();
builder.Services.AddHttpClient<ICandidateGenerator, CandidateGenerator>();
builder.Services.AddHttpClient<IDeepFetcher, DeepFetcher>();
builder.Services.AddHttpClient<INlpExtractor, NlpExtractor>();
builder.Services.AddScoped<IValuationPipeline, V6ValuationPipeline>();
builder.Services.AddScoped<IPlaywrightScraper, PlaywrightScraper>();

var app = builder.Build();

// --- Middleware ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

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
