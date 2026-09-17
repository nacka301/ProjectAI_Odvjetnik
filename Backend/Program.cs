using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Backend.Extensions;
using Backend.Middleware;
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

Console.WriteLine($"[CORS] Allowed origins: {string.Join(", ", allowedOrigins)}");
Console.WriteLine($"[CORS] Vercel preview deploys also allowed (projectai|odvjetnik|nacka)");

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
            .AllowAnyMethod()
            .AllowCredentials();
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

    // Auth endpoints: 5 requests per 10 minutes per IP (anti-brute-force)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;
    });

    // Prijave grešaka u izračunu: 5 na sat PO KORISNIKU. Particionirano, za
    // razliku od gornjih politika koje dijele jedan prozor za sve — inače bi
    // pet prijava jednog ureda zatvorilo gumb svima. Ključ je user id iz
    // SupabaseJwtMiddlewarea, koji se izvršava prije UseRateLimiter.
    options.AddPolicy("prijave", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Items["AuthUserId"] as string
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "nepoznat",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));

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

// SmtpSettings je uklonjen zajedno s AuthEmailService-om — jedini čitatelj.
// Transakcijska pošta ide kroz ResendEmailService, koji svoje postavke čita iz
// sekcije "Resend".

// ── Svi domenski servisi ──
// Registracije žive u Backend.Extensions.DomainServiceCollectionExtensions kako bi ih
// Backend.Tests.DependencyInjectionTests mogao provjeriti bez pokretanja hosta.
builder.Services.AddDomainServices();

var app = builder.Build();

// --- Middleware ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

// ── Supabase JWT Authentication Middleware ──
// Verifies Bearer token, extracts user_id/email from JWT claims.
// Resolves TenantId via ITenantResolver (cached).
// Fails closed: anything outside its public allowlist is 401 without a valid token.
app.UseMiddleware<SupabaseJwtMiddleware>();

// ── Admin Guard Middleware ──
// Blocks non-admin users from /api/rag/, /api/admin/, etc., and from modules
// still in development (see ModuleRegistry).
// MUST run after SupabaseJwtMiddleware (needs AuthEmail in HttpContext.Items)
// and before MapControllers, so a refused call never spends a credit.
app.UseMiddleware<AdminGuardMiddleware>();

// ── Subscription Gate Middleware ──
// Refuses the finished modules (Troskovnik, Stranke) once an office's trial or
// subscription has run out. MUST run after AdminGuardMiddleware so a module that
// is merely unfinished still answers MODULE_IN_DEVELOPMENT rather than a demand
// for payment, and before MapControllers so a refused call costs nothing.
app.UseMiddleware<SubscriptionGateMiddleware>();

// ── Audit Logging Middleware ──
// Logs: userId, tenantId, endpoint, HTTP method, status code, duration.
// NEVER logs request/response bodies (GDPR + attorney-client privilege).
app.UseMiddleware<AuditLoggingMiddleware>();

// NOTE: the former X-API-Key middleware has been removed. The only client is the
// browser, so the key had to ship as NEXT_PUBLIC_API_KEY — i.e. inside the JS
// bundle — which made it a shared public secret rather than a control. Requests
// are authenticated by SupabaseJwtMiddleware, which fails closed.

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

